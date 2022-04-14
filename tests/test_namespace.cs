using System;
using System.IO;
using System.Text;
using bhl;

public class TestNamespace : BHL_TestBase
{
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

    var foo = vm.Types.Resolve("foo") as Namespace;
    AssertTrue(foo != null);
    AssertEqual(1, foo.members.Count);
    AssertTrue(foo.Resolve("test") is FuncSymbol);

    var bar = vm.Types.Resolve("bar") as Namespace;
    AssertTrue(bar != null);
    AssertEqual(1, bar.members.Count);
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

    var foo = vm.Types.Resolve("foo") as Namespace;
    AssertTrue(foo != null);
    AssertEqual(2, foo.members.Count);
    AssertTrue(foo.Resolve("test") is FuncSymbol);
    AssertTrue(foo.Resolve("what") is FuncSymbol);

    var bar = vm.Types.Resolve("bar") as Namespace;
    AssertTrue(bar != null);
    AssertEqual(1, bar.members.Count);
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

    var foo = vm.Types.Resolve("foo") as Namespace;
    AssertTrue(foo != null);
    AssertEqual(3, foo.members.Count);
    AssertTrue(foo.Resolve("test") is FuncSymbol);
    AssertTrue(foo.Resolve("what") is FuncSymbol);
    AssertTrue(foo.Resolve("bar") is Namespace);

    var bar = vm.Types.Resolve("bar") as Namespace;
    AssertTrue(bar != null);
    AssertEqual(2, bar.members.Count);
    AssertTrue(bar.Resolve("test") is FuncSymbol);
    AssertTrue(bar.Resolve("foo") is Namespace);
  }

  [IsTested()]
  public void TestNamespacesLink()
  {
    var ns1 = new Namespace("");
    {
      var foo = new Namespace("foo");
      ns1.Define(foo);
    }

    var ns2 = new Namespace("");
    ns2.Link(ns1);

    AssertTrue(ns2.Resolve("foo") is Namespace);
  }

  [IsTested()]
  public void TestSubNamespacesLink()
  {
    var ns1 = new Namespace("");
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

    var ns2 = new Namespace("");
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

    {
      var foo = ns2.Resolve("foo") as Namespace;
      AssertTrue(foo != null);

      var foo_sub = foo.Resolve("foo_sub") as Namespace;
      AssertTrue(foo_sub != null);

      var cl_wow = foo_sub.Resolve("Wow") as ClassSymbol;
      AssertTrue(cl_wow != null);

      var cl_hey = foo_sub.Resolve("Hey") as ClassSymbol;
      AssertTrue(cl_hey != null);

      var bar = ns2.Resolve("bar") as Namespace;
      AssertTrue(bar != null);

      var wow = ns2.Resolve("wow") as Namespace;
      AssertTrue(wow != null);

    }
  }

  //TODO:
  //[IsTested()]
  public void TestNamespacesExec()
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
    AssertEqual(1, Execute(vm, "foo.test", Val.NewNum(vm, 3)).result.PopRelease().num);
    AssertEqual(0, Execute(vm, "bar.test", Val.NewNum(vm, 3)).result.PopRelease().num);
  }
}
