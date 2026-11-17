using System;
using System.Collections.Generic;

namespace bhl
{

//NOTE: the non-generic base exists for the counter: a leak check wants one number across
//      every held type, not one per T. It's thread local like the pool, so it tracks whatever
//      ran on this thread - with VMs ticked from one thread, that's what a leak check needs
public abstract class ValHolder
{
  //NOTE: no counter of its own - a pool already knows how many it handed out and never got
  //      back, that's BusyCount. Registered once per held type, so renting stays free
  [ThreadStatic] static List<Func<int>> _busy_counts;

  protected static void TrackPool(Func<int> busy_count)
  {
    if(_busy_counts == null)
      _busy_counts = new List<Func<int>>();
    _busy_counts.Add(busy_count);
  }

  //NOTE: holders rented and not yet returned, across every held type. Zero between runs, or
  //      something leaked
  public static int Outstanding
  {
    get
    {
      int n = 0;
      if(_busy_counts != null)
        for(int i = 0; i < _busy_counts.Count; ++i)
          n += _busy_counts[i]();
      return n;
    }
  }
}

//NOTE: holds a native struct inside a Val's 'obj' slot, for structs that can't be encoded in
//      Val's numeric slots - anything holding a reference (those slots aren't GC scanned) or
//      simply too big for them.
//
//      Val.Clone() copies the numeric slots but shares 'obj', so a struct kept there would get
//      reference semantics: 'b = a' followed by a write to b would be visible through a. To
//      keep it behaving like a struct, a field write copies instead of mutating in place -
//      but only when somebody else can observe it. The refcount already answers that: at one
//      reference nothing else points here, so the write happens in place and costs nothing.
//
//      Bindings go through MutableRef()/Ref() rather than casting Val.obj themselves, so the
//      representation stays an implementation detail and the retain/release around a copy
//      lives in one place (Val.SetObj neither retains the new value nor releases the old).
public class ValHolder<T> : ValHolder, IRefcounted where T : struct
{
  public T v;

  internal int _refs;
  public int refs => _refs;

  //NOTE: thread local, like every other pooled type here, so two VMs on different threads
  //      never share one. Two VMs on the same thread do, on purpose: a reloaded VM inherits a
  //      warm pool instead of allocating its own from cold
  [ThreadStatic] static Pool<ValHolder<T>> _pool;

  static Pool<ValHolder<T>> GetPool()
  {
    if(_pool == null)
    {
      //NOTE: the instance is captured, not the thread static field - another thread asking for
      //      Outstanding must not end up reading its own pool through this
      var pool = new Pool<ValHolder<T>>(32);
      _pool = pool;
      TrackPool(() => pool.BusyCount);
    }
    return _pool;
  }

  public static Pool<ValHolder<T>> Pool => GetPool();

  //NOTE: use New() instead
  internal ValHolder()
  {}

  public static ValHolder<T> New()
  {
    var pool = GetPool();

    ValHolder<T> h;
    if(pool.stack.Count == 0)
    {
      ++pool.miss;
      h = new ValHolder<T>();
    }
    else
    {
      ++pool.hits;
      h = pool.stack.Pop();

      if(h._refs != -1)
        throw new Exception("Expected to be released, refs " + h._refs);
    }

    h._refs = 1;
    h.v = default(T);

    return h;
  }

  static void Del(ValHolder<T> h)
  {
    if(h._refs != 0)
      throw new Exception("Freeing invalid object, refs " + h._refs);

    h._refs = -1;
    h.v = default(T);
    //NOTE: back to this thread's pool rather than the one it came from - an object rented on
    //      another thread would otherwise have two threads pushing into the same stack
    GetPool().stack.Push(h);
  }

  public void Retain()
  {
    if(_refs == -1)
      throw new Exception("Invalid state(-1)");
    ++_refs;
  }

  public void Release()
  {
    if(_refs == -1)
      throw new Exception("Invalid state(-1)");
    if(_refs == 0)
      throw new Exception("Double free(0)");

    --_refs;
    if(_refs == 0)
      Del(this);
  }

  ///////////////////////////////////////

  public static Val NewVal(IType type, T v)
  {
    var h = New();
    h.v = v;
    return Val.NewObj(h, type);
  }

  //NOTE: reading never copies - the holder is a class, so this is a ref into the heap object
  public static ref T Ref(Val ctx)
  {
    return ref ((ValHolder<T>)ctx.obj).v;
  }

  //NOTE: hands back a struct nobody else observes. Shared means somebody could see the write,
  //      so it gets its own copy first; unshared writes go straight through
  public static ref T MutableRef(ref Val ctx)
  {
    var h = (ValHolder<T>)ctx.obj;

    if(h._refs == 1)
      return ref h.v;

    var fresh = New();
    fresh.v = h.v;

    //NOTE: SetObj is 'this = NewObj(..)' - it neither retains what it's given nor releases
    //      what was there, so ctx's own reference has to be handed back by hand
    ctx.SetObj(fresh, ctx.type);
    h.Release();

    return ref fresh.v;
  }
}

}
