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
using Serilog;
using OmniSharp.Extensions.LanguageServer.Server;
using bhl.lsp;

public class TestLSPShared : BHL_TestBase
{
  public sealed class TestLSPHost : IDisposable
  {
    private readonly Task<LanguageServer> _server;
    private readonly CancellationTokenSource _cts;

    // client-facing ends
    private readonly Stream _clientInput;   // what we write requests into
    private readonly Stream _clientOutput;  // what we read responses from

    private static readonly JsonSerializerSettings _jsonOptions = new()
    {
      ContractResolver = new CamelCasePropertyNamesContractResolver(),
      NullValueHandling = NullValueHandling.Ignore
    };

    private int _nextId = 1;

    public class LspMessage
    {
      public int? Id;
      public string Method;
      public JToken Params;
      public JToken Result;
      public JToken Error;
      public string Json;

      public static LspMessage Parse(string json)
      {
        var node = JObject.Parse(json);
        var msg = new LspMessage();

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

      // Start server
      var serverTask = Task.Run(() =>
          ServerFactory.CreateAsync(
            logger,
            input: serverInput,
            output: serverToClient,
            workspace: workspace,
            ct: cts.Token
            )
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

    public async Task<string> RecvAsync(CancellationToken ct = default)
    {
      using var reader = new StreamReader(_clientOutput, Encoding.UTF8, leaveOpen: true);

      // LSP is message-framed, so read until we have a complete response
      // First read headers
      string line;
      int contentLength = 0;
      while(!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
      {
        if(line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
          contentLength = int.Parse(line.Substring("Content-Length:".Length).Trim());
      }

      if(contentLength == 0)
        return string.Empty;

      char[] buffer = new char[contentLength];
      int read = 0;
      while(read < contentLength)
      {
        int r = await reader.ReadAsync(buffer, read, contentLength - read);
        if (r == 0) break;
        read += r;
      }

      return new string(buffer, 0, read);
    }

    public async Task<LspMessage> RecvMsgAsync(CancellationToken ct = default)
    {
      await foreach(var msg in RecvAllMsgsAsync(ct))
      {
        return msg;
      }

      throw new Exception("No message received");
    }

    public async IAsyncEnumerable<LspMessage> RecvAllMsgsAsync([EnumeratorCancellation] CancellationToken ct = default)
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
        yield return LspMessage.Parse(json);
      }
    }

    public async Task<TResult> SendRequestAsync<TParams, TResult>(
      string method,
      TParams @params,
      CancellationToken ct = default) where TResult : class
    {
      var msg = await SendRequestAsync<TParams>(method, @params, ct);
      if(msg.Result != null)
        return msg.Result.ToObject<TResult>(JsonSerializer.CreateDefault(_jsonOptions));

      if(msg.Error != null)
        throw new InvalidOperationException($"LSP error: {msg.Error}");

      throw new InvalidOperationException("No response received");
    }

    public async Task<LspMessage> SendRequestAsync<TParams>(
      string method,
      TParams @params,
      CancellationToken ct = default)
    {
      var id = Interlocked.Increment(ref _nextId);

      var request = new
      {
        jsonrpc = "2.0",
        id,
        method,
        @params
      };

      var json = JsonConvert.SerializeObject(request, _jsonOptions);
      await SendAsync(json, ct);

      await foreach(var msg in RecvAllMsgsAsync(ct))
      {
        if(msg.Id == id)
          return msg;
      }

      throw new InvalidOperationException("No response received");
    }

    public async Task SendNotificationAsync<TParams>(
      string method,
      TParams @params,
      CancellationToken ct = default)
    {
      var notification = new
      {
        jsonrpc = "2.0",
        method,
        @params
      };

      var json = JsonConvert.SerializeObject(notification, _jsonOptions);
      await SendAsync(json, ct);
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

  //public static string FindReferencesReq(bhl.lsp.proto.Uri uri, string needle)
  //{
  //  var pos = Pos(File.ReadAllText(uri.path), needle);
  //  return "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/references\", \"params\":" +
  //         "{\"textDocument\": {\"uri\": \"" + uri + "\"}, \"position\": " + AsJson(pos) + "}}";
  //}

  //public struct UriNeedle
  //{
  //  public bhl.lsp.proto.Uri uri;
  //  public string needle;
  //  public int end_line_offset;
  //  public int end_column_offset;

  //  public UriNeedle(bhl.lsp.proto.Uri uri, string needle, int end_line_offset = 0, int end_column_offset = 0)
  //  {
  //    this.uri = uri;
  //    this.needle = needle;
  //    this.end_line_offset = end_line_offset;
  //    this.end_column_offset = end_column_offset;
  //  }
  //}

  //public static string FindReferencesRsp(params UriNeedle[] uns)
  //{
  //  string rsp = "{\"id\":1,\"result\":[";

  //  foreach(var un in uns)
  //  {
  //    var start = Pos(File.ReadAllText(un.uri.path), un.needle);
  //    var end = new bhl.SourcePos(start.line + un.end_line_offset, start.column + un.end_column_offset);
  //    rsp += "{\"uri\":\"" + un.uri + "\",\"range\":{\"start\":" +
  //           AsJson(start) + ",\"end\":" + AsJson(end) + "}},";
  //  }

  //  rsp = rsp.TrimEnd(',');

  //  rsp += "],\"jsonrpc\":\"2.0\"}";

  //  return rsp;
  //}

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

  //public static void CleanTestFiles()
  //{
  //  string dir = GetTestDirPath();
  //  if(Directory.Exists(dir))
  //    Directory.Delete(dir, true /*recursive*/);
  //}

  //public static bhl.ProjectConf GetTestProjConf()
  //{
  //  var conf = new bhl.ProjectConf();
  //  conf.src_dirs.Add(GetTestDirPath());
  //  conf.inc_path = GetTestIncPath();
  //  return conf;
  //}

  //public static bhl.IncludePath GetTestIncPath()
  //{
  //  var inc_path = new bhl.IncludePath();
  //  inc_path.Add(GetTestDirPath());
  //  return inc_path;
  //}

  //public static string GetTestDirPath()
  //{
  //  string self_bin = System.Reflection.Assembly.GetExecutingAssembly().Location;
  //  return Path.GetDirectoryName(self_bin) + "/tmp/bhlsp";
  //}

  //public static bhl.lsp.proto.Uri MakeTestDocument(string path, string text, List<string> files = null)
  //{
  //  string full_path = bhl.BuildUtils.NormalizeFilePath(GetTestDirPath() + "/" + path);
  //  Directory.CreateDirectory(Path.GetDirectoryName(full_path));
  //  File.WriteAllText(full_path, text);
  //  if(files != null)
  //    files.Add(full_path);
  //  var uri = new bhl.lsp.proto.Uri(full_path);
  //  return uri;
  //}

  //public static bhl.SourcePos Pos(string code, string needle)
  //{
  //  int idx = code.IndexOf(needle);
  //  if(idx == -1)
  //    throw new Exception("Needle not found: " + needle);
  //  var indexer = new CodeIndex();
  //  indexer.Update(code);
  //  var pos = indexer.GetIndexPosition(idx);
  //  if(pos.line == -1 && pos.column == -1)
  //    throw new Exception("Needle not mapped position: " + needle);
  //  return pos;
  //}

  //public static string AsJson(bhl.SourcePos pos)
  //{
  //  return "{\"line\":" + pos.line + ",\"character\":" + pos.column + "}";
  //}

  //public static Logger NoLogger()
  //{
  //  return new Logger(0, new NoLogger());
  //}

  //public static string NullResultJson(int id)
  //{
  //  return "{\"id\":" + id + ",\"jsonrpc\":\"2.0\",\"result\":null}";
  //}

  //public static IConnection NoConnection()
  //{
  //  return new MockConnection();
  //}
}
