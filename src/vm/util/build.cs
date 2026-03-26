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
    return System.Reflection.Assembly.GetExecutingAssembly().Location;
  }

  public static string GetSelfDir()
  {
    return Path.GetDirectoryName(GetSelfFile());
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

  static public List<string> Glob(string s)
  {
    var files = new List<string>();
    int idx = s.IndexOf('*');
    if(idx != -1)
    {
      string dir = s.Substring(0, idx);
      string mask = s.Substring(idx);

      if(Directory.Exists(dir))
        files.AddRange(Directory.GetFiles(dir, mask));
    }
    else
      files.Add(s);

    return files;
  }

  static public void Rm(string path)
  {
    if(Directory.Exists(path))
      Directory.Delete(path, true);
    else
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
