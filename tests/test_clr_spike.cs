using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

// Spike, not a compiler: the C# a CLR backend would have to emit for
// TestParalWithForInABranchAndDefer, hand written, asserting the same trace the VM produces.
// See spikes/clr/README.md.
public class TestCLRSpike
{
  public struct Tick {}

  //NOTE: paral steps each branch once per tick, ends when one of them does, and disposes the
  //      rest - disposing an iterator runs its pending finally blocks, which is what makes a
  //      cancelled branch still run its defers
  static IEnumerator<Tick> Paral(params IEnumerator<Tick>[] branches)
  {
    try
    {
      while(true)
      {
        for(int i = 0; i < branches.Length; ++i)
          if(!branches[i].MoveNext())
            yield break;

        yield return default(Tick);
      }
    }
    finally
    {
      for(int i = 0; i < branches.Length; ++i)
        branches[i].Dispose();
    }
  }

  // { yield() yield() yield() }
  static IEnumerator<Tick> BranchA()
  {
    yield return default(Tick);
    yield return default(Tick);
    yield return default(Tick);
  }

  // for(int i = 0; i < 3; i++) { defer { trace(i+"~;") } trace(i+";") yield() }
  //
  //NOTE: lowered to a while with the increment INSIDE the try, not a C# for - defer sees the
  //      incremented variable when the scope exits normally, but not when it's cancelled, and
  //      a C# for runs its finally before the increment
  static IEnumerator<Tick> BranchB(Action<string> trace)
  {
    int i = 0;
    while(i < 3)
    {
      try
      {
        trace(i + ";");
        yield return default(Tick);
        i++;
      }
      finally
      {
        trace(i + "~;");
      }
    }
  }

  //NOTE: what a 'for' would lower to if translated literally - kept to show it's wrong
  static IEnumerator<Tick> BranchB_NaiveFor(Action<string> trace)
  {
    for(int i = 0; i < 3; i++)
    {
      try
      {
        trace(i + ";");
        yield return default(Tick);
      }
      finally
      {
        trace(i + "~;");
      }
    }
  }

  static string Run(Func<Action<string>, IEnumerator<Tick>> branch_b = null)
  {
    var log = new StringBuilder();
    Action<string> trace = s => log.Append(s);

    branch_b = branch_b ?? BranchB;
    var test = Paral(BranchA(), branch_b(trace));
    while(test.MoveNext())
    {}
    test.Dispose();

    return log.ToString();
  }

  [Fact]
  public void TestMatchesTheVM()
  {
    //NOTE: the exact string tests/test_paral.cs asserts for the same script
    Assert.Equal("0;1~;1;2~;2;2~;", Run());
  }

  //NOTE: C# runs a for's finally before the increment, BHL's defer sees it after - so a 'for'
  //      carrying a defer can't lower to a C# 'for'
  [Fact]
  public void TestNaiveForLoweringDisagrees()
  {
    Assert.Equal("0;0~;1;1~;2;2~;", Run(BranchB_NaiveFor));
    Assert.NotEqual("0;1~;1;2~;2;2~;", Run(BranchB_NaiveFor));
  }
}
