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
  void GetInstanceTypesSet(HashSet<IInstanceType> s);
}

public class Scope : IScope 
{
  protected IScope fallback;

  protected SymbolsStorage members;

  public Scope(IScope fallback = null) 
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

    // if not here, check any fallback scope
    if(fallback != null) 
      return fallback.Resolve(name);

    return null;
  }

  public virtual void Define(Symbol sym) 
  {
    if(fallback != null && fallback.Resolve(sym.name) != null)
      throw new SymbolError(sym, "already defined symbol '" + sym.name + "'"); 

    if(members.Contains(sym.name))
      throw new SymbolError(sym, "already defined symbol '" + sym.name + "'"); 

    members.Add(sym);
  }

  public IScope GetFallbackScope() { return fallback; }

  public override string ToString() { return string.Join(",", members.GetStringKeys().ToArray()); }
}

public class GlobalScope : Scope 
{
  public GlobalScope() 
    : base(null) 
  {}
}

public class ModuleScope : Scope, IMarshallable
{
  public string module_name;

  List<ModuleScope> imports = new List<ModuleScope>();

  public ModuleScope(string module_name, GlobalScope globs) 
    : base(globs) 
  {
    this.module_name = module_name;
  }

  //marshall version
  public ModuleScope(GlobalScope globs) 
    : base(globs)
  {}

  public void AddImport(ModuleScope other)
  {
    if(other == this)
      return;
    if(imports.Contains(other))
      return;
    imports.Add(other);
  }

  public override Symbol Resolve(string name) 
  {
    var s = ResolveLocal(name);
    if(s != null)
      return s;

    foreach(var imp in imports)
    {
      s = imp.ResolveLocal(name);
      if(s != null)
        return s;
    }
    return null;
  }

  public Symbol ResolveLocal(string name) 
  {
    return base.Resolve(name);
  }

  public override void Define(Symbol sym) 
  {
    foreach(var imp in imports)
    {
      if(imp.ResolveLocal(sym.name) != null)
        throw new SymbolError(sym, "already defined symbol '" + sym.name + "'"); 
    }

    if(sym is VariableSymbol vs)
    {
      //NOTE: calculating scope idx only for global variables for now
      //      (we are not interested in calculating scope indices for global
      //      funcs for now so that these indices won't clash)
      if(vs.scope_idx == -1)
      {
        int c = 0;
        for(int i=0;i<members.Count;++i)
          if(members[i] is VariableSymbol)
            ++c;
        vs.scope_idx = c;
      }
    } 

    base.Define(sym);
  }

  public void Sync(SyncContext ctx) 
  {
    Marshall.Sync(ctx, ref module_name);
    Marshall.Sync(ctx, ref members);
  }
}

} //namespace bhl
