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
    Console.WriteLine("bhl run <script.bhl>");
    Console.WriteLine(msg);
    Environment.Exit(1);
  }

  [Task]
  public static async ThreadTask run(Taskman tm, string[] args)
  {
    var files = new List<string>();

    var p = new OptionSet()
    {
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

    files.AddRange(extra);

    for(int i = files.Count; i-- > 0;)
    {
      if(string.IsNullOrEmpty(files[i]))
        files.RemoveAt(i);
    }

    if(files.Count == 0)
      run_usage("No files to run");

    var vm = await CompilationExecutor.CompileAndLoadVM(files);
    if(vm == null)
      Environment.Exit(ERROR_EXIT_CODE);

    var argv_lst = ValList.New(vm);
    //TODO:
    //foreach(var arg in args)
    //  argv_lst.Add(Val.NewStr(vm, arg));
    var argv = Val.NewObj(argv_lst, Types.Array);
    if(vm.Start("main", argv) == null)
      throw new Exception("No 'main' function found");

    const float dt = 0.016f;
    while(vm.Tick())
      System.Threading.Thread.Sleep((int)(dt * 1000));
  }
}
