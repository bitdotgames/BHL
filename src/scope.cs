using System;
using System.Collections.Generic;

namespace bhl {

using marshall;

public interface IScope
{
  // Look up name in this scope without fallback!
  Symbol Resolve(string name);

  // Define a symbol in the current scope
  void Define(Symbol sym);

  // Readonly collection of members
  SymbolsStorage GetMembers();

  // Where to look next for symbols in case if not found (e.g super class) 
  IScope GetFallbackScope();
}

public interface ISymbolResolver
{
  Symbol ResolveByFullName(string full_name);
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
    return members.Find(name);
  }

  public virtual void Define(Symbol sym) 
  {
    if(this.Resolve(sym.name) != null)
      throw new SymbolError(sym, "already defined symbol '" + sym.name + "'"); 

    members.Add(sym);
  }

  public IScope GetFallbackScope() { return fallback; }
}

public class Namespace : Symbol, IScope, IMarshallable, ISymbolResolver
{
  public const uint CLASS_ID = 20;

  //TODO: think how to get rid of this dependency
  Types types;

  public string module_name = "";

  public SymbolsStorage members;

  public List<Namespace> imports = new List<Namespace>();

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public Namespace(Types types, string name, string module_name = "")
    : base(name, default(TypeProxy))
  {
    this.types = types;
    this.module_name = module_name;
    this.members = new SymbolsStorage(this);
  }

  //root and marshall version 
  public Namespace(Types types)
    : this(types, "", "")
  {}

  public Namespace Clone()
  {
    var copy = new Namespace(types, name, module_name);

    for(int i=0;i<members.Count;++i)
      copy.members.Add(members[i]);

    foreach(var imp in imports)
      copy.imports.Add(imp.Clone());

    return copy;
  }

  public void Import(Namespace other)
  {
    var conflict_symb = TryImport(other);
    if(conflict_symb != null)
      throw new SymbolError(conflict_symb, "already defined symbol '" + conflict_symb.name + "'");
  }

  //NOTE: returns conflicting symbol or null
  //NOTE: here we combine only similar namespaces but we don't
  //      add other symbols from them
  public Symbol TryImport(Namespace other)
  {
    if(imports.Contains(other))
      return null;

    for(int i=0;i<other.members.Count;++i)
    {
      var other_symb = other.members[i];

      var this_symb = Resolve(other_symb.name);

      if(other_symb is Namespace other_ns)
      {
        //NOTE: if there's no such local symbol let's
        //      create an empty namespace
        if(this_symb == null)
        {
          var ns = new Namespace(types, other_symb.name);
          ns.imports.Add(other_ns);
          members.Add(ns);
        }
        else if(this_symb is Namespace this_ns)
        {
          var conflict = this_ns.TryImport(other_ns);
          if(conflict != null)
            return conflict;
        }
        else if(this_symb != null)
          return this_symb;
      }
      else if(this_symb != null)
        return this_symb;
    }

    imports.Add(other);
    return null;
  }

  public void Unimport(Namespace other)
  {
    for(int i=0;i<other.members.Count;++i)
    {
      var other_symb = other.members[i];

      if(other_symb is Namespace other_ns)
      {
        var this_symb = members.Find(other_symb.name);
        if(this_symb is Namespace this_ns)
        {
          this_ns.Unimport(other_ns);
          if(this_ns.scope == this && this_ns.members.Count == 0)
            members.RemoveAt(members.IndexOf(this_ns));
        }
      }
    }

    imports.Remove(other);
  }

  public void UnimportAll()
  {
    for(int i=0;i<members.Count;++i)
    {
      if(members[i] is Namespace ns)
        ns.UnimportAll();
    }
    imports.Clear();
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

      if(c == owner.imports.Count)
        return false;
      current = owner.imports[c];
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
    while(it.Next())
    {
      var s = it.current.members.Find(name);
      if(s != null)
        return s;
    }
    return null;
  }

  public void Define(Symbol sym) 
  {
    if(Resolve(sym.name) != null)
      throw new SymbolError(sym, "already defined symbol '" + sym.name + "'"); 

    if(sym is IScopeIndexed si && si.scope_idx == -1)
    {
      //TODO: We need some abstraction here for all kinds of
      //      symbols. For example, we need to know an index of 
      //      a function defined in a module. Likewise we have
      //      something similar for global variables.
      //NOTE: For native func symbols we store the unique global index
      if(sym is FuncSymbolNative)
      {
        si.scope_idx = types.natives.Count;
        types.natives.Add(sym);
      }
      else
        si.scope_idx = members.Count; 
    }

    members.Add(sym);
  }

  public Symbol ResolveByFullName(string full_name)
  {
    IScope scope = this;

    int start_idx = 0;
    int next_idx = full_name.IndexOf('.');

    while(true)
    {
      string name = 
        next_idx == -1 ? 
        (start_idx == 0 ? full_name : full_name.Substring(start_idx)) : 
        full_name.Substring(start_idx, next_idx - start_idx);

      var symb = scope.Resolve(name);

      if(symb == null)
        break;

      //let's check if it's the last path item
      if(next_idx == -1)
        return symb;

      start_idx = next_idx + 1;
      next_idx = full_name.IndexOf('.', start_idx);

      scope = symb as IScope;
      //we can't proceed 'deeper' if the last resolved 
      //symbol is not a scope
      if(scope == null)
        break;
    }

    return null;
  }

  public override void Sync(SyncContext ctx) 
  {
    Marshall.Sync(ctx, ref name);
    Marshall.Sync(ctx, ref module_name);
    Marshall.Sync(ctx, ref members);
  }
}

public static class ScopeExtensions
{
  public static string GetFullName(this Symbol sym)
  {
    return GetNamespaceNamePrefix(
      sym.scope as Namespace, 
      sym.name
    );
  }

  public static string GetNamespaceNamePrefix(Namespace parent, string name)
  {
    while(parent != null && parent.name.Length > 0)
    {
      name = parent.name + '.' + name;
      parent = (Namespace)parent.scope;
    }
    return name;
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
