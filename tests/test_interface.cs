using System;
using System.Collections.Generic;
using System.Linq;
using bhl;

public class TestInterface : BHL_TestBase
{
  [IsTested()]
  public void TestEmptyUserInterface()
  {
    {
      string bhl = @"
      interface Foo {}
      ";

      var vm = MakeVM(bhl);
      var symb = vm.ResolveNamedByPath("Foo") as InterfaceSymbolScript;
      AssertTrue(symb != null);
    }

    {
      string bhl = @"
      interface Foo { }
      ";

      var vm = MakeVM(bhl);
      var symb = vm.ResolveNamedByPath("Foo") as InterfaceSymbolScript;
      AssertTrue(symb != null);
    }

    {
      string bhl = @"
      interface Foo { 
      }
      ";

      var vm = MakeVM(bhl);
      var symb = vm.ResolveNamedByPath("Foo") as InterfaceSymbolScript;
      AssertTrue(symb != null);
    }
  }

  [IsTested()]
  public void TestUserInterfaceWithMethod()
  {
    string bhl = @"
    interface Foo { 
      func hey(int a, float b)
    }
    ";

    var vm = MakeVM(bhl);
    var symb = vm.ResolveNamedByPath("Foo") as InterfaceSymbolScript;
    AssertTrue(symb != null);
    var hey = symb.FindMethod("hey").signature;
    AssertTrue(hey != null);
    AssertEqual(2, hey.arg_types.Count);
    AssertEqual(Types.Int, hey.arg_types[0].Get());
    AssertEqual(Types.Float, hey.arg_types[1].Get());
    AssertEqual(Types.Void, hey.ret_type.Get());
  }

  [IsTested()]
  public void TestUserInterfaceWithSeveralMethods()
  {
    string bhl = @"
    class Bar { 
    }

    interface Foo { 
      func bool hey(int a, float b)
      func Bar,int bar(string s)
    }
    ";

    var vm = MakeVM(bhl);
    var symb = vm.ResolveNamedByPath("Foo") as InterfaceSymbolScript;
    AssertTrue(symb != null);

    var hey = symb.FindMethod("hey").signature;
    AssertTrue(hey != null);
    AssertEqual(2, hey.arg_types.Count);
    AssertEqual(Types.Int, hey.arg_types[0].Get());
    AssertEqual(Types.Float, hey.arg_types[1].Get());
    AssertEqual(Types.Bool, hey.ret_type.Get());

    var bar = symb.FindMethod("bar").signature;
    AssertTrue(bar != null);
    AssertEqual(1, bar.arg_types.Count);
    AssertEqual(Types.String, bar.arg_types[0].Get());
    var tuple = (TupleType)bar.ret_type.Get();
    AssertEqual(2, tuple.Count);
    AssertEqual("Bar", tuple[0].path);
    AssertEqual(Types.Int, tuple[1].Get());
  }

  [IsTested()]
  public void TestUserInterfaceInheritanceIrrelevantOrder()
  {
    string bhl = @"
    interface Foo : Wow { 
      func bool hey(int a, float b)
    }

    interface Wow 
    {
      func Bar,int bar(string s)
    }

    class Bar { 
    }

    ";

    var vm = MakeVM(bhl);
    {
      var symb = vm.ResolveNamedByPath("Foo") as InterfaceSymbolScript;
      AssertTrue(symb != null);
      AssertEqual(1, symb.inherits.Count);
      AssertEqual("Wow", symb.inherits[0].name);
      AssertEqual(1, symb.Count());

      var hey = symb.FindMethod("hey").signature;
      AssertTrue(hey != null);
      AssertEqual(2, hey.arg_types.Count);
      AssertEqual(Types.Int, hey.arg_types[0].Get());
      AssertEqual(Types.Float, hey.arg_types[1].Get());
      AssertEqual(Types.Bool, hey.ret_type.Get());

      var bar = symb.FindMethod("bar").signature;
      AssertTrue(bar != null);
      AssertEqual(1, bar.arg_types.Count);
      AssertEqual(Types.String, bar.arg_types[0].Get());
      var tuple = (TupleType)bar.ret_type.Get();
      AssertEqual(2, tuple.Count);
      AssertEqual("Bar", tuple[0].path);
      AssertEqual(Types.Int, tuple[1].Get());
    }

    {
      var symb = vm.ResolveNamedByPath("Wow") as InterfaceSymbolScript;
      AssertTrue(symb != null);
      AssertEqual(0, symb.inherits.Count);
      AssertEqual(1, symb.Count());

      var bar = symb.FindMethod("bar").signature;
      AssertTrue(bar != null);
      AssertEqual(1, bar.arg_types.Count);
      AssertEqual(Types.String, bar.arg_types[0].Get());
      var tuple = (TupleType)bar.ret_type.Get();
      AssertEqual(2, tuple.Count);
      AssertEqual("Bar", tuple[0].path);
      AssertEqual(Types.Int, tuple[1].Get());
    }
  }

  [IsTested()]
  public void TestUserInterfaceDeepInheritance()
  {
    string bhl = @"
    interface A_sub : A {
      func int a_sub()
    }

    interface AB : A, B_sub, A_sub {
      func int ab()
    }

    class Imp : AB, B_sub { 
      func int a() {
        return 4
      }
      func int b() {
        return 3
      }
      func int a_sub() {
        return 40
      }
      func int b_sub() {
        return 30
      }
      func int ab() {
        return this.a() + this.b() + this.a_sub() + this.b_sub()
      }
    }

    interface A {
      func int a()
    }

    interface B_sub : B {
      func int b_sub()
    }

    interface B {
      func int b()
    }

    func int test() {
      Imp imp = {}
      return imp.ab()
    }

    ";

    var vm = MakeVM(bhl);
    AssertEqual(4+3+40+30, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserInterfaceMethodDefaultValuesNotAllowed()
  {
    string bhl = @"
    interface Foo { 
      func hey(int a, float b = 1)
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "default argument value is not allowed",
      new PlaceAssert(bhl, @"
      func hey(int a, float b = 1)
----------------------^"
      )
    );
  }

  [IsTested()]
  public void TestClassDoesntImplementInterface()
  {
    {
      string bhl = @"
      interface IFoo { 
        func int bar(int i)
      }
      class Foo : IFoo {
      }
      ";
      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "class 'Foo' doesn't implement interface 'IFoo' method 'func int bar(int i)'",
        new PlaceAssert(bhl, @"
      class Foo : IFoo {
------^"
        )
      );
    }

    {
      string bhl = @"
      interface IFoo { 
        func int bar(int i)
      }
      class Foo : IFoo {
        func bar(int i) { } 
      }
      ";
      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "class 'Foo' doesn't implement interface 'IFoo' method 'func int bar(int i)'",
        new PlaceAssert(bhl, @"
      class Foo : IFoo {
------^"
        )
      );
    }

    {
      string bhl = @"
      interface IFoo { 
        func int,string bar(int i)
      }
      class Foo : IFoo {
        func int,int bar(int i) { 
          return 1,2
        } 
      }
      ";
      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "class 'Foo' doesn't implement interface 'IFoo' method 'func int,string bar(int i)'",
        new PlaceAssert(bhl, @"
      class Foo : IFoo {
------^"
        )
      );
    }

    {
      string bhl = @"
      interface IFoo { 
        func int,string bar(int i)
      }

      class Foo {
      }

      func foo(IFoo ifoo) { 
      }

      func test() {
        Foo f = {} 
        foo(f)
      }
      ";
      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "incompatible types: 'IFoo' and 'Foo'",
        new PlaceAssert(bhl, @"
        foo(f)
------------^"
        )
      );
    }
  }

  [IsTested()]
  public void TestDoubleImplementationIsNotAllowed()
  {
    string bhl = @"
    interface IFoo { 
      func foo()
    }

    interface IBar { 
      func bar()
    }

    class Foo : IFoo, IBar, IFoo { 
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "interface is implemented already",
      new PlaceAssert(bhl, @"
    class Foo : IFoo, IBar, IFoo { 
----------------------------^"
      )
    );
  }

  [IsTested()]
  public void TestDoubleInheritanceIsNotAllowed()
  {
    string bhl = @"
    interface IFoo { 
      func foo()
    }

    interface IBar : IFoo, IFoo { 
      func bar()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "interface is inherited already",
      new PlaceAssert(bhl, @"
    interface IBar : IFoo, IFoo { 
---------------------------^"
      )
    );
  }

  [IsTested()]
  public void TestSelfInheritanceIsNotAllowed()
  {
    string bhl = @"
    interface IFoo { 
      func foo()
    }

    interface IBar : IBar { 
      func bar()
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "self inheritance is not allowed",
      new PlaceAssert(bhl, @"
    interface IBar : IBar { 
---------------------^"
      )
    );
  }

  [IsTested()]
  public void TestClassImplementsInterface()
  {
    {
      string bhl = @"
      interface IFoo { 
        func int bar(int i)
      }
      class Foo : IFoo {
        func int bar(int i) {
          return i
        }
      }
      ";
      var vm = MakeVM(bhl);
      var symb = vm.ResolveNamedByPath("Foo") as ClassSymbol;
      AssertTrue(symb != null);
      AssertEqual(1, symb.implements.Count);
      AssertEqual("IFoo", symb.implements[0].GetName());
    }

    {
      string bhl = @"
      interface IFoo { 
        func int,string bar(int i)
      }
      class Foo : IFoo {
        func int,string bar(int i) {
          return i,""foo""
        }
      }
      ";
      Compile(bhl);
    }
  }

  [IsTested()]
  public void TestCallInterfaceFuncAsVar()
  {
    {
      string bhl = @"
      interface IFoo { 
        func int bar(int i)
      }
      class Foo : IFoo {
        func int bar(int i) {
          return i+1
        }
      }

      func int test() {
        Foo foo = {}
        IFoo ifoo = foo
        return ifoo.bar(42)
      }
      ";

      var c = Compile(bhl);

      var expected = 
        new ModuleCompiler()
        .UseCode()
        .EmitThen(Opcodes.InitFrame, new int[] { 1+1/*this*/+1/*args info*/})
        .EmitThen(Opcodes.ArgVar, new int[] { 1 })
        .EmitThen(Opcodes.GetVar, new int[] { 1 })
        .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
        .EmitThen(Opcodes.Add)
        .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
        .EmitThen(Opcodes.ExitFrame)
        .EmitThen(Opcodes.InitFrame, new int[] { 2+1 /*args info*/})
        .EmitThen(Opcodes.New, new int[] { ConstIdx(c, c.ns.T("Foo")) }) 
        .EmitThen(Opcodes.SetVar, new int[] { 0 })
        .EmitThen(Opcodes.GetVar, new int[] { 0 })
        .EmitThen(Opcodes.SetVar, new int[] { 1 })
        .EmitThen(Opcodes.GetVar, new int[] { 1 })
        .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 42) })
        .EmitThen(Opcodes.CallMethodIface, new int[] { 0, ConstIdx(c, c.ns.T("IFoo")), 1 })
        .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
        .EmitThen(Opcodes.ExitFrame)
        ;
      AssertEqual(c, expected);

      var vm = MakeVM(c);
      AssertEqual(43, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      interface IBarBase1 { 
        func int bar1(int i)
      }
      interface IBarBase2 { 
        func int bar2(int i)
      }
      interface IBar : IBarBase1, IBarBase2 { 
        func foo()
      }
      class Foo : IBar {
        func foo() { } 

        func int bar1(int i) {
          return i+1
        }

        func int bar2(int i) {
          return i-1
        }
      }

      func int test() {
        Foo foo = {}
        IBarBase2 ifoo = foo
        return ifoo.bar2(42)
      }
      ";
      var vm = MakeVM(bhl);
      AssertEqual(41, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestCallInterfaceFuncAsVarOrderIrrelevant()
  {
    string bhl = @"
    func int test() {
      Foo foo = {}
      IBarBase2 ifoo = foo
      return ifoo.bar2(42)
    }
    
    interface IBar : IBarBase1, IBarBase2 { 
      func foo()

      func Foo circularDependency()
    }
    class Foo : IBar {
      func foo() { } 

      func int bar1(int i) {
        return i+1
      }

      func int bar2(int i) {
        return i-1
      }

      func Foo circularDependency() {
        return this
      }
    }

    interface IBarBase1 { 
      func int bar1(int i)
    }
    interface IBarBase2 { 
      func int bar2(int i)
    }
    ";
    var vm = MakeVM(bhl);
    AssertEqual(41, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassInterfaceAsFuncArgAndCall()
  {
    {
      string bhl = @"
      interface IFoo { 
        func int bar(int i)
      }
      class Foo : IFoo {
        func int bar(int i) {
          return i+1
        }
      }

      func int call(IFoo f, int i) {
        return f.bar(i)
      }

      func int test() {
        Foo f = {}
        return call(f, 42)
      }
      ";
      var vm = MakeVM(bhl);
      AssertEqual(43, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      interface IBarBase { 
        func int bar(int i)
      }
      interface IBar : IBarBase { 
        func foo()
      }
      class Foo : IBar {
        func foo() { } 

        func int bar(int i) {
          return i+1
        }
      }

      func int call(IBarBase b, int i) {
        return b.bar(i)
      }

      func int test() {
        Foo f = {}
        return call(f, 42)
      }
      ";
      var vm = MakeVM(bhl);
      AssertEqual(43, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestPassInterfaceAsFuncArgAndCallOrderIrrelevant()
  {
    {
      string bhl = @"
      func int test() {
        Foo f = {}
        return call(f, 42)
      }
      class Foo : IBar {
        func foo() { } 

        func int bar(int i) {
          return i+1
        }
      }

      interface IBar : IBarBase { 
        func foo()
      }
      interface IBarBase { 
        func int bar(int i)
      }
      func int call(IBarBase b, int i) {
        return b.bar(i)
      }

      ";
      var vm = MakeVM(bhl);
      AssertEqual(43, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestNullInterface()
  {
    string bhl = @"
    interface IFoo {
      func int foo()
    }

    func bool test() {
      IFoo ifoo = null
      return ifoo == null
    }
    ";
    var vm = MakeVM(bhl);
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNonNullInterface()
  {
    string bhl = @"
    interface IFoo {
      func int foo()
    }

    class Foo : IFoo {
      func int foo() {
        return 42
      }
    }

    func bool test() {
      IFoo ifoo = new Foo
      return ifoo != null
    }
    ";
    var vm = MakeVM(bhl);
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImplementingNativeInterfaceNotSupported()
  {
    string bhl = @"
    class Foo : IFoo {
      func int bar(int i) {
        return i+1
      }
    }
    ";
    var ts_fn = new Action<Types>((ts) => {
      var ifs = new InterfaceSymbolNative(
          new Origin(),
          "IFoo", 
          null, 
          new FuncSymbolNative(new Origin(), "bar", ts.T("int"), null, 
            new FuncArgSymbol("int", ts.T("int")) 
          )
      );
      ts.ns.Define(ifs);
    });

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts_fn);
      },
      "implementing native interfaces is not supported",
      new PlaceAssert(bhl, @"
    class Foo : IFoo {
----------------^"
      )
    );
  }

  [IsTested()]
  public void TestMixNativeAndScriptInterfacesNotSupported()
  {
    string bhl = @"
    interface IFoo { 
      func int foo(int k)
    }
    class Foo : IBar, IFoo {
      func int foo(int k) 
      { 
        return k
      } 

      func int bar(int i) {
        return i+1
      }
    }
    ";

    var ts_fn = new Action<Types>((ts) => { 
      var ifs = new InterfaceSymbolNative(
          new Origin(),
          "IBar", 
          null, 
          new FuncSymbolNative(new Origin(), "bar", ts.T("int"), null, 
            new FuncArgSymbol("int", ts.T("int")) 
          )
      );
      ts.ns.Define(ifs);
    });

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts_fn);
      },
      "implementing native interfaces is not supported",
      new PlaceAssert(bhl, @"
    class Foo : IBar, IFoo {
----------------^"
      )
    );
  }

  public interface IFooLocal
  {}

  public class LocalFoo : IFooLocal
  {
    public int X() 
    {
      return 10;
    }

    public int Y() 
    {
      return 20;
    }
  }

  [IsTested()]
  public void TestNativeInterfaceCastToConcreteType()
  {
    string bhl = @"
    func int test() {
      IFoo ifoo = create()
      Foo foo = (Foo)ifoo
      return foo.Y()
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      {
        var fn = new FuncSymbolNative(new Origin(), "create", ts.T("IFoo"),
            delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
              var foo = new LocalFoo();
              var v = Val.NewObj(frm.vm, foo, ts.T("IFoo").Get()); //NOTE: we set IFoo type
              stack.Push(v);
              return null;
            }
        );

        ts.ns.Define(fn);
      }

      {
        var ifs = new InterfaceSymbolNative(
            new Origin(),
            "IFoo", 
            null,
            typeof(IFooLocal)
        );
        ts.ns.Define(ifs);
        ifs.Setup();
      }

      {
        var cl = new ClassSymbolNative(new Origin(), "Foo", new List<TypeProxy<IType>>(){ ts.T("IFoo") },
          delegate(VM.Frame frm, ref Val v, IType type) 
          { 
            v.SetObj(new LocalFoo(), type);
          },
          typeof(LocalFoo)
        );
        ts.ns.Define(cl);

        {
          var m = new FuncSymbolNative(new Origin(), "X", ts.T("int"),
            delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
            {
              var foo = (LocalFoo)stack.PopRelease().obj;
              stack.Push(Val.NewInt(frm.vm, foo.X()));
              return null;
            }
          );
          cl.Define(m);
        }

        {
          var m = new FuncSymbolNative(new Origin(), "Y", ts.T("int"),
            delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
            {
              var foo = (LocalFoo)stack.PopRelease().obj;
              stack.Push(Val.NewInt(frm.vm, foo.Y()));
              return null;
            }
          );
          cl.Define(m);
        }

        cl.Setup();
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(20, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCallImportedMethodFromLocalMethod()
  {
    string bhl1 = @"
    interface IFoo {
      func int a()
    }
    class A : IFoo { 
      func int a() {
        return 10
      }
    }
    ";
      
  string bhl2 = @"
    import ""bhl1""  

    func int test() {
      IFoo foo = new A
      return foo.a()
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
  public void TestBugPassThisAsInterfaceArgToFreeFunc()
  {
    string bhl1 = @"
    import ""bhl2""
    namespace a {
      namespace c {
        interface IKlass {
          func Test()
        }
      }
      namespace b {
        func Foo(a.c.IKlass v) { 
        }
      }
    }
    ";
      
  string bhl2 = @"
    import ""bhl1""
    namespace a {
      namespace c {
        class Klass : IKlass {
          func Test() {
            a.b.Foo(this)
          }
        }
      }
    }
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2}
      }
    );

    vm.LoadModule("bhl2");
    CommonChecks(vm);
  }

  public interface INativeFoo 
  {
    int foo(int n);
  }

  public class NativeFoo : INativeFoo
  {
    public int foo(int n) { return n; }
  }

  [IsTested()]
  public void TestNativeClassAndInterface()
  {
    string bhl = @"
    func int test() {
      NativeFoo foo = {}
      INativeFoo ifoo = foo
      return ifoo.foo(42)
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      var ifs = new InterfaceSymbolNative(
        new Origin(),
        "INativeFoo", 
        null, 
        new FuncSymbolNative(new Origin(), "foo", ts.T("int"),
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
          {
            var n = (int)stack.PopRelease().num;
            var f = (INativeFoo)stack.PopRelease().obj;
            stack.Push(Val.NewInt(frm.vm, f.foo(n)));
            return null;
          },
          new FuncArgSymbol("int", ts.T("int")) 
        )
      );
      ts.ns.Define(ifs);
      ifs.Setup();

      var cl = new ClassSymbolNative(new Origin(), "NativeFoo", new List<TypeProxy<IType>>(){ ts.T("INativeFoo") },
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          v.SetObj(new NativeFoo(), type);
        }
      );
      ts.ns.Define(cl);

      var m = new FuncSymbolNative(new Origin(), "foo", ts.T("int"),
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
        {
          var n = (int)stack.PopRelease().num;
          var f = (NativeFoo)stack.PopRelease().obj;
          stack.Push(Val.NewInt(frm.vm, f.foo(n)));
          return null;
        },
        new FuncArgSymbol("int", ts.T("int")) 
      );
      cl.Define(m);
      cl.Setup();
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCircularDependencyInModules()
  {
    string bhl1 = @"
    import ""bhl2""
    namespace a {
      namespace b {
        func Foo(a.c.IKlass v) { 
        }
      }
    }
    ";
      
  string bhl2 = @"
    import ""bhl1""
    namespace a {
      namespace c {
        interface IKlass {
          func Test()
        }
        class Klass : IKlass {
          func Test() {
            a.b.Foo(this)
          }
        }
      }
    }
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2}
      }
    );

    vm.LoadModule("bhl2");
    CommonChecks(vm);
  }
}
