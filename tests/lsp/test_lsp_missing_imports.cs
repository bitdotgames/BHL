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

  [Fact]
  public async Task AddsCorrectImportForNativeModuleSymbol()
  {
    // std.io is a native module (registered in Types, not tracked in Path2Proc like
    // user .bhl files) - the fix has to consult it explicitly.
    string bhl1 = "func void bar() { std.io.WriteLine(\"hi\") }";
    var uri1 = MakeTestDocument("bhl1.bhl", bhl1);

    await TriggerWorkspaceSetup(uri1);

    var edits = ws.GetMissingImportEdits(uri1);

    Assert.NotNull(edits);
    Assert.Single(edits);
    Assert.Equal("import \"std/io\"\n", edits[0].NewText);
  }

  [Fact]
  public async Task DisambiguatesNamespaceFragmentedAcrossNativeModules()
  {
    // 'std' itself is nested separately by the std, std/io and std/bind native modules -
    // GetType() belongs to the plain "std" module, not "std/io" or "std/bind", and only
    // the member-access chain following 'std' can tell them apart.
    string bhl1 = "func void bar() { std.GetType(1) }";
    var uri1 = MakeTestDocument("bhl1.bhl", bhl1);

    await TriggerWorkspaceSetup(uri1);

    var edits = ws.GetMissingImportEdits(uri1);

    Assert.NotNull(edits);
    Assert.Single(edits);
    Assert.Equal("import \"std\"\n", edits[0].NewText);
  }

  [Fact]
  public async Task DoesNotSuggestUnrelatedFileThatOnlyReExportsTheSymbol()
  {
    // hello.bhl imports std/io for its own use; hello_func.bhl also calls std.io.WriteLine
    // but has no import of its own. The missing 'std' must resolve to the native "std/io"
    // module, not to "hello" just because hello.bhl happens to re-export 'std' via its own
    // import (a link-shadow member, not a genuine declaration).
    string hello = "import \"std/io\"\nfunc void main() { std.io.WriteLine(\"Hello World!\") }";
    string hello_func = "func void other() { std.io.WriteLine(\"hi\") }";

    MakeTestDocument("hello.bhl", hello);
    var uri2 = MakeTestDocument("hello_func.bhl", hello_func);

    await TriggerWorkspaceSetup(uri2);

    var edits = ws.GetMissingImportEdits(uri2);

    Assert.NotNull(edits);
    Assert.Single(edits);
    Assert.Equal("import \"std/io\"\n", edits[0].NewText);
  }
}
