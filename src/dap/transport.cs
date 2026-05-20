using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace bhl.dap
{

// Reads and writes DAP messages (JSON-RPC with Content-Length framing).
public class Transport
{
  readonly Stream _in;
  readonly Stream _out;
  readonly SemaphoreSlim _write_lock = new SemaphoreSlim(1, 1);
  int _seq = 1;

  public Transport(Stream input, Stream output)
  {
    _in = input;
    _out = output;
  }

  public async Task<JObject> ReadAsync(CancellationToken ct)
  {
    // parse Content-Length header
    int content_length = -1;
    while(true)
    {
      var header = await ReadLineAsync(ct);
      if(header == null)
        return null;
      if(header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
        content_length = int.Parse(header.Substring("Content-Length:".Length).Trim());
      else if(header == "")
        break;
    }

    if(content_length < 0)
      return null;

    var buf = new byte[content_length];
    int read = 0;
    while(read < content_length)
    {
      int n = await _in.ReadAsync(buf, read, content_length - read, ct);
      if(n == 0) return null;
      read += n;
    }

    var json = Encoding.UTF8.GetString(buf);
    return JObject.Parse(json);
  }

  public async Task SendResponseAsync(JObject request, bool success, JObject body = null)
  {
    var msg = new JObject
    {
      ["seq"]         = _seq++,
      ["type"]        = "response",
      ["request_seq"] = request["seq"],
      ["success"]     = success,
      ["command"]     = request["command"],
    };
    if(body != null)
      msg["body"] = body;
    await WriteAsync(msg);
  }

  public async Task SendEventAsync(string event_name, JObject body = null)
  {
    var msg = new JObject
    {
      ["seq"]   = _seq++,
      ["type"]  = "event",
      ["event"] = event_name,
    };
    if(body != null)
      msg["body"] = body;
    await WriteAsync(msg);
  }

  async Task WriteAsync(JObject msg)
  {
    var json = msg.ToString(Newtonsoft.Json.Formatting.None);
    var bytes = Encoding.UTF8.GetBytes(json);
    var header = Encoding.UTF8.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");

    await _write_lock.WaitAsync();
    try
    {
      await _out.WriteAsync(header, 0, header.Length);
      await _out.WriteAsync(bytes, 0, bytes.Length);
      await _out.FlushAsync();
    }
    finally
    {
      _write_lock.Release();
    }
  }

  async Task<string> ReadLineAsync(CancellationToken ct)
  {
    var sb = new StringBuilder();
    while(true)
    {
      var buf = new byte[1];
      int n = await _in.ReadAsync(buf, 0, 1, ct);
      if(n == 0) return null;
      char c = (char)buf[0];
      if(c == '\n') return sb.ToString().TrimEnd('\r');
      sb.Append(c);
    }
  }
}

}
