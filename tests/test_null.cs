using System;
using System.Text;
using bhl;
using Xunit;

public class TestNull : BHL_TestBase
{
  [Fact]
  public void TestStructCanBeNull()
  {
    string bhl = @"

    func bool test()
    {
      IntStruct c = null
      return c == null
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStructEncoded(ts); });

    var vm = MakeVM(bhl, ts_fn);
    Assert.True(Execute(vm, "test").Stack.Pop().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNullWithEncodedStruct()
  {
    string bhl = @"

    func test()
    {
      IntStruct c = null
      IntStruct c2 = {n: 1}
      if(c == null) {
        trace(""NULL;"")
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
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindIntStructEncoded(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("NULL;NOTNULL;EQ;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestNullPassedAsNullObj()
  {
    string bhl = @"

    func test(StringClass c)
    {
      if(c != null) {
        trace(""NEVER;"")
      }
      if(c == null) {
        trace(""NULL;"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindStringClass(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test", Val.NewObj(null, Types.Any));
    AssertEqual("NULL;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestNullPassedFromAbove()
  {
    string bhl = @"

    func test(StringClass c)
    {
      if(c != null) {
        trace(""NEVER;"")
      }
      if(c == null) {
        trace(""NULL;"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindStringClass(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test", VM.Null);
    AssertEqual("NULL;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestSetNullObjFromUserBinding()
  {
    string bhl = @"

    func test()
    {
      Color c = mkcolor_null()
      if(c == null) {
        trace(""NULL;"")
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
    AssertEqual("NULL;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestNullIncompatible()
  {
    string bhl = @"

    func bool test()
    {
      return 0 == null
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "incompatible types: 'int' and 'null'",
      new PlaceAssert(bhl, @"
      return 0 == null
------------------^"
      )
    );
  }

  [Fact]
  public void TestNullArray()
  {
    string bhl = @"

    func test()
    {
      []Color cs = null
      []Color cs2 = new []Color
      if(cs == null) {
        trace(""NULL;"")
      }
      if(cs2 == null) {
        trace(""NULL2;"")
      }
      if(cs2 != null) {
        trace(""NOT NULL;"")
      }
      cs = cs2
      if(cs2 == cs) {
        trace(""EQ;"")
      }
      if(cs2 == null) {
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
    AssertEqual("NULL;NOT NULL;EQ;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestNullArrayByDefault()
  {
    string bhl = @"

    func test()
    {
      []Color cs
      if(cs == null) {
        trace(""NULL;"")
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
    AssertEqual("NULL;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestNullFuncPtr()
  {
    string bhl = @"

    func test()
    {
      func() fn = null
      func() fn2 = func () { }
      if(fn == null) {
        trace(""NULL;"")
      }
      if(fn != null) {
        trace(""NEVER;"")
      }
      if(fn2 == null) {
        trace(""NEVER2;"")
      }
      if(fn2 != null) {
        trace(""NOT NULL;"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("NULL;NOT NULL;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestNullFuncPtrAsDefaultFuncArg()
  {
    string bhl = @"

    func foo(int a, func int(int) fn = null) {
      if(fn == null) {
        trace(""NULL;"")
      } else {
        trace(""NOT NULL;"")
      }
    }

    func test()
    {
      foo(1)
      foo(2, func int(int a) { return a})
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("NULL;NOT NULL;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestNullFuncPtrAsDefaultFuncArgClosure()
  {
    string bhl = @"

    func foo(int a, int b = 10, func int(int) fn = null)
    {
      var temp = func() {
        if(fn != null) {
          fn(b)
          trace(""NOT NULL;"")
        } else {
          trace(""NULL;"")
        }
      }
      temp()
    }

    func test()
    {
      foo(1)
      foo(2, fn: func int(int a) { return a})
      foo(3, fn: null)
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("NULL;NOT NULL;NULL;", log.ToString());
    CommonChecks(vm);
  }
}
