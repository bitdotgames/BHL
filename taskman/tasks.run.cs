using System;
using System.IO;
using System.Collections.Generic;
using Mono.Options;
using ThreadTask = System.Threading.Tasks.Task;

#pragma warning disable CS8981

namespace bhl.taskman;

public static partial class Tasks
{
  static void run_usage(string msg = "")
  {
    Console.WriteLine("Usage:");
    Console.WriteLine("bhl run <script.bhl> [args...] [--proj=<bhl.proj file>] [--func=<name>] [--tick-ms=<n>]");
    Console.WriteLine(msg);
    Environment.Exit(1);
  }

  static string FindProjFileUpwards(string dir)
  {
    var di = string.IsNullOrEmpty(dir) ? null : new DirectoryInfo(dir);
    while(di != null)
    {
      string candidate = Path.Combine(di.FullName, "bhl.proj");
      if(File.Exists(candidate))
        return candidate;
      di = di.Parent;
    }
    return null;
  }

  public class RunArgs
  {
    public string func = "main";
    public int tick_ms = 0;
    public string proj_file = "";
  }

  //NOTE: shared by run() for real parsing and by 'bhl help run' for documentation
  //      (see the '_options' convention in tasks.cs's help task)
  static OptionSet run_options(RunArgs a) => new OptionSet()
  {
    {
      "p|proj=", "project config file, so the script's imports and bindings resolve against it",
      v => a.proj_file = v
    },
    {
      "func=", "function to run instead of 'main'",
      v => a.func = v
    },
    {
      "tick-ms=", "sleep this many milliseconds between VM ticks (default: no sleep)",
      v => a.tick_ms = int.Parse(v)
    }
  };

  [Task(verbose: false, desc: "Compiles and runs a single .bhl script")]
  public static async ThreadTask run(Taskman tm, string[] args)
  {
    var a = new RunArgs();
    var p = run_options(a);

    var extra = new List<string>();
    try
    {
      extra = p.Parse(args);
    }
    catch(OptionException e)
    {
      run_usage(e.Message);
    }

    for(int i = extra.Count; i-- > 0;)
    {
      if(string.IsNullOrEmpty(extra[i]))
        extra.RemoveAt(i);
    }

    if(extra.Count == 0)
      run_usage("No files to run");

    //NOTE: only the first positional arg is the script to run, the rest
    //      are forwarded to the entry function as its args
    string script_file = Path.GetFullPath(extra[0]);
    var script_args = extra.GetRange(1, extra.Count - 1);

    //NOTE: without a project a script's imports can't resolve, so look for one the way
    //      most tools find their config - nearest bhl.proj walking up from the script
    if(string.IsNullOrEmpty(a.proj_file))
      a.proj_file = FindProjFileUpwards(Path.GetDirectoryName(script_file));
    else if(!File.Exists(a.proj_file))
      run_usage("Project file not found: " + a.proj_file);

    ProjectConf proj = null;
    var files = new List<string> { script_file };
    IUserBindings bindings = null;

    if(!string.IsNullOrEmpty(a.proj_file))
    {
      proj = ProjectConf.ReadFromFile(a.proj_file);
      foreach(var w in proj.warnings)
        ErrorUtils.OutputWarning(w);

      bindings = proj.LoadBindings();

      //NOTE: the script must stay at index 0, it's the module which gets loaded
      var rest = new List<string>();
      proj.CollectSourceFiles(rest);

      var seen = new HashSet<string>(BuildUtils.NormalizeFilePaths(files));
      foreach(var f in BuildUtils.NormalizeFilePaths(rest))
        if(seen.Add(f))
          files.Add(f);
    }

    var vm = await CompilationExecutor.CompileAndLoadVM(
      files, add_debug_info: true, bindings: bindings, proj: proj);
    if(vm == null)
      Environment.Exit(ERROR_EXIT_CODE);

    if(!vm.TryFindFuncAddr(a.func, out _))
      run_usage($"No '{a.func}' function found");

    var argv_lst = ValList.New(vm);
    foreach(var arg in script_args)
      argv_lst.Add(Val.NewStr(arg));
    var argv = Val.NewObj(argv_lst, Types.Array);
    vm.Start(a.func, argv);

    while(vm.Tick())
    {
      if(a.tick_ms > 0)
        System.Threading.Thread.Sleep(a.tick_ms);
    }
  }
}
