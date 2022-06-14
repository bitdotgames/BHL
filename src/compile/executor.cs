using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

namespace bhl {

using marshall;

public class CompileConf
{
  public string args = ""; 
  public List<string> files = new List<string>();
  public Types ts;
  public string self_file = "";
  public string inc_dir = "";
  public string res_file = "";
  public string tmp_dir = "";
  public bool use_cache = true;
  public string err_file = "";
  public IFrontPostProcessor postproc = new EmptyPostProcessor();
  public IUserBindings userbindings = new EmptyUserBindings();
  public int max_threads = 1;
  public bool check_deps = true;
  public bool debug = false;
  public bool verbose = true;
  public ModuleBinaryFormat module_fmt = ModuleBinaryFormat.FMT_LZ4; 
}
 
public class CompilationExecutor
{
  const byte COMPILE_FMT = 2;
  const uint FILE_VERSION = 1;

  public struct InterimResult
  {
    public bool use_file_cache;
    public ANTLR_Result parsed;
  }

  public class Cache : ANTLR_Parser.IParsedCache
  {
    public Dictionary<string, InterimResult> file2interim = new Dictionary<string, InterimResult>();

    public bool TryFetch(string file, out ANTLR_Result parsed)
    {
      parsed = null;
      InterimResult interim;
      if(file2interim.TryGetValue(file, out interim) && !interim.use_file_cache && interim.parsed != null)
      {
        parsed = interim.parsed;
        return true;
      }
      return false;
    }
  }

  public ICompileError Exec(CompileConf conf)
  {
    var sw = new Stopwatch();
    sw.Start();

    var err = DoExec(conf);

    sw.Stop();

    if(conf.verbose)
      Console.WriteLine("BHL build done({0} sec)", Math.Round(sw.ElapsedMilliseconds/1000.0f,2));

    return err;
  }

  ICompileError DoExec(CompileConf conf)
  {
    var res_dir = Path.GetDirectoryName(conf.res_file); 
    if(res_dir.Length > 0)
      Directory.CreateDirectory(res_dir);

    Directory.CreateDirectory(conf.tmp_dir);

    var args_changed = CheckArgsSignatureFile(conf);

    if(conf.use_cache && 
       !args_changed && 
       !BuildUtil.NeedToRegen(conf.res_file, conf.files)
      )
    {
      if(conf.verbose)
        Console.WriteLine("BHL no need to re-build");
      return null;
    }

    var ts = conf.ts;
    if(ts == null)
      ts = new Types();

    conf.userbindings.Register(ts);

    var parse_workers = StartParseWorkers(conf);
    var compiler_workers = StartAndWaitCompileWorkers(conf, ts, parse_workers);

    var tmp_res_file = conf.tmp_dir + "/" + Path.GetFileName(conf.res_file) + ".tmp";

    var werror = WriteCompilationResultToFile(conf, compiler_workers, tmp_res_file);
    if(werror != null)
      return werror;

    if(File.Exists(conf.res_file))
      File.Delete(conf.res_file);
    File.Move(tmp_res_file, conf.res_file);

    conf.postproc.Tally();

    return null;
  }

  static List<ParseWorker> StartParseWorkers(CompileConf conf)
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
      pw.cache_dir = conf.tmp_dir;
      pw.check_deps = conf.check_deps;
      pw.files = conf.files;
      pw.verbose = conf.verbose;

      parse_workers.Add(pw);
      pw.Start();

      idx += count;
    }

    return parse_workers;
  }

  static List<CompilerWorker> StartAndWaitCompileWorkers(CompileConf conf, Types ts, List<ParseWorker> parse_workers)
  {
    var compiler_workers = new List<CompilerWorker>();

    var cache = new Cache();

    //1. waiting for parse workers to finish
    foreach(var pw in parse_workers)
    {
      pw.Join();

      //let's create the shared interim result
      foreach(var kv in pw.file2interim)
        cache.file2interim.Add(kv.Key, kv.Value);
    }

    bool had_errors = false;
    foreach(var pw in parse_workers)
    {
      var cw = new CompilerWorker();
      cw.id = pw.id;
      cw.cache = cache;
      cw.inc_dir = conf.inc_dir;
      cw.cache_dir = pw.cache_dir;
      cw.ts = ts;
      cw.files = pw.files;
      cw.start = pw.start;
      cw.count = pw.count;
      cw.verbose = pw.verbose;
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

  ICompileError WriteCompilationResultToFile(CompileConf conf, List<CompilerWorker> compiler_workers, string file_path)
  {
    using(FileStream dfs = new FileStream(file_path, FileMode.Create, System.IO.FileAccess.Write))
    {
      var mwriter = new MsgPack.MsgPackWriter(dfs);

      mwriter.Write(ModuleLoader.COMPILE_FMT);
      mwriter.Write(FILE_VERSION);

      //used as a global namespace for unique symbols check
      var ns = new Namespace();

      int total_modules = 0;
      foreach(var w in compiler_workers)
      {
        if(w.error == null)
          CheckUniqueSymbols(ns, w);

        //NOTE: exit in case of error
        if(w.error != null)
        {
          if(conf.err_file == "-")
            Console.Error.WriteLine(ErrorUtils.ToJson(w.error));
          else
            File.WriteAllText(conf.err_file, ErrorUtils.ToJson(w.error));

          return w.error;
        }
        total_modules += w.file2path.Count;
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
            var path = w.file2path[file];
            var compiled_file = w.file2compiled[file];

            mwriter.Write((byte)conf.module_fmt);
            mwriter.Write(path.name);

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

      return null;
    }
  }

  static bool CheckArgsSignatureFile(CompileConf conf)
  {
    var tmp_args_file = conf.tmp_dir + "/" + Path.GetFileName(conf.res_file) + ".args";
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

  void CheckUniqueSymbols(Namespace ns, CompilerWorker w)
  {
    foreach(var kv in w.file2ns)
    {
      var file = kv.Key;
      var file_ns = kv.Value;
      file_ns.UnlinkAll();

      var conflict = ns.TryLink(file_ns);
      if(conflict != null)
        w.error = new BuildError(file, "symbol '" + conflict.GetFullName() + "' is already declared in module '" + (conflict.scope as Namespace)?.module_name + "'");
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
    public bool verbose;
    public List<string> inc_path = new List<string>();
    public List<string> files;
    public Dictionary<string, InterimResult> file2interim = new Dictionary<string, InterimResult>();
    public ICompileError error = null;

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

            var cache_file = GetCompiledCacheFile(w.cache_dir, file);

            var interim = new InterimResult();

            if(!w.use_cache || BuildUtil.NeedToRegen(cache_file, deps))
            {
              var parser = ANTLR_Parser.Stream2Parser(file, sfs);
              var parsed = new ANTLR_Result(parser.TokenStream, parser.program());

              interim.parsed = parsed;

              ++cache_miss;
              //Console.WriteLine("PARSE " + file + " " + cache_file);
            }
            else
            {
              interim.use_file_cache = true;

              ++cache_hit;
            }

            w.file2interim[file] = interim;
          }
        }
      }
      catch(Exception e)
      {
        if(e is ICompileError ie)
          w.error = ie;
        else
        {
          //let's log unexpected exceptions immediately
          Console.Error.WriteLine(e.Message + " " + e.StackTrace);
          w.error = new BuildError(w.files[i], e);
        }
      }

      sw.Stop();
      if(w.verbose)
        Console.WriteLine("BHL parser {0} done(hit/miss:{2}/{3}, {1} sec)", w.id, Math.Round(sw.ElapsedMilliseconds/1000.0f,2), cache_hit, cache_miss);
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
        return Marshall.File2Obj<FileImports>(cache_imports_file);
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
    public Thread th;
    public string inc_dir;
    public string cache_dir;
    public List<string> files;
    public Types ts;
    public int start;
    public int count;
    public bool verbose;
    public IFrontPostProcessor postproc;
    public ICompileError error = null;
    public Cache cache;
    public Dictionary<string, ModulePath> file2path = new Dictionary<string, ModulePath>();
    public Dictionary<string, string> file2compiled = new Dictionary<string, string>();
    public Dictionary<string, Namespace> file2ns = new Dictionary<string, Namespace>();

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
      w.file2path.Clear();
      w.file2compiled.Clear();

      var imp = new ANTLR_Parser.Importer();
      imp.SetParsedCache(w.cache);
      if(!string.IsNullOrEmpty(w.inc_dir))
        imp.AddToIncludePath(w.inc_dir);

      int i = w.start;

      int cache_hit = 0;
      int cache_miss = 0;

      try
      {
        for(int cnt = 0;i<(w.start + w.count);++i,++cnt)
        {
          var file = w.files[i]; 

          var compiled_file = GetCompiledCacheFile(w.cache_dir, file);
          var file_module = new Module(w.ts, imp.FilePath2ModuleName(file), file);

          InterimResult interim;
          if(w.cache.file2interim.TryGetValue(file, out interim) 
              && interim.use_file_cache)
          {
            ++cache_hit;

            w.file2path.Add(file, file_module.path);
          }
          else
          {
            ++cache_miss;

            ANTLR_Parser.Result front_res = null;

            if(interim.parsed != null)
              front_res = ANTLR_Parser.ProcessParsed(file_module, interim.parsed, w.ts, imp);
            else
              front_res = ANTLR_Parser.ProcessFile(file, w.ts, imp);

            front_res = w.postproc.Patch(front_res, file);

            w.file2path.Add(file, file_module.path);
            w.file2ns.Add(file, front_res.module.ns);

            var c  = new ModuleCompiler(front_res);
            var cm = c.Compile();
            CompiledModule.ToFile(cm, compiled_file);
          }

          w.file2compiled.Add(file, compiled_file);
        }
      }
      catch(Exception e)
      {
        if(e is ICompileError ie)
          w.error = ie;
        else
        {
          //let's log unexpected exceptions immediately
          Console.Error.WriteLine(e.Message + " " + e.StackTrace);
          w.error = new BuildError(w.files[i], e);
        }
      }

      sw.Stop();
      if(w.verbose)
        Console.WriteLine("BHL compiler {0} done(hit/miss:{2}/{3}, {1} sec)", w.id, Math.Round(sw.ElapsedMilliseconds/1000.0f,2), cache_hit, cache_miss);
    }
  }

  public static string GetCompiledCacheFile(string cache_dir, string file)
  {
    return cache_dir + "/" + Path.GetFileName(file) + "_" + Hash.CRC32(file) + ".compile.cache";
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

public interface IFrontPostProcessor
{
  //NOTE: returns patched result
  ANTLR_Parser.Result Patch(ANTLR_Parser.Result fres, string src_file);
  void Tally();
}

public class EmptyPostProcessor : IFrontPostProcessor 
{
  public ANTLR_Parser.Result Patch(ANTLR_Parser.Result fres, string src_file) { return fres; }
  public void Tally() {}
}

public static class BuildUtil
{
  static ConcurrentDictionary<string, DateTime> mtimes = new ConcurrentDictionary<string, DateTime>();
  static ConcurrentDictionary<string, bool> exists = new ConcurrentDictionary<string, bool>();

  static public bool NeedToRegen(string file, List<string> deps)
  {
    if(!FileExists(file))
    {
      //Console.WriteLine("Missing " + file);
      return true;
    }

    var fmtime = GetLastWriteTime(file);
    foreach(var dep in deps)
    {
      if(FileExists(dep) && GetLastWriteTime(dep) > fmtime)
      {
        //Console.WriteLine("Stale " + dep + " " + file);
        return true;
      }
    }

    //Console.WriteLine("Hit "+ file);
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
