using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using bhl;

public class HotReloadExample
{
  const string ModuleName = "hotreload";

  public static void Main(string[] args)
  {
    if(args.Length < 1)
    {
      Console.WriteLine("Usage: hotreload <project dir>");
      Environment.Exit(1);
    }

    string dir = args[0];

    //NOTE: this Types instance lives for the whole process - it's what every reload's
    //      module gets deserialized against, so MigrateInstance's field type comparisons
    //      stay consistent across reloads. Compiling itself uses its own throwaway Types
    //      each time (see RecompileModule) - it only needs bindings *signatures* to
    //      type-check against, not working native delegates.
    var ts = new Types();

    Console.WriteLine("Compiling...");
    var stream = RecompileModule(dir).GetAwaiter().GetResult();
    var loader = new ModuleLoader(ts, stream);
    //NOTE: mirrors VM.FromBytecode - attaches the real Trace/Rand delegates (from
    //      bindings.cs's self-registered MyBindings) to `ts`, matched against the
    //      bytecode's declared required-bindings list
    BindingsRegistry.RegisterRequiredBindings(ts, loader);

    var vm = new VM(ts);
    vm.LoadModule(new Module(loader.Load(ModuleName, null)));

    var unit_val = RunToCompletion(vm, "make_bot");

    //NOTE: polling mtimes is the reliable path - it's been instant in testing here, whereas
    //      FileSystemWatcher events arrived with multi-minute delays on this setup even with
    //      the main thread fully idle. Both run independently on their own thread and just
    //      set a shared flag the main loop checks once per tick - neither ever blocks it.
    int pending_reload = 0;

    var watcher = new FileSystemWatcher(dir);
    watcher.IncludeSubdirectories = true;
    watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime;
    FileSystemEventHandler on_event = (s, e) =>
    {
      if(e.FullPath.EndsWith(".bhl"))
        Interlocked.Exchange(ref pending_reload, 1);
    };
    watcher.Changed += on_event;
    watcher.Created += on_event;
    watcher.Renamed += (s, e) =>
    {
      if(e.FullPath.EndsWith(".bhl"))
        Interlocked.Exchange(ref pending_reload, 1);
    };
    watcher.EnableRaisingEvents = true;

    var poll_thread = new Thread(() =>
    {
      const int poll_interval_ms = 300;

      var mtimes = new Dictionary<string, DateTime>();
      foreach(var f in Directory.GetFiles(dir, "*.bhl", SearchOption.AllDirectories))
        mtimes[f] = File.GetLastWriteTimeUtc(f);

      while(true)
      {
        Thread.Sleep(poll_interval_ms);

        bool changed = false;
        foreach(var f in Directory.GetFiles(dir, "*.bhl", SearchOption.AllDirectories))
        {
          var mtime = File.GetLastWriteTimeUtc(f);
          if(!mtimes.TryGetValue(f, out var prev) || prev != mtime)
          {
            mtimes[f] = mtime;
            changed = true;
          }
        }

        if(changed)
          Interlocked.Exchange(ref pending_reload, 1);
      }
    });
    poll_thread.IsBackground = true;
    poll_thread.Start();

    Console.WriteLine($"Watching '{dir}' for changes - edit hotreload.bhl and save to see it hot-reload live");

    Time.dt = 0.1f;
    while(true)
    {
      bool should_reload = Interlocked.Exchange(ref pending_reload, 0) == 1;

      if(should_reload)
      {
        //NOTE: gives the editor a moment to finish writing before we read the file
        Thread.Sleep(50);
        try
        {
          var new_stream = RecompileModule(dir).GetAwaiter().GetResult();
          var new_loader = new ModuleLoader(ts, new_stream);
          var new_decl = new_loader.Load(ModuleName, null);

          vm.Reload(new Module(new_decl));
          vm.MigrateInstance(ref unit_val);
          Console.WriteLine("--- reloaded ---");
        }
        catch(CompileErrorsException ex)
        {
          Console.WriteLine("Reload failed, keeping current behavior:\n" + ex.Message);
        }
      }

      unit_val.RetainData();
      var fb = vm.Start("tick_bot", unit_val, Time.dt);
      while(vm.Tick()) {}

      Thread.Sleep((int)(Time.dt * 1000));
    }
  }

  static Val RunToCompletion(VM vm, string func)
  {
    var fb = vm.Start(func);
    while(vm.Tick()) {}
    return fb.Stack.Pop();
  }

  static async Task<Stream> RecompileModule(string dir)
  {
    var proj = ProjectConf.ReadFromFile(Path.Combine(dir, "bhl.proj"));
    var bindings = proj.LoadBindings();

    var files = new List<string>();
    foreach(var src_dir in proj.src_dirs)
      CompilationExecutor.AddFilesFromDir(src_dir, files);

    var conf = new CompileConf();
    conf.proj = proj;
    conf.logger = new Logger(0, new ConsoleLogger());
    conf.self_file = BuildUtils.GetSelfFile();
    conf.files = BuildUtils.NormalizeFilePaths(files);
    conf.bindings = bindings;
    conf.ts = new Types();

    var executor = new CompilationExecutor();
    var result = await executor.Exec(conf);
    if(result.errors.Count > 0)
      throw new CompileErrorsException(result.errors);

    return new MemoryStream(File.ReadAllBytes(conf.proj.result_file));
  }
}
