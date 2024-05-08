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
//      the one created in C#
public class Module
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
  public ModulePath path;
  
  public Namespace ns;

  public bool is_native = true;
  
  //used for assigning incremental indexes to module global vars,
  //contains imported variables as well
  public VarScopeIndexer gvar_index = new VarScopeIndexer();
  
  public FixedStack<Val> gvar_vals = new FixedStack<Val>(MAX_GLOBALS);
  
  //used for assigning incremental indexes to native funcs,
  //NOTE: currently this indexer is global, while it must be local per module
  public NativeFuncScopeIndexer nfunc_index;
  
  //used for assigning incremental module indexes to funcs
  public FuncModuleIndexer func_index = new FuncModuleIndexer();

  //TODO: probably we need script functions per module indexer, like gvars?
  //setup once the module is loaded to find functions by their ip
  internal Dictionary<int, FuncSymbolScript> _ip2func = new Dictionary<int, FuncSymbolScript>();

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

  //NOTE: below are compiled module parts
  //normalized module names, not actual import paths
  public List<string> imports;
  public byte[] initcode;
  public byte[] bytecode;
  //filled during runtime module setup procedure since
  //until this moment we don't know about other modules 
  internal Module[] _imported;
  public List<Const> constants;
  public Ip2SrcLine ip2src_line;
  public int init_func_idx = -1;
  
  public Module(Types ts, ModulePath path)
    : this(ts, path, new Namespace())
  {}

  public Module(Types ts, string name = "", string file_path = "")
    : this(ts, new ModulePath(name, file_path), new Namespace())
  {}

  public Module(Types ts, ModulePath path, Namespace ns)
  {
    nfunc_index = ts.nfunc_index;
    //let's setup the link
    ns.module = this;
    this.path = path;
    this.ns = ns;
  }

  public void InitCompiled(
    int init_func_idx,
    int total_gvars_num,
    List<string> imports,
    List<Const> constants, 
    byte[] initcode,
    byte[] bytecode, 
    Ip2SrcLine ip2src_line
    )
  {
    is_native = false;
    
    this.init_func_idx = init_func_idx;
    this.imports = imports;
    this.constants = constants;
    this.initcode = initcode;
    this.bytecode = bytecode;
    this.ip2src_line = ip2src_line;
    gvar_vals.Resize(total_gvars_num);
  }

  //convenience version for tests
  public void InitCompiled()
  {
    InitCompiled(
      -1,
      0,
      new List<string>(),
      new List<Const>(),
      new byte[0],
      new byte[0],
      new Ip2SrcLine()
    );
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
    foreach(var imp in imports)
      ns.Link(name2module(imp).ns);
      
    _imported = new Module[imports.Count];
    
    for(int i = 0; i < imports.Count; ++i)
    {
      var imported = name2module(imports[i]);
      if(imported == null)
        throw new Exception("Module '" + imports[i] + "' not found");

      _imported[i] = imported;
    }

    ns.ForAllLocalSymbols(delegate(Symbol s)
      {
        if(s is Namespace ns)
          ns.module = this;
        else if(s is ClassSymbol cs)
        {
          cs.Setup();

          foreach(var kv in cs._vtable)
          {
            if(kv.Value is FuncSymbolScript vfs)
              PrepareFuncSymbol(vfs, name2module);
          }
        }
        else if(s is VariableSymbol vs && vs.scope is Namespace)
          gvar_index.index.Add(vs);
        else if (s is FuncSymbolScript fs)
          PrepareFuncSymbol(fs, name2module);
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
  
  void PrepareFuncSymbol(FuncSymbolScript fss, Func<string, Module> name2module)
  {
    if(fss.scope is InterfaceSymbol)
      return;
    
    if(fss.ip_addr == -1)
      throw new Exception("Func ip_addr is not set: " + fss.GetFullPath());
    
    _ip2func[fss.ip_addr] = fss;

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
