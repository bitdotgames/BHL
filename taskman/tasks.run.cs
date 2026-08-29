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
    Console.WriteLine("bhl run <script.bhl> [args...] [--func=<name>] [--tick-ms=<n>]");
    Console.WriteLine(msg);
    Environment.Exit(1);
  }

  [Task(verbose: false)]
  public static async ThreadTask run(Taskman tm, string[] args)
  {
    string func = "main";
    int tick_ms = 0;

    var p = new OptionSet()
    {
      {
        "func=", "function to run instead of 'main'",
        v => func = v
      },
      {
        "tick-ms=", "sleep this many milliseconds between VM ticks (default: no sleep)",
        v => tick_ms = int.Parse(v)
      }
    };

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

    var vm = await CompilationExecutor.CompileAndLoadVM(new List<string> { script_file }, add_debug_info: true);
    if(vm == null)
      Environment.Exit(ERROR_EXIT_CODE);

    if(!vm.TryFindFuncAddr(func, out _))
      run_usage($"No '{func}' function found");

    var argv_lst = ValList.New(vm);
    foreach(var arg in script_args)
      argv_lst.Add(Val.NewStr(arg));
    var argv = Val.NewObj(argv_lst, Types.Array);
    vm.Start(func, argv);

    while(vm.Tick())
    {
      if(tick_ms > 0)
        System.Threading.Thread.Sleep(tick_ms);
    }
  }
}
