using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Mono.Options;
using ThreadTask = System.Threading.Tasks.Task;

#pragma warning disable CS8981

namespace bhl.taskman;

public static partial class Tasks
{
  public static void compile_usage(string msg = "")
  {
    Console.WriteLine("Usage:");
    Console.WriteLine(
      "bhl compile [--proj=<bhl.proj file>] [--dir=<src dirs separated with ;>] [--files=<file>] [--result=<result file>] " +
      "[--tmp-dir=<tmp dir>] [--error=<err file>] [--bindings-dll=<index>=<dll path>] [--postproc-dll=<postproc dll path>] [-d] [--deterministic] [--module-fmt=<0=bin,1=lz4,2=lz4_chunked>] [--debug-info] " +
      "[--bindings-only] [--postproc-only]");
    Console.WriteLine(msg);
    Environment.Exit(1);
  }

  public class CompileFlagsArgs
  {
    public bool bindings_only = false;
    public bool postproc_only = false;
  }

  //NOTE: shared by compile() for real parsing and by 'bhl help compile' for documentation
  //      (see the '_options' convention in tasks.cs's help task, and compile_options() below
  //      for why this is a separate stage from compile_full_options())
  static OptionSet compile_flags_options(CompileFlagsArgs a) => new OptionSet()
  {
    {
      "bindings-only", "only prebuild each bindings entry's dll from its sources (C# or .bhl), then exit",
      v => a.bindings_only = v != null
    },
    {
      "postproc-only", "only prebuild postproc_dll from postproc_sources, then exit",
      v => a.postproc_only = v != null
    }
  };

  [Task(desc: "Compiles bhl scripts into bytecode")]
  public static async ThreadTask compile(Taskman tm, string[] args)
  {
    var flags_args = new CompileFlagsArgs();
    var flags = compile_flags_options(flags_args);
    args = flags.Parse(args).ToArray();

    string proj_file;
    var runtime_args = GetProjectArg(args, out proj_file);

    var proj = new ProjectConf();
    if(!string.IsNullOrEmpty(proj_file))
      proj = ProjectConf.ReadFromFile(proj_file);

    bool force_rebuild = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BHL_REBUILD"));

    //NOTE: manual_build opts a prebuilt/committed dll out of the normal auto-rebuild
    //      check - an entry's sources can still be listed for documentation and manual
    //      rebuilds (--bindings-only/--postproc-only), without an unwanted rebuild firing
    //      during a plain compile (e.g. on a fresh checkout where tmp_dir's cache doesn't
    //      exist yet, which would otherwise make the committed dll look stale)
    var built_bindings_dlls = BuildBindingsDlls(tm, force_rebuild, proj, flags_args.bindings_only);
    foreach(var kv in built_bindings_dlls)
      runtime_args.Add($"--bindings-dll={kv.Key}={kv.Value}");

    string postproc_dll_path = null;
    if(!proj.postproc_manual_build || flags_args.postproc_only || force_rebuild)
      postproc_dll_path = BuildPostprocDll(tm, force_rebuild, proj);
    if(postproc_dll_path != null)
      runtime_args.Add($"--postproc-dll={postproc_dll_path}");

    if(flags_args.bindings_only || flags_args.postproc_only)
    {
      if(flags_args.bindings_only)
      {
        bool any = built_bindings_dlls.Count > 0;
        foreach(var kv in built_bindings_dlls)
          Console.WriteLine($"[{kv.Key}]: {kv.Value}");

        for(int i = 0; i < proj.bindings.Count; ++i)
        {
          if(built_bindings_dlls.ContainsKey(i))
            continue;

          //NOTE: unlike C# sources (built above via BuildBindingsDlls), .bhl sources are
          //      normally compiled lazily as a side effect of the regular compile pipeline
          //      (ScriptedBindings.Register()); here we trigger that same compile-and-cache
          //      step explicitly, without a host project to compile
          var path = await BuildScriptedBindingsBytecode(proj, proj.bindings[i]);
          if(path != null)
          {
            Console.WriteLine($"[{i}]: {path}");
            any = true;
          }
        }

        if(!any)
          Console.WriteLine($"No bindings sources found in '{proj_file}', nothing to build");
      }

      if(flags_args.postproc_only)
        Console.WriteLine(postproc_dll_path ?? $"No postproc_sources found in '{proj_file}', nothing to build");

      return;
    }

    await _compile(runtime_args.ToArray(), force_rebuild);
  }

  //NOTE: returns null if the entry has no .bhl sources or its 'dll' isn't a .bhc bytecode path
  static async System.Threading.Tasks.Task<string> BuildScriptedBindingsBytecode(ProjectConf proj, BindingsEntryConf b)
  {
    var bhl_scripts = new List<string>();
    foreach(var s in b.sources.Where(f => f.EndsWith(".bhl")))
      bhl_scripts.AddRange(BuildUtils.Glob(s));

    if(bhl_scripts.Count == 0 || string.IsNullOrEmpty(b.dll) || !b.dll.EndsWith(".bhc"))
      return null;

    var vm = await CompilationExecutor.CompileAndLoadVM(
      bhl_scripts,
      use_cache: proj.use_cache,
      bytecode_result_file: b.dll,
      tmp_dir: proj.tmp_dir
    );
    if(vm == null)
      Environment.Exit(ERROR_EXIT_CODE);

    return b.dll;
  }

  public class CompileFullArgs
  {
    public ProjectConf proj = new ProjectConf();
    public List<string> files = new List<string>();
    public bool add_debug_info = false;
  }

  //NOTE: shared by _compile() for real parsing and by 'bhl help compile' for documentation
  //      (see compile_options() below) - a.proj is reassigned wholesale by '--proj=', which
  //      is why it lives on a shared mutable 'a' rather than being a plain local: a closure
  //      can't rebind a variable declared in its caller, only mutate/reassign a field on an
  //      object both sides hold a reference to
  static OptionSet compile_full_options(CompileFullArgs a) => new OptionSet()
  {
    {
      "p|proj=", "project config file",
      v => { a.proj = ProjectConf.ReadFromFile(v); }
    },
    {
      "dir=", "source directories separated by ;",
      v => a.proj.src_dirs.AddRange(v.Split(';'))
    },
    {
      "files=", "file containing all source files list",
      v => a.files.AddRange(File.ReadAllText(v).Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None))
    },
    {
      "result=", "resulting file",
      v => a.proj.result_file = v
    },
    {
      "tmp-dir=", "tmp dir",
      v => a.proj.tmp_dir = v
    },
    {
      "C", "don't use cache",
      v => a.proj.use_cache = v == null
    },
    {
      "bindings-dll=", "bindings entry dll file path, as <index>=<path> (repeatable); " +
        "out-of-range/non-numeric index appends a new entry",
      v =>
      {
        int idx = v.IndexOf('=');
        if(idx < 0)
          throw new OptionException("Expected --bindings-dll=<index>=<path>", "bindings-dll");

        string idx_str = v.Substring(0, idx);
        string path = v.Substring(idx + 1);

        if(int.TryParse(idx_str, out int entry_idx) && entry_idx >= 0 && entry_idx < a.proj.bindings.Count)
          a.proj.bindings[entry_idx].dll = path;
        else
          a.proj.bindings.Add(new BindingsEntryConf { dll = path });
      }
    },
    {
      "postproc-dll=", "postprocess dll file path",
      v => a.proj.postproc_dll = v
    },
    {
      "error=", "error file",
      v => a.proj.error_file = v
    },
    {
      "deterministic", "deterministic build (sorts files by name)",
      v => a.proj.deterministic = v != null
    },
    {
      "threads=", "number of threads",
      v => a.proj.max_threads = int.Parse(v)
    },
    {
      "d", "debug verbosity level",
      v => a.proj.verbosity = v != null ? 2 : 1
    },
    {
      "module-fmt=", "binary module format",
      v => a.proj.module_fmt = (ModuleBinaryFormat)int.Parse(v)
    },
    {
      "debug-info", "emit local variable names for the debugger",
      v => a.add_debug_info = v != null
    }
  };

  static async ThreadTask _compile(string[] args, bool force_rebuild)
  {
    var a = new CompileFullArgs();
    var p = compile_full_options(a);

    if(force_rebuild)
      a.proj.use_cache = false;

    var extra = new List<string>();
    try
    {
      extra = p.Parse(args);
    }
    catch(OptionException e)
    {
      compile_usage(e.Message);
    }

    if(Environment.GetEnvironmentVariable("BHL_VERBOSE") != null)
      int.TryParse(Environment.GetEnvironmentVariable("BHL_VERBOSE"), out a.proj.verbosity);

    var logger = new Logger(a.proj.verbosity, new ConsoleLogger());

    a.files.AddRange(extra);

    for(int i = 0; i < a.proj.src_dirs.Count; ++i)
      if(!Directory.Exists(a.proj.src_dirs[i]))
        compile_usage("Source directory not found: " + a.proj.src_dirs[i]);

    if(string.IsNullOrEmpty(a.proj.result_file))
      compile_usage("Result file path not set");

    if(string.IsNullOrEmpty(a.proj.tmp_dir))
      compile_usage("Tmp dir not set");

    IUserBindings bindings = null;
    try
    {
      bindings = a.proj.LoadBindings();
    }
    catch(Exception e)
    {
      compile_usage($"Could not load bindings: " + e);
    }

    IFrontPostProcessor postproc = null;
    try
    {
      postproc = a.proj.LoadPostprocessor();
    }
    catch(Exception e)
    {
      compile_usage($"Could not load postproc({a.proj.postproc_dll}): " + e);
    }

    if(a.files.Count == 0)
    {
      for(int i = 0; i < a.proj.src_dirs.Count; ++i)
        CompilationExecutor.AddFilesFromDir(a.proj.src_dirs[i], a.files);
    }
    else
    {
      for(int i = a.files.Count; i-- > 0;)
      {
        if(string.IsNullOrEmpty(a.files[i]))
          a.files.RemoveAt(i);
      }
    }

    logger.Log(1, $"BHL({Version.Name}) files: {a.files.Count}, cache: {a.proj.use_cache}, debug info: {a.add_debug_info}");
    var conf = new CompileConf();
    conf.proj = a.proj;
    conf.logger = logger;
    conf.args_signature = string.Join(";", args) + ";sv=" + ModuleDeclared.STREAM_VERSION;
    conf.self_file = BuildUtils.GetSelfFile();
    conf.files = BuildUtils.NormalizeFilePaths(a.files);
    foreach(var b in a.proj.bindings)
      if(File.Exists(b.dll))
        conf.global_file_deps.Add(b.dll);
    //NOTE: bhl.proj-only settings (e.g. defines) have no cache-invalidation signal
    //      otherwise, so track the proj file itself as a dep
    if(!string.IsNullOrEmpty(a.proj.proj_file) && File.Exists(a.proj.proj_file))
      conf.global_file_deps.Add(a.proj.proj_file);
    conf.bindings = bindings;
    conf.postproc = postproc;
    conf.add_debug_info = a.add_debug_info;

    var executor = new CompilationExecutor();
    var result = await executor.Exec(conf);

    foreach(var warn in result.warnings)
      ErrorUtils.OutputWarning(warn.file, warn.range.start.line, warn.range.start.column, warn.text);

    if(result.errors.Count > 0)
    {
      if(string.IsNullOrEmpty(a.proj.error_file))
      {
        foreach(var err in result.errors)
          ErrorUtils.OutputError(err.file, err.range.start.line, err.range.start.column, err.text);
      }

      Environment.Exit(ERROR_EXIT_CODE);
    }
  }

  public static List<string> GetProjectArg(string[] args, out string proj_file)
  {
    string _proj_file = "";

    var p = new OptionSet()
    {
      {
        "p|proj=", "project config file",
        v => _proj_file = v
      }
    };

    var left = p.Parse(args);

    proj_file = _proj_file;

    if (!string.IsNullOrEmpty(proj_file))
      left.Insert(0, "--proj=" + proj_file);
    return left;
  }

  //NOTE: 'compile' parses its options in two stages (compile_flags_options up front, then
  //      compile_full_options inside _compile(), on whatever's left after prebuilding
  //      bindings/postproc dlls) - this merges both purely for 'bhl help compile' to display
  //      as one list, without changing how the real two-stage parse works
  static OptionSet compile_options()
  {
    var combined = new OptionSet();
    foreach(var o in compile_flags_options(new CompileFlagsArgs()))
      combined.Add(o);
    foreach(var o in compile_full_options(new CompileFullArgs()))
      combined.Add(o);
    return combined;
  }
}
