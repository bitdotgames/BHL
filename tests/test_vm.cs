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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 123);
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
    var num = fb.stack.PopRelease().num;

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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(true) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.stack.PopRelease().bval);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(false) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(!fb.stack.PopRelease().bval);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.UnaryNot)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(true) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(!fb.stack.PopRelease().bval);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.UnaryNot)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(false) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.stack.PopRelease().bval);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const("Hello") });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().str, "Hello");
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, true) })
      .Emit(Opcodes.TypeCast, new int[] { ConstIdx(c, "int") })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(true), new Const("int") });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 1);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 7) })
      .Emit(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(7), new Const("string") });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().str, "7");
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
    var str = Execute(vm, "test", Val.NewNum(vm, 3)).stack.PopRelease().str;
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
    var res = Execute(vm, "test", Val.NewNum(vm, 3.9)).stack.PopRelease().num;
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
    var res = Execute(vm, "test").stack.PopRelease().num;
    AssertEqual(res, 121);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastIntToAny2()
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
    var res = Execute(vm, "test").stack.PopRelease().num;
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
    var res = Execute(vm, "test").stack.PopRelease().str;
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
    var res = Execute(vm, "test", Val.NewNum(vm, 3)).stack.PopRelease().str;
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
    var res = Execute(vm, "test").stack.PopRelease().num;
    AssertEqual(res, 3);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImplicitIntArgsCastBindFunc()
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

    var globs = SymbolTable.VM_CreateBuiltins();
    BindMin(globs);

    var vm = MakeVM(bhl, globs);
    var res = Execute(vm, "test").stack.PopRelease().num;
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

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new FuncSymbolNative("func_with_def", globs.Type("float"), null, 
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var b = args_info.IsDefaultArgUsed(0) ? 2 : frm.stack.PopRelease().num;
            var a = frm.stack.PopRelease().num;

            frm.stack.Push(Val.NewNum(frm.vm, a + b));

            return null;
          },
          1);
      fn.Define(new FuncArgSymbol("a", globs.Type("float")));
      fn.Define(new FuncArgSymbol("b", globs.Type("float")));

      globs.Define(fn);
    }

    var vm = MakeVM(bhl, globs);
    var res = Execute(vm, "test", Val.NewNum(vm, 42)).stack.PopRelease().num;
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

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new FuncSymbolNative("func_with_def", globs.Type("float"), null, 
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var b = args_info.IsDefaultArgUsed(0) ? 2 : frm.stack.PopRelease().num;
            var a = frm.stack.PopRelease().num;

            frm.stack.Push(Val.NewNum(frm.vm, a + b));

            return null;
          },
          1);
      fn.Define(new FuncArgSymbol("a", globs.Type("float")));
      fn.Define(new FuncArgSymbol("b", globs.Type("float")));

      globs.Define(fn);
    }

    var vm = MakeVM(bhl, globs);
    var res = Execute(vm, "test", Val.NewNum(vm, 42)).stack.PopRelease().num;
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

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new FuncSymbolNative("func_with_def", globs.Type("float"), null, 
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var a = args_info.IsDefaultArgUsed(0) ? 14 : frm.stack.PopRelease().num;

            frm.stack.Push(Val.NewNum(frm.vm, a));

            return null;
          },
          1);
      fn.Define(new FuncArgSymbol("a", globs.Type("float")));

      globs.Define(fn);
    }

    var vm = MakeVM(bhl, globs);
    var res = Execute(vm, "test").stack.PopRelease().num;
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

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new FuncSymbolNative("func_with_def", globs.Type("float"), null, 
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var b = args_info.IsDefaultArgUsed(1) ? 2 : frm.stack.PopRelease().num;
            var a = args_info.IsDefaultArgUsed(0) ? 10 : frm.stack.PopRelease().num;

            frm.stack.Push(Val.NewNum(frm.vm, a + b));

            return null;
          },
          2);
      fn.Define(new FuncArgSymbol("a", globs.Type("int")));
      fn.Define(new FuncArgSymbol("b", globs.Type("int")));

      globs.Define(fn);
    }

    var vm = MakeVM(bhl, globs);
    var res = Execute(vm, "test", Val.NewNum(vm, 42)).stack.PopRelease().num;
    AssertEqual(res, 52);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestReturnFailureFirst()
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
  public void TestFailureInBindFunction()
  {
    string bhl = @"

    func float test() 
    {
      float val = foo()
      return val
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new FuncSymbolNative("foo", globs.Type("float"), null,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
            status = BHS.FAILURE; 
            return null;
          });
      globs.Define(fn);
    }

    var vm = MakeVM(bhl, globs);
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
    var num = Execute(vm, "test").stack.PopRelease().num;
    AssertEqual(num, 300);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.ReturnVal, new int[] { 2 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = Execute(vm, "test");
    var num = fb.stack.PopRelease().num;
    var str = fb.stack.PopRelease().str;
    AssertEqual(num, 300);
    AssertEqual(str, "bar");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestMultiReturnVarsOrder()
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
    var num = fb.stack.PopRelease().num;
    AssertEqual(num, 200);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.And)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(false), new Const(true) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.stack.PopRelease().bval == false);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Or)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(false), new Const(true) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.stack.PopRelease().bval);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.BitAnd)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(3), new Const(1) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 1);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.BitOr)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(3), new Const(4) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 7);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Mod)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(3), new Const(2)});

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 1);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.DeclVar, new int[] { 0, (int)Val.NUMBER })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 0);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 42);
    CommonChecks(vm);
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
    AssertEqual(Execute(vm, "test", Val.NewNum(vm, 3)).stack.PopRelease().num, 1);
    CommonChecks(vm);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 246);
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
    AssertEqual(Execute(vm, "test", Val.NewNum(vm, 3)).stack.PopRelease().num, 45);
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

    AssertError<UserError>(
      delegate() { 
        Compile(bhl);
      },
      "symbol not resolved"
    );
  }

  [IsTested()]
  public void TestDeclVarWithoutValue()
  {
    string bhl = @"
    func bool test()
    {
      int i
      string s
      bool b
      return (i == 0 && b == false && s == """")
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 3 + 1 /*args info*/})
      .Emit(Opcodes.DeclVar, new int[] { 0, (int)Val.NUMBER })
      .Emit(Opcodes.DeclVar, new int[] { 1, (int)Val.STRING })
      .Emit(Opcodes.DeclVar, new int[] { 2, (int)Val.BOOL })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Equal)
      .Emit(Opcodes.GetVar, new int[] { 2 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Equal)
      .Emit(Opcodes.And)
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.Equal)
      .Emit(Opcodes.And)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(0), new Const(false), new Const("") });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.stack.PopRelease().bval);
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
    AssertEqual(fb.stack.PopRelease().num, 42);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Equal)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { Const.Nil });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.stack.PopRelease().bval);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.UnaryNeg)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(1) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, -1);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(10), new Const(20)});

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 30);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(20), new Const(10) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 10);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Div)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })   
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(20), new Const(10) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 2);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Mul)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(10), new Const(20) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 200);
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
    var num = Execute(vm, "test", Val.NewNum(vm, 4)).stack.PopRelease().num;
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
    var num = Execute(vm, "test", Val.NewNum(vm, 4)).stack.PopRelease().num;
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
    var num = Execute(vm, "test", Val.NewNum(vm, 4)).stack.PopRelease().num;
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
    var num = Execute(vm, "test", Val.NewNum(vm, 4)).stack.PopRelease().num;
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
    var num = Execute(vm, "test", Val.NewNum(vm, 4)).stack.PopRelease().num;
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
    var num = Execute(vm, "test", Val.NewNum(vm, 2)).stack.PopRelease().num;
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

    var c = Compile(bhl);
    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.ArgVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.LTE)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var num = Execute(vm, "test", Val.NewNum(vm, 20)).stack.PopRelease().num;
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
    var num = Execute(vm, "test", Val.NewNum(vm, 20)).stack.PopRelease().num;
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
    var num = Execute(vm, "test", Val.NewNum(vm, 2)).stack.PopRelease().num;
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
    var num = Execute(vm, "test", Val.NewStr(vm, "b")).stack.PopRelease().num;
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
    var num = Execute(vm, "test", Val.NewNum(vm, 20)).stack.PopRelease().num;
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
    var num = Execute(vm, "test", Val.NewStr(vm, "b")).stack.PopRelease().num;
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
    var num = Execute(vm, "test", Val.NewStr(vm, "b")).stack.PopRelease().num;
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

    AssertEqual(c.Constants.Count, 260);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 33930);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const("Hello "), new Const("world !")});

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().str, "Hello world !");
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

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().str, "bar\n");
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

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().str, "bar\n\n");
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

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().str, "bar\\n");
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

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().str, "bar\\n\n");
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

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().str, "bar\\n\\n");
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

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().str, "bar\t");
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

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().str, "bar\t\t");
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

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().str, "bar\\t");
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

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().str, "bar\\t\t");
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

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().str, "bar\\t\\t");
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(10) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 20);
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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

    AssertError<UserError>(
      delegate() { 
        Compile(bhl);
      },
      " : incompatible variable type"
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

    AssertError<UserError>(
      delegate() { 
        Compile(bhl);
      },
      " : incompatible variable type"
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

    AssertError<UserError>(
      delegate() { 
        Compile(bhl);
      },
      " : incompatible variable type"
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

    AssertError<UserError>(
      delegate() { 
        Compile(bhl);
      },
      " : incompatible variable type"
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

    AssertError<UserError>(
      delegate() { 
        Compile(bhl);
      },
      " have incompatible types"
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.Mul)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(10), new Const(20), new Const(30) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 500);
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

    AssertError<UserError>(
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

    AssertError<UserError>(
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
      seq {
        int i = 1
        i = i + 1
      }
      seq {
        i = i + 2
      }
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Compile(bhl);
      },
      "i : symbol not resolved"
    );
  }

  [IsTested()]
  public void TestVarDeclMustBeInUpperScope2()
  {
    string bhl = @"

    func test() 
    {
      paral_all {
        seq {
          int i = 1
          seq {
            i = i + 1
          }
        }
        seq {
          if(i == 2) {
            suspend()
          }
        }
      }
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Compile(bhl);
      },
      "i : symbol not resolved"
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 123);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    var fn = BindTrace(globs, log);

    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler(globs)
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    var fn = new FuncSymbolNative("answer42", globs.Type("int"), null,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.stack.Push(Val.NewNum(frm.vm, 42));
          return null;
        } 
    );
    globs.Define(fn);

    var vm = MakeVM(bhl, globs);
    var num = Execute(vm, "test").stack.PopRelease().num;
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);

    AssertError<UserError>(
      delegate() { 
        Compile(bhl, globs);
      },
      "already defined symbol 'trace'"
    );
  }

  [IsTested()]
  public void TestFuncAlreadyDeclaredArg()
  {
    string bhl = @"
    func void foo(int k, int k)
    {
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Compile(bhl);
      },
      "@(2,29) k:<int>: already defined symbol 'k'"
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
    AssertEqual(41, num);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .Emit(Opcodes.GT)
      .Emit(Opcodes.CondJump, new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 4);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 100);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.GT)
      .Emit(Opcodes.CondJump, new int[] { 9 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Jump, new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 5);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 10);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.GT)
      .Emit(Opcodes.CondJump, new int[] { 9 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Jump, new int[] { 19 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.UnaryNeg)
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.GT)
      .Emit(Opcodes.CondJump, new int[] { 9 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 30) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Jump, new int[] { 18 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.GT)
      .Emit(Opcodes.CondJump, new int[] { 9 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Jump, new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 40) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 7);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 20);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      //__while__//
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.GTE)
      .Emit(Opcodes.CondJump, new int[] { 12 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Jump, new int[] { -22 })
      //__//
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(100), new Const(10) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 0);
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
    AssertEqual(3, Execute(vm, "test").stack.PopRelease().num);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      //__while__//
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.GTE)
      .Emit(Opcodes.CondJump, new int[] { 25 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 80) })
      .Emit(Opcodes.LT)
      .Emit(Opcodes.CondJump, new int[] { 3 })
      .Emit(Opcodes.Jump, new int[] { 3 })
      .Emit(Opcodes.Jump, new int[] { -35 })
      //__//
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 70);
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
    AssertEqual(fb.stack.PopRelease().num, 0);
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
      .Emit(Opcodes.InitFrame, new int[] { 2 + 1/*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      //__for__//
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.SetVar, new int[] { 1 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.LT)
      .Emit(Opcodes.CondJump, new int[] { 19 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 3 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.SetVar, new int[] { 1 })
      .Emit(Opcodes.Jump, new int[] { -29 })
      //__//
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(10), new Const(0), new Const(3), new Const(1) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 7);
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
    AssertEqual(fb.stack.PopRelease().num, 4);
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
      .Emit(Opcodes.InitFrame, new int[] { 3 + 2/*hidden vars*/ + 1/*cargs*/})
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "[]") }) 
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAddIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAddIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .Emit(Opcodes.SetVar, new int[] { 1 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 3 }) //assign tmp arr
      .Emit(Opcodes.DeclVar, new int[] { 4, (int)Val.NUMBER })//declare counter
      .Emit(Opcodes.DeclVar, new int[] { 2, (int)Val.NUMBER  })//declare iterator
      .Emit(Opcodes.GetVar, new int[] { 4 })
      .Emit(Opcodes.GetVar, new int[] { 3 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "[]"), ArrCountIdx })
      .Emit(Opcodes.LT) //compare counter and tmp arr size
      .Emit(Opcodes.CondJump, new int[] { 0x1d })
      //call arr idx method
      .Emit(Opcodes.GetVar, new int[] { 3 })
      .Emit(Opcodes.GetVar, new int[] { 4 })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAtIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 2 })
      .Emit(Opcodes.GetVar, new int[] { 1 }) //accum = accum + iterator var
      .Emit(Opcodes.GetVar, new int[] { 2 }) 
      .Emit(Opcodes.Add)
      .Emit(Opcodes.SetVar, new int[] { 1 })
      .Emit(Opcodes.Inc, new int[] { 4 }) //fast increment hidden counter
      .Emit(Opcodes.Jump, new int[] { -43 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
    ;

    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 4);
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
      .Emit(Opcodes.InitFrame, new int[] { 2 + 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      //__for__//
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.SetVar, new int[] { 1 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .Emit(Opcodes.LT)
      .Emit(Opcodes.CondJump, new int[] { 22 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Jump, new int[] { 12 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.SetVar, new int[] { 1 })
      .Emit(Opcodes.Jump, new int[] { -32 })
      //__//
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(10), new Const(1), new Const(3)});

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 9);
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
      .Emit(Opcodes.InitFrame, new int[] { 2 + 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      //__for__//
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.SetVar, new int[] { 1 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .Emit(Opcodes.LT)
      .Emit(Opcodes.CondJump, new int[] { 22 })
      .Emit(Opcodes.Jump, new int[] { 7 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.SetVar, new int[] { 1 })
      .Emit(Opcodes.Jump, new int[] { -32 })
      //__//
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(10), new Const(1), new Const(3)});

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 10);
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
    AssertEqual(fb.stack.PopRelease().num, 1);
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
    AssertEqual(fb.stack.PopRelease().num, 3);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.CondJump, new int[] { 14 })
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Jump, new int[] { 11 })
      .Emit(Opcodes.GetFunc, new int[] { 9 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(1), new Const(2), new Const(true) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.stack.PopRelease().bval);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .Emit(Opcodes.GT)
      .Emit(Opcodes.CondJump, new int[] { 14 })
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Jump, new int[] { 11 })
      .Emit(Opcodes.GetFunc, new int[] { 9 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 3);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 1);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      //test2
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.ArgVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      //test1
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.ArgVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.Constant, new int[] { 3 })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.GetFunc, new int[] { 14 })
      .Emit(Opcodes.Call, new int[] { 1 })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(98), new Const(1), new Const(5), new Const(30) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 125);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      //StringTest
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.ArgVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 1 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const("Hello"), new Const(" world !") });

    var vm = MakeVM(c);
    var fb = vm.Start("Test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().str, "Hello world !");
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
      //dummy
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      //lambda
      .Emit(Opcodes.Lambda, new int[] { 9 })
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.GetLambda, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(0), new Const(123) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 123);
    CommonChecks(vm);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      //dummy
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.CondJump, new int[] { 27 })
      //lambda
      .Emit(Opcodes.Lambda, new int[] { 9 })
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.GetLambda, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Jump,     new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(false), new Const(123), new Const(321) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 321);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      //dummy
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.CondJump, new int[] { 27 })
      //lambda
      .Emit(Opcodes.Lambda, new int[] { 9 })
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.GetLambda, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Jump,     new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(true), new Const(123), new Const(321) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 123);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      //dummy
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Return)
      //foo
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Lambda, new int[] { 9 })
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.GetLambda, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.GetFunc, new int[] { 3 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 123);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      //dummy
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.Lambda, new int[] { 9 })
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetFuncFromVar, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 123);
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
    AssertEqual(fb.stack.PopRelease().num, 246);
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
    AssertEqual(fb.stack.PopRelease().num, 41);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      //dummy
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.Lambda, new int[] { 9 })
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.CondJump, new int[] { 12 })
      .Emit(Opcodes.GetFuncFromVar, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Jump, new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123), new Const(false), new Const(321) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 321);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      //dummy
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.Lambda, new int[] { 9 })
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.CondJump, new int[] { 12 })
      .Emit(Opcodes.GetFuncFromVar, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Jump, new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123), new Const(true), new Const(321) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 123);
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
      //dummy
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      //lambda
      .Emit(Opcodes.Lambda, new int[] { 7 })
      .Emit(Opcodes.InitFrame, new int[] { 1+1 /*args info*/})
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.UseUpval, new int[] { 0, 0 })
      .Emit(Opcodes.GetLambda, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 123);
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
      //dummy
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 2 + 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.SetVar, new int[] { 1 })
      //lambda
      .Emit(Opcodes.Lambda, new int[] { 19 })
      .Emit(Opcodes.InitFrame, new int[] { 3+1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetVar, new int[] { 2 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.UseUpval, new int[] { 0, 1 })
      .Emit(Opcodes.UseUpval, new int[] { 1, 2 })
      .Emit(Opcodes.GetLambda, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(20), new Const(10), new Const(5) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 35);
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
      //dummy
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      //lambda
      .Emit(Opcodes.Lambda, new int[] { 40 })
      .Emit(Opcodes.InitFrame, new int[] { 1+1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Lambda, new int[] { 13 })
      .Emit(Opcodes.InitFrame, new int[] { 2+1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.UseUpval, new int[] { 0, 1 })
      .Emit(Opcodes.GetLambda, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.GetLambda, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123), new Const(321) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 321);
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1+1 /*args info*/}) 
      .Emit(Opcodes.Constant, new int[] { 0 })                  
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Lambda, new int[] { 23 })
      .Emit(Opcodes.InitFrame, new int[] { 2+1 /*args info*/})
      .Emit(Opcodes.Lambda, new int[] { 12 })
      .Emit(Opcodes.InitFrame, new int[] { 1+1 /*args info*/})
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.UseUpval, new int[] { 1, 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.UseUpval, new int[] { 0, 1 })
      .Emit(Opcodes.GetLambda, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
    AssertEqual(num, 3);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestClosure()
  {
    string bhl = @"

    func float test() 
    {
      //TODO: need more flexible types support for this:
      //float^(float)^(float)
      
      return func float^(float) (float a) {
        return func float (float b) { return a + b }
      }(2)(3)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").stack.PopRelease().num;
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
      //foo
      .Emit(Opcodes.InitFrame, new int[] { 3+1/*args info*/ })
      .Emit(Opcodes.ArgVar, new int[] { 0 })
      .Emit(Opcodes.DefArg, new int[] { 0, 9 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.ArgVar, new int[] { 1 })
      .Emit(Opcodes.DefArg, new int[] { 1, 4 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.ArgVar, new int[] { 2 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetVar, new int[] { 2 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 130 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(1), new Const(0), new Const(10) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 12);
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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

    AssertError<UserError>(
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

    AssertError<UserError>(
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

    AssertError<UserError>(
      delegate() { 
        Compile(bhl);
      },
      "k: already passed before"
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

    AssertError<UserError>(
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      //foo
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.DefArg, new int[] { 0, 4 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ArgVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 1 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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

    AssertError<UserError>(
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

    AssertError<UserError>(
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

    AssertError<UserError>(
      delegate() { 
        Compile(bhl);
      },
      "f: no such named argument"
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.ArgRef, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.ArgVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 1 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var num = Execute(vm, "test", Val.NewNum(vm, 3)).stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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

    AssertError<UserError>(
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
    var num = Execute(vm, "test", Val.NewNum(vm, 3)).stack.PopRelease().num;
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
    var num = Execute(vm, "test", Val.NewNum(vm, 3)).stack.PopRelease().num;
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
    var num = Execute(vm, "test", Val.NewNum(vm, 3)).stack.PopRelease().num;
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

    var globs = SymbolTable.VM_CreateBuiltins();

    {
      var fn = new FuncSymbolNative("func_with_ref", globs.Type("void"), null, 
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var b = frm.stack.Pop();
            var a = frm.stack.PopRelease().num;

            b.num = a * 2;
            b.Release();
            return null;
          }
          );
      fn.Define(new FuncArgSymbol("a", globs.Type("float")));
      fn.Define(new FuncArgSymbol("b", globs.Type("float"), true/*is ref*/));

      globs.Define(fn);
    }

    var vm = MakeVM(bhl, globs);
    var num = Execute(vm, "test", Val.NewNum(vm, 3)).stack.PopRelease().num;
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
    var num = Execute(vm, "test", Val.NewNum(vm, 3)).stack.PopRelease().num;
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
    var num = Execute(vm, "test", Val.NewNum(vm, 3)).stack.PopRelease().num;
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

    AssertError<UserError>(
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

    AssertError<UserError>(
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
    var num1 = fb.stack.PopRelease().num;
    var num2 = fb.stack.PopRelease().num;
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

    AssertError<UserError>(
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
      .UseInitCode()
      .Emit(Opcodes.ClassBegin, new int[] { ConstIdx(c, "Wow"), -1 })
      .Emit(Opcodes.ClassMember, new int[] { ConstIdx(c, "float"), ConstIdx(c, "c") })
      .Emit(Opcodes.ClassEnd)
      .Emit(Opcodes.ClassBegin, new int[] { ConstIdx(c, "Bar"), -1 })
      .Emit(Opcodes.ClassMember, new int[] { ConstIdx(c, "Wow"), ConstIdx(c, "w") })
      .Emit(Opcodes.ClassEnd)
      .UseByteCode()
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.ArgRef, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "Bar") }) 
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "Wow") }) 
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 4) })
      .Emit(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Wow"), 0 })
      .Emit(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Bar"), 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.RefAttr, new int[] { ConstIdx(c, "Bar"), 0 })
      .Emit(Opcodes.RefAttr, new int[] { ConstIdx(c, "Wow"), 0 })
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 1 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "Bar"), 0 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "Wow"), 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() {
        Compile(bhl, globs);
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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
    var num = Execute(vm, "test", args_inf).stack.PopRelease().num;
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
    var num = Execute(vm, "test", Val.NewNum(vm, 3)).stack.PopRelease().num;
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
    var num = Execute(vm, "test").stack.PopRelease().num;
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      //foo
      .Emit(Opcodes.InitFrame, new int[] { 3+1/*args info*/ })
      .Emit(Opcodes.ArgVar, new int[] { 0 })
      .Emit(Opcodes.DefArg, new int[] { 0, 9 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.ArgVar, new int[] { 1 })
      .Emit(Opcodes.DefArg, new int[] { 1, 4 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.ArgVar, new int[] { 2 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetVar, new int[] { 2 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.Constant, new int[] { 3 })
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 66 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(1), new Const(0), new Const(10), new Const(2) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 5);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "[]") }) 
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const("[]") });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    var lst = fb.stack.Pop();
    AssertEqual((lst.obj as ValList).Count, 0);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "[]") }) 
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, "test") })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAddIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAtIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const("[]"), new Const("test"), new Const(0) });

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().str, "test");
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
    var res = Execute(vm, "test").stack.PopRelease().str;
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      //mkarray
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "[]") }) 
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAddIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAddIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAtIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 1);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      //mkarray
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "[]") }) 
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAddIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAddIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrRemoveIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    var lst = fb.stack.Pop();
    AssertEqual((lst.obj as ValList).Count, 1);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      //mkarray
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "[]") }) 
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAddIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAddIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "[]"), ArrCountIdx })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 2);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "[]") }) 
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, "foo") })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAddIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, "tst") })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrSetIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, "bar") })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAddIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    var val = fb.stack.Pop();
    var lst = val.obj as ValList;
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      //foo
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.ArgVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAddIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "[]") }) 
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAddIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAddIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 1 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAtIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 100);
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
    AssertEqual(fb.stack.PopRelease().num, 100);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("suspend") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.Return)
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    var fn = BindWaitTicks(globs, log);

    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler(globs)
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("yield") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("yield") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("yield") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 1);
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
        seq {
          suspend() 
        }
        seq {
          yield()
          a = 1
        }
      }
      return a
    }
    ";

    var globs = SymbolTable.VM_CreateBuiltins();
    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.DeclVar, new int[] { 0, (int)Val.NUMBER })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.PARAL, 32})
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 9})
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("suspend") })
          .Emit(Opcodes.CallNative, new int[] { 0 })
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 15})
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("yield") })
          .Emit(Opcodes.CallNative, new int[] { 0 })
          .Emit(Opcodes.Constant, new int[] { 0 })
          .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 1);
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
        seq {
          yield()
          a = 1
        }
      }
      return a
    }
    ";

    var globs = SymbolTable.VM_CreateBuiltins();
    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.DeclVar, new int[] { 0, (int)Val.NUMBER })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.PARAL, 32})
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 9})
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("suspend") })
          .Emit(Opcodes.CallNative, new int[] { 0 })
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 15})
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("yield") })
          .Emit(Opcodes.CallNative, new int[] { 0 })
          .Emit(Opcodes.Constant, new int[] { 0 })
          .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 1);
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
        seq {
          foo()
        }
        seq {
          a = bar()
        }
      }
      return a
    }
    ";

    var globs = SymbolTable.VM_CreateBuiltins();
    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      //foo
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("suspend") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.Return)
      //bar
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("yield") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.DeclVar, new int[] { 0, (int)Val.NUMBER })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.PARAL, 28})
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 9})
          .Emit(Opcodes.GetFunc, new int[] { 0/*foo*/ })
          .Emit(Opcodes.Call, new int[] { 0 })
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 11})
          .Emit(Opcodes.GetFunc, new int[] { 12/*bar*/})
          .Emit(Opcodes.Call, new int[] { 0 })
          .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 1);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      //foo
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("suspend") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.Return)
      //bar
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("yield") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.DeclVar, new int[] { 0, (int)Val.NUMBER  })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.PARAL, 28})
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 9})
          .Emit(Opcodes.GetFunc, new int[] { 0/*foo*/ })
          .Emit(Opcodes.Call, new int[] { 0 })
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 11})
          .Emit(Opcodes.GetFunc, new int[] { 12/*bar*/ })
          .Emit(Opcodes.Call, new int[] { 0 })
          .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBasicParalAllRunning()
  {
    string bhl = @"
    func int test()
    {
      int a
      paral_all {
        seq {
          suspend() 
        }
        seq {
          yield()
          a = 1
        }
      }
      return a
    }
    ";

    var globs = SymbolTable.VM_CreateBuiltins();
    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.DeclVar, new int[] { 0, (int)Val.NUMBER  })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.PARAL_ALL, 32})
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 9})
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("suspend") })
          .Emit(Opcodes.CallNative, new int[] { 0 })
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 15})
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("yield") })
          .Emit(Opcodes.CallNative, new int[] { 0 })
          .Emit(Opcodes.Constant, new int[] { 0 })
          .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    for(int i=0;i<99;i++)
      AssertTrue(vm.Tick());
  }

  [IsTested()]
  public void TestBasicParalAllFinished()
  {
    string bhl = @"
    func int test()
    {
      int a
      paral_all {
        seq {
          yield()
          yield()
        }
        seq {
          yield()
          a = 1
        }
      }
      return a
    }
    ";

    var globs = SymbolTable.VM_CreateBuiltins();
    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.DeclVar, new int[] { 0, (int)Val.NUMBER  })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.PARAL_ALL, 41})
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 18})
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("yield") })
          .Emit(Opcodes.CallNative, new int[] { 0 })
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("yield") })
          .Emit(Opcodes.CallNative, new int[] { 0 })
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 15})
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("yield") })
          .Emit(Opcodes.CallNative, new int[] { 0 })
          .Emit(Opcodes.Constant, new int[] { 0 })
          .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 1);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var c = Compile(bhl, globs);

    var vm = MakeVM(c);
    var fb = vm.Start("test");

    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    var val = fb.stack.PopRelease();
    AssertEqual(3, val.num);
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

    var globs = SymbolTable.VM_CreateBuiltins();

    var c = Compile(bhl, globs);

    var vm = MakeVM(c); 
    vm.Start("test");
    AssertFalse(vm.Tick());

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
        seq {
          foo(1, ret_int(val: 2, ticks: ticky()))
          suspend()
        }
        foo(10, ret_int(val: 20, ticks: 2))
      }
    }
    ";

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);

    var c = Compile(bhl, globs);

    var vm = MakeVM(c);
    vm.Start("test");
    while(vm.Tick()) {}
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    var fn = BindTrace(globs, log);

    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
        .Emit(Opcodes.Constant, new int[] { 0 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;

    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("foobar", log.ToString());
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

    var globs = SymbolTable.VM_CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Compile(bhl, globs);
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

    var globs = SymbolTable.VM_CreateBuiltins();

    Compile(bhl, globs);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);

    var c = Compile(bhl, globs);

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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    var fn = BindTrace(globs, log);

    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
        .Emit(Opcodes.Constant, new int[] { 0 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
        .Emit(Opcodes.Constant, new int[] { 1 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;

    AssertEqual(c, expected);

    var vm = MakeVM(c);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    var fn = BindTrace(globs, log);

    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      //foo
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
        .Emit(Opcodes.Constant, new int[] { 0 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
        .Emit(Opcodes.Constant, new int[] { 2 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
        .Emit(Opcodes.Constant, new int[] { 3 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
        .Emit(Opcodes.Constant, new int[] { 4 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 5 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
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
      seq {
        defer {
          trace(""bar"")
        }
      }
      trace(""foo"")
    }
    ";

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    var fn = BindTrace(globs, log);

    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
        .Emit(Opcodes.Constant, new int[] { 0 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 17})
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
          .Emit(Opcodes.Constant, new int[] { 1 })
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
          .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;

    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("barfoohey", log.ToString());
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);

    var c = Compile(bhl, globs);

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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);

    var c = Compile(bhl, globs);

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

    AssertError<UserError>(
      delegate() {
        Compile(bhl1);
      },
      "@(4,26) \"Foo\":<string>, @(4,34) 1:<int> have incompatible types"
    );

    AssertError<UserError>(
      delegate() {
        Compile(bhl2);
      },
      "@(2,4) funcinttest(){returntrue?\"Foo\":\"Bar\"}:<int>, @(4,13) true?\"Foo\":\"Bar\":<string> have incompatible types"
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

    AssertEqual(Execute(vm, "test1").stack.PopRelease().num, 100500);
    AssertEqual(Execute(vm, "test2").stack.PopRelease().num, 100500);
    AssertEqual(Execute(vm, "test3", Val.NewNum(vm, 0)).stack.PopRelease().str, "default value");
    AssertEqual(Execute(vm, "test3", Val.NewNum(vm, 2)).stack.PopRelease().str, "second value");

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

    var globs = SymbolTable.CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);

    var vm = MakeVM(bhl, globs);

    AssertEqual(Execute(vm, "test1").stack.PopRelease().num, 0);

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

    AssertError<UserError>(
      delegate() {
        Compile(bhl1);
      },
      "extraneous input '++' expecting '}'"
    );

    AssertError<UserError>(
      delegate() {
        Compile(bhl2);
      },
      "operator ++ is not supported for string type"
    );

    AssertError<UserError>(
      delegate() {
        Compile(bhl3);
      },
      "extraneous input '++' expecting ')'"
    );

    AssertError<UserError>(
      delegate() {
        Compile(bhl4);
      },
      "extraneous input '++' expecting ';'"
    );

    AssertError<UserError>(
      delegate() {
        Compile(bhl5);
      },
      "symbol not resolved"
    );

    AssertError<UserError>(
      delegate() {
        Compile(bhl6);
      },
      "no viable alternative at input 'foo(i++'"
    );

    AssertError<UserError>(
      delegate() {
        Compile(bhl7);
      },
      "extraneous input '++' expecting ']'"
    );

    AssertError<UserError>(
      delegate() {
        Compile(bhl8);
      },
      "no viable alternative at input 'foo(i++'"
    );

    AssertError<UserError>(
      delegate() {
        Compile(bhl9);
      },
      "return value is missing"
    );

    AssertError<UserError>(
      delegate() {
        Compile(bhl10);
      },
      "extraneous input '++' expecting '}'"
    );

    AssertError<UserError>(
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

    AssertError<UserError>(
      delegate() {
        Compile(bhl1);
      },
      "extraneous input '--' expecting '}'"
    );

    AssertError<UserError>(
      delegate() {
        Compile(bhl2);
      },
      "operator -- is not supported for string type"
    );

    AssertError<UserError>(
      delegate() {
        Compile(bhl3);
      },
      "extraneous input '--' expecting ')'"
    );

    AssertError<UserError>(
      delegate() {
        Compile(bhl4);
      },
      "extraneous input '--' expecting ';'"
    );

    AssertError<UserError>(
      delegate() {
        Compile(bhl5);
      },
      "no viable alternative at input 'foo(i--'"
    );

    AssertError<UserError>(
      delegate() {
        Compile(bhl6);
      },
      "extraneous input '--' expecting ']'"
    );

    AssertError<UserError>(
      delegate() {
        Compile(bhl7);
      },
      "return value is missing"
    );

    AssertError<UserError>(
      delegate() {
        Compile(bhl8);
      },
      "extraneous input '--' expecting '}'"
    );

    AssertError<UserError>(
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

    var globs = SymbolTable.CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);

    var vm = MakeVM(bhl, globs);

    AssertEqual(Execute(vm, "test1").stack.PopRelease().num, 0);

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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);

    var c = Compile(bhl, globs);

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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    var fn = BindTrace(globs, log);

    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
        .Emit(Opcodes.Constant, new int[] { 0 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.PARAL, 34})
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
          .Emit(Opcodes.Constant, new int[] { 1 })
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
          .Emit(Opcodes.CallNative, new int[] { 1 })
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 13})
          .Emit(Opcodes.Constant, new int[] { 2 })
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
          .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 3 })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;

    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("wowbarfoohey", log.ToString());
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
        seq {
          yield()
          trace(""wow"")
        }
      }
      trace(""foo"")
    }
    ";

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    var fn = BindTrace(globs, log);

    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
        .Emit(Opcodes.Constant, new int[] { 0 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.PARAL, 43})
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
          .Emit(Opcodes.Constant, new int[] { 1 })
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
          .Emit(Opcodes.CallNative, new int[] { 1 })
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 22})
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("yield") })
          .Emit(Opcodes.CallNative, new int[] { 0 })
          .Emit(Opcodes.Constant, new int[] { 2 })
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
          .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 3 })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;

    AssertEqual(c, expected);

    var vm = MakeVM(c);
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
        seq {
          defer {
            trace(""2"")
          }
          suspend()
        }
        seq {
          defer {
            trace(""3"")
          }
          suspend()
        }
        seq {
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);

    var c = Compile(bhl, globs);

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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    var fn = BindTrace(globs, log);

    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
        .Emit(Opcodes.Constant, new int[] { 0 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.PARAL_ALL, 34})
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
          .Emit(Opcodes.Constant, new int[] { 1 })
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
          .Emit(Opcodes.CallNative, new int[] { 1 })
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 13})
          .Emit(Opcodes.Constant, new int[] { 2 })
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
          .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 3 })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;

    AssertEqual(c, expected);

    var vm = MakeVM(c);
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
        seq {
          yield()
          trace(""wow"")
        }
      }
      trace(""foo"")
    }
    ";

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    var fn = BindTrace(globs, log);

    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
        .Emit(Opcodes.Constant, new int[] { 0 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.PARAL_ALL, 43})
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
          .Emit(Opcodes.Constant, new int[] { 1 })
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
          .Emit(Opcodes.CallNative, new int[] { 1 })
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 22})
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("yield") })
          .Emit(Opcodes.CallNative, new int[] { 0 })
          .Emit(Opcodes.Constant, new int[] { 2 })
          .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
          .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 3 })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;

    AssertEqual(c, expected);

    var vm = MakeVM(c);
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
        seq {
          defer {
            trace(""2"")
          }
          suspend()
        }
        seq {
          defer {
            trace(""3"")
          }
          suspend()
        }
        seq {
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);

    var c = Compile(bhl, globs);

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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    var fn = BindTrace(globs, log);

    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
        .Emit(Opcodes.Constant, new int[] { 0 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
      //lambda
      .Emit(Opcodes.Lambda, new int[] { 33 } )
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
        .Emit(Opcodes.Constant, new int[] { 1 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
        .Emit(Opcodes.Constant, new int[] { 2 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.GetLambda, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 3 })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;

    AssertEqual(c, expected);

    var vm = MakeVM(c);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .UseInitCode()
      .Emit(Opcodes.ClassBegin, new int[] { ConstIdx(c, "Foo"), -1 })
      .Emit(Opcodes.ClassEnd)
      .UseByteCode()
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "Foo") }) 
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.NotEqual)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.stack.PopRelease().bval);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    var fn = BindTrace(globs, log);
    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .UseInitCode()
      .Emit(Opcodes.ClassBegin, new int[] { ConstIdx(c, "Foo"), -1 })
      .Emit(Opcodes.ClassMember, new int[] { ConstIdx(c, "int"), ConstIdx(c, "Int") })
      .Emit(Opcodes.ClassMember, new int[] { ConstIdx(c, "float"), ConstIdx(c, "Flt") })
      .Emit(Opcodes.ClassMember, new int[] { ConstIdx(c, "string"), ConstIdx(c, "Str") })
      .Emit(Opcodes.ClassEnd)
      .UseByteCode()
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "Foo") }) 
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.SetAttr, new int[] { ConstIdx(c, "Foo"), 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 14.2) })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.SetAttr, new int[] { ConstIdx(c, "Foo"), 1 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.SetAttr, new int[] { ConstIdx(c, "Foo"), 2 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 0 })
      .Emit(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 1 })
      .Emit(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 2 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("10;14.2;Hey", log.ToString());
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
      
    func void test() 
    {
      Foo f = {Int: 10, Flt: 14.2, Str: ""Hey""}
      trace((string)f.Int + "";"" + (string)f.Flt + "";"" + f.Str)
    }
    ";

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    var fn = BindTrace(globs, log);
    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .UseInitCode()
      .Emit(Opcodes.ClassBegin, new int[] { ConstIdx(c, "Foo"), -1 })
      .Emit(Opcodes.ClassMember, new int[] { ConstIdx(c, "int"), ConstIdx(c, "Int") })
      .Emit(Opcodes.ClassMember, new int[] { ConstIdx(c, "float"), ConstIdx(c, "Flt") })
      .Emit(Opcodes.ClassMember, new int[] { ConstIdx(c, "string"), ConstIdx(c, "Str") })
      .Emit(Opcodes.ClassEnd)
      .UseByteCode()
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "Foo") }) 
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Foo"), 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 14.2) })
      .Emit(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Foo"), 1 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .Emit(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Foo"), 2 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 0 })
      .Emit(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 1 })
      .Emit(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 2 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("10;14.2;Hey", log.ToString());
    CommonChecks(vm);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    var fn = BindTrace(globs, log);
    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .UseInitCode()
      .Emit(Opcodes.ClassBegin, new int[] { ConstIdx(c, "Foo"), -1 })
      .Emit(Opcodes.ClassMember, new int[] { ConstIdx(c, "int"), ConstIdx(c, "Int") })
      .Emit(Opcodes.ClassMember, new int[] { ConstIdx(c, "float"), ConstIdx(c, "Flt") })
      .Emit(Opcodes.ClassMember, new int[] { ConstIdx(c, "string"), ConstIdx(c, "Str") })
      .Emit(Opcodes.ClassEnd)
      .UseByteCode()
      .Emit(Opcodes.InitFrame, new int[] { 2 + 1 /*args info*/})
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "[]") }) 
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "Foo") }) 
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Foo"), 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 14.2) })
      .Emit(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Foo"), 1 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .Emit(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Foo"), 2 })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAddInplaceIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAtIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 1 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 0 })
      .Emit(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 1 })
      .Emit(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "Foo"), 2 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("10;14.2;Hey", log.ToString());
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);
    var c = Compile(bhl, globs);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("2;15.1;Foo-10;14.2;Hey", log.ToString());
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

    var globs = SymbolTable.VM_CreateBuiltins();
    BindBar(globs);
    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "Bar") }) 
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstNullIdx(c) })
      .Emit(Opcodes.NotEqual)
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.stack.PopRelease().bval);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    var fn = BindTrace(globs, log);
    BindBar(globs);
    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "Bar") }) 
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.SetAttr, new int[] { ConstIdx(c, "Bar"), 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 14.5) })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.SetAttr, new int[] { ConstIdx(c, "Bar"), 1 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.SetAttr, new int[] { ConstIdx(c, "Bar"), 2 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "Bar"), 0 })
      .Emit(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "Bar"), 1 })
      .Emit(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "Bar"), 2 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("10;14.5;Hey", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeClassMembersAccess()
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

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    var vm = MakeVM(bhl, globs);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).stack.PopRelease().num;
    AssertEqual(res, 202);
    CommonChecks(vm);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    BindColorAlpha(globs);

    var vm = MakeVM(bhl, globs);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).stack.PopRelease().num;
    AssertEqual(res, 1202);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    var fn = BindTrace(globs, log);
    BindBar(globs);
    var c = Compile(bhl, globs);

    var expected = 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 2 + 1 /*args info*/})
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "[]") }) 
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "Bar") }) 
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Bar"), 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 14.5) })
      .Emit(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Bar"), 1 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .Emit(Opcodes.SetAttrInplace, new int[] { ConstIdx(c, "Bar"), 2 })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAddInplaceIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAtIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 1 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "Bar"), 0 })
      .Emit(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "Bar"), 1 })
      .Emit(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "Bar"), 2 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("10;14.5;Hey", log.ToString());
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    BindColor(globs);

    var vm = MakeVM(bhl, globs);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).stack.PopRelease().num;
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

    var globs = SymbolTable.VM_CreateBuiltins();

    BindColor(globs);

    var vm = MakeVM(bhl, globs);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).stack.PopRelease().num;
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
    var res = Execute(vm, "test").stack.PopRelease().bval;
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
    var res = Execute(vm, "test").stack.PopRelease().bval;
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Compile(bhl, globs);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Compile(bhl, globs);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Compile(bhl, globs);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Compile(bhl, globs);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Compile(bhl, globs);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Compile(bhl, globs);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Compile(bhl, globs);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Compile(bhl, globs);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Compile(bhl, globs);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Compile(bhl, globs);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Compile(bhl, globs);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Compile(bhl, globs);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Compile(bhl, globs);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Compile(bhl, globs);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    var cl = BindColor(globs);
    var op = new FuncSymbolNative("+", globs.Type("Color"), null,
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
      }
    );
    op.Define(new FuncArgSymbol("r", globs.Type("Color")));
    cl.OverloadBinaryOperator(op);

    var vm = MakeVM(bhl, globs);
    var res = (Color)Execute(vm, "test").stack.PopRelease().obj;
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    var cl = BindColor(globs);
    var op = new FuncSymbolNative("*", globs.Type("Color"), null,
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
      }
    );
    op.Define(new FuncArgSymbol("k", globs.Type("float")));
    cl.OverloadBinaryOperator(op);

    var vm = MakeVM(bhl, globs);
    var res = (Color)Execute(vm, "test").stack.PopRelease().obj;
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    var cl = BindColor(globs);
    {
      var op = new FuncSymbolNative("*", globs.Type("Color"), null,
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
      }
      );
      op.Define(new FuncArgSymbol("k", globs.Type("float")));
      cl.OverloadBinaryOperator(op);
    }
    
    {
      var op = new FuncSymbolNative("+", globs.Type("Color"), null,
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
      }
      );
      op.Define(new FuncArgSymbol("r", globs.Type("Color")));
      cl.OverloadBinaryOperator(op);
    }

    var vm = MakeVM(bhl, globs);
    var res = (Color)Execute(vm, "test").stack.PopRelease().obj;
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);
    
    var cl = BindColor(globs);
    var op = new FuncSymbolNative("==", globs.Type("bool"), null,
      delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
      {
        var arg = (Color)frm.stack.PopRelease().obj;
        var c = (Color)frm.stack.PopRelease().obj;

        var v = Val.NewBool(frm.vm, c.r == arg.r && c.g == arg.g);
        frm.stack.Push(v);

        return null;
      }
    );
    op.Define(new FuncArgSymbol("arg", globs.Type("Color")));
    cl.OverloadBinaryOperator(op);

    var vm = MakeVM(bhl, globs);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    
    var cl = BindColor(globs);
    var op = new FuncSymbolNative("*", globs.Type("Color"), null, null);
    op.Define(new FuncArgSymbol("k", globs.Type("float")));
    cl.OverloadBinaryOperator(op);

    AssertError<UserError>(
      delegate() { 
        Compile(bhl, globs);
      },
      @"<string> have incompatible types"
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
    AssertEqual(fb.stack.PopRelease().num, 123);
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
    var num = Execute(vm, "test", Val.NewNum(vm, 3), Val.NewNum(vm, 7)).stack.PopRelease().num; 
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
    var num = Execute(vm, "test", Val.NewNum(vm, 3), Val.NewNum(vm, 7)).stack.PopRelease().num; 
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
    AssertEqual(fb1.stack.PopRelease().num, 123);
    AssertEqual(fb2.stack.PopRelease().num, 123);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);

    var c = Compile(bhl, globs);

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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);

    var c = Compile(bhl, globs);

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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);

    var c = Compile(bhl, globs);

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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);

    var c = Compile(bhl, globs);

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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);

    var c = Compile(bhl, globs);

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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);

    var importer = new ModuleImporter(CompileFiles(files, globs));


    var vm = new VM(globs: globs, importer: importer);

    vm.ImportModule("bhl1");
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
    vm.RegisterModule(new CompiledModule(c.Module.name, c.GetByteCode(), c.Constants, c.Func2Ip));
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 123);
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
      .UseInitCode()
      .Emit(Opcodes.Import, new int[] { 0 })
      .UseByteCode()
      .Emit(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.GetFuncImported, new int[] { 0, 0 })
      .Emit(Opcodes.Call, new int[] { 1 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
    );
    AssertEqual(importer.Import("bhl2"), 
      new ModuleCompiler()
      .UseInitCode()
      .Emit(Opcodes.Import, new int[] { 0 })
      .UseByteCode()
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.ArgVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetFuncImported, new int[] { 0, 0 })
      .Emit(Opcodes.Call, new int[] { 1 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
    );
    AssertEqual(importer.Import("bhl3"), 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .Emit(Opcodes.ArgVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal, new int[] { 1 })
      .Emit(Opcodes.Return)
    );

    var vm = new VM(globs: null, importer: importer);
    vm.ImportModule("bhl1");
    var fb = vm.Start("bhl1");
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 23);
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    BindTrace(globs, log);

    var importer = new ModuleImporter(CompileFiles(files, globs));

    var vm = new VM(globs: globs, importer: importer);

    vm.ImportModule("bhl2");
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("10;14.2;Hey", log.ToString());
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var trace = new List<VM.TraceItem>();
    {
      var fn = new FuncSymbolNative("record_callstack", globs.Type("void"), null,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          frm.fb.GetStackTrace(trace); 
          return null;
        });
      globs.Define(fn);
    }

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var importer = new ModuleImporter(CompileFiles(files, globs));

    var vm = new VM(globs: globs, importer: importer);
    vm.ImportModule("bhl1");
    var fb = vm.Start("test", Val.NewNum(vm, 3));
    AssertFalse(vm.Tick());
    AssertEqual(fb.stack.PopRelease().num, 3);

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

    var globs = SymbolTable.VM_CreateBuiltins();

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);

    var importer = new ModuleImporter(CompileFiles(files, globs));

    var vm = new VM(globs: globs, importer: importer);
    vm.ImportModule("bhl1");
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

    var globs = SymbolTable.VM_CreateBuiltins();
    var info = new Dictionary<VM.Fiber, List<VM.TraceItem>>();
    {
      var fn = new FuncSymbolNative("throw", globs.Type("void"), null,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          //emulating null reference
          frm = null;
          frm.fb = null;
          return null;
        });
      globs.Define(fn);
    }

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var importer = new ModuleImporter(CompileFiles(files, globs));

    var vm = new VM(globs: globs, importer: importer);
    vm.ImportModule("bhl1");
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
    AssertEqual(fb.stack.PopRelease().num, 610);
    CommonChecks(vm);
    Console.WriteLine("bhl vm fib ticks: {0}", stopwatch.ElapsedTicks);
  }

  ///////////////////////////////////////

  static ModuleCompiler MakeCompiler(GlobalScope globs = null)
  {
    globs = globs == null ? SymbolTable.VM_CreateBuiltins() : globs;
    //NOTE: we don't want to affect the original globs
    var globs_copy = globs.Clone();
    return new ModuleCompiler(globs_copy);
  }

  Stream CompileFiles(List<string> files, GlobalScope globs = null)
  {
    globs = globs == null ? SymbolTable.VM_CreateBuiltins() : globs;
    //NOTE: we don't want to affect the original globs
    var globs_copy = globs.Clone();

    var conf = new BuildConf();
    conf.compile_fmt = CompileFormat.VM;
    conf.module_fmt = ModuleBinaryFormat.FMT_BIN;
    conf.globs = globs_copy;
    conf.files = files;
    conf.res_file = TestDirPath() + "/result.bin";
    conf.inc_dir = TestDirPath();
    conf.cache_dir = TestDirPath() + "/cache";
    conf.err_file = TestDirPath() + "/error.log";
    conf.use_cache = false;
    conf.debug = true;

    var bld = new Build();
    int res = bld.Exec(conf);
    if(res != 0)
      throw new UserError(File.ReadAllText(conf.err_file));

    return new MemoryStream(File.ReadAllBytes(conf.res_file));
  }

  ModuleCompiler Compile(string bhl, GlobalScope globs = null, bool show_ast = false, bool show_bytes = false)
  {
    globs = globs == null ? SymbolTable.VM_CreateBuiltins() : globs;
    //NOTE: we don't want to affect the original globs
    var globs_copy = globs.Clone();

    var mdl = new bhl.Module("", "");
    var mreg = new ModuleRegistry();
    var ast = Src2AST(bhl, mdl, mreg, globs_copy);
    if(show_ast)
      Util.ASTDump(ast);
    var c  = new ModuleCompiler(globs_copy, ast, mdl.path);
    c.Compile();
    if(show_bytes)
      Dump(c);
    return c;
  }

  AST Src2AST(string src, bhl.Module mdl, ModuleRegistry mreg, GlobalScope globs = null)
  {
    globs = globs == null ? SymbolTable.VM_CreateBuiltins() : globs;

    var ms = new MemoryStream();
    Frontend.Source2Bin(mdl, src.ToStream(), ms, globs, mreg);
    ms.Position = 0;

    return Util.Bin2Meta<AST_Module>(ms);
  }

  void CommonChecks(VM vm, bool check_frames = true, bool check_fibers = true, bool check_instructions = true)
  {
    //for extra debug
    if(vm.vals_pool.Allocs != vm.vals_pool.Free)
      Console.WriteLine(vm.vals_pool.Dump());

    AssertEqual(vm.vals_pool.Allocs, vm.vals_pool.Free);
    AssertEqual(vm.vlsts_pool.Allocs, vm.vlsts_pool.Free);
    AssertEqual(vm.vdicts_pool.Allocs, vm.vdicts_pool.Free);
    if(check_frames)
      AssertEqual(vm.frames_pool.Allocs, vm.frames_pool.Free);
    if(check_fibers)
      AssertEqual(vm.fibers_pool.Allocs, vm.fibers_pool.Free);
    if(check_instructions)
      AssertEqual(vm.instr_pool.Allocs, vm.instr_pool.Free);
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
    var bs = c.GetByteCode();
    ModuleCompiler.OpDefinition op = null;
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
    AssertEqual(ca.GetModule(), cb.GetModule());
  }

  public static void AssertEqual(CompiledModule ca, ModuleCompiler cb)
  {
    AssertEqual(ca, cb.GetModule());
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
    Dump(c.GetModule());
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

  static void Dump(byte[] a)
  {
    string res = "";

    ModuleCompiler.OpDefinition aop = null;
    int aop_size = 0;

    for(int i=0;i<a?.Length;i++)
    {
      res += string.Format("{1:00} 0x{0:x2} {0}", a[i], i);
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
        res += "(" + aop.name.ToString() + ")";
        if(aop_size == 0)
          aop = null;
      }
      res += "\n";
    }

    Console.WriteLine(res);
  }

  static bool CompareCode(byte[] a, byte[] b, out string cmp)
  {
    ModuleCompiler.OpDefinition aop = null;
    int aop_size = 0;
    ModuleCompiler.OpDefinition bop = null;
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
    var vm = new VM(c.Globs);
    var m = c.GetModule();
    vm.RegisterModule(m);
    return vm;
  }

  VM MakeVM(string bhl, GlobalScope globs = null, bool show_ast = false, bool show_bytes = false)
  {
    return MakeVM(Compile(bhl, globs, show_ast: show_ast, show_bytes: show_bytes));
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
    while(vm.Tick()) {}
    return fb;
  }

  static int PredictOpcodeSize(ModuleCompiler.OpDefinition op, byte[] bytes, int start_pos)
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
      return GenericArrayTypeSymbol.IDX_Add;
    }
  }

  public static int ArrSetIdx {
    get {
      return GenericArrayTypeSymbol.IDX_SetAt;
    }
  }

  public static int ArrRemoveIdx {
    get {
      return GenericArrayTypeSymbol.IDX_RemoveAt;
    }
  }

  public static int ArrCountIdx {
    get {
      return GenericArrayTypeSymbol.IDX_Count;
    }
  }

  public static int ArrAtIdx {
    get {
      return GenericArrayTypeSymbol.IDX_At;
    }
  }

  public static int ArrAddInplaceIdx {
    get {
      return GenericArrayTypeSymbol.IDX_AddInplace;
    }
  }

  static int H(string name)
  {
    return (int)(new HashedName(name).n1);
  }

  FuncSymbolNative BindTrace(GlobalScope globs, StringBuilder log)
  {
    var fn = new FuncSymbolNative("trace", globs.Type("void"), null,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          string str = frm.stack.PopRelease().str;
          log.Append(str);
          return null;
        } 
    );
    fn.Define(new FuncArgSymbol("str", globs.Type("string")));
    globs.Define(fn);
    return fn;
  }

  //simple console outputting version
  void BindLog(GlobalScope globs)
  {
    {
      var fn = new FuncSymbolNative("log", globs.Type("void"), null,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
            string str = frm.stack.PopRelease().str;
            Console.WriteLine(str); 
            return null;
          } 
      );
      fn.Define(new FuncArgSymbol("str", globs.Type("string")));
      globs.Define(fn);
    }
  }

  void BindMin(GlobalScope globs)
  {
    var fn = new FuncSymbolNative("min", globs.Type("float"), null,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          var b = (float)frm.stack.PopRelease().num;
          var a = (float)frm.stack.PopRelease().num;
          frm.stack.Push(Val.NewNum(frm.vm, a > b ? b : a)); 
          return null;
        } 
    );
    fn.Define(new FuncArgSymbol("a", globs.Type("float")));
    fn.Define(new FuncArgSymbol("b", globs.Type("float")));
    globs.Define(fn);
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

  ClassSymbolNative BindColor(GlobalScope globs)
  {
    var cl = new ClassSymbolNative("Color", null, null,
      delegate(VM.Frame frm, ref Val v) 
      { 
        v.obj = new Color();
      }
    );

    globs.Define(cl);
    cl.Define(new FieldSymbol("r", globs.Type("float"), null, null, null,
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
    cl.Define(new FieldSymbol("g", globs.Type("float"), null, null, null,
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

    //{
    //  var m = new FuncSymbolSimpleNative("Add", globs.Type("Color"),
    //    delegate()
    //    {
    //      var interp = Interpreter.instance;

    //      var k = (float)interp.PopValue().num;
    //      var c = (Color)interp.PopValue().obj;

    //      var newc = new Color();
    //      newc.r = c.r + k;
    //      newc.g = c.g + k;

    //      var dv = DynVal.NewObj(newc);
    //      interp.PushValue(dv);

    //      return BHS.SUCCESS;
    //    }
    //  );
    //  m.Define(new FuncArgSymbol("k", globs.Type("float")));

    //  cl.Define(m);
    //}
    //
    {
      var m = new FuncSymbolNative("mult_summ", globs.Type("float"), null,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
        {
          var k = frm.stack.PopRelease().num;
          var c = (Color)frm.stack.PopRelease().obj;

          frm.stack.Push(Val.NewNum(frm.vm, (c.r * k) + (c.g * k)));

          return null;
        }
      );
      m.Define(new FuncArgSymbol("k", globs.Type("float")));

      cl.Define(m);
    }
    //
    //{
    //  var fn = new FuncSymbolNative("mkcolor", globs.Type("Color"),
    //      delegate() { return new MkColorNode(); }
    //  );
    //  fn.Define(new FuncArgSymbol("r", globs.Type("float")));

    //  globs.Define(fn);
    //}
    //
    //{
    //  var fn = new FuncSymbolSimpleNative("mkcolor_null", globs.Type("Color"),
    //      delegate() { 
    //        var interp = Interpreter.instance;
    //        var dv = DynVal.New();
    //        dv.obj = null;
    //        interp.PushValue(dv);
    //        return BHS.SUCCESS;
    //      }
    //  );

    //  globs.Define(fn);
    //}

    return cl;
  }

  void BindColorAlpha(GlobalScope globs, bool bind_parent = true)
  {
    if(bind_parent)
      BindColor(globs);

    {
      var cl = new ClassSymbolNative("ColorAlpha", globs.Type("Color"), null,
        delegate(VM.Frame frm, ref Val v) 
        { 
          v.obj = new ColorAlpha();
        }
      );

      globs.Define(cl);

      cl.Define(new FieldSymbol("a", globs.Type("float"), null, null, null,
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

      //{
      //  var m = new FuncSymbolSimpleNative("mult_summ_alpha", globs.Type("float"),
      //    delegate()
      //    {
      //      var interp = Interpreter.instance;

      //      var c = (ColorAlpha)interp.PopValue().obj;

      //      interp.PushValue(DynVal.NewNum((c.r * c.a) + (c.g * c.a)));

      //      return BHS.SUCCESS;
      //    }
      //  );

      //  cl.Define(m);
      //}
    }
  }

  class CoroutineWaitTicks : IInstruction
  {
    int c;
    int ticks_ttl;

    public void Tick(VM.Frame frm, ref BHS status)
    {
      //first time
      if(c++ == 0)
        ticks_ttl = (int)frm.stack.PopRelease().num;

      if(ticks_ttl-- > 0)
        status = BHS.RUNNING;
    }

    public void Cleanup(VM vm)
    {
      c = 0;
      ticks_ttl = 0;
    }
  }

  FuncSymbolNative BindWaitTicks(GlobalScope globs, StringBuilder log)
  {
    var fn = new FuncSymbolNative("WaitTicks", globs.Type("void"), null,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          return Instructions.New<CoroutineWaitTicks>(frm.vm);
        } 
    );
    fn.Define(new FuncArgSymbol("ticks", globs.Type("int")));
    globs.Define(fn);
    return fn;
  }

  public class Bar
  {
    public int Int;
    public float Flt;
    public string Str;
  }

  ClassSymbolNative BindBar(GlobalScope globs)
  {
    var cl = new ClassSymbolNative("Bar", null,
      delegate(VM.Frame frm, ref Val v) 
      { 
        v.SetObj(new Bar());
      }
    );

    globs.Define(cl);
    cl.Define(new FieldSymbol("Int", globs.Type("int"), null, null, null,
      delegate(Val ctx, ref Val v)
      {
        var c = (Bar)ctx.obj;
        v.SetNum(c.Int);
      },
      delegate(ref Val ctx, Val v)
      {
        var c = (Bar)ctx.obj;
        c.Int = (int)v.num; 
        ctx.obj = c;
      }
    ));
    cl.Define(new FieldSymbol("Flt", globs.Type("float"), null, null, null,
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
    cl.Define(new FieldSymbol("Str", globs.Type("string"), null, null, null,
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
  
