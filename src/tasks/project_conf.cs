using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace bhl
{

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

  //NOTE: this is rather a path to directory containing dll
  public string bindings_dll = "";

  public List<string> postproc_sources = new List<string>();

  //NOTE: this is rather a path to directory containing dll
  public string postproc_dll = "";

  public void Setup()
  {
    for (int i = 0; i < bindings_sources.Count; ++i)
      bindings_sources[i] = ProjectConf.NormalizePath(proj_file, bindings_sources[i]);
    bindings_dll = ProjectConf.NormalizePath(proj_file, bindings_dll);

    for (int i = 0; i < postproc_sources.Count; ++i)
      postproc_sources[i] = ProjectConf.NormalizePath(proj_file, postproc_sources[i]);
    postproc_dll = ProjectConf.NormalizePath(proj_file, postproc_dll);
  }
}

}