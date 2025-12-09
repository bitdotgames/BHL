using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace bhl;

public class ProjectConf
{
  const string FILE_NAME = "bhl.proj";

  public static ProjectConf ReadFromFile(string file_path)
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

  [JsonIgnore] public string proj_file = "";

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

  public List<string> bindings_sources = new List<string>();

  //NOTE: this can be a directory path as well containing dll
  public string bindings_dll = "";

  public List<string> postproc_sources = new List<string>();

  //NOTE: this can be a directory path as well containing dll
  public string postproc_dll = "";

  public static string NormalizePath(string proj_file, string file_path)
  {
    if(Path.IsPathRooted(file_path))
      return BuildUtils.NormalizeFilePath(file_path);
    else if(!string.IsNullOrEmpty(proj_file) &&
            !string.IsNullOrEmpty(file_path) &&
            file_path[0] == '.')
      return BuildUtils.NormalizeFilePath(Path.Combine(Path.GetDirectoryName(proj_file), file_path));
    return file_path;
  }

  public void Setup()
  {
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

    for(int i = 0; i < bindings_sources.Count; ++i)
      bindings_sources[i] = NormalizePath(proj_file, bindings_sources[i]);
    bindings_dll = NormalizePath(proj_file, bindings_dll);

    for(int i = 0; i < postproc_sources.Count; ++i)
      postproc_sources[i] = NormalizePath(proj_file, postproc_sources[i]);
    postproc_dll = NormalizePath(proj_file, postproc_dll);

    result_file = NormalizePath(proj_file, result_file);
    tmp_dir = NormalizePath(proj_file, tmp_dir);
    error_file = NormalizePath(proj_file, error_file);
  }

  static System.Reflection.Assembly LoadAssemblyFromDirOrFile(string path)
  {
    return System.Reflection.Assembly.LoadFrom(
      Directory.Exists(path) ? path + "/" + Path.GetFileName(path) : path
    );
  }

  public IUserBindings LoadBindings()
  {
    if(string.IsNullOrEmpty(bindings_dll))
      return new EmptyUserBindings();

    var userbindings_assembly = LoadAssemblyFromDirOrFile(bindings_dll);
    var types = userbindings_assembly.GetTypes();

    Type userbindings_class = null;
    foreach(var type in types)
    {
      if(typeof(IUserBindings).IsAssignableFrom(type))
      {
        userbindings_class = type;
        break;
      }
    }

    if(userbindings_class == null)
      throw new Exception("IUserBindings instance not found");

    return Activator.CreateInstance(userbindings_class) as IUserBindings;
  }

  public IFrontPostProcessor LoadPostprocessor()
  {
    if(string.IsNullOrEmpty(postproc_dll))
      return new EmptyPostProcessor();

    var postproc_assembly = LoadAssemblyFromDirOrFile(postproc_dll);
    var types = postproc_assembly.GetTypes();

    var postproc_classes = new List<Type>();
    foreach(var type in types)
    {
      if(typeof(IFrontPostProcessor).IsAssignableFrom(type))
        postproc_classes.Add(type);
    }

    if(postproc_classes.Count == 0)
      throw new Exception("IFrontPostProcessor instance not found");

    if(postproc_classes.Count == 1)
      return InstantiatePostProc(postproc_classes[0]);

    var postproc_instances = postproc_classes.Select(InstantiatePostProc).ToList();
    return new CombinedPostProcessor(postproc_instances);
  }

  static IFrontPostProcessor InstantiatePostProc(System.Type type)
  {
    return Activator.CreateInstance(type) as IFrontPostProcessor;
  }
}
