using System.Linq;
using bhl;
using Xunit;

// The collector behind BHL_OPCODE_PROFILE. It's compiled unconditionally so it can be tested
// here, while the dispatch loop only calls it in a profiling build.
public class TestOpcodeProfiler
{
  [Fact]
  public void TestCountsRunsOfTwoToFour()
  {
    OpcodeProfiler.Clear();

    //NOTE: run it twice so counts are distinguishable from a single pass
    for(int i = 0; i < 2; ++i)
    {
      OpcodeProfiler.Record(Opcodes.GetVarScalar);
      OpcodeProfiler.Record(Opcodes.Constant);
      OpcodeProfiler.Record(Opcodes.Add);
      OpcodeProfiler.Record(Opcodes.SetVarScalar);
    }

    var top = OpcodeProfiler.Top(100);

    Assert.Equal(2, top.Single(e => e.run == "GetVarScalar,Constant").count);
    Assert.Equal(2, top.Single(e => e.run == "GetVarScalar,Constant,Add").count);
    Assert.Equal(2, top.Single(e => e.run == "GetVarScalar,Constant,Add,SetVarScalar").count);

    //NOTE: longer than the window is never recorded
    Assert.All(top, e => Assert.InRange(e.len, 2, 4));
  }

  // A run that straddles a jump or a call could never be fused, so the window ends there.
  [Fact]
  public void TestRunsDoNotSpanAControlTransfer()
  {
    OpcodeProfiler.Clear();

    OpcodeProfiler.Record(Opcodes.GetVarScalar);
    OpcodeProfiler.Record(Opcodes.JumpZ);
    OpcodeProfiler.Record(Opcodes.Constant);
    OpcodeProfiler.Record(Opcodes.Add);

    var top = OpcodeProfiler.Top(100);

    //NOTE: the terminator still closes a run - 'cmp, JumpZ' is exactly a shape worth fusing
    Assert.Equal(1, top.Single(e => e.run == "GetVarScalar,JumpZ").count);
    //NOTE: but nothing continues through it
    Assert.DoesNotContain(top, e => e.run.StartsWith("JumpZ,"));
    Assert.Equal(1, top.Single(e => e.run == "Constant,Add").count);
  }

  [Fact]
  public void TestResetStartsAFreshRun()
  {
    OpcodeProfiler.Clear();

    OpcodeProfiler.Record(Opcodes.GetVarScalar);
    OpcodeProfiler.Reset();
    OpcodeProfiler.Record(Opcodes.Constant);

    Assert.DoesNotContain(OpcodeProfiler.Top(100), e => e.run == "GetVarScalar,Constant");
  }
}
