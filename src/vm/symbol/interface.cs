using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {
  
public abstract class InterfaceSymbol : Symbol, IInstantiable, IEnumerable<Symbol>
{
  internal SymbolsStorage members;

  public TypeSet<InterfaceSymbol> inherits = new TypeSet<InterfaceSymbol>();

  HashSet<IInstantiable> related_types;

  public InterfaceSymbol(Origin origin, string name)
    : base(origin, name)
  {
    this.members = new SymbolsStorage(this);
  }

  //marshall factory version
  public InterfaceSymbol()
    : this(null, null)
  {}

  public void Define(Symbol sym)
  {
    if(!(sym is FuncSymbol))
      throw new Exception("Only function symbols supported, given " + sym?.GetType().Name);
    
    members.Add(sym);
  }

  public Symbol Resolve(string name) 
  {
    var sym =  members.Find(name);
    if(sym != null)
      return sym;

    for(int i=0;i<inherits.Count;++i)
    {
      var tmp = inherits[i].Resolve(name);
      if(tmp != null)
        return tmp;
    }

    return null;
  }

  public IScope GetFallbackScope() { return scope; }

  public IEnumerator<Symbol> GetEnumerator() { return members.GetEnumerator(); }
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public void SetInherits(IList<InterfaceSymbol> inherits)
  {
    if(inherits == null)
      return;

    foreach(var ext in inherits)
      this.inherits.Add(ext);
  }

  public FuncSymbol FindMethod(string name)
  {
    return Resolve(name) as FuncSymbol;
  }

  public HashSet<IInstantiable> GetAllRelatedTypesSet()
  {
    if(related_types == null)
    {
      related_types = new HashSet<IInstantiable>();
      related_types.Add(this);
      for(int i=0;i<inherits.Count;++i)
      {
        var ext = (IInstantiable)inherits[i];
        if(!related_types.Contains(ext))
          related_types.UnionWith(ext.GetAllRelatedTypesSet());
      }
    }
    return related_types;
  }
}

public class InterfaceSymbolScript : InterfaceSymbol
{
  public const uint CLASS_ID = 18;
  
  public InterfaceSymbolScript(Origin origin, string name)
    : base(origin, name)
  {}

  //marshall factory version
  public InterfaceSymbolScript() 
    : this(null, null)
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {
    refs.Index(inherits.list);
    refs.Index(members.list);
  }
  
  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.Sync(ctx, ref inherits); 
    marshall.Marshall.Sync(ctx, ref members); 
  }
}

public class InterfaceSymbolNative : InterfaceSymbol, INativeType
{
  IList<ProxyType> proxy_inherits;
  FuncSymbol[] funcs;
  System.Type native_type;

  public InterfaceSymbolNative(
    Origin origin,
    string name, 
    IList<ProxyType> proxy_inherits,
    params FuncSymbol[] funcs
  )
    : base(origin, name)
  {
    this.proxy_inherits = proxy_inherits;
    this.funcs = funcs;
  }

  public InterfaceSymbolNative(
    Origin origin,
    string name, 
    IList<ProxyType> proxy_inherits,
    System.Type native_type,
    params FuncSymbol[] funcs
  )
    : this(origin, name, proxy_inherits, funcs)
  {
    this.native_type = native_type;
  }

  public System.Type GetNativeType()
  {
    return native_type;
  }

  public object GetNativeObject(Val v)
  {
    return v?._obj;
  }

  public void Setup()
  {
    List<InterfaceSymbol> inherits = null;
    if(proxy_inherits != null && proxy_inherits.Count > 0)
    {
      inherits = new List<InterfaceSymbol>();
      foreach(var pi in proxy_inherits)
      {
        var iface = pi.Get() as InterfaceSymbol;
        if(iface == null) 
          throw new Exception("Inherited interface not found" + pi);

        inherits.Add(iface);
      }
      SetInherits(inherits);
    }

    foreach(var func in funcs)
      Define(func);
  }

  public override uint ClassId()
  {
    throw new NotImplementedException();
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {
    if(proxy_inherits != null)
      refs.Index(proxy_inherits);
  }
  
  public override void Sync(marshall.SyncContext ctx)
  {
    throw new NotImplementedException();
  }
}

}