using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using bhl.marshall;

namespace bhl;

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

  public int cache_hits { get; private set; }
  public int cache_miss { get; private set; }
  public int cache_errs { get; private set; }

  public async Task<CompileErrors> Exec(CompileConf conf)
  {
    var sw = Stopwatch.StartNew();

    var errors = new CompileErrors();

    try
    {
      await _Exec(conf, errors);
    }
    catch(Exception e)
    {
      errors.Add(new BuildError("?", e));
    }

    sw.Stop();

    conf.logger.Log(1,
      $"BHL all done(hits/miss/errs: {cache_hits}/{cache_miss}/{errors.Count}) ({Math.Round(sw.ElapsedMilliseconds / 1000.0f, 2)} sec)");

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

  async Task _Exec(CompileConf conf, CompileErrors errors)
  {
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
       !BuildUtils.NeedToRegen(conf.proj.result_file, conf.files)
      )
    {
      conf.logger.Log(1, "BHL no stale files detected");
      return;
    }

    if(conf.ts == null)
      conf.ts = new Types();

    conf.bindings.Register(conf.ts);

    var pipeline = new Pipeline<CompileConf, List<ProcAndCompileWorker>>(conf.logger)
        .Transform<CompileConf, List<ParseWorker>>(
          "BHL parse init",
          MakeParseWorkers
        )
        .Parallel<ParseWorker, ParseWorker>(
          "BHL parse (workers: %workers%)",
          async (worker, token) =>
          {
            await Task.Run(worker.Parse, token);
            return worker;
          })
        .Transform<List<ParseWorker>, ProjectCompilationStateBundle>(
          "BHL parse finalize",
          (workers) =>
          {
            ProcessParseWorkers(workers, errors);
            return MakeStateBundle(conf, workers, errors);
          })
        .Transform<ProjectCompilationStateBundle, List<ProcAndCompileWorker>>(
          "BHL parsed -> AST",
          (bundle) =>
          {
            //TODO: can it be made parallel?
            ANTLR_Processor.ProcessAll(bundle);
            //NOTE: let's add processors errors to the all errors but continue execution
            foreach(var kv in bundle.file2proc)
              errors.AddRange(kv.Value.result.errors);

            return MakeCompilerWorkers(conf, bundle);
          }
        )
        .Parallel<ProcAndCompileWorker, ProcAndCompileWorker>(
          "BHL compile AST (workers: %workers%)",
          async (worker, token) =>
          {
            await Task.Run(worker.Phase1_ProcessAST, token);
            return worker;
          })
        .Transform<List<ProcAndCompileWorker>, List<ProcAndCompileWorker>>(
          "BHL compile patch",
          (workers) =>
          {
            Patch(workers);
            return workers;
          })
        .Parallel<ProcAndCompileWorker, ProcAndCompileWorker>(
          "BHL compile write (workers: %workers%)",
          async (worker, token) =>
          {
            await Task.Run(worker.Phase2_WriteByteCode, token);
            return worker;
          })
        .Transform<List<ProcAndCompileWorker>, List<ProcAndCompileWorker>>(
          "BHL compile finalize",
          (workers) =>
          {
            foreach(var w in workers)
              errors.AddRange(w.errors);

            if(errors.Count > 0)
              throw new TooManyErrorsException();

            var check_err = CheckUniqueSymbols(workers);
            if(check_err != null)
            {
              errors.Add(check_err);
              throw new TooManyErrorsException();
            }

            return workers;
          })
        .Transform<List<ProcAndCompileWorker>, List<ProcAndCompileWorker>>(
          "BHL write to file",
          (workers) =>
          {
            string tmp_res_file = conf.proj.tmp_dir + "/" + Path.GetFileName(conf.proj.result_file) + ".tmp";

            WriteCompilationResultToFile(conf, workers, tmp_res_file);

            if(File.Exists(conf.proj.result_file))
              File.Delete(conf.proj.result_file);
            File.Move(tmp_res_file, conf.proj.result_file);
            return workers;
          })
        .Transform<List<ProcAndCompileWorker>, List<ProcAndCompileWorker>>(
          "BHL postproc finalize",
          (workers) =>
          {
            conf.postproc.Tally();
            return workers;
          })
      ;

    try
    {
      await pipeline.RunAsync(conf, default);
    }
    catch (TooManyErrorsException)
    {
    }
  }

  public class TooManyErrorsException : Exception
  {
  }

  static ANTLR_Processor ParseIfNeededAndMakeProcessor(
    CompileConf conf,
    string file,
    ProjectCompilationStateBundle.InterimParseResult interim)
  {
    var file_module = new Module(
      conf.ts,
      conf.proj.inc_path.FilePath2ModuleName(file),
      file
    );

    var err_hub = CompileErrorsHub.MakeStandard(file);

    var parsed = interim.parsed;
    if(parsed == null)
      parsed = ANTLR_Processor.Parse(
        file_module,
        err_hub,
        new HashSet<string>(conf.proj.defines),
        out var _
      );

    var proc = new ANTLR_Processor(
      parsed,
      file_module,
      interim.imports_maybe,
      conf.ts,
      err_hub.errors
    );
    return proc;
  }

  ProjectCompilationStateBundle MakeStateBundle(
    CompileConf conf,
    List<ParseWorker> parse_workers,
    CompileErrors errors
  )
  {
    var proc_bundle = new ProjectCompilationStateBundle(conf.ts);
    proc_bundle.parse_workers = parse_workers;

    //1. let's merge all interim results
    foreach(var pw in parse_workers)
    {
      foreach(var kv in pw.file2interim)
        proc_bundle.file2parsed.Add(kv.Key, kv.Value);
    }

    //2. let's create collections for files to be processed and used from cache
    foreach(var kv in proc_bundle.file2parsed)
    {
      if(kv.Value.cached == null &&
         //NOTE: no need to process a file if it contains parsing errors
         !errors.FileHasAnyErrors(kv.Key))
      {
        proc_bundle.file2proc.Add(kv.Key, ParseIfNeededAndMakeProcessor(conf, kv.Key, kv.Value));
      }
      else if(kv.Value.cached != null)
      {
        if(ValidateParseCache(proc_bundle, kv.Value))
        {
          proc_bundle.file2cached.Add(kv.Key, kv.Value.cached);
        }
        else
        {
          var proc = ParseIfNeededAndMakeProcessor(conf, kv.Key, kv.Value);
          proc_bundle.file2proc.Add(kv.Key, proc);

          kv.Value.parsed = proc.parsed;
          kv.Value.cached = null;

          cache_hits--;
          cache_miss++;
        }
      }
    }

    return proc_bundle;
  }

  bool ValidateParseCache(ProjectCompilationStateBundle proc_bundle,
    ProjectCompilationStateBundle.InterimParseResult interim)
  {
    return true;
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

  static List<ParseWorker> MakeParseWorkers(CompileConf conf)
  {
    var parse_workers = new List<ParseWorker>();

    int files_per_worker = conf.files.Count < conf.proj.max_threads
      ? conf.files.Count
      : (int)Math.Ceiling((float)conf.files.Count / (float)conf.proj.max_threads);

    int idx = 0;
    int wid = 0;

    while(idx < conf.files.Count)
    {
      int count = (idx + files_per_worker) > conf.files.Count ? (conf.files.Count - idx) : files_per_worker;

      var pw = new ParseWorker();
      pw.conf = conf;
      pw.id = ++wid;
      pw.start = idx;
      pw.count = count;

      parse_workers.Add(pw);

      idx += count;
    }

    return parse_workers;
  }

  List<ParseWorker> ProcessParseWorkers(List<ParseWorker> workers, CompileErrors errors)
  {
    foreach(var pw in workers)
    {
      cache_hits += pw.cache_hits;
      cache_miss += pw.cache_miss;
      cache_errs += pw.cache_errs;

      errors.AddRange(pw.errors);
    }

    return workers;
  }

  static List<ParseWorker> StartParseWorkers(CompileConf conf)
  {
    var parse_workers = new List<ParseWorker>();

    int files_per_worker = conf.files.Count < conf.proj.max_threads
      ? conf.files.Count
      : (int)Math.Ceiling((float)conf.files.Count / (float)conf.proj.max_threads);

    int idx = 0;
    int wid = 0;

    while(idx < conf.files.Count)
    {
      int count = (idx + files_per_worker) > conf.files.Count ? (conf.files.Count - idx) : files_per_worker;

      var pw = new ParseWorker();
      pw.conf = conf;
      pw.id = ++wid;
      pw.start = idx;
      pw.count = count;

      parse_workers.Add(pw);

      idx += count;
    }

    return parse_workers;
  }

  static List<ProcAndCompileWorker> MakeCompilerWorkers(
    CompileConf conf,
    ProjectCompilationStateBundle proc_bundle
  )
  {
    var compiler_workers = new List<ProcAndCompileWorker>();

    foreach(var pw in proc_bundle.parse_workers)
    {
      var cw = new ProcAndCompileWorker();
      cw.conf = pw.conf;
      cw.id = pw.id;
      cw.file2interim = proc_bundle.file2parsed;
      cw.file2proc = proc_bundle.file2proc;
      cw.ts = conf.ts;
      cw.start = pw.start;
      cw.count = pw.count;
      cw.postproc = conf.postproc;

      compiler_workers.Add(cw);
    }

    return compiler_workers;
  }

  static void Patch(List<ProcAndCompileWorker> compiler_workers)
  {
    foreach(var w in compiler_workers)
    {
      var failed_files = new List<string>();

      foreach(var kv in w.file2compiler)
      {
        try
        {
          kv.Value.Compile_PatchInstructions();
        }
        catch(Exception e)
        {
          failed_files.Add(kv.Key);

          if(e is ICompileError ie)
            w.errors.Add(ie);
          else
          {
            w.errors.Add(new BuildError(kv.Key, e));
          }
        }
      }

      //NOTE: let's remove failed compilers from further processing
      foreach(var failed_file in failed_files)
        w.file2compiler.Remove(failed_file);
    }
  }

  SymbolError CheckUniqueSymbols(List<ProcAndCompileWorker> compiler_workers)
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

  void WriteCompilationResultToFile(CompileConf conf, List<ProcAndCompileWorker> compiler_workers, string file_path)
  {
    using(FileStream dfs = new FileStream(file_path, FileMode.Create, System.IO.FileAccess.Write))
    {
      var mwriter = new MsgPack.MsgPackWriter(dfs);

      mwriter.Write(ModuleLoader.COMPILE_FMT);
      mwriter.Write(FILE_VERSION);

      int total_modules = conf.files.Count;
      mwriter.Write(total_modules);

      //NOTE: we'd like to write file binary modules in the same order they were added
      for(int file_idx = 0; file_idx < conf.files.Count; ++file_idx)
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
    bool changed = !File.Exists(tmp_args_file) ||
                   (File.Exists(tmp_args_file) && File.ReadAllText(tmp_args_file) != conf.args);
    if(changed)
      File.WriteAllText(tmp_args_file, conf.args);
    return changed;
  }

  static byte[] EncodeToLZ4(byte[] bytes)
  {
    var lz4_bytes = LZ4ps.LZ4Codec.Encode64(bytes, 0, bytes.Length);
    return lz4_bytes;
  }

  SymbolError CheckUniqueSymbols(Namespace ns, ProcAndCompileWorker w)
  {
    foreach(var kv in w.file2module)
    {
      var file_ns = kv.Value.ns.UnlinkAll();

      var conflict = ns.TryLink(file_ns);
      if(!conflict.Ok && !conflict.other.IsLocal() && !conflict.local.IsLocal())
        return new SymbolError(conflict.local,
          "symbol '" + conflict.other.GetFullTypePath() + "' is already declared in module '" +
          (conflict.other.scope as Namespace)?.module.name + "'");
    }

    return null;
  }

  public class ParseWorker
  {
    public CompileConf conf;
    public int id;
    public int start;
    public int count;

    public Dictionary<string, ProjectCompilationStateBundle.InterimParseResult> file2interim =
      new Dictionary<string, ProjectCompilationStateBundle.InterimParseResult>();

    public CompileErrors errors = new CompileErrors();
    public int cache_hits;
    public int cache_miss;
    public int cache_errs;
    string current_file;

    public void Parse()
    {
      try
      {
        for(int i = start; i < (start + count); ++i)
          Parse_At(i);
      }
      catch(Exception e)
      {
        if(e is ICompileError ie)
          errors.Add(ie);
        else
          errors.Add(new BuildError(current_file, e));
      }

      conf.logger.Log(2, $"BHL parser {id} done(hit/miss/err:{cache_hits}/{cache_miss}/{cache_errs})");
    }

    void Parse_At(int i)
    {
      current_file = conf.files[i];

      using(var sfs = File.OpenRead(current_file))
      {
        var imports_maybe = GetMaybeImports(current_file, sfs);
        var deps = new List<string>(imports_maybe.file_paths);
        deps.Add(current_file);

        //NOTE: adding self binary as a dep
        if(conf.self_file.Length > 0)
          deps.Add(conf.self_file);

        var compiled_file = GetCompiledCacheFile(conf.proj.tmp_dir, current_file);

        var interim = new ProjectCompilationStateBundle.InterimParseResult();
        interim.module_path = new ModulePath(conf.proj.inc_path.FilePath2ModuleName(current_file), current_file);
        interim.imports_maybe = imports_maybe;
        interim.compiled_file = compiled_file;

        bool use_cache = conf.proj.use_cache && !BuildUtils.NeedToRegen(compiled_file, deps);

        if(use_cache)
        {
          try
          {
            interim.cached = CompiledModule.FromFile(compiled_file, conf.ts);
            ++cache_hits;
          }
          catch(Exception)
          {
            use_cache = false;
            ++cache_errs;
          }
        }

        if(!use_cache)
        {
          //for parsing time debug
          //var sw = Stopwatch.StartNew();

          interim.parsed = ANTLR_Processor.Parse(
            new Module(conf.ts, interim.module_path),
            sfs,
            CompileErrorsHub.MakeStandard(current_file, errors),
            defines: new HashSet<string>(conf.proj.defines),
            preproc_parsed: out var _
          );

          //sw.Stop();
          //conf.logger.Log(0, $"BHL parse file done {current_file} ({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
          ++cache_miss;
        }

        file2interim[current_file] = interim;
      }
    }

    FileImports GetMaybeImports(string file, FileStream fsf)
    {
      var imports = TryReadImportsCache(file);
      if(imports == null)
      {
        imports = ParseMaybeImports(conf.proj.inc_path, file, fsf);
        WriteImportsCache(file, imports);
      }

      return imports;
    }

    FileImports TryReadImportsCache(string file)
    {
      var cache_imports_file = GetImportsCacheFile(conf.proj.tmp_dir, file);

      if(BuildUtils.NeedToRegen(cache_imports_file, file))
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
    public static FileImports ParseMaybeImports(IncludePath inc_path, string file, Stream stream)
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

  public class ProcAndCompileWorker
  {
    public CompileConf conf;
    public int id;
    public Types ts;
    public int start;
    public int count;
    public IFrontPostProcessor postproc;
    public CompileErrors errors = new CompileErrors();

    public Dictionary<string, ProjectCompilationStateBundle.InterimParseResult> file2interim =
      new Dictionary<string, ProjectCompilationStateBundle.InterimParseResult>();

    public Dictionary<string, ANTLR_Processor> file2proc = new Dictionary<string, ANTLR_Processor>();
    public Dictionary<string, ModuleCompiler> file2compiler = new Dictionary<string, ModuleCompiler>();
    public Dictionary<string, Module> file2module = new Dictionary<string, Module>();
    string current_file;

    public void Phase1_ProcessAST()
    {
      try
      {
        for(int i = start; i < (start + count); ++i)
          ProcessAST_At(i);
      }
      catch(Exception e)
      {
        if(e is ICompileError ie)
          errors.Add(ie);
        else
          errors.Add(new BuildError(current_file, e));
      }
    }

    public void Phase2_WriteByteCode()
    {
      try
      {
        for(int i = start; i < (start + count); ++i)
          WriteByteCode_At(i);
      }
      catch(Exception e)
      {
        if(e is ICompileError ie)
          errors.Add(ie);
        else
          errors.Add(new BuildError(current_file, e));
      }
    }

    void ProcessAST_At(int i)
    {
      current_file = conf.files[i];

      var interim = file2interim[current_file];

      if(interim.cached == null)
      {
        //NOTE: add ModuleCompiler only if there were no errors in corresponding processor
        if(file2proc.TryGetValue(current_file, out var proc) &&
           !HasAnyRelatedErrors(proc))
        {
          var proc_result = postproc.Patch(proc.result, current_file);
          errors.AddRange(proc_result.errors);

          var c = new ModuleCompiler(proc_result);
          file2compiler.Add(current_file, c);
          c.Compile_VisitAST();
        }
      }
    }

    void WriteByteCode_At(int i)
    {
      current_file = conf.files[i];

      var interim = file2interim[current_file];

      if(interim.cached != null)
      {
        file2module.Add(current_file, interim.cached);
      }
      else
      {
        //NOTE: in case of parse/process errors compiler won't be present,
        //      we should check for this situation
        if(file2compiler.TryGetValue(current_file, out var c))
        {
          var cm = c.Compile_Finish();

          CompiledModule.ToFile(cm, interim.compiled_file);
          file2module.Add(current_file, cm);
        }
      }
    }

    bool HasAnyRelatedErrors(ANTLR_Processor proc, HashSet<ANTLR_Processor> seen = null)
    {
      if(seen == null)
        seen = new HashSet<ANTLR_Processor>();
      seen.Add(proc);

      //let's check processor related errors
      if(proc.result.errors.Count > 0)
        return true;

      foreach(var proc_import in proc.imports)
      {
        //for C# modules file_path is empty
        if(string.IsNullOrEmpty(proc_import.file_path))
          continue;

        //if there's no such a processor for file then file is already compiled
        if(!file2proc.TryGetValue(proc_import.file_path, out var import_proc))
          continue;

        if(seen.Contains(import_proc))
          continue;

        if(HasAnyRelatedErrors(import_proc, seen))
          return true;
      }

      return false;
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
