using System;
using System.Collections.Generic;
using System.IO;

namespace bhl
{

public class IncludePath
{
  List<string> _items = new List<string>();
  IReadOnlyList<string> Items => _items;

  public int Count
  {
    get { return _items.Count; }
  }

  public string this[int i]
  {
    get { return _items[i]; }
  }

  public IncludePath()
  {}

  public IncludePath(IList<string> items)
  {
    foreach(string item in items)
      Add(item);
  }

  public override string ToString()
  {
    return string.Join(',', _items);
  }

  public void Add(string path)
  {
    string normalized = BuildUtils.NormalizeFilePath(path);
    _items.Add(normalized);
  }

  public void Clear()
  {
    _items.Clear();
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
    for(int i = 0; i < _items.Count; ++i)
    {
      var inc_path = _items[i];
      if(!StartsWithDir(full_path, inc_path))
        continue;

      norm_path = full_path.Substring(inc_path.Length);
      norm_path = norm_path.Replace('\\', '/');
      //stripping .bhl extension
      norm_path = norm_path.Substring(0, norm_path.Length - 4);
      //stripping initial /
      norm_path = norm_path.TrimStart('/', '\\');
      break;
    }

    if(norm_path.Length == 0)
    {
      //NOTE: a module is named by stripping its include path prefix, so a file outside all of
      //      them has no name to be given - in practice a src_dir that no inc_dir covers
      throw new Exception(
        "File '" + full_path + "' is not under any include dir (" + this + "): " +
        "add the directory holding it to 'inc_dirs', or drop 'inc_dirs' from bhl.proj " +
        "to have them default to 'src_dirs'");
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

  //NOTE: a plain prefix test would take '<root>/src' to cover '<root>/srcx/b.bhl' and name
  //      that module 'x/b', colliding with a real '<root>/src/x/b.bhl' - the prefix has to
  //      end on a path boundary to actually mean 'inside this directory'
  static bool StartsWithDir(string full_path, string dir)
  {
    if(dir.Length == 0 || full_path.Length <= dir.Length)
      return false;

    if(!full_path.StartsWith(dir, StringComparison.Ordinal))
      return false;

    //NOTE: an inc dir may be written with a trailing separator ("../" and the like are common
    //      in a bhl.proj), in which case the boundary is already part of the prefix
    return IsSep(dir[dir.Length - 1]) || IsSep(full_path[dir.Length]);
  }

  static bool IsSep(char c)
  {
    return c == '/' || c == '\\';
  }

  public string TryIncludePaths(string path)
  {
    for(int i = 0; i < this.Count; ++i)
    {
      var file_path = BuildUtils.NormalizeFilePath(this[i] + "/" + path  + ".bhl");
      if(File.Exists(file_path))
        return file_path;
    }

    return null;
  }
}

}
