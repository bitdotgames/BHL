using System;
using System.Collections.Generic;

namespace bhl
{

public interface ICoroutine
{
  void Tick(VM.ExecState exec);
  void Destruct(VM.ExecState exec);
}

public abstract class Coroutine : ICoroutine
{
  internal VM vm;
  internal Pool<Coroutine> pool;

  public abstract void Tick(VM.ExecState exec);

  public virtual void Destruct(VM.ExecState exec)
  {}
}

public class CoroutinePool
{
  //TODO: add debug inspection for concrete types?
  static class PoolHolder<T> where T : Coroutine
  {
    [ThreadStatic] static public Pool<Coroutine> _pool;

    static public Pool<Coroutine> pool
    {
      get
      {
        if(_pool == null)
          _pool = new Pool<Coroutine>(32);
        return _pool;
      }
    }
  }

  internal int hits;
  internal int miss;
  internal int news;
  internal int dels;

  public int HitCount
  {
    get { return hits; }
  }

  public int MissCount
  {
    get { return miss; }
  }

  public int DelCount
  {
    get { return dels; }
  }

  public int NewCount
  {
    get { return news; }
  }

#if BHL_TEST
  public HashSet<Pool<Coroutine>> pools_tracker = new HashSet<Pool<Coroutine>>();
#endif

  static public T New<T>(VM vm) where T : Coroutine, new()
  {
    var pool = PoolHolder<T>.pool;

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

    coro.vm = vm;
    coro.pool = pool;

#if BHL_TEST
    vm.coro_pool.pools_tracker.Add(pool);
#endif

    return (T)coro;
  }

  static public void Del(VM.ExecState exec, Coroutine coro)
  {
    if(coro == null)
      return;

    var pool = coro.pool;

    coro.Destruct(exec);

    ++coro.vm.coro_pool.dels;
    pool.stack.Push(coro);

    if(pool.stack.Count > pool.miss)
      throw new Exception("Unbalanced New/Del " + pool.stack.Count + " " + pool.miss);
  }
}

}
