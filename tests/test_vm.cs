using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using bhl;

public class BHL_TestVM : BHL_TestBase
{
  [IsTested()]
  public void TestNumConstant()
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 123);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBoolConstant()
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(true) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertTrue(vm.PopRelease().bval);
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
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, true) })
      .Emit(Opcodes.TypeCast, new int[] { ConstIdx(c, "int") })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(true), new Const("int") });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 1);
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
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 7) })
      .Emit(Opcodes.TypeCast, new int[] { ConstIdx(c, "string") })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(7), new Const("string") });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().str, "7");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStringConstant()
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const("Hello") });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().str, "Hello");
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.And)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(false), new Const(true) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertTrue(vm.PopRelease().bval == false);
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Or)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(false), new Const(true) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertTrue(vm.PopRelease().bval);
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.BitAnd)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(3), new Const(1) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 1);
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.BitOr)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(3), new Const(4) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 7);
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Mod)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(3), new Const(2)});

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 1);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Equal)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { Const.Nil });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertTrue(vm.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUnaryNot()
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.UnaryNot)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(true) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertTrue(vm.PopRelease().bval == false);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.UnaryNeg)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(1) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, -1);
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(10), new Const(20)});

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 30);
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
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 33930);
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const("Hello "), new Const("world !")});

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().str, "Hello world !");
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(10) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 20);
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(20), new Const(10) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 10);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDiv()
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Div)
      .Emit(Opcodes.ReturnVal)   
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(20), new Const(10) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestMul()
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Mul)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(10), new Const(20) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 200);
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.Mul)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(10), new Const(20), new Const(30) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 500);
    CommonChecks(vm);
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 123);
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
    ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual("foo", log.ToString());
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
    ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "[]") }) 
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const("[]") });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    var lst = vm.Stack.PopFast();
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
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
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const("[]"), new Const("test"), new Const(0) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().str, "test");
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
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
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAtIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 1);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
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
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    var lst = vm.Stack.PopFast();
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
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
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.GetAttr, new int[] { ConstIdx(c, "[]"), ArrCountIdx })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 2);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
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
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    var val = vm.Stack.PopFast();
    var lst = val.obj as ValList;
    AssertEqual(lst.Count, 2);
    AssertEqual(lst[0].str, "tst");
    AssertEqual(lst[1].str, "bar");
    val.Release();
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassArrayToFunctionByValue()
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

      return a[0]
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      //foo
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.ArgVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAddIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 })
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
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .Emit(Opcodes.GetMethodNative, new int[] { ArrAtIdx, ConstIdx(c, "[]") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 1);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 246);
    CommonChecks(vm);
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
      .Emit(Opcodes.InitFrame, new int[] { 3 })
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
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(0), new Const(false), new Const("") });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertTrue(vm.PopRelease().bval);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.Less)
      .Emit(Opcodes.CondJump, new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 4);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 100);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .Emit(Opcodes.Less)
      .Emit(Opcodes.CondJump, new int[] { 9 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Jump, new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 5);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 10);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .Emit(Opcodes.Less)
      .Emit(Opcodes.CondJump, new int[] { 9 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Jump, new int[] { 19 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.UnaryNeg)
      .Emit(Opcodes.Less)
      .Emit(Opcodes.CondJump, new int[] { 9 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 30) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Jump, new int[] { 18 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .Emit(Opcodes.Less)
      .Emit(Opcodes.CondJump, new int[] { 9 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Jump, new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 40) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 7);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 20);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      //__while__//
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.LessOrEqual)
      .Emit(Opcodes.CondJump, new int[] { 12 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Jump, new int[] { -22 })
      //__//
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(100), new Const(10) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 0);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      //__while__//
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.LessOrEqual)
      .Emit(Opcodes.CondJump, new int[] { 25 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 80) })
      .Emit(Opcodes.Less)
      .Emit(Opcodes.CondJump, new int[] { 3 })
      .Emit(Opcodes.Jump, new int[] { 3 })
      .Emit(Opcodes.Jump, new int[] { -35 })
      //__//
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 70);
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
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 0);
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
      .Emit(Opcodes.InitFrame, new int[] { 2 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      //__for__//
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.SetVar, new int[] { 1 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.Less)
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
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(10), new Const(0), new Const(3), new Const(1) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 7);
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
      .Emit(Opcodes.InitFrame, new int[] { 3 + 2/*hidden vars*/ })
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
      .Emit(Opcodes.Less) //compare counter and tmp arr size
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
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
    ;

    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 4);
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
      .Emit(Opcodes.InitFrame, new int[] { 2 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      //__for__//
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.SetVar, new int[] { 1 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .Emit(Opcodes.Less)
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
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(10), new Const(1), new Const(3)});

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 9);
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
      .Emit(Opcodes.InitFrame, new int[] { 2 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      //__for__//
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.SetVar, new int[] { 1 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
      .Emit(Opcodes.Less)
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
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(10), new Const(1), new Const(3)});

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 10);
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
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 1);
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
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 3);
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
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.CondJump, new int[] { 13 })
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Jump, new int[] { 10 })
      .Emit(Opcodes.GetFunc, new int[] { 6 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(1), new Const(2), new Const(true) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertTrue(vm.PopRelease().bval);
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
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .Emit(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .Emit(Opcodes.Less)
      .Emit(Opcodes.CondJump, new int[] { 13 })
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Jump, new int[] { 10 })
      .Emit(Opcodes.GetFunc, new int[] { 6 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 3);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 1);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.ArgVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //test1
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.ArgVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.Constant, new int[] { 3 })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.GetFunc, new int[] { 13 })
      .Emit(Opcodes.Call, new int[] { 1 })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(98), new Const(1), new Const(5), new Const(30) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 125);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.ArgVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 1 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const("Hello"), new Const(" world !") });

    var vm = MakeVM(c);
    vm.Start("Test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().str, "Hello world !");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSimpleLambdaCall()
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
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      //lambda
      .Emit(Opcodes.Jump, new int[] { 6 } ) //skip lambda
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Lambda, new int[] { 11, 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(0), new Const(123) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 123);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSimpleLambdaCallInFalseCondition()
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
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.CondJump, new int[] { 23 })
      //lambda
      .Emit(Opcodes.Jump, new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Lambda, new int[] { 10, 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Jump,     new int[] { 5 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(false), new Const(123), new Const(321) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 321);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSimpleLambdaCallInTrueCondition()
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
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.CondJump, new int[] { 23 })
      //lambda
      .Emit(Opcodes.Jump, new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Lambda, new int[] { 10, 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Jump,     new int[] { 5 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(true), new Const(123), new Const(321) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 123);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSimpleLambdaCallFromSubFunc()
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
      .Emit(Opcodes.Return)
      //foo
      .Emit(Opcodes.Jump, new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Lambda, new int[] { 3, 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.GetFunc, new int[] { 1 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 123);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSimpleLambdaCallAsVar()
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
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.Jump, new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Lambda, new int[] { 5, 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetFuncFromVar, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 123);
    CommonChecks(vm);
  }
  
  [IsTested()]
  public void TestSimpleLambdaCallAsVarInFalseCondition()
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
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.Jump, new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Lambda, new int[] { 5, 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.CondJump, new int[] { 11 })
      .Emit(Opcodes.GetFuncFromVar, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Jump, new int[] { 5 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123), new Const(false), new Const(321) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 321);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSimpleLambdaCallAsVarInTrueCondition()
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
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.Jump, new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Lambda, new int[] { 5, 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.CondJump, new int[] { 11 })
      .Emit(Opcodes.GetFuncFromVar, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Jump, new int[] { 5 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123), new Const(true), new Const(321) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 123);
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
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      //lambda
      .Emit(Opcodes.Jump, new int[] { 4 } ) //skip lambda
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Lambda, new int[] { 11, 1 })
      .Emit(Opcodes.UseUpval, new int[] { 0, 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 123);
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
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 2 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.SetVar, new int[] { 1 })
      //lambda
      .Emit(Opcodes.Jump, new int[] { 16 } ) //skip lambda
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetVar, new int[] { 2 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Lambda, new int[] { 17, 3 })
      .Emit(Opcodes.UseUpval, new int[] { 0, 1 })
      .Emit(Opcodes.UseUpval, new int[] { 1, 2 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(20), new Const(10), new Const(5) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 35);
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
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      //lambda
      .Emit(Opcodes.Jump, new int[] { 34 } ) //skip lambda
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Jump, new int[] { 10 } ) //skip lambda
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Lambda, new int[] { 20, 2 })
      .Emit(Opcodes.UseUpval, new int[] { 0, 1 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Lambda, new int[] { 11, 1 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123), new Const(321) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 321);
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
      .Emit(Opcodes.InitFrame, new int[] { 3+1/*for cargs bits*/ })
      .Emit(Opcodes.ArgVar, new int[] { 3 }) //cargs bits
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
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 130 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(1), new Const(0), new Const(10) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 12);
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
      .Emit(Opcodes.InitFrame, new int[] { 3+1/*for cargs bits*/ })
      .Emit(Opcodes.ArgVar, new int[] { 3 }) //cargs bits
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
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.Constant, new int[] { 3 })
      .Emit(Opcodes.GetFunc, new int[] { 0 })
      .Emit(Opcodes.Call, new int[] { 66 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(1), new Const(0), new Const(10), new Const(2) });

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 5);
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
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("suspend") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    for(int i=0;i<99;i++)
      AssertEqual(vm.Tick(), BHS.RUNNING);
    CommonChecks(vm, check_frames: false, check_fibers: false);
    AssertEqual(vm.frames_pool.Allocs, 1);
    AssertEqual(vm.fibers_pool.Allocs, 1);
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
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("yield") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("yield") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("yield") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 1);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
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
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 1);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
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
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 1);
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
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("suspend") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.Return)
      //bar
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("yield") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.DeclVar, new int[] { 0, (int)Val.NUMBER })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.PARAL, 28})
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 9})
          .Emit(Opcodes.GetFunc, new int[] { 0/*foo*/ })
          .Emit(Opcodes.Call, new int[] { 0 })
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 11})
          .Emit(Opcodes.GetFunc, new int[] { 10/*bar*/})
          .Emit(Opcodes.Call, new int[] { 0 })
          .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 1);
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
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("suspend") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.Return)
      //bar
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf("yield") })
      .Emit(Opcodes.CallNative, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.DeclVar, new int[] { 0, (int)Val.NUMBER  })
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.PARAL, 28})
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 9})
          .Emit(Opcodes.GetFunc, new int[] { 0/*foo*/ })
          .Emit(Opcodes.Call, new int[] { 0 })
        .Emit(Opcodes.Block, new int[] { (int)EnumBlock.SEQ, 11})
          .Emit(Opcodes.GetFunc, new int[] { 10/*bar*/ })
          .Emit(Opcodes.Call, new int[] { 0 })
          .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 1);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
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
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    for(int i=0;i<99;i++)
      AssertEqual(vm.Tick(), BHS.RUNNING);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
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
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 1);
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
    vm.Start("test");

    var status = vm.Tick();
    AssertEqual(BHS.RUNNING, status);

    status = vm.Tick();
    AssertEqual(BHS.RUNNING, status);

    status = vm.Tick();
    AssertEqual(BHS.RUNNING, status);

    status = vm.Tick();
    AssertEqual(BHS.SUCCESS, status);

    var val = vm.PopRelease();
    AssertEqual(3, val.num);
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
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual("elsefoohey", log.ToString());
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
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual("", log.ToString());
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual("", log.ToString());
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual("", log.ToString());
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual("", log.ToString());
    //TODO: VM.Tick() returns BHS.SUCCESS when all fibers exited 
    //      regardless of their individual exit status
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
        .Emit(Opcodes.Constant, new int[] { 0 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
      //lambda
      .Emit(Opcodes.Jump, new int[] { 31 } ) //skip lambda
      .Emit(Opcodes.Block, new int[] { (int)EnumBlock.DEFER, 13})
        .Emit(Opcodes.Constant, new int[] { 1 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
        .Emit(Opcodes.Constant, new int[] { 2 })
        .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
        .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Lambda, new int[] { 19, 0 })
      .Emit(Opcodes.Call, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 3 })
      .Emit(Opcodes.GetFuncNative, new int[] { globs.GetMembers().IndexOf(fn) })
      .Emit(Opcodes.CallNative, new int[] { 1 })
      .Emit(Opcodes.Return)
      ;

    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "Foo") }) 
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.NotEqual)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertTrue(vm.PopRelease().bval);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
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
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
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
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
      .Emit(Opcodes.InitFrame, new int[] { 2 })
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
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
    AssertEqual(vm.Tick(), BHS.SUCCESS);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.New, new int[] { ConstIdx(c, "Bar") }) 
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { ConstNullIdx(c) })
      .Emit(Opcodes.NotEqual)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertTrue(vm.PopRelease().bval);
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
      .Emit(Opcodes.InitFrame, new int[] { 1 })
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
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual("10;14.5;Hey", log.ToString());
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
      .Emit(Opcodes.InitFrame, new int[] { 2 })
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
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual("10;14.5;Hey", log.ToString());
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

    vm.Start("test");
    vm.Start("test");

    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 123);
    AssertEqual(vm.PopRelease().num, 123);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStartFiber()
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
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual("done", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStopFiber()
  {
    string bhl = @"
    func test()
    {
      int fid = start(func() {
        defer {
          trace(""0"")
        }
        trace(""1"")
        yield()
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
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual("130", log.ToString());
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
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 123);
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
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.GetFuncImported, new int[] { 0, 0 })
      .Emit(Opcodes.Call, new int[] { 1 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
    );
    AssertEqual(importer.Import("bhl2"), 
      new ModuleCompiler()
      .UseInitCode()
      .Emit(Opcodes.Import, new int[] { 0 })
      .UseByteCode()
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.ArgVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetFuncImported, new int[] { 0, 0 })
      .Emit(Opcodes.Call, new int[] { 1 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
    );
    AssertEqual(importer.Import("bhl3"), 
      new ModuleCompiler()
      .Emit(Opcodes.InitFrame, new int[] { 1 })
      .Emit(Opcodes.ArgVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
    );

    var vm = new VM(globs: null, importer: importer);
    vm.ImportModule("bhl1");
    vm.Start("bhl1");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopRelease().num, 23);
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
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual("10;14.2;Hey", log.ToString());
    CommonChecks(vm);
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
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    stopwatch.Stop();
    AssertEqual(vm.PopRelease().num, 610);
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

  ModuleCompiler Compile(string bhl, GlobalScope globs = null)
  {
    globs = globs == null ? SymbolTable.VM_CreateBuiltins() : globs;
    //NOTE: we don't want to affect the original globs
    var globs_copy = globs.Clone();

    var mdl = new bhl.Module("", "");
    var mreg = new ModuleRegistry();
    var ast = Src2AST(bhl, mdl, mreg, globs_copy);
    var c  = new ModuleCompiler(globs_copy, ast, mdl.path);
    c.Compile();
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

    AssertEqual(vm.Stack.Count, 0);
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
    AssertEqual(new CompiledModule(ca), new CompiledModule(cb));
  }

  public static void AssertEqual(CompiledModule ca, ModuleCompiler cb)
  {
    AssertEqual(ca, new CompiledModule(cb));
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
    var m = new CompiledModule(c);
    vm.RegisterModule(m);
    return vm;
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
        delegate(VM.Frame frm, ref BHS status) { 
          string str = frm.PopRelease().str;
          log.Append(str);
          return null;
        } 
    );
    fn.Define(new FuncArgSymbol("str", globs.Type("string")));
    globs.Define(fn);
    return fn;
  }

  class CoroutineWaitTicks : IInstruction
  {
    int c;
    int ticks_ttl;

    public void Tick(VM.Frame frm, ref BHS status)
    {
      //first time
      if(c++ == 0)
        ticks_ttl = (int)frm.PopRelease().num;

      if(ticks_ttl-- > 0)
        status = BHS.RUNNING;
    }

    public void Release(VM vm)
    {
      c = 0;
      ticks_ttl = 0;
    }
  }

  FuncSymbolNative BindWaitTicks(GlobalScope globs, StringBuilder log)
  {
    var fn = new FuncSymbolNative("WaitTicks", globs.Type("void"), null,
        delegate(VM.Frame frm, ref BHS status) { 
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
      delegate(ref Val v) 
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
  
