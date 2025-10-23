using System;
using System.Collections.Generic;
using System.IO;

namespace bhl
{

static public class BuildUtils
{
  static public string NormalizeFilePath(string file_path)
  {
    var path = Path.GetFullPath(file_path).Replace("\\", "/");
    if(path[1] == ':' && char.IsUpper(path[2]))
    {
      //path = char.ToLower(path[0]) + path.Substring(1);
      Span<char> span = path.ToCharArray();
      span[0] = char.ToLower(span[0]);
      path = new string(span);
    }
    return path;
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

}
