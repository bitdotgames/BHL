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
  
  public List<string> imports {
    get {
      return compiled.imports;
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

  public void Setup(Func<string, Module> import2module)
  {
    _imported = new Module[compiled.imports.Count];

    for(int i = 0; i < compiled.imports.Count; ++i)
    {
      var imported = import2module(compiled.imports[i]);
      if(imported == null)
        throw new Exception("Module '" + compiled.imports[i] + "' not found");

      _imported[i] = imported;
    }

    foreach(var imp in compiled.imports)
      ns.Link(import2module(imp).ns);
    
    //NOTE: currently local symbols might include symbols located in different modules (
    //      other than our namespace), e.g with the base class members located in a different
    //      module. This must be taken into account.
    ns.ForAllLocalSymbols(delegate(Symbol s)
      {
        if(s is Namespace ns)
          ns.module = this;
        else if(s is ClassSymbol cs)
          cs.Setup();
        else if(s is VariableSymbol vs && vs.scope is Namespace)
          gvar_index.index.Add(vs);
        else if(s is FuncSymbolScript fs)
          SetupFuncSymbol(fs);
        else if(s is FuncSymbolVirtual fssv && fssv.GetTopOverride() is FuncSymbolScript vsf)
          SetupFuncSymbol(vsf);
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

  void SetupFuncSymbol(FuncSymbolScript fss)
  {
    //NOTE: the func symbol might be from another module (e.g in case of class inheritance with the base class
    //      located in a different module). Here we check if module was already set and if so ignore the symbol
    if(fss._module != null)
      return;

    //interface methods are skipped
    if(fss.scope is InterfaceSymbol)
      return;

    //NOTE: let's ignore not 'our' functions, e.g methods from other base classes located in other modules.
    //      This is especially important for cases when the cached module is being setup during the compilation,
    //      while the module which actually contains the func symbol is not yet parsed and the function is not
    //      assigned ip_addr yet
    if(fss.GetModule().name != name)
      return;
    
    if(fss.ip_addr == -1)
      throw new Exception("Func " + fss.GetFullPath() + " ip_addr is not set, module '" + name + "'");
    
    //for faster runtime module lookups we cache it here
    fss._module = this;

    //TODO: there's definitely questionable code duplication - we add script functions
    //      to index when defining them in the scope them and here, when setting up the module
    func_index.index.Add(fss);
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
