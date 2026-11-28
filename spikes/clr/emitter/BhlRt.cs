using System;
using System.Collections.Generic;

namespace bhl.spike.clr
{

// What generated coroutine code runs against. The lowerings here are the ones the probes in
// tests/test_clr_spike.cs established against the VM.
public static class BhlRt
{
  public struct Tick {}

  //NOTE: an IEnumerator can't hand a value back to its caller, so a coro func takes one of
  //      these and the caller reads it afterwards
  public class Ret
  {
    public double val;
  }

  //NOTE: 'yield <coro>' - the callee has to be disposed, or cancelling the caller leaves the
  //      callee's defers unrun and nothing else ever will
  public static IEnumerator<Tick> Drive(IEnumerator<Tick> co)
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

  //NOTE: steps each branch once per tick, ends when one of them does, disposes the rest -
  //      disposing runs their pending finally blocks, which is how a cancelled branch still
  //      runs its defers
  public static IEnumerator<Tick> Paral(params IEnumerator<Tick>[] branches)
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

  //NOTE: waits for all of them instead of the first
  public static IEnumerator<Tick> ParalAll(params IEnumerator<Tick>[] branches)
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

  //NOTE: drives a top level coro to completion, the way the VM ticks a fiber
  public static double Run(IEnumerator<Tick> co, Ret ret)
  {
    while(co.MoveNext())
    {}
    co.Dispose();
    return ret == null ? 0 : ret.val;
  }
}

}
