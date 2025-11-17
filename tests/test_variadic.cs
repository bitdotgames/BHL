using System;
using System.Text;
using bhl;
using Xunit;

public class TestVariadic : BHL_TestBase
{
  [Fact]
  public void TestSimpleCount()
  {
    string bhl = @"
    func int count(...[]int ns) {
      return ns.Count
    }

    func int test() {
      return count(1, 2, 3)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(3, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
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

    func int test() {
      return sum(1, 2, 3)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(6, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNoArgs()
  {
    string bhl = @"
    func int sum(...[]int ns) {
      int s = 0
      foreach(int n in ns) {
        s += n
      }
      return s
    }

    func int test() {
      return sum()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(0, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
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
      return sum(3, /*variadics*/1, 2, 3)
    }

    func int test2() {
      return sum(3)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(3 * 1 + 3 * 2 + 3 * 3, Execute(vm, "test1").Stack.Pop().num);
    Assert.Equal(0, Execute(vm, "test2").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCombineWithDefaultArgSimpleCount()
  {
    string bhl = @"
    func int foo(int k = 10, ...[]int ns) {
      return ns.Count
    }

    func int test() {
      return foo(1, /*variadics*/20, 30)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(2, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCombineWithDefaultArgSimpleCountNoArgs()
  {
    string bhl = @"
    func int foo(int k = 10, ...[]int ns) {
      return ns.Count
    }

    func int test() {
      return foo()
    }
    ";

    var vm = MakeVM(bhl, show_bytes: true);
    Assert.Equal(0, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
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
      return sum(30, /*variadics*/1, 2, 3)
    }

    func int test2() {
      return sum()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(30 + 1 + 2 + 3, Execute(vm, "test1").Stack.Pop().num);
    Assert.Equal(10, Execute(vm, "test2").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassArrayAsVariadicArg()
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
      return sum(...[1, 2, 3])
    }

    func int test2() {
      return sum(...[])
    }

    func int test3() {
      []int ns = [1, 2, 3]
      return sum(...ns)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(6, Execute(vm, "test1").Stack.Pop().num);
    Assert.Equal(0, Execute(vm, "test2").Stack.Pop().num);
    Assert.Equal(6, Execute(vm, "test3").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
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
        delegate() { Compile(bhl); },
        "incompatible types",
        new PlaceAssert(bhl, @"
        sum(1, ""foo"", 2)
---------------^"
        )
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
        delegate() { Compile(bhl); },
        "incompatible types",
        new PlaceAssert(bhl, @"
        sum(""foo"", 2)
------------^"
        )
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
        delegate() { Compile(bhl); },
        "incompatible types",
        new PlaceAssert(bhl, @"
        sum(1, ""foo"")
---------------^"
        )
      );
    }
  }

  [Fact]
  public void TestDefaultArgIsNotAllowed()
  {
    string bhl = @"
    func sum(...[]int ns = [1, 2]) {
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "default argument is not allowed",
      new PlaceAssert(bhl, @"
    func sum(...[]int ns = [1, 2]) {
-------------------------^"
      )
    );
  }

  [Fact]
  public void TestMustBeLast()
  {
    string bhl = @"
    func sum(...[]int ns, int a) {
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "variadic argument must be last",
      new PlaceAssert(bhl, @"
    func sum(...[]int ns, int a) {
-------------^"
      )
    );
  }

  [Fact]
  public void TestNoRefAllowed()
  {
    string bhl = @"
    func sum(ref ...[]int ns) {
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "pass by 'ref' not allowed",
      new PlaceAssert(bhl, @"
    func sum(ref ...[]int ns) {
-------------^"
      )
    );
  }

  [Fact]
  public void TestNoRefAllowed2()
  {
    string bhl = @"
     func sum(...[]ref int ns) {
     }
     ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "extraneous input 'ref'",
      new PlaceAssert(bhl, @"
     func sum(...[]ref int ns) {
-------------------^"
      )
    );
  }

  [Fact]
  public void TestInvalidVariadicUnpacking()
  {
    {
      string bhl = @"
      func sum(int n, ...[]int ns){}

      func test() {
        sum(...1)
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "not variadic argument",
        new PlaceAssert(bhl, @"
        sum(...1)
------------^"
        )
      );
    }

    {
      string bhl = @"
      func sum(int n, ...[]int ns){}

      func test() {
        sum(...[])
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "not variadic argument",
        new PlaceAssert(bhl, @"
        sum(...[])
------------^"
        )
      );
    }

    {
      string bhl = @"
      func sum(int n, ...[]int ns){}

      func test() {
        sum(0, ...1)
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "incompatible types",
        new PlaceAssert(bhl, @"
        sum(0, ...1)
---------------^"
        )
      );
    }
  }

  [Fact]
  public void TestInterleaveValuesStackInParalAll()
  {
    string bhl = @"
    func foo(...[]int ns) {
      trace((string)ns[0] + "" "" + (string)ns[1] + "";"")
    }

    coro func int ret_int(int val, int ticks) {
      while(ticks > 0) {
        yield()
        ticks = ticks - 1
      }
      return val
    }

    coro func test()
    {
      paral_all {
        foo(1, yield ret_int(val: 2, ticks: 1))
        foo(10, yield ret_int(val: 20, ticks: 2))
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("1 2;10 20;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeBinding()
  {
    string bhl = @"
    func int test1() {
      return sum(1, 2, 3)
    }

    func int test2() {
      return sum()
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      var fn = new FuncSymbolNative(new Origin(), "sum", FuncAttrib.VariadicArgs, Types.Int, 0,
        (VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          var ns = exec.stack.Pop();
          var vs = (ValList)ns.obj;
          int sum = 0;
          for(int i = 0; i < vs.Count; ++i)
            sum += (int)vs[i].num;
          vs.Release();
          exec.stack.Push(sum);
          return null;
        },
        new FuncArgSymbol("ns", ts.TArr("int"))
      );
      ts.ns.Define(fn);
    });

    var vm = MakeVM(bhl, ts_fn);
    Assert.Equal(6, Execute(vm, "test1").Stack.Pop().num);
    Assert.Equal(0, Execute(vm, "test2").Stack.Pop().num);
    CommonChecks(vm);
  }
}
