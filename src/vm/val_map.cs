using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace bhl
{

public class ValMap : IDictionary<ValOld, ValOld>, IValRefcounted
{
  //NOTE: Since we track the lifetime of the key as well as of a value
  //      we need to efficiently access the added key, for this reason
  //      we store the key alongside with the value in a KeyValuePair
  Dictionary<ValOld, KeyValuePair<ValOld, ValOld>> map =
    new Dictionary<ValOld, KeyValuePair<ValOld, ValOld>>(new Comparer());

  //NOTE: -1 means it's in released state,
  //      public only for quick inspection
  public int _refs;

  public int refs => _refs;

  public VM vm;

  //TODO: make it 'poolable' in the future
  public class Enumerator : IDictionaryEnumerator, IEnumerator<KeyValuePair<ValOld, ValOld>>
  {
    Dictionary<ValOld, KeyValuePair<ValOld, ValOld>>.Enumerator en;

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
      get { return en.Current.Value.Value; }
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

    KeyValuePair<ValOld, ValOld> IEnumerator<KeyValuePair<ValOld, ValOld>>.Current
    {
      get
      {
        var tmp = en.Current;
        return tmp.Value;
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

  public ICollection<ValOld> Keys
  {
    get { return map.Keys; }
  }

  public ICollection<ValOld> Values
  {
    get { throw new NotImplementedException(); }
  }

  public void Add(KeyValuePair<ValOld, ValOld> p)
  {
    throw new NotImplementedException();
  }

  public void Add(ValOld k, ValOld v)
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

  public ValOld this[ValOld k]
  {
    get { return map[k].Value; }
    set { map[k] = new KeyValuePair<ValOld, ValOld>(k, value); }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetValueCopyAt(ValOld k, ValOld value)
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
      map[k] = new KeyValuePair<ValOld, ValOld>(k, value.CloneValue());
    }
  }

  public bool TryGetValue(ValOld k, out ValOld v)
  {
    bool yes = map.TryGetValue(k, out var p);
    v = p.Value;
    return yes;
  }

  public bool Contains(KeyValuePair<ValOld, ValOld> p)
  {
    throw new NotImplementedException();
  }

  public bool ContainsKey(ValOld k)
  {
    return map.ContainsKey(k);
  }

  public bool Remove(ValOld k)
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

  public bool Remove(KeyValuePair<ValOld, ValOld> p)
  {
    throw new NotImplementedException();
  }

  public void CopyTo(KeyValuePair<ValOld, ValOld>[] arr, int len)
  {
    throw new NotImplementedException();
  }

  public IEnumerator<KeyValuePair<ValOld, ValOld>> GetEnumerator()
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

  class Comparer : IEqualityComparer<ValOld>
  {
    public bool Equals(ValOld a, ValOld b)
    {
      if(a == null && b == null)
        return true;
      else if(a == null || b == null)
        return false;

      return a.IsValueEqual(b);
    }

    public int GetHashCode(ValOld v)
    {
      return v.GetValueHashCode();
    }
  }
}

}