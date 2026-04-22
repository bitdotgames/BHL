using System;
using System.Threading.Tasks;
using bhl.lsp;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Xunit;

public class TestLSPRenameIface : TestLSPShared, IDisposable
{
  string bhl1 = @"
  interface IShape {
    func float Area()
    func float Perimeter()
  }

  class Circle : IShape {
    float radius
    func float Area() { return 0 }
    func float Perimeter() { return 0 }
  }

  class Square : IShape {
    float side
    func float Area() { return 0 }
    func float Perimeter() { return 0 }
  }
  ";

  string bhl2 = @"
  import ""bhl1""

  func float TotalArea(IShape s)
  {
    return s.Area()
  }

  func float TotalPerimeter(IShape s)
  {
    return s.Perimeter()
  }

  func test()
  {
    IShape s = new Circle
    float a = s.Area()
  }
  ";

  TestLSPHost srv;
  DocumentUri uri1;
  DocumentUri uri2;

  public TestLSPRenameIface()
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
  public async Task rename_interface_itself()
  {
    await SendInit(srv);

    var edit = await RenameSymbol(srv, uri1, "IShape {", "IGeometry");
    var edits = FlattenEdits(edit);

    Assert.All(edits, e => Assert.Equal("IGeometry", e.newText));

    // bhl1: declaration, Circle : IShape, Square : IShape
    var e1 = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Equal(3, e1.Count);

    // bhl2: param type in TotalArea, param type in TotalPerimeter, local var "IShape s"
    var e2 = edits.FindAll(e => e.file == uri2.PathNormalized());
    Assert.Equal(3, e2.Count);
  }

  [Fact]
  public async Task rename_interface_method_via_declaration()
  {
    await SendInit(srv);

    // Rename "Area" declared in IShape — cursor on the first "Area()" in the interface body
    var edit = await RenameSymbol(srv, uri1, "Area()\n    func float Perimeter", "ComputeArea");
    var edits = FlattenEdits(edit);

    Assert.All(edits, e => Assert.Equal("ComputeArea", e.newText));

    // bhl1: only the interface declaration (class implementations are distinct symbols)
    var e1 = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Single(e1);

    // bhl2: s.Area() in TotalArea + s.Area() in test() — both through IShape-typed variable
    var e2 = edits.FindAll(e => e.file == uri2.PathNormalized());
    Assert.Equal(2, e2.Count);
  }

  [Fact]
  public async Task rename_interface_method_via_call_site()
  {
    await SendInit(srv);

    // Rename via "s.Area()" in TotalArea (bhl2) — s is IShape typed
    var edit = await RenameSymbol(srv, uri2, "Area()\n  }", "ComputeArea");
    var edits = FlattenEdits(edit);

    Assert.All(edits, e => Assert.Equal("ComputeArea", e.newText));

    // bhl1: interface declaration
    var e1 = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Single(e1);

    // bhl2: s.Area() in TotalArea + s.Area() in test()
    var e2 = edits.FindAll(e => e.file == uri2.PathNormalized());
    Assert.Equal(2, e2.Count);
  }

  [Fact]
  public async Task rename_class_method_override_independently()
  {
    await SendInit(srv);

    // Renaming Circle.Area (implementation) is independent from IShape.Area (interface decl).
    // Cursor on Circle's "Area()" — the first one after "Circle : IShape {"
    var edit = await RenameSymbol(srv, uri1, "Area() { return 0 }\n    func float Perimeter() { return 0 }\n  }\n\n  class Square", "CircleArea");
    var edits = FlattenEdits(edit);

    Assert.All(edits, e => Assert.Equal("CircleArea", e.newText));

    // Only the Circle implementation — interface decl and Square impl are different symbols
    var e1 = edits.FindAll(e => e.file == uri1.PathNormalized());
    Assert.Single(e1);

    // No edits in bhl2 (calls go through IShape, which is a different symbol)
    var e2 = edits.FindAll(e => e.file == uri2.PathNormalized());
    Assert.Empty(e2);
  }
}
