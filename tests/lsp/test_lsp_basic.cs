using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using bhl;
using bhl.lsp;
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
    public async Task TestErrorOnOpen()
    {
      string bhl_v1 = @"
    import hey""

    class Foo {
      int BAR
    }
    ";

      CleanTestFiles();
      MakeTestProjConf();

      var ws = new Workspace();
      using var srv = NewTestServer(ws);

      await SendInit(srv);

      var uri = MakeTestDocument("bhl1.bhl", bhl_v1);

      await srv.SendNotificationAsync("textDocument/didOpen",
        new DidOpenTextDocumentParams()
        {
          TextDocument = new ()
          {
            LanguageId = "bhl",
            Version = 0,
            Uri = uri,
            Text = bhl_v1
          }
        }
      );

      var diagnostics = await srv.RecvEventAsync<PublishDiagnosticsParams>("textDocument/publishDiagnostics");
      AssertContains(diagnostics.Diagnostics.Last().Message, "invalid import 'missing NORMALSTRING'");

      Assert.True(CountErrors(ws) > 0);
    }

    [Fact]
    public async Task TestErrorOnChange()
    {
      string bhl_v1 = @"
    func float test1(float k)
    {
      return 0
    }
    ";

      string bhl_v2 = @"
    func float test1(float k, int ) // <--- error
    {
      return 0
    }
    ";

      CleanTestFiles();
      MakeTestProjConf();

      var uri = MakeTestDocument("bhl1.bhl", bhl_v1);

      var ws = new Workspace();

      using var srv = NewTestServer(ws);

      await SendInit(srv);

      Assert.Single(ws.Path2Doc);
      Assert.Equal(0, CountErrors(ws));

      await srv.SendNotificationAsync("textDocument/didOpen",
        new DidOpenTextDocumentParams()
          {
            TextDocument = new ()
            {
              LanguageId = "bhl",
              Version = 0,
              Uri = uri,
              Text = bhl_v1
            }
          }
        );

      Assert.Equal(0, CountErrors(ws));

      await srv.SendNotificationAsync("textDocument/didChange",
        new DidChangeTextDocumentParams()
        {
          TextDocument = new ()
          {
            Version = 0,
            Uri = uri,
          },
          ContentChanges = new (
            new TextDocumentContentChangeEvent() { Text = bhl_v2 }
            )
        }
      );

      var diagnostics = await srv.RecvEventAsync<PublishDiagnosticsParams>("textDocument/publishDiagnostics");
      AssertContains(diagnostics.Diagnostics.Last().Message, "missing NAME at ')'");

      Assert.Equal(1, CountErrors(ws));
      Assert.Single(ws.Path2Doc);
    }
  }

  public static int CountErrors(Workspace ws)
  {
    int count = 0;
    foreach (var kv in ws.GetCompileErrors())
      count += kv.Value.Count;
    return count;
  }
}
