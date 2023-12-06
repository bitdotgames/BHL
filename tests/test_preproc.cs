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

  [IsTested()]
  public void TestElse()
  {
    string bhl = @"
    func int test() 
    {
#if FOO
      return 1
#else 
      return 2
#endif
      return 3
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestElseWithDefined()
  {
    string bhl = @"
    func int test() 
    {
#if FOO
      return 1
#else 
      return 2
#endif
      return 3
    }
    ";

    var vm = MakeVM(bhl, defines: new HashSet<string>() {"FOO"});
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }
}
