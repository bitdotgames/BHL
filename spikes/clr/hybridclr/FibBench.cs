using System;
using System.Collections.Generic;
using System.Diagnostics;

// Drop this into a HybridCLR hot-update assembly and call FibBench.Run(). It times the same
// two shapes bench/bench_fib.cs does, so the numbers line up with the ones measured on the
// JIT: plain C# (what a non-coro BHL func compiles to) and the IEnumerator lowering (what a
// 'coro func' compiles to - see spikes/clr/README.md).
//
// The question it answers: how much of the CLR backend's speed survives being interpreted by
// HybridCLR instead of JIT'd, since IL2CPP has no JIT. If little survives, the whole
// direction costs a dependency and buys only interop.
public static class FibBench
{
  public class Ret { public double val; }
  public struct Tick {}

  static double fib_plain(double x)
  {
    if(x == 0) return 0;
    if(x == 1) return 1;
    return fib_plain(x - 1) + fib_plain(x - 2);
  }

  static IEnumerator<Tick> fib_coro(double x, Ret ret)
  {
    if(x == 0) { ret.val = 0; yield break; }
    if(x == 1) { ret.val = 1; yield break; }

    var r1 = new Ret();
    var co1 = fib_coro(x - 1, r1);
    try { while(co1.MoveNext()) yield return default(Tick); }
    finally { co1.Dispose(); }

    var r2 = new Ret();
    var co2 = fib_coro(x - 2, r2);
    try { while(co2.MoveNext()) yield return default(Tick); }
    finally { co2.Dispose(); }

    ret.val = r1.val + r2.val;
  }

  static void RunCoroOnce(int n)
  {
    var ret = new Ret();
    var co = fib_coro(n, ret);
    while(co.MoveNext())
    {}
    co.Dispose();
  }

  //NOTE: best of several, like the C++ prototypes are measured - a single pass on a cold
  //      interpreter says nothing
  static double BestUs(Action body, int iters, int rounds)
  {
    double best = double.MaxValue;
    for(int r = 0; r < rounds; ++r)
    {
      var sw = Stopwatch.StartNew();
      for(int i = 0; i < iters; ++i)
        body();
      sw.Stop();

      double us = sw.Elapsed.TotalMilliseconds * 1000.0 / iters;
      if(us < best)
        best = us;
    }
    return best;
  }

  public static string Run(int n = 15, int iters = 2000, int rounds = 5)
  {
    //NOTE: warm up first, the interpreter caches per method
    fib_plain(n);
    RunCoroOnce(n);

    double plain = BestUs(() => fib_plain(n), iters, rounds);
    double coro = BestUs(() => RunCoroOnce(n), iters, rounds);

    return $"fib({n})  plain {plain:F2} us/op   coro-style {coro:F2} us/op";
  }
}
