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
}
