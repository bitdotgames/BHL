using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

namespace bhl {

using marshall;

public class BuildConf
{
  public string args = ""; 
  public List<string> files = new List<string>();
  public Types ts;
  public string self_file = "";
  public string inc_dir = "";
  public string res_file = "";
  public string cache_dir = "";
  public bool use_cache = true;
  public string err_file = "";
  public IBuildPostProcessor postproc = new EmptyPostProcessor();
  public UserBindings userbindings = new EmptyUserBindings();
  public int max_threads = 1;
  public bool check_deps = true;
  public bool debug = false; //TODO: not really used
  public ModuleBinaryFormat module_fmt = ModuleBinaryFormat.FMT_LZ4; 
}
 
public class Build
{
  const byte COMPILE_FMT = 2;
  const uint FILE_VERSION = 1;
  const int ERROR_EXIT_CODE = 2;

  Dictionary<string, string> name2file = new Dictionary<string, string>();

  public int Exec(BuildConf conf)
  {
    var sw = new Stopwatch();
    sw.Start();

    int code = DoExec(conf);

    sw.Stop();

    Console.WriteLine("BHL Build done({0} sec)", Math.Round(sw.ElapsedMilliseconds/1000.0f,2));

    return code;
  }

  int DoExec(BuildConf conf)
  {
    var res_dir = Path.GetDirectoryName(conf.res_file); 
    if(res_dir.Length > 0)
      Directory.CreateDirectory(res_dir);

    Directory.CreateDirectory(conf.cache_dir);

    var args_changed = CheckArgsSignatureFile(conf);

    if(conf.use_cache && 
        !args_changed && 
        !BuildUtil.NeedToRegen(conf.res_file, conf.files)
      )
      return 0;

    var ts = conf.ts;
    if(ts == null)
      ts = new Types();
    conf.userbindings.Register(ts);

    var parse_workers = StartParseWorkers(conf);
    var compiler_workers = StartAndWaitCompileWorkers(conf, ts, parse_workers);

    var tmp_res_file = conf.cache_dir + "/" + Path.GetFileName(conf.res_file) + ".tmp";

    if(!WriteCompilationResultToFile(conf, compiler_workers, tmp_res_file))
      return ERROR_EXIT_CODE;

    if(File.Exists(conf.res_file))
      File.Delete(conf.res_file);
    File.Move(tmp_res_file, conf.res_file);

    conf.postproc.Tally();

    return 0;
  }

  static List<ParseWorker> StartParseWorkers(BuildConf conf)
  {
    var parse_workers = new List<ParseWorker>();

    int files_per_worker = conf.files.Count < conf.max_threads ? conf.files.Count : (int)Math.Ceiling((float)conf.files.Count / (float)conf.max_threads);

    int idx = 0;
    int wid = 0;

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

    return parse_workers;
  }

  static List<CompilerWorker> StartAndWaitCompileWorkers(BuildConf conf, Types ts, List<ParseWorker> parse_workers)
  {
    var compiler_workers = new List<CompilerWorker>();

    //1. parsing first and creating the shared parsed cache 
    var parsed_result = new Dictionary<string, ANTLR_Result>(); 

    bool had_errors = false;
    foreach(var pw in parse_workers)
    {
      pw.Join();

      foreach(var kv in pw.parsed_result)
        parsed_result.Add(kv.Key, kv.Value);

      var cw = new CompilerWorker();
      cw.id = pw.id;
      cw.parsed_result = parsed_result;
      cw.inc_dir = conf.inc_dir;
      cw.cache_dir = pw.cache_dir;
      cw.use_cache = pw.use_cache;
      cw.ts = ts;
      cw.files = pw.files;
      cw.start = pw.start;
      cw.count = pw.count;
      cw.postproc = conf.postproc;

      //passing parser error if any
      cw.error = pw.error;

      if(pw.error != null)
        had_errors = true;

      compiler_workers.Add(cw);
    }

    //2. starting workers which now actually compile the code
    //   if there were no errors
    if(!had_errors)
    {
      foreach(var w in compiler_workers)
        w.Start();

      foreach(var w in compiler_workers)
        w.Join();
    }

    return compiler_workers;
  }

  bool WriteCompilationResultToFile(BuildConf conf, List<CompilerWorker> compiler_workers, string file_path)
  {
    using(FileStream dfs = new FileStream(file_path, FileMode.Create, System.IO.FileAccess.Write))
    {
      var mwriter = new MsgPack.MsgPackWriter(dfs);

      mwriter.Write(ModuleImporter.COMPILE_FMT);
      mwriter.Write(FILE_VERSION);

      int total_modules = 0;
      foreach(var w in compiler_workers)
      {
        if(w.error == null)
          CheckUniqueSymbols(w);

        //NOTE: exit in case of error
        if(w.error != null)
        {
          if(conf.err_file == "-")
            Console.Error.WriteLine(ErrorUtils.ToJson(w.error));
          else
            File.WriteAllText(conf.err_file, ErrorUtils.ToJson(w.error));

          return false;
        }
        total_modules += w.file2module.Count;
      }
      mwriter.Write(total_modules);

      //NOTE: we'd like to write file binary modules in the same order they were added
      for(int file_idx=0; file_idx < conf.files.Count; ++file_idx)
      {
        var file = conf.files[file_idx];

        foreach(var w in compiler_workers)
        {
          if(file_idx >= w.start && file_idx < w.start + w.count) 
          {
            var module = w.file2module[file];
            var compiled_file = w.file2compiled[file];

            mwriter.Write((byte)conf.module_fmt);
            mwriter.Write(module.name);

            if(conf.module_fmt == ModuleBinaryFormat.FMT_BIN)
              mwriter.Write(File.ReadAllBytes(compiled_file));
            else if(conf.module_fmt == ModuleBinaryFormat.FMT_LZ4)
              mwriter.Write(EncodeToLZ4(File.ReadAllBytes(compiled_file)));
            else if(conf.module_fmt == ModuleBinaryFormat.FMT_FILE_REF)
              mwriter.Write(compiled_file);
            else
              throw new Exception("Unsupported format: " + conf.module_fmt);

            break;
          }
        }
      }

      return true;
    }
  }

  static bool CheckArgsSignatureFile(BuildConf conf)
  {
    var tmp_args_file = conf.cache_dir + "/" + Path.GetFileName(conf.res_file) + ".args";
    bool changed = !File.Exists(tmp_args_file) || (File.Exists(tmp_args_file) && File.ReadAllText(tmp_args_file) != conf.args);
    if(changed)
      File.WriteAllText(tmp_args_file, conf.args);
    return changed;
  }

  static byte[] EncodeToLZ4(byte[] bytes)
  {
    var lz4_bytes = LZ4ps.LZ4Codec.Encode64(
        bytes, 0, bytes.Length
        );
    return lz4_bytes;
  }

  public class Symbols : IMarshallable
  {
    //all collections have the same amount ot items
    public List<string> names = new List<string>();
    public List<string> files = new List<string>();

    public int Count => names.Count;

    public void Sync(SyncContext ctx) 
    {
      Marshall.Sync(ctx, names);
      Marshall.Sync(ctx, files);
    }

    public void Add(string name, string file)
    {
      names.Add(name);
      files.Add(file);
    }
  }

  void CheckUniqueSymbols(CompilerWorker w)
  {
    foreach(var kv in w.file2symbols)
    {
      var symbols = kv.Value;
      var file = kv.Key;

      for(int i=0;i<symbols.GetMembers().Count;++i)
      {
        var s = symbols.GetMembers()[i];
        string fpath;
        if(name2file.TryGetValue(s.name, out fpath))
        {
          w.error = new BuildError(file, "symbol '" + s.name + "' is already declared in '" + fpath + "'");
          break;
        }
        else
        {
          name2file.Add(s.name, file);
        }
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
    //NOTE: null value means file is cached
    public Dictionary<string, ANTLR_Result> parsed_result = new Dictionary<string, ANTLR_Result>();
    public Exception error = null;

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

      int cache_hit = 0;
      int cache_miss = 0;

      try
      {
        for(int cnt = 0;i<(w.start + w.count);++i,++cnt)
        {
          //too verbose?
          //if(cnt > 0 && cnt % 1000 == 0)
          //{
          //  var elapsed = Math.Round(sw.ElapsedMilliseconds/1000.0f,2);
          //  Console.WriteLine("BHL Parser " + w.id + " " + cnt + "/" + w.count + ", " + elapsed + " sec");
          //}

          var file = w.files[i]; 
          using(var sfs = File.OpenRead(file))
          {
            var deps = new List<string>();

            if(w.check_deps)
            {
              var imports = w.ParseImports(file, sfs);
              deps = imports.files;
            }
            deps.Add(file);

            //NOTE: adding self binary as a dep
            if(self_bin_file.Length > 0)
              deps.Add(self_bin_file);

            var cache_file = GetBuildCacheFile(w.cache_dir, file);

            //NOTE: null means - "try from file cache"
            ANTLR_Result parsed = null;
            if(!w.use_cache || BuildUtil.NeedToRegen(cache_file, deps))
            {
              var parser = Frontend.Stream2Parser(file, sfs);
              parsed = new ANTLR_Result(parser.TokenStream, parser.program());
              ++cache_miss;
              //Console.WriteLine("PARSE " + file + " " + cache_file);
            }
            else
              ++cache_hit;

            w.parsed_result[file] = parsed;
          }
        }
      }
      catch(Exception e)
      {
        if(e is IError)
          w.error = e;
        else
        {
          //let's log unexpected exceptions immediately
          Console.Error.WriteLine(e.Message + " " + e.StackTrace);
          w.error = new BuildError(w.files[i], e);
        }
      }

      sw.Stop();
      Console.WriteLine("BHL Parser {0} done(hit/miss:{2}/{3}, {1} sec)", w.id, Math.Round(sw.ElapsedMilliseconds/1000.0f,2), cache_hit, cache_miss);
    }

    public class FileImports : IMarshallable
    {
      public List<string> files = new List<string>();

      public void Reset() 
      {
        files.Clear();
      }

      public void Sync(SyncContext ctx) 
      {
        Marshall.Sync(ctx, files);
      }
    }

    FileImports ParseImports(string file, FileStream fsf)
    {
      var imports = TryReadImportsCache(file);
      if(imports == null)
      {
        imports = new FileImports();
        imports.files = ParseImports(inc_path, file, fsf);
        WriteImportsCache(file, imports);
      }
      return imports;
    }

    FileImports TryReadImportsCache(string file)
    {
      var cache_imports_file = GetImportsCacheFile(cache_dir, file);

      if(BuildUtil.NeedToRegen(cache_imports_file, file))
        return null;

      try
      {
        return Marshall.File2Obj<FileImports>(cache_imports_file, new AST_Factory());
      }
      catch
      {
        return null;
      }
    }

    void WriteImportsCache(string file, FileImports imports)
    {
      //Console.WriteLine("IMPORTS MISS " + file);
      var cache_imports_file = GetImportsCacheFile(cache_dir, file);
      Marshall.Obj2File(imports, cache_imports_file);
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
  }

  public class CompilerWorker
  {
    public int id;
    public Dictionary<string, ANTLR_Result> parsed_result;
    public Thread th;
    public string inc_dir;
    public bool use_cache;
    public string cache_dir;
    public List<string> files;
    public Types ts;
    public int start;
    public int count;
    public IBuildPostProcessor postproc;
    public Exception error = null;
    public Dictionary<string, Module> file2module = new Dictionary<string, Module>();
    public Dictionary<string, string> file2compiled = new Dictionary<string, string>();
    public Dictionary<string, ModuleScope> file2symbols = new Dictionary<string, ModuleScope>();

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

      var w = (CompilerWorker)data;
      w.file2module.Clear();
      w.file2compiled.Clear();

      var imp = new Frontend.Importer();
      imp.SetParsedCache(w.parsed_result);
      imp.AddToIncludePath(w.inc_dir);

      int i = w.start;

      int cache_hit = 0;
      int cache_miss = 0;

      try
      {
        for(int cnt = 0;i<(w.start + w.count);++i,++cnt)
        {
          var file = w.files[i]; 

          var cache_file = GetBuildCacheFile(w.cache_dir, file);
          var file_module = new Module(w.ts.globs, imp.FilePath2ModuleName(file), file);

          FrontendResult front_res = null;

          ANTLR_Result parsed;
          w.parsed_result.TryGetValue(file, out parsed);
          ////NOTE: checking if data must be taken from the file cache (parsed is null)
          //if(w.parsed_cache.TryGetValue(file, out parsed) && parsed == null)
          //{
          //  ++cache_hit;
          //  //TODO:
          //  throw new NotImplementedException();
          //}
          //else
          //{
            ++cache_miss;

            if(parsed == null)
              front_res = Frontend.ProcessFile(file, w.ts, imp);
            else
              front_res = Frontend.ProcessParsed(file_module, parsed, w.ts, imp);
          //}

          w.file2module.Add(file, file_module);
          w.file2symbols.Add(file, front_res.module.scope);

          string compiled_file = w.postproc.Patch(front_res, file, cache_file);

          var c  = new ModuleCompiler(w.ts, front_res);
          var cm = c.Compile();
          CompiledModule.ToFile(cm, compiled_file);

          w.file2compiled.Add(file, compiled_file);
        }
      }
      catch(Exception e)
      {
        if(e is IError)
          w.error = e;
        else
        {
          //let's log unexpected exceptions immediately
          Console.Error.WriteLine(e.Message + " " + e.StackTrace);
          w.error = new BuildError(w.files[i], e);
        }
      }

      sw.Stop();
      Console.WriteLine("BHL Compiler {0} done(hit/miss:{2}/{3}, {1} sec)", w.id, Math.Round(sw.ElapsedMilliseconds/1000.0f,2), cache_hit, cache_miss);
    }
  }

  public static string GetBuildCacheFile(string cache_dir, string file)
  {
    return cache_dir + "/" + Path.GetFileName(file) + "_" + Hash.CRC32(file) + ".bld.cache";
  }

  public static string GetImportsCacheFile(string cache_dir, string file)
  {
    return cache_dir + "/" + Hash.CRC32(file) + ".imports.cache";
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

public interface IBuildPostProcessor
{
  //NOTE: returns path to the result file
  string Patch(FrontendResult build_res, string src_file, string result_file);
  void Tally();
}

public class EmptyPostProcessor : IBuildPostProcessor 
{
  public string Patch(FrontendResult build_res, string src_file, string result_file) { return result_file; }
  public void Tally() {}
}

public static class BuildUtil
{
  static ConcurrentDictionary<string, DateTime> mtimes = new ConcurrentDictionary<string, DateTime>();
  static ConcurrentDictionary<string, bool> exists = new ConcurrentDictionary<string, bool>();

  static public bool NeedToRegen(string file, List<string> deps)
  {
    if(!FileExists(file))
      return true;

    var fmtime = GetLastWriteTime(file);
    foreach(var dep in deps)
    {
      if(FileExists(dep) && GetLastWriteTime(dep) > fmtime)
        return true;
    }

    return false;
  }

  //optimized version for just one dependency
  static public bool NeedToRegen(string file, string dep)
  {
    if(!FileExists(file))
      return true;

    var fmtime = GetLastWriteTime(file);
    if(FileExists(dep) && GetLastWriteTime(dep) > fmtime)
      return true;

    return false;
  }

  static DateTime GetLastWriteTime(string file)
  {
    return mtimes.GetOrAdd(file, (key) => new FileInfo(key).LastWriteTime);
  }

  static bool FileExists(string file)
  {
    return exists.GetOrAdd(file, (key) => File.Exists(key));
  }
}

} //namespace bhl
