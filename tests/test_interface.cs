using System;
using System.IO;
using System.Text;
using bhl;

public class TestInterfaces : BHL_TestBase
{
  [IsTested()]
  public void TestEmptyUserInterface()
  {
    string bhl = @"
    interface Foo { }
    ";

    var vm = MakeVM(bhl);
    var symb = vm.Types.Resolve("Foo") as InterfaceSymbolScript;
    AssertTrue(symb != null);
  }

  [IsTested()]
  public void TestUserInterfaceWithMethod()
  {
    string bhl = @"
    interface Foo { 
      func hey(int a, float b)
    }
    ";

    var vm = MakeVM(bhl);
    var symb = vm.Types.Resolve("Foo") as InterfaceSymbolScript;
    AssertTrue(symb != null);
    var hey = symb.FindSignature("hey");
    AssertTrue(hey != null);
    AssertEqual(2, hey.arg_types.Count);
    AssertEqual(Types.Int, hey.arg_types[0].Get());
    AssertEqual(Types.Float, hey.arg_types[1].Get());
    AssertEqual(Types.Void, hey.ret_type.Get());
  }

  [IsTested()]
  public void TestUserInterfaceWithSeveralMethods()
  {
    string bhl = @"
    class Bar { 
    }

    interface Foo { 
      func bool hey(int a, float b)
      func Bar,int bar(string s)
    }
    ";

    var vm = MakeVM(bhl);
    var symb = vm.Types.Resolve("Foo") as InterfaceSymbolScript;
    AssertTrue(symb != null);

    var hey = symb.FindSignature("hey");
    AssertTrue(hey != null);
    AssertEqual(2, hey.arg_types.Count);
    AssertEqual(Types.Int, hey.arg_types[0].Get());
    AssertEqual(Types.Float, hey.arg_types[1].Get());
    AssertEqual(Types.Bool, hey.ret_type.Get());

    var bar = symb.FindSignature("bar");
    AssertTrue(bar != null);
    AssertEqual(1, bar.arg_types.Count);
    AssertEqual(Types.String, bar.arg_types[0].Get());
    var tuple = (TupleType)bar.ret_type.Get();
    AssertEqual(2, tuple.Count);
    AssertEqual("Bar", tuple[0].name);
    AssertEqual(Types.Int, tuple[1].Get());
  }

  [IsTested()]
  public void TestUserInterfaceMethodDefaultValuesNotAllowed()
  {
    string bhl = @"
    interface Foo { 
      func hey(int a, float b = 1)
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "default value is not allowed"
    );
  }

  [IsTested()]
  public void TestClassImplementsInterface()
  {
    string bhl = @"
    interface IFoo { 
      func int bar(int i)
    }

    class Foo : IFoo {
      func int bar(int i) {
        return i
      }
    }

    func int test() {
      Foo foo = {}
      return foo.bar(42)
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
  }
}
