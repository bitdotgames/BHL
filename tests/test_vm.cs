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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(123) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 123);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(true) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertTrue(vm.PopValue().bval);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.TypeCast, new int[] { (int)SymbolTable.symb_int.name.n })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(true) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 1);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.TypeCast, new int[] { (int)SymbolTable.symb_string.name.n })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(7) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().str, "7");
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const("Hello") });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().str, "Hello");
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.And)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(false), new Const(true) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertTrue(vm.PopValue().bval == false);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Or)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(false), new Const(true) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertTrue(vm.PopValue().bval);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.BitAnd)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(3), new Const(1) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 1);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.BitOr)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(3), new Const(4) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 7);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Mod)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(3), new Const(2)});

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNullConstant()
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Equal)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { Const.NewNil() });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertTrue(vm.PopValue().bval);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.UnaryNot)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(true) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertTrue(vm.PopValue().bval == false);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.UnaryNeg)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(1) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, -1);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(10), new Const(20)});

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 30);
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

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 33930);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const("Hello "), new Const("world !")});

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().str, "Hello world !");
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(10) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 20);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSub()
  {
    string bhl = @"
    func int test() 
    {
      return 20 - 10
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(20), new Const(10) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 10);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Div)
      .Emit(Opcodes.ReturnVal)   
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(20), new Const(10) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 2);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Mul)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(10), new Const(20) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 200);
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
      new Compiler(c.Symbols)
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

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 500);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSimpleBindFunc()
  {
    string bhl = @"
    func void test() 
    {
      trace(""foo"")
    }
    ";

    var globs = SymbolTable.VM_CreateBuiltins();
    var log = new StringBuilder();
    var fn = new VM_FuncBindSymbol("trace", globs.Type("void"),
        delegate(VM _, VM.Frame fr) { 
          string str = fr.PopValue().str;
          log.Append(str);
          return null;
        } 
    );
    fn.Define(new FuncArgSymbol("str", globs.Type("string")));
    globs.Define(fn);

    var c = Compile(bhl, globs);

    var expected = 
      new Compiler(globs)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.FuncCall, new int[] { 1, globs.GetMembers().IndexOf(fn), 1 })
      .Emit(Opcodes.Return)
    ;
    AssertEqual(c, expected);

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);

    AssertEqual("foo", log.ToString());

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
      new Compiler(c.Symbols)
      .Emit(Opcodes.ArrNew) 
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants.Count, 0);

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    var lst = vm.AquireValue();
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.ArrNew)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.MethodCall, new int[] { ArrType, ArrAddIdx })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.MethodCall, new int[] { ArrType, ArrAtIdx })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const("test"), new Const(0) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().str, "test");
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
      new Compiler(c.Symbols)
      //mkarray
      .Emit(Opcodes.ArrNew)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.MethodCall, new int[] { ArrType, ArrAddIdx })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.MethodCall, new int[] { ArrType, ArrAddIdx })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.FuncCall, new int[] { 0, 0, 0 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.MethodCall, new int[] { ArrType, ArrAtIdx })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(1), new Const(2), new Const(0) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 1);
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
      new Compiler(c.Symbols)
      //mkarray
      .Emit(Opcodes.ArrNew)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.MethodCall, new int[] { ArrType, ArrAddIdx })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.MethodCall, new int[] { ArrType, ArrAddIdx })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.MethodCall, new int[] { ArrType, ArrRemoveIdx })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.FuncCall, new int[] { 0, 0, 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(1), new Const(100), new Const(0) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    var lst = vm.AquireValue();
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
      new Compiler(c.Symbols)
      //mkarray
      .Emit(Opcodes.ArrNew)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.MethodCall, new int[] { ArrType, ArrAddIdx })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.MethodCall, new int[] { ArrType, ArrAddIdx })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.FuncCall, new int[] { 0, 0, 0 })
      .Emit(Opcodes.GetMVar, new int[] { ArrType, ArrCountIdx })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(1), new Const(100) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 2);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.ArrNew)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.MethodCall, new int[] { ArrType, ArrAddIdx })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.MethodCall, new int[] { ArrType, ArrSetIdx })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 3 })
      .Emit(Opcodes.MethodCall, new int[] { ArrType, ArrAddIdx })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const("foo"), new Const("tst"), new Const(0), new Const("bar") });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    var val = vm.AquireValue();
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
      new Compiler(c.Symbols)
      //foo
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.MethodCall, new int[] { ArrType, ArrAddIdx })
      .Emit(Opcodes.Return)
      //test
      .Emit(Opcodes.ArrNew)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.MethodCall, new int[] { ArrType, ArrAddIdx })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.MethodCall, new int[] { ArrType, ArrAddIdx })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.FuncCall, new int[] { 0, 0, 1 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 3 })
      .Emit(Opcodes.MethodCall, new int[] { ArrType, ArrAtIdx })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(100), new Const(1), new Const(2), new Const(0) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 1);
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
      new Compiler(c.Symbols)
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

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 246);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestVarWithDefaultValue()
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

    AssertEqual(c.Constants, new List<Const>() { new Const(0), new Const(false), new Const("") });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertTrue(vm.PopValue().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFunctionCall()
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
      new Compiler(c.Symbols)
      //1 func code
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //2 func code
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //test program code
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.FuncCall, new int[] { 0, 0, 1 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.Constant, new int[] { 3 })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.FuncCall, new int[] { 0, 9, 1 })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(98), new Const(1), new Const(5), new Const(30) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 125);
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
      new Compiler(c.Symbols)
      //1 func code
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //test program code
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.FuncCall, new int[] { 0, 0, 1 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const("Hello"), new Const(" world !") });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("Test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().str, "Hello world !");
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.Greater)
      .Emit(Opcodes.CondJump, new int[] { 4 })
      .Emit(Opcodes.Constant, new int[] { 3 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(100), new Const(1), new Const(2), new Const(10) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 100);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.Greater)
      .Emit(Opcodes.CondJump, new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { 3 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Jump, new int[] { 4 })
      .Emit(Opcodes.Constant, new int[] { 4 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(0), new Const(2), new Const(1), new Const(10), new Const(20) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 10);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Greater)
      .Emit(Opcodes.CondJump, new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Jump, new int[] { 12 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.UnaryNeg)
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Greater)
      .Emit(Opcodes.CondJump, new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { 3 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Jump, new int[] { 11 })
      .Emit(Opcodes.Constant, new int[] { 4 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Greater)
      .Emit(Opcodes.CondJump, new int[] { 6 })
      .Emit(Opcodes.Constant, new int[] { 5 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.Jump, new int[] { 4 })
      .Emit(Opcodes.Constant, new int[] { 6 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(0), new Const(1), new Const(10), new Const(30), new Const(3), new Const(20), new Const(40) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 20);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestWhileCondition()
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      //__while__//
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.GreaterOrEqual)
      .Emit(Opcodes.CondJump, new int[] { 9 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.LoopJump, new int[] { 16 })
      //__//
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(100), new Const(10) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 0);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForCondition()
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      //__for__//
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.SetVar, new int[] { 1 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.Less)
      .Emit(Opcodes.CondJump, new int[] { 16 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Constant, new int[] { 3 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.SetVar, new int[] { 1 })
      .Emit(Opcodes.LoopJump, new int[] { 23 })
      //__//
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(10), new Const(0), new Const(3), new Const(1) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 7);
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
      if( true )
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.CondJump, new int[] { 7 })
      .Emit(Opcodes.FuncCall, new int[] { 0, 0, 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Jump, new int[] { 5 })
      .Emit(Opcodes.FuncCall, new int[] { 0, 4, 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(1), new Const(2), new Const(true) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertTrue(vm.PopValue().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFunctionCallInCondition()
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.Greater)
      .Emit(Opcodes.CondJump, new int[] { 7 })
      .Emit(Opcodes.FuncCall, new int[] { 0, 0, 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Jump, new int[] { 5 })
      .Emit(Opcodes.FuncCall, new int[] { 0, 4, 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(1), new Const(2), new Const(0) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 1);
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
      new Compiler(c.Symbols)
      //foo func code
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.DefArg, new int[] { 5 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.Sub)
      .Emit(Opcodes.SetVar, new int[] { 1 })
      .Emit(Opcodes.DefArg, new int[] { 2 })
      .Emit(Opcodes.Constant, new int[] { 2 })
      .Emit(Opcodes.SetVar, new int[] { 2 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.GetVar, new int[] { 1 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.GetVar, new int[] { 2 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //test program code
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.Add)
      .Emit(Opcodes.Constant, new int[] { 1 })
      .Emit(Opcodes.FuncCall, new int[] { 0, 0, 130 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.Constants, new List<Const>() { new Const(1), new Const(0), new Const(10) });

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 12);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.FuncCall, new int[] { 1, globs.GetMembers().IndexOf("suspend"), 0 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    for(int i=0;i<99;i++)
      AssertEqual(vm.Tick(), BHS.RUNNING);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.FuncCall, new int[] { 1, globs.GetMembers().IndexOf("yield"), 0 })
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.FuncCall, new int[] { 1, globs.GetMembers().IndexOf("yield"), 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.FuncCall, new int[] { 1, globs.GetMembers().IndexOf("yield"), 0 })
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 1);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.PushBlock, new int[] { (int)EnumBlock.PARAL, 20})
        .Emit(Opcodes.PushBlock, new int[] { (int)EnumBlock.SEQ, 4})
        .Emit(Opcodes.FuncCall, new int[] { 1, globs.GetMembers().IndexOf("suspend"), 0 })
        .Emit(Opcodes.PopBlock)
      .Emit(Opcodes.PushBlock, new int[] { (int)EnumBlock.SEQ, 8})
        .Emit(Opcodes.FuncCall, new int[] { 1, globs.GetMembers().IndexOf("yield"), 0 })
        .Emit(Opcodes.Constant, new int[] { 0 })
        .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.PopBlock)
      .Emit(Opcodes.PopBlock)
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 1);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.PushBlock, new int[] { (int)EnumBlock.PARAL, 20})
        .Emit(Opcodes.PushBlock, new int[] { (int)EnumBlock.SEQ, 4})
        .Emit(Opcodes.FuncCall, new int[] { 1, globs.GetMembers().IndexOf("suspend"), 0 })
        .Emit(Opcodes.PopBlock)
      .Emit(Opcodes.PushBlock, new int[] { (int)EnumBlock.SEQ, 8})
        .Emit(Opcodes.FuncCall, new int[] { 1, globs.GetMembers().IndexOf("yield"), 0 })
        .Emit(Opcodes.Constant, new int[] { 0 })
        .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.PopBlock)
      .Emit(Opcodes.PopBlock)
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 1);
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
      new Compiler(c.Symbols)
      //foo
      .Emit(Opcodes.FuncCall, new int[] { 1, globs.GetMembers().IndexOf("suspend"), 0 })
      .Emit(Opcodes.Return)
      //bar
      .Emit(Opcodes.FuncCall, new int[] { 1, globs.GetMembers().IndexOf("yield"), 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.PushBlock, new int[] { (int)EnumBlock.PARAL, 18})
        .Emit(Opcodes.PushBlock, new int[] { (int)EnumBlock.SEQ, 4})
        .Emit(Opcodes.FuncCall, new int[] { 0, 0/*foo*/, 0 })
        .Emit(Opcodes.PopBlock)
      .Emit(Opcodes.PushBlock, new int[] { (int)EnumBlock.SEQ, 6})
        .Emit(Opcodes.FuncCall, new int[] { 0, 5/*bar*/, 0 })
        .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.PopBlock)
      .Emit(Opcodes.PopBlock)
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 1);
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
      new Compiler(c.Symbols)
      //foo
      .Emit(Opcodes.FuncCall, new int[] { 1, globs.GetMembers().IndexOf("suspend"), 0 })
      .Emit(Opcodes.Return)
      //bar
      .Emit(Opcodes.FuncCall, new int[] { 1, globs.GetMembers().IndexOf("yield"), 0 })
      .Emit(Opcodes.Constant, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      //
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.PushBlock, new int[] { (int)EnumBlock.PARAL, 18})
        .Emit(Opcodes.PushBlock, new int[] { (int)EnumBlock.SEQ, 4})
        .Emit(Opcodes.FuncCall, new int[] { 0, 0/*foo*/, 0 })
        .Emit(Opcodes.PopBlock)
      .Emit(Opcodes.PushBlock, new int[] { (int)EnumBlock.SEQ, 6})
        .Emit(Opcodes.FuncCall, new int[] { 0, 5/*bar*/, 0 })
        .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.PopBlock)
      .Emit(Opcodes.PopBlock)
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 1);
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.PushBlock, new int[] { (int)EnumBlock.PARAL_ALL, 20})
        .Emit(Opcodes.PushBlock, new int[] { (int)EnumBlock.SEQ, 4})
        .Emit(Opcodes.FuncCall, new int[] { 1, globs.GetMembers().IndexOf("suspend"), 0 })
        .Emit(Opcodes.PopBlock)
      .Emit(Opcodes.PushBlock, new int[] { (int)EnumBlock.SEQ, 8})
        .Emit(Opcodes.FuncCall, new int[] { 1, globs.GetMembers().IndexOf("yield"), 0 })
        .Emit(Opcodes.Constant, new int[] { 0 })
        .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.PopBlock)
      .Emit(Opcodes.PopBlock)
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    for(int i=0;i<99;i++)
      AssertEqual(vm.Tick(), BHS.RUNNING);
    //NOTE: since VM is in the running state we need to explicitely clear the pools
    //TODO: probably Val pools should be stored in VM rather than static members
    Val.PoolClear();
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
      new Compiler(c.Symbols)
      .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.PushBlock, new int[] { (int)EnumBlock.PARAL_ALL, 24})
        .Emit(Opcodes.PushBlock, new int[] { (int)EnumBlock.SEQ, 8})
        .Emit(Opcodes.FuncCall, new int[] { 1, globs.GetMembers().IndexOf("yield"), 0 })
        .Emit(Opcodes.FuncCall, new int[] { 1, globs.GetMembers().IndexOf("yield"), 0 })
        .Emit(Opcodes.PopBlock)
      .Emit(Opcodes.PushBlock, new int[] { (int)EnumBlock.SEQ, 8})
        .Emit(Opcodes.FuncCall, new int[] { 1, globs.GetMembers().IndexOf("yield"), 0 })
        .Emit(Opcodes.Constant, new int[] { 0 })
        .Emit(Opcodes.SetVar, new int[] { 0 })
      .Emit(Opcodes.PopBlock)
      .Emit(Opcodes.PopBlock)
      .Emit(Opcodes.GetVar, new int[] { 0 })
      .Emit(Opcodes.ReturnVal)
      .Emit(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 1);
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

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    vm.Start("test");

    var status = vm.Tick();
    AssertEqual(BHS.RUNNING, status);

    status = vm.Tick();
    AssertEqual(BHS.RUNNING, status);

    status = vm.Tick();
    AssertEqual(BHS.RUNNING, status);

    status = vm.Tick();
    AssertEqual(BHS.SUCCESS, status);

    var val = vm.PopValue();
    AssertEqual(3, val.num);
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

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);

    vm.Start("test");
    vm.Start("test");

    AssertEqual(vm.Tick(), BHS.RUNNING);
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    AssertEqual(vm.PopValue().num, 123);
    AssertEqual(vm.PopValue().num, 123);
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

    var vm = new VM(c.Symbols, c.GetBytes(), c.Constants, c.Func2Offset);
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    vm.Start("test");
    AssertEqual(vm.Tick(), BHS.SUCCESS);
    stopwatch.Stop();
    AssertEqual(vm.PopValue().num, 610);
    CommonChecks(vm);
    Console.WriteLine("bhl vm fib ticks: {0}", stopwatch.ElapsedTicks);
  }

  ///////////////////////////////////////

  static Compiler TestCompiler(GlobalScope globs = null)
  {
    globs = globs == null ? SymbolTable.VM_CreateBuiltins() : globs;
    //NOTE: we want to work with original globs
    var globs_copy = globs.Clone();
    return new Compiler(globs_copy);
  }

  Compiler Compile(string bhl, GlobalScope globs = null)
  {
    globs = globs == null ? SymbolTable.VM_CreateBuiltins() : globs;
    //NOTE: we want to work with original globs
    var globs_copy = globs.Clone();

    Util.DEBUG = true;
    var ast = Src2AST(bhl, globs_copy);
    var c  = new Compiler(globs_copy);
    c.Compile(ast);
    return c;
  }

  AST Src2AST(string src, GlobalScope globs = null)
  {
    globs = globs == null ? SymbolTable.VM_CreateBuiltins() : globs;

    var mreg = new ModuleRegistry();
    //fake module for this specific case
    var mod = new bhl.Module("", "");
    var ms = new MemoryStream();
    Frontend.Source2Bin(mod, src.ToStream(), ms, globs, mreg);
    ms.Position = 0;

    return Util.Bin2Meta<AST_Module>(ms);
  }

  void CommonChecks(VM vm)
  {
    //for extra debug
    if(Val.PoolCount != Val.PoolCountFree)
      Console.WriteLine(Val.PoolDump());

    AssertEqual(vm.Stack.Count, 0);
    AssertEqual(Val.PoolCount, Val.PoolCountFree);
    AssertEqual(ValList.PoolCount, ValList.PoolCountFree);
    AssertEqual(ValDict.PoolCount, ValDict.PoolCountFree);
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

  public static void AssertEqual(Compiler ca, Compiler cb)
  {
    var a = ca.GetBytes();
    var b = cb.GetBytes();

    Compiler.OpDefinition aop = null;
    int aop_size = 0;
    Compiler.OpDefinition bop = null;
    int bop_size = 0;

    bool equal = true;
    string cmp = "";
    var lens = new List<int>();
    int max_len = 0;
    for(int i=0;i<(a.Length > b.Length ? a.Length : b.Length);i++)
    {
      string astr = "";
      if(i < a.Length)
      {
        astr = string.Format("0x{0:x2}", a[i]);
        if(aop != null)
        {
          --aop_size;
          if(aop_size == 0)
            aop = null; 
        }
        else
        {
          aop = ca.LookupOpcode((Opcodes)a[i]);
          aop_size = PredictOpcodeSize(aop, a, i);
          astr += "(" + aop.name.ToString() + ")";
          if(aop_size == 0)
            aop = null;
        }
      }

      string bstr = "";
      if(i < b.Length)
      {
        bstr = string.Format("0x{0:x2}", b[i]);
        if(bop != null)
        {
          --bop_size;
          if(bop_size == 0)
            bop = null; 
        }
        else
        {
          bop = cb.LookupOpcode((Opcodes)b[i]);
          bop_size = PredictOpcodeSize(bop, b, i);
          bstr += "(" + bop.name.ToString() + ")";
          if(bop_size == 0)
            bop = null;
        }
      }

      lens.Add(astr.Length);
      if(astr.Length > max_len)
        max_len = astr.Length;
      cmp += string.Format("{0:x2}", i) + " " + astr + "{fill" + lens.Count + "} | " + bstr;

      if(a.Length <= i || b.Length <= i || a[i] != b[i])
      {
        equal = false;
        cmp += " <===";
      }

      cmp += "\n";
    }

    for(int i=1;i<=lens.Count;++i)
    {
      cmp = cmp.Replace("{fill" + i + "}", new String(' ', max_len - lens[i-1]));
    }

    if(!equal)
    {
      Console.WriteLine(cmp);
      throw new Exception("Assertion failed: bytes not equal");
    }
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

  static int PredictOpcodeSize(Compiler.OpDefinition op, byte[] bytes, int start_pos)
  {
    if(op.operand_width == null)
      return 0;
    uint pos = (uint)start_pos;
    foreach(int ow in op.operand_width)
      Bytecode.Decode(bytes, ref pos);
    return (int)pos - start_pos;
  }

  public static int ArrType {
    get {
      return GenericArrayTypeSymbol.VM_Type;
    }
  }
  public static int ArrAddIdx {
    get {
      return GenericArrayTypeSymbol.VM_AddIdx;
    }
  }
  public static int ArrSetIdx {
    get {
      return GenericArrayTypeSymbol.VM_SetIdx;
    }
  }
  public static int ArrRemoveIdx {
    get {
      return GenericArrayTypeSymbol.VM_RemoveIdx;
    }
  }
  public static int ArrCountIdx {
    get {
      return GenericArrayTypeSymbol.VM_CountIdx;
    }
  }
  public static int ArrAtIdx {
    get {
      return GenericArrayTypeSymbol.VM_AtIdx;
    }
  }

  //simple console outputting version
  void BindLog(GlobalScope globs)
  {
    {
      var fn = new VM_FuncBindSymbol("log", globs.Type("void"),
          delegate(VM vm, VM.Frame fr) { 
            Console.WriteLine(fr.PopValue().str); 
            return null;
          } );
      fn.Define(new FuncArgSymbol("str", globs.Type("string")));

      globs.Define(fn);
    }
  }
}
  
