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

    Assert.Single(c.compiled.constants);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.True(fb.result.PopRelease().bval);
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
    Assert.True(res);
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
    Assert.True(res);
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
    Assert.Equal(202, res);
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
    Assert.True(res);
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
    Assert.Equal(110, res);
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
    Assert.True(res);
    CommonChecks(vm);
  }
  
  [Fact]
  public void TestCastNativeTypeArrayToAnyArray()
  {
    string bhl = @"
      
    func int test() 
    {
      Color c1 = new Color
      c1.r = 1
      Color c2 = new Color
      c2.r = 10
      Color c3 = new Color
      c3.r = 100
      Color c4 = new Color
      c4.r = 1000

      []Color cs = [c1, c2]

      //converting to []any
      []any anys = ([]any)cs

      //converting back to []Color
      []Color cs2 = ([]Color)anys
      cs2.Add(c3)

      //let's try as casting
      []Color cs3 = anys as []Color
      cs3.Add(c4)

      //let's try some incompatible cast
      []int ints = anys as []int
      if(ints != null) {
        ints.Add(14)
      }
      
      int summ = 0
      foreach(var a in anys) {
        summ += ((Color)a).r
      }
      return summ
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var num = Execute(vm, "test").result.PopRelease().num;
    Assert.Equal(1111, num);
    CommonChecks(vm);
  }
  
  [Fact]
  public void TestCastUserTypeArrayToAnyArray()
  {
    string bhl = @"
    class Color {
      float r
    }
      
    func int test() 
    {
      Color c1 = new Color
      c1.r = 1
      Color c2 = new Color
      c2.r = 10
      Color c3 = new Color
      c3.r = 100
      Color c4 = new Color
      c4.r = 1000

      []Color cs = []

      //casting to []any explicitely
      []any anys = ([]any)cs
      anys.Add(c1)

      //casting implicitely
      anys = cs
      anys.Add(c2)

      //casting back to []Color
      []Color cs2 = ([]Color)anys
      cs2.Add(c3)

      //let's try 'as' casting
      []Color cs3 = anys as []Color
      cs3.Add(c4)

      //let's try some incompatible cast
      []int ints = anys as []int
      if(ints != null) {
        ints.Add(14)
      }
      
      int summ = 0
      foreach(var a in anys) {
        summ += ((Color)a).r
      }
      return summ
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    Assert.Equal(1111, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNeedExplicitCastForAnyArray()
  {
    string bhl = @"
      
    func int test() 
    {
      []int cs
      []any anys

      cs = anys

    }
    ";

    AssertError(() => MakeVM(bhl),
    "incompatible types: '[]int' and '[]any'"
    );
  }
  
  [Fact]
  public void TestCastUserTypeMapToAnyMap()
  {
    string bhl = @"
    class Color {
      float r
    }
      
    func int test() 
    {
      Color c1 = new Color
      c1.r = 1
      Color c2 = new Color
      c2.r = 10
      Color c3 = new Color
      c3.r = 100
      Color c4 = new Color
      c4.r = 1000

      [int]Color cs = []

      //casting to [any]any
      [any]any anys = ([any]any)cs
      anys.Add(1, c1)

      //implicit casting to [any]any
      anys = cs
      anys.Add(2, c2)

      //converting back to [int]Color
      [int]Color cs2 = ([int]Color)anys
      cs2.Add(3, c3)

      //let's try as casting
      [int]Color cs3 = anys as [int]Color
      cs3.Add(4, c4)

      //let's try some incompatible cast
      [int]int ints = anys as [int]int
      if(ints != null) {
        ints.Add(100, 14)
      }
      
      int summ = 0
      foreach(var k, var v in anys) {
        summ += (int)k + ((Color)v).r
      }
      return summ
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    Assert.Equal(10 + 1111, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNeedExplicitCastForAnyMap()
  {
    string bhl = @"
      
    func int test() 
    {
      [string]int cs
      [any]any anys

      cs = anys

    }
    ";

    AssertError(() => MakeVM(bhl),
    "incompatible types: '[string]int' and '[any]any'"
    );
  }
}
