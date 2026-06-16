using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using ThreadTask = System.Threading.Tasks.Task;

namespace bhl.taskman;

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
  public IList<Task> Tasks => tasks;

  HashSet<Task> invoked = new HashSet<Task>();

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

  public ThreadTask Run(string[] args)
  {
    if(args.Length == 0)
      throw new Exception("No task specified");

    var task = FindTask(args[0]);
    if(task == null)
      throw new Exception("No such task: " + args[0]);

    verbose = task.attr.verbose;

    var task_args = args.ToList();
    task_args.RemoveAt(0);
    return Invoke(task, task_args.ToArray());
  }

  public async ThreadTask Invoke(Task task, string[] task_args)
  {
    if(invoked.Contains(task))
      return;
    invoked.Add(task);

    foreach(var dep in task.Deps)
      await Invoke(dep, new string[] { });

    Echo($"***** BHL '{task.Name}' start *****");
    var sw = new Stopwatch();
    sw.Start();
    await ((ThreadTask)task.func.Invoke(null, new object[] { this, task_args }));
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

  public void Echo(string s)
  {
    if(verbose)
      Console.WriteLine(s);
  }

  public void Shell(string binary, string args)
  {
    int exit_code = TryShell(binary, args);
    if (exit_code != 0)
      throw new ShellException(exit_code, $"Error exit code: {exit_code}");
  }

  public int TryShell(string binary, string args)
  {
    binary = BuildUtils.CLIPath(binary);
    Echo($"shell: {binary} {args}");

    var p = new System.Diagnostics.Process();

    if(BuildUtils.IsWin)
    {
      string tmp_path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/build/";
      string cmd = binary + " " + args;
      var bat_file = tmp_path + Hash.CRC32(cmd) + ".bat";
      BuildUtils.Write(bat_file, cmd);

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
