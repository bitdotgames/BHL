using System;
using System.Collections.Generic;

namespace bhl
{

public class GlobalVarMigration
{
  public string name;
  //false if added/removed/type-changed - reset to the new module's own init value
  public bool migrated;
}

public class ReloadReport
{
  public string module_name;
  public List<GlobalVarMigration> globals = new List<GlobalVarMigration>();
}

public partial class VM : INamedResolver
{
  //NOTE: old module's frames/fibers keep running against it unaffected, since it's never
  //      mutated; only name-based resolution after this returns sees the new version.
  //      Call between ticks, never while this VM is mid-Tick().
  public ReloadReport Reload(Module new_module)
  {
    var old_module = FindModule(new_module.name);
    if(old_module == null)
      throw new Exception($"Module '{new_module.name}' is not loaded, use LoadModule() for the initial load");

    new_module.decl.AssignId();

    Init_Phase1(new_module);
    Init_Phase2(new_module);
    Init_Phase3(new_module);

    var report = new ReloadReport() { module_name = new_module.name };
    MigrateGlobalVars(old_module, new_module, report);

    modules.Remove(old_module.decl);

    return report;
  }

  static void MigrateGlobalVars(Module old_module, Module new_module, ReloadReport report)
  {
    var old_index = old_module.decl.gvar_index;
    var new_index = new_module.decl.gvar_index;

    for(int i = 0; i < old_module.decl.local_gvars_num; ++i)
    {
      var old_sym = old_index[i];

      var entry = new GlobalVarMigration() { name = old_sym.name };
      report.globals.Add(entry);

      int ni = new_index.IndexOf(old_sym.name);
      if(ni == -1 || ni >= new_module.decl.local_gvars_num)
        continue;

      var new_sym = new_index[ni];
      if(!new_sym.type.Equals(old_sym.type))
        continue;

      ref var new_val = ref new_module.gvars.vals[ni];
      new_val.ReleaseData();

      ref var old_val = ref old_module.gvars.vals[i];
      old_val.RetainData();

      new_val = old_val;
      entry.migrated = true;
    }
  }

  //NOTE: safe only for importers compiled with ModuleCompiler.indirect_imports=true;
  //      a direct-mode caller has the callee's ip baked as a literal operand, which
  //      would now point at garbage in the new module's bytecode. Not automatic/cascading.
  public void RelinkImports(string imported_module_name)
  {
    var fresh = FindModule(imported_module_name);
    if(fresh == null)
      throw new Exception($"Module '{imported_module_name}' is not loaded");

    foreach(var kv in modules)
    {
      var importer = kv.Value;
      if(importer == fresh)
        continue;

      var import_names = importer.decl.imports;
      for(int i = 0; i < import_names.Count; ++i)
      {
        if(import_names[i] == imported_module_name)
          importer._imported[i] = fresh;
      }
    }
  }

  //NOTE: retags `instance` to whatever is now the current class of that name and rebuilds
  //      its field storage, matching old fields to new ones by name+type (same idea as
  //      MigrateGlobalVars, per-instance). Methods aren't migrated since dispatch already
  //      reads them fresh off the (now updated) instance type on every call.
  //      Only updates this one `Val` - other copies referencing the same old instance
  //      elsewhere are unaffected. A no-op if the class wasn't reloaded.
  public void MigrateInstance(ref Val instance)
  {
    if(instance.type is not ClassSymbolScript old_class)
      return;

    if(ResolveNamedByPath(((Symbol)old_class).GetFullTypePath()) is not ClassSymbolScript new_class ||
       new_class == old_class)
      return;

    var old_vl = (ValList)instance.obj;
    var new_vl = ValList.New(this);

    for(int i = 0; i < new_class._all_members.Length; ++i)
    {
      var m = new_class._all_members[i];
      if(m is VariableSymbol new_field)
      {
        if(TryFindFieldValue(old_class, old_vl, new_field, out var v))
        {
          v.RetainData();
          new_vl.Add(v);
        }
        else
        {
          var dv = new Val();
          InitDefaultVal(new_field.type.Get(), ref dv);
          new_vl.Add(dv);
        }
      }
      else
        new_vl.Add(Null);
    }

    old_vl.Release();

    instance.SetObj(new_vl, new_class);
  }

  static bool TryFindFieldValue(ClassSymbolScript old_class, ValList old_vl, VariableSymbol new_field, out Val value)
  {
    for(int i = 0; i < old_class._all_members.Length; ++i)
    {
      if(old_class._all_members[i] is VariableSymbol old_field &&
         old_field.name == new_field.name &&
         old_field.type.Equals(new_field.type))
      {
        value = old_vl[i];
        return true;
      }
    }

    value = default;
    return false;
  }
}

}
