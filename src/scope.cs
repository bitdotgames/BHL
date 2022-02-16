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
  SymbolsDictionary GetMembers();
}

public class Scope : IScope 
{
  protected IScope fallback;

  protected SymbolsDictionary members = new SymbolsDictionary();

  public Scope(IScope fallback = null) 
  { 
    this.fallback = fallback;  
  }

  public SymbolsDictionary GetMembers() { return members; }

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

    sym.scope = this; // track the scope in each symbol

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
  public uint module_id;

  List<ModuleScope> imports = new List<ModuleScope>();

  public ModuleScope(uint module_id, TypeSystem types) 
    : base(types.globs) 
  {
    this.module_id = module_id;
  }

  //marshall version
  public ModuleScope(TypeSystem types) 
    : base(types.globs)
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
    var s = ResolveFlat(name);
    if(s != null)
      return s;

    foreach(var imp in imports)
    {
      s = imp.ResolveFlat(name);
      if(s != null)
        return s;
    }
    return null;
  }

  public Symbol ResolveFlat(string name) 
  {
    return base.Resolve(name);
  }

  public override void Define(Symbol sym) 
  {
    if(sym is VariableSymbol vs)
    {
      //NOTE: adding module id if it's not added already
      if(vs.module_id == 0)
        vs.module_id = module_id;

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
    else if(sym is FuncSymbolScript fs)
    {
      //NOTE: adding module id if it's not added already
      if(fs.decl != null && fs.decl.module_id == 0)
        fs.decl.module_id = module_id;
    }

    foreach(var imp in imports)
    {
      if(imp.ResolveFlat(sym.name) != null)
        throw new SymbolError(sym, "already defined symbol '" + sym.name + "'"); 
    }

    base.Define(sym);
  }

  public int GetFieldsNum()
  {
    return 2;
  }

  public void Sync(SyncContext ctx) 
  {
    Marshall.Sync(ctx, ref module_id);
    Marshall.Sync(ctx, ref members);
    if(ctx.is_read)
    {
      for(int i=0;i<members.Count;++i)
        members[i].scope = this;
    }
  }
}

} //namespace bhl
