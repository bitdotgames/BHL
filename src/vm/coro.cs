using System;
using System.Collections.Generic;

namespace bhl {

public interface ICoroutine
{
  void Tick(VM.Frame frm, VM.ExecState exec, ref BHS status);
  void Cleanup(VM.Frame frm, VM.ExecState exec);
}

public interface IBranchyCoroutine : ICoroutine
{
  void Attach(ICoroutine branch);
}

public interface IInspectableCoroutine 
{
  int Count { get; }
  ICoroutine At(int i);
}

public abstract class Coroutine : ICoroutine
{
  internal VM.Pool<Coroutine> pool;

  public abstract void Tick(VM.Frame frm, VM.ExecState exec, ref BHS status);
  public virtual void Cleanup(VM.Frame frm, VM.ExecState exec) {}
}

public class CoroutinePool
{
  //TODO: add debug inspection for concrete types
  static class PoolHolder<T> where T : Coroutine
  {
    //alternative implemenation
    //[ThreadStatic]
    //static public VM.Pool<Coroutine> _pool;
    //static public VM.Pool<Coroutine> pool {
    //  get {
    //    if(_pool == null) 
    //      _pool = new VM.Pool<Coroutine>(); 
    //    return _pool;
    //  }
    //}

    public static System.Threading.ThreadLocal<VM.Pool<Coroutine>> pool =
      new System.Threading.ThreadLocal<VM.Pool<Coroutine>>(() =>
      {
        return new VM.Pool<Coroutine>();
      });
  }

  internal int hits;
  internal int miss;
  internal int news;
  internal int dels;

  public int HitCount {
    get { return hits; }
  }

  public int MissCount {
    get { return miss; }
  }

  public int DelCount {
    get { return dels; }
  }

  public int NewCount {
    get { return news; }
  }

#if BHL_TEST
  public HashSet<VM.Pool<Coroutine>> pools_tracker = new HashSet<VM.Pool<Coroutine>>(); 
#endif

  static public T New<T>(VM vm) where T : Coroutine, new()
  {
    var pool = PoolHolder<T>.pool.Value;

    Coroutine coro = null;
    if(pool.stack.Count == 0)
    {
      ++pool.miss;
      ++vm.coro_pool.miss;
      coro = new T();
    }
    else
    {
      ++pool.hits;
      ++vm.coro_pool.hits;
      coro = pool.stack.Pop();
    }

    ++vm.coro_pool.news;

    coro.pool = pool;

#if BHL_TEST
    vm.coro_pool.pools_tracker.Add(pool);
#endif

    return (T)coro;
  }

  static public void Del(VM.Frame frm, VM.ExecState exec, Coroutine coro)
  {
    if(coro == null)
      return;

    var pool = coro.pool;

    coro.Cleanup(frm, exec);

    ++frm.vm.coro_pool.dels;
    pool.stack.Push(coro);

    if(pool.stack.Count > pool.miss)
      throw new Exception("Unbalanced New/Del " + pool.stack.Count + " " + pool.miss);
  }

  static public void Dump(ICoroutine coro, int level = 0)
  {
    if(level == 0)
      Console.WriteLine("<<<<<<<<<<<<<<<");

    string str = new String(' ', level);
    Console.WriteLine(str + coro.GetType().Name + " " + coro.GetHashCode());

    if(coro is IInspectableCoroutine ti)
    {
      for(int i=0;i<ti.Count;++i)
        Dump(ti.At(i), level + 1);
    }

    if(level == 0)
      Console.WriteLine(">>>>>>>>>>>>>>>");
  }
}

}
