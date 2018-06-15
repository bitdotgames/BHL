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
    bool use_cache = true;
    string err_file = "";
    string postproc_dll_path = "";
    string userbindings_dll_path = "";
    int MAX_THREADS = 1;
    bool check_deps = true;

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
      { "threads=", "number of threads",
          v => MAX_THREADS = int.Parse(v) },
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

    if(use_cache && !Util.NeedToRegen(res_file, files) && (postproc != null && !postproc.NeedToRegen(files)))
      return;

    var globs = SymbolTable.CreateBuiltins();
    userbindings.Register(globs);

    Util.SetupAutogenFactory();

    var parse_worker = new List<ParseWorker>();
    var workers = new List<Worker>();

    int files_per_worker = files.Count < MAX_THREADS ? files.Count : (int)Math.Ceiling((float)files.Count / (float)MAX_THREADS);
    int idx = 0;
    int wid = 0;

    var parsed_cache = new Dictionary<string, Parsed>(); 

    while(idx < files.Count)
    {
      int count = (idx + files_per_worker) > files.Count ? (files.Count - idx) : files_per_worker; 

      ++wid;

      var pw = new ParseWorker();
      pw.id = wid;
      pw.start = idx;
      pw.inc_path.Add(src_dir);
      pw.count = count;
      pw.use_cache = use_cache;
      pw.cache_dir = cache_dir;
      pw.check_deps = check_deps;
      pw.files = files;

      parse_worker.Add(pw);
      pw.Start();

      idx += count;
    }

    //1. parsing first and creating the shared parsed cache 
    bool had_errors = false;
    foreach(var pw in parse_worker)
    {
      pw.Join();

      foreach(var kv in pw.parsed)
        parsed_cache.Add(kv.Key, kv.Value);

      var w = new Worker();
      w.id = pw.id;
      w.parsed = parsed_cache;
      w.inc_dir = src_dir;
      w.cache_dir = pw.cache_dir;
      w.use_cache = pw.use_cache;
      w.bindings = globs;
      w.files = pw.files;
      w.start = pw.start;
      w.count = pw.count;
      w.postproc = postproc;

      //passing parser error if any
      w.error = pw.error;

      if(pw.error != null)
        had_errors = true;

      workers.Add(w);
    }

    //2. starting workers which now actually compile the code
    if(!had_errors)
    {
      foreach(var w in workers)
        w.Start();

      foreach(var w in workers)
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

        //NOTE: exit in case of error
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
    // Knuth shuffle algorithm - courtesy of Wikipedia :)
    for (int t = 0; t < arr.Count; ++t)
    {
      var tmp = arr[t];
      int r = rnd.Next(t, arr.Count);
      arr[t] = arr[r];
      arr[r] = tmp;
    }
  }

  public class UniqueSymbols
  {
    Dictionary<ulong, string> hash2path = new Dictionary<ulong, string>();
    Dictionary<string, string> str2path = new Dictionary<string, string>();

    public bool TryGetValue(HashedName key, out string path)
    {
      if(hash2path.TryGetValue(key.n, out path))
        return true;
      return str2path.TryGetValue(key.s, out path);
    }

    public void Add(HashedName name, string path)
    {
      // Dictionary operation first, so exception thrown if key already exists.
      if(!string.IsNullOrEmpty(name.s))
        str2path.Add(name.s, path);
      hash2path.Add(name.n, path);
    }

    public void Clear()
    {
      str2path.Clear();
      hash2path.Clear();
    }
  }

  static UniqueSymbols uniq_symbols = new UniqueSymbols();

  static void CheckUniqueSymbols(Worker w)
  {
    for(int i=0;i<w.symbols.Count; ++i)
    {
      var symb = w.symbols[i];

      string fpath;
      if(uniq_symbols.TryGetValue(symb.name, out fpath))
      {
        w.error = new UserError(symb.file, "Symbol '" + symb.name + "' is already declared in '" + fpath + "'");
        break;
      }
      else
      {
        uniq_symbols.Add(symb.name, symb.file);
      }
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

  public static string GetCacheFile(string cache_dir, string file)
  {
    return cache_dir + "/" + Hash.CRC32(file + Util.DEBUG) + ".cache";
  }

  public class ParseWorker
  {
    public int id;
    public Thread th;
    public bool check_deps;
    public bool use_cache;
    public string cache_dir;
    public int start;
    public int count;
    public List<string> inc_path = new List<string>();
    public List<string> files;
    //NOTE: null bhlParser means file is cached
    public Dictionary<string, Parsed> parsed = new Dictionary<string, Parsed>();
    public UserError error = null;

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

    public static void DoWork(object data)
    {
      var sw = new Stopwatch();
      sw.Start();

      var w = (ParseWorker)data;

      string self_bin_file = GetSelfFile();

      int i = w.start;

      try
      {
        for(int cnt = 0;i<(w.start + w.count);++i,++cnt)
        {
          if(cnt > 0 && cnt % 500 == 0)
          {
            var elapsed = Math.Round(sw.ElapsedMilliseconds/1000.0f,2);
            Console.WriteLine("BHL Parser " + w.id + " " + cnt + "/" + w.count + ", " + elapsed + " sec");
          }

          var file = w.files[i]; 
          using(var sfs = File.OpenRead(file))
          {
            var deps = w.check_deps ? 
              ParseImports(w.inc_path, file, sfs) : 
              new List<string>();
            deps.Add(file);
            //NOTE: adding self binary as a dep
            deps.Add(self_bin_file);

            var cache_file = GetCacheFile(w.cache_dir, file);

            //NOTE: null means - "try from cache"
            Parsed parsed = null;
            if(!w.use_cache || Util.NeedToRegen(cache_file, deps))
            {
              var parser = Frontend.Source2Parser(sfs);
              parsed = new Parsed();
              parsed.tokens = parser.TokenStream;
              parsed.prog = Frontend.ParseProgram(parser, file);
            }

            w.parsed[file] = parsed;
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
      Console.WriteLine("BHL Parser {0} done({1} sec)", w.id, Math.Round(sw.ElapsedMilliseconds/1000.0f,2));
    }
  }

  public class Worker
  {
    public int id;
    public Dictionary<string, Parsed> parsed;
    public Thread th;
    public string inc_dir;
    public bool use_cache;
    public string cache_dir;
    public List<string> files;
    public GlobalScope bindings;
    public int start;
    public int count;
    public List<AST_Module> result = new List<AST_Module>();
    public List<byte[]> lz4_result = new List<byte[]>();
    public List<Symbol2File> symbols = new List<Symbol2File>();
    public PostProcessor postproc;
    public UserError error = null;

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

    public static void DoWork(object data)
    {
      var sw = new Stopwatch();
      sw.Start();

      var w = (Worker)data;
      w.result.Clear();
      w.lz4_result.Clear();

      var mreg = new ModuleRegistry();
      mreg.SetParsedCache(w.parsed);
      mreg.AddToIncludePath(w.inc_dir);

      int i = w.start;

      int cache_hit = 0;
      int cache_miss = 0;

      try
      {
        for(int cnt = 0;i<(w.start + w.count);++i,++cnt)
        {
          //var sw1 = Stopwatch.StartNew();

          var file = w.files[i]; 

          var cache_file = GetCacheFile(w.cache_dir, file);
          var mod = new Module(mreg.FilePath2ModulePath(file), file);
          AST_Module ast = null;

          Parsed parsed = null;
          //NOTE: checking if file must taken from cache
          if(w.parsed != null && w.parsed.TryGetValue(file, out parsed) && parsed == null)
          {
            try
            {
              ast = Util.File2Meta<AST_Module>(cache_file);
              ++cache_hit;
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
            ++cache_miss;

            if(parsed == null)
            {
              using(var sfs = File.OpenRead(file))
              {
                ast = Frontend.Source2AST(mod, sfs, w.bindings, mreg);
              }
            }
            else
              ast = Frontend.Parsed2AST(mod, parsed, w.bindings, mreg);

            //save the cache if neccessary
            if(w.use_cache)
              Util.Meta2File(ast, cache_file);
          }

          foreach(var c in ast.children)
          {
            var fn = c as AST_FuncDecl;
            if(fn != null)
            {
              w.symbols.Add(new Symbol2File(fn.Name(), file));
              continue;
            }
            var cd = c as AST_ClassDecl;
            if(cd != null)
            {
              w.symbols.Add(new Symbol2File(cd.Name(), file));
              continue;
            }
            var vd = c as AST_VarDecl;
            if(vd != null)
            {
              w.symbols.Add(new Symbol2File(vd.Name(), file));
              continue;
            }
          }

          w.postproc.PostProc(ref ast);
          w.result.Add(ast);
          w.lz4_result.Add(EncodeToLZ4(ast));

          //sw1.Stop();
          //Console.WriteLine("BHL Builder {0}: file '{1}'({2} sec)", w.id, file, Math.Round(sw1.ElapsedMilliseconds/1000.0f,2));
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
      Console.WriteLine("BHL Builder {0} done(hit/miss:{2}/{3}, {1} sec)", w.id, Math.Round(sw.ElapsedMilliseconds/1000.0f,2), cache_hit, cache_miss);
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
