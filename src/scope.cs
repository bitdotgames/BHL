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

  public string module_name = "";

  public SymbolsStorage members;

  public List<Namespace> links = new List<Namespace>();

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public Namespace(string name, string module_name = "")
    : base(name, default(TypeProxy))
  {
    this.module_name = module_name;
    this.members = new SymbolsStorage(this);
  }

  //root and marshall version 
  public Namespace()
    : this("", "")
  {}

  public Namespace Clone()
  {
    var copy = new Namespace(name, module_name);

    for(int i=0;i<members.Count;++i)
      copy.members.Add(members[i]);

    foreach(var link in links)
      copy.links.Add(link.Clone());

    return copy;
  }

  public void Link(Namespace other)
  {
    var conflict_symb = TryLink(other);
    if(conflict_symb != null)
      throw new SymbolError(conflict_symb, "already defined symbol '" + conflict_symb.name + "'");
  }

  //NOTE: returns conflicting symbol or null
  //NOTE: here we combine only similar namespaces but we don't
  //      add other symbols from them
  public Symbol TryLink(Namespace other)
  {
    if(links.Contains(other))
      return null;

    for(int i=0;i<other.members.Count;++i)
    {
      var other_symb = other.members[i];

      var this_symb = ResolveNoFallback(other_symb.name);

      if(other_symb is Namespace other_ns)
      {
        //NOTE: if there's no such local symbol let's
        //      create an empty namespace which can be
        //      later linked
        if(this_symb == null)
        {
          var ns = new Namespace(other_symb.name);
          members.Add(ns);
          ns.links.Add(other_ns);
        }
        else if(this_symb is Namespace this_ns)
        {
          var conflict = this_ns.TryLink(other_ns);
          if(conflict != null)
            return conflict;
        }
        else if(this_symb != null)
          return this_symb;
      }
      else if(this_symb != null)
        return this_symb;
    }

    links.Add(other);
    return null;
  }

  public void Unlink(Namespace other)
  {
    for(int i=0;i<other.members.Count;++i)
    {
      var other_symb = other.members[i];

      if(other_symb is Namespace other_ns)
      {
        Symbol this_symb;
        members.TryGetValue(other_symb.name, out this_symb);
        if(this_symb is Namespace this_ns)
        {
          this_ns.Unlink(other_ns);
          if(this_ns.scope == this && this_ns.members.Count == 0)
            members.RemoveAt(members.IndexOf(this_ns));
        }
      }
    }

    links.Remove(other);
  }

  public struct LinksIterator
  {
    Namespace owner;
    int c;

    public Namespace current;

    public LinksIterator(Namespace owner)
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

  LinksIterator GetLinksIterator()
  {
    return new LinksIterator(this);
  }

  public IScope GetFallbackScope() { return scope; }

  //TODO: cache it?
  public SymbolsStorage GetMembers() 
  { 
    var all = new SymbolsStorage(this);
    var it = GetLinksIterator();
    while(it.Next())
    {
      for(int i=0;i<it.current.members.Count;++i)
      {
        var m = it.current.members[i];
        if(m is Namespace ns)
        {
          if(!all.Contains(ns.name))
            all.Add(ns);
          else 
            continue;
        }
        else
          all.Add(m);
      }
    }
    return all;
  }

  public Symbol Resolve(string name)
  {
    var s = ResolveNoFallback(name); 
    if(s != null)
      return s;

    if(scope != null) 
      return scope.Resolve(name);
    
    return null;
  }

  public Symbol ResolveNoFallback(string name)
  {
    var it = GetLinksIterator();
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
    int start_idx = 0;
    int next_idx = path.IndexOf('.');

    IScope scope = this;
    while(true)
    {
      string name = next_idx == -1 ? path.Substring(start_idx) : path.Substring(start_idx, next_idx - start_idx);

      Symbol symb;
      //special case for namespace
      if(scope is Namespace ns)
        symb = ns.ResolveNoFallback(name);
      else
        scope.GetMembers().TryGetValue(name, out symb);

      if(symb == null)
        break;

      //let's check if it's the last path item
      if(next_idx == -1)
        return symb;

      start_idx = next_idx + 1;
      next_idx = path.IndexOf('.', start_idx);

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
    if(ResolveNoFallback(sym.name) != null)
      throw new SymbolError(sym, "already defined symbol '" + sym.name + "'"); 

    //NOTE: calculating scope idx only for global variables for now
    //      (we are not interested in calculating scope indices for global
    //      funcs for now so that these indices won't clash)
    if(sym is VariableSymbol vs)
    {
      if(vs.scope_idx == -1)
      {
        int c = 0;
        for(int i=0;i<members.Count;++i)
          if(members[i] is VariableSymbol)
            ++c;
        vs.scope_idx = c;
      }
    } 

    members.Add(sym);
  }

  public override void Sync(SyncContext ctx) 
  {
    Marshall.Sync(ctx, ref name);
    Marshall.Sync(ctx, ref module_name);
    Marshall.Sync(ctx, ref members);
  }
}

} //namespace bhl
