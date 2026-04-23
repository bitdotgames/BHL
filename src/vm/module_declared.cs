using System;
using System.Threading;

namespace bhl
{

public class ModuleDeclared : INamedResolver
{
  public int id;

  static int _seq_id = 0;
  internal static int NextId() => Interlocked.Increment(ref _seq_id);

  public string name;
  public string file_path;
  public Namespace ns;

  public FuncNativeModuleIndexer nfunc_index = new FuncNativeModuleIndexer();
  public VarScopeIndexer gvar_index = new VarScopeIndexer();
  public FuncModuleIndexer func_index = new FuncModuleIndexer();

  public int local_gvars_mark = -1;
  public int local_gvars_num => local_gvars_mark == -1 ? gvar_index.Count : local_gvars_mark;

  //non-null for script (compiled) modules once compiled/loaded
  public CompiledModule compiled = CompiledModule.Empty;
  public bool is_native => compiled == CompiledModule.Empty;

  public bool is_fully_setup;

  public ModuleDeclared(string name = "", string file_path = "")
  {
    this.name = name;
    this.file_path = file_path;
    this.ns = new Namespace(this, "");
  }

  public ModuleDeclared(ModulePath path)
    : this(path.name, path.file_path)
  {}

  public void InitWithCompiled(CompiledModule compiled)
  {
    this.compiled = compiled;
  }

  public void AssignId()
  {
    if(id == 0)
      id = NextId();
  }

  public void AddImportedGlobalVars(ModuleDeclared imported)
  {
    //NOTE: let's add imported global vars to module's global vars index
    if(local_gvars_mark == -1)
      local_gvars_mark = gvar_index.Count;

    var idx = imported.gvar_index;
    int num = imported.local_gvars_num;

    //NOTE: adding directly without indexing
    for(int i = 0; i < num; ++i)
      gvar_index.index.Add(idx[i]);
  }

  [System.Flags]
  public enum SetupFlags
  {
    All        = 255,
    Imports    = 1,
    Funcs      = 2,
    Gvars      = 4,
    Classes    = 8,
    Namespaces = 16,
  }

  public void Setup(Func<string, ModuleDeclared> import2module)
  {
    Setup(import2module, SetupFlags.All);
    is_fully_setup = true;
  }

  public void Setup(Func<string, ModuleDeclared> import2module, SetupFlags flags)
  {
    if(flags.HasFlag(SetupFlags.Imports))
    {
      foreach(var imp in compiled.imports)
        ns.Link(import2module(imp).ns);
    }

    ns.ForAllLocalSymbols(delegate(Symbol s)
      {
        if(flags.HasFlag(SetupFlags.Funcs) && s is FuncSymbolScript fs)
          SetupFuncSymbol(fs);
        else if(flags.HasFlag(SetupFlags.Funcs) && s is FuncSymbolVirtual fssv &&
                fssv.GetTopOverride() is FuncSymbolScript vsf)
          SetupFuncSymbol(vsf);
        else if(flags.HasFlag(SetupFlags.Gvars) && s is VariableSymbol vs && vs.scope is Namespace)
          gvar_index.index.Add(vs);
        else if(flags.HasFlag(SetupFlags.Classes) && s is ClassSymbol cs)
          cs.Setup();
        else if(flags.HasFlag(SetupFlags.Namespaces) && s is Namespace sns)
          sns.module = this;
      }
    );

    compiled.ResolveTypeRefs();
  }

  public FuncSymbolScript TryMapIp2Func(int ip)
  {
    FuncSymbolScript fsymb = null;
    ns.ForAllLocalSymbols(delegate(Symbol s)
    {
      if(s is FuncSymbolScript ftmp && ftmp._ip_addr == ip)
        fsymb = ftmp;
      else if(s is FuncSymbolVirtual fsv && fsv.GetTopOverride() is FuncSymbolScript fssv && fssv._ip_addr == ip)
        fsymb = fssv;
    });
    return fsymb;
  }

  void SetupFuncSymbol(FuncSymbolScript fss)
  {
    //NOTE: the func symbol might be from another module (e.g in case of class inheritance with the base class
    //      located in a different module). Here we check if module was already set up and if so ignore the symbol
    if(fss._module != null)
      return;

    //interface methods are skipped
    if(fss.scope is InterfaceSymbol)
      return;

    //NOTE: let's ignore not 'our' functions, e.g methods from other base classes located in other modules.
    //      This is especially important for cases when the cached module is being setup during the compilation,
    //      while the module which actually contains the func symbol is not yet parsed and the function is not
    //      assigned ip_addr yet
    if(fss.GetModule()?.name != name)
      return;

    if(fss._ip_addr == -1)
      throw new Exception("Func " + fss.GetFullTypePath() + " ip_addr is not set, module '" + name + "'");

    //caching declared data; serves as a guard that the symbol was set up
    fss._module = this;

    //TODO: there's definitely questionable code duplication - we add script functions
    //      to index when defining them in the scope them and here, when setting up the module
    func_index.index.Add(fss);
  }

  public INamed ResolveNamedByPath(NamePath path)
  {
    return ns.ResolveSymbolByPath(path);
  }
}

}
