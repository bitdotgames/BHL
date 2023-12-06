using System;           
using System.Text;
using System.Collections.Generic;
using bhl;

public class TestPreproc : BHL_TestBase
{
  [IsTested()]
  public void TestIfMissingDefine()
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
  public void TestIfDefineExists()
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
  public void TestIfNotMissingDefine()
  {
    string bhl = @"
    func bool test() 
    {
#if !FOO
      return false
#endif
      return true
    }
    ";

    var vm = MakeVM(bhl);
    AssertFalse(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIfNotDefineExists()
  {
    string bhl = @"
    func bool test() 
    {
#if !FOO
      return false
#endif
      return true
    }
    ";

    var vm = MakeVM(bhl, defines: new HashSet<string>() {"FOO"});
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestElseMissingDefine()
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
  public void TestElseDefineExists()
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

  [IsTested()]
  public void TestDanglingEndif()
  {
    string bhl = @"
    func test() 
    {
  #endif
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"invalid usage",
      new PlaceAssert(bhl, @"
  #endif
---^"
      )
    );
  }

  [IsTested()]
  public void TestDoubleElse()
  {
    string bhl = @"
    func test() 
    {
  #if FOO
  #else
  #else
  #endif
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"invalid usage",
      new PlaceAssert(bhl, @"
  #else
---^"
      )
    );
  }

  [IsTested()]
  public void TestNotClosedIf()
  {
    string bhl = @"
    func test() 
    {
  #if FOO
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"invalid usage",
      new PlaceAssert(bhl, @"
  #if FOO
---^"
      )
    );
  }

  [IsTested()]
  public void TestWeirdIf()
  {
    string bhl = @"
    func test() 
    {
  #if
  #endif
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"mismatched input",
      new PlaceAssert(bhl, @"
  #if
-----^"
      )
    );
  }
}
