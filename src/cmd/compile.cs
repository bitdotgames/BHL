using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using Mono.Options;

namespace bhl {

public class CompileCmd : ICmd
{
  const int ERROR_EXIT_CODE = 2;

  public static void Usage(string msg = "")
  {
    Console.WriteLine("Usage:");
    Console.WriteLine("bhl compile [--proj=<bhl.proj file>] [--dir=<src dirs separated with ;>] [--files=<file>] [--result=<result file>] " + 
                     "[--tmp-dir=<tmp dir>] [--error=<err file>] [--bindings-dll=<bindings dll path>] [--postproc-dll=<postproc dll path>] [-d] [--deterministic] [--module-fmt=<1,2>]");
    Console.WriteLine(msg);
    Environment.Exit(1);
  }

  public void Run(string[] args)
  {
    var files = new List<string>();

    var proj = new ProjectConf();

    var p = new OptionSet() {
      { "p|proj=", "project config file",
        v => { 
          proj = ProjectConf.ReadFromFile(v);
        } },
      { "dir=", "source directories separated by ;",
        v => proj.src_dirs.AddRange(v.Split(';')) },
      { "files=", "file containing all source files list",
        v => files.AddRange(File.ReadAllText(v).Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None)) },
      { "result=", "resulting file",
        v => proj.result_file = v },
      { "tmp-dir=", "tmp dir",
        v => proj.tmp_dir = v },
      { "C", "don't use cache",
        v => proj.use_cache = v == null },
      { "bindings-dll=", "bindings dll file path",
        v => proj.bindings_dll = v },
      { "postproc-dll=", "postprocess dll file path",
        v => proj.postproc_dll = v },
      { "error=", "error file",
        v => proj.error_file = v },
      { "deterministic", "deterministic build (sorts files by name)",
        v => proj.deterministic = v != null },
      { "threads=", "number of threads",
          v => proj.max_threads = int.Parse(v) },
      { "d", "debug verbosity level",
        v => proj.verbosity = v != null ? 2 : 1 },
      { "module-fmt=", "binary module format",
        v => proj.module_fmt = (ModuleBinaryFormat)int.Parse(v) }
     };

    var extra = new List<string>();
    try
    {
      extra = p.Parse(args);
    }
    catch(OptionException e)
    {
      Usage(e.Message);
    }

    var logger = new Logger(proj.verbosity, new ConsoleLogger()); 

    files.AddRange(extra);

    for(int i=0;i<proj.inc_path.Count;++i)
      if(!Directory.Exists(proj.inc_path[i]))
        Usage("Source directory not found: " + proj.inc_path[i]);

    if(string.IsNullOrEmpty(proj.result_file))
      Usage("Result file path not set");

    if(string.IsNullOrEmpty(proj.tmp_dir))
      Usage("Tmp dir not set");

    IUserBindings bindings = null;
    try
    {
      bindings = proj.LoadBindings();
    }
    catch(Exception e)
    {
      Usage($"Could not load bindings({proj.bindings_dll}): " + e);
    }

    IFrontPostProcessor postproc = null;
    try
    {
      postproc = proj.LoadPostprocessor();
    }
    catch(Exception e)
    {
      Usage($"Could not load postproc({proj.postproc_dll}): " + e);
    }

    if(files.Count == 0)
    {
      for(int i=0;i<proj.inc_path.Count;++i)
        CompilationExecutor.AddFilesFromDir(proj.inc_path[i], files);
    }
    else
    {
      for(int i=files.Count;i-- > 0;)
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
    conf.self_file = GetSelfFile();
    conf.files = Util.NormalizeFilePaths(files);
    conf.bindings = bindings;
    conf.postproc = postproc;

    var cmp = new CompilationExecutor();
    var errors = cmp.Exec(conf);
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

  public static string GetSelfFile()
  {
    return System.Reflection.Assembly.GetExecutingAssembly().Location;
  }
}

}
