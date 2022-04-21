using System;
using System.IO;
using System.Text;
using bhl;

public class TestNamespace : BHL_TestBase
{
  [IsTested()]
  public void TestNamespacesSimpleLink()
  {
    var ns1 = new Namespace();
    {
      var foo = new Namespace("foo");
      ns1.Define(foo);
    }

    var ns2 = new Namespace();

    /*
    {
      foo {
      }
    }
    */
    ns2.Link(ns1);

    {
      var foo = ns2.Resolve("foo") as Namespace;
      AssertTrue(foo != null);
      AssertEqual(0, foo.GetMembers().Count);
    }
  }

  [IsTested()]
  public void TestNamespacesDeepLink()
  {
    /*
    {
      foo {
        foo_sub {
          class Wow {}
        }
      }

      wow {
      }
    }
    */
    var ns1 = new Namespace();
    {
      var foo = new Namespace("foo");

      var foo_sub = new Namespace("foo_sub");

      var cl = new ClassSymbolNative("Wow", null);
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns1.Define(foo);

      var wow = new Namespace("wow");
      ns1.Define(wow);
    }

    /*
    {
      foo {
        foo_sub {
          class Hey {}
        }
      }

      bar {
      }
    }
    */
    var ns2 = new Namespace();
    {
      var foo = new Namespace("foo");

      var foo_sub = new Namespace("foo_sub");

      var cl = new ClassSymbolNative("Hey", null);
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns2.Define(foo);

      var bar = new Namespace("bar");
      ns2.Define(bar);
    }

    ns2.Link(ns1);

    /*
    {
      foo {
        foo_sub {
          class Wow {}

          class Hey {}
        }
      }

      wow {
      }

      bar {
      }
    }
    */
    {
      var foo = ns2.ResolveNoFallback("foo") as Namespace;
      AssertTrue(foo != null);
      AssertEqual(1, foo.GetMembers().Count);

      var foo_sub = foo.ResolveNoFallback("foo_sub") as Namespace;
      AssertTrue(foo_sub != null);
      AssertEqual(2, foo_sub.GetMembers().Count);

      var cl_wow = foo_sub.ResolveNoFallback("Wow") as ClassSymbol;
      AssertTrue(cl_wow != null);

      var cl_hey = foo_sub.ResolveNoFallback("Hey") as ClassSymbol;
      AssertTrue(cl_hey != null);

      var bar = ns2.ResolveNoFallback("bar") as Namespace;
      AssertTrue(bar != null);
      AssertEqual(0, bar.GetMembers().Count);

      var wow = ns2.ResolveNoFallback("wow") as Namespace;
      AssertTrue(wow != null);
      AssertEqual(0, wow.GetMembers().Count);
    }

    AssertEqual("foo", ns2.ResolvePath("foo").name);
    AssertEqual("foo_sub", ns2.ResolvePath("foo.foo_sub").name);
    AssertEqual("Hey", ns2.ResolvePath("foo.foo_sub.Hey").name);
    AssertEqual("Wow", ns2.ResolvePath("foo.foo_sub.Wow").name);
    AssertEqual("wow", ns2.ResolvePath("wow").name);
    AssertEqual("bar", ns2.ResolvePath("bar").name);

    AssertTrue(ns2.ResolvePath("") == null);
    AssertTrue(ns2.ResolvePath(".") == null);
    AssertTrue(ns2.ResolvePath("foo.") == null);
    AssertTrue(ns2.ResolvePath(".foo.") == null);
    AssertTrue(ns2.ResolvePath("foo..") == null);
    AssertTrue(ns2.ResolvePath("foo.bar") == null);
    AssertTrue(ns2.ResolvePath(".foo.foo_sub..") == null);
  }

  [IsTested()]
  public void TestNamespacesUnlink()
  {
    /*
    {
      foo {
        foo_sub {
          class Wow {}
        }
      }

      wow {
      }
    }
    */
    var ns1 = new Namespace();
    {
      var foo = new Namespace("foo");

      var foo_sub = new Namespace("foo_sub");

      var cl = new ClassSymbolNative("Wow", null);
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns1.Define(foo);

      var wow = new Namespace("wow");
      ns1.Define(wow);
    }

    /*
    {
      foo {
        foo_sub {
          class Hey {}
        }
      }

      bar {
      }
    }
    */
    var ns2 = new Namespace();
    {
      var foo = new Namespace("foo");

      var foo_sub = new Namespace("foo_sub");

      var cl = new ClassSymbolNative("Hey", null);
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns2.Define(foo);

      var bar = new Namespace("bar");
      ns2.Define(bar);
    }

    ns2.Link(ns1);

    /*
    {
      foo {
        foo_sub {
          class Wow {}

          class Hey {}
        }
      }

      wow {
      }

      bar {
      }
    }
    */

    ns2.Unlink(ns1);

    /*
    {
      foo {
        foo_sub {
          class Hey {}
        }
      }

      bar {
      }
    }
    */

    {
      var foo = ns2.ResolveNoFallback("foo") as Namespace;
      AssertTrue(foo != null);
      AssertEqual(1, foo.GetMembers().Count);

      var foo_sub = foo.ResolveNoFallback("foo_sub") as Namespace;
      AssertTrue(foo_sub != null);
      AssertEqual(1, foo_sub.GetMembers().Count);

      var cl_wow = foo_sub.ResolveNoFallback("Wow") as ClassSymbol;
      AssertTrue(cl_wow == null);

      var cl_hey = foo_sub.ResolveNoFallback("Hey") as ClassSymbol;
      AssertTrue(cl_hey != null);

      var bar = ns2.ResolveNoFallback("bar") as Namespace;
      AssertTrue(bar != null);

      var wow = ns2.ResolveNoFallback("wow") as Namespace;
      AssertTrue(wow == null);
    }
  }

  [IsTested()]
  public void TestNamespacesUnlinkPreserveChangedStuff()
  {
    /*
    {
      foo {
        foo_sub {
          class Wow {}
        }
      }

      wow {
      }
    }
    */
    var ns1 = new Namespace();
    {
      var foo = new Namespace("foo");

      var foo_sub = new Namespace("foo_sub");

      var cl = new ClassSymbolNative("Wow", null);
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns1.Define(foo);

      var wow = new Namespace("wow");
      ns1.Define(wow);
    }

    /*
    {
      foo {
        foo_sub {
          class Hey {}
        }
      }

      bar {
      }
    }
    */
    var ns2 = new Namespace();
    {
      var foo = new Namespace("foo");

      var foo_sub = new Namespace("foo_sub");

      var cl = new ClassSymbolNative("Hey", null);
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns2.Define(foo);

      var bar = new Namespace("bar");
      ns2.Define(bar);
    }

    ns2.Link(ns1);

    /*
    {
      foo {
        foo_sub {
          class Wow {}

          class Hey {}
        }
      }

      wow {
      }

      bar {
      }
    }
    */

    (ns2.ResolveNoFallback("wow") as Namespace).Define(new Namespace("wow_sub"));
    ns2.Unlink(ns1);

    /*
    {
      foo {
        foo_sub {
          class Hey {}
        }
      }

      wow {
        wow_sub {
        }
      }

      bar {
      }
    }
    */

    {
      var foo = ns2.ResolveNoFallback("foo") as Namespace;
      AssertTrue(foo != null);
      AssertEqual(1, foo.GetMembers().Count);

      var foo_sub = foo.ResolveNoFallback("foo_sub") as Namespace;
      AssertTrue(foo_sub != null);
      AssertEqual(1, foo_sub.GetMembers().Count);

      var cl_wow = foo_sub.ResolveNoFallback("Wow") as ClassSymbol;
      AssertTrue(cl_wow == null);

      var cl_hey = foo_sub.ResolveNoFallback("Hey") as ClassSymbol;
      AssertTrue(cl_hey != null);

      var bar = ns2.ResolveNoFallback("bar") as Namespace;
      AssertTrue(bar != null);

      var wow = ns2.ResolveNoFallback("wow") as Namespace;
      AssertTrue(wow != null);

      AssertTrue(wow.ResolveNoFallback("wow_sub") is Namespace);
    }
  }

  [IsTested()]
  public void TestNamespacesLinkConflict()
  {
    /*
    {
      foo {
        foo_sub {
          class Wow {}
        }
      }

      wow {
      }
    }
    */
    var ns1 = new Namespace();
    {
      var foo = new Namespace("foo");

      var foo_sub = new Namespace("foo_sub");

      var cl = new ClassSymbolNative("Wow", null);
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns1.Define(foo);

      var wow = new Namespace("wow");
      ns1.Define(wow);
    }

    /*
    {
      foo {
        foo_sub {
          class Wow {}
        }
      }
    }
    */
    var ns2 = new Namespace();
    {
      var foo = new Namespace("foo");

      var foo_sub = new Namespace("foo_sub");

      var cl = new ClassSymbolNative("Wow", null);
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns2.Define(foo);
    }

    var conflict_symb = ns2.TryLink(ns1);
    AssertEqual("Wow", conflict_symb.name);

  }

  [IsTested()]
  public void TestNamespacesDecl()
  {
    string bhl = @"
    namespace foo {
      func bool test()
      {
        return true
      }
    }

    namespace bar {
      func bool test()
      {
        return false
      }
    }
    ";

    var vm = MakeVM(bhl);

    var foo = vm.Types.ns.Resolve("foo") as Namespace;
    AssertTrue(foo != null);
    AssertEqual(1, foo.GetMembers().Count);
    AssertTrue(foo.Resolve("test") is FuncSymbol);

    var bar = vm.Types.ns.Resolve("bar") as Namespace;
    AssertTrue(bar != null);
    AssertEqual(1, bar.GetMembers().Count);
    AssertTrue(foo.Resolve("test") is FuncSymbol);
  }

  [IsTested()]
  public void TestNamespacesPartialDecls()
  {
    string bhl = @"
    namespace foo {
      func bool test()
      {
        return true
      }
    }

    namespace bar {
      func bool test()
      {
        return false
      }
    }

    namespace foo {
      func bool what()
      {
        return false
      }
    }
    ";

    var vm = MakeVM(bhl);

    var foo = vm.Types.ns.Resolve("foo") as Namespace;
    AssertTrue(foo != null);
    AssertEqual(2, foo.GetMembers().Count);
    AssertTrue(foo.Resolve("test") is FuncSymbol);
    AssertTrue(foo.Resolve("what") is FuncSymbol);

    var bar = vm.Types.ns.Resolve("bar") as Namespace;
    AssertTrue(bar != null);
    AssertEqual(1, bar.GetMembers().Count);
    AssertTrue(bar.Resolve("test") is FuncSymbol);
  }

  [IsTested()]
  public void TestSubNamespaces()
  {
    string bhl = @"
    namespace foo {
      func bool test()
      {
        return true
      }
    }

    namespace bar {
      func bool test()
      {
        return false
      }

      namespace foo {
        func bool test()
        {
          return true
        }
      }
    }

    namespace foo {
      func bool what()
      {
        return false
      }

      namespace bar {
        func bool test()
        {
          return true
        }
      }
    }
    ";

    var vm = MakeVM(bhl);

    var foo = vm.Types.ns.Resolve("foo") as Namespace;
    AssertTrue(foo != null);
    AssertEqual(3, foo.GetMembers().Count);
    AssertTrue(foo.Resolve("test") is FuncSymbol);
    AssertTrue(foo.Resolve("what") is FuncSymbol);
    AssertTrue(foo.Resolve("bar") is Namespace);

    var bar = vm.Types.ns.Resolve("bar") as Namespace;
    AssertTrue(bar != null);
    AssertEqual(2, bar.GetMembers().Count);
    AssertTrue(bar.Resolve("test") is FuncSymbol);
    AssertTrue(bar.Resolve("foo") is Namespace);
  }

  [IsTested()]
  public void TestNamespacesFuncStart()
  {
    string bhl = @"
    namespace foo {
      func bool test()
      {
        return true
      }
    }

    namespace bar {
      func bool test()
      {
        return false
      }
    }
    ";
    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "foo.test").result.PopRelease().num);
    AssertEqual(0, Execute(vm, "bar.test").result.PopRelease().num);
  }

  [IsTested()]
  public void TestNamespacesFuncCall()
  {
    {
      string bhl = @"
      namespace foo {
        func bool test() {
          return true
        }
      }

      namespace bar {
        func bool test() {
          return foo.test()
        }
      }
      ";
      var vm = MakeVM(bhl);
      AssertEqual(1, Execute(vm, "bar.test").result.PopRelease().num);
    }

    {
      string bhl = @"
      namespace foo {
        func int wow() {
          return 1
        }
      }

      namespace bar {
        func int wow() {
          return 10
        }

        func int test() {
          return foo.wow() + wow()
        }
      }
      ";
      var vm = MakeVM(bhl);
      AssertEqual(11, Execute(vm, "bar.test").result.PopRelease().num);
    }
  }
}
