using System;
using System.Text;
using bhl;
using Xunit;

public class TestARC : BHL_TestBase
{
  [Fact]
  public void TestRefCountSimple()
  {
    string bhl = @"
    func test()
    {
      RefC r = new RefC
    }
    ";

    var logs = new StringBuilder();

    var ts_fn = new Action<Types>((ts) => { BindRefC(ts, logs); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("INC1;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestRefCountReturnResult()
  {
    string bhl = @"
    func RefC test()
    {
      return new RefC
    }
    ";

    var logs = new StringBuilder();

    var ts_fn = new Action<Types>((ts) => { BindRefC(ts, logs); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test").Stack.PopRelease();
    Assert.Equal("INC1;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestRefCountAssignSame()
  {
    string bhl = @"
    func test()
    {
      RefC r1 = new RefC
      RefC r2 = r1
      r2 = r1
    }
    ";

    var logs = new StringBuilder();

    var ts_fn = new Action<Types>((ts) => { BindRefC(ts, logs); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("INC1;INC2;INC3;DEC2;DEC1;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestRefCountAssignSelf()
  {
    string bhl = @"
    func test()
    {
      RefC r1 = new RefC
      r1 = r1
    }
    ";

    var logs = new StringBuilder();

    var ts_fn = new Action<Types>((ts) => { BindRefC(ts, logs); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("INC1;INC2;DEC1;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestRefCountAssignOverwrite()
  {
    string bhl = @"
    func test()
    {
      RefC r1 = new RefC
      RefC r2 = new RefC
      r1 = r2
    }
    ";

    var logs = new StringBuilder();

    var ts_fn = new Action<Types>((ts) => { BindRefC(ts, logs); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("INC1;INC1;INC2;DEC0;DEC1;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestRefCountSeveral()
  {
    string bhl = @"
    func test()
    {
      RefC r1 = new RefC
      RefC r2 = new RefC
    }
    ";

    var logs = new StringBuilder();

    var ts_fn = new Action<Types>((ts) => { BindRefC(ts, logs); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("INC1;INC1;DEC0;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestRefCountInLambda()
  {
    string bhl = @"
    func test()
    {
      RefC r1 = new RefC

      trace(""REFS"" + (string)r1.refs + "";"")

      func() fn = func() {
        trace(""REFS"" + (string)r1.refs + "";"")
      }

      fn()
      trace(""REFS"" + (string)r1.refs + "";"")
    }
    ";

    var logs = new StringBuilder();

    var ts_fn = new Action<Types>((ts) =>
    {
      BindRefC(ts, logs);
      BindTrace(ts, logs);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("INC1;INC2;DEC1;INC2;DEC1;REFS2;INC2;INC3;INC4;DEC3;REFS4;DEC2;INC3;DEC2;REFS3;DEC1;DEC0;",
      logs.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestRefCountInArray()
  {
    string bhl = @"
    func test()
    {
      []RefC rs = new []RefC
      rs.Add(new RefC)
    }
    ";

    var logs = new StringBuilder();

    var ts_fn = new Action<Types>((ts) => { BindRefC(ts, logs); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("INC1;INC2;DEC1;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestRefCountSeveralInArrayAccess()
  {
    string bhl = @"
    func test()
    {
      []RefC rs = new []RefC
      rs.Add(new RefC)
      rs.Add(new RefC)
      float refs = rs[1].refs
    }
    ";

    var logs = new StringBuilder();

    var ts_fn = new Action<Types>((ts) => { BindRefC(ts, logs); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("INC1;INC2;DEC1;INC1;INC2;DEC1;INC2;DEC1;DEC0;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestRefCountReturn()
  {
    string bhl = @"

    func RefC make()
    {
      RefC c = new RefC
      return c
    }

    func test()
    {
      RefC c1 = make()
    }
    ";

    var logs = new StringBuilder();

    var ts_fn = new Action<Types>((ts) => { BindRefC(ts, logs); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("INC1;INC2;DEC1;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestRefCountPass()
  {
    string bhl = @"

    func foo(RefC c)
    {
      trace(""HERE;"")
    }

    func test()
    {
      foo(new RefC)
    }
    ";

    var logs = new StringBuilder();

    var ts_fn = new Action<Types>((ts) =>
    {
      BindRefC(ts, logs);
      BindTrace(ts, logs);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("INC1;HERE;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestRefCountReturnPass()
  {
    string bhl = @"

    func RefC make()
    {
      RefC c = new RefC
      return c
    }

    func foo(RefC c)
    {
      RefC c2 = c
    }

    func test()
    {
      RefC c1 = make()
      foo(c1)
    }
    ";

    var logs = new StringBuilder();

    var ts_fn = new Action<Types>((ts) => { BindRefC(ts, logs); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("INC1;INC2;DEC1;INC2;INC3;DEC2;DEC1;DEC0;", logs.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestRefCountForUserClassAttrSet()
  {
    string bhl = @"

    class Foo {
    }

    class Bar {
      Foo foo
    }

    func test()
    {
      var foo = new Foo
      var bar = new Bar
      bar.foo = foo
    }
    ";

    var vm = MakeVM(bhl);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [Fact]
  public void TestRefCountForUserClassAttrGetSet()
  {
    string bhl = @"

    class Foo {
    }

    class Bar {
      Foo foo
    }

    func test()
    {
      var foo = new Foo
      var bar1 = new Bar
      bar1.foo = foo
      var bar2 = new Bar
      bar2.foo = bar1.foo
    }
    ";

    var vm = MakeVM(bhl);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  void BindRefC(Types ts, StringBuilder logs)
  {
    {
      var cl = new ClassSymbolNative(new Origin(), "RefC", null,
        delegate(VM vm, ref Val v, IType type) { v.SetObj(new RefC(logs), type); }
      );
      {
        var vs = new FieldSymbol(new Origin(), "refs", Types.Int,
          delegate(VM vm, Val ctx, ref Val v, FieldSymbol fld) { v._num = ((RefC)ctx.obj)._refs; },
          //read only property
          null
        );
        cl.Define(vs);
      }
      cl.Setup();
      ts.ns.Define(cl);
    }
  }

  public class RefC : IValRefcounted
  {
    public int _refs;
    public StringBuilder logs;

    public int refs => _refs;

    public RefC(StringBuilder logs)
    {
      ++_refs;
      logs.Append("INC" + _refs + ";");
      this.logs = logs;
    }

    public void Retain()
    {
      ++_refs;
      logs.Append("INC" + _refs + ";");
    }

    public void Release()
    {
      --_refs;
      logs.Append("DEC" + _refs + ";");
    }
  }
}
