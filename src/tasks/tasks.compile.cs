using System;
using System.IO;
using System.Collections.Generic;
using Mono.Options;
using ThreadTask = System.Threading.Tasks.Task;

#pragma warning disable CS8981

namespace bhl
{

public static partial class Tasks
{
  public static void compile_usage(string msg = "")
  {
    Console.WriteLine("Usage:");
    Console.WriteLine(
      "bhl compile [--proj=<bhl.proj file>] [--dir=<src dirs separated with ;>] [--files=<file>] [--result=<result file>] " +
      "[--tmp-dir=<tmp dir>] [--error=<err file>] [--bindings-dll=<bindings dll path>] [--postproc-dll=<postproc dll path>] [-d] [--deterministic] [--module-fmt=<1,2>]");
    Console.WriteLine(msg);
    Environment.Exit(1);
  }

  [Task]
  public static ThreadTask compile(Taskman tm, string[] args)
  {
    string proj_file;
    var runtime_args = GetProjectArg(args, out proj_file);

    var proj = new ProjectConfPartial();
    if(!string.IsNullOrEmpty(proj_file))
      proj = ProjectConfPartial.ReadFromFile(proj_file);

    bool force_rebuild = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BHL_REBUILD"));

    var bindings_sources = proj.bindings_sources;
    if(bindings_sources.Count > 0)
    {
      if(string.IsNullOrEmpty(proj.bindings_dll))
        throw new Exception("Resulting 'bindings_dll' is not set");

      bindings_sources.Add($"{BHL_ROOT}/src/compile/bhl_front.csproj");
      string bindings_dll_path = DotnetBuildLibrary(
        tm,
        force_rebuild,
        bindings_sources.ToArray(),
        proj.bindings_dll,
        new List<string>() { "BHL_FRONT" }
      );
      runtime_args.Add($"--bindings-dll={bindings_dll_path}");
    }

    var postproc_sources = proj.postproc_sources;
    if(postproc_sources.Count > 0)
    {
      if(string.IsNullOrEmpty(proj.postproc_dll))
        throw new Exception("Resulting 'postproc_dll' is not set");

      postproc_sources.Add($"{BHL_ROOT}/src/compile/bhl_front.csproj");
      postproc_sources.Add("Antlr4.Runtime.Standard=4.13.1");
      string postproc_dll_path = DotnetBuildLibrary(
        tm,
        force_rebuild,
        postproc_sources.ToArray(),
        proj.postproc_dll,
        new List<string>() { "BHL_FRONT" }
      );
      runtime_args.Add($"--postproc-dll={postproc_dll_path}");
    }

    return _compile(tm, runtime_args.ToArray());
  }

  static async ThreadTask _compile(Taskman tm, string[] args)
  {
    var files = new List<string>();

    var proj = new ProjectConf();

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
      }
    };

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

    logger.Log(1, $"BHL({Version.Name}) files: {files.Count}, cache: {proj.use_cache}");
    var conf = new CompileConf();
    conf.proj = proj;
    conf.logger = logger;
    conf.args = string.Join(";", args);
    conf.self_file = BuildUtils.GetSelfFile();
    conf.files = BuildUtils.NormalizeFilePaths(files);
    conf.bindings = bindings;
    conf.postproc = postproc;

    var cmp = new CompilationExecutor();
    var errors = await cmp.Exec(conf);
    if(errors.Count > 0)
    {
      if(string.IsNullOrEmpty(proj.error_file))
      {
        foreach(var err in errors)
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

}