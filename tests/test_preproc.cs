using System;           
using System.Text;
using System.Collections.Generic;
using bhl;

public class TestPreproc : BHL_TestBase
{
  [IsTested()]
  public void TestIf()
  {
    string bhl = @"
    func bool test() 
    {
#if FOO
      return false
#endif
      return true
    }
    ";

    var vm = MakeVM(bhl);
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIfDefined()
  {
    string bhl = @"
    func bool test() 
    {
#if FOO
      return false
#endif
      return true
    }
    ";

    var vm = MakeVM(bhl, defines: new HashSet<string>() {"FOO"});
    AssertFalse(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }
}
