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
  const string FILE_NAME = "bhl.proj";

  public static ProjectConf ReadFromFile(string file_path)
  {
    var proj = JsonConvert.DeserializeObject<ProjectConf>(File.ReadAllText(file_path));
    proj.proj_file = file_path;
    proj.Setup();
    return proj;
  }

  public static ProjectConf TryReadFromDir(string dir_path)
  {
    string proj_file = dir_path + "/" + FILE_NAME; 
    if(!File.Exists(proj_file))
      return null;
    return ReadFromFile(proj_file);
  }

  [JsonIgnore]
  public string proj_file = "";

  public ModuleBinaryFormat module_fmt = ModuleBinaryFormat.FMT_LZ4;

  public List<string> inc_dirs = new List<string>();
  [JsonIgnore]
  public IncludePath inc_path = new IncludePath();

  public List<string> src_dirs = new List<string>();

  public List<string> defines = new List<string>();

  public string result_file = "";
  public string tmp_dir = "";
  public string error_file = "";
  public bool use_cache = true;
  public int verbosity = 1;
  public int max_threads = 1;
  public bool deterministic = false;

  public List<string> bindings_sources = new List<string>();
  public string bindings_dll = "";

  public List<string> postproc_sources = new List<string>();
  public string postproc_dll = "";

  string NormalizePath(string file_path)
  {
    if(Path.IsPathRooted(file_path))
      return Util.NormalizeFilePath(file_path);
    else if(!string.IsNullOrEmpty(proj_file) && !string.IsNullOrEmpty(file_path) && file_path[0] == '.')
      return Util.NormalizeFilePath(Path.Combine(Path.GetDirectoryName(proj_file), file_path));
    return file_path;
  }

  public void Setup()
  {
    for(int i=0;i<inc_dirs.Count;++i)
    {
      inc_dirs[i] = NormalizePath(inc_dirs[i]);
      inc_path.Add(inc_dirs[i]);
    }

    for(int i=0;i<src_dirs.Count;++i)
    {
      src_dirs[i] = NormalizePath(src_dirs[i]);
      if(inc_dirs.Count == 0)
        inc_path.Add(src_dirs[i]);
    }

    for(int i=0;i<bindings_sources.Count;++i)
      bindings_sources[i] = NormalizePath(bindings_sources[i]);
    bindings_dll = NormalizePath(bindings_dll);

    for(int i=0;i<postproc_sources.Count;++i)
      postproc_sources[i] = NormalizePath(postproc_sources[i]);
    postproc_dll = NormalizePath(postproc_dll);

    result_file = NormalizePath(result_file);
    tmp_dir = NormalizePath(tmp_dir);
    error_file = NormalizePath(error_file);
  }

  public IUserBindings LoadBindings()
  {
    if(string.IsNullOrEmpty(bindings_dll))
      return new EmptyUserBindings();

    var userbindings_assembly = System.Reflection.Assembly.LoadFrom(bindings_dll);
    var userbindings_class = userbindings_assembly.GetTypes()[0];
    var bindings = System.Activator.CreateInstance(userbindings_class) as IUserBindings;
    return bindings;
  }

  public IFrontPostProcessor LoadPostprocessor()
  {
    if(string.IsNullOrEmpty(postproc_dll))
      return new EmptyPostProcessor();

    var postproc_assembly = System.Reflection.Assembly.LoadFrom(postproc_dll);
    var postproc_class = postproc_assembly.GetTypes()[0];
    var postproc = System.Activator.CreateInstance(postproc_class) as IFrontPostProcessor;
    return postproc;
  }
}

public class CompileConf
{
  public ProjectConf proj;
  public Logger logger;
  public Types ts;
  public string args = ""; 
  public List<string> files = new List<string>();
  public string self_file = "";
  public IFrontPostProcessor postproc = new EmptyPostProcessor();
  public IUserBindings bindings = new EmptyUserBindings();
}
 
public class CompilationExecutor
{
  const byte COMPILE_FMT = 2;
  const uint FILE_VERSION = 1;

  public struct InterimResult
  {
    public ModulePath module_path;
    public string compiled_file;
    public FileImports imports_maybe;
    public ANTLR_Parsed parsed;
    //loaded from cache compiled result
    public Module compiled;
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
      conf.logger.Error(e.Message + " " + e.StackTrace);
      errors.Add(new BuildError("?", e));
    }

    sw.Stop();

    conf.logger.Log(1, $"BHL build done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");

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

    if(!CheckModuleNamesCollision(conf, errors))
      return;

    if(conf.proj.deterministic)
      conf.files.Sort();

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
      conf.logger.Log(1, "BHL no stale files detected");
      return;
    }

    if(conf.ts == null)
      conf.ts = new Types();

    conf.bindings.Register(conf.ts);

    sw = Stopwatch.StartNew();
    //1. start parse workers
    var parse_workers = StartParseWorkers(conf);

    //2. wait for the completion of parse workers
    foreach(var pw in parse_workers)
      pw.Join();

    sw.Stop();
    conf.logger.Log(2, $"BHL parse done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
    
    foreach(var pw in parse_workers)
      errors.AddRange(pw.errors);

    //2.1 let's collect all interim results collected in parse workers
    var file2interim = new Dictionary<string, InterimResult>();

    //3 let's create the processed bundle containing already compiled cached modules
    //  and the newly processed ones
    var proc_bundle = new ANTLR_Processor.ProcessedBundle(conf.proj.inc_path);

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
            kv.Value.imports_maybe, 
            kv.Value.parsed, 
            conf.ts, 
            proc_errs,
            ErrorHandlers.MakeStandard(kv.Key, proc_errs),
            out var preproc_parsed
          );
          proc_bundle.file2proc.Add(kv.Key, proc);
        }
        else
          proc_bundle.file2cached.Add(kv.Key, kv.Value.compiled);

        file2interim.Add(kv.Key, kv.Value);
      }
    }
    sw.Stop();
    conf.logger.Log(2, $"BHL proc make done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");

    sw = Stopwatch.StartNew();
    //4. wait for ANTLR processors execution
    //TODO: it's not multithreaded yet
    ANTLR_Processor.ProcessAll(proc_bundle);
    sw.Stop();
    conf.logger.Log(2, $"BHL proc all done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");

    foreach(var kv in proc_bundle.file2proc)
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
      proc_bundle.file2proc
    );
    sw.Stop();
    conf.logger.Log(2, $"BHL compile done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");

    foreach(var cw in compiler_workers)
      errors.AddRange(cw.errors);
    if(errors.Count > 0)
      return;

    var tmp_res_file = conf.proj.tmp_dir + "/" + Path.GetFileName(conf.proj.result_file) + ".tmp";

    sw = Stopwatch.StartNew();
    var check_err = CheckUniqueSymbols(compiler_workers);
    sw.Stop();
    conf.logger.Log(2, $"BHL check done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
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
    conf.logger.Log(2, $"BHL postproc done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
  }

  static bool CheckModuleNamesCollision(CompileConf conf, CompileErrors errors)
  {
    var module2file = new Dictionary<string, string>();
    bool has_collision = false;

    foreach(var file in conf.files)
    {
      string module = conf.proj.inc_path.FilePath2ModuleName(file, normalized: true);
      if(module2file.TryGetValue(module, out var existing_file))
      {
        errors.Add(new BuildError(file, $"module '{module}' ambiguous resolving: '{existing_file}' and '{file}'"));
        has_collision = true;
      }
      else
        module2file.Add(module, file);
    }
    return !has_collision;
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
      if(!conflict.Ok && !conflict.other.IsLocal() && !conflict.local.IsLocal())
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
            var imports_maybe = w.GetImports(file, sfs);
            var deps = new List<string>(imports_maybe.file_paths);
            deps.Add(file);

            //NOTE: adding self binary as a dep
            if(self_bin_file.Length > 0)
              deps.Add(self_bin_file);

            var compiled_file = GetCompiledCacheFile(w.conf.proj.tmp_dir, file);

            var interim = new InterimResult();
            interim.module_path = new ModulePath(w.conf.proj.inc_path.FilePath2ModuleName(file), file);
            interim.imports_maybe = imports_maybe;
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
              var err_handlers = ErrorHandlers.MakeStandard(file, w.errors);
              var parser = ANTLR_Processor.Stream2Parser(
                new Module(w.conf.ts, interim.module_path), 
                w.errors,
                err_handlers,
                sfs, 
                defines: new HashSet<string>(w.conf.proj.defines),
                preproc_parsed: out var preproc_parsed
              );
              //NOTE: parsing happens here 
              interim.parsed = new ANTLR_Parsed(parser, parser.program());

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
          w.conf.logger.Error(e.Message + " " + e.StackTrace);
          w.errors.Add(new BuildError(w.conf.files[i], e));
        }
      }

      sw.Stop();
      w.conf.logger.Log(1, $"BHL parser {w.id} done(hit/miss/err:{w.cache_hits}/{w.cache_miss}/{w.cache_errs}, {Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
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
      var cache_imports_file = GetImportsCacheFile(conf.proj.tmp_dir, file);
      Marshall.Obj2File(imports, cache_imports_file);
    }

    //TODO: this one doesn't take into account commented imports!
    //      use ANTLR lightweight parser for that?...
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
            else
              break;
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
            w.file2ns.Add(current_file, interim.compiled.ns);
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
          w.conf.logger.Error(e.Message + " " + e.StackTrace);
          w.errors.Add(new BuildError(current_file, e));
        }
      }

      sw.Stop();
      w.conf.logger.Log(1, $"BHL compiler {w.id} done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
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
