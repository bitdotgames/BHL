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
      "[--tmp-dir=<tmp dir>] [--error=<err file>] [--bindings-dll=<bindings dll path>] [--postproc-dll=<postproc dll path>] [-d] [--deterministic] [--module-fmt=<1,2>] [--debug-info] " +
      "[--bindings-only] [--postproc-only]");
    Console.WriteLine(msg);
    Environment.Exit(1);
  }

  [Task]
  public static async ThreadTask compile(Taskman tm, string[] args)
  {
    bool bindings_only = false;
    bool postproc_only = false;

    var flags = new OptionSet()
    {
      {
        "bindings-only", "only prebuild bindings_dll from bindings_sources (C# or .bhl), then exit",
        v => bindings_only = v != null
      },
      {
        "postproc-only", "only prebuild postproc_dll from postproc_sources, then exit",
        v => postproc_only = v != null
      }
    };
    args = flags.Parse(args).ToArray();

    string proj_file;
    var runtime_args = GetProjectArg(args, out proj_file);

    var proj = new ProjectConf();
    if(!string.IsNullOrEmpty(proj_file))
      proj = ProjectConf.ReadFromFile(proj_file);

    bool force_rebuild = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BHL_REBUILD"));

    string bindings_dll_path = BuildBindingsDll(tm, force_rebuild, proj);
    if(bindings_dll_path != null)
      runtime_args.Add($"--bindings-dll={bindings_dll_path}");

    string postproc_dll_path = BuildPostprocDll(tm, force_rebuild, proj);
    if(postproc_dll_path != null)
      runtime_args.Add($"--postproc-dll={postproc_dll_path}");

    if(bindings_only || postproc_only)
    {
      if(bindings_only)
      {
        //NOTE: unlike C# bindings_sources (built above via BuildBindingsDll), .bhl
        //      bindings_sources are normally compiled lazily as a side effect of the
        //      regular compile pipeline (ScriptedBindings.Register()); here we trigger
        //      that same compile-and-cache step explicitly, without a host project to compile
        if(bindings_dll_path == null)
          bindings_dll_path = await BuildScriptedBindingsBytecode(proj);

        Console.WriteLine(bindings_dll_path ?? $"No bindings_sources found in '{proj_file}', nothing to build");
      }

      if(postproc_only)
        Console.WriteLine(postproc_dll_path ?? $"No postproc_sources found in '{proj_file}', nothing to build");

      return;
    }

    await _compile(runtime_args.ToArray(), force_rebuild);
  }

  //NOTE: returns null if proj has no .bhl bindings_sources or proj.bindings_dll
  //      isn't a .bhc bytecode path
  static async System.Threading.Tasks.Task<string> BuildScriptedBindingsBytecode(ProjectConf proj)
  {
    var bhl_scripts = new List<string>();
    foreach(var s in proj.bindings_sources.Where(f => f.EndsWith(".bhl")))
      bhl_scripts.AddRange(BuildUtils.Glob(s));

    if(bhl_scripts.Count == 0 || !proj.bindings_dll.EndsWith(".bhc"))
      return null;

    var vm = await CompilationExecutor.CompileAndLoadVM(
      bhl_scripts,
      use_cache: proj.use_cache,
      bytecode_result_file: proj.bindings_dll
    );
    if(vm == null)
      Environment.Exit(ERROR_EXIT_CODE);

    return proj.bindings_dll;
  }

  static async ThreadTask _compile(string[] args, bool force_rebuild)
  {
    var files = new List<string>();

    var proj = new ProjectConf();
    bool add_debug_info = false;

    var p = new OptionSet()
    {
      {
        "p|proj=", "project config file",
        v => { proj = ProjectConf.ReadFromFile(v); }
      },
      {
        "dir=", "source directories separated by ;",
        v => proj.src_dirs.AddRange(v.Split(';'))
      },
      {
        "files=", "file containing all source files list",
        v => files.AddRange(File.ReadAllText(v).Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None))
      },
      {
        "result=", "resulting file",
        v => proj.result_file = v
      },
      {
        "tmp-dir=", "tmp dir",
        v => proj.tmp_dir = v
      },
      {
        "C", "don't use cache",
        v => proj.use_cache = v == null
      },
      {
        "bindings-dll=", "bindings dll file path",
        v => proj.bindings_dll = v
      },
      {
        "postproc-dll=", "postprocess dll file path",
        v => proj.postproc_dll = v
      },
      {
        "error=", "error file",
        v => proj.error_file = v
      },
      {
        "deterministic", "deterministic build (sorts files by name)",
        v => proj.deterministic = v != null
      },
      {
        "threads=", "number of threads",
        v => proj.max_threads = int.Parse(v)
      },
      {
        "d", "debug verbosity level",
        v => proj.verbosity = v != null ? 2 : 1
      },
      {
        "module-fmt=", "binary module format",
        v => proj.module_fmt = (ModuleBinaryFormat)int.Parse(v)
      },
      {
        "debug-info", "emit local variable names for the debugger",
        v => add_debug_info = v != null
      }
    };

    if(force_rebuild)
      proj.use_cache = false;

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
      int.TryParse(Environment.GetEnvironmentVariable("BHL_VERBOSE"), out proj.verbosity);

    var logger = new Logger(proj.verbosity, new ConsoleLogger());

    files.AddRange(extra);

    for(int i = 0; i < proj.src_dirs.Count; ++i)
      if(!Directory.Exists(proj.src_dirs[i]))
        compile_usage("Source directory not found: " + proj.src_dirs[i]);

    if(string.IsNullOrEmpty(proj.result_file))
      compile_usage("Result file path not set");

    if(string.IsNullOrEmpty(proj.tmp_dir))
      compile_usage("Tmp dir not set");

    IUserBindings bindings = null;
    try
    {
      bindings = proj.LoadBindings();
    }
    catch(Exception e)
    {
      compile_usage($"Could not load bindings({proj.bindings_dll}): " + e);
    }

    IFrontPostProcessor postproc = null;
    try
    {
      postproc = proj.LoadPostprocessor();
    }
    catch(Exception e)
    {
      compile_usage($"Could not load postproc({proj.postproc_dll}): " + e);
    }

    if(files.Count == 0)
    {
      for(int i = 0; i < proj.src_dirs.Count; ++i)
        CompilationExecutor.AddFilesFromDir(proj.src_dirs[i], files);
    }
    else
    {
      for(int i = files.Count; i-- > 0;)
      {
        if(string.IsNullOrEmpty(files[i]))
          files.RemoveAt(i);
      }
    }

    logger.Log(1, $"BHL({Version.Name}) files: {files.Count}, cache: {proj.use_cache}, debug info: {add_debug_info}");
    var conf = new CompileConf();
    conf.proj = proj;
    conf.logger = logger;
    conf.args_signature = string.Join(";", args) + ";sv=" + ModuleDeclared.STREAM_VERSION;
    conf.self_file = BuildUtils.GetSelfFile();
    conf.files = BuildUtils.NormalizeFilePaths(files);
    if(File.Exists(proj.bindings_dll))
      conf.global_file_deps.Add(proj.bindings_dll);
    conf.bindings = bindings;
    conf.postproc = postproc;
    conf.add_debug_info = add_debug_info;

    var executor = new CompilationExecutor();
    var result = await executor.Exec(conf);

    foreach(var warn in result.warnings)
      ErrorUtils.OutputWarning(warn.file, warn.range.start.line, warn.range.start.column, warn.text);

    if(result.errors.Count > 0)
    {
      if(string.IsNullOrEmpty(proj.error_file))
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
}
