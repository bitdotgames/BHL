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

    string src_dir = Path.GetDirectoryName(files[0]);
    if(string.IsNullOrEmpty(src_dir))
      src_dir = "./";

    var proj = new ProjectConf();
    proj.module_fmt = ModuleBinaryFormat.FMT_BIN;
    proj.use_cache = false;
    proj.max_threads = 1;
    proj.src_dirs.Add(src_dir);
    proj.result_file = src_dir + "/" + Path.GetFileNameWithoutExtension(files[0]) + ".bhc";
    proj.tmp_dir = Path.GetTempPath();
    proj.verbosity = 0;
    proj.Setup();

    var conf = new CompileConf();
    conf.logger = new Logger(1, new ConsoleLogger());
    conf.proj = proj;
    conf.ts = new Types();
    conf.self_file = BuildUtils.GetSelfFile();
    conf.files = BuildUtils.NormalizeFilePaths(files);
    conf.bindings = new EmptyUserBindings();
    conf.postproc = new EmptyPostProcessor();

    var cmp = new CompilationExecutor();
    var errors = await cmp.Exec(conf);

    if(errors.Count > 0)
    {
      foreach(var err in errors)
        ErrorUtils.OutputError(err.file, err.range.start.line, err.range.start.column, err.text);
      Environment.Exit(ERROR_EXIT_CODE);
    }

    var bytes = new MemoryStream(File.ReadAllBytes(conf.proj.result_file));
    var vm = new VM(conf.ts, new ModuleLoader(conf.ts, bytes));

    vm.LoadModule(Path.GetFileNameWithoutExtension(files[0]));

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
