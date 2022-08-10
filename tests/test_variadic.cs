using System;           
using System.IO;
using System.Text;
using System.Collections.Generic;
using bhl;

public class TestVariadic : BHL_TestBase
{
  [IsTested()]
  public void TestSimpleUsage()
  {
    string bhl = @"
    func int sum(...[]int ns) {
      int s = 0
      foreach(int n in ns) {
        s += n
      }
      return s
    }

    func int test1() {
      return sum(1, 2, 3)
    }

    func int test2() {
      return sum()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(6, Execute(vm, "test1").result.PopRelease().num);
    AssertEqual(0, Execute(vm, "test2").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCombineWithOtherArgs()
  {
    string bhl = @"
    func int sum(int k, ...[]int ns) {
      int s = 0
      foreach(int n in ns) {
        s += k*n
      }
      return s
    }

    func int test1() {
      return sum(3, 1, 2, 3)
    }

    func int test2() {
      return sum(3)
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(3*1 + 3*2 + 3*3, Execute(vm, "test1").result.PopRelease().num);
    AssertEqual(0, Execute(vm, "test2").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCombineWithOtherDefaultArg()
  {
    string bhl = @"
    func int sum(int k = 10, ...[]int ns) {
      int s = k
      foreach(int n in ns) {
        s += n
      }
      return s
    }

    func int test1() {
      return sum(30, 1, 2, 3)
    }

    func int test2() {
      return sum()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(30+1+2+3, Execute(vm, "test1").result.PopRelease().num);
    AssertEqual(10, Execute(vm, "test2").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTypeMismatch()
  {
    {
      string bhl = @"
      func sum(...[]int ns) {
      }

      func test() {
        sum(1, ""foo"", 2)
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "incompatible types"
      );
    }

    {
      string bhl = @"
      func sum(...[]int ns) {
      }

      func test() {
        sum(""foo"", 2)
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "incompatible types"
      );
    }

    {
      string bhl = @"
      func sum(...[]int ns) {
      }

      func test() {
        sum(1, ""foo"")
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "incompatible types"
      );
    }
  }

  [IsTested()]
  public void TestDefaultArgIsNotAllowed()
  {
    string bhl = @"
    func sum(...[]int ns = [1, 2]) {
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "default argument is not allowed"
    );
  }

  [IsTested()]
  public void TestMustBeLast()
  {
    string bhl = @"
    func sum(...[]int ns, int a) {
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "variadic argument must be last"
    );
  }
}
