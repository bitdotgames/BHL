using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {
  
public class SymbolsStorage : marshall.IMarshallable, IEnumerable<Symbol>
{
  internal IScope scope;
  internal List<Symbol> list = new List<Symbol>();
  //NOTE: used for lookup by name
  Dictionary<string, int> name2idx = new Dictionary<string, int>();

  public int Count
  {
    get {
      return list.Count;
    }
  }

  public Symbol this[int index]
  {
    get {
      return list[index];
    }
  }

  public SymbolsStorage(IScope scope)
  {
    if(scope == null)
      throw new Exception("Scope is null");
    this.scope = scope;
  }

  public bool Contains(string name)
  {
    return Find(name) != null;
  }

  public Symbol Find(string name)
  {
    int idx;
    if(!name2idx.TryGetValue(name, out idx))
      return null;
    return list[idx];
  }

  public void Add(Symbol s)
  {
    if(Find(s.name) != null)
      throw new SymbolError(s, "already defined symbol '" + s.name + "'"); 

    //only assingning scope if it's not assigned yet
    if(s.scope == null)
      s.scope = scope;

    if(s is IScopeIndexed si && si.scope_idx == -1)
      si.scope_idx = list.Count;

    list.Add(s);
    name2idx.Add(s.name, list.Count-1);
  }

  public void RemoveAt(int index)
  {
    var s = list[index];
    if(s.scope == scope)
      s.scope = null;
    list.RemoveAt(index);
    name2idx.Remove(s.name);
  }

  public Symbol TryAt(int index)
  {
    if(index < 0 || index >= list.Count)
      return null;
    return list[index];
  }

  public int IndexOf(Symbol s)
  {
    //TODO: use lookup by name instead?
    return list.IndexOf(s);
  }

  public int IndexOf(string name)
  {
    int idx;
    if(!name2idx.TryGetValue(name, out idx))
      return -1;
    return idx;
  }

  public bool Replace(Symbol what, Symbol subst)
  {
    int idx = IndexOf(what);
    if(idx == -1)
      return false;
    
    list[idx] = subst;

    name2idx.Remove(what.name);
    name2idx[subst.name] = idx;

    return true;
  }

  public void Clear()
  {
    foreach(var s in list)
    {
      if(s.scope == scope)
        s.scope = null;
    }
    list.Clear();
    name2idx.Clear();
  }

  public void Sync(marshall.SyncContext ctx) 
  {
    marshall.Marshall.SyncGeneric(ctx, list);

    if(ctx.is_read)
    {
      for(int idx=0;idx<list.Count;++idx)
      {
        var tmp = list[idx];
        tmp.scope = scope;
        name2idx.Add(tmp.name, idx);
      }
    }
  }

  public void UnionWith(SymbolsStorage o)
  {
    for(int i=0;i<o.Count;++i)
      Add(o[i]);
  }

  public IEnumerator<Symbol> GetEnumerator()
  {
    return list.GetEnumerator();
  }
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
    
}
