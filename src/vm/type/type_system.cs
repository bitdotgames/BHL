using System;
using System.Collections.Generic;
using bhl.marshall;

namespace bhl
{

public partial class Types : INamedResolver, IProxyTypeCache
{
  //NOTE: keyed by bindings entry name, since several distinct bindings can register
  //      into the same Types - see BindingsRegistry.TryGetVersion
  Dictionary<string, string> bindings_versions = new Dictionary<string, string>();

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

    BindingsRegistry.RegisterForType(this, BindingsRegistry.PreludeName);
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

  public void RegisterBindingsVersion(string name, string version)
  {
    bindings_versions.Add(name, version);
  }

  public bool TryGetBindingsVersion(string name, out string version)
  {
    return bindings_versions.TryGetValue(name, out version);
  }

  //NOTE: lets a caller diff before/after Register() to see what it self-declared
  //      (see ProjectConf.LoadBindings) - never empty, a fresh Types() already has "prelude"
  public IEnumerable<string> BindingsVersionNames => bindings_versions.Keys;

  public INamed ResolveNamedByPath(NamePath path)
  {
    var found = ns.ResolveSymbolByPath(path);
    if(found != null)
      return found;

    //NOTE: mirrors what import's Link() does for a real script - try each module's own ns
    foreach(var m in modules.Values)
    {
      var found_in_module = m.ns.ResolveSymbolByPath(path);
      if(found_in_module != null)
        return found_in_module;
    }

    return null;
  }
}

//NOTE: the built-in std/std.io/std.bind modules, unified under BindingsRegistry like any
//      other binding so Types() bootstraps through the same RegisterForType() path
[BhlBinding(BindingsRegistry.PreludeName, "1.0.0")]
public class PreludeBindings : IUserBindings
{
  //NOTE: module initializers aren't guaranteed to fire under IL2CPP, so Unity gets its
  //      own reliable hooks instead - RuntimeInitializeOnLoadMethod for Player/Play mode,
  //      InitializeOnLoadMethod so it also happens in the Editor outside Play
#if UNITY_5_3_OR_NEWER
#if UNITY_EDITOR
  [UnityEditor.InitializeOnLoadMethod]
#endif
  [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
#else
  [System.Runtime.CompilerServices.ModuleInitializer]
#endif
  internal static void Init() => BindingsRegistry.Register<PreludeBindings>();

  public void Register(Types ts)
  {
    ts.RegisterModule(std.MakeModule(ts));
    ts.RegisterModule(std.io.MakeModule(ts));
    ts.RegisterModule(std.bind.MakeModule(ts));
  }
}

}
