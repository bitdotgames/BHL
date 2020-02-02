using System;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using System.Text;
using Antlr4.Runtime;
using bhl;

public class BHL_TestVM : BHL_TestBase
{
  [IsTested()]
  public void TestCompileNumConstant()
  {
    string bhl = @"
    func int test()
    {
      return 123
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 1);
    AssertEqual((int)c.GetConstants()[0].type, (int)EnumLiteral.NUM);
    AssertEqual(c.GetConstants()[0].num, 123);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().num, 123);
  }

  [IsTested()]
  public void TestCompileBoolConstant()
  {
    string bhl = @"
    func bool test()
    {
      return true
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 1);
    AssertEqual((int)c.GetConstants()[0].type, (int)EnumLiteral.BOOL);
    AssertEqual(c.GetConstants()[0].num, 1);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertTrue(vm.GetStackTop().bval);
  }

  [IsTested()]
  public void TestCompileStringConstant()
  {
    string bhl = @"
    func string test()
    {
      return ""Hello""
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 1);
    AssertEqual((int)c.GetConstants()[0].type, (int)EnumLiteral.STR);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().str, "Hello");
  }

  [IsTested()]
  public void TestCompileLogicalAnd()
  {
    string bhl = @"
    func bool test()
    {
      return false && true
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.And)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 2);
    AssertEqual(c.GetConstants()[0].num, 0);
    AssertEqual(c.GetConstants()[1].num, 1);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertTrue(vm.GetStackTop().bval == false);
  }

  [IsTested()]
  public void TestCompileLogicalOr()
  {
    string bhl = @"
    func bool test()
    {
      return false || true
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.Or)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 2);
    AssertEqual(c.GetConstants()[0].num, 0);
    AssertEqual(c.GetConstants()[1].num, 1);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertTrue(vm.GetStackTop().bval);
  }

  [IsTested()]
  public void TestCompileBitAnd()
  {
    string bhl = @"
    func int test()
    {
      return 3 & 1
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.BitAnd)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 2);
    AssertEqual(c.GetConstants()[0].num, 3);
    AssertEqual(c.GetConstants()[1].num, 1);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().num, 1);
  }

  [IsTested()]
  public void TestCompileBitOr()
  {
    string bhl = @"
    func int test()
    {
      return 3 | 4
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.BitOr)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 2);
    AssertEqual(c.GetConstants()[0].num, 3);
    AssertEqual(c.GetConstants()[1].num, 4);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().num, 7);
  }

  [IsTested()]
  public void TestCompileMod()
  {
    string bhl = @"
    func int test()
    {
      return 3 % 2
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.Mod)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 2);
    AssertEqual(c.GetConstants()[0].num, 3);
    AssertEqual(c.GetConstants()[1].num, 2);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().num, 1);
  }

  [IsTested()]
  public void TestCompileArray()
  {
    string bhl = @"
    func int[] test()
    {
      int[] a = new int[]
      return a
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.New, new int[] { 0 }) 
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 0);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertTrue(vm.GetStackTop().type == DynVal.OBJ);
    //vm.ShowFullStack();
  }

  [IsTested()]
  public void TestCompileStringArray()
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

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.New, new int[] { 0 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.MethodCall, new int[] { (int)BuiltInArray.Add })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.IdxGet)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 2);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertTrue(vm.GetStackTop().str == "test");
  }

  [IsTested()]
  public void TestCompileArrayIdx()
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

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      //mkarray
      .TestEmit(Opcodes.New, new int[] { 0 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.MethodCall, new int[] { (int)BuiltInArray.Add })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.MethodCall, new int[] { (int)BuiltInArray.Add })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.ReturnVal)
      //test
      .TestEmit(Opcodes.FuncCall, new [] { 0,0 })
      .TestEmit(Opcodes.Constant, new int[] { 2 })
      .TestEmit(Opcodes.IdxGet)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 3);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertTrue(vm.GetStackTop().num == 1);
  }

  [IsTested()]
  public void TestCompileArrayRemoveAt()
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

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      //mkarray
      .TestEmit(Opcodes.New, new int[] { 0 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.MethodCall, new int[] { (int)BuiltInArray.Add })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.MethodCall, new int[] { (int)BuiltInArray.Add })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 2 })
      .TestEmit(Opcodes.MethodCall, new int[] { (int)BuiltInArray.RemoveAt })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.ReturnVal)
      //test
      .TestEmit(Opcodes.FuncCall, new [] { 0,0 })
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 3);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    var ret = vm.GetStackTop()._obj as DynValList;
    AssertTrue( ret.Count == 1);
  }

  [IsTested()]
  public void TestCompileArrayCount() 
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

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      //mkarray
      .TestEmit(Opcodes.New, new int[] { 0 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.MethodCall, new int[] { (int)BuiltInArray.Add })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.MethodCall, new int[] { (int)BuiltInArray.Add })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.ReturnVal)
      //test
      .TestEmit(Opcodes.FuncCall, new [] { 0,0 })
      .TestEmit(Opcodes.MethodCall, new int[] { (int)BuiltInArray.Count })
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 2);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertTrue(vm.GetStackTop().num == 2);
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

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.New, new int[] { 0 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.MethodCall, new int[] { (int)BuiltInArray.Add })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 2 })
      .TestEmit(Opcodes.MethodCall, new int[] { (int)BuiltInArray.SetAt })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 3 })
      .TestEmit(Opcodes.MethodCall, new int[] { (int)BuiltInArray.Add })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 4);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    var ret = vm.GetStackTop()._obj as DynValList;
    AssertTrue( ret.Count == 2);
    AssertTrue( ret[0].str == "tst");
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

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      //foo
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.MethodCall, new int[] { (int)BuiltInArray.Add })
      .TestEmit(Opcodes.ReturnVal)
      //test
      .TestEmit(Opcodes.New, new int[] { 0 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.MethodCall, new int[] { (int)BuiltInArray.Add })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 2 })
      .TestEmit(Opcodes.MethodCall, new int[] { (int)BuiltInArray.Add })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.FuncCall, new [] { 0,1 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 3 })
      .TestEmit(Opcodes.IdxGet)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 4);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertTrue(vm.GetStackTop().num == 1);
  }

  [IsTested()]
  public void TestCompileUnaryNot()
  {
    string bhl = @"
    func bool test() 
    {
      return !true
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.UnaryNot)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 1);
    AssertTrue(c.GetConstants()[0].bval);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertTrue(vm.GetStackTop().bval == false);
  }

  [IsTested()]
  public void TestCompileUnaryNegVar()
  {
    string bhl = @"
    func int test() 
    {
      int x = 1
      return -x
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.UnaryNeg)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 1);
    AssertEqual(c.GetConstants()[0].num, 1);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().num, -1);
  }

  [IsTested()]
  public void TestCompileAdd()
  {
    string bhl = @"
    func int test() 
    {
      return 10 + 20
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.Add)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 2);
    AssertEqual(c.GetConstants()[0].num, 10);
    AssertEqual(c.GetConstants()[1].num, 20);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().num, 30);
  }

  [IsTested()]
  public void TestCompileStringConcat()
  {
    string bhl = @"
    func string test() 
    {
      return ""Hello "" + ""world !""
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.Add)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 2);
    AssertEqual(c.GetConstants()[0].str, "Hello ");
    AssertEqual(c.GetConstants()[1].str, "world !");
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().str, "Hello world !");
  }

  [IsTested()]
  public void TestCompileAddSameConstants()
  {
    string bhl = @"
    func int test() 
    {
      return 10 + 10
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.Add)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 1);
    AssertEqual(c.GetConstants()[0].num, 10);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().num, 20);
  }

  [IsTested()]
  public void TestCompileSub()
  {
    string bhl = @"
    func int test() 
    {
      return 20 - 10
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.Sub)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 2);
    AssertEqual(c.GetConstants()[0].num, 20);
    AssertEqual(c.GetConstants()[1].num, 10);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().num, 10);
  }

  [IsTested()]
  public void TestCompileDiv()
  {
    string bhl = @"
    func int test() 
    {
      return 20 / 10
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.Div)
      .TestEmit(Opcodes.ReturnVal)   
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 2);
    AssertEqual(c.GetConstants()[0].num, 20);
    AssertEqual(c.GetConstants()[1].num, 10);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().num, 2);
  }

  [IsTested()]
  public void TestCompileMul()
  {
    string bhl = @"
    func int test() 
    {
      return 10 * 20
    }
    ";

    var c = Compile(bhl);

    var result = Compile(bhl).GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.Mul)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 2);
    AssertEqual(c.GetConstants()[0].num, 10);
    AssertEqual(c.GetConstants()[1].num, 20);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().num, 200);
  }

  [IsTested()]
  public void TestCompileParenthesisExpression()
  {
    string bhl = @"
    func int test() 
    {
      return 10 * (20 + 30)
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.Constant, new int[] { 2 })
      .TestEmit(Opcodes.Add)
      .TestEmit(Opcodes.Mul)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertTrue(result.Length > 0);
    AssertEqual(c.GetConstants().Count, 3);
    AssertEqual(c.GetConstants()[0].num, 10);
    AssertEqual(c.GetConstants()[1].num, 20);
    AssertEqual(c.GetConstants()[2].num, 30);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().num, 500);
  }

  [IsTested()]
  public void TestCompileWriteReadVar()
  {
    string bhl = @"
    func int test() 
    {
      int a = 123
      return a + a
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Add)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 1);
    AssertEqual(c.GetConstants()[0].num, 123);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().num, 246);
  }

  [IsTested()]
  public void TestCompileFunctionCall()
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

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      //1 func code
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Add)
      .TestEmit(Opcodes.ReturnVal)
      //2 func code
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.ReturnVal)
      //test program code
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.Add)
      .TestEmit(Opcodes.FuncCall, new [] { 0,1 })
      .TestEmit(Opcodes.Constant, new int[] { 2 })
      .TestEmit(Opcodes.Constant, new int[] { 3 })
      .TestEmit(Opcodes.Sub)
      .TestEmit(Opcodes.FuncCall, new [] { 8,1 })
      .TestEmit(Opcodes.Sub)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 4);
    
    AssertEqual(c.GetConstants()[0].num, 98);
    AssertEqual(c.GetConstants()[1].num, 1);
    AssertEqual(c.GetConstants()[2].num, 5);
    AssertEqual(c.GetConstants()[3].num, 30);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().num, 125);
  }

  [IsTested()]
  public void TestCompileStringArgFunctionCall()
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

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      //1 func code
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Add)
      .TestEmit(Opcodes.ReturnVal)
      //test program code
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.FuncCall, new [] { 0,1 })
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 2);
    
    AssertEqual(c.GetConstants()[0].str, "Hello");
    AssertEqual(c.GetConstants()[1].str, " world !");
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("Test");
    AssertEqual(vm.GetStackTop().str, "Hello world !");
  }

  [IsTested()]
  public void TestCompileIfCondition()
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

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.Constant, new int[] { 2 })
      .TestEmit(Opcodes.Greather)
      .TestEmit(Opcodes.CondJump, new int[] { 4 })
      .TestEmit(Opcodes.Constant, new int[] { 3 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 4);
    AssertEqual(c.GetConstants()[0].num, 100);
    AssertEqual(c.GetConstants()[1].num, 1);
    AssertEqual(c.GetConstants()[2].num, 2);
    AssertEqual(c.GetConstants()[3].num, 10);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().num, 100);
  }

  [IsTested()]
  public void TestCompileIfElseCondition()
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

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.Constant, new int[] { 2 })
      .TestEmit(Opcodes.Greather)
      .TestEmit(Opcodes.CondJump, new int[] { 6 })
      .TestEmit(Opcodes.Constant, new int[] { 3 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.Jump, new int[] { 4 })
      .TestEmit(Opcodes.Constant, new int[] { 4 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 5);
    AssertEqual(c.GetConstants()[0].num, 0);
    AssertEqual(c.GetConstants()[1].num, 2);
    AssertEqual(c.GetConstants()[2].num, 1);
    AssertEqual(c.GetConstants()[3].num, 10);
    AssertEqual(c.GetConstants()[4].num, 20);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().num, 10);
  }

  [IsTested()]
  public void TestCompileWhileCondition()
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

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      //__while statenemt__//
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.GreatherOrEqual)
      .TestEmit(Opcodes.CondJump, new int[] { 9 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.Sub)
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.LoopJump, new int[] { 16 })
      //__//
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 2);
    AssertEqual(c.GetConstants()[0].num, 100);
    AssertEqual(c.GetConstants()[1].num, 10);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().num, 0);
  }

  [IsTested()]
  public void TestCompileForCondition()
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

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      //__for statenemt__//
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.SetVar, new int[] { 1 })
      .TestEmit(Opcodes.GetVar, new int[] { 1 })
      .TestEmit(Opcodes.Constant, new int[] { 2 })
      .TestEmit(Opcodes.Less)
      .TestEmit(Opcodes.CondJump, new int[] { 16 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 1 })
      .TestEmit(Opcodes.Sub)
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 1 })
      .TestEmit(Opcodes.Constant, new int[] { 3 })
      .TestEmit(Opcodes.Add)
      .TestEmit(Opcodes.SetVar, new int[] { 1 })
      .TestEmit(Opcodes.LoopJump, new int[] { 23 })
      //__//
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 4);
    AssertEqual(c.GetConstants()[0].num, 10);
    AssertEqual(c.GetConstants()[1].num, 0);
    AssertEqual(c.GetConstants()[2].num, 3);
    AssertEqual(c.GetConstants()[3].num, 1);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().num, 7);
  }

  [IsTested()]
  public void TestCompileBooleanInCondition()
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

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.ReturnVal)
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.ReturnVal)
      .TestEmit(Opcodes.Constant, new int[] { 2 })
      .TestEmit(Opcodes.CondJump, new int[] { 6 })
      .TestEmit(Opcodes.FuncCall, new [] { 0,0 })
      .TestEmit(Opcodes.ReturnVal)
      .TestEmit(Opcodes.Jump, new int[] { 4 })
      .TestEmit(Opcodes.FuncCall, new [] { 3,0 })
      .TestEmit(Opcodes.ReturnVal)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 3);
    AssertEqual(c.GetConstants()[0].num, 1);
    AssertEqual(c.GetConstants()[1].num, 2);
    AssertTrue(c.GetConstants()[2].bval);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertTrue(vm.GetStackTop().bval);
  }

  [IsTested()]
  public void TestCompileFunctionCallInCondition()
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

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.ReturnVal)
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.ReturnVal)
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 2 })
      .TestEmit(Opcodes.Greather)
      .TestEmit(Opcodes.CondJump, new int[] { 6 })
      .TestEmit(Opcodes.FuncCall, new [] { 0,0 })
      .TestEmit(Opcodes.ReturnVal)
      .TestEmit(Opcodes.Jump, new int[] { 4 })
      .TestEmit(Opcodes.FuncCall, new [] { 3,0 })
      .TestEmit(Opcodes.ReturnVal)
      .TestEmit(Opcodes.ReturnVal)
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 3);
    AssertEqual(c.GetConstants()[0].num, 1);
    AssertEqual(c.GetConstants()[1].num, 2);
    AssertEqual(c.GetConstants()[2].num, 0);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);

    var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
    vm.Exec("test");
    AssertEqual(vm.GetStackTop().num, 1);
  }

  [IsTested()]
  public void TestFib()
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
    var result = c.GetBytes();
    AssertTrue(result.Length > 0);

    {
      var vm = new VM(result, c.GetConstants(), c.GetFuncBuffer());
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      vm.Exec("test");
      stopwatch.Stop();
      AssertEqual(vm.GetStackTop().num, 610);
      Console.WriteLine("bhl vm fib ticks: {0}", stopwatch.ElapsedTicks);
    }
  }

  ///////////////////////////////////////
  Compiler Compile(string bhl)
  {
    Util.DEBUG = true;
    var ast = Src2AST(bhl);
    var c  = new Compiler();
    c.Compile(ast);
    return c;
  }

  AST Src2AST(string src, GlobalScope globs = null)
  {
    globs = globs == null ? SymbolTable.CreateBuiltins() : globs;

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
    //Console.WriteLine(DynVal.PoolDump());

    //AssertEqual(intp.stack.Count, 0);
    //AssertEqual(DynVal.PoolCount, DynVal.PoolCountFree);
    //AssertEqual(DynValList.PoolCount, DynValList.PoolCountFree);
    //AssertEqual(DynValDict.PoolCount, DynValDict.PoolCountFree);
    //AssertEqual(FuncCtx.PoolCount, FuncCtx.PoolCountFree);
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
    var ahex = ByteArrayToString(a);
    var bhex = ByteArrayToString(b);

    if(!(ahex == bhex))
      throw new Exception("Assertion failed: '" + ahex + "' != '" + bhex + "'");
  }
}
