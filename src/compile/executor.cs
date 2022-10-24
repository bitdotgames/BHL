using System;
using System.IO;
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
  public IncludePath inc_path = new IncludePath();
  public string res_file = "";
  public string tmp_dir = "";
  public bool use_cache = true;
  public string err_file = "";
  public IFrontPostProcessor postproc = new EmptyPostProcessor();
  public IUserBindings userbindings = new EmptyUserBindings();
  public int max_threads = 1;
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
    public string compiled_file;
    public FileImports imports;
    public ANTLR_Parsed parsed;
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
    if(!string.IsNullOrEmpty(conf.err_file))
      File.Delete(conf.err_file);

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
        Console.WriteLine("No stale files detected");
      return null;
    }

    var ts = conf.ts;
    if(ts == null)
      ts = new Types();

    conf.userbindings.Register(ts);

    //1. start parse workers
    var parse_workers = StartParseWorkers(conf);

    var file2interim = new Dictionary<string, InterimResult>();
    var file2proc = new Dictionary<string, ANTLR_Processor>(); 
    //2. wait for the completion of parse workers
    foreach(var pw in parse_workers)
    {
      pw.Join();

      foreach(var kv in pw.file2interim)
      {
        var file_module = new Module(ts, conf.inc_path.FilePath2ModuleName(kv.Key), kv.Key);
        var proc = ANTLR_Processor.MakeProcessor(file_module, kv.Value.imports, kv.Value.parsed, ts);

        file2proc.Add(kv.Key, proc);
        file2interim.Add(kv.Key, kv.Value);
      }
    }

    //3. process parse result
    //TODO: it's not multithreaded yet
    ANTLR_Processor.ProcessAll(file2proc, conf.inc_path);
    
    //4. compile to bytecode 
    var compiler_workers = StartAndWaitCompileWorkers(
      conf, 
      ts, 
      parse_workers, 
      file2interim,
      file2proc
    );

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
      pw.conf = conf;
      pw.id = wid;
      pw.verbose = conf.verbose;
      pw.start = idx;
      pw.count = count;

      parse_workers.Add(pw);
      pw.Start();

      idx += count;
    }

    return parse_workers;
  }

  static List<CompilerWorker> StartAndWaitCompileWorkers(
    CompileConf conf, 
    Types ts, 
    List<ParseWorker> parse_workers, 
    Dictionary<string, InterimResult> file2interim,
    Dictionary<string, ANTLR_Processor> file2proc
    )
  {
    var compiler_workers = new List<CompilerWorker>();

    bool had_errors = false;
    foreach(var pw in parse_workers)
    {
      var cw = new CompilerWorker();
      cw.conf = pw.conf;
      cw.id = pw.id;
      cw.file2interim = file2interim;
      cw.file2proc = file2proc;
      cw.ts = ts;
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
          if(!string.IsNullOrEmpty(conf.err_file))
          {
            if(conf.err_file == "-")
              Console.Error.WriteLine(ErrorUtils.ToJson(w.error));
            else
              File.WriteAllText(conf.err_file, ErrorUtils.ToJson(w.error));
          }

          return w.error;
        }
        total_modules += w.file2modpath.Count;
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
            var path = w.file2modpath[file];
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
      if(!conflict.Ok)
        w.error = new SymbolError(conflict.local, "symbol '" + conflict.other.GetFullPath() + "' is already declared in module '" + (conflict.other.scope as Namespace)?.module_name + "'");
    }
  }

  public class ParseWorker
  {
    public CompileConf conf;
    public int id;
    public Thread th;
    public string self_file;
    public int start;
    public int count;
    public bool verbose;
    public IncludePath inc_path = new IncludePath();
    public Dictionary<string, InterimResult> file2interim = new Dictionary<string, InterimResult>();
    public ICompileError error = null;
    public int cache_hit;
    public int cache_miss;

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

      string self_bin_file = w.conf.self_file;

      int i = w.start;

      try
      {
        for(int cnt = 0;i<(w.start + w.count);++i,++cnt)
        {
          var file = w.conf.files[i]; 
          using(var sfs = File.OpenRead(file))
          {
            var imports = w.GetImports(file, sfs);
            var deps = new List<string>(imports.file_paths);
            deps.Add(file);

            //NOTE: adding self binary as a dep
            if(self_bin_file.Length > 0)
              deps.Add(self_bin_file);

            var compiled_file = GetCompiledCacheFile(w.conf.tmp_dir, file);

            var interim = new InterimResult();
            interim.imports = imports;
            interim.compiled_file = compiled_file;

            if(!w.conf.use_cache || BuildUtil.NeedToRegen(compiled_file, deps))
            {
              var proc = ANTLR_Processor.Stream2Parser(file, sfs);
              var parsed = new ANTLR_Parsed(proc.TokenStream, proc.program());

              interim.parsed = parsed;

              ++w.cache_miss;
              //Console.WriteLine("PARSE " + file + " " + cache_file);
            }
            else
            {
              interim.use_file_cache = true;

              ++w.cache_hit;
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
          w.error = new BuildError(w.conf.files[i], e);
        }
      }

      sw.Stop();
      if(w.verbose)
        Console.WriteLine("BHL parser {0} done(hit/miss:{2}/{3}, {1} sec)", w.id, Math.Round(sw.ElapsedMilliseconds/1000.0f,2), w.cache_hit, w.cache_miss);
    }

    FileImports GetImports(string file, FileStream fsf)
    {
      var imports = TryReadImportsCache(file);
      if(imports == null)
      {
        imports = ParseImports(inc_path, file, fsf);
        WriteImportsCache(file, imports);
      }
      return imports;
    }

    FileImports TryReadImportsCache(string file)
    {
      var cache_imports_file = GetImportsCacheFile(conf.tmp_dir, file);

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
      var cache_imports_file = GetImportsCacheFile(conf.tmp_dir, file);
      Marshall.Obj2File(imports, cache_imports_file);
    }

    static FileImports ParseImports(IncludePath inc_path, string file, FileStream fs)
    {
      var imps = new FileImports();

      var r = new StreamReader(fs);

      while(true)
      {
        var line = r.ReadLine();
        if(line == null)
          break;

        int import_idx = line.IndexOf("import");
        while(import_idx != -1)
        {
          int q1_idx = line.IndexOf('"', import_idx + 1);
          if(q1_idx != -1)
          {
            int q2_idx = line.IndexOf('"', q1_idx + 1);
            if(q2_idx != -1)
            {
              string rel_import = line.Substring(q1_idx + 1, q2_idx - q1_idx - 1);
              string file_path = inc_path.ResolveImportPath(file, rel_import);
              imps.Add(rel_import, file_path);
            }
            import_idx = line.IndexOf("import", q2_idx + 1);
          }
          else
            break;
        }
      }

      fs.Position = 0;

      return imps;
    }
  }

  public class CompilerWorker
  {
    public CompileConf conf;
    public int id;
    public Thread th;
    public Types ts;
    public int start;
    public int count;
    public IFrontPostProcessor postproc;
    public ICompileError error = null;
    public Dictionary<string, InterimResult> file2interim = new Dictionary<string, InterimResult>();
    public Dictionary<string, ANTLR_Processor> file2proc = new Dictionary<string, ANTLR_Processor>();
    public Dictionary<string, ModulePath> file2modpath = new Dictionary<string, ModulePath>();
    public Dictionary<string, string> file2compiled = new Dictionary<string, string>();
    public Dictionary<string, Namespace> file2ns = new Dictionary<string, Namespace>();
    public int cache_hit;
    public int cache_miss;

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

      string current_file = "";

      try
      {
        int i = w.start;
        for(int cnt = 0;i<(w.start + w.count);++i,++cnt)
        {
          current_file = w.conf.files[i]; 

          var interim = w.file2interim[current_file];
          var proc = w.file2proc[current_file];

          w.file2compiled.Add(current_file, interim.compiled_file);
          w.file2modpath.Add(current_file, proc.module.path);

          bool file_cache_ok = false;
          if(interim.use_file_cache)
          {
            try
            {
              var cm = CompiledModule.FromFile(interim.compiled_file, w.ts);
              w.file2ns.Add(current_file, cm.ns);

              ++w.cache_hit;
              file_cache_ok = true;
            }
            catch(Exception)
            {}
          }

          if(!file_cache_ok)
          {
            ++w.cache_miss;

            var proc_result = w.postproc.Patch(proc.result, current_file);

            var c  = new ModuleCompiler(proc_result);
            var cm = c.Compile();

            var compiled_file = w.file2compiled[current_file];
            CompiledModule.ToFile(cm, compiled_file);

            w.file2ns.Add(current_file, proc_result.module.ns);
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
          w.error = new BuildError(current_file, e);
        }
      }

      sw.Stop();
      if(w.conf.verbose)
        Console.WriteLine("BHL compiler {0} done(hit/miss:{2}/{3}, {1} sec)", w.id, Math.Round(sw.ElapsedMilliseconds/1000.0f,2), w.cache_hit, w.cache_miss);
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
  ANTLR_Processor.Result Patch(ANTLR_Processor.Result fres, string src_file);
  void Tally();
}

public class EmptyPostProcessor : IFrontPostProcessor 
{
  public ANTLR_Processor.Result Patch(ANTLR_Processor.Result fres, string src_file) { return fres; }
  public void Tally() {}
}

public static class BuildUtil
{
  static public bool NeedToRegen(string file, List<string> deps)
  {
    if(!File.Exists(file))
    {
      //Console.WriteLine("Missing " + file);
      return true;
    }

    var fmtime = GetLastWriteTime(file);
    foreach(var dep in deps)
    {
      if(File.Exists(dep) && GetLastWriteTime(dep) > fmtime)
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
    if(!File.Exists(file))
      return true;

    var fmtime = GetLastWriteTime(file);
    if(File.Exists(dep) && GetLastWriteTime(dep) > fmtime)
      return true;

    return false;
  }

  static DateTime GetLastWriteTime(string file)
  {
    return new FileInfo(file).LastWriteTime;
  }
}

} //namespace bhl
