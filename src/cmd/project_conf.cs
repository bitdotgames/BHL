using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace bhl {
  
//NOTE: representing only part of the original ProjectConf
public class ProjectConfPartial
{
  public static ProjectConfPartial ReadFromFile(string file_path)
  {
    var proj = JsonConvert.DeserializeObject<ProjectConfPartial>(File.ReadAllText(file_path));
    proj.proj_file = file_path;
    proj.Setup();
    return proj;
  }

  [JsonIgnore] public string proj_file = "";

  public List<string> bindings_sources = new List<string>();
  public string bindings_dll = "";

  public List<string> postproc_sources = new List<string>();
  public string postproc_dll = "";

  string NormalizePath(string file_path)
  {
    if (Path.IsPathRooted(file_path))
      return Path.GetFullPath(file_path);
    else if (!string.IsNullOrEmpty(proj_file) && !string.IsNullOrEmpty(file_path) && file_path[0] == '.')
      return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(proj_file), file_path));
    return file_path;
  }

  public void Setup()
  {
    for (int i = 0; i < bindings_sources.Count; ++i)
      bindings_sources[i] = NormalizePath(bindings_sources[i]);
    bindings_dll = NormalizePath(bindings_dll);

    for (int i = 0; i < postproc_sources.Count; ++i)
      postproc_sources[i] = NormalizePath(postproc_sources[i]);
    postproc_dll = NormalizePath(postproc_dll);
  }
}

}