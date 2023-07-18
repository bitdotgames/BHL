using System;
using System.Text;
using System.Collections.Generic;
using bhl;

public class TestDefer : BHL_TestBase
{
  [IsTested()]
  public void TestBasicDefer()
  {
    string bhl = @"
    func test() 
    {
      defer {
        trace(""bar"")
      }
      
      trace(""foo"")
    }
    ";

    var log = new StringBuilder();
    FuncSymbolNative fn = null;
    var ts_fn = new Action<Types>((ts) => {
      fn = BindTrace(ts, log);
    });

    var c = Compile(bhl, ts_fn);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Block, new int[] { (int)BlockType.DEFER, 12})
        .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "bar") })
        .EmitThen(Opcodes.CallNative, new int[] { c.module.nfuncs.IndexOf(fn), 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "foo") })
      .EmitThen(Opcodes.CallNative, new int[] { c.module.nfuncs.IndexOf(fn), 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;

    AssertEqual(c, expected);

    var vm = MakeVM(c, ts_fn);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("foobar", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDeferAccessVar()
  {
    string bhl = @"

    func foo(float k)
    {
      trace((string)k)
    }

    func test() 
    {
      float k = 142
      defer {
        foo(k)
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("142", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDeferAccessVarInCoroutine()
  {
    string bhl = @"

    func foo(float k)
    {
      trace((string)k)
    }

    coro func int doer() 
    {
      float k = 1
      defer {
        if(k == 2) {
          foo(k)
        }
      }
      yield()
      yield()
      k = 2

      return 100
    }

    coro func test() 
    {
      paral {
        {
          yield doer()
        }
        {
          yield suspend()
        }
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindLog(ts);
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("2", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDeferAccessVarInCoroutineInterrupted()
  {
    string bhl = @"

    func foo(float k)
    {
      trace((string)k)
    }

    coro func doer() 
    {
      float k = 1
      defer {
        if(k == 1) {
          foo(k)
        }
      }
      yield suspend()
      k = 2
    }

    coro func test() 
    {
      paral {
        {
          yield doer()
        }
        {
          yield()
          yield()
        }
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("1", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDeferScopes()
  {
    string bhl = @"

    func test() 
    {
      defer {
        trace(""0"")
      }

      {
        defer {
          trace(""1"")
        }
        trace(""2"")
      }

      trace(""3"")

      defer {
        trace(""4"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("21340", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDeferNestedNotAllowed()
  {
    string bhl = @"

    func bar()
    {
      defer {
        trace(""~BAR1"")
        defer {
          trace(""~BAR2"")
        }
      }
    }

    func test() 
    {
      bar()
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts_fn);
      },
      @"nested defers are not allowed",
      new PlaceAssert(bhl, @"
        defer {
--------^"
     )
    );
  }

  [IsTested()]
  public void TestDeferNestedSubCall()
  {
    string bhl = @"
    func foo() {
      defer {
        trace(""~FOO"")
      }
    }

    func bar() {
      defer {
        trace(""~BAR"")
        foo()
      }
    }

    func test() {
      bar()
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("~BAR~FOO", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDeferAndReturn()
  {
    string bhl = @"

    func bar(float k)
    {
      defer {
        trace(""~BAR"")
      }
      trace(""BAR"")
    }

    func float foo()
    {
      defer {
        trace(""~FOO"")
      }
      return 3
      trace(""FOO"")
    }

    func test() 
    {
      bar(foo())
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("~FOOBAR~BAR", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDeferOnFailure()
  {
    string bhl = @"

    func bar()
    {
      trace(""BAR"")
      fail()
    }

    func hey()
    {
      trace(""HEY"")
    }

    func foo()
    {
      trace(""FOO"")
    }

    func test() 
    {
      defer {
        foo()
      }
      bar()
      hey()
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
      BindFail(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("BARFOO", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnInDeferIsForbidden()
  {
    string bhl = @"

    func test() 
    {
      defer {
        return
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"return is not allowed in defer block",
      new PlaceAssert(bhl, @"
        return
--------^"
      )
    );
  }

  [IsTested()]
  public void TestReturnInDeferIsOkInLambda()
  {
    string bhl = @"

    func test() 
    {
      defer {
        func() {
          return
        } ()
      }
    }
    ";

    Compile(bhl);
  }

  [IsTested()]
  public void TestSubCallsInDefer()
  {
    string bhl = @"
    func string bar() 
    {
      return ""bar""
    }
    func test() 
    {
      defer {
        trace(bar())
      }
      
      trace(""foo"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("foobar", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSeveralDefers()
  {
    string bhl = @"
    func test() 
    {
      defer {
        trace(""bar"")
      }

      defer {
        trace(""hey"")
      }
      
      trace(""foo"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("fooheybar", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSubFuncDefer()
  {
    string bhl = @"

    func foo() {
      defer {
        trace(""foo1"")
      }

      trace(""foo"")

      defer {
        trace(""foo2"")
      }
      return
    }

    func test() {
      defer {
        trace(""test1"")
      }

      foo()

      defer {
        trace(""test2"")
      }
      
      trace(""test"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("foofoo2foo1testtest2test1", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSubSubFuncDefer()
  {
    string bhl = @"

    func level_start() {
      defer {
        trace(""~level_start"")
      }
    }

    coro func level_body(coro func() cb) {
      defer {
        trace(""~level_body"")
      }

      yield cb()

      level_start()
    }

    coro func test() {
      yield level_body(coro func() { 
        yield() 
      } )
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("~level_start~level_body", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSequenceDefer()
  {
    string bhl = @"
    func test() 
    {
      defer {
        trace(""hey"")
      }
      {
        defer {
          trace(""bar"")
        }
      }
      trace(""foo"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("barfoohey", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSequenceDeferAfterSubCall()
  {
    string bhl = @"
    func bar()
    {
      defer {
        trace(""4"")
      }
    }
    func test() 
    {
      defer {
        trace(""1"")
      }
      {
        bar()
        defer {
          trace(""2"")
        }
      }
      trace(""3"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("4231", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIfTrueDefer()
  {
    string bhl = @"

    func test() 
    {
      defer {
        trace(""hey"")
      }
      if(true) {
        defer {
          trace(""if"")
        }
      } else {
        defer {
          trace(""else"")
        }
      }
      trace(""foo"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("iffoohey", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIfElseDefer()
  {
    string bhl = @"

    func test() 
    {
      defer {
        trace(""hey"")
      }
      if(false) {
        defer {
          trace(""if"")
        }
      } else {
        defer {
          trace(""else"")
        }
      }
      trace(""foo"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("elsefoohey", log.ToString());
    CommonChecks(vm);
  }


  [IsTested()]
  public void TestBugWithStartingFiberFromDeferInterruptedParal()
  {
    string bhl = @"

    coro func doer() 
    {
      defer {
        start(coro func() { yield() })
        trace(""142"")
      }
      yield suspend()
    }

    coro func wow()
    {
      paral {
        {
          yield doer()
        }
        {
          paral {
            {
              return
            }
          }
        }
      }
    }

    coro func test() 
    {
      yield wow()
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("142", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBugWithStartingFiberFromDeferInterruptedParalAndRefs()
  {
    string bhl = @"

    coro func doer(ref int a) 
    {
      defer {
        start(coro func() { yield() })
      }
      a = 42
      yield suspend()
    }

    coro func wow(ref int a)
    {
      paral {
        {
          yield doer(ref a)
        }
        {
          yield()
          return
        }
      }
    }

    coro func test() 
    {
      int a = 0
      yield wow(ref a)
      trace((string)a)
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("42", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBadBreakInDefer()
  {
    string bhl = @"

    func test() 
    {
      while(true) {
        defer {
          break
        }
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "not within loop construct",
      new PlaceAssert(bhl, @"
          break
----------^"
      )
    );
  }

  [IsTested()]
  public void TestBreakInLoopInDefer()
  {
    string bhl = @"

    func test() 
    {
      defer {
        while(true) {
          trace(""2;"")
          break
        }
      }
      trace(""1;"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("1;2;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestContinueInLoopInDefer()
  {
    string bhl = @"

    func test() 
    {
      defer {
        int i = 0
        while(i < 2) {
          i = i + 1
          if(i == 1) {
            continue;
          }
          trace(""2;"")
        }
      }
      trace(""1;"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("1;2;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSeqFailureWithEmptyDefer()
  {
    string bhl = @"
    func foo()
    {
      fail()
    }

    func test() 
    {
      defer {
      }
      foo()
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindFail(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestWhileDefer()
  {
    string bhl = @"

    func test() 
    {
      defer {
        trace(""hey"")
      }
      int i = 0
      while(i < 2) {
        defer {
          trace(""while"")
        }
        i = i + 1
      }
      trace(""foo"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("whilewhilefoohey", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDeferInInfiniteLoop()
  {
    string bhl = @"

    coro func test() 
    {
      while(true) {
        defer {
          trace(""HEY;"")
        }
        yield()
      }
      defer {
        trace(""NEVER;"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    var fb = vm.Start("test");

    for(int i=0;i<5;++i)
      AssertTrue(vm.Tick());
    //...will be running forever, well, we assume that :)

    //NOTE: on the first tick we yield() is executed and 
    //      defer block is not run
    AssertEqual("HEY;HEY;HEY;HEY;", log.ToString());
    vm.Stop(fb);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDeferInInfiniteWithBreak()
  {
    string bhl = @"

    coro func test() 
    {
      int i = 0
      while(true) {
        defer {
          trace(""HEY;"")
        }
        i = i + 1
        if(i == 2) {
          break
        }
        yield()
      }
      defer {
        trace(""YOU;"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("HEY;HEY;YOU;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestWhileBreakDefer()
  {
    string bhl = @"

    func test() 
    {
      defer {
        trace(""hey"")
      }
      int i = 0
      while(i < 3) {
        defer {
          trace(""while"")
        }
        i = i + 1
        if(i == 2) {
          break
        }
      }
      trace(""foo"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("whilewhilefoohey", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalDefer()
  {
    string bhl = @"
    func test() 
    {
      defer {
        trace(""hey"")
      }
      paral {
        defer {
          trace(""bar"")
        }
        trace(""wow"")
      }
      trace(""foo"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("wowbarfoohey", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDeferAccessVarInParal()
  {
    string bhl = @"

    func foo() {
      float k = 1
      defer {
        trace((string)k)
      }
      k = 10
    }

    func test() 
    {
      paral {
        foo()
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("10", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDeferInSubParalFuncCall()
  {
    string bhl = @"
    coro func wait_3() {
      defer {
        trace(""~wait_3"")
      }

      trace(""!wait_3"")
      yield suspend()
    }

    coro func doer() {
      defer {
        trace(""~doer"")
      }
      paral {
        {
          yield wait_3()
        }
        {
          yield()
          trace(""!here"")
          yield()
        }
      }
    }

    coro func test() 
    {
      yield doer()
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("!wait_3!here~wait_3~doer", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalWithYieldDefer()
  {
    string bhl = @"
    coro func test() 
    {
      defer {
        trace(""hey"")
      }
      paral {
        defer {
          trace(""bar"")
        }
        {
          yield()
          trace(""wow"")
        }
      }
      trace(""foo"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertEqual("", log.ToString());
    AssertFalse(vm.Tick());
    AssertEqual("wowbarfoohey", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalWithMultiSeqDefers()
  {
    string bhl = @"

    coro func test() 
    {
      defer {
        trace(""1"")
      }
      paral {
        {
          defer {
            trace(""2"")
          }
          yield suspend()
        }
        {
          defer {
            trace(""3"")
          }
          yield suspend()
        }
        {
          defer {
            trace(""4"")
          }
          yield()
          yield()
          trace(""5"")
        }
      }
      trace(""6"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
      BindFail(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertEqual("", log.ToString());
    AssertFalse(vm.Tick());
    AssertEqual("542361", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalAllDefer()
  {
    string bhl = @"
    func test() 
    {
      defer {
        trace(""hey"")
      }
      paral_all {
        defer {
          trace(""bar"")
        }
        trace(""wow"")
      }
      trace(""foo"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("wowbarfoohey", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalAllWithYieldDefer()
  {
    string bhl = @"

    coro func test() 
    {
      defer {
        trace(""hey"")
      }
      paral_all {
        defer {
          trace(""bar"")
        }
        {
          yield()
          trace(""wow"")
        }
      }
      trace(""foo"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertEqual("", log.ToString());
    AssertFalse(vm.Tick());
    AssertEqual("wowbarfoohey", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalAllFailureWithMultiSeqDefers()
  {
    string bhl = @"

    coro func test() 
    {
      defer {
        trace(""1"")
      }
      paral_all {
        {
          defer {
            trace(""2"")
          }
          yield suspend()
        }
        {
          defer {
            trace(""3"")
          }
          yield suspend()
        }
        {
          defer {
            trace(""4"")
          }
          yield()
          yield()
          trace(""5"")
          fail()
        }
      }
      trace(""6"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
      BindFail(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertEqual("", log.ToString());
    //TODO: VM.Tick() returns BHS.SUCCESS when all fibers exited 
    //      regardless of their individual exit status
    AssertFalse(vm.Tick());
    AssertEqual("54231", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaDefer()
  {
    string bhl = @"

    func test() 
    {
      defer {
        trace(""hey"")
      }
      func() {
        defer {
          trace(""lmb2"")
        }
        trace(""lmb1"")
      }()
      trace(""foo"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("lmb1lmb2foohey", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStopFiberExternallyWithProperDefers()
  {
    string bhl = @"
    coro func foo()
    {
      defer {
        trace(""4"")
      }
      trace(""1"")
      yield()
    }

    coro func test()
    {
      int fb = start(coro func() {
        defer {
          trace(""0"")
        }
        yield foo()
        trace(""2"")
        yield()
      })
      defer {
        trace(""5"")
        stop(fb)
      }

      yield()
      trace(""3"")
      yield()
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);

    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    AssertEqual("1", log.ToString());
    AssertTrue(vm.Tick());
    AssertEqual("1342", log.ToString());
    vm.Stop(fb);
    AssertEqual("134250", log.ToString());
    AssertFalse(vm.Tick());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStopFiberExternallyWithProperDefersInParalsInModules()
  {
    string bhl2 = @"
    coro func foo()
    {
      paral {
        defer {
          trace(""2"")
        }
        yield suspend()
      }
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    coro func test()
    {
      int fb = start(coro func() {
        yield foo()
      })

      defer {
        trace(""1"")
        stop(fb)
      }

      yield suspend()
    }
    ";

    var log = new StringBuilder();

    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      },
      ts_fn
    );

    vm.LoadModule("bhl1");
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertEqual("", log.ToString());
    vm.Stop(fb);
    AssertEqual("12", log.ToString());
    AssertFalse(vm.Tick());
    CommonChecks(vm);
  }

}
