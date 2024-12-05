using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace bhl {

public class ValMap : IDictionary<Val,Val>, IValRefcounted
{
  //NOTE: Since we track the lifetime of the key as well as of a value
  //      we need to efficiently access the added key, for this reason
  //      we store the key alongside with the value in a KeyValuePair
  Dictionary<Val,KeyValuePair<Val, Val>> map = 
    new Dictionary<Val,KeyValuePair<Val,Val>>(new Comparer());

  //NOTE: -1 means it's in released state,
  //      public only for quick inspection
  public int _refs;

  public int refs => _refs; 

  public VM vm;

  //TODO: make it 'poolable' in the future
  public class Enumerator : IDictionaryEnumerator
  {
    Dictionary<Val,KeyValuePair<Val, Val>>.Enumerator en;

    public Enumerator(ValMap m)
      : this(m.map.GetEnumerator())
    {}

    public Enumerator(Dictionary<Val,KeyValuePair<Val, Val>>.Enumerator en)
    {
      this.en = en;
    }

    public DictionaryEntry Entry {
      get {
        throw new NotImplementedException();
      }
    }

    public object Current {
      get {
        throw new NotImplementedException();
      }
    }

    public object Value {
      get {
        return en.Current.Value.Value;
      }
    }

    public object Key {
      get {
        return en.Current.Key;
      }
    }

    public bool MoveNext()
    {
      return en.MoveNext();
    }

    public void Reset()
    {
      throw new NotImplementedException();
    }
  }

  //////////////////IDictionary//////////////////

  public int Count { get { return map.Count; } }

  public bool IsReadOnly { get { return false; } }

  public ICollection<Val> Keys { get { return map.Keys; } }

  public ICollection<Val> Values { get { throw new NotImplementedException(); } }

  public void Add(KeyValuePair<Val,Val> p)
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
      en.Current.Key.Release();
      en.Current.Value.Value.Release();
    }
    map.Clear();
  }

  public Val this[Val k]
  {
    get {
      return map[k].Value;
    }
    set {
      map[k] = new KeyValuePair<Val,Val>(k, value);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetValueCopyAt(Val k, Val value)
  {
    //NOTE: we are going to re-use the existing k/v,
    //      thus we need to decrease/increase user payload
    //      refcounts properly 
    if(map.TryGetValue(k, out var curr))
    {
      curr.Value._refc?.Release();
      curr.Value.ValueCopyFrom(value);
      curr.Value._refc?.Retain();
    }
    else
    {
      k = k.CloneValue();
      map[k] = new KeyValuePair<Val,Val>(k, value.CloneValue());
    }
  }

  public bool TryGetValue(Val k, out Val v)
  {
    bool yes = map.TryGetValue(k, out var p);
    v = p.Value;
    return yes;
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
      prev.Key.Release();
      prev.Value.Release();
    }
    return removed;
  }

  public bool Remove(KeyValuePair<Val,Val> p)
  {
    throw new NotImplementedException();
  }

  public void CopyTo(KeyValuePair<Val,Val>[] arr, int len)
  {
    throw new NotImplementedException();
  }

  public IEnumerator<KeyValuePair<Val,Val>> GetEnumerator()
  {
    throw new NotImplementedException();
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return new Enumerator(map.GetEnumerator());
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
      if(a == null && b == null)
        return true;
      else if(a == null || b == null)
        return false;

      return a.IsValueEqual(b);
    }

    public int GetHashCode(Val v)
    {
      return v.GetValueHashCode();
    }
  }
}

}
