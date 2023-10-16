using System;
using System.Collections.Generic;
using System.IO;
using bhl;

public class TestInit : BHL_TestBase
{
  //TODO:
  //[IsTested()]
  public void TestAutoCallInitFunc()
  {
    string bhl = @"
    int foo = 0

    static func init()
    {
      foo = 10
    }

    func int test() 
    {
      return foo
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 10);
    CommonChecks(vm);
  }

}
