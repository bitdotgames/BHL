using System;
using System.Collections.Generic;
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

    var c = Compile(bhl);

    var ts = new Types();

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/, 12 /*exit offset*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
      .EmitThen(Opcodes.TypeCast, new int[] { TypeIdx(c, ts.T("int")), 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.compiled.constants.Count, 1);

    var vm = MakeVM(c);
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

    var c = Compile(bhl);

    var ts = new Types();

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/, 12 /*exit offset*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 7) })
      .EmitThen(Opcodes.TypeCast, new int[] { TypeIdx(c, ts.T("string")), 1 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.compiled.constants.Count, 1);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "7");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImplicitIntToStringTypeCastLhs()
  {
    string bhl = @"
    func string test()
    {
      return 7 + """"
    }
    ";

    var c = Compile(bhl);

    var ts = new Types();

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/, 17 /*exit offset*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 7) })
      .EmitThen(Opcodes.TypeCast, new int[] { TypeIdx(c, ts.T("string")), 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.compiled.constants.Count, 2);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "7");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImplicitIntToStringTypeCastRhs()
  {
    string bhl = @"
    func string test()
    {
      return """" + 7
    }
    ";

    var c = Compile(bhl);

    var ts = new Types();

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/, 17 /*exit offset*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "") })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 7) })
      .EmitThen(Opcodes.TypeCast, new int[] { TypeIdx(c, ts.T("string")), 0 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    AssertEqual(c.compiled.constants.Count, 2);

    var vm = MakeVM(c);
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
  public void TestStrDecIsForbidden()
  {
    string bhl = @"
    func test(int k) 
    {
      return ""foo"" - k
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incompatible types: 'string' and 'int'",
      new PlaceAssert(bhl, @"
      return ""foo"" - k
---------------------^"
      )
    );
  }

  [IsTested()]
  public void TestStrConcatImplicitTypes()
  {
    SubTest("int", () => {
      string bhl = @"
      func string test() 
      {
        int a = 11
        return ""what"" + a + ""now""
      }
      ";

      var vm = MakeVM(bhl);
      var res = Execute(vm, "test").result.PopRelease().str;
      AssertEqual(res, "what11now");
      CommonChecks(vm);
    });

    SubTest("int and float", () => {
      string bhl = @"
      func string test() 
      {
        int a = 11
        float b = 12.1
        return ""what"" + a + ""now"" + b
      }
      ";

      var vm = MakeVM(bhl);
      var res = Execute(vm, "test").result.PopRelease().str;
      AssertEqual(res, "what11now12.1");
      CommonChecks(vm);
    });

    SubTest("arr count 1", () => {
      string bhl = @"
      func string test() 
      {
        []int arr = [1, 2, 3]
        return ""what"" + arr.Count
      }
      ";

      var vm = MakeVM(bhl);
      var res = Execute(vm, "test").result.PopRelease().str;
      AssertEqual(res, "what3");
      CommonChecks(vm);
    });

    SubTest("arr count 2", () => {
      string bhl = @"
      func string test() 
      {
        []int arr = [1, 2, 3]
        return arr.Count + ""now""
      }
      ";

      var vm = MakeVM(bhl);
      var res = Execute(vm, "test").result.PopRelease().str;
      AssertEqual(res, "3now");
      CommonChecks(vm);
    });

    SubTest("+=", () => {
      string bhl = @"
      func string test() 
      {
        string s = ""???""
        s += 100
        return s
      }
      ";

      var vm = MakeVM(bhl);
      var res = Execute(vm, "test").result.PopRelease().str;
      AssertEqual(res, "???100");
      CommonChecks(vm);
    });

    SubTest("empty string var", () => {
      string bhl = @"
      func string test() 
      {
        string s
        return s + ""hey"" + 1
      }
      ";

      var vm = MakeVM(bhl);
      var res = Execute(vm, "test").result.PopRelease().str;
      AssertEqual(res, "hey1");
      CommonChecks(vm);
    });

    SubTest("bools", () => {
      string bhl = @"
      func string test() 
      {
        bool t = true
        bool f = false
        return f + ""hey"" + t
      }
      ";

      var vm = MakeVM(bhl);
      var res = Execute(vm, "test").result.PopRelease().str;
      AssertEqual(res, "0hey1");
      CommonChecks(vm);
    });

    SubTest("enums", () => {
      string bhl = @"
      enum Foo {
        A = 10
        B = 20
      }
      func string test() 
      {
        return Foo.A + ""hey"" + Foo.B
      }
      ";

      var vm = MakeVM(bhl);
      var res = Execute(vm, "test").result.PopRelease().str;
      AssertEqual(res, "10hey20");
      CommonChecks(vm);
    });
  }

  [IsTested()]
  public void TestImplicitIntArgsCast()
  {
    string bhl = @"

    func float foo(float a, float b)
    {
      return a + b
    }
      
    func float test() 
    {
      return foo(1, 2.0)
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 3);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImplicitIntArgsCastNativeFunc()
  {
    string bhl = @"

    func float bar(float a)
    {
      return a
    }
      
    func float test() 
    {
      return bar(a : min(1, 0.3))
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindMin(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 0.3f);
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
  public void TestCastIntsDivResultToToInt()
  {
    string bhl = @"

    func int test() 
    {
      int a = 11
      int b = 2
      return a / b
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 5);
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
  public void TestInternalIntIsLong()
  {
    string bhl = @"
    func int test() 
    {
      return (int)(2147483647 + 2147483647)
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 2147483647L + 2147483647L);
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

    var c = Compile(bhl);

    var ts = new Types();
    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1+1 /*args info*/, 15 /*exit offset*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.TypeIs, new int[] { TypeIdx(c, ts.T("int")) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(bhl);
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

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1+1 /*args info*/, 26 /*exit offset*/})
      .EmitThen(Opcodes.New, new int[] { TypeIdx(c, c.ns.T("Foo")) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 14) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 0 })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.TypeAs, new int[] { TypeIdx(c, c.ns.T("Foo")), 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(bhl);
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
  
  public interface INativeFoo
  {}

  public class NativeFoo : INativeFoo
  {
    public int foo = 10;
  }
  public class NativeBar : NativeFoo 
  {
    public int bar = 100;
  }

  [IsTested()]
  public void TestIsForChildNativeClass()
  {
    string bhl = @"
    func bool test() 
    {
      var f = NewFooHiddenBar();
      return f is Bar
    }

    func bool test2() 
    {
      var b = NewFooHiddenBar() as Bar;
      return b != null
    }

    func int test3() 
    {
      var b = NewFooHiddenBar() as Bar;
      return b.bar
    }
    ";

    var ts_fn = new Action<Types>((ts) => { 
      var cl1 = new ClassSymbolNative(new Origin(), "Bar", ts.T("Foo"),
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          v.SetObj(new NativeBar(), type);
        },
        typeof(NativeBar)
      );
      cl1.Define(new FieldSymbol(new Origin(), "bar", ts.T("int"), 
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var bar = (NativeBar)ctx.obj;
          v.SetInt(bar.bar);
        },
        null
      ));
      ts.ns.Define(cl1);

      var cl2 = new ClassSymbolNative(new Origin(), "Foo", null,
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          v.SetObj(new NativeFoo(), type);
        },
        typeof(NativeFoo)
      );
      cl2.Define(new FieldSymbol(new Origin(), "foo", ts.T("int"), 
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var foo = (NativeFoo)ctx.obj;
          v.SetInt(foo.foo);
        },
        null
      ));
      ts.ns.Define(cl2);

      cl1.Setup();
      cl2.Setup();

      var fn = new FuncSymbolNative(new Origin(), "NewFooHiddenBar", ts.T("Foo"), 
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
          stack.Push(Val.NewObj(frm.vm, new NativeBar(), ts.T("Foo").Get()));
          return null;
        }
      );
      ts.ns.Define(fn);
    });

    SubTest(() => {
      var vm = MakeVM(bhl, ts_fn);
      Assert(Execute(vm, "test").result.PopRelease().bval);
      CommonChecks(vm);
    });

    SubTest(() => {
      var vm = MakeVM(bhl, ts_fn);
      Assert(Execute(vm, "test2").result.PopRelease().bval);
      CommonChecks(vm);
    });

    SubTest(() => {
      var vm = MakeVM(bhl, ts_fn);
      AssertEqual(100, Execute(vm, "test3").result.PopRelease().num);
      CommonChecks(vm);
    });
  }
  
  [IsTested()]
  public void TestIsForClassImplementingNativeInterface()
  {
    string bhl = @"
      func bool test() {
        IFoo foo = new Foo
        return foo is Foo 
      }
    ";

    var ts_fn = new Action<Types>((ts) => { 
      var ifs = new InterfaceSymbolNative(
          new Origin(),
          "IFoo", 
          null,
          typeof(INativeFoo)
      );
      ts.ns.Define(ifs);
      ifs.Setup();

      var cl = new ClassSymbolNative(
        new Origin(), 
        "Foo", 
        new List<ProxyType>(){ ts.T("IFoo") },
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          v.SetObj(new NativeFoo(), type);
        },
        typeof(NativeFoo)
      );
      ts.ns.Define(cl);
      cl.Setup();
    });

    var vm = MakeVM(bhl, ts_fn);
    Assert(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  public interface INativeWow {}
  public class NativeWow : INativeWow {}

  [IsTested()]
  public void TestIsForClassImplementingNativeInterfaceReturnedAsInterface()
  {
    string bhl = @"
      func bool test() {
        IWow wow = MakeIWow()
        return wow is Wow
      }
    ";

    var ts_fn = new Action<Types>((ts) => { 
      var ifs = new InterfaceSymbolNative(
          new Origin(),
          "IWow", 
          null,
          typeof(INativeWow)
      );
      ts.ns.Define(ifs);
      ifs.Setup();

      var cl = new ClassSymbolNative(new Origin(), "Wow", new List<ProxyType>(){ ts.T("IWow") },
        native_type: typeof(NativeWow),
        creator: delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          v.SetObj(new NativeWow(), type);
        }
      );
      ts.ns.Define(cl);
      cl.Setup();

      var fn = new FuncSymbolNative(new Origin(), "MakeIWow", ts.T("IWow"), 
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
          stack.Push(Val.NewObj(frm.vm, new NativeWow(), ts.T("IWow").Get()));
          return null;
        }
      );
      ts.ns.Define(fn);
    });

    var vm = MakeVM(bhl, ts_fn);
    Assert(Execute(vm, "test").result.PopRelease().bval);
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

    func Bar make_bar() 
    {
      Foo f = {foo: 14, bar: 41}
      return f; 
    }

    func int test() 
    {
      Foo f = {foo: 14, bar: 41}
      return (f as Bar).bar
    }

    func int test2() 
    {
      Bar b = make_bar()
      return (b as Foo).foo
    }

    func int test3() 
    {
      Bar b = make_bar()
      return (b as Bar).bar
    }
    ";

    SubTest(() => {
      var vm = MakeVM(bhl);
      AssertEqual(41, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    });

    SubTest(() => {
      var vm = MakeVM(bhl);
      AssertEqual(14, Execute(vm, "test2").result.PopRelease().num);
      CommonChecks(vm);
    });

    SubTest(() => {
      var vm = MakeVM(bhl);
      AssertEqual(41, Execute(vm, "test3").result.PopRelease().num);
      CommonChecks(vm);
    });
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

  [IsTested()]
  public void TestCastNullToUserType()
  {
    string bhl = @"
    class Foo { }

    func bool test() 
    {
      any o = null
      Foo f = (Foo)o
      return f == null
    }
    ";

    var vm = MakeVM(bhl);
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBadCastInRuntime()
  {
    string bhl = @"
    class Bar { }
    class Foo { }

    func test() 
    {
      Bar b = {}
      any o = b
      Foo f = (Foo)o
    }
    ";

    var vm = MakeVM(bhl);
    AssertError<Exception>(
      delegate() { 
        Execute(vm, "test");
      },
      "Invalid type cast: type 'Bar' can't be cast to 'Foo'"
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

    var ts_fn = new Action<Types>((ts) => {
      BindColorAlpha(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
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

    var ts_fn = new Action<Types>((ts) => {
      BindColorAlpha(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
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

    var ts_fn = new Action<Types>((ts) => {
      BindColorAlpha(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
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

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);

      {
        var cl = new ClassSymbolNative(new Origin(), "Foo", null,
          delegate(VM.Frame frm, ref Val v, IType type) 
          { 
            v.SetObj(null, type);
          }
        );
        ts.ns.Define(cl);
      }
    });

    AssertError<Exception>(
       delegate() {
         Compile(bhl, ts_fn);
       },
      "incompatible types: 'Foo' and 'Color'",
      new PlaceAssert(bhl, @"
      Foo tmp = new Color
----------^"
      )
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

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);

      {
        var cl = new ClassSymbolNative(new Origin(), "Foo", null,
          delegate(VM.Frame frm, ref Val v, IType type) 
          { 
            v.SetObj(null, type);
          }
        );
        ts.ns.Define(cl);
      }
    });

    AssertError<Exception>(
       delegate() {
         Compile(bhl, ts_fn);
       },
      "incompatible types for casting",
      new PlaceAssert(bhl, @"
      Foo tmp = (Foo)new Color
----------------^"
      )
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

      var log = new StringBuilder();
      var ts_fn = new Action<Types>((ts) => {
        BindTrace(ts, log);
      });

      var vm = MakeVM(bhl, ts_fn);
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
      "type 'Foo' not found",
      new PlaceAssert(bhl, @"
      Type t = typeof(Foo)
----------------------^"
      )
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
    SubTest(() =>
    {
      string bhl = @"
      import ""std""

      func string test()
      {
        int a
        Type t = std.GetType(a)
        return t.Name
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual("int", Execute(vm, "test").result.PopRelease().str);
      CommonChecks(vm);
    });

    SubTest(() =>
    {
      string bhl = @"
      import ""std""

      func string test()
      {
        string a
        Type t = std.GetType(a)
        return t.Name
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual("string", Execute(vm, "test").result.PopRelease().str);
      CommonChecks(vm);
    });

    SubTest(() =>
    {
      string bhl = @"
      import ""std""

      func string test()
      {
        func() a 
        Type t = std.GetType(a)
        return t.Name
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual("func void()", Execute(vm, "test").result.PopRelease().str);
      CommonChecks(vm);
    });
  }

  [IsTested()]
  public void TestMissingTypePropertyIsNotConfusedWithTypeFunc1()
  {
    string bhl = @"
    class Foo {
      int int_type
    }

    func test() {
      var foo = new Foo
      foo.type = 1
    }
    ";

    AssertError<Exception>(
       delegate() {
         Compile(bhl);
       },
      "symbol 'type' not resolved",
      new PlaceAssert(bhl, @"
      foo.type = 1
----------^"
      )
    );
  }

  [IsTested()]
  public void TestMissingTypePropertyIsNotConfusedWithTypeFunc2()
  {
    string bhl = @"
    class Foo {
      int int_type
    }

    func Foo foo() {
      return {}
    }

    func bool test() {
      return foo().type == 1
    }
    ";

    AssertError<Exception>(
       delegate() {
         Compile(bhl);
       },
      "symbol 'type' not resolved",
      new PlaceAssert(bhl, @"
      return foo().type == 1
-------------------^"
      )
    );
  }

  public void BindEnum(Types ts)
  {
    var en = new EnumSymbolScript(new Origin(), "EnumState");
    ts.ns.Define(en);

    en.Define(new EnumItemSymbol(new Origin(), "SPAWNED",  10));
    en.Define(new EnumItemSymbol(new Origin(), "SPAWNED2", 20));
  }

  [IsTested()]
  public void TestCastEnumToInt()
  {
    string bhl = @"
      
    func int test() 
    {
      return (int)EnumState.SPAWNED2
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindEnum(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 20);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastEnumToFloat()
  {
    string bhl = @"
      
    func float test() 
    {
      return (float)EnumState.SPAWNED
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindEnum(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 10);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastEnumToStr()
  {
    string bhl = @"
      
    func string test() 
    {
      return (string)EnumState.SPAWNED2
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindEnum(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test").result.PopRelease().str;
    AssertEqual(res, "20");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserEnumIntCast()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
    }
      
    func int test() 
    {
      return (int)Foo.B + (int)Foo.A
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 3);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIntCastToUserEnum()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
    }
      
    func int test() 
    {
      Foo f = (Foo)2
      return (int)f
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCastFromEnumToIntIsNoOp()
  {
    string bhl = @"
    enum Foo {
      A = 1
    }

    func int test() {
      return (int)Foo.A
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/, 7 /*exit offset*/})
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBugCastForTemporaryInstance()
  {
    string bhl = @"

    class StateHandler {
    }

    class PlayerState_AbilityBase : StateHandler
    {
      func StateHandler Init() {
        return this
      }
    }

    func StateHandler GetShootArrowState() {
      return (new PlayerState_AbilityBase).Init()
    }
      
    func bool test() {
      return ((PlayerState_AbilityBase)GetShootArrowState()) != null
    }
    ";
    
    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().bval;
    AssertTrue(res);
    CommonChecks(vm);
  }
}
