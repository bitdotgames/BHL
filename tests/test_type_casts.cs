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
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, ts.T("int")), 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
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
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 7) })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, ts.T("string")), 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
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
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1+1 /*args info*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.TypeIs, new int[] { ConstIdx(c, ts.T("int")) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
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
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1+1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, c.ns.T("Foo")) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 14) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 0 })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.TypeAs, new int[] { ConstIdx(c, c.ns.T("Foo")) })
      .EmitThen(Opcodes.GetAttr, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
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
  public void TestCastToChildTypeAndCallMethod()
  {
    string bhl = @"
    class Bar 
    {
      int bar
    }

    class Foo : Bar
    {
      int foo

      func int getFoo() {
        return this.foo
      }
    }

    func Foo cast(Bar bar)
    {
      return (Foo)bar;
    }

    func int test() 
    {
      Foo f = {foo: 14, bar: 41}
      return cast(f).getFoo()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(14, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastAnyToChildTypeAndCallMethod()
  {
    string bhl = @"
    class Bar 
    {
      int bar
    }

    class Foo : Bar
    {
      int foo

      func int getFoo() {
        return this.foo
      }
    }

    func Foo cast(any bar)
    {
      return (Foo)bar;
    }

    func int test() 
    {
      Foo f = {foo: 14, bar: 41}
      return cast(f).getFoo()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(14, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  //TODO:
  //[IsTested()]
  public void TestBadCastInRuntime()
  {
    string bhl = @"
    class Bar 
    {
      int bar
    }

    class Foo : Bar
    {
      int foo

      func int getFoo() {
        return this.foo
      }
    }

    func Foo cast(any bar)
    {
      return (Foo)bar;
    }

    func int test() 
    {
      Bar b = {bar: 41}
      return cast(b).getFoo()
    }
    ";

    var vm = MakeVM(bhl);
    AssertError<Exception>(
      delegate() { 
        Execute(vm, "test");
      },
      "Invalid type cast"
    );
  }

  [IsTested()]
  public void TestNativeChildClassImplicitBaseCast()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      Color c = new ColorAlpha
      c.r = k*1
      c.g = k*100
      return c.r + c.g
    }
    ";

    var ts = new Types();
    
    BindColorAlpha(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 202);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeChildClassExplicitBaseCast()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      Color c = (Color)new ColorAlpha
      c.r = k*1
      c.g = k*100
      return c.r + c.g
    }
    ";

    var ts = new Types();
    
    BindColorAlpha(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 202);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBindChildClassExplicitDownCast()
  {
    string bhl = @"
      
    func float test() 
    {
      ColorAlpha orig = new ColorAlpha
      orig.a = 1000
      Color tmp = (Color)orig
      tmp.r = 1
      tmp.g = 100
      ColorAlpha c = (ColorAlpha)tmp
      return c.r + c.g + c.a
    }
    ";

    var ts = new Types();
    
    BindColorAlpha(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 1101);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIncompatibleImplicitClassCast()
  {
    string bhl = @"
      
    func float test() 
    {
      Foo tmp = new Color
      return 1
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    {
      var cl = new ClassSymbolNative("Foo", null,
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          v.SetObj(null, type);
        }
      );
      ts.ns.Define(cl);
    }

    AssertError<Exception>(
       delegate() {
         Compile(bhl, ts);
       },
      "incompatible types"
    );
  }

  [IsTested()]
  public void TestIncompatibleExplicitClassCast()
  {
    string bhl = @"
      
    func test() 
    {
      Foo tmp = (Foo)new Color
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    {
      var cl = new ClassSymbolNative("Foo", null,
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          v.SetObj(null, type);
        }
      );
      ts.ns.Define(cl);
    }

    AssertError<Exception>(
       delegate() {
         Compile(bhl, ts);
       },
      "incompatible types for casting"
    );
  }


  [IsTested()]
  public void TestAsForChildClassObjReturnedFromMethod()
  {
    {
      string bhl = @"
      class GameObject {
        func Component GetComponentByName(string name) {
          if(name == ""Canvas"") {
            return new Canvas
          } else {
            return null
          }
        }
      }

      class Component {
      }

      class Canvas : Component {
      }

      func bool test() {
        GameObject go = {}
        Canvas wcanvas = go.GetComponentByName(""Canvas"") as Canvas
        return wcanvas is Canvas
      }
      ";

      var vm = MakeVM(bhl);
      AssertTrue(Execute(vm, "test").result.PopRelease().bval);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      class GameObject {
        func Component GetComponentByName(string name) {
          if(name == ""Canvas"") {
            return new Canvas
          } else {
            return null
          }
        }
      }

      class Component {
      }

      class Canvas : Component {
      }

      func bool test() {
        GameObject go = {}
        return go.GetComponentByName(""Canvas"") is Canvas
      }
      ";

      var vm = MakeVM(bhl);
      AssertTrue(Execute(vm, "test").result.PopRelease().bval);
      CommonChecks(vm);
    }
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

    {
      string bhl = @"
      func bool test() 
      {
        []int s = [10, 20]
        any foo = s
        return (foo as []string) == null
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestAsForArrayOfFuncPtrs()
  {
    {
      string bhl = @"
      func string test() 
      {
        []func string() s = [func string() { return ""hey"" }, func string () { return ""wow"" } ]
        any foo = s
        return (foo as []func string())[1]()
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual("wow", Execute(vm, "test").result.PopRelease().str);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func bool test() 
      {
        []func() s = [func() { }]
        any foo = s
        return (foo as []func int()) == null
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }
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

  [IsTested()]
  public void TestBasicTypeof()
  {
    {
      string bhl = @"
      func string test()
      {
        Type t = typeof(int)
        return t.Name
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual("int", Execute(vm, "test").result.PopRelease().str);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func string test()
      {
        Type t = typeof(string)
        return t.Name
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual("string", Execute(vm, "test").result.PopRelease().str);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func string test()
      {
        Type t = typeof(func())
        return t.Name
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual("func void()", Execute(vm, "test").result.PopRelease().str);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func bool test()
      {
        return typeof(string) == typeof(string)
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
        return typeof(int) != typeof(string)
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestTypeNotFoundInTypeof()
  {
    string bhl = @"
    func test()
    {
      Type t = typeof(Foo)
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "type 'Foo' not found"
    );
  }

  [IsTested()]
  public void TestPassTypeAsArg()
  {
    string bhl = @"
    func string name(Type t) 
    {
      return t.Name
    }

    func string test()
    {
      return name(typeof(int))
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("int", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBasicType()
  {
    {
      string bhl = @"
      func string test()
      {
        int a
        Type t = type(a)
        return t.Name
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual("int", Execute(vm, "test").result.PopRelease().str);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func string test()
      {
        string a
        Type t = type(a)
        return t.Name
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual("string", Execute(vm, "test").result.PopRelease().str);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func string test()
      {
        func() a 
        Type t = type(a)
        return t.Name
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual("func void()", Execute(vm, "test").result.PopRelease().str);
      CommonChecks(vm);
    }
  }
}
