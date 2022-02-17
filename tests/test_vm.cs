using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using bhl;

public class BHL_TestVM : BHL_TestBase
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 123) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 2);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 2);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, false) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 2);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
      .EmitThen(Opcodes.UnaryNot)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 2);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, false) })
      .EmitThen(Opcodes.UnaryNot)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 2);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "Hello") })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 2);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "Hello");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBoolToIntTypeCast()
  {
    string bhl = @"
    func int test()
    {
      return (int)true
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, "int") })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 3);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIntToStringTypeCast()
  {
    string bhl = @"
    func string test()
    {
      return (string)7
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 7) })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 3);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "7");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastFloatToStr()
  {
    string bhl = @"
      
    func string test(float k) 
    {
      return (string)k
    }
    ";

    var vm = MakeVM(bhl);
    var str = Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().str;
    AssertEqual(str, "3");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastFloatToInt()
  {
    string bhl = @"

    func int test(float k) 
    {
      return (int)k
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test", Val.NewNum(vm, 3.9)).result.PopRelease().num;
    AssertEqual(res, 3);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastIntToAny()
  {
    string bhl = @"
      
    func any test() 
    {
      return (any)121
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 121);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastIntToAnyFuncArg()
  {
    string bhl = @"

    func int foo(any a)
    {
      return (int)a
    }
      
    func int test() 
    {
      return foo(121)
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 121);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastStrToAny()
  {
    string bhl = @"
      
    func any test() 
    {
      return (any)""foo""
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().str;
    AssertEqual(res, "foo");
    CommonChecks(vm);
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
  public void TestImplicitIntArgsCasNativeFunc()
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();

    {
      var fn = new FuncSymbolNative("func_with_def", ts.Type("float"), 1,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var b = args_info.IsDefaultArgUsed(0) ? 2 : frm.stack.PopRelease().num;
            var a = frm.stack.PopRelease().num;

            frm.stack.Push(Val.NewNum(frm.vm, a + b));

            return null;
          },
          new FuncArgSymbol("a", ts.Type("float")),
          new FuncArgSymbol("b", ts.Type("float"))
        );

      ts.globs.Define(fn);
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

    var ts = new TypeSystem();

    {
      var fn = new FuncSymbolNative("func_with_def", ts.Type("float"), 1,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var b = args_info.IsDefaultArgUsed(0) ? 2 : frm.stack.PopRelease().num;
            var a = frm.stack.PopRelease().num;

            frm.stack.Push(Val.NewNum(frm.vm, a + b));

            return null;
          },
          new FuncArgSymbol("a", ts.Type("float")),
          new FuncArgSymbol("b", ts.Type("float"))
        );

      ts.globs.Define(fn);
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

    var ts = new TypeSystem();

    {
      var fn = new FuncSymbolNative("func_with_def", ts.Type("float"), 1,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var a = args_info.IsDefaultArgUsed(0) ? 14 : frm.stack.PopRelease().num;

            frm.stack.Push(Val.NewNum(frm.vm, a));

            return null;
          },
          new FuncArgSymbol("a", ts.Type("float"))
        );

      ts.globs.Define(fn);
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

    var ts = new TypeSystem();

    {
      var fn = new FuncSymbolNative("func_with_def", ts.Type("float"), 2,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var b = args_info.IsDefaultArgUsed(1) ? 2 : frm.stack.PopRelease().num;
            var a = args_info.IsDefaultArgUsed(0) ? 10 : frm.stack.PopRelease().num;

            frm.stack.Push(Val.NewNum(frm.vm, a + b));

            return null;
          },
          new FuncArgSymbol("a", ts.Type("int")),
          new FuncArgSymbol("b", ts.Type("int"))
        );

      ts.globs.Define(fn);
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

    var ts = new TypeSystem();

    {
      var fn = new FuncSymbolNative("foo", ts.Type("float"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
            status = BHS.FAILURE; 
            return null;
          });
      ts.globs.Define(fn);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.DeclVar, new int[] { 0, ConstIdx(c, "float") })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 42) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
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

    var ts = new TypeSystem();
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "bar") })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 300) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 2 })
      .EmitThen(Opcodes.Return)
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

    var ts = new TypeSystem();
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
      string[] s = [""""]
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
      Color[] c = [{}]
      c[0].r,s = foo()
      return c[0].r,s
    }
    ";

    var ts = new TypeSystem();
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
      "symbol not resolved"
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

    var ts = new TypeSystem();

    {
      var fn = new FuncSymbolNative("func_mult", ts.TypeTuple("float", "string"),
          delegate(VM.Frame frm, FuncArgsInfo arg_info, ref BHS status)
          {
            frm.stack.Push(Val.NewStr(frm.vm, "foo"));
            frm.stack.Push(Val.NewNum(frm.vm, 42));
            return null;
          }
        );
      ts.globs.Define(fn);
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

    var ts = new TypeSystem();

    {
      var fn = new FuncSymbolNative("func_mult", ts.TypeTuple("float","string","int","float"),
          delegate(VM.Frame frm, FuncArgsInfo arg_info, ref BHS status)
          {
            frm.stack.Push(Val.NewNum(frm.vm, 42.5));
            frm.stack.Push(Val.NewNum(frm.vm, 12));
            frm.stack.Push(Val.NewStr(frm.vm, "foo"));
            frm.stack.Push(Val.NewNum(frm.vm, 104));
            return null;
          }
        );
      ts.globs.Define(fn);
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
  public void TestVarValueNonConsumed()
  {
    string bhl = @"

    func int test() 
    {
      float foo = 1
      foo
      return 2
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncPtrReturnNonConsumed()
  {
    string bhl = @"

    func int test() 
    {
      int^() ptr = func int() {
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
  public void TestFuncPtrArrReturnNonConsumed()
  {
    string bhl = @"

    func int test() 
    {
      int[]^() ptr = func int[]() {
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
  public void TestAttributeNonConsumed()
  {
    string bhl = @"

    func int test() 
    {
      Color c = new Color
      c.r
      return 2
    }
    ";

    var ts = new TypeSystem();

    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 2);
    CommonChecks(vm);
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, false) })
      .EmitThen(Opcodes.JumpPeekZ, new int[] { 5 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
      .EmitThen(Opcodes.And)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 3);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, false) })
      .EmitThen(Opcodes.JumpPeekNZ, new int[] { 5 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
      .EmitThen(Opcodes.Or)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 3);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.BitAnd)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 3);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 4) })
      .EmitThen(Opcodes.BitOr)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 3);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .EmitThen(Opcodes.Mod)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 3);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2.7) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .EmitThen(Opcodes.Mod)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 3);

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

    func void test() 
    {
      ().foo
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"mismatched input '(' expecting '}'"
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
      "mismatched input '(' expecting '}'"
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 123) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 2);

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
  public void TestLocalVarHiding2()
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

    func float test() 
    {
      return bar(100)
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 42);
    CommonChecks(vm);
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
      "symbol not resolved"
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.DeclVar, new int[] { 0, ConstIdx(c, "int") })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.DeclVar, new int[] { 0, ConstIdx(c, "float") })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.DeclVar, new int[] { 0, ConstIdx(c, "string")})
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.DeclVar, new int[] { 0, ConstIdx(c, "bool") })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { 1 })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { 1 })
      .EmitThen(Opcodes.Equal)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 2);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.UnaryNeg)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 2);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 3);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.Sub)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 3);

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

    AssertEqual(c.Constants.Count, 1+260);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 2);

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
      
    func void test() 
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
      
    func void test() 
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
      
    func void test() 
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
      
    func void test() 
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
  public void TestPostOpAssignExpIncompatibleTypesNotAllowed()
  {
    string bhl = @"
      
    func void test() 
    {
      int k
      float a
      k += a
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 30) })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Mul)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 4);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 500);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLocalScopeNotSupported()
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
  public void TestLocalScopeNotSupported2()
  {
    string bhl = @"

    func test() 
    {
      {
        int i = 1
        i = i + 1
      }
      {
        string i
        i = ""foo""
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
      "symbol not resolved"
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
      "symbol not resolved"
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "bar"), 0 })
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 9 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 123) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Call, new int[] { 0, 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 3);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 123);
    CommonChecks(vm);
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
  public void TestSimpleNativeFunc()
  {
    string bhl = @"
    func void test() 
    {
      trace(""foo"")
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();
    var fn = BindTrace(ts, log);

    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler(ts)
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "foo") })
      .EmitThen(Opcodes.CallNative, new int[] { ts.globs.GetMembers().IndexOf(fn), 1 })
      .EmitThen(Opcodes.Return)
    ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
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

    var ts = new TypeSystem();
    
    var fn = new FuncSymbolNative("answer42", ts.Type("int"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.stack.Push(Val.NewNum(frm.vm, 42));
          return null;
        } 
    );
    ts.globs.Define(fn);

    var vm = MakeVM(bhl, ts);
    var num = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(42, num);
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

    var ts = new TypeSystem();
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
    func void foo(int k, int k)
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
      int^() ptr = foo
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
      int^(int, int) ptr = foo
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

    func void test() 
    {
      void^(string) ptr = trace
      ptr(""Hey"")
    }
    ";

    var ts = new TypeSystem();
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
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
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 5);

    var vm = MakeVM(c);
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
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
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 6);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
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
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 8);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 20);
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
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
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 3);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 0);
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
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
      .EmitThen(Opcodes.Break, new int[] { 3 })
      .EmitThen(Opcodes.Jump, new int[] { -35 })
      //__//
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();

    var vm = MakeVM(bhl, ts);
    var fb = vm.Start("test");

    //NodeDump(node);
    
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
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
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 5);

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
  public void TestForeachLoop()
  {
    string bhl = @"
    func int test()
    {
      int[] arr = new int[]
      arr.Add(1)
      arr.Add(3)
      int accum = 0

      foreach(arr as int a) {
        accum = accum + a
      }

      return accum
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 3 + 2/*hidden vars*/ + 1/*cargs*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, "[]") }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.CallMethodNative, new int[] { ArrAddIdx, ConstIdx(c, "[]"), 1 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .EmitThen(Opcodes.CallMethodNative, new int[] { ArrAddIdx, ConstIdx(c, "[]"), 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .EmitThen(Opcodes.SetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetVar, new int[] { 3 }) //assign tmp arr
      .EmitThen(Opcodes.DeclVar, new int[] { 4, ConstIdx(c, "int") })//declare counter
      .EmitThen(Opcodes.DeclVar, new int[] { 2, ConstIdx(c, "int")  })//declare iterator
      .EmitThen(Opcodes.GetVar, new int[] { 4 })
      .EmitThen(Opcodes.GetVar, new int[] { 3 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "[]"), ArrCountIdx })
      .EmitThen(Opcodes.LT) //compare counter and tmp arr size
      .EmitThen(Opcodes.JumpZ, new int[] { 28 })
      //call arr idx method
      .EmitThen(Opcodes.GetVar, new int[] { 3 })
      .EmitThen(Opcodes.GetVar, new int[] { 4 })
      .EmitThen(Opcodes.CallMethodNative, new int[] { ArrAtIdx, ConstIdx(c, "[]"), 0 })
      .EmitThen(Opcodes.SetVar, new int[] { 2 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 }) //accum = accum + iterator var
      .EmitThen(Opcodes.GetVar, new int[] { 2 }) 
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.SetVar, new int[] { 1 })
      .EmitThen(Opcodes.Inc, new int[] { 4 }) //fast increment hidden counter
      .EmitThen(Opcodes.Jump, new int[] { -42 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
    ;

    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 4);
    CommonChecks(vm);
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
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
      .EmitThen(Opcodes.Break, new int[] { 12 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.SetVar, new int[] { 1 })
      .EmitThen(Opcodes.Jump, new int[] { -32 })
      //__//
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 4);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 9);
    CommonChecks(vm);
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
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
      .EmitThen(Opcodes.Continue, new int[] { 7 })
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
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 4);

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
      int[] arr = new int[]
      arr.Add(1)
      arr.Add(3)
      int accum = 0

      foreach(arr as int a) {
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
      int[] arr = new int[]
      arr.Add(1)
      arr.Add(3)
      int accum = 0

      foreach(arr as int a) {
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "first"), 0 })
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "second"), 9 })
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 18 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
      .EmitThen(Opcodes.JumpZ, new int[] { 13 })
      .EmitThen(Opcodes.Call, new int[] { 0, 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Jump, new int[] { 10 })
      .EmitThen(Opcodes.Call, new int[] { 9, 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 6);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "first"), 0 })
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "second"), 9 })
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 18 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
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
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 6);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "dummy"), 0 })
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 3 })
      .UseCode()
      //dummy
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Return)
      //test
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      //lambda
      .EmitThen(Opcodes.Lambda, new int[] { 9 })
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 123) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      .EmitThen(Opcodes.FuncPtrToTop, new int[] { 0 })
      .EmitThen(Opcodes.CallPtr, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 4);

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
      return func int[](int a) { 
        int[] ns = new int[]
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
      "accessing not an array type 'bool^(int)'"
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
      int^() a = func int() {
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
      int^() a = func int() {
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
      int^(int,int) a = func int(int c, int b) {
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
  public void TestLambdaCallAsVarInFalseCondition()
  {
    string bhl = @"
    func dummy() {
    }

    func int test()
    {
      int^() a = func int() {
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
      int^() a = func int() {
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "dummy"), 0 })
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 3 })
      .UseCode()
      //dummy
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Return)
      //test
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 123) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      //lambda
      .EmitThen(Opcodes.Lambda, new int[] { 7 })
      .EmitThen(Opcodes.InitFrame, new int[] { 1+1 /*args info*/})
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      .EmitThen(Opcodes.UseUpval, new int[] { 0, 0 })
      .EmitThen(Opcodes.FuncPtrToTop, new int[] { 0 })
      .EmitThen(Opcodes.CallPtr, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 3);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "dummy"), 0 })
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 3 })
      .UseCode()
      //dummy
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Return)
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
      .EmitThen(Opcodes.Return)
      .EmitThen(Opcodes.UseUpval, new int[] { 0, 1 })
      .EmitThen(Opcodes.UseUpval, new int[] { 1, 2 })
      .EmitThen(Opcodes.FuncPtrToTop, new int[] { 0 })
      .EmitThen(Opcodes.CallPtr, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 5);

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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "dummy"), 0 })
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 3 })
      .UseCode()
      //dummy
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Return)
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
      .EmitThen(Opcodes.Return)
      .EmitThen(Opcodes.UseUpval, new int[] { 0, 1 })
      .EmitThen(Opcodes.FuncPtrToTop, new int[] { 0 })
      .EmitThen(Opcodes.CallPtr, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      .EmitThen(Opcodes.FuncPtrToTop, new int[] { 0 })
      .EmitThen(Opcodes.CallPtr, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 4);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 321);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLambdaChangesSeveralVars()
  {
    string bhl = @"

    func foo(void^() fn) 
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
          void^() fn = func() {
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

    func foo(void^() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float a = 2
      float b = 10
      foo(func() { 
          void^() p = func() {
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

      void^() fn = func void^() (float a, int b) { 
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

    func void test() 
    {
      int[]^() ptr = func int[]() {
        return [1,2]
      }
      trace((string)ptr()[1])
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("2", log.ToString());
    CommonChecks(vm);
  }

  //TODO: think about alternative Go-alike types notation?
  //[IsTested()]
  public void TestFuncPtrReturningArrOfArrLambda()
  {
    string bhl = @"

    func test()
    {
      //TODO:
      //[]func()[]string ptr = [
      string[]^()[] ptr = [
        func string[] () { return [""a"",""b""] },
        func string[] () { return [""c"",""d""] }
      ]
      trace(ptr[1]()[0])
    }
    ";

    var ts = new TypeSystem();
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
      void^(string) ptr = func(string arg) {
        trace(arg)
        yield()
      }
      paral {
        ptr(""FOO"")
        ptr(""BAR"")
      }
    }
    ";

    var ts = new TypeSystem();
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
      bool^(int,string) ptr = foo
      return ptr(a, ""HEY"")
    }
    ";

    var ts = new TypeSystem();
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
      bool^(int,string) ptr = foo
      return ptr(a, ""HEY"") && ptr(a-1, ""BAR"")
    }
    ";

    var ts = new TypeSystem();
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
      int^(int) p = 
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
      int^(int) p = 
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
      int^(int) p = 
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
    func int foo(int^(int) p, int a)
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
      int^(int,string)[] ptrs = new int^(int,string)[]
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

    var ts = new TypeSystem();
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
    func bool^(int) foo()
    {
      return func bool(int a) { return a > 2 } 
    }

    func bool test(int a) 
    {
      bool^(int) ptr = foo()
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
    func bool^(int) foo()
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
      void^(int,string, ref  bool) ptr = foo
      bool res = false
      ptr(a, ""HEY"", ref res)
      return res
    }
    ";

    var ts = new TypeSystem();
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
      bool^(int,string) ptr = 
        func bool (int a, string k)
        {
          trace(k)
          return a > 2
        }
      return ptr(a, ""HEY"")
    }
    ";

    var ts = new TypeSystem();
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
      void^(int) ptr = 
        func void (int a)
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

    var ts = new TypeSystem();
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
      bool^(int) ptr = foo
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
    func void foo(int a) { }

    func void test() 
    {
      void^(float) ptr = foo
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
    func void foo(int a) { }

    func void test() 
    {
      void^(int) ptr = foo
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
    func void foo(int a, ref  float b) { }

    func void test() 
    {
      float b = 1
      void^(int, ref float) ptr = foo
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
    func void foo(int a, ref float b) { }

    func void test() 
    {
      float b = 1
      void^(int, float) ptr = foo
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
      void^(int,string,ref bool) ptr = foo
      bool res = false
      ptr(a, ""HEY"", ref res)
      return res
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();

    {
      var cl = new ClassSymbolNative("refbool", null,
        delegate(VM.Frame frm, ref Val v) 
        {}
      );
      ts.globs.Define(cl);
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
    func void foo(int a) { }

    func void test() 
    {
      void^(int) ptr = foo
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
    func void foo(int a, float b) { }

    func void test() 
    {
      void^(int, float) ptr = foo
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
    func void foo(int a) { }

    func void test() 
    {
      void^(int) ptr = foo
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

    func foo(void^() fn)
    {
      fn()
    }

    func void test() 
    {
      void^() fun = 
        func()
        { 
          trace(""HERE"")
        }             

      foo(fun)
    }
    ";

    var ts = new TypeSystem();
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

    func foo(void^() fn)
    {
      fn()
    }

    func hey()
    {
      float foo
      trace(""HERE"")
    }

    func void test() 
    {
      foo(hey)
    }
    ";

    var ts = new TypeSystem();
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
    func void test() 
    {
      void^() ptr = foo
      ptr()
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();

    {
      var fn = new FuncSymbolNative("foo", ts.Type("void"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS _)
          {
            log.Append("FOO");
            return null;
          }
          );
      ts.globs.Define(fn);
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
    func void test() 
    {
      void^() fun = 
        func()
        { 
          trace(""HERE"")
        }             

      fun()
      fun()
      fun()
    }
    ";

    var ts = new TypeSystem();
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
      void^() fn = func()
      {
        float b = time
        trace((string)b)
      }
      fn()
    }

    func void test() 
    {
      foo(10)
    }
    ";

    var ts = new TypeSystem();
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
    func void test() 
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
    func void test() 
    {
      start(native)
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();
    var fn = new FuncSymbolNative("native", ts.Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          log.Append("HERE");
          return null;
        } 
    );
    ts.globs.Define(fn);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("HERE", log.ToString());
    CommonChecks(vm);
  }

  class TraceAfterYield : ICoroutine
  {
    bool first_time = true;
    public StringBuilder log;

    public void Tick(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> frames, ref BHS status)
    {
      if(first_time)
      {
        status = BHS.RUNNING;
        first_time = false;
      }
      else
        log.Append("HERE");
    }

    public void Cleanup(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> frames)
    {
      first_time = true;
    }
  }

  [IsTested()]
  public void TestStartNativeStatefulFunc()
  {
    string bhl = @"
    func void test() 
    {
      start(yield_and_trace)
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();
    {
      var fn = new FuncSymbolNative("yield_and_trace", ts.Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          var inst = CoroutinePool.New<TraceAfterYield>(frm.vm);
          inst.log = log;
          return inst;
        } 
      );
      ts.globs.Define(fn);
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
    func void test() 
    {
      start(
        func()
        { 
          trace(""HERE"")
        }             
      ) 
    }

    func void test2() 
    {
      start(
        func()
        { 
          trace(""HERE2"")
        }             
      ) 
    }
    ";

    var ts = new TypeSystem();
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
    func void test() 
    {
      start(
        func()
        { 
          trace(""HERE"")
        }             
      ) 
    }
    ";

    var ts = new TypeSystem();
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
    func void foo() {
      trace(""FOO1"")
      yield()
      trace(""FOO2"")
    }

    func void test() {
      void^() fn = foo
      void^() fn2 = fn
      paral_all {
        start(fn2)
        start(fn2)
      }
    }
    ";

    var ts = new TypeSystem();
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
    func void foo() {
      trace(""FOO1"")
      yield()
      trace(""FOO2"")
    }
    ";

    string bhl1 = @"
    import ""bhl2""

    func void test() {
      void^() fn = foo
      void^() fn2 = fn
      paral_all {
        start(fn2)
        start(fn2)
      }
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();

    BindTrace(ts, log);

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);

    var importer = new ModuleImporter(CompileFiles(files, ts));

    var vm = new VM(ts, importer);
    vm.LoadModule("bhl1");
    Execute(vm, "test");
    AssertEqual("FOO1FOO1FOO2FOO2", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStartLambdaCaptureVars()
  {
    string bhl = @"
    func void test() 
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

    var ts = new TypeSystem();
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
    func void test() 
    {
      float a = 10
      float b = 20
      start(
        func()
        { 
          float k = a 

          void^() fn = func() 
          {
            trace((string)k + (string)b)
          }

          fn()
        }             
      ) 
    }
    ";

    var ts = new TypeSystem();
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
    func void test() 
    {
      float a = 10
      float b = 20
      start(
        func()
        { 
          float k = a 

          void^() fn = func() 
          {
            void^() fn = func() 
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

    var ts = new TypeSystem();
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

    func call(void^() fn, void^() fn2)
    {
      start(fn2)
    }

    func void foo(float a = 1, float b = 2)
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

    func void bar()
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

    func void test() 
    {
      foo(10, 20)
      bar()
      foo()
      bar()
    }
    ";

    var ts = new TypeSystem();
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
      //TODO: need more flexible types support for this:
      //func(float)func(float)
      
      return func float^(float) (float a) {
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "foo"), 0 })
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 40 })
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
      .EmitThen(Opcodes.Return)
      //test
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .EmitThen(Opcodes.Call, new int[] { 0, 130 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 5);

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

    func void foo(float b, float k)
    {
      float f = 3
      float a = b + k
    }
      
    func void test() 
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
      
    func void foo(float a)
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
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "foo"), 0 })
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 14 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.ArgRef, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Return)
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.ArgVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Call, new int[] { 0, 1 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
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

    var ts = new TypeSystem();

    {
      var fn = new FuncSymbolNative("func_with_ref", ts.Type("void"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var b = frm.stack.Pop();
            var a = frm.stack.PopRelease().num;

            b.num = a * 2;
            b.Release();
            return null;
          },
          new FuncArgSymbol("a", ts.Type("float")),
          new FuncArgSymbol("b", ts.Type("float"), true/*is ref*/)
        );

      ts.globs.Define(fn);
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

    func void test() 
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

    func void foo(ref float k = 10)
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

    func void test() 
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
      float[] fs = [1,10,20]

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
      Bar[] bs = [{f:1},{f:10},{f:20}]

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
      .UseInit()
      .EmitThen(Opcodes.ClassBegin, new int[] { ConstIdx(c, "Wow"), -1 })
      .EmitThen(Opcodes.ClassMember, new int[] { ConstIdx(c, "float"), ConstIdx(c, "c"), 0 })
      .EmitThen(Opcodes.ClassEnd)
      .EmitThen(Opcodes.ClassBegin, new int[] { ConstIdx(c, "Bar"), -1 })
      .EmitThen(Opcodes.ClassMember, new int[] { ConstIdx(c, "Wow"), ConstIdx(c, "w"), 0 })
      .EmitThen(Opcodes.ClassEnd)
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "foo"), 0 })
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 14 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.ArgRef, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Return)
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, "Bar") }) 
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, "Wow") }) 
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 4) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Wow"), 0 })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Bar"), 0 })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.RefAttr, new int[] { ConstIdx(c, "Bar"), 0 })
      .EmitThen(Opcodes.RefAttr, new int[] { ConstIdx(c, "Wow"), 0 })
      .EmitThen(Opcodes.Call, new int[] { 0, 1 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "Bar"), 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "Wow"), 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
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

    func change(ref int^() p)
    {
      p = _2
    }

    func int test() 
    {
      int^() ptr = _1
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
      int^() p
    }

    func int _5()
    {
      return 5
    }

    func int _10()
    {
      return 10
    }

    func foo(ref int^() p) 
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

    var ts = new TypeSystem();
    
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

    func foo(void^() fn) 
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

    func foo(void^() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float[] a = []
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

    func float[] make()
    {
      float[] fs = [42]
      return fs
    }

    func foo(ref float[] a) 
    {
      a = make()
    }
      
    func float test() 
    {
      float[] a
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

    func foo(ref float[] a) 
    {
      a = [42]
    }
      
    func float test() 
    {
      float[] a = []
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

    func foo(ref float[] a) 
    {
      a = [42]
      float[] b = a
    }
      
    func float test() 
    {
      float[] a = []
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

    func foo(int[] a)
    {
      a = [100]
    }
      
    func int test() 
    {
      int[] a = [1, 2]

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

    func foo(void^() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float[] a
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

    func foo(void^() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float[] a = []
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

    func foo(ref float^() a) 
    {
      a = bar
    }
      
    func float test() 
    {
      float^() a
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

    func foo(ref float^() a) 
    {
      a = bar
    }
      
    func float test() 
    {
      float^() a = func float() { return 45 } 
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

    func foo(void^() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float^() a
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

    func foo(void^() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float^() a = func float() { return 45 } 
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
    func int[] test()
    {
      int[] a = new int[]
      return a
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, "[]") }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 2);

    var vm = MakeVM(c);
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
      string[] a = new string[]
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
      string[] arr = new string[]
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
    func int[] mkarray()
    {
      int[] a = new int[]
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
    func int[] mkarray()
    {
      int[] arr = new int[]
      arr.Add(1)
      arr.Add(100)
      arr.RemoveAt(0)
      return arr
    }
      
    func int[] test() 
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
    func int[] mkarray()
    {
      int[] arr = new int[]
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

    func int[] mkarray()
    {
      int[] arr = new int[]
      arr.Add(1)
      arr.Add(100)
      return arr
    }
      
    func void test() 
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

    func int[] mkarray()
    {
      int[] arr = new int[]
      arr.Add(1)
      arr.Add(100)
      return arr
    }
      
    func void test() 
    {
      mkarray().Add(300)
    }
    ";

    var vm = MakeVM(bhl);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStringArrayAssign()
  {
    string bhl = @"
      
    func string[] test() 
    {
      string[] arr = new string[]
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
  public void TestArrayPassedToFuncIsChanged()
  {
    string bhl = @"
    func foo(int[] a)
    {
      a.Add(100)
    }
      
    func int test() 
    {
      int[] a = new int[]
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
    func foo(ref int[] a)
    {
      a.Add(100)
    }
      
    func int test() 
    {
      int[] a = new int[]
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
      string[] arr = new string[]
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
      string[] arr = new string[]
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

    func string[] make()
    {
      string[] arr = new string[]
      return arr
    }

    func add(string[] arr)
    {
      arr.Add(""foo"")
      arr.Add(""bar"")
    }
      
    func string[] test() 
    {
      string[] arr = make()
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

    func string[] make()
    {
      string[] arr = new string[]
      return arr
    }
      
    func test() 
    {
      while(true) {
        string[] arr = new string[]
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

    var ts = new TypeSystem();
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().str;
    AssertEqual(res, "3102030");
    CommonChecks(vm);
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.CallNative, new int[] { ts.globs.GetMembers().IndexOf("suspend"), 0 })
      .EmitThen(Opcodes.Return)
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
    func void test() 
    {
      WaitTicks(2)
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();
    var fn = BindWaitTicks(ts, log);

    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler(ts)
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .EmitThen(Opcodes.CallNative, new int[] { ts.globs.GetMembers().IndexOf(fn), 1 })
      .EmitThen(Opcodes.Return)
    ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.DeclVar, new int[] { 0, ConstIdx(c, "int") })
      .EmitThen(Opcodes.Block, new int[] { (int)EnumBlock.PARAL, 30})
        .EmitThen(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 8})
          .EmitThen(Opcodes.CallNative, new int[] { ts.globs.GetMembers().IndexOf("suspend"), 0 })
        .EmitThen(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 14})
          .EmitThen(Opcodes.CallNative, new int[] { ts.globs.GetMembers().IndexOf("yield"), 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
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

    var ts = new TypeSystem();
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.DeclVar, new int[] { 0, ConstIdx(c, "int") })
      .EmitThen(Opcodes.Block, new int[] { (int)EnumBlock.PARAL, 30})
        .EmitThen(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 8})
          .EmitThen(Opcodes.CallNative, new int[] { ts.globs.GetMembers().IndexOf("suspend"), 0})
        .EmitThen(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 14})
          .EmitThen(Opcodes.CallNative, new int[] { ts.globs.GetMembers().IndexOf("yield"), 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
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

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();
    var c = Compile(bhl, ts);

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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
    var log = new StringBuilder();

    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("BARFOO", log.ToString());
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();

    {
      var fn = new FuncSymbolNative("foo", ts.Type("int"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) {
            frm.stack.PopRelease();
            frm.stack.Push(Val.NewNum(frm.vm, 42));
            return null;
          },
          new FuncArgSymbol("b", ts.Type("bool"))
        );
      ts.globs.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("bar_fail", ts.Type("int"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) {
            frm.stack.PopRelease();
            status = BHS.FAILURE;
            return null;
          },
          new FuncArgSymbol("n", ts.Type("int"))
        );
      ts.globs.Define(fn);
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

    var ts = new TypeSystem();

    {
      var fn = new FuncSymbolNative("hey", ts.Type("void"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          { return null; },
          new FuncArgSymbol("s", ts.Type("string")),
          new FuncArgSymbol("i", ts.Type("int"))
        );
      ts.globs.Define(fn);
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
      void^(int,int) ptr
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    func int[] make_ints(int n, int k)
    {
      int[] res = []
      return res
    }

    func int do_fail()
    {
      fail()
      return 10
    }

    func void test() 
    {
      foo(func int[] () { return make_ints(2, do_fail()) } )
      trace(""HERE"")
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    {
      var fn = new FuncSymbolNative("foo", ts.Type("Foo"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) {
          var fn_ptr = frm.stack.Pop();
          frm.vm.Start((VM.FuncPtr)fn_ptr.obj, frm);
          fn_ptr.Release();
          return null;
        },
        new FuncArgSymbol("fn", ts.TypeFunc(ts.TypeArr("int")))
      );

      ts.globs.Define(fn);
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
      int^(int) p = foo
      Foo f = PassthruFoo({hey:1, colors:[{r:p(42)}]})
      trace((string)f.hey)
    }

    func void test() 
    {
      bar()
    }
    ";

    var ts = new TypeSystem();
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

    func void test() 
    {
      bar()
    }
    ";

    var ts = new TypeSystem();
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

    func void test() 
    {
      bar()
    }
    ";

    var ts = new TypeSystem();
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
      int^(int) p = foo
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

    func void test() 
    {
      bar()
    }
    ";

    var ts = new TypeSystem();
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

    func void test() 
    {
      bar()
    }
    ";

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    func void test() 
    {
      bar()
    }
    ";

    var ts = new TypeSystem();
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

    func void test() 
    {
      int^(int,int) p = ret_int
      paral {
        {
          foo(1, p(2, 1))
          suspend()
        }
        foo(10, p(20, 2))
      }
    }
    ";

    var ts = new TypeSystem();
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
      int^(int,int) ptr
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

    func void test() 
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

    var ts = new TypeSystem();
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

    func void test() 
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

    var ts = new TypeSystem();
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

    func void test() 
    {
      paral_all {
        foo(1, ret_int(val: 2, ticks: 1))
        foo(10, ret_int(val: 20, ticks: 2))
      }
    }
    ";

    var ts = new TypeSystem();
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

    public void Tick(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> ext_frames, ref BHS status)
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

    public void Cleanup(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> frames)
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

    func void test() 
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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    {
      var cl = new ClassSymbolNative("Bar", null,
        delegate(VM.Frame frm, ref Val v) 
        { 
          //fake object
          v.obj = null;
        }
      );
      ts.globs.Define(cl);

      {
        var m = new FuncSymbolNative("self", ts.Type("Bar"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) {
            var obj = frm.stack.PopRelease().obj;
            frm.stack.Push(Val.NewObj(frm.vm, obj));
            return null;
          }
        );
        cl.Define(m);
      }

      {
        var m = new FuncSymbolNative("ret_int", ts.Type("int"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            return CoroutinePool.New<Bar_ret_int>(frm.vm);
          },
          new FuncArgSymbol("val", ts.Type("int")),
          new FuncArgSymbol("ticks", ts.Type("int"))
        );
        cl.Define(m);
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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    var fn = BindTrace(ts, log);

    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 12})
        .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "bar") })
        .EmitThen(Opcodes.CallNative, new int[] { ts.globs.GetMembers().IndexOf(fn), 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "foo") })
      .EmitThen(Opcodes.CallNative, new int[] { ts.globs.GetMembers().IndexOf(fn), 1 })
      .EmitThen(Opcodes.Return)
      ;

    AssertEqual(c, expected);

    var vm = MakeVM(c);
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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("142", log.ToString());
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    func void bar(float k)
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var c = Compile(bhl, ts);

    var vm = MakeVM(c);
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("foofoo2foo1testtest2test1", log.ToString());
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var c = Compile(bhl, ts);

    var vm = MakeVM(c);
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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var c = Compile(bhl, ts);

    var vm = MakeVM(c);
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
      int^() af = func int() { return 500100 } //500100

      int^() bf = func int() { 
        int a = 2
        int b = 1

        int c = a > b ? 100500 : 500100

        return c //100500
      }

      int^() cf = func int() { 
        return true ? 100500 : 500100 //100500
      }

      return min(af()/*500100*/, 
        af() > bf()/*true*/ ? (false ? af() : cf()/*100500*/) : bf())
        /*100500*/ > af()/*500100*/ ? af() : cf()/*100500*/
    }

    func string test3(int v)
    {
      string^() af = func string() {
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

      for(j = 0, int k = 0, k++; j < 3; j++, k++) {
        trace((string)j)
        trace((string)k)
      }
    }
    ";

    var ts = new TypeSystem();
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
      int[] arr = [0, 1, 3, 4]
      int i = 0
      int j = arr[i++]
    }
    ";

    string bhl8 = @"
    func int test()
    {
      bool^(int) foo = func bool(int b) { return b > 1 }

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
      "operator ++ is not supported for string type"
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
      "symbol not resolved"
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
      "extraneous input '++' expecting ']'"
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
      int[] arr = [0, 1, 3, 4]
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
      "operator -- is not supported for string type"
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
      "extraneous input '--' expecting ']'"
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var c = Compile(bhl, ts);

    var vm = MakeVM(c);
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var c = Compile(bhl, ts);

    var vm = MakeVM(c);
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("10", log.ToString());
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var c = Compile(bhl, ts);

    var vm = MakeVM(c);
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var c = Compile(bhl, ts);

    var vm = MakeVM(c);
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("lmb1lmb2foohey", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestEmptyUserClass()
  {
    string bhl = @"

    class Foo { }
      
    func bool test() 
    {
      Foo f = new Foo
      return f != null
    }
    ";

    var ts = new TypeSystem();
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.ClassBegin, new int[] { ConstIdx(c, "Foo"), -1 })
      .EmitThen(Opcodes.ClassEnd)
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, "Foo") }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { 2 })
      .EmitThen(Opcodes.NotEqual)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassBindConflict()
  {
    string bhl = @"

    class Foo { }
      
    func void test() 
    {
      Foo f = {}
    }
    ";

    var ts = new TypeSystem();
    BindFoo(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "already defined symbol 'Foo'"
    );
  }

  [IsTested()]
  public void TestSeveralEmptyUserClasses()
  {
    string bhl = @"

    class Foo { }
    class Bar { }
      
    func bool test() 
    {
      Foo f = {}
      Bar b = {}
      return f != null && b != null
    }
    ";

    var vm = MakeVM(bhl);
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTypeidForBuiltinType()
  {
    string bhl = @"

    func int test() 
    {
      return typeid(int)
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, Hash.CRC28("int"));
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTypeidForBuiltinArrType()
  {
    string bhl = @"

    func int test() 
    {
      return typeid(int[])
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, Hash.CRC28("[]"));
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTypeidForUserClass()
  {
    string bhl = @"

    class Foo { }
      
    func int test() 
    {
      return typeid(Foo)
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(Execute(vm, "test").result.PopRelease().num, Hash.CRC28("Foo"));
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTypeidEqual()
  {
    string bhl = @"

    class Foo { }
      
    func bool test() 
    {
      return typeid(Foo) == typeid(Foo)
    }
    ";

    var vm = MakeVM(bhl);
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTypeidNotEqual()
  {
    string bhl = @"

    class Foo { }
    class Bar { }
      
    func bool test() 
    {
      return typeid(Foo) == typeid(Bar)
    }
    ";

    var vm = MakeVM(bhl);
    AssertFalse(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  //TODO:
  //[IsTested()]
  public void TestTypeidNotEqualArrType()
  {
    string bhl = @"

    func bool test() 
    {
      return typeid(int[]) == typeid(float[])
    }
    ";

    var vm = MakeVM(bhl);
    AssertFalse(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  //TODO:
  //[IsTested()]
  public void TestTypeidBadType()
  {
    string bhl = @"

    func int test() 
    {
      return typeid(Foo)
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"type 'Foo' not found"
    );
  }

  //TODO: do we really need it?
  //[IsTested()]
  public void TestTypeidIsEncodedInUserClassObj()
  {
    string bhl = @"

    class Foo { }
      
    func Foo test() 
    {
      return {}
    }
    ";

    var vm = MakeVM(bhl);
    var val = Execute(vm, "test").result.Pop();
    AssertEqual(val.num, Hash.CRC28("Foo"));
    val.Release();
    CommonChecks(vm);
  }

  //TODO:
  //[IsTested()]
  public void TestTypeidIsEncodedInUserClassInHierarchy()
  {
    string bhl = @"

    class Foo { }
    class Bar : Foo { }
      
    func Bar test() 
    {
      return {}
    }
    ";

    var vm = MakeVM(bhl);
    var val = Execute(vm, "test").result.Pop();
    AssertEqual(val.num, Hash.CRC28("Bar"));
    val.Release();
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassWithSimpleMembers()
  {
    string bhl = @"

    class Foo { 
      int Int
      float Flt
      string Str
    }
      
    func void test() 
    {
      Foo f = new Foo
      f.Int = 10
      f.Flt = 14.2
      f.Str = ""Hey""
      trace((string)f.Int + "";"" + (string)f.Flt + "";"" + f.Str)
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();
    var fn = BindTrace(ts, log);
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.ClassBegin, new int[] { ConstIdx(c, "Foo"), -1 })
      .EmitThen(Opcodes.ClassMember, new int[] { ConstIdx(c, "int"), ConstIdx(c, "Int"), 0 })
      .EmitThen(Opcodes.ClassMember, new int[] { ConstIdx(c, "float"), ConstIdx(c, "Flt"), 1 })
      .EmitThen(Opcodes.ClassMember, new int[] { ConstIdx(c, "string"), ConstIdx(c, "Str"), 2 })
      .EmitThen(Opcodes.ClassEnd)
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, "Foo") }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { ConstIdx(c, "Foo"), 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 14.2) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { ConstIdx(c, "Foo"), 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { ConstIdx(c, "Foo"), 2 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 0 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 1 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 2 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.CallNative, new int[] { ts.globs.GetMembers().IndexOf(fn), 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("10;14.2;Hey", log.ToString().Replace(',', '.')/*locale issues*/);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassMemberOperations()
  {
    string bhl = @"

    class Foo { 
      float b
    }
      
    func float test() 
    {
      Foo f = { b : 101 }
      f.b = f.b + 1
      return f.b
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(102, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserSeveralClassMembersOperations()
  {
    string bhl = @"

    class Foo { 
      float b
      int c
    }

     class Bar {
       float a
     }
      
    func float test() 
    {
      Foo f = { c : 2, b : 101.5 }
      Bar b = { a : 10 }
      f.b = f.b + f.c + b.a
      return f.b
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(113.5, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassDefaultInit()
  {
    string bhl = @"

    class Foo { 
      float b
      int c
      string s
    }
      
    func string test() 
    {
      Foo f = {}
      return (string)f.b + "";"" + (string)f.c + "";"" + f.s + "";""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("0;0;;", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassDefaultInitBool()
  {
    string bhl = @"

    class Foo { 
      bool c
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.c == false
    }
    ";

    var vm = MakeVM(bhl);
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassWithArr()
  {
    string bhl = @"

    class Foo { 
      int[] a
    }
      
    func int test() 
    {
      Foo f = {a : [10, 20]}
      return f.a[1]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(20, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassDefaultInitArr()
  {
    string bhl = @"

    class Foo { 
      int[] a
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.a == null
    }
    ";

    var vm = MakeVM(bhl);
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassArrayClear()
  {
    string bhl = @"

    class Foo { 
      int[] a
    }
      
    func int test() 
    {
      Foo f = {a : [10, 20]}
      f.a.Clear()
      f.a.Add(30)
      return f.a[0]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(30, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassWithFuncPtr()
  {
    string bhl = @"

    func int foo(int a)
    {
      return a + 1
    }

    class Foo { 
      int^(int) ptr
    }
      
    func int test() 
    {
      Foo f = {}
      f.ptr = foo
      return f.ptr(2)
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(3, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassDefaultInitFuncPtr()
  {
    string bhl = @"

    class Foo { 
      void^(int) ptr
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.ptr == null
    }
    ";

    var vm = MakeVM(bhl);
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassWithFuncPtrsArray()
  {
    string bhl = @"

    func int foo(int a)
    {
      return a + 1
    }

    func int foo2(int a)
    {
      return a + 10
    }

    class Foo { 
      int^(int)[] ptrs
    }
      
    func int test() 
    {
      Foo f = {ptrs: []}
      f.ptrs.Add(foo)
      f.ptrs.Add(foo2)
      return f.ptrs[1](2)
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(12, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassWithFuncPtrsArrayCleanArgsStack()
  {
    string bhl = @"

    func int foo(int a,int b)
    {
      return a + 1
    }

    func int foo2(int a,int b)
    {
      return a + 10
    }

    class Foo { 
      int^(int,int)[] ptrs
    }

    func int bar()
    {
      fail()
      return 1
    }
      
    func void test() 
    {
      Foo f = {ptrs: []}
      f.ptrs.Add(foo)
      f.ptrs.Add(foo2)
      f.ptrs[1](2, bar())
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(0, Execute(vm, "test").result.Count);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassDefaultInitEnum()
  {
    string bhl = @"

    enum Bar {
      NONE = 0
      FOO  = 1
    }

    class Foo { 
      Bar b
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.b == Bar::NONE
    }
    ";

    var vm = MakeVM(bhl);
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassWithAnotherUserClassMember()
  {
    string bhl = @"

    class Bar {
      int[] a
    }

    class Foo { 
      Bar b
    }
      
    func int test() 
    {
      Foo f = {b : { a : [10, 21] } }
      return f.b.a[1]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(21, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassDefaultInitStringConcat()
  {
    string bhl = @"

    class Foo { 
      string s1
      string s2
    }
      
    func string test() 
    {
      Foo f = {}
      return f.s1 + f.s2
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassAny()
  {
    string bhl = @"

    class Foo { }
      
    func bool test() 
    {
      Foo f = {}
      any foo = f
      return foo != null
    }
    ";

    var vm = MakeVM(bhl);
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassAnyCastBack()
  {
    string bhl = @"

    class Foo { int x }
      
    func int test() 
    {
      Foo f = {x : 10}
      any foo = f
      Foo f1 = (Foo)foo
      return f1.x
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassMethod()
  {
    string bhl = @"

    class Foo {
      
      int a

      func int getA() 
      {
        return this.a
      }
    }

    func int test()
    {
      Foo f = {}
      f.a = 10
      return f.getA()
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.ClassBegin, new int[] { ConstIdx(c, "Foo"), -1 })
      .EmitThen(Opcodes.ClassMember, new int[] { ConstIdx(c, "int"), ConstIdx(c, "a"), 0 })
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "getA"), 0 })
      .EmitThen(Opcodes.ClassEnd)
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 13 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1+1 /*args info*/})
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      .EmitThen(Opcodes.InitFrame, new int[] { 1+1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, "Foo") }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { ConstIdx(c, "Foo"), 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.CallMethod, new int[] { 1, ConstIdx(c, "Foo"), 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSeveralUserClassMethods()
  {
    string bhl = @"

    class Foo {
      
      int a
      int b

      func int getA()
      {
        return this.a
      }

      func int getB() 
      {
        return this.b
      }
    }

    func int test()
    {
      Foo f = {}
      f.a = 10
      f.b = 20
      return f.getA() + f.getB()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(30, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassMethodNamedLikeClass()
  {
    string bhl = @"

    class Foo {
      
      func int Foo()
      {
        return 2
      }
    }

    func int test()
    {
      Foo f = {}
      return f.Foo()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassMethodLocalVariableNotDeclared()
  {
    string bhl = @"
      class Foo {
        
        int a

        func int Summ()
        {
          a = 1
          return this.a + a
        }
      }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "symbol not resolved"
    );
  }

  [IsTested()]
  public void TestUserClassMethodLocalVarAndAttribute()
  {
    string bhl = @"
      class Foo {
        
        int a

        func int Foo()
        {
          int a = 1
          return this.a + a
        }
      }

      func int test()
      {
        Foo f = {}
        f.a = 10
        return f.Foo()
      }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(11, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserChildClassMethod()
  {
    string bhl = @"

    class Foo {
      
      int a
      int b

      func int getA() {
        return this.a
      }

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int c

      func int getC() {
        return this.c
      }
    }

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.c = 100
      return b.getA() + b.getB() + b.getC()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserSubChildClassMethod()
  {
    string bhl = @"

    class Base {
      int a

      func int getA() {
        return this.a
      }
    }

    class Foo : Base {
      
      int b

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int c

      func int getC() {
        return this.c
      }
    }

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.c = 100
      return b.getA() + b.getB() + b.getC()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserChildClassMethodAccessesParent()
  {
    string bhl = @"

    class Foo {
      
      int a
      int b

      func int getA() {
        return this.a
      }

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int c

      func int getC() {
        return this.c
      }

      func int getSumm() {
        return this.getC() + this.getB() + this.getA() 
      }
    }

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.c = 100
      return b.getSumm()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserSubChildClassMethodAccessesParent()
  {
    string bhl = @"
    class Base {
      int a

      func int getA() {
        return this.a
      }
    }

    class Foo : Base {
      
      int b

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int c

      func int getC() {
        return this.c
      }

      func int getSumm() {
        return this.getC() + this.getB() + this.getA() 
      }
    }

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.c = 100
      return b.getSumm()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserChildClassMethodAlreadyDefined()
  {
    string bhl = @"
    class Foo {
      int a
      func int getA() {
        return this.a
      }
    }

    class Bar : Foo {
      func int getA() {
        return this.a
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"already defined symbol 'getA'"
    );
  }

  [IsTested()]
  public void TestArrayOfUserClasses()
  {
    string bhl = @"

    class Foo { 
      float b
      int c
    }

    func float test() 
    {
      Foo[] fs = [{b:1, c:2}]
      fs.Add({b:10, c:20})

      return fs[0].b + fs[0].c + fs[1].b + fs[1].c
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(33, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassCast()
  {
    string bhl = @"

    class Foo { 
      float b
    }
      
    func float test() 
    {
      Foo f = { b : 101 }
      any a = f
      Foo f1 = (Foo)a
      return f1.b
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(101, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassInlineCast()
  {
    string bhl = @"

    class Foo { 
      float b
    }
      
    func float test() 
    {
      Foo f = { b : 101 }
      any a = f
      return ((Foo)a).b
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(101, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassInlineCastArr()
  {
    string bhl = @"

    class Foo { 
      int[] b
    }
      
    func int test() 
    {
      Foo f = { b : [101, 102] }
      any a = f
      return ((Foo)a).b[1]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(102, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassInlineCastArr2()
  {
    string bhl = @"

    class Foo { 
      int[] b
    }
      
    func int test() 
    {
      Foo f = { b : [101, 102] }
      any a = f
      return ((Foo)a).b.Count
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestChildUserClass()
  {
    string bhl = @"

    class Base {
      float x 
    }

    class Foo : Base { 
      float y
    }
      
    func float test() {
      Foo f = { x : 1, y : 2}
      return f.y + f.x
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(3, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestChildUserClassDowncast()
  {
    string bhl = @"

    class Base {
      float x 
    }

    class Foo : Base 
    { 
      float y
    }
      
    func float test() 
    {
      Foo f = { x : 1, y : 2}
      Base b = (Foo)f
      return b.x
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestChildUserClassUpcast()
  {
    string bhl = @"

    class Base {
      float x 
    }

    class Foo : Base 
    { 
      float y
    }
      
    func float test() 
    {
      Foo f = { x : 1, y : 2}
      Base b = (Foo)f
      Foo f2 = (Foo)b
      return f2.x + f2.y
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(3, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestChildUserClassDefaultInit()
  {
    string bhl = @"

    class Base {
      float x 
    }

    class Foo : Base 
    { 
      float y
    }
      
    func float test() 
    {
      Foo f = { y : 2}
      return f.x + f.y
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTmpUserClassReadDefaultField()
  {
    string bhl = @"

    class Foo { 
      int c
    }
      
    func int test() 
    {
      return (new Foo).c
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTmpUserClassReadField()
  {
    string bhl = @"

    class Foo { 
      int c
    }
      
    func int test() 
    {
      return (new Foo{c: 10}).c
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTmpUserClassWriteFieldNotAllowed()
  {
    string bhl = @"

    class Foo { 
      int c
    }
      
    func test() 
    {
      (new Foo{c: 10}).c = 20
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"mismatched input '(' expecting '}'"
    );
  }

  [IsTested()]
  public void TestChildUserClassAlreadyDefinedMember()
  {
    string bhl = @"

    class Base {
      float x 
    }

    class Foo : Base 
    { 
      int x
    }
      
    func float test() 
    {
      Foo f = { x : 2}
      return f.x
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "already defined symbol 'x'"
    );
  }

  [IsTested()]
  public void TestChildUserClassOfNativeClassIsForbidden()
  {
    string bhl = @"

    class ColorA : Color 
    { 
      float a
    }
    ";

    var ts = new TypeSystem();
    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "extending native classes is not supported"
    );
  }

  [IsTested()]
  public void TestUserClassNotAllowedToContainThisMember()
  {
    string bhl = @"
      class Foo {
        int this
      }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "the keyword \"this\" is reserved"
    );
  }

  [IsTested()]
  public void TestUserClassContainThisMethodNotAllowed()
  {
    string bhl = @"
      class Foo {

        func bool this()
        {
          return false
        }
      }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "the keyword \"this\" is reserved"
    );
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
      
    func void test() 
    {
      Foo f = {Int: 10, Flt: 14.2, Str: ""Hey""}
      trace((string)f.Int + "";"" + (string)f.Flt + "";"" + f.Str)
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();
    var fn = BindTrace(ts, log);
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.ClassBegin, new int[] { ConstIdx(c, "Foo"), -1 })
      .EmitThen(Opcodes.ClassMember, new int[] { ConstIdx(c, "int"), ConstIdx(c, "Int"), 0 })
      .EmitThen(Opcodes.ClassMember, new int[] { ConstIdx(c, "float"), ConstIdx(c, "Flt"), 1 })
      .EmitThen(Opcodes.ClassMember, new int[] { ConstIdx(c, "string"), ConstIdx(c, "Str"), 2 })
      .EmitThen(Opcodes.ClassEnd)
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, "Foo") }) 
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Foo"), 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 14.2) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Foo"), 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Foo"), 2 })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 0 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 1 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 2 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.CallNative, new int[] { ts.globs.GetMembers().IndexOf(fn), 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    AssertEqual(11, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonCtorNotExpectedMember()
  {
    string bhl = @"
    func void test()
    {
      Color c = {b: 10}
    }
    ";

    var ts = new TypeSystem();
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
    func void test()
    {
      Color c = {r: ""what""}
    }
    ";

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();
    
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

    var ts = new TypeSystem();
    
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

    var ts = new TypeSystem();
    
    BindColorAlpha(ts);

    var vm = MakeVM(bhl, ts);
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonExplicitSubClassNotCompatible()
  {
    string bhl = @"
      
    func void test() 
    {
      ColorAlpha c = new Color {g: 10, r: 100}
    }
    ";

    var ts = new TypeSystem();
    
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

    var ts = new TypeSystem();
    
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonExplicitNoSuchClass()
  {
    string bhl = @"
      
    func void test() 
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
      
    func void test() 
    {
      Foo[] fs = [{Int: 10, Flt: 14.2, Str: ""Hey""}]
      Foo f = fs[0]
      trace((string)f.Int + "";"" + (string)f.Flt + "";"" + f.Str)
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();
    var fn = BindTrace(ts, log);
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.ClassBegin, new int[] { ConstIdx(c, "Foo"), -1 })
      .EmitThen(Opcodes.ClassMember, new int[] { ConstIdx(c, "int"), ConstIdx(c, "Int"), 0 })
      .EmitThen(Opcodes.ClassMember, new int[] { ConstIdx(c, "float"), ConstIdx(c, "Flt"), 1 })
      .EmitThen(Opcodes.ClassMember, new int[] { ConstIdx(c, "string"), ConstIdx(c, "Str"), 2 })
      .EmitThen(Opcodes.ClassEnd)
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 2 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, "[]") }) 
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, "Foo") }) 
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Foo"), 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 14.2) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Foo"), 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Foo"), 2 })
      .EmitThen(Opcodes.CallMethodNative, new int[] { ArrAddInplaceIdx, ConstIdx(c, "[]"), 1 })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .EmitThen(Opcodes.CallMethodNative, new int[] { ArrAtIdx, ConstIdx(c, "[]"), 1 })
      .EmitThen(Opcodes.SetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 0 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 1 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 2 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.CallNative, new int[] { ts.globs.GetMembers().IndexOf(fn), 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
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
      
    func void test() 
    {
      Foo[] fs = [{Int: 10, Flt: 14.2, Str: ""Hey""},{Flt: 15.1, Int: 2, Str: ""Foo""}]
      trace((string)fs[1].Int + "";"" + (string)fs[1].Flt + "";"" + fs[1].Str + ""-"" + 
           (string)fs[0].Int + "";"" + (string)fs[0].Flt + "";"" + fs[0].Str)
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);
    var c = Compile(bhl, ts);

    var vm = MakeVM(c);
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
      int[] a = []
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
      int[] a = [1,2,3]
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
      Color[] cs = [{r:10, g:100}, {g:1000, r:1}]
      return cs[0].r + cs[0].g + cs[1].r + cs[1].g
    }
    ";

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();

    BindColor(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrDefaultArg()
  {
    string bhl = @"
    func float foo(Color[] c = [{r:10}])
    {
      return c[0].r
    }

    func float test()
    {
      return foo()
    }
    ";

    var ts = new TypeSystem();

    BindColor(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrEmptyDefaultArg()
  {
    string bhl = @"
    func int foo(Color[] c = [])
    {
      return c.Count
    }

    func int test()
    {
      return foo()
    }
    ";

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();

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
      float[] b = [1]
      b = [2,3]
      return b[0] + b[1]
    }
    ";

    var ts = new TypeSystem();

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
      Color[] cs = [new ColorAlpha {a:4}, {g:10}, new Color {r:100}]
      ColorAlpha ca = (ColorAlpha)cs[0]
      return ca.a + cs[1].g + cs[2].r
    }
    ";

    var ts = new TypeSystem();

    BindColorAlpha(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(114, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrayReturn()
  {
    string bhl = @"

    func Color[] make()
    {
      return [new ColorAlpha {a:4}, {g:10}, new Color {r:100}]
    }
      
    func float test() 
    {
      Color[] cs = make()
      ColorAlpha ca = (ColorAlpha)cs[0]
      return ca.a + cs[1].g + cs[2].r
    }
    ";

    var ts = new TypeSystem();

    BindColorAlpha(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(114, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrayExplicitAsArg()
  {
    string bhl = @"

    func float foo(Color[] cs)
    {
      ColorAlpha ca = (ColorAlpha)cs[0]
      return ca.a + cs[1].g + cs[2].r
    }
      
    func float test() 
    {
      return foo([new ColorAlpha {a:4}, {g:10}, new Color {r:100}])
    }
    ";

    var ts = new TypeSystem();

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
      Color[] cs = [{r:1,g:2}, new ColorAlpha {g: 10, r: 100, a:2}]
      ColorAlpha c = (ColorAlpha)cs[1]
      return c.r + c.g + c.a
    }
    ";

    var ts = new TypeSystem();

    BindColorAlpha(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(112, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrayExplicitSubClassNotCompatible()
  {
    string bhl = @"
      
    func void test() 
    {
      ColorAlpha[] c = [{r:1,g:2,a:100}, new Color {g: 10, r: 100}]
    }
    ";

    var ts = new TypeSystem();
    
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

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();

    BindMasterStruct(ts);
    var vm = MakeVM(bhl, ts);
    AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonFuncArg()
  {
    string bhl = @"
    func void test(float b) 
    {
      Foo f = PassthruFoo({hey:142, colors:[{r:2}, {g:3}, {g:b}]})
      trace((string)f.hey + (string)f.colors.Count + (string)f.colors[0].r + (string)f.colors[1].g + (string)f.colors[2].g)
    }
    ";

    var ts = new TypeSystem();

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
    func void test(float b) 
    {
      trace((string)PassthruFoo({hey:142, colors:[{r:2}, {g:3}, {g:b}]}).colors.Count)
    }
    ";

    var ts = new TypeSystem();

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
  public void TestBindNativeClass()
  {
    string bhl = @"

    func bool test() 
    {
      Bar b = new Bar
      return b != null
    }
    ";

    var ts = new TypeSystem();
    BindBar(ts);
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, "Bar") }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstNullIdx(c) })
      .EmitThen(Opcodes.NotEqual)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeClassWithSimpleMembers()
  {
    string bhl = @"

    func void test() 
    {
      Bar o = new Bar
      o.Int = 10
      o.Flt = 14.5
      o.Str = ""Hey""
      trace((string)o.Int + "";"" + (string)o.Flt + "";"" + o.Str)
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();
    var fn = BindTrace(ts, log);
    BindBar(ts);
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, "Bar") }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { ConstIdx(c, "Bar"), 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 14.5) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { ConstIdx(c, "Bar"), 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { ConstIdx(c, "Bar"), 2 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "Bar"), 0 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "Bar"), 1 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "Bar"), 2 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.CallNative, new int[] { ts.globs.GetMembers().IndexOf(fn), 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("10;14.5;Hey", log.ToString().Replace(',', '.')/*locale issues*/);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeClassAttributesAccess()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      Color c = new Color
      c.r = k*1
      c.g = k*100
      return c.r + c.g
    }
    ";

    var ts = new TypeSystem();
    
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 202);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeClassCallMember()
  {
    string bhl = @"

    func float test(float a)
    {
      return mkcolor(a).r
    }
    ";

    var ts = new TypeSystem();
    
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 10)).result.PopRelease().num;
    AssertEqual(res, 10);
    res = Execute(vm, "test", Val.NewNum(vm, 20)).result.PopRelease().num;
    AssertEqual(res, 20);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeAttributeIsNotAFunction()
  {
    string bhl = @"

    func float r()
    {
      return 0
    }
      
    func void test() 
    {
      Color c = new Color
      c.r()
    }
    ";

    var ts = new TypeSystem();

    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "symbol is not a function"
    );
  }

  [IsTested()]
  public void TestNullWithClassInstance()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c = null
      Color c2 = new Color
      if(c == null) {
        trace(""NULL;"")
      }
      if(c != null) {
        trace(""NEVER;"")
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
      if(c != null) {
        trace(""NOTNULL;"")
      }
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("NULL;NOTNULL;EQ;NOTNULL;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNullWithEncodedStruct()
  {
    string bhl = @"
      
    func void test() 
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

    var ts = new TypeSystem();
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
      
    func void test(StringClass c) 
    {
      if(c != null) {
        trace(""NEVER;"")
      }
      if(c == null) {
        trace(""NULL;"")
      }
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindStringClass(ts);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test", Val.NewObj(vm, null));
    AssertEqual("NULL;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNullPassedAsNewNil()
  {
    string bhl = @"
      
    func void test(StringClass c) 
    {
      if(c != null) {
        trace(""NEVER;"")
      }
      if(c == null) {
        trace(""NULL;"")
      }
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindStringClass(ts);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test", Val.NewObj(vm, null));
    AssertEqual("NULL;", log.ToString());
    CommonChecks(vm);
  }

  ////TODO: do we really need this behavior supported?
  ////[IsTested()]
  //public void TestNullPassedAsCustomNull()
  //{
  //  string bhl = @"
  //    
  //  func void test(CustomNull c) 
  //  {
  //    if(c != null) {
  //      trace(""NOTNULL;"")
  //    }
  //    if(c == null) {
  //      trace(""NULL;"")
  //    }

  //    yield()

  //    if(c != null) {
  //      trace(""NOTNULL2;"")
  //    }
  //    if(c == null) {
  //      trace(""NULL2;"")
  //    }
  //  }
  //  ";

  //  var ts = SymbolTable.CreateBuiltins();
  //  var trace_stream = new MemoryStream();

  //  BindTrace(ts, trace_stream);
  //  BindCustomNull(ts);

  //  var intp = Interpret(bhl, ts);
  //  var node = intp.GetFuncCallNode("test");
  //  var cn = new CustomNull();
  //  node.SetArgs(DynVal.NewObj(cn));

  //  cn.is_null = false;
  //  node.run();

  //  cn.is_null = true;
  //  node.run();

  //  var str = GetString(trace_stream);
  //  AssertEqual("NOTNULL;NULL2;", str);
  //  CommonChecks(intp);
  //}

  [IsTested()]
  public void TestSetNullObjFromUserBinding()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c = mkcolor_null()
      if(c == null) {
        trace(""NULL;"")
      }
    }
    ";

    var ts = new TypeSystem();
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
      
    func void test() 
    {
      Color[] cs = null
      Color[] cs2 = new Color[]
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

    var ts = new TypeSystem();
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
      
    func void test() 
    {
      Color[] cs
      if(cs == null) {
        trace(""NULL;"")
      }
    }
    ";

    var ts = new TypeSystem();
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
      
    func void test() 
    {
      void^() fn = null
      void^() fn2 = func () { }
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

    var ts = new TypeSystem();
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
      int[] arr = []
      int i = 0
      while(i < arr.Count) {
        int tmp = arr[i]
        trace((string)tmp)
        i = i + 1
      }
    }
    ";

    var ts = new TypeSystem();
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
      int[] arr = [1,2,3]
      int i = 0
      while(i < arr.Count) {
        int tmp = arr[i]
        trace((string)tmp)
        i = i + 1
      }
    }
    ";

    var ts = new TypeSystem();
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
      int[] arr = [1,2]
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFor()
  {
    string bhl = @"

    func test() 
    {
      for(int i = 0; i < 3; i = i + 1) {
        trace((string)i)
      }
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("012", log.ToString());
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("0,0;0,1;1,0;1,1;2,0;2,1;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForSeveral()
  {
    string bhl = @"

    func test() 
    {
      for(int i = 0; i < 3; i = i + 1) {
        trace((string)i)
      }

      for(i = 0; i < 30; i = i + 10) {
        trace((string)i)
      }
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("01201020", log.ToString());
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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
  public void TestForeachTraceItems()
  {
    string bhl = @"

    func test() 
    {
      int[] is = [1, 2, 3]
      foreach(is as int it) {
        trace((string)it)
      }
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("123", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForeachForNativeArrayBinding()
  {
    string bhl = @"

    func test() 
    {
      Color[] cs = [{r:1}, {r:2}, {r:3}]
      foreach(cs as Color c) {
        trace((string)c.r)
      }
    }
    ";

    var ts = new TypeSystem();
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
      int[] is = [1, 2, 3]
      foreach(is as it) {
        trace((string)it)
      }
    }
    ";

    var ts = new TypeSystem();
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
      foreach([1,2,3] as int it) {
        trace((string)it)
      }
    }
    ";

    var ts = new TypeSystem();
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

    func int[] foo()
    {
      return [1,2,3]
    }

    func test() 
    {
      foreach(foo() as int it) {
        trace((string)it)
      }
    }
    ";

    var ts = new TypeSystem();
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
        foreach([1,2,3] as int it) {
          trace((string)it)
          yield()
        }
        suspend()
      }
    }
    ";

    var ts = new TypeSystem();
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
  public void TestForeachBreak()
  {
    string bhl = @"

    func test() 
    {
      foreach([1,2,3] as int it) {
        if(it == 3) {
          break
        }
        trace((string)it)
      }
    }
    ";

    var ts = new TypeSystem();
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
      int[] is = [1,2,3]
      foreach(is as int it) {
        trace((string)it)
      }

      foreach(is as int it2) {
        trace((string)it2)
      }
    }
    ";

    var ts = new TypeSystem();
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
      int[] is = [1,2,3]
      foreach(is as int it) {
        foreach(is as int it2) {
          trace((string)it + "","" + (string)it2 + "";"")
        }
      }
    }
    ";

    var ts = new TypeSystem();
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
      foreach([1,2,3] as int it) {
        foreach([20,30] as int it2) {
          trace((string)it + "","" + (string)it2 + "";"")
        }
      }
    }
    ";

    var ts = new TypeSystem();
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
      foreach([1,2,3] as string it) {
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
      foreach(foo() as float it) {
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
      foreach([1,2,3] as it) {
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
      foreach([1,2,3] as int it) {
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
  public void TestBindNativeChildClass()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      ColorAlpha c = new ColorAlpha
      c.r = k*1
      c.g = k*100
      c.a = 1000
      return c.r + c.g + c.a
    }
    ";

    var ts = new TypeSystem();
    
    BindColorAlpha(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 1202);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeChildClassCallParentMethod()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      ColorAlpha c = new ColorAlpha
      c.r = 10
      c.g = 20
      return c.mult_summ(k)
    }
    ";

    var ts = new TypeSystem();
    
    BindColorAlpha(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 60);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeChildClassCallOwnMethod()
  {
    string bhl = @"
      
    func float test() 
    {
      ColorAlpha c = new ColorAlpha
      c.r = 10
      c.g = 20
      c.a = 3
      return c.mult_summ_alpha()
    }
    ";

    var ts = new TypeSystem();
    
    BindColorAlpha(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 90);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeChildClassImplicitBaseCast()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      Color c = new ColorAlpha
      c.r = k*1
      c.g = k*100
      return c.r + c.g
    }
    ";

    var ts = new TypeSystem();
    
    BindColorAlpha(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 202);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeChildClassExplicitBaseCast()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      Color c = (Color)new ColorAlpha
      c.r = k*1
      c.g = k*100
      return c.r + c.g
    }
    ";

    var ts = new TypeSystem();
    
    BindColorAlpha(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 202);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBindChildClassExplicitDownCast()
  {
    string bhl = @"
      
    func float test() 
    {
      ColorAlpha orig = new ColorAlpha
      orig.a = 1000
      Color tmp = (Color)orig
      tmp.r = 1
      tmp.g = 100
      ColorAlpha c = (ColorAlpha)tmp
      return c.r + c.g + c.a
    }
    ";

    var ts = new TypeSystem();
    
    BindColorAlpha(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 1101);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIncompatibleImplicitClassCast()
  {
    string bhl = @"
      
    func float test() 
    {
      Foo tmp = new Color
      return 1
    }
    ";

    var ts = new TypeSystem();
    
    BindColor(ts);

    {
      var cl = new ClassSymbolNative("Foo", null,
        delegate(VM.Frame frm, ref Val v) 
        { 
          v.obj = null;
        }
      );
      ts.globs.Define(cl);
    }

    AssertError<Exception>(
       delegate() {
         Compile(bhl, ts);
       },
      "incompatible types"
    );
  }

  [IsTested()]
  public void TestIncompatibleExplicitClassCast()
  {
    string bhl = @"
      
    func test() 
    {
      Foo tmp = (Foo)new Color
    }
    ";

    var ts = new TypeSystem();
    
    BindColor(ts);

    {
      var cl = new ClassSymbolNative("Foo", null,
        delegate(VM.Frame frm, ref Val v) 
        { 
          v.obj = null;
        }
      );
      ts.globs.Define(cl);
    }

    AssertError<Exception>(
       delegate() {
         Compile(bhl, ts);
       },
      "incompatible types for casting"
    );
  }

  [IsTested()]
  public void TestNestedMembersAccess()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      ColorNested cn = new ColorNested
      cn.c.r = k*1
      cn.c.g = k*100
      return cn.c.r + cn.c.g
    }
    ";

    var ts = new TypeSystem();

    BindColor(ts);

    {
      var cl = new ClassSymbolNative("ColorNested", null,
        delegate(VM.Frame frm, ref Val v) 
        { 
          v.obj = new ColorNested();
        }
      );

      ts.globs.Define(cl);

      cl.Define(new FieldSymbol("c", ts.Type("Color"), 
        delegate(Val ctx, ref Val v)
        {
          var cn = (ColorNested)ctx.obj;
          v.obj = cn.c;
        },
        delegate(ref Val ctx, Val v)
        {
          var cn = (ColorNested)ctx.obj;
          cn.c = (Color)v.obj; 
          ctx.obj = cn;
        }
      ));
    }

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 202);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnNativeInstance()
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
      return MakeColor(k).g + MakeColor(k).r
    }
    ";

    var ts = new TypeSystem();
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestOrShortCircuit()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c = null
      if(c == null || c.r == 0) {
        trace(""OK;"")
      } else {
        trace(""NEVER;"")
      }
    }
    ";

    var ts = new TypeSystem();
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
      
    func void test() 
    {
      Color c = null
      if(c != null && c.r == 0) {
        trace(""NEVER;"")
      } else {
        trace(""OK;"")
      }
    }
    ";

    var ts = new TypeSystem();
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
      
    func void test() 
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

    var ts = new TypeSystem();
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
      
    func void test() 
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

    var ts = new TypeSystem();
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
      
    func void test() 
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
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

    func void test() 
    {
      Bar[] bs = [{Int: 10, Flt: 14.5, Str: ""Hey""}]
      Bar b = bs[0]
      trace((string)b.Int + "";"" + (string)b.Flt + "";"" + b.Str)
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();
    var fn = BindTrace(ts, log);
    BindBar(ts);
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 2 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, "[]") }) 
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, "Bar") }) 
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Bar"), 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 14.5) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Bar"), 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Bar"), 2 })
      .EmitThen(Opcodes.CallMethodNative, new int[] { ArrAddInplaceIdx, ConstIdx(c, "[]"), 1 })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .EmitThen(Opcodes.CallMethodNative, new int[] { ArrAtIdx, ConstIdx(c, "[]"), 1 })
      .EmitThen(Opcodes.SetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "Bar"), 0 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "Bar"), 1 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetAttr, new int[] { ConstIdx(c, "Bar"), 2 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.CallNative, new int[] { ts.globs.GetMembers().IndexOf(fn), 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
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

    var ts = new TypeSystem();
    
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

    var ts = new TypeSystem();

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
      
    func void test() 
    {
      Color c1
      Color c2
      Color c3 = c1 + c2
    }
    ";

    var ts = new TypeSystem();
    
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
      
    func void test() 
    {
      Color c1
      Color c2
      Color c3 = c1 - c2
    }
    ";

    var ts = new TypeSystem();
    
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
      
    func void test() 
    {
      Color c1
      Color c2
      Color c3 = c1 * c2
    }
    ";

    var ts = new TypeSystem();
    
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
      
    func void test() 
    {
      Color c1
      Color c2
      Color c3 = c1 / c2
    }
    ";

    var ts = new TypeSystem();
    
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
      
    func void test() 
    {
      Color c1
      Color c2
      bool r = c1 > c2
    }
    ";

    var ts = new TypeSystem();
    
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
      
    func void test() 
    {
      Color c1
      Color c2
      bool r = c1 >= c2
    }
    ";

    var ts = new TypeSystem();
    
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
      
    func void test() 
    {
      Color c1
      Color c2
      bool r = c1 > c2
    }
    ";

    var ts = new TypeSystem();
    
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
      
    func void test() 
    {
      Color c1
      Color c2
      bool r = c1 > c2
    }
    ";

    var ts = new TypeSystem();
    
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
      
    func void test() 
    {
      Color c1
      Color c2 = -c1
    }
    ";

    var ts = new TypeSystem();
    
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
      
    func void test() 
    {
      Color c1
      Color c2
      int a = c2 & c1
    }
    ";

    var ts = new TypeSystem();
    
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
      
    func void test() 
    {
      Color c1
      Color c2
      int a = c2 | c1
    }
    ";

    var ts = new TypeSystem();
    
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
      
    func void test() 
    {
      Color c1
      Color c2
      bool a = c2 && c1
    }
    ";

    var ts = new TypeSystem();
    
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
      
    func void test() 
    {
      Color c1
      Color c2
      bool a = c2 || c1
    }
    ";

    var ts = new TypeSystem();
    
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
      
    func void test() 
    {
      Color c1
      bool a = !c1
    }
    ";

    var ts = new TypeSystem();
    
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

    var ts = new TypeSystem();
    
    var cl = BindColor(ts);
    var op = new FuncSymbolNative("+", ts.Type("Color"),
      delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
      {
        var r = (Color)frm.stack.PopRelease().obj;
        var c = (Color)frm.stack.PopRelease().obj;

        var newc = new Color();
        newc.r = c.r + r.r;
        newc.g = c.g + r.g;

        var v = Val.NewObj(frm.vm, newc);
        frm.stack.Push(v);

        return null;
      },
      new FuncArgSymbol("r", ts.Type("Color"))
    );
    cl.OverloadBinaryOperator(op);

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

    var ts = new TypeSystem();
    
    var cl = BindColor(ts);
    var op = new FuncSymbolNative("*", ts.Type("Color"),
      delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
      {
        var k = (float)frm.stack.PopRelease().num;
        var c = (Color)frm.stack.PopRelease().obj;

        var newc = new Color();
        newc.r = c.r * k;
        newc.g = c.g * k;

        var v = Val.NewObj(frm.vm, newc);
        frm.stack.Push(v);

        return null;
      },
      new FuncArgSymbol("k", ts.Type("float"))
    );
    cl.OverloadBinaryOperator(op);

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

    var ts = new TypeSystem();
    
    var cl = BindColor(ts);
    {
      var op = new FuncSymbolNative("*", ts.Type("Color"),
      delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
      {
        var k = (float)frm.stack.PopRelease().num;
        var c = (Color)frm.stack.PopRelease().obj;

        var newc = new Color();
        newc.r = c.r * k;
        newc.g = c.g * k;

        var v = Val.NewObj(frm.vm, newc);
        frm.stack.Push(v);

        return null;
      },
      new FuncArgSymbol("k", ts.Type("float"))
      );
      cl.OverloadBinaryOperator(op);
    }
    
    {
      var op = new FuncSymbolNative("+", ts.Type("Color"),
      delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
      {
        var r = (Color)frm.stack.PopRelease().obj;
        var c = (Color)frm.stack.PopRelease().obj;

        var newc = new Color();
        newc.r = c.r + r.r;
        newc.g = c.g + r.g;

        var v = Val.NewObj(frm.vm, newc);
        frm.stack.Push(v);

        return null;
      },
      new FuncArgSymbol("r", ts.Type("Color"))
      );
      cl.OverloadBinaryOperator(op);
    }

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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);
    
    var cl = BindColor(ts);
    var op = new FuncSymbolNative("==", ts.Type("bool"),
      delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
      {
        var arg = (Color)frm.stack.PopRelease().obj;
        var c = (Color)frm.stack.PopRelease().obj;

        var v = Val.NewBool(frm.vm, c.r == arg.r && c.g == arg.g);
        frm.stack.Push(v);

        return null;
      },
      new FuncArgSymbol("arg", ts.Type("Color"))
    );
    cl.OverloadBinaryOperator(op);

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

    var ts = new TypeSystem();
    
    var cl = BindColor(ts);
    var op = new FuncSymbolNative("*", ts.Type("Color"), null,
      new FuncArgSymbol("k", ts.Type("float"))
    );
    cl.OverloadBinaryOperator(op);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "incompatible types"
    );
  }

  void BindEnum(TypeSystem ts)
  {
    var en = new EnumSymbol("EnumState");
    ts.globs.Define(en);

    en.Define(new EnumItemSymbol(en, "SPAWNED",  10));
    en.Define(new EnumItemSymbol(en, "SPAWNED2", 20));
  }

  [IsTested()]
  public void TestBindEnum()
  {
    string bhl = @"
      
    func int test() 
    {
      return (int)EnumState::SPAWNED + (int)EnumState::SPAWNED2
    }
    ";

    var ts = new TypeSystem();

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
      return (int)EnumState::SPAWNED2
    }
    ";

    var ts = new TypeSystem();

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
      return (float)EnumState::SPAWNED
    }
    ";

    var ts = new TypeSystem();

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
      return (string)EnumState::SPAWNED2
    }
    ";

    var ts = new TypeSystem();

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
      return state == EnumState::SPAWNED2
    }
    ";

    var ts = new TypeSystem();

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
      return state != EnumState::SPAWNED
    }
    ";

    var ts = new TypeSystem();

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
      
    func EnumState[] test() 
    {
      EnumState[] arr = new EnumState[]
      arr.Add(EnumState::SPAWNED2)
      arr.Add(EnumState::SPAWNED)
      return arr
    }
    ";

    var ts = new TypeSystem();

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
      return StateIs(state : EnumState::SPAWNED2)
    }
    ";

    var ts = new TypeSystem();

    BindEnum(ts);

    {
      var fn = new FuncSymbolNative("StateIs", ts.Type("bool"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          var n = frm.stack.PopRelease().num;
          frm.stack.Push(Val.NewBool(frm.vm, n == 20));
          return null;
        },
        new FuncArgSymbol("state", ts.Type("EnumState"))
        );

      ts.globs.Define(fn);
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
      Foo f = Foo::B 
      return (int)f
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
      return (int)Foo::B + (int)Foo::A
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
  public void TestUserEnumItemBadChainCall()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
    }

    func foo(Foo f) 
    {
    }

    func test() 
    {
      foo(Foo.A)
    }

    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"symbol usage is not valid"
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var c = Compile(bhl, ts);

    var vm = MakeVM(c);

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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var c = Compile(bhl, ts);

    var vm = MakeVM(c);

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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var c = Compile(bhl, ts);

    var vm = MakeVM(c);

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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var c = Compile(bhl, ts);

    var vm = MakeVM(c);

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

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var c = Compile(bhl, ts);

    var vm = MakeVM(c);

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

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var importer = new ModuleImporter(CompileFiles(files, ts));

    var vm = new VM(ts, importer);

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
    vm.RegisterModule(c.Compile());
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

    var importer = new ModuleImporter(CompileFiles(files));

    AssertEqual(importer.Import("bhl1"), 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Import, new int[] { 0 })
      .EmitThen(Opcodes.Func, new int[] { 1, 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.Constant, new int[] { 2 })
      .EmitThen(Opcodes.CallImported, new int[] { 0, 1 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
    );
    AssertEqual(importer.Import("bhl2"), 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Import, new int[] { 0 })
      .EmitThen(Opcodes.Func, new int[] { 1, 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.ArgVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.CallImported, new int[] { 0, 1 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
    );
    AssertEqual(importer.Import("bhl3"), 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.Func, new int[] { 0, 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.ArgVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
    );

    var vm = new VM(null, importer);
    vm.LoadModule("bhl1");
    AssertEqual(Execute(vm, "bhl1").result.PopRelease().num, 23);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportUserClass()
  {
    string bhl1 = @"
    class Foo { 
      int Int
      float Flt
      string Str
    }
    ";
      
  string bhl2 = @"
    import ""bhl1""  
    func void test() 
    {
      Foo f = new Foo
      f.Int = 10
      f.Flt = 14.2
      f.Str = ""Hey""
      trace((string)f.Int + "";"" + (string)f.Flt + "";"" + f.Str)
    }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var importer = new ModuleImporter(CompileFiles(files, ts));

    var vm = new VM(ts, importer);

    vm.LoadModule("bhl2");
    Execute(vm, "test");
    AssertEqual("10;14.2;Hey", log.ToString().Replace(',', '.')/*locale issues*/);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportClassMoreComplex()
  {
    string bhl1 = @"
    import ""bhl2""  
    func float test(float k) 
    {
      Foo f = { x : k }
      return bar(f)
    }
    ";

    string bhl2 = @"
    import ""bhl3""  

    class Foo
    {
      float x
    }

    func float bar(Foo f)
    {
      return hey(f.x)
    }
    ";

    string bhl3 = @"
    func float hey(float k)
    {
      return k
    }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var importer = new ModuleImporter(CompileFiles(files));

    var vm = new VM(null, importer);

    vm.LoadModule("bhl1");
    AssertEqual(42, Execute(vm, "test", Val.NewNum(vm, 42)).result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportClassConflict()
  {
    string bhl1 = @"
    import ""bhl2""  

    class Bar { }

    func test() { }
    ";

    string bhl2 = @"
    class Bar { }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    
    AssertError<Exception>(
      delegate() { 
        CompileFiles(files);
      },
      @"already defined symbol 'Bar'"
    );
  }

  [IsTested()]
  public void TestImportEnum()
  {
    string bhl1 = @"
    import ""bhl2""  

    func float test() 
    {
      Foo f = Foo::B
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

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);

    var importer = new ModuleImporter(CompileFiles(files));

    var vm = new VM(null, importer);

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

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);

    AssertError<Exception>(
      delegate() { 
        CompileFiles(files);
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

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);

    var importer = new ModuleImporter(CompileFiles(files));

    var vm = new VM(null, importer);

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

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);

    var importer = new ModuleImporter(CompileFiles(files));

    var vm = new VM(null, importer);

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

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var importer = new ModuleImporter(CompileFiles(files));

    var vm = new VM(null, importer);

    vm.LoadModule("bhl1");
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
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

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);

    AssertError<Exception>(
      delegate() { 
        CompileFiles(files);
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

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var importer = new ModuleImporter(CompileFiles(files));

    var vm = new VM(null, importer);

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

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var importer = new ModuleImporter(CompileFiles(files));

    var vm = new VM(null, importer);

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

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);

    AssertError<Exception>(
      delegate() { 
        CompileFiles(files);
      },
      @"already defined symbol 'bar'"
    );
  }

  [IsTested()]
  public void TestStartLambdaInScriptMgr()
  {
    string bhl = @"

    func void test() 
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

    var ts = new TypeSystem();
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

    func void test() 
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

    var ts = new TypeSystem();
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

    func void test() 
    {
      StartScriptInMgr(
        script: func() { 
          suspend()
        },
        spawns : 3
      )
    }
    ";

    var ts = new TypeSystem();
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

    func void test() 
    {
      void^() fn = func() {
        trace(""HERE;"")
        suspend()
      }

      StartScriptInMgr(
        script: fn,
        spawns : 2
      )
    }
    ";

    var ts = new TypeSystem();
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

    func void test() 
    {
      StartScriptInMgr(
        script: say_here,
        spawns : 2
      )
    }
    ";

    var ts = new TypeSystem();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindStartScriptInMgr(ts);

    {
      var fn = new FuncSymbolNative("say_here", ts.Type("void"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            log.Append("HERE;");
            return null;
          }
          );
      ts.globs.Define(fn);
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

    func void test() 
    {
      void^() fn = func() {
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

    var ts = new TypeSystem();
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

    func void test() 
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

    var ts = new TypeSystem();
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

    func void test() 
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

    var ts = new TypeSystem();
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

    var ts = new TypeSystem();
    var trace = new List<VM.TraceItem>();
    {
      var fn = new FuncSymbolNative("record_callstack", ts.Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.fb.GetStackTrace(trace); 
          return null;
        });
      ts.globs.Define(fn);
    }

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var importer = new ModuleImporter(CompileFiles(files, ts));

    var vm = new VM(ts, importer);
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

    var ts = new TypeSystem();
    var trace = new List<VM.TraceItem>();
    {
      var fn = new FuncSymbolNative("record_callstack", ts.Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.fb.GetStackTrace(trace); 
          return null;
        });
      ts.globs.Define(fn);
    }

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var importer = new ModuleImporter(CompileFiles(files, ts));

    var vm = new VM(ts, importer);
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

    var ts = new TypeSystem();

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);

    var importer = new ModuleImporter(CompileFiles(files, ts));

    var vm = new VM(ts, importer);
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

    var ts = new TypeSystem();
    var info = new Dictionary<VM.Fiber, List<VM.TraceItem>>();
    {
      var fn = new FuncSymbolNative("throw", ts.Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          //emulating null reference
          frm = null;
          frm.fb = null;
          return null;
        });
      ts.globs.Define(fn);
    }

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var importer = new ModuleImporter(CompileFiles(files, ts));

    var vm = new VM(ts, importer);
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

    var ts = new TypeSystem();
    var trace = new List<VM.TraceItem>();
    {
      var fn = new FuncSymbolNative("record_callstack", ts.Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.fb.GetStackTrace(trace); 
          return null;
        });
      ts.globs.Define(fn);
    }

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var importer = new ModuleImporter(CompileFiles(files, ts));

    var vm = new VM(ts, importer);
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

    var ts = new TypeSystem();
    var trace = new List<VM.TraceItem>();
    {
      var fn = new FuncSymbolNative("record_callstack", ts.Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.fb.GetStackTrace(trace); 
          return null;
        });
      ts.globs.Define(fn);
    }

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var importer = new ModuleImporter(CompileFiles(files, ts));

    var vm = new VM(ts, importer);
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

    var ts = new TypeSystem();
    var trace = new List<VM.TraceItem>();
    {
      var fn = new FuncSymbolNative("record_callstack", ts.Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.fb.GetStackTrace(trace); 
          return null;
        });
      ts.globs.Define(fn);
    }

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var importer = new ModuleImporter(CompileFiles(files, ts));

    var vm = new VM(ts, importer);
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

    var ts = new TypeSystem();
    var trace = new List<VM.TraceItem>();
    {
      var fn = new FuncSymbolNative("record_callstack", ts.Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.fb.GetStackTrace(trace); 
          return null;
        });
      ts.globs.Define(fn);
    }

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var importer = new ModuleImporter(CompileFiles(files, ts));

    var vm = new VM(ts, importer);
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

    var ts = new TypeSystem();
    var trace = new List<VM.TraceItem>();
    {
      var fn = new FuncSymbolNative("record_callstack", ts.Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.fb.GetStackTrace(trace); 
          return null;
        });
      ts.globs.Define(fn);
    }

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var importer = new ModuleImporter(CompileFiles(files, ts));

    var vm = new VM(ts, importer);
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
  public void TestSimpleGlobalVariableDecl()
  {
    string bhl = @"

    float foo
      
    func float test() 
    {
      return foo
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.DeclVar, new int[] { 0, ConstIdx(c, "float") })
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.GetGVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
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
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
      .EmitThen(Opcodes.SetGVar, new int[] { 0 })
      .EmitThen(Opcodes.GetGVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
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
      .EmitThen(Opcodes.Func, new int[] { ConstIdx(c, "test"), 0 })
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.GetGVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
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

    Foo[] foos = [{b : 100}, {b: 200}]
      
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

    var ts = new TypeSystem();
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
      AssertEqual(dv._refs, 2);

      lst.Clear();
      AssertEqual(dv._refs, 1);
      dv.Release();
    }

    {
      var dv = Val.New(vm);
      lst.Add(dv);
      AssertEqual(dv._refs, 2);

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
      AssertEqual(dv0._refs, 2);
      AssertEqual(dv1._refs, 2);

      lst.RemoveAt(1);
      AssertEqual(dv0._refs, 2);
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
    func void test() 
    {
      RefC r = new RefC
    }
    ";

    var ts = new TypeSystem();

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

    var ts = new TypeSystem();

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
    func void test() 
    {
      RefC r1 = new RefC
      RefC r2 = r1
      r2 = r1
    }
    ";

    var ts = new TypeSystem();

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
    func void test() 
    {
      RefC r1 = new RefC
      r1 = r1
    }
    ";

    var ts = new TypeSystem();

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
    func void test() 
    {
      RefC r1 = new RefC
      RefC r2 = new RefC
      r1 = r2
    }
    ";

    var ts = new TypeSystem();

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
    func void test() 
    {
      RefC r1 = new RefC
      RefC r2 = new RefC
    }
    ";

    var ts = new TypeSystem();

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
    func void test() 
    {
      RefC r1 = new RefC

      trace(""REFS"" + (string)r1.refs + "";"")

      void^() fn = func() {
        trace(""REFS"" + (string)r1.refs + "";"")
      }
      
      fn()
      trace(""REFS"" + (string)r1.refs + "";"")
    }
    ";

    var ts = new TypeSystem();

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
    func void test() 
    {
      RefC[] rs = new RefC[]
      rs.Add(new RefC)
    }
    ";

    var ts = new TypeSystem();

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
    func void test() 
    {
      RefC[] rs = new RefC[]
      rs.Add(new RefC)
      rs.Add(new RefC)
      float refs = rs[1].refs
    }
    ";

    var ts = new TypeSystem();

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

    func void test() 
    {
      RefC c1 = make()
    }
    ";

    var ts = new TypeSystem();

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

    func void foo(RefC c)
    { 
      trace(""HERE;"")
    }

    func void test() 
    {
      foo(new RefC)
    }
    ";

    var ts = new TypeSystem();

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

    func void foo(RefC c)
    {
      RefC c2 = c
    }

    func void test() 
    {
      RefC c1 = make()
      foo(c1)
    }
    ";

    var ts = new TypeSystem();

    var logs = new StringBuilder();
    BindRefC(ts, logs);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("INC1;INC2;DEC1;INC2;DEC1;INC2;DEC1;INC2;INC3;DEC2;INC3;INC4;DEC3;DEC2;DEC1;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSerializeModuleScope()
  {

    var s = new MemoryStream();
    {
      var types = new TypeSystem();

      var ms = new ModuleScope(1, types);
      types.AddSource(ms);

      ms.Define(new VariableSymbol("foo", types.Type(TypeSystem.Int)));

      ms.Define(new VariableSymbol("bar", types.Type(TypeSystem.String)));

      ms.Define(new VariableSymbol("wow", types.TypeArr("bool")));

      ms.Define(new FuncSymbolScript(new FuncSignature(types.TypeTuple("int","float"), types.Type("int"), types.Type("string")), "Test", 1, 4, 155));

      ms.Define(new FuncSymbolScript(new FuncSignature(types.TypeArr("string"), types.Type("Bar")), "Make", 10, 3, 15));

      var Foo = new ClassSymbolScript("Foo", null);
      Foo.Define(new FieldSymbolScript("Int", types.Type("int")));
      Foo.Define(new FuncSymbolScript(new FuncSignature(types.Type("void")), "Hey", 0, 4, 3));
      ms.Define(Foo);
      var Bar = new ClassSymbolScript("Bar", null, Foo);
      Bar.Define(new FieldSymbolScript("Float", types.Type("float")));
      Bar.Define(new FuncSymbolScript(new FuncSignature(types.TypeTuple("bool","bool"), types.Type("int")), "What", 1, 5, 1));
      ms.Define(Bar);

      Util.Struct2Data(ms, s);
    }

    {
      var types = new TypeSystem();
      var factory = new SymbolFactory(types);

      var ms = new ModuleScope(types);
      types.AddSource(ms);

      s.Position = 0;
      Util.Data2Struct(s, factory, ms);

      AssertEqual(ms.module_id, 1);

      AssertEqual(7, ms.GetMembers().Count);

      var foo = (VariableSymbol)ms.Resolve("foo");
      AssertEqual(foo.name, "foo");
      AssertEqual(foo.type.Get(), TypeSystem.Int);
      AssertEqual(foo.scope, ms);

      var bar = (VariableSymbol)ms.Resolve("bar");
      AssertEqual(bar.name, "bar");
      AssertEqual(bar.type.Get(), TypeSystem.String);
      AssertEqual(bar.scope, ms);

      var wow = (VariableSymbol)ms.Resolve("wow");
      AssertEqual(wow.name, "wow");
      AssertEqual(wow.type.Get().GetName(), types.TypeArr("string").Get().GetName());
      AssertEqual(((GenericArrayTypeSymbol)wow.type.Get()).item_type.Get(), TypeSystem.Bool);
      AssertEqual(wow.scope, ms);

      var Test = (FuncSymbolScript)ms.Resolve("Test");
      AssertEqual(Test.name, "Test");
      AssertEqual(types.TypeFunc(types.TypeTuple("int", "float"), "int", "string").name, Test.GetSignature().name);
      AssertEqual(1, Test.default_args_num);
      AssertEqual(4, Test.local_vars_num);
      AssertEqual(155, Test.ip_addr);

      var Make = (FuncSymbolScript)ms.Resolve("Make");
      AssertEqual(Make.name, "Make");
      AssertEqual(1, Make.GetSignature().arg_types.Count);
      AssertEqual(types.TypeArr("string").Get().GetName(), Make.GetReturnType().GetName());
      AssertEqual(types.Type("Bar").Get(), Make.GetSignature().arg_types[0].Get());
      AssertEqual(10, Make.default_args_num);
      AssertEqual(3, Make.local_vars_num);
      AssertEqual(15, Make.ip_addr);

      var Foo = (ClassSymbolScript)ms.Resolve("Foo");
      AssertTrue(Foo.super_class == null);
      AssertEqual(Foo.name, "Foo");
      AssertEqual(Foo.GetMembers().Count, 2);
      var Foo_Int = Foo.Resolve("Int") as FieldSymbolScript;
      AssertEqual(Foo_Int.name, "Int");
      AssertEqual(Foo_Int.type.Get(), TypeSystem.Int);
      var Foo_Hey = Foo.Resolve("Hey") as FuncSymbolScript;
      AssertEqual(Foo_Hey.name, "Hey");
      AssertEqual(Foo_Hey.GetReturnType(), TypeSystem.Void);
      AssertEqual(0, Foo_Hey.default_args_num);
      AssertEqual(4, Foo_Hey.local_vars_num);
      AssertEqual(3, Foo_Hey.ip_addr);

      var Bar = (ClassSymbolScript)ms.Resolve("Bar");
      AssertEqual(Bar.super_class.name, Foo.name);
      AssertEqual(Bar.super_class, Foo);
      AssertEqual(Bar.name, "Bar");
      AssertEqual(Bar.GetMembers().Count, 2/*from parent*/+2);
      var Bar_Float = Bar.Resolve("Float") as FieldSymbolScript;
      AssertEqual(Bar_Float.name, "Float");
      AssertEqual(Bar_Float.type.Get(), TypeSystem.Float);
      var Bar_What = Bar.Resolve("What") as FuncSymbolScript;
      AssertEqual(Bar_What.name, "What");
      AssertEqual(Bar_What.GetReturnType().GetName(), types.TypeTuple("bool", "bool").Get().GetName());
      AssertEqual(Bar_What.GetSignature().arg_types[0].Get(), TypeSystem.Int);
      AssertEqual(1, Bar_What.default_args_num);
      AssertEqual(5, Bar_What.local_vars_num);
      AssertEqual(1, Bar_What.ip_addr);

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

  ///////////////////////////////////////

  static ModuleCompiler MakeCompiler(TypeSystem ts = null)
  {
    if(ts == null)
      ts = new TypeSystem();
    //NOTE: we don't want to affect the original ts
    var ts_copy = ts.Clone();
    return new ModuleCompiler(ts_copy);
  }

  Stream CompileFiles(List<string> files, TypeSystem ts = null)
  {
    if(ts == null)
      ts = new TypeSystem();
    //NOTE: we don't want to affect the original ts
    var ts_copy = ts.Clone();

    var conf = new BuildConf();
    conf.module_fmt = ModuleBinaryFormat.FMT_BIN;
    conf.ts = ts_copy;
    conf.files = files;
    conf.res_file = TestDirPath() + "/result.bin";
    conf.inc_dir = TestDirPath();
    conf.cache_dir = TestDirPath() + "/cache";
    conf.err_file = TestDirPath() + "/error.log";
    conf.use_cache = false;

    var bld = new Build();
    int res = bld.Exec(conf);
    if(res != 0)
      throw new Exception(File.ReadAllText(conf.err_file));

    return new MemoryStream(File.ReadAllBytes(conf.res_file));
  }

  ModuleCompiler Compile(string bhl, TypeSystem ts = null, bool show_ast = false, bool show_bytes = false)
  {
    if(ts == null)
      ts = new TypeSystem();
    //NOTE: we don't want to affect the original ts
    var ts_copy = ts.Clone();

    var mdl = new bhl.Module(ts_copy, "", "");
    var mreg = new ModuleRegistry();
    var ast = Src2AST(bhl, mdl, mreg, ts_copy);
    if(show_ast)
      Util.ASTDump(ast);
    var c  = new ModuleCompiler(ts_copy, ast, mdl.path);
    c.Compile();
    if(show_bytes)
      Dump(c);
    return c;
  }

  AST_Nested Src2AST(string src, bhl.Module mdl, ModuleRegistry mreg, TypeSystem ts = null)
  {
    if(ts == null)
      ts = new TypeSystem();

    var ms = new MemoryStream();
    Frontend.Source2Bin(mdl, src.ToStream(), ms, ts, mreg);
    ms.Position = 0;

    return Util.Data2Struct<AST_Module>(ms, new AST_Factory());
  }

  void CommonChecks(VM vm, bool check_frames = true, bool check_fibers = true, bool check_instructions = true)
  {
    //cleaning globals
    vm.UnloadModules();

    //for extra debug
    if(vm.vals_pool.Allocs != vm.vals_pool.Free)
      Console.WriteLine(vm.vals_pool.Dump());

    AssertEqual(vm.vlsts_pool.Allocs, vm.vlsts_pool.Free);
    AssertEqual(vm.vals_pool.Allocs, vm.vals_pool.Free);
    AssertEqual(vm.ptrs_pool.Allocs, vm.ptrs_pool.Free);
    if(check_frames)
      AssertEqual(vm.frames_pool.Allocs, vm.frames_pool.Free);
    if(check_fibers)
      AssertEqual(vm.fibers_pool.Allocs, vm.fibers_pool.Free);
    if(check_instructions)
      AssertEqual(vm.coro_pool.Allocs, vm.coro_pool.Free);
  }

  public static string ByteArrayToString(byte[] ba)
  {
    var hex = new StringBuilder(ba.Length * 2);
    foreach(byte b in ba)
      hex.AppendFormat("{0:x2}", b);
    return hex.ToString();
  }

  public static void AssertEqual(byte[] a, byte[] b)
  {
    bool equal = true;
    string cmp = "";
    for(int i=0;i<(a.Length > b.Length ? a.Length : b.Length);i++)
    {
      string astr = "";
      if(i < a.Length)
        astr = string.Format("{0:x2}", a[i]);

      string bstr = "";
      if(i < b.Length)
        bstr = string.Format("{0:x2}", b[i]);

      cmp += string.Format("{0:x2}", i) + " " + astr + " | " + bstr;

      if(astr != bstr)
      {
        equal = false;
        cmp += " !!!";
      }

      cmp += "\n";
    }

    if(!equal)
    {
      Console.WriteLine(cmp);
      throw new Exception("Assertion failed: bytes not equal");
    }
  }

  public static void Print(ModuleCompiler c)
  {
    var bs = c.Compile().bytecode;
    ModuleCompiler.Definition op = null;
    int op_size = 0;

    for(int i=0;i<bs.Length;i++)
    {
      string str = string.Format("0x{0:x2}", bs[i]);
      if(op != null)
      {
        --op_size;
        if(op_size == 0)
          op = null; 
      }
      else
      {
        op = ModuleCompiler.LookupOpcode((Opcodes)bs[i]);
        op_size = PredictOpcodeSize(op, bs, i);
        str += "(" + op.name.ToString() + ")";
        if(op_size == 0)
          op = null;
      }
      Console.WriteLine(string.Format("{0:x2}", i) + " " + str);
    }
    Console.WriteLine("============");
  }

  public static void AssertEqual(ModuleCompiler ca, ModuleCompiler cb)
  {
    AssertEqual(ca.Compile(), cb.Compile());
  }

  public static void AssertEqual(CompiledModule ca, ModuleCompiler cb)
  {
    AssertEqual(ca, cb.Compile());
  }

  public static void AssertEqual(CompiledModule ca, CompiledModule cb)
  {
    string cmp;

    if(!CompareCode(ca.initcode, cb.initcode, out cmp))
    {
      Console.WriteLine(cmp);
      throw new Exception("Assertion failed: init bytes not equal");
    }

    if(!CompareCode(ca.bytecode, cb.bytecode, out cmp))
    {
      Console.WriteLine(cmp);
      throw new Exception("Assertion failed: bytes not equal");
    }
  }

  static void Dump(ModuleCompiler c)
  {
    Dump(c.Compile());
  }

  static void Dump(CompiledModule c)
  {
    if(c.initcode?.Length > 0)
    {
      Console.WriteLine("=== INIT ===");
      Dump(c.initcode);
    }
    Console.WriteLine("=== CODE ===");
    Dump(c.bytecode);
  }

  static void Dump(byte[] bs)
  {
    string res = "";

    ModuleCompiler.Definition op = null;
    int op_size = 0;

    for(int i=0;i<bs?.Length;i++)
    {
      res += string.Format("{1:00} 0x{0:x2} {0}", bs[i], i);
      if(op != null)
      {
        --op_size;
        if(op_size == 0)
          op = null; 
      }
      else
      {
        op = ModuleCompiler.LookupOpcode((Opcodes)bs[i]);
        op_size = PredictOpcodeSize(op, bs, i);
        res += "(" + op.name.ToString() + ")";
        if(op_size == 0)
          op = null;
      }
      res += "\n";
    }

    Console.WriteLine(res);
  }

  static bool CompareCode(byte[] a, byte[] b, out string cmp)
  {
    ModuleCompiler.Definition aop = null;
    int aop_size = 0;
    ModuleCompiler.Definition bop = null;
    int bop_size = 0;

    bool equal = true;
    cmp = "";
    var lens = new List<int>();
    int max_len = 0;
    for(int i=0;i<(a?.Length > b?.Length ? a?.Length : b?.Length);i++)
    {
      string astr = "";
      if(i < a?.Length)
      {
        astr = string.Format("0x{0:x2} {0}", a[i]);
        if(aop != null)
        {
          --aop_size;
          if(aop_size == 0)
            aop = null; 
        }
        else
        {
          aop = ModuleCompiler.LookupOpcode((Opcodes)a[i]);
          aop_size = PredictOpcodeSize(aop, a, i);
          astr += "(" + aop.name.ToString() + ")";
          if(aop_size == 0)
            aop = null;
        }
      }

      string bstr = "";
      if(i < b?.Length)
      {
        bstr = string.Format("0x{0:x2} {0}", b[i]);
        if(bop != null)
        {
          --bop_size;
          if(bop_size == 0)
            bop = null; 
        }
        else
        {
          bop = ModuleCompiler.LookupOpcode((Opcodes)b[i]);
          bop_size = PredictOpcodeSize(bop, b, i);
          bstr += "(" + bop.name.ToString() + ")";
          if(bop_size == 0)
            bop = null;
        }
      }

      lens.Add(astr.Length);
      if(astr.Length > max_len)
        max_len = astr.Length;
      cmp += string.Format("{0,2}", i) + " " + astr + "{fill" + lens.Count + "} | " + bstr;

      if(a?.Length <= i || b?.Length <= i || a[i] != b[i])
      {
        equal = false;
        cmp += " <============== actual vs expected";
      }

      cmp += "\n";
    }

    for(int i=1;i<=lens.Count;++i)
    {
      cmp = cmp.Replace("{fill" + i + "}", new String(' ', max_len - lens[i-1]));
    }

    return equal;
  }

  public static void AssertEqual(List<Const> cas, List<Const> cbs)
  {
    AssertEqual(cas.Count, cbs.Count);
    for(int i=0;i<cas.Count;++i)
    {
      AssertEqual((int)cas[i].type, (int)cbs[i].type);
      AssertEqual(cas[i].num, cbs[i].num);
      AssertEqual(cas[i].str, cbs[i].str);
    }
  }

  static VM MakeVM(ModuleCompiler c)
  {
    var vm = new VM(c.Types);
    var m = c.Compile();
    vm.RegisterModule(m);
    return vm;
  }

  VM MakeVM(string bhl, TypeSystem ts = null, bool show_ast = false, bool show_bytes = false)
  {
    return MakeVM(Compile(bhl, ts, show_ast: show_ast, show_bytes: show_bytes));
  }

  VM.Fiber Execute(VM vm, string fn_name, params Val[] args)
  {
    return Execute(vm, fn_name, 0, args);
  }

  VM.Fiber Execute(VM vm, string fn_name, FuncArgsInfo args_info, params Val[] args)
  {
    return Execute(vm, fn_name, args_info.bits, args);
  }

  VM.Fiber Execute(VM vm, string fn_name, uint cargs_bits, params Val[] args)
  {
    var fb = vm.Start(fn_name, cargs_bits, args);
    const int LIMIT = 20;
    int c = 0;
    for(;c<LIMIT;++c)
    {
      if(!vm.Tick())
        return fb;
    }
    throw new Exception("Too many iterations: " + c);
  }

  static int PredictOpcodeSize(ModuleCompiler.Definition op, byte[] bytes, int start_pos)
  {
    if(op.operand_width == null)
      return 0;
    int pos = start_pos;
    foreach(int ow in op.operand_width)
      Bytecode.Decode(bytes, ow, ref pos);
    return pos - start_pos;
  }

  public static int ArrAddIdx {
    get {
      var ts = new TypeSystem();
      var class_symb = (ClassSymbol)ts.Resolve(GenericArrayTypeSymbol.CLASS_TYPE);
      return ((IScopeIndexed)class_symb.Resolve("Add")).scope_idx;
    }
  }

  public static int ArrSetIdx {
    get {
      var ts = new TypeSystem();
      var class_symb = (ClassSymbol)ts.Resolve(GenericArrayTypeSymbol.CLASS_TYPE);
      return ((IScopeIndexed)class_symb.Resolve("SetAt")).scope_idx;
    }
  }

  public static int ArrRemoveIdx {
    get {
      var ts = new TypeSystem();
      var class_symb = (ClassSymbol)ts.Resolve(GenericArrayTypeSymbol.CLASS_TYPE);
      return ((IScopeIndexed)class_symb.Resolve("RemoveAt")).scope_idx;
    }
  }

  public static int ArrCountIdx {
    get {
      var ts = new TypeSystem();
      var class_symb = (ClassSymbol)ts.Resolve(GenericArrayTypeSymbol.CLASS_TYPE);
      return ((IScopeIndexed)class_symb.Resolve("Count")).scope_idx;
    }
  }

  public static int ArrAtIdx {
    get {
      var ts = new TypeSystem();
      var class_symb = (ClassSymbol)ts.Resolve(GenericArrayTypeSymbol.CLASS_TYPE);
      return ((IScopeIndexed)class_symb.Resolve("At")).scope_idx;
    }
  }

  public static int ArrAddInplaceIdx {
    get {
      var ts = new TypeSystem();
      var class_symb = (ClassSymbol)ts.Resolve(GenericArrayTypeSymbol.CLASS_TYPE);
      return ((IScopeIndexed)class_symb.Resolve("$AddInplace")).scope_idx;
    }
  }

  FuncSymbolNative BindTrace(TypeSystem ts, StringBuilder log)
  {
    var fn = new FuncSymbolNative("trace", ts.Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          string str = frm.stack.PopRelease().str;
          log.Append(str);
          return null;
        }, 
        new FuncArgSymbol("str", ts.Type("string"))
    );
    ts.globs.Define(fn);
    return fn;
  }

  //simple console outputting version
  void BindLog(TypeSystem ts)
  {
    {
      var fn = new FuncSymbolNative("log", ts.Type("void"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
            string str = frm.stack.PopRelease().str;
            Console.WriteLine(str); 
            return null;
          },
          new FuncArgSymbol("str", ts.Type("string"))
      );
      ts.globs.Define(fn);
    }
  }

  void BindMin(TypeSystem ts)
  {
    var fn = new FuncSymbolNative("min", ts.Type("float"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          var b = (float)frm.stack.PopRelease().num;
          var a = (float)frm.stack.PopRelease().num;
          frm.stack.Push(Val.NewNum(frm.vm, a > b ? b : a)); 
          return null;
        },
        new FuncArgSymbol("a", ts.Type("float")),
        new FuncArgSymbol("b", ts.Type("float"))
    );
    ts.globs.Define(fn);
  }

  public class Color
  {
    public float r;
    public float g;

    public override string ToString()
    {
      return "[r="+r+",g="+g+"]";
    }
  }

  public class ColorAlpha : Color
  {
    public float a;

    public override string ToString()
    {
      return "[r="+r+",g="+g+",a="+a+"]";
    }
  }

  public class ColorNested
  {
    public Color c = new Color();
  }

  ClassSymbolNative BindColor(TypeSystem ts)
  {
    var cl = new ClassSymbolNative("Color", null,
      delegate(VM.Frame frm, ref Val v) 
      { 
        v.obj = new Color();
      }
    );

    ts.globs.Define(cl);
    cl.Define(new FieldSymbol("r", ts.Type("float"),
      delegate(Val ctx, ref Val v)
      {
        var c = (Color)ctx.obj;
        v.SetNum(c.r);
      },
      delegate(ref Val ctx, Val v)
      {
        var c = (Color)ctx.obj;
        c.r = (float)v.num; 
        ctx.obj = c;
      }
    ));
    cl.Define(new FieldSymbol("g", ts.Type("float"),
      delegate(Val ctx, ref Val v)
      {
        var c = (Color)ctx.obj;
        v.SetNum(c.g);
      },
      delegate(ref Val ctx, Val v)
      {
        var c = (Color)ctx.obj;
        c.g = (float)v.num; 
        ctx.obj = c;
      }
    ));

    {
      var m = new FuncSymbolNative("Add", ts.Type("Color"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
        {
          var k = (float)frm.stack.PopRelease().num;
          var c = (Color)frm.stack.PopRelease().obj;

          var newc = new Color();
          newc.r = c.r + k;
          newc.g = c.g + k;

          var v = Val.NewObj(frm.vm, newc);
          frm.stack.Push(v);

          return null;
        },
        new FuncArgSymbol("k", ts.Type("float"))
      );

      cl.Define(m);
    }
    
    {
      var m = new FuncSymbolNative("mult_summ", ts.Type("float"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
        {
          var k = frm.stack.PopRelease().num;
          var c = (Color)frm.stack.PopRelease().obj;
          frm.stack.Push(Val.NewNum(frm.vm, (c.r * k) + (c.g * k)));
          return null;
        },
        new FuncArgSymbol("k", ts.Type("float"))
      );

      cl.Define(m);
    }
    
    {
      var fn = new FuncSymbolNative("mkcolor", ts.Type("Color"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
            var r = frm.stack.PopRelease().num;
            var c = new Color();
            c.r = (float)r;
            var v = Val.NewObj(frm.vm, c);
            frm.stack.Push(v);
            return null;
          },
        new FuncArgSymbol("r", ts.Type("float"))
      );

      ts.globs.Define(fn);
    }
    
    {
      var fn = new FuncSymbolNative("mkcolor_null", ts.Type("Color"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
            frm.stack.Push(frm.vm.Null);
            return null;
          }
      );

      ts.globs.Define(fn);
    }

    ts.globs.Define(new ArrayTypeSymbolT<Color>(ts, "ArrayT_Color", ts.Type("Color"), delegate() { return new List<Color>(); } ));

    return cl;
  }

  void BindColorAlpha(TypeSystem ts)
  {
    BindColor(ts);

    {
      var cl = new ClassSymbolNative("ColorAlpha", (ClassSymbol)ts.Type("Color").Get(),
        delegate(VM.Frame frm, ref Val v) 
        { 
          v.obj = new ColorAlpha();
        }
      );

      ts.globs.Define(cl);

      cl.Define(new FieldSymbol("a", ts.Type("float"),
        delegate(Val ctx, ref Val v)
        {
          var c = (ColorAlpha)ctx.obj;
          v.num = c.a;
        },
        delegate(ref Val ctx, Val v)
        {
          var c = (ColorAlpha)ctx.obj;
          c.a = (float)v.num; 
          ctx.obj = c;
        }
      ));

      {
        var m = new FuncSymbolNative("mult_summ_alpha", ts.Type("float"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var c = (ColorAlpha)frm.stack.PopRelease().obj;

            frm.stack.Push(Val.NewNum(frm.vm, (c.r * c.a) + (c.g * c.a)));

            return null;
          }
        );

        cl.Define(m);
      }
    }
  }

  public struct IntStruct
  {
    public int n;

    public static void Decode(Val v, ref IntStruct dst)
    {
      dst.n = (int)v._num;
    }

    public static void Encode(Val v, IntStruct src)
    {
      v._num = src.n;
    }
  }

  void BindIntStruct(TypeSystem ts)
  {
    {
      var cl = new ClassSymbolNative("IntStruct", null,
        delegate(VM.Frame frm, ref Val v) 
        { 
          var s = new IntStruct();
          IntStruct.Encode(v, s);
        }
      );

      ts.globs.Define(cl);

      cl.Define(new FieldSymbol("n", ts.Type("int"),
        delegate(Val ctx, ref Val v)
        {
          var s = new IntStruct();
          IntStruct.Decode(ctx, ref s);
          v.num = s.n;
        },
        delegate(ref Val ctx, Val v)
        {
          var s = new IntStruct();
          IntStruct.Decode(ctx, ref s);
          s.n = (int)v.num;
          IntStruct.Encode(ctx, s);
        }
      ));
    }
  }

  public class StringClass
  {
    public string str;
  }

  void BindStringClass(TypeSystem ts)
  {
    {
      var cl = new ClassSymbolNative("StringClass", null,
        delegate(VM.Frame frm, ref Val v) 
        { 
          v.obj = new StringClass();
        }
      );

      ts.globs.Define(cl);

      cl.Define(new FieldSymbol("str", ts.Type("string"),
        delegate(Val ctx, ref Val v)
        {
          var c = (StringClass)ctx.obj;
          v.str = c.str;
        },
        delegate(ref Val ctx, Val v)
        {
          var c = (StringClass)ctx.obj;
          c.str = v.str; 
          ctx.obj = c;
        }
      ));
    }
  }

  public struct MasterStruct
  {
    public StringClass child;
    public StringClass child2;
    public IntStruct child_struct;
    public IntStruct child_struct2;
  }

  void BindMasterStruct(TypeSystem ts)
  {
    BindStringClass(ts);
    BindIntStruct(ts);

    {
      var cl = new ClassSymbolNative("MasterStruct", null,
        delegate(VM.Frame frm, ref Val v) 
        { 
          var o = new MasterStruct();
          o.child = new StringClass();
          o.child2 = new StringClass();
          v.obj = o;
        }
      );

      ts.globs.Define(cl);

      cl.Define(new FieldSymbol("child", ts.Type("StringClass"),
        delegate(Val ctx, ref Val v)
        {
          var c = (MasterStruct)ctx.obj;
          v.SetObj(c.child);
        },
        delegate(ref Val ctx, Val v)
        {
          var c = (MasterStruct)ctx.obj;
          c.child = (StringClass)v._obj; 
          ctx.obj = c;
        }
      ));

      cl.Define(new FieldSymbol("child2", ts.Type("StringClass"),
        delegate(Val ctx, ref Val v)
        {
          var c = (MasterStruct)ctx.obj;
          v.obj = c.child2;
        },
        delegate(ref Val ctx, Val v)
        {
          var c = (MasterStruct)ctx.obj;
          c.child2 = (StringClass)v.obj; 
          ctx.obj = c;
        }
      ));

      cl.Define(new FieldSymbol("child_struct", ts.Type("IntStruct"),
        delegate(Val ctx, ref Val v)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct.Encode(v, c.child_struct);
        },
        delegate(ref Val ctx, Val v)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct s = new IntStruct();
          IntStruct.Decode(v, ref s);
          c.child_struct = s;
          ctx.obj = c;
        }
      ));

      cl.Define(new FieldSymbol("child_struct2", ts.Type("IntStruct"),
        delegate(Val ctx, ref Val v)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct.Encode(v, c.child_struct2);
        },
        delegate(ref Val ctx, Val v)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct s = new IntStruct();
          IntStruct.Decode(v, ref s);
          c.child_struct2 = s;
          ctx.obj = c;
        }
      ));

    }
  }

  public class Foo
  {
    public int hey;
    public List<Color> colors = new List<Color>();
    public Color sub_color = new Color();

    public void reset()
    {
      hey = 0;
      colors.Clear();
      sub_color = new Color();
    }
  }

  void BindFoo(TypeSystem ts)
  {
    {
      var cl = new ClassSymbolNative("Foo", null,
        delegate(VM.Frame frm, ref Val v) 
        { 
          v.obj = new Foo();
        }
      );
      ts.globs.Define(cl);

      cl.Define(new FieldSymbol("hey", ts.Type("int"),
        delegate(Val ctx, ref Val v)
        {
          var f = (Foo)ctx.obj;
          v.SetNum(f.hey);
        },
        delegate(ref Val ctx, Val v)
        {
          var f = (Foo)ctx.obj;
          f.hey = (int)v.num; 
          ctx.obj = f;
        }
      ));
      cl.Define(new FieldSymbol("colors", ts.Type("ArrayT_Color"),
        delegate(Val ctx, ref Val v)
        {
          var f = (Foo)ctx.obj;
          v.obj = f.colors;
        },
        delegate(ref Val ctx, Val v)
        {
          var f = (Foo)ctx.obj;
          f.colors = (List<Color>)v.obj;
        }
      ));
      cl.Define(new FieldSymbol("sub_color", ts.Type("Color"),
        delegate(Val ctx, ref Val v)
        {
          var f = (Foo)ctx.obj;
          v.obj = f.sub_color;
        },
        delegate(ref Val ctx, Val v)
        {
          var f = (Foo)ctx.obj;
          f.sub_color = (Color)v.obj; 
          ctx.obj = f;
        }
      ));
    }

    {
      var fn = new FuncSymbolNative("PassthruFoo", ts.Type("Foo"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
            frm.stack.Push(frm.stack.Pop());
            return null;
          },
          new FuncArgSymbol("foo", ts.Type("Foo"))
      );

      ts.globs.Define(fn);
    }
  }

  class CoroutineWaitTicks : ICoroutine
  {
    int c;
    int ticks_ttl;

    public void Tick(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> frames, ref BHS status)
    {
      //first time
      if(c++ == 0)
        ticks_ttl = (int)frm.stack.PopRelease().num;

      if(ticks_ttl-- > 0)
      {
        status = BHS.RUNNING;
      }
    }

    public void Cleanup(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> frames)
    {
      c = 0;
      ticks_ttl = 0;
    }
  }

  FuncSymbolNative BindWaitTicks(TypeSystem ts, StringBuilder log)
  {
    var fn = new FuncSymbolNative("WaitTicks", ts.Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          return CoroutinePool.New<CoroutineWaitTicks>(frm.vm);
        }, 
        new FuncArgSymbol("ticks", ts.Type("int"))
    );
    ts.globs.Define(fn);
    return fn;
  }

  public class Bar
  {
    public int Int;
    public float Flt;
    public string Str;
  }

  ClassSymbolNative BindBar(TypeSystem ts)
  {
    var cl = new ClassSymbolNative("Bar", null,
      delegate(VM.Frame frm, ref Val v) 
      { 
        v.SetObj(new Bar());
      }
    );

    ts.globs.Define(cl);
    cl.Define(new FieldSymbol("Int", ts.Type("int"),
      delegate(Val ctx, ref Val v)
      {
        var c = (Bar)ctx.obj;
        v.SetNum(c.Int);
      },
      delegate(ref Val ctx, Val v)
      {
        var c = (Bar)ctx.obj;
        c.Int = (int)v.num; 
        ctx.SetObj(c);
      }
    ));
    cl.Define(new FieldSymbol("Flt", ts.Type("float"),
      delegate(Val ctx, ref Val v)
      {
        var c = (Bar)ctx.obj;
        v.SetNum(c.Flt);
      },
      delegate(ref Val ctx, Val v)
      {
        var c = (Bar)ctx.obj;
        c.Flt = (float)v.num; 
        ctx.obj = c;
      }
    ));
    cl.Define(new FieldSymbol("Str", ts.Type("string"),
      delegate(Val ctx, ref Val v)
      {
        var c = (Bar)ctx.obj;
        v.SetStr(c.Str);
      },
      delegate(ref Val ctx, Val v)
      {
        var c = (Bar)ctx.obj;
        c.Str = (string)v.obj; 
        ctx.obj = c;
      }
    ));

    return cl;
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

  void BindRefC(TypeSystem ts, StringBuilder logs)
  {
    {
      var cl = new ClassSymbolNative("RefC", null,
        delegate(VM.Frame frm, ref Val v) 
        { 
          v.obj = new RefC(logs);
        }
      );
      {
        var vs = new bhl.FieldSymbol("refs", ts.Type("int"),
          delegate(Val ctx, ref Val v)
          {
            v.num = ((RefC)ctx.obj).refs;
          },
          //read only property
          null
        );
        cl.Define(vs);
      }
      ts.globs.Define(cl);
    }
  }

  void BindStartScriptInMgr(TypeSystem ts)
  {
    {
      var fn = new FuncSymbolNative("StartScriptInMgr", ts.Type("void"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) {
            int spawns = (int)frm.stack.PopRelease().num;
            var ptr = frm.stack.Pop();

            for(int i=0;i<spawns;++i)
              ScriptMgr.instance.Start(frm, (VM.FuncPtr)ptr.obj);

            ptr.Release();

            return null;
          },
        new FuncArgSymbol("script", ts.TypeFunc("void")),
        new FuncArgSymbol("spawns", ts.Type("int"))
      );

      ts.globs.Define(fn);
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

  static string TestDirPath()
  {
    string self_bin = System.Reflection.Assembly.GetExecutingAssembly().Location;
    return Path.GetDirectoryName(self_bin) + "/tmp/tests";
  }

  class TestImporter : IModuleImporter
  {
    public Dictionary<string, CompiledModule> mods = new Dictionary<string, CompiledModule>();

    public CompiledModule Import(string name)
    {
      return mods[name];
    }
  }

  static void CleanTestDir()
  {
    string dir = TestDirPath();
    if(Directory.Exists(dir))
      Directory.Delete(dir, true/*recursive*/);
  }

  static void NewTestFile(string path, string text, ref List<string> files)
  {
    string full_path = TestDirPath() + "/" + path;
    Directory.CreateDirectory(Path.GetDirectoryName(full_path));
    File.WriteAllText(full_path, text);
    files.Add(full_path);
  }

  static int ConstIdx(ModuleCompiler c, string str)
  {
    for(int i=0;i<c.Constants.Count;++i)
    {
      var cn = c.Constants[i];
      if(cn.type == EnumLiteral.STR && cn.str == str)
        return i;
    }
    throw new Exception("Constant not found: " + str);
  }

  static int ConstIdx(ModuleCompiler c, bool v)
  {
    for(int i=0;i<c.Constants.Count;++i)
    {
      var cn = c.Constants[i];
      if(cn.type == EnumLiteral.BOOL && cn.num == (v ? 1 : 0))
        return i;
    }
    throw new Exception("Constant not found: " + v);
  }

  static int ConstIdx(ModuleCompiler c, double num)
  {
    for(int i=0;i<c.Constants.Count;++i)
    {
      var cn = c.Constants[i];
      if(cn.type == EnumLiteral.NUM && cn.num == num)
        return i;
    }
    throw new Exception("Constant not found: " + num);
  }

  static int ConstNullIdx(ModuleCompiler c)
  {
    for(int i=0;i<c.Constants.Count;++i)
    {
      var cn = c.Constants[i];
      if(cn.type == EnumLiteral.NIL)
        return i;
    }
    throw new Exception("Constant null not found");
  }
}
  
