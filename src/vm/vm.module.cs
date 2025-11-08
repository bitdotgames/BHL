using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace bhl
{

public partial class VM : INamedResolver
{
  //NOTE: key is a Module's name
  Dictionary<string, Module> registered_modules = new Dictionary<string, Module>();

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
  FuncSymbolScript[] trampolines_cache = new FuncSymbolScript[128];

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

  public Module FindModule(string module_name)
  {
    var rm = types.FindRegisteredModule(module_name);
    if(rm != null)
      return rm;

    if(registered_modules.TryGetValue(module_name, out rm))
      return rm;

    return null;
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

    var lm = new LoadingModule();
    lm.name = module_name;
    loading_modules.Add(lm);

    var loaded = loader.Load(module_name, this);

    //if no such a module let's remove it from the loading list
    if(loaded == null)
    {
      loading_modules.Remove(lm);
    }
    else
    {
      //let's add all imported modules as well
      foreach(var imported in loaded.compiled.imports)
        TryAddToLoadingList(imported);
      lm.loaded = loaded;

      Init_Phase1(loaded);
    }

    return true;
  }

  void Init_Phase1(Module module)
  {
    //NOTE: for simplicity we add it to the modules at once,
    //      this is probably a bit 'smelly' but makes further
    //      symbols setup logic easier
    registered_modules[module.name] = module;
  }

  void Init_Phase2(Module module)
  {
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
    if(!registered_modules.TryGetValue(module_name, out var rm))
      return;

    rm.ClearGlobalVars();

    registered_modules.Remove(module_name);
  }

  public void UnloadModules()
  {
    var keys = new List<string>();
    foreach(var kv in registered_modules)
      keys.Add(kv.Key);
    foreach(string key in keys)
      UnloadModule(key);
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
    var rm = registered_modules[((Namespace)symb.scope).module.name];

    ms.module = rm;
    ms.symbol = symb;

    if(use_cache)
      symbol_spec2module_cache[spec] = ms;

    return LoadModuleSymbolError.Ok;
  }

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
    //NOTE: 0 idx is assumed to be 'unassigned'
    if(trampoline_idx == 0)
      trampoline_idx = Interlocked.Increment(ref trampoline_ids_seq);

    if(trampolines_cache.Length <= trampoline_idx)
      Array.Resize(ref trampolines_cache, ToNextNearestPow2(trampoline_idx + 1));

    var fs = trampolines_cache[trampoline_idx];
    if(fs == null)
    {
      var err = TryLoadModuleSymbol(new SymbolSpec(module, path), out var ms);
      if(err != 0)
        throw new Exception($"Module '{module}' symbol '{path}' not found: {err}");

      fs = (FuncSymbolScript)ms.symbol;
      trampolines_cache[trampoline_idx] = fs;
    }

    return fs;
  }
}

}
