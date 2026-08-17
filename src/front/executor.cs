#if (BHL_PARSER || UNITY_EDITOR)

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using bhl.marshall;


namespace bhl
{

public class CompileConf
{
  public ProjectConf proj;
  public Logger logger;
  public Types ts;
  public string args_signature = "";
  public List<string> files = new List<string>();
  public List<string> global_file_deps = new List<string>();
  public string self_file = "";
  public IUserBindings bindings = new EmptyUserBindings();
  public IFrontPostProcessor postproc = new EmptyPostProcessor();
  public int max_errors_num = 100;
  public bool add_debug_info = true;
  //NOTE: see ModuleCompiler.indirect_calls - not yet part of the cache signature (CheckDebugInfoSignatureFile),
  //      so toggling it with use_cache=true and no source changes can serve stale bytecode.
  public bool indirect_calls;

  //NOTE: populated internally at the start of Exec(); a single consolidated
  //      cache file (instead of two files per source file) to avoid Windows'
  //      per-file I/O overhead (AV scanning, NTFS metadata churn) on projects
  //      with many source files
  public CompileCacheBlob cache_blob;
  public long run_ticks;

  //NOTE: populated internally at the start of Exec(); true if add_debug_info changed
  //      since the last run - it's baked into compiled_bytes with no mtime dep of its
  //      own, so a change must bust every file's compiled cache entry this run
  public bool debug_info_changed;
}

public class CompilationResult
{
  public CompileErrors errors;
  public CompileWarnings warnings;

  public CompilationResult(CompileErrors errors, CompileWarnings warnings)
  {
    this.errors = errors;
    this.warnings = warnings;
  }
}

public class CompilationExecutor
{
  const uint FILE_VERSION = 1;
  const int MAX_THREADS = 6;

  public int cache_hits { get; private set; }
  public int cache_miss { get; private set; }
  public int cache_errs { get; private set; }

  List<(string file, long write_ticks, byte[] bytes)> pending_maybe_imports_entries =
    new List<(string file, long write_ticks, byte[] bytes)>();

  //NOTE: compiles all files *but loads the module from the file at index 0*,
  //      returns null in case of compilation errors
  public static async Task<VM> CompileAndLoadVM(
    List<string> files,
    bool use_cache = false,
    string bytecode_result_file = null,
    bool add_debug_info = false,
    string tmp_dir = null,
    IUserBindings bindings = null,
    IFrontPostProcessor postproc = null,
    int verbosity = -1
  )
  {
    var proj = new ProjectConf();
    proj.module_fmt = ModuleBinaryFormat.FMT_BIN;
    proj.use_cache = use_cache;
    proj.max_threads = files.Count == 1 ? 1 : MAX_THREADS;
    foreach(var file in files)
    {
      var dir = Path.GetDirectoryName(file);
      if(proj.src_dirs.IndexOf(dir) == -1)
        proj.src_dirs.Add(dir);
    }
    proj.result_file = bytecode_result_file ?? Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".bhc");
    //NOTE: falls back to the OS temp dir for callers with no project context of their
    //      own (e.g. standalone 'bhl run <script.bhl>'); callers compiling on behalf of
    //      a bhl.proj should pass its tmp_dir so per-file/import caches actually persist
    //      across invocations instead of living in an OS-swept temp directory
    proj.tmp_dir = string.IsNullOrEmpty(tmp_dir) ? Path.GetTempPath() : tmp_dir;
    proj.verbosity = 0;
    proj.Setup();

    var conf = new CompileConf();
    conf.logger = new Logger(verbosity, new ConsoleLogger());
    conf.proj = proj;
    conf.ts = new Types();
    conf.self_file = BuildUtils.GetSelfFile();
    conf.files = BuildUtils.NormalizeFilePaths(files);
    conf.bindings = bindings ?? new EmptyUserBindings();
    conf.postproc = postproc ?? new EmptyPostProcessor();
    conf.add_debug_info = add_debug_info;

    var cmp = new CompilationExecutor();
    var result = await cmp.Exec(conf);

    foreach(var warn in result.warnings)
      ErrorUtils.OutputWarning(warn.file, warn.range.start.line, warn.range.start.column, warn.text);

    if(result.errors.Count > 0)
    {
      foreach(var err in result.errors)
        ErrorUtils.OutputError(err.file, err.range.start.line, err.range.start.column, err.text);
      return null;
    }

    var bytes = new MemoryStream(File.ReadAllBytes(conf.proj.result_file));
    var vm = new VM(conf.ts, new ModuleLoader(conf.ts, bytes));

    vm.LoadModule(Path.GetFileNameWithoutExtension(files[0]));

    return vm;
  }

  public async Task<CompilationResult> Exec(CompileConf conf)
  {
    var sw = Stopwatch.StartNew();

    var errors = new CompileErrors();
    var warnings = new CompileWarnings();

    try
    {
      await _Exec(conf, errors, warnings);
    }
    catch(Exception e)
    {
      errors.Add(new BuildError("?", e));
    }

    sw.Stop();

    long result_size = File.Exists(conf.proj.result_file) ? new FileInfo(conf.proj.result_file).Length : 0;
    conf.logger.Log(1,
      $"BHL all done(hits/miss/errs/warns: {cache_hits}/{cache_miss}/{errors.Count}/{warnings.Count}) size: {result_size} bytes ({Math.Round(sw.ElapsedMilliseconds / 1000.0f, 2)} sec)");

    if(errors.Count > 0)
    {
      if(errors.Count > conf.max_errors_num)
      {
        int total_errors = errors.Count;
        errors.RemoveRange(conf.max_errors_num, errors.Count - conf.max_errors_num);
        errors.Add(new BuildError(errors[errors.Count - 1].file, "Too many errors (" + total_errors + "), showing only top first"));
      }

      if(!string.IsNullOrEmpty(conf.proj.error_file))
      {
        string err_str = "";
        for(int i = 0; i < errors.Count; ++i)
        {
          var err = errors[i];
          err_str += ErrorUtils.ToJson(err) + "\n";
        }
        err_str = err_str.Trim();

        if(conf.proj.error_file == "-")
          Console.Error.WriteLine(err_str);
        else
          File.WriteAllText(conf.proj.error_file, err_str);
      }
    }

    return new CompilationResult(errors, warnings);
  }

  async Task _Exec(CompileConf conf, CompileErrors errors, CompileWarnings warnings)
  {
    if(!CheckModuleNamesCollision(conf, errors))
      return;

    if(conf.proj.deterministic)
      conf.files.Sort();

    if(!string.IsNullOrEmpty(conf.proj.error_file))
      BuildUtils.Rm(conf.proj.error_file);

    var res_dir = Path.GetDirectoryName(conf.proj.result_file);
    if(res_dir.Length > 0)
      Directory.CreateDirectory(res_dir);

    Directory.CreateDirectory(conf.proj.tmp_dir);

    var args_changed = CheckArgsSignatureFile(conf);
    conf.debug_info_changed = CheckDebugInfoSignatureFile(conf);

    //NOTE: bhl.proj/self binary can also silently change compiled content
    var global_deps = new List<string>(conf.global_file_deps);
    if(!string.IsNullOrEmpty(conf.proj.proj_file))
      global_deps.Add(conf.proj.proj_file);
    if(!string.IsNullOrEmpty(conf.self_file))
      global_deps.Add(conf.self_file);

    if(conf.proj.use_cache &&
       !args_changed &&
       !conf.debug_info_changed &&
       !BuildUtils.NeedToRegen(conf.proj.result_file, conf.files) &&
       !BuildUtils.NeedToRegen(conf.proj.result_file, global_deps)
      )
    {
      conf.logger.Log(1, "BHL no stale files detected");
      return;
    }

    //NOTE: loaded regardless of use_cache so that a run with caching disabled
    //      (e.g. a forced rebuild) doesn't wipe out entries a later cached run
    //      could still reuse; use_cache only gates whether we *read* from it
    conf.cache_blob = CompileCacheBlob.Load(GetCacheBlobFile(conf.proj.tmp_dir));
    conf.run_ticks = DateTime.Now.Ticks;
    pending_maybe_imports_entries.Clear();

    if(conf.ts == null)
      conf.ts = new Types();

    var pipeline = new Pipeline<CompileConf, List<ProcAndCompileWorker>>(conf.logger)
        .Transform<CompileConf, CompileConf>(
          "BHL register bindings",
          (conf) => {
            conf.bindings.Register(conf.ts);
            return conf;
          }
        )
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
            //NOTE: let's add processors errors/warnings to the all errors but continue execution
            foreach(var kv in bundle.file2proc)
            {
              errors.AddRange(kv.Value.result.errors);
              warnings.AddRange(kv.Value.result.warnings);
            }

            if(errors.Count > conf.max_errors_num)
              throw new TooManyErrorsException();

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
          "BHL cache blob write",
          (workers) =>
          {
            WriteCacheBlob(conf, workers);
            return workers;
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

            BuildUtils.Rm(conf.proj.result_file);
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
    var file_module = new ModuleDeclared(
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

      pending_maybe_imports_entries.AddRange(pw.maybe_imports_cache_entries);

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

      if(conf.proj.module_fmt == ModuleBinaryFormat.FMT_LZ4_CHUNKED)
      {
#if BHL_LZ4
        WriteChunkedModules(conf, compiler_workers, mwriter);
        WriteRequiredBindings(conf, mwriter);
        return;
#else
        throw new Exception("Unsupported format: " + conf.proj.module_fmt);
#endif
      }

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
            var interim = cw.file2interim[file];
            var path = interim.module_path;

            mwriter.Write((byte)conf.proj.module_fmt);
            mwriter.Write(path.name);

            if(conf.proj.module_fmt == ModuleBinaryFormat.FMT_BIN)
              mwriter.Write(interim.compiled_bytes);
#if BHL_LZ4
            else if(conf.proj.module_fmt == ModuleBinaryFormat.FMT_LZ4)
              mwriter.Write(EncodeToLZ4(interim.compiled_bytes));
#endif
            else
              throw new Exception("Unsupported format: " + conf.proj.module_fmt);

            break;
          }
        }
      }

      WriteRequiredBindings(conf, mwriter);
    }
  }

  //NOTE: trailing/optional, appended after all module entries - old readers (which stop
  //      once they've read the declared entry count) simply never reach these bytes, so
  //      no COMPILE_FMT/FILE_VERSION bump is needed
  static void WriteRequiredBindings(CompileConf conf, MsgPack.MsgPackWriter mwriter)
  {
    var info = (conf.bindings as UserBindingsWithInfo)?.info;

    mwriter.Write(info?.Count ?? 0);
    if(info == null)
      return;

    foreach(var rb in info)
    {
      mwriter.Write(rb.name);
      mwriter.Write(rb.version);
    }
  }

#if BHL_LZ4
  //NOTE: modules are grouped into chunks (never splitting a single module
  //      across two chunks) and each chunk is LZ4-compressed as a whole,
  //      since compressing many small modules together compacts noticeably
  //      better than LZ4-ing each one individually
  void WriteChunkedModules(CompileConf conf, List<ProcAndCompileWorker> compiler_workers, MsgPack.MsgPackWriter mwriter)
  {
    var module_names = new string[conf.files.Count];
    var module_bytes = new byte[conf.files.Count][];

    //NOTE: chunk boundaries depend on the cumulative size across the whole
    //      file list, not on any single worker's own slice, so we need
    //      every module's bytes gathered up-front before we can chunk them
    for(int file_idx = 0; file_idx < conf.files.Count; ++file_idx)
    {
      var file = conf.files[file_idx];

      foreach(var cw in compiler_workers)
      {
        if(file_idx >= cw.start && file_idx < cw.start + cw.count)
        {
          var interim = cw.file2interim[file];
          module_names[file_idx] = interim.module_path.name;
          module_bytes[file_idx] = interim.compiled_bytes;
          break;
        }
      }
    }

    var locators = new (int chunk_index, int offset, int length)[module_bytes.Length];
    var chunks = new List<byte[]>();

    var chunk_buffer = new MemoryStream();
    for(int i = 0; i < module_bytes.Length; ++i)
    {
      var bytes = module_bytes[i];
      locators[i] = (chunks.Count, (int)chunk_buffer.Position, bytes.Length);
      chunk_buffer.Write(bytes, 0, bytes.Length);

      bool last = i == module_bytes.Length - 1;
      if(chunk_buffer.Position >= conf.proj.lz4_chunk_size || last)
      {
        chunks.Add(EncodeToLZ4(chunk_buffer.ToArray()));
        chunk_buffer = new MemoryStream();
      }
    }

    mwriter.Write(module_bytes.Length + chunks.Count);

    for(int i = 0; i < module_bytes.Length; ++i)
    {
      mwriter.Write((byte)ModuleBinaryFormat.FMT_LZ4_CHUNKED);
      mwriter.Write(module_names[i]);
      mwriter.Write(EncodeChunkLocator(locators[i]));
    }

    for(int i = 0; i < chunks.Count; ++i)
    {
      mwriter.Write((byte)ModuleBinaryFormat.FMT_LZ4_CHUNKED);
      mwriter.Write(ModuleLoader.CHUNK_ENTRY_NAME_PREFIX + i);
      mwriter.Write(chunks[i]);
    }
  }

  static byte[] EncodeChunkLocator((int chunk_index, int offset, int length) loc)
  {
    var bytes = new byte[12];
    Buffer.BlockCopy(BitConverter.GetBytes(loc.chunk_index), 0, bytes, 0, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(loc.offset), 0, bytes, 4, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(loc.length), 0, bytes, 8, 4);
    return bytes;
  }
#endif

  void WriteCacheBlob(CompileConf conf, List<ProcAndCompileWorker> workers)
  {
    var maybe_imports_fresh = new Dictionary<string, (long ticks, byte[] bytes)>();
    foreach(var e in pending_maybe_imports_entries)
      maybe_imports_fresh[e.file] = (e.write_ticks, e.bytes);

    var compiled_fresh = new Dictionary<string, (long ticks, byte[] bytes)>();
    if(workers.Count > 0)
    {
      var file2interim = workers[0].file2interim;
      foreach(var file in conf.files)
      {
        if(file2interim.TryGetValue(file, out var interim) && interim.compiled_bytes != null)
          compiled_fresh[file] = (interim.compiled_write_ticks, interim.compiled_bytes);
      }
    }

    var writer = new CompileCacheBlob.Writer();

    //NOTE: for whichever files this run didn't (re)write, fall back to
    //      whatever was already in the blob rather than dropping it -
    //      e.g. use_cache=false only forces a fresh compile, it shouldn't
    //      also throw away a previously cached imports scan
    foreach(var file in conf.files)
    {
      if(maybe_imports_fresh.TryGetValue(file, out var fresh_maybe_imports))
        writer.AddMaybeImports(file, fresh_maybe_imports.ticks, fresh_maybe_imports.bytes);
      else if(conf.cache_blob.TryGetMaybeImports(file, out var old_bytes, out var old_ticks))
        writer.AddMaybeImports(file, old_ticks, old_bytes);

      if(compiled_fresh.TryGetValue(file, out var fresh_compiled))
        writer.AddCompiled(file, fresh_compiled.ticks, fresh_compiled.bytes);
      else if(conf.cache_blob.TryGetCompiled(file, out var old_cbytes, out var old_cticks))
        writer.AddCompiled(file, old_cticks, old_cbytes);
    }

    writer.Save(GetCacheBlobFile(conf.proj.tmp_dir));
  }

  public static string GetCacheBlobFile(string tmp_dir)
  {
    return tmp_dir + "/cache.blob";
  }

  static bool CheckArgsSignatureFile(CompileConf conf)
  {
    var tmp_args_file = conf.proj.tmp_dir + "/" + Path.GetFileName(conf.proj.result_file) + ".args";
    bool changed = !File.Exists(tmp_args_file) ||
                   (File.Exists(tmp_args_file) && File.ReadAllText(tmp_args_file) != conf.args_signature);
    if(changed)
      File.WriteAllText(tmp_args_file, conf.args_signature);
    return changed;
  }

  //NOTE: kept separate from args_signature so this doesn't force a full rebuild on
  //      unrelated CLI noise (-d, --error=, etc.)
  static bool CheckDebugInfoSignatureFile(CompileConf conf)
  {
    var tmp_file = conf.proj.tmp_dir + "/" + Path.GetFileName(conf.proj.result_file) + ".debuginfo";
    string signature = conf.add_debug_info.ToString();
    bool changed = !File.Exists(tmp_file) ||
                   (File.Exists(tmp_file) && File.ReadAllText(tmp_file) != signature);
    if(changed)
      File.WriteAllText(tmp_file, signature);
    return changed;
  }

#if BHL_LZ4
  static byte[] EncodeToLZ4(byte[] bytes)
  {
    var lz4_bytes = LZ4ps.LZ4Codec.Encode64(bytes, 0, bytes.Length);
    return lz4_bytes;
  }
#endif

  SymbolError CheckUniqueSymbols(Namespace ns, ProcAndCompileWorker w)
  {
    foreach(var kv in w.file2module)
    {
      var file_ns = kv.Value.ns.UnlinkAll();

      var conflict = ns.TryLink(file_ns);
      if(!conflict.Ok && !conflict.other.IsModuleLocal() && !conflict.local.IsModuleLocal())
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
    public List<(string file, long write_ticks, byte[] bytes)> maybe_imports_cache_entries =
      new List<(string file, long write_ticks, byte[] bytes)>();
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
        deps.AddRange(conf.global_file_deps);

        //NOTE: adding self binary as a dep
        if(conf.self_file.Length > 0)
          deps.Add(conf.self_file);

        var interim = new ProjectCompilationStateBundle.InterimParseResult();
        interim.module_path = new ModulePath(conf.proj.inc_path.FilePath2ModuleName(current_file), current_file);
        interim.imports_maybe = imports_maybe;

        bool use_cache;

        if(conf.proj.use_cache &&
           !conf.debug_info_changed &&
           conf.cache_blob.TryGetCompiled(current_file, out var cached_bytes, out var write_ticks) &&
           !CompileCacheBlob.IsStale(write_ticks, deps))
        {
          try
          {
            interim.cached = ModuleDeclared.FromStream(conf.ts, new MemoryStream(cached_bytes));
            interim.compiled_bytes = cached_bytes;
            interim.compiled_write_ticks = write_ticks;
            use_cache = true;
          }
          catch(Exception)
          {
            use_cache = false;
            ++cache_errs;
          }
        }
        else
        {
          use_cache = false;
        }

        if(use_cache)
        {
          ++cache_hits;
        }
        else
        {
          //for parsing time debug
          //var sw = Stopwatch.StartNew();

          interim.parsed = ANTLR_Processor.Parse(
            new ModuleDeclared(interim.module_path),
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
      if(conf.proj.use_cache &&
         conf.cache_blob.TryGetMaybeImports(file, out var cached_bytes, out var write_ticks) &&
         !CompileCacheBlob.IsStale(write_ticks, file))
      {
        try
        {
          var cached = Marshall.Stream2Obj<FileImports>(new MemoryStream(cached_bytes));
          maybe_imports_cache_entries.Add((file, write_ticks, cached_bytes));
          return cached;
        }
        catch
        {
          //NOTE: fall through and reparse below
        }
      }

      var imports = ParseMaybeImports(conf.proj.inc_path, file, fsf);

      if(conf.proj.use_cache)
      {
        byte[] bytes;
        using(var ms = new MemoryStream())
        {
          Marshall.Obj2Stream(imports, ms);
          bytes = ms.ToArray();
        }
        maybe_imports_cache_entries.Add((file, conf.run_ticks, bytes));
      }

      return imports;
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
              if(!string.IsNullOrEmpty(file_path))
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
    public Dictionary<string, ModuleDeclared> file2module = new Dictionary<string, ModuleDeclared>();
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
          c.add_debug_info = conf.add_debug_info;
          c.indirect_calls = conf.indirect_calls;
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
          var module = c.Compile_Finish();

          using(var ms = new MemoryStream())
          {
            module.ToStream(ms, leave_open: true);
            interim.compiled_bytes = ms.ToArray();
          }
          interim.compiled_write_ticks = conf.run_ticks;

          file2module.Add(current_file, module);
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
        //for C# (native) modules file_path is empty or not available
        var proc_import_fp = proc_import.file_path;
        if(string.IsNullOrEmpty(proc_import_fp))
          continue;

        //if there's no such a processor for file then file is already compiled
        if(!file2proc.TryGetValue(proc_import_fp, out var import_proc))
          continue;

        if(seen.Contains(import_proc))
          continue;

        if(HasAnyRelatedErrors(import_proc, seen))
          return true;
      }

      return false;
    }
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
}

#endif
