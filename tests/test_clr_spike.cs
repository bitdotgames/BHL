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

  ///////////////////////////////////////////////////
  // paral_all with an early return from a branch:
  //
  //   coro func int test() {
  //     paral_all {
  //       { return 1 }
  //       yield suspend()
  //     }
  //     return 0
  //   }
  //
  //NOTE: a return inside a branch leaves the whole function, even with the other branch
  //      suspended forever. An IEnumerator can't return a value to its caller, so the branch
  //      signals it and the driver propagates - this is the piece 'defer' didn't need
  class Ret
  {
    public bool has;
    public double val;
  }

  static IEnumerator<Tick> Branch_Returns1(Ret ret)
  {
    ret.has = true;
    ret.val = 1;
    yield break;
  }

  static IEnumerator<Tick> Branch_Suspends()
  {
    while(true)
      yield return default(Tick);
  }

  //NOTE: paral_all waits for every branch, unlike paral - but a branch that returns cuts it
  //      short, so the loop has to watch for that as well as for all of them finishing
  static IEnumerator<Tick> ParalAll(Ret ret, params IEnumerator<Tick>[] branches)
  {
    var alive = new bool[branches.Length];
    for(int i = 0; i < alive.Length; ++i)
      alive[i] = true;

    try
    {
      while(true)
      {
        int running = 0;
        for(int i = 0; i < branches.Length; ++i)
        {
          if(!alive[i])
            continue;

          if(branches[i].MoveNext())
            ++running;
          else
            alive[i] = false;

          if(ret.has)
            yield break;
        }

        if(running == 0)
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

  static double RunParalAllReturn()
  {
    var ret = new Ret();
    var test = ParalAll(ret, Branch_Returns1(ret), Branch_Suspends());
    while(test.MoveNext())
    {}
    test.Dispose();

    return ret.has ? ret.val : 0;
  }

  [Fact]
  public void TestParalAllEarlyReturnLeavesTheFunction()
  {
    //NOTE: tests/test_paral.cs asserts 1 for the same script
    Assert.Equal(1, RunParalAllReturn());
  }

  ///////////////////////////////////////////////////
  // defer firing before the value reaches the caller:
  //
  //   func bar(float k) { defer { trace("~BAR") } trace("BAR") }
  //   func float foo()  { defer { trace("~FOO") } return 3  trace("FOO") }
  //   func test()       { bar(foo()) }

  static double Foo(Action<string> trace)
  {
    try
    {
      return 3;
      //NOTE: the trace("FOO") after the return is dead, as it is in the script
    }
    finally
    {
      trace("~FOO");
    }
  }

  static void Bar(Action<string> trace, double k)
  {
    try
    {
      trace("BAR");
    }
    finally
    {
      trace("~BAR");
    }
  }

  [Fact]
  public void TestDeferRunsBeforeTheCallerUsesTheValue()
  {
    var log = new StringBuilder();
    Action<string> trace = x => log.Append(x);

    Bar(trace, Foo(trace));

    //NOTE: tests/test_defer.cs asserts this for the same script
    Assert.Equal("~FOOBAR~BAR", log.ToString());
  }

  ///////////////////////////////////////////////////
  // 'yield doer()' where doer holds a defer and is cancelled while suspended:
  //
  //   coro func doer() {
  //     float k = 1
  //     defer { if(k == 1) { foo(k) } }
  //     yield suspend()
  //     k = 2
  //   }
  //   coro func test() { paral { { yield doer() } { yield() yield() } } }

  static IEnumerator<Tick> Doer(Action<string> trace)
  {
    double k = 1;
    try
    {
      //NOTE: yield suspend() never resumes, so the 'k = 2' after it is unreachable here too
      while(true)
        yield return default(Tick);
    }
    finally
    {
      if(k == 1)
        trace(k.ToString());
    }
  }

  //NOTE: 'yield <coro>' drives the sub coroutine, and has to dispose it - cancelling the
  //      caller otherwise leaves the callee's defers unrun, since nothing else ever will
  static IEnumerator<Tick> YieldCoro(IEnumerator<Tick> co)
  {
    try
    {
      while(co.MoveNext())
        yield return default(Tick);
    }
    finally
    {
      co.Dispose();
    }
  }

  //NOTE: what it looks like without that guard
  static IEnumerator<Tick> YieldCoro_NoDispose(IEnumerator<Tick> co)
  {
    while(co.MoveNext())
      yield return default(Tick);
  }

  static IEnumerator<Tick> TwoYields()
  {
    yield return default(Tick);
    yield return default(Tick);
  }

  static string RunYieldCoro(Func<IEnumerator<Tick>, IEnumerator<Tick>> drive)
  {
    var log = new StringBuilder();
    Action<string> trace = x => log.Append(x);

    var test = Paral(drive(Doer(trace)), TwoYields());
    while(test.MoveNext())
    {}
    test.Dispose();

    return log.ToString();
  }

  [Fact]
  public void TestCancellingReachesTheSubCoroutinesDefer()
  {
    //NOTE: tests/test_defer.cs asserts "1" for the same script
    Assert.Equal("1", RunYieldCoro(YieldCoro));
  }

  [Fact]
  public void TestWithoutDisposingTheSubCoroutineTheDeferIsLost()
  {
    Assert.Equal("", RunYieldCoro(YieldCoro_NoDispose));
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
