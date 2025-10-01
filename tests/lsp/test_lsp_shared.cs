using System;
using System.IO;
using System.IO.Pipes;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;
using bhl;
using Serilog;
using OmniSharp.Extensions.LanguageServer.Server;
using bhl.lsp;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

public class TestLSPShared : BHL_TestBase
{
  public sealed class TestLSPHost : IDisposable
  {
    private readonly Task<LanguageServer> _server;
    private readonly CancellationTokenSource _cts;

    public Task<LanguageServer> ServerTask => _server;

    // client-facing ends
    private readonly Stream _clientInput;   // what we write requests into
    private readonly Stream _clientOutput;  // what we read responses from

    private static readonly JsonSerializerSettings _jsonOptions = new()
    {
      ContractResolver = new CamelCasePropertyNamesContractResolver(),
      NullValueHandling = NullValueHandling.Ignore
    };

    private int _nextId = 1;

    public class LspResponse
    {
      public int? Id;
      public string Method;
      public JToken Params;
      public JToken Result;
      public JToken Error;
      public string Json;

      public static LspResponse Parse(string json)
      {
        var node = JObject.Parse(json);
        var msg = new LspResponse();

        msg.Json = json;

        if(node["id"] != null)
          msg.Id = node["id"].Value<int>();

        if(node["method"] != null)
          msg.Method = node["method"].Value<string>();

        if(node["params"] != null)
          msg.Params = node["params"];

        if(node["result"] != null)
          msg.Result = node["result"];

        if(node["error"] != null)
          msg.Error = node["error"];

        return msg;
      }
    }

    public static TestLSPHost NewServer(
      Workspace workspace,
      ILogger logger = null,
      CancellationToken ct = default
    )
    {
      var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

      logger ??= new LoggerConfiguration().CreateLogger();

      // Pipes for client <-> server communication
      var clientToServer = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
      var serverInput = new AnonymousPipeClientStream(PipeDirection.In, clientToServer.GetClientHandleAsString());

      var serverToClient = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
      var clientOutput = new AnonymousPipeClientStream(PipeDirection.In, serverToClient.GetClientHandleAsString());
      var serverTask = ServerFactory.CreateAsync(
          logger,
          input: serverInput,
          output: serverToClient,
          workspace: workspace,
          ct: cts.Token
        );

      return new TestLSPHost(serverTask, cts, clientToServer, clientOutput);
    }

    private TestLSPHost(
      Task<LanguageServer> server,
      CancellationTokenSource cts,
      Stream clientInput,
      Stream clientOutput
    )
    {
      _server = server;
      _cts = cts;
      _clientInput = clientInput;
      _clientOutput = clientOutput;
    }

    public async Task SendAsync(string json, CancellationToken ct = default)
    {
      Console.WriteLine("SEND: " + json);

      var bytes = Encoding.UTF8.GetBytes(json);
      var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");

      await _clientInput.WriteAsync(header, 0, header.Length, ct);
      await _clientInput.WriteAsync(bytes, 0, bytes.Length, ct);
      await _clientInput.FlushAsync(ct);
    }

    public async Task<LspResponse> RecvMsgAsync(CancellationToken ct = default)
    {
      await foreach(var msg in RecvMsgsAsync(ct))
      {
        return msg;
      }

      throw new Exception("No message received");
    }

    public async IAsyncEnumerable<LspResponse> RecvMsgsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
      using var reader = new StreamReader(_clientOutput, Encoding.UTF8, leaveOpen: true);

      while(!ct.IsCancellationRequested)
      {
        // ---- Read headers ----
        string line;
        int contentLength = 0;

        while(!string.IsNullOrEmpty(line = await reader.ReadLineAsync().WaitAsync(ct)))
        {
          if(line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            contentLength = int.Parse(line.Substring("Content-Length:".Length).Trim());
        }

        if(contentLength == 0)
          continue;

        // ---- Read body ----
        char[] buffer = new char[contentLength];
        int read = 0;
        while(read < contentLength)
        {
          int r = await reader.ReadAsync(buffer, read, contentLength - read).WaitAsync(ct);
          if (r == 0) break;
          read += r;
        }

        var json = new string(buffer, 0, read);
        Console.WriteLine("RECV: " + json);
        yield return LspResponse.Parse(json);
      }
    }

    public async Task<TResult> SendRequestAsync<TParams, TResult>(
      string method,
      TParams @params,
      CancellationToken ct = default) where TResult : class
    {
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(
        new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token,
        ct);

      var msg = await SendRequestAsync<TParams>(method, @params, cts.Token);

      if(msg.Result != null)
        return msg.Result.ToObject<TResult>(JsonSerializer.CreateDefault(_jsonOptions));

      if(msg.Error != null)
        throw new InvalidOperationException($"LSP error: {msg.Error}");

      throw new InvalidOperationException("No response received");
    }

    public async Task<LspResponse> SendRequestAsync<TParams>(
      string method,
      TParams @params,
      CancellationToken ct = default)
    {
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(
        new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token,
        ct);

      var id = Interlocked.Increment(ref _nextId);

      var request = new
      {
        jsonrpc = "2.0",
        id,
        method,
        @params
      };

      var json = JsonConvert.SerializeObject(request, _jsonOptions);
      await SendAsync(json, cts.Token);

      await foreach(var msg in RecvMsgsAsync(cts.Token))
      {
        if(msg.Id != id)
          throw new InvalidOperationException($"Unexpected response id: {msg.Id}, expected {id}");
        return msg;
      }

      throw new InvalidOperationException("No response received");
    }

    public async Task<TResult> RecvEventAsync<TResult>(
      string method,
      CancellationToken ct = default) where TResult : class
    {
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(
        new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token,
        ct);

      LspResponse msg = null;
      await foreach(var tmp in RecvMsgsAsync(cts.Token))
      {
        if(tmp.Method != method)
          throw new InvalidOperationException($"Unexpected message method: {msg.Method}, expected {method}");

        msg = tmp;
        break;
      }

      if(msg.Params != null)
        return msg.Params.ToObject<TResult>(JsonSerializer.CreateDefault(_jsonOptions));

      if(msg.Error != null)
        throw new InvalidOperationException($"LSP error: {msg.Error}");

      throw new InvalidOperationException("No response received");
    }

    public async Task SendNotificationAsync<TParams>(
      string method,
      TParams @params,
      CancellationToken ct = default)
    {
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(
        new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token,
        ct);

      var notification = new
      {
        jsonrpc = "2.0",
        method,
        @params
      };

      var json = JsonConvert.SerializeObject(notification, _jsonOptions);
      await SendAsync(json, cts.Token);
    }

    public void Dispose()
    {
      _cts.Cancel();
      _clientInput.Dispose();
      _clientOutput.Dispose();
    }
  }

  public static TestLSPHost NewTestServer(
    Workspace workspace,
    ILogger logger = null,
    CancellationToken ct = default
    )
  {
    return TestLSPHost.NewServer(workspace, logger, ct);
  }

  //public static string GoToDefinitionReq(bhl.lsp.proto.Uri uri, string needle)
  //{
  //  var pos = Pos(File.ReadAllText(uri.path), needle);
  //  return "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/definition\", \"params\":" +
  //         "{\"textDocument\": {\"uri\": \"" + uri + "\"}, \"position\": " + AsJson(pos) + "}}";
  //}

  //public static string GoToDefinitionRsp(bhl.lsp.proto.Uri uri, string needle, int end_line_offset = 0,
  //  int end_column_offset = 0)
  //{
  //  var start = Pos(File.ReadAllText(uri.path), needle);
  //  var end = new bhl.SourcePos(start.line + end_line_offset, start.column + end_column_offset);
  //  return "{\"id\":1,\"result\":{\"uri\":\"" + uri + "\",\"range\":{\"start\":" +
  //         AsJson(start) + ",\"end\":" + AsJson(end) + "}},\"jsonrpc\":\"2.0\"}";
  //}

  public static ReferenceParams MakeFindReferencesReq(DocumentUri uri, string needle)
  {
    var pos = FindPos(File.ReadAllText(uri.Path), needle);

    var result = new ReferenceParams()
    {
      TextDocument = uri,
      Position = pos.ToPosition(),
    };
    return result;
  }

  public struct UriNeedle
  {
    public DocumentUri uri;
    public string needle;
    public int end_line_offset;
    public int end_column_offset;

    public UriNeedle(DocumentUri uri, string needle, int end_line_offset = 0, int end_column_offset = 0)
    {
      this.uri = uri;
      this.needle = needle;
      this.end_line_offset = end_line_offset;
      this.end_column_offset = end_column_offset;
    }
  }

  public static LocationContainer MakeFindReferencesRsp(params UriNeedle[] uns)
  {
    var locations = new List<Location>();
    foreach(var un in uns)
    {
      var start = FindPos(File.ReadAllText(un.uri.Path), un.needle);
      var end = new bhl.SourcePos(start.line + un.end_line_offset, start.column + un.end_column_offset);
      var location = new Location()
      {
        Uri = un.uri,
        Range = new Range() { Start = start.ToPosition(), End = end.ToPosition() }
      };
      locations.Add(location);
    }
    return locations;
  }

  //public static string HoverReq(bhl.lsp.proto.Uri uri, string needle)
  //{
  //  var pos = Pos(File.ReadAllText(uri.path), needle);
  //  return "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/hover\", \"params\":" +
  //         "{\"textDocument\": {\"uri\": \"" + uri + "\"}, \"position\": " + AsJson(pos) + "}}";
  //}

  //public static string SignatureHelpReq(bhl.lsp.proto.Uri uri, string needle)
  //{
  //  var pos = Pos(File.ReadAllText(uri.path), needle);
  //  return "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/signatureHelp\", \"params\":" +
  //         "{\"textDocument\": {\"uri\": \"" + uri + "\"}, \"position\": " + AsJson(pos) + "}}";
  //}

  public static void CleanTestFiles()
  {
    string dir = GetTestDirPath();
    if(Directory.Exists(dir))
      Directory.Delete(dir, true /*recursive*/);
  }

  public static bhl.ProjectConf GetTestProjConf()
  {
    var conf = new bhl.ProjectConf();
    conf.src_dirs.Add(GetTestDirPath());
    conf.inc_path = GetTestIncPath();
    return conf;
  }

  public static bhl.IncludePath GetTestIncPath()
  {
    var inc_path = new bhl.IncludePath();
    inc_path.Add(GetTestDirPath());
    return inc_path;
  }

  public static string GetTestDirPath()
  {
    string self_bin = System.Reflection.Assembly.GetExecutingAssembly().Location;
    return Path.GetDirectoryName(self_bin) + "/tmp/bhlsp";
  }

  public static DocumentUri MakeTestDocument(string path, string text, List<string> files = null)
  {
    string full_path = bhl.BuildUtils.NormalizeFilePath(GetTestDirPath() + "/" + path);
    Directory.CreateDirectory(Path.GetDirectoryName(full_path));
    File.WriteAllText(full_path, text);
    if(files != null)
      files.Add(full_path);
    var uri = DocumentUri.Parse("file://" + full_path);
    return uri;
  }

  public string MakeTestProjConf()
  {
    var conf = new bhl.ProjectConf();
    conf.inc_dirs.Add(GetTestDirPath());
    conf.src_dirs.Add(GetTestDirPath());
    string path = GetTestDirPath() + "/bhl.proj";
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    ProjectConf.WriteToFile(conf, path);
    return path;
  }

  public static bhl.SourcePos FindPos(string code, string needle)
  {
    int idx = code.IndexOf(needle);
    if(idx == -1)
      throw new Exception("Needle not found: " + needle);
    var indexer = new CodeIndex();
    indexer.Update(code);
    var pos = indexer.GetIndexPosition(idx);
    if(pos.line == -1 && pos.column == -1)
      throw new Exception("Needle not mapped position: " + needle);
    return pos;
  }

  public static string AsJson(bhl.SourcePos pos)
  {
    return "{\"line\":" + pos.line + ",\"character\":" + pos.column + "}";
  }

  public static async Task SendInit(TestLSPHost srv)
  {
    var result = await srv.SendRequestAsync<InitializeParams, InitializeResult>(
      "initialize",
      new ()
      {
        Capabilities = new (),
        ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
        RootPath = GetTestDirPath()
      }
    );
  }

  public static int CountErrors(Workspace ws)
  {
    int count = 0;
    foreach (var kv in ws.GetCompileErrors())
      count += kv.Value.Count;
    return count;
  }
}
