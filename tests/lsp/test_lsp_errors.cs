using System.Linq;
using System.Threading.Tasks;
using bhl.lsp;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

public class TestLspDocumentErrors : TestLSPShared
{
  [Fact]
  public async Task invalid_method()
  {
    using var srv = NewTestServer();

    string json = "{\"jsonrpc\": \"2.0\", \"method\": 1, \"params\": \"bar\",\"id\": 1}";
    await srv.SendAsync(json);
    var msg = await srv.RecvMsgAsync();
    AssertEqual("window/logMessage", msg.Method);
    AssertContains( msg.Params.Value<string>("message"), "InvalidRequest | Method='1'");
  }

  [Fact]
  public async Task invalid_params()
  {
    using var srv = NewTestServer();

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
    MakeTestProjConfFile();

    var ws = new Workspace();
    using var srv = NewTestServer(workspace: ws);

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

    Assert.True(ws.GetCompileErrors().Count > 0);
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
    MakeTestProjConfFile();

    var uri = MakeTestDocument("bhl1.bhl", bhl_v1);

    var ws = new Workspace();

    using var srv = NewTestServer(workspace: ws);

    await SendInit(srv);

    Assert.Single(ws.Path2Doc);
    Assert.Empty(ws.GetCompileErrors(filter_empty: true));

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

    Assert.Empty(ws.GetCompileErrors(filter_empty: true));

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

    Assert.Single(ws.GetCompileErrors(filter_empty: true));
    Assert.Single(ws.Path2Doc);
  }
}
