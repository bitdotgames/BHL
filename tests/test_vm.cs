using System;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using bhl;
using Xunit;

public class TestVM : BHL_TestBase
{
  [Fact]
  public void TestEmptyFunc()
  {
    {
      string bhl = @"
      func test() {}
      ";

      var c = Compile(bhl);

      var expected =
          new ModuleCompiler()
            .UseCode()
            .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
            .EmitThen(Opcodes.Return)
        ;
      AssertEqual(c, expected);

      var vm = MakeVM(c);
      vm.Start("test");
      Assert.False(vm.Tick());
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func test(){}
      ";

      var c = Compile(bhl);

      var expected =
          new ModuleCompiler()
            .UseCode()
            .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
            .EmitThen(Opcodes.Return)
        ;
      AssertEqual(c, expected);

      var vm = MakeVM(c);
      vm.Start("test");
      Assert.False(vm.Tick());
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func test(){
      }
      ";

      var c = Compile(bhl);

      var expected =
          new ModuleCompiler()
            .UseCode()
            .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
            .EmitThen(Opcodes.Return)
        ;
      AssertEqual(c, expected);

      var vm = MakeVM(c);
      vm.Start("test");
      Assert.False(vm.Tick());
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func test() {
      }
      ";

      var c = Compile(bhl);

      var expected =
          new ModuleCompiler()
            .UseCode()
            .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
            .EmitThen(Opcodes.Return)
        ;
      AssertEqual(c, expected);

      var vm = MakeVM(c);
      vm.Start("test");
      Assert.False(vm.Tick());
      CommonChecks(vm);
    }
  }

  [Fact]
  public void TestReturnNumConstant()
  {
    string bhl = @"
    func int test()
    {
      return 123
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 123) })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Single(c.compiled.constants);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(123, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNumberPrecision()
  {
    DoTestReturnNum(bhlnum: "100", expected_num: 100);
    DoTestReturnNum(bhlnum: "2147483647", expected_num: 2147483647);
    DoTestReturnNum(bhlnum: "2147483648", expected_num: 2147483648);
    DoTestReturnNum(bhlnum: "100.5", expected_num: 100.5);
    DoTestReturnNum(bhlnum: "2147483648.1", expected_num: 2147483648.1);
  }

  void DoTestReturnNum(string bhlnum, double expected_num)
  {
    string bhl = @"

    func float test()
    {
      return " + bhlnum + @"
    }
    ";

    var c = Compile(bhl);
    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    var num = fb.Stack.Pop().num;

    Assert.Equal(num, expected_num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnTrueBoolConstant()
  {
    string bhl = @"
    func bool test()
    {
      return true
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Single(c.compiled.constants);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.True(fb.Stack.PopRelease().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnFalseBoolConstant()
  {
    string bhl = @"
    func bool test()
    {
      return false
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, false) })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Single(c.compiled.constants);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.True(!fb.Stack.PopRelease().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnTrueNegated()
  {
    string bhl = @"
    func bool test()
    {
      return !true
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
          .EmitThen(Opcodes.UnaryNot)
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Single(c.compiled.constants);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.True(!fb.Stack.PopRelease().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnFalseNegated()
  {
    string bhl = @"
    func bool test()
    {
      return !false
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, false) })
          .EmitThen(Opcodes.UnaryNot)
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Single(c.compiled.constants);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.True(fb.Stack.PopRelease().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnBinaryNot()
  {
    string bhl = @"
    func int test()
    {
      return ~0
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
          .EmitThen(Opcodes.UnaryBitNot)
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Single(c.compiled.constants);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(-1, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnStringConstant()
  {
    string bhl = @"
    func string test()
    {
      return ""Hello""
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "Hello") })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Single(c.compiled.constants);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal("Hello", fb.Stack.PopRelease().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFailureBeforeReturn()
  {
    string bhl = @"

    func float foo()
    {
      fail()
      return 100
    }

    func float test()
    {
      float val = foo()
      return val
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindFail(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var fb = vm.Start("test");
    vm.Tick();
    Assert.Equal(BHS.FAILURE, fb.status);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFailureInNativeFunction()
  {
    string bhl = @"

    func float test()
    {
      float val = foo()
      return val
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      {
        var fn = new FuncSymbolNative(new Origin(), "foo", ts.T("float"),
          (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            exec.status = BHS.FAILURE;
            return null;
          });
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    var fb = vm.Start("test");
    vm.Tick();
    Assert.Equal(BHS.FAILURE, fb.status);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSimpleIncrement()
  {
    string bhl = @"

    func float test()
    {
      float k = 1
      k++
      return k
    }
    ";

    var vm = MakeVM(bhl);
    double num = Execute(vm, "test").Stack.Pop();
    Assert.Equal(2, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSeveralReturns()
  {
    string bhl = @"

    func float test()
    {
      return 300
      float k = 1
      return 100
    }
    ";

    var vm = MakeVM(bhl);
    double num = Execute(vm, "test").Stack.Pop();
    Assert.Equal(300, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnDefaultVar()
  {
    string bhl = @"
    func float test()
    {
      float k
      return k
    }
    ";

    var c = Compile(bhl);

    var ts = new Types();

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/ })
          .EmitThen(Opcodes.DeclVar, new int[] { 0, TypeIdx(c, ts.T("float")) })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(0, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnVar()
  {
    string bhl = @"
    func float test()
    {
      float k = 42
      return k
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 42) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(42, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnVoid()
  {
    string bhl = @"

    func void test()
    {
      trace(""HERE"")
      return
      trace(""NOT HERE"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("HERE", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestMultiReturn()
  {
    string bhl = @"

    func float,string test()
    {
      return 300,""bar""
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "bar") })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 300) })
          .EmitThen(Opcodes.ReturnVal, new int[] { 2 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = Execute(vm, "test");
    double num = fb.Stack.Pop();
    var str = fb.Stack.Pop();
    Assert.Equal(300, num);
    Assert.Equal("bar", str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestMultiReturnVarsAssign()
  {
    string bhl = @"
    func float,float foo()
    {
      return 300,100
    }

    func float test()
    {
      float f1,float f2 = foo()
      return f1-f2
    }
    ";

    var vm = MakeVM(bhl);
    var fb = Execute(vm, "test");
    double num = fb.Stack.Pop();
    Assert.Equal(200, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestMultiReturnVarAssign2()
  {
    string bhl = @"
    func float,string foo()
    {
      return 100,""bar""
    }

    func float,string test()
    {
      string s
      float a,s = foo()
      return a,s
    }
    ";

    var vm = MakeVM(bhl);
    var fb = Execute(vm, "test");
    var num = fb.Stack.Pop().num;
    var str = fb.Stack.PopRelease().str;
    Assert.Equal(100, num);
    Assert.Equal("bar", str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnMultipleVarAssignObjectAttr()
  {
    string bhl = @"

    func float,string foo()
    {
      return 100,""bar""
    }

    func float,string test()
    {
      string s
      Color c = {}
      c.r,s = foo()
      return c.r,s
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var fb = Execute(vm, "test");
    Assert.Equal(100, fb.Stack.Pop().num);
    Assert.Equal("bar", fb.Stack.Pop().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnMultipleVarAssignArrItem()
  {
    string bhl = @"

    func float,string foo()
    {
      return 100,""bar""
    }

    func float,string test()
    {
      []string s = [""""]
      float r,s[0] = foo()
      return r,s[0]
    }
    ";

    var vm = MakeVM(bhl);
    var fb = Execute(vm, "test");
    Assert.Equal(100, fb.Stack.Pop().num);
    Assert.Equal("bar", fb.Stack.Pop().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnMultipleVarAssignArrItem2()
  {
    string bhl = @"
    func float,string foo()
    {
      return 100,""bar""
    }

    func float,string test()
    {
      string s
      []Color c = [{}]
      c[0].r,s = foo()
      return c[0].r,s
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var fb = Execute(vm, "test");
    Assert.Equal(100, fb.Stack.Pop().num);
    Assert.Equal("bar", fb.Stack.Pop().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestMultiReturnBug()
  {
    string bhl = @"
    func int,int doer() {
      return 1, 0
    }

    func test() {
      int a, int b = doer()
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.ReturnVal, new int[] { 2 })
          .EmitThen(Opcodes.Return)
          .EmitThen(Opcodes.InitFrame, new int[] { 2 + 1 /*args info*/ })
          .EmitThen(Opcodes.CallLocal, new int[] { 0, 0 })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.SetVar, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnMultipleVarAssignNoSuchSymbol()
  {
    string bhl = @"

    func float,string foo()
    {
      return 100,""bar""
    }

    func float,string test()
    {
      float a,s = foo()
      return a,s
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "symbol 's' not resolved",
      new PlaceAssert(bhl, @"
      float a,s = foo()
--------------^"
      )
    );
  }

  public class TestVariableDeclMissingAssignFromFuncCall : BHL_TestBase
  {
    [Fact]
    public void _1()
    {
      string bhl = @"
      func test()
      {
        int a Foo()
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "no viable alternative at input 'Foo'",
        new PlaceAssert(bhl, @"
        int a Foo()
--------------^"
        )
      );
    }

    [Fact]
    public void _2()
    {
      string bhl = @"
      func test()
      {
        int a,string b Foo()
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "no viable alternative at input 'Foo'",
        new PlaceAssert(bhl, @"
        int a,string b Foo()
-----------------------^"
        )
      );
    }
  }

  [Fact]
  public void TestReturnMultipleNotEnough()
  {
    string bhl = @"

    func float,string foo()
    {
      return 100,""bar""
    }

    func void test()
    {
      float s = foo()
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "multi return size doesn't match destination",
      new PlaceAssert(bhl, @"
      float s = foo()
--------------^"
      )
    );
  }

  [Fact]
  public void TestReturnMultipleTooMany()
  {
    string bhl = @"

    func float,string foo()
    {
      return 100,""bar""
    }

    func void test()
    {
      float s,string a,int f = foo()
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "multi return size doesn't match destination",
      new PlaceAssert(bhl, @"
      float s,string a,int f = foo()
-----------------------------^"
      )
    );
  }

  [Fact]
  public void TestReturnNotAllPathsReturnValue()
  {
    string bhl = @"
    coro func int test()
    {
      paral_all {
        {
          return 1
        }
        yield()
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "matching 'return' statement not found",
      new PlaceAssert(bhl, @"
    coro func int test()
--------------^"
      )
    );
  }

  [Fact]
  public void TestReturnNotFoundInLambda()
  {
    string bhl = @"
    func foo() { }

    func test()
    {
      func bool() { foo() }()
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "matching 'return' statement not found",
      new PlaceAssert(bhl, @"
      func bool() { foo() }()
-----------^"
      )
    );
  }

  [Fact]
  public void TestReturnMultipleInFuncBadCast()
  {
    string bhl = @"

    func float,string foo()
    {
      return ""bar"",100
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "incompatible types: 'float' and 'string'",
      new PlaceAssert(bhl, @"
      return ""bar"",100
-------------^"
      )
    );
  }

  [Fact]
  public void TestReturnMultipleBadCast()
  {
    string bhl = @"

    func float,string foo()
    {
      return 100,""bar""
    }

    func void test()
    {
      string a,float s = foo()
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "incompatible types: 'string' and 'float'",
      new PlaceAssert(bhl, @"
      string a,float s = foo()
-------------^"
      )
    );
  }

  [Fact]
  public void TestReturnMultipleFromBindings()
  {
    string bhl = @"

    func float,string test()
    {
      return func_mult()
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      {
        var fn = new FuncSymbolNative(new Origin(), "func_mult", ts.T("float", "string"),
          (VM vm, VM.ExecState exec, FuncArgsInfo arg_info) =>
          {
            exec.stack.Push("foo");
            exec.stack.Push(42);
            return null;
          }
        );
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    var fb = Execute(vm, "test");
    Assert.Equal(42, fb.Stack.Pop().num);
    Assert.Equal("foo", fb.Stack.Pop().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnMultiple3()
  {
    string bhl = @"

    func float,string,int test()
    {
      return 100,""foo"",3
    }
    ";

    var vm = MakeVM(bhl);
    var fb = Execute(vm, "test");
    Assert.Equal(100, fb.Stack.Pop().num);
    Assert.Equal("foo", fb.Stack.Pop().str);
    Assert.Equal(3, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnMultiple4FromBindings()
  {
    string bhl = @"

    func float,string,int,float test()
    {
      float a,string b,int c,float d = func_mult()
      return a,b,c,d
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      {
        var fn = new FuncSymbolNative(new Origin(), "func_mult", ts.T("float", "string", "int", "float"),
          (VM vm, VM.ExecState exec, FuncArgsInfo arg_info) =>
          {
            exec.stack.Push(42.5);
            exec.stack.Push(12);
            exec.stack.Push("foo");
            exec.stack.Push(104);
            return null;
          }
        );
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    var fb = Execute(vm, "test");
    Assert.Equal(104, fb.Stack.Pop().num);
    Assert.Equal("foo", fb.Stack.Pop().str);
    Assert.Equal(12, fb.Stack.Pop().num);
    Assert.Equal(42.5, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestVarValueIsUselessStatement()
  {
    string bhl = @"

    func int test()
    {
      float foo = 1
      foo
      return 2
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      @"unexpected expression",
      new PlaceAssert(bhl, @"
      foo
------^"
      )
    );
  }

  [Fact]
  public void TestBindFunctionWithDefaultArgs()
  {
    string bhl = @"

    func float test(int k)
    {
      return func_with_def(k)
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      {
        var fn = new FuncSymbolNative(new Origin(), "func_with_def", ts.T("float"), 1,
          (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            double b = args_info.IsDefaultArgUsed(0) ? 2 : exec.stack.Pop();
            double a = exec.stack.Pop();

            exec.stack.Push(a + b);

            return null;
          },
          new FuncArgSymbol("a", ts.T("float")),
          new FuncArgSymbol("b", ts.T("float"))
        );

        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    double res = Execute(vm, "test", 42).Stack.Pop();
    Assert.Equal(44, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestBindFunctionWithDefaultArgs2()
  {
    string bhl = @"

    func float foo(float a)
    {
      return a
    }

    func float test(int k)
    {
      return func_with_def(k, foo(k)+1)
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      {
        var fn = new FuncSymbolNative(new Origin(), "func_with_def", ts.T("float"), 1,
          (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            double b = args_info.IsDefaultArgUsed(0) ? 2 : exec.stack.Pop();
            double a = exec.stack.Pop();

            exec.stack.Push(a + b);

            return null;
          },
          new FuncArgSymbol("a", ts.T("float")),
          new FuncArgSymbol("b", ts.T("float"))
        );

        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = ExecuteOld(vm, "test", ValOld.NewNum(vm, 42)).Stack.Pop().num;
    Assert.Equal(42 + 43, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestBindFunctionWithDefaultArgs3()
  {
    string bhl = @"

    func float test()
    {
      return func_with_def()
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      {
        var fn = new FuncSymbolNative(new Origin(), "func_with_def", ts.T("float"), 1,
          (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            double a = args_info.IsDefaultArgUsed(0) ? 14 : exec.stack.Pop();

            exec.stack.Push(a);

            return null;
          },
          new FuncArgSymbol("a", ts.T("float"))
        );

        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(14, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestBindFunctionWithDefaultArgsOmittingSome()
  {
    string bhl = @"

    func float test(int k)
    {
      return func_with_def(b : k)
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      {
        var fn = new FuncSymbolNative(new Origin(), "func_with_def", ts.T("float"), 2,
          (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            double b = args_info.IsDefaultArgUsed(1) ? 2 : exec.stack.Pop();
            double a = args_info.IsDefaultArgUsed(0) ? 10 : exec.stack.Pop();

            exec.stack.Push(a + b);

            return null;
          },
          new FuncArgSymbol("a", Types.Int),
          new FuncArgSymbol("b", Types.Int)
        );

        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = ExecuteOld(vm, "test", ValOld.NewNum(vm, 42)).Stack.Pop().num;
    Assert.Equal(52, res);
    CommonChecks(vm);
  }


  [Fact]
  public void TestFuncPtrReturnNonConsumed()
  {
    string bhl = @"

    func int test()
    {
      func int() ptr = func int() {
        return 1
      }
      ptr()
      return 2
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(2, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncPtrUselessStatement()
  {
    string bhl = @"

    func test()
    {
      int a = 1
      suspend
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      @"unexpected expression",
      new PlaceAssert(bhl, @"
      suspend
------^"
      )
    );
  }

  [Fact]
  public void TestFuncPtrArrReturnNonConsumed()
  {
    string bhl = @"

    func int test()
    {
      func []int() ptr = func []int() {
        return [1,2]
      }
      ptr()
      return 2
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(2, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestAttributeUselessStatement()
  {
    string bhl = @"

    func int test()
    {
      Color c = new Color
      c.r
      return 2
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      @"unexpected expression",
      new PlaceAssert(bhl, @"
      c.r
------^"
      )
    );
  }

  [Fact]
  public void TestReturnNonConsumed()
  {
    string bhl = @"

    func float foo()
    {
      return 100
    }

    func int test()
    {
      foo()
      return 2
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(2, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnNonConsumedInParal()
  {
    string bhl = @"

    func float foo()
    {
      return 100
    }

    coro func int test()
    {
      paral {
        foo()
        yield suspend()
      }
      return 2
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(2, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnMultipleNonConsumed()
  {
    string bhl = @"

    func float,string foo()
    {
      return 100,""bar""
    }

    func int test()
    {
      foo()
      return 2
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(2, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestLogicalAnd()
  {
    string bhl = @"
    func bool test()
    {
      return false && true
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, false) })
          .EmitThen(Opcodes.JumpPeekZ, new int[] { 5 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
          .EmitThen(Opcodes.And)
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Equal(2, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.True(fb.Stack.PopRelease().bval == false);
    CommonChecks(vm);
  }

  [Fact]
  public void TestLogicalOr()
  {
    string bhl = @"
    func bool test()
    {
      return false || true
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, false) })
          .EmitThen(Opcodes.JumpPeekNZ, new int[] { 5 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
          .EmitThen(Opcodes.Or)
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Equal(2, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.True(fb.Stack.PopRelease().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestLogicalAndHasPriorityOverOr()
  {
    string bhl = @"
    bool a = true
    bool b = true
    bool c = false
    func bool test()
    {
      return a && b || b && c
    }
    ";

    var vm = MakeVM(bhl);
    Assert.True(Execute(vm, "test").result_old.PopRelease().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestBitAnd()
  {
    string bhl = @"
    func int test()
    {
      return 3 & 1
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.BitAnd)
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Equal(2, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(1, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestBitOr()
  {
    string bhl = @"
    func int test()
    {
      return 3 | 4
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 4) })
          .EmitThen(Opcodes.BitOr)
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Equal(2, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(7, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestBitShiftRight()
  {
    string bhl = @"
    func int test()
    {
      return 2 >> 1
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.BitShr)
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Equal(2, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(1, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestBitShiftLeft()
  {
    string bhl = @"
    func int test()
    {
      return 1 << 2
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
          .EmitThen(Opcodes.BitShl)
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Equal(2, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(4, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestMod()
  {
    string bhl = @"
    func int test()
    {
      return 3 % 2
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
          .EmitThen(Opcodes.Mod)
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Equal(2, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(1, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestModDouble()
  {
    string bhl = @"
    func float test()
    {
      return 2.7 % 2
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2.7) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
          .EmitThen(Opcodes.Mod)
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Equal(2, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(2.7 % 2, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestEmptyParenExpression()
  {
    string bhl = @"

    func test()
    {
      ().foo
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      @"no viable alternative",
      new PlaceAssert(bhl, @"
      ().foo
-------^"
      )
    );
  }

  [Fact]
  public void TestSimpleExpression()
  {
    string bhl = @"

    func float test(float k)
    {
      return ((k*100) + 100) / 400
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1, ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSemicolonStatementSeparator()
  {
    string bhl = @"

    func float test(float k)
    {
      int a = 100; ; int b = 100; int c = 400
      return ((k*a) + b) / c
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1, ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestDanglingBrackets()
  {
    string bhl = @"

    func test()
    {
      ()
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "no viable alternative at input",
      new PlaceAssert(bhl, @"
      ()
-------^"
      )
    );
  }

  [Fact]
  public void TestDanglingBrackets2()
  {
    string bhl = @"

    func foo()
    {
    }

    func test()
    {
      foo() ()
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "no func to call",
      new PlaceAssert(bhl, @"
      foo() ()
------------^"
      )
    );
  }

  [Fact]
  public void TestWriteReadVar()
  {
    string bhl = @"
    func int test()
    {
      int a = 123
      return a + a
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/ })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 123) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.Add)
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Single(c.compiled.constants);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(246, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestLocalVariables()
  {
    string bhl = @"

    func float foo(float k)
    {
      float b = 5
      return k + 5
    }

    func float test(float k)
    {
      float b = 10
      return k * foo(b)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(45, ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestLocalVarHiding()
  {
    string bhl = @"

    func float time()
    {
      return 42
    }

    func float bar(float time)
    {
      return time
    }

    func float test()
    {
      return bar(100)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(100, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestLocalVarConflictsWithFunc()
  {
    string bhl = @"

    func float time()
    {
      return 42
    }

    func float bar(float time)
    {
      if(time == 0)
      {
        return time
      }
      else
      {
        return time()
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "symbol is not a function",
      new PlaceAssert(bhl, @"
        return time()
---------------^"
      )
    );
  }

  [Fact]
  public void TestVarSelfDecl()
  {
    string bhl = @"

    func float test()
    {
      float k = k
      return k
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "symbol 'k' not resolved",
      new PlaceAssert(bhl, @"
      float k = k
----------------^"
      )
    );
  }

  [Fact]
  public void TestDeclVarIntWithoutValue()
  {
    string bhl = @"
    func int test()
    {
      int i
      return i
    }
    ";

    var c = Compile(bhl);

    var ts = new Types();

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/ })
          .EmitThen(Opcodes.DeclVar, new int[] { 0, TypeIdx(c, ts.T("int")) })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(0, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestDeclVarFloatWithoutValue()
  {
    string bhl = @"
    func float test()
    {
      float i
      return i
    }
    ";

    var c = Compile(bhl);

    var ts = new Types();

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/ })
          .EmitThen(Opcodes.DeclVar, new int[] { 0, TypeIdx(c, ts.T("float")) })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(0, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestDeclVarStringWithoutValue()
  {
    string bhl = @"
    func string test()
    {
      string i
      return i
    }
    ";

    var c = Compile(bhl);

    var ts = new Types();

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
          .EmitThen(Opcodes.DeclVar, new int[] { 0, TypeIdx(c, ts.T("string"))})
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    AssertEqual(fb.Stack.PopRelease().str, "");
    CommonChecks(vm);
  }

  [Fact]
  public void TestDeclVarBoolWithoutValue()
  {
    string bhl = @"
    func bool test()
    {
      bool i
      return i
    }
    ";

    var c = Compile(bhl);

    var ts = new Types();

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
          .EmitThen(Opcodes.DeclVar, new int[] { 0, TypeIdx(c, ts.T("bool")) })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.False(fb.Stack.PopRelease().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestAssignVars()
  {
    string bhl = @"
    func float test()
    {
      float k
      k = 42
      float r
      r = k
      return r
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(42, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUnaryNegVar()
  {
    string bhl = @"
    func int test()
    {
      int x = 1
      return -x
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.UnaryNeg)
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Single(c.compiled.constants);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(-1, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestAdd()
  {
    string bhl = @"
    func int test()
    {
      return 10 + 20
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
          .EmitThen(Opcodes.Add)
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Equal(2, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(30, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSubtract()
  {
    string bhl = @"
    func int test()
    {
      return 20 - 10
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.Sub)
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Equal(2, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(10, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestDivision()
  {
    string bhl = @"
    func int test()
    {
      return 20 / 10
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(2, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestMultiply()
  {
    string bhl = @"
    func int test()
    {
      return 10 * 20
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(200, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGT()
  {
    string bhl = @"

    func bool test(float k)
    {
      return k > 2
    }
    ";

    var vm = MakeVM(bhl);
    bool res = Execute(vm, "test", Val.NewNum(4)).Stack.Pop();
    Assert.True(res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNotGT()
  {
    string bhl = @"

    func bool test(float k)
    {
      return k > 5
    }
    ";

    var vm = MakeVM(bhl);
    bool res = Execute(vm, "test", Val.NewNum(4)).Stack.Pop();
    Assert.False(res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestLT()
  {
    string bhl = @"

    func bool test(float k)
    {
      return k < 20
    }
    ";

    var vm = MakeVM(bhl);
    double num = Execute(vm, "test", 4).Stack.Pop();
    Assert.Equal(1, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNotLT()
  {
    string bhl = @"

    func bool test(float k)
    {
      return k < 2
    }
    ";

    var vm = MakeVM(bhl);
    double num = Execute(vm, "test", 4).Stack.Pop();
    Assert.Equal(0, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGTE()
  {
    string bhl = @"

    func bool test(float k)
    {
      return k >= 2
    }
    ";

    var vm = MakeVM(bhl);
    double num = Execute(vm, "test", 4).Stack.Pop();
    Assert.Equal(1, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGTE2()
  {
    string bhl = @"

    func bool test(float k)
    {
      return k >= 2
    }
    ";

    var vm = MakeVM(bhl);
    double num = Execute(vm, "test", 2).Stack.Pop();
    Assert.Equal(1, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestLTE()
  {
    string bhl = @"

    func bool test(float k)
    {
      return k <= 21
    }
    ";

    var vm = MakeVM(bhl);
    double num = Execute(vm, "test", 20).Stack.Pop();
    Assert.Equal(1, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestLTE2()
  {
    string bhl = @"

    func bool test(float k)
    {
      return k <= 20
    }
    ";

    var vm = MakeVM(bhl);
    double num = Execute(vm, "test", 20).Stack.Pop();
    Assert.Equal(1, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestEqNumber()
  {
    string bhl = @"

    func bool test(float k)
    {
      return k == 2
    }
    ";

    var vm = MakeVM(bhl);
    var num = ExecuteOld(vm, "test", ValOld.NewNum(vm, 2)).Stack.Pop().num;
    Assert.Equal(1, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestEqString()
  {
    string bhl = @"

    func bool test(string k)
    {
      return k == ""b""
    }
    ";

    var vm = MakeVM(bhl);
    bool res = Execute(vm, "test", "b").Stack.Pop();
    Assert.True(res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNotEqNum()
  {
    string bhl = @"

    func bool test(float k)
    {
      return k == 2
    }
    ";

    var vm = MakeVM(bhl);
    bool res = Execute(vm, "test", 20.0).Stack.Pop();
    Assert.False(res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNotEqString()
  {
    string bhl = @"

    func bool test(string k)
    {
      return k != ""c""
    }
    ";

    var vm = MakeVM(bhl);
    bool res = Execute(vm, "test", "b").Stack.Pop();
    Assert.True(res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNotEqString2()
  {
    string bhl = @"

    func bool test(string k)
    {
      return k == ""c""
    }
    ";

    var vm = MakeVM(bhl);
    bool res = Execute(vm, "test", "b").Stack.Pop();
    Assert.False(res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestConstantWithBigIndex()
  {
    string bhl = @"
    func int test()
    {
      return 1+2+3+4+5+6+7+8+9+10+11+12+13+14+15+16+17+18+19+20
      +21+22+23+24+25+26+27+28+29+30+31+32+33+34+35+36+37+38+39
      +40+41+42+43+44+45+46+47+48+49+50+51+52+53+54+55+56+57+58
      +59+60+61+62+63+64+65+66+67+68+69+70+71+72+73+74+75+76+77
      +78+79+80+81+82+83+84+85+86+87+88+89+90+91+92+93+94+95+96
      +97+98+99+100+101+102+103+104+105+106+107+108+109+110+111
      +112+113+114+115+116+117+118+119+120+121+122+123+124+125
      +126+127+128+129+130+131+132+133+134+135+136+137+138+139
      +140+141+142+143+144+145+146+147+148+149+150+151+152+153
      +154+155+156+157+158+159+160+161+162+163+164+165+166+167
      +168+169+170+171+172+173+174+175+176+177+178+179+180+181
      +182+183+184+185+186+187+188+189+190+191+192+193+194+195
      +196+197+198+199+200+201+202+203+204+205+206+207+208+209
      +210+211+212+213+214+215+216+217+218+219+220+221+222+223
      +224+225+226+227+228+229+230+231+232+233+234+235+236+237
      +238+239+240+241+242+243+244+245+246+247+248+249+250+251
      +252+253+254+255+256+257+258+259+260
    }
    ";

    var c = Compile(bhl);

    Assert.Equal(260, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(33930, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestAddSameConstants()
  {
    string bhl = @"
    func int test()
    {
      return 10 + 10
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.Add)
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Single(c.compiled.constants);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(20, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPostOpAddAssign()
  {
    string bhl = @"
    func int test()
    {
      int k
      k += 10
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(10, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPostOpSubAssign()
  {
    string bhl = @"

    func int test()
    {
      int k
      k -= 10
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(-10, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPostOpMulAssign()
  {
    string bhl = @"

    func int test()
    {
      int k = 1
      k *= 10
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(10, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPostOpDivAssign()
  {
    string bhl = @"

    func int test()
    {
      int k = 10
      k /= 10
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(1, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPostOpAddAssignString()
  {
    string bhl = @"

    func string test()
    {
      string k = ""bar""
      k += ""foo""
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var str = Execute(vm, "test").result_old.PopRelease().str;
    AssertEqual(str, "barfoo");
    CommonChecks(vm);
  }

  [Fact]
  public void TestPostOpAddAssignStringNotCompatible()
  {
    string bhl = @"

    func test()
    {
      int k
      k += ""foo""
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "incompatible types: 'int' and 'string'",
      new PlaceAssert(bhl, @"
      k += ""foo""
-----------^"
      )
    );
  }

  [Fact]
  public void TestPostOpSubAssignStringNotAllowed()
  {
    string bhl = @"

    func test()
    {
      string k
      k -= ""foo""
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "is not numeric type",
      new PlaceAssert(bhl, @"
      k -= ""foo""
------^"
      )
    );
  }

  [Fact]
  public void TestPostOpMulAssignStringNotAllowed()
  {
    string bhl = @"

    func test()
    {
      string k
      k *= ""foo""
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "is not numeric type",
      new PlaceAssert(bhl, @"
      k *= ""foo""
------^"
      )
    );
  }

  [Fact]
  public void TestPostOpDivAssignStringNotAllowed()
  {
    string bhl = @"

    func test()
    {
      string k
      k /= ""foo""
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "is not numeric type",
      new PlaceAssert(bhl, @"
      k /= ""foo""
------^"
      )
    );
  }

  [Fact]
  public void TestPostOpAssignExpCompatibleTypes()
  {
    string bhl = @"

    func float test()
    {
      float k = 2.1
      int a = 1
      k += a
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(3.1, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParenthesisExpression()
  {
    string bhl = @"
    func int test()
    {
      return 10 * (20 + 30)
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 30) })
          .EmitThen(Opcodes.Add)
          .EmitThen(Opcodes.Mul)
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Equal(3, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(500, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestLocalScopeVarHiding()
  {
    string bhl = @"

    func test()
    {
      int i = 1
      {
        int i = 2
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "already defined symbol 'i'",
      new PlaceAssert(bhl, @"
        int i = 2
------------^"
      )
    );
  }

  [Fact]
  public void TestLocalScopeVarHidingInSubSubScope()
  {
    string bhl = @"

    func test()
    {
      int i = 1
      {
        {
          {
            int i = 2
          }
        }
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "already defined symbol 'i'",
      new PlaceAssert(bhl, @"
            int i = 2
----------------^"
      )
    );
  }

  [Fact]
  public void TestLocalScopeVarOverridesGlobalVar()
  {
    string bhl = @"

    int i = 10
    func int test()
    {
      int i = 1
      return i
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestLocalScopeForVars()
  {
    string bhl = @"

    func test()
    {
      {
        int i = 1
        i = i + 1
        trace((string)i)
      }
      {
        string i
        i = ""foo""
        trace((string)i)
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");

    var fn = (FuncSymbolScript)vm.ResolveNamedByPath("test");
    Assert.Equal(1 + 1 /*for now*/, fn.local_vars_num);

    Assert.Equal("2foo", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestLocalScopeForVarsInParal()
  {
    string bhl = @"

    coro func test()
    {
      paral_all {
        {
          int i = 1
          i = i + 1
          yield()
          trace((string)i)
        }
        {
          string i
          i = ""foo""
          yield()
          trace((string)i)
        }
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");

    Assert.Equal("2foo", log.ToString());

    var fn = (FuncSymbolScript)vm.ResolveNamedByPath("test");
    Assert.Equal(2, fn.local_vars_num);

    CommonChecks(vm);
  }

  [Fact]
  public void TestVarDeclMustBeInUpperScope()
  {
    string bhl = @"

    func test()
    {
      {
        int i = 1
        i = i + 1
      }
      {
        i = i + 2
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "symbol 'i' not resolved",
      new PlaceAssert(bhl, @"
        i = i + 2
--------^"
      )
    );
  }

  [Fact]
  public void TestVarDeclMustBeInUpperScope2()
  {
    string bhl = @"

    func test()
    {
      paral_all {
        {
          int i = 1
          {
            i = i + 1
          }
        }
        {
          if(i == 2) {
            suspend()
          }
        }
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "symbol 'i' not resolved",
      new PlaceAssert(bhl, @"
          if(i == 2) {
-------------^"
      )
    );
  }

  [Fact]
  public void TestSimpleFuncCall()
  {
    string bhl = @"
    func int bar()
    {
      return 123
    }
    func int test()
    {
      return bar()
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 123) })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
          .EmitThen(Opcodes.CallLocal, new int[] { 0, 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Single(c.compiled.constants);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(123, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNotAllowedAssignAFuncCall()
  {
    {
      string bhl = @"
      func int bar()
      {
        return 123
      }
      func test()
      {
        bar() = 1
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "invalid assignment",
        new PlaceAssert(bhl, @"
        bar() = 1
--------------^"
        )
      );
    }

    {
      string bhl = @"
      func test()
      {
        func int () { return 1 }() = 1
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "invalid assignment",
        new PlaceAssert(bhl, @"
        func int () { return 1 }() = 1
-----------------------------------^"
        )
      );
    }

    {
      string bhl = @"
      func test()
      {
        func int() f = func int () { return 1 }
        f() = 1
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "invalid assignment",
        new PlaceAssert(bhl, @"
        f() = 1
------------^"
        )
      );
    }
  }

  [Fact]
  public void TestSimpleSubFuncsReturn()
  {
    string bhl = @"
    func int wow() {
      return 1
    }

    func int foo() {
      return wow()
    }

    func int bar() {
      return foo()
    }

    func int test() {
      return bar()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncDeclOrderIsIrrelevant()
  {
    string bhl = @"
    func int bar() {
      return foo()
    }

    func int foo() {
      return wow()
    }

    func int test() {
      return bar()
    }

    func int wow() {
      return 1
    }

    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSimpleNativeFunc()
  {
    string bhl = @"
    func test()
    {
      trace(""foo"")
    }
    ";

    var log = new StringBuilder();
    FuncSymbolNative fn = null;
    var ts_fn = new Action<Types>((ts) => { fn = BindTrace(ts, log); });

    var c = Compile(bhl, ts_fn);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "foo") })
          .EmitThen(Opcodes.CallGlobNative, new int[] { c.ts.module.nfunc_index.IndexOf(fn), 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts_fn);
    vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal("foo", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestSimpleNativeFuncReturnValue()
  {
    string bhl = @"
    func int test()
    {
      return answer42()
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      var fn = new FuncSymbolNative(new Origin(), "answer42", Types.Int,
        (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          exec.stack.Push(42);
          return null;
        }
      );
      ts.ns.Define(fn);
    });

    var vm = MakeVM(bhl, ts_fn);
    double num = Execute(vm, "test").Stack.Pop();
    Assert.Equal(42, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSimpleNativeFuncWithSeveralArgs()
  {
    string bhl = @"
    func int test()
    {
      return answer(1, 2)
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      var fn = new FuncSymbolNative(new Origin(), "answer", Types.Int,
        (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          double b = exec.stack.Pop();
          double a = exec.stack.Pop();

          exec.stack.Push(b - a);
          return null;
        },
        new FuncArgSymbol("a", ts.T("int")),
        new FuncArgSymbol("b", ts.T("int"))
      );
      ts.ns.Define(fn);
    });

    var vm = MakeVM(bhl, ts_fn);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(1, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSimpleImportedNativeFunc()
  {
    string bhl = @"
    import ""std""

    func test()
    {
      int a
      std.GetType(a)
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 2 /*args info*/})
          .EmitThen(Opcodes.DeclVar, new int[] { 0, TypeIdx(c, Types.Int) })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.CallNative, new int[] { 1, 0, 1 })
          .EmitThen(Opcodes.Pop)
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeFuncBindConflict()
  {
    string bhl = @"
    func trace(string hey)
    {
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      "already defined symbol 'trace'",
      new PlaceAssert(bhl, @"
    func trace(string hey)
---------^"
      )
    );
  }

  [Fact]
  public void TestRecursion()
  {
    string bhl = @"

    func float mult(float k)
    {
      if(k == 0) {
        return 1
      }
      return 2 * mult(k-1)
    }

    func float test(float k)
    {
      return mult(k)
    }
    ";

    var vm = MakeVM(bhl);
    var num = ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).Stack.Pop().num;
    Assert.Equal(8, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncAlreadyDeclaredArg()
  {
    string bhl = @"
    func foo(int k, int k)
    {
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "already defined symbol 'k'"
    );
  }

  [Fact]
  public void TestFuncPtr()
  {
    string bhl = @"
    func int foo()
    {
      return 1
    }

    func int test()
    {
      func int() ptr = foo
      return ptr()
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(1, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncPtrWithArgs()
  {
    string bhl = @"
    func int foo(int a, int b)
    {
      return a - b
    }

    func int test()
    {
      func int(int, int) ptr = foo
      return ptr(42, 1)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(41, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeFuncPtrWithArgs()
  {
    string bhl = @"

    func test()
    {
      func(string) ptr = trace
      ptr(""Hey"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("Hey", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestIfCondition()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      if( 1 > 2 )
      {
        x1 = 10
      }

      return x1
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
          .EmitThen(Opcodes.GT)
          .EmitThen(Opcodes.JumpZ, new int[] { 6 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Equal(4, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(100, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestEmptyIfBody()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      if(1 > 2)
      {}

      return x1
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(100, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestIfElseCondition()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 0

      if( 2 > 1 )
      {
        x1 = 10
      }
      else
      {
        x1 = 20
      }

      return x1
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.GT)
          .EmitThen(Opcodes.JumpZ, new int[] { 9 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.Jump, new int[] { 6 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Equal(5, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(10, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestMultiIfElseCondition()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 0

      if(0 > 1)
      {
        x1 = 10
      }
      else if(-1 > 1)
      {
        x1 = 30
      }
      else if(3 > 1)
      {
        x1 = 20
      }
      else
      {
        x1 = 40
      }

      return x1
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.GT)
          .EmitThen(Opcodes.JumpZ, new int[] { 9 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.Jump, new int[] { 19 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.UnaryNeg)
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.GT)
          .EmitThen(Opcodes.JumpZ, new int[] { 9 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 30) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.Jump, new int[] { 18 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.GT)
          .EmitThen(Opcodes.JumpZ, new int[] { 9 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.Jump, new int[] { 6 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 40) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Equal(7, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(20, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestEmptyElseBody()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      if(1 > 2) {
        x1 = 200
      } else {}

      return x1
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(100, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestIfFalseComplexCondition()
  {
    string bhl = @"
    func test()
    {
      if(false || !true) {
        trace(""NEVER"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestIfElseIf()
  {
    string bhl = @"

    func test()
    {
      if(false) {
        trace(""NEVER"")
      } else if(false) {
        trace(""NEVER2"")
      } else if(true) {
        trace(""OK"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("OK", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestIfElseIfComplexCondition()
  {
    string bhl = @"

    func test()
    {
      if(false) {
        trace(""NEVER"")
      } else if(false) {
        trace(""NEVER2"")
      } else if(true && !false) {
        trace(""OK"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("OK", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestIfElse()
  {
    string bhl = @"

    func test()
    {
      if(false) {
        trace(""NEVER"")
      } else {
        trace(""ELSE"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("ELSE", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestIfElseIfElse()
  {
    string bhl = @"

    func test()
    {
      if(false) {
        trace(""NEVER"")
      }
      else if (false) {
        trace(""NEVER2"")
      } else {
        trace(""ELSE"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("ELSE", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestNonMatchingReturnAfterIf()
  {
    string bhl = @"

    func int test()
    {
      if(false) {
        return 10
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "matching 'return' statement not found"
    );
  }

  [Fact]
  public void TestNonMatchingReturnAfterElseIf()
  {
    string bhl = @"

    func int test()
    {
      if(false) {
        return 10
      } else if (true) {
        return 20
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "matching 'return' statement not found",
      new PlaceAssert(bhl, @"
    func int test()
---------^"
      )
    );
  }

  [Fact]
  public void TestNonMatchingReturnAfterElseIf2()
  {
    string bhl = @"

    func int test()
    {
      if(false) {
      } else if (true) {
        return 20
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "matching 'return' statement not found",
      new PlaceAssert(bhl, @"
    func int test()
---------^"
      )
    );
  }

  [Fact]
  public void TestNonMatchingVoidReturn()
  {
    string bhl = @"
    func VoidFunc() {
    }

    func bool test() {
      return VoidFunc()
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "incompatible types: 'bool' and 'void'",
      new PlaceAssert(bhl, @"
      return VoidFunc()
-------------^"
      )
    );
  }

  [Fact]
  public void TestMatchingReturnInElse()
  {
    string bhl = @"

    func int test()
    {
      if(false) {
        return 10
      } else if (false) {
        return 20
      } else {
        return 30
      }
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(30, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNonMatchingReturnInWhile()
  {
    {
      string bhl = @"

      func int test() {
        while(true) {
          return 10
        }
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "matching 'return' statement not found"
      );
    }

    {
      string bhl = @"

      func int test() {
        while(true) {
          return 10
        }
        return 2
      }
      ";

      var vm = MakeVM(bhl);
      Assert.Equal(10, Execute(vm, "test").Stack.Pop().num);
    }
  }

  [Fact]
  public void TestNonMatchingReturnInDoWhile()
  {
    {
      string bhl = @"

      func int test() {
       do {
          return 10
        } while(true)
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "matching 'return' statement not found"
      );
    }

    {
      string bhl = @"

      func int test() {
        do {
          return 10
        } while(true)
        return 2
      }
      ";

      var vm = MakeVM(bhl);
      Assert.Equal(10, Execute(vm, "test").Stack.Pop().num);
    }
  }

  [Fact]
  public void TestNonMatchingReturnInFor()
  {
    {
      string bhl = @"
      func int test() {
       for(int i=0;i<10;i++) {
          return 10
        }
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "matching 'return' statement not found"
      );
    }

    {
      string bhl = @"
      func int test() {
       for(int i=0;i<10;i++) {
          if(i == 1) {
            continue
          }
          return 10
        }
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "matching 'return' statement not found"
      );
    }

    {
      string bhl = @"
      func int test() {
       for(int i=0;i<10;i++) {
          return 10
        }
       return 1
      }
      ";

      var vm = MakeVM(bhl);
      Assert.Equal(10, Execute(vm, "test").Stack.Pop().num);
    }
  }

  [Fact]
  public void TestNonMatchingReturnInForeach()
  {
    {
      string bhl = @"
      func int test() {
       []int ints = [1, 2]
       foreach(var i in ints) {
          if(i == 1) {
            continue
          }
          return 10
        }
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "matching 'return' statement not found"
      );
    }

    {
      string bhl = @"
      func int test() {
       []int ints = [1, 2]
       foreach(var i in ints) {
          if(i == 1) {
            return 20
          }
          return 10
        }
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "matching 'return' statement not found"
      );
    }

    {
      string bhl = @"
      func int test() {
       []int ints = [1, 2]
       foreach(var i in ints) {
          if(i == 1) {
            return 20
          } else {
            return 20
          }
          return 10
        }
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "matching 'return' statement not found"
      );
    }

    {
      string bhl = @"
      func int test() {
       []int ints = [1, 2]
       foreach(var i in ints) {
          return 10
        }
       return 1
      }
      ";

      var vm = MakeVM(bhl);
      Assert.Equal(10, Execute(vm, "test").Stack.Pop().num);
    }
  }

  [Fact]
  public void TestIfWithMultipleReturns()
  {
    string bhl = @"

    func int test(int b)
    {
      if(b == 1) {
        return 2
      }

      return 3
    }
    ";

    var vm = MakeVM(bhl);
    {
      var num = ExecuteOld(vm, "test", ValOld.NewNum(vm, 1)).Stack.Pop().num;
      Assert.Equal(2, num);
    }
    {
      var num = ExecuteOld(vm, "test", ValOld.NewNum(vm, 10)).Stack.Pop().num;
      Assert.Equal(3, num);
    }
    CommonChecks(vm);
  }

  [Fact]
  public void TestWhileWithCondition()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      while( x1 >= 10 )
      {
        x1 = x1 - 10
      }

      return x1
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          //__while__//
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.GTE)
          .EmitThen(Opcodes.JumpZ, new int[] { 12 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.Sub)
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.Jump, new int[] { -22 })
          //__//
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Equal(2, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(0, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestEmptyWhileBody()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      while(false) {}

      return x1
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(100, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestWhileComplexCondition()
  {
    string bhl = @"

    func int test()
    {
      int i = 0
      while(i < 3 && true) {
        i = i + 1
      }
      return i
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(3, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestBreakInWhile()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      while( x1 >= 10 )
      {
        x1 = x1 - 10
        if(x1 < 80) {
          break
        }
      }

      return x1
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          //__while__//
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.GTE)
          .EmitThen(Opcodes.JumpZ, new int[] { 25 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.Sub)
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 80) })
          .EmitThen(Opcodes.LT)
          .EmitThen(Opcodes.JumpZ, new int[] { 3 })
          .EmitThen(Opcodes.Jump /*break*/, new int[] { 3 })
          .EmitThen(Opcodes.Jump, new int[] { -35 })
          //__//
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(70, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestContinueInWhile()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      while( x1 >= 10 )
      {
        x1 = x1 - 10
        continue
        x1 = x1 + 10
      }

      return x1
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(0, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestWhileFailure()
  {
    string bhl = @"

    func test()
    {
      int i = 0
      while(i < 3) {
        trace((string)i)
        i = i + 1
        if(i == 2) {
          fail()
        }
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindFail(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var fb = vm.Start("test");

    vm.Tick();
    Assert.Equal(BHS.FAILURE, fb.status);

    Assert.Equal("01", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestDoWhileWithCondition()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      do
      {
        x1 = x1 - 10
      }
      while( x1 >= 10 )

      return x1
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          //__while__//
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.Sub)
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.GTE)
          .EmitThen(Opcodes.JumpZ, new int[] { 3 })
          .EmitThen(Opcodes.Jump, new int[] { -22 })
          //__//
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Equal(2, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(0, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestDoWhileFeature()
  {
    string bhl = @"

    func int test()
    {
      int i = 0
      do {
        i = i + 1
      } while(false)
      return i
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestDoWhileComplexCondition()
  {
    string bhl = @"

    func int test()
    {
      int i = 0
      do {
        i = i + 1
      } while(i < 3 && true)
      return i
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(3, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParseErrorInDoWhileBlockWithReturn()
  {
    string bhl = @"

    func int test()
    {
      string i;
      do {
        return i
      } while(true)
    }
    ";
    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "incompatible types: 'int' and 'string'",
      new PlaceAssert(bhl, @"
        return i
---------------^"
      )
    );
  }

  [Fact]
  public void TestBreakInDoWhile()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      do
      {
        x1 = x1 - 10
        if(x1 < 80) {
          break
        }
      } while( x1 >= 10 )

      return x1
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          //__while__//
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.Sub)
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 80) })
          .EmitThen(Opcodes.LT)
          .EmitThen(Opcodes.JumpZ, new int[] { 3 })
          .EmitThen(Opcodes.Jump /*break*/, new int[] { 13 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.GTE)
          .EmitThen(Opcodes.JumpZ, new int[] { 3 })
          .EmitThen(Opcodes.Jump, new int[] { -35 })
          //__//
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(70, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestContinueInDoWhile()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 100

      do
      {
        x1 = x1 - 10
        continue
        x1 = x1 + 10
      } while( x1 >= 10 )

      return x1
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(0, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestVarInInfiniteLoop()
  {
    string bhl = @"

    coro func test()
    {
      while(true) {
        int foo = 1
        trace((string)foo + "";"")
        yield()
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    var fb = vm.Start("test");
    for(int i = 0; i < 5; ++i)
      vm.Tick();
    Assert.Equal(5, vm.vals_pool.MissCount);
    Assert.Equal(3, vm.vals_pool.IdleCount);

    var str = log.ToString();

    Assert.Equal("1;1;1;1;1;", str);

    for(int i = 0; i < 5; ++i)
      vm.Tick();
    Assert.Equal(5, vm.vals_pool.MissCount);
    Assert.Equal(3, vm.vals_pool.IdleCount);

    str = log.ToString();

    Assert.Equal("1;1;1;1;1;" + "1;1;1;1;1;", str);

    vm.Stop(fb);

    CommonChecks(vm);
  }

  [Fact]
  public void TestDoWhileFailure()
  {
    string bhl = @"

    func test()
    {
      int i = 0
      do {
        trace((string)i)
        i = i + 1
        if(i == 2) {
          fail()
        }
      } while(i < 3)
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindFail(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var fb = vm.Start("test");

    vm.Tick();
    Assert.Equal(BHS.FAILURE, fb.status);

    Assert.Equal("01", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestStackInInfiniteLoop()
  {
    string bhl = @"

    func int foo()
    {
      return 100
    }

    func hey(int a)
    {
    }

    coro func test()
    {
      while(true) {
        hey(foo())
        yield()
      }
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");

    for(int i = 0; i < 5; ++i)
      vm.Tick();
    Assert.Equal(0, fb.exec.stack.sp);
    Assert.Equal(4, vm.vals_pool.MissCount);
    Assert.Equal(3, vm.vals_pool.IdleCount);

    vm.Stop(fb);
    CommonChecks(vm);
  }

  [Fact]
  public void TestForLoop()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 10

      for( int i = 0; i < 3; i = i + 1 )
      {
        x1 = x1 - i
      }

      return x1
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 2 + 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          //__for__//
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
          .EmitThen(Opcodes.SetVar, new int[] { 1 })
          .EmitThen(Opcodes.GetVar, new int[] { 1 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
          .EmitThen(Opcodes.LT)
          .EmitThen(Opcodes.JumpZ, new int[] { 19 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 1 })
          .EmitThen(Opcodes.Sub)
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 1 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.Add)
          .EmitThen(Opcodes.SetVar, new int[] { 1 })
          .EmitThen(Opcodes.Jump, new int[] { -29 })
          //__//
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Equal(4, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(7, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSeveralForLoops()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 10

      for( int i = 0; i < 3; i = i + 1 )
      {
        x1 = x1 - i
      }

      for( int j = 1; j < 3; j = j + 1 )
      {
        x1 = x1 - j
      }


      return x1
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(4, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestForLoopLocalScope()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 10

      for( int i = 0; i < 3; i = i + 1 )
      {
        x1 = x1 - i
      }

      int i = 2

      return x1 - i
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(5, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSeveralForLoopsLocalScope()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 10

      for( int i = 0; i < 3; i = i + 1 )
      {
        x1 = x1 - i
      }

      for( int i = 1; i < 3; i = i + 1 )
      {
        x1 = x1 - i
      }


      return x1
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(4, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestForMultiExpression()
  {
    string bhl = @"

    func test()
    {
      for(int i = 0, int j = 1; i < 3; i = i + 1, j = j + 2) {
        trace((string)(i*j) + "";"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("0;3;10;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestForReverse()
  {
    string bhl = @"

    func test()
    {
      for(int i = 2; i >= 0; i = i - 1) {
        trace((string)i)
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("210", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestForUseExternalVar()
  {
    string bhl = @"
    func test()
    {
      int i
      for(i = 1; i < 3; i = i + 1) {
        trace((string)i)
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("12", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestForNested()
  {
    string bhl = @"

    func test()
    {
      for(int i = 0; i < 3; i = i + 1) {
        for(int j = 0; j < 2; j = j + 1) {
          trace((string)i + "","" + (string)j + "";"")
        }
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("0,0;0,1;1,0;1,1;2,0;2,1;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestForInParal()
  {
    string bhl = @"

    coro func test()
    {
      paral {
        for(int i = 0; i < 3; i = i + 1) {
          trace((string)i)
          yield()
        }
        yield suspend()
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    Assert.Equal("012", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestForBadPreSection()
  {
    string bhl = @"
    func test()
    {
      int i = 0
      for(i ; i < 3; i = i + 1) {
        trace((string)i)
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "mismatched input ';' expecting '='",
      new PlaceAssert(bhl, @"
      for(i ; i < 3; i = i + 1) {
------------^"
      )
    );
  }

  [Fact]
  public void TestForEmptyPreSection()
  {
    string bhl = @"

    func test()
    {
      int i = 0
      for(; i < 3; i = i + 1) {
        trace((string)i)
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("012", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestForEmptyPostSection()
  {
    string bhl = @"

    func test()
    {
      int i = 0
      for(; i < 3;) {
        trace((string)i)
        i = i + 1
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("012", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestForBadPostSection()
  {
    string bhl = @"
    func test()
    {
      for(int i = 0 ; i < 3; i) {
        trace((string)i)
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "mismatched input ')'",
      new PlaceAssert(bhl, @"
      for(int i = 0 ; i < 3; i) {
------------------------------^"
      )
    );
  }

  [Fact]
  public void TestForCondIsRequired()
  {
    string bhl = @"

    func test()
    {
      for(;;) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "mismatched input ';'",
      new PlaceAssert(bhl, @"
      for(;;) {
-----------^"
      )
    );
  }

  [Fact]
  public void TestForNonBoolCond()
  {
    string bhl = @"

    func int foo()
    {
      return 14
    }

    func test()
    {
      for(; foo() ;) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "incompatible types: 'bool' and 'int'",
      new PlaceAssert(bhl, @"
      for(; foo() ;) {
------------^"
      )
    );
  }

  [Fact]
  public void TestForeachLoop()
  {
    string bhl = @"
    func int test()
    {
      []int arr = new []int
      arr.Add(1)
      arr.Add(3)
      int accum = 0

      foreach(int a in arr) {
        accum = accum + a
      }

      return accum
    }
    ";

    var c = Compile(bhl);

    var ts = new Types();

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 3 + 2 /*hidden vars*/ + 1 /*cargs*/})
          .EmitThen(Opcodes.New, new int[] { TypeIdx(c, ts.TArr("int")) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.CallMethodNative, new int[] { ArrAddIdx, 1 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
          .EmitThen(Opcodes.CallMethodNative, new int[] { ArrAddIdx, 1 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
          .EmitThen(Opcodes.SetVar, new int[] { 1 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.SetVar, new int[] { 3 }) //assign tmp arr
          .EmitThen(Opcodes.DeclVar, new int[] { 4, TypeIdx(c, ts.T("int")) }) //declare counter
          .EmitThen(Opcodes.DeclVar, new int[] { 2, TypeIdx(c, ts.T("int"))  }) //declare iterator
          .EmitThen(Opcodes.GetVar, new int[] { 4 })
          .EmitThen(Opcodes.GetVar, new int[] { 3 })
          .EmitThen(Opcodes.GetAttr, new int[] { ArrCountIdx })
          .EmitThen(Opcodes.LT) //compare counter and tmp arr size
          .EmitThen(Opcodes.JumpZ, new int[] { 19 })
          //call arr idx method
          .EmitThen(Opcodes.GetVar, new int[] { 3 })
          .EmitThen(Opcodes.GetVar, new int[] { 4 })
          .EmitThen(Opcodes.ArrIdx)
          .EmitThen(Opcodes.SetVar, new int[] { 2 })
          .EmitThen(Opcodes.GetVar, new int[] { 1 }) //accum = accum + iterator var
          .EmitThen(Opcodes.GetVar, new int[] { 2 })
          .EmitThen(Opcodes.Add)
          .EmitThen(Opcodes.SetVar, new int[] { 1 })
          .EmitThen(Opcodes.Inc, new int[] { 4 }) //fast increment hidden counter
          .EmitThen(Opcodes.Jump, new int[] { -30 })
          .EmitThen(Opcodes.GetVar, new int[] { 1 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;

    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(4, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestForeachTraceItems()
  {
    string bhl = @"

    func test()
    {
      []int its = [1, 2, 3]
      foreach(int it in its) {
        trace((string)it)
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("123", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestForeachLocalScope()
  {
    string bhl = @"

    func test()
    {
      foreach(int it in [1, 2, 3]) {
        trace((string)it)
      }

      foreach(int it in [3, 2, 1]) {
        trace((string)it)
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("123321", log.ToString());
    CommonChecks(vm);
  }


  [Fact]
  public void TestForeachForNativeArrayBinding()
  {
    string bhl = @"

    func test()
    {
      []Color cs = [{r:1}, {r:2}, {r:3}]
      foreach(Color c in cs) {
        trace((string)c.r)
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
    Assert.Equal("123", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestForeachUseExternalIteratorVar()
  {
    string bhl = @"

    func test()
    {
      int it
      []int its = [1, 2, 3]
      foreach(it in its) {
        trace((string)it)
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("123", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestForeachWithInPlaceArr()
  {
    string bhl = @"

    func test()
    {
      foreach(int it in [1,2,3]) {
        trace((string)it)
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("123", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestForeachWithReturnedArr()
  {
    string bhl = @"

    func []int foo()
    {
      return [1,2,3]
    }

    func test()
    {
      foreach(int it in foo()) {
        trace((string)it)
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("123", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestForeachInParal()
  {
    string bhl = @"

    coro func test()
    {
      paral {
        foreach(int it in [1,2,3]) {
          trace((string)it)
          yield()
        }
        yield suspend()
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    Assert.Equal("123", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestSeveralForeachInParalAll()
  {
    string bhl = @"

    coro func test()
    {
      paral_all {
        foreach(int it in [1,2,3]) {
          trace((string)it)
          yield()
        }
        foreach(int it2 in [4,5,6]) {
          trace((string)it2)
          yield()
        }
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("142536", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestForeachBreak()
  {
    string bhl = @"

    func test()
    {
      foreach(int it in [1,2,3]) {
        if(it == 3) {
          break
        }
        trace((string)it)
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("12", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestForeachSeveral()
  {
    string bhl = @"

    func test()
    {
      []int its = [1,2,3]
      foreach(int it in its) {
        trace((string)it)
      }

      foreach(int it2 in its) {
        trace((string)it2)
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("123123", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestForeachNested()
  {
    string bhl = @"

    func test()
    {
      []int its = [1,2,3]
      foreach(int it in its) {
        foreach(int it2 in its) {
          trace((string)it + "","" + (string)it2 + "";"")
        }
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("1,1;1,2;1,3;2,1;2,2;2,3;3,1;3,2;3,3;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestForeachNested2()
  {
    string bhl = @"

    func test()
    {
      foreach(int it in [1,2,3]) {
        foreach(int it2 in [20,30]) {
          trace((string)it + "","" + (string)it2 + "";"")
        }
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("1,20;1,30;2,20;2,30;3,20;3,30;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestForeachIteratorVarBadType()
  {
    string bhl = @"

    func test()
    {
      foreach(string it in [1,2,3]) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "incompatible types: 'string' and 'int'",
      new PlaceAssert(bhl, @"
      foreach(string it in [1,2,3]) {
----------------------------^"
      )
    );
  }

  [Fact]
  public void TestForeachArrBadType()
  {
    string bhl = @"

    func float foo()
    {
      return 14
    }

    func test()
    {
      foreach(float it in foo()) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "incompatible types: '[]float' and 'float'",
      new PlaceAssert(bhl, @"
      foreach(float it in foo()) {
--------------------------^"
      )
    );
  }

  [Fact]
  public void TestForeachExternalIteratorVarBadType()
  {
    string bhl = @"

    func test()
    {
      string it
      foreach(it in [1,2,3]) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "incompatible types: 'string' and 'int'",
      new PlaceAssert(bhl, @"
      foreach(it in [1,2,3]) {
---------------------^"
      )
    );
  }

  [Fact]
  public void TestForeachRedeclareError()
  {
    string bhl = @"

    func test()
    {
      string it
      foreach(int it in [1,2,3]) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "already defined symbol 'it'",
      new PlaceAssert(bhl, @"
      foreach(int it in [1,2,3]) {
------------------^"
      )
    );
  }

  [Fact]
  public void TestForeachBadSyntax()
  {
    string bhl = @"

    func test()
    {
      foreach([1,2,3] as int t) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "no viable alternative",
      new PlaceAssert(bhl, @"
      foreach([1,2,3] as int t) {
---------------^"
      )
    );
  }

  [Fact]
  public void TestForeachBadSyntaxInNamespace()
  {
    string bhl = @"
    namespace foo {
    int a = 1
    func test()
    {
      foreach([1,2,3] as int t) {
      }
    }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "no viable alternative",
      new PlaceAssert(bhl, @"
      foreach([1,2,3] as int t) {
---------------^"
      )
    );
  }

  [Fact]
  public void TestBreakInForLoop()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 10

      for( int i = 1; i < 3; i = i + 1 )
      {
        x1 = x1 - i
        break
      }

      return x1
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 2 + 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          //__for__//
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.SetVar, new int[] { 1 })
          .EmitThen(Opcodes.GetVar, new int[] { 1 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
          .EmitThen(Opcodes.LT)
          .EmitThen(Opcodes.JumpZ, new int[] { 22 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 1 })
          .EmitThen(Opcodes.Sub)
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.Jump /*break*/, new int[] { 12 })
          .EmitThen(Opcodes.GetVar, new int[] { 1 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.Add)
          .EmitThen(Opcodes.SetVar, new int[] { 1 })
          .EmitThen(Opcodes.Jump, new int[] { -32 })
          //__//
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    Assert.Equal(3, c.compiled.constants.Length);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(9, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestBadBreak()
  {
    string bhl = @"

    func test()
    {
      break
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "not within loop construct",
      new PlaceAssert(bhl, @"
      break
------^"
      )
    );
  }

  [Fact]
  public void TestContinueInForLoop()
  {
    string bhl = @"
    func int test()
    {
      int x1 = 10

      for( int i = 1; i < 3; i = i + 1 )
      {
        continue
        x1 = x1 - i
      }

      return x1
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 2 + 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          //__for__//
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.SetVar, new int[] { 1 })
          .EmitThen(Opcodes.GetVar, new int[] { 1 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 3) })
          .EmitThen(Opcodes.LT)
          .EmitThen(Opcodes.JumpZ, new int[] { 22 })
          .EmitThen(Opcodes.Jump /*continue*/, new int[] { 7 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 1 })
          .EmitThen(Opcodes.Sub)
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 1 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.Add)
          .EmitThen(Opcodes.SetVar, new int[] { 1 })
          .EmitThen(Opcodes.Jump, new int[] { -32 })
          //__//
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(10, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestBreakInForeachLoop()
  {
    string bhl = @"
    func int test()
    {
      []int arr = new []int
      arr.Add(1)
      arr.Add(3)
      int accum = 0

      foreach(int a in arr) {
        if(a == 3) {
          break
        }
        accum = accum + a
      }

      return accum
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(1, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestInfiniteLoopBreak()
  {
    string bhl = @"
    coro func int test()
    {
      int i = 0
      while(true) {
        i = i + 1
        if(i == 3) {
          break
        }
        yield()
      }
      return i
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(3, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestBreakFromNestedSequences()
  {
    string bhl = @"
    func int test()
    {
      int i = 0
      while(true) {
        {
          //without defer{..} sequence block won't be created
          //for optimization purposes
          defer {
            i = 1
          }
          {
            defer {
              i = 2
            }
            i = 3
            break
          }
        }
      }
      return i
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestContinueInForeachLoop()
  {
    string bhl = @"
    func int test()
    {
      []int arr = new []int
      arr.Add(1)
      arr.Add(3)
      int accum = 0

      foreach(int a in arr) {
        if(a == 1) {
          continue
        }
        accum = accum + a
      }

      return accum
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(3, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestContinueFromNestedSequences()
  {
    string bhl = @"
    func int test()
    {
      int i = 0
      while(i == 0) {
        {
          //without defer{..} sequence block won't be created
          //for optimization purposes
          defer {
            i = 1
          }
          {
            defer {
              i = 2
            }
            i = 3
            continue
          }
        }
      }
      return i
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestBooleanInCondition()
  {
    string bhl = @"
    func int first()
    {
      return 1
    }
    func int second()
    {
      return 2
    }
    func int test()
    {
      if( true ) {
        return first()
      } else {
        return second()
      }
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, true) })
          .EmitThen(Opcodes.JumpZ, new int[] { 13 })
          .EmitThen(Opcodes.CallLocal, new int[] { 0, 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Jump, new int[] { 10 })
          .EmitThen(Opcodes.CallLocal, new int[] { 9, 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.True(fb.Stack.PopRelease().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncCallInCondition()
  {
    string bhl = @"
    func int first()
    {
      return 1
    }
    func int second()
    {
      return 2
    }
    func int test()
    {
      if( 1 > 0 )
      {
        return first()
      }
      else
      {
        return second()
      }
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
          .EmitThen(Opcodes.GT)
          .EmitThen(Opcodes.JumpZ, new int[] { 13 })
          .EmitThen(Opcodes.CallLocal, new int[] { 0, 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Jump, new int[] { 10 })
          .EmitThen(Opcodes.CallLocal, new int[] { 9, 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(1, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestMultiFuncCall()
  {
    string bhl = @"
    func int test2(int x)
    {
      return 98 + x
    }
    func int test1(int x1)
    {
      return x1
    }
    func int test()
    {
      return test2(1+1) - test1(5-30)
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(125, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStringArgFunctionCall()
  {
    string bhl = @"

    func string StringTest(string s)
    {
      return ""Hello"" + s
    }
    func string Test()
    {
      string s = "" world !""
      return StringTest(s)
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("Test");
    Assert.False(vm.Tick());
    Assert.Equal("Hello world !", fb.Stack.Pop().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestDefaultFuncOtherFunc()
  {
    string bhl = @"
      func int bar(int i) {
        return i
      }

      func int foo(int a, func int(int) cb = bar) {
        return a + cb(1)
      }

      func int test()
      {
        return foo(10)
      }
      ";

    var vm = MakeVM(bhl);
    Assert.Equal(10 + 1, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestDefaultFuncOtherFuncOrderIndependent()
  {
    string bhl = @"
    func int foo(int a, func int(int) cb = bar) {
      return a + cb(1)
    }

    func int bar(int i) {
      return i
    }

    func int test()
    {
      return foo(10)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(10 + 1, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestDefaultFuncLambdaFunc()
  {
    string bhl = @"
    func int foo(int a, func int(int) cb = func int (int i) { return i} ) {
      return a + cb(1)
    }

    func int test()
    {
      return foo(10)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(10 + 1, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtr()
  {
    string bhl = @"
    func bool foo(int a, string k)
    {
      trace(k)
      return a > 2
    }

    func bool test(int a)
    {
      func bool(int,string) ptr = foo
      return ptr(a, ""HEY"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Assert.True(ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).result_old.PopRelease().bval);
    Assert.Equal("HEY", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtrSeveralTimes()
  {
    string bhl = @"
    func bool foo(int a, string k)
    {
      trace(k)
      return a > 2
    }

    func bool test(int a)
    {
      func bool(int,string) ptr = foo
      return ptr(a, ""HEY"") && ptr(a-1, ""BAR"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Assert.False(ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).result_old.PopRelease().bval);
    Assert.Equal("HEYBAR", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtrSeveralTimes2()
  {
    string bhl = @"
    func int foo(int a)
    {
      func int(int) p =
        func int (int a) {
          return a * 2
        }

      return p(a)
    }

    func int test(int a)
    {
      return foo(a) + foo(a+1)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(14, ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtrSeveralTimes3()
  {
    string bhl = @"
    func int foo(int a)
    {
      func int(int) p =
        func int (int a) {
          return a
        }

      return p(a)
    }

    func int test(int a)
    {
      return foo(a) + foo(a)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(6, ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).Stack.Pop().num);
    Assert.Equal(8, ExecuteOld(vm, "test", ValOld.NewNum(vm, 4)).Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtrSeveralTimes4()
  {
    string bhl = @"
    func int foo(int a)
    {
      func int(int) p =
        func int (int a) {
          return a
        }

      int tmp = p(a)

      p = func int (int a) {
          return a * 2
      }

      return tmp + p(a)
    }

    func int test(int a)
    {
      return foo(a) + foo(a+1)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(9 + 12, ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).Stack.Pop().num);
    Assert.Equal(12 + 15, ExecuteOld(vm, "test", ValOld.NewNum(vm, 4)).Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtrSeveralTimes5()
  {
    string bhl = @"
    func int foo(func int(int) p, int a)
    {
      return p(a)
    }

    func int test(int a)
    {
      return foo(func int(int a) { return a }, a) +
             foo(func int(int a) { return a * 2 }, a + 1)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(3 + 8, ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).Stack.Pop().num);
    Assert.Equal(4 + 10, ExecuteOld(vm, "test", ValOld.NewNum(vm, 4)).Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestArrayOfComplexFuncPtrs()
  {
    string bhl = @"
    func int test(int a)
    {
      []func int(int,string) ptrs = new []func int(int,string)
      ptrs.Add(func int(int a, string b) {
          trace(b)
          return a*2
      })
      ptrs.Add(func int(int a, string b) {
          trace(b)
          return a*10
      })

      return ptrs[0](a, ""what"") + ptrs[1](a, ""hey"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Assert.Equal(3 * 2 + 3 * 10, ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).Stack.Pop().num);
    Assert.Equal("whathey", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnComplexFuncPtr()
  {
    string bhl = @"
    func func bool(int) foo()
    {
      return func bool(int a) { return a > 2 }
    }

    func bool test(int a)
    {
      func bool(int) ptr = foo()
      return ptr(a)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.True(ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).result_old.PopRelease().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnAndCallComplexFuncPtr()
  {
    string bhl = @"
    func func bool(int) foo()
    {
      return func bool(int a) { return a > 2 }
    }

    func bool test(int a)
    {
      return foo()(a)
    }
    ";

    var vm = MakeVM(bhl);
    Assert.True(ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).result_old.PopRelease().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtrPassRef()
  {
    string bhl = @"
    func void foo(int a, string k, ref bool res)
    {
      trace(k)
      res = a > 2
    }

    func bool test(int a)
    {
      func(int,string, ref bool) ptr = foo
      bool res = false
      ptr(a, ""HEY"", ref res)
      return res
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Assert.True(ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).result_old.PopRelease().bval);
    Assert.Equal("HEY", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtrLambda()
  {
    string bhl = @"
    func bool test(int a)
    {
      func bool(int,string) ptr =
        func bool (int a, string k)
        {
          trace(k)
          return a > 2
        }
      return ptr(a, ""HEY"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Assert.True(ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).result_old.PopRelease().bval);
    Assert.Equal("HEY", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtrLambdaInALoop()
  {
    string bhl = @"
    func test()
    {
      func(int) ptr =
        func (int a)
        {
          trace((string)a)
        }
      int i = 0
      while(i < 5)
      {
        ptr(i)
        i = i + 1
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    ExecuteOld(vm, "test", ValOld.NewNum(vm, 3));
    Assert.Equal("01234", log.ToString());
    Assert.Equal(2, vm.frames_pool.MissCount);
    CommonChecks(vm);
  }

  [Fact]
  public void TestComplexFuncPtrIncompatibleRetType()
  {
    string bhl = @"
    func void foo(int a) { }

    func void test()
    {
      func bool(int) ptr = foo
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "incompatible types: 'func bool(int)' and 'func void(int)'",
      new PlaceAssert(bhl, @"
      func bool(int) ptr = foo
---------------------^"
      )
    );
  }

  [Fact]
  public void TestComplexFuncPtrArgTypeCheck()
  {
    string bhl = @"
    func foo(int a) { }

    func test()
    {
      func(float) ptr = foo
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "incompatible types: 'func void(float)' and 'func void(int)'",
      new PlaceAssert(bhl, @"
      func(float) ptr = foo
------------------^"
      )
    );
  }

  [Fact]
  public void TestComplexFuncPtrCallArgTypeCheck()
  {
    string bhl = @"
    func foo(int a) { }

    func test()
    {
      func(int) ptr = foo
      ptr(""hey"")
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "incompatible types: 'int' and 'string'",
      new PlaceAssert(bhl, @"
      ptr(""hey"")
----------^"
      )
    );
  }

  [Fact]
  public void TestComplexFuncPtrCallArgRefTypeCheck()
  {
    string bhl = @"
    func foo(int a, ref  float b) { }

    func test()
    {
      float b = 1
      func(int, ref float) ptr = foo
      ptr(10, b)
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "'ref' is missing",
      new PlaceAssert(bhl, @"
      ptr(10, b)
--------------^"
      )
    );
  }

  [Fact]
  public void TestComplexFuncPtrCallArgRefTypeCheck2()
  {
    string bhl = @"
    func foo(int a, ref float b) { }

    func test()
    {
      float b = 1
      func(int, float) ptr = foo
      ptr(10, ref b)
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "incompatible types: 'func void(int,float)' and 'func void(int,ref float)'",
      new PlaceAssert(bhl, @"
      func(int, float) ptr = foo
-----------------------^"
      )
    );
  }

  [Fact]
  public void TestComplexFuncPtrPassConflictingRef()
  {
    string bhl = @"
    func void foo(int a, string k, refbool res)
    {
      trace(k)
    }

    func bool test(int a)
    {
      func(int,string,ref bool) ptr = foo
      bool res = false
      ptr(a, ""HEY"", ref res)
      return res
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      {
        var cl = new ClassSymbolNative(new Origin(), "refbool",
          delegate(VM vm, ref Val v, IType type) { }
        );
        ts.ns.Define(cl);
      }

      BindTrace(ts, log);
    });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      "incompatible types: 'func void(int,string,ref bool)' and 'func void(int,string,refbool)'",
      new PlaceAssert(bhl, @"
      func(int,string,ref bool) ptr = foo
--------------------------------^"
      )
    );
  }

  [Fact]
  public void TestComplexFuncPtrCallArgNotEnoughArgsCheck()
  {
    string bhl = @"
    func foo(int a) { }

    func test()
    {
      func(int) ptr = foo
      ptr()
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "missing argument of type 'int'",
      new PlaceAssert(bhl, @"
      ptr()
---------^"
      )
    );
  }

  [Fact]
  public void TestComplexFuncPtrCallArgNotEnoughArgsCheck2()
  {
    string bhl = @"
    func foo(int a, float b) { }

    func test()
    {
      func(int, float) ptr = foo
      ptr(10)
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "missing argument of type 'float'",
      new PlaceAssert(bhl, @"
      ptr(10)
---------^"
      )
    );
  }

  [Fact]
  public void TestComplexFuncPtrCallArgTooManyArgsCheck()
  {
    string bhl = @"
    func foo(int a) { }

    func test()
    {
      func(int) ptr = foo
      ptr(10, 30)
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "too many arguments",
      new PlaceAssert(bhl, @"
      ptr(10, 30)
---------^"
      )
    );
  }

  [Fact]
  public void TestFuncPtrWithDeclPassedAsArg()
  {
    string bhl = @"

    func foo(func() fn)
    {
      fn()
    }

    func hey()
    {
      float foo
      trace(""HERE"")
    }

    func test()
    {
      foo(hey)
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    ExecuteOld(vm, "test", ValOld.NewNum(vm, 3));
    Assert.Equal("HERE", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncPtrForNativeFunc()
  {
    string bhl = @"
    func test()
    {
      func() ptr = foo
      ptr()
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      {
        var fn = new FuncSymbolNative(new Origin(), "foo", Types.Void,
          (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            log.Append("FOO");
            return null;
          }
        );
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("FOO", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncDefaultArg()
  {
    string bhl = @"

    func int foo(int k0, int k1 = 1 - 0, int k2 = 10)
    {
      return k0+k1+k2
    }

    func int test()
    {
      return foo(1 + 1, 0)
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseCode()
          //foo
          .EmitThen(Opcodes.InitFrame, new int[] { 3 + 1 /*args info*/})
          .EmitThen(Opcodes.ArgVar, new int[] { 0 })
          .EmitThen(Opcodes.DefArg, new int[] { 0, 9 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
          .EmitThen(Opcodes.Sub)
          .EmitThen(Opcodes.ArgVar, new int[] { 1 })
          .EmitThen(Opcodes.DefArg, new int[] { 1, 4 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.ArgVar, new int[] { 2 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 1 })
          .EmitThen(Opcodes.Add)
          .EmitThen(Opcodes.GetVar, new int[] { 2 })
          .EmitThen(Opcodes.Add)
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
          //test
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.Add)
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
          .EmitThen(Opcodes.CallLocal, new int[] { 0, 130 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(12, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncManyDefaultArgs()
  {
    string bhl = @"

    func float foo(
          float k1 = 1, float k2 = 1, float k3 = 1, float k4 = 1, float k5 = 1, float k6 = 1, float k7 = 1, float k8 = 1, float k9 = 1,
          float k10 = 1, float k11 = 1, float k12 = 1, float k13 = 1, float k14 = 1, float k15 = 1, float k16 = 1, float k17 = 1, float k18 = 1,
          float k19 = 1, float k20 = 1, float k21 = 1, float k22 = 1, float k23 = 1, float k24 = 1, float k25 = 1, float k26 = 42
        )
    {
      return k26
    }

    func float test()
    {
      return foo()
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(42, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncTooManyDefaultArgs()
  {
    string bhl = @"

    func float foo(
          float k1 = 1, float k2 = 1, float k3 = 1, float k4 = 1, float k5 = 1, float k6 = 1, float k7 = 1, float k8 = 1, float k9 = 1,
          float k10 = 1, float k11 = 1, float k12 = 1, float k13 = 1, float k14 = 1, float k15 = 1, float k16 = 1, float k17 = 1, float k18 = 1,
          float k19 = 1, float k20 = 1, float k21 = 1, float k22 = 1, float k23 = 1, float k24 = 1, float k25 = 1, float k26 = 1, float k27 = 1
        )
    {
      return k27
    }

    func float test()
    {
      return foo()
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "max default arguments reached",
      new PlaceAssert(bhl, @"
      return foo()
----------------^"
      )
    );
  }

  [Fact]
  public void TestFuncNotEnoughArgs()
  {
    string bhl = @"

    func bool foo(bool k)
    {
      return k
    }

    func bool test()
    {
      return foo()
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "missing argument 'k'",
      new PlaceAssert(bhl, @"
      return foo()
----------------^"
      )
    );
  }

  [Fact]
  public void TestFuncPassingExtraNamedArgs()
  {
    string bhl = @"

    func int foo(int k)
    {
      return k
    }

    func int test()
    {
      return foo(k: 1, k: 2)
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "argument already passed before",
      new PlaceAssert(bhl, @"
      return foo(k: 1, k: 2)
-----------------------^"
      )
    );
  }

  [Fact]
  public void TestFuncNotEnoughArgsWithDefaultArgs()
  {
    string bhl = @"

    func bool foo(float radius, bool k = true)
    {
      return k
    }

    func bool test()
    {
      return foo()
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "missing argument 'radius'",
      new PlaceAssert(bhl, @"
      return foo()
----------------^"
      )
    );
  }

  [Fact]
  public void TestFuncDefaultArgIgnored()
  {
    string bhl = @"

    func float foo(float k = 42)
    {
      return k
    }

    func float test()
    {
      return foo(24)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(24, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncDefaultArgMixed()
  {
    string bhl = @"

    func float foo(float b, float k = 42)
    {
      return b + k
    }

    func float test()
    {
      return foo(24)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(66, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncDefaultArgIsFunc()
  {
    string bhl = @"

    func float bar(float m)
    {
      return m
    }

    func float foo(float b, float k = bar(1))
    {
      return b + k
    }

    func float test()
    {
      return foo(24)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(25, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncDefaultArgIsFunc2()
  {
    string bhl = @"

    func float bar(float m)
    {
      return m
    }

    func float foo(float b = 1, float k = bar(1))
    {
      return b + k
    }

    func float test()
    {
      return foo(26, bar(2))
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(28, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncMissingReturnArgument()
  {
    string bhl = @"
    func int test()
    {
      return
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "return value is missing",
      new PlaceAssert(bhl, @"
      return
------^"
      )
    );
  }

  [Fact]
  public void TestFuncMissingDefaultArgument()
  {
    string bhl = @"

    func float foo(float b = 23, float k)
    {
      return b + k
    }

    func float test()
    {
      return foo(24)
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "missing default argument expression",
      new PlaceAssert(bhl, @"
    func float foo(float b = 23, float k)
---------------------------------------^"
      )
    );
  }

  [Fact]
  public void TestFuncExtraArgumentMatchesLocalVariable()
  {
    string bhl = @"

    func foo(float b, float k)
    {
      float f = 3
      float a = b + k
    }

    func test()
    {
      foo(b: 24, k: 3, f : 1)
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "no such named argument",
      new PlaceAssert(bhl, @"
      foo(b: 24, k: 3, f : 1)
-----------------------^"
      )
    );
  }

  [Fact]
  public void TestFuncSeveralDefaultArgsMixed()
  {
    string bhl = @"

    func float foo(float b = 100, float k = 1000)
    {
      return k - b
    }

    func float test()
    {
      return foo(k : 2, b : 5)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(-3, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncArgPassedByValue()
  {
    string bhl = @"

    func foo(float a)
    {
      a = a + 1
    }

    func float test()
    {
      float k = 1
      foo(k)
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(1, num);
    CommonChecks(vm);
  }

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
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.Add)
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.Return)
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
          .EmitThen(Opcodes.ArgVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.CallLocal, new int[] { 0, 1 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var num = ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).Stack.Pop().num;
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
    var num = ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).Stack.Pop().num;
    Assert.Equal(3, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassByRefNested()
  {
    string bhl = @"

    func bar(ref float b)
    {
      b = b * 2
    }

    func foo(ref float a)
    {
      a = a + 1
      bar(ref a)
    }

    func float test(float k)
    {
      foo(ref k)
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).Stack.Pop().num;
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
    var num = ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).Stack.Pop().num;
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
            throw new NotImplementedException();

            //ref var b = ref stack.Pop();
            //double a = stack.Pop();

            //b._num = a * 2;
            //b.Release();
            return null;
          },
          new FuncArgSymbol("a", ts.T("float")),
          new FuncArgSymbol("b", ts.T("float"), true /*is ref*/)
        );

        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    var num = ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).Stack.Pop().num;
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
    var num = ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).Stack.Pop().num;
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
    var num = ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).Stack.Pop().num;
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
  public void TestPassByRefNullValue()
  {
    string bhl = @"

    func float foo(ref float k = null)
    {
      if((any)k != null) {
        k = k + 1
        return k
      } else {
        return 1
      }
    }

    func float,float test()
    {
      float res = 0
      float k = 10
      res = res + foo()
      res = res + foo(ref k)
      return res,k
    }
    ";

    var vm = MakeVM(bhl);
    var fb = ExecuteOld(vm, "test", ValOld.NewNum(vm, 3));
    var num1 = fb.Stack.Pop().num;
    var num2 = fb.Stack.Pop().num;
    Assert.Equal(12, num1);
    Assert.Equal(11, num2);
    CommonChecks(vm);
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
    var num = Execute(vm, "test").Stack.Pop().num;
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
  public void TestPassArrayToFunctionByValue()
  {
    string bhl = @"

    func foo([]int a)
    {
      a = [100]
    }

    func int test()
    {
      []int a = [1, 2]

      foo(a)

      return a[0]
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(1, num);
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

  [Fact]
  public void TestFuncSeveralDefaultArgsOmittingSome()
  {
    string bhl = @"

    func float foo(float b = 100, float k = 1000)
    {
      return k - b
    }

    func float test()
    {
      return foo(k : 2)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(-98, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStartFiberByFuncAddr()
  {
    string bhl = @"
    func int test()
    {
      return 123
    }
    ";

    var vm = MakeVM(bhl);
    Assert.False(vm.TryFindFuncAddr("garbage", out var _));
    Assert.True(vm.TryFindFuncAddr("test", out var addr));
    {
      var fb = vm.Start(addr);
      Assert.False(vm.Tick());
      Assert.Equal(123, fb.Stack.Pop().num);
    }
    {
      var fb = vm.Start(addr);
      Assert.False(vm.Tick());
      Assert.Equal(123, fb.Stack.Pop().num);
    }
    CommonChecks(vm);
  }

  [Fact]
  public void TestFindFuncAddr()
  {
    string bhl = @"
    func test()
    {
    }
    ";

    var vm = MakeVM(bhl);
    {
      Assert.True(vm.TryFindFuncAddr("test", out var addr));
      Assert.Equal("test", addr.symbol.name);
    }
    {
      Assert.True(vm.TryFindFuncAddr("start", out var addr));
      Assert.Equal("start", addr.symbol.name);
    }

    CommonChecks(vm);
  }

  [Fact]
  public void TestDetachFiber()
  {
    string bhl = @"
    coro func int test()
    {
      yield()
      return 123
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.Equal(BHS.NONE, fb.status);
    vm.Detach(fb);
    Assert.Equal(BHS.NONE, fb.status);
    Assert.False(vm.Tick());
    Assert.Equal(BHS.NONE, fb.status);
    Assert.True(vm.Tick(fb));
    Assert.False(vm.Tick(fb));
    Assert.Equal(123, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestDetachAndTickSeveralFibers()
  {
    string bhl = @"
    coro func test()
    {
      yield()
    }

    coro func test2()
    {
      yield()
      yield()
    }
    ";

    var vm = MakeVM(bhl);
    var fbs = new List<VM.Fiber>();
    fbs.Add(vm.Start("test"));
    fbs.Add(vm.Start("test2"));
    vm.Detach(fbs[0]);
    vm.Detach(fbs[1]);

    Assert.Equal(2, fbs.Count);

    Assert.True(vm.Tick(fbs));
    Assert.Equal(2, fbs.Count);

    Assert.True(vm.Tick(fbs));
    Assert.Single(fbs);

    Assert.False(vm.Tick(fbs));
    Assert.Empty(fbs);

    CommonChecks(vm);
  }

  [Fact]
  public void TestFiberFuncDefaultArg()
  {
    string bhl = @"

    func float test(float k = 42)
    {
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var args_inf = new FuncArgsInfo();
    args_inf.UseDefaultArg(0, true);
    var num = Execute(vm, "test", args_inf).Stack.Pop().num;
    Assert.Equal(42, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassNamedValue()
  {
    string bhl = @"

    func float foo(float a)
    {
      return a
    }

    func float test(float k)
    {
      return foo(a : k)
    }
    ";

    var vm = MakeVM(bhl);
    var num = ExecuteOld(vm, "test", ValOld.NewNum(vm, 3)).Stack.Pop().num;
    Assert.Equal(3, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassSeveralNamedValues()
  {
    string bhl = @"

    func float foo(float a, float b)
    {
      return b - a
    }

    func float test()
    {
      return foo(b : 5, a : 3)
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(2, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFuncDefaultNamedArg()
  {
    string bhl = @"

    func int foo(int k0, int k1 = 1 - 0, int k2 = 10)
    {
      return k0+k1+k2
    }

    func int test()
    {
      return foo(1 + 1, k2: 2)
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(5, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStatefulNativeFunc()
  {
    string bhl = @"
    coro func test()
    {
      yield WaitTicks(2)
    }
    ";

    FuncSymbolNative fn = null;
    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { fn = BindWaitTicks(ts, log); });

    var c = Compile(bhl, ts_fn);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 2) })
          .EmitThen(Opcodes.CallGlobNative, new int[] { c.ts.module.nfunc_index.IndexOf(fn), 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts_fn);
    vm.Start("test");
    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    CommonChecks(vm);
  }

  [Fact]
  public void TestSeqSuccess()
  {
    string bhl = @"

    func foo()
    {
      trace(""FOO"")
    }

    func bar()
    {
      trace(""BAR"")
    }

    func test()
    {
      {
        bar()
        foo()
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("BARFOO", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestSeqFailure()
  {
    string bhl = @"

    func foo()
    {
      trace(""FOO"")
      fail()
    }

    func bar()
    {
      trace(""BAR"")
    }

    func test()
    {
      {
        bar()
        foo()
        bar()
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindFail(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("BARFOO", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestSeqReturn()
  {
    string bhl = @"

    func int test()
    {
      int a = 1
      {
        defer { } //this will make sure seq is created
        a = 2
        return a
      }
      return a
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(2, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNestedSeqReturn()
  {
    string bhl = @"

    func int foo() {
      {
        defer { } //this will make sure seq is created
        {
          defer { } //this will make sure seq is created
          return 2
        }
      }
      return 0
    }

    func int test() {
      {
        defer { } //this will make sure seq is created
        {
          defer { } //this will make sure seq is created
          return foo()
        }
      }
      return 0
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(2, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCleanFuncArgsOnStackUserBindInBinOp()
  {
    string bhl = @"
    func test()
    {
      bool res = foo(false) != bar_fail(4)
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      {
        var fn = new FuncSymbolNative(new Origin(), "foo", Types.Int,
          (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            exec.stack.Pop();
            exec.stack.Push(42);
            return null;
          },
          new FuncArgSymbol("b", ts.T("bool"))
        );
        ts.ns.Define(fn);
      }

      {
        var fn = new FuncSymbolNative(new Origin(), "bar_fail", Types.Int,
          (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            exec.stack.Pop();
            exec.status = BHS.FAILURE;
            return null;
          },
          new FuncArgSymbol("n", Types.Int)
        );
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(BHS.FAILURE, fb.status);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCleanFuncArgsStackOnFailure()
  {
    string bhl = @"
    func int foo(int v)
    {
      fail()
      return v
    }

    func hey(int a, int b)
    { }

    func test()
    {
      hey(1, foo(10))
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindFail(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(0, fb.exec.stack.sp);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCleanFuncArgsOnStackForSubFailure()
  {
    string bhl = @"

    func hey(int n, int m)
    {
    }

    func int bar()
    {
      return 1
    }

    func int foo()
    {
      fail()
      return 100
    }

    func test()
    {
      hey(bar(), foo())
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindFail(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(0, fb.exec.stack.sp);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCleanFuncArgsOnStackForNativeBind()
  {
    string bhl = @"

    func int foo()
    {
      fail()
      return 100
    }

    func test()
    {
      hey(""bar"", foo())
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      BindFail(ts);

      {
        var fn = new FuncSymbolNative(new Origin(), "hey", Types.Void,
          (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>  { return null; },
          new FuncArgSymbol("s", ts.T("string")),
          new FuncArgSymbol("i", Types.Int)
        );
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(0, fb.exec.stack.sp);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCleanFuncArgsOnStackForYield()
  {
    string bhl = @"

    coro func int foo()
    {
      yield()
      return 100
    }

    func hey(string b, int a)
    {
    }

    coro func test()
    {
      hey(""bar"", 1 + yield foo())
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.True(vm.Tick());
    vm.Stop(fb);
    Assert.Equal(0, fb.exec.stack.sp);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCleanFuncArgsOnStackForYieldInWhile()
  {
    string bhl = @"

    coro func int foo()
    {
      yield()
      return 1
    }

    coro func doer(ref int c)
    {
      while(c < 2) {
        c = c + yield foo()
      }
    }

    coro func test()
    {
      int c = 0
      yield doer(ref c)
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    Assert.Equal(0, fb.exec.stack.sp);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCleanFuncArgsOnStackClassPtr()
  {
    string bhl = @"

    class Foo
    {
      func(int,int) ptr
    }

    func int foo()
    {
      fail()
      return 10
    }

    func void bar(int a, int b)
    {
    }

    func test()
    {
      Foo f = {}
      f.ptr = bar
      f.ptr(10, foo())
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      BindColor(ts);
      BindFail(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    Assert.Equal(0, Execute(vm, "test").result_old.Count);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCleanFuncThisArgOnStackForMethod()
  {
    string bhl = @"

    func float foo()
    {
      fail()
      return 10
    }

    func test()
    {
      Color c = new Color
      c.mult_summ(foo())
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      BindColor(ts);
      BindFail(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    Assert.Equal(0, Execute(vm, "test").result_old.Count);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCleanFuncStackInExpressionForYield()
  {
    string bhl = @"

    coro func int sub_sub_call()
    {
      yield()
      return 2
    }

    coro func int sub_call()
    {
      return 1 + 10 + 12 + yield sub_sub_call()
    }

    coro func test()
    {
      int cost = 1 + yield sub_call()
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.True(vm.Tick());
    vm.Stop(fb);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCleanFuncArgsOnStackForFuncPtrPassedAsArg()
  {
    string bhl = @"

    func []int make_ints(int n, int k)
    {
      []int res = []
      return res
    }

    func int do_fail()
    {
      fail()
      return 10
    }

    func test()
    {
      foo(func []int () { return make_ints(2, do_fail()) } )
      trace(""HERE"")
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindFail(ts);

      {
        var fn = new FuncSymbolNative(new Origin(), "foo", ts.T("Foo"),
          (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            throw new NotImplementedException();
            var fn_ptr = exec.stack.Pop();
            //exec.vm.Start((VM.FuncPtr)fn_ptr.obj, frm);
            fn_ptr.Release();
            return null;
          },
          new FuncArgSymbol("fn", ts.TFunc(ts.TArr(Types.Int)))
        );

        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal(log.ToString(), "HERE");
    CommonChecks(vm);
  }

  [Fact]
  public void TestCleanFuncArgsOnStackForFuncPtr()
  {
    string bhl = @"
    func int foo(int v)
    {
      fail()
      return v
    }

    func bar()
    {
      func int(int) p = foo
      Foo f = PassthruFoo({hey:1, colors:[{r:p(42)}]})
      trace((string)f.hey)
    }

    func test()
    {
      bar()
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindColor(ts);
      BindFoo(ts);
      BindFail(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestCleanFuncArgsOnStackForLambda()
  {
    string bhl = @"
    func bar()
    {
      Foo f = PassthruFoo({hey:1, colors:[{r:
          func int (int v) {
            fail()
            return v
          }(42)
        }]})
      trace((string)f.hey)
    }

    func test()
    {
      bar()
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindColor(ts);
      BindFoo(ts);
      BindFail(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestCleanArgsStackInParal()
  {
    string bhl = @"
    coro func int foo(int ticks)
    {
      if(ticks == 2) {
        yield()
        yield()
        fail()
      } else if(ticks == 3) {
        yield()
        yield()
        fail()
      }
      return 42
    }

    coro func bar()
    {
      Foo f
      paral_all {
        {
          f = PassthruFoo({hey:10, colors:[{r: yield foo(2)}]})
        }
        {
          f = PassthruFoo({hey:20, colors:[{r: yield foo(3)}]})
        }
      }
      trace((string)f.hey)
    }

    coro func test()
    {
      yield bar()
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindColor(ts);
      BindFoo(ts);
      BindFail(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var fb = vm.Start("test");
    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    Assert.Equal(BHS.FAILURE, fb.status);
    Assert.Equal("", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestCleanFuncPtrArgsStackInParal()
  {
    string bhl = @"
    coro func int foo(int ticks)
    {
      if(ticks == 2) {
        yield()
        yield()
        fail()
      } else if(ticks == 3) {
        yield()
        yield()
        fail()
      }
      return 42
    }

    coro func bar()
    {
      coro func int(int) p = foo
      Foo f
      paral_all {
        {
          f = PassthruFoo({hey:10, colors:[{r:yield p(2)}]})
        }
        {
          f = PassthruFoo({hey:20, colors:[{r:yield p(3)}]})
        }
      }
      trace((string)f.hey)
    }

    coro func test()
    {
      yield bar()
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindColor(ts);
      BindFoo(ts);
      BindFail(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var fb = vm.Start("test");
    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    Assert.Equal(BHS.FAILURE, fb.status);
    Assert.Equal("", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestCleanArgsStackInParalForLambda()
  {
    string bhl = @"
    coro func bar()
    {
      Foo f
      paral_all {
        {
          f = PassthruFoo({hey:10, colors:[{r:
              yield coro func int (int n)
              {
                yield()
                yield()
                fail()
                return n
              }(2)
              }]})
        }
        {
          f = PassthruFoo({hey:20, colors:[{r:
              yield coro func int (int n)
              {
                yield()
                yield()
                yield()
                fail()
                return n
              }(3)
              }]})
        }
      }
      trace((string)f.hey)
    }

    coro func test()
    {
      yield bar()
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindColor(ts);
      BindFoo(ts);
      BindFail(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var fb = vm.Start("test");
    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    Assert.Equal(BHS.FAILURE, fb.status);
    Assert.Equal("", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestInterleaveValuesStackInParal()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    func int ticky()
    {
      return 1
    }

    coro func int ret_int(int val, int ticks)
    {
      while(ticks > 0) {
        yield()
        ticks = ticks - 1
      }
      return val
    }

    coro func test()
    {
      paral {
        {
          foo(1, yield ret_int(val: 2, ticks: ticky()))
          yield suspend()
        }
        foo(10, yield ret_int(val: 20, ticks: 2))
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("1 2;10 20;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestInterleaveFuncStackInParal()
  {
    string bhl = @"
    coro func int foo(int ticks)
    {
      if(ticks == 2) {
        yield()
        yield()
      } else if(ticks == 3) {
        yield()
        yield()
        yield()
      }
      return ticks
    }

    coro func bar()
    {
      Foo f
      paral_all {
        {
          f = PassthruFoo({hey:yield foo(3), colors:[]})
          trace((string)f.hey)
        }
        {
          f = PassthruFoo({hey:yield foo(2), colors:[]})
          trace((string)f.hey)
        }
      }
      trace((string)f.hey)
    }

    coro func test()
    {
      yield bar()
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindColor(ts);
      BindFoo(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("233", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestInterleaveValuesStackInParalWithPtrCall()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    coro func int ret_int(int val, int ticks)
    {
      while(ticks > 0)
      {
        yield()
        ticks = ticks - 1
      }
      return val
    }

    coro func test()
    {
      coro func int(int,int) p = ret_int
      paral {
        {
          foo(1, yield p(2, 1))
          yield suspend()
        }
        foo(10, yield p(20, 2))
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("1 2;10 20;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestInterleaveValuesStackInParalWithMemberPtrCall()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    class Bar {
      coro func int(int,int) ptr
    }

    coro func int ret_int(int val, int ticks)
    {
      while(ticks > 0)
      {
        yield()
        ticks = ticks - 1
      }
      return val
    }

    coro func test()
    {
      Bar b = {}
      b.ptr = ret_int
      paral {
        {
          foo(1, yield b.ptr(2, 1))
          yield suspend()
        }
        foo(10, yield b.ptr(20, 2))
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("1 2;10 20;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestInterleaveValuesStackInParalWithLambdaCall()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    coro func test()
    {
      paral {
        {
          foo(1,
              yield coro func int (int val, int ticks) {
                while(ticks > 0) {
                  yield()
                  ticks = ticks - 1
                }
                return val
              }(2, 1))
          yield suspend()
        }
        foo(10,
            yield coro func int (int val, int ticks) {
              while(ticks > 0) {
                yield()
                ticks = ticks - 1
              }
              return val
            }(20, 2))
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("1 2;10 20;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestInterleaveValuesStackInParalAll()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    coro func int ret_int(int val, int ticks)
    {
      while(ticks > 0)
      {
        yield()
        ticks = ticks - 1
      }
      return val
    }

    coro func test()
    {
      paral_all {
        foo(1, yield ret_int(val: 2, ticks: 1))
        foo(10, yield ret_int(val: 20, ticks: 2))
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("1 2;10 20;", log.ToString());
    CommonChecks(vm);
  }

  public class Bar_ret_int : Coroutine
  {
    bool first_time = true;

    int ticks;
    int ret;

    public override void Tick(VM.FrameOld frm, VM.ExecState exec)
    {
      if(first_time)
      {
        ticks = (int)exec.stack_old.PopRelease().num;
        ret = (int)exec.stack_old.PopRelease().num;
        //self
        exec.stack_old.PopRelease();
        first_time = false;
      }

      if(ticks-- > 0)
      {
        exec.status = BHS.RUNNING;
      }
      else
      {
        exec.stack_old.Push(ValOld.NewNum(frm.vm, ret));
      }
    }

    public override void Cleanup(VM.FrameOld frm, VM.ExecState exec)
    {
      first_time = true;
    }
  }

  [Fact]
  public void TestInterleaveValuesStackInParalWithMethods()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    coro func test()
    {
      paral {
        {
          foo(1, yield (new Bar).self().ret_int(val: 2, ticks: 1))
          yield suspend()
        }
        foo(10, yield (new Bar).self().ret_int(val: 20, ticks: 2))
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);

      {
        var cl = new ClassSymbolNative(new Origin(), "Bar",
          delegate(VM vm, ref Val v, IType type)
          {
            //fake object
            v.SetObj(null, type);
          }
        );
        ts.ns.Define(cl);

        {
          var m = new FuncSymbolNative(new Origin(), "self", ts.T("Bar"),
            (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
            {
              var obj = exec.stack.Pop().obj;
              exec.stack.Push(Val.NewObj(obj, ts.T("Bar").Get()));
              return null;
            }
          );
          cl.Define(m);
        }

        {
          var m = new FuncSymbolNative(new Origin(), "ret_int", FuncAttrib.Coro, Types.Int, 0,
            (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
            {
              return CoroutinePool.New<Bar_ret_int>(vm);
            },
            new FuncArgSymbol("val", Types.Int),
            new FuncArgSymbol("ticks", Types.Int)
          );
          cl.Define(m);
          cl.Setup();
        }
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("1 2;10 20;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestBugWithParalInWhile()
  {
    string bhl = @"

    coro func changer() {
      yield()
    }

    coro func doer(int i) {
      yield suspend()
    }

    coro func test()
    {
      int i = 0
      while(i < 2) {
        paral {
          yield changer()
          yield doer(i)
        }
        i++
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [Fact]
  public void TestOperatorTernaryIfIncompatibleTypes()
  {
    string bhl1 = @"
    func test()
    {
      string foo = true ? ""Foo"" : 1
    }
    ";

    string bhl2 = @"
    func int test()
    {
      return true ? ""Foo"" : ""Bar""
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl1); },
      "incompatible types: 'string' and 'int'",
      new PlaceAssert(bhl1, @"
      string foo = true ? ""Foo"" : 1
----------------------------------^" /*taking into account ""*/
      )
    );

    AssertError<Exception>(
      delegate() { Compile(bhl2); },
      "incompatible types: 'int' and 'string'",
      new PlaceAssert(bhl2, @"
      return true ? ""Foo"" : ""Bar""
-------------^"
      )
    );
  }

  [Fact]
  public void TestOperatorTernaryIf()
  {
    string bhl = @"

    func int min(float a, int b)
    {
      return a < b ? (int)a : b
    }

    func int test1()
    {
      float a = 100500
      int b   = 500100

      int c = a > b ? b : (int)a //500100

      return min(a > c ? b : c/*500100*/, (int)a/*100500*/)
    }

    func int test2()
    {
      func int() af = func int() { return 500100 } //500100

      func int() bf = func int() {
        int a = 2
        int b = 1

        int c = a > b ? 100500 : 500100

        return c //100500
      }

      func int() cf = func int() {
        return true ? 100500 : 500100 //100500
      }

      return min(af()/*500100*/,
        af() > bf()/*true*/ ? (false ? af() : cf()/*100500*/) : bf())
        /*100500*/ > af()/*500100*/ ? af() : cf()/*100500*/
    }

    func string test3(int v)
    {
      func string() af = func string() {
        return v == 1 ? ""first value""  :
               v == 2 ? ""second value"" :
               v == 3 ? ""result value"" : ""default value""
      }

      return af()
    }
    ";

    var vm = MakeVM(bhl);

    Assert.Equal(100500, Execute(vm, "test1").Stack.Pop().num);
    Assert.Equal(100500, Execute(vm, "test2").Stack.Pop().num);
    Assert.Equal("default value", ExecuteOld(vm, "test3", ValOld.NewNum(vm, 0)).result_old.PopRelease().str);
    Assert.Equal("second value", ExecuteOld(vm, "test3", ValOld.NewNum(vm, 2)).result_old.PopRelease().str);

    CommonChecks(vm);
  }

  [Fact]
  public void TestOperatorPostfixIncrementCall()
  {
    string bhl = @"
    func int test1()
    {
      int i = 0
      i++
      i = i - 1
      i++
      i = i - 1

      return i
    }

    func test2()
    {
      for(int i = 0; i < 3;) {
        trace((string)i)

        i++
      }

      for(int j = 0; j < 3; j++) {
        trace((string)j)
      }

      for(int j = 0, int k = 1; j < 3; j++, k++) {
        trace((string)j)
        trace((string)k)
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);

    Assert.Equal(0, Execute(vm, "test1").Stack.Pop().num);

    Execute(vm, "test2");
    Assert.Equal("012012011223", log.ToString());

    CommonChecks(vm);
  }

  [Fact]
  public void TestBadOperatorPostfixIncrementCall()
  {
    string bhl1 = @"
    func test()
    {
      ++
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl1); },
      "extraneous input '++'",
      new PlaceAssert(bhl1, @"
      ++
------^"
      )
    );

    string bhl2 = @"
    func test()
    {
      string str = ""Foo""
      str++
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl2); },
      "only numeric types supported",
      new PlaceAssert(bhl2, @"
      str++
------^"
      )
    );

    string bhl3 = @"
    func test()
    {
      for(int j = 0; j < 3; ++) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl3); },
      "extraneous input '++'"
    );

    string bhl4 = @"
    func test()
    {
      int j = 0
      for(++; j < 3; j++) {

      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl4); },
      "extraneous input '++'"
    );

    string bhl5 = @"
    func test()
    {
      for(j = 0, int k = 0; j < 3; j++, k++) {
        trace((string)j)
        trace((string)k)
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl5); },
      "symbol 'j' not resolved"
    );

    string bhl6 = @"
    func foo(float a)
    {
    }

    func int test()
    {
      int i = 0
      foo(i++)
      return i
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl6); },
      "no viable alternative at input '('"
    );

    string bhl7 = @"
    func test()
    {
      []int arr = [0, 1, 3, 4]
      int i = 0
      int j = arr[i++]
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl7); },
      "no viable alternative at input 'int j = arr[i++'"
    );

    string bhl8 = @"
    func int test()
    {
      func bool(int) foo = func bool(int b) { return b > 1 }

      int i = 0
      foo(i++)
      return i
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl8); },
      "no viable alternative at input '('"
    );

    string bhl9 = @"
    func int test()
    {
      int i = 0
      return i++
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl9); },
      "no viable alternative at input 'i'"
    );

    string bhl10 = @"
    func int, int test()
    {
      int i = 0
      int j = 1
      return j, i++
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl10); },
      "rule eos failed predicate",
      new PlaceAssert(bhl10, @"
      return j, i++
-----------------^"
      )
    );

    string bhl11 = @"
    func int, int test()
    {
      int i = 0
      int j = 1
      return j++, i
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl11); },
      "no viable alternative at input 'j'",
      new PlaceAssert(bhl11, @"
      return j++, i
-------------^"
      )
    );
  }

  [Fact]
  public void TestBadOperatorPostfixDecrementCall()
  {
    string bhl1 = @"
    func test()
    {
      --
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl1); },
      "extraneous input '--'"
    );

    string bhl2 = @"
    func test()
    {
      string str = ""Foo""
      str--
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl2); },
      "only numeric types supported"
    );

    string bhl3 = @"
    func test()
    {
      for(int j = 0; j < 3; --) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl3); },
      "extraneous input '--'"
    );

    string bhl4 = @"
    func test()
    {
      int j = 0
      for(--; j < 3; j++) {

      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl4); },
      "extraneous input '--'"
    );

    string bhl5 = @"

    func foo(float a)
    {
    }

    func int test()
    {
      int i = 0
      foo(i--)
      return i
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl5); },
      "no viable alternative at input '('"
    );

    string bhl6 = @"
    func test()
    {
      []int arr = [0, 1, 3, 4]
      int i = 0
      int j = arr[i--]
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl6); },
      "no viable alternative at input 'int j = arr[i--'"
    );

    string bhl7 = @"
    func int test()
    {
      int i = 0
      return i--
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl7); },
      "no viable alternative at input 'i'"
    );

    string bhl8 = @"
    func int, int test()
    {
      int i = 0
      int j = 1
      return j, i--
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl8); },
      "rule eos failed predicate"
    );

    string bhl9 = @"
    func int, int test()
    {
      int i = 0
      int j = 1
      return j--, i
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl9); },
      "no viable alternative at input 'j'"
    );
  }

  [Fact]
  public void TestOperatorPostfixDecrementCall()
  {
    string bhl = @"
    func int test1()
    {
      int i = 0
      i--
      i = i + 1
      i--
      i = i + 1

      return i
    }

    func test2()
    {
      for(int i = 3; i >= 0;) {
        trace((string)i)

        i--
      }

      for(int j = 3; j >= 0; j--) {
        trace((string)j)
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);

    Assert.Equal(0, Execute(vm, "test1").Stack.Pop().num);

    Execute(vm, "test2");
    Assert.Equal("32103210", log.ToString());

    CommonChecks(vm);
  }

  [Fact]
  public void TestBugReturnTypeInsteadOfValue()
  {
    {
      string bhl = @"
      func string test()
      {
        return string
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "symbol usage is not valid",
        new PlaceAssert(bhl, @"
        return string
---------------^"
        )
      );
    }

    {
      string bhl = @"
      class Foo {
        interface IFoo {
        }
      }

      func Foo.IFoo test()
      {
        return Foo.IFoo
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "symbol usage is not valid",
        new PlaceAssert(bhl, @"
        return Foo.IFoo
-------------------^"
        )
      );
    }

    {
      string bhl = @"
      enum Bar {
        DUMMY = 1
      }

      func Bar test()
      {
        return Bar
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "symbol usage is not valid",
        new PlaceAssert(bhl, @"
        return Bar
---------------^"
        )
      );
    }

    {
      string bhl = @"
      class Foo {
        enum Bar {
          DUMMY = 1
        }
      }

      func Foo.Bar test()
      {
        return Foo.Bar
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "symbol usage is not valid",
        new PlaceAssert(bhl, @"
        return Foo.Bar
-------------------^"
        )
      );
    }
  }

  [Fact]
  public void TestBugEarlyReturnBeforeVarDecl()
  {
    {
      string bhl = @"
      enum Bar {
        DUMMY = 1
      }

      func Bar test()
      {
        Bar bar
        {
          return Bar.DUMMY
          Bar bar = Bar.DUMMY
        }
      }
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        "already defined symbol 'bar'",
        new PlaceAssert(bhl, @"
          Bar bar = Bar.DUMMY
--------------^"
        )
      );
    }

    {
      string bhl = @"
      func test()
      {
        return
        int foo = 10
      }
      ";
      var vm = MakeVM(bhl);
      Execute(vm, "test");
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func test()
      {
        return
        string str
      }
      ";

      var vm = MakeVM(bhl);
      Execute(vm, "test");
      CommonChecks(vm);
    }
  }

  [Fact]
  public void TestWhileEmptyArrLoop()
  {
    string bhl = @"

    func test()
    {
      []int arr = []
      int i = 0
      while(i < arr.Count) {
        int tmp = arr[i]
        trace((string)tmp)
        i = i + 1
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestWhileArrLoop()
  {
    string bhl = @"

    func test()
    {
      []int arr = [1,2,3]
      int i = 0
      while(i < arr.Count) {
        int tmp = arr[i]
        trace((string)tmp)
        i = i + 1
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("123", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestNestedWhileArrLoopWithVarDecls()
  {
    string bhl = @"

    func test()
    {
      []int arr = [1,2]
      int i = 0
      while(i < arr.Count) {
        int tmp = arr[i]
        int j = 0
        while(j < arr.Count) {
          int tmp2 = arr[j]
          trace((string)tmp + "","" + (string)tmp2 + "";"")
          j = j + 1
        }
        i = i + 1
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("1,1;1,2;2,1;2,2;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestWhileCounterOnTheTop()
  {
    string bhl = @"

    func test()
    {
      int i = 0
      while(i < 3) {
        i = i + 1
        trace((string)i)
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("123", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestWhileFalse()
  {
    string bhl = @"

    func test()
    {
      int i = 0
      while(false) {
        trace((string)i)
        i = i + 1
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestOrShortCircuit()
  {
    string bhl = @"

    func test()
    {
      Color c = null
      if(c == null || c.r == 0) {
        trace(""OK;"")
      } else {
        trace(""NEVER;"")
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
    Assert.Equal("OK;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestAndShortCircuit()
  {
    string bhl = @"

    func test()
    {
      Color c = null
      if(c != null && c.r == 0) {
        trace(""NEVER;"")
      } else {
        trace(""OK;"")
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
    Assert.Equal("OK;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestAndShortCircuitWithBoolNot()
  {
    string bhl = @"

    func test()
    {
      bool ready = false
      bool activated = true
      if(!ready && activated) {
        trace(""OK;"")
      } else {
        trace(""NEVER;"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);

    Execute(vm, "test");
    Assert.Equal("OK;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestAndShortCircuitWithBoolNotForClassMembers()
  {
    string bhl = @"

    class Foo
    {
      bool ready
      bool activated
    }

    func test()
    {
      Foo foo = {}
      foo.ready = false
      foo.activated = true
      if(!foo.ready && foo.activated) {
        trace(""OK;"")
      } else {
        trace(""NEVER;"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);

    Execute(vm, "test");
    Assert.Equal("OK;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestOrShortCircuitWithBoolNotForClassMembers()
  {
    string bhl = @"

    class Foo
    {
      bool ready
      bool activated
    }

    func test()
    {
      Foo foo = {}
      foo.ready = false
      foo.activated = true
      if(foo.ready || foo.activated) {
        trace(""OK;"")
      } else {
        trace(""NEVER;"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);

    Execute(vm, "test");
    Assert.Equal("OK;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestChainCall()
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
      return MakeColor(k).mult_summ(k*2)
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var res = ExecuteOld(vm, "test", ValOld.NewNum(vm, 2)).Stack.Pop().num;
    Assert.Equal(8, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestChainCall2()
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
      return MakeColor(k).g
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var res = ExecuteOld(vm, "test", ValOld.NewNum(vm, 2)).Stack.Pop().num;
    Assert.Equal(2, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestChainCall3()
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
      return MakeColor(k).Add(1).g
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var res = ExecuteOld(vm, "test", ValOld.NewNum(vm, 2)).Stack.Pop().num;
    Assert.Equal(3, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestChainCall4()
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
      return MakeColor(MakeColor(k).g).g
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var res = ExecuteOld(vm, "test", ValOld.NewNum(vm, 2)).Stack.Pop().num;
    Assert.Equal(2, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestChainCall5()
  {
    string bhl = @"

    func Color MakeColor2(string temp)
    {
      Color c = new Color
      return c
    }

    func Color MakeColor(float g)
    {
      Color c = new Color
      c.g = g
      return c
    }

    func bool Check(bool cond)
    {
      return cond
    }

    func bool test(float k)
    {
      Color o = new Color
      o.g = k
      return Check(MakeColor2(""hey"").Add(1).Add(MakeColor(k).g).r == 3)
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var res = ExecuteOld(vm, "test", ValOld.NewNum(vm, 2)).Stack.Pop().num;
    Assert.Equal(1, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeClassCallMethod()
  {
    string bhl = @"

    func float test(float k)
    {
      Color c = new Color
      c.r = 10
      c.g = 20
      return c.mult_summ(k)
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var res = ExecuteOld(vm, "test", ValOld.NewNum(vm, 2)).Stack.Pop().num;
    Assert.Equal(60, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestUsingBultinTypeAsFunc()
  {
    string bhl = @"

    func float foo()
    {
      return 14
    }

    func int test()
    {
      return int(foo())
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "symbol is not a function",
      new PlaceAssert(bhl, @"
      return int(foo())
-------------^"
      )
    );
  }

  [Fact]
  public void TestPassArgToFiber()
  {
    string bhl = @"
    coro func int test(int a)
    {
      yield()
      return a
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);

    var fb = vm.StartOld("test", ValOld.NewNum(vm, 123));

    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    Assert.Equal(123, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassArgsToFiber()
  {
    string bhl = @"
    coro func float test(float k, float m)
    {
      yield()
      return m
    }
    ";

    var vm = MakeVM(bhl);
    var num = ExecuteOld(vm, "test", ValOld.NewNum(vm, 3), ValOld.NewNum(vm, 7)).Stack.Pop().num;
    Assert.Equal(7, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestPassArgsToFiber2()
  {
    string bhl = @"
    coro func float test(float k, float m)
    {
      yield()
      return k
    }
    ";

    var vm = MakeVM(bhl);
    var num = ExecuteOld(vm, "test", ValOld.NewNum(vm, 3), ValOld.NewNum(vm, 7)).Stack.Pop().num;
    Assert.Equal(3, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStartSeveralFibers()
  {
    string bhl = @"
    coro func int test()
    {
      yield()
      return 123
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);

    var fb1 = vm.Start("test");
    var fb2 = vm.Start("test");

    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    Assert.Equal(123, fb1.Stack.Pop().num);
    Assert.Equal(123, fb2.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFrameCache()
  {
    string bhl = @"

    coro func foo()
    {
      yield()
    }

    coro func test()
    {
      yield foo()
    }
    ";

    var vm = MakeVM(bhl);

    {
      vm.Start("test");
      Assert.Equal(1, vm.frames_pool.MissCount);
      Assert.Equal(0, vm.frames_pool.IdleCount);
      vm.Tick();
      Assert.Equal(2, vm.frames_pool.MissCount);
      Assert.Equal(0, vm.frames_pool.IdleCount);
      vm.Tick();
      Assert.Equal(2, vm.frames_pool.MissCount);
      Assert.Equal(2, vm.frames_pool.IdleCount);
    }

    //no new allocs
    {
      vm.Start("test");
      vm.Tick();
      vm.Tick();
      Assert.Equal(2, vm.frames_pool.MissCount);
      Assert.Equal(2, vm.frames_pool.IdleCount);
    }
    CommonChecks(vm);
  }

  [Fact]
  public void TestRegisterModule()
  {
    string bhl = @"
    func int test()
    {
      return 123
    }
    ";

    var c = Compile(bhl);

    var vm = new VM();
    vm.LoadModule(c);
    Assert.Equal(123, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestLoadModuleTwice()
  {
    string bhl1 = @"
    import ""bhl2""
    func float bhl1()
    {
      return bhl2(23)
    }
    ";

    string bhl2 = @"
    import ""bhl3""

    func float bhl2(float k)
    {
      return bhl3(k)
    }
    ";

    string bhl3 = @"
    func float bhl3(float k)
    {
      return k
    }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var ts = new Types();
    var loader = new ModuleLoader(ts, await CompileFiles(files));

    var vm = new VM(ts, loader);
    Assert.True(vm.LoadModule("bhl1"));
    Assert.Equal(23, Execute(vm, "bhl1").Stack.Pop().num);

    Assert.True(vm.LoadModule("bhl1"));
    Assert.Equal(23, Execute(vm, "bhl1").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestLoadNonExistingModule()
  {
    string bhl1 = @"
    func float bhl1()
    {
      return 42
    }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);

    var ts = new Types();
    var loader = new ModuleLoader(ts, await CompileFiles(files));

    var vm = new VM(ts, loader);
    Assert.False(vm.LoadModule("garbage"));
    Assert.True(vm.LoadModule("bhl1"));
    CommonChecks(vm);
  }

  [Fact]
  public void TestSimpleGlobalVariableDecl()
  {
    string bhl = @"
    float foo

    func float test()
    {
      return foo
    }
    ";

    var c = Compile(bhl);

    var ts = new Types();

    var expected =
        new ModuleCompiler()
          .UseInit()
          .EmitThen(Opcodes.DeclVar, new int[] { 0, TypeIdx(c, ts.T("float")) })
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
          .EmitThen(Opcodes.GetGVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    Assert.Equal(0, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGlobalVariableWriteRead()
  {
    string bhl = @"
    float foo = 10

    func float test()
    {
      foo = 20
      return foo
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseInit()
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
          .EmitThen(Opcodes.SetGVar, new int[] { 0 })
          .EmitThen(Opcodes.GetGVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    Assert.Equal(20, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGlobalVariableAssignConst()
  {
    string bhl = @"

    float foo = 10

    func float test()
    {
      return foo
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseInit()
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/})
          .EmitThen(Opcodes.GetGVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    Assert.Equal(10, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGlobalVariableAssignNegativeNumber()
  {
    string bhl = @"

    float foo = -10

    func float test()
    {
      return foo
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(-10, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGlobalVariableAssignAndReadObject()
  {
    string bhl = @"

    class Foo {
      float b
    }

    Foo foo = {b : 100}

    func float test()
    {
      return foo.b
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(100, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGlobalVariableAssignAndReadArray()
  {
    string bhl = @"
    class Foo {
      float b
    }

    []Foo foos = [{b : 100}, {b: 200}]

    func float test()
    {
      return foos[1].b
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(200, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGlobalVariableWrite()
  {
    string bhl = @"
    class Foo {
      float b
    }

    Foo foo = {b : 100}

    func float bar()
    {
      return foo.b
    }

    func float test()
    {
      foo.b = 101
      return bar()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(101, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGlobalVariableAlreadyDeclared()
  {
    string bhl = @"

    int foo = 0
    int foo = 1

    func int test()
    {
      return foo
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      @"already defined symbol 'foo'",
      new PlaceAssert(bhl, @"
    int foo = 1
--------^"
      )
    );
  }

  [Fact]
  public void TestGlobalVariableSelfAssignError()
  {
    string bhl = @"

    int foo = foo
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      @"symbol 'foo' not resolved",
      new PlaceAssert(bhl, @"
    int foo = foo
--------------^"
      )
    );
  }

  [Fact]
  public void TestGlobalVariableFuncCallsNotAllowed()
  {
    {
      string bhl = @"
      func int make()
      {
        return 10
      }

      int foo = make()
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        @"function calls not allowed in global context",
        new PlaceAssert(bhl, @"
      int foo = make()
----------------^"
        )
      );
    }

    {
      string bhl = @"

      class Foo {
        func int make()
        {
          return 10
        }
      }

      int foo = (new Foo).make()
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        @"function calls not allowed in global context",
        new PlaceAssert(bhl, @"
      int foo = (new Foo).make()
--------------------------^"
        )
      );
    }
  }

  [Fact]
  public void TestLocalVariableHasPriorityOverGlobalOne()
  {
    string bhl = @"

    class Foo {
      float b
    }

    Foo foo = {b : 100}

    func float test()
    {
      Foo foo = {b : 200}
      return foo.b
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(200, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGlobalVarInLambda()
  {
    string bhl = @"

    class Foo {
      float b
    }

    Foo foo = {b : 100}

    func float test()
    {
      float a = 1
      return func float() {
        return foo.b + a
      }()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(101, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  public class TestSetGlobalFromOutside : BHL_TestBase
  {
    [Fact]
    public void _1()
    {
      string bhl = @"
      int foo = 10
      ";

      var vm = MakeVM(bhl);
      Assert.False(vm.TryFindVarAddr("hey", out var addr));
    }

    [Fact]
    public void _2()
    {
      string bhl = @"
      namespace what {
        int hey = 10
      }
      ";

      var vm = MakeVM(bhl);
      Assert.False(vm.TryFindVarAddr("hey", out var addr));
    }

    [Fact]
    public void _3()
    {
      string bhl = @"
      int bar = 1
      int foo = 10

      func int test()
      {
        return foo
      }
      ";

      var vm = MakeVM(bhl);
      Assert.True(vm.TryFindVarAddr("foo", out var addr));
      addr.val.num = 100;
      Assert.Equal(100, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }

    [Fact]
    public void _4()
    {
      string bhl = @"
      int bar = 1
      namespace what {
        int foo = 10
      }

      func int test()
      {
        return what.foo
      }
      ";

      var vm = MakeVM(bhl);
      Assert.True(vm.TryFindVarAddr("what.foo", out var addr));
      Assert.Equal(10, addr.val.num);
      addr.val.num = 100;
      Assert.Equal(100, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }

    [Fact]
    public async Task _5()
    {
      string bar_bhl = @"
      int bar = 1
      ";

      string test_bhl = @"
      import ""bar""

      func int test() {
        return bar
      }
      ";

      var vm = await MakeVM(new Dictionary<string, string>()
        {
          {"bar.bhl", bar_bhl},
          {"test.bhl", test_bhl},
        }
      );
      Assert.False(vm.TryFindVarAddr("bar", out var _));

      vm.LoadModule("bar");
      Assert.True(vm.TryFindVarAddr("bar", out var addr));
      Assert.Equal(1, addr.val.num);
      addr.val.num = 100;

      vm.LoadModule("test");
      Assert.Equal(100, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }

    [Fact]
    public async Task _6()
    {
      string bar_bhl = @"
      namespace N {
        int bar = 1
      }
      ";

      string test_bhl = @"
      import ""bar""

      namespace N {
        int foo = 2
      }

      func int test() {
        return N.bar
      }
      ";

      var vm = await MakeVM(new Dictionary<string, string>()
        {
          {"test.bhl", test_bhl},
          {"bar.bhl", bar_bhl},
        }
      );
      Assert.False(vm.TryFindVarAddr("N.bar", out var _));

      vm.LoadModule("test");
      Assert.True(vm.TryFindVarAddr("N.bar", out var addr));
      Assert.Equal(1, addr.val.num);
      addr.val.num = 100;

      vm.LoadModule("test");
      Assert.Equal(100, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }
  }

  [Fact]
  public void TestWeirdMix()
  {
    string bhl = @"

    coro func A(int b = 1)
    {
      trace(""A"" + (string)b)
      yield suspend()
    }

    coro func test()
    {
      while(true) {
        int i = 0
        paral {
          yield A()
          {
            while(i < 1) {
              yield()
              i = i + 1
            }
          }
        }
        yield()
      }
    }
    ";

    var log = new StringBuilder();

    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    var fb = vm.Start("test");

    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    vm.Stop(fb);

    Assert.Equal("A1A1", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestFixedStack()
  {
    //Push/PopFast
    {
      var st = new FixedStack<int>(16);
      st.Push(1);
      st.Push(10);

      Assert.Equal(2, st.Count);
      Assert.Equal(1, st[0]);
      Assert.Equal(10, st[1]);

      Assert.Equal(10, st.Pop());
      Assert.Equal(1, st.Count);
      Assert.Equal(1, st[0]);

      Assert.Equal(1, st.Pop());
      Assert.Equal(0, st.Count);
    }

    //Push/Dec
    {
      var st = new FixedStack<int>(16);
      st.Push(1);
      st.Push(10);

      Assert.Equal(2, st.Count);
      Assert.Equal(1, st[0]);
      Assert.Equal(10, st[1]);

      --st.Count;
      Assert.Equal(1, st.Count);
      Assert.Equal(1, st[0]);
    }

    //RemoveAt
    {
      var st = new FixedStack<int>(16);
      st.Push(1);
      st.Push(2);
      st.Push(3);

      st.RemoveAt(1);
      Assert.Equal(2, st.Count);
      Assert.Equal(1, st[0]);
      Assert.Equal(3, st[1]);
    }

    //RemoveAt
    {
      var st = new FixedStack<int>(16);
      st.Push(1);
      st.Push(2);
      st.Push(3);

      st.RemoveAt(0);
      Assert.Equal(2, st.Count);
      Assert.Equal(2, st[0]);
      Assert.Equal(3, st[1]);
    }

    //RemoveAt
    {
      var st = new FixedStack<int>(16);
      st.Push(1);
      st.Push(2);
      st.Push(3);

      st.RemoveAt(2);
      Assert.Equal(2, st.Count);
      Assert.Equal(1, st[0]);
      Assert.Equal(2, st[1]);
    }
  }

  public static int ArrAddIdx
  {
    get
    {
      var ts = new Types();
      var arr = (ClassSymbol)ts.TArr("int").Get();
      return ((IScopeIndexed)arr.Resolve("Add")).scope_idx;
    }
  }

  public static int ArrSetIdx
  {
    get
    {
      var ts = new Types();
      var arr = (ClassSymbol)ts.TArr("int").Get();
      return ((IScopeIndexed)arr.Resolve("SetAt")).scope_idx;
    }
  }

  public static int ArrRemoveIdx
  {
    get
    {
      var ts = new Types();
      var arr = (ClassSymbol)ts.TArr("int").Get();
      return ((IScopeIndexed)arr.Resolve("RemoveAt")).scope_idx;
    }
  }

  public static int ArrCountIdx
  {
    get
    {
      var ts = new Types();
      var arr = (ClassSymbol)ts.TArr("int").Get();
      return ((IScopeIndexed)arr.Resolve("Count")).scope_idx;
    }
  }

  class CoroutineWaitTicks : Coroutine
  {
    int c;
    int ticks_ttl;

    public override void Tick(VM.FrameOld frm, VM.ExecState exec)
    {
      //first time
      if(c++ == 0)
        ticks_ttl = (int)exec.stack_old.PopRelease().num;

      if(ticks_ttl-- > 0)
      {
        exec.status = BHS.RUNNING;
      }
    }

    public override void Cleanup(VM.FrameOld frm, VM.ExecState exec)
    {
      c = 0;
      ticks_ttl = 0;
    }
  }

  FuncSymbolNative BindWaitTicks(Types ts, StringBuilder log)
  {
    var fn = new FuncSymbolNative(new Origin(), "WaitTicks", FuncAttrib.Coro, Types.Void, 0,
      (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
      {
        return CoroutinePool.New<CoroutineWaitTicks>(vm);
      },
      new FuncArgSymbol("ticks", Types.Int)
    );
    ts.ns.Define(fn);
    return fn;
  }
}
