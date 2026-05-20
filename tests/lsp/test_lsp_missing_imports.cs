using System.Linq;
using System.Threading.Tasks;
using bhl.lsp;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

public class TestLSPMissingImports : TestLSPShared, System.IDisposable
{
  TestLSPHost srv;
  Workspace ws;

  public TestLSPMissingImports()
  {
    CleanTestFiles();
    ws = new Workspace();
    srv = NewTestServer(workspace: ws);
  }

  public void Dispose() => srv.Dispose();

  async Task TriggerWorkspaceSetup(DocumentUri uri)
  {
    await SendInit(srv);
    await srv.SendRequestAsync<SemanticTokensParams, SemanticTokens>(
      "textDocument/semanticTokens/full",
      new() { TextDocument = uri }
    );
  }

  [Fact]
  public async Task AddsMissingImport()
  {
    string bhl1 = "func void foo() {}";
    string bhl2 = "func void bar() { foo() }";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    var edits = ws.GetMissingImportEdits(uri2);

    Assert.NotNull(edits);
    Assert.Single(edits);
    Assert.Equal("import \"bhl1\"\n", edits[0].NewText);
  }

  [Fact]
  public async Task NoEditsWhenNoErrors()
  {
    string bhl1 = "func void foo() {}";
    string bhl2 = "import \"bhl1\"\nfunc void bar() { foo() }";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    var edits = ws.GetMissingImportEdits(uri2);

    Assert.Null(edits);
  }

  [Fact]
  public async Task NoEditsWhenAlreadyImported()
  {
    string bhl1 = "func void foo() {}";
    string bhl2 = "import \"bhl1\"\nfunc void bar() { foo() }";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    var edits = ws.GetMissingImportEdits(uri2);
    Assert.Null(edits);
  }

  [Fact]
  public async Task NoEditsWhenSymbolNotInAnyModule()
  {
    string bhl2 = "func void bar() { completelymissing() }";

    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    var edits = ws.GetMissingImportEdits(uri2);
    Assert.Null(edits);
  }

  [Fact]
  public async Task AddsMultipleMissingImports()
  {
    string bhl1 = "func void foo() {}";
    string bhl3 = "func void baz() {}";
    string bhl2 = "func void bar() { foo() baz() }";

    MakeTestDocument("bhl1.bhl", bhl1);
    MakeTestDocument("bhl3.bhl", bhl3);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    var edits = ws.GetMissingImportEdits(uri2);

    Assert.NotNull(edits);
    Assert.Equal(2, edits.Count);
    var texts = edits.Select(e => e.NewText).ToHashSet();
    Assert.Contains("import \"bhl1\"\n", texts);
    Assert.Contains("import \"bhl3\"\n", texts);
  }

  [Fact]
  public async Task InsertsAtTopWhenNoExistingImports()
  {
    string bhl1 = "func void foo() {}";
    string bhl2 = "func void bar() { foo() }";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    var edits = ws.GetMissingImportEdits(uri2);

    Assert.NotNull(edits);
    Assert.Equal(0, edits[0].Range.Start.Line);
    Assert.Equal(0, edits[0].Range.Start.Character);
  }

  [Fact]
  public async Task InsertsAfterExistingImports()
  {
    string bhl1 = "func void foo() {}";
    string bhl3 = "func void baz() {}";
    // bhl2 already imports bhl1 but is missing bhl3
    string bhl2 = "import \"bhl1\"\nfunc void bar() { foo() baz() }";

    MakeTestDocument("bhl1.bhl", bhl1);
    MakeTestDocument("bhl3.bhl", bhl3);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    var edits = ws.GetMissingImportEdits(uri2);

    Assert.NotNull(edits);
    Assert.Single(edits);
    Assert.Equal("import \"bhl3\"\n", edits[0].NewText);
    // insertion must be after the existing import line
    Assert.True(edits[0].Range.Start.Line > 0);
  }
}
