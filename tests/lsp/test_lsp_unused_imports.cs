using System.Linq;
using System.Threading.Tasks;
using bhl.lsp;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

public class TestLSPUnusedImports : TestLSPShared, System.IDisposable
{
  TestLSPHost srv;
  Workspace ws;

  public TestLSPUnusedImports()
  {
    CleanTestFiles();
    ws = new Workspace();
    srv = NewTestServer(workspace: ws);
  }

  public void Dispose()
  {
    srv.Dispose();
  }

  async Task TriggerWorkspaceSetup(DocumentUri uri)
  {
    await SendInit(srv);
    await srv.SendRequestAsync<SemanticTokensParams, SemanticTokens>(
      "textDocument/semanticTokens/full",
      new() { TextDocument = uri }
    );
  }

  bool HasUnusedImportWarning(DocumentUri uri, string importName = null)
  {
    string filePath = uri.PathNormalized();
    if(!ws.Path2Proc.TryGetValue(filePath, out var proc))
      return false;
    return proc.result.warnings.Any(w =>
      w.text.Contains("Unused import") &&
      (importName == null || w.text.Contains(importName)));
  }

  [Fact]
  public async Task TestUnusedImportWarning()
  {
    string bhl1 = @"
func void foo() {}
";
    string bhl2 = @"
import ""bhl1""
func void bar() {}
";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    Assert.True(HasUnusedImportWarning(uri2, "bhl1"), "unused import should produce a warning");
  }

  [Fact]
  public async Task TestUsedImportNoWarning()
  {
    string bhl1 = @"
func void foo() {}
";
    string bhl2 = @"
import ""bhl1""
func void bar() { foo() }
";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    Assert.False(HasUnusedImportWarning(uri2), "used import should not produce a warning");
  }

  [Fact]
  public async Task TestPartiallyUsedImports()
  {
    string bhl1 = @"
func void foo() {}
";
    string bhl2 = @"
func void bar() {}
";
    string bhl3 = @"
import ""bhl1""
import ""bhl2""
func void test() { foo() }
";

    MakeTestDocument("bhl1.bhl", bhl1);
    MakeTestDocument("bhl2.bhl", bhl2);
    var uri3 = MakeTestDocument("bhl3.bhl", bhl3);

    await TriggerWorkspaceSetup(uri3);

    Assert.False(HasUnusedImportWarning(uri3, "bhl1"), "used bhl1 import should not produce a warning");
    Assert.True(HasUnusedImportWarning(uri3, "bhl2"), "unused bhl2 import should produce a warning");
  }

  [Fact]
  public async Task TestUsedViaFuncParamType()
  {
    string bhl1 = @"
class Vec3 { float x float y float z }
";
    string bhl2 = @"
import ""bhl1""
func void move(Vec3 pos) {}
";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    Assert.False(HasUnusedImportWarning(uri2), "import used via func param type should not produce a warning");
  }

  [Fact]
  public async Task TestUsedViaClassFieldType()
  {
    string bhl1 = @"
class Foo {}
";
    string bhl2 = @"
import ""bhl1""
class Bar {
  Foo foo
}
";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    Assert.False(HasUnusedImportWarning(uri2), "import used via class field type should not produce a warning");
  }

  [Fact]
  public async Task TestUsedViaClassFieldArrayType()
  {
    string bhl1 = @"
class Foo {}
";
    string bhl2 = @"
import ""bhl1""
class Bar {
  []Foo foos
}
";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    Assert.False(HasUnusedImportWarning(uri2), "import used via class field array type should not produce a warning");
  }

  [Fact]
  public async Task TestUsedViaGlobalVarArrayType()
  {
    string bhl1 = @"
class Foo {}
";
    string bhl2 = @"
import ""bhl1""
[]Foo items
";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    Assert.False(HasUnusedImportWarning(uri2), "import used via global var array type should not produce a warning");
  }

  [Fact]
  public async Task TestUsedViaFuncParamArrayType()
  {
    string bhl1 = @"
class Foo {}
";
    string bhl2 = @"
import ""bhl1""
func void process([]Foo items) {}
";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    Assert.False(HasUnusedImportWarning(uri2), "import used via func param array type should not produce a warning");
  }

  [Fact]
  public async Task RemoveUnusedImportOnSave_SingleUnused()
  {
    string bhl1 = @"func void foo() {}";
    string bhl2 = "import \"bhl1\"\nfunc void bar() {}";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    var edits = ws.GetUnusedImportEdits(uri2);

    Assert.NotNull(edits);
    Assert.Single(edits);
    // the import is on line 0 (0-based); edit should delete that entire line
    Assert.Equal(0, edits[0].Range.Start.Line);
    Assert.Equal(0, edits[0].Range.Start.Character);
    Assert.Equal(1, edits[0].Range.End.Line);
    Assert.Equal(0, edits[0].Range.End.Character);
    Assert.Equal("", edits[0].NewText);
  }

  [Fact]
  public async Task RemoveUnusedImportOnSave_NoUnused()
  {
    string bhl1 = @"func void foo() {}";
    string bhl2 = "import \"bhl1\"\nfunc void bar() { foo() }";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    var edits = ws.GetUnusedImportEdits(uri2);

    Assert.Null(edits);
  }

  [Fact]
  public async Task RemoveUnusedImportOnSave_MultipleUnused()
  {
    string bhl1 = @"func void foo() {}";
    string bhl2 = @"func void bar() {}";
    string bhl3 = "import \"bhl1\"\nimport \"bhl2\"\nfunc void test() {}";

    MakeTestDocument("bhl1.bhl", bhl1);
    MakeTestDocument("bhl2.bhl", bhl2);
    var uri3 = MakeTestDocument("bhl3.bhl", bhl3);

    await TriggerWorkspaceSetup(uri3);

    var edits = ws.GetUnusedImportEdits(uri3);

    Assert.NotNull(edits);
    Assert.Equal(2, edits.Count);
    // both import lines should be deleted
    var lines = edits.Select(e => e.Range.Start.Line).OrderBy(l => l).ToList();
    Assert.Equal(0, lines[0]);
    Assert.Equal(1, lines[1]);
  }

  [Fact]
  public async Task RemoveUnusedImportOnSave_PartiallyUnused()
  {
    string bhl1 = @"func void foo() {}";
    string bhl2 = @"func void bar() {}";
    string bhl3 = "import \"bhl1\"\nimport \"bhl2\"\nfunc void test() { foo() }";

    MakeTestDocument("bhl1.bhl", bhl1);
    MakeTestDocument("bhl2.bhl", bhl2);
    var uri3 = MakeTestDocument("bhl3.bhl", bhl3);

    await TriggerWorkspaceSetup(uri3);

    var edits = ws.GetUnusedImportEdits(uri3);

    Assert.NotNull(edits);
    Assert.Single(edits);
    // only bhl2 (line 1) should be removed
    Assert.Equal(1, edits[0].Range.Start.Line);
  }
}
