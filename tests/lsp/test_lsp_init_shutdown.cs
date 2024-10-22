using System.Threading.Tasks;
using bhl.lsp;
using Xunit;

public class TestLSPInitShutdownExit : TestLSPShared
{
  Server srv;

  public TestLSPInitShutdownExit()
  {
    srv = new Server(NoLogger(), NoConnection(), new Workspace());
    srv.AttachService(new LifecycleService(srv));
  }
  
  [Fact]
  public async Task _1()
  {
    string req = "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"initialize\", \"params\": {\"capabilities\":{}}}";
    
    string rsp = "{\"id\":1,\"result\":{" +
            "\"capabilities\":{" +
              "\"textDocumentSync\":null," +
              "\"hoverProvider\":null," +
              "\"declarationProvider\":null," +
              "\"definitionProvider\":null," +
              "\"typeDefinitionProvider\":null," +
              "\"implementationProvider\":null," +
              "\"referencesProvider\":null," +
              "\"documentHighlightProvider\":null," +
              "\"documentSymbolProvider\":null," +
              "\"codeActionProvider\":null," +
              "\"colorProvider\":null," +
              "\"documentFormattingProvider\":null," +
              "\"documentRangeFormattingProvider\":null," +
              "\"renameProvider\":null," +
              "\"foldingRangeProvider\":null," +
              "\"selectionRangeProvider\":null," +
              "\"linkedEditingRangeProvider\":null," +
              "\"callHierarchyProvider\":null," +
              "\"semanticTokensProvider\":null," +
              "\"monikerProvider\":null," +
              "\"workspaceSymbolProvider\":null}," +
              "\"serverInfo\":{\"name\":\"bhlsp\",\"version\":\"" + bhl.Version.Name + "\"}}," +
              "\"jsonrpc\":\"2.0\"}";

    AssertEqual(await srv.Handle(req), rsp);
  }
  
  [Fact]
  public async Task _2()
  {
    AssertEqual(
      await srv.Handle(new Request(1, "initialized").ToJson()),
      string.Empty
    );
  }
  
  [Fact]
  public async Task _3()
  {
    AssertEqual(
      await srv.Handle(new Request(1, "shutdown").ToJson()),
      NullResultJson(1)
    );
  }
  
  [Fact]
  public async Task _4()
  {
    AssertFalse(srv.going_to_exit);
    AssertEqual(
      await srv.Handle(new Request(1, "exit").ToJson()),
      string.Empty
    );
    AssertTrue(srv.going_to_exit);
  }
}