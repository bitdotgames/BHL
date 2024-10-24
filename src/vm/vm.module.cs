using System;
using System.Collections.Generic;

namespace bhl {

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

  Dictionary<SymbolSpec, ModuleSymbol> symbol_spec2module = new Dictionary<SymbolSpec, ModuleSymbol>();
  Dictionary<object, ModuleSymbol> cache_tag2module = new Dictionary<object, ModuleSymbol>();

  public enum LoadModuleSymbolError
  {
    Ok,
    ModuleNotFound,
    SymbolNotFound
  }

  public bool LoadModule(string module_name)
  {
    //Console.WriteLine("==START LOAD " + module_name);
    if(loading_modules.Count > 0)
      throw new Exception("Already loading modules");

    //let's check if it's already loaded
    if(!TryAddToLoadingList(module_name))
      return true;

    if(loading_modules.Count == 0)
      return false;

    //NOTE: registering modules in reverse order
    for(int i=loading_modules.Count;i-- > 0;)
      FinishRegistration(loading_modules[i].loaded);
    loading_modules.Clear();

    return true;
  }

  //NOTE: this method is public only for testing convenience
  public void LoadModule(Module module)
  {
    BeginRegistration(module);
    FinishRegistration(module);
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

    //NOTE: passing self as a type proxies 'resolver'
    var loaded = loader.Load(module_name, this);

    //if no such a module let's remove it from the loading list
    if(loaded == null)
    {
      loading_modules.Remove(lm);
    }
    else
    {
      foreach(var imported in loaded.compiled.imports)
        TryAddToLoadingList(imported);
      lm.loaded = loaded;

      BeginRegistration(loaded);
    }

    return true;
  }

  void BeginRegistration(Module module)
  {
    //NOTE: for simplicity we add it to the modules at once,
    //      this is probably a bit 'smelly' but makes further
    //      symbols setup logic easier
    registered_modules[module.name] = module;

    module.InitGlobalVars(this);
  }

  void FinishRegistration(Module module)
  {
    module.Setup(name => FindModule(name));

    module.InitRuntimeGlobalVars();

    ExecInitCode(module);
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

  public LoadModuleSymbolError TryLoadModuleSymbol(object cache_tag, SymbolSpec spec, out ModuleSymbol ms)
  {
    if(cache_tag != null && cache_tag2module.TryGetValue(cache_tag, out ms))
      return LoadModuleSymbolError.Ok;

    if(symbol_spec2module.TryGetValue(spec, out ms))
    {
      if(cache_tag != null)
        cache_tag2module[cache_tag] = ms;

      return LoadModuleSymbolError.Ok;
    }

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

    symbol_spec2module[spec] = ms;

    if(cache_tag != null)
      cache_tag2module[cache_tag] = ms;

    return LoadModuleSymbolError.Ok;
  }

  public LoadModuleSymbolError TryLoadModuleSymbol(SymbolSpec spec, out ModuleSymbol ms)
  {
    return TryLoadModuleSymbol(null, spec, out ms);
  }
}

}
