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

  //TODO: make it 'poolable' in the future
  public class Enumerator : IDictionaryEnumerator, IEnumerator<KeyValuePair<Val, Val>>
  {
    Dictionary<Val, Val>.Enumerator en;

    public Enumerator(ValMap m)
    {
      en = m.map.GetEnumerator();
    }

    public DictionaryEntry Entry
    {
      get { throw new NotImplementedException(); }
    }

    public object Current
    {
      get { throw new NotImplementedException(); }
    }

    public object Value
    {
      get { return en.Current.Value; }
    }

    public object Key
    {
      get { return en.Current.Key; }
    }

    public bool MoveNext()
    {
      return en.MoveNext();
    }

    public void Reset()
    {
      throw new NotImplementedException();
    }

    KeyValuePair<Val, Val> IEnumerator<KeyValuePair<Val, Val>>.Current
    {
      get
      {
        return en.Current;
      }
    }

    public void Dispose()
    {
      en.Dispose();
    }
  }

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
      en.Current.Key.ReleaseData();
      en.Current.Value.ReleaseData();
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
      curr._refc?.Release();
      map[k] = v.Clone();
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
    return new Enumerator(this);
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return new Enumerator(this);
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
