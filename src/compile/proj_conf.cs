using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace bhl {
  
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

  public static ProjectConf TryReadFromDir(string dir_path)
  {
    string proj_file = dir_path + "/" + FILE_NAME; 
    if(!File.Exists(proj_file))
      return null;
    return ReadFromFile(proj_file);
  }

  [JsonIgnore]
  public string proj_file = "";

  public ModuleBinaryFormat module_fmt = ModuleBinaryFormat.FMT_LZ4;

  public List<string> inc_dirs = new List<string>();
  [JsonIgnore]
  public IncludePath inc_path = new IncludePath();

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
  public string bindings_dll = "";

  public List<string> postproc_sources = new List<string>();
  public string postproc_dll = "";

  string NormalizePath(string file_path)
  {
    if(Path.IsPathRooted(file_path))
      return BuildUtils.NormalizeFilePath(file_path);
    else if(!string.IsNullOrEmpty(proj_file) && !string.IsNullOrEmpty(file_path) && file_path[0] == '.')
      return BuildUtils.NormalizeFilePath(Path.Combine(Path.GetDirectoryName(proj_file), file_path));
    return file_path;
  }

  public void Setup()
  {
    for(int i=0;i<inc_dirs.Count;++i)
    {
      inc_dirs[i] = NormalizePath(inc_dirs[i]);
      inc_path.Add(inc_dirs[i]);
    }

    for(int i=0;i<src_dirs.Count;++i)
    {
      src_dirs[i] = NormalizePath(src_dirs[i]);
      if(inc_dirs.Count == 0)
        inc_path.Add(src_dirs[i]);
    }

    for(int i=0;i<bindings_sources.Count;++i)
      bindings_sources[i] = NormalizePath(bindings_sources[i]);
    bindings_dll = NormalizePath(bindings_dll);

    for(int i=0;i<postproc_sources.Count;++i)
      postproc_sources[i] = NormalizePath(postproc_sources[i]);
    postproc_dll = NormalizePath(postproc_dll);

    result_file = NormalizePath(result_file);
    tmp_dir = NormalizePath(tmp_dir);
    error_file = NormalizePath(error_file);
  }

  public IUserBindings LoadBindings()
  {
    if(string.IsNullOrEmpty(bindings_dll))
      return new EmptyUserBindings();

    var userbindings_assembly = System.Reflection.Assembly.LoadFrom(bindings_dll);
    var userbindings_class = userbindings_assembly.GetTypes()[0];
    var bindings = System.Activator.CreateInstance(userbindings_class) as IUserBindings;
    return bindings;
  }

  public IFrontPostProcessor LoadPostprocessor()
  {
    if(string.IsNullOrEmpty(postproc_dll))
      return new EmptyPostProcessor();

    var postproc_assembly = System.Reflection.Assembly.LoadFrom(postproc_dll);
    var postproc_class = postproc_assembly.GetTypes()[0];
    var postproc = System.Activator.CreateInstance(postproc_class) as IFrontPostProcessor;
    return postproc;
  }
}

}
