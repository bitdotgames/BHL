using System;
using bhl;
using Xunit;

public class TestYield : BHL_TestBase
{
  [Fact]
  public void TestFuncWithYieldMustBeCoro()
  {
    string bhl = @"
    func test() {
      yield()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "function with yield calls must be coro",
      new PlaceAssert(bhl, @"
    func test() {
----^"
      )
    );
  }

  [Fact]
  public void TestEmptyCoroFuncNotAllowed()
  {
    {
      string bhl = @"
      coro func test() {
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "coro functions without yield calls not allowed",
        new PlaceAssert(bhl, @"
      coro func test() {
------^"
        )
      );
    }

    {
      string bhl = @"
      coro func test() {
        int a = 10
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "coro functions without yield calls not allowed",
        new PlaceAssert(bhl, @"
      coro func test() {
------^"
        )
      );
    }

    {
      string bhl = @"
      coro func test() {
        start(coro func() {
            yield()
        })
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "coro functions without yield calls not allowed",
        new PlaceAssert(bhl, @"
      coro func test() {
------^"
        )
      );
    }

    {
      string bhl = @"
      func test() {
        start(coro func() {
        })
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "coro functions without yield calls not allowed",
        new PlaceAssert(bhl, @"
        start(coro func() {
--------------^"
        )
      );
    }
  }

  [Fact]
  public void TestBasicYield()
  {
    string bhl = @"
    coro func test()
    {
      yield()
    }
    ";

    var vm = MakeVM(bhl);
    vm.Start("test");
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    CommonChecks(vm);
  }

  [Fact]
  public void TestMethodYield()
  {
    string bhl = @"
    class Foo {
      coro func bar() {
        yield()
      }
    }
    coro func test()
    {
      var foo = new Foo
      yield foo.bar()
    }
    ";

    var vm = MakeVM(bhl);
    vm.Start("test");
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    CommonChecks(vm);
  }

  [Fact]
  public void TestMethodBaseYield()
  {
    string bhl = @"
    class FooBase {
      coro func foo() {
        yield()
      }
    }

    class Foo : FooBase {
      coro func bar() {
        yield base.foo()
      }
    }
    coro func test()
    {
      var foo = new Foo
      yield foo.bar()
    }
    ";

    var vm = MakeVM(bhl);
    vm.Start("test");
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    CommonChecks(vm);
  }

  [Fact]
  public void TestInterfaceYield()
  {
    string bhl = @"
    interface IFoo {
      coro func Doer()
    }

    class Foo : IFoo {
      coro func Doer() {
        yield()
      }
    }
    coro func test()
    {
      var foo = new Foo
      yield foo.Doer()
    }
    ";

    var vm = MakeVM(bhl);
    vm.Start("test");
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    CommonChecks(vm);
  }

  [Fact]
  public void TestInterfaceImplentingCoroAsRegularMethod()
  {
    string bhl = @"
    interface IFoo {
      coro func int Doer()
    }

    class Foo : IFoo {
      func int Doer() {
        return 42
      }
    }

    func int test() {
      var foo = new Foo
      return foo.Doer()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestInterfaceImplentingCoro()
  {
    string bhl = @"
    interface IFoo {
      coro func int Doer()
    }

    class Foo : IFoo {
      func int Doer() {
        return 42
      }
    }

    coro func int test() {
      IFoo ifoo = new Foo
      return yield ifoo.Doer()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestInterfaceParentOverride()
  {
    string bhl = @"
    interface IFoo {
      coro func int Doer()
    }

    class Foo : IFoo {
      coro virtual func int Doer() {
        yield()
        return 42
      }
    }

    class SubFoo : Foo {
      coro override func int Doer() {
        return 10 + yield base.Doer()
      }
    }

    coro func int test() {
      var o = new SubFoo
      return yield o.Doer()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(52, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestInterfaceParentOverrideError()
  {
    string bhl = @"
    interface IFoo {
      coro func int Doer()
    }

    class Foo : IFoo {
      virtual func int Doer() {
        return 42
      }
    }

    class SubFoo : Foo {
      coro override func int Doer() {
        yield()
        return 10 + base.Doer()
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "base: 'func int()', override: 'coro func int()'",
      new PlaceAssert(bhl, @"
      coro override func int Doer() {
------^"
      )
    );
  }

  [Fact]
  public void TestInterfaceParentSyncOverrideCoroIsOk()
  {
    string bhl = @"
    interface IFoo {
      coro func int Doer()
    }

    class Foo : IFoo {
      coro virtual func int Doer() {
        yield()
        return 42
      }
    }

    class SubFoo : Foo {
      override func int Doer() {
        return 10
      }
    }

    func int test() {
      var o = new SubFoo
      return o.Doer()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSuspend()
  {
    string bhl = @"
    coro func test()
    {
      yield suspend()
    }
    ";

    var c = Compile(bhl);

    var ts = new Types();

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.CallGlobNative, new int[] { ts.module.nfunc_index.IndexOf("suspend"), 0 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    for(int i=0;i<99;i++)
      Assert.True(vm.Tick());
    vm.Stop(fb);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCoroFuncMustBeCalledWithYield()
  {
    string bhl = @"
    func test() {
      suspend()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "coro function must be called via yield",
      new PlaceAssert(bhl, @"
      suspend()
------^"
      )
    );
  }

  [Fact]
  public void TestCoroMethodMustBeCalledWithYield()
  {
    string bhl = @"
    class Foo {
      coro func bar() {
        yield()
      }
    }
    func test()
    {
      var foo = new Foo
      foo.bar()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "coro function must be called via yield",
      new PlaceAssert(bhl, @"
      foo.bar()
----------^"
      )
    );
  }

  [Fact]
  public void TestCoroInChainCallIsNotAllowed()
  {
    {
      string bhl = @"
      class Bar {
        int number
      }
      class Foo {
        coro func Bar bar() {
          yield()
          return new Bar
        }
      }
      func test()
      {
        var foo = new Foo
        int n = foo.bar().number
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "coro function must be called via yield",
        new PlaceAssert(bhl, @"
        int n = foo.bar().number
--------------------^"
        )
      );
    }

    {
      string bhl = @"
      class Hey {
        int number
      }
      class Bar {
        coro func Hey() ptr
      }
      class Foo {
        func Bar bar() {
          var b = new Bar
          b.ptr = coro func Hey() { 
            yield()
            return new Hey
          }
          return b
        }
      }
      func test()
      {
        var foo = new Foo
        int n = foo.bar().ptr().number
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "coro function must be called via yield",
        new PlaceAssert(bhl, @"
        int n = foo.bar().ptr().number
--------------------------^"
        )
      );
    }
  }

  [Fact]
  public void TestYieldSeveralTimesAndReturnValue()
  {
    string bhl = @"
    coro func int test()
    {
      yield()
      int a = 1
      yield()
      return a
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    Assert.Equal(1, fb.result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestYieldInParal()
  {
    string bhl = @"

    coro func int test() 
    {
      int i = 0
      paral {
        while(i < 3) { yield() }
        while(true) {
          i = i + 1
          yield()
        }
      }
      return i
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");

    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    Assert.Equal(3, fb.result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncWithYieldWhileMustByCoro()
  {
    string bhl = @"
    func test() {
      yield while(false)
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "function with yield calls must be coro",
      new PlaceAssert(bhl, @"
    func test() {
----^"
      )
    );
  }

  [Fact]
  public void TestYieldWhileInParal()
  {
    string bhl = @"

    coro func int test() 
    {
      int i = 0
      paral {
        yield while(i < 3)
        while(true) {
          i = i + 1
          yield()
        }
      }
      return i
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");

    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    var val = fb.result.PopRelease();
    Assert.Equal(3, val.num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSeveralYieldWhilesInParal()
  {
    string bhl = @"

    coro func int test() 
    {
      int i = 0
      paral {
        yield while(i < 5)
        while(true) {
          yield while(i < 7)
        }
        while(true) {
          i = i + 1
          yield()
        }
      }
      return i
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(5, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestYieldWhileBugInParal()
  {
    string bhl = @"
    coro func Foo() {
      yield suspend()
    }

    coro func test() {
      paral {
        yield while(true)

        while(true) {
          yield while(false)
          yield Foo()
        }
      }
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    for(int i=0;i<5;++i)
      Assert.True(vm.Tick());
    //...will be running forever, well, we assume that :)
    vm.Stop(fb);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncWithYieldFuncCallMustByCoro()
  {
    string bhl = @"
    func test() {
      yield suspend()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "function with yield calls must be coro",
      new PlaceAssert(bhl, @"
    func test() {
----^"
      )
    );
  }

  [Fact]
  public void TestFuncPtrIsSubsetOfCoroPtr()
  {
    string bhl = @"
    coro func int test() {
      coro func int() p = func int() {
        return 42
      }
      return yield p()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestOnlyCoroFuncCanBeYielded()
  {
    string bhl = @"
    func foo() {
    }
    coro func test() {
      yield foo()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "not a coro function",
      new PlaceAssert(bhl, @"
      yield foo()
------------^"
      )
    );
  }

  [Fact]
  public void TestOnlyCoroPtrCanBeYielded()
  {
    string bhl = @"
    coro func test() {
      func () p = func() {}
      yield p()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "not a coro function",
      new PlaceAssert(bhl, @"
      yield p()
------------^"
      )
    );
  }

  [Fact]
  public void TestCoroFuncPtrIsNotSubsetOfFuncPtr()
  {
    string bhl = @"
    func int test() {
      func int() p = coro func int() {
        yield()
        return 42
      }
      return p()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incompatible types: 'func int()' and 'coro func int()'",
      new PlaceAssert(bhl, @"
      func int() p = coro func int() {
-----------------^"
      )
    );
  }

  [Fact]
  public void TestYieldIsForbiddenInDefer()
  {
    {
      string bhl = @"
      coro func test() {
        defer {
          yield()
        }
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "yield is not allowed in defer block",
        new PlaceAssert(bhl, @"
          yield()
----------^"
        )
      );
    }

    {
      string bhl = @"
      coro func test() {
        defer {
          yield while(true)
        }
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "yield is not allowed in defer block",
        new PlaceAssert(bhl, @"
          yield while(true)
----------^"
        )
      );
    }

    {
      string bhl = @"
      coro func foo() {
        yield()
      }
      coro func test() {
        defer {
          yield foo()
        }
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "yield is not allowed in defer block",
        new PlaceAssert(bhl, @"
          yield foo()
----------^"
        )
      );
    }
  }

  [Fact]
  public void TestCallNonExistingFunc()
  {
    string bhl = @"
    coro func test() 
    {
      yield FOO()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "symbol 'FOO' not resolved",
      new PlaceAssert(bhl, @"
      yield FOO()
------------^"
       )
    );
  }

  [Fact]
  public void TestCallNonExistingFuncInParal()
  {
    string bhl = @"
    coro func test() 
    {
      paral {
        yield FOO()
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "symbol 'FOO' not resolved",
      new PlaceAssert(bhl, @"
        yield FOO()
--------------^"
       )
    );
  }

  [Fact]
  public void TestWait()
  {
    string bhl = @"
    coro func test()
    {
      yield wait(5)
    }
    ";

    var vm = MakeVM(bhl);
    int start = System.Environment.TickCount;
    var fb = vm.Start("test");
    for(int c=0;c<10;++c)
    {
      if(!vm.Tick())
        break;
      System.Threading.Thread.Sleep(1);
    }
    Assert.True(fb.IsStopped());
    Assert.True(System.Environment.TickCount - start >= 5);
    CommonChecks(vm);
  }

}
