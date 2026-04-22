using System;
using System.Threading.Tasks;
using bhl.lsp;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Xunit;

// Tests that symbol renames propagate correctly through a multi-file import chain:
//   bhl3 imports bhl2
//   bhl2 imports bhl1
// Symbols defined in bhl1 may be used in bhl2 and bhl3.
// Renaming from any file must update all occurrences across the whole chain.
public class TestLSPRenameImports : TestLSPShared, IDisposable
{
  // bhl1: definitions only
  string bhl1 = @"
  func int Compute(int n)
  {
    return n
  }

  class Widget {
    int value
    func int Get() { return 0 }
  }

  enum Color
  {
    Red = 1
  }
  ";

  // bhl2: imports bhl1, uses all three symbols
  string bhl2 = @"
  import ""bhl1""

  func int Double(int n)
  {
    return Compute(n)
  }

  func Widget MakeWidget()
  {
    var w = new Widget
    w.value = 1
    return w
  }

  func Color GetColor()
  {
    return Color.Red
  }
  ";

  // bhl3: imports bhl2 (which re-exports bhl1 symbols) and also imports bhl1 directly
  string bhl3 = @"
  import ""bhl1""
  import ""bhl2""

  func int Triple(int n)
  {
    return Compute(n) + Double(n)
  }

  func test()
  {
    var w = new Widget
    int v = w.Get()
    Color c = Color.Red
  }
  ";

  TestLSPHost srv;

  DocumentUri uri1;
  DocumentUri uri2;
  DocumentUri uri3;

  public TestLSPRenameImports()
  {
    srv = NewTestServer();
    CleanTestFiles();
    uri1 = MakeTestDocument("bhl1.bhl", bhl1);
    uri2 = MakeTestDocument("bhl2.bhl", bhl2);
    uri3 = MakeTestDocument("bhl3.bhl", bhl3);
  }

  public void Dispose()
  {
    srv.Dispose();
  }

  [Fact]
  public async Task rename_func_propagates_to_all_importers()
  {
    await SendInit(srv);

    // Rename "Compute" defined in bhl1 — used in bhl2 and bhl3
    var edit = await RenameSymbol(srv, uri1, "Compute(int n)", "Calculate");
    var edits = FlattenEdits(edit);

    Assert.All(edits, e => Assert.Equal("Calculate", e.newText));

    // bhl1: declaration
    var e1 = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Single(e1);

    // bhl2: one call site
    var e2 = edits.FindAll(e => e.file == uri2.PathNormalized());
    Assert.Single(e2);

    // bhl3: one call site
    var e3 = edits.FindAll(e => e.file == uri3.PathNormalized());
    Assert.Single(e3);
  }

  [Fact]
  public async Task rename_func_via_deepest_importer()
  {
    await SendInit(srv);

    // Rename via usage in bhl3 — must reach declaration in bhl1 and usage in bhl2
    var edit = await RenameSymbol(srv, uri3, "Compute(n) +", "Calculate");
    var edits = FlattenEdits(edit);

    Assert.All(edits, e => Assert.Equal("Calculate", e.newText));

    var e1 = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Single(e1); // declaration

    var e2 = edits.FindAll(e => e.file == uri2.PathNormalized());
    Assert.Single(e2); // usage in Double()

    var e3 = edits.FindAll(e => e.file == uri3.PathNormalized());
    Assert.Single(e3); // usage in Triple()
  }

  [Fact]
  public async Task rename_class_propagates_to_all_importers()
  {
    await SendInit(srv);

    var edit = await RenameSymbol(srv, uri1, "Widget {", "Control");
    var edits = FlattenEdits(edit);

    Assert.All(edits, e => Assert.Equal("Control", e.newText));

    // bhl1: class declaration
    var e1 = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Single(e1);

    // bhl2: return type of MakeWidget() + "new Widget"
    var e2 = edits.FindAll(e => e.file == uri2.PathNormalized());
    Assert.Equal(2, e2.Count);

    // bhl3: "new Widget" (var w uses type inference, no explicit Widget annotation)
    var e3 = edits.FindAll(e => e.file == uri3.PathNormalized());
    Assert.Single(e3);
  }

  [Fact]
  public async Task rename_class_field_propagates_to_all_importers()
  {
    await SendInit(srv);

    // Rename "value" field via usage in bhl2
    var edit = await RenameSymbol(srv, uri2, "value = 1", "score");
    var edits = FlattenEdits(edit);

    Assert.All(edits, e => Assert.Equal("score", e.newText));

    // bhl1: declaration "int value"
    var e1 = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Single(e1);

    // bhl2: "w.value = 1"
    var e2 = edits.FindAll(e => e.file == uri2.PathNormalized());
    Assert.Single(e2);
  }

  [Fact]
  public async Task rename_class_method_propagates_to_all_importers()
  {
    await SendInit(srv);

    // Rename "Get" method via usage in bhl3
    var edit = await RenameSymbol(srv, uri3, "Get()", "Fetch");
    var edits = FlattenEdits(edit);

    Assert.All(edits, e => Assert.Equal("Fetch", e.newText));

    // bhl1: declaration "func int Get()"
    var e1 = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Single(e1);

    // bhl3: "w.Get()" call
    var e3 = edits.FindAll(e => e.file == uri3.PathNormalized());
    Assert.Single(e3);
  }

  [Fact]
  public async Task rename_enum_propagates_to_all_importers()
  {
    await SendInit(srv);

    var edit = await RenameSymbol(srv, uri1, "Color\n", "Hue");
    var edits = FlattenEdits(edit);

    Assert.All(edits, e => Assert.Equal("Hue", e.newText));

    // bhl1: enum declaration
    var e1 = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Single(e1);

    // bhl2: return type of GetColor() + Color.Red
    var e2 = edits.FindAll(e => e.file == uri2.PathNormalized());
    Assert.Equal(2, e2.Count);

    // bhl3: local var type "Color c" + Color.Red
    var e3 = edits.FindAll(e => e.file == uri3.PathNormalized());
    Assert.Equal(2, e3.Count);
  }

  [Fact]
  public async Task rename_enum_item_propagates_to_all_importers()
  {
    await SendInit(srv);

    // Rename "Red" enum item via declaration in bhl1
    var edit = await RenameSymbol(srv, uri1, "Red = 1", "Crimson");
    var edits = FlattenEdits(edit);

    Assert.All(edits, e => Assert.Equal("Crimson", e.newText));

    // bhl1: declaration
    var e1 = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Single(e1);

    // bhl2: Color.Red in GetColor()
    var e2 = edits.FindAll(e => e.file == uri2.PathNormalized());
    Assert.Single(e2);

    // bhl3: Color.Red in test()
    var e3 = edits.FindAll(e => e.file == uri3.PathNormalized());
    Assert.Single(e3);
  }

  [Fact]
  public async Task rename_enum_item_via_deepest_importer()
  {
    await SendInit(srv);

    // Rename via usage in bhl3 — must reach declaration in bhl1 and usage in bhl2
    var edit = await RenameSymbol(srv, uri3, "Red\n", "Crimson");
    var edits = FlattenEdits(edit);

    Assert.All(edits, e => Assert.Equal("Crimson", e.newText));

    var e1 = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Single(e1);

    var e2 = edits.FindAll(e => e.file == uri2.PathNormalized());
    Assert.Single(e2);

    var e3 = edits.FindAll(e => e.file == uri3.PathNormalized());
    Assert.Single(e3);
  }
}
