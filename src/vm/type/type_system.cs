using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
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

  //NOTE: O(1) access to a binding's own types, keyed by a tag type it declares. Index global,
  //      symbol per Types - several Types coexist, their symbols compare unequal. Never serialized
  static int _type_index_count;

  public static int NextTypeIndex()
  {
    return Interlocked.Increment(ref _type_index_count) - 1;
  }

  //NOTE: one static per closed generic - tags should be structs, so it's specialized
  static class TypeIndexOf<T>
  {
    public static readonly int idx = NextTypeIndex();
  }

  //NOTE: written while registering bindings, before the compile goes parallel - no locking.
  //      System.Array: a bare 'Array' is bhl's own one here
  IType[] _type_index = System.Array.Empty<IType>();

  //NOTE: throws for a never indexed tag - use FindIndexedType when the binding may not be there
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public IType GetIndexedType<T>()
  {
    return _type_index[TypeIndexOf<T>.idx];
  }

  //NOTE: null for a never indexed tag, e.g. a project whose bhl.proj doesn't list the binding
  public IType FindIndexedType<T>()
  {
    int idx = TypeIndexOf<T>.idx;
    return idx < _type_index.Length ? _type_index[idx] : null;
  }

  public void IndexType<T>(IType t)
  {
    int idx = TypeIndexOf<T>.idx;
    //NOTE: to the tag count, not idx, so many types resize once. Max() as the unfenced
    //      count may lag another thread's
    if(idx >= _type_index.Length)
      System.Array.Resize(ref _type_index, System.Math.Max(idx + 1, _type_index_count));
    _type_index[idx] = t;
  }

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

    RegisterBindings(new PreludeBindings());
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

  //NOTE: attaches bindings the caller already holds, instead of having BindingsRegistry find
  //      them by name. Only the instance's own type is reflected, for its [BhlBinding] - no
  //      domain scan, which under IL2CPP materializes metadata that is never released
  public void RegisterBindings(IUserBindings bindings)
  {
    var attr = (BhlBindingAttribute)System.Attribute.GetCustomAttribute(
      bindings.GetType(), typeof(BhlBindingAttribute));

    //NOTE: no attribute means it declares its version itself, if at all
    if(attr != null)
      RegisterBindingsVersion(attr.Name, attr.Version);

    bindings.Register(this);
  }

  public void RegisterBindingsVersion(string name, string version)
  {
    bindings_versions.Add(name, version);
  }

  public bool TryGetBindingsVersion(string name, out string version)
  {
    return bindings_versions.TryGetValue(name, out version);
  }

  //NOTE: finishes a type declared by bindings once all of them are defined: resolves it by name
  //      and sets up a native class or interface (their parents and interfaces)
  public IType SetupType(string name)
  {
    var tmp = this.T(name).Get();
    if(tmp == null)
      throw new Exception("Type '" + name + "' not resolved");

    if(tmp is ClassSymbolNative csn)
      csn.Setup();
    else if(tmp is InterfaceSymbolNative isn)
      isn.Setup();

    return tmp;
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

//NOTE: the built-in std/std.io/std.bind modules. Types() attaches them directly (no registry
//      scan); BindingsRegistry still lists them like any other binding (IsRegistered,
//      TryGetVersion, a bytecode requiring them), discovered by reflection - [Preserve] keeps
//      IL2CPP/Mono from stripping it as unreferenced by the registry
#if UNITY_5_3_OR_NEWER
[UnityEngine.Scripting.Preserve]
#endif
[BhlBinding(BindingsRegistry.PreludeName, Version)]
public class PreludeBindings : IUserBindings
{
  public const string Version = "1.0.0";

  public void Register(Types ts)
  {
    ts.RegisterModule(std.MakeModule(ts));
    ts.RegisterModule(std.io.MakeModule(ts));
    ts.RegisterModule(std.bind.MakeModule(ts));
  }
}

}
