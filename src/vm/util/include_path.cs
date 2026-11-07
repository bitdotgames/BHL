using System;
using System.Collections.Generic;
using System.IO;

namespace bhl
{

public class IncludePath
{
  //NOTE: 'prefix' is the declaring library's name (see ProjectConf.name) and empty for the
  //      project's own dirs. A module under such a dir is named '<prefix>/<rest>', so its
  //      identity belongs to the library rather than to however the consumer spelled inc_dirs
  public struct Entry
  {
    public string dir;
    public string prefix;
  }

  List<Entry> _items = new List<Entry>();

  public int Count
  {
    get { return _items.Count; }
  }

  public Entry this[int i]
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
    var sb = new System.Text.StringBuilder();
    for(int i = 0; i < _items.Count; ++i)
    {
      if(i > 0)
        sb.Append(',');
      if(_items[i].prefix.Length > 0)
        sb.Append(_items[i].prefix).Append('=');
      sb.Append(_items[i].dir);
    }
    return sb.ToString();
  }

  public void Add(string path)
  {
    Add(path, "");
  }

  public void Add(string path, string prefix)
  {
    var entry = new Entry {
      dir = BuildUtils.NormalizeFilePath(path),
      prefix = prefix ?? ""
    };

    //NOTE: the same dir can be reached twice (a library's own inc_dirs and its src_dirs),
    //      and a duplicate entry would only cost a redundant File.Exists per import
    for(int i = 0; i < _items.Count; ++i)
      if(_items[i].dir == entry.dir && _items[i].prefix == entry.prefix)
        return;

    _items.Add(entry);
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
    //NOTE: a library that declared a name owns the identity of its files, so it wins over a
    //      prefix-less dir the including project happens to have pointed at the same tree -
    //      otherwise the consumer's inc_dirs would still decide the library's module names
    int found = -1;
    for(int i = 0; i < _items.Count; ++i)
    {
      if(!StartsWithDir(full_path, _items[i].dir))
        continue;

      if(_items[i].prefix.Length > 0)
      {
        found = i;
        break;
      }

      if(found == -1)
        found = i;
    }

    string norm_path = "";
    if(found != -1)
    {
      var item = _items[found];

      norm_path = full_path.Substring(item.dir.Length);
      norm_path = norm_path.Replace('\\', '/');
      //stripping .bhl extension
      norm_path = norm_path.Substring(0, norm_path.Length - 4);
      //stripping initial /
      norm_path = norm_path.TrimStart('/', '\\');

      if(item.prefix.Length > 0)
        norm_path = item.prefix + "/" + norm_path;
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
    //NOTE: a leading separator is tolerated and meaningless for a plain dir (the combined path
    //      normalizes the same), so it must not change whether a named library matches either
    path = path.TrimStart('/', '\\');

    for(int i = 0; i < _items.Count; ++i)
    {
      var item = _items[i];

      string rest = path;
      if(item.prefix.Length > 0)
      {
        //NOTE: a named library is only reachable through its own name, so an import can't
        //      land in it by accident just because this dir happens to come first
        if(!path.StartsWith(item.prefix, StringComparison.Ordinal) ||
           path.Length <= item.prefix.Length ||
           !IsSep(path[item.prefix.Length]))
          continue;

        rest = path.Substring(item.prefix.Length + 1);
      }

      var file_path = BuildUtils.NormalizeFilePath(item.dir + "/" + rest + ".bhl");
      if(File.Exists(file_path))
        return file_path;
    }

    return null;
  }
}

}
