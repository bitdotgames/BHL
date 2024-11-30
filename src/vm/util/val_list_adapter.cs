using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace bhl {
  
//TODO: writing is not implemented
public class ValList<T> : IList<T>, IValRefcounted, IDisposable
{
  internal Pool<ValList<T>> pool;
   
  Func<Val, T> val2native;
  
  ValList lst;

  public int Count => lst.Count;
  
  public bool IsReadOnly => false;

  //NOTE: -1 means it's in released state,
  //      public only for quick inspection
  internal int _refs;
   
  public int refs => _refs; 
  
  [ThreadStatic]
  static Pool<ValList<T>> _pool;

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static Pool<ValList<T>> GetPool()
  {
    if(_pool == null) 
      _pool = new Pool<ValList<T>>(); 
    return _pool;
  }

  static public ValList<T> New(ValList lst, Func<Val, T> val2native)
  {
    var pool = GetPool();

    ValList<T> vls = null;
    if(pool.stack.Count == 0)
    {
      ++pool.miss;
      vls = new ValList<T>(lst, val2native);
    }
    else
    {
      ++pool.hits;
      vls = pool.stack.Pop();
    }

    vls._refs = 1;
    vls.lst = lst;
    vls.val2native = val2native;
    vls.pool = pool;

    return vls;
  }

  public ValList(ValList lst, Func<Val, T> val2native)
  {
    this.lst = lst;
    this.val2native = val2native;
  }
  
  public void Dispose()
  {
    Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public T At(int idx)
  {
    var res = val2native(lst[idx]);
    return res;
  }
  
  public void Retain()
  {
    if(_refs == -1)
      throw new Exception("Invalid state(-1)");
    ++_refs;
    lst.Retain();
  }

  public void Release()
  {
    if(_refs == -1)
      throw new Exception("Invalid state(-1)");
    if(_refs == 0)
      throw new Exception("Double free(0)");

    --_refs;
    lst.Release();
    if(_refs == 0)
      Del(this);
  }
 
  static void Del(ValList<T> lst)
  {
    if(lst._refs != 0)
      throw new Exception("Freeing invalid object, refs " + lst._refs);

    lst._refs = -1;
    
    lst.pool.stack.Push(lst);
  } 
  
  public struct Enumerator<T1> : IEnumerator<T1>
  {
    public T1 Current => target.At(idx);
    private int idx;
    private ValList<T1> target;

    public Enumerator(ValList<T1> target)
    {
      this.target = target;
      idx = -1;
    }

    public bool MoveNext() => ++idx < target.Count;

    public void Reset()
    {
      idx = -1;
    }

    object IEnumerator.Current => Current;

    public void Dispose() { }
  }
  
  public IEnumerator<T> GetEnumerator()
  {
    return new Enumerator<T>(this);
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  public void Add(T item)
  {
    throw new NotImplementedException();
  }

  public void Clear()
  {
    lst.Clear();
  }

  public bool Contains(T item)
  {
    return IndexOf(item) != -1;
  }

  public void CopyTo(T[] array, int start_idx)
  {
    int target_size = Count - start_idx;
    if(target_size > array.Length)
      throw new ArgumentException(string.Format("Array size {0} less target size {1}", array.Length, target_size));

    for(int i = start_idx, j = 0; i < Count; i++, j++)
      array[j] = this[i];
  }

  public bool Remove(T item)
  {
    int idx = IndexOf(item);
    if(idx < 0)
      return false;
    RemoveAt(idx);
    return true;
  }

  public int IndexOf(T item)
  {
    for(int i=0; i<Count; ++i)
    {
      if(this[i].Equals(item))
        return i;
    }
    return -1;
  }

  public void Insert(int idx, T item)
  {
    throw new NotImplementedException();
  }

  public void RemoveAt(int idx)
  {
    lst.RemoveAt(idx);
  }

  public T this[int idx]
  {
    get => At(idx);
    set => throw new NotImplementedException();
  }
}
    
}
