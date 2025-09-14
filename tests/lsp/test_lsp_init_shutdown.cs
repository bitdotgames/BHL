using System;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using bhl.lsp;
using Xunit;

public class TestLSPInitShutdownExit : TestLSPShared
{
  [Fact]
  public async Task _1()
  {
    using var srv = NewTestServer(new Workspace());
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    var initParams = new InitializeParams
    {
      Capabilities = new ClientCapabilities(),
      ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id
    };

    var result = await srv.SendRequestAsync<InitializeParams, InitializeResult>(
      "initialize",
      initParams,
      cts.Token
    );

    Assert.NotNull(result.Capabilities);

    //string req = "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"initialize\", \"params\": {\"capabilities\":{}}}";

    //string expected = "{\"id\":1,\"result\":{" +
    //             "\"capabilities\":{" +
    //             "\"textDocumentSync\":null," +
    //             "\"hoverProvider\":null," +
    //             "\"declarationProvider\":null," +
    //             "\"definitionProvider\":null," +
    //             "\"typeDefinitionProvider\":null," +
    //             "\"implementationProvider\":null," +
    //             "\"referencesProvider\":null," +
    //             "\"documentHighlightProvider\":null," +
    //             "\"documentSymbolProvider\":null," +
    //             "\"codeActionProvider\":null," +
    //             "\"colorProvider\":null," +
    //             "\"documentFormattingProvider\":null," +
    //             "\"documentRangeFormattingProvider\":null," +
    //             "\"renameProvider\":null," +
    //             "\"foldingRangeProvider\":null," +
    //             "\"selectionRangeProvider\":null," +
    //             "\"linkedEditingRangeProvider\":null," +
    //             "\"callHierarchyProvider\":null," +
    //             "\"semanticTokensProvider\":null," +
    //             "\"monikerProvider\":null," +
    //             "\"workspaceSymbolProvider\":null}," +
    //             "\"serverInfo\":{\"name\":\"bhlsp\",\"version\":\"" + bhl.Version.Name + "\"}}," +
    //             "\"jsonrpc\":\"2.0\"}";

    //await srv.SendAsync(req);
    //var rsp = await srv.RecvAsync();
    //AssertEqual(rsp, expected);
  }
//
//  [Fact]
//  public async Task _2()
//  {
//    AssertEqual(
//      await srv.Handle(new Request(1, "initialized").ToJson()),
//      string.Empty
//    );
//  }
//
//  [Fact]
//  public async Task _3()
//  {
//    AssertEqual(
//      await srv.Handle(new Request(1, "shutdown").ToJson()),
//      NullResultJson(1)
//    );
//  }
//
//  [Fact]
//  public async Task _4()
//  {
//    Assert.False(srv.going_to_exit);
//    AssertEqual(
//      await srv.Handle(new Request(1, "exit").ToJson()),
//      string.Empty
//    );
//    Assert.True(srv.going_to_exit);
//  }
}
