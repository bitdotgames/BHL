using System.Collections.Generic;
using bhl;
using Xunit;

// The peephole pass fuses a few hot opcode runs. Fused ops keep the bytes of the run they
// replace, so what's pinned here is that they actually fire and that execution still lands
// where it used to - especially when a jump points into the middle of a candidate run.
public class TestSuperInstructions : BHL_TestBase
{
  List<Opcodes> Ops(byte[] bs)
  {
    var res = new List<Opcodes>();
    for(int i = 0; i < bs.Length; )
    {
      var op = (Opcodes)bs[i];
      res.Add(op);
      i += ModuleCompiler.PredictOpcodeSize(ModuleCompiler.LookupOpcode(op), bs, i) + 1;
    }
    return res;
  }

  [Fact]
  public void TestFibFusesAndStillComputes()
  {
    string bhl = @"
    func int fib(int x)
    {
      if(x == 0) {
        return 0
      } else {
        if(x == 1) {
          return 1
        } else {
          return fib(x - 1) + fib(x - 2)
        }
      }
    }
    ";

    var c = Compile(bhl);
    var ops = Ops(c.compiled.bytecode);

    Assert.Contains(Opcodes.LocalConstCmpJump, ops);
    Assert.Contains(Opcodes.LocalConstArith, ops);
    Assert.Contains(Opcodes.ConstReturn, ops);

    //NOTE: the replaced opcodes are still in the bytes, just never dispatched
    Assert.DoesNotContain(Opcodes.EqualScalar, ops);

    var vm = MakeVM(c);
    Assert.Equal(55, Execute(vm, "fib", Val.NewInt(10)).Stack.PopRelease().num);
    CommonChecks(vm);
  }

  //NOTE: a loop body is a jump target, so a run reached by the back jump must be left alone
  [Fact]
  public void TestLoopWithJumpIntoACandidateRun()
  {
    string bhl = @"
    func int test()
    {
      int i = 10
      int s = 0
      while(i != 0) {
        s = s + i
        i = i - 1
      }
      return s
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(55, Execute(vm, "test").Stack.PopRelease().num);
    CommonChecks(vm);
  }

  //NOTE: every arith and compare the fused opcodes dispatch on, each against a constant
  [Theory]
  [InlineData("x + 3", 13)]
  [InlineData("x - 3", 7)]
  [InlineData("x * 3", 30)]
  [InlineData("x / 5", 2)]
  [InlineData("x % 3", 1)]
  public void TestEveryFusedArith(string expr, int expected)
  {
    var vm = MakeVM("func int test(int x) { return " + expr + " }");
    Assert.Equal(expected, Execute(vm, "test", Val.NewInt(10)).Stack.PopRelease().num);
    CommonChecks(vm);
  }

  [Theory]
  [InlineData("x == 5", 0)]
  [InlineData("x == 10", 1)]
  [InlineData("x < 10", 0)]
  [InlineData("x <= 10", 1)]
  [InlineData("x > 10", 0)]
  [InlineData("x >= 10", 1)]
  public void TestEveryFusedCompareJump(string cond, int expected)
  {
    var vm = MakeVM("func int test(int x) { if(" + cond + ") { return 1 } else { return 0 } }");
    Assert.Equal(expected, Execute(vm, "test", Val.NewInt(10)).Stack.PopRelease().num);
    CommonChecks(vm);
  }

  //NOTE: a ternary jumps into the middle of a fusable run - without the barrier set this
  //      miscompiles, and VerifyFusedCode throws at compile time
  [Fact]
  public void TestTernaryJumpsIntoACandidateRun()
  {
    var vm = MakeVM("func int test(int x) { return x == 1 ? x + 10 : x - 10 }");
    Assert.Equal(11, Execute(vm, "test", Val.NewInt(1)).Stack.PopRelease().num);
    Assert.Equal(-8, Execute(vm, "test", Val.NewInt(2)).Stack.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFloatAndStringConstantsAreNotMiscompiled()
  {
    string bhl = @"
    func float f(float x)
    {
      return x - 0.5
    }

    func string s()
    {
      return ""hey""
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1.5, Execute(vm, "f", Val.NewFlt(2.0)).Stack.PopRelease().num);
    Assert.Equal("hey", Execute(vm, "s").Stack.PopRelease().str);
    CommonChecks(vm);
  }
}
