using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {
  
//TODO: not fully implemented
public class ValList<T> : IList<T>, IValRefcounted, IDisposable
{
  internal VM.Pool<ValList<T>> pool;
   
  Func<Val, T> val2native;
  
  ValList lst;

  public int Count => lst.Count;
  
  public bool IsReadOnly => false;

  //NOTE: -1 means it's in released state,
  //      public only for quick inspection
  internal int _refs;
   
  public int refs => _refs; 
  
  static class PoolHolder<T1>
  {
    public static System.Threading.ThreadLocal<VM.Pool<ValList<T1>>> pool =
      new System.Threading.ThreadLocal<VM.Pool<ValList<T1>>>(() =>
      {
        return new VM.Pool<ValList<T1>>();
      });
  }

  static public ValList<T> New(ValList lst, Func<Val, T> val2native)
  {
    var pool = PoolHolder<T>.pool.Value;

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
    throw new NotImplementedException();
  }

  public void CopyTo(T[] array, int start_idx)
  {
    int target_size = Count - start_idx;
    if(target_size > array.Length)
      throw new ArgumentException(string.Format("Array size {0} less target size {1}", array.Length, target_size));

    for (int i = start_idx, j = 0; i < Count; i++, j++)
      array[j] = this[i];
  }

  public bool Remove(T item)
  {
    throw new NotImplementedException();
  }

  public int IndexOf(T item)
  {
    throw new NotImplementedException();
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
