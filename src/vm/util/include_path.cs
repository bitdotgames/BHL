
using System;
using System.Collections.Generic;
using System.IO;

namespace bhl {
  
public class IncludePath
{
  List<string> items = new List<string>();

  public int Count {
    get {
      return items.Count;
    }
  }

  public string this[int i]
  {
    get {
      return items[i];
    }

    set {
      items[i] = value;
    }
  }

  public IncludePath()
  {}

  public IncludePath(IList<string> items)
  {
    this.items.AddRange(items);
  }

  public override string ToString()
  {
    return string.Join(',', items);
  }

  public void Add(string path)
  {
    items.Add(BuildUtils.NormalizeFilePath(path));
  }

  public void Clear()
  {
    items.Clear();
  }

  public string ResolveImportPath(string self_path, string path)
  {
    //relative import
    if(path[0] == '.')
    {
      var dir = Path.GetDirectoryName(self_path);
      return BuildUtils.NormalizeFilePath(Path.Combine(dir, path) + ".bhl");
    }
    //import via include path
    else
      return TryIncludePaths(path);
  }

  public string FilePath2ModuleName(string full_path, bool normalized = false)
  {
    return _FilePath2ModuleName(normalized ? full_path : BuildUtils.NormalizeFilePath(full_path));
  }

  string _FilePath2ModuleName(string full_path)
  {
    string norm_path = "";
    for(int i=0;i<items.Count;++i)
    {
      var inc_path = items[i];
      if(full_path.IndexOf(inc_path) == 0)
      {
        norm_path = full_path.Substring(inc_path.Length);
        norm_path = norm_path.Replace('\\', '/');
        //stripping .bhl extension
        norm_path = norm_path.Substring(0, norm_path.Length-4);
        //stripping initial /
        norm_path = norm_path.TrimStart('/', '\\');
        break;
      }
    }

    if(norm_path.Length == 0)
    {
      throw new Exception("File path '" + full_path + "' was not normalized, inc.path: " + this);
    }
    return norm_path;
  }

  public void ResolvePath(string self_path, string path, out string file_path, out string norm_path)
  {
    file_path = "";
    norm_path = "";

    if(path.Length == 0)
      throw new Exception("Bad path");

    file_path = ResolveImportPath(self_path, path);
    norm_path = _FilePath2ModuleName(file_path);
  }

  public string TryIncludePaths(string path)
  {
    for(int i=0;i<this.Count;++i)
    {
      var file_path = BuildUtils.NormalizeFilePath(this[i] + "/" + path  + ".bhl");
      if(File.Exists(file_path))
        return file_path;
    }
    return null;
  }
}

}
