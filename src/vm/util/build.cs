using System;
using System.Collections.Generic;
using System.IO;

namespace bhl {

static public class BuildUtils
{
  static public string NormalizeFilePath(string file_path)
  {
    return Path.GetFullPath(file_path).Replace("\\", "/");
  }

  static public List<string> NormalizeFilePaths(List<string> file_paths)
  {
    var res = new List<string>();
    foreach(var path in file_paths)
      res.Add(NormalizeFilePath(path));
    return res;
  }

  public static string GetSelfFile()
  {
    return System.Reflection.Assembly.GetExecutingAssembly().Location;
  }

  public static string GetSelfDir()
  {
    return Path.GetDirectoryName(GetSelfFile());
  }

  static public bool NeedToRegen(string file, List<string> deps)
  {
    if(!File.Exists(file))
    {
      //Console.WriteLine("Missing " + file);
      return true;
    }

    var fmtime = GetLastWriteTime(file);
    foreach(var dep in deps)
    {
      if(File.Exists(dep) && GetLastWriteTime(dep) > fmtime)
      {
        //Console.WriteLine("Stale " + dep + " " + file + " : " + GetLastWriteTime(dep) + " VS " + fmtime);
        return true;
      }
    }

    //Console.WriteLine("Hit "+ file);
    return false;
  }

  //optimized version for just one dependency
  static public bool NeedToRegen(string file, string dep)
  {
    if(!File.Exists(file))
      return true;

    var fmtime = GetLastWriteTime(file);
    if(File.Exists(dep) && GetLastWriteTime(dep) > fmtime)
      return true;

    return false;
  }

  static DateTime GetLastWriteTime(string file)
  {
    return new FileInfo(file).LastWriteTime;
  }
}

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
      throw new Exception("File path '" + full_path + "' was not normalized");
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

public class FileImports : marshall.IMarshallable
{
  public List<string> import_paths = new List<string>();
  public List<string> file_paths = new List<string>();

  public FileImports()
  {}

  public FileImports(Dictionary<string, string> imps)
  {
    foreach(var kv in imps)
    {
      import_paths.Add(kv.Key);
      file_paths.Add(kv.Value);
    }
  }

  //TODO: handle duplicate file_paths?
  //NOTE: file_path can be null if imported module is native for example
  public void Add(string import_path, string file_path)
  {
    if(import_paths.Contains(import_path))
      return;
    import_paths.Add(import_path);
    file_paths.Add(file_path);
  }

  public string MapToFilePath(string import_path)
  {
    int idx = import_paths.IndexOf(import_path);
    if(idx == -1)
      return null;
    return file_paths[idx];
  }

  public void Reset() 
  {
    import_paths.Clear();
    file_paths.Clear();
  }
  
  public void IndexTypeRefs(TypeRefIndex refs)
  {}

  public void Sync(marshall.SyncContext ctx) 
  {
    marshall.Marshall.Sync(ctx, import_paths);
    marshall.Marshall.Sync(ctx, file_paths);
  }
}

}