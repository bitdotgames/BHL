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
