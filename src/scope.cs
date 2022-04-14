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

  public SymbolsStorage members;

  public List<Namespace> links = new List<Namespace>();

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public Namespace(string name)
    : base(name, default(TypeProxy))
  {
    this.members = new SymbolsStorage(this);
  }

  //marshall version
  public Namespace()
    : base("", default(TypeProxy))
  {
    this.members = new SymbolsStorage(this);
  }

  public void Link(Namespace ns)
  {
    for(int i=0;i<ns.members.Count;++i)
    {
      if(ns.members[i] is Namespace mns)
      {
        var sym = ResolveLocal(mns.name);
        //NOTE: if there's no such local symbol let's
        //      create an empty namespace which can be
        //      later linked
        if(sym == null)
          sym = new Namespace(mns.name);
        if(sym is Namespace lns)
          lns.Link(mns);
        else if(sym != null)
          throw new SymbolError(sym, "already defined symbol '" + sym.name + "'");
      }
    }

    this.links.Add(ns);
  }

  public IScope GetFallbackScope() { return scope; }

  public SymbolsStorage GetMembers() { return members; }

  public struct Iterator
  {
    Namespace owner;
    int c;

    public Namespace current;

    public Iterator(Namespace owner)
    {
      this.owner = owner;
      c = -1;
      current = null;
    }

    public bool Next()
    {
      //special case for itself
      if(c == -1)
      {
        current = owner;
        ++c;
        return true;
      }

      if(c == owner.links.Count)
        return false;
      current = owner.links[c];
      ++c;
      return true;
    }
  }

  public Iterator GetIterator()
  {
    return new Iterator(this);
  }

  public Symbol Resolve(string name)
  {
    var s = ResolveLocal(name); 
    if(s != null)
      return s;

    if(scope != null) 
      return scope.Resolve(name);
    
    return null;
  }

  public Symbol ResolveLocal(string name)
  {
    var it = GetIterator();
    while(it.Next())
    {
      Symbol s;
      it.current.members.TryGetValue(name, out s);
      if(s != null)
        return s;
    }
    return null;
  }

  public Symbol ResolvePath(string path)
  {
    var names = path.Split('.');
    IScope scope = this;
    for(int i=0;i<names.Length;++i)
    {
      var name = names[i];

      Symbol symb;
      //special case for namespace
      if(scope is Namespace ns)
        symb = ns.ResolveLocal(name);
      else
        scope.GetMembers().TryGetValue(name, out symb);

      if(symb == null)
        break;
      //let's check if it's the last path item
      else if(i == names.Length-1)
        return symb;

      scope = symb as IScope;
      //we can't proceed 'deeper' if the last resolved 
      //symbol is not a scope
      if(scope == null)
        break;
    }

    return null;
  }

  public void Define(Symbol sym) 
  {
    if(ResolveLocal(sym.name) != null)
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
    root = new Namespace("");
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

    root = new Namespace("");
    root.Link(globs.root);
  }

  public IScope GetFallbackScope() { return globs; }

  public SymbolsStorage GetMembers() 
  { 
    var all = new SymbolsStorage(this);

    var it = root.GetIterator();
    while(it.Next())
      all.UnionWith(it.current.members);

    return all;
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
