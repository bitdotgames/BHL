using System;
using System.Text;
using bhl;
using Xunit;

public class TestOperatorOverload : BHL_TestBase
{
  [Fact]
  public void TestEqualityOverloadedForNativeClassAndNull()
  {
    string bhl = @"

    func test()
    {
      Color c1 = null
      if(c1 == null) {
        trace(""YES"")
      }
    }
    ";

    var log = new StringBuilder();

    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);

      var cl = BindColor(ts, call_setup: false);
      var op = new FuncSymbolNative(new Origin(), "==", FuncAttrib.Static, ts.T("bool"), 0,
        (VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          var ov = exec.stack.PopRelease().obj;
          var cv = exec.stack.PopRelease().obj;

          //null comparison guard
          if(cv == null || ov == null)
          {
            exec.stack.Push(cv == ov);
            return null;
          }

          var o = (Color)ov;
          var c = (Color)cv;

          var v = c.r == o.r && c.g == o.g;
          exec.stack.Push(v);

          return null;
        },
        new FuncArgSymbol("c", ts.T("Color")),
        new FuncArgSymbol("o", ts.T("Color"))
      );
      cl.Define(op);
      cl.Setup();
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual(log.ToString(), "YES");
    CommonChecks(vm);
  }

  [Fact]
  public void TestCustomOperatorOverloadInvalidForNativeClass()
  {
    {
      var ts = new Types();
      var cl = BindColor(ts);
      var op = new FuncSymbolNative(new Origin(), "*", ts.T("Color"), null,
        new FuncArgSymbol("k", ts.T("float"))
      );

      AssertError<Exception>(
        delegate() { cl.Define(op); },
        "operator overload must be static"
      );
    }

    {
      var ts = new Types();
      var cl = BindColor(ts);
      var op = new FuncSymbolNative(new Origin(), "*", FuncAttrib.Static, ts.T("Color"), 0, null,
        new FuncArgSymbol("k", ts.T("float"))
      );

      AssertError<Exception>(
        delegate() { cl.Define(op); },
        "operator overload must have exactly 2 arguments"
      );
    }

    {
      var ts = new Types();
      var cl = BindColor(ts);
      var op = new FuncSymbolNative(new Origin(), "*", FuncAttrib.Static, ts.T("void"), 0, null,
        new FuncArgSymbol("c", ts.T("Color")),
        new FuncArgSymbol("k", ts.T("float"))
      );

      AssertError<Exception>(
        delegate() { cl.Define(op); },
        "operator overload return value can't be void"
      );
    }
  }

  [Fact]
  public void TestCustomOperatorOverloadTypeMismatchForNativeClass()
  {
    string bhl = @"

    func Color test()
    {
      Color c1 = {r:1,g:2}
      return c1 * ""hey""
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      var cl = BindColor(ts);
      var op = new FuncSymbolNative(new Origin(), "*", FuncAttrib.Static, ts.T("Color"), 0, null,
        new FuncArgSymbol("c", ts.T("Color")),
        new FuncArgSymbol("k", ts.T("float"))
      );
      cl.Define(op);
    });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      "incompatible types: 'float' and 'string'",
      new PlaceAssert(bhl, @"
      return c1 * ""hey""
------------------^"
      )
    );
  }

  [Fact]
  public void TestPlusNotOverloadedForNativeClass()
  {
    string bhl = @"

    func test()
    {
      Color c1
      Color c2
      Color c3 = c1 + c2
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      @"operator is not overloaded",
      new PlaceAssert(bhl, @"
      Color c3 = c1 + c2
-----------------^"
      )
    );
  }

  [Fact]
  public void TestMinusNotOverloadedForNativeClass()
  {
    string bhl = @"

    func test()
    {
      Color c1
      Color c2
      Color c3 = c1 - c2
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      @"operator is not overloaded",
      new PlaceAssert(bhl, @"
      Color c3 = c1 - c2
-----------------^"
      )
    );
  }

  [Fact]
  public void TestMultNotOverloadedForNativeClass()
  {
    string bhl = @"

    func test()
    {
      Color c1
      Color c2
      Color c3 = c1 * c2
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      @"operator is not overloaded",
      new PlaceAssert(bhl, @"
      Color c3 = c1 * c2
-----------------^"
      )
    );
  }

  [Fact]
  public void TestDivNotOverloadedForNativeClass()
  {
    string bhl = @"

    func test()
    {
      Color c1
      Color c2
      Color c3 = c1 / c2
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      @"operator is not overloaded",
      new PlaceAssert(bhl, @"
      Color c3 = c1 / c2
-----------------^"
      )
    );
  }

  [Fact]
  public void TestGtNotOverloadedForNativeClass()
  {
    string bhl = @"

    func test()
    {
      Color c1
      Color c2
      bool r = c1 > c2
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      @"operator is not overloaded",
      new PlaceAssert(bhl, @"
      bool r = c1 > c2
---------------^"
      )
    );
  }

  [Fact]
  public void TestGteNotOverloadedForNativeClass()
  {
    string bhl = @"

    func test()
    {
      Color c1
      Color c2
      bool r = c1 >= c2
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      @"operator is not overloaded",
      new PlaceAssert(bhl, @"
      bool r = c1 >= c2
---------------^"
      )
    );
  }

  [Fact]
  public void TestLtNotOverloadedForNativeClass()
  {
    string bhl = @"

    func test()
    {
      Color c1
      Color c2
      bool r = c1 > c2
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      @"operator is not overloaded",
      new PlaceAssert(bhl, @"
      bool r = c1 > c2
---------------^"
      )
    );
  }

  [Fact]
  public void TestLteNotOverloadedForNativeClass()
  {
    string bhl = @"

    func test()
    {
      Color c1
      Color c2
      bool r = c1 > c2
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      @"operator is not overloaded",
      new PlaceAssert(bhl, @"
      bool r = c1 > c2
---------------^"
      )
    );
  }

  [Fact]
  public void TestUnaryMinusNotOverloadedForNativeClass()
  {
    string bhl = @"

    func test()
    {
      Color c1
      Color c2 = -c1
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      @"must be numeric type",
      new PlaceAssert(bhl, @"
      Color c2 = -c1
------------------^"
      )
    );
  }

  [Fact]
  public void TestBitAndNotOverloadedForNativeClass()
  {
    string bhl = @"

    func test()
    {
      Color c1
      Color c2
      int a = c2 & c1
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      @"must be int type",
      new PlaceAssert(bhl, @"
      int a = c2 & c1
--------------^"
      )
    );
  }

  [Fact]
  public void TestBitOrNotOverloadedForNativeClass()
  {
    string bhl = @"

    func test()
    {
      Color c1
      Color c2
      int a = c2 | c1
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      @"must be int type",
      new PlaceAssert(bhl, @"
      int a = c2 | c1
--------------^"
      )
    );
  }

  [Fact]
  public void TestLogicalAndNotOverloadedForNativeClass()
  {
    string bhl = @"

    func test()
    {
      Color c1
      Color c2
      bool a = c2 && c1
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      @"must be bool type",
      new PlaceAssert(bhl, @"
      bool a = c2 && c1
---------------^"
      )
    );
  }

  [Fact]
  public void TestLogicalOrNotOverloadedForNativeClass()
  {
    string bhl = @"

    func test()
    {
      Color c1
      Color c2
      bool a = c2 || c1
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      @"must be bool type",
      new PlaceAssert(bhl, @"
      bool a = c2 || c1
---------------^"
      )
    );
  }

  [Fact]
  public void TestUnaryNotNotOverloadedForNativeClass()
  {
    string bhl = @"

    func test()
    {
      Color c1
      bool a = !c1
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    AssertError<Exception>(
      delegate() { Compile(bhl, ts_fn); },
      @"must be bool type",
      new PlaceAssert(bhl, @"
      bool a = !c1
----------------^"
      )
    );
  }

  [Fact]
  public void TestPlusOverloadedForNativeClass()
  {
    string bhl = @"

    func Color test()
    {
      Color c1 = {r:1,g:2}
      Color c2 = {r:20,g:30}
      Color c3 = c1 + c2
      return c3
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      var cl = BindColor(ts, call_setup: false);
      var op = new FuncSymbolNative(new Origin(), "+", FuncAttrib.Static, ts.T("Color"), 0,
        (VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          var o = (Color)exec.stack.PopRelease().obj;
          var c = (Color)exec.stack.PopRelease().obj;

          var newc = new Color();
          newc.r = c.r + o.r;
          newc.g = c.g + o.g;

          var v = Val.NewObj(newc, ts.T("Color").Get());
          exec.stack.Push(v);

          return null;
        },
        new FuncArgSymbol("c", ts.T("Color")),
        new FuncArgSymbol("o", ts.T("Color"))
      );
      cl.Define(op);
      cl.Setup();
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = (Color)Execute(vm, "test").result_old.PopRelease().obj;
    Assert.Equal(21, res.r);
    Assert.Equal(32, res.g);
    CommonChecks(vm);
  }

  [Fact]
  public void TestMultOverloadedForNativeClass()
  {
    string bhl = @"

    func Color test()
    {
      Color c1 = {r:1,g:2}
      Color c2 = c1 * 2
      return c2
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      var cl = BindColor(ts, call_setup: false);
      var op = new FuncSymbolNative(new Origin(), "*", FuncAttrib.Static, ts.T("Color"), 0,
        (VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          var k = (float)exec.stack.PopRelease().num;
          var c = (Color)exec.stack.PopRelease().obj;

          var newc = new Color();
          newc.r = c.r * k;
          newc.g = c.g * k;

          var v = Val.NewObj(newc, ts.T("Color").Get());
          exec.stack.Push(v);

          return null;
        },
        new FuncArgSymbol("c", ts.T("Color")),
        new FuncArgSymbol("k", ts.T("float"))
      );
      cl.Define(op);
      cl.Setup();
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = (Color)Execute(vm, "test").result_old.PopRelease().obj;
    Assert.Equal(2, res.r);
    Assert.Equal(4, res.g);
    CommonChecks(vm);
  }

  [Fact]
  public void TestOverloadedBinOpsPriorityForNativeClass()
  {
    string bhl = @"

    func Color test()
    {
      Color c1 = {r:1,g:2}
      Color c2 = {r:10,g:20}
      Color c3 = c1 + c2 * 2
      return c3
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      var cl = BindColor(ts, call_setup: false);
      {
        var op = new FuncSymbolNative(new Origin(), "*", FuncAttrib.Static, ts.T("Color"), 0,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            var k = (float)exec.stack.PopRelease().num;
            var c = (Color)exec.stack.PopRelease().obj;

            var newc = new Color();
            newc.r = c.r * k;
            newc.g = c.g * k;

            var v = Val.NewObj(newc, ts.T("Color").Get());
            exec.stack.Push(v);

            return null;
          },
          new FuncArgSymbol("c", ts.T("Color")),
          new FuncArgSymbol("k", ts.T("float"))
        );
        cl.Define(op);
      }

      {
        var op = new FuncSymbolNative(new Origin(), "+", FuncAttrib.Static, ts.T("Color"), 0,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            var o = (Color)exec.stack.PopRelease().obj;
            var c = (Color)exec.stack.PopRelease().obj;

            var newc = new Color();
            newc.r = c.r + o.r;
            newc.g = c.g + o.g;

            var v = Val.NewObj(newc, ts.T("Color").Get());
            exec.stack.Push(v);

            return null;
          },
          new FuncArgSymbol("c", ts.T("Color")),
          new FuncArgSymbol("r", ts.T("Color"))
        );
        cl.Define(op);
      }
      cl.Setup();
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = (Color)Execute(vm, "test").result_old.PopRelease().obj;
    Assert.Equal(21, res.r);
    Assert.Equal(42, res.g);
    CommonChecks(vm);
  }

  [Fact]
  public void TestEqualityOverloadedForNativeClass()
  {
    string bhl = @"

    func test()
    {
      Color c1 = {r:1,g:2}
      Color c2 = {r:1,g:2}
      Color c3 = {r:10,g:20}
      if(c1 == c2) {
        trace(""YES"")
      }
      if(c1 == c3) {
        trace(""NO"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);

      var cl = BindColor(ts, call_setup: false);
      var op = new FuncSymbolNative(new Origin(), "==", FuncAttrib.Static, ts.T("bool"), 0,
        (VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          var o = (Color)exec.stack.Pop().obj;
          var c = (Color)exec.stack.Pop().obj;

          exec.stack.Push(c.r == o.r && c.g == o.g);

          return null;
        },
        new FuncArgSymbol("c", ts.T("Color")),
        new FuncArgSymbol("o", ts.T("Color"))
      );
      cl.Define(op);
      cl.Setup();
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual(log.ToString(), "YES");
    CommonChecks(vm);
  }
}
