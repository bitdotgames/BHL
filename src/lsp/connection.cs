using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace bhl.lsp {

public interface IConnection
{
  Task<string> Read(CancellationToken ct);
  Task Write(string json, CancellationToken ct);
}

public class ConnectionStdIO : IConnection
{
  const int SeparatorLen = 2;
  
  private readonly Logger logger;
  private readonly Stream input;
  private readonly Stream output;

  private byte[] buffer = new byte[1024];
  private int buffer_available_len = 0;
  
  public ConnectionStdIO(Logger logger, Stream output, Stream input)
  {
    this.logger = logger;

    this.input = input;
    this.output = output;
  }

  public async Task<string> Read(CancellationToken ct)
  {
    int content_len = 0;

    var header_bytes = await ReadToSeparator(ct);
    
    while(header_bytes.Length != 0)
    {
      logger.Log(1, "HEADER BYTES: " + header_bytes.Length);
      
      string header_line = Encoding.ASCII.GetString(header_bytes);
      logger.Log(1, "HEADER LINE: '" + header_line + "'");
      int position = header_line.IndexOf(": ", StringComparison.Ordinal);
      if(position >= 0)
      {
        string key = header_line.Substring(0, position);
        string value = header_line.Substring(position + 2);
        
        logger.Log(1, "HEADER: '" + key  + "' '" + value + "'");
        
        if(string.Equals(key, "Content-Length", StringComparison.Ordinal))
          int.TryParse(value, out content_len);
      }
      
      header_bytes = await ReadToSeparator(ct);
    }
    
    if(content_len == 0)
      return string.Empty;
    
    var bytes = await ReadBytes(content_len, ct);
    return Encoding.UTF8.GetString(bytes);
  }

  async Task<byte[]> ReadToSeparator(CancellationToken ct)
  {
    while(true)
    {
      logger.Log(1, "BUF CAP " + buffer.Length + " AVAIL " + buffer_available_len);
      
      if(buffer_available_len > 0)
      {
        int separator_pos = FindSeparatorPos(new Span<byte>(buffer, 0, buffer_available_len));
        if(separator_pos != -1)
        {
          var result = new byte[separator_pos];
          Buffer.BlockCopy(buffer, 0, result, 0, result.Length);
          
          int buffer_leftover_pos = separator_pos + SeparatorLen;
          int buffer_leftover = buffer_available_len - buffer_leftover_pos;
          logger.Log(1, "SEPARATOR " + separator_pos + " LEFTOVER " + buffer_leftover + " PREV " + buffer_available_len + " RES " + result.Length);
          //let's copy leftover to the beginning
          Buffer.BlockCopy(buffer, buffer_leftover_pos, buffer, 0, buffer_leftover);
          buffer_available_len = buffer_leftover;
          return result;
        }
      }

      //let's double the buffer if it's not enough 
      if(buffer.Length - buffer_available_len == 0)
      {
        var temp = new byte[buffer.Length * 2];
        Buffer.BlockCopy(buffer, 0, temp, 0, buffer.Length);
        buffer = temp;
        logger.Log(1, "BUF INC " + buffer.Length);
      }

      logger.Log(1, "BUF TRY READ " + (buffer.Length - buffer_available_len));
      int read_len = await input.ReadAsync(buffer, buffer_available_len, buffer.Length - buffer_available_len, ct);
      logger.Log(1, "BUF DID READ " + read_len);
      if(read_len == 0)
        throw new EndOfStreamException();

      buffer_available_len += read_len;
    }
  }

  async Task<byte[]> ReadBytes(int total_len, CancellationToken ct)
  {
    var result = new byte[total_len];

    FillFromAvailableBuffer(ref total_len, result);
    
    while(total_len > 0)
    {
      int read_len = await input.ReadAsync(result, result.Length - total_len, total_len, ct);
      logger.Log(1, "BUF RLEN " + read_len + " OFFSET " + (result.Length - total_len) + " LEN " + total_len + " CAP " + result.Length);
      if (read_len == 0)
        throw new EndOfStreamException();

      total_len -= read_len;
    }

    return result;
  }

  void FillFromAvailableBuffer(ref int result_len, byte[] result)
  {
    if(buffer_available_len == 0)
      return;
    
    if(result_len <= buffer_available_len)
    {
      logger.Log(1, "AVALABLE FULL " + result_len + " VS " + buffer_available_len);
      //let's copy from the buffer to result
      Buffer.BlockCopy(buffer, 0, result, 0, result_len);
      //let's copy leftover to the beginning
      Buffer.BlockCopy(buffer, result_len, buffer, 0, buffer_available_len - result_len);
      buffer_available_len -= result_len;
      result_len = 0;
    }
    else
    {
      logger.Log(1, "AVALABLE PART " + result_len + " VS " + buffer_available_len);
      //let's copy from the buffer to result
      Buffer.BlockCopy(buffer, 0, result, 0, buffer_available_len);
      result_len -= buffer_available_len;
      buffer_available_len = 0;
    }
  }

  static int FindSeparatorPos(Span<byte> bytes)
  {
    for(int i = 0; i < bytes.Length - 1; ++i)
    {
      if(bytes[i] == 13/* \r */ && 
         bytes[i + 1] == 10/* \n */)
        return i;
    }

    return -1;
  }
  
  public async Task Write(string json, CancellationToken ct)
  {
    var utf8 = Encoding.UTF8.GetBytes(json);
    using(var writer = new StreamWriter(output, Encoding.ASCII, 1024, true))
    {
      writer.Write($"Content-Length: {utf8.Length}\r\n");
      writer.Write("\r\n");
      writer.Flush();
    }
      
    await output.WriteAsync(utf8, 0, utf8.Length, ct);
    await output.FlushAsync(ct);
  }
}

}
