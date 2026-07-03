using System;
using System.Collections.Generic;
using bhl.marshall;

namespace bhl
{

public partial class Types : INamedResolver, IProxyTypeCache
{
  //global module
  public ModuleDeclared module;

  public Namespace ns
  {
    get { return module.ns;  }
  }

  internal Dictionary<string, ModuleDeclared> modules = new Dictionary<string, ModuleDeclared>();

  //NOTE: interning of ProxyType instances requested by name, e.g T("Color"),
  //      so that similar requests return the same cached instance;
  //      concurrent since Types instance is shared by compile threads
  System.Collections.Concurrent.ConcurrentDictionary<string, ProxyType> _proxy_cache =
    new System.Collections.Concurrent.ConcurrentDictionary<string, ProxyType>();

  //NOTE: interning of ProxyType instances wrapping an already resolved IType;
  //      weak-keyed so it never pins the type instances it caches
  System.Runtime.CompilerServices.ConditionalWeakTable<IType, ProxyType> _resolved_proxy_cache =
    new System.Runtime.CompilerServices.ConditionalWeakTable<IType, ProxyType>();

  public ProxyType InternProxyType(string name)
  {
    return _proxy_cache.GetOrAdd(name, static (n, ts) => new ProxyType(ts, n), this);
  }

  public ProxyType InternProxyType(string key, Func<string, ProxyType> factory)
  {
    return _proxy_cache.GetOrAdd(key, factory);
  }

  public ProxyType InternProxyType(IType t)
  {
    return _resolved_proxy_cache.GetValue(t, static key => new ProxyType(key));
  }

  static Types()
  {
    InitBuiltins();
  }

  public Types()
  {
    module = new ModuleDeclared();

    CopyFromStaticModule();

    RegisterModule(std.MakeModule(this));
    RegisterModule(std.io.MakeModule(this));
    RegisterModule(std.bind.MakeModule(this));
  }

  public bool IsImported(ModuleDeclared d)
  {
    return !(d == static_module || d == module);
  }

  public IEnumerable<ModuleDeclared> GetModules()
  {
    yield return static_module;

    foreach(var kv in modules)
      yield return kv.Value;
  }

  void CopyFromStaticModule()
  {
    //NOTE: dumb copy of all items from the static module
    module.nfunc_index.index.AddRange(static_module.nfunc_index.index);
    ns.members.UnionWith(static_module.ns.members);
  }

  public void RegisterModule(ModuleDeclared m)
  {
    m.AssignId();
    modules.Add(m.name, m);
  }

  public ModuleDeclared FindRegisteredModule(string name)
  {
    modules.TryGetValue(name, out var m);
    return m;
  }

  public INamed ResolveNamedByPath(NamePath path)
  {
    return ns.ResolveSymbolByPath(path);
  }
}

}
