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
    var symb = vm.Types.Resolve("Foo");
    AssertTrue(symb != null);
  }
}
