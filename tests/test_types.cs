using System;
using System.IO;
using System.Text;
using bhl;

public class TestTypes : BHL_TestBase
{
  //[IsTested()]
  public void TestSimpleAs()
  {
    string bhl = @"
    interface IBar 
    {
      func int bar()
    }

    interface IFoo
    {
      func int foo()
    }

    interface IHey
    {
      func int hey()
    }

    class Wow : IBar, IFoo
    {
      func int bar() {
        return 42
      }

      func int foo() {
        return 24
      }
    }

    func int test() 
    {
      Wow w = {}
      int summ
      IBar b = w as IBar
      if(b != null) {
        summ = summ + b.bar()
      }
      IFoo f = w as IFoo
      if(f != null) {
        summ = summ + f.foo()
      }
      IHey h = w as IHey
      if(h != null) {
        summ = summ + h.hey()
      }
      return summ
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(42+24, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }
}
