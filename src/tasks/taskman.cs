using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace bhl {
  
public class ShellException : Exception
{
  public int code;

  public ShellException(int code, string message)
    : base(message)
  {
    this.code = code;
  }
}

public class Taskman
{
  public bool verbose = true;

  public class Task
  {
    public TaskAttribute attr;
    public MethodInfo func;

    public string Name
    {
      get { return func.Name; }
    }

    public List<Task> Deps = new List<Task>();
  }

  List<Task> tasks = new List<Task>();
  HashSet<Task> invoked = new HashSet<Task>();

  public bool IsWin
  {
    get { return !IsUnix; }
  }

  public bool IsUnix
  {
    get
    {
      int p = (int)Environment.OSVersion.Platform;
      return (p == 4) || (p == 6) || (p == 128);
    }
  }

  public Taskman(Type tasks_class)
  {
    foreach (var method in tasks_class.GetMethods())
    {
      var attr = GetAttribute<TaskAttribute>(method);
      if(attr == null)
        continue;
      var task = new Task()
      {
        attr = attr,
        func = method
      };
      tasks.Add(task);
    }

    foreach (var task in tasks)
    {
      foreach(var dep_name in task.attr.deps)
      {
        var dep = FindTask(dep_name);
        if(dep == null)
          throw new Exception($"No such dependency '{dep_name}' for task '{task.Name}'");
        task.Deps.Add(dep);
      }
    }
  }

  static T GetAttribute<T>(MemberInfo member) where T : Attribute
  {
    foreach(var attribute in member.GetCustomAttributes(true))
    {
      if(attribute is TaskAttribute)
        return (T)attribute;
    }

    return null;
  }

  public void Run(string[] args)
  {
    if(args.Length == 0)
      throw new Exception("No task specified");

    var task = FindTask(args[0]);
    if(task == null)
      throw new Exception("No such task: " + args[0]);

    verbose = task.attr.verbose;

    var task_args = args.ToList();
    task_args.RemoveAt(0);
    Invoke(task, task_args.ToArray());
  }

  public void Invoke(Task task, string[] task_args)
  {
    if(invoked.Contains(task))
      return;
    invoked.Add(task);

    foreach(var dep in task.Deps)
      Invoke(dep, new string[] { });

    Echo($"***** BHL '{task.Name}' start *****");
    var sw = new Stopwatch();
    sw.Start();
    task.func.Invoke(null, new object[] { this, task_args });
    var elapsed = Math.Round(sw.ElapsedMilliseconds / 1000.0f, 2);
    Echo($"***** BHL '{task.Name}' done({elapsed} sec.) *****");
  }

  public Task FindTask(string name)
  {
    foreach(var t in tasks)
    {
      if(t.Name == name)
        return t;
    }

    return null;
  }

  public string CLIPath(string p)
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

  public void Echo(string s)
  {
    if(verbose)
      Console.WriteLine(s);
  }

  public void Mkdir(string path)
  {
    if(!Directory.Exists(path))
      Directory.CreateDirectory(path);
  }

  public void Copy(string src, string dst)
  {
    if(File.Exists(dst))
      File.Delete(dst);
    Mkdir(Path.GetDirectoryName(dst));
    File.Copy(src, dst);
  }

  public void Shell(string binary, string args)
  {
    int exit_code = TryShell(binary, args);
    if (exit_code != 0)
      throw new ShellException(exit_code, $"Error exit code: {exit_code}");
  }

  public int TryShell(string binary, string args)
  {
    binary = CLIPath(binary);
    Echo($"shell: {binary} {args}");

    var p = new System.Diagnostics.Process();

    if(IsWin)
    {
      string tmp_path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/build/";
      string cmd = binary + " " + args;
      var bat_file = tmp_path + Hash.CRC32(cmd) + ".bat";
      Write(bat_file, cmd);

      p.StartInfo.FileName = "cmd.exe";
      p.StartInfo.Arguments = "/c " + bat_file;
    }
    else
    {
      p.StartInfo.FileName = binary;
      p.StartInfo.Arguments = args;
    }

    p.StartInfo.UseShellExecute = false;
    p.StartInfo.RedirectStandardOutput = false;
    p.StartInfo.RedirectStandardError = false;
    
    p.Start();
    p.WaitForExit();
    
    return p.ExitCode;
  }

  public string[] Glob(string s)
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

    return files.ToArray();
  }

  public void Rm(string path)
  {
    if(Directory.Exists(path))
      Directory.Delete(path, true);
    else
      File.Delete(path);
  }

  public void Write(string path, string text)
  {
    Mkdir(Path.GetDirectoryName(path));
    File.WriteAllText(path, text);
  }

  public void Touch(string path, DateTime dt)
  {
    if(!File.Exists(path))
      File.WriteAllText(path, "");
    File.SetLastWriteTime(path, dt);
  }

  public bool NeedToRegen(string file, IList<string> deps)
  {
    if(!File.Exists(file))
      return true;

    var fmtime = new FileInfo(file).LastWriteTime;
    foreach(var dep in deps)
    {
      if(File.Exists(dep) && (new FileInfo(dep).LastWriteTime > fmtime))
        return true;
    }

    return false;
  }
}

public class TaskAttribute : Attribute
{
  public bool verbose = true;
  public string[] deps;

  public TaskAttribute(params string[] deps)
  {
    this.deps = deps;
  }

  public TaskAttribute(bool verbose, params string[] deps)
  {
    this.verbose = verbose;
    this.deps = deps;
  }
}

}