using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using bhl;

public class HotReloadExample
{
  //NOTE: the one module whose class we keep a live instance of, so it's the one
  //      case that needs MigrateInstance rather than just Reload+RelinkImports
  const string BotModuleName = "hotreload";

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
    //NOTE: "combat" has to be loaded before "hotreload", since "hotreload" imports it and
    //      Module.Setup() resolves imports by looking them up in VM.modules at that point
    vm.LoadModule(new Module(loader.Load("combat", null)));
    vm.LoadModule(new Module(loader.Load(BotModuleName, null)));

    var unit_val = RunToCompletion(vm, "make_bot");

    //NOTE: polling mtimes is the reliable path - it's been instant in testing here, whereas
    //      FileSystemWatcher events arrived with multi-minute delays on this setup even with
    //      the main thread fully idle. Both run independently on their own thread and just
    //      record changed module names into a shared set the main loop drains once per tick -
    //      neither ever blocks it.
    var pending_modules = new ConcurrentDictionary<string, bool>();

    Action<string> signal_for_path = (path) =>
    {
      if(path.EndsWith(".bhl"))
        pending_modules[Path.GetFileNameWithoutExtension(path)] = true;
    };

    var watcher = new FileSystemWatcher(dir);
    watcher.IncludeSubdirectories = true;
    watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime;
    FileSystemEventHandler on_event = (s, e) => signal_for_path(e.FullPath);
    watcher.Changed += on_event;
    watcher.Created += on_event;
    watcher.Renamed += (s, e) => signal_for_path(e.FullPath);
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

        foreach(var f in Directory.GetFiles(dir, "*.bhl", SearchOption.AllDirectories))
        {
          var mtime = File.GetLastWriteTimeUtc(f);
          if(!mtimes.TryGetValue(f, out var prev) || prev != mtime)
          {
            mtimes[f] = mtime;
            signal_for_path(f);
          }
        }
      }
    });
    poll_thread.IsBackground = true;
    poll_thread.Start();

    Console.WriteLine($"Watching '{dir}' for changes - edit hotreload.bhl or combat.bhl and save to see it hot-reload live");

    Time.dt = 0.1f;
    while(true)
    {
      var changed_modules = Interlocked.Exchange(ref pending_modules, new ConcurrentDictionary<string, bool>());

      if(changed_modules.Count > 0)
      {
        //NOTE: gives the editor a moment to finish writing before we read the file
        Thread.Sleep(50);
        try
        {
          var new_stream = RecompileModule(dir).GetAwaiter().GetResult();
          var new_loader = new ModuleLoader(ts, new_stream);

          //NOTE: uniform for every changed module - RelinkImports on one that nobody
          //      currently imports is simply a no-op, so there's nothing module-specific
          //      to branch on here
          foreach(var module_name in changed_modules.Keys)
          {
            var new_decl = new_loader.Load(module_name, null);
            //NOTE: the changed-file signal can be noisy (an editor's temp/swap file that
            //      happens to end in .bhl, a file deleted right after triggering the event) -
            //      if it doesn't correspond to an actual module in the fresh compile, skip it
            //      rather than crash the whole loop over it
            if(new_decl == null)
            {
              Console.WriteLine($"--- '{module_name}.bhl' not found in the latest compile, skipping ---");
              continue;
            }

            if(vm.FindModule(module_name) != null)
            {
              vm.Reload(new Module(new_decl));
              vm.RelinkImports(module_name);
              Console.WriteLine($"--- {module_name} reloaded ---");
            }
            else
            {
              //NOTE: a brand-new .bhl file added to the project - nothing to migrate,
              //      just load it for the first time
              vm.LoadModule(new Module(new_decl));
              Console.WriteLine($"--- {module_name} loaded (new) ---");
            }
          }

          //NOTE: the one module-specific step - only the Bot's own class instance needs
          //      migrating, since it's the only live instance this example tracks
          if(changed_modules.ContainsKey(BotModuleName))
            vm.MigrateInstance(ref unit_val);
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
    //NOTE: this is what makes "hotreload"'s calls into "combat" resolve via combat's
    //      func_idx table at call time instead of a baked-in ip - required for
    //      VM.RelinkImports("combat") above to be safe at all
    conf.indirect_calls = true;

    var executor = new CompilationExecutor();
    var result = await executor.Exec(conf);
    if(result.errors.Count > 0)
      throw new CompileErrorsException(result.errors);

    return new MemoryStream(File.ReadAllBytes(conf.proj.result_file));
  }
}
