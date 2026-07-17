using System;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

public class TestLSPHover : TestLSPShared, IDisposable
{
  string bhl1 = @"
  func float test1(float k = 0, float n = 1)
  {
    return k //return k
  }

  func test2()
  {
    test1() //hover 1
  }
  ";

  TestLSPHost srv;

  DocumentUri uri;

  public TestLSPHover()
  {
    srv = NewTestServer();

    CleanTestFiles();

    uri = MakeTestDocument("bhl1.bhl", bhl1);
  }

  public void Dispose()
  {
    srv.Dispose();
  }

  [Fact]
  public async Task _1()
  {
    await SendInit(srv);

    Assert.Equal(
      new Hover() { Contents = new (new MarkupContent { Kind = MarkupKind.PlainText, Value = "func float test1(float k,float n)"})},
      await GetHover(srv, uri, "est1() //hover 1")
    );
  }

  [Fact]
  public async Task _2()
  {
    await SendInit(srv);

    Assert.Equal(
      new Hover() { Contents = new (new MarkupContent { Kind = MarkupKind.PlainText, Value = "float k"})},
      await GetHover(srv, uri, "k //return k")
    );
  }
}

public class TestLSPHoverNamespaceEnumClass : TestLSPShared, IDisposable
{
  string bhl1 = @"
  enum EnumUnit
  {
    Foo = 1
  }

  namespace Unit
  {
    namespace View
    {
      func void Death() { }
    }

    class Weapon
    {
      static func void Fire() { }
    }
  }

  func test()
  {
    Unit.View.Death() //hover ns root
    Unit.View.Death() //hover ns nested
    Unit.Weapon.Fire() //hover class qualifier
    EnumUnit e = EnumUnit.Foo //hover enum qualifier
  }
  ";

  TestLSPHost srv;

  DocumentUri uri;

  public TestLSPHoverNamespaceEnumClass()
  {
    srv = NewTestServer();

    CleanTestFiles();

    uri = MakeTestDocument("bhl1.bhl", bhl1);
  }

  public void Dispose()
  {
    srv.Dispose();
  }

  [Fact]
  public async Task hover_on_namespace_root()
  {
    await SendInit(srv);

    Assert.Equal(
      new Hover() { Contents = new (new MarkupContent { Kind = MarkupKind.PlainText, Value = "namespace Unit"})},
      await GetHover(srv, uri, "Unit.View.Death() //hover ns root")
    );
  }

  [Fact]
  public async Task hover_on_nested_namespace()
  {
    await SendInit(srv);

    Assert.Equal(
      new Hover() { Contents = new (new MarkupContent { Kind = MarkupKind.PlainText, Value = "namespace View"})},
      await GetHover(srv, uri, "View.Death() //hover ns nested")
    );
  }

  [Fact]
  public async Task hover_on_class_qualifier()
  {
    await SendInit(srv);

    Assert.Equal(
      new Hover() { Contents = new (new MarkupContent { Kind = MarkupKind.PlainText, Value = "class Weapon"})},
      await GetHover(srv, uri, "Weapon.Fire() //hover class qualifier")
    );
  }

  [Fact]
  public async Task hover_on_enum_qualifier()
  {
    await SendInit(srv);

    Assert.Equal(
      new Hover() { Contents = new (new MarkupContent { Kind = MarkupKind.PlainText, Value = "enum EnumUnit"})},
      await GetHover(srv, uri, "EnumUnit.Foo //hover enum qualifier")
    );
  }
}

// Regression: a namespace resolved through an import link gets a "linked shadow" Namespace
// instance whose ToString() appends " -> N" (an internal import re-export depth marker).
// Hover must show the plain name regardless.
public class TestLSPHoverImportedNamespace : TestLSPShared, IDisposable
{
  string bhl_ns = @"
  namespace Foo
  {
    func void Bar() { }
  }
  ";

  string bhl_use = @"
  import ""bhl_ns""

  func test()
  {
    Foo.Bar() //hover imported ns
  }
  ";

  TestLSPHost srv;

  DocumentUri uri;

  public TestLSPHoverImportedNamespace()
  {
    srv = NewTestServer();

    CleanTestFiles();

    MakeTestDocument("bhl_ns.bhl", bhl_ns);
    uri = MakeTestDocument("bhl_use.bhl", bhl_use);
  }

  public void Dispose()
  {
    srv.Dispose();
  }

  [Fact]
  public async Task hover_on_imported_namespace_has_no_indirectness_marker()
  {
    await SendInit(srv);

    Assert.Equal(
      new Hover() { Contents = new (new MarkupContent { Kind = MarkupKind.PlainText, Value = "namespace Foo"})},
      await GetHover(srv, uri, "Foo.Bar() //hover imported ns")
    );
  }
}
