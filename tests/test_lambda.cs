using System;
using System.Text;
using System.Collections.Generic;
using bhl;

public class TestLambda : BHL_TestBase
{
  [IsTested()]
  public void TestLambdaUselessStatement()
  {
    string bhl = @"

    func test() 
    {
      int a = 1
      func() {}
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"unexpected expression",
      new PlaceAssert(bhl, @"
      func() {}
------^"
      )
    );
  }

  [IsTested()]
  public void TestLambdaCallInplace()
  {
    string bhl = @"
    func dummy() {
    }

    func int test()
    {
      int dummy = 0
      return func int() {
        return 123
      }()
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      //dummy
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.ExitFrame)
      //test
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      //lambda
      .EmitThen(Opcodes.Lambda, new int[] { 9 })
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 123) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      .EmitThen(Opcodes.LastArgToTop, new int[] { 0 })
      .EmitThen(Opcodes.CallPtr, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 123);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCallLambdaInPlaceArray()
  {
    string bhl = @"

    func int test(int a) 
    {
      return func []int(int a) { 
        []int ns = new []int
        ns.Add(a)
        ns.Add(a*2)
        return ns
      }(a)[1] 
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(6, Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCallLambdaInPlaceInvalid()
  {
    string bhl = @"
    func bool test(int a) 
    {
      return func bool(int a) { return a > 2 }.foo 
    }
    ";

    AssertError<Exception>(
      delegate() {
        Compile(bhl);
      },
      "type doesn't support member access via '.'",
      new PlaceAssert(bhl, @"
      return func bool(int a) { return a > 2 }.foo 
----------------------------------------------^"
      )
    );
  }

  [IsTested()]
  public void TestCallLambdaInPlaceInvalid2()
  {
    string bhl = @"

    func bool test(int a) 
    {
      return func bool(int a) { return a > 2 }[10] 
    }
    ";

    AssertError<Exception>(
      delegate() {
        Compile(bhl);
      },
      "accessing not an array/map type",
      new PlaceAssert(bhl, @"
      return func bool(int a) { return a > 2 }[10] 
----------------------------------------------^"
      )
    );
  }

  [IsTested()]
  public void TestLambdaCallInFalseCondition()
  {
    string bhl = @"
    func dummy() {
    }

    func int test()
    {
      if(false) {
        return func int() {
          return 123
        }()
      } else {
        return 321
      }
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 321);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaCallInTrueCondition()
  {
    string bhl = @"
    func dummy() {
    }

    func int test()
    {
      if(true) {
        return func int() {
          return 123
        }()
      } else {
        return 321
      }
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 123);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaCallFromSubFunc()
  {
    string bhl = @"
    func dummy() {
    }

    func int foo() 
    {
      return func int() {
        return 123
      }()
    }

    func int test()
    {
      return foo()
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 123);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaCallAsVar()
  {
    string bhl = @"
    func dummy() {
    }

    func int test()
    {
      func int() a = func int() {
        return 123
      }
      return a()
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 123);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaCallAsVarSeveralTimes()
  {
    string bhl = @"
    func dummy() {
    }

    func int test()
    {
      func int() a = func int() {
        return 123
      }
      return a() + a()
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 246);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaCallAsVarWithArgs()
  {
    string bhl = @"
    func int test()
    {
      func int(int,int) a = func int(int c, int b) {
        return c - b
      }
      return a(42, 1)
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 41);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaCantHaveArgsWithDefaultValues()
  {
    string bhl = @"
    func test()
    {
      var p = func int(int c, int b = 1) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "default argument values not allowed for lambdas",
      new PlaceAssert(bhl, @"
      var p = func int(int c, int b = 1) {
----------------------------------^"
      )
    );
  }
  
  
  [IsTested()]
  public void TestLambdaCallAsVarInFalseCondition()
  {
    string bhl = @"
    func dummy() {
    }

    func int test()
    {
      func int() a = func int() {
        return 123
      }
      if(false) {
        return a()
      } else {
        return 321
      }
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 321);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaCallAsVarInTrueCondition()
  {
    string bhl = @"
    func dummy() {
    }

    func int test()
    {
      func int() a = func int() {
        return 123
      }
      if(true) {
        return a()
      } else {
        return 321
      }
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 123);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaCapturesVar()
  {
    string bhl = @"
    func dummy() {
    }

    func int test()
    {
      int dummy = 123
      return func int() {
        return dummy
      }()
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      //dummy
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.ExitFrame)
      //test
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 123) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      //lambda
      .EmitThen(Opcodes.Lambda, new int[] { 7 })
      .EmitThen(Opcodes.InitFrame, new int[] { 1+1 /*args info*/})
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      .EmitThen(Opcodes.UseUpval, new int[] { 0, 0, 0 })
      .EmitThen(Opcodes.LastArgToTop, new int[] { 0 })
      .EmitThen(Opcodes.CallPtr, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 123);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaCapturesSeveralVars()
  {
    string bhl = @"
    func dummy() {
    }

    func int test()
    {
      int a = 20
      int b = 10
      return func int() {
        int c = 5
        return c + a + b
      }()
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      //dummy
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.ExitFrame)
      //test
      .EmitThen(Opcodes.InitFrame, new int[] { 2 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.SetVar, new int[] { 1 })
      //lambda
      .EmitThen(Opcodes.Lambda, new int[] { 19 })
      .EmitThen(Opcodes.InitFrame, new int[] { 3+1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 5) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 2 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      .EmitThen(Opcodes.UseUpval, new int[] { 0, 1, 0 })
      .EmitThen(Opcodes.UseUpval, new int[] { 1, 2, 0 })
      .EmitThen(Opcodes.LastArgToTop, new int[] { 0 })
      .EmitThen(Opcodes.CallPtr, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 35);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSubLambdaCapturesVar()
  {
    string bhl = @"
    func dummy() {
    }

    func int test()
    {
      int dummy = 123
      return func int() {
        int foo = 321
        return func int() {
          int wow = 123
          return foo
        }()
      }()
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      //dummy
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.ExitFrame)
      //test
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 123) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      //lambda
      .EmitThen(Opcodes.Lambda, new int[] { 41 })
      .EmitThen(Opcodes.InitFrame, new int[] { 1+1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 321) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Lambda, new int[] { 13 })
      .EmitThen(Opcodes.InitFrame, new int[] { 2+1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 123) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      .EmitThen(Opcodes.UseUpval, new int[] { 0, 1, 0 })
      .EmitThen(Opcodes.LastArgToTop, new int[] { 0 })
      .EmitThen(Opcodes.CallPtr, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      .EmitThen(Opcodes.LastArgToTop, new int[] { 0 })
      .EmitThen(Opcodes.CallPtr, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 321);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaChangesVar()
  {
    string bhl = @"

    func foo(func void() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float a = 2
      foo(func() { 
          a = a + 1 
        } 
      )
      return a
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 3);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaChangesSeveralVars()
  {
    string bhl = @"

    func foo(func void() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float a = 2
      float b = 10
      foo(func()
        { 
          a = a + 1 
          b = b * 2
        } 
      )
      return a + b
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 3+20);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaNestedNoCalls()
  {
    string bhl = @"

    func float test() 
    {
      float a = 2
      func() {
          func() fn = func() {
            a = a + 1    
          }
      }()
      return a
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaCapturesNested()
  {
    string bhl = @"

    func foo(func() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float a = 2
      float b = 10
      foo(func() { 
          func() p = func() {
            a = a + 1    
            b = b * 2    
          }
          p()
        } 
      )
      return a+b
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 23);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaSelfCallAndBindValues()
  {
    string bhl = @"
    func test() 
    {
      float a = 2

      func() fn = func func() (float a) { 
        return func() { 
          a = 1
        }
      }(a) 

      fn()
    }
    ";

    var vm = MakeVM(bhl);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaSelfCallAndBindValues2()
  {
    string bhl = @"

    func float test() 
    {
      float a = 2

      float res

      func() fn = func func() (float a, int b) { 
        return func() { 
          res = a + b 
        }
      }(a, 1) 

      a = 100

      fn()

      return res
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 3);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnMultipleLambda()
  {
    string bhl = @"
    func float,string test() 
    {
      return func float,string () 
        { return 30, ""foo"" }()
    }
    ";

    var vm = MakeVM(bhl);
    var fb = Execute(vm, "test");
    AssertEqual(fb.result.PopRelease().num, 30);
    AssertEqual(fb.result.PopRelease().str, "foo");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnMultipleLambdaIncompatibleTypes()
  {
    string bhl = @"

    func float,string test() 
    {
      return func string,string () 
        { return ""bar"", ""foo"" }()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incompatible types: 'float,string' and 'string,string'",
      new PlaceAssert(bhl, @"
      return func string,string () 
-------------^"
        )
    );
  }

  [IsTested()]
  public void TestReturnMultipleLambdaIncompatibleTypes2()
  {
    string bhl = @"

    func string test() 
    {
      return func string,string () 
        { return ""bar"", ""foo"" }()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incompatible types: 'string' and 'string,string'",
      new PlaceAssert(bhl, @"
      return func string,string () 
-------------^"
        )
    );
  }

  [IsTested()]
  public void TestReturnMultipleLambdaViaVars()
  {
    string bhl = @"

    func float,string test() 
    {
      float a,string s = func float,string () 
        { return 30, ""foo"" }()
      return a,s
    }
    ";

    var vm = MakeVM(bhl);
    var fb = Execute(vm, "test");
    AssertEqual(fb.result.PopRelease().num, 30);
    AssertEqual(fb.result.PopRelease().str, "foo");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncPtrReturningArrLambda()
  {
    string bhl = @"

    func test() 
    {
      func []int() ptr =  func []int () {
        return [1,2]
      }
      trace((string)ptr()[1])
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("2", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncPtrReturningArrOfArrLambda()
  {
    string bhl = @"

    func test()
    {
      []func []string() ptr = [
        func []string () { return [""a"",""b""] },
        func []string () { return [""c"",""d""] }
      ]
      trace(ptr[1]()[0])
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("c", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncPtrSeveralLambdaRunning()
  {
    string bhl = @"

    coro func test() 
    {
      coro func(string) ptr = coro func(string arg) {
        trace(arg)
        yield()
      }
      paral {
        yield ptr(""FOO"")
        yield ptr(""BAR"")
      }
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
    AssertEqual("FOOBAR", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaPassAsVar()
  {
    string bhl = @"

    func foo(func() fn)
    {
      fn()
    }

    func test() 
    {
      func() fun = 
        func()
        { 
          trace(""HERE"")
        }             

      foo(fun)
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test", Val.NewNum(vm, 3));
    AssertEqual("HERE", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaVarSeveralTimes()
  {
    string bhl = @"
    func test() 
    {
      func() fun = 
        func()
        { 
          trace(""HERE"")
        }             

      fun()
      fun()
      fun()
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("HEREHEREHERE", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaCaptureVarsHiding()
  {
    string bhl = @"

    func float time()
    {
      return 42
    }

    func void foo(float time)
    {
      func() fn = func()
      {
        float b = time
        trace((string)b)
      }
      fn()
    }

    func test() 
    {
      foo(10)
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
  public void TestLambdaLocalVarsConflict()
  {
    string bhl = @"
    func test() 
    {
      float a = 10
      float b = 20
      start(
        func()
        { 
          float a = a 
        }             
      ) 
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "already defined symbol 'a'",
      new PlaceAssert(bhl, @"
          float a = a 
----------------^"
     )
    );
  }

  [IsTested()]
  public void TestStartSeveralLambdas()
  {
    string bhl = @"
    func test() 
    {
      start(
        func()
        { 
          trace(""HERE"")
        }             
      ) 
    }

    func test2() 
    {
      start(
        func()
        { 
          trace(""HERE2"")
        }             
      ) 
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Execute(vm, "test2");
    AssertEqual("HEREHERE2", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStartLambdaSeveralTimes()
  {
    string bhl = @"
    func test() 
    {
      start(
        func()
        { 
          trace(""HERE"")
        }             
      ) 
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Execute(vm, "test");
    AssertEqual("HEREHERE", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStartLambdaCaptureVars()
  {
    string bhl = @"
    func test() 
    {
      float a = 10
      float b = 20
      start(
        func()
        { 
          float k = a 
          trace((string)k + (string)b)
        }             
      ) 
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("1020", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStartLambdaCaptureVarsNested()
  {
    string bhl = @"
    func test() 
    {
      float a = 10
      float b = 20
      start(
        func()
        { 
          float k = a 

          func() fn = func() 
          {
            trace((string)k + (string)b)
          }

          fn()
        }             
      ) 
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("1020", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStartLambdaCaptureVarsNested2()
  {
    string bhl = @"
    func test() 
    {
      float a = 10
      float b = 20
      start(
        func()
        { 
          float k = a 

          func() fn = func() 
          {
            func() fn = func() 
            {
              trace((string)k + (string)b)
            }

            fn()
          }

          fn()
        }             
      ) 
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("1020", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStartLambdaCaptureVars2()
  {
    string bhl = @"

    func call(func() fn, func() fn2)
    {
      start(fn2)
    }

    func foo(float a = 1, float b = 2)
    {
      call(fn2 :
        func()
        { 
          float k = a 
          trace((string)k + (string)b)
        },
        fn : func() { }
      ) 
    }

    func bar()
    {
      call(
        fn2 :
        func()
        { 
          trace(""HEY!"")
        },             
        fn : func() { }
      ) 
    }

    func test() 
    {
      foo(10, 20)
      bar()
      foo()
      bar()
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("1020HEY!12HEY!", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStartLambdaCaptureCopyOfLoopVars()
  {
    string bhl = @"
    func test() 
    {
      for(int i=0;i<3;i++)
      {
        start(func() [i] { 
            trace((string)i)
          }) 
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("012", log.ToString());
    CommonChecks(vm);
  }

  public void TestClosure()
  {
    string bhl = @"

    func float test() 
    {
      return func func float(float) (float a) {
        return func float (float b) { return a + b }
      }(2)(3)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 5);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaUsesValueByRef()
  {
    string bhl = @"

    func foo(func() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float a = 2
      foo(func() { a = a + 1 } )
      return a
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 3);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaUsesArrayByRef()
  {
    string bhl = @"

    func foo(func() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      []float a = []
      foo(func() { a.Add(10) } )
      return a[0]
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 10);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaReplacesArrayValueByRef()
  {
    string bhl = @"

    func foo(func() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      []float a
      foo(func() { 
          a = [42]
        } 
      )
      return a[0]
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 42);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaReplacesArrayValueByRef2()
  {
    string bhl = @"

    func foo(func() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      []float a = []
      foo(func() { 
          a = [42]
        } 
      )
      return a[0]
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 42);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaReplacesFuncPtrByRef()
  {
    string bhl = @"

    func foo(func() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      func float() a
      foo(func() { 
          a = func float () { return 42 }
        } 
      )
      return a()
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 42);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaReplacesFuncPtrByRef2()
  {
    string bhl = @"

    func foo(func() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      func float() a = func float() { return 45 } 
      foo(func() { 
          a = func float () { return 42 }
        } 
      )
      return a()
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 42);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaVariableShadowing()
  {
    string bhl = @"
    func test(int a) 
    {
      func() {
        int b = 10
        int a = 20
      }()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "already defined symbol 'a'",
      new PlaceAssert(typeof(ParseError), bhl, @"
        int a = 20
------------^"
      )
    );
  }

}
