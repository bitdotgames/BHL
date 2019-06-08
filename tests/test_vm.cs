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
  public void TestCompileFunctionCall()
  {
    string bhl = @"
    func int test() 
    {
      return 1
    }
    int res = test()
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var func_code = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .GetBytes();

    var program_code = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.FuncCall, new int[] { 0 })
      .TestEmit(Opcodes.SetVar, new int[] { 1 })
      .GetBytes();

    var expected = new byte[func_code.Length + program_code.Length];
    Array.Copy(func_code, expected, func_code.Length);
    Array.Copy(program_code, 0, expected, func_code.Length, program_code.Length);

    AssertEqual(c.GetConstants().Count, 2);
    AssertEqual((double)c.GetConstants()[0].nval, 1);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);
  }

  [IsTested()]
  public void TestCompileConstant()
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
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 2);
    AssertEqual((double)c.GetConstants()[0].nval, 123);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);
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
      .TestEmit(Opcodes.Constant, new int[] { 2 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 3);
    AssertEqual((double)c.GetConstants()[0].nval, 10);
    AssertEqual((double)c.GetConstants()[1].nval, 20);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);
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
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 2);
    AssertEqual((double)c.GetConstants()[0].nval, 10);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);
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
      .TestEmit(Opcodes.Constant, new int[] { 2 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 3);
    AssertEqual((double)c.GetConstants()[0].nval, 20);
    AssertEqual((double)c.GetConstants()[1].nval, 10);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);
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
      .TestEmit(Opcodes.Constant, new int[] { 2 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 3);
    AssertEqual((double)c.GetConstants()[0].nval, 20);
    AssertEqual((double)c.GetConstants()[1].nval, 10);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);
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
      .TestEmit(Opcodes.Constant, new int[] { 2 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 3);
    AssertEqual((double)c.GetConstants()[0].nval, 10);
    AssertEqual((double)c.GetConstants()[1].nval, 20);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);
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
      .TestEmit(Opcodes.Constant, new int[] { 3 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .GetBytes();

    AssertTrue(result.Length > 0);
    AssertEqual(c.GetConstants().Count, 4);
    AssertEqual((double)c.GetConstants()[0].nval, 10);
    AssertEqual((double)c.GetConstants()[1].nval, 20);
    AssertEqual((double)c.GetConstants()[2].nval, 30);
    AssertEqual(result, expected);
  }

  [IsTested()]
  public void TestCompileWriteReadVar()
  {
    string bhl = @"
    func int test() 
    {
      int a = 123
      return a
    }
    ";

    var c = Compile(bhl);

    var result = c.GetBytes();

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.SetVar, new int[] { 0 })
      .TestEmit(Opcodes.GetVar, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.SetVar, new int[] { 1 })
      .GetBytes();

    AssertEqual(c.GetConstants().Count, 2);
    AssertEqual((double)c.GetConstants()[0].nval, 123);
    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);
  }

  ///////////////////////////////////////
  Compiler Compile(string bhl)
  {
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
