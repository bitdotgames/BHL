using System;
using System.Collections.Generic;
using System.IO;
using bhl;

public class TestInit : BHL_TestBase
{
  [IsTested()]
  public void TestAutoCallInitFunc()
  {
    string bhl = @"
    static int foo = 0

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

  [IsTested()]
  public void TestInitFuncCantBeCoro()
  {
    string bhl = @"
    static coro func init()
    {}
    ";

    AssertError<Exception>(
      delegate() { 
        MakeVM(bhl);
      },
      "module 'init' function can't be a coroutine"
    );
  }

  [IsTested()]
  public void TestInitFuncCantHaveArgs()
  {
    string bhl = @"
    static func init(int a)
    {}
    ";

    AssertError<Exception>(
      delegate() { 
        MakeVM(bhl);
      },
      "module 'init' function can't have any arguments"
    );
  }

  [IsTested()]
  public void TestInitFuncCantHaveReturnValue()
  {
    string bhl = @"
    static func int init()
    {
      return 1;
    }
    ";

    AssertError<Exception>(
      delegate() { 
        MakeVM(bhl);
      },
      "module 'init' function must be void"
    );
  }
}
