using System;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using bhl;
using bhl.lsp;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

public class TestLSPBasic : TestLSPShared
{
  public class TestDocumentCalcByteIndex : BHL_TestBase
  {
    [Fact]
    public void Unix_line_endings()
    {
      string bhl = "func int test()\n{\nwhat()\n}";
      var code = new CodeIndex();
      code.Update(bhl);

      AssertEqual("f", bhl[code.CalcByteIndex(0, 0)].ToString());
      AssertEqual("u", bhl[code.CalcByteIndex(0, 1)].ToString());
      AssertEqual("{", bhl[code.CalcByteIndex(1, 0)].ToString());
      AssertEqual("h", bhl[code.CalcByteIndex(2, 1)].ToString());
      AssertEqual("}", bhl[code.CalcByteIndex(3, 0)].ToString());

      Assert.Equal(-1, code.CalcByteIndex(4, 0));
      Assert.Equal(-1, code.CalcByteIndex(100, 1));
    }

    [Fact]
    public void Windows_line_endings()
    {
      string bhl = "func int test()\r\n{\r\nwhat()\r\n}";
      var code = new CodeIndex();
      code.Update(bhl);

      AssertEqual("f", bhl[code.CalcByteIndex(0, 0)].ToString());
      AssertEqual("u", bhl[code.CalcByteIndex(0, 1)].ToString());
      AssertEqual("{", bhl[code.CalcByteIndex(1, 0)].ToString());
      AssertEqual("h", bhl[code.CalcByteIndex(2, 1)].ToString());
      AssertEqual("}", bhl[code.CalcByteIndex(3, 0)].ToString());

      Assert.Equal(-1, code.CalcByteIndex(4, 0));
      Assert.Equal(-1, code.CalcByteIndex(100, 1));
    }

    [Fact]
    public void Mixed_line_endings()
    {
      string bhl = "func int test()\n{\r\nwhat()\r}";
      var code = new CodeIndex();
      code.Update(bhl);

      AssertEqual("f", bhl[code.CalcByteIndex(0, 0)].ToString());
      AssertEqual("u", bhl[code.CalcByteIndex(0, 1)].ToString());
      AssertEqual("{", bhl[code.CalcByteIndex(1, 0)].ToString());
      AssertEqual("h", bhl[code.CalcByteIndex(2, 1)].ToString());
      AssertEqual("}", bhl[code.CalcByteIndex(3, 0)].ToString());

      Assert.Equal(-1, code.CalcByteIndex(4, 0));
      Assert.Equal(-1, code.CalcByteIndex(100, 1));
    }
  }

  [Fact]
  public void TestDocumentGetLineColumn()
  {
    string bhl = "func int test()\n{\nwhat()\n}";
    var code = new CodeIndex();
    code.Update(bhl);

    {
      var pos = code.GetIndexPosition(0);
      AssertEqual("f", bhl[code.CalcByteIndex(pos.line, pos.column)].ToString());
    }

    {
      var pos = code.GetIndexPosition(1);
      AssertEqual("u", bhl[code.CalcByteIndex(pos.line, pos.column)].ToString());
    }

    {
      var pos = code.GetIndexPosition(16);
      AssertEqual("{", bhl[code.CalcByteIndex(pos.line, pos.column)].ToString());
    }

    {
      var pos = code.GetIndexPosition(bhl.Length - 1);
      AssertEqual("}", bhl[code.CalcByteIndex(pos.line, pos.column)].ToString());
    }

    {
      var pos = code.GetIndexPosition(bhl.Length);
      Assert.Equal(-1, pos.line);
      Assert.Equal(-1, pos.column);
    }
  }

  [Fact]
  public void TestNativeSymbolReflection()
  {
    var fn = new FuncSymbolNative(new Origin(), "test", Types.Void,
      delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { return null; }
    );
    Assert.EndsWith("test_lsp_basic.cs", fn.origin.source_file);
    Assert.True(fn.origin.source_range.start.line > 0);
  }

  public class TestRpcResponseErrors : TestLSPShared
  {
    [Fact]
    public async Task invalid_method()
    {
      using var srv = NewTestServer(new Workspace());

      string json = "{\"jsonrpc\": \"2.0\", \"method\": 1, \"params\": \"bar\",\"id\": 1}";
      await srv.SendAsync(json);
      var msg = await srv.RecvMsgAsync();
      AssertEqual("window/logMessage", msg.Method);
      AssertContains( msg.Params.Value<string>("message"), "InvalidRequest | Method='1'");
    }

    [Fact]
    public async Task invalid_params()
    {
      using var srv = NewTestServer(new Workspace());

      string json = "{\"jsonrpc\": \"2.0\", \"method\": \"initialize\", \"params\": \"bar\",\"id\": 1}";
      await srv.SendAsync(json);
      var msg = await srv.RecvMsgAsync();
      AssertContains( msg.Params.Value<string>("message"), "InvalidRequest | Method='initialize'");
    }

  [Fact]
  public async Task TestBadImportError()
  {
    string bhl_v1 = @"
    import hey""

    class Foo {
      int BAR
    }
    ";

    var ws = new Workspace();
    using var srv = NewTestServer(ws);

    CleanTestFiles();
    MakeTestProjConf();

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

    var uri = MakeTestDocument("bhl1.bhl", bhl_v1);

    {
      ws.IndexFiles();

      var didOpen = new DidOpenTextDocumentParams()
      {
        TextDocument = new ()
        {
          LanguageId = "bhl",
          Version = 0,
          Uri = uri,
          Text = bhl_v1
        }
      };
      var result = await srv.SendRequestAsync("textDocument/didOpen", didOpen);
      Assert.Equal(JTokenType.Null, result.Result.Type);
      //AssertContains(conn.buffer, "invalid import 'missing NORMALSTRING'");
    }
  }
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
  }
}
