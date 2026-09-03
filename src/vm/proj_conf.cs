using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace bhl
{

public class BindingsEntryConf
{
  //NOTE: required - cherry-picks a BindingsRegistry-discoverable binding by this name
  //      when available, else falls back to sources/dll below (see LoadBindingsEntry)
  public string name = "";

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

  //NOTE: marks the entry Setup() synthesizes from the old flat bindings_sources/bindings_dll
  //      fields - exempt from LoadBindings()'s "must declare a version" check
  [JsonIgnore] public bool is_legacy = false;
}

//NOTE: lets an old dict-shaped "bindings" JSON value ({"name": {...}}) keep working - the
//      dict key becomes the entry's `name` (now required, see BindingsEntryConf.name),
//      unless the entry body already sets its own
class BindingsListConverter : JsonConverter
{
  public override bool CanConvert(Type objectType) => objectType == typeof(List<BindingsEntryConf>);

  public override bool CanWrite => false;

  public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
    JsonSerializer serializer)
  {
    var token = JToken.Load(reader);
    var list = new List<BindingsEntryConf>();

    if(token.Type == JTokenType.Null)
      return list;

    if(token.Type == JTokenType.Array)
    {
      foreach(var item in token)
        list.Add(item.ToObject<BindingsEntryConf>(serializer));
      return list;
    }

    if(token.Type == JTokenType.Object)
    {
      foreach(var prop in ((JObject)token).Properties())
      {
        var entry = prop.Value.ToObject<BindingsEntryConf>(serializer);
        if(string.IsNullOrEmpty(entry.name))
          entry.name = prop.Name;
        list.Add(entry);
      }
      return list;
    }

    throw new JsonSerializationException("'bindings' must be a JSON array or object, got " + token.Type);
  }

  public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
  {
    throw new NotSupportedException();
  }
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

  //NOTE: paths to other bhl.proj files whose src_dirs/bindings/inc_dirs get folded in (see
  //      ExpandIncludes) - a non-empty 'defines' on one of them is rejected, everything else is ignored
  public List<string> includes = new List<string>();

  public List<string> defines = new List<string>();

  public string result_file = "";
  public string tmp_dir = "";
  public string error_file = "";
  public bool use_cache = true;
  public int verbosity = 1;
  //NOTE: 0 (or unset) means auto - see Setup()
  public int max_threads = 0;
  public bool deterministic = true;

  public const string DefaultBindingsScriptName = "RegisterBindings";

  //NOTE: optional companion function - 'func string,string BindingInfo() { return "name",
  //      "1.0.0" }' - cheaper to discover a scripted binding's (name, version) than running
  //      the whole RegisterBindings (see ScriptedBindings.GetDeclaredBindings)
  public const string DefaultBindingsInfoScriptName = "BindingInfo";

  [JsonIgnore] public string proj_file = "";

  //NOTE: canonical paths of every bhl.proj folded in via 'includes' (recursively) - lets a
  //      caller (e.g. the LSP's file watcher) tell it apart from an unrelated bhl.proj
  [JsonIgnore] public HashSet<string> included_files = new HashSet<string>();

  //NOTE: every entry is used unconditionally, no per-entry "enabled" flag; every non-legacy
  //      entry must declare a `name` (checked in Setup())
  [JsonConverter(typeof(BindingsListConverter))]
  public List<BindingsEntryConf> bindings = new List<BindingsEntryConf>();

  //NOTE: legacy fields, kept for BC with older bhl.proj files - folded into `bindings`
  //      (as an is_legacy-flagged entry) during Setup()
  public List<string> bindings_sources = new List<string>();
  public string bindings_dll = "";
  public bool bindings_manual_build = false;

  public void Setup()
  {
    if(max_threads <= 0)
      max_threads = Environment.ProcessorCount;

    ExpandIncludes(this, proj_file, new HashSet<string>(), included_files);

    if(bindings_sources.Count > 0 || !string.IsNullOrEmpty(bindings_dll))
    {
      var legacy = bindings.Find(b => b.is_legacy);
      if(legacy == null)
      {
        legacy = new BindingsEntryConf { is_legacy = true };
        bindings.Add(legacy);
      }
      legacy.sources.AddRange(bindings_sources);
      if(string.IsNullOrEmpty(legacy.dll))
        legacy.dll = bindings_dll;
      legacy.manual_build |= bindings_manual_build;
    }

    //NOTE: stable per bhl.proj (hashed from its own path), so repeated runs reuse the
    //      same scratch space instead of each caller having to invent/manage their own
    if(string.IsNullOrEmpty(tmp_dir))
      tmp_dir = DefaultProjectTempDir(proj_file);
    tmp_dir = NormalizePath(proj_file, tmp_dir);

    //NOTE: two entries sharing a name (own + include, or two includes) would otherwise only
    //      fail later, during the real compile, as a confusing raw dictionary-key exception
    //      deep in Types.RegisterModule/RegisterBindingsVersion - catch it here instead
    var seen_binding_names = new HashSet<string>();
    foreach(var b in bindings)
    {
      //NOTE: legacy entries predate `name` entirely and stay exempt
      if(!b.is_legacy && string.IsNullOrEmpty(b.name))
        throw new Exception("Bindings entry must have a non-empty 'name'");

      if(!b.is_legacy && !seen_binding_names.Add(b.name))
        throw new Exception($"Bindings entry '{b.name}' declared more than once - names must be unique");

      for(int i = 0; i < b.sources.Count; ++i)
        b.sources[i] = NormalizePath(proj_file, b.sources[i]);

      //NOTE: no explicit build target - park one under tmp_dir instead, stable per entry
      //      name so repeated compiles reuse the same path
      if(string.IsNullOrEmpty(b.dll) && b.sources.Count > 0)
        b.dll = AutoBindingsDllPath(b);
      else
        b.dll = NormalizePath(proj_file, b.dll);
    }

    //NOTE: keep legacy fields normalized/in sync too, for anyone reading them directly
    var legacy_final = bindings.Find(b => b.is_legacy);
    if(legacy_final != null)
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

    //NOTE: wildcard-expanded (e.g. a UPM package's version-suffixed PackageCache path),
    //      same as bindings' `sources` - see BuildUtils.Glob
    var expanded_src_dirs = new List<string>();
    foreach(var src_dir in src_dirs)
    {
      string pattern = NormalizePath(proj_file, src_dir);
      expanded_src_dirs.AddRange(BuildUtils.Glob(pattern).Where(Directory.Exists));
    }
    src_dirs = expanded_src_dirs;

    if(inc_dirs.Count == 0)
    {
      foreach(var src_dir in src_dirs)
        inc_path.Add(src_dir);
    }

    result_file = NormalizePath(proj_file, result_file);

    if(string.IsNullOrEmpty(error_file))
      error_file = Path.Combine(tmp_dir, "bhl.error");
    error_file = NormalizePath(proj_file, error_file);
  }

  //NOTE: implemented in proj_conf.postproc.cs when present, no-op otherwise
  partial void SetupPostproc();

  //NOTE: implemented in proj_conf.postproc.cs when present, no-op otherwise - rejects
  //      postproc_* fields on an included proj (called from ExpandIncludes below), same
  //      treatment as 'defines' and the legacy bindings_* fields
  partial void CheckNoPostproc(string included_file);

  public static string NormalizePath(string proj_file, string file_path)
  {
    if(Path.IsPathRooted(file_path))
      return BuildUtils.NormalizeFilePath(file_path);

    if(!string.IsNullOrEmpty(proj_file) && !string.IsNullOrEmpty(file_path) && file_path[0] == '.')
      return BuildUtils.NormalizeFilePath(Path.Combine(Path.GetDirectoryName(proj_file), file_path));

    return file_path;
  }

  //NOTE: recursively folds each included bhl.proj into 'proj'. 'visiting' is the current
  //      chain (cycle detection); 'already_included' is 'proj.included_files' itself, so a
  //      diamond-shaped include graph is folded in only once
  static void ExpandIncludes(ProjectConf proj, string anchor_file, HashSet<string> visiting, HashSet<string> already_included)
  {
    if(proj.includes.Count == 0)
      return;

    var patterns = proj.includes;
    proj.includes = new List<string>();

    foreach(var pattern_raw in patterns)
    {
      string pattern = NormalizePath(anchor_file, pattern_raw);
      var matches = BuildUtils.Glob(pattern).Where(File.Exists).ToList();

      if(matches.Count == 0)
        throw new Exception($"Include '{pattern_raw}' did not match any existing file");
      if(matches.Count > 1)
        throw new Exception($"Include '{pattern_raw}' matched more than one file: {string.Join(", ", matches)}");

      string included_file = matches[0];
      string canon = BuildUtils.NormalizeFilePath(included_file);
      if(!visiting.Add(canon))
        throw new Exception($"Include cycle detected at '{included_file}'");

      if(already_included.Add(canon))
      {
        var included = JsonConvert.DeserializeObject<ProjectConf>(File.ReadAllText(included_file)) ?? new ProjectConf();
        ExpandIncludes(included, included_file, visiting, already_included);

        if(included.defines.Count > 0)
          throw new Exception(
            $"Included bhl.proj '{included_file}' must not declare 'defines' - " +
            "defines only apply to the including project"
          );

        //NOTE: legacy bindings_sources/bindings_dll only get folded into 'bindings' by the
        //      root's own Setup() - an included file's legacy fields would otherwise be
        //      silently dropped (never read), so reject them outright instead
        if(included.bindings_sources.Count > 0 || !string.IsNullOrEmpty(included.bindings_dll) || included.bindings_manual_build)
          throw new Exception(
            $"Included bhl.proj '{included_file}' must not use the legacy 'bindings_sources'/" +
            "'bindings_dll'/'bindings_manual_build' fields - use the 'bindings' array instead"
          );

        included.CheckNoPostproc(included_file);

        foreach(var src_dir in included.src_dirs)
          proj.src_dirs.Add(NormalizePath(included_file, src_dir));

        foreach(var inc_dir in included.inc_dirs)
          proj.inc_dirs.Add(NormalizePath(included_file, inc_dir));

        foreach(var b in included.bindings)
        {
          for(int i = 0; i < b.sources.Count; ++i)
            b.sources[i] = NormalizePath(included_file, b.sources[i]);
          if(!string.IsNullOrEmpty(b.dll))
            b.dll = NormalizePath(included_file, b.dll);
          proj.bindings.Add(b);
        }
      }

      visiting.Remove(canon);
    }
  }

  static string DefaultProjectTempDir(string proj_file)
  {
    string key = string.IsNullOrEmpty(proj_file) ? Guid.NewGuid().ToString() : Path.GetFullPath(proj_file);
    return BuildUtils.NormalizeFilePath(Path.Combine(Path.GetTempPath(), "bhl_" + ShortHash(key)));
  }

  string AutoBindingsDllPath(BindingsEntryConf b)
  {
    string ext = b.sources.Any(s => s.EndsWith(".cs")) ? ".dll" : ".bhc";
    string label = string.IsNullOrEmpty(b.name) ? "bindings" : b.name;
    string safe_label = new string(label.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
    return BuildUtils.NormalizeFilePath(Path.Combine(tmp_dir, safe_label + ext));
  }

  static string ShortHash(string s)
  {
    using(var md5 = System.Security.Cryptography.MD5.Create())
    {
      var bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s));
      var sb = new System.Text.StringBuilder();
      foreach(var b in bytes)
        sb.Append(b.ToString("x2"));
      return sb.ToString().Substring(0, 12);
    }
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

  //NOTE: a name already discoverable in this process (e.g. Unity Editor) resolves to
  //      that live binding; otherwise falls back to sources/dll (e.g. a separate LSP/CLI)
  IUserBindings LoadBindingsEntry(BindingsEntryConf b)
  {
    if(BindingsRegistry.IsRegistered(b.name))
      return new RegistryBindings(b.name);

    if(!string.IsNullOrEmpty(b.dll) && b.dll.EndsWith(".dll"))
      return new DllBindings(b.dll);

    if(TryGetScriptedBindings(b, out var bindings_scripts, out string func_name, out var bindings_bytecode_file))
      return new ScriptedBindings(bindings_scripts, func_name, use_cache, bindings_bytecode_file, tmp_dir);

    return new EmptyUserBindings();
  }

  //NOTE: build/LSP-time loading, via dll loading and/or the compiler frontend. Entries
  //      run in list order for determinism. The returned UserBindingsWithInfo.info
  //      (discovered per entry, see DiscoverDeclaredBindings) is embedded into the compiled
  //      .bhc so an incompatible version at load time is a clear failure instead of a
  //      confusing symbol-resolution one (BindingsRegistry.RegisterRequiredBindings)
  public IUserBindings LoadBindings()
  {
    var loaded = bindings.Select(LoadBindingsEntry).ToList();

    var versions = new List<(string name, string version)>();
    for(int i = 0; i < bindings.Count; ++i)
    {
      //NOTE: legacy entries predate 'name'/BindingInfo() entirely and were never required to
      //      declare either - but if one has opted in anyway (e.g. adding BindingInfo() to an
      //      existing legacy script without migrating to the newer 'bindings' array shape),
      //      pick it up. This is best-effort only: unlike the required/matched case below,
      //      a failed discovery attempt (e.g. bindings_dll not built yet) must stay a
      //      harmless no-op, exactly like a legacy entry with no BindingInfo() at all
      if(bindings[i].is_legacy)
      {
        try
        {
          versions.AddRange(DiscoverDeclaredBindings(loaded[i]));
        }
        catch
        {
        }
        continue;
      }

      var declared = DiscoverDeclaredBindings(loaded[i]).ToList();
      if(declared.Count == 0)
        throw new Exception($"Bindings entry '{bindings[i].name}' does not declare a version");

      //NOTE: catches a stale/typo'd `name` early
      if(!declared.Any(d => d.name == bindings[i].name))
        throw new Exception(
          $"Bindings entry '{bindings[i].name}' does not match its actual declared name(s) " +
          $"({string.Join(", ", declared.Select(d => d.name))}) - check bhl.proj's 'name' field"
        );

      versions.AddRange(declared);
    }

    IUserBindings combined =
      loaded.Count == 0 ? new EmptyUserBindings() :
      loaded.Count == 1 ? loaded[0] :
      new CombinedUserBindings(loaded.Cast<IUserBindings>().ToList());

    return new UserBindingsWithInfo(combined, versions);
  }

  //NOTE: a dll's names come off each class's own [BhlBinding] attribute, a scripted binding's
  //      off its optional 'BindingInfo' function (see ScriptedBindings.GetDeclaredBindings) -
  //      both without running the real registration. Anything else is run once on a scratch
  //      Types() and diffed against its baseline ("prelude" is always there) to see what its
  //      own RegisterBindingsVersion(...) added (e.g. a reflection-discovered C# binding)
  static IEnumerable<(string name, string version)> DiscoverDeclaredBindings(IUserBindings b)
  {
    if(b is DllBindings dll)
      return dll.GetDeclaredBindings();

    if(b is ScriptedBindings scripted)
      return scripted.GetDeclaredBindings();

    if(b is EmptyUserBindings)
      return Enumerable.Empty<(string, string)>();

    var scratch = new Types();
    var before = new HashSet<string>(scratch.BindingsVersionNames);
    b.Register(scratch);

    var result = new List<(string name, string version)>();
    foreach(var name in scratch.BindingsVersionNames)
    {
      if(before.Contains(name))
        continue;
      scratch.TryGetBindingsVersion(name, out var version);
      result.Add((name, version));
    }
    return result;
  }
}
}
