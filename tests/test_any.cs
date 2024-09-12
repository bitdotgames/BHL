using System;
using System.Text;
using bhl;

public class TestAny : BHL_TestBase
{
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
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { 0 })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { 0 })
      .EmitThen(Opcodes.Equal)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.compiled.constants.Count, 1);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.result.PopRelease().bval);
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

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 202);
    CommonChecks(vm);
  }
  
  [IsTested()]
  public void TestDynamicArrayOfAny()
  {
    string bhl = @"
      
    func bool test() 
    {
      var ans = new []any [ ""hey"", 1, new []any[10, ""bar""], 200 ]
      return (int)ans[3] == 200
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().bval;
    AssertTrue(res);
    CommonChecks(vm);
  }
}
