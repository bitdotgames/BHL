using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace bhl
{

//TODO: Not all interfaces are fully implemented
//NOTE: StackList is passed by value since it's a struct
public struct StackList<T> : IList<T>, IReadOnlyList<T>, IList
{
  const int StackCapacity = 16;

  Array16<T> storage;
  List<T> fallback;

  public struct Enumerator<T1> : IEnumerator<T1>
  {
    public T1 Current => target[index];
    private int index;
    private StackList<T1> target;

    public Enumerator(StackList<T1> target)
    {
      this.target = target;
      index = -1;
    }

    public bool MoveNext() => ++index < target.Count;

    public void Reset()
    {
      index = -1;
    }

    object IEnumerator.Current => Current;

    public void Dispose()
    {
    }
  }

  public void CopyTo(Array array, int index)
  {
    throw new NotImplementedException();
  }

  public int Count { get ; private set; }
  public bool IsSynchronized { get; }
  public object SyncRoot { get; }

  public bool IsFixedSize
  {
    get { return false; }
  }

  public bool IsReadOnly
  {
    get { return false; }
  }

  object IList.this[int index]
  {
    get => this[index];
    set => this[index] = (T)value;
  }

  public StackList(IList<T> list)
    : this()
  {
    storage = new Array16<T>();
    AddRange(list);
  }

  //convenience special case
  public StackList(T v)
    : this()
  {
    storage = new Array16<T>();
    Add(v);
  }

  //convenience special case
  public StackList(T v1, T v2)
    : this()
  {
    storage = new Array16<T>();
    Add(v1);
    Add(v2);
  }

  //convenience special case
  public StackList(T v1, T v2, T v3)
    : this()
  {
    storage = new Array16<T>();
    Add(v1);
    Add(v2);
    Add(v3);
  }

  public void AddRange(IList<T> list)
  {
    for(int i = 0; i < list?.Count; ++i)
      Add(list[i]);
  }

  public void Clear()
  {
    if(fallback == null)
    {
      for(int i = 0; i < Count; ++i)
        this[i] = default(T);
    }
    else
      fallback.Clear();

    Count = 0;
  }

  public T this[int i]
  {
    get
    {
      if(fallback == null)
        return storage[i];
      else
        return fallback[i];
    }
    set
    {
      if(fallback == null)
        storage[i] = value;
      else
        fallback[i] = value;
    }
  }

  public void Add(T val)
  {
    Count++;
    if(fallback == null && Count > StackCapacity)
      Fallback();

    if(fallback == null)
      this[Count - 1] = val;
    else
      fallback.Add(val);
  }

  public void RemoveAt(int index)
  {
    if(fallback == null)
    {
      for(int i = 0; i < Count - 1; ++i)
      {
        if(i >= index)
          storage[i] = storage[i + 1];
      }
    }
    else
      fallback.RemoveAt(index);

    Count--;
  }

  void Fallback()
  {
    fallback = new List<T>(2 * StackCapacity);
    for(int i = 0; i < StackCapacity; ++i)
      fallback.Add(storage[i]);
  }

  public int IndexOf(T o)
  {
    if(fallback != null)
      return fallback.IndexOf(o);

    for(int i = 0; i < Count; ++i)
    {
      if(this[i].Equals(o))
        return i;
    }

    return -1;
  }

  public bool Contains(T o)
  {
    return IndexOf(o) >= 0;
  }

  public bool Remove(T o)
  {
    int idx = IndexOf(o);
    if(idx < 0)
      return false;
    RemoveAt(idx);
    return true;
  }

  public void CopyTo(T[] array, int start_idx)
  {
    int target_size = Count - start_idx;
    if(target_size > array.Length)
      throw new ArgumentException(string.Format("Array size {0} less target size {1}", array.Length, target_size));

    for(int i = start_idx, j = 0; i < Count; i++, j++)
      array[j] = this[i];
  }

  public Enumerator<T> GetEnumerator()
  {
    return new Enumerator<T>(this);
  }

  IEnumerator<T> IEnumerable<T>.GetEnumerator()
  {
    return GetEnumerator();
  }

  // not implemented IList<T> interface methods
  public void Insert(int pos, T o)
  {
    throw new NotImplementedException();
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    throw new NotImplementedException();
  }

  // not implemented IList interface methods
  public int Add(object value)
  {
    throw new NotImplementedException();
  }

  public bool Contains(object value)
  {
    throw new NotImplementedException();
  }

  public int IndexOf(object value)
  {
    throw new NotImplementedException();
  }

  public void Insert(int index, object value)
  {
    throw new NotImplementedException();
  }

  public void Remove(object value)
  {
    throw new NotImplementedException();
  }
}

public struct Array16<T>
{
  internal T v0;
  internal T v1;
  internal T v2;
  internal T v3;
  internal T v4;
  internal T v5;
  internal T v6;
  internal T v7;
  internal T v8;
  internal T v9;
  internal T v10;
  internal T v11;
  internal T v12;
  internal T v13;
  internal T v14;
  internal T v15;

  public T this[int i]
  {
    get
    {
      if(i == 0)
        return v0;
      else if(i == 1)
        return v1;
      else if(i == 2)
        return v2;
      else if(i == 3)
        return v3;
      else if(i == 4)
        return v4;
      else if(i == 5)
        return v5;
      else if(i == 6)
        return v6;
      else if(i == 7)
        return v7;
      else if(i == 8)
        return v8;
      else if(i == 9)
        return v9;
      else if(i == 10)
        return v10;
      else if(i == 11)
        return v11;
      else if(i == 12)
        return v12;
      else if(i == 13)
        return v13;
      else if(i == 14)
        return v14;
      else if(i == 15)
        return v15;
      else
      {
        throw new Exception("Too large idx: " + i);
      }
    }
    set
    {
      if(i == 0)
        v0 = value;
      else if(i == 1)
        v1 = value;
      else if(i == 2)
        v2 = value;
      else if(i == 3)
        v3 = value;
      else if(i == 4)
        v4 = value;
      else if(i == 5)
        v5 = value;
      else if(i == 6)
        v6 = value;
      else if(i == 7)
        v7 = value;
      else if(i == 8)
        v8 = value;
      else if(i == 9)
        v9 = value;
      else if(i == 10)
        v10 = value;
      else if(i == 11)
        v11 = value;
      else if(i == 12)
        v12 = value;
      else if(i == 13)
        v13 = value;
      else if(i == 14)
        v14 = value;
      else if(i == 15)
        v15 = value;
      else
      {
        throw new Exception("Too large idx: " + i);
      }
    }
  }
}

}
