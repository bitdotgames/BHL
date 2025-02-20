using System;
using System.Collections.Generic;

namespace bhl {

public static class ScopeExtensions
{
  public struct Scope2Resolver : INamedResolver
  {
    IScope scope;

    public Scope2Resolver(IScope scope)
    {
      this.scope = scope;
    }

    public INamed ResolveNamedByPath(NamePath path)
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

  public static NamePath GetFullTypePath(this Symbol sym)
  {
    if(sym == null)
      return "?";
    return sym.scope.GetFullTypePath(sym.name);
  }

  public static NamePath GetFullTypePath(this IType type)
  {
    if(type == null)
      return "?";
    else if(type is Symbol s)
      return s.GetFullTypePath();
    else
      return type.ToString();
  }

  public static NamePath GetFullTypePath(this IScope scope, string name)
  {
    if(name == null)
      return "?";

    //TODO: this looks a bit like a hack but it's needed for some types (e.g arrays) which names are fully specified
    if(name.IndexOf('.') != -1)
      return name;

    var path = new NamePath(name);

    while(scope != null)
    {
      if(scope is Namespace ns)
      {
        if(ns.name.Length == 0)
          break;

        path.Add(ns.name);
        scope = ns.scope;
      }
      else if(scope is ClassSymbol cl)
      {
        path.Add(cl.name);
        scope = cl.scope;
      }
      else if(scope is InterfaceSymbol ifs)
      {
        path.Add(ifs.name);
        scope = ifs.scope;
      }
      else
        scope = scope.GetFallbackScope();
    }

    //let's reverse path
    if(path.Count > 1)
    {
      for(int i = 0; i < (path.Count / 2); ++i)
        (path[i], path[path.Count - i - 1]) = (path[path.Count - i - 1], path[i]);
    }

    return path;
  }

  public static IScope GetRootScope(this Symbol sym)
  {
    return sym.scope?.GetRootScope();
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

  public static Module GetModule(this IScope scope)
  {
    return scope.GetNamespace()?.module;
  }

  //NOTE: the first item of the resolved path is tried to be resolved
  //      with fallback (e.g. trying the 'upper' scopes)
  public static Symbol ResolveSymbolByPath(this IScope scope, NamePath path)
  {
    for(int i=0; i<path.Count; ++i)
    {
      //NOTE: for the root item let's resolve with fallback
      var symb = i == 0 ?
        scope.ResolveWithFallback(path[i]) :
        scope.ResolveRelatedOnly(path[i]);

      //let's check if it's the last path item
      if(i == path.Count - 1)
        return symb;

      scope = symb as IScope;
      //we can't proceed 'deeper' if the last resolved
      //symbol is not a scope
      if(scope == null)
        return null;
    }

    return null;
  }

  public static Symbol ResolveWithFallback(this IScope scope, string name, int level = 0)
  {
    var s = scope.Resolve(name);

    if(s == null)
    {
      var fallback = scope.GetFallbackScope();
      if(fallback != null)
        s = fallback.ResolveWithFallback(name, level + 1);
    }

    if(s != null)
    {
      //NOTE: in case the symbol is local to module we should
      //      return it only if we actually own it
      bool check_local = level == 0 && s.IsLocal();
      if(!check_local || (check_local && s.scope.GetModule() == scope.GetModule()))
        return s;
    }

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
    if(scope is IInstantiable iitype)
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

    if(scope is IEnumerable<Symbol> ies)
    {
      foreach(var m in ies)
      {
        if(scope is FuncSymbol)
          str += m.name + " : " + ((ITyped)m).GetIType().GetName() + ", ";
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
    if(!(scope is IEnumerable<Symbol> ies))
      return;

    foreach(var m in ies)
    {
      cb(m);
      if(m is IScope s)
        s.ForAllSymbols(cb);
    }
  }

  public struct TypeArg
  {
    public string name;
    public ProxyType tp;

    public static implicit operator TypeArg(string name)
    {
      return new TypeArg(name);
    }

    public static implicit operator TypeArg(ProxyType tp)
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
      this.tp = default(ProxyType);
    }

    public TypeArg(ProxyType tp)
    {
      this.name = null;
      this.tp = tp;
    }
  }

  public static ProxyType T(this INamedResolver self, IType t)
  {
    return new ProxyType(t);
  }

  public static ProxyType T(this INamedResolver self, string name)
  {
    return new ProxyType(self, name);
  }

  public static ProxyType T(this INamedResolver self, TypeArg tn)
  {
    if(!tn.tp.IsEmpty())
      return tn.tp;
    else
      return self.T(tn.name);
  }

  public static ProxyType TRef(this INamedResolver self, TypeArg tn)
  {
    return self.T(new RefType(self.T(tn)));
  }

  public static ProxyType TArr(this INamedResolver self, TypeArg tn)
  {
    var arr_type = new GenericArrayTypeSymbol(new Origin(), self.T(tn));
    arr_type.Setup();
    return self.T(arr_type);
  }

  public static ProxyType TMap(this INamedResolver self, TypeArg kt, TypeArg vt)
  {
    var map_type = new GenericMapTypeSymbol(new Origin(), self.T(kt), self.T(vt));
    map_type.Setup();
    return self.T(map_type);
  }

  public static ProxyType TFunc(this INamedResolver self, bool is_coro, TypeArg ret_type, params TypeArg[] arg_types)
  {
    return self.TFunc(is_coro ? FuncSignatureAttrib.Coro : 0, ret_type, arg_types);
  }

  public static ProxyType TFunc(this INamedResolver self, FuncSignatureAttrib attribs, TypeArg ret_type, params TypeArg[] arg_types)
  {
    var sig = new FuncSignature(attribs, self.T(ret_type));
    foreach(var arg_type in arg_types)
      sig.AddArg(self.T(arg_type));
    return self.T(sig);
  }

  public static ProxyType TFunc(this INamedResolver self, TypeArg ret_type, params TypeArg[] arg_types)
  {
    return self.TFunc(false, ret_type, arg_types);
  }

  public static ProxyType T(this INamedResolver self, TypeArg tn, params TypeArg[] types)
  {
    var tuple = new TupleType();
    tuple.Add(self.T(tn));
    foreach(var type in types)
      tuple.Add(self.T(type));
    return self.T(tuple);
  }
}

}
