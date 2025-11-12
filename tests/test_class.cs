using System;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using bhl;
using Xunit;

public class TestClass : BHL_TestBase
{
  [Fact]
  public void TestEmptyUserClass()
  {
    {
      string bhl = @"
      class Foo {}

      func bool test()
      {
        Foo f = new Foo
        return f != null
      }
      ";

      var c = Compile(bhl);

      var expected =
          new ModuleCompiler()
            .UseCode()
            .EmitChain(Opcodes.Frame, new int[] { 1, 1 })
            .EmitChain(Opcodes.New, new int[] { TypeIdx(c, c.ns.T("Foo")) })
            .EmitChain(Opcodes.SetVar, new int[] { 0 })
            .EmitChain(Opcodes.GetVar, new int[] { 0 })
            .EmitChain(Opcodes.Constant, new int[] { 0 })
            .EmitChain(Opcodes.EqualEx)
            .EmitChain(Opcodes.UnaryNot)
            .EmitChain(Opcodes.Return)
        ;
      AssertEqual(c, expected);

      var vm = MakeVM(c);
      var fb = vm.Start("test");
      Assert.False(vm.Tick());
      Assert.True(fb.Stack.Pop());
      CommonChecks(vm);
    }

    {
      string bhl = @"

      class Foo { }

      func bool test()
      {
        Foo f = new Foo
        return f != null
      }
      ";

      var vm = MakeVM(bhl);
      var fb = vm.Start("test");
      Assert.False(vm.Tick());
      Assert.True(fb.Stack.Pop());
      CommonChecks(vm);
    }
  }

  [Fact]
  public void TestUserClassBindConflict()
  {
    string bhl = @"

    class Foo {}

    func test()
    {
      Foo f = {}
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindFoo(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      "already defined symbol 'Foo'",
      new PlaceAssert(bhl, @"
    class Foo {}
----------^"
      )
    );
  }

  [Fact]
  public async Task TestCallFreeFuncFromMethodWithSameName()
  {
    string bhl1 = @"
    func int Calc(int i) {
      return i
    }
    ";

    string bhl2 = @"
    import ""bhl1""

    namespace bar {
      class Foo {
        func int Calc() {
          return Calc(10)
        }
      }
    }

    func int test()
    {
      bar.Foo f = {}
      return f.Calc()
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );

    vm.LoadModule("bhl2");

    Assert.Equal(10, (int)Execute(vm, "test").Stack.Pop());
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestCallFreeFuncFromStaticMethodWithSameName()
  {
    string bhl1 = @"
    func int Calc(int i) {
      return i
    }
    ";

    string bhl2 = @"
    import ""bhl1""

    namespace bar {
      class Foo {
        static func int Calc() {
          return ..Calc(10) //explicit global free func call
        }
      }
    }

    func int test()
    {
      return bar.Foo.Calc()
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );

    vm.LoadModule("bhl2");

    Assert.Equal(10, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSeveralEmptyUserClasses()
  {
    string bhl = @"

    class Foo {}
    class Bar{
    }

    func bool test()
    {
      Foo f = {}
      Bar b = {
      }
      return f != null && b != null
    }
    ";

    var vm = MakeVM(bhl);
    Assert.True(Execute(vm, "test").Stack.Pop());
    CommonChecks(vm);
  }

  [Fact]
  public void TestSelfInheritanceIsNotAllowed()
  {
    string bhl = @"
    class Foo : Foo {}
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "self inheritance is not allowed",
      new PlaceAssert(bhl, @"
    class Foo : Foo {}
----------------^"
      )
    );
  }

  [Fact]
  public void TestUserClassWithSimpleMembers()
  {
    string bhl = @"

    class Foo {
      int Int
      float Flt
      string Str
    }

    func test()
    {
      Foo f = new Foo
      f.Int = 10
      f.Flt = 14.2
      f.Str = ""Hey""
      trace((string)f.Int + "";"" + (string)f.Flt + "";"" + f.Str)
    }
    ";

    var log = new StringBuilder();
    FuncSymbolNative fn = null;
    var ts_fn = new Action<Types>((ts) => { fn = BindTrace(ts, log); });
    var c = Compile(bhl, ts_fn);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitChain(Opcodes.Frame, new int[] { 1, 0})
          .EmitChain(Opcodes.New, new int[] { TypeIdx(c, c.ns.T("Foo")) })
          .EmitChain(Opcodes.SetVar, new int[] { 0 })
          .EmitChain(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitChain(Opcodes.GetVar, new int[] { 0 })
          .EmitChain(Opcodes.SetAttr, new int[] { 0 })
          .EmitChain(Opcodes.Constant, new int[] { ConstIdx(c, 14.2) })
          .EmitChain(Opcodes.GetVar, new int[] { 0 })
          .EmitChain(Opcodes.SetAttr, new int[] { 1 })
          .EmitChain(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
          .EmitChain(Opcodes.GetVar, new int[] { 0 })
          .EmitChain(Opcodes.SetAttr, new int[] { 2 })
          .EmitChain(Opcodes.GetVar, new int[] { 0 })
          .EmitChain(Opcodes.GetAttr, new int[] { 0 })
          .EmitChain(Opcodes.TypeCast, new int[] { TypeIdx(c, c.ns.T("string")), 1 })
          .EmitChain(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
          .EmitChain(Opcodes.Add)
          .EmitChain(Opcodes.GetVar, new int[] { 0 })
          .EmitChain(Opcodes.GetAttr, new int[] { 1 })
          .EmitChain(Opcodes.TypeCast, new int[] { TypeIdx(c, c.ns.T("string")), 1 })
          .EmitChain(Opcodes.Add)
          .EmitChain(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
          .EmitChain(Opcodes.Add)
          .EmitChain(Opcodes.GetVar, new int[] { 0 })
          .EmitChain(Opcodes.GetAttr, new int[] { 2 })
          .EmitChain(Opcodes.Add)
          .EmitChain(Opcodes.CallGlobNative, new int[] { c.ts.module.nfunc_index.IndexOf(fn), 1 })
          .EmitChain(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts_fn);
    vm.Start("test");
    Assert.False(vm.Tick());
    AssertEqual("10;14.2;Hey", log.ToString().Replace(',', '.') /*locale issues*/);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassMemberOperations()
  {
    string bhl = @"

    class Foo {
      float b
    }

    func float test()
    {
      Foo f = { b : 101 }
      f.b = f.b + 1
      return f.b
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(102, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserSeveralClassMembersOperations()
  {
    string bhl = @"

    class Foo {
      float b
      int c
    }

     class Bar {
       float a
     }

    func float test()
    {
      Foo f = { c : 2, b : 101.5 }
      Bar b = { a : 10 }
      f.b = f.b + f.c + b.a
      return f.b
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(113.5, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestBindNativeChildClass()
  {
    string bhl = @"

    func float test(float k)
    {
      ColorAlpha c = new ColorAlpha
      c.r = k*1
      c.g = k*100
      c.a = 1000
      return c.r + c.g + c.a
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColorAlpha(ts); });

    var vm = MakeVM(bhl, ts_fn);
    double res = Execute(vm, "test", 2).Stack.Pop();
    Assert.Equal(1202, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeChildClassCallParentMethod()
  {
    string bhl = @"

    func float test(float k)
    {
      ColorAlpha c = new ColorAlpha
      c.r = 10
      c.g = 20
      return c.mult_summ(k)
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColorAlpha(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test", 2).Stack.Pop().num;
    Assert.Equal(60, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeChildClassCallOwnMethod()
  {
    string bhl = @"

    func float test()
    {
      ColorAlpha c = new ColorAlpha
      c.r = 10
      c.g = 20
      c.a = 3
      return c.mult_summ_alpha()
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColorAlpha(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(90, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNestedMembersAccess()
  {
    string bhl = @"

    func float test(float k)
    {
      ColorNested cn = new ColorNested
      cn.c.r = k*1
      cn.c.g = k*100
      return cn.c.r + cn.c.g
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      BindColor(ts);

      var cl = new ClassSymbolNative(new Origin(), "ColorNested", null,
        (VM.ExecState exec, ref Val v, IType type) => { v.SetObj(new ColorNested(), type); }
      );

      ts.ns.Define(cl);

      cl.Define(new FieldSymbol(new Origin(), "c", ts.T("Color"),
        delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld)
        {
          var cn = (ColorNested)ctx.obj;
          v.SetObj(cn.c, ts.T("Color").Get());
        },
        delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld)
        {
          var cn = (ColorNested)ctx.obj;
          cn.c = (Color)v.obj;
          ctx.SetObj(cn, ctx.type);
        }
      ));
      cl.Setup();
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test", 2).Stack.Pop().num;
    Assert.Equal(202, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCtorNotAllowed()
  {
    string bhl = @"

    func test()
    {
      Foo f = new Foo
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      var cl = new ClassSymbolNative(new Origin(), "Foo");

      ts.ns.Define(cl);

      cl.Setup();
    });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      "constructor is not defined",
      new PlaceAssert(bhl, @"
      Foo f = new Foo
--------------^"
      )
    );
  }

  [Fact]
  public void TestJsonCtorNotAllowed()
  {
    string bhl = @"

    func test()
    {
      Foo f = {}
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      var cl = new ClassSymbolNative(new Origin(), "Foo");

      ts.ns.Define(cl);

      cl.Setup();
    });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      "constructor is not defined",
      new PlaceAssert(bhl, @"
      Foo f = {}
--------------^"
      )
    );
  }

  [Fact]
  public void TestGetFieldOperationIsNotAllowed()
  {
    string bhl = @"

    func int test()
    {
      Foo f = {}
      return f.x
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      var cl = new ClassSymbolNative(new Origin(), "Foo", null,
        (VM.ExecState exec, ref Val v, IType type) =>
        {
          //dummy
        }
      );

      ts.ns.Define(cl);

      cl.Define(new FieldSymbol(new Origin(), "x", ts.T("int"),
        null, //no getter
        delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld)
        {
          //dummy
        }
      ));
      cl.Setup();
    });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      "get operation is not defined",
      new PlaceAssert(bhl, @"
      return f.x
---------------^"
      )
    );
  }

  [Fact]
  public void TestSetFieldOperationIsNotAllowed()
  {
    string bhl = @"

    func test()
    {
      Foo f = {}
      int tmp = f.x
      f.x = 10
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      var cl = new ClassSymbolNative(new Origin(), "Foo", null,
        (VM.ExecState exec, ref Val v, IType type) =>
        {
          //dummy
        }
      );

      ts.ns.Define(cl);

      cl.Define(new FieldSymbol(new Origin(), "x", ts.T("int"),
        delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld)
        {
          //dummy
        },
        null //no setter
      ));
      cl.Setup();
    });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      "set operation is not defined",
      new PlaceAssert(bhl, @"
      f.x = 10
--------^"
      )
    );
  }

  [Fact]
  public void TestReturnNativeInstance()
  {
    string bhl = @"

    func Color MakeColor(float g)
    {
      Color c = new Color
      c.g = g
      return c
    }

    func float test(float k)
    {
      return MakeColor(k).g + MakeColor(k).r
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    var vm = MakeVM(bhl, ts_fn);
    double res = Execute(vm, "test", 2).Stack.Pop();
    Assert.Equal(2, res);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestImportUserClass()
  {
    string bhl1 = @"
    class Foo {
      int Int
      float Flt
      string Str
    }
    ";

    string bhl2 = @"
    import ""bhl1""
    func test()
    {
      Foo f = new Foo
      f.Int = 10
      f.Flt = 14.2
      f.Str = ""Hey""
      trace((string)f.Int + "";"" + (string)f.Flt + "";"" + f.Str)
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      },
      ts_fn
    );

    vm.LoadModule("bhl2");
    Execute(vm, "test");
    AssertEqual("10;14.2;Hey", log.ToString().Replace(',', '.') /*locale issues*/);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestImportClassMoreComplex()
  {
    string bhl1 = @"
    import ""bhl2""
    func float test(float k)
    {
      Foo f = { x : k }
      return bar(f)
    }
    ";

    string bhl2 = @"
    import ""bhl3""

    class Foo
    {
      float x
    }

    func float bar(Foo f)
    {
      return hey(f.x)
    }
    ";

    string bhl3 = @"
    func float hey(float k)
    {
      return k
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      }
    );

    vm.LoadModule("bhl1");
    Assert.Equal(42, Execute(vm, "test", 42).Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestImportClassConflict()
  {
    string bhl1 = @"
    import ""bhl2""

    class Bar { }

    func test() { }
    ";

    string bhl2 = @"
    class Bar { }
    ";

    await AssertErrorAsync<Exception>(
      async delegate()
      {
        await MakeVM(new Dictionary<string, string>()
          {
            {"bhl1.bhl", bhl1},
            {"bhl2.bhl", bhl2},
          }
        );
      },
      @"already defined symbol 'Bar'",
      new PlaceAssert(bhl1, @"
    class Bar { }
----^"
      )
    );
  }

  [Fact]
  public void TestUserClassDefaultInit()
  {
    string bhl = @"

    class Foo {
      float b
      int c
      string s
    }

    func string test()
    {
      Foo f = {}
      return (string)f.b + "";"" + (string)f.c + "";"" + f.s + "";""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("0;0;;", Execute(vm, "test").Stack.Pop().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassDefaultInitBool()
  {
    string bhl = @"

    class Foo {
      bool c
    }

    func bool test()
    {
      Foo f = {}
      return f.c == false
    }
    ";

    var vm = MakeVM(bhl);
    Assert.True(Execute(vm, "test").Stack.Pop().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassWithArr()
  {
    string bhl = @"

    class Foo {
      []int a
    }

    func int test()
    {
      Foo f = {a : [10, 20]}
      return f.a[1]
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(20, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassDefaultInitArr()
  {
    string bhl = @"

    class Foo {
      []int a
    }

    func bool test()
    {
      Foo f = {}
      return f.a == null
    }
    ";

    var vm = MakeVM(bhl);
    Assert.True(Execute(vm, "test").Stack.Pop().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestArrayOfNonExistingClass()
  {
    string bhl = @"

    func []Foo fetch() {
      return null
    }

    func test() {
      []Foo fs = fetch()
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "type 'Foo' not found",
      new PlaceAssert(bhl, @"
    func []Foo fetch() {
---------^"
      )
    );
  }

  [Fact]
  public void TestUserClassArrayClear()
  {
    string bhl = @"

    class Foo {
      []int a
    }

    func int test()
    {
      Foo f = {a : [10, 20]}
      f.a.Clear()
      f.a.Add(30)
      return f.a[0]
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(30, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassWithFuncPtr()
  {
    string bhl = @"

    func int foo(int a)
    {
      return a + 1
    }

    class Foo {
      func int(int) ptr
    }

    func int test()
    {
      Foo f = {}
      f.ptr = foo
      return f.ptr(2)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(3, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact(Skip = "TODO: not implemented yet")]
  public void TestSelfReferenceLeak()
  {
    string bhl = @"
    class Bar {
      Bar b?
    }

    func test()
    {
      Bar b = {}
      b.b = b
    }
    ";

    var vm = MakeVM(bhl);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [Fact(Skip = "TODO: not implemented yet")]
  public void TestFuncPtrAccessThisMethodLeak()
  {
    string bhl = @"
    class Bar {
      func() ptr
      int d
      func Dummy() {
        this.d = 1
      }
    }

    func test()
    {
      Bar b = {}
      b.ptr = func() [b?] {
        b.Dummy()
      }
    }
    ";

    var vm = MakeVM(bhl);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassDefaultInitFuncPtr()
  {
    string bhl = @"

    class Foo {
      func void(int) ptr
    }

    func bool test()
    {
      Foo f = {}
      return f.ptr == null
    }
    ";

    var vm = MakeVM(bhl);
    Assert.True(Execute(vm, "test").Stack.Pop().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassWithFuncPtrsArray()
  {
    string bhl = @"

    func int foo(int a)
    {
      return a + 1
    }

    func int foo2(int a)
    {
      return a + 10
    }

    class Foo {
      []func int(int) ptrs
    }

    func int test()
    {
      Foo f = {ptrs: []}
      f.ptrs.Add(foo)
      f.ptrs.Add(foo2)
      return f.ptrs[1](2)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(12, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassWithFuncPtrsArrayCleanArgsStack()
  {
    string bhl = @"

    func int foo(int a,int b)
    {
      return a + 1
    }

    func int foo2(int a,int b)
    {
      return a + 10
    }

    class Foo {
      []func int(int,int) ptrs
    }

    func int bar()
    {
      fail()
      return 1
    }

    func void test()
    {
      Foo f = {ptrs: []}
      f.ptrs.Add(foo)
      f.ptrs.Add(foo2)
      f.ptrs[1](2, bar())
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindFail(ts); });

    var vm = MakeVM(bhl, ts_fn);
    Assert.Equal(0, Execute(vm, "test").Stack.sp);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGettingMethodPtrsNotAllowedYet()
  {
    string bhl = @"

    class Foo {
      func void foo() {
      }
    }

    func test()
    {
      Foo f = {}
      func() ptr = f.foo
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "method pointers not supported",
      new PlaceAssert(bhl, @"
      func() ptr = f.foo
--------------------^"
      )
    );
  }

  [Fact]
  public void TestStaticMethodPtrs()
  {
    string bhl = @"

    class Foo {
      func garbage() { }
      static func int foo() {
        return 10
      }
    }

    func int test()
    {
      func int() ptr = Foo.foo
      return ptr()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(10, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestAssigningMethodsNotAllowed()
  {
    string bhl = @"

    class Foo {
      func void foo() {
      }
    }

    func test()
    {
      Foo f = {}
      f.foo = null
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "invalid assignment",
      new PlaceAssert(bhl, @"
      f.foo = null
-------^"
      )
    );
  }

  [Fact]
  public void TestUserClassDefaultInitEnum()
  {
    string bhl = @"

    enum Bar {
      NONE = 0
      FOO  = 1
    }

    class Foo {
      Bar b
    }

    func bool test()
    {
      Foo f = {}
      return f.b == Bar.NONE
    }
    ";

    var vm = MakeVM(bhl);
    Assert.True(Execute(vm, "test").Stack.Pop().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassWithAnotherUserClassMember()
  {
    string bhl = @"

    class Bar {
      []int a
    }

    class Foo {
      Bar b
    }

    func int test()
    {
      Foo f = {b : { a : [10, 21] } }
      return f.b.a[1]
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(21, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassDefaultInitStringConcat()
  {
    string bhl = @"

    class Foo {
      string s1
      string s2
    }

    func string test()
    {
      Foo f = {}
      return f.s1 + f.s2
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal("", Execute(vm, "test").Stack.Pop().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassAny()
  {
    string bhl = @"

    class Foo { }

    func bool test()
    {
      Foo f = {}
      any foo = f
      return foo != null
    }
    ";

    var vm = MakeVM(bhl);
    Assert.True(Execute(vm, "test").Stack.Pop().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassAnyCastBack()
  {
    string bhl = @"

    class Foo { int x }

    func int test()
    {
      Foo f = {x : 10}
      any foo = f
      Foo f1 = (Foo)foo
      return f1.x
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(10, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassMethod()
  {
    string bhl = @"

    class Foo {

      int a

      func int getA()
      {
        return this.a
      }
    }

    func int test()
    {
      Foo f = {}
      f.a = 10
      return f.getA()
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitChain(Opcodes.Frame, new int[] { 1, 1 })
          .EmitChain(Opcodes.GetVar, new int[] { 0 })
          .EmitChain(Opcodes.GetAttr, new int[] { 0 })
          .EmitChain(Opcodes.Return)
          .EmitChain(Opcodes.Frame, new int[] { 1, 1 })
          .EmitChain(Opcodes.New, new int[] { TypeIdx(c, c.ns.T("Foo")) })
          .EmitChain(Opcodes.SetVar, new int[] { 0 })
          .EmitChain(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitChain(Opcodes.GetVar, new int[] { 0 })
          .EmitChain(Opcodes.SetAttr, new int[] { 0 })
          .EmitChain(Opcodes.GetVar, new int[] { 0 })
          .EmitChain(Opcodes.CallMethod, new int[] { 1, 0 })
          .EmitChain(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    Assert.Equal(10, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSeveralUserClassMethods()
  {
    string bhl = @"

    class Foo {

      int a
      int b

      func int getA()
      {
        return this.a
      }

      func int getB()
      {
        return this.b
      }
    }

    func int test()
    {
      Foo f = {}
      f.a = 10
      f.b = 20
      return f.getA() + f.getB()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(30, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassMethodCallMemberMethod()
  {
    string bhl = @"

    class Bar {
      int b
      func int getB() {
        return this.b
      }
    }

    class Foo {
      Bar bar

      func int getBarB()
      {
        this.bar = new Bar
        this.bar.b = 10
        return this.bar.getB()
      }
    }

    func int test()
    {
      Foo f = {}
      return f.getBarB()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(10, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestThisInLambda()
  {
    string bhl = @"

    class Foo {

      int a

      func int getA()
      {
        return func int() { return this.a }()
      }
    }

    func int test()
    {
      Foo f = {}
      f.a = 10
      return f.getA()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(10, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestBaseNotAllowedInRootClass()
  {
    string bhl = @"

    class Foo {
      int a
      func int getA()
      {
        return base.a
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "no base class",
      new PlaceAssert(bhl, @"
        return base.a
---------------^"
      )
    );
  }

  [Fact]
  public void TestMemberNotFoundInBaseClass()
  {
    string bhl = @"

    class Bar {
    }
    class Foo : Bar {
      int b
      func int getA()
      {
        return base.b
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "symbol 'b' not resolved",
      new PlaceAssert(bhl, @"
        return base.b
--------------------^"
      )
    );
  }


  [Fact]
  public void TestBaseKeywordIsReserved()
  {
    string bhl = @"
    class Hey {}

    class Foo : Hey {
      func int getA()
      {
        int base = 1
        return base
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "keyword 'base' is reserved",
      new PlaceAssert(bhl, @"
        int base = 1
------------^"
      )
    );
  }

  [Fact]
  public void TestPassArgToClassMethod()
  {
    string bhl = @"

    class Foo {

      int a

      func int getA(int i)
      {
        return this.a + i
      }
    }

    func int test()
    {
      Foo f = {}
      f.a = 10
      return f.getA(2)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(12, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassArgToClassMethodFromAnotherMethod()
  {
    string bhl = @"

    class Foo {
      func Bar() {
        this.Wow(0)
      }
      func Wow(float t) {
      }
    }

    func test()
    {
      Foo f = {}
      f.Bar()
    }
    ";

    var vm = MakeVM(bhl);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [Fact]
  public void TestDefaultArgValueInMethod()
  {
    string bhl = @"

    class Foo {

      int a

      func int getA(int i, int c = 10)
      {
        return this.a + i + c
      }
    }

    func int test()
    {
      Foo f = {}
      f.a = 10
      return f.getA(2)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(22, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestDefaultArgValueInMethod2()
  {
    string bhl = @"

    class Foo {

      int a

      func int getA(int i, int c = 10)
      {
        return this.a + i + c
      }
    }

    func int test()
    {
      Foo f = {}
      f.a = 10
      return f.getA(2, 100)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(112, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassMethodNamedLikeClass()
  {
    string bhl = @"

    class Foo {

      func int Foo()
      {
        return 2
      }
    }

    func int test()
    {
      Foo f = {}
      return f.Foo()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(2, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassMethodLocalVariableNotDeclared()
  {
    string bhl = @"
      class Foo {

        int a

        func int Summ()
        {
          a = 1
          return this.a + a
        }
      }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "symbol 'a' not resolved",
      new PlaceAssert(bhl, @"
          a = 1
----------^"
      )
    );
  }

  [Fact]
  public void TestUserClassMethodLocalVarAndAttribute()
  {
    string bhl = @"
      class Foo {

        int a

        func int Foo()
        {
          int a = 1
          return this.a + a
        }
      }

      func int test()
      {
        Foo f = {}
        f.a = 10
        return f.Foo()
      }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(11, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserChildAttributes()
  {
    string bhl = @"
    class Foo {
      int a
      int b
    }

    class Bar : Foo {
      int c
    }

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.c = 100
      return b.a + b.b + b.c
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(111, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserChildAttributeConflict()
  {
    string bhl = @"

    class Foo {
      int a
      int b
    }

    class Bar : Foo {
      int b
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "already defined symbol 'b'",
      new PlaceAssert(bhl, @"
      int b
------^"
      )
    );
  }

  [Fact]
  public void TestNonStaticFieldAccessAsStaticNotAllowed()
  {
    string bhl = @"
    class Foo {
      int a
    }

    func int test() {
      return Foo.a
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "accessing instance attribute as static is forbidden",
      new PlaceAssert(bhl, @"
      return Foo.a
----------------^"
      )
    );
  }

  [Fact]
  public void TestStaticFieldAccessAsNonStaticNotAllowed()
  {
    string bhl = @"
    class Foo {
      static int a
    }

    func int test() {
      Foo foo = {}
      return foo.a
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "accessing static field on instance is forbidden",
      new PlaceAssert(bhl, @"
      return foo.a
----------------^"
      )
    );
  }

  [Fact]
  public void TestMethodCallsStaticFieldWithoutPrefix()
  {
    string bhl = @"
    class Bar {
      static int b

      func int foo() {
        return b
      }

    }

    func int test()
    {
      Bar.b = 42
      var bar = new Bar
      return bar.foo()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(42, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStaticMethodCallsStaticFieldWithoutPrefix()
  {
    string bhl = @"
    class Bar {
      static int b

      static func int foo() {
        return b
      }

    }

    func int test()
    {
      Bar.b = 42
      return Bar.foo()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(42, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserChildClassMethod()
  {
    string bhl = @"

    class Foo {

      int a
      int b

      func int getA() {
        return this.a
      }

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int c

      func int getC() {
        return this.c
      }
    }

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.c = 100
      return b.getA() + b.getB() + b.getC()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(111, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserSubChildClassMethod()
  {
    string bhl = @"
    class Base {
      int a

      func int getA() {
        return this.a
      }
    }

    class Foo : Base {

      int b

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int c

      func int getC() {
        return this.c
      }
    }

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.c = 100
      return b.getA() + b.getB() + b.getC()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(111, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserChildClassMethodAccessesParent()
  {
    string bhl = @"
    class Foo {

      int a
      int b

      func int getA() {
        return this.a
      }

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int c

      func int getC() {
        return this.c
      }

      func int getSumm() {
        return this.getC() + this.getB() + this.getA()
      }
    }

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.c = 100
      return b.getSumm()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(111, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserChildClassMethodAccessesParentViaBase()
  {
    string bhl = @"
    class Foo {

      int a
      int b

      func int getA() {
        return this.a
      }

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int c

      func int getC() {
        return this.c
      }

      func int getSumm() {
        return this.getC() + base.getB() + base.a
      }
    }

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.c = 100
      return b.getSumm()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(111, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassAndLocalVariableOwnership()
  {
    string bhl = @"
    class Foo {
      int a
    }

    func int test1()
    {
      Foo f = {}
      for(int i=0;i<2;i++) {
        f.a = i
      }
      int local_var_garbage = 100
      return f.a
    }

    func int test2()
    {
      Foo f = null
      for(int i=0;i<2;i++) {
        f = {a : i}
      }
      int local_var_garbage = 100
      return f.a
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1, Execute(vm, "test1").Stack.Pop().num);
    Assert.Equal(1, Execute(vm, "test2").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserChildClassMethodsOrderIrrelevant()
  {
    string bhl = @"

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.c = 100
      return b.getSumm()
    }

    class Bar : Foo {

      func int getSumm() {
        return this.getC() + this.getB() + this.getA()
      }

      func int getC() {
        return this.c
      }

      int c
    }

    class Foo : Hey {

      func int getA() {
        return this.a
      }

      func int getB() {
        return this.b
      }

      int a
    }

    class Hey {
      int b
    }

    ";

    var vm = MakeVM(bhl);
    Assert.Equal(111, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserSubChildClassMethodAccessesParent()
  {
    string bhl = @"
    class Base {
      int a

      func int getA() {
        return this.a
      }
    }

    class Foo : Base {

      int b

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int c

      func int getC() {
        return this.c
      }

      func int getSumm() {
        return this.getC() + this.getB() + this.getA()
      }
    }

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.c = 100
      return b.getSumm()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(111, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserChildClassMethodAlreadyDefined()
  {
    string bhl = @"
    class Foo {
      int a
      func int getA() {
        return this.a
      }
    }

    class Bar : Foo {
      func int getA() {
        return this.a
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      @"already defined symbol 'getA'",
      new PlaceAssert(bhl, @"
      func int getA() {
------^"
      )
    );
  }

  [Fact]
  public void TestArrayOfUserClasses()
  {
    string bhl = @"
    class Foo {
      float b
      int c
    }

    func float test()
    {
      []Foo fs = [{b:1, c:2}]
      fs.Add({b:10, c:20})

      return fs[0].b + fs[0].c + fs[1].b + fs[1].c
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(33, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassCast()
  {
    string bhl = @"

    class Foo {
      float b
    }

    func float test()
    {
      Foo f = { b : 101 }
      any a = f
      Foo f1 = (Foo)a
      return f1.b
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(101, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassInlineCast()
  {
    string bhl = @"

    class Foo {
      float b
    }

    func float test()
    {
      Foo f = { b : 101 }
      any a = f
      return ((Foo)a).b
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(101, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassInlineCastArr()
  {
    string bhl = @"

    class Foo {
      []int b
    }

    func int test()
    {
      Foo f = { b : [101, 102] }
      any a = f
      return ((Foo)a).b[1]
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(102, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUserClassInlineCastArr2()
  {
    string bhl = @"

    class Foo {
      []int b
    }

    func int test()
    {
      Foo f = { b : [101, 102] }
      any a = f
      return ((Foo)a).b.Count
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(2, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestChildUserClass()
  {
    string bhl = @"

    class Base {
      float x
    }

    class Foo : Base {
      float y
    }

    func float test() {
      Foo f = { x : 1, y : 2}
      return f.y + f.x
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(3, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestChildUserClassOrderIrrelevant()
  {
    string bhl = @"

    func float test() {
      Foo f = { x : 1, y : 2}
      return f.y + f.x
    }

    class Foo : Base {
      float y
    }

    class Base {
      float x
    }

    ";

    var vm = MakeVM(bhl);
    Assert.Equal(3, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestChildUserClassDowncast()
  {
    string bhl = @"

    class Base {
      float x
    }

    class Foo : Base
    {
      float y
    }

    func float test()
    {
      Foo f = { x : 1, y : 2}
      Base b = (Foo)f
      return b.x
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestChildUserClassUpcast()
  {
    string bhl = @"

    class Base {
      float x
    }

    class Foo : Base
    {
      float y
    }

    func float test()
    {
      Foo f = { x : 1, y : 2}
      Base b = (Foo)f
      Foo f2 = (Foo)b
      return f2.x + f2.y
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(3, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestChildUserClassDefaultInit()
  {
    string bhl = @"

    class Base {
      float x
    }

    class Foo : Base
    {
      float y
    }

    func float test()
    {
      Foo f = { y : 2}
      return f.x + f.y
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(2, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestTmpUserClassReadDefaultField()
  {
    string bhl = @"

    class Foo {
      int c
    }

    func int test()
    {
      return (new Foo).c
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(0, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestTmpUserClassReadField()
  {
    string bhl = @"

    class Foo {
      int c
    }

    func int test()
    {
      return (new Foo{c: 10}).c
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(10, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestTmpUserClassWriteFieldAllowed()
  {
    string bhl = @"

    class Foo {
      int c
    }

    func test()
    {
      (new Foo{c: 10}).c = 20
    }
    ";

    var vm = MakeVM(bhl);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [Fact]
  public void TestTmpNativeClassCallMethod()
  {
    string bhl = @"
    func Color get() {
      return mkcolor(1)
    }

    func test()
    {
      get().mult_summ(10)
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [Fact]
  public void TestChildUserClassAlreadyDefinedMember()
  {
    string bhl = @"

    class Base {
      float x
    }

    class Foo : Base
    {
      int x
    }

    func float test()
    {
      Foo f = { x : 2}
      return f.x
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "already defined symbol 'x'",
      new PlaceAssert(bhl, @"
      int x
------^"
      )
    );
  }

  [Fact]
  public void TestChildUserClassOfNativeClassIsForbidden()
  {
    string bhl = @"

    class ColorA : Color
    {
      float a
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      "extending native classes is not supported",
      new PlaceAssert(bhl, @"
    class ColorA : Color
-------------------^"
      )
    );
  }

  [Fact]
  public void TestUserClassNotAllowedToContainThisMember()
  {
    string bhl = @"
      class Foo {
        int b
        int this
        int a
      }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "the keyword 'this' is reserved",
      new PlaceAssert(bhl, @"
        int this
------------^"
      )
    );
  }

  [Fact]
  public void TestUserClassContainThisMethodNotAllowed()
  {
    string bhl = @"
      class Foo {

        func bool this()
        {
          return false
        }

        func bar() {}
      }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "the keyword 'this' is reserved",
      new PlaceAssert(bhl, @"
        func bool this()
------------------^"
      )
    );
  }

  [Fact]
  public void TestBindNativeClass()
  {
    string bhl = @"

    func bool test()
    {
      Bar b = new Bar
      return b != null
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindBar(ts); });

    var c = Compile(bhl, ts_fn);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitChain(Opcodes.Frame, new int[] { 1, 1 })
          .EmitChain(Opcodes.New, new int[] { TypeIdx(c, c.ns.T("Bar")) })
          .EmitChain(Opcodes.SetVar, new int[] { 0 })
          .EmitChain(Opcodes.GetVar, new int[] { 0 })
          .EmitChain(Opcodes.Constant, new int[] { ConstNullIdx(c) })
          .EmitChain(Opcodes.EqualEx)
          .EmitChain(Opcodes.UnaryNot)
          .EmitChain(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts_fn);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.True(fb.Stack.Pop().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeClassWithSimpleMembers()
  {
    string bhl = @"

    func test()
    {
      Bar o = new Bar
      o.Int = 10
      o.Flt = 14.5
      o.Str = ""Hey""
      trace((string)o.Int + "";"" + (string)o.Flt + "";"" + o.Str)
    }
    ";

    var log = new StringBuilder();

    FuncSymbolNative fn = null;
    var ts_fn = new Action<Types>((ts) =>
    {
      fn = BindTrace(ts, log);
      BindBar(ts);
    });
    var c = Compile(bhl, ts_fn);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitChain(Opcodes.Frame, new int[] { 1, 0})
          .EmitChain(Opcodes.New, new int[] { TypeIdx(c, c.ns.T("Bar")) })
          .EmitChain(Opcodes.SetVar, new int[] { 0 })
          .EmitChain(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitChain(Opcodes.GetVar, new int[] { 0 })
          .EmitChain(Opcodes.SetAttr, new int[] { 0 })
          .EmitChain(Opcodes.Constant, new int[] { ConstIdx(c, 14.5) })
          .EmitChain(Opcodes.GetVar, new int[] { 0 })
          .EmitChain(Opcodes.SetAttr, new int[] { 1 })
          .EmitChain(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
          .EmitChain(Opcodes.GetVar, new int[] { 0 })
          .EmitChain(Opcodes.SetAttr, new int[] { 2 })
          .EmitChain(Opcodes.GetVar, new int[] { 0 })
          .EmitChain(Opcodes.GetAttr, new int[] { 0 })
          .EmitChain(Opcodes.TypeCast, new int[] { TypeIdx(c, c.ns.T("string")), 1 })
          .EmitChain(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
          .EmitChain(Opcodes.Add)
          .EmitChain(Opcodes.GetVar, new int[] { 0 })
          .EmitChain(Opcodes.GetAttr, new int[] { 1 })
          .EmitChain(Opcodes.TypeCast, new int[] { TypeIdx(c, c.ns.T("string")), 1 })
          .EmitChain(Opcodes.Add)
          .EmitChain(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
          .EmitChain(Opcodes.Add)
          .EmitChain(Opcodes.GetVar, new int[] { 0 })
          .EmitChain(Opcodes.GetAttr, new int[] { 2 })
          .EmitChain(Opcodes.Add)
          .EmitChain(Opcodes.CallGlobNative, new int[] { c.ts.module.nfunc_index.IndexOf(fn), 1 })
          .EmitChain(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts_fn);
    vm.Start("test");
    Assert.False(vm.Tick());
    AssertEqual("10;14.5;Hey", log.ToString().Replace(',', '.') /*locale issues*/);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeClassAttributesAccess()
  {
    string bhl = @"

    func float test(float k)
    {
      Color c = new Color
      c.r = k*1
      c.g = k*100
      return c.r + c.g
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    var vm = MakeVM(bhl, ts_fn);
    double res = Execute(vm, "test", 2).Stack.Pop();
    Assert.Equal(202, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeClassCallMember()
  {
    string bhl = @"

    func float test(float a)
    {
      return mkcolor(a).r
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    var vm = MakeVM(bhl, ts_fn);
    double res = Execute(vm, "test", 10).Stack.Pop();
    Assert.Equal(10, res);
    res = Execute(vm, "test", 20).Stack.Pop();
    Assert.Equal(20, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeAttributeIsNotAFunction()
  {
    string bhl = @"

    func float r()
    {
      return 0
    }

    func test()
    {
      Color c = new Color
      c.r()
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      "symbol is not a function",
      new PlaceAssert(bhl, @"
      c.r()
--------^"
      )
    );
  }

  [Fact]
  public void TestNativeClassTypeRefNotFound()
  {
    string bhl = @"
    func test(Foo foo)
    {
      var native = foo.sub
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      {
        var cl = new ClassSymbolNative(new Origin(), "Foo");
        ts.ns.Define(cl);

        cl.Define(new FieldSymbol(new Origin(), "sub", ts.T("Bar"),
          delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld) { }, null
        ));
        cl.Setup();
      }
    });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      "type 'Bar' not found",
      new PlaceAssert(bhl, @"
      var native = foo.sub
-----------------------^"
      )
    );
  }

  [Fact]
  public void TestNullWithClassInstance()
  {
    string bhl = @"

    func test()
    {
      Color c = null
      Color c2 = new Color
      if(c == null) {
        trace(""NULL;"")
      }
      if(c != null) {
        trace(""NEVER;"")
      }
      if(c2 == null) {
        trace(""NEVER;"")
      }
      if(c2 != null) {
        trace(""NOTNULL;"")
      }
      c = c2
      if(c2 == c) {
        trace(""EQ;"")
      }
      if(c2 == null) {
        trace(""NEVER;"")
      }
      if(c != null) {
        trace(""NOTNULL;"")
      }
    }
    ";

    var log = new StringBuilder();

    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("NULL;NOTNULL;EQ;NOTNULL;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestCallImportedMethodFromLocalMethod()
  {
    string bhl1 = @"
    class A {
      func int a() {
        return 10
      }
    }
    ";

    string bhl2 = @"
    import ""bhl1""
    class B {
      func int b() {
        A a = {}
        return a.a()
      }
    }

    func int test() {
      B b = {}
      return b.b()
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2}
      }
    );

    vm.LoadModule("bhl2");
    Assert.Equal(10, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestCallImportedMemberMethodFromMethod()
  {
    string bhl1 = @"
    import ""bhl2""
    namespace a {
      class A : b.B {
      }
    }
    ";

    string bhl2 = @"
    import ""bhl1""
    namespace b {
      class B {
        func int Foo() {
          return 42
        }
      }
    }
  ";

    string bhl3 = @"
    import ""bhl1""
    import ""bhl2""

    class C {
      a.A a
      func int getAFoo() {
        this.a = {}
        return this.a.Foo()
      }
    }

    func int test() {
      C c = {}
      return c.getAFoo()
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3}
      }
    );

    vm.LoadModule("bhl3");
    Assert.Equal(42, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestCallImportedVirtualMemberMethodFromMethod()
  {
    string bhl1 = @"
    import ""bhl2""
    namespace a {
      class A : b.B {
      }
    }
    ";

    string bhl2 = @"
    import ""bhl1""
    namespace b {
      class B {
        virtual func int Foo() {
          return 42
        }
      }
    }
  ";

    string bhl3 = @"
    import ""bhl1""
    import ""bhl2""

    class C {
      a.A a
      func int getAFoo() {
        this.a = {}
        return this.a.Foo()
      }
    }

    func int test() {
      C c = {}
      return c.getAFoo()
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3}
      }
    );

    vm.LoadModule("bhl3");
    Assert.Equal(42, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestBasicVirtualMethodsSupport()
  {
    string bhl = @"

    class Foo {

      int a
      int b

      virtual func int getA() {
        return this.a
      }

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int new_a

      override func int getA() {
        return this.new_a
      }
    }

    func int test1()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.new_a = 100
      return b.getA() + b.getB()
    }

    func int test2()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.new_a = 100
      //NOTE: Bar.getA() will be called anyway!
      return ((Foo)b).getA() + b.getB()
    }

    func int foo_caller(Foo f)
    {
      return f.getA();
    }

    func int bar_caller(Bar b)
    {
      return b.getA();
    }

    func int test3()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.new_a = 100
      return foo_caller(b) + bar_caller(b)
    }

    ";

    var vm = MakeVM(bhl);
    Assert.Equal(110, Execute(vm, "test1").Stack.Pop().num);
    Assert.Equal(110, Execute(vm, "test2").Stack.Pop().num);
    Assert.Equal(200, Execute(vm, "test3").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestVirtualMethodsChecks()
  {
    {
      string bhl = @"
      class Foo {
        override func int getA() {
          return 42
        }
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "no base virtual method to override",
        new PlaceAssert(bhl, @"
        override func int getA() {
--------^"
        )
      );
    }

    {
      string bhl = @"
      class Hey {
        func int getA() {
          return 42
        }
      }
      class Foo : Hey {
        override func int getA() {
          return 42
        }
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "no base virtual method to override",
        new PlaceAssert(bhl, @"
        override func int getA() {
--------^"
        )
      );
    }

    {
      string bhl = @"
      class Hey {
        func int getA() {
          return 42
        }
      }
      class Foo : Hey {
        virtual func int getA() {
          return 42
        }
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "already defined symbol 'getA'",
        new PlaceAssert(bhl, @"
        virtual func int getA() {
--------^"
        )
      );
    }

    {
      string bhl = @"
      class Hey {
        virtual func int getA() {
          return 4
        }
      }
      class Foo : Hey {
        override func float getA() {
          return 42
        }
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "virtual method signatures don't match, base",
        new PlaceAssert(bhl, @"
        override func float getA() {
--------^"
        )
      );
    }

    {
      string bhl = @"
      class Foo {
        virtual func void getA(int b, int a = 1) {
        }
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "virtual methods are not allowed to have default arguments",
        new PlaceAssert(bhl, @"
        virtual func void getA(int b, int a = 1) {
--------^"
        )
      );
    }

    {
      string bhl = @"
      class Foo {
        virtual func void getA(int b, int a) {
        }
      }

      class Bar : Foo {
        override func void getA(int b, int a = 1) {
        }
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "virtual methods are not allowed to have default arguments",
        new PlaceAssert(bhl, @"
        override func void getA(int b, int a = 1) {
--------^"
        )
      );
    }
  }

  [Fact]
  public void TestVirtualOverrideIn3dChild()
  {
    string bhl = @"

    class Foo {

      int a
      int b

      func int getB() {
        return this.b
      }

      virtual func int getA() {
        return this.a
      }
    }

    class Bar : Foo {
      int bar_a

      override func int getA() {
        return this.bar_a
      }
    }

    class Hey : Bar {
      int hey_a

      override func int getA() {
        return this.hey_a
      }
    }

    func int test1()
    {
      Hey h = {}
      h.a = 1
      h.b = 10
      h.bar_a = 100
      h.hey_a = 200
      return h.getA() + h.getB()
    }

    func int test2()
    {
      Hey h = {}
      h.a = 1
      h.b = 10
      h.bar_a = 100
      h.hey_a = 200
      //NOTE: Hey.getA() will called anyway
      return ((Foo)h).getA() + h.getB()
    }

    func int test3()
    {
      Hey h = {}
      h.a = 1
      h.b = 10
      h.bar_a = 100
      h.hey_a = 200
      //NOTE: Hey.getA() will called anyway
      return ((Bar)h).getA() + h.getB()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(210, Execute(vm, "test1").Stack.Pop().num);
    Assert.Equal(210, Execute(vm, "test2").Stack.Pop().num);
    Assert.Equal(210, Execute(vm, "test3").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestVirtualMethodsSupportBaseCall()
  {
    string bhl = @"

    class Foo {

      int a
      int b

      virtual func int getA() {
        return this.a
      }

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int new_a

      override func int getA() {
        return base.getA() + this.new_a
      }
    }

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.new_a = 100
      return b.getA() + b.getB()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(111, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestImportedClassVirtualMethodsSupport()
  {
    string bhl1 = @"
    class Foo {

      int b
      int a

      func int DummyGarbage0() {
        return 42
      }

      virtual func int getA() {
        return this.a
      }

      func int getB() {
        return this.b
      }
    }
  ";

    string bhl2 = @"
    import ""bhl1""

    class Bar : Foo {
      int new_a

      override func int getA() {
        return this.new_a
      }

      func DummyGarbage1() {}
    }

    func int test1()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.new_a = 100
      return b.getA() + b.getB()
    }

    func int test2()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.new_a = 100
      //NOTE: Bar.getA() will called anyway
      return ((Foo)b).getA() + b.getB()
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2}
      }
    );

    vm.LoadModule("bhl2");
    Assert.Equal(110, Execute(vm, "test1").Stack.Pop().num);
    Assert.Equal(110, Execute(vm, "test2").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestImportedClassVirtualMethodsSupportFromCachedModule()
  {
    string bhl_1 = @"
    namespace Unit.Traits {
      class Trait {

        int b
        int a

        func int DummyGarbage0() {
          return 42
        }

        coro virtual func int getA() {
          yield()
          return this.a
        }

        func int getB() {
          return this.b
        }
      }
    }
  ";

    string garbage = @"
    class Interim { }
  ";

    string bhl_2 = @"
    import ""bhl_1""

    namespace Unit.Pilot {
      class Trait : Unit.Traits.Trait {
        int new_a

        func DummyGarbage2() {}

        coro override func int getA() {
          yield()
          return this.new_a
        }

        func DummyGarbage20() {}
      }
    }

    coro func int test1()
    {
      Unit.Pilot.Trait b = {}
      b.a = 1
      b.b = 10
      b.new_a = 100
      return yield b.getA() + b.getB()
    }

    coro func int test2()
    {
      Unit.Pilot.Trait b = {}
      b.a = 1
      b.b = 10
      b.new_a = 100
      //NOTE: Bar.getA() will called anyway
      return yield ((Unit.Traits.Trait)b).getA() + b.getB()
    }
    ";

    var files = MakeFiles(new Dictionary<string, string>()
    {
      { "bhl_2.bhl", bhl_2 },
      { "garbage.bhl", garbage },
      { "bhl_1.bhl", bhl_1 },
    });

    {
      var vm = await MakeVM(files);
      vm.LoadModule("bhl_2");
      Assert.Equal(110, Execute(vm, "test1").Stack.Pop().num);
      Assert.Equal(110, Execute(vm, "test2").Stack.Pop().num);
      CommonChecks(vm);
    }

    {
      //NOTE: 'touching' garbage file so that any compilation actually occurs.
      //       This garbage file is not included by any of modules in question.
      //       Modules are loaded from the cache and we check the fact the cached modules
      //       are loaded properly disregarding their order of load
      System.IO.File.SetLastWriteTimeUtc(files[1], DateTime.UtcNow.AddSeconds(1));
      var conf = MakeCompileConf(files, use_cache: true, max_threads: 1);
      var vm = await MakeVM(conf);
      vm.LoadModule("bhl_2");
      Assert.Equal(110, Execute(vm, "test1").Stack.Pop().num);
      Assert.Equal(110, Execute(vm, "test2").Stack.Pop().num);
      CommonChecks(vm);
    }
  }

  [Fact]
  public async Task TestImportedClassVirtualMethodsSupportFromCachedModule2()
  {
    string bhl_1 = @"
    import ""interim""

    namespace Unit.Traits {
      class Trait {

        int b
        int a

        func int DummyGarbage0() {
          return 42
        }

        coro virtual func int getA() {
          yield()
          return this.a
        }

        func int getB() {
          return this.b
        }
      }
    }
  ";

    string interim = @"
    class Interim { }
  ";

    string bhl_2 = @"
    import ""bhl_1""

    namespace Unit.Pilot {
      class Trait : Unit.Traits.Trait {
        int new_a

        func DummyGarbage2() {}

        coro override func int getA() {
          yield()
          return this.new_a
        }

        func DummyGarbage20() {}
      }
    }

    coro func int test1()
    {
      Unit.Pilot.Trait b = {}
      b.a = 1
      b.b = 10
      b.new_a = 100
      return yield b.getA() + b.getB()
    }

    coro func int test2()
    {
      Unit.Pilot.Trait b = {}
      b.a = 1
      b.b = 10
      b.new_a = 100
      //NOTE: Bar.getA() will called anyway
      return yield ((Unit.Traits.Trait)b).getA() + b.getB()
    }
    ";

    var files = MakeFiles(new Dictionary<string, string>()
    {
      { "bhl_2.bhl", bhl_2 },
      { "interim.bhl", interim },
      { "bhl_1.bhl", bhl_1 },
    });

    {
      var vm = await MakeVM(files);
      vm.LoadModule("bhl_2");
      Assert.Equal(110, Execute(vm, "test1").Stack.Pop().num);
      Assert.Equal(110, Execute(vm, "test2").Stack.Pop().num);
      CommonChecks(vm);
    }

    {
      //NOTE: 'touching' interim file
      System.IO.File.SetLastWriteTimeUtc(files[1], DateTime.UtcNow.AddSeconds(1));
      var conf = MakeCompileConf(files, use_cache: true, max_threads: 1);
      var exec = new CompilationExecutor();
      var vm = await MakeVM(conf, exec: exec);
      vm.LoadModule("bhl_2");
      Assert.Equal(110, Execute(vm, "test1").Stack.Pop().num);
      Assert.Equal(110, Execute(vm, "test2").Stack.Pop().num);
      Assert.Equal(1, exec.cache_hits);
      Assert.Equal(2, exec.cache_miss);
      CommonChecks(vm);
    }
  }

  [Fact]
  public async Task TestImportedClassStaticMethodsSupportFromCachedModule()
  {
    string bhl_1 = @"
    namespace Unit.Traits {
      class Trait {

        func int DummyGarbage0() {
          return 42
        }

        static func int GetB() {
          return 10
        }
      }
    }
  ";

    string bhl_2 = @"
    import ""bhl_1""

    func int test()
    {
      return Unit.Traits.Trait.GetB()
    }
    ";

    var files = MakeFiles(new Dictionary<string, string>()
    {
      { "bhl_2.bhl", bhl_2 },
      { "bhl_1.bhl", bhl_1 },
    });

    {
      var vm = await MakeVM(files);
      vm.LoadModule("bhl_2");
      Assert.Equal(10, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }

    {
      System.IO.File.SetLastWriteTimeUtc(files[0], DateTime.UtcNow.AddSeconds(1));
      var conf = MakeCompileConf(files, use_cache: true, max_threads: 1);
      var exec = new CompilationExecutor();
      var vm = await MakeVM(conf, exec: exec);
      vm.LoadModule("bhl_2");
      Assert.Equal(10, Execute(vm, "test").Stack.Pop().num);
      Assert.Equal(1, exec.cache_hits);
      Assert.Equal(1, exec.cache_miss);
      CommonChecks(vm);
    }
  }

  [Fact]
  public async Task TestImportedClassVirtualMethodsSupportFromRootNamespace()
  {
    string bhl1 = @"
      class BaseBar {

        int b
        int a

        func int DummyGarbage0() {
          return 42
        }

        virtual func int getA() {
          return this.a
        }

        func int getB() {
          return this.b
        }
      }
  ";

    string bhl2 = @"
    import ""bhl1""

    namespace foo {
      class Bar : BaseBar {
        int new_a

        override func int getA() {
          return this.new_a
        }

        func DummyGarbage1() {}
      }
    }

    func int test()
    {
      foo.Bar b = {}
      b.a = 1
      b.new_a = 100
      return b.getA()
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2}
      }
    );

    vm.LoadModule("bhl2");
    Assert.Equal(100, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestImportedClassVirtualMethodsSupportInADifferentNamespace()
  {
    string bhl1 = @"
    namespace fns {
      class Item {
        int a
      }
      class Foo {

        int b
        int a

        func int DummyGarbage0() {
          return 42
        }

        virtual func int getA(Item item) {
          return this.a + item.a
        }

        func int getB() {
          return this.b
        }
      }
    }
  ";

    string bhl2 = @"
    import ""bhl1""

    class Bar : fns.Foo {
      int new_a

      override func int getA(fns.Item item) {
        return this.new_a + item.a
      }

      func DummyGarbage1() {}
    }

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.new_a = 100
      fns.Item item = {a: 1000}
      return b.getA(item)
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2}
      }
    );

    vm.LoadModule("bhl2");
    Assert.Equal(100 + 1000, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestVirtualCoroutineSupport()
  {
    string bhl = @"
    class Foo {

      int a
      int b

      coro virtual func int getA() {
        var tmp = this.a
        yield()
        return tmp
      }

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int new_a

      coro override func int getA() {
        var tmp = this.new_a
        yield()
        yield()
        return tmp
      }
    }

    coro func int test1()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.new_a = 100
      return yield b.getA() + b.getB()
    }

    coro func int test2()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.new_a = 100
      //NOTE: Bar.getA() will be called anyway!
      return yield ((Foo)b).getA() + b.getB()
    }

    coro func int foo_caller(Foo f)
    {
      return yield f.getA();
    }

    coro func int bar_caller(Bar b)
    {
      return yield b.getA();
    }

    coro func int test3()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.new_a = 100
      return yield foo_caller(b) + yield bar_caller(b)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(110, Execute(vm, "test1").Stack.Pop().num);
    Assert.Equal(110, Execute(vm, "test2").Stack.Pop().num);
    Assert.Equal(200, Execute(vm, "test3").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestInvalidFuncAttrubutes()
  {
    string bhl = @"
    override func int getA() {
      return 42
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "improper usage of attribute",
      new PlaceAssert(bhl, @"
    override func int getA() {
----^"
      )
    );
  }

  [Fact]
  public async Task TestImportedClassVirtualMethodsOrderIsIrrelevant()
  {
    string bhl1 = @"
    import ""bhl2""
    namespace a {
      class A : b.B {
        override func int Foo(int v) {
          return v + 1
        }
      }
    }
  ";

    string bhl2 = @"
    import ""bhl1""
    namespace b {
      class B {
        virtual func int Foo(int v) {
          return v
        }
      }
    }

    func int test1()
    {
      a.A a = {}
      return a.Foo(1)
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2}
      }
    );

    vm.LoadModule("bhl2");
    Assert.Equal(2, Execute(vm, "test1").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestMixInterfaceWithVirtualMethod()
  {
    string bhl = @"
    func IBar MakeIBar() {
      return new Bar
    }

    interface IBar {
      func int test()
    }

    class BaseBar : IBar {
      virtual func int test() { return 1 }
    }

    class Bar : BaseBar {
      override func int test() { return 2 }
    }

    func int test() {
      IBar bar = MakeIBar()
      return bar.test()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(2, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestMixInterfaceWithVirtualMethodAndCasting()
  {
    string bhl = @"
    func IBar MakeIBar() {
      return new Bar
    }

    interface IBar {
      func int test(int v)
    }

    class BaseBar : IBar {
      virtual func int test(int v) { return v+1 }
    }

    class Bar : BaseBar {
      override func int test(int v) { return v+2 }
    }

    func int test1() {
      IBar bar = MakeIBar()
      BaseBar bbar = bar as BaseBar
      //NOTE: Bar.test() implementation is called
      return bbar.test(10)
    }

    func int test2() {
      IBar bar = MakeIBar()
      BaseBar bbar = (BaseBar)bar
      //NOTE: Bar.test() implementation is called
      return bbar.test(10)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(12, Execute(vm, "test1").Stack.Pop().num);
    Assert.Equal(12, Execute(vm, "test2").Stack.Pop().num);
    CommonChecks(vm);
  }

  public class VirtFoo
  {
    public int a;
    public int b;

    public virtual int getA()
    {
      return a;
    }

    public int getB()
    {
      return b;
    }
  }

  public class VirtBar : VirtFoo
  {
    public int new_a;

    public override int getA()
    {
      return new_a;
    }
  }

  void BindVirtualFooBar(Types ts)
  {
    {
      var cl = new ClassSymbolNative(new Origin(), "Foo", null,
        (VM.ExecState exec, ref Val v, IType type) => { v.SetObj(new VirtFoo(), type); },
        typeof(VirtFoo)
      );
      ts.ns.Define(cl);

      cl.Define(new FieldSymbol(new Origin(), "a", Types.Int,
        delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld)
        {
          var f = (VirtFoo)ctx.obj;
          v.SetNum(f.a);
        },
        delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld)
        {
          var f = (VirtFoo)ctx.obj;
          f.a = (int)v.num;
          ctx.SetObj(f, ctx.type);
        }
      ));

      cl.Define(new FieldSymbol(new Origin(), "b", Types.Int,
        delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld)
        {
          var f = (VirtFoo)ctx.obj;
          v.SetNum(f.b);
        },
        delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld)
        {
          var f = (VirtFoo)ctx.obj;
          f.b = (int)v.num;
          ctx.SetObj(f, ctx.type);
        }
      ));

      {
        var m = new FuncSymbolNative(new Origin(), "getA", ts.T("int"),
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            var f = (VirtFoo)exec.stack.Pop().obj;
            exec.stack.Push(f.getA());
            return null;
          }
        );
        cl.Define(m);
      }

      {
        var m = new FuncSymbolNative(new Origin(), "getB", ts.T("int"),
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            var f = (VirtFoo)exec.stack.Pop().obj;
            exec.stack.Push(f.getB());
            return null;
          }
        );
        cl.Define(m);
      }
      cl.Setup();
    }

    {
      var cl = new ClassSymbolNative(new Origin(), "Bar", ts.T("Foo"),
        (VM.ExecState exec, ref Val v, IType type) => { v.SetObj(new VirtBar(), type); },
        typeof(VirtBar)
      );

      cl.Define(new FieldSymbol(new Origin(), "new_a", Types.Int,
        delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld)
        {
          var b = (VirtBar)ctx.obj;
          v.SetNum(b.new_a);
        },
        delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld)
        {
          var b = (VirtBar)ctx.obj;
          b.new_a = (int)v.num;
          ctx.SetObj(b, ctx.type);
        }
      ));
      cl.Setup();
      ts.ns.Define(cl);
    }
  }

  [Fact]
  public void TestNativeVirtualMethodsSupport()
  {
    string bhl = @"
    func int test1()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.new_a = 100
      return b.getA() + b.getB()
    }

    func int test2()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.new_a = 100
      //NOTE: there's not static cast in C#
      return ((Foo)b).getA() + b.getB()
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindVirtualFooBar(ts); });

    var vm = MakeVM(bhl, ts_fn);
    Assert.Equal(110, Execute(vm, "test1").Stack.Pop().num);
    Assert.Equal(110, Execute(vm, "test2").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSimpleNestedClass()
  {
    string bhl = @"

    class Bar {
       int b
       class Foo {
         int f
       }
       int c
    }

    func int test()
    {
      Bar.Foo foo = {f: 1}
      Bar bar = {b: 10, c: 20}
      return foo.f + bar.b + bar.c
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(31, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNestedClassIsAvailableWithoutPrefixInMasterMethod()
  {
    string bhl = @"

    class Bar {
       int b
       class Foo {
         int f
       }
       func int calc() {
         Foo foo = {f: 1}
         return this.b + foo.f
       }
    }

    func int test()
    {
      Bar bar = {b: 10}
      return bar.calc()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(11, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSimpleNestedClassMethods()
  {
    string bhl = @"

    class Bar {
       int b
       class Foo {
         int f
         func int getF() {
           return this.f
         }
       }
       int c
       func int getC() {
        return this.c
       }
    }

    func int test()
    {
      Bar.Foo foo = {f: 1}
      Bar bar = {b: 10, c: 20}
      return foo.getF() + bar.b + bar.getC()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(31, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSubNestedClassMethods()
  {
    string bhl = @"

    class Bar {
       int b
       class Foo {
         int f
         func int getF() {
           return this.f
         }
         class Wow {
           int w
           func int getW() {
             return this.w
           }
         }
       }
       int c
       func int getC() {
        return this.c
       }
    }

    func int test()
    {
      Bar.Foo foo = {f: 1}
      Bar bar = {b: 10, c: 20}
      Bar.Foo.Wow wow = {w: 50}
      return foo.getF() + bar.b + bar.getC() + wow.getW()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(81, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSimpleNestedEnum()
  {
    string bhl = @"
    class Bar {
       enum E {
         E1 = 1
         E2 = 2
       }
       int b
       func E getE2() {
         return E.E2
       }
       int c
    }

    func int test()
    {
      Bar bar = {}
      Bar.E e1 = Bar.E.E1
      Bar.E e2 = bar.getE2()
      return (int)e1 + (int)e2
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(3, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSimpleNestedInterface()
  {
    string bhl = @"
    class Bar {
      interface IFoo {
        func int foo()
      }

      class Foo : IFoo {
        func int foo() {
          return 1
        }
      }
    }

    class Foo : Bar.IFoo {
      func int foo() {
        return 10
      }
    }

    func int test()
    {
      Bar.Foo foo1 = {}
      Foo foo2 = {}
      return foo1.foo() + foo2.foo()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(11, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSimpleStaticMethod()
  {
    string bhl = @"
    class Bar {
      static func int foo() {
        return 42
      }
    }

    func int test()
    {
      return Bar.foo()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(42, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStaticMethodCallsAnotherStaticMethodWithoutPrefix()
  {
    string bhl = @"
    class Bar {
      static func int foo() {
        return bar()
      }

      static func int bar() {
        return 42
      }
    }

    func int test()
    {
      return Bar.foo()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(42, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestMethodCallsStaticMethodWithoutPrefix()
  {
    string bhl = @"
    class Bar {
      func int foo() {
        return bar()
      }

      static func int bar() {
        return 42
      }
    }

    func int test()
    {
      var bar = new Bar
      return bar.foo()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(42, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStaticMethodCallsSimilarWithMethodFreeFunc()
  {
    string bhl = @"
    func int Calc(int n) {
      return n
    }

    class Bar {
      static func int Doer() {
        return Calc(42) //won't lookup instance Calc() method
      }

      func int Calc() {
        return Calc(10) //just some garbage
      }
    }

    func int test()
    {
      return Bar.Doer()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(42, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSimpleStaticMethodWithArgs()
  {
    string bhl = @"
    class Bar {
      static func int foo(int a, int b) {
        return 42 + a + b
      }
    }

    func int test()
    {
      return Bar.foo(10, 100)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(42 + 10 + 100, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSimpleStaticMethodMixed()
  {
    string bhl = @"
    class Bar {
      int b
      static func int foo() {
        return 42
      }
      func int getC() {
        return this.c
      }
      int c
    }

    func int test()
    {
      Bar bar = {b: 10, c:20}
      return Bar.foo() + bar.b + bar.getC()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(42 + 10 + 20, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestStaticMethodImported()
  {
    string bhl1 = @"
    namespace a {
      class Bar {
        int b
        func garbage() { }

        static func int foo() {
          return 42
        }
        func int getC() {
          return this.c
        }
        int c
      }
    }
    ";

    string bhl2 = @"
    import ""bhl1""
    func int test()
    {
      a.Bar bar = {b: 10, c:20}
      return a.Bar.foo() + bar.b + bar.getC()
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );

    vm.LoadModule("bhl2");
    Assert.Equal(42 + 10 + 20, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStaticMethodCantBeCalledOnInstance()
  {
    string bhl = @"
    class Bar {
      static func int foo() {
        return 42
      }
    }

    func int test()
    {
      Bar b = {}
      return b.foo()
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "calling static method on instance is forbidden",
      new PlaceAssert(bhl, @"
      return b.foo()
--------------^"
      )
    );
  }

  [Fact]
  public void TestInstanceMethodCantBeCalledAsStatic()
  {
    string bhl = @"
    class Bar {
      func int foo() {
        return 42
      }
    }

    func int test()
    {
      return Bar.foo()
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "calling instance method as static is forbidden",
      new PlaceAssert(bhl, @"
      return Bar.foo()
----------------^"
      )
    );
  }

  [Fact]
  public void TestStaticMethodNoThisAllowed()
  {
    string bhl = @"
    class Bar {
      int a
      static func int foo() {
        return this.a
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "symbol 'this' not resolved",
      new PlaceAssert(bhl, @"
        return this.a
---------------^"
      )
    );
  }

  public class NativeFoo
  {
    public static int static_bar;

    public static int static_foo(int n)
    {
      return n;
    }
  }

  [Fact]
  public void TestNativeClassStaticMethod()
  {
    string bhl = @"
    func int test() {
      return NativeFoo.static_foo(42)
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      var cl = new ClassSymbolNative(new Origin(), "NativeFoo", null,
        (VM.ExecState exec, ref Val v, IType type) => { v.SetObj(new NativeFoo(), type); }
      );
      ts.ns.Define(cl);

      var m = new FuncSymbolNative(new Origin(), "static_foo", FuncAttrib.Static, ts.T("int"), 0,
        (VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          int n = exec.stack.Pop();
          exec.stack.Push(NativeFoo.static_foo(n));
          return null;
        },
        new FuncArgSymbol("int", ts.T("int"))
      );
      cl.Define(m);
      cl.Setup();
    });

    var vm = MakeVM(bhl, ts_fn);
    Assert.Equal(42, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSimpleStaticField()
  {
    string bhl = @"
    class Bar {
      int b
      static int foo
    }

    func int test()
    {
      Bar.foo = 42
      return Bar.foo
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(42, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStaticFieldInitNotAllowed()
  {
    string bhl = @"
    class Bar {
      int b
      static int foo = 42
    }

    func int test()
    {
      return Bar.foo
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "mismatched input '='",
      new PlaceAssert(bhl, @"
      static int foo = 42
---------------------^"
      )
    );
  }

  [Fact]
  public void TestNonCoroMemberCalledInYield()
  {
    string bhl = @"
    class Bar {
      func () cb

      coro func test() {
        yield this.cb()
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "not a coro function",
      new PlaceAssert(bhl, @"
        yield this.cb()
-------------------^"
      )
    );
  }

  [Fact]
  public async Task TestStaticFieldImported()
  {
    string bhl1 = @"
    namespace a {
      class Bar {
        int b
        static int foo
      }
    }
    ";

    string bhl2 = @"
    import ""bhl1""
    func int test()
    {
      a.Bar.foo = 42
      return a.Bar.foo
    }
    ";
    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );

    vm.LoadModule("bhl2");
    Assert.Equal(42, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestStaticFieldImportedMixWithGlobalVars()
  {
    string bhl0 = @"
      int G = 111
      int F
    ";

    string bhl1 = @"
    import ""bhl0""

    int A = 10
    namespace a {
      int A = 20
      class Bar {
        int b
        static int foo
      }
    }
    ";

    string bhl2 = @"
    import ""bhl1""

    int B = 30

    func int test()
    {
      a.Bar.foo = 42
      return a.A + A + B + a.Bar.foo
    }
    ";
    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl0.bhl", bhl0},
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );

    vm.LoadModule("bhl2");
    Assert.Equal(10 + 20 + 30 + 42, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeStaticField()
  {
    string bhl = @"
    func int test()
    {
      NativeFoo.static_bar = 42
      NativeFoo.static_bar++
      return NativeFoo.static_bar
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      var cl = new ClassSymbolNative(new Origin(), "NativeFoo");
      ts.ns.Define(cl);

      cl.Define(new FieldSymbol(new Origin(), "static_bar", FieldAttrib.Static, ts.T("int"),
        delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld) { v.SetInt(NativeFoo.static_bar); },
        delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld) { NativeFoo.static_bar = (int)v.num; }
      ));
      cl.Setup();
    });

    var vm = MakeVM(bhl, ts_fn);
    NativeFoo.static_bar = 14;
    Assert.Equal(43, Execute(vm, "test").Stack.Pop().num);
    Assert.Equal(43, NativeFoo.static_bar);
    CommonChecks(vm);
  }

  public class A
  {
    public int a;
    public int a2;
  }

  public class B : A
  {
    public int b;
    public int b2;
  }

  public class C : B
  {
    public int c;
  }

  public class TestNestedNativeClassesIrrelevantOrder : BHL_TestBase
  {
    Action<Types> ts_fn;

    public TestNestedNativeClassesIrrelevantOrder()
    {
      ts_fn = new Action<Types>((ts) =>
      {
        {
          var cl = new ClassSymbolNative(new Origin(), "C", ts.T("B"),
            (VM.ExecState exec, ref Val v, IType type) => { v.SetObj(new C(), type); }
          );

          ts.ns.Define(cl);

          cl.Define(new FieldSymbol(new Origin(), "c", ts.T("int"),
            delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld)
            {
              var c = (C)ctx.obj;
              v.SetInt(c.c);
            },
            delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld)
            {
              var c = (C)ctx.obj;
              c.c = (int)v.num;
            }
          ));
        }

        {
          var cl = new ClassSymbolNative(new Origin(), "B", ts.T("A"),
            (VM.ExecState exec, ref Val v, IType type) => { v.SetObj(new B(), type); }
          );

          ts.ns.Define(cl);

          cl.Define(new FieldSymbol(new Origin(), "b", ts.T("int"),
            delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld)
            {
              var b = (B)ctx.obj;
              v.SetInt(b.b);
            },
            delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld)
            {
              var b = (B)ctx.obj;
              b.b = (int)v.num;
            }
          ));

          cl.Define(new FieldSymbol(new Origin(), "b2", ts.T("int"),
            delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld)
            {
              var b = (B)ctx.obj;
              v.SetInt(b.b2);
            },
            delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld)
            {
              var b = (B)ctx.obj;
              b.b2 = (int)v.num;
            }
          ));
        }

        {
          var cl = new ClassSymbolNative(new Origin(), "A",
            (VM.ExecState exec, ref Val v, IType type) => { v.SetObj(new A(), type); }
          );

          ts.ns.Define(cl);

          cl.Define(new FieldSymbol(new Origin(), "a", ts.T("int"),
            delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld)
            {
              var a = (A)ctx.obj;
              v.SetInt(a.a);
            },
            delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld)
            {
              var a = (A)ctx.obj;
              a.a = (int)v._num;
            }
          ));

          cl.Define(new FieldSymbol(new Origin(), "a2", ts.T("int"),
            delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld)
            {
              var a = (A)ctx.obj;
              v.SetInt(a.a2);
            },
            delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld)
            {
              var a = (A)ctx.obj;
              a.a2 = (int)v.num;
            }
          ));
        }

        {
          //NOTE: setting up classes not in 'natural' order
          (ts.T("C").Get() as ClassSymbolNative).Setup();
          (ts.T("B").Get() as ClassSymbolNative).Setup();
          (ts.T("A").Get() as ClassSymbolNative).Setup();
        }
      });
    }

    [Fact]
    public void _1()
    {
      string bhl = @"
      func int test()
      {
        A a = {}
        B b = {}
        a.a = 10
        b.a = 100
        b.b = 1000
        return a.a + b.a + b.b
      }
      ";

      var vm = MakeVM(bhl, ts_fn);
      Assert.Equal(1110, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }

    [Fact]
    public void _2()
    {
      string bhl = @"
      func int test()
      {
        A a = {}
        B b = {}
        a.a2 = 10
        b.a2 = 100
        b.b2 = 1000
        return a.a2 + b.a2 + b.b2
      }
      ";

      var vm = MakeVM(bhl, ts_fn);
      Assert.Equal(1110, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }

    [Fact]
    public void _3()
    {
      string bhl = @"
      func int test()
      {
        C c = {}
        c.b2 = 10
        return c.b2
      }
      ";

      var vm = MakeVM(bhl, ts_fn);
      Assert.Equal(10, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }
  }

  [Fact]
  public void TestTypoArgWithClassType()
  {
    string bhl = @"
    class Bar {
    }

    func test1(Bar bar) {
    }

    func test2(Bar bar) {
      test1(Bar)
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "symbol usage is not valid",
      new PlaceAssert(bhl, @"
      test1(Bar)
------------^"
      )
    );
  }
}
