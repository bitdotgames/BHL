using System;
using System.IO;
using System.Text;
using bhl;

public class TestInterfaces : BHL_TestBase
{
  [IsTested()]
  public void TestEmptyUserInterface()
  {
    string bhl = @"
    interface Foo { }
    ";

    var vm = MakeVM(bhl);
    var symb = vm.Types.Resolve("Foo") as InterfaceSymbolScript;
    AssertTrue(symb != null);
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
    var symb = vm.Types.Resolve("Foo") as InterfaceSymbolScript;
    AssertTrue(symb != null);
    var hey = symb.FindMethod("hey").GetSignature();
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
    var symb = vm.Types.Resolve("Foo") as InterfaceSymbolScript;
    AssertTrue(symb != null);

    var hey = symb.FindMethod("hey").GetSignature();
    AssertTrue(hey != null);
    AssertEqual(2, hey.arg_types.Count);
    AssertEqual(Types.Int, hey.arg_types[0].Get());
    AssertEqual(Types.Float, hey.arg_types[1].Get());
    AssertEqual(Types.Bool, hey.ret_type.Get());

    var bar = symb.FindMethod("bar").GetSignature();
    AssertTrue(bar != null);
    AssertEqual(1, bar.arg_types.Count);
    AssertEqual(Types.String, bar.arg_types[0].Get());
    var tuple = (TupleType)bar.ret_type.Get();
    AssertEqual(2, tuple.Count);
    AssertEqual("Bar", tuple[0].name);
    AssertEqual(Types.Int, tuple[1].Get());
  }

  [IsTested()]
  public void TestUserInterfaceInheritance()
  {
    string bhl = @"
    class Bar { 
    }

    interface Wow 
    {
      func Bar,int bar(string s)
    }

    interface Foo : Wow { 
      func bool hey(int a, float b)
    }
    ";

    var vm = MakeVM(bhl);
    {
      var symb = vm.Types.Resolve("Foo") as InterfaceSymbolScript;
      AssertTrue(symb != null);
      AssertEqual(1, symb.inherits.Count);
      AssertEqual("Wow", symb.inherits[0].name);
      AssertEqual(2, symb.GetMembers().Count);

      var hey = symb.FindMethod("hey").GetSignature();
      AssertTrue(hey != null);
      AssertEqual(2, hey.arg_types.Count);
      AssertEqual(Types.Int, hey.arg_types[0].Get());
      AssertEqual(Types.Float, hey.arg_types[1].Get());
      AssertEqual(Types.Bool, hey.ret_type.Get());

      var bar = symb.FindMethod("bar").GetSignature();
      AssertTrue(bar != null);
      AssertEqual(1, bar.arg_types.Count);
      AssertEqual(Types.String, bar.arg_types[0].Get());
      var tuple = (TupleType)bar.ret_type.Get();
      AssertEqual(2, tuple.Count);
      AssertEqual("Bar", tuple[0].name);
      AssertEqual(Types.Int, tuple[1].Get());
    }

    {
      var symb = vm.Types.Resolve("Wow") as InterfaceSymbolScript;
      AssertTrue(symb != null);
      AssertEqual(0, symb.inherits.Count);
      AssertEqual(1, symb.GetMembers().Count);

      var bar = symb.FindMethod("bar").GetSignature();
      AssertTrue(bar != null);
      AssertEqual(1, bar.arg_types.Count);
      AssertEqual(Types.String, bar.arg_types[0].Get());
      var tuple = (TupleType)bar.ret_type.Get();
      AssertEqual(2, tuple.Count);
      AssertEqual("Bar", tuple[0].name);
      AssertEqual(Types.Int, tuple[1].Get());
    }
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
      "default value is not allowed"
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
        "class 'Foo' doesn't implement interface 'IFoo' method 'func int bar(int)'"
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
        "class 'Foo' doesn't implement interface 'IFoo' method 'func int bar(int)'"
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
        "class 'Foo' doesn't implement interface 'IFoo' method 'func int,string bar(int)'"
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
        "incompatible types"
      );
    }
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
      var symb = vm.Types.Resolve("Foo") as ClassSymbol;
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
        new Compiler()
        .UseCode()
        .EmitThen(Opcodes.InitFrame, new int[] { 1+1/*this*/+1/*args info*/})
        .EmitThen(Opcodes.ArgVar, new int[] { 1 })
        .EmitThen(Opcodes.GetVar, new int[] { 1 })
        .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
        .EmitThen(Opcodes.Add)
        .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
        .EmitThen(Opcodes.Return)
        .EmitThen(Opcodes.InitFrame, new int[] { 2+1 /*args info*/})
        .EmitThen(Opcodes.New, new int[] { ConstIdx(c, "Foo") }) 
        .EmitThen(Opcodes.SetVar, new int[] { 0 })
        .EmitThen(Opcodes.GetVar, new int[] { 0 })
        .EmitThen(Opcodes.SetVar, new int[] { 1 })
        .EmitThen(Opcodes.GetVar, new int[] { 1 })
        .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 42) })
        .EmitThen(Opcodes.CallMethodVirt, new int[] { 0, ConstIdx(c, "IFoo"), 1 })
        .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
        .EmitThen(Opcodes.Return)
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
  public void TestBindNativeInterface()
  {
    string bhl = @"
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
    var ts = new Types();

    var ifs = new InterfaceSymbolNative(
        "IFoo", 
        null, 
        new FuncSymbolNative("bar", ts.Type("int"), null, 
          new FuncArgSymbol("int", ts.Type("int")) 
        )
    );
    ts.globs.Define(ifs);

    var vm = MakeVM(bhl, ts);
    AssertEqual(43, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestMixNativeAndScriptInterfaces()
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

    func int test() {
      Foo f = {}
      IBar ifb = f;
      IFoo iff = f;
      return iff.foo(1) + ifb.bar(10)
    }
    ";

    var ts = new Types();

    var ifs = new InterfaceSymbolNative(
        "IBar", 
        null, 
        new FuncSymbolNative("bar", ts.Type("int"), null, 
          new FuncArgSymbol("int", ts.Type("int")) 
        )
    );
    ts.globs.Define(ifs);

    var vm = MakeVM(bhl, ts);
    AssertEqual(12, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }
}
