using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Antlr4.Runtime.Misc;

namespace bhl {

public class BuildConf
{
  public List<string> files = new List<string>();
  public GlobalScope globs;
  public string self_file = "";
  public string inc_dir = "";
  public string res_file = "";
  public string cache_dir = "";
  public bool use_cache = true;
  public string err_file = "";
  public PostProcessor postproc = new EmptyPostProcessor();
  public UserBindings userbindings = new EmptyUserBindings();
  public int max_threads = 1;
  public bool check_deps = true;
  public bool debug = false;
}
 
public class Build
{
  UniqueSymbols uniq_symbols = new UniqueSymbols();

  public int Exec(BuildConf conf)
  {
    var res_dir = Path.GetDirectoryName(conf.res_file); 
    if(res_dir.Length > 0)
      Directory.CreateDirectory(res_dir);

    Directory.CreateDirectory(conf.cache_dir);

    if(conf.use_cache && !Util.NeedToRegen(conf.res_file, conf.files) && (conf.postproc != null && !conf.postproc.NeedToRegen(conf.files)))
      return 0;

    var globs = conf.globs;
    if(globs == null)
      globs = SymbolTable.CreateBuiltins();
    conf.userbindings.Register(globs);

    Util.DEBUG = conf.debug;
    Util.SetupASTFactory();

    var parse_workers = new List<ParseWorker>();
    var workers = new List<Worker>();

    int files_per_worker = conf.files.Count < conf.max_threads ? conf.files.Count : (int)Math.Ceiling((float)conf.files.Count / (float)conf.max_threads);
    int idx = 0;
    int wid = 0;

    var parsed_cache = new Dictionary<string, Parsed>(); 

    while(idx < conf.files.Count)
    {
      int count = (idx + files_per_worker) > conf.files.Count ? (conf.files.Count - idx) : files_per_worker; 

      ++wid;

      var pw = new ParseWorker();
      pw.id = wid;
      pw.self_file = conf.self_file;
      pw.start = idx;
      pw.inc_path.Add(conf.inc_dir);
      pw.count = count;
      pw.use_cache = conf.use_cache;
      pw.cache_dir = conf.cache_dir;
      pw.check_deps = conf.check_deps;
      pw.files = conf.files;

      parse_workers.Add(pw);
      pw.Start();

      idx += count;
    }

    //1. parsing first and creating the shared parsed cache 
    bool had_errors = false;
    foreach(var pw in parse_workers)
    {
      pw.Join();

      foreach(var kv in pw.parsed)
        parsed_cache.Add(kv.Key, kv.Value);

      var w = new Worker();
      w.id = pw.id;
      w.parsed = parsed_cache;
      w.inc_dir = conf.inc_dir;
      w.cache_dir = pw.cache_dir;
      w.use_cache = pw.use_cache;
      w.bindings = globs;
      w.files = pw.files;
      w.start = pw.start;
      w.count = pw.count;
      w.postproc = conf.postproc;

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

    var tmp_res_file = conf.cache_dir + "/" + Path.GetFileName(conf.res_file) + ".tmp";
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
          if(conf.err_file == "-")
            Console.Error.WriteLine(w.error.ToJson());
          else
            File.WriteAllText(conf.err_file, w.error.ToJson());

          return 2;
        }
        total_modules += w.result.Count;
      }

      mwriter.Write(total_modules);

      //NOTE: we'd like to write file binary modules in the same order they were added
      for(int file_idx=0; file_idx < conf.files.Count; ++file_idx)
      {
        var file = conf.files[file_idx];

        foreach(var w in workers)
        {
          if(file_idx >= w.start && file_idx < w.start + w.count) 
          {
            var m = w.result[file];
            var lz4 = w.lz4_result[file];

            mwriter.Write(ModuleLoader.FMT_LZ4);
            mwriter.Write(m.nname);
            mwriter.Write(lz4);
            break;
          }
        }
      }
    }

    if(File.Exists(conf.res_file))
      File.Delete(conf.res_file);
    File.Move(tmp_res_file, conf.res_file);

    conf.postproc.Finish();

    return 0;
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

  void CheckUniqueSymbols(Worker w)
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

  public class ParseWorker
  {
    public int id;
    public Thread th;
    public string self_file;
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

      string self_bin_file = w.self_file;

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
            if(self_bin_file.Length > 0)
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
    public Dictionary<string, AST_Module> result = new Dictionary<string, AST_Module>();
    public Dictionary<string, byte[]> lz4_result = new Dictionary<string, byte[]>();
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
          w.result.Add(file, ast);
          w.lz4_result.Add(file, EncodeToLZ4(ast));

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

  public static string GetCacheFile(string cache_dir, string file)
  {
    return cache_dir + "/" + Hash.CRC32(file + Util.DEBUG) + ".cache";
  }

  public static bool TestFile(string file)
  {
    return file.EndsWith(".bhl");
  }

  public delegate void FileCb(string file);

  public static void DirWalk(string sDir, FileCb cb)
  {
    foreach(string f in Directory.GetFiles(sDir))
      cb(f);

    foreach(string d in Directory.GetDirectories(sDir))
      DirWalk(d, cb);
  }

  public static void AddFilesFromDir(string dir, List<string> files)
  {
    DirWalk(dir, 
      delegate(string file) 
      { 
        if(TestFile(file))
          files.Add(file);
      }
    );
  }
}

} //namespace bhl
