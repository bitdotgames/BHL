using System;
using System.Runtime.CompilerServices;

namespace bhl
{

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

//NOTE: runtime container for a module; holds gvars and imported module refs.
//      Static/declared data (indices, bytecode, namespace) lives in ModuleDeclared.
public class Module : INamedResolver
{
  public ModuleDeclared decl;

  public string name => decl.name;
  public string file_path => decl.file_path;

  public Namespace ns => decl.ns;

  public ValStack gvars = new ValStack(16);

  //filled during runtime module setup procedure since
  //until this moment we don't know about other modules
  internal Module[] _imported;

  public Module(ModuleDeclared decl)
  {
    this.decl = decl;
    if(!decl.is_native)
      gvars.Reserve(decl.compiled.total_gvars_num);
  }

  public INamed ResolveNamedByPath(NamePath path)
  {
    return ns.ResolveSymbolByPath(path);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal void ClearGlobalVars()
  {
    gvars.ClearAndRelease();
  }

  public void Setup(Func<string, Module> import2module)
  {
    _imported = new Module[decl.compiled.imports.Count];

    for(int i = 0; i < decl.compiled.imports.Count; ++i)
    {
      var imported = import2module(decl.compiled.imports[i]);
      if(imported == null)
        throw new Exception("Module '" + decl.compiled.imports[i] + "' not found");

      _imported[i] = imported;
    }

    if(!decl.is_fully_setup)
      decl.Setup(name => import2module(name)?.decl);
  }

  public void ImportGlobalVars()
  {
    int gvars_offset = decl.local_gvars_num;

    for(int i = 0; i < _imported.Length; ++i)
    {
      var imported = _imported[i];
      //NOTE: taking only local imported module's gvars
      for(int g = 0; g < imported.decl.local_gvars_num; ++g)
      {
        ref var imp_gvar = ref imported.gvars.vals[g];
        imp_gvar._refc?.Retain();
        gvars.Reserve(gvars_offset);
        gvars.vals[gvars_offset] = imp_gvar;
        ++gvars_offset;
      }
    }
    gvars.sp = gvars_offset;
  }

}

}
