using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using bhl;
using bhl.marshall;

public class TestVM : BHL_TestBase
{
  [IsTested()]
  public void TestReturnNumConstant()
  {
    string bhl = @"
    func int test()
    {
      return 123
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 123) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 1);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 123);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNumberPrecision()
  {
    DoTestReturnNum(bhlnum: "100", expected_num: 100);
    DoTestReturnNum(bhlnum: "2147483647", expected_num: 2147483647);
    DoTestReturnNum(bhlnum: "2147483648", expected_num: 2147483648);
    DoTestReturnNum(bhlnum: "100.5", expected_num: 100.5);
    DoTestReturnNum(bhlnum: "2147483648.1", expected_num: 2147483648.1);
  }

  void DoTestReturnNum(string bhlnum, double expected_num) 
  {
    string bhl = @"
      
    func float test() 
    {
      return "+bhlnum+@"
    }
    ";

    var c = Compile(bhl);
    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    var num = fb.result.PopRelease().num;

    AssertEqual(num, expected_num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnTrueBoolConstant()
  {
    string bhl = @"
    func bool test()
    {
      return true
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 1);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnFalseBoolConstant()
  {
    string bhl = @"
    func bool test()
    {
      return false
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, false) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 1);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(!fb.result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnTrueNegated()
  {
    string bhl = @"
    func bool test()
    {
      return !true
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
      .EmitThen(Opcodes.UnaryNot)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 1);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(!fb.result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnFalseNegated()
  {
    string bhl = @"
    func bool test()
    {
      return !false
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, false) })
      .EmitThen(Opcodes.UnaryNot)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 1);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnStringConstant()
  {
    string bhl = @"
    func string test()
    {
      return ""Hello""
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "Hello") })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 1);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "Hello");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestEmptyFuncBody()
  {
    {
      string bhl = @"
      func test() {}
      ";

      var c = Compile(bhl);

      var expected = 
        new ModuleCompiler()
        .UseCode()
        .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
        .EmitThen(Opcodes.ExitFrame)
        ;
      AssertEqual(c, expected);

      var vm = MakeVM(c);
      vm.Start("test");
      AssertFalse(vm.Tick());
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func test(){}
      ";

      var c = Compile(bhl);

      var expected = 
        new ModuleCompiler()
        .UseCode()
        .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
        .EmitThen(Opcodes.ExitFrame)
        ;
      AssertEqual(c, expected);

      var vm = MakeVM(c);
      vm.Start("test");
      AssertFalse(vm.Tick());
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func test(){
      }
      ";

      var c = Compile(bhl);

      var expected = 
        new ModuleCompiler()
        .UseCode()
        .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
        .EmitThen(Opcodes.ExitFrame)
        ;
      AssertEqual(c, expected);

      var vm = MakeVM(c);
      vm.Start("test");
      AssertFalse(vm.Tick());
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func test() {
      }
      ";

      var c = Compile(bhl);

      var expected = 
        new ModuleCompiler()
        .UseCode()
        .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
        .EmitThen(Opcodes.ExitFrame)
        ;
      AssertEqual(c, expected);

      var vm = MakeVM(c);
      vm.Start("test");
      AssertFalse(vm.Tick());
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestStrConcat()
  {
    string bhl = @"
      
    func string test(int k) 
    {
      return (string)k + (string)(k*2)
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().str;
    AssertEqual(res, "36");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImplicitIntArgsCast()
  {
    string bhl = @"

    func float foo(float a, float b)
    {
      return a + b
    }
      
    func float test() 
    {
      return foo(1, 2.0)
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 3);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImplicitIntArgsCastNativeFunc()
  {
    string bhl = @"

    func float bar(float a)
    {
      return a
    }
      
    func float test() 
    {
      return bar(a : min(1, 0.3))
    }
    ";

    var ts = new Types();
    BindMin(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 0.3f);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBindFunctionWithDefaultArgs()
  {
    string bhl = @"
      
    func float test(int k) 
    {
      return func_with_def(k)
    }
    ";

    var ts = new Types();

    {
      var fn = new FuncSymbolNative("func_with_def", ts.T("float"), 1,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var b = args_info.IsDefaultArgUsed(0) ? 2 : frm.stack.PopRelease().num;
            var a = frm.stack.PopRelease().num;

            frm.stack.Push(Val.NewFlt(frm.vm, a + b));

            return null;
          },
          new FuncArgSymbol("a", ts.T("float")),
          new FuncArgSymbol("b", ts.T("float"))
        );

      ts.ns.Define(fn);
    }

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 42)).result.PopRelease().num;
    AssertEqual(res, 44);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBindFunctionWithDefaultArgs2()
  {
    string bhl = @"

    func float foo(float a)
    {
      return a
    }
      
    func float test(int k) 
    {
      return func_with_def(k, foo(k)+1)
    }
    ";

    var ts = new Types();

    {
      var fn = new FuncSymbolNative("func_with_def", ts.T("float"), 1,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var b = args_info.IsDefaultArgUsed(0) ? 2 : frm.stack.PopRelease().num;
            var a = frm.stack.PopRelease().num;

            frm.stack.Push(Val.NewFlt(frm.vm, a + b));

            return null;
          },
          new FuncArgSymbol("a", ts.T("float")),
          new FuncArgSymbol("b", ts.T("float"))
        );

      ts.ns.Define(fn);
    }

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 42)).result.PopRelease().num;
    AssertEqual(res, 42+43);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBindFunctionWithDefaultArgs3()
  {
    string bhl = @"

    func float test() 
    {
      return func_with_def()
    }
    ";

    var ts = new Types();

    {
      var fn = new FuncSymbolNative("func_with_def", ts.T("float"), 1,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var a = args_info.IsDefaultArgUsed(0) ? 14 : frm.stack.PopRelease().num;

            frm.stack.Push(Val.NewFlt(frm.vm, a));

            return null;
          },
          new FuncArgSymbol("a", ts.T("float"))
        );

      ts.ns.Define(fn);
    }

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 14);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBindFunctionWithDefaultArgsOmittingSome()
  {
    string bhl = @"
      
    func float test(int k) 
    {
      return func_with_def(b : k)
    }
    ";

    var ts = new Types();

    {
      var fn = new FuncSymbolNative("func_with_def", ts.T("float"), 2,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var b = args_info.IsDefaultArgUsed(1) ? 2 : frm.stack.PopRelease().num;
            var a = args_info.IsDefaultArgUsed(0) ? 10 : frm.stack.PopRelease().num;

            frm.stack.Push(Val.NewFlt(frm.vm, a + b));

            return null;
          },
          new FuncArgSymbol("a", Types.Int),
          new FuncArgSymbol("b", Types.Int)
        );

      ts.ns.Define(fn);
    }

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 42)).result.PopRelease().num;
    AssertEqual(res, 52);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFailureBeforeReturn()
  {
    string bhl = @"

    func float foo()
    {
      fail()
      return 100
    }
      
    func float test() 
    {
      float val = foo()
      return val
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    vm.Tick();
    AssertEqual(fb.status, BHS.FAILURE);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFailureInNativeFunction()
  {
    string bhl = @"

    func float test() 
    {
      float val = foo()
      return val
    }
    ";

    var ts = new Types();

    {
      var fn = new FuncSymbolNative("foo", ts.T("float"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
            status = BHS.FAILURE; 
            return null;
          });
      ts.ns.Define(fn);
    }

    var vm = MakeVM(bhl, ts);
    var fb = vm.Start("test");
    vm.Tick();
    AssertEqual(fb.status, BHS.FAILURE);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSeveralReturns()
  {
    string bhl = @"
      
    func float test() 
    {
      return 300
      float k = 1
      return 100
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 300);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnDefaultVar()
  {
    string bhl = @"
    func float test()
    {
      float k
      return k
    }
    ";

    var ts = new Types();
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.DeclVar, new int[] { 0, ConstIdx(c, ts.T("float")) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 0);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnVar()
  {
    string bhl = @"
    func float test()
    {
      float k = 42
      return k
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 42) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 42);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnVoid()
  {
    string bhl = @"
      
    func void test() 
    {
      trace(""HERE"")
      return
      trace(""NOT HERE"")
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    
    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("HERE", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestMultiReturn()
  {
    string bhl = @"
      
    func float,string test() 
    {
      return 300,""bar""
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "bar") })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 300) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 2 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = Execute(vm, "test");
    var num = fb.result.PopRelease().num;
    var str = fb.result.PopRelease().str;
    AssertEqual(num, 300);
    AssertEqual(str, "bar");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestMultiReturnVarsAssign()
  {
    string bhl = @"
    func float,float foo() 
    {
      return 300,100
    }
      
    func float test() 
    {
      float f1,float f2 = foo()
      return f1-f2
    }
    ";

    var vm = MakeVM(bhl);
    var fb = Execute(vm, "test");
    var num = fb.result.PopRelease().num;
    AssertEqual(num, 200);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestMultiReturnVarAssign2()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func float,string test() 
    {
      string s
      float a,s = foo()
      return a,s
    }
    ";

    var vm = MakeVM(bhl);
    var fb = Execute(vm, "test");
    var num = fb.result.PopRelease().num;
    var str = fb.result.PopRelease().str;
    AssertEqual(num, 100);
    AssertEqual(str, "bar");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnMultipleVarAssignObjectAttr()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func float,string test() 
    {
      string s
      Color c = {}
      c.r,s = foo()
      return c.r,s
    }
    ";

    var ts = new Types();
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var fb = Execute(vm, "test");
    AssertEqual(fb.result.PopRelease().num, 100);
    AssertEqual(fb.result.PopRelease().str, "bar");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnMultipleVarAssignArrItem()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func float,string test() 
    {
      []string s = [""""]
      float r,s[0] = foo()
      return r,s[0]
    }
    ";

    var vm = MakeVM(bhl);
    var fb = Execute(vm, "test");
    AssertEqual(fb.result.PopRelease().num, 100);
    AssertEqual(fb.result.PopRelease().str, "bar");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnMultipleVarAssignArrItem2()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func float,string test() 
    {
      string s
      []Color c = [{}]
      c[0].r,s = foo()
      return c[0].r,s
    }
    ";

    var ts = new Types();
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var fb = Execute(vm, "test");
    AssertEqual(fb.result.PopRelease().num, 100);
    AssertEqual(fb.result.PopRelease().str, "bar");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnMultipleVarAssignNoSuchSymbol()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func float,string test() 
    {
      float a,s = foo()
      return a,s
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "symbol 's' not resolved"
    );
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
      "incompatible types"
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
      "incompatible types"
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
  public void TestReturnMultipleNotEnough()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func void test() 
    {
      float s = foo()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "multi return size doesn't match destination"
    );
  }

  [IsTested()]
  public void TestReturnMultipleTooMany()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func void test() 
    {
      float s,string a,int f = foo()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "multi return size doesn't match destination"
    );
  }

  [IsTested()]
  public void TestReturnNotAllPathsReturnValue()
  {
    string bhl = @"
    func int test() 
    {
      paral_all {
        {
          return 1
        }
        yield()
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "matching 'return' statement not found"
    );
  }

  [IsTested()]
  public void TestReturnNotFoundInLambda()
  {
    string bhl = @"
    func foo() { }

    func test() 
    {
      func bool() { foo() }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "matching 'return' statement not found"
    );
  }

  [IsTested()]
  public void TestReturnMultipleInFuncBadCast()
  {
    string bhl = @"

    func float,string foo() 
    {
      return ""bar"",100
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "@(5,13) : incompatible types"
    );
  }

  [IsTested()]
  public void TestReturnMultipleBadCast()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func void test() 
    {
      string a,float s = foo()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "@(10,13) : incompatible types"
    );
  }

  [IsTested()]
  public void TestReturnMultipleFromBindings()
  {
    string bhl = @"
      
    func float,string test() 
    {
      return func_mult()
    }
    ";

    var ts = new Types();

    {
      var fn = new FuncSymbolNative("func_mult", ts.T("float", "string"),
          delegate(VM.Frame frm, FuncArgsInfo arg_info, ref BHS status)
          {
            frm.stack.Push(Val.NewStr(frm.vm, "foo"));
            frm.stack.Push(Val.NewNum(frm.vm, 42));
            return null;
          }
        );
      ts.ns.Define(fn);
    }

    var vm = MakeVM(bhl, ts);
    var fb = Execute(vm, "test");
    AssertEqual(fb.result.PopRelease().num, 42);
    AssertEqual(fb.result.PopRelease().str, "foo");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnMultiple3()
  {
    string bhl = @"
      
    func float,string,int test() 
    {
      return 100,""foo"",3
    }
    ";

    var vm = MakeVM(bhl);
    var fb = Execute(vm, "test");
    AssertEqual(fb.result.PopRelease().num, 100);
    AssertEqual(fb.result.PopRelease().str, "foo");
    AssertEqual(fb.result.PopRelease().num, 3);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnMultiple4FromBindings()
  {
    string bhl = @"
      
    func float,string,int,float test() 
    {
      float a,string b,int c,float d = func_mult()
      return a,b,c,d
    }
    ";

    var ts = new Types();

    {
      var fn = new FuncSymbolNative("func_mult", ts.T("float","string","int","float"),
          delegate(VM.Frame frm, FuncArgsInfo arg_info, ref BHS status)
          {
            frm.stack.Push(Val.NewFlt(frm.vm, 42.5));
            frm.stack.Push(Val.NewNum(frm.vm, 12));
            frm.stack.Push(Val.NewStr(frm.vm, "foo"));
            frm.stack.Push(Val.NewNum(frm.vm, 104));
            return null;
          }
        );
      ts.ns.Define(fn);
    }

    var vm = MakeVM(bhl, ts);
    var fb = Execute(vm, "test");
    AssertEqual(fb.result.PopRelease().num, 104);
    AssertEqual(fb.result.PopRelease().str, "foo");
    AssertEqual(fb.result.PopRelease().num, 12);
    AssertEqual(fb.result.PopRelease().num, 42.5);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestVarValueIsUselessStatement()
  {
    string bhl = @"

    func int test() 
    {
      float foo = 1
      foo
      return 2
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"useless statement"
    );
  }

  [IsTested()]
  public void TestFuncPtrReturnNonConsumed()
  {
    string bhl = @"

    func int test() 
    {
      func int() ptr = func int() {
        return 1
      }
      ptr()
      return 2
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncPtrUselessStatement()
  {
    string bhl = @"

    func test() 
    {
      int a = 1
      suspend
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"useless statement"
    );
  }

  [IsTested()]
  public void TestFuncPtrArrReturnNonConsumed()
  {
    string bhl = @"

    func int test() 
    {
      func []int() ptr = func []int() {
        return [1,2]
      }
      ptr()
      return 2
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestAttributeUselessStatement()
  {
    string bhl = @"

    func int test() 
    {
      Color c = new Color
      c.r
      return 2
    }
    ";

    var ts = new Types();

    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      @"useless statement"
    );
  }

  [IsTested()]
  public void TestReturnNonConsumed()
  {
    string bhl = @"

    func float foo() 
    {
      return 100
    }
      
    func int test() 
    {
      foo()
      return 2
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnNonConsumedInParal()
  {
    string bhl = @"

    func float foo() 
    {
      return 100
    }
      
    func int test() 
    {
      paral {
        foo()
        suspend()
      }
      return 2
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnMultipleNonConsumed()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func int test() 
    {
      foo()
      return 2
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLogicalAnd()
  {
    string bhl = @"
    func bool test()
    {
      return false && true
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, false) })
      .EmitThen(Opcodes.JumpPeekZ, new int[] { 5 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
      .EmitThen(Opcodes.And)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 2);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.result.PopRelease().bval == false);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLogicalOr()
  {
    string bhl = @"
    func bool test()
    {
      return false || true
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, false) })
      .EmitThen(Opcodes.JumpPeekNZ, new int[] { 5 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
      .EmitThen(Opcodes.Or)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 2);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBitAnd()
  {
    string bhl = @"
    func int test()
    {
      return 3 & 1
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.BitAnd)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 2);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBitOr()
  {
    string bhl = @"
    func int test()
    {
      return 3 | 4
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 4) })
      .EmitThen(Opcodes.BitOr)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 2);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 7);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestMod()
  {
    string bhl = @"
    func int test()
    {
      return 3 % 2
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .EmitThen(Opcodes.Mod)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 2);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 1);
    CommonChecks(vm);
  }
  
  [IsTested()]
  public void TestModDouble()
  {
    string bhl = @"
    func float test()
    {
      return 2.7 % 2
    }
    ";

    var c = Compile(bhl);


    var expected =
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2.7) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .EmitThen(Opcodes.Mod)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 2);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    double expectedNum = 2.7 % 2;
    AssertEqual(fb.result.PopRelease().num, expectedNum);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestEmptyParenExpression()
  {
    string bhl = @"

    func test() 
    {
      ().foo
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"mismatched input"
    );
  }

  [IsTested()]
  public void TestSimpleExpression()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      return ((k*100) + 100) / 400
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSemicolonStatementSeparator()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      int a = 100; ; int b = 100; int c = 400
      return ((k*a) + b) / c
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDanglingBrackets()
  {
    string bhl = @"

    func test() 
    {
      ()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "mismatched input"
    );
  }

  [IsTested()]
  public void TestDanglingBrackets2()
  {
    string bhl = @"

    func foo() 
    {
    }

    func test() 
    {
      foo() ()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "no func to call"
    );
  }

  [IsTested()]
  public void TestWriteReadVar()
  {
    string bhl = @"
    func int test() 
    {
      int a = 123
      return a + a
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 123) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 1);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 246);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLocalVariables()
  {
    string bhl = @"
      
    func float foo(float k)
    {
      float b = 5
      return k + 5
    }

    func float test(float k) 
    {
      float b = 10
      return k * foo(b)
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().num, 45);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLocalVarHiding()
  {
    string bhl = @"
      
    func float time()
    {
      return 42
    }

    func float bar(float time)
    {
      return time
    }

    func float test() 
    {
      return bar(100)
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 100);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLocalVarConflictsWithFunc()
  {
    string bhl = @"
      
    func float time()
    {
      return 42
    }

    func float bar(float time)
    {
      if(time == 0)
      {
        return time
      }
      else
      {
        return time()
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "symbol is not a function"
    );
  }

  [IsTested()]
  public void TestVarSelfDecl()
  {
    string bhl = @"
      
    func float test() 
    {
      float k = k
      return k
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "symbol 'k' not resolved"
    );
  }

  [IsTested()]
  public void TestDeclVarIntWithoutValue()
  {
    string bhl = @"
    func int test()
    {
      int i
      return i
    }
    ";

    var ts = new Types();
    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.DeclVar, new int[] { 0, ConstIdx(c, ts.T("int")) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 0);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDeclVarFloatWithoutValue()
  {
    string bhl = @"
    func float test()
    {
      float i
      return i
    }
    ";

    var ts = new Types();
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.DeclVar, new int[] { 0, ConstIdx(c, ts.T("float")) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 0);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDeclVarStringWithoutValue()
  {
    string bhl = @"
    func string test()
    {
      string i
      return i
    }
    ";

    var ts = new Types();
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.DeclVar, new int[] { 0, ConstIdx(c, ts.T("string"))})
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDeclVarBoolWithoutValue()
  {
    string bhl = @"
    func bool test()
    {
      bool i
      return i
    }
    ";

    var ts = new Types();
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.DeclVar, new int[] { 0, ConstIdx(c, ts.T("bool")) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertFalse(fb.result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestAssignVars()
  {
    string bhl = @"
    func float test() 
    {
      float k
      k = 42
      float r
      r = k 
      return r
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 42);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestAnyAndNullConstant()
  {
    string bhl = @"
    func bool test()
    {
      any fn = null
      return fn == null
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { 0 })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { 0 })
      .EmitThen(Opcodes.Equal)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 1);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUnaryNegVar()
  {
    string bhl = @"
    func int test() 
    {
      int x = 1
      return -x
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.UnaryNeg)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 1);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, -1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestAdd()
  {
    string bhl = @"
    func int test() 
    {
      return 10 + 20
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 2);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 30);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSubtract()
  {
    string bhl = @"
    func int test() 
    {
      return 20 - 10
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.Sub)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 2);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 10);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDivision()
  {
    string bhl = @"
    func int test() 
    {
      return 20 / 10
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestMultiply()
  {
    string bhl = @"
    func int test() 
    {
      return 10 * 20
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 200);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestGT()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k > 2
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 4)).result.PopRelease().num;
    AssertEqual(num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNotGT()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k > 5
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 4)).result.PopRelease().num;
    AssertEqual(num, 0);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLT()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k < 20
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 4)).result.PopRelease().num;
    AssertEqual(num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNotLT()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k < 2
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 4)).result.PopRelease().num;
    AssertEqual(num, 0);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestGTE()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k >= 2
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 4)).result.PopRelease().num;
    AssertEqual(num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestGTE2()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k >= 2
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLTE()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k <= 21
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 20)).result.PopRelease().num;
    AssertEqual(num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLTE2()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k <= 20
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 20)).result.PopRelease().num;
    AssertEqual(num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestEqNumber()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k == 2
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestEqString()
  {
    string bhl = @"
      
    func bool test(string k) 
    {
      return k == ""b""
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewStr(vm, "b")).result.PopRelease().num;
    AssertEqual(num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNotEqNum()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k == 2
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 20)).result.PopRelease().num;
    AssertEqual(num, 0);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNotEqString()
  {
    string bhl = @"
      
    func bool test(string k) 
    {
      return k != ""c""
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewStr(vm, "b")).result.PopRelease().num;
    AssertEqual(num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNotEqString2()
  {
    string bhl = @"
      
    func bool test(string k) 
    {
      return k == ""c""
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewStr(vm, "b")).result.PopRelease().num;
    AssertEqual(num, 0);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestConstantWithBigIndex()
  {
    string bhl = @"
    func int test()
    {
      return 1+2+3+4+5+6+7+8+9+10+11+12+13+14+15+16+17+18+19+20
      +21+22+23+24+25+26+27+28+29+30+31+32+33+34+35+36+37+38+39
      +40+41+42+43+44+45+46+47+48+49+50+51+52+53+54+55+56+57+58
      +59+60+61+62+63+64+65+66+67+68+69+70+71+72+73+74+75+76+77
      +78+79+80+81+82+83+84+85+86+87+88+89+90+91+92+93+94+95+96
      +97+98+99+100+101+102+103+104+105+106+107+108+109+110+111
      +112+113+114+115+116+117+118+119+120+121+122+123+124+125
      +126+127+128+129+130+131+132+133+134+135+136+137+138+139
      +140+141+142+143+144+145+146+147+148+149+150+151+152+153
      +154+155+156+157+158+159+160+161+162+163+164+165+166+167
      +168+169+170+171+172+173+174+175+176+177+178+179+180+181
      +182+183+184+185+186+187+188+189+190+191+192+193+194+195
      +196+197+198+199+200+201+202+203+204+205+206+207+208+209
      +210+211+212+213+214+215+216+217+218+219+220+221+222+223
      +224+225+226+227+228+229+230+231+232+233+234+235+236+237
      +238+239+240+241+242+243+244+245+246+247+248+249+250+251
      +252+253+254+255+256+257+258+259+260
    }
    ";

    var c = Compile(bhl);

    AssertEqual(c.constants.Count, 260);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 33930);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStringConcat()
  {
    string bhl = @"
    func string test() 
    {
      return ""Hello "" + ""world !""
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "Hello world !");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrNewLine()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\n""
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "bar\n");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrNewLine2()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\n\n""
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "bar\n\n");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrNewLineEscape()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\\n""
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "bar\\n");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrNewLineEscape2()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\\n\n""
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "bar\\n\n");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrNewLineEscape3()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\\n\\n""
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "bar\\n\\n");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrTab()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\t""
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "bar\t");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrTab2()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\t\t""
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "bar\t\t");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrTabEscape()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\\t""
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "bar\\t");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrTabEscape2()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\\t\t""
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "bar\\t\t");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrTabEscape3()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\\t\\t""
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "bar\\t\\t");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestAddSameConstants()
  {
    string bhl = @"
    func int test() 
    {
      return 10 + 10
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 1);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 20);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPostOpAddAssign()
  {
    string bhl = @"
      
    func int test() 
    {
      int k
      k += 10
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 10);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPostOpSubAssign()
  {
    string bhl = @"
      
    func int test() 
    {
      int k
      k -= 10
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, -10);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPostOpMulAssign()
  {
    string bhl = @"
      
    func int test() 
    {
      int k = 1
      k *= 10
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 10);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPostOpDivAssign()
  {
    string bhl = @"
      
    func int test() 
    {
      int k = 10
      k /= 10
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPostOpAddAssignStringNotAllowed()
  {
    string bhl = @"
      
    func test() 
    {
      string k
      k += ""foo""
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incompatible types"
    );
  }

  [IsTested()]
  public void TestPostOpSubAssignStringNotAllowed()
  {
    string bhl = @"
      
    func test() 
    {
      string k
      k -= ""foo""
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incompatible types"
    );
  }

  [IsTested()]
  public void TestPostOpMulAssignStringNotAllowed()
  {
    string bhl = @"
      
    func test() 
    {
      string k
      k *= ""foo""
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incompatible types"
    );
  }

  [IsTested()]
  public void TestPostOpDivAssignStringNotAllowed()
  {
    string bhl = @"
      
    func test() 
    {
      string k
      k /= ""foo""
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incompatible types"
    );
  }

  [IsTested()]
  public void TestPostOpAssignExpCompatibleTypes()
  {
    string bhl = @"
      
    func float test() 
    {
      float k = 2.1
      int a = 1
      k += a
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 3.1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParenthesisExpression()
  {
    string bhl = @"
    func int test() 
    {
      return 10 * (20 + 30)
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 30) })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Mul)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 3);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 500);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLocalScopeVarHiding()
  {
    string bhl = @"

    func test() 
    {
      int i = 1
      {
        int i = 2
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "already defined symbol 'i'"
    );
  }

  [IsTested()]
  public void TestLocalScopeVarHidingInSubSubScope()
  {
    string bhl = @"

    func test() 
    {
      int i = 1
      {
        {
          {
            int i = 2
          }
        }
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "already defined symbol 'i'"
    );
  }

  [IsTested()]
  public void TestLocalScopeVarOverridesGlobalVar()
  {
    string bhl = @"

    int i = 10
    func int test() 
    {
      int i = 1
      return i
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLocalScopeForVars()
  {
    string bhl = @"

    func test() 
    {
      {
        int i = 1
        i = i + 1
        trace((string)i)
      }
      {
        string i
        i = ""foo""
        trace((string)i)
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");

    var fn = (FuncSymbolScript)vm.ResolveNamedByPath("test");
    AssertEqual(1+1/*for now*/, fn.local_vars_num);

    AssertEqual("2foo", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLocalScopeForVarsInParal()
  {
    string bhl = @"

    func test() 
    {
      paral_all {
        {
          int i = 1
          i = i + 1
          yield()
          trace((string)i)
        }
        {
          string i
          i = ""foo""
          yield()
          trace((string)i)
        }
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");

    AssertEqual("2foo", log.ToString());

    var fn = (FuncSymbolScript)vm.ResolveNamedByPath("test");
    AssertEqual(2, fn.local_vars_num);

    CommonChecks(vm);
  }

  [IsTested()]
  public void TestVarDeclMustBeInUpperScope()
  {
    string bhl = @"

    func test() 
    {
      {
        int i = 1
        i = i + 1
      }
      {
        i = i + 2
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "symbol 'i' not resolved"
    );
  }

  [IsTested()]
  public void TestVarDeclMustBeInUpperScope2()
  {
    string bhl = @"

    func test() 
    {
      paral_all {
        {
          int i = 1
          {
            i = i + 1
          }
        }
        {
          if(i == 2) {
            suspend()
          }
        }
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "symbol 'i' not resolved"
    );
  }

  [IsTested()]
  public void TestSimpleFuncCall()
  {
    string bhl = @"
    func int bar() 
    {
      return 123
    }
    func int test()
    {
      return bar()
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 123) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Call, new int[] { 0, 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 1);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 123);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNotAllowedAssignAFuncCall()
  {
    {
      string bhl = @"
      func int bar() 
      {
        return 123
      }
      func test()
      {
        bar() = 1
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "invalid assignment"
      );
    }

    {
      string bhl = @"
      func test()
      {
        func int () { return 1 }() = 1
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "mismatched input"
      );
    }

    {
      string bhl = @"
      func test()
      {
        func int() f = func int () { return 1 }
        f() = 1
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "invalid assignment"
      );
    }
  }

  [IsTested()]
  public void TestSimpleSubFuncsReturn()
  {
    string bhl = @"
    func int wow() {
      return 1
    }

    func int foo() {
      return wow()
    }

    func int bar() {
      return foo()
    }

    func int test() {
      return bar()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncDeclOrderIsIrrelevant()
  {
    string bhl = @"
    func int bar() {
      return foo()
    }

    func int foo() {
      return wow()
    }

    func int test() {
      return bar()
    }

    func int wow() {
      return 1
    }

    ";

    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSimpleNativeFunc()
  {
    string bhl = @"
    func test() 
    {
      trace(""foo"")
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    var fn = BindTrace(ts, log);

    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "foo") })
      .EmitThen(Opcodes.CallNative, new int[] { ts.nfunc_index.IndexOf(fn), 1 })
      .EmitThen(Opcodes.ExitFrame)
    ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("foo", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSimpleNativeFuncReturnValue()
  {
    string bhl = @"
    func int test() 
    {
      return answer42()
    }
    ";

    var ts = new Types();
    
    var fn = new FuncSymbolNative("answer42", Types.Int,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.stack.Push(Val.NewNum(frm.vm, 42));
          return null;
        } 
    );
    ts.ns.Define(fn);

    var vm = MakeVM(bhl, ts);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(42, num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSimpleNativeFuncWithSeveralArgs()
  {
    string bhl = @"
    func int test() 
    {
      return answer(1, 2)
    }
    ";

    var ts = new Types();
    
    var fn = new FuncSymbolNative("answer", Types.Int,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          
          var b = frm.stack.PopRelease().num;
          var a = frm.stack.PopRelease().num;

          frm.stack.Push(Val.NewFlt(frm.vm, b-a));
          return null;
        }, 
        new FuncArgSymbol("a", ts.T("int")),
        new FuncArgSymbol("b", ts.T("int"))
    );
    ts.ns.Define(fn);

    var vm = MakeVM(bhl, ts);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(1, num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeFuncBindConflict()
  {
    string bhl = @"
    func trace(string hey)
    {
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "already defined symbol 'trace'"
    );
  }

  [IsTested()]
  public void TestRecursion()
  {
    string bhl = @"
      
    func float mult(float k)
    {
      if(k == 0) {
        return 1
      }
      return 2 * mult(k-1)
    }

    func float test(float k) 
    {
      return mult(k)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().num;
    AssertEqual(num, 8);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncAlreadyDeclaredArg()
  {
    string bhl = @"
    func foo(int k, int k)
    {
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "already defined symbol 'k'"
    );
  }

  [IsTested()]
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
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(1, num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(41, num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeFuncPtrWithArgs()
  {
    string bhl = @"

    func test() 
    {
      func(string) ptr = trace
      ptr(""Hey"")
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("Hey", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIfCondition()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      if( 1 > 2 )
      {
        x1 = 10
      }
      
      return x1
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .EmitThen(Opcodes.GT)
      .EmitThen(Opcodes.JumpZ, new int[] { 6 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 4);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 100);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestEmptyIfBody()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      if(1 > 2) 
      {}
      
      return x1
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 100);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIfElseCondition()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 0

      if( 2 > 1 )
      {
        x1 = 10
      }
      else
      {
        x1 = 20
      }
      
      return x1
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.GT)
      .EmitThen(Opcodes.JumpZ, new int[] { 9 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Jump, new int[] { 6 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 5);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 10);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestMultiIfElseCondition()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 0

      if(0 > 1)
      {
        x1 = 10
      }
      else if(-1 > 1)
      {
        x1 = 30
      }
      else if(3 > 1)
      {
        x1 = 20
      }
      else
      {
        x1 = 40
      }
      
      return x1
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.GT)
      .EmitThen(Opcodes.JumpZ, new int[] { 9 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Jump, new int[] { 19 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.UnaryNeg)
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.GT)
      .EmitThen(Opcodes.JumpZ, new int[] { 9 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 30) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Jump, new int[] { 18 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.GT)
      .EmitThen(Opcodes.JumpZ, new int[] { 9 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Jump, new int[] { 6 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 40) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 7);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 20);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestEmptyElseBody()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      if(1 > 2) {
        x1 = 200
      } else {}
      
      return x1
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 100);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIfFalseComplexCondition()
  {
    string bhl = @"
    func test() 
    {
      if(false || !true) {
        trace(""NEVER"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIfElseIf()
  {
    string bhl = @"

    func test() 
    {
      if(false) {
        trace(""NEVER"")
      } else if(false) {
        trace(""NEVER2"")
      } else if(true) {
        trace(""OK"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("OK", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIfElseIfComplexCondition()
  {
    string bhl = @"

    func test() 
    {
      if(false) {
        trace(""NEVER"")
      } else if(false) {
        trace(""NEVER2"")
      } else if(true && !false) {
        trace(""OK"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("OK", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIfElse()
  {
    string bhl = @"

    func test() 
    {
      if(false) {
        trace(""NEVER"")
      } else {
        trace(""ELSE"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("ELSE", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIfElseIfElse()
  {
    string bhl = @"

    func test() 
    {
      if(false) {
        trace(""NEVER"")
      }
      else if (false) {
        trace(""NEVER2"")
      } else {
        trace(""ELSE"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("ELSE", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNonMatchingReturnAfterIf()
  {
    string bhl = @"

    func int test() 
    {
      if(false) {
        return 10
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "matching 'return' statement not found"
    );
  }

  [IsTested()]
  public void TestNonMatchingReturnAfterElseIf()
  {
    string bhl = @"

    func int test() 
    {
      if(false) {
        return 10
      } else if (true) {
        return 20
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "matching 'return' statement not found"
    );
  }

  [IsTested()]
  public void TestNonMatchingReturnAfterElseIf2()
  {
    string bhl = @"

    func int test() 
    {
      if(false) {
      } else if (true) {
        return 20
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "matching 'return' statement not found"
    );
  }

  [IsTested()]
  public void TestNonMatchingVoidReturn()
  {
    string bhl = @"
    func VoidFunc() {
    }

    func bool test() {
      return VoidFunc()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incompatible types"
    );
  }

  [IsTested()]
  public void TestMatchingReturnInElse()
  {
    string bhl = @"

    func int test() 
    {
      if(false) {
        return 10
      } else if (false) {
        return 20
      } else {
        return 30
      }
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(30, num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIfWithMultipleReturns()
  {
    string bhl = @"

    func int test(int b) 
    {
      if(b == 1) {
        return 2
      }

      return 3
    }
    ";

    var vm = MakeVM(bhl);
    {
      var num = Execute(vm, "test", Val.NewNum(vm, 1)).result.PopRelease().num;
      AssertEqual(2, num);
    }
    {
      var num = Execute(vm, "test", Val.NewNum(vm, 10)).result.PopRelease().num;
      AssertEqual(3, num);
    }
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestWhileWithCondition()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      while( x1 >= 10 )
      {
        x1 = x1 - 10
      }

      return x1
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      //__while__//
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.GTE)
      .EmitThen(Opcodes.JumpZ, new int[] { 12 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.Sub)
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Jump, new int[] { -22 })
      //__//
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 2);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 0);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestEmptyWhileBody()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      while(false) {}

      return x1
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 100);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestWhileComplexCondition()
  {
    string bhl = @"

    func int test() 
    {
      int i = 0
      while(i < 3 && true) {
        i = i + 1
      }
      return i
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(3, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBreakInWhile()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      while( x1 >= 10 )
      {
        x1 = x1 - 10
        if(x1 < 80) {
          break
        }
      }

      return x1
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      //__while__//
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.GTE)
      .EmitThen(Opcodes.JumpZ, new int[] { 25 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.Sub)
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 80) })
      .EmitThen(Opcodes.LT)
      .EmitThen(Opcodes.JumpZ, new int[] { 3 })
      .EmitThen(Opcodes.Jump/*break*/, new int[] { 3 })
      .EmitThen(Opcodes.Jump, new int[] { -35 })
      //__//
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 70);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestContinueInWhile()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      while( x1 >= 10 )
      {
        x1 = x1 - 10
        continue
        x1 = x1 + 10
      }

      return x1
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 0);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestWhileFailure()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      while(i < 3) {
        trace((string)i)
        i = i + 1
        if(i == 2) {
          fail()
        }
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    var fb = vm.Start("test");

    vm.Tick();
    AssertEqual(fb.status, BHS.FAILURE);

    AssertEqual("01", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDoWhileWithCondition()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      do
      {
        x1 = x1 - 10
      }
      while( x1 >= 10 )

      return x1
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      //__while__//
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.Sub)
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.GTE)
      .EmitThen(Opcodes.JumpZ, new int[] { 3 })
      .EmitThen(Opcodes.Jump, new int[] { -22 })
      //__//
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 2);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 0);
    CommonChecks(vm);
  }
  
  [IsTested()]
  public void TestDoWhileFeature()
  {
    string bhl = @"

    func int test() 
    {
      int i = 0
      do {
        i = i + 1
      } while(false)
      return i
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDoWhileComplexCondition()
  {
    string bhl = @"

    func int test() 
    {
      int i = 0
      do {
        i = i + 1
      } while(i < 3 && true)
      return i
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(3, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBreakInDoWhile()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      do 
      {
        x1 = x1 - 10
        if(x1 < 80) {
          break
        }
      } while( x1 >= 10 )

      return x1
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      //__while__//
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.Sub)
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 80) })
      .EmitThen(Opcodes.LT)
      .EmitThen(Opcodes.JumpZ, new int[] { 3 })
      .EmitThen(Opcodes.Jump/*break*/, new int[] { 13 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.GTE)
      .EmitThen(Opcodes.JumpZ, new int[] { 3 })
      .EmitThen(Opcodes.Jump, new int[] { -35 })
      //__//
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 70);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestContinueInDoWhile()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      do
      {
        x1 = x1 - 10
        continue
        x1 = x1 + 10
      } while( x1 >= 10 )

      return x1
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 0);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestVarInInfiniteLoop()
  {
    string bhl = @"

    func test() 
    {
      while(true) {
        int foo = 1
        trace((string)foo + "";"")
        yield()
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    var fb = vm.Start("test");
    for(int i=0;i<5;++i)
      vm.Tick();
    AssertEqual(vm.vals_pool.Allocs, 5);
    AssertEqual(vm.vals_pool.Free, 3);

    var str = log.ToString();

    AssertEqual("1;1;1;1;1;", str);

    for(int i=0;i<5;++i)
      vm.Tick();
    AssertEqual(vm.vals_pool.Allocs, 5);
    AssertEqual(vm.vals_pool.Free, 3);

    str = log.ToString();

    AssertEqual("1;1;1;1;1;" + "1;1;1;1;1;", str);

    vm.Stop(fb);

    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDoWhileFailure()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      do {
        trace((string)i)
        i = i + 1
        if(i == 2) {
          fail()
        }
      } while(i < 3)
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    var fb = vm.Start("test");

    vm.Tick();
    AssertEqual(fb.status, BHS.FAILURE);

    AssertEqual("01", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStackInInfiniteLoop()
  {
    string bhl = @"

    func int foo()
    {
      return 100
    }

    func hey(int a)
    {
    }

    func test() 
    {
      while(true) {
        hey(foo())
        yield()
      }
    }
    ";

    var ts = new Types();

    var vm = MakeVM(bhl, ts);
    var fb = vm.Start("test");
    
    for(int i=0;i<5;++i)
      vm.Tick();
    AssertEqual(fb.result.Count, 0);
    AssertEqual(vm.vals_pool.Allocs, 4);
    AssertEqual(vm.vals_pool.Free, 3);

    vm.Stop(fb);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForLoop()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 10

      for( int i = 0; i < 3; i = i + 1 )
      {
        x1 = x1 - i
      }

      return x1
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 2 + 1/*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      //__for__//
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .EmitThen(Opcodes.SetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .EmitThen(Opcodes.LT)
      .EmitThen(Opcodes.JumpZ, new int[] { 19 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.Sub)
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.SetVar, new int[] { 1 })
      .EmitThen(Opcodes.Jump, new int[] { -29 })
      //__//
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 4);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 7);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSeveralForLoops()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 10

      for( int i = 0; i < 3; i = i + 1 )
      {
        x1 = x1 - i
      }

      for( int j = 1; j < 3; j = j + 1 )
      {
        x1 = x1 - j
      }


      return x1
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 4);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForLoopLocalScope()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 10

      for( int i = 0; i < 3; i = i + 1 )
      {
        x1 = x1 - i
      }

      int i = 2

      return x1 - i
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 5);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSeveralForLoopsLocalScope()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 10

      for( int i = 0; i < 3; i = i + 1 )
      {
        x1 = x1 - i
      }

      for( int i = 1; i < 3; i = i + 1 )
      {
        x1 = x1 - i
      }


      return x1
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 4);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForMultiExpression()
  {
    string bhl = @"

    func test() 
    {
      for(int i = 0, int j = 1; i < 3; i = i + 1, j = j + 2) {
        trace((string)(i*j) + "";"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("0;3;10;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForReverse()
  {
    string bhl = @"

    func test() 
    {
      for(int i = 2; i >= 0; i = i - 1) {
        trace((string)i)
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("210", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForUseExternalVar()
  {
    string bhl = @"

    func test() 
    {
      int i
      for(i = 1; i < 3; i = i + 1) {
        trace((string)i)
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("12", log.ToString());
    CommonChecks(vm);
  }
  
  [IsTested()]
  public void TestForNested()
  {
    string bhl = @"

    func test() 
    {
      for(int i = 0; i < 3; i = i + 1) {
        for(int j = 0; j < 2; j = j + 1) {
          trace((string)i + "","" + (string)j + "";"")
        }
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("0,0;0,1;1,0;1,1;2,0;2,1;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForInParal()
  {
    string bhl = @"

    func test() 
    {
      paral {
        for(int i = 0; i < 3; i = i + 1) {
          trace((string)i)
          yield()
        }
        suspend()
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual("012", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForBadPreSection()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      for(i ; i < 3; i = i + 1) {
        trace((string)i)
      }
    }
    ";

    var ts = new Types();

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "no viable alternative at input 'i ;"
    );
  }

  [IsTested()]
  public void TestForEmptyPreSection()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      for(; i < 3; i = i + 1) {
        trace((string)i)
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("012", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForEmptyPostSection()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      for(; i < 3;) {
        trace((string)i)
        i = i + 1
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("012", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForBadPostSection()
  {
    string bhl = @"

    func test() 
    {
      for(int i = 0 ; i < 3; i) {
        trace((string)i)
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "no viable alternative at input 'i)'"
    );
  }

  [IsTested()]
  public void TestForCondIsRequired()
  {
    string bhl = @"

    func test() 
    {
      for(;;) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "no viable alternative at input ';'"
    );
  }

  [IsTested()]
  public void TestForNonBoolCond()
  {
    string bhl = @"

    func int foo() 
    {
      return 14
    }

    func test() 
    {
      for(; foo() ;) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incompatible types"
    );
  }

  [IsTested()]
  public void TestForeachLoop()
  {
    string bhl = @"
    func int test()
    {
      []int arr = new []int
      arr.Add(1)
      arr.Add(3)
      int accum = 0

      foreach(int a in arr) {
        accum = accum + a
      }

      return accum
    }
    ";

    var ts = new Types();
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 3 + 2/*hidden vars*/ + 1/*cargs*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, ts.TArr("int")) }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.CallMethodNative, new int[] { ArrAddIdx, 1 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .EmitThen(Opcodes.CallMethodNative, new int[] { ArrAddIdx, 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .EmitThen(Opcodes.SetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetVar, new int[] { 3 }) //assign tmp arr
      .EmitThen(Opcodes.DeclVar, new int[] { 4, ConstIdx(c, ts.T("int")) })//declare counter
      .EmitThen(Opcodes.DeclVar, new int[] { 2, ConstIdx(c, ts.T("int"))  })//declare iterator
      .EmitThen(Opcodes.GetVar, new int[] { 4 })
      .EmitThen(Opcodes.GetVar, new int[] { 3 })
      .EmitThen(Opcodes.GetAttr, new int[] { ArrCountIdx })
      .EmitThen(Opcodes.LT) //compare counter and tmp arr size
      .EmitThen(Opcodes.JumpZ, new int[] { 19 })
      //call arr idx method
      .EmitThen(Opcodes.GetVar, new int[] { 3 })
      .EmitThen(Opcodes.GetVar, new int[] { 4 })
      .EmitThen(Opcodes.ArrIdx)
      .EmitThen(Opcodes.SetVar, new int[] { 2 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 }) //accum = accum + iterator var
      .EmitThen(Opcodes.GetVar, new int[] { 2 }) 
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.SetVar, new int[] { 1 })
      .EmitThen(Opcodes.Inc, new int[] { 4 }) //fast increment hidden counter
      .EmitThen(Opcodes.Jump, new int[] { -30 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
    ;

    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 4);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForeachTraceItems()
  {
    string bhl = @"

    func test() 
    {
      []int its = [1, 2, 3]
      foreach(int it in its) {
        trace((string)it)
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("123", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForeachLocalScope()
  {
    string bhl = @"

    func test() 
    {
      foreach(int it in [1, 2, 3]) {
        trace((string)it)
      }

      foreach(int it in [3, 2, 1]) {
        trace((string)it)
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("123321", log.ToString());
    CommonChecks(vm);
  }


  [IsTested()]
  public void TestForeachForNativeArrayBinding()
  {
    string bhl = @"

    func test() 
    {
      []Color cs = [{r:1}, {r:2}, {r:3}]
      foreach(Color c in cs) {
        trace((string)c.r)
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("123", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForeachUseExternalIteratorVar()
  {
    string bhl = @"

    func test() 
    {
      int it
      []int its = [1, 2, 3]
      foreach(it in its) {
        trace((string)it)
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("123", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForeachWithInPlaceArr()
  {
    string bhl = @"

    func test() 
    {
      foreach(int it in [1,2,3]) {
        trace((string)it)
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("123", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForeachWithReturnedArr()
  {
    string bhl = @"

    func []int foo()
    {
      return [1,2,3]
    }

    func test() 
    {
      foreach(int it in foo()) {
        trace((string)it)
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("123", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForeachInParal()
  {
    string bhl = @"

    func test() 
    {
      paral {
        foreach(int it in [1,2,3]) {
          trace((string)it)
          yield()
        }
        suspend()
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual("123", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSeveralForeachInParalAll()
  {
    string bhl = @"

    func test() 
    {
      paral_all {
        foreach(int it in [1,2,3]) {
          trace((string)it)
          yield()
        }
        foreach(int it2 in [4,5,6]) {
          trace((string)it2)
          yield()
        }
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("142536", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForeachBreak()
  {
    string bhl = @"

    func test() 
    {
      foreach(int it in [1,2,3]) {
        if(it == 3) {
          break
        }
        trace((string)it)
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("12", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForeachSeveral()
  {
    string bhl = @"

    func test() 
    {
      []int its = [1,2,3]
      foreach(int it in its) {
        trace((string)it)
      }

      foreach(int it2 in its) {
        trace((string)it2)
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("123123", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForeachNested()
  {
    string bhl = @"

    func test() 
    {
      []int its = [1,2,3]
      foreach(int it in its) {
        foreach(int it2 in its) {
          trace((string)it + "","" + (string)it2 + "";"")
        }
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("1,1;1,2;1,3;2,1;2,2;2,3;3,1;3,2;3,3;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForeachNested2()
  {
    string bhl = @"

    func test() 
    {
      foreach(int it in [1,2,3]) {
        foreach(int it2 in [20,30]) {
          trace((string)it + "","" + (string)it2 + "";"")
        }
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("1,20;1,30;2,20;2,30;3,20;3,30;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForeachIteratorVarBadType()
  {
    string bhl = @"

    func test() 
    {
      foreach(string it in [1,2,3]) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incompatible types"
    );
  }

  [IsTested()]
  public void TestForeachArrBadType()
  {
    string bhl = @"

    func float foo()
    {
      return 14
    }

    func test() 
    {
      foreach(float it in foo()) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incompatible types"
    );
  }

  [IsTested()]
  public void TestForeachExternalIteratorVarBadType()
  {
    string bhl = @"

    func test() 
    {
      string it
      foreach(it in [1,2,3]) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incompatible types"
    );
  }

  [IsTested()]
  public void TestForeachRedeclareError()
  {
    string bhl = @"

    func test() 
    {
      string it
      foreach(int it in [1,2,3]) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "already defined symbol 'it'"
    );
  }

  [IsTested()]
  public void TestBreakInForLoop()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 10

      for( int i = 1; i < 3; i = i + 1 )
      {
        x1 = x1 - i
        break
      }

      return x1
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 2 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      //__for__//
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.SetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .EmitThen(Opcodes.LT)
      .EmitThen(Opcodes.JumpZ, new int[] { 22 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.Sub)
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Jump/*break*/, new int[] { 12 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.SetVar, new int[] { 1 })
      .EmitThen(Opcodes.Jump, new int[] { -32 })
      //__//
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 3);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 9);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBadBreak()
  {
    string bhl = @"

    func test() 
    {
      break
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "not within loop construct"
    );
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
      "not within loop construct"
    );
  }

  [IsTested()]
  public void TestContinueInForLoop()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 10

      for( int i = 1; i < 3; i = i + 1 )
      {
        continue
        x1 = x1 - i
      }

      return x1
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 2 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      //__for__//
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.SetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .EmitThen(Opcodes.LT)
      .EmitThen(Opcodes.JumpZ, new int[] { 22 })
      .EmitThen(Opcodes.Jump/*continue*/, new int[] { 7 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.Sub)
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.SetVar, new int[] { 1 })
      .EmitThen(Opcodes.Jump, new int[] { -32 })
      //__//
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 10);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBreakInForeachLoop()
  {
    string bhl = @"
    func int test()
    {
      []int arr = new []int
      arr.Add(1)
      arr.Add(3)
      int accum = 0

      foreach(int a in arr) {
        if(a == 3) {
          break
        }
        accum = accum + a
      }

      return accum
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForeverLoopBreak()
  {
    string bhl = @"
    func int test() 
    {
      int i = 0
      while(true) {
        i = i + 1
        if(i == 3) {
          break
        }
        yield()
      }
      return i
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 3);
    CommonChecks(vm);
  }
  
  [IsTested()]
  public void TestParalBreak()
  {
    string bhl = @"

    func int test() 
    {
      int n = 0
      while(true) {
        paral {
          {
            n = 1
            break
            suspend()
          }
          {
            n = 2
            suspend()
          }
        }
      }
      return n
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalAllBreak()
  {
    string bhl = @"

    func int test() 
    {
      int n = 0
      while(true) {
        paral_all {
          {
            n = 1
            break
            suspend()
          }
          {
            n = 2
            suspend()
          }
        }
      }
      return n
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalReuse()
  {
    string bhl = @"

    func int test() 
    {
      int n = 0
      paral {
        {
          suspend()
          n = 10
        }
        {
          n = 1
        }
      }

      paral {
        {
          n = 2
        }
        {
          suspend()
          n = 3
        }
      }
      return n
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBreakFromNestedSequences()
  {
    string bhl = @"
    func int test() 
    {
      int i = 0
      while(true) {
        {
          //without defer{..} sequence block won't be created
          //for optimization purposes
          defer {
            i = 1
          }
          {
            defer {
              i = 2
            }
            i = 3
            break
          }
        }
      }
      return i
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestContinueInForeachLoop()
  {
    string bhl = @"
    func int test()
    {
      []int arr = new []int
      arr.Add(1)
      arr.Add(3)
      int accum = 0

      foreach(int a in arr) {
        if(a == 1) {
          continue
        }
        accum = accum + a
      }

      return accum
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 3);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestContinueFromNestedSequences()
  {
    string bhl = @"
    func int test() 
    {
      int i = 0
      while(i == 0) {
        {
          //without defer{..} sequence block won't be created
          //for optimization purposes
          defer {
            i = 1
          }
          {
            defer {
              i = 2
            }
            i = 3
            continue
          }
        }
      }
      return i
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBooleanInCondition()
  {
    string bhl = @"
    func int first()
    {
      return 1
    }
    func int second()
    {
      return 2
    }
    func int test()
    {
      if( true ) {
        return first()
      } else {
        return second()
      }
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
      .EmitThen(Opcodes.JumpZ, new int[] { 13 })
      .EmitThen(Opcodes.Call, new int[] { 0, 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Jump, new int[] { 10 })
      .EmitThen(Opcodes.Call, new int[] { 9, 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncCallInCondition()
  {
    string bhl = @"
    func int first()
    {
      return 1
    }
    func int second()
    {
      return 2
    }
    func int test()
    {
      if( 1 > 0 )
      {
        return first()
      }
      else
      {
        return second()
      }
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .EmitThen(Opcodes.GT)
      .EmitThen(Opcodes.JumpZ, new int[] { 13 })
      .EmitThen(Opcodes.Call, new int[] { 0, 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Jump, new int[] { 10 })
      .EmitThen(Opcodes.Call, new int[] { 9, 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestMultiFuncCall()
  {
    string bhl = @"
    func int test2(int x) 
    {
      return 98 + x
    }
    func int test1(int x1) 
    {
      return x1
    }
    func int test()
    {
      return test2(1+1) - test1(5-30)
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 125);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStringArgFunctionCall()
  {
    string bhl = @"

    func string StringTest(string s) 
    {
      return ""Hello"" + s
    }
    func string Test()
    {
      string s = "" world !""
      return StringTest(s)
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("Test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "Hello world !");
    CommonChecks(vm);
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
      "type doesn't support member access via '.'"
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
      "accessing not an array/map type 'func bool(int)'"
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
      func int(int c, int b = 1) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "default argument values not allowed for lambdas"
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
      .EmitThen(Opcodes.UseUpval, new int[] { 0, 0 })
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
      .EmitThen(Opcodes.UseUpval, new int[] { 0, 1 })
      .EmitThen(Opcodes.UseUpval, new int[] { 1, 2 })
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
      .EmitThen(Opcodes.Lambda, new int[] { 40 })
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
      .EmitThen(Opcodes.UseUpval, new int[] { 0, 1 })
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
  public void TestDefaultFuncLambdaArg()
  {
    string bhl = @"
    func int effect_body(
        int dummy, 
        func(int) on_create_fn = func(int _) {} 
    ) {
      return dummy
    }

    func int effect(int dummy) {
      return effect_body(dummy)
    }

    func int test()
    {
      return effect(10)
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
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

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("c", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncPtrSeveralLambdaRunning()
  {
    string bhl = @"

    func test() 
    {
      func(string) ptr = func(string arg) {
        trace(arg)
        yield()
      }
      paral {
        ptr(""FOO"")
        ptr(""BAR"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual("FOOBAR", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
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

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    AssertTrue(Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().bval);
    AssertEqual("HEY", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
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

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    AssertFalse(Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().bval);
    AssertEqual("HEYBAR", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(14, Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(6, Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().num);
    AssertEqual(8, Execute(vm, "test", Val.NewNum(vm, 4)).result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(9+12, Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().num);
    AssertEqual(12+15, Execute(vm, "test", Val.NewNum(vm, 4)).result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(3+8, Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().num);
    AssertEqual(4+10, Execute(vm, "test", Val.NewNum(vm, 4)).result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    AssertEqual(3*2 + 3*10, Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().num);
    AssertEqual("whathey", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertTrue(Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertTrue(Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
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

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    AssertTrue(Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().bval);
    AssertEqual("HEY", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
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

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    AssertTrue(Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().bval);
    AssertEqual("HEY", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
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

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test", Val.NewNum(vm, 3));
    AssertEqual("01234", log.ToString());
    AssertEqual(1/*0 frame*/+2, vm.frames_pool.Allocs);
    CommonChecks(vm);
  }

  [IsTested()]
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
      delegate() {
        Compile(bhl);
      },
      "incompatible types"
    );
  }

  [IsTested()]
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
      delegate() {
        Compile(bhl);
      },
      "incompatible types"
    );
  }

  [IsTested()]
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
      delegate() {
        Compile(bhl);
      },
      "incompatible types"
    );
  }

  [IsTested()]
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
      delegate() {
        Compile(bhl);
      },
      "'ref' is missing"
    );
  }

  [IsTested()]
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
      delegate() {
        Compile(bhl);
      },
      "incompatible types"
    );
  }

  [IsTested()]
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

    var ts = new Types();
    var log = new StringBuilder();

    {
      var cl = new ClassSymbolNative("refbool", null,
        delegate(VM.Frame frm, ref Val v, IType type) 
        {}
      );
      ts.ns.Define(cl);
    }

    BindTrace(ts, log);

    AssertError<Exception>(
      delegate() {
        Compile(bhl, ts);
      },
      "incompatible types"
    );
  }

  [IsTested()]
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
      delegate() {
        Compile(bhl);
      },
      "missing argument of type 'int'"
    );
  }

  [IsTested()]
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
      delegate() {
        Compile(bhl);
      },
      "missing argument of type 'float'"
    );
  }

  [IsTested()]
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
      delegate() {
        Compile(bhl);
      },
      "too many arguments"
    );
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

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test", Val.NewNum(vm, 3));
    AssertEqual("HERE", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
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

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test", Val.NewNum(vm, 3));
    AssertEqual("HERE", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncPtrForNativeFunc()
  {
    string bhl = @"
    func test() 
    {
      func() ptr = foo
      ptr()
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();

    {
      var fn = new FuncSymbolNative("foo", Types.Void,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS _)
          {
            log.Append("FOO");
            return null;
          }
          );
      ts.ns.Define(fn);
    }

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("FOO", log.ToString());
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

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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
      "already defined symbol 'a'"
    );
  }

  [IsTested()]
  public void TestStartNativeFunc()
  {
    string bhl = @"
    func test() 
    {
      start(native)
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    var fn = new FuncSymbolNative("native", Types.Void,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          log.Append("HERE");
          return null;
        } 
    );
    ts.ns.Define(fn);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("HERE", log.ToString());
    CommonChecks(vm);
  }

  class TraceAfterYield : ICoroutine
  {
    bool first_time = true;
    public StringBuilder log;

    public void Tick(VM.Frame frm, VM.ExecState exec, ref BHS status)
    {
      if(first_time)
      {
        status = BHS.RUNNING;
        first_time = false;
      }
      else
        log.Append("HERE");
    }

    public void Cleanup(VM.Frame frm, VM.ExecState exec)
    {
      first_time = true;
    }
  }

  [IsTested()]
  public void TestStartNativeStatefulFunc()
  {
    string bhl = @"
    func test() 
    {
      start(yield_and_trace)
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    {
      var fn = new FuncSymbolNative("yield_and_trace", Types.Void,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          var inst = CoroutinePool.New<TraceAfterYield>(frm.vm);
          inst.log = log;
          return inst;
        } 
      );
      ts.ns.Define(fn);
    }

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("HERE", log.ToString());
    CommonChecks(vm);
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

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    Execute(vm, "test");
    AssertEqual("HEREHERE", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStartSameFuncPtr()
  {
    string bhl = @"
    func foo() {
      trace(""FOO1"")
      yield()
      trace(""FOO2"")
    }

    func test() {
      func() fn = foo
      func() fn2 = fn
      paral_all {
        start(fn2)
        start(fn2)
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("FOO1FOO1FOO2FOO2", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStartImportedSameFuncPtr()
  {
    string bhl2 = @"
    func foo() {
      trace(""FOO1"")
      yield()
      trace(""FOO2"")
    }
    ";

    string bhl1 = @"
    import ""bhl2""

    func test() {
      func() fn = foo
      func() fn2 = fn
      paral_all {
        start(fn2)
        start(fn2)
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      },
      ts
    );

    vm.LoadModule("bhl1");
    Execute(vm, "test");
    AssertEqual("FOO1FOO1FOO2FOO2", log.ToString());
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

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("1020HEY!12HEY!", log.ToString());
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
  public void TestFuncDefaultArg()
  {
    string bhl = @"

    func int foo(int k0, int k1 = 1 - 0, int k2 = 10) 
    {
      return k0+k1+k2
    }
      
    func int test()
    {
      return foo(1 + 1, 0)
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      //foo
      .EmitThen(Opcodes.InitFrame, new int[] { 3+1/*args info*/ })
      .EmitThen(Opcodes.ArgVar, new int[] { 0 })
      .EmitThen(Opcodes.DefArg, new int[] { 0, 9 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .EmitThen(Opcodes.Sub)
      .EmitThen(Opcodes.ArgVar, new int[] { 1 })
      .EmitThen(Opcodes.DefArg, new int[] { 1, 4 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.ArgVar, new int[] { 2 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 2 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      //test
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .EmitThen(Opcodes.Call, new int[] { 0, 130 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 12);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncManyDefaultArgs()
  {
    string bhl = @"

    func float foo(
          float k1 = 1, float k2 = 1, float k3 = 1, float k4 = 1, float k5 = 1, float k6 = 1, float k7 = 1, float k8 = 1, float k9 = 1,
          float k10 = 1, float k11 = 1, float k12 = 1, float k13 = 1, float k14 = 1, float k15 = 1, float k16 = 1, float k17 = 1, float k18 = 1,
          float k19 = 1, float k20 = 1, float k21 = 1, float k22 = 1, float k23 = 1, float k24 = 1, float k25 = 1, float k26 = 42
        )
    {
      return k26
    }
      
    func float test() 
    {
      return foo()
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 42);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncTooManyDefaultArgs()
  {
    string bhl = @"

    func float foo(
          float k1 = 1, float k2 = 1, float k3 = 1, float k4 = 1, float k5 = 1, float k6 = 1, float k7 = 1, float k8 = 1, float k9 = 1,
          float k10 = 1, float k11 = 1, float k12 = 1, float k13 = 1, float k14 = 1, float k15 = 1, float k16 = 1, float k17 = 1, float k18 = 1,
          float k19 = 1, float k20 = 1, float k21 = 1, float k22 = 1, float k23 = 1, float k24 = 1, float k25 = 1, float k26 = 1, float k27 = 1
        )
    {
      return k27
    }
      
    func float test() 
    {
      return foo()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "max default arguments reached"
    );
  }

  [IsTested()]
  public void TestFuncNotEnoughArgs()
  {
    string bhl = @"

    func bool foo(bool k)
    {
      return k
    }
      
    func bool test() 
    {
      return foo()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "missing argument 'k'"
    );
  }

  [IsTested()]
  public void TestFuncPassingExtraNamedArgs()
  {
    string bhl = @"

    func int foo(int k)
    {
      return k
    }
      
    func int test() 
    {
      return foo(k: 1, k: 2)
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "argument already passed before"
    );
  }

  [IsTested()]
  public void TestFuncNotEnoughArgsWithDefaultArgs()
  {
    string bhl = @"

    func bool foo(float radius, bool k = true)
    {
      return k
    }
      
    func bool test() 
    {
      return foo()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "missing argument 'radius'"
    );
  }

  [IsTested()]
  public void TestFuncDefaultArgIgnored()
  {
    string bhl = @"

    func float foo(float k = 42)
    {
      return k
    }
      
    func float test() 
    {
      return foo(24)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 24);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncDefaultArgMixed()
  {
    string bhl = @"

    func float foo(float b, float k = 42)
    {
      return b + k
    }
      
    func float test() 
    {
      return foo(24)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 66);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncDefaultArgIsFunc()
  {
    string bhl = @"

    func float bar(float m)
    {
      return m
    }

    func float foo(float b, float k = bar(1))
    {
      return b + k
    }
      
    func float test() 
    {
      return foo(24)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 25);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncDefaultArgIsFunc2()
  {
    string bhl = @"

    func float bar(float m)
    {
      return m
    }

    func float foo(float b = 1, float k = bar(1))
    {
      return b + k
    }
      
    func float test() 
    {
      return foo(26, bar(2))
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 28);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncMissingReturnArgument()
  {
    string bhl = @"
    func int test() 
    {
      return
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "return value is missing"
    );
  }

  [IsTested()]
  public void TestFuncMissingDefaultArgument()
  {
    string bhl = @"

    func float foo(float b = 23, float k)
    {
      return b + k
    }
      
    func float test() 
    {
      return foo(24)
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "missing default argument expression"
    );
  }
  
  [IsTested()]
  public void TestFuncExtraArgumentMatchesLocalVariable()
  {
    string bhl = @"

    func foo(float b, float k)
    {
      float f = 3
      float a = b + k
    }
      
    func test() 
    {
      foo(b: 24, k: 3, f : 1)
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "no such named argument"
    );
  }

  [IsTested()]
  public void TestFuncSeveralDefaultArgsMixed()
  {
    string bhl = @"

    func float foo(float b = 100, float k = 1000)
    {
      return k - b
    }
      
    func float test() 
    {
      return foo(k : 2, b : 5)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, -3);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncArgPassedByValue()
  {
    string bhl = @"
      
    func foo(float a)
    {
      a = a + 1
    }

    func float test() 
    {
      float k = 1
      foo(k)
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassByRef()
  {
    string bhl = @"

    func foo(ref float a) 
    {
      a = a + 1
    }
      
    func float test(float k) 
    {
      foo(ref k)
      return k
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.ArgRef, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.ExitFrame)
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.ArgVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Call, new int[] { 0, 1 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var num = Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().num;
    AssertEqual(num, 4);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassByRefNonAssignedValue()
  {
    string bhl = @"

    func foo(ref float a) 
    {
      a = a + 1
    }
      
    func float test() 
    {
      float k
      foo(ref k)
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassByRefAlreadyDefinedError()
  {
    string bhl = @"

    func foo(ref float a, float a) 
    {
      a = a + 1
    }
      
    func float test(float k) 
    {
      foo(ref k, k)
      return k
    }
    ";

    AssertError<Exception>(
      delegate() {
        Compile(bhl);
      },
      "already defined symbol 'a'"
    );
  }

  [IsTested()]
  public void TestPassByRefAssignToNonRef()
  {
    string bhl = @"

    func foo(ref float a) 
    {
      float b = a
      b = b + 1
    }
      
    func float test(float k) 
    {
      foo(ref k)
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().num;
    AssertEqual(num, 3);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassByRefNested()
  {
    string bhl = @"

    func bar(ref float b)
    {
      b = b * 2
    }

    func foo(ref float a) 
    {
      a = a + 1
      bar(ref a)
    }
      
    func float test(float k) 
    {
      foo(ref k)
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().num;
    AssertEqual(num, 8);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassByRefMixed()
  {
    string bhl = @"

    func foo(ref float a, float b) 
    {
      a = a + b
    }
      
    func float test(float k) 
    {
      foo(ref k, k)
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().num;
    AssertEqual(num, 6);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassByRefInUserBinding()
  {
    string bhl = @"

    func float test(float k) 
    {
      func_with_ref(k, ref k)
      return k
    }
    ";

    var ts = new Types();

    {
      var fn = new FuncSymbolNative("func_with_ref", Types.Void,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var b = frm.stack.Pop();
            var a = frm.stack.PopRelease().num;

            b.num = a * 2;
            b.Release();
            return null;
          },
          new FuncArgSymbol("a", ts.T("float")),
          new FuncArgSymbol("b", ts.T("float"), true/*is ref*/)
        );

      ts.ns.Define(fn);
    }

    var vm = MakeVM(bhl, ts);
    var num = Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().num;
    AssertEqual(num, 6);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassByRefNamedArg()
  {
    string bhl = @"

    func foo(ref float a, float b) 
    {
      a = a + b
    }
      
    func float test(float k) 
    {
      foo(a : ref k, b: k)
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().num;
    AssertEqual(num, 6);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassByRefAndThenReturn()
  {
    string bhl = @"

    func float foo(ref float a) 
    {
      a = a + 1
      return a
    }
      
    func float test(float k) 
    {
      return foo(ref k)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().num;
    AssertEqual(num, 4);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestRefsAllowedInFuncArgsOnly()
  {
    string bhl = @"

    func test() 
    {
      ref float a
    }
    ";

    AssertError<Exception>(
      delegate() {
        Compile(bhl);
      },
      "mismatched input 'ref'"
    );
  }

  [IsTested()]
  public void TestPassByRefDefaultArgsNotAllowed()
  {
    string bhl = @"

    func foo(ref float k = 10)
    {
    }
    ";

    AssertError<Exception>(
      delegate() {
        Compile(bhl);
      },
      "'ref' is not allowed to have a default value"
    );
  }

  [IsTested()]
  public void TestPassByRefNullValue()
  {
    string bhl = @"

    func float foo(ref float k = null)
    {
      if((any)k != null) {
        k = k + 1
        return k
      } else {
        return 1
      }
    }

    func float,float test() 
    {
      float res = 0
      float k = 10
      res = res + foo()
      res = res + foo(ref k)
      return res,k
    }
    ";

    var vm = MakeVM(bhl);
    var fb = Execute(vm, "test", Val.NewNum(vm, 3));
    var num1 = fb.result.PopRelease().num;
    var num2 = fb.result.PopRelease().num;
    AssertEqual(num1, 12);
    AssertEqual(num2, 11);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassByRefLiteralNotAllowed()
  {
    string bhl = @"

    void foo(ref int a) 
    {
    }

    func test() 
    {
      foo(ref 10)  
    }
    ";

    AssertError<Exception>(
      delegate() {
        Compile(bhl);
      },
      "mismatched input '('"
    );
  }

  [IsTested()]
  public void TestPassByRefClassField()
  {
    string bhl = @"

    class Bar
    {
      float a
    }

    func foo(ref float a) 
    {
      a = a + 1
    }
      
    func float test() 
    {
      Bar b = { a: 10}

      foo(ref b.a)
      return b.a
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 11);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassByRefArrayItem()
  {
    string bhl = @"

    func foo(ref float a) 
    {
      a = a + 1
    }
      
    func float test() 
    {
      []float fs = [1,10,20]

      foo(ref fs[1])
      return fs[0] + fs[1] + fs[2]
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 32);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassByRefArrayObj()
  {
    string bhl = @"

    class Bar
    {
      float f
    }

    func foo(ref float a) 
    {
      a = a + 1
    }
      
    func float test() 
    {
      []Bar bs = [{f:1},{f:10},{f:20}]

      foo(ref bs[1].f)
      return bs[0].f + bs[1].f + bs[2].f
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 32);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassByRefTmpClassField()
  {
    string bhl = @"

    class Bar
    {
      float a
    }

    func float foo(ref float a) 
    {
      a = a + 1
      return a
    }
      
    func float test() 
    {
      return foo(ref (new Bar).a)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassByRefClassFieldNested()
  {
    string bhl = @"

    class Wow
    {
      float c
    }

    class Bar
    {
      Wow w
    }

    func foo(ref float a) 
    {
      a = a + 1
    }
      
    func float test() 
    {
      Bar b = { w: { c : 4} }

      foo(ref b.w.c)
      return b.w.c
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.ArgRef, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.ExitFrame)
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, c.ns.T("Bar")) }) 
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, c.ns.T("Wow")) }) 
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 4) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 0 })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 0 })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.RefAttr, new int[] { 0 })
      .EmitThen(Opcodes.RefAttr, new int[] { 0 })
      .EmitThen(Opcodes.Call, new int[] { 0, 1 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 5);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncPtrByRef()
  {
    string bhl = @"
    func int _1()
    {
      return 1
    }

    func int _2()
    {
      return 2
    }

    func change(ref func int() p)
    {
      p = _2
    }

    func int test() 
    {
      func int() ptr = _1
      change(ref ptr)
      return ptr()
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(2, num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassByRefClassFieldFuncPtr()
  {
    string bhl = @"

    class Bar
    {
      func int() p
    }

    func int _5()
    {
      return 5
    }

    func int _10()
    {
      return 10
    }

    func foo(ref func int() p) 
    {
      p = _10
    }
      
    func int test() 
    {
      Bar b = {p: _5}

      foo(ref b.p)
      return b.p()
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 10);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassByRefNativeClassFieldNotSupported()
  {
    string bhl = @"

    func foo(ref float a) 
    {
      a = a + 1
    }
      
    func float test() 
    {
      Color c = new Color

      foo(ref c.r)
      return c.r
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    AssertError<Exception>(
      delegate() {
        Compile(bhl, ts);
      },
      "getting field by 'ref' not supported"
    );
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
  public void TestFuncReplacesArrayValueByRef()
  {
    string bhl = @"

    func []float make()
    {
      []float fs = [42]
      return fs
    }

    func foo(ref []float a) 
    {
      a = make()
    }
      
    func float test() 
    {
      []float a
      foo(ref a)
      return a[0]
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 42);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncReplacesArrayValueByRef2()
  {
    string bhl = @"

    func foo(ref []float a) 
    {
      a = [42]
    }
      
    func float test() 
    {
      []float a = []
      foo(ref a)
      return a[0]
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 42);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncReplacesArrayValueByRef3()
  {
    string bhl = @"

    func foo(ref []float a) 
    {
      a = [42]
      []float b = a
    }
      
    func float test() 
    {
      []float a = []
      foo(ref a)
      return a[0]
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 42);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassArrayToFunctionByValue()
  {
    string bhl = @"

    func foo([]int a)
    {
      a = [100]
    }
      
    func int test() 
    {
      []int a = [1, 2]

      foo(a)

      return a[0]
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 1);
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
  public void TestFuncReplacesFuncPtrByRef()
  {
    string bhl = @"

    func float bar() 
    { 
      return 42
    } 

    func foo(ref func float() a) 
    {
      a = bar
    }
      
    func float test() 
    {
      func float() a
      foo(ref a)
      return a()
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 42);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncReplacesFuncPtrByRef2()
  {
    string bhl = @"

    func float bar() 
    { 
      return 42
    } 

    func foo(ref func float() a) 
    {
      a = bar
    }
      
    func float test() 
    {
      func float() a = func float() { return 45 } 
      foo(ref a)
      return a()
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
  public void TestFuncSeveralDefaultArgsOmittingSome()
  {
    string bhl = @"

    func float foo(float b = 100, float k = 1000)
    {
      return k - b
    }
      
    func float test() 
    {
      return foo(k : 2)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, -98);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFiberFuncDefaultArg()
  {
    string bhl = @"

    func float test(float k = 42) 
    {
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var args_inf = new FuncArgsInfo();
    args_inf.UseDefaultArg(0, true);
    var num = Execute(vm, "test", args_inf).result.PopRelease().num;
    AssertEqual(num, 42);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassNamedValue()
  {
    string bhl = @"
      
    func float foo(float a)
    {
      return a
    }

    func float test(float k) 
    {
      return foo(a : k)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().num;
    AssertEqual(num, 3);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassSeveralNamedValues()
  {
    string bhl = @"
      
    func float foo(float a, float b)
    {
      return b - a
    }

    func float test() 
    {
      return foo(b : 5, a : 3)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(num, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncDefaultNamedArg()
  {
    string bhl = @"

    func int foo(int k0, int k1 = 1 - 0, int k2 = 10) 
    {
      return k0+k1+k2
    }
      
    func int test()
    {
      return foo(1 + 1, k2: 2)
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 5);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestEmptyIntArray()
  {
    string bhl = @"
    func []int test()
    {
      []int a = new []int
      return a
    }
    ";

    var ts = new Types();
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, ts.TArr("int")) }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    var lst = fb.result.Pop();
    AssertEqual((lst.obj as IList<Val>).Count, 0);
    lst.Release();
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestAddToStringArray()
  {
    string bhl = @"
    func string test()
    {
      []string a = new []string
      a.Add(""test"")
      return a[0]
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "test");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStringArrayIndex()
  {
    string bhl = @"
      
    func string test() 
    {
      []string arr = new []string
      arr.Add(""bar"")
      arr.Add(""foo"")
      return arr[1]
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().str;
    AssertEqual(res, "foo");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTmpArrayAtIdx()
  {
    string bhl = @"
    func []int mkarray()
    {
      []int a = new []int
      a.Add(1)
      a.Add(2)
      return a
    }

    func int test()
    {
      return mkarray()[0]
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestArrayRemoveAt()
  {
    string bhl = @"
    func []int mkarray()
    {
      []int arr = new []int
      arr.Add(1)
      arr.Add(100)
      arr.RemoveAt(0)
      return arr
    }
      
    func []int test() 
    {
      return mkarray()
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    var lst = fb.result.Pop();
    AssertEqual((lst.obj as IList<Val>).Count, 1);
    lst.Release();
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTmpArrayCount() 
  {
    string bhl = @"
    func []int mkarray()
    {
      []int arr = new []int
      arr.Add(1)
      arr.Add(100)
      return arr
    }
      
    func int test() 
    {
      return mkarray().Count
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTmpArrayRemoveAt()
  {
    string bhl = @"

    func []int mkarray()
    {
      []int arr = new []int
      arr.Add(1)
      arr.Add(100)
      return arr
    }
      
    func test() 
    {
      mkarray().RemoveAt(0)
    }
    ";

    var vm = MakeVM(bhl);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTmpArrayAdd()
  {
    string bhl = @"

    func []int mkarray()
    {
      []int arr = new []int
      arr.Add(1)
      arr.Add(100)
      return arr
    }
      
    func test() 
    {
      mkarray().Add(300)
    }
    ";

    var vm = MakeVM(bhl);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestArrayIndexOf()
  {
    {
      string bhl = @"
      func int test() 
      {
        []int arr = [1, 2, 10]
        return arr.IndexOf(2)
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func int test() 
      {
        []int arr = [1, 2, 10]
        return arr.IndexOf(1)
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func int test() 
      {
        []int arr = [1, 2, 10]
        return arr.IndexOf(10)
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func int test() 
      {
        []int arr = [1, 2, 10]
        return arr.IndexOf(100)
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(-1, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestStringArrayAssign()
  {
    string bhl = @"
      
    func []string test() 
    {
      []string arr = new []string
      arr.Add(""foo"")
      arr[0] = ""tst""
      arr.Add(""bar"")
      return arr
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    var val = fb.result.Pop();
    var lst = val.obj as IList<Val>;
    AssertEqual(lst.Count, 2);
    AssertEqual(lst[0].str, "tst");
    AssertEqual(lst[1].str, "bar");
    val.Release();
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestValueIsClonedOnceStoredInArray()
  {
    string bhl = @"
    func test()
    {
      []int ints = []
      for(int i=0;i<3;i++) {
        ints.Add(i)
      }
      
      int local_var_garbage = 10
      for(int i=0;i<ints.Count;i++) {
        trace((string)ints[i] + "";"")
      }

      for(int i=0;i<ints.Count;i++) {
        ints[i] = i
      }

      int local_var_garbage2 = 20
      for(int i=0;i<ints.Count;i++) {
        trace((string)ints[i] + "";"")
      }

    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual(log.ToString(), "0;1;2;0;1;2;");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestArrayPassedToFuncIsChanged()
  {
    string bhl = @"
    func foo([]int a)
    {
      a.Add(100)
    }
      
    func int test() 
    {
      []int a = new []int
      a.Add(1)
      a.Add(2)

      foo(a)

      return a[2]
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 100);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestArrayPassedToFuncByRef()
  {
    string bhl = @"
    func foo(ref []int a)
    {
      a.Add(100)
    }
      
    func int test() 
    {
      []int a = new []int
      a.Add(1)
      a.Add(2)

      foo(ref a)

      return a[2]
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 100);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestArrayCount()
  {
    string bhl = @"
      
    func int test() 
    {
      []string arr = new []string
      arr.Add(""foo"")
      arr.Add(""bar"")
      int c = arr.Count
      return c
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestClearArray()
  {
    string bhl = @"
      
    func string test() 
    {
      []string arr = new []string
      arr.Add(""bar"")
      arr.Clear()
      arr.Add(""foo"")
      return arr[0]
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().str;
    AssertEqual(res, "foo");
    CommonChecks(vm);
  }


  [IsTested()]
  public void TestArrayPool()
  {
    string bhl = @"

    func []string make()
    {
      []string arr = new []string
      return arr
    }

    func add([]string arr)
    {
      arr.Add(""foo"")
      arr.Add(""bar"")
    }
      
    func []string test() 
    {
      []string arr = make()
      add(arr)
      return arr
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.Pop();

    var lst = res.obj as IList<Val>;
    AssertEqual(lst.Count, 2);
    AssertEqual(lst[0].str, "foo");
    AssertEqual(lst[1].str, "bar");

    AssertEqual(vm.vlsts_pool.Allocs, 1);
    AssertEqual(vm.vlsts_pool.Free, 0);
    
    res.Release();

    CommonChecks(vm);
  }

  [IsTested()]
  public void TestArrayPoolInInfiniteLoop()
  {
    string bhl = @"

    func []string make()
    {
      []string arr = new []string
      return arr
    }
      
    func test() 
    {
      while(true) {
        []string arr = new []string
        yield()
      }
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    
    for(int i=0;i<3;++i)
      vm.Tick();

    vm.Stop(fb);

    AssertEqual(vm.vlsts_pool.Allocs, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeClassArray()
  {
    string bhl = @"
      
    func string test(float k) 
    {
      ArrayT_Color cs = new ArrayT_Color
      Color c0 = new Color
      cs.Add(c0)
      cs.RemoveAt(0)
      Color c1 = new Color
      c1.r = 10
      Color c2 = new Color
      c2.g = 20
      cs.Add(c1)
      cs.Add(c2)
      Color c3 = new Color
      cs.Add(c3)
      cs[2].r = 30
      Color c4 = new Color
      cs.Add(c4)
      cs.RemoveAt(3)
      return (string)cs.Count + (string)cs[0].r + (string)cs[1].g + (string)cs[2].r
    }
    ";

    var ts = new Types();
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().str;
    AssertEqual(res, "3102030");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeClassArrayIndexOf()
  {
    {
      string bhl = @"
      func int test() 
      {
        ArrayT_Color cs = []
        Color c0 = {r:1}
        Color c1 = {r:2}
        cs.Add(c0)
        cs.Add(c1)
        return cs.IndexOf(c0)
      }
      ";

      var ts = new Types();
      BindColor(ts);
      var vm = MakeVM(bhl, ts);
      AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func int test() 
      {
        ArrayT_Color cs = []
        Color c0 = {r:1}
        Color c1 = {r:2}
        cs.Add(c0)
        cs.Add(c1)
        return cs.IndexOf(c1)
      }
      ";

      var ts = new Types();
      BindColor(ts);
      var vm = MakeVM(bhl, ts);
      AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }

    //NOTE: Color is a class not a struct, and when we search
    //      for a similar element in an array the 'value equality' 
    //      routine is not used but rather the 'pointers equality' 
    //      one. Maybe it's an expected behavior, maybe not :(
    //      Need more feedback on this issue.
    //
    {
      string bhl = @"
      func int test() 
      {
        ArrayT_Color cs = []
        Color c0 = {r:1}
        Color c1 = {r:2}
        cs.Add(c0)
        cs.Add(c1)
        return cs.IndexOf({r:1})
      }
      ";

      var ts = new Types();
      BindColor(ts);
      var vm = MakeVM(bhl, ts);
      AssertEqual(-1, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestNativeClassTmpArray()
  {
    string bhl = @"

    func ArrayT_Color mkarray()
    {
      ArrayT_Color cs = new ArrayT_Color
      Color c0 = new Color
      c0.g = 1
      cs.Add(c0)
      Color c1 = new Color
      c1.r = 10
      cs.Add(c1)
      return cs
    }
      
    func float test() 
    {
      return mkarray()[1].r
    }
    ";

    var ts = new Types();
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 10);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeSubClassArray()
  {
    string bhl = @"
      
    func string test(float k) 
    {
      Foo f = new Foo
      f.hey = 10
      Color c1 = new Color
      c1.r = 20
      Color c2 = new Color
      c2.g = 30
      f.colors.Add(c1)
      f.colors.Add(c2)
      return (string)f.colors.Count + (string)f.hey + (string)f.colors[0].r + (string)f.colors[1].g
    }
    ";

    var ts = new Types();
    BindColor(ts);
    BindFoo(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().str;
    AssertEqual(res, "2102030");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSuspend()
  {
    string bhl = @"
    func test()
    {
      suspend()
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
  public void TestStatefulNativeFunc()
  {
    string bhl = @"
    func test() 
    {
      WaitTicks(2)
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    var fn = BindWaitTicks(ts, log);

    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .EmitThen(Opcodes.CallNative, new int[] { ts.nfunc_index.IndexOf(fn), 1 })
      .EmitThen(Opcodes.ExitFrame)
    ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestYield()
  {
    string bhl = @"
    func test()
    {
      yield()
    }
    ";

    var ts = new Types();
    var vm = MakeVM(bhl, ts);
    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestYieldSeveralTimesAndReturnValue()
  {
    string bhl = @"
    func int test()
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

    func int test() 
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
  public void TestBasicParal()
  {
    string bhl = @"
    func int test()
    {
      int a
      paral {
        {
          suspend() 
        }
        {
          yield()
          a = 1
        }
      }
      return a
    }
    ";

    var ts = new Types();
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.DeclVar, new int[] { 0, ConstIdx(c, ts.T("int")) })
      .EmitThen(Opcodes.Block, new int[] { (int)BlockType.PARAL, 30})
        .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 8})
          .EmitThen(Opcodes.CallNative, new int[] { ts.nfunc_index.IndexOf("suspend"), 0 })
        .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 14})
          .EmitThen(Opcodes.CallNative, new int[] { ts.nfunc_index.IndexOf("yield"), 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBasicParalAutoSeqWrap()
  {
    string bhl = @"
    func int test()
    {
      int a
      paral {
        suspend() 
        {
          yield()
          a = 1
        }
      }
      return a
    }
    ";

    var ts = new Types();
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.DeclVar, new int[] { 0, ConstIdx(c, ts.T("int")) })
      .EmitThen(Opcodes.Block, new int[] { (int)BlockType.PARAL, 30})
        .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 8})
          .EmitThen(Opcodes.CallNative, new int[] { ts.nfunc_index.IndexOf("suspend"), 0})
        .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 14})
          .EmitThen(Opcodes.CallNative, new int[] { ts.nfunc_index.IndexOf("yield"), 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalWithSubFuncs()
  {
    string bhl = @"
    func foo() {
      suspend()
    }

    func int bar() {
      yield()
      return 1
    }

    func int test() {
      int a
      paral {
        {
          foo()
        }
        {
          a = bar()
        }
      }
      return a
    }
    ";

    var ts = new Types();

    var vm = MakeVM(bhl, ts);
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalWithSubFuncsAndAutoSeqWrap()
  {
    string bhl = @"
    func foo() {
      suspend()
    }

    func int bar() {
      yield()
      return 1
    }

    func int test() {
      int a
      paral {
        foo()
        a = bar()
      }
      return a
    }
    ";

    var ts = new Types();

    var vm = MakeVM(bhl, ts);
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalForComplexStatement()
  {
    string bhl = @"

    func int foo(int a)
    {
      return a
    }

    func float test() 
    {
      Color c = new Color
      int a = 0
      float s = 0
      paral {
        a = foo(10)
        c.r = 142
        s = c.mult_summ(a)
      }
      return a
    }
    ";

    var ts = new Types();

    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 10);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalAllRunning()
  {
    string bhl = @"
    func int test()
    {
      int a
      paral_all {
        {
          suspend() 
        }
        {
          yield()
          a = 1
        }
      }
      return a
    }
    ";

    var ts = new Types();

    var vm = MakeVM(bhl, ts);
    vm.Start("test");
    for(int i=0;i<99;i++)
      AssertTrue(vm.Tick());
  }

  [IsTested()]
  public void TestParalAllFinished()
  {
    string bhl = @"
    func int test()
    {
      int a
      paral_all {
        {
          yield()
          yield()
        }
        {
          yield()
          a = 1
        }
      }
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
  public void TestParalAllForComplexStatement()
  {
    string bhl = @"

    func int foo(int a)
    {
      return a
    }

    func float test() 
    {
      Color c = new Color
      int a = 0
      float s = 0
      paral_all {
        c.r = 142
        s = c.mult_summ(a)
        a = foo(10)
      }
      return a+c.r
    }
    ";

    var ts = new Types();

    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 152);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalAllForNestedSeqs()
  {
    string bhl = @"

    func int foo(int a)
    {
      paral {
        return a
      }
      return 0
    }

    func int test() 
    {
      int i = 0
      paral_all {
        {
          yield()
          if(i == 1) {
            i = foo(2)
          }
        }
        {
          if(i == 0) {
            i = foo(1)
          }
          yield()
        }
      }
      return i
    }
    ";

    var ts = new Types();

    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestYieldWhileInParal()
  {
    string bhl = @"

    func int test() 
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

    func int test() 
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
  public void TestSeqSuccess()
  {
    string bhl = @"

    func foo()
    {
      trace(""FOO"")
    }

    func bar()
    {
      trace(""BAR"")
    }

    func test() 
    {
      {
        bar()
        foo()
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("BARFOO", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSeqFailure()
  {
    string bhl = @"

    func foo()
    {
      trace(""FOO"")
      fail()
    }

    func bar()
    {
      trace(""BAR"")
    }

    func test() 
    {
      {
        bar()
        foo()
        bar()
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("BARFOO", log.ToString());
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

    var vm = MakeVM(bhl);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSeqReturn()
  {
    string bhl = @"

    func int test() 
    {
      int a = 1
      {
        defer { } //this will make sure seq is created
        a = 2
        return a
      }
      return a
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNestedSeqReturn()
  {
    string bhl = @"

    func int foo() {
      {
        defer { } //this will make sure seq is created
        {
          defer { } //this will make sure seq is created
          return 2
        }
      }
      return 0
    }

    func int test() {
      {
        defer { } //this will make sure seq is created
        {
          defer { } //this will make sure seq is created
          return foo()
        }
      }
      return 0
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalFailure()
  {
    string bhl = @"
    func foo()
    {
      yield()
      yield()
      yield()
      trace(""A"")
    }

    func bar()
    {
      yield()
      yield()
      fail()
      trace(""B"")
    }

    func test() 
    {
      paral {
        bar()
        foo()
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());

    AssertEqual("", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalAllFailure()
  {
    string bhl = @"
    func foo()
    {
      yield()
      yield()
      yield()
      trace(""A"")
    }

    func bar()
    {
      yield()
      yield()
      fail()
      trace(""B"")
    }

    func test() 
    {
      paral_all {
        bar()
        foo()
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());

    AssertEqual("", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackUserBindInBinOp()
  {
    string bhl = @"
    func test() 
    {
      bool res = foo(false) != bar_fail(4)
    }
    ";

    var ts = new Types();

    {
      var fn = new FuncSymbolNative("foo", Types.Int,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) {
            frm.stack.PopRelease();
            frm.stack.Push(Val.NewNum(frm.vm, 42));
            return null;
          },
          new FuncArgSymbol("b", ts.T("bool"))
        );
      ts.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("bar_fail", Types.Int,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) {
            frm.stack.PopRelease();
            status = BHS.FAILURE;
            return null;
          },
          new FuncArgSymbol("n", Types.Int)
        );
      ts.ns.Define(fn);
    }

    var vm = MakeVM(bhl, ts);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(BHS.FAILURE, fb.status);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCleanFuncArgsStackOnFailure()
  {
    string bhl = @"
    func int foo(int v) 
    {
      fail()
      return v
    }

    func hey(int a, int b)
    { }

    func test() 
    {
      hey(1, foo(10))
    }
    ";

    var vm = MakeVM(bhl); 
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(0, fb.result.Count);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForSubFailure()
  {
    string bhl = @"

    func hey(int n, int m)
    {
    }

    func int bar()
    {
      return 1
    }

    func int foo()
    {
      fail()
      return 100
    }

    func test() 
    {
      hey(bar(), foo())
    }
    ";

    var vm = MakeVM(bhl); 
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(0, fb.result.Count);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForNativeBind()
  {
    string bhl = @"

    func int foo()
    {
      fail()
      return 100
    }

    func test() 
    {
      hey(""bar"", foo())
    }
    ";

    var ts = new Types();

    {
      var fn = new FuncSymbolNative("hey", Types.Void,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          { return null; },
          new FuncArgSymbol("s", ts.T("string")),
          new FuncArgSymbol("i", Types.Int)
        );
      ts.ns.Define(fn);
    }

    var vm = MakeVM(bhl, ts); 
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(0, fb.result.Count);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForYield()
  {
    string bhl = @"

    func int foo()
    {
      yield()
      return 100
    }

    func hey(string b, int a)
    {
    }

    func test() 
    {
      hey(""bar"", 1 + foo())
    }
    ";

    var vm = MakeVM(bhl); 
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    vm.Stop(fb);
    AssertEqual(0, fb.result.Count);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForYieldInWhile()
  {
    string bhl = @"

    func int foo()
    {
      yield()
      return 1
    }

    func doer(ref int c)
    {
      while(c < 2) {
        c = c + foo()
      }
    }

    func test() 
    {
      int c = 0
      doer(ref c)
    }
    ";

    var vm = MakeVM(bhl); 
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(0, fb.result.Count);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackClassPtr()
  {
    string bhl = @"

    class Foo
    {
      func(int,int) ptr
    }

    func int foo()
    {
      fail()
      return 10
    }

    func void bar(int a, int b)
    {
    }

    func test() 
    {
      Foo f = {}
      f.ptr = bar
      f.ptr(10, foo())
    }
    ";

    var ts = new Types();
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    AssertEqual(0, Execute(vm, "test").result.Count);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCleanFuncThisArgOnStackForMethod()
  {
    string bhl = @"

    func float foo()
    {
      fail()
      return 10
    }

    func test() 
    {
      Color c = new Color
      c.mult_summ(foo())
    }
    ";

    var ts = new Types();
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    AssertEqual(0, Execute(vm, "test").result.Count);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCleanFuncStackInExpressionForYield()
  {
    string bhl = @"

    func int sub_sub_call()
    {
      yield()
      return 2
    }

    func int sub_call()
    {
      return 1 + 10 + 12 + sub_sub_call()
    }

    func test() 
    {
      int cost = 1 + sub_call()
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    vm.Stop(fb);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForFuncPtrPassedAsArg()
  {
    string bhl = @"

    func []int make_ints(int n, int k)
    {
      []int res = []
      return res
    }

    func int do_fail()
    {
      fail()
      return 10
    }

    func test() 
    {
      foo(func []int () { return make_ints(2, do_fail()) } )
      trace(""HERE"")
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    {
      var fn = new FuncSymbolNative("foo", ts.T("Foo"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) {
          var fn_ptr = frm.stack.Pop();
          frm.vm.Start((VM.FuncPtr)fn_ptr.obj, frm);
          fn_ptr.Release();
          return null;
        },
        new FuncArgSymbol("fn", ts.TFunc(ts.TArr(Types.Int)))
      );

      ts.ns.Define(fn);
    }

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual(log.ToString(), "HERE");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForFuncPtr()
  {
    string bhl = @"
    func int foo(int v) 
    {
      fail()
      return v
    }

    func bar()
    {
      func int(int) p = foo
      Foo f = PassthruFoo({hey:1, colors:[{r:p(42)}]})
      trace((string)f.hey)
    }

    func test() 
    {
      bar()
    }
    ";

    var ts = new Types();
    BindColor(ts);
    BindFoo(ts);
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForLambda()
  {
    string bhl = @"
    func bar()
    {
      Foo f = PassthruFoo({hey:1, colors:[{r:
          func int (int v) { 
            fail()
            return v
          }(42) 
        }]})
      trace((string)f.hey)
    }

    func test() 
    {
      bar()
    }
    ";

    var ts = new Types();
    BindColor(ts);
    BindFoo(ts);
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCleanArgsStackInParal()
  {
    string bhl = @"
    func int foo(int ticks) 
    {
      if(ticks == 2) {
        yield()
        yield()
        fail()
      } else if(ticks == 3) {
        yield()
        yield()
        fail()
      }
      return 42
    }

    func bar()
    {
      Foo f
      paral_all {
        {
          f = PassthruFoo({hey:10, colors:[{r:foo(2)}]})
        }
        {
          f = PassthruFoo({hey:20, colors:[{r:foo(3)}]})
        }
      }
      trace((string)f.hey)
    }

    func test() 
    {
      bar()
    }
    ";

    var ts = new Types();
    BindColor(ts);
    BindFoo(ts);
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(fb.status, BHS.FAILURE);
    AssertEqual("", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCleanFuncPtrArgsStackInParal()
  {
    string bhl = @"
    func int foo(int ticks) 
    {
      if(ticks == 2) {
        yield()
        yield()
        fail()
      } else if(ticks == 3) {
        yield()
        yield()
        fail()
      }
      return 42
    }

    func bar()
    {
      func int(int) p = foo
      Foo f
      paral_all {
        {
          f = PassthruFoo({hey:10, colors:[{r:p(2)}]})
        }
        {
          f = PassthruFoo({hey:20, colors:[{r:p(3)}]})
        }
      }
      trace((string)f.hey)
    }

    func test() 
    {
      bar()
    }
    ";

    var ts = new Types();
    BindColor(ts);
    BindFoo(ts);
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(fb.status, BHS.FAILURE);
    AssertEqual("", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCleanArgsStackInParalForLambda()
  {
    string bhl = @"
    func bar()
    {
      Foo f
      paral_all {
        {
          f = PassthruFoo({hey:10, colors:[{r:
              func int (int n) 
              { 
                yield()
                yield()
                fail()
                return n
              }(2) 
              }]})
        }
        {
          f = PassthruFoo({hey:20, colors:[{r:
              func int (int n) 
              { 
                yield()
                yield()
                yield()
                fail()
                return n
              }(3) 
              }]})
        }
      }
      trace((string)f.hey)
    }

    func test() 
    {
      bar()
    }
    ";

    var ts = new Types();
    BindColor(ts);
    BindFoo(ts);
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(fb.status, BHS.FAILURE);
    AssertEqual("", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestInterleaveValuesStackInParal()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    func int ticky()
    {
      return 1
    }

    func int ret_int(int val, int ticks)
    {
      while(ticks > 0) {
        yield()
        ticks = ticks - 1
      }
      return val
    }

    func test() 
    {
      paral {
        {
          foo(1, ret_int(val: 2, ticks: ticky()))
          suspend()
        }
        foo(10, ret_int(val: 20, ticks: 2))
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("1 2;10 20;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestInterleaveFuncStackInParal()
  {
    string bhl = @"
    func int foo(int ticks) 
    {
      if(ticks == 2) {
        yield()
        yield()
      } else if(ticks == 3) {
        yield()
        yield()
        yield()
      }
      return ticks
    }

    func bar()
    {
      Foo f
      paral_all {
        {
          f = PassthruFoo({hey:foo(3), colors:[]})
          trace((string)f.hey)
        }
        {
          f = PassthruFoo({hey:foo(2), colors:[]})
          trace((string)f.hey)
        }
      }
      trace((string)f.hey)
    }

    func test() 
    {
      bar()
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindColor(ts);
    BindFoo(ts);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("233", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestInterleaveValuesStackInParalWithPtrCall()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    func int ret_int(int val, int ticks)
    {
      while(ticks > 0)
      {
        yield()
        ticks = ticks - 1
      }
      return val
    }

    func test() 
    {
      func int(int,int) p = ret_int
      paral {
        {
          foo(1, p(2, 1))
          suspend()
        }
        foo(10, p(20, 2))
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("1 2;10 20;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestInterleaveValuesStackInParalWithMemberPtrCall()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    class Bar { 
      func int(int,int) ptr
    }

    func int ret_int(int val, int ticks)
    {
      while(ticks > 0)
      {
        yield()
        ticks = ticks - 1
      }
      return val
    }

    func test() 
    {
      Bar b = {}
      b.ptr = ret_int
      paral {
        {
          foo(1, b.ptr(2, 1))
          suspend()
        }
        foo(10, b.ptr(20, 2))
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("1 2;10 20;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestInterleaveValuesStackInParalWithLambdaCall()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    func test() 
    {
      paral {
        {
          foo(1, 
              func int (int val, int ticks) {
                while(ticks > 0) {
                  yield()
                  ticks = ticks - 1
                }
                return val
              }(2, 1))
          suspend()
        }
        foo(10, 
            func int (int val, int ticks) {
              while(ticks > 0) {
                yield()
                ticks = ticks - 1
              }
              return val
            }(20, 2))
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("1 2;10 20;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestInterleaveValuesStackInParalAll()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    func int ret_int(int val, int ticks)
    {
      while(ticks > 0)
      {
        yield()
        ticks = ticks - 1
      }
      return val
    }

    func test() 
    {
      paral_all {
        foo(1, ret_int(val: 2, ticks: 1))
        foo(10, ret_int(val: 20, ticks: 2))
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("1 2;10 20;", log.ToString());
    CommonChecks(vm);
  }

  public class Bar_ret_int : ICoroutine
  {
    bool first_time = true;

    int ticks;
    int ret;

    public void Tick(VM.Frame frm, VM.ExecState exec, ref BHS status)
    {
      if(first_time)
      {
        ticks = (int)frm.stack.PopRelease().num;
        ret = (int)frm.stack.PopRelease().num;
        //self
        frm.stack.PopRelease();
        first_time = false;
      }

      if(ticks-- > 0)
      {
        status = BHS.RUNNING;
      }
      else
      {
        frm.stack.Push(Val.NewNum(frm.vm, ret));
      }
    }

    public void Cleanup(VM.Frame frm, VM.ExecState exec)
    {
      first_time = true;
    }
  }

  [IsTested()]
  public void TestInterleaveValuesStackInParalWithMethods()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    func test() 
    {
      paral {
        {
          foo(1, (new Bar).self().ret_int(val: 2, ticks: 1))
          suspend()
        }
        foo(10, (new Bar).self().ret_int(val: 20, ticks: 2))
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    {
      var cl = new ClassSymbolNative("Bar", null,
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          //fake object
          v.SetObj(null, type);
        }
      );
      ts.ns.Define(cl);

      {
        var m = new FuncSymbolNative("self", ts.T("Bar"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) {
            var obj = frm.stack.PopRelease().obj;
            frm.stack.Push(Val.NewObj(frm.vm, obj, ts.T("Bar").Get()));
            return null;
          }
        );
        cl.Define(m);
      }

      {
        var m = new FuncSymbolNative("ret_int", Types.Int,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            return CoroutinePool.New<Bar_ret_int>(frm.vm);
          },
          new FuncArgSymbol("val", Types.Int),
          new FuncArgSymbol("ticks", Types.Int)
        );
        cl.Define(m);
        cl.Setup();
      }
    }

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("1 2;10 20;", log.ToString());
    CommonChecks(vm);
  }

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

    var ts = new Types();
    var log = new StringBuilder();
    var fn = BindTrace(ts, log);

    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Block, new int[] { (int)BlockType.DEFER, 12})
        .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "bar") })
        .EmitThen(Opcodes.CallNative, new int[] { ts.nfunc_index.IndexOf(fn), 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "foo") })
      .EmitThen(Opcodes.CallNative, new int[] { ts.nfunc_index.IndexOf(fn), 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;

    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    func int doer() 
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

    func test() 
    {
      paral {
        {
          doer()
        }
        {
          suspend()
        }
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindLog(ts);
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    func doer() 
    {
      float k = 1
      defer {
        if(k == 1) {
          foo(k)
        }
      }
      suspend()
      k = 2
    }

    func test() 
    {
      paral {
        {
          doer()
        }
        {
          yield()
          yield()
        }
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("1", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBugWithStartingFiberFromDeferInterruptedParal()
  {
    string bhl = @"

    func doer() 
    {
      defer {
        start(func() {})
        trace(""142"")
      }
      suspend()
    }

    func wow()
    {
      paral {
        {
          doer()
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

    func test() 
    {
      wow()
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("142", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBugWithStartingFiberFromDeferInterruptedParalAndRefs()
  {
    string bhl = @"

    func doer(ref int a) 
    {
      defer {
        start(func() {})
      }
      a = 42
      suspend()
    }

    func wow(ref int a)
    {
      paral {
        {
          doer(ref a)
        }
        {
          yield()
          return
        }
      }
    }

    func test() 
    {
      int a = 0
      wow(ref a)
      trace((string)a)
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("42", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBugWithParalInWhile()
  {
    string bhl = @"

    func changer() {
      yield()
    }

    func doer(int i) {
      suspend()
    }

    func test() 
    {
      int i = 0
      while(i < 2) {
        paral {
          changer()
          doer(i)
        }
        i++
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      @"nested defers are not allowed"
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      @"return is not allowed in defer block"
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

    var ts = new Types();

    Compile(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    func level_body(func() cb) {
      defer {
        trace(""~level_body"")
      }

      cb()

      level_start()
    }

    func test() {
      level_body(func() { 
        yield() 
      } )
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("elsefoohey", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestOperatorTernaryIfIncompatibleTypes()
  {
    string bhl1 = @"
    func test()
    {
      string foo = true ? ""Foo"" : 1
    }
    ";

    string bhl2 = @"
    func int test()
    {
      return true ? ""Foo"" : ""Bar""
    }
    ";

    AssertError<Exception>(
      delegate() {
        Compile(bhl1);
      },
      "incompatible types"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl2);
      },
      "incompatible types"
    );
  }

  [IsTested()]
  public void TestOperatorTernaryIf()
  {
    string bhl = @"

    func int min(float a, int b)
    {
      return a < b ? (int)a : b
    }

    func int test1() 
    {
      float a = 100500
      int b   = 500100

      int c = a > b ? b : (int)a //500100
      
      return min(a > c ? b : c/*500100*/, (int)a/*100500*/)
    }

    func int test2() 
    {
      func int() af = func int() { return 500100 } //500100

      func int() bf = func int() { 
        int a = 2
        int b = 1

        int c = a > b ? 100500 : 500100

        return c //100500
      }

      func int() cf = func int() { 
        return true ? 100500 : 500100 //100500
      }

      return min(af()/*500100*/, 
        af() > bf()/*true*/ ? (false ? af() : cf()/*100500*/) : bf())
        /*100500*/ > af()/*500100*/ ? af() : cf()/*100500*/
    }

    func string test3(int v)
    {
      func string() af = func string() {
        return v == 1 ? ""first value""  :
               v == 2 ? ""second value"" :
               v == 3 ? ""result value"" : ""default value""
      }

      return af()
    }
    ";

    var vm = MakeVM(bhl);

    AssertEqual(Execute(vm, "test1").result.PopRelease().num, 100500);
    AssertEqual(Execute(vm, "test2").result.PopRelease().num, 100500);
    AssertEqual(Execute(vm, "test3", Val.NewNum(vm, 0)).result.PopRelease().str, "default value");
    AssertEqual(Execute(vm, "test3", Val.NewNum(vm, 2)).result.PopRelease().str, "second value");

    CommonChecks(vm);
  }

  [IsTested()]
  public void TestOperatorPostfixIncrementCall()
  {
    string bhl = @"
    func int test1()
    {
      int i = 0
      i++
      i = i - 1
      i++
      i = i - 1

      return i
    }

    func test2()
    {
      for(int i = 0; i < 3;) {
        trace((string)i)

        i++
      }

      for(int j = 0; j < 3; j++) {
        trace((string)j)
      }

      for(int j = 0, int k = 0, k++; j < 3; j++, k++) {
        trace((string)j)
        trace((string)k)
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);

    AssertEqual(Execute(vm, "test1").result.PopRelease().num, 0);

    Execute(vm, "test2");
    AssertEqual("012012011223", log.ToString());

    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBadOperatorPostfixIncrementCall()
  {
    string bhl1 = @"
    func test()
    {
      ++
    }
    ";

    string bhl2 = @"
    func test()
    {
      string str = ""Foo""
      str++
    }
    ";

    string bhl3 = @"
    func test()
    {
      for(int j = 0; j < 3; ++) {
      }
    }
    ";

    string bhl4 = @"
    func test()
    {
      int j = 0
      for(++; j < 3; j++) {

      }
    }
    ";

    string bhl5 = @"
    func test()
    {
      for(j = 0, k++, int k = 0; j < 3; j++, k++) {
        trace((string)j)
        trace((string)k)
      }
    }
    ";

    string bhl6 = @"
    
    func foo(float a)
    {
    }

    func int test()
    {
      int i = 0
      foo(i++)
      return i
    }
    ";

    string bhl7 = @"
    func test()
    {
      []int arr = [0, 1, 3, 4]
      int i = 0
      int j = arr[i++]
    }
    ";

    string bhl8 = @"
    func int test()
    {
      func bool(int) foo = func bool(int b) { return b > 1 }

      int i = 0
      foo(i++)
      return i
    }
    ";

    string bhl9 = @"
    func int test()
    {
      int i = 0
      return i++
    }
    ";

    string bhl10 = @"
    func int, int test()
    {
      int i = 0
      int j = 1
      return j, i++
    }
    ";

    string bhl11 = @"
    func int, int test()
    {
      int i = 0
      int j = 1
      return j++, i
    }
    ";

    AssertError<Exception>(
      delegate() {
        Compile(bhl1);
      },
      "extraneous input '++' expecting '}'"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl2);
      },
      "incompatible types"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl3);
      },
      "extraneous input '++' expecting ')'"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl4);
      },
      "extraneous input '++' expecting ';'"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl5);
      },
      "symbol 'j' not resolved"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl6);
      },
      "no viable alternative at input 'foo(i++'"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl7);
      },
      "no viable alternative at input '[i++'"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl8);
      },
      "no viable alternative at input 'foo(i++'"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl9);
      },
      "return value is missing"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl10);
      },
      "extraneous input '++' expecting '}'"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl11);
      },
      "mismatched input ',' expecting '}'"
    );
  }

  [IsTested()]
  public void TestBadOperatorPostfixDecrementCall()
  {
    string bhl1 = @"
    func test()
    {
      --
    }
    ";

    string bhl2 = @"
    func test()
    {
      string str = ""Foo""
      str--
    }
    ";

    string bhl3 = @"
    func test()
    {
      for(int j = 0; j < 3; --) {
      }
    }
    ";

    string bhl4 = @"
    func test()
    {
      int j = 0
      for(--; j < 3; j++) {

      }
    }
    ";

    string bhl5 = @"
    
    func foo(float a)
    {
    }

    func int test()
    {
      int i = 0
      foo(i--)
      return i
    }
    ";

    string bhl6 = @"
    func test()
    {
      []int arr = [0, 1, 3, 4]
      int i = 0
      int j = arr[i--]
    }
    ";

    string bhl7 = @"
    func int test()
    {
      int i = 0
      return i--
    }
    ";

    string bhl8 = @"
    func int, int test()
    {
      int i = 0
      int j = 1
      return j, i--
    }
    ";

    string bhl9 = @"
    func int, int test()
    {
      int i = 0
      int j = 1
      return j--, i
    }
    ";

    AssertError<Exception>(
      delegate() {
        Compile(bhl1);
      },
      "extraneous input '--' expecting '}'"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl2);
      },
      "incompatible types"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl3);
      },
      "extraneous input '--' expecting ')'"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl4);
      },
      "extraneous input '--' expecting ';'"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl5);
      },
      "no viable alternative at input 'foo(i--'"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl6);
      },
      "no viable alternative at input '[i--'"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl7);
      },
      "return value is missing"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl8);
      },
      "extraneous input '--' expecting '}'"
    );

    AssertError<Exception>(
      delegate() {
        Compile(bhl9);
      },
      "mismatched input ',' expecting '}'"
    );
  }

  [IsTested()]
  public void TestOperatorPostfixDecrementCall()
  {
    string bhl = @"
    func int test1()
    {
      int i = 0
      i--
      i = i + 1
      i--
      i = i + 1

      return i
    }

    func test2()
    {
      for(int i = 3; i >= 0;) {
        trace((string)i)

        i--
      }

      for(int j = 3; j >= 0; j--) {
        trace((string)j)
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);

    AssertEqual(Execute(vm, "test1").result.PopRelease().num, 0);

    Execute(vm, "test2");
    AssertEqual("32103210", log.ToString());

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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("whilewhilefoohey", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDeferInInfiniteLoop()
  {
    string bhl = @"

    func test() 
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    func test() 
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("10", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDeferInSubParalFuncCall()
  {
    string bhl = @"
    func wait_3() {
      defer {
        trace(""~wait_3"")
      }

      trace(""!wait_3"")
      suspend()
    }

    func doer() {
      defer {
        trace(""~doer"")
      }
      paral {
        {
          wait_3()
        }
        {
          yield()
          trace(""!here"")
          yield()
        }
      }
    }

    func test() 
    {
      doer()
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("!wait_3!here~wait_3~doer", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalWithYieldDefer()
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
        {
          yield()
          trace(""wow"")
        }
      }
      trace(""foo"")
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    func test() 
    {
      defer {
        trace(""1"")
      }
      paral {
        {
          defer {
            trace(""2"")
          }
          suspend()
        }
        {
          defer {
            trace(""3"")
          }
          suspend()
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("wowbarfoohey", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalAllWithYieldDefer()
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
        {
          yield()
          trace(""wow"")
        }
      }
      trace(""foo"")
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

    func test() 
    {
      defer {
        trace(""1"")
      }
      paral_all {
        {
          defer {
            trace(""2"")
          }
          suspend()
        }
        {
          defer {
            trace(""3"")
          }
          suspend()
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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

  //TODO:
  //[IsTested()]
  public void TestBugReturnTypeInsteadOfValue()
  {
    string bhl = @"
    enum Bar {
      DUMMY = 1
    }

    func Bar test() 
    {
      return Bar
    }
    ";

    var vm = MakeVM(bhl);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBugEarlyReturnBeforeVarDecl()
  {
    {
    string bhl = @"
      enum Bar {
        DUMMY = 1
      }

      func Bar test() 
      {
        Bar bar
        {
          return
          Bar bar = Bar.DUMMY
        }
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "already defined symbol 'bar'"
      );
    }

    {
    string bhl = @"
      func test() 
      {
        return
        int foo = 10
      }
      ";
      var vm = MakeVM(bhl);
      Execute(vm, "test");
      CommonChecks(vm);
    }

    {
    string bhl = @"
      func test() 
      {
        return
        string str
      }
      ";

      var vm = MakeVM(bhl);
      Execute(vm, "test");
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestParalReturn()
  {
    string bhl = @"

    func int test() 
    {
      paral {
        {
          return 1
        }
        suspend()
      }
      return 0
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalReturnFromSubFunc()
  {
    string bhl = @"

    func int foo() {
      paral {
        return 1
      }
      return 0
    }

    func int test() 
    {
      paral {
        return foo()
      }
      return 0
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalNestedReturnFromSubFuncWithSeq()
  {
    string bhl = @"

    func int foo() {
      {
        defer { }
        return 1
      }
      return 0
    }

    func int test() 
    {
      paral {
        paral {
          return foo()
        }
      }
      return 0
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalNestedReturnFromSubFuncWithParalAllSeq()
  {
    string bhl = @"

    func int foo() {
      paral_all {
        {
          {
            defer { }
            return 1
          }
        }
      }
      return 0
    }

    func int test() 
    {
      paral {
        paral {
          return foo()
        }
      }
      return 0
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalNestedReturn()
  {
    string bhl = @"

    func int test() 
    {
      paral {
        {
          paral {
            suspend()
            {
              return 1
            }
          }
        }
        suspend()
      }
      return 0
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalAllReturn()
  {
    string bhl = @"

    func int test() 
    {
      paral_all {
        {
          return 1
        }
        suspend()
      }
      return 0
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalAllYieldReturn()
  {
    string bhl = @"

    func int test() 
    {
      paral_all {
        yield()
        return 1
      }
      return 0
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }
  
  [IsTested()]
  public void TestParalAllNestedReturn()
  {
    string bhl = @"

    func int test() 
    {
      paral_all {
        {
          paral_all {
            suspend()
            {
              return 1
            }
          }
        }
        suspend()
      }
      return 0
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestParalAllNestedParalInFunc()
  {
    string bhl = @"
    func bar() {
      trace(""bar"")
    }

    func foo() {
      paral {
        bar()
      }
    }

    func test() {
      paral_all {
        foo()
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("bar", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestRunningInDeferIsException()
  {
    string bhl = @"

    func test() 
    {
      defer {
        suspend()
      }
    }
    ";

    var vm = MakeVM(bhl);

    AssertError<Exception>(
      delegate() { 
        Execute(vm, "test");
      },
      "Defer execution invalid status: RUNNING"
    );
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("lmb1lmb2foohey", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonInitForUserClass()
  {
    string bhl = @"

    class Foo { 
      int Int
      float Flt
      string Str
    }
      
    func test() 
    {
      Foo f = {Int: 10, Flt: 14.2, Str: ""Hey""}
      trace((string)f.Int + "";"" + (string)f.Flt + "";"" + f.Str)
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    var fn = BindTrace(ts, log);
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, c.ns.T("Foo")) }) 
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 14.2) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 2 })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 0 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, c.ns.T("string")), 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 1 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, c.ns.T("string")), 0 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 2 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.CallNative, new int[] { ts.nfunc_index.IndexOf(fn), 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("10;14.2;Hey", log.ToString().Replace(',', '.')/*locale issues*/);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonEmptyCtor()
  {
    string bhl = @"
    func float test()
    {
      Color c = {}
      return c.r + c.g
    }
    ";

    var ts = new Types();
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonPartialCtor()
  {
    string bhl = @"
    func float test()
    {
      Color c = {g: 10}
      return c.r + c.g
    }
    ";

    var ts = new Types();
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonFullCtor()
  {
    string bhl = @"
    func float test()
    {
      Color c = {r: 1, g: 10}
      return c.r + c.g
    }
    ";

    var ts = new Types();
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    AssertEqual(11, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonCtorNotExpectedMember()
  {
    string bhl = @"
    func test()
    {
      Color c = {b: 10}
    }
    ";

    var ts = new Types();
    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      @"no such attribute 'b' in class 'Color"
    );
  }

  [IsTested()]
  public void TestJsonCtorBadType()
  {
    string bhl = @"
    func test()
    {
      Color c = {r: ""what""}
    }
    ";

    var ts = new Types();

    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "incompatible types"
    );
  }

  [IsTested()]
  public void TestJsonExplicitEmptyClass()
  {
    string bhl = @"
      
    func float test() 
    {
      ColorAlpha c = new ColorAlpha {}
      return c.r + c.g + c.a
    }
    ";

    var ts = new Types();
    
    BindColorAlpha(ts);

    var vm = MakeVM(bhl, ts);
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonExplicitClass()
  {
    string bhl = @"
      
    func float test() 
    {
      ColorAlpha c = new ColorAlpha {a: 1, g: 10, r: 100}
      return c.r + c.g + c.a
    }
    ";

    var ts = new Types();
    
    BindColorAlpha(ts);

    var vm = MakeVM(bhl, ts);
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonExplicitSubClass()
  {
    string bhl = @"
      
    func float test() 
    {
      Color c = new ColorAlpha {a: 1, g: 10, r: 100}
      ColorAlpha ca = (ColorAlpha)c
      return ca.r + ca.g + ca.a
    }
    ";

    var ts = new Types();
    
    BindColorAlpha(ts);

    var vm = MakeVM(bhl, ts);
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonExplicitSubClassNotCompatible()
  {
    string bhl = @"
      
    func test() 
    {
      ColorAlpha c = new Color {g: 10, r: 100}
    }
    ";

    var ts = new Types();
    
    BindColorAlpha(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "incompatible types"
    );
  }

  [IsTested()]
  public void TestJsonReturnObject()
  {
    string bhl = @"

    func Color make()
    {
      return {r: 42}
    }
      
    func float test() 
    {
      Color c = make()
      return c.r
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonExplicitNoSuchClass()
  {
    string bhl = @"
      
    func test() 
    {
      any c = new Foo {}
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"type 'Foo' not found"
    );
  }

  [IsTested()]
  public void TestJsonArrInitForUserClass()
  {
    string bhl = @"

    class Foo { 
      int Int
      float Flt
      string Str
    }
      
    func test() 
    {
      []Foo fs = [{Int: 10, Flt: 14.2, Str: ""Hey""}]
      Foo f = fs[0]
      trace((string)f.Int + "";"" + (string)f.Flt + "";"" + f.Str)
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    var fn = BindTrace(ts, log);
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 2 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, c.ns.TArr("Foo")) }) 
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, c.ns.T( "Foo")) }) 
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 14.2) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 2 })
      .EmitThen(Opcodes.ArrAddInplace)
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .EmitThen(Opcodes.ArrIdx)
      .EmitThen(Opcodes.SetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetAttr, new int[] { 0 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, c.ns.T("string")), 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetAttr, new int[] { 1 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, c.ns.T("string")), 0 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetAttr, new int[] { 2 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.CallNative, new int[] { ts.nfunc_index.IndexOf(fn), 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("10;14.2;Hey", log.ToString().Replace(',', '.')/*locale issues*/);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrInitForUserClassMultiple()
  {
    string bhl = @"

    class Foo { 
      int Int
      float Flt
      string Str
    }
      
    func test() 
    {
      []Foo fs = [{Int: 10, Flt: 14.2, Str: ""Hey""},{Flt: 15.1, Int: 2, Str: ""Foo""}]
      trace((string)fs[1].Int + "";"" + (string)fs[1].Flt + "";"" + fs[1].Str + ""-"" + 
           (string)fs[0].Int + "";"" + (string)fs[0].Flt + "";"" + fs[0].Str)
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("2;15.1;Foo-10;14.2;Hey", log.ToString().Replace(',', '.')/*locale issues*/);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonEmptyArrCtor()
  {
    string bhl = @"
    func int test()
    {
      []int a = []
      return a.Count 
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrCtor()
  {
    string bhl = @"
    func int test()
    {
      []int a = [1,2,3]
      return a[0] + a[1] + a[2]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(6, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrComplexCtor()
  {
    string bhl = @"
    func float test()
    {
      []Color cs = [{r:10, g:100}, {g:1000, r:1}]
      return cs[0].r + cs[0].g + cs[1].r + cs[1].g
    }
    ";

    var ts = new Types();

    BindColor(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(1111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonDefaultEmptyArg()
  {
    string bhl = @"
    func float foo(Color c = {})
    {
      return c.r
    }

    func float test()
    {
      return foo()
    }
    ";

    var ts = new Types();

    BindColor(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonDefaultEmptyArgWithOtherDefaultArgs()
  {
    string bhl = @"
    func float foo(Color c = {}, float a = 10)
    {
      return c.r
    }

    func float test()
    {
      return foo(a : 20)
    }
    ";

    var ts = new Types();

    BindColor(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonDefaultArgWithOtherDefaultArgs()
  {
    string bhl = @"
    func float foo(Color c = {r:20}, float a = 10)
    {
      return c.r + a
    }

    func float test()
    {
      return foo(a : 20)
    }
    ";

    var ts = new Types();

    BindColor(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(40, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonDefaultArgIncompatibleType()
  {
    string bhl = @"
    func float foo(ColorAlpha c = new Color{r:20})
    {
      return c.r
    }

    func float test()
    {
      return foo()
    }
    ";

    var ts = new Types();

    BindColorAlpha(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "incompatible types"
    );
  }

  [IsTested()]
  public void TestJsonArgTypeMismatch()
  {
    string bhl = @"
    func float foo(float a = 10)
    {
      return a
    }

    func float test()
    {
      return foo(a : {})
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"can't be specified with {..}"
    );
  }

  [IsTested()]
  public void TestJsonDefaultArg()
  {
    string bhl = @"
    func float foo(Color c = {r:10})
    {
      return c.r
    }

    func float test()
    {
      return foo()
    }
    ";

    var ts = new Types();

    BindColor(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrDefaultArg()
  {
    string bhl = @"
    func float foo([]Color c = [{r:10}])
    {
      return c[0].r
    }

    func float test()
    {
      return foo()
    }
    ";

    var ts = new Types();

    BindColor(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrEmptyDefaultArg()
  {
    string bhl = @"
    func int foo([]Color c = [])
    {
      return c.Count
    }

    func int test()
    {
      return foo()
    }
    ";

    var ts = new Types();

    BindColor(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonObjReAssign()
  {
    string bhl = @"
    func float test()
    {
      Color c = {r: 1}
      c = {g:10}
      return c.r + c.g
    }
    ";

    var ts = new Types();

    BindColor(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrReAssign()
  {
    string bhl = @"
    func float test()
    {
      []float b = [1]
      b = [2,3]
      return b[0] + b[1]
    }
    ";

    var ts = new Types();

    BindColor(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(5, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrayExplicit()
  {
    string bhl = @"
      
    func float test() 
    {
      []Color cs = [new ColorAlpha {a:4}, {g:10}, new Color {r:100}]
      ColorAlpha ca = (ColorAlpha)cs[0]
      return ca.a + cs[1].g + cs[2].r
    }
    ";

    var ts = new Types();

    BindColorAlpha(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(114, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrayReturn()
  {
    string bhl = @"

    func []Color make()
    {
      return [new ColorAlpha {a:4}, {g:10}, new Color {r:100}]
    }
      
    func float test() 
    {
      []Color cs = make()
      ColorAlpha ca = (ColorAlpha)cs[0]
      return ca.a + cs[1].g + cs[2].r
    }
    ";

    var ts = new Types();

    BindColorAlpha(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(114, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrayExplicitAsArg()
  {
    string bhl = @"

    func float foo([]Color cs)
    {
      ColorAlpha ca = (ColorAlpha)cs[0]
      return ca.a + cs[1].g + cs[2].r
    }
      
    func float test() 
    {
      return foo([new ColorAlpha {a:4}, {g:10}, new Color {r:100}])
    }
    ";

    var ts = new Types();

    BindColorAlpha(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(114, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrayExplicitSubClass()
  {
    string bhl = @"
      
    func float test() 
    {
      []Color cs = [{r:1,g:2}, new ColorAlpha {g: 10, r: 100, a:2}]
      ColorAlpha c = (ColorAlpha)cs[1]
      return c.r + c.g + c.a
    }
    ";

    var ts = new Types();

    BindColorAlpha(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(112, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrayExplicitSubClassNotCompatible()
  {
    string bhl = @"
      
    func test() 
    {
      []ColorAlpha c = [{r:1,g:2,a:100}, new Color {g: 10, r: 100}]
    }
    ";

    var ts = new Types();
    
    BindColorAlpha(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "incompatible types"
    );
  }

  [IsTested()]
  public void TestJsonMasterStructWithClass()
  {
    string bhl = @"
      
    func string test() 
    {
      MasterStruct n = {
        child : {str : ""hey""},
        child2 : {str : ""hey2""}
      }
      return n.child2.str
    }
    ";

    var ts = new Types();

    BindMasterStruct(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual("hey2", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonMasterStructWithStruct()
  {
    string bhl = @"
      
    func int test() 
    {
      MasterStruct n = {
        child_struct : {n: 1},
        child_struct2 : {n: 2}
      }
      return n.child_struct2.n
    }
    ";

    var ts = new Types();

    BindMasterStruct(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonFuncArg()
  {
    string bhl = @"
    func test(float b) 
    {
      Foo f = PassthruFoo({hey:142, colors:[{r:2}, {g:3}, {g:b}]})
      trace((string)f.hey + (string)f.colors.Count + (string)f.colors[0].r + (string)f.colors[1].g + (string)f.colors[2].g)
    }
    ";

    var ts = new Types();

    var log = new StringBuilder();
    BindTrace(ts, log);
    BindColor(ts);
    BindFoo(ts);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test", Val.NewNum(vm, 42));
    AssertEqual("14232342", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonFuncArgChainCall()
  {
    string bhl = @"
    func test(float b) 
    {
      trace((string)PassthruFoo({hey:142, colors:[{r:2}, {g:3}, {g:b}]}).colors.Count)
    }
    ";

    var ts = new Types();

    var log = new StringBuilder();
    BindTrace(ts, log);
    BindColor(ts);
    BindFoo(ts);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test", Val.NewNum(vm, 42));
    AssertEqual("3", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNullWithEncodedStruct()
  {
    string bhl = @"
      
    func test() 
    {
      IntStruct c = null
      IntStruct c2 = {n: 1}
      if(c == null) {
        trace(""NULL;"")
      }
      if(c2 == null) {
        trace(""NEVER;"")
      }
      if(c2 != null) {
        trace(""NOTNULL;"")
      }
      c = c2
      if(c2 == c) {
        trace(""EQ;"")
      }
      if(c2 == null) {
        trace(""NEVER;"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindIntStruct(ts);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("NULL;NOTNULL;EQ;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNullPassedAsNullObj()
  {
    string bhl = @"
      
    func test(StringClass c) 
    {
      if(c != null) {
        trace(""NEVER;"")
      }
      if(c == null) {
        trace(""NULL;"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindStringClass(ts);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test", Val.NewObj(vm, null, Types.Any));
    AssertEqual("NULL;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNullPassedAsNewNil()
  {
    string bhl = @"
      
    func test(StringClass c) 
    {
      if(c != null) {
        trace(""NEVER;"")
      }
      if(c == null) {
        trace(""NULL;"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindStringClass(ts);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test", Val.NewObj(vm, null, Types.Any));
    AssertEqual("NULL;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSetNullObjFromUserBinding()
  {
    string bhl = @"
      
    func test() 
    {
      Color c = mkcolor_null()
      if(c == null) {
        trace(""NULL;"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("NULL;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNullIncompatible()
  {
    string bhl = @"
      
    func bool test() 
    {
      return 0 == null
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incompatible types"
    );
  }

  [IsTested()]
  public void TestNullArray()
  {
    string bhl = @"
      
    func test() 
    {
      []Color cs = null
      []Color cs2 = new []Color
      if(cs == null) {
        trace(""NULL;"")
      }
      if(cs2 == null) {
        trace(""NULL2;"")
      }
      if(cs2 != null) {
        trace(""NOT NULL;"")
      }
      cs = cs2
      if(cs2 == cs) {
        trace(""EQ;"")
      }
      if(cs2 == null) {
        trace(""NEVER;"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("NULL;NOT NULL;EQ;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNullArrayByDefault()
  {
    string bhl = @"
      
    func test() 
    {
      []Color cs
      if(cs == null) {
        trace(""NULL;"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("NULL;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNullFuncPtr()
  {
    string bhl = @"
      
    func test() 
    {
      func() fn = null
      func() fn2 = func () { }
      if(fn == null) {
        trace(""NULL;"")
      }
      if(fn != null) {
        trace(""NEVER;"")
      }
      if(fn2 == null) {
        trace(""NEVER2;"")
      }
      if(fn2 != null) {
        trace(""NOT NULL;"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("NULL;NOT NULL;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNullFuncPtrAsDefaultFuncArg()
  {
    string bhl = @"

    func foo(int a, func int(int) fn = null) {
      if(fn == null) {
        trace(""NULL;"")
      } else {
        trace(""NOT NULL;"")
      }
    }
      
    func test() 
    {
      foo(1)
      foo(2, func int(int a) { return a})
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("NULL;NOT NULL;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestWhileEmptyArrLoop()
  {
    string bhl = @"

    func test() 
    {
      []int arr = []
      int i = 0
      while(i < arr.Count) {
        int tmp = arr[i]
        trace((string)tmp)
        i = i + 1
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestWhileArrLoop()
  {
    string bhl = @"

    func test() 
    {
      []int arr = [1,2,3]
      int i = 0
      while(i < arr.Count) {
        int tmp = arr[i]
        trace((string)tmp)
        i = i + 1
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("123", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNestedWhileArrLoopWithVarDecls()
  {
    string bhl = @"

    func test() 
    {
      []int arr = [1,2]
      int i = 0
      while(i < arr.Count) {
        int tmp = arr[i]
        int j = 0
        while(j < arr.Count) {
          int tmp2 = arr[j]
          trace((string)tmp + "","" + (string)tmp2 + "";"")
          j = j + 1
        }
        i = i + 1
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("1,1;1,2;2,1;2,2;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestWhileCounterOnTheTop()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      while(i < 3) {
        i = i + 1
        trace((string)i)
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("123", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestWhileFalse()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      while(false) {
        trace((string)i)
        i = i + 1
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestOrShortCircuit()
  {
    string bhl = @"
      
    func test() 
    {
      Color c = null
      if(c == null || c.r == 0) {
        trace(""OK;"")
      } else {
        trace(""NEVER;"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindColor(ts);

    var vm = MakeVM(bhl, ts);

    Execute(vm, "test");
    AssertEqual("OK;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestAndShortCircuit()
  {
    string bhl = @"
      
    func test() 
    {
      Color c = null
      if(c != null && c.r == 0) {
        trace(""NEVER;"")
      } else {
        trace(""OK;"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindColor(ts);

    var vm = MakeVM(bhl, ts);

    Execute(vm, "test");
    AssertEqual("OK;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestAndShortCircuitWithBoolNot()
  {
    string bhl = @"
      
    func test() 
    {
      bool ready = false
      bool activated = true
      if(!ready && activated) {
        trace(""OK;"")
      } else {
        trace(""NEVER;"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);

    Execute(vm, "test");
    AssertEqual("OK;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestAndShortCircuitWithBoolNotForClassMembers()
  {
    string bhl = @"

    class Foo 
    {
      bool ready
      bool activated
    }
      
    func test() 
    {
      Foo foo = {}
      foo.ready = false
      foo.activated = true
      if(!foo.ready && foo.activated) {
        trace(""OK;"")
      } else {
        trace(""NEVER;"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);

    Execute(vm, "test");
    AssertEqual("OK;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestOrShortCircuitWithBoolNotForClassMembers()
  {
    string bhl = @"

    class Foo 
    {
      bool ready
      bool activated
    }
      
    func test() 
    {
      Foo foo = {}
      foo.ready = false
      foo.activated = true
      if(foo.ready || foo.activated) {
        trace(""OK;"")
      } else {
        trace(""NEVER;"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);

    Execute(vm, "test");
    AssertEqual("OK;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestChainCall()
  {
    string bhl = @"
      
    func Color MakeColor(float g)
    {
      Color c = new Color
      c.g = g
      return c
    }

    func float test(float k) 
    {
      return MakeColor(k).mult_summ(k*2)
    }
    ";

    var ts = new Types();
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 8);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestChainCall2()
  {
    string bhl = @"
      
    func Color MakeColor(float g)
    {
      Color c = new Color
      c.g = g
      return c
    }

    func float test(float k) 
    {
      return MakeColor(k).g
    }
    ";

    var ts = new Types();
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestChainCall3()
  {
    string bhl = @"
      
    func Color MakeColor(float g)
    {
      Color c = new Color
      c.g = g
      return c
    }

    func float test(float k) 
    {
      return MakeColor(k).Add(1).g
    }
    ";

    var ts = new Types();
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 3);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestChainCall4()
  {
    string bhl = @"
      
    func Color MakeColor(float g)
    {
      Color c = new Color
      c.g = g
      return c
    }

    func float test(float k) 
    {
      return MakeColor(MakeColor(k).g).g
    }
    ";

    var ts = new Types();
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestChainCall5()
  {
    string bhl = @"
      
    func Color MakeColor2(string temp)
    {
      Color c = new Color
      return c
    }

    func Color MakeColor(float g)
    {
      Color c = new Color
      c.g = g
      return c
    }

    func bool Check(bool cond)
    {
      return cond
    }

    func bool test(float k) 
    {
      Color o = new Color
      o.g = k
      return Check(MakeColor2(""hey"").Add(1).Add(MakeColor(k).g).r == 3)
    }
    ";

    var ts = new Types();
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrInitForNativeClass()
  {
    string bhl = @"

    func test() 
    {
      []Bar bs = [{Int: 10, Flt: 14.5, Str: ""Hey""}]
      Bar b = bs[0]
      trace((string)b.Int + "";"" + (string)b.Flt + "";"" + b.Str)
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    var fn = BindTrace(ts, log);
    BindBar(ts);
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 2 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, ts.TArr("Bar")) }) 
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, ts.T("Bar")) }) 
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 14.5) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 2 })
      .EmitThen(Opcodes.ArrAddInplace)
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .EmitThen(Opcodes.ArrIdx)
      .EmitThen(Opcodes.SetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetAttr, new int[] { 0 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, ts.T("string")), 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetAttr, new int[] { 1 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, ts.T("string")), 0 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetAttr, new int[] { 2 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.CallNative, new int[] { ts.nfunc_index.IndexOf(fn), 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("10;14.5;Hey", log.ToString().Replace(',', '.')/*locale issues*/);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeClassCallMethod()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      Color c = new Color
      c.r = 10
      c.g = 20
      return c.mult_summ(k)
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 60);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastClassToAny()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      Color c = new Color
      c.r = k
      c.g = k*100
      any a = (any)c
      Color b = (Color)a
      return b.g + b.r 
    }
    ";

    var ts = new Types();

    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 202);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestAnyNullEquality()
  {
    string bhl = @"
      
    func bool test() 
    {
      any foo
      return foo == null
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().bval;
    AssertTrue(res);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestAnyNullAssign()
  {
    string bhl = @"
      
    func bool test() 
    {
      any foo = null
      return foo == null
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().bval;
    AssertTrue(res);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPlusNotOverloadedForNativeClass()
  {
    string bhl = @"
      
    func test() 
    {
      Color c1
      Color c2
      Color c3 = c1 + c2
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestMinusNotOverloadedForNativeClass()
  {
    string bhl = @"
      
    func test() 
    {
      Color c1
      Color c2
      Color c3 = c1 - c2
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestMultNotOverloadedForNativeClass()
  {
    string bhl = @"
      
    func test() 
    {
      Color c1
      Color c2
      Color c3 = c1 * c2
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestDivNotOverloadedForNativeClass()
  {
    string bhl = @"
      
    func test() 
    {
      Color c1
      Color c2
      Color c3 = c1 / c2
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestGtNotOverloadedForNativeClass()
  {
    string bhl = @"
      
    func test() 
    {
      Color c1
      Color c2
      bool r = c1 > c2
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestGteNotOverloadedForNativeClass()
  {
    string bhl = @"
      
    func test() 
    {
      Color c1
      Color c2
      bool r = c1 >= c2
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestLtNotOverloadedForNativeClass()
  {
    string bhl = @"
      
    func test() 
    {
      Color c1
      Color c2
      bool r = c1 > c2
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestLteNotOverloadedForNativeClass()
  {
    string bhl = @"
      
    func test() 
    {
      Color c1
      Color c2
      bool r = c1 > c2
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestUnaryMinusNotOverloadedForNativeClass()
  {
    string bhl = @"
      
    func test() 
    {
      Color c1
      Color c2 = -c1
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      @"must be numeric type"
    );
  }

  [IsTested()]
  public void TestBitAndNotOverloadedForNativeClass()
  {
    string bhl = @"
      
    func test() 
    {
      Color c1
      Color c2
      int a = c2 & c1
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      @"must be int type"
    );
  }

  [IsTested()]
  public void TestBitOrNotOverloadedForNativeClass()
  {
    string bhl = @"
      
    func test() 
    {
      Color c1
      Color c2
      int a = c2 | c1
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      @"must be int type"
    );
  }

  [IsTested()]
  public void TestLogicalAndNotOverloadedForNativeClass()
  {
    string bhl = @"
      
    func test() 
    {
      Color c1
      Color c2
      bool a = c2 && c1
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      @"must be bool type"
    );
  }

  [IsTested()]
  public void TestLogicalOrNotOverloadedForNativeClass()
  {
    string bhl = @"
      
    func test() 
    {
      Color c1
      Color c2
      bool a = c2 || c1
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      @"must be bool type"
    );
  }

  [IsTested()]
  public void TestUnaryNotNotOverloadedForNativeClass()
  {
    string bhl = @"
      
    func test() 
    {
      Color c1
      bool a = !c1
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      @"must be bool type"
    );
  }

  [IsTested()]
  public void TestPlusOverloadedForNativeClass()
  {
    string bhl = @"
      
    func Color test() 
    {
      Color c1 = {r:1,g:2}
      Color c2 = {r:20,g:30}
      Color c3 = c1 + c2
      return c3
    }
    ";

    var ts = new Types();
    
    var cl = BindColor(ts, setup: false);
    var op = new FuncSymbolNative("+", ts.T("Color"),
      delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
      {
        var r = (Color)frm.stack.PopRelease().obj;
        var c = (Color)frm.stack.PopRelease().obj;

        var newc = new Color();
        newc.r = c.r + r.r;
        newc.g = c.g + r.g;

        var v = Val.NewObj(frm.vm, newc, ts.T("Color").Get());
        frm.stack.Push(v);

        return null;
      },
      new FuncArgSymbol("r", ts.T("Color"))
    );
    cl.OverloadBinaryOperator(op);
    cl.Setup();

    var vm = MakeVM(bhl, ts);
    var res = (Color)Execute(vm, "test").result.PopRelease().obj;
    AssertEqual(21, res.r);
    AssertEqual(32, res.g);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestMultOverloadedForNativeClass()
  {
    string bhl = @"
      
    func Color test() 
    {
      Color c1 = {r:1,g:2}
      Color c2 = c1 * 2
      return c2
    }
    ";

    var ts = new Types();
    
    var cl = BindColor(ts, setup: false);
    var op = new FuncSymbolNative("*", ts.T("Color"),
      delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
      {
        var k = (float)frm.stack.PopRelease().num;
        var c = (Color)frm.stack.PopRelease().obj;

        var newc = new Color();
        newc.r = c.r * k;
        newc.g = c.g * k;

        var v = Val.NewObj(frm.vm, newc, ts.T("Color").Get());
        frm.stack.Push(v);

        return null;
      },
      new FuncArgSymbol("k", ts.T("float"))
    );
    cl.OverloadBinaryOperator(op);
    cl.Setup();

    var vm = MakeVM(bhl, ts);
    var res = (Color)Execute(vm, "test").result.PopRelease().obj;
    AssertEqual(2, res.r);
    AssertEqual(4, res.g);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestOverloadedBinOpsPriorityForNativeClass()
  {
    string bhl = @"
      
    func Color test() 
    {
      Color c1 = {r:1,g:2}
      Color c2 = {r:10,g:20}
      Color c3 = c1 + c2 * 2
      return c3
    }
    ";

    var ts = new Types();
    
    var cl = BindColor(ts, setup: false);
    {
      var op = new FuncSymbolNative("*", ts.T("Color"),
      delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
      {
        var k = (float)frm.stack.PopRelease().num;
        var c = (Color)frm.stack.PopRelease().obj;

        var newc = new Color();
        newc.r = c.r * k;
        newc.g = c.g * k;

        var v = Val.NewObj(frm.vm, newc, ts.T("Color").Get());
        frm.stack.Push(v);

        return null;
      },
      new FuncArgSymbol("k", ts.T("float"))
      );
      cl.OverloadBinaryOperator(op);
    }
    
    {
      var op = new FuncSymbolNative("+", ts.T("Color"),
      delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
      {
        var r = (Color)frm.stack.PopRelease().obj;
        var c = (Color)frm.stack.PopRelease().obj;

        var newc = new Color();
        newc.r = c.r + r.r;
        newc.g = c.g + r.g;

        var v = Val.NewObj(frm.vm, newc, ts.T("Color").Get());
        frm.stack.Push(v);

        return null;
      },
      new FuncArgSymbol("r", ts.T("Color"))
      );
      cl.OverloadBinaryOperator(op);
    }
    cl.Setup();

    var vm = MakeVM(bhl, ts);
    var res = (Color)Execute(vm, "test").result.PopRelease().obj;
    AssertEqual(21, res.r);
    AssertEqual(42, res.g);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestEqualityOverloadedForNativeClass()
  {
    string bhl = @"
      
    func test() 
    {
      Color c1 = {r:1,g:2}
      Color c2 = {r:1,g:2}
      Color c3 = {r:10,g:20}
      if(c1 == c2) {
        trace(""YES"")
      }
      if(c1 == c3) {
        trace(""NO"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    
    var cl = BindColor(ts, setup: false);
    var op = new FuncSymbolNative("==", ts.T("bool"),
      delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
      {
        var arg = (Color)frm.stack.PopRelease().obj;
        var c = (Color)frm.stack.PopRelease().obj;

        var v = Val.NewBool(frm.vm, c.r == arg.r && c.g == arg.g);
        frm.stack.Push(v);

        return null;
      },
      new FuncArgSymbol("arg", ts.T("Color"))
    );
    cl.OverloadBinaryOperator(op);
    cl.Setup();

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual(log.ToString(), "YES");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCustomOperatorOverloadTypeMismatchForNativeClass()
  {
    string bhl = @"
      
    func Color test() 
    {
      Color c1 = {r:1,g:2}
      return c1 * ""hey""
    }
    ";

    var ts = new Types();
    
    var cl = BindColor(ts);
    var op = new FuncSymbolNative("*", ts.T("Color"), null,
      new FuncArgSymbol("k", ts.T("float"))
    );
    cl.OverloadBinaryOperator(op);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "incompatible types"
    );
  }

  void BindEnum(Types ts)
  {
    var en = new EnumSymbol("EnumState");
    ts.ns.Define(en);

    en.Define(new EnumItemSymbol("SPAWNED",  10));
    en.Define(new EnumItemSymbol("SPAWNED2", 20));
  }

  [IsTested()]
  public void TestBindEnum()
  {
    string bhl = @"
      
    func int test() 
    {
      return (int)EnumState.SPAWNED + (int)EnumState.SPAWNED2
    }
    ";

    var ts = new Types();

    BindEnum(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 30);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastEnumToInt()
  {
    string bhl = @"
      
    func int test() 
    {
      return (int)EnumState.SPAWNED2
    }
    ";

    var ts = new Types();

    BindEnum(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 20);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastEnumToFloat()
  {
    string bhl = @"
      
    func float test() 
    {
      return (float)EnumState.SPAWNED
    }
    ";

    var ts = new Types();

    BindEnum(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 10);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastEnumToStr()
  {
    string bhl = @"
      
    func string test() 
    {
      return (string)EnumState.SPAWNED2
    }
    ";

    var ts = new Types();

    BindEnum(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test").result.PopRelease().str;
    AssertEqual(res, "20");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestEqEnum()
  {
    string bhl = @"
      
    func bool test(EnumState state) 
    {
      return state == EnumState.SPAWNED2
    }
    ";

    var ts = new Types();

    BindEnum(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 20)).result.PopRelease().num;
    AssertEqual(res, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNotEqEnum()
  {
    string bhl = @"
      
    func bool test(EnumState state) 
    {
      return state != EnumState.SPAWNED
    }
    ";

    var ts = new Types();

    BindEnum(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 20)).result.PopRelease().num;
    AssertEqual(res, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestEnumArray()
  {
    string bhl = @"
      
    func []EnumState test() 
    {
      []EnumState arr = new []EnumState
      arr.Add(EnumState.SPAWNED2)
      arr.Add(EnumState.SPAWNED)
      return arr
    }
    ";

    var ts = new Types();

    BindEnum(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test").result.Pop();
    var lst = res.obj as IList<Val>;
    AssertEqual(lst.Count, 2);
    AssertEqual(lst[0].num, 20);
    AssertEqual(lst[1].num, 10);
    res.Release();
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassEnumToNativeFunc()
  {
    string bhl = @"
      
    func bool test() 
    {
      return StateIs(state : EnumState.SPAWNED2)
    }
    ";

    var ts = new Types();

    BindEnum(ts);

    {
      var fn = new FuncSymbolNative("StateIs", ts.T("bool"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          var n = frm.stack.PopRelease().num;
          frm.stack.Push(Val.NewBool(frm.vm, n == 20));
          return null;
        },
        new FuncArgSymbol("state", ts.T("EnumState"))
        );

      ts.ns.Define(fn);
    }

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test").result.PopRelease().bval;
    AssertTrue(res);
  }

  [IsTested()]
  public void TestUserEnum()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
    }
      
    func int test() 
    {
      Foo f = Foo.B 
      return (int)f
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserEnumOrderIrrelevant()
  {
    string bhl = @"

    func int test() {
      return (int)foo()
    }

    func Foo foo() {
      return Foo.B
    }

    enum Foo
    {
      A = 1
      B = 2
    }
      
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserEnumIntCast()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
    }
      
    func int test() 
    {
      return (int)Foo.B + (int)Foo.A
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 3);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIntCastToUserEnum()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
    }
      
    func int test() 
    {
      Foo f = (Foo)2
      return (int)f
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserEnumWithDuplicateKey()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
      A = 10
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"duplicate key 'A'"
    );
  }

  [IsTested()]
  public void TestUserEnumWithDuplicateValue()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
      C = 1
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"duplicate value '1'"
    );
  }

  [IsTested()]
  public void TestUserEnumConflictsWithAnotherEnum()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
    }

    enum Foo
    {
      C = 3
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"already defined symbol 'Foo'"
    );
  }

  [IsTested()]
  public void TestUserEnumConflictsWithClass()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
    }

    class Foo
    {
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"already defined symbol 'Foo'"
    );
  }

  [IsTested()]
  public void TestUsingBultinTypeAsFunc()
  {
    string bhl = @"

    func float foo()
    {
      return 14
    }
      
    func int test() 
    {
      return int(foo())
    }
    ";

    var ts = new Types();
    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "symbol is not a function"
    );
  }

  [IsTested()]
  public void TestPassArgToFiber()
  {
    string bhl = @"
    func int test(int a)
    {
      yield()
      return a
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);

    var fb = vm.Start("test", Val.NewNum(vm, 123));

    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 123);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassArgsToFiber()
  {
    string bhl = @"
    func float test(float k, float m) 
    {
      yield()
      return m
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 3), Val.NewNum(vm, 7)).result.PopRelease().num; 
    AssertEqual(num, 7);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassArgsToFiber2()
  {
    string bhl = @"
    func float test(float k, float m) 
    {
      yield()
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test", Val.NewNum(vm, 3), Val.NewNum(vm, 7)).result.PopRelease().num; 
    AssertEqual(num, 3);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStartSeveralFibers()
  {
    string bhl = @"
    func int test()
    {
      yield()
      return 123
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);

    var fb1 = vm.Start("test");
    var fb2 = vm.Start("test");

    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(fb1.result.PopRelease().num, 123);
    AssertEqual(fb2.result.PopRelease().num, 123);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStartFiberFromScript()
  {
    string bhl = @"
    func test()
    {
      start(func() {
        yield()
        trace(""done"")
      })
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);

    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual("done", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStopFiberFromScript()
  {
    string bhl = @"
    func foo()
    {
      defer {
        trace(""4"")
      }
      trace(""1"")
      yield()
    }

    func test()
    {
      int fid = start(func() {
        defer {
          trace(""0"")
        }
        foo()
        trace(""2"")
      })

      yield()
      trace(""3"")
      stop(fid)
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);

    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual("1340", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDoubleStopFiberFromScript()
  {
    string bhl = @"
    func foo()
    {
      defer {
        trace(""4"")
      }
      trace(""1"")
      yield()
    }

    func test()
    {
      int fid = start(func() {
        defer {
          trace(""0"")
        }
        foo()
        trace(""2"")
      })

      yield()
      trace(""3"")
      stop(fid)
      stop(fid)
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);

    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual("1340", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
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

    func test()
    {
      int fid
      fid = start(func() {
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);

    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual("3140", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStopFiberExternallyWithProperDefers()
  {
    string bhl = @"
    func foo()
    {
      defer {
        trace(""4"")
      }
      trace(""1"")
      yield()
    }

    func test()
    {
      int fb = start(func() {
        defer {
          trace(""0"")
        }
        foo()
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);

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
    func foo()
    {
      paral {
        defer {
          trace(""2"")
        }
        suspend()
      }
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func test()
    {
      int fb = start(func() {
        foo()
      })

      defer {
        trace(""1"")
        stop(fb)
      }

      suspend()
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      },
      ts
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

  [IsTested()]
  public void TestFrameCache()
  {
    string bhl = @"
      
    func foo()
    {
      yield()
    }

    func test() 
    {
      foo()
    }
    ";

    var vm = MakeVM(bhl);

    {
      vm.Start("test");
      AssertEqual(1/*0 frame*/+1, vm.frames_pool.Allocs);
      AssertEqual(0, vm.frames_pool.Free);
      vm.Tick();
      AssertEqual(1/*0 frame*/+2, vm.frames_pool.Allocs);
      AssertEqual(0, vm.frames_pool.Free);
      vm.Tick();
      AssertEqual(1/*0 frame*/+2, vm.frames_pool.Allocs);
      AssertEqual(1/*0 frame*/+2, vm.frames_pool.Free);
    }

    //no new allocs
    {
      vm.Start("test");
      vm.Tick();
      vm.Tick();
      AssertEqual(1/*0 frame*/+2, vm.frames_pool.Allocs);
      AssertEqual(1/*0 frame*/+2, vm.frames_pool.Free);
    }
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFiberCache()
  {
    string bhl = @"
      
    func foo()
    {
      yield()
    }

    func test() 
    {
      foo()
    }
    ";

    var vm = MakeVM(bhl);
    {
      vm.Start("test");
      AssertEqual(1, vm.fibers_pool.Allocs);
      AssertEqual(0, vm.fibers_pool.Free);
      vm.Tick();
      vm.Start("test");
      AssertEqual(2, vm.fibers_pool.Allocs);
      AssertEqual(0, vm.fibers_pool.Free);
      vm.Tick();
      AssertEqual(2, vm.fibers_pool.Allocs);
      AssertEqual(1, vm.fibers_pool.Free);
      vm.Tick();
      AssertEqual(2, vm.fibers_pool.Allocs);
      AssertEqual(2, vm.fibers_pool.Free);
    }
    //no new allocs
    {
      vm.Start("test");
      vm.Tick();
      vm.Tick();
      AssertEqual(2, vm.fibers_pool.Allocs);
      AssertEqual(2, vm.fibers_pool.Free);
    }
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestRegisterModule()
  {
    string bhl = @"
    func int test()
    {
      return 123
    }
    ";

    var c = Compile(bhl);

    var vm = new VM();
    vm.LoadModule(c);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 123);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSimpleImport()
  {
    string bhl1 = @"
    import ""bhl2""  
    func float bhl1() 
    {
      return bhl2(23)
    }
    ";

    string bhl2 = @"
    import ""bhl3""  

    func float bhl2(float k)
    {
      return bhl3(k)
    }
    ";

    string bhl3 = @"
    func float bhl3(float k)
    {
      return k
    }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var ts = new Types();
    var loader = new ModuleLoader(ts, CompileFiles(files));

    AssertEqual(loader.Load("bhl1", ts, null), 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { 0 })
      .EmitThen(Opcodes.CallFunc, new int[] { 1, 1 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
    );
    AssertEqual(loader.Load("bhl2", ts, null), 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.ArgVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.CallFunc, new int[] { 0, 1 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
    );
    AssertEqual(loader.Load("bhl3", ts, null), 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.ArgVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
    );

    var vm = new VM(ts, loader);
    vm.LoadModule("bhl1");
    AssertEqual(Execute(vm, "bhl1").result.PopRelease().num, 23);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLoadModuleTwice()
  {
    string bhl1 = @"
    import ""bhl2""  
    func float bhl1() 
    {
      return bhl2(23)
    }
    ";

    string bhl2 = @"
    import ""bhl3""  

    func float bhl2(float k)
    {
      return bhl3(k)
    }
    ";

    string bhl3 = @"
    func float bhl3(float k)
    {
      return k
    }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var ts = new Types();
    var loader = new ModuleLoader(ts, CompileFiles(files));

    var vm = new VM(ts, loader);
    AssertTrue(vm.LoadModule("bhl1"));
    AssertEqual(Execute(vm, "bhl1").result.PopRelease().num, 23);

    AssertTrue(vm.LoadModule("bhl1"));
    AssertEqual(Execute(vm, "bhl1").result.PopRelease().num, 23);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLoadNonExistingModule()
  {
    string bhl1 = @"
    func float bhl1() 
    {
      return 42
    }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);

    var ts = new Types();
    var loader = new ModuleLoader(ts, CompileFiles(files));

    var vm = new VM(ts, loader);
    AssertFalse(vm.LoadModule("garbage"));
    AssertTrue(vm.LoadModule("bhl1"));
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBadImport()
  {
    string bhl1 = @"
    import ""garbage""  
    func float bhl1() 
    {
      return bhl2(23)
    }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);

    AssertError<Exception>(
      delegate() { 
        CompileFiles(files);
      },
     "invalid import"
    );
  }

  [IsTested()]
  public void TestImportEnum()
  {
    string bhl1 = @"
    import ""bhl2""  

    func float test() 
    {
      Foo f = Foo.B
      return bar(f)
    }
    ";

    string bhl2 = @"
    enum Foo
    {
      A = 2
      B = 3
    }

    func int bar(Foo f)
    {
      return (int)f * 10
    }
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );

    vm.LoadModule("bhl1");
    AssertEqual(30, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportEnumConflict()
  {
    string bhl1 = @"
    import ""bhl2""  

    enum Bar { 
      FOO = 1
    }

    func test() { }
    ";

    string bhl2 = @"
    enum Bar { 
      BAR = 2
    }
    ";

    AssertError<Exception>(
      delegate() { 
        MakeVM(new Dictionary<string, string>() {
            {"bhl1.bhl", bhl1},
            {"bhl2.bhl", bhl2},
          }
        );
      },
      @"already defined symbol 'Bar'"
    );
  }

  [IsTested()]
  public void TestImportReadWriteGlobalVar()
  {
    string bhl1 = @"
    import ""bhl2""  
    func float test() 
    {
      foo = 10
      return foo
    }
    ";

    string bhl2 = @"

    float foo = 1

    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );

    vm.LoadModule("bhl1");
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportReadWriteSeveralGlobalVars()
  {
    string bhl1 = @"
    import ""bhl2""  
    func float test() 
    {
      foo = 10
      return foo + boo
    }
    ";

    string bhl2 = @"

    float foo = 1
    float boo = 2

    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );

    vm.LoadModule("bhl1");
    AssertEqual(12, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportGlobalObjectVar()
  {
    string bhl1 = @"
    import ""bhl3""  
    func float test() 
    {
      return foo.x
    }
    ";

    string bhl2 = @"

    class Foo
    {
      float x
    }

    ";

    string bhl3 = @"
    import ""bhl2""  

    Foo foo = {x : 10}

    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      }
    );

    vm.LoadModule("bhl1");
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportGlobalObjectVarWithCycles()
  {
    string main_bhl = @"
    import ""g""  
    import ""input""  
    import ""unit""  

    func int test() {
      return unit_count()
    }
    ";

    string g_bhl = @"

    class G
    {
      float x
    }

    ";

    string input_bhl = @"
    import ""unit""  

    []int ins = []

    func int unit_count() {
      return ins.Count
    }

    ";

    string unit_bhl = @"
    import ""g""  
    import ""ability""

    func int garbage_func() {
      return 1
    }

    []int units = []

    ";

    string ability_bhl = @"
      import ""unit""

      func abliity_dummy() {}
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"main.bhl", main_bhl},
        {"g.bhl", g_bhl},
        {"input.bhl", input_bhl},
        {"ability.bhl", ability_bhl},
        {"unit.bhl", unit_bhl},
      }
    );

    vm.LoadModule("main");
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportGlobalVarConflict()
  {
    string bhl1 = @"
    import ""bhl2""  

    int foo = 10

    func test() { }
    ";

    string bhl2 = @"
    float foo = 100
    ";

    AssertError<Exception>(
      delegate() { 
        MakeVM(new Dictionary<string, string>() {
            {"bhl1.bhl", bhl1},
            {"bhl2.bhl", bhl2},
          }
        );
      },
      @"already defined symbol 'foo'"
    );
  }

  [IsTested()]
  public void TestImportMixed()
  {
    string bhl1 = @"
    import ""bhl3""
    func float what(float k)
    {
      return hey(k)
    }

    import ""bhl2""  
    func float test(float k) 
    {
      return bar(k) * what(k)
    }

    ";

    string bhl2 = @"
    func float bar(float k)
    {
      return k
    }
    ";

    string bhl3 = @"
    func float hey(float k)
    {
      return k
    }
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      }
    );

    vm.LoadModule("bhl1");
    AssertEqual(4, Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportWithCycles()
  {
    string bhl1 = @"
    import ""bhl2""  
    import ""bhl3""  

    func float test(float k) 
    {
      return bar(k)
    }
    ";

    string bhl2 = @"
    import ""bhl3""  

    func float bar(float k)
    {
      return hey(k)
    }
    ";

    string bhl3 = @"
    import ""bhl2""

    func float hey(float k)
    {
      return k
    }
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      }
    );

    vm.LoadModule("bhl1");
    AssertEqual(23, Execute(vm, "test", Val.NewNum(vm, 23)).result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportWithSemicolon()
  {
    string bhl1 = @"
    import ""bhl2"";;;import ""bhl3"";  

    func float test(float k) 
    {
      return bar(k)
    }
    ";

    string bhl2 = @"
    import ""bhl3""  

    func float bar(float k)
    {
      return hey(k)
    }
    ";

    string bhl3 = @"
    import ""bhl2""

    func float hey(float k)
    {
      return k
    }
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      }
    );

    vm.LoadModule("bhl1");
    AssertEqual(23, Execute(vm, "test", Val.NewNum(vm, 23)).result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportConflict()
  {
    string bhl1 = @"
    import ""bhl2""  

    func float bar() 
    {
      return 1
    }

    func float test() 
    {
      return bar()
    }
    ";

    string bhl2 = @"
    func float bar()
    {
      return 2
    }
    ";

    AssertError<Exception>(
      delegate() { 
        MakeVM(new Dictionary<string, string>() {
            {"bhl1.bhl", bhl1},
            {"bhl2.bhl", bhl2},
          }
        );
      },
      @"already defined symbol 'bar'"
    );
  }

  [IsTested()]
  public void TestImportInvalidateCachesAfterChange()
  {
    string file_unit = @"
      class Unit {
        int test
      }
      Unit u = {test: 23}
    ";

    string file_test = @"
    import ""garbage"";import ""unit"";  

    func int test() 
    {
      return u.test
    }
    ";

    string file_garbage = @"
      func garbage() {}
    ";

    CleanTestDir();

    var files = new List<string>();
    NewTestFile("unit.bhl", file_unit, ref files);
    NewTestFile("test.bhl", file_test, ref files);
    NewTestFile("garbage.bhl", file_garbage, ref files);

    {
      var ts = new Types();
      var loader = new ModuleLoader(ts, CompileFiles(files, ts, use_cache: true));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      AssertEqual(Execute(vm, "test").result.PopRelease().num, 23);
    }

    string new_file_unit = @"
      class Unit {
        string new_field
        int test
      }
      Unit u = {test: 32}
    ";
    files.RemoveAt(0);
    NewTestFile("unit.bhl", new_file_unit, ref files);
    System.IO.File.SetLastWriteTimeUtc(files[files.Count-1], DateTime.UtcNow.AddSeconds(1));

    {
      var ts = new Types();
      var loader = new ModuleLoader(ts, CompileFiles(files, ts, use_cache: true));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      AssertEqual(Execute(vm, "test").result.PopRelease().num, 32);
    }
  }

  [IsTested()]
  public void TestIncremetalBuildOfChangedFiles()
  {
    string file_unit = @"
      class Unit {
        int test
      }
      Unit u = {test: 23}
    ";

    string file_test = @"
    import ""garbage"";import ""unit"";  

    func int test() 
    {
      return u.test
    }
    ";

    string file_garbage = @"
      func garbage() {}
    ";

    CleanTestDir();

    var files = new List<string>();
    NewTestFile("unit.bhl", file_unit, ref files);
    NewTestFile("test.bhl", file_test, ref files);
    NewTestFile("garbage.bhl", file_garbage, ref files);

    {
      var ts = new Types();
      var loader = new ModuleLoader(ts, CompileFiles(files, ts, use_cache: true, max_threads: 3));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      AssertEqual(Execute(vm, "test").result.PopRelease().num, 23);
    }

    string new_file_unit = @"
      class Unit {
        string new_field
        int test
      }
      Unit u = {test: 32}
    ";
    files.RemoveAt(0);
    NewTestFile("unit.bhl", new_file_unit, ref files);
    System.IO.File.SetLastWriteTimeUtc(files[files.Count-1], DateTime.UtcNow.AddSeconds(1));

    {
      var ts = new Types();
      var loader = new ModuleLoader(ts, CompileFiles(files, ts, use_cache: true, max_threads: 3));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      AssertEqual(Execute(vm, "test").result.PopRelease().num, 32);
    }
  }

  [IsTested()]
  public void TestStartLambdaInScriptMgr()
  {
    string bhl = @"

    func test() 
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindStartScriptInMgr(ts);

    var vm = MakeVM(bhl, ts);
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

  [IsTested()]
  public void TestStartLambdaRunninInScriptMgr()
  {
    string bhl = @"

    func test() 
    {
      while(true) {
        StartScriptInMgr(
          script: func() { 
            trace(""HERE;"") 
            suspend()
          },
          spawns : 1
        )
        yield()
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindStartScriptInMgr(ts);

    var vm = MakeVM(bhl, ts);
    vm.Start("test");

    {
      AssertTrue(vm.Tick());
      ScriptMgr.instance.Tick();

      AssertEqual("HERE;", log.ToString());

      var cs = ScriptMgr.instance.active;
      AssertEqual(1, cs.Count); 
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

  [IsTested()]
  public void TestStartLambdaManyTimesInScriptMgr()
  {
    string bhl = @"

    func test() 
    {
      StartScriptInMgr(
        script: func() { 
          suspend()
        },
        spawns : 3
      )
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindStartScriptInMgr(ts);

    var vm = MakeVM(bhl, ts);
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

  [IsTested()]
  public void TestStartFuncPtrManyTimesInScriptMgr()
  {
    string bhl = @"

    func test() 
    {
      func() fn = func() {
        trace(""HERE;"")
        suspend()
      }

      StartScriptInMgr(
        script: fn,
        spawns : 2
      )
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindStartScriptInMgr(ts);

    var vm = MakeVM(bhl, ts);
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

  [IsTested()]
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindStartScriptInMgr(ts);

    {
      var fn = new FuncSymbolNative("say_here", Types.Void,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            log.Append("HERE;");
            return null;
          }
          );
      ts.ns.Define(fn);
    }

    var vm = MakeVM(bhl, ts);
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

  [IsTested()]
  public void TestStartLambdaVarManyTimesInScriptMgr()
  {
    string bhl = @"

    func test() 
    {
      func() fn = func() {
        trace(""HERE;"")
        suspend()
      }

      StartScriptInMgr(
        script: func() { 
          fn()
        },
        spawns : 2
      )
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindStartScriptInMgr(ts);

    var vm = MakeVM(bhl, ts);
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

  [IsTested()]
  public void TestStartLambdaManyTimesInScriptMgrWithUpVals()
  {
    string bhl = @"

    func test() 
    {
      float a = 0
      StartScriptInMgr(
        script: func() { 
          a = a + 1
          trace((string) a + "";"") 
          suspend()
        },
        spawns : 3
      )
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindStartScriptInMgr(ts);

    var vm = MakeVM(bhl, ts);
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

  [IsTested()]
  public void TestStartLambdaManyTimesInScriptMgrWithValCopies()
  {
    string bhl = @"

    func test() 
    {
      float a = 1
      StartScriptInMgr(
        script: func() { 
            func (float a) { 
              a = a + 1
              trace((string) a + "";"") 
              suspend()
          }(a)
        },
        spawns : 3
      )
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindStartScriptInMgr(ts);

    var vm = MakeVM(bhl, ts);
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

  [IsTested()]
  public void TestGetStackTrace()
  {
    string bhl3 = @"
    func float wow(float b)
    {
      record_callstack()
      return b
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    func float bar(float b)
    {
      return wow(b)
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func float foo(float k)
    {
      return bar(k)
    }

    func float test(float k) 
    {
      return foo(k)
    }
    ";

    var ts = new Types();
    var trace = new List<VM.TraceItem>();
    {
      var fn = new FuncSymbolNative("record_callstack", Types.Void,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.fb.GetStackTrace(trace); 
          return null;
        });
      ts.ns.Define(fn);
    }

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts
    );
    vm.LoadModule("bhl1");
    var fb = vm.Start("test", Val.NewNum(vm, 3));
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 3);

    AssertEqual(4, trace.Count);

    AssertEqual("wow", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(4, trace[0].line);

    AssertEqual("bar", trace[1].func);
    AssertEqual("bhl2.bhl", trace[1].file);
    AssertEqual(5, trace[1].line);

    AssertEqual("foo", trace[2].func);
    AssertEqual("bhl1.bhl", trace[2].file);
    AssertEqual(5, trace[2].line);

    AssertEqual("test", trace[3].func);
    AssertEqual("bhl1.bhl", trace[3].file);
    AssertEqual(10, trace[3].line);
  }

  [IsTested()]
  public void TestGetStackTraceFromFuncAsArg()
  {
    string bhl3 = @"
    func float wow(float b)
    {
      record_callstack()
      return b
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    func float bar(float b)
    {
      return wow(b)
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func float foo(float k)
    {
      return k
    }

    func float test(float k) 
    {
      return foo(
          bar(k)
      )
    }
    ";

    var ts = new Types();
    var trace = new List<VM.TraceItem>();
    {
      var fn = new FuncSymbolNative("record_callstack", Types.Void,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.fb.GetStackTrace(trace); 
          return null;
        });
      ts.ns.Define(fn);
    }

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts
    );
    vm.LoadModule("bhl1");
    var fb = vm.Start("test", Val.NewNum(vm, 3));
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 3);

    AssertEqual(3, trace.Count);

    AssertEqual("wow", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(4, trace[0].line);

    AssertEqual("bar", trace[1].func);
    AssertEqual("bhl2.bhl", trace[1].file);
    AssertEqual(5, trace[1].line);

    AssertEqual("test", trace[2].func);
    AssertEqual("bhl1.bhl", trace[2].file);
    AssertEqual(11, trace[2].line);
  }

  [IsTested()]
  public void TestGetStackTraceForUserObjNullRef()
  {
    string bhl2 = @"
    class Foo {
      float k
    }
    func float bar()
    {
      Foo f
      return f.k
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func float test() 
    {
      return bar()
    }
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );
    vm.LoadModule("bhl1");
    var fb = vm.Start("test");
    
    var info = new Dictionary<VM.Fiber, List<VM.TraceItem>>();

    try
    {
      vm.Tick();
    }
    catch(Exception)
    {
      vm.GetStackTrace(info);
    }

    AssertEqual(1, info.Count);

    var trace = info[fb];
    AssertEqual(2, trace.Count);

    AssertEqual("bar", trace[0].func);
    AssertEqual("bhl2.bhl", trace[0].file);
    AssertEqual(8, trace[0].line);

    AssertEqual("test", trace[1].func);
    AssertEqual("bhl1.bhl", trace[1].file);
    AssertEqual(5, trace[1].line);
  }

  [IsTested()]
  public void TestGetStackTraceAfterNativeException()
  {
    string bhl3 = @"
    func float wow(float b)
    {
      throw()
      return b
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    func float bar(float b)
    {
      return wow(b)
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func float foo(float k)
    {
      return bar(k)
    }

    func float test(float k) 
    {
      return foo(k)
    }
    ";

    var ts = new Types();
    var info = new Dictionary<VM.Fiber, List<VM.TraceItem>>();
    {
      var fn = new FuncSymbolNative("throw", Types.Void,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          //emulating null reference
          frm = null;
          frm.fb = null;
          return null;
        });
      ts.ns.Define(fn);
    }

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts
    );
    vm.LoadModule("bhl1");
    var fb = vm.Start("test", Val.NewNum(vm, 3));
    try
    {
      vm.Tick();
    }
    catch(Exception)
    {
      vm.GetStackTrace(info);
    }

    AssertEqual(1, info.Count);

    var trace = info[fb];
    AssertEqual(4, trace.Count);

    AssertEqual("wow", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(4, trace[0].line);

    AssertEqual("bar", trace[1].func);
    AssertEqual("bhl2.bhl", trace[1].file);
    AssertEqual(5, trace[1].line);

    AssertEqual("foo", trace[2].func);
    AssertEqual("bhl1.bhl", trace[2].file);
    AssertEqual(5, trace[2].line);

    AssertEqual("test", trace[3].func);
    AssertEqual("bhl1.bhl", trace[3].file);
    AssertEqual(10, trace[3].line);
  }

  [IsTested()]
  public void TestGetStackTraceFromParal()
  {
    string bhl3 = @"
    func float wow(float b)
    {
      paral {
        yield()
        record_callstack()
      }
      return b
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    func float bar(float b)
    {
      return wow(b)
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func float foo(float k)
    {
      return bar(k)
    }

    func float test(float k) 
    {
      return foo(k)
    }
    ";

    var ts = new Types();
    var trace = new List<VM.TraceItem>();
    {
      var fn = new FuncSymbolNative("record_callstack", Types.Void,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.fb.GetStackTrace(trace); 
          return null;
        });
      ts.ns.Define(fn);
    }

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts
    );
    vm.LoadModule("bhl1");
    var fb = vm.Start("test", Val.NewNum(vm, 3));
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 3);

    AssertEqual(4, trace.Count);

    AssertEqual("wow", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(6, trace[0].line);

    AssertEqual("bar", trace[1].func);
    AssertEqual("bhl2.bhl", trace[1].file);
    AssertEqual(5, trace[1].line);

    AssertEqual("foo", trace[2].func);
    AssertEqual("bhl1.bhl", trace[2].file);
    AssertEqual(5, trace[2].line);

    AssertEqual("test", trace[3].func);
    AssertEqual("bhl1.bhl", trace[3].file);
    AssertEqual(10, trace[3].line);
  }

  [IsTested()]
  public void TestGetStackTraceFromParalAll()
  {
    string bhl3 = @"
    func float wow(float b)
    {
      paral_all {
        yield()
        record_callstack()
      }
      return b
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    func float bar(float b)
    {
      return wow(b)
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func float foo(float k)
    {
      return bar(k)
    }

    func float test(float k) 
    {
      return foo(k)
    }
    ";

    var ts = new Types();
    var trace = new List<VM.TraceItem>();
    {
      var fn = new FuncSymbolNative("record_callstack", Types.Void,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.fb.GetStackTrace(trace); 
          return null;
        });
      ts.ns.Define(fn);
    }

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts
    );
    vm.LoadModule("bhl1");
    vm.Start("test", Val.NewNum(vm, 3));
    AssertTrue(vm.Tick());

    AssertEqual(4, trace.Count);

    AssertEqual("wow", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(6, trace[0].line);

    AssertEqual("bar", trace[1].func);
    AssertEqual("bhl2.bhl", trace[1].file);
    AssertEqual(5, trace[1].line);

    AssertEqual("foo", trace[2].func);
    AssertEqual("bhl1.bhl", trace[2].file);
    AssertEqual(5, trace[2].line);

    AssertEqual("test", trace[3].func);
    AssertEqual("bhl1.bhl", trace[3].file);
    AssertEqual(10, trace[3].line);
  }

  [IsTested()]
  public void TestGetStackTraceInDefer()
  {
    string bhl3 = @"
    func float wow(float b)
    {

      defer {
        record_callstack()
      }
      return b
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    func float bar(float b)
    {
      return wow(b)
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func float foo(float k)
    {
      return bar(k)
    }

    func float test(float k) 
    {
      return foo(k)
    }
    ";

    var ts = new Types();
    var trace = new List<VM.TraceItem>();
    {
      var fn = new FuncSymbolNative("record_callstack", Types.Void,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.fb.GetStackTrace(trace); 
          return null;
        });
      ts.ns.Define(fn);
    }

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts
    );
    vm.LoadModule("bhl1");
    vm.Start("test", Val.NewNum(vm, 3));
    AssertFalse(vm.Tick());

    AssertEqual(4, trace.Count);

    AssertEqual("wow", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(6, trace[0].line);

    AssertEqual("bar", trace[1].func);
    AssertEqual("bhl2.bhl", trace[1].file);
    AssertEqual(5, trace[1].line);

    AssertEqual("foo", trace[2].func);
    AssertEqual("bhl1.bhl", trace[2].file);
    AssertEqual(5, trace[2].line);

    AssertEqual("test", trace[3].func);
    AssertEqual("bhl1.bhl", trace[3].file);
    AssertEqual(10, trace[3].line);
  }

  [IsTested()]
  public void TestGetStackTraceInParalDefer()
  {
    string bhl3 = @"
    func hey()
    {
      record_callstack()
    }

    func wow(float b)
    {

      defer {
        hey()
      }
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    func bar(float b)
    {
      paral_all {
        wow(b)
      }
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func foo(float k)
    {
      bar(k)
    }

    func test() 
    {
      foo(14)
    }
    ";

    var ts = new Types();
    var trace = new List<VM.TraceItem>();
    {
      var fn = new FuncSymbolNative("record_callstack", Types.Void,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.fb.GetStackTrace(trace); 
          return null;
        });
      ts.ns.Define(fn);
    }

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts
    );
    vm.LoadModule("bhl1");
    vm.Start("test");
    AssertFalse(vm.Tick());

    AssertEqual(5, trace.Count);

    AssertEqual("hey", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(4, trace[0].line);

    AssertEqual("wow", trace[1].func);
    AssertEqual("bhl3.bhl", trace[1].file);
    AssertEqual(11, trace[1].line);

    AssertEqual("bar", trace[2].func);
    AssertEqual("bhl2.bhl", trace[2].file);
    AssertEqual(6, trace[2].line);

    AssertEqual("foo", trace[3].func);
    AssertEqual("bhl1.bhl", trace[3].file);
    AssertEqual(5, trace[3].line);

    AssertEqual("test", trace[4].func);
    AssertEqual("bhl1.bhl", trace[4].file);
    AssertEqual(10, trace[4].line);
  }

  [IsTested()]
  public void TestGetStackTraceInSubParal()
  {
    string bhl3 = @"
    func hey()
    {
      record_callstack()
    }

    func wow(float b)
    {

      paral {
        hey()
      }
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    func bar(float b)
    {
      paral_all {
        wow(b)
      }
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func foo(float k)
    {
      bar(k)
    }

    func test() 
    {
      foo(14)
    }
    ";

    var ts = new Types();
    var trace = new List<VM.TraceItem>();
    {
      var fn = new FuncSymbolNative("record_callstack", Types.Void,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.fb.GetStackTrace(trace); 
          return null;
        });
      ts.ns.Define(fn);
    }

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts
    );
    vm.LoadModule("bhl1");
    vm.Start("test");
    AssertFalse(vm.Tick());

    AssertEqual(5, trace.Count);

    AssertEqual("hey", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(4, trace[0].line);

    AssertEqual("wow", trace[1].func);
    AssertEqual("bhl3.bhl", trace[1].file);
    AssertEqual(11, trace[1].line);

    AssertEqual("bar", trace[2].func);
    AssertEqual("bhl2.bhl", trace[2].file);
    AssertEqual(6, trace[2].line);

    AssertEqual("foo", trace[3].func);
    AssertEqual("bhl1.bhl", trace[3].file);
    AssertEqual(5, trace[3].line);

    AssertEqual("test", trace[4].func);
    AssertEqual("bhl1.bhl", trace[4].file);
    AssertEqual(10, trace[4].line);
  }

  [IsTested()]
  public void TestGetStackTraceInLambda()
  {
    string bhl3 = @"
    func hey(func() cb)
    {
      cb()
    }

    func wow(func() cb)
    {

      paral {
        hey(cb)
      }
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    func bar(func() cb)
    {
      paral_all {
        wow(cb)
      }
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func foo(func() cb)
    {
      bar(cb)
    }

    func test() 
    {
      foo(func() {

            record_callstack()
          }
      )
    }
    ";

    var ts = new Types();
    var trace = new List<VM.TraceItem>();
    {
      var fn = new FuncSymbolNative("record_callstack", Types.Void,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.fb.GetStackTrace(trace); 
          return null;
        });
      ts.ns.Define(fn);
    }

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts
    );
    vm.LoadModule("bhl1");
    vm.Start("test");
    AssertFalse(vm.Tick());

    AssertEqual(6, trace.Count);

    AssertEqual("?", trace[0].func);
    AssertEqual("bhl1.bhl", trace[0].file);
    AssertEqual(12, trace[0].line);

    AssertEqual("hey", trace[1].func);
    AssertEqual("bhl3.bhl", trace[1].file);
    AssertEqual(4, trace[1].line);

    AssertEqual("wow", trace[2].func);
    AssertEqual("bhl3.bhl", trace[2].file);
    AssertEqual(11, trace[2].line);

    AssertEqual("bar", trace[3].func);
    AssertEqual("bhl2.bhl", trace[3].file);
    AssertEqual(6, trace[3].line);

    AssertEqual("foo", trace[4].func);
    AssertEqual("bhl1.bhl", trace[4].file);
    AssertEqual(5, trace[4].line);

    AssertEqual("test", trace[5].func);
    AssertEqual("bhl1.bhl", trace[5].file);
    AssertEqual(10, trace[5].line);
  }

  [IsTested()]
  public void TestSimpleGlobalVariableDecl()
  {
    string bhl = @"

    float foo
      
    func float test() 
    {
      return foo
    }
    ";

    var ts = new Types();
    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.DeclVar, new int[] { 0, ConstIdx(c, ts.T("float")) })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.GetGVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestGlobalVariableWriteRead()
  {
    string bhl = @"

    float foo = 10
      
    func float test() 
    {
      foo = 20
      return foo
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
      .EmitThen(Opcodes.SetGVar, new int[] { 0 })
      .EmitThen(Opcodes.GetGVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    AssertEqual(20, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestGlobalVariableAssignConst()
  {
    string bhl = @"

    float foo = 10
      
    func float test() 
    {
      return foo
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.GetGVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestGlobalVariableAssignAndReadObject()
  {
    string bhl = @"

    class Foo { 
      float b
    }

    Foo foo = {b : 100}
      
    func float test() 
    {
      return foo.b
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(100, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestGlobalVariableAssignAndReadArray()
  {
    string bhl = @"

    class Foo { 
      float b
    }

    []Foo foos = [{b : 100}, {b: 200}]
      
    func float test() 
    {
      return foos[1].b
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(200, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestGlobalVariableWrite()
  {
    string bhl = @"

    class Foo { 
      float b
    }

    Foo foo = {b : 100}

    func float bar()
    {
      return foo.b
    }
      
    func float test() 
    {
      foo.b = 101
      return bar()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(101, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestGlobalVariableAlreadyDeclared()
  {
    string bhl = @"

    int foo = 0
    int foo = 1
      
    func int test() 
    {
      return foo
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"already defined symbol 'foo'"
    );
  }

  [IsTested()]
  public void TestGlobalVariableSelfAssignError()
  {
    string bhl = @"

    int foo = foo
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"symbol 'foo' not resolved"
    );
  }

  [IsTested()]
  public void TestLocalVariableHasPriorityOverGlobalOne()
  {
    string bhl = @"

    class Foo { 
      float b
    }

    Foo foo = {b : 100}
      
    func float test() 
    {
      Foo foo = {b : 200}
      return foo.b
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(200, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestGlobalVarInLambda()
  {
    string bhl = @"

    class Foo { 
      float b
    }

    Foo foo = {b : 100}
      
    func float test() 
    {
      float a = 1
      return func float() {
        return foo.b + a
      }()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(101, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  //TODO: do we really need this?
  //[IsTested()]
  public void TestGlobalVariableInitWithSubCall()
  {
    string bhl = @"

    class Foo { 
      float b
    }

    func float bar(float f) {
      return f
    }

    Foo foo = {b : bar(100)}
      
    func float test() 
    {
      return foo.b
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(100, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestWeirdMix()
  {
    string bhl = @"

    func A(int b = 1)
    {
      trace(""A"" + (string)b)
      suspend()
    }

    func test() 
    {
      while(true) {
        int i = 0
        paral {
          A()
          {
            while(i < 1) {
              yield()
              i = i + 1
            }
          }
        }
        yield()
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    var fb = vm.Start("test");

    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    vm.Stop(fb);

    AssertEqual("A1A1", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFixedStack()
  {
    //Push/PopFast
    {
      var st = new FixedStack<int>(16); 
      st.Push(1);
      st.Push(10);

      AssertEqual(st.Count, 2);
      AssertEqual(1, st[0]);
      AssertEqual(10, st[1]);

      AssertEqual(10, st.Pop());
      AssertEqual(st.Count, 1);
      AssertEqual(1, st[0]);

      AssertEqual(1, st.Pop());
      AssertEqual(st.Count, 0);
    }
    
    //Push/Pop(repl)
    {
      var st = new FixedStack<int>(16); 
      st.Push(1);
      st.Push(10);

      AssertEqual(st.Count, 2);
      AssertEqual(1, st[0]);
      AssertEqual(10, st[1]);

      AssertEqual(10, st.Pop(0));
      AssertEqual(st.Count, 1);
      AssertEqual(1, st[0]);

      AssertEqual(1, st.Pop(0));
      AssertEqual(st.Count, 0);
    }

    //Push/Dec
    {
      var st = new FixedStack<int>(16); 
      st.Push(1);
      st.Push(10);

      AssertEqual(st.Count, 2);
      AssertEqual(1, st[0]);
      AssertEqual(10, st[1]);

      st.Dec();
      AssertEqual(st.Count, 1);
      AssertEqual(1, st[0]);

      st.Dec();
      AssertEqual(st.Count, 0);
    }

    //RemoveAt
    {
      var st = new FixedStack<int>(16); 
      st.Push(1);
      st.Push(2);
      st.Push(3);

      st.RemoveAt(1);
      AssertEqual(st.Count, 2);
      AssertEqual(1, st[0]);
      AssertEqual(3, st[1]);
    }

    //RemoveAt
    {
      var st = new FixedStack<int>(16); 
      st.Push(1);
      st.Push(2);
      st.Push(3);

      st.RemoveAt(0);
      AssertEqual(st.Count, 2);
      AssertEqual(2, st[0]);
      AssertEqual(3, st[1]);
    }

    //RemoveAt
    {
      var st = new FixedStack<int>(16); 
      st.Push(1);
      st.Push(2);
      st.Push(3);

      st.RemoveAt(2);
      AssertEqual(st.Count, 2);
      AssertEqual(1, st[0]);
      AssertEqual(2, st[1]);
    }
  }

  [IsTested()]
  public void TestValListOwnership()
  {
    var vm = new VM();

    var lst = ValList.New(vm);

    {
      var dv = Val.New(vm);
      lst.Add(dv);
      AssertEqual(dv._refs, 1);

      lst.Clear();
      AssertEqual(dv._refs, 1);
      dv.Release();
    }

    {
      var dv = Val.New(vm);
      lst.Add(dv);
      AssertEqual(dv._refs, 1);

      lst.RemoveAt(0);
      AssertEqual(dv._refs, 1);

      lst.Clear();
      AssertEqual(dv._refs, 1);
      dv.Release();
    }

    {
      var dv0 = Val.New(vm);
      var dv1 = Val.New(vm);
      lst.Add(dv0);
      lst.Add(dv1);
      AssertEqual(dv0._refs, 1);
      AssertEqual(dv1._refs, 1);

      lst.RemoveAt(1);
      AssertEqual(dv0._refs, 1);
      AssertEqual(dv1._refs, 1);

      lst.Clear();
      AssertEqual(dv0._refs, 1);
      AssertEqual(dv1._refs, 1);
      dv0.Release();
      dv1.Release();
    }

    lst.Release();

    CommonChecks(vm);
  }

  [IsTested()]
  public void TestRefCountSimple()
  {
    string bhl = @"
    func test() 
    {
      RefC r = new RefC
    }
    ";

    var ts = new Types();

    var logs = new StringBuilder();
    BindRefC(ts, logs);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("INC1;INC2;DEC1;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestRefCountReturnResult()
  {
    string bhl = @"
    func RefC test() 
    {
      return new RefC
    }
    ";

    var ts = new Types();

    var logs = new StringBuilder();
    BindRefC(ts, logs);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test").result.PopRelease();
    AssertEqual("INC1;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestRefCountAssignSame()
  {
    string bhl = @"
    func test() 
    {
      RefC r1 = new RefC
      RefC r2 = r1
      r2 = r1
    }
    ";

    var ts = new Types();

    var logs = new StringBuilder();
    BindRefC(ts, logs);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("INC1;INC2;DEC1;INC2;INC3;DEC2;INC3;INC4;DEC3;DEC2;DEC1;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestRefCountAssignSelf()
  {
    string bhl = @"
    func test() 
    {
      RefC r1 = new RefC
      r1 = r1
    }
    ";

    var ts = new Types();

    var logs = new StringBuilder();
    BindRefC(ts, logs);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("INC1;INC2;DEC1;INC2;INC3;DEC2;INC3;DEC2;DEC1;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestRefCountAssignOverwrite()
  {
    string bhl = @"
    func test() 
    {
      RefC r1 = new RefC
      RefC r2 = new RefC
      r1 = r2
    }
    ";

    var ts = new Types();

    var logs = new StringBuilder();
    BindRefC(ts, logs);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("INC1;INC2;DEC1;INC1;INC2;DEC1;INC2;INC3;DEC0;DEC2;DEC1;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestRefCountSeveral()
  {
    string bhl = @"
    func test() 
    {
      RefC r1 = new RefC
      RefC r2 = new RefC
    }
    ";

    var ts = new Types();

    var logs = new StringBuilder();
    BindRefC(ts, logs);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("INC1;INC2;DEC1;INC1;INC2;DEC1;DEC0;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestRefCountInLambda()
  {
    string bhl = @"
    func test() 
    {
      RefC r1 = new RefC

      trace(""REFS"" + (string)r1.refs + "";"")

      func() fn = func() {
        trace(""REFS"" + (string)r1.refs + "";"")
      }
      
      fn()
      trace(""REFS"" + (string)r1.refs + "";"")
    }
    ";

    var ts = new Types();

    var logs = new StringBuilder();
    BindRefC(ts, logs);
    BindTrace(ts, logs);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("INC1;INC2;DEC1;INC2;DEC1;REFS2;INC2;INC3;INC4;DEC3;REFS4;DEC2;INC3;DEC2;REFS3;DEC1;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestRefCountInArray()
  {
    string bhl = @"
    func test() 
    {
      []RefC rs = new []RefC
      rs.Add(new RefC)
    }
    ";

    var ts = new Types();

    var logs = new StringBuilder();
    BindRefC(ts, logs);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("INC1;INC2;DEC1;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestRefCountSeveralInArrayAccess()
  {
    string bhl = @"
    func test() 
    {
      []RefC rs = new []RefC
      rs.Add(new RefC)
      rs.Add(new RefC)
      float refs = rs[1].refs
    }
    ";

    var ts = new Types();

    var logs = new StringBuilder();
    BindRefC(ts, logs);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("INC1;INC2;DEC1;INC1;INC2;DEC1;INC2;DEC1;DEC0;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestRefCountReturn()
  {
    string bhl = @"

    func RefC make()
    {
      RefC c = new RefC
      return c
    }

    func test() 
    {
      RefC c1 = make()
    }
    ";

    var ts = new Types();

    var logs = new StringBuilder();
    BindRefC(ts, logs);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("INC1;INC2;DEC1;INC2;DEC1;INC2;DEC1;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestRefCountPass()
  {
    string bhl = @"

    func foo(RefC c)
    { 
      trace(""HERE;"")
    }

    func test() 
    {
      foo(new RefC)
    }
    ";

    var ts = new Types();

    var logs = new StringBuilder();
    BindRefC(ts, logs);
    BindTrace(ts, logs);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("INC1;INC2;DEC1;HERE;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestRefCountReturnPass()
  {
    string bhl = @"

    func RefC make()
    {
      RefC c = new RefC
      return c
    }

    func foo(RefC c)
    {
      RefC c2 = c
    }

    func test() 
    {
      RefC c1 = make()
      foo(c1)
    }
    ";

    var ts = new Types();

    var logs = new StringBuilder();
    BindRefC(ts, logs);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("INC1;INC2;DEC1;INC2;DEC1;INC2;DEC1;INC2;INC3;DEC2;INC3;INC4;DEC3;DEC2;DEC1;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSerializeModuleSymbols()
  {
    var s = new MemoryStream();
    {
      var ts = new Types();

      var ns = new Namespace(null, new VarIndex());
      ns.Link(ts.ns);

      ns.Define(new VariableSymbol("foo", Types.Int));

      ns.Define(new VariableSymbol("bar", Types.String));

      ns.Define(new VariableSymbol("wow", ns.TArr(Types.Bool)));

      ns.Define(new FuncSymbolScript(null, new FuncSignature(ns.T(Types.Int,Types.Float), ns.TRef(Types.Int), Types.String), "Test", 1, 155));

      ns.Define(new FuncSymbolScript(null, new FuncSignature(ns.TArr(Types.String), ns.T("Bar")), "Make", 3, 15));

      var Foo = new ClassSymbolScript("Foo");
      Foo.Define(new FieldSymbolScript("Int", Types.Int));
      Foo.Define(new FuncSymbolScript(null, new FuncSignature(Types.Void), "Hey", 0, 3));
      ns.Define(Foo);
      var Bar = new ClassSymbolScript("Bar", Foo);
      Bar.Define(new FieldSymbolScript("Float", Types.Float));
      Bar.Define(new FuncSymbolScript(null, new FuncSignature(ns.T(Types.Bool,Types.Bool), Types.Int), "What", 1, 1));
      ns.Define(Bar);

      var Enum = new EnumSymbolScript("Enum");
      Enum.TryAddItem("Type1", 1);
      Enum.TryAddItem("Type2", 2);
      ns.Define(Enum);

      Marshall.Obj2Stream(ns, s);
    }

    {
      var ts = new Types();

      var ns = new Namespace();
      ns.Link(ts.ns);

      var factory = new SymbolFactory(ts, ns);

      s.Position = 0;
      Marshall.Stream2Obj(s, ns, factory);

      ns.SetupSymbols();

      AssertEqual(8 + ts.ns.members.Count, ns.GetSymbolsEnumerator().Count);
      AssertEqual(8, ns.members.Count);

      var foo = (VariableSymbol)ns.Resolve("foo");
      AssertEqual(foo.name, "foo");
      AssertEqual(foo.type.Get(), Types.Int);
      AssertEqual(foo.scope, ns);
      AssertEqual(foo.scope_idx, 0);

      var bar = (VariableSymbol)ns.Resolve("bar");
      AssertEqual(bar.name, "bar");
      AssertEqual(bar.type.Get(), Types.String);
      AssertEqual(bar.scope, ns);
      AssertEqual(bar.scope_idx, 1);

      var wow = (VariableSymbol)ns.Resolve("wow");
      AssertEqual(wow.name, "wow");
      AssertEqual(wow.type.Get().GetName(), ns.TArr(Types.Bool).Get().GetName());
      AssertEqual(((GenericArrayTypeSymbol)wow.type.Get()).item_type.Get(), Types.Bool);
      AssertEqual(wow.scope, ns);
      AssertEqual(wow.scope_idx, 2);

      var Test = (FuncSymbolScript)ns.Resolve("Test");
      AssertEqual(Test.name, "Test");
      AssertEqual(Test.scope, ns);
      AssertEqual(ns.TFunc(ns.T(Types.Int, Types.Float), ns.TRef(Types.Int), Types.String).path, Test.signature.GetName());
      AssertEqual(1, Test.default_args_num);
      AssertEqual(0, Test.local_vars_num);
      AssertEqual(155, Test.ip_addr);
      AssertEqual(3, Test.scope_idx);

      var Make = (FuncSymbolScript)ns.Resolve("Make");
      AssertEqual(Make.name, "Make");
      AssertEqual(Make.scope, ns);
      AssertEqual(1, Make.signature.arg_types.Count);
      AssertEqual(ns.TArr(Types.String).Get().GetName(), Make.GetReturnType().GetName());
      AssertEqual(ns.T("Bar").Get(), Make.signature.arg_types[0].Get());
      AssertEqual(3, Make.default_args_num);
      AssertEqual(0, Make.local_vars_num);
      AssertEqual(15, Make.ip_addr);
      AssertEqual(4, Make.scope_idx);

      var Foo = (ClassSymbolScript)ns.Resolve("Foo");
      AssertEqual(Foo.scope, ns);
      AssertTrue(Foo.super_class == null);
      AssertEqual(Foo.name, "Foo");
      AssertEqual(Foo.GetSymbolsEnumerator().Count, 2);
      var Foo_Int = Foo.Resolve("Int") as FieldSymbolScript;
      AssertEqual(Foo_Int.scope, Foo);
      AssertEqual(Foo_Int.name, "Int");
      AssertEqual(Foo_Int.type.Get(), Types.Int);
      AssertEqual(Foo_Int.scope_idx, 0);
      var Foo_Hey = Foo.Resolve("Hey") as FuncSymbolScript;
      AssertEqual(Foo_Hey.scope, Foo);
      AssertEqual(Foo_Hey.name, "Hey");
      AssertEqual(Foo_Hey.GetReturnType(), Types.Void);
      AssertEqual(0, Foo_Hey.default_args_num);
      AssertEqual(0, Foo_Hey.local_vars_num);
      AssertEqual(3, Foo_Hey.ip_addr);
      AssertEqual(1, Foo_Hey.scope_idx);

      var Bar = (ClassSymbolScript)ns.Resolve("Bar");
      AssertEqual(Bar.scope, ns);
      AssertEqual(Bar.super_class, Foo);
      AssertEqual(Bar.name, "Bar");
      AssertEqual(Bar.GetSymbolsEnumerator().Count, 2/*from parent*/+2);
      var Bar_Float = Bar.Resolve("Float") as FieldSymbolScript;
      AssertEqual(Bar_Float.scope, Bar);
      AssertEqual(Bar_Float.name, "Float");
      AssertEqual(Bar_Float.type.Get(), Types.Float);
      AssertEqual(Bar_Float.scope_idx, 2);
      var Bar_What = Bar.Resolve("What") as FuncSymbolScript;
      AssertEqual(Bar_What.name, "What");
      AssertEqual(Bar_What.GetReturnType().GetName(), ns.T(Types.Bool, Types.Bool).Get().GetName());
      AssertEqual(Bar_What.signature.arg_types[0].Get(), Types.Int);
      AssertEqual(1, Bar_What.default_args_num);
      AssertEqual(0, Bar_What.local_vars_num);
      AssertEqual(1, Bar_What.ip_addr);

      var Enum = (EnumSymbolScript)ns.Resolve("Enum");
      AssertEqual(Enum.scope, ns);
      AssertEqual(Enum.name, "Enum");
      AssertEqual(Enum.members.Count, 2);
      AssertEqual(((EnumItemSymbol)Enum.Resolve("Type1")).owner, Enum);
      AssertEqual(Enum.Resolve("Type1").scope, Enum);
      AssertEqual(((EnumItemSymbol)Enum.Resolve("Type1")).val, 1);
      AssertEqual(((EnumItemSymbol)Enum.Resolve("Type2")).owner, Enum);
      AssertEqual(Enum.Resolve("Type2").scope, Enum);
      AssertEqual(((EnumItemSymbol)Enum.Resolve("Type2")).val, 2);
    }
  }

  [IsTested()]
  public void TestFibonacci()
  {
    string bhl = @"

    func int fib(int x)
    {
      if(x == 0) {
        return 0
      } else {
        if(x == 1) {
          return 1
        } else {
          return fib(x - 1) + fib(x - 2)
        }
      }
    }

    func int test() 
    {
      int x = 15 
      return fib(x)
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    stopwatch.Stop();
    AssertEqual(fb.result.PopRelease().num, 610);
    Console.WriteLine("bhl vm fib ticks: {0}", stopwatch.ElapsedTicks);

    CommonChecks(vm);
  }

  public static int ArrAddIdx {
    get {
      var ts = new Types();
      var arr = (ClassSymbol)ts.TArr("int").Get();
      return ((IScopeIndexed)arr.Resolve("Add")).scope_idx;
    }
  }

  public static int ArrSetIdx {
    get {
      var ts = new Types();
      var arr = (ClassSymbol)ts.TArr("int").Get();
      return ((IScopeIndexed)arr.Resolve("SetAt")).scope_idx;
    }
  }

  public static int ArrRemoveIdx {
    get {
      var ts = new Types();
      var arr = (ClassSymbol)ts.TArr("int").Get();
      return ((IScopeIndexed)arr.Resolve("RemoveAt")).scope_idx;
    }
  }

  public static int ArrCountIdx {
    get {
      var ts = new Types();
      var arr = (ClassSymbol)ts.TArr("int").Get();
      return ((IScopeIndexed)arr.Resolve("Count")).scope_idx;
    }
  }

  void BindMin(Types ts)
  {
    var fn = new FuncSymbolNative("min", ts.T("float"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          var b = (float)frm.stack.PopRelease().num;
          var a = (float)frm.stack.PopRelease().num;
          frm.stack.Push(Val.NewFlt(frm.vm, a > b ? b : a)); 
          return null;
        },
        new FuncArgSymbol("a", ts.T("float")),
        new FuncArgSymbol("b", ts.T("float"))
    );
    ts.ns.Define(fn);
  }

  public struct IntStruct
  {
    public int n;

    public static void Decode(Val v, ref IntStruct dst)
    {
      dst.n = (int)v._num;
    }

    public static void Encode(Val v, IntStruct src, IType type)
    {
      v.type = type;
      v._num = src.n;
    }
  }

  void BindIntStruct(Types ts)
  {
    {
      var cl = new ClassSymbolNative("IntStruct", null,
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          var s = new IntStruct();
          IntStruct.Encode(v, s, type);
        }
      );

      ts.ns.Define(cl);

      cl.Define(new FieldSymbol("n", Types.Int,
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var s = new IntStruct();
          IntStruct.Decode(ctx, ref s);
          v.num = s.n;
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var s = new IntStruct();
          IntStruct.Decode(ctx, ref s);
          s.n = (int)v.num;
          IntStruct.Encode(ctx, s, ctx.type);
        }
      ));
      cl.Setup();
    }
  }

  public class StringClass
  {
    public string str;
  }

  void BindStringClass(Types ts)
  {
    {
      var cl = new ClassSymbolNative("StringClass", null,
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          v.SetObj(new StringClass(), type);
        }
      );

      ts.ns.Define(cl);

      cl.Define(new FieldSymbol("str", ts.T("string"),
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var c = (StringClass)ctx.obj;
          v.str = c.str;
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var c = (StringClass)ctx.obj;
          c.str = v.str; 
          ctx.SetObj(c, ctx.type);
        }
      ));
      cl.Setup();
    }
  }

  public struct MasterStruct
  {
    public StringClass child;
    public StringClass child2;
    public IntStruct child_struct;
    public IntStruct child_struct2;
  }

  void BindMasterStruct(Types ts)
  {
    BindStringClass(ts);
    BindIntStruct(ts);

    {
      var cl = new ClassSymbolNative("MasterStruct", null,
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          var o = new MasterStruct();
          o.child = new StringClass();
          o.child2 = new StringClass();
          v.SetObj(o, type);
        }
      );

      ts.ns.Define(cl);

      cl.Define(new FieldSymbol("child", ts.T("StringClass"),
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var c = (MasterStruct)ctx.obj;
          v.SetObj(c.child, fld.type.Get());
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var c = (MasterStruct)ctx.obj;
          c.child = (StringClass)v._obj; 
          ctx.SetObj(c, ctx.type);
        }
      ));

      cl.Define(new FieldSymbol("child2", ts.T("StringClass"),
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var c = (MasterStruct)ctx.obj;
          v.SetObj(c.child2, fld.type.Get());
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var c = (MasterStruct)ctx.obj;
          c.child2 = (StringClass)v.obj; 
          ctx.SetObj(c, ctx.type);
        }
      ));

      cl.Define(new FieldSymbol("child_struct", ts.T("IntStruct"),
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct.Encode(v, c.child_struct, fld.type.Get());
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct s = new IntStruct();
          IntStruct.Decode(v, ref s);
          c.child_struct = s;
          ctx.SetObj(c, ctx.type);
        }
      ));

      cl.Define(new FieldSymbol("child_struct2", ts.T("IntStruct"),
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct.Encode(v, c.child_struct2, fld.type.Get());
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct s = new IntStruct();
          IntStruct.Decode(v, ref s);
          c.child_struct2 = s;
          ctx.SetObj(c, ctx.type);
        }
      ));
      cl.Setup();

    }
  }

  class CoroutineWaitTicks : ICoroutine
  {
    int c;
    int ticks_ttl;

    public void Tick(VM.Frame frm, VM.ExecState exec, ref BHS status)
    {
      //first time
      if(c++ == 0)
        ticks_ttl = (int)frm.stack.PopRelease().num;

      if(ticks_ttl-- > 0)
      {
        status = BHS.RUNNING;
      }
    }

    public void Cleanup(VM.Frame frm, VM.ExecState exec)
    {
      c = 0;
      ticks_ttl = 0;
    }
  }

  FuncSymbolNative BindWaitTicks(Types ts, StringBuilder log)
  {
    var fn = new FuncSymbolNative("WaitTicks", Types.Void,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          return CoroutinePool.New<CoroutineWaitTicks>(frm.vm);
        }, 
        new FuncArgSymbol("ticks", Types.Int)
    );
    ts.ns.Define(fn);
    return fn;
  }

  public class RefC : IValRefcounted
  {
    public int refs;
    public StringBuilder logs;

    public RefC(StringBuilder logs)
    {
      ++refs;
      logs.Append("INC" + refs + ";");
      this.logs = logs;
    }

    public void Retain()
    {
      ++refs;
      logs.Append("INC" + refs + ";");
    }

    public void Release()
    {
      --refs;
      logs.Append("DEC" + refs + ";");
    }
  }

  void BindRefC(Types ts, StringBuilder logs)
  {
    {
      var cl = new ClassSymbolNative("RefC", null,
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          v.SetObj(new RefC(logs), type);
        }
      );
      {
        var vs = new bhl.FieldSymbol("refs", Types.Int,
          delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
          {
            v.num = ((RefC)ctx.obj).refs;
          },
          //read only property
          null
        );
        cl.Define(vs);
      }
      cl.Setup();
      ts.ns.Define(cl);
    }
  }

  void BindStartScriptInMgr(Types ts)
  {
    {
      var fn = new FuncSymbolNative("StartScriptInMgr", Types.Void,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) {
            int spawns = (int)frm.stack.PopRelease().num;
            var ptr = frm.stack.Pop();

            for(int i=0;i<spawns;++i)
              ScriptMgr.instance.Start(frm, (VM.FuncPtr)ptr.obj);

            ptr.Release();

            return null;
          },
        new FuncArgSymbol("script", ts.TFunc(Types.Void)),
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

    public void Start(VM.Frame origin, VM.FuncPtr ptr)
    {
      var fb = origin.vm.Start(ptr, origin);
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
