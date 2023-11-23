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
}

public interface INamedResolver
{
  INamed ResolveNamedByPath(string path);
}

public interface ISymbolsIterator
{
  int Count { get; }
  Symbol this[int index] { get; }
}

public interface ISymbolsIteratable
{
  // A read-only symbols accessing interface 
  ISymbolsIterator GetSymbolsIterator();
}

public interface IInstanceType : IType, IScope 
{
  HashSet<IInstanceType> GetAllRelatedTypesSet();
}

public class LocalScope : IScope, ISymbolsIteratable
{
  bool is_paral;
  int next_idx;

  FuncSymbolScript func_owner;

  IScope fallback;

  internal SymbolsStorage members;

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
    func_owner._current_scope = this;
  }

  public void Exit()
  {
    if(fallback is LocalScope fallback_ls)
    {
      if(fallback_ls.is_paral)
        fallback_ls.next_idx = next_idx;
      else //TODO: just for now, trying to spot a possible bug
        fallback_ls.next_idx = next_idx;

      func_owner._current_scope = fallback_ls;
    }
  }

  public ISymbolsIterator GetSymbolsIterator() { return members; }

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

public class Namespace : Symbol, IScope, marshall.IMarshallable, ISymbolsIteratable, INamedResolver
{
  public const uint CLASS_ID = 20;

  public Module module;

  internal List<Namespace> links = new List<Namespace>();

  public SymbolsStorage members;

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public Namespace(Module module, string name)
    : base(null, name)
  {
    this.module = module;
    this.members = new SymbolsStorage(this);
  }

  public Namespace(Module module)
    : this(module, "")
  {}

  //marshall version 
  public Namespace()
    : this(null, "")
  {}

  public INamed ResolveNamedByPath(string path)
  {
    return this.ResolveSymbolByPath(path);
  }

  public void ForAllLocalSymbols(System.Action<Symbol> cb)
  {
    for(int m=0;m<members.Count;++m)
    {
      var member = members[m];
      cb(member);
      if(member is Namespace ns)
        ns.ForAllLocalSymbols(cb);
      else if(member is IScope s)
        s.ForAllSymbols(cb);
    }
  }

  public Namespace Nest(string name)
  {
    var sym = Resolve(name);
    if(sym != null && !(sym is Namespace)) 
      throw new SymbolError(sym, "already defined symbol '" + sym.name + "'"); 

    if(sym == null)
    {
      sym = new Namespace(module, name);
      Define(sym);
    }

    return sym as Namespace;
  }

  public void Link(Namespace other)
  {
    var conflict = TryLink(other);
    if(!conflict.Ok)
      throw new SymbolError(conflict.local, "already defined symbol '" + conflict.local.name + "'");
  }

  public struct LinkConflict
  {
    public Symbol local;
    public Symbol other;

    public bool Ok { 
      get { return local == null && other == null; } 
    } 

    public LinkConflict(Symbol local, Symbol other)
    {
      this.local = local;
      this.other = other;
    }
  }

  //NOTE: here we link only namespaces named similar but we don't
  //      add symbols from them
  public LinkConflict TryLink(Namespace other)
  {
    if(links.Contains(other))
      return default(LinkConflict);

    for(int i=0;i<other.members.Count;++i)
    {
      var other_symb = other.members[i];

      var this_symb = Resolve(other_symb.name);

      if(other_symb is Namespace other_ns)
      {
        if(this_symb is Namespace this_ns)
        {
          var conflict = this_ns.TryLink(other_ns);
          if(!conflict.Ok)
            return conflict;
        }
        else if(this_symb != null)
          return new LinkConflict(this_symb, other_symb);
        else
        {
          //NOTE: let's create a local version of the linked namespace
          //      which is linked to the original one
          var ns = new Namespace(other_ns.module, other_ns.name);
          ns.links.Add(other_ns);
          members.Add(ns);
        }
      }
      else if(this_symb != null)
      {
        //NOTE: let's ignore other local module symbols
        if(!other_symb.IsLocal())
          return new LinkConflict(this_symb, other_symb);
      }
    }

    links.Add(other);

    return default(LinkConflict);
  }

  public void PreLink(Namespace other)
  {
    for(int i=0;i<other.members.Count;++i)
    {
      var other_symb = other.members[i];

      var this_symb = Resolve(other_symb.name);

      if(other_symb is Namespace other_ns)
      {
        if(this_symb == null)
        {
          //NOTE: let's create a local version of the linked namespace
          //      which is linked to the original one
          var ns = new Namespace(other_ns.module, other_ns.name);
          ns.links.Add(other_ns);
          members.Add(ns);
        }
      }
    }
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

  public ISymbolsIterator GetSymbolsIterator() 
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

    if(sym is FuncSymbolNative fsn)
      module.nfuncs.Index(fsn);
    else if(sym is VariableSymbol vs)
      module.gvars.Index(vs);

    members.Add(sym);
  }

  public override void Sync(marshall.SyncContext ctx) 
  {
    //NOTE: module and links are not persisted since it's assumed 
    //      they are restored by the more high level code
    
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.Sync(ctx, ref members);
  }

  public void SetupSymbols()
  {
    ForAllLocalSymbols((s) => 
      {
        if(s is ClassSymbolScript cs)
          cs.Setup();
        else if(s is FuncSymbolScript fs && module != null)
          module._ip2func[fs.ip_addr] = fs;
      }
    );
  }
}

public static class ScopeExtensions
{
  public struct Scope2Resolver : INamedResolver  
  {
    IScope scope;

    public Scope2Resolver(IScope scope)
    {
      this.scope = scope;
    }

    public INamed ResolveNamedByPath(string path)
    {
      return scope.ResolveSymbolByPath(path);
    }
  }

  public static bool TryDefine(this IScope scope, Symbol symb, out SymbolError error)
  {
    error = null;

    try
    {
      scope.Define(symb);
    }
    catch(SymbolError err)
    {
      error = err;
      return false;
    }

    return true;
  }

  public static Scope2Resolver R(this IScope scope)
  {
    return new Scope2Resolver(scope);
  }

  public static T Resolve<T>(this IScope scope, string name) where T : Symbol
  {
    return scope.Resolve(name) as T;
  }

  public static string GetFullPath(this Symbol sym)
  {
    if(sym == null)
      return "?";
    return sym.scope.GetFullPath(sym.name);
  }

  public static string GetFullPath(this IType type)
  {
    if(type == null)
      return "?";
    else if(type is Symbol s)
      return s.GetFullPath();
    else
      return type.ToString();
  }

  public static string GetFullPath(this IScope scope, string name)
  {
    if(name == null)
      return "?";

    if(name.IndexOf('.') != -1)
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
    IScope tmp;
    while((tmp = scope.GetFallbackScope()) != null) 
      scope = tmp;
    return scope;
  }

  public static Namespace GetNamespace(this IScope scope)
  {
    var tmp = scope;
    do
    {
      if(tmp is Namespace ns)
        return ns;
      tmp = tmp.GetFallbackScope();
    } while(tmp != null);
    return null;
  }

  public static Namespace GetRootNamespace(this IScope scope)
  {
    return scope.GetRootScope() as Namespace;
  }

  //NOTE: the first item of the resolved path is tried to be resolved
  //      with fallback (e.g. trying the 'upper' scopes)
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

      //NOTE: for the root item let's resolve with fallback
      var symb = start_idx == 0 ? 
        scope.ResolveWithFallback(name) : 
        scope.ResolveRelatedOnly(name);

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
    {
      //NOTE: in case the symbol is local to module we should 
      //      return it only if we actually own it
      bool is_local = s.IsLocal();
      if(!is_local || (is_local && s.scope == scope))
        return s;
    }

    var fallback = scope.GetFallbackScope();
    if(fallback != null) 
      return fallback.ResolveWithFallback(name);

    return null;
  }

  public static bool IsLocal(this Symbol symb)
  {
    if(symb is FuncSymbol fs && fs.attribs.HasFlag(FuncAttrib.Static) && fs.scope is Namespace)
      return true;
    else if(symb is GlobalVariableSymbol gs && gs.is_local)
      return true;

    return false;
  }

  public static Symbol ResolveRelatedOnly(this IScope scope, string name)
  {
    if(scope is IInstanceType iitype)
    {
      var type_set = iitype.GetAllRelatedTypesSet();
      foreach(var item in type_set)
      {
        var s = item.Resolve(name);
        if(s != null)
          return s;
      }
      return null;
    }
    else
      return scope.Resolve(name);
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
    string str = new String(' ', level) + (scope is INamed named ? named.GetName() + " : " : "") + scope.GetType().Name;

    if(scope is FuncSymbol)
      str += "(";
    else
      str += " {\n";

    if(scope is ISymbolsIteratable isi)
    {
      var idx = isi.GetSymbolsIterator();
      for(int i=0;i<idx.Count;++i)
      {
        var m = idx[i];
        if(scope is FuncSymbol)
          str += m.name + " : " + ((ITyped)m).GetIType().GetName() + (i < idx.Count-1 ? ", " : "");
        else if(m is IScope s)
          str += s.DumpMembers(level+1) + "\n";
        else if(m is ITyped typed)
          str += new String(' ', level+1) + m.name + " : " + typed.GetIType()?.GetName() + "(" + m.GetType().Name + ")\n";
        else
          str += new String(' ', level+1) + m.name + " : " + m.GetType().Name + "\n";
      }
    }
    if(scope is FuncSymbol)
      str += ")";
    else
    str += new String(' ', level) + "}";
    return str;
  }

  public static void ForAllSymbols(this IScope scope, System.Action<Symbol> cb)
  {
    if(!(scope is ISymbolsIteratable isi))
      return;
    
    var idx = isi.GetSymbolsIterator();
    for(int i=0;i<idx?.Count;++i)
    {
      var m = idx[i];
      cb(m);
      if(m is IScope s)
        s.ForAllSymbols(cb);
    }
  }

  public struct TypeArg
  {
    public string name;
    public Proxy<IType> tp;

    public static implicit operator TypeArg(string name)
    {
      return new TypeArg(name);
    }

    public static implicit operator TypeArg(Proxy<IType> tp)
    {
      return new TypeArg(tp);
    }

    public static implicit operator TypeArg(IntSymbol s)
    {
      return new TypeArg(s);
    }

    public static implicit operator TypeArg(BoolSymbol s)
    {
      return new TypeArg(s);
    }

    public static implicit operator TypeArg(StringSymbol s)
    {
      return new TypeArg(s);
    }

    public static implicit operator TypeArg(FloatSymbol s)
    {
      return new TypeArg(s);
    }

    public static implicit operator TypeArg(VoidSymbol s)
    {
      return new TypeArg(s);
    }

    public TypeArg(string name)
    {
      this.name = name;
      this.tp = default(Proxy<IType>);
    }

    public TypeArg(Proxy<IType> tp)
    {
      this.name = null;
      this.tp = tp;
    }
  }

  public static Proxy<IType> T(this INamedResolver self, IType t)
  {
    return new Proxy<IType>(t);
  }

  public static Proxy<IType> T(this INamedResolver self, string name)
  {
    return new Proxy<IType>(self, name);
  }

  public static Proxy<IType> T(this INamedResolver self, TypeArg tn)
  {
    if(!tn.tp.IsEmpty())
      return tn.tp;
    else
      return self.T(tn.name);
  }

  public static Proxy<IType> TRef(this INamedResolver self, TypeArg tn)
  {           
    return self.T(new RefType(self.T(tn)));
  }

  public static Proxy<IType> TArr(this INamedResolver self, TypeArg tn)
  {           
    return self.T(new GenericArrayTypeSymbol(new Origin(), self.T(tn)));
  }

  public static Proxy<IType> TMap(this INamedResolver self, TypeArg kt, TypeArg vt)
  {           
    return self.T(new GenericMapTypeSymbol(new Origin(), self.T(kt), self.T(vt)));
  }

  public static Proxy<IType> TFunc(this INamedResolver self, bool is_coro, TypeArg ret_type, params TypeArg[] arg_types)
  {           
    return self.TFunc(is_coro ? FuncSignatureAttrib.Coro : 0, ret_type, arg_types);
  }

  public static Proxy<IType> TFunc(this INamedResolver self, FuncSignatureAttrib attribs, TypeArg ret_type, params TypeArg[] arg_types)
  {           
    var sig = new FuncSignature(attribs, self.T(ret_type));
    foreach(var arg_type in arg_types)
      sig.AddArg(self.T(arg_type));
    return self.T(sig);
  }

  public static Proxy<IType> TFunc(this INamedResolver self, TypeArg ret_type, params TypeArg[] arg_types)
  {           
    return self.TFunc(false, ret_type, arg_types);
  }

  public static Proxy<IType> T(this INamedResolver self, TypeArg tn, params TypeArg[] types)
  {
    var tuple = new TupleType();
    tuple.Add(self.T(tn));
    foreach(var type in types)
      tuple.Add(self.T(type));
    return self.T(tuple);
  }
}

} //namespace bhl
