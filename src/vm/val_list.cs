using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace bhl
{

//NOTE: in case of IList implementation ValList doesn't apply owning semantics
public class ValList : IList<Val>, IList, IRefcounted
{
  //TODO: use raw array?
  List<Val> lst = new List<Val>();

  //NOTE: -1 means it's in released state,
  //      public only for quick inspection
  public int _refs;

  public int refs => _refs;

  public VM vm;

  //////////////////IList//////////////////

  public void CopyTo(Array array, int index)
  {
    throw new NotImplementedException();
  }

  public int Count
  {
    get { return lst.Count; }
  }

  public bool IsFixedSize
  {
    get { return false; }
  }

  public bool IsReadOnly
  {
    get { return false; }
  }

  public bool IsSynchronized
  {
    get { throw new NotImplementedException(); }
  }

  public object SyncRoot
  {
    get { throw new NotImplementedException(); }
  }

  public void Add(Val v)
  {
    lst.Add(v);
  }

  public void AddRange(IList<Val> list)
  {
    for(int i = 0; i < list.Count; ++i)
      Add(list[i]);
  }

  public void Remove(object value)
  {
    lst.Remove((Val)value);
  }

  public void RemoveAt(int idx)
  {
    var dv = lst[idx];
    dv._refc?.Release();
    lst.RemoveAt(idx);
  }

  public int Add(object value)
  {
    return ((IList)lst).Add(value);
  }

  public void Clear()
  {
    for(int i = 0; i < Count; ++i)
      lst[i]._refc?.Release();

    lst.Clear();
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

  object IList.this[int index]
  {
    get => lst[index];
    set => lst[index] = (Val)value;
  }

  public Val this[int i]
  {
    get { return lst[i]; }
    set { lst[i] = value; }
  }

  //NOTE: we don't Retain the added value, it's the caller's responsibility
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void ReplaceAt(int i, Val v)
  {
    var span = CollectionsMarshal.AsSpan(lst);
    ref var curr = ref span[i];
    var refc = curr._refc;
    curr.ValueCopyFrom(v);
    refc?.Release();
  }

  public int IndexOf(Val v)
  {
    return lst.IndexOf(v);
  }

  public bool Contains(Val v)
  {
    return IndexOf(v) >= 0;
  }

  public bool Remove(Val v)
  {
    int idx = IndexOf(v);
    if(idx < 0)
      return false;
    RemoveAt(idx);
    return true;
  }

  public void CopyTo(Val[] array, int start_idx)
  {
    int target_size = Count - start_idx;
    if(target_size > array.Length)
      throw new ArgumentException(string.Format("Array size {0} less target size {1}", array.Length, target_size));

    for(int i = start_idx, j = 0; i < Count; i++, j++)
    {
      var tmp = array[j];
      array[j] = this[i];
      tmp._refc?.Release();
    }
  }

  public void Insert(int pos, Val v)
  {
    lst.Insert(pos, v);
  }

  public IEnumerator<Val> GetEnumerator()
  {
    return lst.GetEnumerator();
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  ///////////////////////////////////////

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

  public void CopyFrom(ValList lst)
  {
    Clear();
    for(int i = 0; i < lst.Count; ++i)
      Add(lst[i].CloneValue());
  }

  ///////////////////////////////////////

  //NOTE: use New() instead
  internal ValList(VM vm)
  {
    this.vm = vm;
  }

  public static ValList New(VM vm)
  {
    ValList lst;
    if(vm.vlsts_pool.stack.Count == 0)
    {
      ++vm.vlsts_pool.miss;
      lst = new ValList(vm);
    }
    else
    {
      ++vm.vlsts_pool.hits;
      lst = vm.vlsts_pool.stack.Pop();

      if(lst._refs != -1)
        throw new Exception("Expected to be released, refs " + lst._refs);
    }

    lst._refs = 1;

    return lst;
  }

  static void Del(ValList lst)
  {
    if(lst._refs != 0)
      throw new Exception("Freeing invalid object, refs " + lst._refs);

    lst._refs = -1;
    lst.Clear();
    lst.vm.vlsts_pool.stack.Push(lst);

    if(lst.vm.vlsts_pool.stack.Count > lst.vm.vlsts_pool.miss)
      throw new Exception("Unbalanced New/Del");
  }
}

}
