using System;
using System.Collections.Generic;
using bhl.marshall;

namespace bhl {
  
// For lazy evaluation of types and forward declarations
public struct ProxyType : IMarshallable, IEquatable<ProxyType>
{
  public IType resolved;
  
  public INamedResolver resolver;
  //NOTE: for symbols it's a full absolute path from the very top namespace
  public string path;

  public ProxyType(INamedResolver resolver, string path)
  {
    if(path.Length == 0)
      throw new Exception("Type path is empty");
    if(Types.IsCompoundType(path))
      throw new Exception("Type spec contains illegal characters: '" + path + "'");
    
    this.resolver = resolver;
    this.path = path;
    
    resolved = null;
  }

  public ProxyType(IType obj)
  {
    resolver = null;
    path = null;
    
    resolved = null;
    
    SetResolved(obj);
  }

  public bool IsEmpty()
  {
    return string.IsNullOrEmpty(path) && 
           resolved == null;
  }

  public bool IsNull()
  {
    return resolved == null && 
           resolver == null && path == null;
  }

  public void Clear()
  {
    path = null;
    resolved = null;
  }

  public IType Get()
  {
    if(resolved != null)
      return resolved;

    if(!string.IsNullOrEmpty(path))
      SetResolved(resolver.ResolveNamedByPath(path) as IType);

    return resolved;
  }

  void SetResolved(IType resolved)
  {
    this.resolved = resolved;
    //TODO: some smelly code below - after resolving we re-write the original path
    //      with the normalized one, this is useful for cases when comparing proxies
    //      pointing to types withing namespaces, e.g: func(fns.Item) vs func(Item)
    if(resolved != null)
      path = GetNormalizedPath(resolved);
  }

  static string GetNormalizedPath(IType obj)
  {
    //for symbols full path is used
    return (obj is Symbol sym) ? sym.GetFullPath() : obj.GetName();
  }

  public void Sync(marshall.SyncContext ctx)
  {
    if(ctx.is_read)
      resolver = ((SymbolFactory)ctx.factory).resolver;

    bool is_ephemeral = false;
    if(!ctx.is_read)
      is_ephemeral = Get() is IEphemeralType;
    
    marshall.Marshall.Sync(ctx, ref is_ephemeral);
    
    if(is_ephemeral)
    {
      var eph = ctx.is_read ? null : Get() as IMarshallableGeneric;
      marshall.Marshall.SyncGeneric(ctx, ref eph);
      if(ctx.is_read)
        SetResolved((IType)eph);
    }
    else
      marshall.Marshall.Sync(ctx, ref path);
  }

  public override string ToString()
  {
    if(string.IsNullOrEmpty(path))
      Get();
    return path;
  }

  public override bool Equals(object o)
  {
    if(!(o is ProxyType))
      return false;
    return this.Equals((ProxyType)o);
  }

  public bool Equals(ProxyType o)
  {
    if(resolved != null && o.resolved != null)
      return o.resolved.Equals(resolved);
    else if(resolver != null && o.resolver == resolver && o.path == path)
      return true;
     
    //OK nothing worked, let's resolve the type
    Get();

    if(resolved != null)
      return resolved.Equals(o.Get());
    else //null check
      return null == o.Get();
  }

  public override int GetHashCode()
  {
    if(resolved != null)
      return resolved.GetHashCode();
    
    return path?.GetHashCode() ?? 0;
  }
}
    
public class TypeRefIndex
{
  internal List<ProxyType> all = new List<ProxyType>();

  public int Count {
    get {
      return all.Count;
    }
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
    for(int i=all.Count-1; i <= idx; ++i)
      all.Add(new ProxyType());
        
    all[idx] = v;
  }

  public bool IsValid(int idx)
  {
    return idx >= 0 && idx < all.Count && !all[idx].IsNull();
  }
}
}
