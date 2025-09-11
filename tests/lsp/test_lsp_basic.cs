using System.Threading.Tasks;
using bhl;
using bhl.lsp;
using Xunit;

//public class TestLSPBasic : TestLSPShared
//{
//  public class TestDocumentCalcByteIndex : BHL_TestBase
//  {
//    [Fact]
//    public void Unix_line_endings()
//    {
//      string bhl = "func int test()\n{\nwhat()\n}";
//      var code = new CodeIndex();
//      code.Update(bhl);
//
//      AssertEqual("f", bhl[code.CalcByteIndex(0, 0)].ToString());
//      AssertEqual("u", bhl[code.CalcByteIndex(0, 1)].ToString());
//      AssertEqual("{", bhl[code.CalcByteIndex(1, 0)].ToString());
//      AssertEqual("h", bhl[code.CalcByteIndex(2, 1)].ToString());
//      AssertEqual("}", bhl[code.CalcByteIndex(3, 0)].ToString());
//
//      Assert.Equal(-1, code.CalcByteIndex(4, 0));
//      Assert.Equal(-1, code.CalcByteIndex(100, 1));
//    }
//
//    [Fact]
//    public void Windows_line_endings()
//    {
//      string bhl = "func int test()\r\n{\r\nwhat()\r\n}";
//      var code = new CodeIndex();
//      code.Update(bhl);
//
//      AssertEqual("f", bhl[code.CalcByteIndex(0, 0)].ToString());
//      AssertEqual("u", bhl[code.CalcByteIndex(0, 1)].ToString());
//      AssertEqual("{", bhl[code.CalcByteIndex(1, 0)].ToString());
//      AssertEqual("h", bhl[code.CalcByteIndex(2, 1)].ToString());
//      AssertEqual("}", bhl[code.CalcByteIndex(3, 0)].ToString());
//
//      Assert.Equal(-1, code.CalcByteIndex(4, 0));
//      Assert.Equal(-1, code.CalcByteIndex(100, 1));
//    }
//
//    [Fact]
//    public void Mixed_line_endings()
//    {
//      string bhl = "func int test()\n{\r\nwhat()\r}";
//      var code = new CodeIndex();
//      code.Update(bhl);
//
//      AssertEqual("f", bhl[code.CalcByteIndex(0, 0)].ToString());
//      AssertEqual("u", bhl[code.CalcByteIndex(0, 1)].ToString());
//      AssertEqual("{", bhl[code.CalcByteIndex(1, 0)].ToString());
//      AssertEqual("h", bhl[code.CalcByteIndex(2, 1)].ToString());
//      AssertEqual("}", bhl[code.CalcByteIndex(3, 0)].ToString());
//
//      Assert.Equal(-1, code.CalcByteIndex(4, 0));
//      Assert.Equal(-1, code.CalcByteIndex(100, 1));
//    }
//  }
//
//  [Fact]
//  public void TestDocumentGetLineColumn()
//  {
//    string bhl = "func int test()\n{\nwhat()\n}";
//    var code = new CodeIndex();
//    code.Update(bhl);
//
//    {
//      var pos = code.GetIndexPosition(0);
//      AssertEqual("f", bhl[code.CalcByteIndex(pos.line, pos.column)].ToString());
//    }
//
//    {
//      var pos = code.GetIndexPosition(1);
//      AssertEqual("u", bhl[code.CalcByteIndex(pos.line, pos.column)].ToString());
//    }
//
//    {
//      var pos = code.GetIndexPosition(16);
//      AssertEqual("{", bhl[code.CalcByteIndex(pos.line, pos.column)].ToString());
//    }
//
//    {
//      var pos = code.GetIndexPosition(bhl.Length - 1);
//      AssertEqual("}", bhl[code.CalcByteIndex(pos.line, pos.column)].ToString());
//    }
//
//    {
//      var pos = code.GetIndexPosition(bhl.Length);
//      Assert.Equal(-1, pos.line);
//      Assert.Equal(-1, pos.column);
//    }
//  }
//
//  [Fact]
//  public void TestNativeSymbolReflection()
//  {
//    var fn = new FuncSymbolNative(new Origin(), "test", Types.Void,
//      delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { return null; }
//    );
//    Assert.EndsWith("test_lsp_basic.cs", fn.origin.source_file);
//    Assert.True(fn.origin.source_range.start.line > 0);
//  }
//
//  public class TestRpcResponseErrors : TestLSPShared
//  {
//    [Fact]
//    public async Task parse_error()
//    {
//      var srv = new ServerCreator(NoLogger(), NoConnection(), new Workspace());
//      string json = "{\"jsonrpc\": \"2.0\", \"method\": \"initialize";
//      AssertEqual(
//        await srv.Handle(json),
//        "{\"id\":null,\"error\":{\"code\":-32700,\"message\":\"Parse error\"},\"jsonrpc\":\"2.0\"}"
//      );
//    }
//
//    [Fact]
//    public async Task invalid_request()
//    {
//      var srv = new ServerCreator(NoLogger(), NoConnection(), new Workspace());
//      string json = "{\"jsonrpc\": \"2.0\", \"id\": 1}";
//      AssertEqual(
//        await srv.Handle(json),
//        "{\"id\":1,\"error\":{\"code\":-32600,\"message\":\"\"},\"jsonrpc\":\"2.0\"}"
//      );
//    }
//
//    [Fact]
//    public async Task invalid_request_2()
//    {
//      var srv = new ServerCreator(NoLogger(), NoConnection(), new Workspace());
//      string json = "{\"jsonrpc\": \"2.0\", \"method\": 1, \"params\": \"bar\",\"id\": 1}";
//      AssertEqual(
//        await srv.Handle(json),
//        "{\"id\":1,\"error\":{\"code\":-32600,\"message\":\"\"},\"jsonrpc\":\"2.0\"}"
//      );
//    }
//
//    [Fact]
//    public async Task method_not_found()
//    {
//      var srv = new ServerCreator(NoLogger(), NoConnection(), new Workspace());
//      string json = "{\"jsonrpc\": \"2.0\", \"method\": \"foo\", \"id\": 1}";
//      AssertEqual(
//        await srv.Handle(json),
//        "{\"id\":1,\"error\":{\"code\":-32601,\"message\":\"Method not found: foo\"},\"jsonrpc\":\"2.0\"}"
//      );
//    }
//
//    [Fact]
//    public async Task invalid_params()
//    {
//      var srv = new ServerCreator(NoLogger(), NoConnection(), new Workspace());
//      srv.AttachService(new bhl.lsp.LifecycleService(srv));
//      string json = "{\"jsonrpc\": \"2.0\", \"method\": \"initialize\", \"params\": \"bar\",\"id\": 1}";
//      AssertEqual(
//        await srv.Handle(json),
//        "{\"id\":1,\"error\":{\"code\":-32602,\"message\":\"Error converting value \\\"bar\\\" to type 'bhl.lsp.proto.InitializeParams'. Path ''.\"},\"jsonrpc\":\"2.0\"}"
//      );
//    }
//  }
//
//  [Fact]
//  public async Task TestBadImportError()
//  {
//    string bhl_v1 = @"
//    import hey""
//
//    class Foo {
//      int BAR
//    }
//    ";
//
//    var ws = new Workspace();
//
//    var conn = new MockConnection();
//    var srv = new ServerCreator(NoLogger(), conn, ws);
//    srv.AttachService(new bhl.lsp.TextDocumentSynchronizationService(srv));
//    srv.AttachService(new bhl.lsp.DiagnosticService(srv));
//
//    CleanTestFiles();
//
//    var uri = MakeTestDocument("bhl1.bhl", bhl_v1);
//
//    ws.Init(new bhl.Types(), GetTestProjConf());
//
//    {
//      ws.IndexFiles();
//
//      AssertEqual(
//        await srv.Handle(new Request(1, "textDocument/didOpen",
//          new bhl.lsp.proto.DidOpenTextDocumentParams()
//          {
//            textDocument = new bhl.lsp.proto.TextDocumentItem()
//            {
//              languageId = "bhl",
//              version = 0,
//              uri = uri,
//              text = bhl_v1
//            }
//          }
//        ).ToJson()),
//        string.Empty
//      );
//
//      AssertContains(conn.buffer, "invalid import 'missing NORMALSTRING'");
//    }
//  }
//
//  [Fact]
//  public async Task TestOpenChangeCloseDocument()
//  {
//    string bhl_v1 = @"
//    func float test1(float k)
//    {
//      return 0
//    }
//
//    func test2()
//    {
//      test1(1)
//    }
//    ";
//
//    string bhl_v2 = @"
//    func float test1(float k, int j)
//    {
//      return 0
//    }
//
//    func test2()
//    {
//      test1(1, 2)
//    }
//    ";
//
//    var ws = new Workspace();
//
//    var srv = new ServerCreator(NoLogger(), NoConnection(), ws);
//    srv.AttachService(new bhl.lsp.TextDocumentSynchronizationService(srv));
//
//    CleanTestFiles();
//
//    var uri = MakeTestDocument("bhl1.bhl", bhl_v1);
//
//    ws.Init(new bhl.Types(), GetTestProjConf());
//
//    {
//      ws.IndexFiles();
//
//      AssertEqual(
//        await srv.Handle(new Request(1, "textDocument/didOpen",
//          new bhl.lsp.proto.DidOpenTextDocumentParams()
//          {
//            textDocument = new bhl.lsp.proto.TextDocumentItem()
//            {
//              languageId = "bhl",
//              version = 0,
//              uri = uri,
//              text = bhl_v1
//            }
//          }
//        ).ToJson()),
//        string.Empty
//      );
//    }
//
//    CleanTestFiles();
//
//    {
//      MakeTestDocument("bhl1.bhl", bhl_v2);
//
//      ws.IndexFiles();
//
//      AssertEqual(
//        await srv.Handle(new Request(1, "textDocument/didChange",
//            new bhl.lsp.proto.DidChangeTextDocumentParams()
//            {
//              textDocument = new bhl.lsp.proto.VersionedTextDocumentIdentifier()
//              {
//                version = 1,
//                uri = uri
//              },
//              contentChanges = new[]
//              {
//                new bhl.lsp.proto.TextDocumentContentChangeEvent { text = bhl_v2 }
//              }
//            }).ToJson()
//        ),
//        string.Empty
//      );
//    }
//
//    {
//      AssertEqual(
//        await srv.Handle(new Request(1, "textDocument/didClose",
//            new bhl.lsp.proto.DidCloseTextDocumentParams()
//            {
//              textDocument = new bhl.lsp.proto.TextDocumentIdentifier()
//              {
//                uri = uri
//              }
//            }).ToJson()
//        ),
//        string.Empty
//      );
//    }
//  }
//}
