using System;
using System.Collections.Generic;

namespace bhl {

using marshall;

public interface IScope
{
  // Where to look next for symbols in case if not found (e.g super class) 
  IScope GetFallbackScope();

  // Define a symbol in the current scope
  void Define(Symbol sym);
  // Look up name in this scope or in fallback scope if not here
  Symbol Resolve(string name);

  // Readonly collection of members
  SymbolsStorage GetMembers();
}

public interface IInstanceType : IType, IScope 
{
  HashSet<IInstanceType> GetAllRelatedTypesSet();
}

public class LocalScope : IScope 
{
  IScope fallback;

  public SymbolsStorage members;

  public LocalScope(IScope fallback = null) 
  { 
    members = new SymbolsStorage(this);
    this.fallback = fallback;  
  }

  public SymbolsStorage GetMembers() { return members; }

  public virtual Symbol Resolve(string name) 
  {
    Symbol s = null;
    members.TryGetValue(name, out s);
    if(s != null)
      return s;

    if(fallback != null) 
      return fallback.Resolve(name);

    return null;
  }

  public virtual void Define(Symbol sym) 
  {
    if(Resolve(sym.name) != null)
      throw new SymbolError(sym, "already defined symbol '" + sym.name + "'"); 

    members.Add(sym);
  }

  public IScope GetFallbackScope() { return fallback; }
}

public class Namespace : Symbol, IScope, IMarshallable
{
  public const uint CLASS_ID = 20;

  public IScope origin;

  IScope fallback;

  public SymbolsStorage members;

  public Namespace sibling { get; private set; }

  public Namespace(IScope origin, string name, IScope fallback = null)
    : base(name, default(TypeProxy))
  {
    this.origin = origin;
    this.name = name;
    this.fallback = fallback;
    this.members = new SymbolsStorage(origin);
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public void AttachSibling(Namespace sibling)
  {
    if(this.sibling != null)
      sibling.sibling = this.sibling;
    
    this.sibling = sibling;
  }

  public IScope GetFallbackScope() { return fallback; }

  public SymbolsStorage GetMembers() { return members; }

  public Symbol Resolve(string name)
  {
    Symbol s = null;
    Namespace curr = this;
    while(curr != null)
    {
      curr.members.TryGetValue(name, out s);
      if(s != null)
        return s;
      curr = curr.sibling;
    }

    if(fallback != null) 
      return fallback.Resolve(name);
    
    return null;
  }

  public void Define(Symbol sym) 
  {
    if(Resolve(sym.name) != null)
      throw new SymbolError(sym, "already defined symbol '" + sym.name + "'"); 

    members.Add(sym);
  }

  public override void Sync(SyncContext ctx) 
  {
    Marshall.Sync(ctx, ref name);
    Marshall.Sync(ctx, ref members);
  }
}

public class NativeScope : IScope
{
  public Namespace root;

  public NativeScope() 
  {
    root = new Namespace(this, "");
  }

  public IScope GetFallbackScope() { return null; }

  public SymbolsStorage GetMembers() { return root.members; }

  public Symbol Resolve(string name) 
  {
    return root.Resolve(name);
  }

  public void Define(Symbol sym) 
  {
    root.Define(sym);
  }
}

public class ModuleScope : IScope, IMarshallable
{
  public string module_name;

  NativeScope globs;

  List<ModuleScope> imports = new List<ModuleScope>();

  public Namespace root;

  public ModuleScope(string module_name, NativeScope globs) 
  {
    this.module_name = module_name;

    this.globs = globs;

    root = new Namespace(this, "", globs);
    root.AttachSibling(globs.root);
  }

  public IScope GetFallbackScope() { return globs; }

  public SymbolsStorage GetMembers() 
  { 
    var all = new SymbolsStorage(this);

    Namespace curr = root;
    while(curr != null)
    {
      all.UnionWith(curr.GetMembers());
      curr = curr.sibling;
    }

    return all;
  }

  //marshall version
  public ModuleScope(NativeScope globs) 
  {
    root = new Namespace(this, "", globs);
    root.AttachSibling(globs.root);
  }

  //TODO: Union root namespace
  public void AddImport(ModuleScope other)
  {
    if(other == this)
      return;
    if(imports.Contains(other))
      return;
    imports.Add(other);
  }

  public Symbol Resolve(string name) 
  {
    //var s = ResolveLocal(name);
    //if(s != null)
    //  return s;

    //foreach(var imp in imports)
    //{
    //  s = imp.ResolveLocal(name);
    //  if(s != null)
    //    return s;
    //}
    //return null;

    return root.Resolve(name);
  }

  public void Define(Symbol sym) 
  {
    //foreach(var imp in imports)
    //{
    //  if(imp.ResolveLocal(sym.name) != null)
    //    throw new SymbolError(sym, "already defined symbol '" + sym.name + "'"); 
    //}

    if(sym is VariableSymbol vs)
    {
      //NOTE: calculating scope idx only for global variables for now
      //      (we are not interested in calculating scope indices for global
      //      funcs for now so that these indices won't clash)
      if(vs.scope_idx == -1)
      {
        int c = 0;
        var members = root.GetMembers();
        for(int i=0;i<members.Count;++i)
          if(members[i] is VariableSymbol)
            ++c;
        vs.scope_idx = c;
      }
    } 

    root.Define(sym);
  }

  public void Sync(SyncContext ctx) 
  {
    Marshall.Sync(ctx, ref module_name);

    root.Sync(ctx);
  }
}

} //namespace bhl
