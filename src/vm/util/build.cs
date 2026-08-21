using System;
using System.Collections.Generic;
using System.IO;

namespace bhl
{

static public class BuildUtils
{
  static public bool IsWin
  {
    get { return !IsUnix; }
  }

  static public bool IsUnix
  {
    get
    {
      int p = (int)Environment.OSVersion.Platform;
      return (p == 4) || (p == 6) || (p == 128);
    }
  }

  static public string NormalizeFilePath(string file_path)
  {
    var path = Path.GetFullPath(file_path).Replace("\\", "/");
    if(path[1] == ':' && char.IsUpper(path[0]))
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
    //NOTE: Assembly.Location is empty for single-file apps, so we reconstruct
    //      the assembly path from the app base dir; returns "" when the assembly
    //      isn't a standalone file on disk (single-file deployment)
    var asm = System.Reflection.Assembly.GetExecutingAssembly();
    var path = Path.Combine(AppContext.BaseDirectory, asm.GetName().Name + ".dll");
    return File.Exists(path) ? path : "";
  }

  public static string GetSelfDir()
  {
    return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, '/');
  }

  static public bool NeedToRegen(string file, IEnumerable<string> deps)
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

  //NOTE: expands one path segment at a time, so '*' works in any segment, not just a trailing one
  static public List<string> Glob(string pattern)
  {
    if(pattern.IndexOf('*') == -1)
      return new List<string> { pattern };

    string norm = pattern.Replace('\\', '/');
    string root = "";
    if(norm.StartsWith("/"))
    {
      root = "/";
      norm = norm.Substring(1);
    }

    var segments = norm.Split('/');
    var current = new List<string> { root };

    for(int i = 0; i < segments.Length; ++i)
    {
      string segment = segments[i];
      bool is_last = i == segments.Length - 1;
      bool has_wildcard = segment.IndexOf('*') != -1;

      var next = new List<string>();
      foreach(var dir in current)
      {
        if(!has_wildcard)
        {
          next.Add(dir.Length == 0 ? segment : dir.EndsWith("/") ? dir + segment : dir + "/" + segment);
          continue;
        }

        if(!Directory.Exists(dir))
          continue;

        next.AddRange(is_last ? Directory.GetFiles(dir, segment) : Directory.GetDirectories(dir, segment));
      }
      current = next;
    }

    return current;
  }

  static public void Rm(string path)
  {
    if(Directory.Exists(path))
      Directory.Delete(path, true);
    else if(File.Exists(path))
      File.Delete(path);
  }

  static public void Write(string path, string text)
  {
    Mkdir(Path.GetDirectoryName(path));
    File.WriteAllText(path, text);
  }

  static public void Touch(string path, DateTime dt)
  {
    if(!File.Exists(path))
      File.WriteAllText(path, "");
    File.SetLastWriteTime(path, dt);
  }

  static public void Mkdir(string path)
  {
    if(!Directory.Exists(path))
      Directory.CreateDirectory(path);
  }

  static public void Copy(string src, string dst)
  {
    Rm(dst);
    Mkdir(Path.GetDirectoryName(dst));
    File.Copy(src, dst);
  }
  static public string CLIPath(string p)
  {
    if(p.IndexOf(" ") == -1)
      return p;

    if(IsWin)
    {
      p = "\"" + p.Trim(new char[] { '"' }) + "\"";
      return p;
    }
    else
    {
      p = "'" + p.Trim(new char[] { '\'' }) + "'";
      return p;
    }
  }
}

}
