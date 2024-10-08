using System;
using bhl;
using Xunit;

public class TestAny : BHL_TestBase
{
  [Fact]
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

  [Fact]
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

  [Fact]
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

  [Fact]
  public void TestCastNativeClassToAny()
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
  
  [Fact]
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
  
  [Fact]
  public void TestCastAnyNativeTypeReturnedFromBindings()
  {
    string bhl = @"
      
    func float test() 
    {
      any tmp = mkcolor_any()
      Color c = (Color)tmp
      c.r = 10
      c.g = 100
      return c.g + c.r 
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
      
      var fn = new FuncSymbolNative(new Origin(), "mkcolor_any", Types.Any,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
          {
            var v = Val.NewObj(frm.vm, new Color(), Types.Any);
            stack.Push(v);
            return null;
          }
      );
      ts.ns.Define(fn);
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 110);
    CommonChecks(vm);
  }
  
  [Fact]
  public void TestCastAnyNativeTypeReturnedFromBindingsAsNull()
  {
    string bhl = @"
      
    func bool test() 
    {
      any tmp = mkcolor_null()
      Color c = (Color)tmp
      return c == null
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test").result.PopRelease().bval;
    AssertTrue(res);
    CommonChecks(vm);
  }
}
