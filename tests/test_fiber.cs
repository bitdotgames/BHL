using System;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using bhl;
using Xunit;

public class TestFiber : BHL_TestBase
{
  [Fact]
  public void TestFuncAddr()
  {
    string bhl = @"
    func int test()
    {
      return 10
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertEqual("test", fb.FuncAddr.symbol.name);
    Assert.False(vm.Tick());
    Assert.Equal(10, fb.result_old.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestResultMustBeReadyOnceFinished()
  {
    string bhl = @"
    func int test()
    {
      return 6
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    fb.Retain();
    Assert.False(vm.Tick());
    Assert.Equal(6, fb.result_old.PopRelease().num);
    fb.Release();
    CommonChecks(vm);
  }

  [Fact]
  public void TestStackList3Args()
  {
    string bhl = @"
    func int test(int a, int b, int c)
    {
      return a + b + c
    }
    ";

    var vm = MakeVM(bhl);
    int a = 1, b = 2, c = 3;
    var args = new Val[3];
    args[0] = a;
    args[1] = b;
    args[2] = c;
    var fb = vm.Start("test", new StackList<Val>(args));
    Assert.False(vm.Tick());
    Assert.Equal(6, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFiberStopChildren()
  {
    string bhl = @"
    coro func foo() {
      yield()
      yield()
    }

    coro func test() {
      start(foo)
      start(foo)
      yield()
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");

    Assert.Empty(fb.Children);

    vm.Tick(fb);

    Assert.Equal(2, fb.Children.Count);
    Assert.Equal(fb, fb.Children[0].Get().Parent.Get());
    Assert.Equal(fb, fb.Children[1].Get().Parent.Get());

    vm.Tick(fb);

    Assert.True(fb.IsStopped());
    Assert.False(fb.Children[0].Get().IsStopped());
    Assert.False(fb.Children[1].Get().IsStopped());

    vm.StopChildren(fb);

    Assert.True(fb.Children[0].Get().IsStopped());
    Assert.True(fb.Children[1].Get().IsStopped());

    vm.Tick(fb);

    CommonChecks(vm);
  }

  [Fact]
  public void TestFiberStopChildrenStartedFromDefer()
  {
    string bhl = @"
    coro func foo() {
      yield suspend()
    }

    coro func test() {
      defer {
        start(foo)
      }
      start(foo)
      start(foo)
      yield suspend()
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");

    Assert.True(fb.Tick());

    fb.Stop(true);

    CommonChecks(vm);
  }

  [Fact]
  public void TestDirectExecuteNativeFunc()
  {
    string bhl = @"
    func dummy()
    {}
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      var fn = new FuncSymbolNative(new Origin(), "mult2", Types.Int,
        (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          double n = exec.stack.Pop();
          exec.stack.Push(n * 2);
          return null;
        },
        new FuncArgSymbol("n", Types.Int)
      );
      ts.ns.Define(fn);
    });

    var vm = MakeVM(bhl, ts_fn);
    var num = ExecuteOld(vm, "mult2", ValOld.NewInt(vm, 10)).result_old.PopRelease().num;
    Assert.Equal(20, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestDirectExecuteNativeFuncWithDefaultArgsPassed()
  {
    string bhl = @"
    func dummy()
    {}
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      var fn = new FuncSymbolNative(new Origin(), "mult2", Types.Int,
        def_args_num: 1,
        cb: (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          var n = args_info.CountArgs() == 0 ? 1 : exec.stack.Pop().num;
          exec.stack.Push(n * 2);
          return null;
        },
        args: new FuncArgSymbol("n", Types.Int)
      );
      ts.ns.Define(fn);
    });

    var vm = MakeVM(bhl, ts_fn);
    var num = ExecuteOld(vm, "mult2", ValOld.NewInt(vm, 10)).result_old.PopRelease().num;
    Assert.Equal(20, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestDirectExecuteNativeFuncWithDefaultArgsNotPassed()
  {
    string bhl = @"
    func dummy()
    {}
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      var fn = new FuncSymbolNative(new Origin(), "mult2", Types.Int,
        def_args_num: 1,
        cb: (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          var n = args_info.CountArgs() == 0 ? 1 : exec.stack.PopRelease().num;
          exec.stack.Push(n * 2);
          return null;
        },
        args: new FuncArgSymbol("n", Types.Int)
      );
      ts.ns.Define(fn);
    });

    var vm = MakeVM(bhl, ts_fn);
    var num = Execute(vm, "mult2").result_old.PopRelease().num;
    Assert.Equal(2, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestDirectExecuteNativeCoro()
  {
    string bhl = @"
    func dummy()
    {}
    ";

    var vm = MakeVM(bhl);
    ExecuteOld(vm, "wait", ValOld.NewInt(vm, 0));
    CommonChecks(vm);
  }

  [Fact]
  public void TestStartNativeFunc()
  {
    string bhl = @"
    func test()
    {
      start(native)
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      var fn = new FuncSymbolNative(new Origin(), "native", Types.Void,
        (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          log.Append("HERE");
          return null;
        }
      );
      ts.ns.Define(fn);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("HERE", log.ToString());
    CommonChecks(vm);
  }

  class TraceAfterYield : Coroutine
  {
    bool first_time = true;
    public StringBuilder log;

    public override void Tick(VM.ExecState exec)
    {
      if(first_time)
      {
        exec.status = BHS.RUNNING;
        first_time = false;
      }
      else
        log.Append("HERE");
    }

    public override void Cleanup(VM.ExecState exec)
    {
      first_time = true;
    }
  }

  [Fact]
  public void TestStartNativeStatefulFunc()
  {
    string bhl = @"
    func test()
    {
      start(yield_and_trace)
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      {
        var fn = new FuncSymbolNative(new Origin(), "yield_and_trace", Types.Void,
          (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            var inst = CoroutinePool.New<TraceAfterYield>(vm);
            inst.log = log;
            return inst;
          }
        );
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("HERE", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestStartSameFuncPtr()
  {
    string bhl = @"
    coro func foo() {
      trace(""FOO1"")
      yield()
      trace(""FOO2"")
    }

    func test() {
      coro func() fn = foo
      coro func() fn2 = fn
      paral_all {
        start(fn2)
        start(fn2)
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("FOO1FOO1FOO2FOO2", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestStartImportedSameFuncPtr()
  {
    string bhl2 = @"
    coro func foo() {
      trace(""FOO1"")
      yield()
      trace(""FOO2"")
    }
    ";

    string bhl1 = @"
    import ""bhl2""

    func test() {
      coro func() fn = foo
      coro func() fn2 = fn
      paral_all {
        start(fn2)
        start(fn2)
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      },
      ts_fn
    );

    vm.LoadModule("bhl1");
    Execute(vm, "test");
    AssertEqual("FOO1FOO1FOO2FOO2", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestStartFiberFromScript()
  {
    string bhl = @"
    func test()
    {
      start(coro func() {
        yield()
        trace(""done"")
      })
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);

    vm.Start("test");
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    AssertEqual("done", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestStopFiberFromScript()
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
      var fb = start(coro func() {
        defer {
          trace(""0"")
        }
        yield foo()
        trace(""2"")
      })

      yield()
      trace(""3"")
      stop(fb)
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);

    vm.Start("test");
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    AssertEqual("1340", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestFiberRefIsRunning()
  {
    string bhl = @"
    coro func test()
    {
      var fb = start(coro func() {
        yield()
        yield()
      })

      trace(""1: "" + fb.IsRunning + "";"")
      yield()
      stop(fb)
      trace(""2: "" + fb.IsRunning + "";"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);

    Execute(vm, "test");
    AssertEqual("1: 1;2: 0;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestDoubleStopFiberFromScript()
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
      var fb = start(coro func() {
        defer {
          trace(""0"")
        }
        yield foo()
        trace(""2"")
      })

      yield()
      trace(""3"")
      stop(fb)
      stop(fb)
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);

    vm.Start("test");
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    AssertEqual("1340", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestDoubleStopFiberFromDefer()
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
      FiberRef fb
      fb = start(coro func() {
        defer {
          trace(""0"")
          stop(fb)
        }
        yield foo()
        trace(""2"")
      })

      yield()
      trace(""3"")
      stop(fb)
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);

    vm.Start("test");
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    AssertEqual("1340", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestSelfStopFiberFromScript()
  {
    string bhl = @"
    func foo()
    {
      defer {
        trace(""4"")
      }
      trace(""1"")
    }

    coro func test()
    {
      FiberRef fb
      fb = start(coro func() {
        defer {
          trace(""0"")
        }
        yield()
        foo()
        stop(fb)
        yield()
        trace(""2"")
      })

      yield()
      trace(""3"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);

    vm.Start("test");
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    AssertEqual("3140", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestSelfStopFiberFromNativeFunc()
  {
    string bhl = @"
    func foo()
    {
      defer {
        trace(""4"")
      }
      trace(""1"")
    }

    coro func test()
    {
      FiberRef fb
      fb = start(coro func() {
        defer {
          trace(""0"")
        }
        yield()
        foo()
        STOP(fb)
        yield()
        trace(""2"")
      })

      yield()
      trace(""3"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);

      var fn = new FuncSymbolNative(new Origin(), "STOP", Types.Void,
        (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          throw new NotImplementedException();
          //var val = stack.Pop();
          //var fb_ref = new VM.FiberRef(val);
          //fb_ref.Get().Stop();
          //val.Release();
          return null;
        },
        new FuncArgSymbol("fb", ts.T(Types.FiberRef))
      );
      ts.ns.Define(fn);
    });

    var vm = MakeVM(bhl, ts_fn);

    vm.Start("test");
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    AssertEqual("3140", log.ToString());
    CommonChecks(vm);
  }

  class YIELD_STOP : Coroutine
  {
    bool done;
    VM.FiberRef fb;

    public override void Tick(VM.ExecState exec)
    {
      //first time
      if(!done)
      {
        var val = exec.stack.Pop();
        throw new NotImplementedException();
        //fb = new VM.FiberRef(val);
        val.Release();
        exec.status = BHS.RUNNING;
        done = true;
      }
      else
        exec.vm.Stop(fb.Get());
    }

    public override void Cleanup(VM.ExecState exec)
    {
      done = false;
    }
  }

  [Fact]
  public void TestSelfStopFiberFromNativeCoro()
  {
    string bhl = @"
    func foo()
    {
      defer {
        trace(""4"")
      }
      trace(""1"")
    }

    coro func test()
    {
      FiberRef fb
      fb = start(coro func() {
        defer {
          trace(""0"")
        }
        yield()
        foo()
        yield YIELD_STOP(fb)
        yield()
        trace(""2"")
      })

      yield()
      trace(""3"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);

      var fn = new FuncSymbolNative(new Origin(), "YIELD_STOP", FuncAttrib.Coro, Types.Void, 0,
        (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          return CoroutinePool.New<YIELD_STOP>(vm);
        },
        new FuncArgSymbol("fb", ts.T(Types.FiberRef))
      );
      ts.ns.Define(fn);
    });

    var vm = MakeVM(bhl, ts_fn);

    vm.Start("test");
    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    AssertEqual("3140", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestFiberCache()
  {
    string bhl = @"

    coro func foo()
    {
      yield()
    }

    coro func test()
    {
      yield foo()
    }
    ";

    var vm = MakeVM(bhl);
    {
      vm.Start("test");
      Assert.Equal(1, vm.fibers_pool.MissCount);
      Assert.Equal(0, vm.fibers_pool.IdleCount);
      vm.Tick();
      vm.Start("test");
      Assert.Equal(2, vm.fibers_pool.MissCount);
      Assert.Equal(0, vm.fibers_pool.IdleCount);
      vm.Tick();
      Assert.Equal(2, vm.fibers_pool.MissCount);
      Assert.Equal(1, vm.fibers_pool.IdleCount);
      vm.Tick();
      Assert.Equal(2, vm.fibers_pool.MissCount);
      Assert.Equal(2, vm.fibers_pool.IdleCount);
    }
    //no new allocs
    {
      vm.Start("test");
      vm.Tick();
      vm.Tick();
      Assert.Equal(2, vm.fibers_pool.MissCount);
      Assert.Equal(2, vm.fibers_pool.IdleCount);
    }
    CommonChecks(vm);
  }

  [Fact]
  public void TestStartLambdaInScriptMgr()
  {
    string bhl = @"

    coro func test()
    {
      while(true) {
        StartScriptInMgr(
          script: func() {
            trace(""HERE;"")
          },
          spawns : 1
        )
        yield()
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");

    {
      Assert.True(vm.Tick());
      ScriptMgr.instance.Tick();

      AssertEqual("HERE;", log.ToString());

      var cs = ScriptMgr.instance.active;
      Assert.Empty(cs);
    }

    {
      Assert.True(vm.Tick());
      ScriptMgr.instance.Tick();

      AssertEqual("HERE;HERE;", log.ToString());

      var cs = ScriptMgr.instance.active;
      Assert.Empty(cs);
    }

    ScriptMgr.instance.Stop();
    Assert.True(!ScriptMgr.instance.Busy);

    vm.Stop();

    CommonChecks(vm);
  }

  [Fact]
  public void TestStartLambdaRunninInScriptMgr()
  {
    string bhl = @"

    coro func test()
    {
      while(true) {
        StartScriptInMgr(
          script: coro func() {
            trace(""HERE;"")
            yield suspend()
          },
          spawns : 1
        )
        yield()
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");

    {
      Assert.True(vm.Tick());
      ScriptMgr.instance.Tick();

      AssertEqual("HERE;", log.ToString());

      var cs = ScriptMgr.instance.active;
      Assert.Single(cs);

      //let's check func addresses, however since it's a lambda, there's no
      //actual func symbol and we simply check if instruction pointer is valid
      Assert.True(cs[0].FuncAddr.ip > -1);
    }

    {
      Assert.True(vm.Tick());
      ScriptMgr.instance.Tick();

      AssertEqual("HERE;HERE;", log.ToString());

      var cs = ScriptMgr.instance.active;
      Assert.Equal(2, cs.Count);
      Assert.True(cs[0] != cs[1]);
    }

    ScriptMgr.instance.Stop();
    Assert.True(!ScriptMgr.instance.Busy);

    vm.Stop();

    CommonChecks(vm);
  }

  [Fact]
  public void TestStartLambdaManyTimesInScriptMgr()
  {
    string bhl = @"

    func test()
    {
      StartScriptInMgr(
        script: coro func() {
          yield suspend()
        },
        spawns : 3
      )
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");

    Assert.False(vm.Tick());
    ScriptMgr.instance.Tick();

    var cs = ScriptMgr.instance.active;
    Assert.Equal(3, cs.Count);
    Assert.True(cs[0] != cs[1]);
    Assert.True(cs[1] != cs[2]);
    Assert.True(cs[0] != cs[2]);

    ScriptMgr.instance.Stop();
    Assert.True(!ScriptMgr.instance.Busy);

    vm.Stop();

    CommonChecks(vm);
  }

  [Fact]
  public void TestStartFuncPtrManyTimesInScriptMgr()
  {
    string bhl = @"

    func test()
    {
      coro func() fn = coro func() {
        trace(""HERE;"")
        yield suspend()
      }

      StartScriptInMgr(
        script: fn,
        spawns : 2
      )
    }
    ";

    var log = new StringBuilder();

    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");

    Assert.False(vm.Tick());
    ScriptMgr.instance.Tick();

    var cs = ScriptMgr.instance.active;
    Assert.Equal(2, cs.Count);
    Assert.True(cs[0] != cs[1]);

    AssertEqual("HERE;HERE;", log.ToString());

    ScriptMgr.instance.Stop();
    Assert.True(!ScriptMgr.instance.Busy);

    vm.Stop();

    CommonChecks(vm);
  }

  [Fact]
  public void TestStartNativeFuncPtrManyTimesInScriptMgr()
  {
    string bhl = @"

    func test()
    {
      StartScriptInMgr(
        script: say_here,
        spawns : 2
      )
    }
    ";

    var log = new StringBuilder();

    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);

      {
        var fn = new FuncSymbolNative(new Origin(), "say_here", Types.Void,
          (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            log.Append("HERE;");
            return null;
          }
        );
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");

    Assert.False(vm.Tick());
    ScriptMgr.instance.Tick();

    var cs = ScriptMgr.instance.active;
    Assert.Empty(cs);

    AssertEqual("HERE;HERE;", log.ToString());

    ScriptMgr.instance.Stop();
    Assert.True(!ScriptMgr.instance.Busy);

    vm.Stop();

    CommonChecks(vm);
  }

  [Fact]
  public void TestStartCoroFuncPtrManyTimesInScriptMgr()
  {
    string bhl = @"
    coro func say_here()
    {
      yield()
      trace(""HERE;"")
    }

    func test()
    {
      StartScriptInMgr(
        script: say_here,
        spawns : 2
      )
    }
    ";

    var log = new StringBuilder();

    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");

    {
      Assert.False(vm.Tick());
      ScriptMgr.instance.Tick();

      var cs = ScriptMgr.instance.active;
      Assert.Equal(2, cs.Count);
      AssertEqual("say_here", cs[0].FuncAddr.symbol.name);
      AssertEqual("say_here", cs[1].FuncAddr.symbol.name);
    }

    {
      Assert.False(vm.Tick());
      ScriptMgr.instance.Tick();

      AssertEqual("HERE;HERE;", log.ToString());

      var cs = ScriptMgr.instance.active;
      Assert.Empty(cs);
    }

    Assert.True(!ScriptMgr.instance.Busy);

    vm.Stop();

    CommonChecks(vm);
  }

  [Fact]
  public void TestStartLambdaVarManyTimesInScriptMgr()
  {
    string bhl = @"

    func test()
    {
      coro func() fn = coro func() {
        trace(""HERE;"")
        yield suspend()
      }

      StartScriptInMgr(
        script: coro func() {
          yield fn()
        },
        spawns : 2
      )
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");

    Assert.False(vm.Tick());
    ScriptMgr.instance.Tick();

    var cs = ScriptMgr.instance.active;
    Assert.Equal(2, cs.Count);
    Assert.True(cs[0] != cs[1]);

    AssertEqual("HERE;HERE;", log.ToString());

    ScriptMgr.instance.Stop();
    Assert.True(!ScriptMgr.instance.Busy);

    vm.Stop();

    CommonChecks(vm);
  }

  [Fact]
  public void TestStartLambdaManyTimesInScriptMgrWithUpVals()
  {
    string bhl = @"

    func test()
    {
      float a = 0
      StartScriptInMgr(
        script: coro func() {
          a = a + 1
          trace((string) a + "";"")
          yield suspend()
        },
        spawns : 3
      )
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");

    Assert.False(vm.Tick());
    ScriptMgr.instance.Tick();

    var cs = ScriptMgr.instance.active;
    Assert.Equal(3, cs.Count);
    Assert.True(cs[0] != cs[1]);
    Assert.True(cs[1] != cs[2]);
    Assert.True(cs[0] != cs[2]);

    AssertEqual("1;2;3;", log.ToString());

    ScriptMgr.instance.Stop();
    Assert.True(!ScriptMgr.instance.Busy);

    vm.Stop();

    CommonChecks(vm);
  }

  [Fact]
  public void TestStartLambdaManyTimesInScriptMgrWithValCopies()
  {
    string bhl = @"

    func test()
    {
      float a = 1
      StartScriptInMgr(
        script: coro func() {
            yield coro func (float a) {
              a = a + 1
              trace((string) a + "";"")
              yield suspend()
          }(a)
        },
        spawns : 3
      )
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");

    Assert.False(vm.Tick());
    ScriptMgr.instance.Tick();

    var cs = ScriptMgr.instance.active;
    Assert.Equal(3, cs.Count);
    Assert.True(cs[0] != cs[1]);
    Assert.True(cs[1] != cs[2]);
    Assert.True(cs[0] != cs[2]);

    AssertEqual("2;2;2;", log.ToString());

    ScriptMgr.instance.Stop();
    Assert.True(!ScriptMgr.instance.Busy);

    vm.Stop();

    CommonChecks(vm);
  }

  [Fact]
  public void TestStaleFrameReferenceFuncPtrBug()
  {
    string bhl = @"

    func sub(int a, int b) {
       trace((string)(b - a) + "";"")
    }

    func test() {
      StartScriptInMgr(
        script: coro func() {
          int a = 1
          int b = 3
          defer {
            sub(a, b)
          }

          yield()
        },
        spawns : 1
      )
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");

    AssertEqual("", log.ToString());

    ScriptMgr.instance.Tick();

    //NOTE: in case of bug the defer block is going to use a stale Frame
    //      and it will lead to origin stack pointing to the new Frame's stack!
    var frm = VM.FrameOld.New(vm);

    ScriptMgr.instance.Tick();

    AssertEqual("2;", log.ToString());

    Assert.True(!ScriptMgr.instance.Busy);

    frm.Release();

    CommonChecks(vm);
  }

  [Fact]
  public void TestExecute()
  {
    string bhl = @"
    func int test()
    {
      return 10
    }
    ";

    var vm = MakeVM(bhl);

    var fs =
      (FuncSymbolScript)new VM.SymbolSpec(TestModuleName, "test").LoadModuleSymbol(vm).symbol;

    {
      var result = vm.ExecuteOld(fs);
      Assert.Equal(10, result.PopRelease().num);
      CommonChecks(vm);
    }

    {
      var result = vm.ExecuteOld(fs);
      Assert.Equal(10, result.PopRelease().num);
      CommonChecks(vm);
    }
  }

  [Fact]
  public void TestExecuteWithArgs()
  {
    string bhl = @"
    func int test(int k, int d)
    {
      return d - k
    }
    ";

    var vm = MakeVM(bhl);

    var fs =
      (FuncSymbolScript)new VM.SymbolSpec(TestModuleName, "test").LoadModuleSymbol(vm).symbol;

    {
      var result = vm.ExecuteOld(fs, new StackList<ValOld>(ValOld.NewInt(vm, 10), ValOld.NewInt(vm, 20)));
      Assert.Equal(10, result.PopRelease().num);
      CommonChecks(vm);
    }

    {
      var result = vm.ExecuteOld(fs, new StackList<ValOld>(ValOld.NewInt(vm, 2), ValOld.NewInt(vm, 1)));
      Assert.Equal(-1, result.PopRelease().num);
      CommonChecks(vm);
    }
  }

  [Fact]
  public void TestExecuteFuncTrampoline()
  {
    string bhl = @"
    func int test()
    {
      return 10
    }

    func int test2()
    {
      return 20
    }
    ";

    var vm = MakeVM(bhl);

    int trampoline1 = 0;
    int trampoline2 = 0;

    var test_fs1 = vm.GetOrMakeFuncTrampoline(ref trampoline1, TestModuleName, "test");
    var test2_fs1 = vm.GetOrMakeFuncTrampoline(ref trampoline2, TestModuleName, "test2");

    int trampoline1_copy = trampoline1;
    Assert.NotEqual(0, trampoline1);

    {
      var result = vm.ExecuteOld(test_fs1);
      Assert.Equal(10, result.PopRelease().num);
      CommonChecks(vm);
    }

    var test_fs2 = vm.GetOrMakeFuncTrampoline(ref trampoline1, TestModuleName, "test");
    Assert.Equal(test_fs1, test_fs2);
    Assert.Equal(trampoline1_copy, trampoline1);

    {
      var result = vm.ExecuteOld(test2_fs1);
      Assert.Equal(20, result.PopRelease().num);
      CommonChecks(vm);
    }
  }

  [Fact]
  public void TestFuncTrampolineNotFound()
  {
    string bhl = @"
    func int test()
    {
      return 10
    }
    ";

    var vm = MakeVM(bhl);

    int trampoline1 = 0;

    AssertError<Exception>(
      () => vm.GetOrMakeFuncTrampoline(ref trampoline1, TestModuleName, "no_such_a_func"),
      $"Module '{TestModuleName}' symbol 'no_such_a_func' not found"
    );
  }

  [Fact]
  public void TestExecuteStartsAFiber()
  {
    string bhl = @"
    func test()
    {
      int i = 10
      start(coro func() {
        yield Doer(i)
      })
    }

    coro func Doer(int i)
    {
      trace((string)i)
      yield()
      i = i + 1
      trace((string)i)
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });
    var vm = MakeVM(bhl, ts_fn);

    var fs =
      (FuncSymbolScript)new VM.SymbolSpec(TestModuleName, "test").LoadModuleSymbol(vm).symbol;

    {
      vm.ExecuteOld(fs);
      vm.Tick();
      vm.Tick();
      AssertEqual("1011", log.ToString());
      CommonChecks(vm);
    }
  }

  void BindStartScriptInMgr(Types ts)
  {
    {
      var fn = new FuncSymbolNative(new Origin(), "StartScriptInMgr", Types.Void,
        (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          int spawns = exec.stack.Pop();
          var ptr = exec.stack.Pop();

          for(int i = 0; i < spawns; ++i)
          {
            throw new NotImplementedException();
            //ScriptMgr.instance.Start(frm, (VM.FuncPtr)ptr.obj, stack);
          }

          ptr.Release();

          return null;
        },
        new FuncArgSymbol("script", ts.TFunc(true, Types.Void)),
        new FuncArgSymbol("spawns", Types.Int)
      );

      ts.ns.Define(fn);
    }
  }

  public class ScriptMgr
  {
    public static ScriptMgr instance = new ScriptMgr();
    public List<VM.Fiber> active = new List<VM.Fiber>();

    public bool Busy
    {
      get { return active.Count > 0; }
    }

    public void Start(VM.ExecState exec, ref VM.Frame origin, VM.FuncPtr ptr)
    {
      var fb = exec.vm.Start(exec, ptr, ref origin, new StackList<Val>());
      exec.vm.Detach(fb);
      active.Add(fb);
    }

    public void Tick()
    {
      if(active.Count > 0)
        active[0].vm.Tick(active);
    }

    public void Stop()
    {
      for(int i = active.Count; i-- > 0;)
      {
        active[i].vm.Stop(active[i]);
        active.RemoveAt(i);
      }
    }
  }
}
