using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using Newtonsoft.Json;

namespace bhl {

using marshall;

public class ProjectConf
{
  [JsonIgnore]
  public string proj_file = "";

  public ModuleBinaryFormat module_fmt = ModuleBinaryFormat.FMT_LZ4;

  public string src_dirs = "";

  [JsonIgnore]
  IncludePath _inc_path;
  public IncludePath inc_path {
    get {
      if(_inc_path == null)
      {
        _inc_path = new IncludePath();
        foreach(var path in src_dirs.Split(';'))
          _inc_path.Add(NormalizePath(path));
      }
      return _inc_path;
    }
  }

  public string result_file = "";
  public string tmp_dir = "";
  public string error_file = "";
  public string postproc_dll_file = "";
  public string userbindings_dll_file = "";
  public bool use_cache = true;
  public int verbosity = 1;
  public int max_threads = 1;
  public bool deterministic = false;

  string NormalizePath(string file_path)
  {
    if(Path.IsPathRooted(file_path))
      return Util.NormalizeFilePath(file_path);
    else if(!string.IsNullOrEmpty(proj_file))
      return Util.NormalizeFilePath(Path.Combine(Path.GetDirectoryName(proj_file), file_path));
    return file_path;
  }

  public void Setup()
  {
    result_file = NormalizePath(result_file);
    tmp_dir = NormalizePath(tmp_dir);
    error_file = NormalizePath(error_file);
    postproc_dll_file = NormalizePath(postproc_dll_file);
    userbindings_dll_file = NormalizePath(userbindings_dll_file);
  }
}

public class CompileConf
{
  public ProjectConf proj;
  public Types ts;
  public string args = ""; 
  public List<string> files = new List<string>();
  public string self_file = "";
  public IFrontPostProcessor postproc = new EmptyPostProcessor();
  public IUserBindings userbindings = new EmptyUserBindings();
}
 
public class CompilationExecutor
{
  const byte COMPILE_FMT = 2;
  const uint FILE_VERSION = 1;

  public struct InterimResult
  {
    public ModulePath module_path;
    public string compiled_file;
    public FileImports imports;
    public ANTLR_Parsed parsed;
    //loaded from cache compiled result
    public CompiledModule compiled;
  }

  public int cache_hits { get; private set; }
  public int cache_miss { get; private set; }
  public int cache_errs { get; private set; }

  public CompileErrors Exec(CompileConf conf)
  {
    var sw = Stopwatch.StartNew();

    var errors = new CompileErrors();

    try
    {
      DoExec(conf, errors);
    }
    catch(Exception e)
    {
      if(conf.proj.verbosity > 0)
        Console.Error.WriteLine(e.Message + " " + e.StackTrace);
      errors.Add(new BuildError("?", e));
    }

    sw.Stop();

    if(conf.proj.verbosity > 0)
      Console.WriteLine("BHL build done({0} sec)", Math.Round(sw.ElapsedMilliseconds/1000.0f,2));

    if(errors.Count > 0)
    {
      if(!string.IsNullOrEmpty(conf.proj.error_file))
      {
        string err_str = "";
        foreach(var err in errors)
          err_str += ErrorUtils.ToJson(err) + "\n";
        err_str = err_str.Trim();
        
        if(conf.proj.error_file == "-")
          Console.Error.WriteLine(err_str);
        else
          File.WriteAllText(conf.proj.error_file, err_str);
      }
    }

    return errors;
  }

  void DoExec(CompileConf conf, CompileErrors errors)
  {
    Stopwatch sw;

    if(!string.IsNullOrEmpty(conf.proj.error_file))
      File.Delete(conf.proj.error_file);

    var res_dir = Path.GetDirectoryName(conf.proj.result_file); 
    if(res_dir.Length > 0)
      Directory.CreateDirectory(res_dir);

    Directory.CreateDirectory(conf.proj.tmp_dir);

    var args_changed = CheckArgsSignatureFile(conf);

    if(conf.proj.use_cache && 
       !args_changed && 
       !Util.NeedToRegen(conf.proj.result_file, conf.files)
      )
    {
      if(conf.proj.verbosity > 0)
        Console.WriteLine("No stale files detected");
      return;
    }

    if(conf.ts == null)
      conf.ts = new Types();

    conf.userbindings.Register(conf.ts);

    sw = Stopwatch.StartNew();
    //1. start parse workers
    var parse_workers = StartParseWorkers(conf);

    //2. wait for the completion of parse workers
    foreach(var pw in parse_workers)
      pw.Join();

    sw.Stop();
    //Console.WriteLine("Parse done({0} sec)", Math.Round(sw.ElapsedMilliseconds/1000.0f,2));
    
    foreach(var pw in parse_workers)
      errors.AddRange(pw.errors);

    //2.1 let's collect all interim results collected in parse workers
    var file2interim = new Dictionary<string, InterimResult>();

    //3. create ANTLR processors for non-compiled files
    var file2proc = new Dictionary<string, ANTLR_Processor>(); 
    var file2compiled = new Dictionary<string, CompiledModule>(); 

    sw = Stopwatch.StartNew();
    foreach(var pw in parse_workers)
    {
      cache_hits += pw.cache_hits;
      cache_miss += pw.cache_miss;
      cache_errs += pw.cache_errs;

      foreach(var kv in pw.file2interim)
      {
        if(kv.Value.compiled == null)
        {
          var file_module = new Module(
            conf.ts,
            conf.proj.inc_path.FilePath2ModuleName(kv.Key), 
            kv.Key
          );
          var proc_errs = new CompileErrors();
          var proc = ANTLR_Processor.MakeProcessor(
            file_module, 
            kv.Value.imports, 
            kv.Value.parsed, 
            conf.ts, 
            proc_errs,
            ErrorHandlers.MakeStandard(kv.Key, proc_errs)
          );
          file2proc.Add(kv.Key, proc);
        }
        else
          file2compiled.Add(kv.Key, kv.Value.compiled);

        file2interim.Add(kv.Key, kv.Value);
      }
    }
    sw.Stop();
    //Console.WriteLine("Proc make done({0} sec)", Math.Round(sw.ElapsedMilliseconds/1000.0f,2));

    sw = Stopwatch.StartNew();
    //4. wait for ANTLR processors execution
    //TODO: it's not multithreaded yet
    ANTLR_Processor.ProcessAll(file2proc, file2compiled, conf.proj.inc_path);
    sw.Stop();
    //Console.WriteLine("Proc all done({0} sec)", Math.Round(sw.ElapsedMilliseconds/1000.0f,2));

    foreach(var kv in file2proc)
      errors.AddRange(kv.Value.result.errors);
    if(errors.Count > 0)
      return;
    
    //5. compile to bytecode 
    sw = Stopwatch.StartNew();
    var compiler_workers = StartAndWaitCompilerWorkers(
      conf, 
      conf.ts, 
      parse_workers, 
      file2interim,
      file2proc
    );
    sw.Stop();
    //Console.WriteLine("Compile done({0} sec)", Math.Round(sw.ElapsedMilliseconds/1000.0f,2));

    foreach(var cw in compiler_workers)
      errors.AddRange(cw.errors);
    if(errors.Count > 0)
      return;

    var tmp_res_file = conf.proj.tmp_dir + "/" + Path.GetFileName(conf.proj.result_file) + ".tmp";

    sw = Stopwatch.StartNew();
    var check_err = CheckUniqueSymbols(compiler_workers);
    sw.Stop();
    //Console.WriteLine("Check done({0} sec)", Math.Round(sw.ElapsedMilliseconds/1000.0f,2));
    if(check_err != null)
    {
      errors.Add(check_err);
      return;
    }

    WriteCompilationResultToFile(conf, compiler_workers, tmp_res_file);

    if(File.Exists(conf.proj.result_file))
      File.Delete(conf.proj.result_file);
    File.Move(tmp_res_file, conf.proj.result_file);

    sw = Stopwatch.StartNew();
    conf.postproc.Tally();
    sw.Stop();
    //Console.WriteLine("Postproc done({0} sec)", Math.Round(sw.ElapsedMilliseconds/1000.0f,2));
  }

  static List<ParseWorker> StartParseWorkers(CompileConf conf)
  {
    var parse_workers = new List<ParseWorker>();

    int files_per_worker = conf.files.Count < conf.proj.max_threads ? conf.files.Count : (int)Math.Ceiling((float)conf.files.Count / (float)conf.proj.max_threads);

    int idx = 0;
    int wid = 0;

    while(idx < conf.files.Count)
    {
      int count = (idx + files_per_worker) > conf.files.Count ? (conf.files.Count - idx) : files_per_worker; 

      ++wid;

      var pw = new ParseWorker();
      pw.conf = conf;
      pw.id = wid;
      pw.start = idx;
      pw.count = count;

      parse_workers.Add(pw);
      pw.Start();

      idx += count;
    }

    return parse_workers;
  }

  static List<CompilerWorker> StartAndWaitCompilerWorkers(
    CompileConf conf, 
    Types ts, 
    List<ParseWorker> parse_workers, 
    Dictionary<string, InterimResult> file2interim,
    Dictionary<string, ANTLR_Processor> file2proc
    )
  {
    var compiler_workers = new List<CompilerWorker>();

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

      compiler_workers.Add(cw);
    }

    foreach(var cw in compiler_workers)
      cw.Start();

    foreach(var cw in compiler_workers)
      cw.Join();

    return compiler_workers;
  }

  SymbolError CheckUniqueSymbols(List<CompilerWorker> compiler_workers)
  {
    //used as a global namespace for unique symbols check
    var ns = new Namespace();
    foreach(var cw in compiler_workers)
    {
      var check_err = CheckUniqueSymbols(ns, cw);
      if(check_err != null)
        return check_err;
    }
    return null;
  }

  void WriteCompilationResultToFile(CompileConf conf, List<CompilerWorker> compiler_workers, string file_path)
  {
    using(FileStream dfs = new FileStream(file_path, FileMode.Create, System.IO.FileAccess.Write))
    {
      var mwriter = new MsgPack.MsgPackWriter(dfs);

      mwriter.Write(ModuleLoader.COMPILE_FMT);
      mwriter.Write(FILE_VERSION);

      int total_modules = conf.files.Count;
      mwriter.Write(total_modules);

      //NOTE: we'd like to write file binary modules in the same order they were added
      for(int file_idx=0; file_idx < conf.files.Count; ++file_idx)
      {
        var file = conf.files[file_idx];

        foreach(var cw in compiler_workers)
        {
          if(file_idx >= cw.start && file_idx < cw.start + cw.count) 
          {
            var path = cw.file2interim[file].module_path;
            var compiled_file = cw.file2interim[file].compiled_file;

            mwriter.Write((byte)conf.proj.module_fmt);
            mwriter.Write(path.name);

            if(conf.proj.module_fmt == ModuleBinaryFormat.FMT_BIN)
              mwriter.Write(File.ReadAllBytes(compiled_file));
            else if(conf.proj.module_fmt == ModuleBinaryFormat.FMT_LZ4)
              mwriter.Write(EncodeToLZ4(File.ReadAllBytes(compiled_file)));
            else if(conf.proj.module_fmt == ModuleBinaryFormat.FMT_FILE_REF)
              mwriter.Write(compiled_file);
            else
              throw new Exception("Unsupported format: " + conf.proj.module_fmt);

            break;
          }
        }
      }
    }
  }

  static bool CheckArgsSignatureFile(CompileConf conf)
  {
    var tmp_args_file = conf.proj.tmp_dir + "/" + Path.GetFileName(conf.proj.result_file) + ".args";
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

  SymbolError CheckUniqueSymbols(Namespace ns, CompilerWorker w)
  {
    foreach(var kv in w.file2ns)
    {
      var file = kv.Key;
      var file_ns = kv.Value;
      file_ns.UnlinkAll();

      var conflict = ns.TryLink(file_ns);
      if(!conflict.Ok)
        return new SymbolError(conflict.local, "symbol '" + conflict.other.GetFullPath() + "' is already declared in module '" + (conflict.other.scope as Namespace)?.module.name + "'");
    }
    return null;
  }

  public class ParseWorker
  {
    public CompileConf conf;
    public int id;
    public Thread th;
    public string self_file;
    public int start;
    public int count;
    public Dictionary<string, InterimResult> file2interim = new Dictionary<string, InterimResult>();
    public CompileErrors errors = new CompileErrors();
    public int cache_hits;
    public int cache_miss;
    public int cache_errs;

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

            var compiled_file = GetCompiledCacheFile(w.conf.proj.tmp_dir, file);

            var interim = new InterimResult();
            interim.module_path = new ModulePath(w.conf.proj.inc_path.FilePath2ModuleName(file), file);
            interim.imports = imports;
            interim.compiled_file = compiled_file;

            bool use_cache = w.conf.proj.use_cache && !Util.NeedToRegen(compiled_file, deps);

            if(use_cache)
            {
              try
              {
                interim.compiled = CompiledModule.FromFile(compiled_file, w.conf.ts);
                ++w.cache_hits;
              }
              catch(Exception)
              {
                use_cache = false;
                ++w.cache_errs;
              }
            }

            if(!use_cache)
            {
              var parser = ANTLR_Processor.Stream2Parser(
                file, 
                sfs, 
                ErrorHandlers.MakeStandard(file, w.errors)
              );
              interim.parsed = new ANTLR_Parsed(parser);

              ++w.cache_miss;
            }

            w.file2interim[file] = interim;
          }
        }
      }
      catch(Exception e)
      {
        if(e is ICompileError ie)
          w.errors.Add(ie);
        else
        {
          if(w.conf.proj.verbosity > 0)
            Console.Error.WriteLine(e.Message + " " + e.StackTrace);
          w.errors.Add(new BuildError(w.conf.files[i], e));
        }
      }

      sw.Stop();
      if(w.conf.proj.verbosity > 0)
        Console.WriteLine("BHL parser {0} done(hit/miss/err:{2}/{3}/{4}, {1} sec)", w.id, Math.Round(sw.ElapsedMilliseconds/1000.0f,2), w.cache_hits, w.cache_miss, w.cache_errs);
    }

    FileImports GetImports(string file, FileStream fsf)
    {
      var imports = TryReadImportsCache(file);
      if(imports == null)
      {
        imports = ParseImports(conf.proj.inc_path, file, fsf);
        WriteImportsCache(file, imports);
      }
      return imports;
    }

    FileImports TryReadImportsCache(string file)
    {
      var cache_imports_file = GetImportsCacheFile(conf.proj.tmp_dir, file);

      if(Util.NeedToRegen(cache_imports_file, file))
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
      var cache_imports_file = GetImportsCacheFile(conf.proj.tmp_dir, file);
      Marshall.Obj2File(imports, cache_imports_file);
    }

    public static FileImports ParseImports(IncludePath inc_path, string file, Stream stream)
    {
      var imps = new FileImports();

      var r = new StreamReader(stream);

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

      stream.Position = 0;

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
    public CompileErrors errors = new CompileErrors();
    public Dictionary<string, InterimResult> file2interim = new Dictionary<string, InterimResult>();
    public Dictionary<string, ANTLR_Processor> file2proc = new Dictionary<string, ANTLR_Processor>();
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

      string current_file = "";

      try
      {
        int i = w.start;
        for(int cnt = 0;i<(w.start + w.count);++i,++cnt)
        {
          current_file = w.conf.files[i]; 

          var interim = w.file2interim[current_file];

          if(interim.compiled != null)
          {
            w.file2ns.Add(current_file, interim.compiled.module.ns);
          }
          else
          {
            var proc = w.file2proc[current_file];

            var proc_result = w.postproc.Patch(proc.result, current_file);

            var c  = new ModuleCompiler(proc_result);
            var cm = c.Compile();

            CompiledModule.ToFile(cm, interim.compiled_file);

            w.file2ns.Add(current_file, proc_result.module.ns);
          }
        }
      }
      catch(Exception e)
      {
        if(e is ICompileError ie)
          w.errors.Add(ie);
        else
        {
          if(w.conf.proj.verbosity > 0)
            Console.Error.WriteLine(e.Message + " " + e.StackTrace);
          w.errors.Add(new BuildError(current_file, e));
        }
      }

      sw.Stop();
      if(w.conf.proj.verbosity > 0)
        Console.WriteLine("BHL compiler {0} done({1} sec)", w.id, Math.Round(sw.ElapsedMilliseconds/1000.0f,2));
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

} //namespace bhl
