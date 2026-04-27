using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace bhl
{

public partial class VM : INamedResolver
{
  Dictionary<ModuleDeclared, Module> modules = new Dictionary<ModuleDeclared, Module>();
  internal Module[] modules_by_id = new Module[8];

  internal class LoadingModule
  {
    internal string name;
    internal Module loaded;
  }

  List<LoadingModule> loading_modules = new List<LoadingModule>();

  public struct ModuleSymbol
  {
    public Module module;
    public Symbol symbol;
  }

  IModuleLoader loader;

  Dictionary<SymbolSpec, ModuleSymbol> symbol_spec2module_cache = new Dictionary<SymbolSpec, ModuleSymbol>();

  static int trampoline_ids_seq = 0;
  FuncSymbolScript[] trampolines_cache = new FuncSymbolScript[ToNextNearestPow2(trampoline_ids_seq + 1)];

  public enum LoadModuleSymbolError
  {
    Ok,
    ModuleNotFound,
    SymbolNotFound
  }

  public bool LoadModule(string module_name)
  {
    if(loading_modules.Count > 0)
      throw new Exception("Already loading modules");

    //let's check if it's already loaded
    if(!TryAddToLoadingList(module_name))
      return true;

    if(loading_modules.Count == 0)
      return false;

    //NOTE: initing modules in reverse order
    for(int i = loading_modules.Count; i-- > 0;)
      Init_Phase2(loading_modules[i].loaded);
    for(int i = loading_modules.Count; i-- > 0;)
      Init_Phase3(loading_modules[i].loaded);

    loading_modules.Clear();

    return true;
  }

  //NOTE: this method is public only for testing convenience
  public void LoadModule(Module module)
  {
    Init_Phase1(module);
    Init_Phase2(module);
    Init_Phase3(module);
  }

  //TODO: store a 'by name' parallel dictionary?
  public Module FindModule(string module_name)
  {
    foreach(var kv in modules)
      if(kv.Key.name == module_name)
        return kv.Value;

    return null;
  }

  void RegisterModule(Module module)
  {
    int id = module.decl.id;
    if(id == 0)
      throw new Exception("Module is not assigned id");

    modules[module.decl] = module;

    if(modules_by_id.Length <= id)
      Array.Resize(ref modules_by_id, ToNextNearestPow2(id + 1));
    modules_by_id[id] = module;
  }

  //NOTE: returns false if module is already loaded
  bool TryAddToLoadingList(string module_name)
  {
    //let's check if it's already available
    if(FindModule(module_name) != null)
      return false;

    //let's check if it's already loading
    foreach(var tmp in loading_modules)
      if(tmp.name == module_name)
        return false;

    if(loader == null)
      return false;

    var lm = new LoadingModule();
    lm.name = module_name;
    loading_modules.Add(lm);

    var declared = loader.Load(module_name, this);

    //if no such a module let's remove it from the loading list
    if(declared == null)
    {
      loading_modules.Remove(lm);
    }
    else
    {
      var module = new Module(declared);
      //let's add all imported modules as well
      foreach(var imported in declared.imports)
        TryAddToLoadingList(imported);
      lm.loaded = module;

      Init_Phase1(module);
    }

    return true;
  }

  void Init_Phase1(Module module)
  {
    RegisterModule(module);
  }

  void Init_Phase2(Module module)
  {
    module.decl.Setup(name => FindModule(name).decl);
    module.Setup(name => FindModule(name));
    ExecInitByteCode(module);
  }

  void Init_Phase3(Module module)
  {
    module.ImportGlobalVars();
    ExecModuleInitFunc(module);
  }

  public void UnloadModule(string module_name)
  {
    foreach(var kv in modules)
    {
      if(kv.Key.name == module_name)
      {
        UnloadModule(kv.Key);
        return;
      }
    }
  }

  void UnloadModule(ModuleDeclared decl)
  {
    modules[decl].ClearGlobalVars();
    modules.Remove(decl);
  }

  public void UnloadModules()
  {
    var keys = new List<ModuleDeclared>();
    foreach(var kv in modules)
      keys.Add(kv.Key);
    foreach(var key in keys)
      UnloadModule(key);

    loading_modules.Clear();
    symbol_spec2module_cache.Clear();
    Array.Clear(trampolines_cache, 0, trampolines_cache.Length);
  }

  public LoadModuleSymbolError TryLoadModuleSymbol(SymbolSpec spec, out ModuleSymbol ms, bool use_cache = true)
  {
    ms = default;

    if(use_cache && symbol_spec2module_cache.TryGetValue(spec, out ms))
      return LoadModuleSymbolError.Ok;

    if(!LoadModule(spec.module))
      return LoadModuleSymbolError.ModuleNotFound;

    var symb = ResolveNamedByPath(spec.path) as Symbol;
    if(symb == null)
      return LoadModuleSymbolError.SymbolNotFound;

    //TODO: should we actually check if loaded module matches
    //      the module where the found symbol actually resides?
    var module = modules[((Namespace)symb.scope).module];

    ms.module = module;
    ms.symbol = symb;

    if(use_cache)
      symbol_spec2module_cache[spec] = ms;

    return LoadModuleSymbolError.Ok;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static int ToNextNearestPow2(int x)
  {
    if(x < 0)
      return 0;
    --x;
    x |= x >> 1;
    x |= x >> 2;
    x |= x >> 4;
    x |= x >> 8;
    x |= x >> 16;
    return x + 1;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public FuncSymbolScript GetOrMakeFuncTrampoline(ref int trampoline_idx, string module, string path)
  {
    int idx = trampoline_idx;
    //NOTE: unsigned cast covers both idx <= 0 and idx >= Length in one comparison
    if((uint)idx < (uint)trampolines_cache.Length)
    {
      var fs = trampolines_cache[idx];
      if(fs != null)
        return fs;
    }
    return GetOrMakeFuncTrampolineSlow(ref trampoline_idx, module, path);
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  FuncSymbolScript GetOrMakeFuncTrampolineSlow(ref int trampoline_idx, string module, string path)
  {
    //NOTE: 0 idx is assumed to be 'unassigned'
    if(trampoline_idx == 0)
      trampoline_idx = Interlocked.Increment(ref trampoline_ids_seq);

    if(trampolines_cache.Length <= trampoline_idx)
      Array.Resize(ref trampolines_cache, ToNextNearestPow2(trampoline_idx + 1));

    var err = TryLoadModuleSymbol(new SymbolSpec(module, path), out var ms);
    if(err != 0)
      throw new Exception($"Module '{module}' symbol '{path}' not found: {err}");

    var fs = (FuncSymbolScript)ms.symbol;
    trampolines_cache[trampoline_idx] = fs;
    return fs;
  }

}

}
