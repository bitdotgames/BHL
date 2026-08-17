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

    //NOTE: polling mtimes instead of FileSystemWatcher - simpler and avoids
    //      platform/editor-specific quirks around atomic write-via-rename saves
    var mtimes = new Dictionary<string, DateTime>();
    Func<bool> bhl_files_changed = () =>
    {
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
      return changed;
    };
    bhl_files_changed(); //NOTE: establishes the initial baseline, ignored

    Console.WriteLine($"Watching '{dir}' for changes - edit hotreload.bhl and save to see it hot-reload live");

    Time.dt = 0.1f;
    while(true)
    {
      if(bhl_files_changed())
      {
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
