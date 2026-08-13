using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;


namespace bhl
{

public class BindingsEntryConf
{
  //NOTE: 1) if there are .bhl scripts they will be built into `dll` (if it's present)
  //      2) if there are .cs sources they will be built into `dll`
  public List<string> sources = new List<string>();

  //NOTE: 1) in case of .bhl bindings it's assumed to have a .bhc extension
  //      2) in case of .cs sources this can be a directory path as well containing an actual dll
  //         (e.g. bindings.dll/bindings.dll)
  public string dll = "";

  //NOTE: if true, `dll` is never auto-rebuilt from `sources` during a normal compile -
  //      only an explicit '--bindings-only' (or BHL_REBUILD) rebuilds it. Useful when
  //      `dll` is a prebuilt artifact committed to the repo: `sources` can still be listed
  //      for documentation/manual rebuilds without risking an unwanted rebuild of the
  //      committed dll (e.g. on a fresh checkout where tmp_dir's cache doesn't exist yet)
  public bool manual_build = false;
}

//NOTE: split from postproc-only fields/methods in src/front/proj_conf.postproc.cs so
//      this part stays compiler-independent and compiles into bhl_runtime too
public partial class ProjectConf
{
  const string FILE_NAME = "bhl.proj";

  public static ProjectConf ReadFromFile(string file_path)
  {
    var proj = JsonConvert.DeserializeObject<ProjectConf>(File.ReadAllText(file_path));
    proj.proj_file = file_path;
    proj.Setup();
    return proj;
  }

  public static void WriteToFile(ProjectConf proj, string file_path)
  {
    File.WriteAllText(file_path, JsonConvert.SerializeObject(proj));
  }

  public static ProjectConf TryReadFromDir(string dir_path)
  {
    string proj_file = dir_path + "/" + FILE_NAME;
    if(!File.Exists(proj_file))
      return null;
    return ReadFromFile(proj_file);
  }

  public ModuleBinaryFormat module_fmt = ModuleBinaryFormat.FMT_LZ4;
  //NOTE: only used when module_fmt is FMT_LZ4_CHUNKED - modules are
  //      accumulated into a chunk up to roughly this many bytes before
  //      it's LZ4-compressed as a whole and a new chunk is started
  public int lz4_chunk_size = 128 * 1024;

  public List<string> inc_dirs = new List<string>();
  [JsonIgnore] public IncludePath inc_path = new IncludePath();

  public List<string> src_dirs = new List<string>();

  public List<string> defines = new List<string>();

  public string result_file = "";
  public string tmp_dir = "";
  public string error_file = "";
  public bool use_cache = true;
  public int verbosity = 1;
  public int max_threads = 1;
  public bool deterministic = false;

  public const string DefaultBindingsScriptName = "RegisterBindings";

  [JsonIgnore] public string proj_file = "";

  //NOTE: every entry is used unconditionally - no per-entry "enabled" flag. At runtime
  //      (LoadRuntimeBindings()) only the keys matter, matched by name against BindingsRegistry
  public Dictionary<string, BindingsEntryConf> bindings = new Dictionary<string, BindingsEntryConf>();

  //NOTE: legacy fields, kept for BC with older bhl.proj files - folded into `bindings`
  //      (under LegacyBindingsKey) during Setup()
  public const string LegacyBindingsKey = "$legacy";

  public List<string> bindings_sources = new List<string>();
  public string bindings_dll = "";
  public bool bindings_manual_build = false;

  public void Setup()
  {
    if(bindings_sources.Count > 0 || !string.IsNullOrEmpty(bindings_dll))
    {
      if(!bindings.TryGetValue(LegacyBindingsKey, out var legacy))
      {
        legacy = new BindingsEntryConf();
        bindings[LegacyBindingsKey] = legacy;
      }
      legacy.sources.AddRange(bindings_sources);
      if(string.IsNullOrEmpty(legacy.dll))
        legacy.dll = bindings_dll;
      legacy.manual_build |= bindings_manual_build;
    }

    foreach(var b in bindings.Values)
    {
      for(int i = 0; i < b.sources.Count; ++i)
        b.sources[i] = NormalizePath(proj_file, b.sources[i]);
      b.dll = NormalizePath(proj_file, b.dll);
    }

    //NOTE: keep legacy fields normalized/in sync too, for anyone reading them directly
    if(bindings.TryGetValue(LegacyBindingsKey, out var legacy_final))
    {
      bindings_sources = legacy_final.sources;
      bindings_dll = legacy_final.dll;
      bindings_manual_build = legacy_final.manual_build;
    }

    SetupPostproc();

    for(int i = 0; i < inc_dirs.Count; ++i)
    {
      inc_dirs[i] = NormalizePath(proj_file, inc_dirs[i]);
      inc_path.Add(inc_dirs[i]);
    }

    for(int i = 0; i < src_dirs.Count; ++i)
    {
      src_dirs[i] = NormalizePath(proj_file, src_dirs[i]);
      if(inc_dirs.Count == 0)
        inc_path.Add(src_dirs[i]);
    }

    result_file = NormalizePath(proj_file, result_file);
    tmp_dir = NormalizePath(proj_file, tmp_dir);
    error_file = NormalizePath(proj_file, error_file);
  }

  //NOTE: implemented in proj_conf.postproc.cs when present, no-op otherwise
  partial void SetupPostproc();

  public static string NormalizePath(string proj_file, string file_path)
  {
    if(Path.IsPathRooted(file_path))
      return BuildUtils.NormalizeFilePath(file_path);

    if(!string.IsNullOrEmpty(proj_file) && !string.IsNullOrEmpty(file_path) && file_path[0] == '.')
      return BuildUtils.NormalizeFilePath(Path.Combine(Path.GetDirectoryName(proj_file), file_path));

    return file_path;
  }

  bool TryGetScriptedBindings(
      BindingsEntryConf b,
      out List<string> bindings_scripts,
      out string func_name,
      out string bindings_bytecode_file
      )
  {
    //TODO: make it configurable as well?
    func_name = DefaultBindingsScriptName;

    var tmp_scripts = b.sources.Where(f => f.EndsWith(".bhl")).ToList();
    bindings_scripts = new List<string>();
    foreach(var s in tmp_scripts)
      bindings_scripts.AddRange(BuildUtils.Glob(s));

    bindings_bytecode_file = null;
    if(!string.IsNullOrEmpty(b.dll) && b.dll.EndsWith(".bhc"))
      bindings_bytecode_file = b.dll;

    return bindings_scripts.Count > 0 || !string.IsNullOrEmpty(bindings_bytecode_file);
  }

  IUserBindings LoadBindingsEntry(BindingsEntryConf b)
  {
    if(!string.IsNullOrEmpty(b.dll) && b.dll.EndsWith(".dll"))
      return new DllBindings(b.dll);

    if(TryGetScriptedBindings(b, out var bindings_scripts, out string func_name, out var bindings_bytecode_file))
      return new ScriptedBindings(bindings_scripts, func_name, use_cache, bindings_bytecode_file, tmp_dir);

    return new EmptyUserBindings();
  }

  //NOTE: build/LSP-time loading, via dll loading and/or the compiler frontend - see
  //      LoadRuntimeBindings() for the shipped-runtime counterpart. Entries run in
  //      ordinal-key order for determinism
  public IUserBindings LoadBindings()
  {
    return LoadBindings(out _);
  }

  //NOTE: `versions` is each entry's own declared version (see BindingsRegistry.
  //      TryGetVersion), embedded into the compiled .bhc so an incompatible version at
  //      load time (see BindingsRegistry.RegisterRequiredBindings) is a clear failure
  //      instead of a confusing symbol-resolution one
  public IUserBindings LoadBindings(out List<(string name, string version)> versions)
  {
    var names = bindings.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
    var loaded = names.Select(k => LoadBindingsEntry(bindings[k])).ToList();

    versions = new List<(string name, string version)>();
    for(int i = 0; i < names.Count; ++i)
    {
      //NOTE: "$legacy" is a synthetic key Setup() invents for old flat bindings_sources/
      //      bindings_dll fields (see LegacyBindingsKey) - the underlying code was never
      //      told that name, so it never declares a version under it. Skip rather than
      //      forcing pre-versioning bhl.proj files to fail compilation entirely
      if(names[i] == LegacyBindingsKey)
        continue;

      if(!BindingsRegistry.TryGetVersion(names[i], loaded[i], out var version))
        throw new Exception($"Bindings entry '{names[i]}' does not declare a version");
      versions.Add((names[i], version));
    }

    if(loaded.Count == 0)
      return new EmptyUserBindings();
    if(loaded.Count == 1)
      return loaded[0];
    return new CombinedUserBindings(loaded.Cast<IUserBindings>().ToList());
  }

  //NOTE: declares a bindings entry name with no sources/dll - for LoadRuntimeBindings()
  //      callers that don't ship/parse bhl.proj itself
  public void DeclareBindingsEntry(string name)
  {
    if(!bindings.ContainsKey(name))
      bindings[name] = new BindingsEntryConf();
  }

  //NOTE: runtime counterpart to LoadBindings() - no dynamic dll loading or compiler
  //      frontend available on a shipped binary, so this matches `bindings`' keys by
  //      name against BindingsRegistry instead. Unmatched names are skipped, not errors
  public IUserBindings LoadRuntimeBindings()
  {
    var names = bindings.Keys.OrderBy(k => k, StringComparer.Ordinal);
    return new CombinedUserBindings(
      BindingsRegistry.GetByNames(names)
        .Select(t => (IUserBindings)Activator.CreateInstance(t))
        .ToList()
    );
  }
}
}
