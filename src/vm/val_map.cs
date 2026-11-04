using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace bhl
{

public class ValMap : IDictionary<Val, Val>, IRefcounted
{
  //NOTE: a value is replaced in the entry, not in the dictionary: Unity's Mono treats overwriting a value
  //      of the dictionary as a modification which breaks an ongoing enumeration (.NET Core doesn't);
  //      the entry also keeps the stored key, it's the one released on removal
  internal class Entry
  {
    public Val key;
    public Val val;
  }

  Dictionary<Val, Entry> map;

  Stack<Entry> free_entries = new Stack<Entry>();

  Comparer comparer = new Comparer();

  public IEqualityComparer<Val> KeyComparer
  {
    get { return comparer.impl; }
    set { comparer.impl = value; }
  }

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
      var e = en.Current.Value;
      e.key.ReleaseData();
      e.val.ReleaseData();
      FreeEntry(e);
    }

    map.Clear();
  }

  public Val this[Val k]
  {
    get { return map[k].val; }
    //NOTE: no refcounting happens here
    set
    {
      if(map.TryGetValue(k, out var e))
        e.val = value;
      else
        AddEntry(k, value);
    }
  }

  void AddEntry(Val k, Val v)
  {
    var e = free_entries.Count > 0 ? free_entries.Pop() : new Entry();
    e.key = k;
    e.val = v;
    map.Add(k, e);
  }

  void FreeEntry(Entry e)
  {
    e.key = default;
    e.val = default;
    free_entries.Push(e);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void ReplaceAt(Val k, Val v)
  {
    //NOTE: we are going to re-use the existing k/v,
    //      thus we need to decrease/increase user payload
    //      refcounts properly
    if(map.TryGetValue(k, out var e))
    {
      var curr = e.val;
      e.val = v.Clone();
      curr._refc?.Release();
    }
    else
    {
      AddEntry(k.Clone(), v.Clone());
    }
  }

  public bool TryGetValue(Val k, out Val v)
  {
    if(map.TryGetValue(k, out var e))
    {
      v = e.val;
      return true;
    }
    v = default;
    return false;
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
    if(!map.TryGetValue(k, out var e))
      return false;

    map.Remove(k);
    //NOTE: the stored key, k may be another object equal to it
    e.key.ReleaseData();
    e.val.ReleaseData();
    FreeEntry(e);
    return true;
  }

  public bool Remove(KeyValuePair<Val, Val> p)
  {
    throw new NotImplementedException();
  }

  public void CopyTo(KeyValuePair<Val, Val>[] arr, int len)
  {
    throw new NotImplementedException();
  }

  public Enumerator GetEnumerator()
  {
    return new Enumerator(map);
  }

  IEnumerator<KeyValuePair<Val, Val>> IEnumerable<KeyValuePair<Val, Val>>.GetEnumerator()
  {
    return GetEnumerator();
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  public struct Enumerator : IEnumerator<KeyValuePair<Val, Val>>
  {
    Dictionary<Val, Entry> map;
    Dictionary<Val, Entry>.Enumerator en;

    internal Enumerator(Dictionary<Val, Entry> map)
    {
      this.map = map;
      en = map.GetEnumerator();
    }

    public KeyValuePair<Val, Val> Current
    {
      get
      {
        var e = en.Current.Value;
        return new KeyValuePair<Val, Val>(e.key, e.val);
      }
    }

    object IEnumerator.Current => Current;

    public bool MoveNext() => en.MoveNext();

    public void Reset() => en = map.GetEnumerator();

    public void Dispose() => en.Dispose();
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
    map = new Dictionary<Val, Entry>(comparer);

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

      //let's restore default comparator
      map.KeyComparer = DefaultComparer.Instance;

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

  public class DefaultComparer : IEqualityComparer<Val>
  {
    public readonly static DefaultComparer Instance = new DefaultComparer();
    public bool Equals(Val a, Val b)
    {
      //NOTE: by both keys, a scalar is never equal to a non scalar (e.g. keys of [any]T)
      bool scalar = Types.IsScalar(a.type);
      if(scalar != Types.IsScalar(b.type))
        return false;
      if(scalar)
        return a.num == b.num;
      else
        return a.obj?.Equals(b.obj) ?? null == b.obj;
    }
    public int GetHashCode(Val v)
    {
      if(Types.IsScalar(v.type))
        return v.num.GetHashCode();
      else
        return v.obj?.GetHashCode() ?? 0;
    }
  }

  class Comparer : IEqualityComparer<Val>
  {
    public IEqualityComparer<Val> impl = DefaultComparer.Instance;

    public bool Equals(Val a, Val b)
    {
      return impl.Equals(a, b);
    }

    public int GetHashCode(Val v)
    {
      return impl.GetHashCode(v);
    }
  }
}

}
