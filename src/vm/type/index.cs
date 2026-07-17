using System;
using System.Collections.Generic;
using bhl.marshall;

namespace bhl
{

public class TypeRefIndex
{
  internal List<ProxyType> all = new List<ProxyType>();

  public int Count
  {
    get { return all.Count; }
  }

  bool TryAdd(ProxyType v)
  {
    if(FindIndex(v) == -1)
    {
      all.Add(v);
      return true;
    }

    return false;
  }

  public void Index(ProxyType v)
  {
    if(!TryAdd(v))
      return;

    if(v.Get() is ITypeRefIndexable itr)
      itr.IndexTypeRefs(this);
  }

  public void Index(IType v)
  {
    Index(new ProxyType(v));
  }

  public void Index(Symbol s)
  {
    if(s is IType itype)
      Index(itype);
    else if(s is ITypeRefIndexable itr)
      itr.IndexTypeRefs(this);
  }

  public void Index(IList<ProxyType> vs)
  {
    foreach(var v in vs)
      Index(v);
  }

  public void Index(IList<Symbol> vs)
  {
    foreach(var v in vs)
      Index(v);
  }

  public void Index<T>(TypeSet<T> vs) where T : class, IType
  {
    for(int i = 0; i < vs.Count; i++)
      Index(vs[i]);
  }

  public int FindIndex(ProxyType v)
  {
    return all.IndexOf(v);
  }

  public int GetIndex(ProxyType v)
  {
    int idx = FindIndex(v);
    if(idx == -1)
      throw new Exception("Not found index for type '" + v + "', total entries " + all.Count);
    return idx;
  }

  public ProxyType Get(int idx)
  {
    return all[idx];
  }

  public void SetAt(int idx, ProxyType v)
  {
    //let's fill the gap if any
    for(int i = all.Count - 1; i <= idx; ++i)
      all.Add(new ProxyType());

    all[idx] = v;
  }

  public bool IsValid(int idx)
  {
    return idx >= 0 && idx < all.Count && !all[idx].IsNull();
  }
}

public class TypeSet<T> : IMarshallable where T : class, IType
{
  //TODO: since TypeProxy implements custom Equals we could use HashSet here
  //NOTE: lazily allocating list memory only if it's really required
  List<ProxyType> list;

  public int Count
  {
    get { return list?.Count ?? 0; }
  }

  public T this[int index]
  {
    get
    {
      var tp = list[index];
      var s = (T)tp.Get();
      if(s == null)
        throw new Exception("Type not found: " + tp);
      return s;
    }
  }

  public bool Add(T t)
  {
    return Add(new ProxyType(t));
  }

  public bool Add(ProxyType tp)
  {
    if(list == null)
      list = new List<ProxyType>();

    if(list.IndexOf(tp) != -1)
      return false;
    list.Add(tp);
    return true;
  }

  public void Clear()
  {
    list?.Clear();
  }

  public void Sync(SyncContext ctx)
  {
    if(ctx.is_read && list == null)
      list = new List<ProxyType>();

    Marshall.SyncTypeRefs(ctx, list);

    //let's not occupy memory if it's not really required
    if(ctx.is_read && list.Count == 0)
      list = null;
  }
}

}
