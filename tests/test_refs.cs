
using System;
using bhl;
using Xunit;

public class TestRefs : BHL_TestBase
{
  [Fact]
  public void TestPassByRef()
  {
    string bhl = @"

    func foo(ref float a)
    {
      a = a + 1
    }

    func float test(float k)
    {
      foo(ref k)
      return k
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
          .EmitThen(Opcodes.ArgRef, new int[] { 0 })
          .EmitThen(Opcodes.GetRef, new int[] { 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.Add)
          .EmitThen(Opcodes.SetRef, new int[] { 0 })
          .EmitThen(Opcodes.Return)
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
          .EmitThen(Opcodes.ArgVar, new int[] { 0 })
          .EmitThen(Opcodes.RefVar, new int[] { 0 })
          .EmitThen(Opcodes.CallLocal, new int[] { 0, 1 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    double num = Execute(vm, "test",  3).Stack.Pop();
    Assert.Equal(4, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassByRefNonAssignedValue()
  {
    string bhl = @"

    func foo(ref float a)
    {
      a = a + 1
    }

    func float test()
    {
      float k
      foo(ref k)
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(1, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassByRefAlreadyDefinedError()
  {
    string bhl = @"

    func foo(ref float a, float a)
    {
      a = a + 1
    }

    func float test(float k)
    {
      foo(ref k, k)
      return k
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "already defined symbol 'a'",
      new PlaceAssert(bhl, @"
    func foo(ref float a, float a)
--------------------------------^"
      )
    );
  }

  [Fact]
  public void TestPassByRefAssignToNonRef()
  {
    string bhl = @"

    func foo(ref float a)
    {
      float b = a
      b = b + 1
    }

    func float test(float k)
    {
      foo(ref k)
      return k
    }
    ";

    var vm = MakeVM(bhl);
    double num = Execute(vm, "test", 3).Stack.Pop();
    Assert.Equal(3, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassByRefNested()
  {
    string bhl = @"

    func bar(ref float b)
    {
      b = b * 2 //8
    }

    func foo(ref float a)
    {
      a = a + 1 //4
      bar(ref a)
    }

    func float test(float k)
    {
      foo(ref k)
      return k
    }
    ";

    var vm = MakeVM(bhl);
    double num = Execute(vm, "test", 3).Stack.Pop();
    Assert.Equal(8, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassByRefMixed()
  {
    string bhl = @"

    func foo(ref float a, float b)
    {
      a = a + b
    }

    func float test(float k)
    {
      foo(ref k, k)
      return k
    }
    ";

    var vm = MakeVM(bhl);
    double num = Execute(vm, "test", 3).Stack.Pop();
    Assert.Equal(6, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassByRefInUserBinding()
  {
    string bhl = @"

    func float test(float k)
    {
      func_with_ref(k, ref k)
      return k
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      {
        var fn = new FuncSymbolNative(new Origin(), "func_with_ref", Types.Void,
          (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            ref var b = ref exec.stack.Pop().Unref();
            double a = exec.stack.Pop();

            b._num = a * 2;
            return null;
          },
          new FuncArgSymbol("a", ts.T("float")),
          new FuncArgSymbol("b", ts.T("float"), true /*is ref*/)
        );

        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    double num = Execute(vm, "test", 3).Stack.Pop();
    Assert.Equal(6, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassByRefNamedArg()
  {
    string bhl = @"

    func foo(ref float a, float b)
    {
      a = a + b
    }

    func float test(float k)
    {
      foo(a : ref k, b: k)
      return k
    }
    ";

    var vm = MakeVM(bhl);
    double num = Execute(vm, "test", 3).Stack.Pop();
    Assert.Equal(6, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassByRefAndThenReturn()
  {
    string bhl = @"

    func float foo(ref float a)
    {
      a = a + 1
      return a
    }

    func float test(float k)
    {
      return foo(ref k)
    }
    ";

    var vm = MakeVM(bhl);
    double num = Execute(vm, "test", 3).Stack.Pop();
    Assert.Equal(4, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestRefsAllowedInFuncArgsOnly()
  {
    string bhl = @"

    func test()
    {
      ref float a
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "extraneous input 'ref'",
      new PlaceAssert(bhl, @"
      ref float a
------^"
      )
    );
  }

  [Fact]
  public void TestPassByRefDefaultArgsNotAllowed()
  {
    string bhl = @"

    func foo(ref float k = 10)
    {
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "'ref' is not allowed to have a default value",
      new PlaceAssert(bhl, @"
    func foo(ref float k = 10)
-----------------------^"
      )
    );
  }

  [Fact]
  public void TestPassByRefForDefaultArgsNotAllowed()
  {
    string bhl = @"
    func foo(ref float k = 1)
    {
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "default values for 'ref' argument not allowed",
      new PlaceAssert(bhl, @"
    func foo(ref float k = 1)
-------------^"
      )
    );
  }

  [Fact]
  public void TestPassByRefLiteralNotAllowed()
  {
    string bhl = @"

    func foo(ref int a)
    {
    }

    func test()
    {
      foo(ref 10)
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "expression is not passable by 'ref'",
      new PlaceAssert(bhl, @"
      foo(ref 10)
----------^"
      )
    );
  }

  [Fact]
  public void TestPassByRefClassField()
  {
    string bhl = @"

    class Bar
    {
      float a
    }

    func foo(ref float a)
    {
      a = a + 1
    }

    func float test()
    {
      Bar b = { a: 10}

      foo(ref b.a)
      return b.a
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(11, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassByRefArrayItem()
  {
    string bhl = @"

    func foo(ref float a)
    {
      a = a + 1
    }

    func float test()
    {
      []float fs = [1,10,20]

      foo(ref fs[1])
      return fs[0] + fs[1] + fs[2]
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(32, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassByRefArrayObj()
  {
    string bhl = @"

    class Bar
    {
      float f
    }

    func foo(ref float a)
    {
      a = a + 1
    }

    func float test()
    {
      []Bar bs = [{f:1},{f:10},{f:20}]

      foo(ref bs[1].f)
      return bs[0].f + bs[1].f + bs[2].f
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(32, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassByRefTmpClassField()
  {
    string bhl = @"

    class Bar
    {
      float a
    }

    func float foo(ref float a)
    {
      a = a + 1
      return a
    }

    func float test()
    {
      return foo(ref (new Bar).a)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(1, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassByRefClassFieldNested()
  {
    string bhl = @"

    class Wow
    {
      float c
    }

    class Bar
    {
      Wow w
    }

    func foo(ref float a)
    {
      a = a + 1
    }

    func float test()
    {
      Bar b = { w: { c : 4} }

      foo(ref b.w.c)
      return b.w.c
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
          .EmitThen(Opcodes.ArgRef, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.Add)
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.Return)
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
          .EmitThen(Opcodes.New, new int[] { TypeIdx(c, c.ns.T("Bar")) })
          .EmitThen(Opcodes.New, new int[] { TypeIdx(c, c.ns.T("Wow")) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 4) })
          .EmitThen(Opcodes.SetAttrInplace, new int[] { 0 })
          .EmitThen(Opcodes.SetAttrInplace, new int[] { 0 })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.RefAttr, new int[] { 0 })
          .EmitThen(Opcodes.RefAttr, new int[] { 0 })
          .EmitThen(Opcodes.CallLocal, new int[] { 0, 1 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetAttr, new int[] { 0 })
          .EmitThen(Opcodes.GetAttr, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(5, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncPtrByRef()
  {
    string bhl = @"
    func int _1()
    {
      return 1
    }

    func int _2()
    {
      return 2
    }

    func change(ref func int() p)
    {
      p = _2
    }

    func int test()
    {
      func int() ptr = _1
      change(ref ptr)
      return ptr()
    }
    ";

    var vm = MakeVM(bhl);
    double num = Execute(vm, "test").Stack.Pop();
    Assert.Equal(2, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassByRefClassFieldFuncPtr()
  {
    string bhl = @"

    class Bar
    {
      func int() p
    }

    func int _5()
    {
      return 5
    }

    func int _10()
    {
      return 10
    }

    func foo(ref func int() p)
    {
      p = _10
    }

    func int test()
    {
      Bar b = {p: _5}

      foo(ref b.p)
      return b.p()
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(10, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassByRefNativeClassFieldNotSupported()
  {
    string bhl = @"

    func foo(ref float a)
    {
      a = a + 1
    }

    func float test()
    {
      Color c = new Color

      foo(ref c.r)
      return c.r
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      "getting native class field by 'ref' not supported",
      new PlaceAssert(bhl, @"
      foo(ref c.r)
----------------^"
      )
    );
  }

  [Fact]
  public void TestFuncReplacesArrayValueByRef()
  {
    string bhl = @"

    func []float make()
    {
      []float fs = [42]
      return fs
    }

    func foo(ref []float a)
    {
      a = make()
    }

    func float test()
    {
      []float a
      foo(ref a)
      return a[0]
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(42, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncReplacesArrayValueByRef2()
  {
    string bhl = @"

    func foo(ref []float a)
    {
      a = [42]
    }

    func float test()
    {
      []float a = []
      foo(ref a)
      return a[0]
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(42, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncReplacesArrayValueByRef3()
  {
    string bhl = @"

    func foo(ref []float a)
    {
      a = [42]
      []float b = a
    }

    func float test()
    {
      []float a = []
      foo(ref a)
      return a[0]
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(42, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncReplacesFuncPtrByRef()
  {
    string bhl = @"

    func float bar()
    {
      return 42
    }

    func foo(ref func float() a)
    {
      a = bar
    }

    func float test()
    {
      func float() a
      foo(ref a)
      return a()
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(42, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncReplacesFuncPtrByRef2()
  {
    string bhl = @"

    func float bar()
    {
      return 42
    }

    func foo(ref func float() a)
    {
      a = bar
    }

    func float test()
    {
      func float() a = func float() { return 45 }
      foo(ref a)
      return a()
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(42, num);
    CommonChecks(vm);
  }

}
