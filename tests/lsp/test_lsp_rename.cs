using System;
using System.Threading.Tasks;
using bhl.lsp;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Xunit;

public class TestLSPRename : TestLSPShared, IDisposable
{
  string bhl1 = @"
  func float test1(float k)
  {
    return 0
  }

  func test2()
  {
    test1(42)
  }

  func test3()
  {
    int upval = 1
    func() {
      upval = upval + 1
    }()
  }

  class Foo {
    int Bar
    func int Method() { return 0 }
  }

  class Baz : Foo {}

  enum Item
  {
    Type = 1
  }
  ";

  string bhl2 = @"
  import ""bhl1""

  func float test5(float k)
  {
    test1(24)
    return 0
  }

  func Foo test6()
  {
    return new Foo
  }

  func Item test7()
  {
    return Item.Type
  }

  func test8()
  {
    var f = new Foo
    f.Bar = 1
    int x = f.Bar
    f.Method()
  }
  ";

  TestLSPHost srv;

  DocumentUri uri1;
  DocumentUri uri2;

  public TestLSPRename()
  {
    srv = NewTestServer();

    CleanTestFiles();

    uri1 = MakeTestDocument("bhl1.bhl", bhl1);
    uri2 = MakeTestDocument("bhl2.bhl", bhl2);
  }

  public void Dispose()
  {
    srv.Dispose();
  }

  [Fact]
  public async Task rename_func_across_files()
  {
    await SendInit(srv);

    var edit = await RenameSymbol(srv, uri1, "st1(42)", "renamed1");
    var edits = FlattenEdits(edit);

    // test1 declaration + call in bhl1, call in bhl2
    Assert.Equal(3, edits.Count);
    Assert.All(edits, e => Assert.Equal("renamed1", e.newText));

    // bhl1: declaration "test1(float k)" and call "test1(42)"
    var bhl1_edits = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Equal(2, bhl1_edits.Count);

    // bhl2: call "test1(24)"
    var bhl2_edits = edits.FindAll(e => e.file == uri2.PathNormalized());
    Assert.Equal(1, bhl2_edits.Count);
  }

  [Fact]
  public async Task rename_local_var()
  {
    await SendInit(srv);

    var edit = await RenameSymbol(srv, uri1, "pval + 1", "cnt");
    var edits = FlattenEdits(edit);

    // upval declaration + two usages inside test3 — all in bhl1 only
    Assert.Equal(3, edits.Count);
    Assert.All(edits, e => Assert.Equal("cnt", e.newText));
    Assert.All(edits, e => Assert.Equal(uri1.PathNormalized(), e.file));
  }

  [Fact]
  public async Task rename_class_across_files()
  {
    await SendInit(srv);

    var edit = await RenameSymbol(srv, uri1, "Foo {", "FooRenamed");
    var edits = FlattenEdits(edit);

    // bhl1: declaration, Baz : Foo; bhl2: return type of test6(), "new Foo", "var f = new Foo"
    Assert.All(edits, e => Assert.Equal("FooRenamed", e.newText));

    var bhl1_edits = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Equal(2, bhl1_edits.Count);

    var bhl2_edits = edits.FindAll(e => e.file == uri2.PathNormalized());
    Assert.Equal(3, bhl2_edits.Count);
  }

  [Fact]
  public async Task rename_enum_across_files()
  {
    await SendInit(srv);

    var edit = await RenameSymbol(srv, uri1, "Item\n", "ItemKind");
    var edits = FlattenEdits(edit);

    // bhl1: declaration; bhl2: return type + Item.Type usage
    Assert.True(edits.Count >= 3);
    Assert.All(edits, e => Assert.Equal("ItemKind", e.newText));
  }

  [Fact]
  public async Task rename_class_field_via_usage()
  {
    await SendInit(srv);

    // Rename via "f.Bar" usage in bhl2 — must also rename the declaration "int Bar" in class Foo
    var edit = await RenameSymbol(srv, uri2, "Bar = 1", "Qux");
    var edits = FlattenEdits(edit);

    // bhl1: declaration "int Bar"; bhl2: "f.Bar = 1" and "f.Bar" (read)
    Assert.Equal(3, edits.Count);
    Assert.All(edits, e => Assert.Equal("Qux", e.newText));

    var bhl1_edits = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Equal(1, bhl1_edits.Count); // just the declaration

    var bhl2_edits = edits.FindAll(e => e.file == uri2.PathNormalized());
    Assert.Equal(2, bhl2_edits.Count); // write + read
  }

  [Fact]
  public async Task rename_class_field_via_declaration()
  {
    await SendInit(srv);

    // Rename via the declaration "int Bar" in bhl1 — position cursor on "Bar" (not the type keyword)
    var edit = await RenameSymbol(srv, uri1, "Bar", "Qux");
    var edits = FlattenEdits(edit);

    Assert.Equal(3, edits.Count);
    Assert.All(edits, e => Assert.Equal("Qux", e.newText));

    var bhl1_edits = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Equal(1, bhl1_edits.Count);

    var bhl2_edits = edits.FindAll(e => e.file == uri2.PathNormalized());
    Assert.Equal(2, bhl2_edits.Count);
  }

  [Fact]
  public async Task rename_class_method_via_usage()
  {
    await SendInit(srv);

    var edit = await RenameSymbol(srv, uri2, "Method()", "DoIt");
    var edits = FlattenEdits(edit);

    // bhl1: declaration "func int Method()"; bhl2: "f.Method()" call
    Assert.Equal(2, edits.Count);
    Assert.All(edits, e => Assert.Equal("DoIt", e.newText));

    var bhl1_edits = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Equal(1, bhl1_edits.Count);

    var bhl2_edits = edits.FindAll(e => e.file == uri2.PathNormalized());
    Assert.Equal(1, bhl2_edits.Count);
  }

  [Fact]
  public async Task rename_enum_item_via_usage()
  {
    await SendInit(srv);

    // Rename via "Item.Type" usage in bhl2 — position on "Type" (after the dot)
    // needle starts at 'T' of "Type" inside "Item.Type"
    var edit = await RenameSymbol(srv, uri2, "Type\n", "Value");
    var edits = FlattenEdits(edit);

    // bhl1: declaration "Type = 1"; bhl2: "Item.Type" usage
    Assert.Equal(2, edits.Count);
    Assert.All(edits, e => Assert.Equal("Value", e.newText));

    var bhl1_edits = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Equal(1, bhl1_edits.Count);

    var bhl2_edits = edits.FindAll(e => e.file == uri2.PathNormalized());
    Assert.Equal(1, bhl2_edits.Count);
  }

  [Fact]
  public async Task rename_enum_item_via_declaration()
  {
    await SendInit(srv);

    // Rename via declaration "Type = 1" in bhl1 — must also rename all usages
    var edit = await RenameSymbol(srv, uri1, "Type = 1", "Value");
    var edits = FlattenEdits(edit);

    Assert.Equal(2, edits.Count);
    Assert.All(edits, e => Assert.Equal("Value", e.newText));

    var bhl1_edits = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Equal(1, bhl1_edits.Count);

    var bhl2_edits = edits.FindAll(e => e.file == uri2.PathNormalized());
    Assert.Equal(1, bhl2_edits.Count);
  }
}
