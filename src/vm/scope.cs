using System;
using System.Collections.Generic;

namespace bhl {

public interface IScope
{
  // Look up name in this scope without fallback!
  Symbol Resolve(string name);

  // Define a symbol in the current scope, throws an exception 
  // if symbol with such a name already exists
  void Define(Symbol sym);

  // Where to look next for symbols in case if not found (e.g super class) 
  IScope GetFallbackScope();

  // Collection of members, depending on concrete implementation may be
  // a readonly one
  SymbolsStorage GetMembers();
}

public interface ISymbolResolver
{
  Symbol ResolveSymbolByPath(string path);
}

public interface IInstanceType : IType, IScope 
{
  HashSet<IInstanceType> GetAllRelatedTypesSet();
}

public class LocalScope : IScope, ISymbolResolver 
{
  bool is_paral;
  int next_idx;

  FuncSymbolScript func_owner;

  IScope fallback;

  SymbolsStorage members;

  public LocalScope(bool is_paral, IScope fallback) 
  { 
    this.is_paral = is_paral;

    this.fallback = fallback;  
    func_owner = fallback.FindEnclosingFuncSymbol();
    if(func_owner == null)
      throw new Exception("No top func symbol found");
    members = new SymbolsStorage(this);
  }

  public void Enter()
  {
    if(fallback is FuncSymbolScript fss)
      //start with func arguments number
      next_idx = fss.local_vars_num;
    else if(fallback is LocalScope fallback_ls)
      next_idx = fallback_ls.next_idx;
    func_owner.current_scope = this;
  }

  public void Exit()
  {
    if(fallback is LocalScope fallback_ls)
    {
      if(fallback_ls.is_paral)
        fallback_ls.next_idx = next_idx;
      func_owner.current_scope = fallback_ls;
    }
  }

  public Symbol ResolveSymbolByPath(string path)
  {
    return ScopeExtensions.ResolveSymbolByPath(this, path);
  }

  public SymbolsStorage GetMembers() { return members; }

  public Symbol Resolve(string name) 
  {
    return members.Find(name);
  }

  public void Define(Symbol sym) 
  {
    EnsureNotDefinedInEnclosingScopes(sym);

    DefineWithoutEnclosingChecks(sym);
  }

  void EnsureNotDefinedInEnclosingScopes(Symbol sym)
  {
    IScope tmp = this;
    while(true)
    {
      if(tmp.Resolve(sym.name) != null)
        throw new SymbolError(sym, "already defined symbol '" + sym.name + "'"); 
      //going 'up' until the owning function
      if(tmp == func_owner)
        break;
      tmp = tmp.GetFallbackScope();
    }
  }

  public void DefineWithoutEnclosingChecks(Symbol sym) 
  {
    //NOTE: overriding SymbolsStorage scope_idx assing logic
    if(sym is IScopeIndexed si && si.scope_idx == -1)
      si.scope_idx = next_idx;

    if(next_idx >= func_owner.local_vars_num)
      func_owner.local_vars_num = next_idx + 1;

    ++next_idx;

    members.Add(sym);
  }

  public IScope GetFallbackScope() { return fallback; }
}

public class Namespace : Symbol, IScope, marshall.IMarshallable, ISymbolResolver
{
  public const uint CLASS_ID = 20;

  public string module_name = "";

  public SymbolsStorage members;

  public List<Namespace> links = new List<Namespace>();

  public Symbol2Index gindex;

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  //for tests
  public Namespace(string name)
    : this(null, name, "")
  {}

  public Namespace(Symbol2Index gindex, string name, string module_name)
    : base(name, default(TypeProxy))
  {
    this.gindex = gindex;
    this.module_name = module_name;
    this.members = new SymbolsStorage(this);
  }

  //marshall version 
  public Namespace(Symbol2Index gindex = null)
    : this(gindex, "", "")
  {}

  public Namespace Nest(string name)
  {
    var sym = Resolve(name);
    if(sym != null && !(sym is Namespace)) 
      throw new SymbolError(sym, "already defined symbol '" + sym.name + "'"); 

    if(sym == null)
    {
      sym = new Namespace(gindex, name, module_name);
      Define(sym);
    }

    return sym as Namespace;
  }

  public Namespace Clone()
  {
    var copy = new Namespace(gindex, name, module_name);

    for(int i=0;i<members.Count;++i)
      copy.members.Add(members[i]);

    foreach(var imp in links)
      copy.links.Add(imp.Clone());

    return copy;
  }

  public void Link(Namespace other)
  {
    var conflict_symb = TryLink(other);
    if(conflict_symb != null)
      throw new SymbolError(conflict_symb, "already defined symbol '" + conflict_symb.name + "'");
  }

  //NOTE: returns conflicting symbol or null
  //NOTE: here we link only namespaces named similar but we don't
  //      add symbols from them
  public Symbol TryLink(Namespace other)
  {
    if(links.Contains(other))
      return null;

    for(int i=0;i<other.members.Count;++i)
    {
      var other_symb = other.members[i];

      var this_symb = Resolve(other_symb.name);

      if(other_symb is Namespace other_ns)
      {
        if(this_symb is Namespace this_ns)
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
        var this_symb = members.Find(other_symb.name);
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

  public void UnlinkAll()
  {
    for(int i=0;i<members.Count;++i)
    {
      if(members[i] is Namespace ns)
        ns.UnlinkAll();
    }
    links.Clear();
  }

  //NOTE: iterator is used for convenience since we need
  //      to iterate ourself and all other linked namespaces
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

  Iterator GetIterator()
  {
    return new Iterator(this);
  }

  public IScope GetFallbackScope() { return scope; }

  //TODO: cache it?
  public SymbolsStorage GetMembers() 
  { 
    var all = new SymbolsStorage(this);
    var it = GetIterator();
    while(it.Next())
    {
      for(int i=0;i<it.current.members.Count;++i)
      {
        var m = it.current.members[i];
        
        if(m is Namespace && all.Contains(m.name))
          continue;
        
        all.Add(m);
      }
    }
    return all;
  }

  public Symbol Resolve(string name)
  {
    var it = GetIterator();
    int c = 0;
    while(it.Next())
    {
      var s = it.current.members.Find(name);
      if(s != null)
      {
        //NOTE: let's create a local version of the linked namespace,
        //      we create it here assuming it might be changed 'on demand',
        //      since we want to prevent changing of the linked namespace
        if(c > 0 && s is Namespace ons)
        {
          var ns = new Namespace(gindex, s.name, module_name);
          ns.links.Add(ons);
          members.Add(ns);
          return ns;
        }
        else
          return s;
      }
      ++c;
    }
    return null;
  }

  public void Define(Symbol sym) 
  {
    if(Resolve(sym.name) != null)
      throw new SymbolError(sym, "already defined symbol '" + sym.name + "'"); 

    //TODO: We need some abstraction here for all kinds of
    //      symbols. For example, we need to know an index of 
    //      a function defined in a module. Likewise we have
    //      something similar for global variables.
    if(sym is FuncSymbolNative fsn && fsn.scope_idx == -1)
      fsn.scope_idx = gindex.Add(sym);

    members.Add(sym);
  }

  public Symbol ResolveSymbolByPath(string path)
  {
    return ScopeExtensions.ResolveSymbolByPath(this, path);
  }

  public override void Sync(marshall.SyncContext ctx) 
  {
    //NOTE: links are not persisted since it's assumed 
    //      they are restored by code above
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.Sync(ctx, ref module_name);
    marshall.Marshall.Sync(ctx, ref members);
  }
}

public static class ScopeExtensions
{
  public static string GetFullPath(this Symbol sym)
  {
    return sym.scope.GetFullPath(sym.name);
  }

  public static string GetFullPath(this IScope scope, string name)
  {
    if(string.IsNullOrEmpty(name) || name.IndexOf('.') != -1)
      return name;

    while(scope != null)
    {
      if(scope is Namespace ns)
      {
        if(ns.name.Length == 0)
          break;
        name = ns.name + '.' + name;

        scope = ns.scope;
      }
      else if(scope is ClassSymbol cl)
      {
        name = cl.name + '.' + name; 

        scope = cl.scope;
      }
      else if(scope is InterfaceSymbol ifs)
      {
        name = ifs.name + '.' + name; 

        scope = ifs.scope;
      }
      else
        scope = scope.GetFallbackScope();
    }
    return name;
  }

  public static IScope GetRootScope(this IScope scope)
  {
    var tmp = scope;
    while(tmp.GetFallbackScope() != null)
      tmp = tmp.GetFallbackScope();
    return tmp;
  }

  public static Symbol ResolveSymbolByPath(this IScope scope, string path)
  {
    int start_idx = 0;
    int next_idx = path.IndexOf('.');

    while(true)
    {
      string name = 
        next_idx == -1 ? 
        (start_idx == 0 ? path : path.Substring(start_idx)) : 
        path.Substring(start_idx, next_idx - start_idx);

      var symb = start_idx == 0 ? scope.ResolveWithFallback(name) : scope.Resolve(name);

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

  public static Symbol ResolveWithFallback(this IScope scope, string name)
  {
    var s = scope.Resolve(name);
    if(s != null)
      return s;

    var fallback = scope.GetFallbackScope();
    if(fallback != null) 
      return fallback.ResolveWithFallback(name);

    return null;
  }

  public static FuncSymbolScript FindEnclosingFuncSymbol(this IScope scope)
	{
    var fallback = scope;
    while(fallback != null)
    {
      if(fallback is FuncSymbolScript fss)
        return fss;
      fallback = fallback.GetFallbackScope();
    }
    return null;
	}

  public static string DumpMembers(this IScope scope, int level = 0)
  {
    string str = new String(' ', level) + (scope is Symbol sym ? "'" + sym.name + "' : " : "") + scope.GetType().Name + " {\n";
    var ms = scope.GetMembers();
    for(int i=0;i<ms.Count;++i)
    {
      var m = ms[i];
      if(m is IScope s)
        str += new String(' ', level) + s.DumpMembers(level+1) + "\n";
      else
        str += new String(' ', level+1) + "'" + m.name + "' : " + m.GetType().Name + "\n";
    }
    str += new String(' ', level) + "}";
    return str;
  }

  public struct TypeArg
  {
    public string name;
    public TypeProxy tp;

    public static implicit operator TypeArg(string name)
    {
      return new TypeArg(name);
    }

    public static implicit operator TypeArg(TypeProxy tp)
    {
      return new TypeArg(tp);
    }

    public static implicit operator TypeArg(BuiltInSymbol s)
    {
      return new TypeArg(new TypeProxy(s));
    }

    public TypeArg(string name)
    {
      this.name = name;
      this.tp = default(TypeProxy);
    }

    public TypeArg(TypeProxy tp)
    {
      this.name = null;
      this.tp = tp;
    }
  }

  public static TypeProxy T(this ISymbolResolver self, IType t)
  {
    return new TypeProxy(t);
  }

  public static TypeProxy T(this ISymbolResolver self, string name)
  {
    return new TypeProxy(self, name);
  }

  public static TypeProxy T(this ISymbolResolver self, TypeArg tn)
  {
    if(!tn.tp.IsEmpty())
      return tn.tp;
    else
      return self.T(tn.name);
  }

  public static TypeProxy TRef(this ISymbolResolver self, TypeArg tn)
  {           
    return self.T(new RefType(self.T(tn)));
  }

  public static TypeProxy TArr(this ISymbolResolver self, TypeArg tn)
  {           
    return self.T(new GenericArrayTypeSymbol(self.T(tn)));
  }

  public static TypeProxy TFunc(this ISymbolResolver self, TypeArg ret_type, params TypeArg[] arg_types)
  {           
    var sig = new FuncSignature(self.T(ret_type));
    foreach(var arg_type in arg_types)
      sig.AddArg(self.T(arg_type));
    return self.T(sig);
  }

  public static TypeProxy T(this ISymbolResolver self, TypeArg tn, params TypeArg[] types)
  {
    var tuple = new TupleType();
    tuple.Add(self.T(tn));
    foreach(var type in types)
      tuple.Add(self.T(type));
    return self.T(tuple);
  }
}

} //namespace bhl
