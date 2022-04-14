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
    var foo = vm.Types.Resolve("foo");
    AssertTrue(foo is Namespace);
  }

  [IsTested()]
  public void TestNamespacesCombine()
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
    var foo = vm.Types.Resolve("foo");
    AssertTrue(foo is Namespace);
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
