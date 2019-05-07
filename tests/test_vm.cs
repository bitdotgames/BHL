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
  public void TestCompileAdd()
  {
    string bhl = @"
    func int test() 
    {
      return 10 + 20
    }
    ";

    var result = Compile(bhl);

    var expected = 
      new Compiler()
      .TestEmit(Opcodes.Constant, new int[] { 0 })
      .TestEmit(Opcodes.Constant, new int[] { 1 })
      .TestEmit(Opcodes.Add)
      .GetBytes();

    AssertTrue(result.Length > 0);
    AssertEqual(result, expected);
  }

  ///////////////////////////////////////
  byte[] Compile(string bhl)
  {
    var ast = Src2AST(bhl);
    return Compile(ast);
  }

  byte[] Compile(AST ast)
  {
    var c = new Compiler();
    c.Compile(ast);
    return c.GetBytes();
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
