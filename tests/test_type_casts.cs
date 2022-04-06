using System;
using System.IO;
using System.Text;
using bhl;

public class TestTypeCasts : BHL_TestBase
{
  [IsTested()]
  public void TestBoolToIntTypeCast()
  {
    string bhl = @"
    func int test()
    {
      return (int)true
    }
    ";

    var ts = new Types();
    var c = Compile(bhl, ts);

    var expected = 
      new Compiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, new TypeProxy(ts, "int")) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 2);

    var vm = MakeVM(c, ts);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 1);
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

    var ts = new Types();
    var c = Compile(bhl, ts);

    var expected = 
      new Compiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 7) })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, new TypeProxy(ts, "string")) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.constants.Count, 2);

    var vm = MakeVM(c, ts);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "7");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastFloatToStr()
  {
    string bhl = @"
      
    func string test(float k) 
    {
      return (string)k
    }
    ";

    var vm = MakeVM(bhl);
    var str = Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().str;
    AssertEqual(str, "3");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastFloatToInt()
  {
    string bhl = @"

    func int test(float k) 
    {
      return (int)k
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test", Val.NewFlt(vm, 3.9)).result.PopRelease().num;
    AssertEqual(res, 3);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastIntToAny()
  {
    string bhl = @"
      
    func any test() 
    {
      return (any)121
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 121);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastIntToAnyFuncArg()
  {
    string bhl = @"

    func int foo(any a)
    {
      return (int)a
    }
      
    func int test() 
    {
      return foo(121)
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 121);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastStrToAny()
  {
    string bhl = @"
      
    func any test() 
    {
      return (any)""foo""
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().str;
    AssertEqual(res, "foo");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSimpleIs()
  {
    string bhl = @"
    func bool test() 
    {
      int i = 1
      return i is int
    }
    ";

    var ts = new Types();
    var c = Compile(bhl, ts);

    var expected = 
      new Compiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1+1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.TypeIs, new int[] { ConstIdx(c, new TypeProxy(ts, "int")) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(bhl, ts);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIsAndBuiltinTypes()
  {
    {
      string bhl = @"
      func bool test() 
      {
        string s = ""hey""
        any foo = s
        return foo is string
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func bool test() 
      {
        int s = 42
        any foo = s
        return foo is int
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func bool test() 
      {
        float s = 42.1
        any foo = s
        return foo is float
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func bool test() 
      {
        bool s = true
        any foo = s
        return foo is bool
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func bool test() 
      {
        string s = ""hey""
        any foo = s
        return foo is bool
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestSimpleAsForSelf()
  {
    string bhl = @"
    class Foo
    {
      int foo
    }

    func int test() 
    {
      Foo f = {foo: 14}
      return (f as Foo).foo
    }
    ";

    var ts = new Types();
    var c = Compile(bhl, ts);

    var expected = 
      new Compiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1+1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, new TypeProxy(ts, "Foo")) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 14) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 0 })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.TypeAs, new int[] { ConstIdx(c, new TypeProxy(ts, "Foo")) })
      .EmitThen(Opcodes.GetAttr, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(bhl, ts);
    AssertEqual(14, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIsForChildClass()
  {
    string bhl = @"
    class Bar 
    { }

    class Foo : Bar
    { }

    func bool test() 
    {
      Foo f = {}
      return f is Bar
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestAsForChildClass()
  {
    string bhl = @"
    class Bar 
    {
      int bar
    }

    class Foo : Bar
    {
      int foo
    }

    func int test() 
    {
      Foo f = {foo: 14, bar: 41}
      return (f as Bar).bar
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(41, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIsForChildClassAndInterface()
  {
    string bhl = @"
    interface IBar
    {
      func int GetBar()
    }

    class Bar : IBar 
    {
      func int GetBar()
      {
        return 0
      }
    }

    class Foo : Bar
    { }

    func bool test() 
    {
      Foo f = {}
      return f is IBar
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestAsForChildClassAndInterface()
  {
    string bhl = @"
    interface IBar
    {
      func int GetBar()
    }

    class Bar : IBar 
    {
      int bar

      func int GetBar()
      {
        return this.bar
      }
    }

    class Foo : Bar
    {
      int foo
    }

    func int test() 
    {
      Foo f = {foo: 14, bar: 41}
      return (f as IBar).GetBar()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(41, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestAsAndBuiltinTypes()
  {
    {
      string bhl = @"
      func string test() 
      {
        string s = ""hey""
        any foo = s
        return foo as string
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual("hey", Execute(vm, "test").result.PopRelease().str);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func int test() 
      {
        int s = 42
        any foo = s
        return foo as int
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func float test() 
      {
        float s = 42.1
        any foo = s
        return foo as float
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(42.1, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func bool test() 
      {
        bool s = true
        any foo = s
        return foo as bool
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestAsForFuncSignature()
  {
    {
      string bhl = @"
      func string hey() {
        return ""hey""
      }
      func string test() 
      {
        func string() s = hey
        any foo = s
        return (foo as func string())()
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual("hey", Execute(vm, "test").result.PopRelease().str);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func test() 
      {
        func(string) s = trace
        any foo = s
        //TODO: below line doesn't compile
        //(foo as func(string))(""hey"")
        func(string) tmp = foo as func(string)
        tmp(""hey"")
      }
      ";

      var ts = new Types();
      var log = new StringBuilder();
      BindTrace(ts, log);

      var vm = MakeVM(bhl, ts);
      Execute(vm, "test");
      AssertEqual("hey", log.ToString());
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func string hey() {
        return ""hey""
      }
      func bool test() 
      {
        func string() s = hey
        any foo = s
        return (foo as func int()) == null
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestAsForArray()
  {
    {
      string bhl = @"
      func string test() 
      {
        []string s = [""hey"", ""wow""]
        any foo = s
        return (foo as []string)[1]
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual("wow", Execute(vm, "test").result.PopRelease().str);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func int test() 
      {
        []int s = [10, 20]
        any foo = s
        return (foo as []int)[1]
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(20, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }

    //TODO:
    //{
    //  string bhl = @"
    //  func bool test() 
    //  {
    //    []int s = [10, 20]
    //    any foo = s
    //    return (foo as []string) == null
    //  }
    //  ";

    //  var vm = MakeVM(bhl);
    //  AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    //  CommonChecks(vm);
    //}
  }

  [IsTested()]
  public void TestAsAndAny()
  {
    string bhl = @"
    class Foo
    {
      int foo
    }

    func int test() 
    {
      Foo f = {foo: 14}
      any any_f = f
      return (any_f as Foo).foo
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(14, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestInterfaceAs()
  {
    string bhl = @"
    interface IBar 
    {
      func int bar()
    }

    interface IFoo
    {
      func int foo()
    }

    interface IHey
    {
      func int hey()
    }

    class Wow : IBar, IFoo
    {
      func int bar() {
        return 42
      }

      func int foo() {
        return 24
      }
    }

    func int test() 
    {
      Wow w = {}
      int summ
      IBar b = w as IBar
      if(b != null) {
        summ = summ + b.bar()
      }
      IFoo f = w as IFoo
      if(f != null) {
        summ = summ + f.foo()
      }
      IHey h = w as IHey
      if(h != null) {
        summ = summ + h.hey()
      }
      return summ
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(42+24, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }
}
