using System;
using System.Text;
using bhl;
using Xunit;

public class TestFuncPtrs : BHL_TestBase
{
  [Fact]
  public void TestFuncPtr()
  {
    string bhl = @"
    func int foo()
    {
      return 1
    }

    func int test()
    {
      func int() ptr = foo
      return ptr()
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(1, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncPtrWithArgs()
  {
    string bhl = @"
    func int foo(int a, int b)
    {
      return a - b
    }

    func int test()
    {
      func int(int, int) ptr = foo
      return ptr(42, 1)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(41, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeFuncPtrWithArgs()
  {
    string bhl = @"

    func test()
    {
      func(string) ptr = trace
      ptr(""Hey"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("Hey", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtr()
  {
    string bhl = @"
    func bool foo(int a, string k)
    {
      trace(k)
      return a > 2
    }

    func bool test(int a)
    {
      func bool(int,string) ptr = foo
      return ptr(a, ""HEY"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Assert.True(Execute(vm, "test", 3).Stack.Pop().bval);
    Assert.Equal("HEY", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtrSeveralTimes()
  {
    string bhl = @"
    func bool foo(int a, string k)
    {
      trace(k)
      return a > 2
    }

    func bool test(int a)
    {
      func bool(int,string) ptr = foo
      return ptr(a, ""HEY"") && ptr(a-1, ""BAR"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Assert.False(Execute(vm, "test", 3).Stack.Pop().bval);
    Assert.Equal("HEYBAR", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtrSeveralTimes2()
  {
    string bhl = @"
    func int foo(int a)
    {
      func int(int) p =
        func int (int a) {
          return a * 2
        }

      return p(a)
    }

    func int test(int a)
    {
      return foo(a) + foo(a+1)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(14, Execute(vm, "test", 3).Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtrSeveralTimes3()
  {
    string bhl = @"
    func int foo(int a)
    {
      func int(int) p =
        func int (int a) {
          return a
        }

      return p(a)
    }

    func int test(int a)
    {
      return foo(a) + foo(a)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(6, Execute(vm, "test", 3).Stack.Pop().num);
    Assert.Equal(8, Execute(vm, "test", 4).Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtrSeveralTimes4()
  {
    string bhl = @"
    func int foo(int a)
    {
      func int(int) p =
        func int (int a) {
          return a
        }

      int tmp = p(a)

      p = func int (int a) {
          return a * 2
      }

      return tmp + p(a)
    }

    func int test(int a)
    {
      return foo(a) + foo(a+1)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(9 + 12, Execute(vm, "test", 3).Stack.Pop().num);
    Assert.Equal(12 + 15, Execute(vm, "test", 4).Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtrSeveralTimes5()
  {
    string bhl = @"
    func int foo(func int(int) p, int a)
    {
      return p(a)
    }

    func int test(int a)
    {
      return foo(func int(int a) { return a }, a) +
             foo(func int(int a) { return a * 2 }, a + 1)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(3 + 8, Execute(vm, "test", 3).Stack.Pop().num);
    Assert.Equal(4 + 10, Execute(vm, "test", 4).Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestArrayOfComplexFuncPtrs()
  {
    string bhl = @"
    func int test(int a)
    {
      []func int(int,string) ptrs = new []func int(int,string)
      ptrs.Add(func int(int a, string b) {
          trace(b)
          return a*2
      })
      ptrs.Add(func int(int a, string b) {
          trace(b)
          return a*10
      })

      return ptrs[0](a, ""what"") + ptrs[1](a, ""hey"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Assert.Equal(3 * 2 + 3 * 10, Execute(vm, "test", 3).Stack.Pop().num);
    Assert.Equal("whathey", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnComplexFuncPtr()
  {
    string bhl = @"
    func func bool(int) foo()
    {
      return func bool(int a) { return a > 2 }
    }

    func bool test(int a)
    {
      func bool(int) ptr = foo()
      return ptr(a)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.True(Execute(vm, "test", 3).Stack.Pop().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnAndCallComplexFuncPtr()
  {
    string bhl = @"
    func func bool(int) foo()
    {
      return func bool(int a) { return a > 2 }
    }

    func bool test(int a)
    {
      return foo()(a)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.True(Execute(vm, "test", 3).Stack.Pop().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtrPassRef()
  {
    string bhl = @"
    func void foo(int a, string k, ref bool res)
    {
      trace(k)
      res = a > 2
    }

    func bool test(int a)
    {
      func(int,string, ref bool) ptr = foo
      bool res = false
      ptr(a, ""HEY"", ref res)
      return res
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Assert.True(Execute(vm, "test", 3).Stack.Pop().bval);
    Assert.Equal("HEY", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtrLambda()
  {
    string bhl = @"
    func bool test(int a)
    {
      func bool(int,string) ptr =
        func bool (int a, string k)
        {
          trace(k)
          return a > 2
        }
      return ptr(a, ""HEY"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Assert.True(Execute(vm, "test", 3).Stack.Pop().bval);
    Assert.Equal("HEY", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtrLambdaInALoop()
  {
    string bhl = @"
    func test()
    {
      func(int) ptr =
        func (int a)
        {
          trace((string)a)
        }
      int i = 0
      while(i < 5)
      {
        ptr(i)
        i = i + 1
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test", 3);
    Assert.Equal("01234", log.ToString());
    Assert.Equal(2, vm.last_fiber.exec.frames_count);
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtrIncompatibleRetType()
  {
    string bhl = @"
    func void foo(int a) { }

    func void test()
    {
      func bool(int) ptr = foo
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "incompatible types: 'func bool(int)' and 'func void(int)'",
      new PlaceAssert(bhl, @"
      func bool(int) ptr = foo
---------------------^"
      )
    );
  }

  [Fact]
  public void TestComplexFuncPtrArgTypeCheck()
  {
    string bhl = @"
    func foo(int a) { }

    func test()
    {
      func(float) ptr = foo
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "incompatible types: 'func void(float)' and 'func void(int)'",
      new PlaceAssert(bhl, @"
      func(float) ptr = foo
------------------^"
      )
    );
  }

  [Fact]
  public void TestComplexFuncPtrCallArgTypeCheck()
  {
    string bhl = @"
    func foo(int a) { }

    func test()
    {
      func(int) ptr = foo
      ptr(""hey"")
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "incompatible types: 'int' and 'string'",
      new PlaceAssert(bhl, @"
      ptr(""hey"")
----------^"
      )
    );
  }

  [Fact]
  public void TestComplexFuncPtrCallArgRefTypeCheck()
  {
    string bhl = @"
    func foo(int a, ref  float b) { }

    func test()
    {
      float b = 1
      func(int, ref float) ptr = foo
      ptr(10, b)
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "'ref' is missing",
      new PlaceAssert(bhl, @"
      ptr(10, b)
--------------^"
      )
    );
  }

  [Fact]
  public void TestComplexFuncPtrCallArgRefTypeCheck2()
  {
    string bhl = @"
    func foo(int a, ref float b) { }

    func test()
    {
      float b = 1
      func(int, float) ptr = foo
      ptr(10, ref b)
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "incompatible types: 'func void(int,float)' and 'func void(int,ref float)'",
      new PlaceAssert(bhl, @"
      func(int, float) ptr = foo
-----------------------^"
      )
    );
  }

  [Fact]
  public void TestComplexFuncPtrPassConflictingRef()
  {
    string bhl = @"
    func void foo(int a, string k, refbool res)
    {
      trace(k)
    }

    func bool test(int a)
    {
      func(int,string,ref bool) ptr = foo
      bool res = false
      ptr(a, ""HEY"", ref res)
      return res
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      {
        var cl = new ClassSymbolNative(new Origin(), "refbool",
          delegate(VM.ExecState exec, ref Val v, IType type) { }
        );
        ts.ns.Define(cl);
      }

      BindTrace(ts, log);
    });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      "incompatible types: 'func void(int,string,ref bool)' and 'func void(int,string,refbool)'",
      new PlaceAssert(bhl, @"
      func(int,string,ref bool) ptr = foo
--------------------------------^"
      )
    );
  }

  [Fact]
  public void TestComplexFuncPtrCallArgNotEnoughArgsCheck()
  {
    string bhl = @"
    func foo(int a) { }

    func test()
    {
      func(int) ptr = foo
      ptr()
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "missing argument of type 'int'",
      new PlaceAssert(bhl, @"
      ptr()
---------^"
      )
    );
  }

  [Fact]
  public void TestComplexFuncPtrCallArgNotEnoughArgsCheck2()
  {
    string bhl = @"
    func foo(int a, float b) { }

    func test()
    {
      func(int, float) ptr = foo
      ptr(10)
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "missing argument of type 'float'",
      new PlaceAssert(bhl, @"
      ptr(10)
---------^"
      )
    );
  }

  [Fact]
  public void TestComplexFuncPtrCallArgTooManyArgsCheck()
  {
    string bhl = @"
    func foo(int a) { }

    func test()
    {
      func(int) ptr = foo
      ptr(10, 30)
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "too many arguments",
      new PlaceAssert(bhl, @"
      ptr(10, 30)
---------^"
      )
    );
  }

  [Fact]
  public void TestFuncPtrWithDeclPassedAsArg()
  {
    string bhl = @"

    func foo(func() fn)
    {
      fn()
    }

    func hey()
    {
      float foo
      trace(""HERE"")
    }

    func test()
    {
      foo(hey)
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test", 3);
    Assert.Equal("HERE", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncPtrForNativeFunc()
  {
    string bhl = @"
    func test()
    {
      func() ptr = foo
      ptr()
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      {
        var fn = new FuncSymbolNative(new Origin(), "foo", Types.Void,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            log.Append("FOO");
            return null;
          }
        );
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("FOO", log.ToString());
    CommonChecks(vm);
  }


}
