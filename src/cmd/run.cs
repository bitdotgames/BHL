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
  const int ERROR_EXIT_CODE = 2;

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

    var p = new OptionSet() {
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

    IUserBindings userbindings = new EmptyUserBindings();

    IFrontPostProcessor postproc = new EmptyPostProcessor();

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
    conf.ts = new Types();
    conf.module_fmt = ModuleBinaryFormat.FMT_BIN;
    conf.use_cache = false;
    conf.self_file = GetSelfFile();
    conf.files = files;
    conf.inc_path.Add(src_dir);
    conf.max_threads = 1;
    conf.res_file = src_dir + "/" + Path.GetFileNameWithoutExtension(files[0]) + ".bhc";
    conf.tmp_dir = Path.GetTempPath();
    conf.userbindings = userbindings;
    conf.postproc = postproc;
    conf.verbose = false;

    var cmp = new CompilationExecutor();
    var err = cmp.Exec(conf);

    if(err != null)
    {
      ErrorUtils.OutputError(err.file, err.line, err.char_pos, err.text);
      Environment.Exit(ERROR_EXIT_CODE);
    }

    var bytes = new MemoryStream(File.ReadAllBytes(conf.res_file));
    var vm = new VM(conf.ts, new ModuleLoader(conf.ts, bytes));

    vm.LoadModule(Path.GetFileNameWithoutExtension(files[0]));

    var argv_lst = ValList.New(vm); 
    //TODO:
    //foreach(var arg in args)
    //  argv_lst.Add(Val.NewStr(vm, arg));
    var argv = Val.NewObj(vm, argv_lst, new GenericArrayTypeSymbol(new Proxy<IType>()));
    if(vm.Start("main", argv) == null)
      throw new Exception("No 'main' function found");

    const float dt = 0.016f;
    while(vm.Tick())
      System.Threading.Thread.Sleep((int)(dt * 1000));
  }

  public static string GetSelfFile()
  {
    return System.Reflection.Assembly.GetExecutingAssembly().Location;
  }
}

}
