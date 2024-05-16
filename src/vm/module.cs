using System;
using System.Collections.Generic;

namespace bhl {

public class ModulePath
{
  public string name;
  public string file_path;

  public ModulePath(string name, string file_path)
  {
    this.name = name;
    this.file_path = file_path;
  }
}

//NOTE: represents a module which can be both a compiled one or
//      the one registered in C#
public class Module : INamedResolver
{
  public const int MAX_GLOBALS = 128;
  
  public string name {
    get {
      return path.name;
    }
  }
  public string file_path {
    get {
      return path.file_path;
    }
  }

  public bool is_compiled {
    get {
      return compiled != CompiledModule.None;
    }
  }
  
  public ModulePath path;
  public Types ts; 
  public Namespace ns;

  //used for assigning incremental indexes to module global vars,
  //contains imported variables as well
  public VarScopeIndexer gvar_index = new VarScopeIndexer();
  //used for assigning incremental module indexes to funcs
  public FuncModuleIndexer func_index = new FuncModuleIndexer();
  //used for assigning incremental module indexes to native funcs
  public FuncNativeModuleIndexer nfunc_index = new FuncNativeModuleIndexer();
  
  public FixedStack<Val> gvar_vals = new FixedStack<Val>(MAX_GLOBALS);

  //if set this mark is the index starting from which 
  //*imported* module variables are stored in gvars
  public int local_gvars_mark = -1;

  //an amount of *local* to this module global variables 
  //stored in gvars
  public int local_gvars_num {
    get {
      return local_gvars_mark == -1 ? gvar_index.Count : local_gvars_mark;
    }
  }
  
  //filled during runtime module setup procedure since
  //until this moment we don't know about other modules 
  internal Module[] _imported;

  public CompiledModule compiled = CompiledModule.None;
  
  public Module(Types ts, ModulePath path)
    : this(ts, path, new Namespace())
  {}

  public Module(Types ts, string name = "", string file_path = "")
    : this(ts, new ModulePath(name, file_path), new Namespace())
  {}

  public Module(Types ts, ModulePath path, Namespace ns)
  {
    this.ts = ts;
    //let's setup the link
    ns.module = this;
    this.path = path;
    this.ns = ns;
  }
  
  public INamed ResolveNamedByPath(string name)
  {
    return ns.ResolveSymbolByPath(name);
  }

  public void InitWithCompiled(CompiledModule compiled)
  {
    this.compiled = compiled;
    gvar_vals.Resize(compiled.total_gvars_num);
  }

  public void InitGlobalVars(VM vm)
  {
    //let's init all our own global variables
    for(int g=0;g<local_gvars_num;++g)
      gvar_vals[g] = Val.New(vm); 
  }

  public void ClearGlobalVars()
  {
    for(int i=0;i<gvar_vals.Count;++i)
    {
      var val = gvar_vals[i];
      val.Release();
    }
    gvar_vals.Clear();
  }
  
  public void Setup(Func<string, Module> name2module)
  {
    foreach(var imp in compiled.imports)
      ns.Link(name2module(imp).ns);
      
    _imported = new Module[compiled.imports.Count];
    
    for(int i = 0; i < compiled.imports.Count; ++i)
    {
      var imported = name2module(compiled.imports[i]);
      if(imported == null)
        throw new Exception("Module '" + compiled.imports[i] + "' not found");

      _imported[i] = imported;
    }

    ns.ForAllLocalSymbols(delegate(Symbol s)
      {
        if(s is Namespace ns)
          ns.module = this;
        else if(s is ClassSymbol cs)
          cs.Setup();
        else if(s is VariableSymbol vs && vs.scope is Namespace)
          gvar_index.index.Add(vs);
        else if(s is FuncSymbolScript fs)
          SetupFuncSymbol(fs, name2module);
        else if(s is FuncSymbolVirtual fssv && fssv.GetTopOverride() is FuncSymbolScript vsf)
          SetupFuncSymbol(vsf, name2module);
      }
    );
  }

  public void InitRuntimeGlobalVars()
  {
    int gvars_offset = local_gvars_num;
    
    for(int i = 0; i < _imported.Length; ++i)
    {
      var imported = _imported[i];
      //NOTE: taking only local imported module's gvars
      for(int g=0;g<imported.local_gvars_num;++g)
      {
        var imp_gvar = imported.gvar_vals[g];
        imp_gvar.Retain();
        gvar_vals[gvars_offset] = imp_gvar;
        ++gvars_offset;
      }
    }
  }
  
  void SetupFuncSymbol(FuncSymbolScript fss, Func<string, Module> name2module)
  {
    if(fss.scope is InterfaceSymbol)
      return;
    
    if(fss.ip_addr == -1)
      throw new Exception("Func ip_addr is not set: " + fss.GetFullPath());
    
    func_index.index.Add(fss);
    
    if(fss._module == null)
    {
      var mod_name = fss.GetModule().name;
      fss._module = mod_name == name ? this : name2module(mod_name);
      if(fss._module == null)
        throw new Exception("Module '" + mod_name + "' not found");
    }
  }

  public void AddImportedGlobalVars(Module imported_module)
  {
    //NOTE: let's add imported global vars to module's global vars index
    if(local_gvars_mark == -1)
      local_gvars_mark = gvar_index.Count;

    //NOTE: adding directly without indexing
    for(int i=0;i<imported_module.local_gvars_num;++i)
      gvar_index.index.Add(imported_module.gvar_index[i]);
  }
}

} //namespace bhl
