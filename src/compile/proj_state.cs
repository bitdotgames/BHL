using System;
using System.Collections.Generic;

namespace bhl;

public class ProjectCompilationStateBundle
{
  public class InterimParseResult
  {
    public ModulePath module_path;
    public string compiled_file;
    public FileImports imports_maybe;

    public ANTLR_Parsed parsed;

    //NOTE: if not null, it's a compiled module cached result
    public ModuleDeclared cached;
  }

  public Types types;

  public List<CompilationExecutor.ParseWorker> parse_workers;

  public Dictionary<string, InterimParseResult> file2parsed = new Dictionary<string, InterimParseResult>();

  public Dictionary<string, ANTLR_Processor> file2proc = new Dictionary<string, ANTLR_Processor>();

  //NOTE: can be null, contains already cached compile modules.
  //      An entry present in file2compiled *doesn't exist* in file2proc
  public Dictionary<string, ModuleDeclared> file2cached = new Dictionary<string, ModuleDeclared>();

  public ProjectCompilationStateBundle(Types types)
  {
    this.types = types;
  }

  public ModuleDeclared FindModule(string file_path)
  {
    //let's check if it's a compiled module and
    //try to fetch it from the cache first
    if(file2cached != null && file2cached.TryGetValue(file_path, out var cm))
      return cm;
    else if(file2proc.TryGetValue(file_path, out var proc))
      return proc.module;
    else
      return null;
  }

  public ModuleDeclared GetModule(string file_path)
  {
    var m = FindModule(file_path);
    if(m == null)
      throw new Exception("No such module found '" + file_path + "'");
    return m;
  }

  public Dictionary<string, ModuleDeclared> GroupModulesByName()
  {
    var all = new Dictionary<string, ModuleDeclared>();

    //NOTE: adding globally registered native modules
    foreach(var kv in types.modules)
      all.Add(kv.Key, kv.Value);

    if(file2cached != null)
    {
      foreach(var kv in file2cached)
        all.Add(kv.Value.name, kv.Value);
    }

    foreach(var kv in file2proc)
      all.Add(kv.Value.module.name, kv.Value.module);

    return all;
  }
}
