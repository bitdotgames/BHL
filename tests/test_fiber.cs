using System;
using System.Text;
using System.Collections.Generic;
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
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 10);
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
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 6);
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
    args[0] = Val.NewNum(vm, a);
    args[1] = Val.NewNum(vm, b);
    args[2] = Val.NewNum(vm, c);
    var fb = vm.Start("test", new StackList<Val>(args));
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 6);
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

    AssertEqual(0, fb.Children.Count);

    vm.Tick(fb);

    AssertEqual(2, fb.Children.Count);
    AssertEqual(fb, fb.Children[0].Get().Parent.Get());
    AssertEqual(fb, fb.Children[1].Get().Parent.Get());

    vm.Tick(fb);

    AssertTrue(fb.IsStopped());
    AssertFalse(fb.Children[0].Get().IsStopped());
    AssertFalse(fb.Children[1].Get().IsStopped());

    vm.StopChildren(fb);

    AssertTrue(fb.Children[0].Get().IsStopped());
    AssertTrue(fb.Children[1].Get().IsStopped());

    vm.Tick(fb);

    CommonChecks(vm);
  }
  
  [Fact]
  public void TestDirectExecuteNativeFunc()
  {
    string bhl = @"
    func dummy() 
    {}
    ";

    var ts_fn = new Action<Types>((ts) => {
      var fn = new FuncSymbolNative(new Origin(), "mult2", Types.Int,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
          {
            var n = stack.PopRelease().num;
            stack.Push(Val.NewInt(frm.vm, n * 2));
            return null;
          }, 
          new FuncArgSymbol("n", Types.Int)
      );
      ts.ns.Define(fn);
    });

    var vm = MakeVM(bhl, ts_fn);
    var num = Execute(vm, "mult2", Val.NewInt(vm, 10)).result.PopRelease().num;
    AssertEqual(num, 20);
    CommonChecks(vm);
  }
  
  [Fact]
  public void TestDirectExecuteNativeFuncWithDefaultArgsPassed()
  {
    string bhl = @"
    func dummy() 
    {}
    ";

    var ts_fn = new Action<Types>((ts) => {
      var fn = new FuncSymbolNative(new Origin(), "mult2", Types.Int,
          def_args_num: 1,
          cb: delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
          {
            var n = args_info.CountArgs() == 0 ? 1 : stack.PopRelease().num;
            stack.Push(Val.NewInt(frm.vm, n * 2));
            return null;
          }, 
          args: new FuncArgSymbol("n", Types.Int)
      );
      ts.ns.Define(fn);
    });

    var vm = MakeVM(bhl, ts_fn);
    var num = Execute(vm, "mult2",Val.NewInt(vm, 10)).result.PopRelease().num;
    AssertEqual(num, 20);
    CommonChecks(vm);
  }
  
  [Fact]
  public void TestDirectExecuteNativeFuncWithDefaultArgsNotPassed()
  {
    string bhl = @"
    func dummy() 
    {}
    ";

    var ts_fn = new Action<Types>((ts) => {
      var fn = new FuncSymbolNative(new Origin(), "mult2", Types.Int,
          def_args_num: 1,
          cb: delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
          {
            var n = args_info.CountArgs() == 0 ? 1 : stack.PopRelease().num;
            stack.Push(Val.NewInt(frm.vm, n * 2));
            return null;
          }, 
          args: new FuncArgSymbol("n", Types.Int)
      );
      ts.ns.Define(fn);
    });

    var vm = MakeVM(bhl, ts_fn);
    var num = Execute(vm, "mult2").result.PopRelease().num;
    AssertEqual(num, 2);
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
    Execute(vm, "wait", Val.NewInt(vm, 0));
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
    var ts_fn = new Action<Types>((ts) => {
      var fn = new FuncSymbolNative(new Origin(), "native", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
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

    public override void Tick(VM.Frame frm, VM.ExecState exec, ref BHS status)
    {
      if(first_time)
      {
        status = BHS.RUNNING;
        first_time = false;
      }
      else
        log.Append("HERE");
    }

    public override void Cleanup(VM.Frame frm, VM.ExecState exec)
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
    var ts_fn = new Action<Types>((ts) => {
      {
        var fn = new FuncSymbolNative(new Origin(), "yield_and_trace", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
          { 
            var inst = CoroutinePool.New<TraceAfterYield>(frm.vm);
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
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("FOO1FOO1FOO2FOO2", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestStartImportedSameFuncPtr()
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
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);

    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
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
      int fid = start(coro func() {
        defer {
          trace(""0"")
        }
        yield foo()
        trace(""2"")
      })

      yield()
      trace(""3"")
      stop(fid)
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);

    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual("1340", log.ToString());
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
      int fid = start(coro func() {
        defer {
          trace(""0"")
        }
        yield foo()
        trace(""2"")
      })

      yield()
      trace(""3"")
      stop(fid)
      stop(fid)
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);

    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
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
      int fid
      fid = start(coro func() {
        defer {
          trace(""0"")
        }
        yield()
        foo()
        stop(fid)
        yield()
        trace(""2"")
      })

      yield()
      trace(""3"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);

    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
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
      int fid
      fid = start(coro func() {
        defer {
          trace(""0"")
        }
        yield()
        foo()
        STOP(fid)
        yield()
        trace(""2"")
      })

      yield()
      trace(""3"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);

      var fn = new FuncSymbolNative(new Origin(), "STOP", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            int fid = (int)stack.PopRelease().num;
            frm.vm.Stop(fid);
            return null;
          }, 
          new FuncArgSymbol("fid", ts.T("int"))
      );
      ts.ns.Define(fn);
    });

    var vm = MakeVM(bhl, ts_fn);

    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual("3140", log.ToString());
    CommonChecks(vm);
  }

  class YIELD_STOP : Coroutine
  {
    bool done;
    int fib;

    public override void Tick(VM.Frame frm, VM.ExecState exec, ref BHS status)
    {
      //first time
      if(!done)
      {
        fib = (int)exec.stack.PopRelease().num;
        status = BHS.RUNNING;
        done = true;
      }
      else
        frm.vm.Stop(fib);
    }

    public override void Cleanup(VM.Frame frm, VM.ExecState exec)
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
      int fid
      fid = start(coro func() {
        defer {
          trace(""0"")
        }
        yield()
        foo()
        yield YIELD_STOP(fid)
        yield()
        trace(""2"")
      })

      yield()
      trace(""3"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);

      var fn = new FuncSymbolNative(new Origin(), "YIELD_STOP", FuncAttrib.Coro, Types.Void, 0,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            return CoroutinePool.New<YIELD_STOP>(frm.vm);
          }, 
          new FuncArgSymbol("fid", ts.T("int"))
      );
      ts.ns.Define(fn);
    });

    var vm = MakeVM(bhl, ts_fn);

    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
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
      AssertEqual(1, vm.fibers_pool.MissCount);
      AssertEqual(0, vm.fibers_pool.IdleCount);
      vm.Tick();
      vm.Start("test");
      AssertEqual(2, vm.fibers_pool.MissCount);
      AssertEqual(0, vm.fibers_pool.IdleCount);
      vm.Tick();
      AssertEqual(2, vm.fibers_pool.MissCount);
      AssertEqual(1, vm.fibers_pool.IdleCount);
      vm.Tick();
      AssertEqual(2, vm.fibers_pool.MissCount);
      AssertEqual(2, vm.fibers_pool.IdleCount);
    }
    //no new allocs
    {
      vm.Start("test");
      vm.Tick();
      vm.Tick();
      AssertEqual(2, vm.fibers_pool.MissCount);
      AssertEqual(2, vm.fibers_pool.IdleCount);
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
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");

    {
      AssertTrue(vm.Tick());
      ScriptMgr.instance.Tick();

      AssertEqual("HERE;", log.ToString());

      var cs = ScriptMgr.instance.active;
      AssertEqual(0, cs.Count); 
    }

    {
      AssertTrue(vm.Tick());
      ScriptMgr.instance.Tick();

      AssertEqual("HERE;HERE;", log.ToString());

      var cs = ScriptMgr.instance.active;
      AssertEqual(0, cs.Count); 
    }

    ScriptMgr.instance.Stop();
    AssertTrue(!ScriptMgr.instance.Busy);

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
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");

    {
      AssertTrue(vm.Tick());
      ScriptMgr.instance.Tick();

      AssertEqual("HERE;", log.ToString());

      var cs = ScriptMgr.instance.active;
      AssertEqual(1, cs.Count); 
      
      //let's check func addresses, however since it's a lambda, there's no
      //actual func symbol and we simply check if instruction pointer is valid
      AssertTrue(cs[0].FuncAddr.ip > -1);
    }

    {
      AssertTrue(vm.Tick());
      ScriptMgr.instance.Tick();

      AssertEqual("HERE;HERE;", log.ToString());

      var cs = ScriptMgr.instance.active;
      AssertEqual(2, cs.Count); 
      AssertTrue(cs[0] != cs[1]);
    }

    ScriptMgr.instance.Stop();
    AssertTrue(!ScriptMgr.instance.Busy);

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
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");

    AssertFalse(vm.Tick());
    ScriptMgr.instance.Tick();

    var cs = ScriptMgr.instance.active;
    AssertEqual(3, cs.Count); 
    AssertTrue(cs[0] != cs[1]);
    AssertTrue(cs[1] != cs[2]);
    AssertTrue(cs[0] != cs[2]);

    ScriptMgr.instance.Stop();
    AssertTrue(!ScriptMgr.instance.Busy);

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

    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");

    AssertFalse(vm.Tick());
    ScriptMgr.instance.Tick();

    var cs = ScriptMgr.instance.active;
    AssertEqual(2, cs.Count); 
    AssertTrue(cs[0] != cs[1]);

    AssertEqual("HERE;HERE;", log.ToString());

    ScriptMgr.instance.Stop();
    AssertTrue(!ScriptMgr.instance.Busy);

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

    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);

      {
        var fn = new FuncSymbolNative(new Origin(), "say_here", Types.Void,
            delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
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

    AssertFalse(vm.Tick());
    ScriptMgr.instance.Tick();

    var cs = ScriptMgr.instance.active;
    AssertEqual(0, cs.Count); 

    AssertEqual("HERE;HERE;", log.ToString());

    ScriptMgr.instance.Stop();
    AssertTrue(!ScriptMgr.instance.Busy);

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

    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");

    {
      AssertFalse(vm.Tick());
      ScriptMgr.instance.Tick();

      var cs = ScriptMgr.instance.active;
      AssertEqual(2, cs.Count);
      AssertEqual("say_here", cs[0].FuncAddr.symbol.name);
      AssertEqual("say_here", cs[1].FuncAddr.symbol.name);
    }

    {
      AssertFalse(vm.Tick());
      ScriptMgr.instance.Tick();
      
      AssertEqual("HERE;HERE;", log.ToString());
      
      var cs = ScriptMgr.instance.active;
      AssertEqual(0, cs.Count);
    }

    AssertTrue(!ScriptMgr.instance.Busy);

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
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");

    AssertFalse(vm.Tick());
    ScriptMgr.instance.Tick();

    var cs = ScriptMgr.instance.active;
    AssertEqual(2, cs.Count); 
    AssertTrue(cs[0] != cs[1]);

    AssertEqual("HERE;HERE;", log.ToString());

    ScriptMgr.instance.Stop();
    AssertTrue(!ScriptMgr.instance.Busy);

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
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");

    AssertFalse(vm.Tick());
    ScriptMgr.instance.Tick();

    var cs = ScriptMgr.instance.active;
    AssertEqual(3, cs.Count); 
    AssertTrue(cs[0] != cs[1]);
    AssertTrue(cs[1] != cs[2]);
    AssertTrue(cs[0] != cs[2]);

    AssertEqual("1;2;3;", log.ToString());

    ScriptMgr.instance.Stop();
    AssertTrue(!ScriptMgr.instance.Busy);

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
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
      BindStartScriptInMgr(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");

    AssertFalse(vm.Tick());
    ScriptMgr.instance.Tick();

    var cs = ScriptMgr.instance.active;
    AssertEqual(3, cs.Count); 
    AssertTrue(cs[0] != cs[1]);
    AssertTrue(cs[1] != cs[2]);
    AssertTrue(cs[0] != cs[2]);

    AssertEqual("2;2;2;", log.ToString());

    ScriptMgr.instance.Stop();
    AssertTrue(!ScriptMgr.instance.Busy);

    vm.Stop();

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

    var fs = new VM.SymbolSpec(TestModuleName, "test").LoadFuncSymbolScript(vm);

    {
      var result = vm.Execute(fs);
      AssertEqual(result.PopRelease().num, 10);
      CommonChecks(vm);
    }

    {
      var result = vm.Execute(fs);
      AssertEqual(result.PopRelease().num, 10);
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

    var fs = new VM.SymbolSpec(TestModuleName, "test").LoadFuncSymbolScript(vm);

    {
      var result = vm.Execute(fs, new StackList<Val>(Val.NewInt(vm, 10), Val.NewInt(vm, 20)));
      AssertEqual(result.PopRelease().num, 10);
      CommonChecks(vm);
    }

    {
      var result = vm.Execute(fs, new StackList<Val>(Val.NewInt(vm, 2), Val.NewInt(vm, 1)));
      AssertEqual(result.PopRelease().num, -1);
      CommonChecks(vm);
    }
  }

  void BindStartScriptInMgr(Types ts)
  {
    {
      var fn = new FuncSymbolNative(new Origin(), "StartScriptInMgr", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) {
            int spawns = (int)stack.PopRelease().num;
            var ptr = stack.Pop();

            for(int i=0;i<spawns;++i)
              ScriptMgr.instance.Start(frm, (VM.FuncPtr)ptr.obj, stack);

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

    public bool Busy {
      get {
        return active.Count > 0;
      }
    }

    public void Start(VM.Frame origin, VM.FuncPtr ptr, ValStack stack)
    {
      var fb = origin.vm.Start(ptr, origin, stack);
      origin.vm.Detach(fb);
      active.Add(fb);
    }

    public void Tick()
    {
      if(active.Count > 0)
        active[0].vm.Tick(active);
    }

    public void Stop()
    {
      for(int i=active.Count;i-- > 0;)
      {
        active[i].vm.Stop(active[i]);
        active.RemoveAt(i);
      }
    }
  }
}
