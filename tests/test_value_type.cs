using System;
using bhl;
using Xunit;

public class TestValueType : BHL_TestBase
{
  [Fact]
  public void TestSetGetAttributeOnEncoded()
  {
    string bhl = @"

    func int test()
    {
      IntStruct s = {}
      s.n = 100
      return s.n
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStructEncoded(ts); });

    var c = Compile(bhl, ts_fn);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitChain(Opcodes.Frame, new int[] { 1, 1 })
          .EmitChain(Opcodes.New, new int[] { TypeIdx(c, c.ns.T("IntStruct")) })
          .EmitChain(Opcodes.SetVar, new int[] { 0 })
          .EmitChain(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
          .EmitChain(Opcodes.SetVarAttr, new int[] { 0, 0 })
          .EmitChain(Opcodes.GetVarAttr, new int[] { 0, 0 })
          .EmitChain(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts_fn);
    Assert.Equal(100, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSetGetAttributeForObjUnsafe()
  {
    string bhl = @"

    func int test()
    {
      IntStruct s = {}
      s.n = 100
      return s.n
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStructAsObjUnsafe(ts); });
    var vm = MakeVM(bhl, ts_fn);
    Assert.Equal(100, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSetGetAttributeForObj()
  {
    string bhl = @"

    func int test()
    {
      IntStruct s = {}
      s.n = 100
      return s.n
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStructAsObj(ts); });
    var vm = MakeVM(bhl, ts_fn);
    Assert.Equal(100, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestChangeAttributeOnACopyEncoded()
  {
    string bhl = @"

    func int,int test()
    {
      IntStruct s1 = {}
      s1.n = 1
      IntStruct s2 = s1
      s2.n = 100
      return s1.n, s2.n
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStructEncoded(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var stack = Execute(vm, "test").Stack;
    Assert.Equal(1, stack.Pop().num);
    Assert.Equal(100, stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestChangeAttributeOnACopyObj()
  {
    string bhl = @"

    func int,int test()
    {
      IntStruct s1 = {}
      s1.n = 1
      IntStruct s2 = s1
      s2.n = 100
      return s1.n, s2.n
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStructAsObj(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var stack = Execute(vm, "test").Stack;
    Assert.Equal(1, stack.Pop().num);
    Assert.Equal(100, stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestChangeAttributeOnACopyUnsafe()
  {
    string bhl = @"

    func int,int test()
    {
      IntStruct s1 = {}
      s1.n = 1
      IntStruct s2 = s1
      s2.n = 100
      return s1.n, s2.n
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStructAsObjUnsafe(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var stack = Execute(vm, "test").Stack;
    Assert.Equal(1, stack.Pop().num);
    Assert.Equal(100, stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestChangeAttributeOnCapturedValueEncoded()
  {
    string bhl = @"

    func int test()
    {
      IntStruct s1 = {}
      s1.n = 1
      func () {
        s1.n = 100
      }()
      return s1.n
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStructEncoded(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var stack = Execute(vm, "test").Stack;
    Assert.Equal(100, stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCallAddMethodOnEncoded()
  {
    string bhl = @"

    func int test()
    {
      IntStruct s = {}
      s.n = 100
      s.Add(10)
      return s.n
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStructEncoded(ts); });

    var c = Compile(bhl, ts_fn);

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitChain(Opcodes.Frame, new int[] { 1, 1 })
          .EmitChain(Opcodes.New, new int[] { TypeIdx(c, c.ns.T("IntStruct")) })
          .EmitChain(Opcodes.SetVar, new int[] { 0 })
          .EmitChain(Opcodes.Constant, new int[] { ConstIdx(c, 100) })
          .EmitChain(Opcodes.SetVarAttr, new int[] { 0, 0 })
          .EmitChain(Opcodes.GetVar, new int[] { 0 })
          .EmitChain(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitChain(Opcodes.CallVarMethodNative, new int[] { 0, 1, 1 })
          .EmitChain(Opcodes.GetVarAttr, new int[] { 0, 0 })
          .EmitChain(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts_fn);
    Assert.Equal(110, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCallAddMethodOnEncoded2()
  {
    string bhl = @"

    func int,int test()
    {
      IntStruct s1 = {}
      s1.n = 100
      var s2 = s1
      s2.Add(10)
      return s1.n, s2.n
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStructEncoded(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var stack = Execute(vm, "test").Stack;
    Assert.Equal(100, stack.Pop().num);
    Assert.Equal(110, stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCallAddMethodOnUnsafe()
  {
    string bhl = @"

    func int,int test()
    {
      IntStruct s1 = {}
      s1.n = 100
      var s2 = s1
      s2.Add(10)
      return s1.n, s2.n
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStructAsObjUnsafe(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var stack = Execute(vm, "test").Stack;
    Assert.Equal(100, stack.Pop().num);
    Assert.Equal(110, stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCallAddMethodOnObj()
  {
    string bhl = @"

    func int,int test()
    {
      IntStruct s1 = {}
      s1.n = 100
      var s2 = s1
      s2.Add(10)
      return s1.n, s2.n
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStructAsObj(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var stack = Execute(vm, "test").Stack;
    Assert.Equal(100, stack.Pop().num);
    Assert.Equal(110, stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCallAddMethodOnRefEncoded()
  {
    string bhl = @"
    func add(ref IntStruct s)
    {
      s.Add(10)
    }

    func int test()
    {
      IntStruct s1 = {}
      s1.n = 100
      add(ref s1)
      return s1.n
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStructEncoded(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var stack = Execute(vm, "test").Stack;
    Assert.Equal(110, stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCallAddMethodOnRefObj()
  {
    string bhl = @"
    func add(ref IntStruct s)
    {
      s.Add(10)
    }

    func int test()
    {
      IntStruct s1 = {}
      s1.n = 100
      add(ref s1)
      return s1.n
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStructAsObj(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var stack = Execute(vm, "test").Stack;
    Assert.Equal(110, stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCallAddMethodOnRefObjUnsafe()
  {
    string bhl = @"
    func add(ref IntStruct s)
    {
      s.Add(10)
    }

    func int test()
    {
      IntStruct s1 = {}
      s1.n = 100
      add(ref s1)
      return s1.n
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStructAsObjUnsafe(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var stack = Execute(vm, "test").Stack;
    Assert.Equal(110, stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestCallAddMethodOnTempEncoded()
  {
    string bhl = @"

    func IntStruct make()
    {
      IntStruct s1 = {n: 100}
      return s1
    }

    func test()
    {
      make().Add(10)
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStructEncoded(ts); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [Fact]
  public void TestCallAddMethodOnTempObj()
  {
    string bhl = @"

    func IntStruct make()
    {
      IntStruct s1 = {n: 100}
      return s1
    }

    func test()
    {
      make().Add(10)
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStructAsObj(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var stack = Execute(vm, "test").Stack;
    //for this special case 'obj' is not cleared because the
    //method binding sets 'obj' directly in memory slot
    Assert.NotNull(stack.vals[0].obj);
    stack.vals[0].obj = null;
    CommonChecks(vm);
  }

  [Fact]
  public void TestCallAddMethodOnTempUnsafeObj()
  {
    string bhl = @"

    func IntStruct make()
    {
      IntStruct s1 = {n: 100}
      return s1
    }

    func test()
    {
      make().Add(10)
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStructAsObjUnsafe(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var stack = Execute(vm, "test").Stack;
    //for this special case 'obj' is not cleared because the
    //method binding sets 'obj' directly in memory slot
    Assert.NotNull(stack.vals[0].obj);
    stack.vals[0].obj = null;
    CommonChecks(vm);
  }

}
