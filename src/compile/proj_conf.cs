using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace bhl;

//NOTE: representing only part of the original ProjectConf
public class ProjectConfShort
{
  public static ProjectConfShort ReadFromFile(string file_path)
  {
    var proj = JsonConvert.DeserializeObject<ProjectConfShort>(File.ReadAllText(file_path));
    proj.proj_file = file_path;
    proj.Setup();
    return proj;
  }

  [JsonIgnore] public string proj_file = "";

  //NOTE: 1) if there are .bhl scripts they will be built into a bindings_dll (if it's present)
  //      2) if there are .cs sources they will be built into a bindings_dll
  public List<string> bindings_sources = new List<string>();

  //NOTE: 1) in case of .bhl bindings it's assumed to have a .bhc extension
  //      2) in case of .cs sources this can be a directory path as well containing an actual dll
  //         (e.g. bindings.dll/bindings.dll)
  public string bindings_dll = "";

  //NOTE: list of .cs sources which are built into posproc_dll
  public List<string> postproc_sources = new List<string>();

  //NOTE: this can be a directory path as well containing an actual dll
  //      (posproc.dll/postproc.dll)
  public string postproc_dll = "";

  public void Setup()
  {
    for (int i = 0; i < bindings_sources.Count; ++i)
      bindings_sources[i] = NormalizePath(proj_file, bindings_sources[i]);
    bindings_dll = NormalizePath(proj_file, bindings_dll);

    for (int i = 0; i < postproc_sources.Count; ++i)
      postproc_sources[i] = NormalizePath(proj_file, postproc_sources[i]);
    postproc_dll = NormalizePath(proj_file, postproc_dll);
  }

  public static string NormalizePath(string proj_file, string file_path)
  {
    if(Path.IsPathRooted(file_path))
      return BuildUtils.NormalizeFilePath(file_path);

    if(!string.IsNullOrEmpty(proj_file) && !string.IsNullOrEmpty(file_path) && file_path[0] == '.')
      return BuildUtils.NormalizeFilePath(Path.Combine(Path.GetDirectoryName(proj_file), file_path));

    return file_path;
  }
}

public class ProjectConf : ProjectConfShort
{
  const string FILE_NAME = "bhl.proj";

  public new static ProjectConf ReadFromFile(string file_path)
  {
    var proj = JsonConvert.DeserializeObject<ProjectConf>(File.ReadAllText(file_path));
    proj.proj_file = file_path;
    proj.Setup();
    return proj;
  }

  public static void WriteToFile(ProjectConf proj, string file_path)
  {
    File.WriteAllText(file_path, JsonConvert.SerializeObject(proj));
  }

  public static ProjectConf TryReadFromDir(string dir_path)
  {
    string proj_file = dir_path + "/" + FILE_NAME;
    if(!File.Exists(proj_file))
      return null;
    return ReadFromFile(proj_file);
  }

  public ModuleBinaryFormat module_fmt = ModuleBinaryFormat.FMT_LZ4;

  public List<string> inc_dirs = new List<string>();
  [JsonIgnore] public IncludePath inc_path = new IncludePath();

  public List<string> src_dirs = new List<string>();

  public List<string> defines = new List<string>();

  public string result_file = "";
  public string tmp_dir = "";
  public string error_file = "";
  public bool use_cache = true;
  public int verbosity = 1;
  public int max_threads = 1;
  public bool deterministic = false;

  public const string DefaultBindingsScriptName = "RegisterBindings";

  public new void Setup()
  {
    base.Setup();

    for(int i = 0; i < inc_dirs.Count; ++i)
    {
      inc_dirs[i] = NormalizePath(proj_file, inc_dirs[i]);
      inc_path.Add(inc_dirs[i]);
    }

    for(int i = 0; i < src_dirs.Count; ++i)
    {
      src_dirs[i] = NormalizePath(proj_file, src_dirs[i]);
      if(inc_dirs.Count == 0)
        inc_path.Add(src_dirs[i]);
    }

    result_file = NormalizePath(proj_file, result_file);
    tmp_dir = NormalizePath(proj_file, tmp_dir);
    error_file = NormalizePath(proj_file, error_file);
  }

  bool TryGetScriptedBindings(
      out List<string> bindings_scripts,
      out string func_name,
      out string bindings_bytecode_file
      )
  {
    //TODO: make it configurable as well?
    func_name = DefaultBindingsScriptName;

    var tmp_scripts = bindings_sources.Where(f => f.EndsWith(".bhl")).ToList();
    bindings_scripts = new List<string>();
    foreach(var s in tmp_scripts)
      bindings_scripts.AddRange(BuildUtils.Glob(s));

    bindings_bytecode_file = null;
    if(!string.IsNullOrEmpty(bindings_dll) && bindings_dll.EndsWith(".bhc"))
      bindings_bytecode_file = bindings_dll;

    return bindings_scripts.Count > 0 || !string.IsNullOrEmpty(bindings_bytecode_file);
  }

  public IUserBindings LoadBindings()
  {
    if(!string.IsNullOrEmpty(bindings_dll) && bindings_dll.EndsWith(".dll"))
      return new DllBindings(bindings_dll);

    if(TryGetScriptedBindings(out var bindings_scripts, out string func_name, out var bindings_bytecode_file))
      return new ScriptedBindings(bindings_scripts, func_name, use_cache, bindings_bytecode_file);

    return new EmptyUserBindings();
  }

  public IFrontPostProcessor LoadPostprocessor()
  {
    if(!string.IsNullOrEmpty(postproc_dll))
      return new DllPostProcessor(postproc_dll);

    return new EmptyPostProcessor();
  }
}
