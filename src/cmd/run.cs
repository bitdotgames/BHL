using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Antlr4.Runtime.Misc;
using Mono.Options;
using Newtonsoft.Json;

namespace bhl {

public class RunCmd : ICmd
{
  public class Error
  {
    public string error;
    public string file;
    public int line;
    public int column;
  }

  public static void Usage(string msg = "")
  {
    Console.WriteLine("Usage:");
    Console.WriteLine("bhl run <script.bhl>");
    Console.WriteLine(msg);
    Environment.Exit(1);
  }

  public void Run(string[] args)
  {
    var files = new List<string>();

    //string src_dir = "";
    //string res_file = "";
    //string tmp_dir = "";
    //bool use_cache = true;
    //string err_file = "";
    //string postproc_dll_path = "";
    //string userbindings_dll_path = "";
    //int max_threads = 1;
    //bool check_deps = true;
    //bool deterministic = false;

    var p = new OptionSet() {
    //  { "dir=", "source dir",
    //    v => src_dir = v },
    //  { "files=", "source files list",
    //    v => files.AddRange(File.ReadAllText(v).Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None)) },
    //  { "result=", "result file",
    //    v => res_file = v },
    //  { "tmp-dir=", "tmp dir",
    //    v => tmp_dir = v },
    //  { "C", "don't use cache",
    //    v => use_cache = v == null },
    //  { "N", "don't check import deps",
    //    v => check_deps = v == null },
    //  { "postproc-dll=", "posprocess dll path",
    //    v => postproc_dll_path = v },
    //  { "bindings-dll=", "bindings dll path",
    //    v => userbindings_dll_path = v },
    //  { "error=", "error file",
    //    v => err_file = v },
    //  { "deterministic=", "deterministic build",
    //    v => deterministic = v != null },
    //  { "threads=", "number of threads",
    //      v => max_threads = int.Parse(v) },
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

    //if(!Directory.Exists(src_dir))
    //  Usage("Root source directory is not valid");
    //src_dir = Path.GetFullPath(src_dir);

    //if(res_file == "")
    //  Usage("Result file path not set");

    //if(tmp_dir == "")
    //  Usage("Tmp dir not set");

    //if(err_file == "")
    //  Usage("Err file not set");
    //if(File.Exists(err_file))
    //  File.Delete(err_file);

    IUserBindings userbindings = new EmptyUserBindings();
    //if(userbindings_dll_path != "")
    //{
    //  var userbindings_assembly = System.Reflection.Assembly.LoadFrom(userbindings_dll_path);
    //  var userbindings_class = userbindings_assembly.GetTypes()[0];
    //  userbindings = System.Activator.CreateInstance(userbindings_class) as IUserBindings;
    //  if(userbindings == null)
    //    Usage("User bindings are invalid");
    //}

    IFrontPostProcessor postproc = new EmptyPostProcessor();
    //if(postproc_dll_path != "")
    //{
    //  var postproc_assembly = System.Reflection.Assembly.LoadFrom(postproc_dll_path);
    //  var postproc_class = postproc_assembly.GetTypes()[0];
    //  postproc = System.Activator.CreateInstance(postproc_class) as IFrontPostProcessor;
    //  if(postproc == null)
    //    Usage("User postprocessor is invalid");
    //}

    for(int i=files.Count;i-- > 0;)
    {
      if(string.IsNullOrEmpty(files[i]))
        files.RemoveAt(i);
    }

    if(files.Count == 0)
      Usage("No files to run");

    string src_dir = Path.GetDirectoryName(files[0]);
    if(string.IsNullOrEmpty(src_dir))
      src_dir = "./";

    var conf = new CompileConf();
    conf.module_fmt = ModuleBinaryFormat.FMT_BIN;
    conf.use_cache = false;
    conf.self_file = GetSelfFile();
    conf.check_deps = true;
    conf.files = files;
    conf.inc_dir = src_dir;
    conf.max_threads = 1;
    conf.res_file = files[0] + ".bhc";
    conf.tmp_dir = Path.GetTempPath();
    conf.err_file = Path.GetTempPath() + "/bhl.error";
    conf.userbindings = userbindings;
    conf.postproc = postproc;
    conf.verbose = false;

    File.Delete(conf.err_file);

    var cmp = new Compiler();
    int err = cmp.Exec(conf);

    if(File.Exists(conf.err_file))
    {
      string err_json = File.ReadAllText(conf.err_file);
      var err_obj = JsonConvert.DeserializeObject<Error>(err_json);
      ShowPosition(files[0], err_obj);
      Console.Error.WriteLine("bhl: " + files[0] + ":" + err_obj.line + ":" + err_obj.column + ": " + err_obj.error);
    }
    if(err != 0)
      Environment.Exit(err);
  }

  static void ShowPosition(string file, Error err_obj)
  {
    var lines = File.ReadAllLines(file);

    if(err_obj.line < lines.Length)
      Console.Error.WriteLine(lines[err_obj.line-1]);
    else if(err_obj.line-1 == lines.Length)
      Console.Error.WriteLine(lines[lines.Length-1]);
    Console.Error.WriteLine(new String('-', err_obj.column) + '^');
  }

  public static string GetSelfFile()
  {
    return System.Reflection.Assembly.GetExecutingAssembly().Location;
  }
}

}
