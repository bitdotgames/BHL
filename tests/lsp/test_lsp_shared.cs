using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Serilog;
using OmniSharp.Extensions.LanguageServer.Server;
using bhl;
using bhl.lsp;

public class TestLSPShared : BHL_TestBase
{
  public sealed class TestLSPHost : IAsyncDisposable
  {
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly LanguageServer _server;

    public TestLSPHost(LanguageServer server, Stream input, Stream output)
    {
      _server = server;
      _input = input;
      _output = output;
    }

    public async Task SendAsync(string json, CancellationToken ct = default)
    {
      // Frame according to LSP spec
      var bytes = Encoding.UTF8.GetBytes(json);
      var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");

      // Write header + json to input stream
      await _input.WriteAsync(header, 0, header.Length, ct);
      await _input.WriteAsync(bytes, 0, bytes.Length, ct);
      await _input.FlushAsync(ct);
    }

    public async Task<string> RecvAsync(CancellationToken ct = default)
    {
      using var reader = new StreamReader(_output, Encoding.UTF8, leaveOpen: true);
      var text = await reader.ReadToEndAsync();
      return text;
    }

    public async ValueTask DisposeAsync()
    {
      _server.Dispose();
      try
      {
        await _server.WaitForExit; // wait for graceful exit
      }
      catch { /* swallow during test */ }
    }
  }

  public static async Task<TestLSPHost> NewTestServer(
    Workspace workspace,
    ILogger logger = null,
    CancellationToken ct = default)
  {
    if(logger == null)
      logger = new LoggerConfiguration().CreateLogger();

    var input = new MemoryStream();
    var output = new MemoryStream();

    var server = await ServerCreator.CreateAsync(logger,
        input: input,
        output: output,
        workspace: workspace,
        ct: ct);

    return new TestLSPHost(server, input: input, output: output);
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
