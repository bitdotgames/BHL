using System;
using bhl;

public class TestYield : BHL_TestBase
{
  [IsTested()]
  public void TestFuncWithYieldMustByAsync()
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
      "function with yield calls must be async",
      new PlaceAssert(bhl, @"
    func test() {
----^"
      )
    );
  }

  [IsTested()]
  public void TestEmptyAsyncFuncNotAllowed()
  {
    {
      string bhl = @"
      async func test() {
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "async functions without yield calls not allowed",
        new PlaceAssert(bhl, @"
      async func test() {
------^"
        )
      );
    }

    {
      string bhl = @"
      async func test() {
        int a = 10
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "async functions without yield calls not allowed",
        new PlaceAssert(bhl, @"
      async func test() {
------^"
        )
      );
    }

    {
      string bhl = @"
      async func test() {
        start(async func() {
            yield()
        })
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "async functions without yield calls not allowed",
        new PlaceAssert(bhl, @"
      async func test() {
------^"
        )
      );
    }
  }

  [IsTested()]
  public void TestBasicYield()
  {
    string bhl = @"
    async func test()
    {
      yield()
    }
    ";

    var vm = MakeVM(bhl);
    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestMethodYield()
  {
    string bhl = @"
    class Foo {
      async func bar() {
        yield()
      }
    }
    async func test()
    {
      var foo = new Foo
      yield foo.bar()
    }
    ";

    var vm = MakeVM(bhl);
    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestMethodBaseYield()
  {
    string bhl = @"
    class FooBase {
      async func foo() {
        yield()
      }
    }

    class Foo : FooBase {
      async func bar() {
        yield base.foo()
      }
    }
    async func test()
    {
      var foo = new Foo
      yield foo.bar()
    }
    ";

    var vm = MakeVM(bhl);
    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSuspend()
  {
    string bhl = @"
    async func test()
    {
      yield suspend()
    }
    ";

    var ts = new Types();
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.CallNative, new int[] { ts.nfunc_index.IndexOf("suspend"), 0 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    for(int i=0;i<99;i++)
      AssertTrue(vm.Tick());
    vm.Stop(fb);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestAsyncFuncMustBeCalledWithYield()
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
      "async function must be called via yield",
      new PlaceAssert(bhl, @"
      suspend()
------^"
      )
    );
  }

  [IsTested()]
  public void TestAsyncMethodMustBeCalledWithYield()
  {
    string bhl = @"
    class Foo {
      async func bar() {
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
      "async function must be called via yield",
      new PlaceAssert(bhl, @"
      foo.bar()
------^"
      )
    );
  }

  [IsTested()]
  public void TestYieldSeveralTimesAndReturnValue()
  {
    string bhl = @"
    async func int test()
    {
      yield()
      int a = 1
      yield()
      return a
    }
    ";

    var ts = new Types();

    var vm = MakeVM(bhl, ts);
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestYieldInParal()
  {
    string bhl = @"

    async func int test() 
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

    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(3, fb.result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncWithYieldWhileMustByAsync()
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
      "function with yield calls must be async",
      new PlaceAssert(bhl, @"
    func test() {
----^"
      )
    );
  }

  [IsTested()]
  public void TestYieldWhileInParal()
  {
    string bhl = @"

    async func int test() 
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

    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    var val = fb.result.PopRelease();
    AssertEqual(3, val.num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSeveralYieldWhilesInParal()
  {
    string bhl = @"

    async func int test() 
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
    AssertEqual(5, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestYieldWhileBugInParal()
  {
    string bhl = @"
    async func Foo() {
      yield suspend()
    }

    async func test() {
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
      AssertTrue(vm.Tick());
    //...will be running forever, well, we assume that :)
    vm.Stop(fb);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncWithYieldFuncCallMustByAsync()
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
      "function with yield calls must be async",
      new PlaceAssert(bhl, @"
    func test() {
----^"
      )
    );
  }

  [IsTested()]
  public void TestFuncPtrIsSubsetOfAsyncPtr()
  {
    string bhl = @"
    async func int test() {
      async func int() p = func int() {
        return 42
      }
      return yield p()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestAsyncFuncPtrIsNotSubsetOfFuncPtr()
  {
    string bhl = @"
    func int test() {
      func int() p = async func int() {
        return 42
      }
      return p()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incompatible types: 'func int()' and 'async func int()'",
      new PlaceAssert(bhl, @"
      func int() p = async func int() {
-------------------^"
      )
    );
  }

  [IsTested()]
  public void TestYieldIsForbiddenInDefer()
  {
    {
      string bhl = @"
      async func test() {
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
      async func test() {
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
      async func foo() {
        yield()
      }
      async func test() {
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
}
