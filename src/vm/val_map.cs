using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace bhl
{

public class ValMap : IDictionary<Val, Val>, IRefcounted
{
  Dictionary<Val, Val> map = new Dictionary<Val, Val>(new Comparer());

  //NOTE: -1 means it's in released state,
  //      public only for quick inspection
  public int _refs;

  public int refs => _refs;

  public VM vm;

  //////////////////IDictionary//////////////////

  public int Count
  {
    get { return map.Count; }
  }

  public bool IsReadOnly
  {
    get { return false; }
  }

  public ICollection<Val> Keys
  {
    get { return map.Keys; }
  }

  public ICollection<Val> Values
  {
    get { throw new NotImplementedException(); }
  }

  public void Add(KeyValuePair<Val, Val> p)
  {
    throw new NotImplementedException();
  }

  public void Add(Val k, Val v)
  {
    throw new NotImplementedException();
  }

  public void Clear()
  {
    var en = map.GetEnumerator();
    while(en.MoveNext())
    {
      var current = en.Current;
      current.Key.ReleaseData();
      current.Value.ReleaseData();
    }

    map.Clear();
  }

  public Val this[Val k]
  {
    get { return map[k]; }
    //NOTE: no refcounting happens here
    set { map[k] = value; }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void ReplaceAt(Val k, Val v)
  {
    //NOTE: we are going to re-use the existing k/v,
    //      thus we need to decrease/increase user payload
    //      refcounts properly
    if(map.TryGetValue(k, out var curr))
    {
      map[k] = v.Clone();
      curr._refc?.Release();
    }
    else
    {
      map[k.Clone()] = v.Clone();
    }
  }

  public bool TryGetValue(Val k, out Val v)
  {
    return map.TryGetValue(k, out v);
  }

  public bool Contains(KeyValuePair<Val, Val> p)
  {
    throw new NotImplementedException();
  }

  public bool ContainsKey(Val k)
  {
    return map.ContainsKey(k);
  }

  public bool Remove(Val k)
  {
    bool existed = map.TryGetValue(k, out var prev);
    bool removed = map.Remove(k);
    if(existed)
    {
      k.ReleaseData();
      prev.ReleaseData();
    }

    return removed;
  }

  public bool Remove(KeyValuePair<Val, Val> p)
  {
    throw new NotImplementedException();
  }

  public void CopyTo(KeyValuePair<Val, Val>[] arr, int len)
  {
    throw new NotImplementedException();
  }

  public IEnumerator<KeyValuePair<Val, Val>> GetEnumerator()
  {
    return map.GetEnumerator();
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return map.GetEnumerator();
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

  //NOTE: use New() instead
  internal ValMap(VM vm)
  {
    this.vm = vm;
  }

  public static ValMap New(VM vm)
  {
    ValMap map;
    if(vm.vmaps_pool.stack.Count == 0)
    {
      ++vm.vmaps_pool.miss;
      map = new ValMap(vm);
    }
    else
    {
      ++vm.vmaps_pool.hits;
      map = vm.vmaps_pool.stack.Pop();

      if(map._refs != -1)
        throw new Exception("Expected to be released, refs " + map._refs);
    }

    map._refs = 1;

    return map;
  }

  static void Del(ValMap map)
  {
    if(map._refs != 0)
      throw new Exception("Freeing invalid object, refs " + map._refs);

    map._refs = -1;
    map.Clear();
    map.vm.vmaps_pool.stack.Push(map);

    if(map.vm.vmaps_pool.stack.Count > map.vm.vmaps_pool.miss)
      throw new Exception("Unbalanced New/Del");
  }

  class Comparer : IEqualityComparer<Val>
  {
    public bool Equals(Val a, Val b)
    {
      return a.IsDataEqual(ref b);
    }

    public int GetHashCode(Val v)
    {
      return v.GetDataHashCode();
    }
  }
}

}
