using System;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using Antlr4.Runtime;
using bhl;

public class BHL_TestVM : BHL_TestBase
{
  [IsTested()]
  public void TestCompile()
  {
    //string bhl = @"
    //  
    //func int test() 
    //{
    //  return 100
    //}
    //";

    //var bc = Compile(bhl);
  }

  [IsTested()]
  public void TestReturnNum()
  {
    //string bhl = @"
    //  
    //func int test() 
    //{
    //  return 100
    //}
    //";

    //var vm = Compile(bhl);
    //var node = vm.GetFuncCallNode("test");
    //var n = ExtractNum(ExecNode(node));

    //AssertEqual(n, 100);
    //CommonChecks(vm);
  }

  byte[] Compile(string bhl)
  {
    var ast = Src2AST(bhl);
    return Compile(ast);
  }

  byte[] Compile(AST ast)
  {
    return null;
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
}
