using System;           
using System.Text;
using System.Collections.Generic;
using bhl;

public class TestSpan : BHL_TestBase
{
  [IsTested()]
  public void TestSpan3Args()
  {
    string bhl = @"
    func int test(int a, int b, int c) 
    {
      return a + b + c
    }
    ";

    var comp = Compile(bhl);
    var vm = MakeVM(comp);
    int a = 1, b = 2, c = 3;
    var expected = a + b + c;
    var args = new Val[32];
    args[0] = Val.NewNum(vm, a);
    args[1] = Val.NewNum(vm, b);
    args[2] = Val.NewNum(vm, c);
    var fb = vm.Start("test", args.AsSpan(0, 3));
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, expected);
    CommonChecks(vm);
  }
  
  [IsTested()]
  public void TestSpan1Args()
  {
    string bhl = @"
    func int test(int a) 
    {
      return a
    }
    ";

    var comp = Compile(bhl);
    var vm = MakeVM(comp);
    int a = 1, b = 2, c = 3;
    var expected = a;
    var args = new Val[32];
    args[0] = Val.NewNum(vm, a);
    args[1] = Val.NewNum(vm, b);
    args[2] = Val.NewNum(vm, c);
    var fb = vm.Start("test", args.AsSpan(0, 1));
    args[1].Release();
    args[2].Release();
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, expected);
    CommonChecks(vm);
  }
}