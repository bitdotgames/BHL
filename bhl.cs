using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Antlr4.Runtime.Misc;
using Mono.Options;
using bhl;

public class BHL
{
  const int MAX_THREADS = 6;

  public static void Usage(string msg = "")
  {
    Console.WriteLine("Usage:");
    Console.WriteLine("./bhl --dir=<root src dir> [--files=<file>] --result=<result file> --cache_dir=<cache dir> --error=<err file> [--postproc_dll=<postproc dll path>] [-d]");
    Console.WriteLine(msg);
    Environment.Exit(1);
  }

  public static void Main(string[] args)
  {
    var files = new List<string>();

    string src_dir = "";
		string res_file = "";
    string cache_dir = "";
    string err_file = "";
    string postproc_dll_path = "";
    string userbindings_dll_path = "";

    var p = new OptionSet () {
			{ "dir=", "source dir",
				v => src_dir = v },
			{ "files=", "source files list",
				v => files.AddRange(File.ReadAllText(v).Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None)) },
			{ "result=", "result file",
				v => res_file = v },
			{ "cache_dir=", "cache dir",
				v => cache_dir = v },
			{ "postproc_dll=", "posprocess dll path",
				v => postproc_dll_path = v },
			{ "bindings_dll=", "bindings dll path",
				v => userbindings_dll_path = v },
			{ "error=", "error file",
				v => err_file = v },
			{ "d", "debug version",
				v => Util.DEBUG = v != null }
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

    PostProcessor postproc = new EmptyPostProcessor();
    if(postproc_dll_path != "")
    {
      var postproc_assembly = System.Reflection.Assembly.LoadFrom(postproc_dll_path);
      var postproc_class = postproc_assembly.GetTypes()[0];
      postproc = System.Activator.CreateInstance(postproc_class) as PostProcessor;
      if(postproc == null)
        Usage("User postprocessor is invalid");
    }

    var res_dir = Path.GetDirectoryName(res_file); 
    if(res_dir.Length > 0)
      Directory.CreateDirectory(res_dir);

    Directory.CreateDirectory(cache_dir);

    if(files.Count == 0)
    {
      DirWalk(src_dir, 
        delegate(string file) 
        { 
          if(TestFile(file))
            files.Add(file);
        }
      );
    }

    Console.WriteLine("Total files {0}(debug: {1})", files.Count, Util.DEBUG);

    if(!Util.NeedToRegen(res_file, files) && (postproc != null && !postproc.NeedToRegen(files)))
      return;

		//Shuffle(files);

    var globs = SymbolTable.CreateBuiltins();
    userbindings.Register(globs);

    Util.SetupAutogenFactory();

    var workers = new List<Worker>();

    int files_per_worker = files.Count < MAX_THREADS ? files.Count : (int)Math.Ceiling((float)files.Count / (float)MAX_THREADS);
    int idx = 0;
    int wid = 0;

    while(idx < files.Count)
    {
      int count = (idx + files_per_worker) > files.Count ? (files.Count - idx) : files_per_worker; 

      var w = new Worker();
      w.id = ++wid;
      w.inc_dir = src_dir;
      w.cache_dir = cache_dir;
      w.bindings = globs;
      w.files = files;
      w.start = idx;
      w.count = count;
      w.postproc = postproc;

      idx += count;

      workers.Add(w);

      w.Start();
    }

    foreach(var w in workers)
    {
      //Console.Write("Joining...\n");
      w.Join();
    }

    var tmp_res_file = cache_dir + "/" + Path.GetFileName(res_file) + ".tmp";
    using(FileStream dfs = new FileStream(tmp_res_file, FileMode.Create, System.IO.FileAccess.Write))
    {
      var mwriter = new MsgPack.MsgPackWriter(dfs);
      int total_modules = 0;
      foreach(var w in workers)
      {
        if(w.error == null)
          CheckUniqueSymbols(w);

        if(w.error != null)
        {
          if(err_file == "-")
            Console.Error.WriteLine(w.error.ToJson());
          else
            File.WriteAllText(err_file, w.error.ToJson());
          Environment.Exit(2);
        }
        total_modules += w.result.Count;
      }

      mwriter.Write(total_modules);

      foreach(var w in workers)
      {
        for(int i=0;i<w.result.Count;++i)
        {
          var m = w.result[i];
          var lz4 = w.lz4_result[i];

          mwriter.Write(1/*lz4*/);
          mwriter.Write(m.nname);
          mwriter.Write(lz4);
        }
      }
    }

    if(File.Exists(res_file))
      File.Delete(res_file);
    File.Move(tmp_res_file, res_file);

    postproc.Finish();
  }

  static void Shuffle(List<string> arr)
  {
    var rnd = new Random();
		// Knuth shuffle algorithm :: courtesy of Wikipedia :)
		for (int t = 0; t < arr.Count; ++t)
		{
			var tmp = arr[t];
			int r = rnd.Next(t, arr.Count);
			arr[t] = arr[r];
			arr[r] = tmp;
		}
  }

  static Dictionary<ulong, string> uniq_symbols = new Dictionary<ulong, string>();

  static void CheckUniqueSymbols(Worker w)
  {
    for(int i=0;i<w.symbols.Count; ++i)
    {
      var symb = w.symbols[i];

      string fpath;
      if(uniq_symbols.TryGetValue(symb.name.n, out fpath))
      {
        w.error = new UserError(symb.file, "Symbol '" + symb.name + "' is already declared in '" + fpath + "'");
        break;
      }
      else
        uniq_symbols.Add(symb.name.n, symb.file);
    }
  }

  public struct Symbol2File
  {
    public HashedName name;
    public string file;

    public Symbol2File(HashedName name, string file)
    {
      this.name = name;
      this.file = file;
    }
  }

  public class Worker
  {
    public int id;
    public Thread th;
    public string inc_dir;
    public string cache_dir;
    public List<string> files;
    public GlobalScope bindings;
    public int start;
    public int count;
    public List<AST_Module> result;
    public List<byte[]> lz4_result;
    public List<Symbol2File> symbols = new List<Symbol2File>();
    public UserError error = null;
    public PostProcessor postproc;

    public void Start()
    {
      var th = new Thread(DoWork);
      this.th = th;
      this.th.Start(this);
    }

    public void Join()
    {
      th.Join();
    }

    public string GetCacheFile(string file)
    {
      return cache_dir + "/" + Hash.CRC32(file + Util.DEBUG) + ".cache";
    }

    static List<string> ParseImports(List<string> inc_paths, string file, FileStream fs)
    {
      var imps = new List<string>();

      var r = new StreamReader(fs);

      while(true)
      {
        var line = r.ReadLine();
        if(line == null)
          break;

        var import_idx = line.IndexOf("import");
        if(import_idx != -1)
        {
          var q1_idx = line.IndexOf('"', import_idx + 1);
          if(q1_idx != -1)
          {
            var q2_idx = line.IndexOf('"', q1_idx + 1);
            if(q2_idx != -1)
            {
              var rel_import = line.Substring(q1_idx + 1, q2_idx - q1_idx - 1);
              var import = Util.ResolveImportPath(inc_paths, file, rel_import);
              if(imps.IndexOf(import) == -1)
                imps.Add(import);
            }
          }
        }
      }

      fs.Position = 0;

      return imps;
    }

    public static string GetSelfFile()
    {
      return System.Reflection.Assembly.GetExecutingAssembly().Location;
    }

    public static void DoWork(object data)
    {
      var sw = new Stopwatch();
      sw.Start();

      var w = (Worker)data;
      w.result = new List<AST_Module>();
      w.lz4_result = new List<byte[]>();

      var mreg = new ModuleRegistry();
      mreg.AddToIncludePath(w.inc_dir);

      int i = w.start;

      string self_bin_file = GetSelfFile();

      try
      {
        for(int cnt = 0;i<(w.start + w.count);++i,++cnt)
        {
          if(cnt > 0 && cnt % 500 == 0)
            Console.WriteLine("BHL Worker " + w.id + " " + cnt + "/" + w.count);

          var file = w.files[i]; 
          //Console.WriteLine("Processing " + file + " " + w.id);
          using(var sfs = File.OpenRead(file))
          {
            var deps = ParseImports(mreg.GetIncludePath(), file, sfs);
            deps.Add(file);
            //NOTE: adding self binary as a dep
            deps.Add(self_bin_file);

            var cache_file = w.GetCacheFile(file);

            AST_Module ast = null;

            if(!Util.NeedToRegen(cache_file, deps))
            {
              try
              {
                ast = Util.File2Meta<AST_Module>(cache_file);
                //Console.WriteLine("Cache hit");
              }
              catch(Exception)
              {
                //something went wrong while reading the cache, let's try parse it again
                ast = null;
                Console.WriteLine("Cache error: " + file);
              }
            }
            
            if(ast == null)
            {
              //Console.WriteLine("CACHE MISS");

              var mod = new Module(mreg.FilePath2ModulePath(file), file);
              ast = AST_Builder.Source2AST(mod, sfs, w.bindings, mreg);

              //save the cache
              Util.Meta2File(ast, cache_file);
            }

            foreach(var c in ast.children)
            {
              var fn = c as AST_FuncDecl;
              if(fn != null)
                w.symbols.Add(new Symbol2File(fn.Name(), file));
            }

            w.postproc.PostProc(ref ast);
            w.result.Add(ast);
            w.lz4_result.Add(EncodeToLZ4(ast));
          }
        }

      }
      catch(UserError e)
      {
        w.error = e;
      }
      catch(Exception e)
      {
        Console.WriteLine(e.Message + " " + e.StackTrace);
        w.error = new UserError(w.files[i], e.Message);
      }
    
      sw.Stop();
      Console.WriteLine("BHL Worker {0} done({1} sec)", w.id, Math.Round(sw.ElapsedMilliseconds/1000.0f,2));
    }
  }

  static byte[] EncodeToLZ4(AST ast)
  {
    var ms = new MemoryStream();

    Util.Meta2Bin(ast, ms);
    var bytes = ms.GetBuffer();

    var lz4_bytes = LZ4ps.LZ4Codec.Encode64(
        bytes, 0, (int)ms.Position
    );
    return lz4_bytes;
  }

  public static bool TestFile(string file)
  {
    return file.EndsWith(".bhl");
  }

  public delegate void FileCb(string file);

  static void DirWalk(string sDir, FileCb cb)
  {
    foreach(string f in Directory.GetFiles(sDir))
      cb(f);

    foreach(string d in Directory.GetDirectories(sDir))
      DirWalk(d, cb);
  }
}
