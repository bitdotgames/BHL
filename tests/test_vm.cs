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
