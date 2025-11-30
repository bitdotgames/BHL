using System;
using bhl;
using Xunit;

public class TestValueType : BHL_TestBase
{
  [Fact]
  public void TestSetGetAttribute()
  {
    string bhl = @"

    func int test()
    {
      IntStruct s = {}
      s.n = 100
      return s.n
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindIntStruct(ts); });

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
  public void TestChangeAttributeOnACopy()
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

    var ts_fn = new Action<Types>((ts) => { BindIntStruct(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var stack = Execute(vm, "test").Stack;
    Assert.Equal(1, stack.Pop().num);
    Assert.Equal(100, stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestChangeAttributeOnCapturedValue()
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

    var ts_fn = new Action<Types>((ts) => { BindIntStruct(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var stack = Execute(vm, "test").Stack;
    Assert.Equal(100, stack.Pop().num);
    CommonChecks(vm);
  }

}
