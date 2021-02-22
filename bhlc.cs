using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Antlr4.Runtime.Misc;
using Mono.Options;
using bhl;

public class BHLC
{
  public static void Usage(string msg = "")
  {
    Console.WriteLine("Usage:");
    Console.WriteLine("bhl run --dir=<root src dir> [--files=<file>] --result=<result file> --cache_dir=<cache dir> --error=<err file> [--postproc_dll=<postproc dll path>] [-d] [--deterministic] [--format=<1,2>]");
    Console.WriteLine(msg);
    Environment.Exit(1);
  }

  public static void Main(string[] args)
  {
    var files = new List<string>();

    string src_dir = "";
    string res_file = "";
    string cache_dir = "";
    bool use_cache = true;
    string err_file = "";
    string postproc_dll_path = "";
    string userbindings_dll_path = "";
    int max_threads = 1;
    bool check_deps = true;
    bool deterministic = false;
    bool debug = false;
    ModuleBinaryFormat format = ModuleBinaryFormat.FMT_LZ4;

    var p = new OptionSet () {
      { "dir=", "source dir",
        v => src_dir = v },
      { "files=", "source files list",
        v => files.AddRange(File.ReadAllText(v).Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None)) },
      { "result=", "result file",
        v => res_file = v },
      { "cache_dir=", "cache dir",
        v => cache_dir = v },
      { "C", "don't use cache",
        v => use_cache = v == null },
      { "N", "don't check import deps",
        v => check_deps = v == null },
      { "postproc_dll=", "posprocess dll path",
        v => postproc_dll_path = v },
      { "bindings_dll=", "bindings dll path",
        v => userbindings_dll_path = v },
      { "error=", "error file",
        v => err_file = v },
      { "deterministic=", "deterministic build",
        v => deterministic = v != null },
      { "threads=", "number of threads",
          v => max_threads = int.Parse(v) },
      { "d", "debug version",
        v => debug = v != null },
      { "format=", "binary module format",
        v => format = (ModuleBinaryFormat)int.Parse(v) }
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
    files.AddRange(extra);

    if(!Directory.Exists(src_dir))
      Usage("Root source directory is not valid");
    src_dir = Path.GetFullPath(src_dir);

    if(res_file == "")
      Usage("Result file path not set");

    if(cache_dir == "")
      Usage("Cache dir not set");

    if(err_file == "")
      Usage("Err file not set");
    if(File.Exists(err_file))
      File.Delete(err_file);

    UserBindings userbindings = new EmptyUserBindings();
    if(userbindings_dll_path != "")
    {
      var userbindings_assembly = System.Reflection.Assembly.LoadFrom(userbindings_dll_path);
      var userbindings_class = userbindings_assembly.GetTypes()[0];
      userbindings = System.Activator.CreateInstance(userbindings_class) as UserBindings;
      if(userbindings == null)
        Usage("User bindings are invalid");
    }

    IPostProcessor postproc = new EmptyPostProcessor();
    if(postproc_dll_path != "")
    {
      var postproc_assembly = System.Reflection.Assembly.LoadFrom(postproc_dll_path);
      var postproc_class = postproc_assembly.GetTypes()[0];
      postproc = System.Activator.CreateInstance(postproc_class) as IPostProcessor;
      if(postproc == null)
        Usage("User postprocessor is invalid");
    }

    if(files.Count == 0)
      Build.AddFilesFromDir(src_dir, files);

    for(int i=files.Count;i-- > 0;)
    {
      if(string.IsNullOrEmpty(files[i]))
        files.RemoveAt(i);
    }

    if(deterministic)
      files.Sort();

    Console.WriteLine("Total files {0}(debug: {1})", files.Count, Util.DEBUG);
    var conf = new BuildConf();
    conf.args = string.Join(";", args);
    conf.use_cache = use_cache;
    conf.self_file = GetSelfFile();
    conf.check_deps = check_deps;
    conf.files = files;
    conf.inc_dir = src_dir;
    conf.max_threads = max_threads;
    conf.res_file = res_file;
    conf.cache_dir = cache_dir;
    conf.err_file = err_file;
    conf.userbindings = userbindings;
    conf.postproc = postproc;
    conf.debug = debug;
    conf.format = format;

    var build = new Build();
    int err = build.Exec(conf);
    if(err != 0)
      Environment.Exit(err);
  }

  public static string GetSelfFile()
  {
    return System.Reflection.Assembly.GetExecutingAssembly().Location;
  }
}
