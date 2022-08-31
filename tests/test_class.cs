using System;           
using System.IO;
using System.Text;
using System.Collections.Generic;
using bhl;

public class TestClasses : BHL_TestBase
{
  [IsTested()]
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
        .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
        .EmitThen(Opcodes.New, new int[] { ConstIdx(c, c.ns.T("Foo")) }) 
        .EmitThen(Opcodes.SetVar, new int[] { 0 })
        .EmitThen(Opcodes.GetVar, new int[] { 0 })
        .EmitThen(Opcodes.Constant, new int[] { 1 })
        .EmitThen(Opcodes.NotEqual)
        .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
        .EmitThen(Opcodes.ExitFrame)
        ;
      AssertEqual(c, expected);

      var vm = MakeVM(c);
      var fb = vm.Start("test");
      AssertFalse(vm.Tick());
      AssertTrue(fb.result.PopRelease().bval);
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
      AssertFalse(vm.Tick());
      AssertTrue(fb.result.PopRelease().bval);
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestUserClassBindConflict()
  {
    string bhl = @"

    class Foo {}
      
    func test() 
    {
      Foo f = {}
    }
    ";

    var ts = new Types();
    BindFoo(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "already defined symbol 'Foo'",
      new PlaceAssert(bhl, @"
    class Foo {}
----^"
      )
    );
  }

  [IsTested()]
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
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSelfInheritanceIsNotAllowed()
  {
    string bhl = @"
    class Foo : Foo {}
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "self inheritance is not allowed",
      new PlaceAssert(bhl, @"
    class Foo : Foo {}
----------------^"
      )
    );
  }

  [IsTested()]
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

    var ts = new Types();
    var log = new StringBuilder();
    var fn = BindTrace(ts, log);
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, c.ns.T("Foo")) }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 14.2) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { 2 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 0 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, c.ns.T("string")), 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 1 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, c.ns.T("string")), 0 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 2 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.CallNative, new int[] { ts.nfunc_index.IndexOf(fn), 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("10;14.2;Hey", log.ToString().Replace(',', '.')/*locale issues*/);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(102, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(113.5, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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

    var ts = new Types();
    
    BindColorAlpha(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 1202);
    CommonChecks(vm);
  }

  [IsTested()]
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

    var ts = new Types();
    
    BindColorAlpha(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 60);
    CommonChecks(vm);
  }

  [IsTested()]
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

    var ts = new Types();
    
    BindColorAlpha(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 90);
    CommonChecks(vm);
  }

  [IsTested()]
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

    var ts = new Types();

    BindColor(ts);

    {
      var cl = new ClassSymbolNative("ColorNested", null,
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          v.SetObj(new ColorNested(), type);
        }
      );

      ts.ns.Define(cl);

      cl.Define(new FieldSymbol("c", ts.T("Color"), 
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var cn = (ColorNested)ctx.obj;
          v.SetObj(cn.c, ts.T("Color").Get());
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var cn = (ColorNested)ctx.obj;
          cn.c = (Color)v.obj; 
          ctx.SetObj(cn, ctx.type);
        }
      ));
      cl.Setup();
    }

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 202);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCtorNotAllowed()
  {
    string bhl = @"
      
    func test() 
    {
      Foo f = new Foo
    }
    ";

    var ts = new Types();

    {
      var cl = new ClassSymbolNative("Foo", null, null);

      ts.ns.Define(cl);

      cl.Setup();
    }

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "constructor is not defined",
      new PlaceAssert(bhl, @"
      Foo f = new Foo
--------------^"
      )
    );
  }

  [IsTested()]
  public void TestJsonCtorNotAllowed()
  {
    string bhl = @"
      
    func test() 
    {
      Foo f = {}
    }
    ";

    var ts = new Types();

    {
      var cl = new ClassSymbolNative("Foo", null, null);

      ts.ns.Define(cl);

      cl.Setup();
    }

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "constructor is not defined",
      new PlaceAssert(bhl, @"
      Foo f = {}
--------------^"
      )
    );
  }

  [IsTested()]
  public void TestGetFieldOperationIsNotAllowed()
  {
    string bhl = @"
      
    func int test() 
    {
      Foo f = {}
      return f.x
    }
    ";

    var ts = new Types();

    {
      var cl = new ClassSymbolNative("Foo", null,
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
         //dummy
        }
      );

      ts.ns.Define(cl);

      cl.Define(new FieldSymbol("x", ts.T("int"), 
        null, //no getter
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          //dummy
        }
      ));
      cl.Setup();
    }

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "get operation is not defined",
      new PlaceAssert(bhl, @"
      return f.x
---------------^"
      )
    );
  }

  [IsTested()]
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

    var ts = new Types();

    {
      var cl = new ClassSymbolNative("Foo", null,
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
         //dummy
        }
      );

      ts.ns.Define(cl);

      cl.Define(new FieldSymbol("x", ts.T("int"), 
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
         //dummy
        },
        null //no setter
      ));
      cl.Setup();
    }

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "set operation is not defined",
      new PlaceAssert(bhl, @"
      f.x = 10
--------^"
      )
    );
  }

  [IsTested()]
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

    var ts = new Types();
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 2);
    CommonChecks(vm);
  }


  [IsTested()]
  public void TestImportUserClass()
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      },
      ts
    );

    vm.LoadModule("bhl2");
    Execute(vm, "test");
    AssertEqual("10;14.2;Hey", log.ToString().Replace(',', '.')/*locale issues*/);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportClassMoreComplex()
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      }
    );

    vm.LoadModule("bhl1");
    AssertEqual(42, Execute(vm, "test", Val.NewNum(vm, 42)).result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportClassConflict()
  {
    string bhl1 = @"
    import ""bhl2""  

    class Bar { }

    func test() { }
    ";

    string bhl2 = @"
    class Bar { }
    ";

    AssertError<Exception>(
      delegate() { 
        MakeVM(new Dictionary<string, string>() {
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


  [IsTested()]
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
    AssertEqual("0;0;;", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(20, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
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
      delegate() { 
        Compile(bhl);
      },
      "type 'Foo' not found",
      new PlaceAssert(bhl, @"
    func []Foo fetch() {
-----------^"
      )
    );
  }

  [IsTested()]
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
    AssertEqual(30, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(3, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(12, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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

    var vm = MakeVM(bhl);
    AssertEqual(0, Execute(vm, "test").result.Count);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(21, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual("", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
      .EmitThen(Opcodes.InitFrame, new int[] { 1+1 /*args info*/})
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      .EmitThen(Opcodes.InitFrame, new int[] { 1+1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, c.ns.T("Foo")) }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.CallMethod, new int[] { 1, 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(30, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
      delegate() { 
        Compile(bhl);
      },
      "no base class",
      new PlaceAssert(bhl, @"
        return base.a
---------------^"
      )
    );
  }

  [IsTested()]
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
      delegate() { 
        Compile(bhl);
      },
      "symbol 'b' not resolved",
      new PlaceAssert(bhl, @"
        return base.b
--------------------^"
      )
    );
  }


  [IsTested()]
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
      delegate() { 
        Compile(bhl);
      },
      "keyword 'base' is reserved",
      new PlaceAssert(bhl, @"
        int base = 1
------------^"
      )
    );
  }

  [IsTested()]
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
    AssertEqual(12, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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

  [IsTested()]
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
    AssertEqual(22, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(112, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
      delegate() { 
        Compile(bhl);
      },
      "symbol 'a' not resolved",
      new PlaceAssert(bhl, @"
          a = 1
----------^"
      )
    );
  }

  [IsTested()]
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
    AssertEqual(11, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
      delegate() { 
        Compile(bhl);
      },
      "already defined symbol 'b'",
      new PlaceAssert(bhl, @"
      int b
------^"
      )
    );
  }

  [IsTested()]
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
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(1, Execute(vm, "test1").result.PopRelease().num);
    AssertEqual(1, Execute(vm, "test2").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
      delegate() { 
        Compile(bhl);
      },
      @"already defined symbol 'getA'",
      new PlaceAssert(bhl, @"
      func int getA() {
------^"
      )
    );
  }

  [IsTested()]
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
    AssertEqual(33, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(101, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(101, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(102, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(3, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(3, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(3, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTmpUserClassWriteFieldNotAllowed()
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

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"mismatched input '(' expecting '}'",
      new PlaceAssert(bhl, @"
      (new Foo{c: 10}).c = 20
------^"
      )
    );
  }

  [IsTested()]
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
      delegate() { 
        Compile(bhl);
      },
      "already defined symbol 'x'",
      new PlaceAssert(bhl, @"
      int x
------^"
      )
    );
  }

  [IsTested()]
  public void TestChildUserClassOfNativeClassIsForbidden()
  {
    string bhl = @"

    class ColorA : Color 
    { 
      float a
    }
    ";

    var ts = new Types();
    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "extending native classes is not supported",
      new PlaceAssert(bhl, @"
    class ColorA : Color 
-------------------^"
      )
    );
  }

  [IsTested()]
  public void TestUserClassNotAllowedToContainThisMember()
  {
    string bhl = @"
      class Foo {
        int this
      }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "the keyword \"this\" is reserved",
      new PlaceAssert(bhl, @"
        int this
------------^"
      )
    );
  }

  [IsTested()]
  public void TestUserClassContainThisMethodNotAllowed()
  {
    string bhl = @"
      class Foo {

        func bool this()
        {
          return false
        }
      }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "the keyword \"this\" is reserved",
      new PlaceAssert(bhl, @"
        func bool this()
------------------^"
      )
    );
  }

  [IsTested()]
  public void TestBindNativeClass()
  {
    string bhl = @"

    func bool test() 
    {
      Bar b = new Bar
      return b != null
    }
    ";

    var ts = new Types();
    BindBar(ts);
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, ts.T("Bar")) }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstNullIdx(c) })
      .EmitThen(Opcodes.NotEqual)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
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

    var ts = new Types();
    var log = new StringBuilder();
    var fn = BindTrace(ts, log);
    BindBar(ts);
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, ts.T("Bar")) }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 14.5) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { 2 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 0 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, ts.T("string")), 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 1 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, ts.T("string")), 0 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 2 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.CallNative, new int[] { ts.nfunc_index.IndexOf(fn), 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("10;14.5;Hey", log.ToString().Replace(',', '.')/*locale issues*/);
    CommonChecks(vm);
  }

  [IsTested()]
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

    var ts = new Types();
    
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 202);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeClassCallMember()
  {
    string bhl = @"

    func float test(float a)
    {
      return mkcolor(a).r
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 10)).result.PopRelease().num;
    AssertEqual(res, 10);
    res = Execute(vm, "test", Val.NewNum(vm, 20)).result.PopRelease().num;
    AssertEqual(res, 20);
    CommonChecks(vm);
  }

  [IsTested()]
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

    var ts = new Types();

    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "symbol is not a function",
      new PlaceAssert(bhl, @"
      c.r()
--------^"
      )
    );
  }

  [IsTested()]
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("NULL;NOTNULL;EQ;NOTNULL;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCallImportedMethodFromLocalMethod()
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2}
      }
    );

    vm.LoadModule("bhl2");
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCallImportedMemberMethodFromMethod()
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3}
      }
    );

    vm.LoadModule("bhl3");
    AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCallImportedVirtualMemberMethodFromMethod()
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
        func virtual int Foo() {
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3}
      }
    );

    vm.LoadModule("bhl3");
    AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBasicVirtualMethodsSupport()
  {
    string bhl = @"

    class Foo {
      
      int a
      int b

      func virtual int getA() {
        return this.a
      }

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int new_a

      func override int getA() {
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
    ";

    var vm = MakeVM(bhl);
    AssertEqual(110, Execute(vm, "test1").result.PopRelease().num);
    AssertEqual(110, Execute(vm, "test2").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestVirtualMethodsChecks()
  {
    {
      string bhl = @"
      class Foo {
        func override int getA() {
          return 42
        }
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "no base virtual method to override",
        new PlaceAssert(bhl, @"
        func override int getA() {
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
        func override int getA() {
          return 42
        }
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "no base virtual method to override",
        new PlaceAssert(bhl, @"
        func override int getA() {
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
        func virtual int getA() {
          return 42
        }
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "already defined symbol 'getA'",
        new PlaceAssert(bhl, @"
        func virtual int getA() {
--------^"
        )
      );
    }

    {
      string bhl = @"
      class Hey {
        func virtual int getA() {
          return 4
        }
      }
      class Foo : Hey {
        func override float getA() {
          return 42
        }
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "signature doesn't match the base one",
        new PlaceAssert(bhl, @"
        func override float getA() {
--------^"
        )
      );
    }

    {
      string bhl = @"
      class Foo {
        func virtual void getA(int b, int a = 1) {
        }
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "virtual methods are not allowed to have default arguments",
        new PlaceAssert(bhl, @"
        func virtual void getA(int b, int a = 1) {
--------^"
        )
      );
    }

    {
      string bhl = @"
      class Foo {
        func virtual void getA(int b, int a) {
        }
      }

      class Bar : Foo {
        func override void getA(int b, int a = 1) {
        }
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "virtual methods are not allowed to have default arguments",
        new PlaceAssert(bhl, @"
        func override void getA(int b, int a = 1) {
--------^"
        )
      );
    }
  }

  [IsTested()]
  public void TestVirtualOverrideIn3dChild()
  {
    string bhl = @"

    class Foo {
      
      int a
      int b

      func int getB() {
        return this.b
      }

      func virtual int getA() {
        return this.a
      }
    }

    class Bar : Foo {
      int bar_a

      func override int getA() {
        return this.bar_a
      }
    }

    class Hey : Bar {
      int hey_a

      func override int getA() {
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
    AssertEqual(210, Execute(vm, "test1").result.PopRelease().num);
    AssertEqual(210, Execute(vm, "test2").result.PopRelease().num);
    AssertEqual(210, Execute(vm, "test3").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestVirtualMethodsSupportBaseCall()
  {
    string bhl = @"

    class Foo {
      
      int a
      int b

      func virtual int getA() {
        return this.a
      }

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int new_a

      func override int getA() {
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
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportedClassVirtualMethodsSupport()
  {
    string bhl1 = @"
    class Foo {
      
      int b
      int a

      func int DummyGarbage0() {
        return 42
      }

      func virtual int getA() {
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

      func override int getA() {
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2}
      }
    );
    
    vm.LoadModule("bhl2");
    AssertEqual(110, Execute(vm, "test1").result.PopRelease().num);
    AssertEqual(110, Execute(vm, "test2").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportedClassVirtualMethodsSupportInADifferentNamespace()
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

        func virtual int getA(Item item) {
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

      func override int getA(fns.Item item) {
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2}
      }
    );
    
    vm.LoadModule("bhl2");
    AssertEqual(100 + 1000, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestInvalidFuncAttrubutes()
  {
    string bhl = @"
    func override int getA() {
      return 42
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "improper usage of attribute",
      new PlaceAssert(bhl, @"
    func override int getA() {
---------^"
      )
    );
  }

  [IsTested()]
  public void TestImportedClassVirtualMethodsOrderIsIrrelevant()
  {
    string bhl1 = @"
    import ""bhl2""  
    namespace a {
      class A : b.B {
        func override int Foo(int v) {
          return v + 1
        }
      }
    }
  ";

  string bhl2 = @"
    import ""bhl1""  
    namespace b {
      class B {
        func virtual int Foo(int v) {
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2}
      }
    );
    
    vm.LoadModule("bhl2");
    AssertEqual(2, Execute(vm, "test1").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
      func virtual int test() { return 1 }
    }

    class Bar : BaseBar {
      func override int test() { return 2 }
    }

    func int test() {
      IBar bar = MakeIBar()
      return bar.test()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
      func virtual int test(int v) { return v+1 }
    }

    class Bar : BaseBar {
      func override int test(int v) { return v+2 }
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
    AssertEqual(12, Execute(vm, "test1").result.PopRelease().num);
    AssertEqual(12, Execute(vm, "test2").result.PopRelease().num);
    CommonChecks(vm);
  }

  public class VirtFoo {
    public int a;
    public int b;

    public virtual int getA() {
      return a;
    }

    public int getB() {
      return b;
    }
  }

  public class VirtBar : VirtFoo {
    public int new_a;

    public override int getA() {
      return new_a;
    }
  }

  void BindVirtualFooBar(Types ts)
  {
    {
      var cl = new ClassSymbolNative("Foo", null,
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          v.SetObj(new VirtFoo(), type);
        }
      );
      ts.ns.Define(cl);

      cl.Define(new FieldSymbol("a", Types.Int,
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var f = (VirtFoo)ctx.obj;
          v.SetNum(f.a);
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var f = (VirtFoo)ctx.obj;
          f.a = (int)v.num; 
          ctx.SetObj(f, ctx.type);
        }
      ));

      cl.Define(new FieldSymbol("b", Types.Int,
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var f = (VirtFoo)ctx.obj;
          v.SetNum(f.b);
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var f = (VirtFoo)ctx.obj;
          f.b = (int)v.num; 
          ctx.SetObj(f, ctx.type);
        }
      ));

      {
        var m = new FuncSymbolNative("getA", ts.T("int"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var f = (VirtFoo)frm.stack.PopRelease().obj;
            var v = Val.NewNum(frm.vm, f.getA());
            frm.stack.Push(v);
            return null;
          }
        );
        cl.Define(m);
      }

      {
        var m = new FuncSymbolNative("getB", ts.T("int"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var f = (VirtFoo)frm.stack.PopRelease().obj;
            var v = Val.NewNum(frm.vm, f.getB());
            frm.stack.Push(v);
            return null;
          }
        );
        cl.Define(m);
      }
      cl.Setup();
    }

    {
      var cl = new ClassSymbolNative("Bar", (ClassSymbol)ts.T("Foo").Get(),
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          v.SetObj(new VirtBar(), type);
        }
      );

      cl.Define(new FieldSymbol("new_a", Types.Int,
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var b = (VirtBar)ctx.obj;
          v.SetNum(b.new_a);
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
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

  [IsTested()]
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
      //NOTE: Bar.getA() will be called anyway!
      return ((Foo)b).getA() + b.getB()
    }
    ";

    var ts = new Types();
    BindVirtualFooBar(ts);

    var vm = MakeVM(bhl, ts);
    AssertEqual(110, Execute(vm, "test1").result.PopRelease().num);
    AssertEqual(110, Execute(vm, "test2").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(31, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(11, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(31, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(81, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(3, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(11, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSimpleStaticMethod()
  {
    string bhl = @"
    class Bar {
      func static int foo() {
        return 42
      }
    }

    func int test() 
    {
      return Bar.foo()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSimpleStaticMethodMixed()
  {
    string bhl = @"
    class Bar {
      int b
      func static int foo() {
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
    AssertEqual(42+10+20, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStaticMethodImported()
  {
    string bhl1 = @"
    namespace a {
      class Bar {
        int b
        func static int foo() {
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );

    vm.LoadModule("bhl2");
    AssertEqual(42+10+20, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStaticMethodCantBeCalledOnInstance()
  {
    string bhl = @"
    class Bar {
      func static int foo() {
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
      delegate() { 
        Compile(bhl);
      },
      "calling static method on instance is forbidden",
      new PlaceAssert(bhl, @"
      return b.foo()
--------------^"
      )
    );
  }

  [IsTested()]
  public void TestStaticMethodNoThisAllowed()
  {
    string bhl = @"
    class Bar {
      int a
      func static int foo() {
        return this.a
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
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

    public static int static_foo(int n) { return n; }
  }

  [IsTested()]
  public void TestNativeClassStaticMethod()
  {
    string bhl = @"
    func int test() {
      return NativeFoo.static_foo(42)
    }
    ";
    var ts = new Types();

    {
      var cl = new ClassSymbolNative("NativeFoo", null,
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          v.SetObj(new NativeFoo(), type);
        }
      );
      ts.ns.Define(cl);

      var m = new FuncSymbolNative("static_foo", FuncAttrib.Static, ts.T("int"), 0,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
        {
          var n = (int)frm.stack.PopRelease().num;
          frm.stack.Push(Val.NewInt(frm.vm, NativeFoo.static_foo(n)));
          return null;
        },
        new FuncArgSymbol("int", ts.T("int")) 
      );
      cl.Define(m);
      cl.Setup();
    }

    var vm = MakeVM(bhl, ts);
    AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
      delegate() { 
        Compile(bhl);
      },
      "mismatched input '='",
      new PlaceAssert(bhl, @"
      static int foo = 42
---------------------^"
      )
    );
  }

  [IsTested()]
  public void TestStaticFieldImported()
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
    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );

    vm.LoadModule("bhl2");
    AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStaticFieldImportedMixWithGlobalVars()
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
    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl0.bhl", bhl0},
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );

    vm.LoadModule("bhl2");
    AssertEqual(10+20+30+42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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

    var ts = new Types();

    {
      var cl = new ClassSymbolNative("NativeFoo", null, null);
      ts.ns.Define(cl);

      cl.Define(new FieldSymbol("static_bar", FieldAttrib.Static, ts.T("int"), 
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          v.SetInt(NativeFoo.static_bar);
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          NativeFoo.static_bar = (int)v.num;
        }
      ));
      cl.Setup();
    }

    var vm = MakeVM(bhl, ts);
    NativeFoo.static_bar = 14;
    AssertEqual(43, Execute(vm, "test").result.PopRelease().num);
    AssertEqual(43, NativeFoo.static_bar);
    CommonChecks(vm);
  }
}
