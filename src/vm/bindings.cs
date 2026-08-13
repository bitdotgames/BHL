using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace bhl
{

public interface IUserBindings
{
  void Register(Types ts);
}

public class EmptyUserBindings : IUserBindings
{
  public void Register(Types ts)
  {
  }
}

//NOTE: implementations self-register here (typically via a [ModuleInitializer] calling
//      Register("name", typeof(X), "X.Y.Z")) instead of being found via assembly-wide
//      reflection, which Unity/IL2CPP's stripper tends to break. `version` is
//      human-maintained and is the single source of truth for it - checked for semver
//      compatibility (see RegisterRequiredBindings) and injected into Types right before
//      a binding's own Register() runs
public static class BindingsRegistry
{
  //NOTE: Types()'s own constructor always attaches this one directly (see PreludeBindings)
  //      so it must be excluded from "attach everything" helpers like CreateCombined()
  public const string PreludeName = "prelude";

  static readonly Dictionary<string, (Type type, string version)> all = new Dictionary<string, (Type type, string version)>();

  public static void Register(string name, Type type, string version)
  {
    if(string.IsNullOrEmpty(name))
      throw new Exception("Bindings entry name must not be empty");
    if(!typeof(IUserBindings).IsAssignableFrom(type))
      throw new Exception($"{type} does not implement {nameof(IUserBindings)}");
    if(!System.Version.TryParse(VersionCore(version), out _))
      throw new Exception($"Bindings '{name}' version '{version}' is not a valid version (expected 'X.Y.Z' or 'X.Y.Z-tag')");

    //NOTE: compares by FullName, not Type reference equality - a Unity script recompile
    //      without a full domain reload (e.g. Enter Play Mode Options with Domain Reload
    //      off) re-fires this with a *different* Type object for the same class, so a
    //      stale entry for it needs replacing rather than piling up as a duplicate. A
    //      different class claiming an already-used name is a real collision, not a
    //      recompile, so that's a hard failure instead of a silent overwrite
    if(all.TryGetValue(name, out var existing) && existing.type.FullName != type.FullName)
      throw new Exception(
        $"Bindings '{name}' already registered by {existing.type.FullName}, " +
        $"cannot also register {type.FullName} under the same name"
      );

    all[name] = (type, version);
  }

  public static IEnumerable<Type> GetAll()
  {
    return all.Values.Select(e => e.type);
  }

  public static IEnumerable<Type> GetForAssembly(System.Reflection.Assembly assembly)
  {
    return all.Values.Where(e => e.type.Assembly == assembly).Select(e => e.type);
  }

  //NOTE: a name with nothing registered under it is skipped, not an error
  public static IEnumerable<Type> GetByNames(IEnumerable<string> names)
  {
    foreach(var name in names)
      if(all.TryGetValue(name, out var e))
        yield return e.type;
  }

  //NOTE: instantiates and fans out to everything self-registered - for a driver with no
  //      compile-time knowledge of which bindings classes exist (e.g. a Unity Editor
  //      integration), unlike RegisterRequiredBindings' name-filtered lookup. Excludes
  //      prelude, since every Types() already attaches that one itself
  public static IUserBindings CreateCombined()
  {
    return new CombinedUserBindings(
      all.Where(kv => kv.Key != PreludeName)
        .Select(kv => (IUserBindings)Activator.CreateInstance(kv.Value.type))
        .ToList()
    );
  }

  //NOTE: falls back to running Register() on a scratch Types() only for entries with no
  //      registry entry (e.g. a .bhl-scripted stub, which self-declares its own version
  //      via ts.RegisterBindingsVersion instead of self-registering here)
  public static bool TryGetVersion(string name, IUserBindings bindings, out string version)
  {
    if(all.TryGetValue(name, out var match))
    {
      version = match.version;
      return true;
    }

    var scratch = new Types();
    bindings.Register(scratch);

    if(all.TryGetValue(name, out match))
    {
      version = match.version;
      return true;
    }

    return scratch.TryGetBindingsVersion(name, out version);
  }

  //NOTE: registers matches for loader.RequiredBindings into `ts` - a missing binding or
  //      an incompatible version is a hard failure here, not a silent skip
  public static void RegisterRequiredBindings(Types ts, ModuleLoader loader)
  {
    foreach(var (name, required_version) in loader.RequiredBindings)
    {
      if(!all.TryGetValue(name, out var match))
        throw new Exception(
          $"Required bindings '{name}' not found - no IUserBindings self-registered " +
          $"under that name (renamed? assembly not loaded?)"
        );

      if(!IsVersionCompatible(match.version, required_version))
        throw new Exception(
          $"Bindings '{name}' version incompatible: required {required_version}, " +
          $"but {match.version} is registered"
        );

      RegisterForType(ts, name);
    }
  }

  //NOTE: instantiates a self-registered entry, injects its version into `ts`, and runs
  //      its Register() - no version-compatibility check, unlike RegisterRequiredBindings.
  //      For entries that are unconditionally needed (e.g. Types()'s own prelude) rather
  //      than checked against a compiled .bhc's required version
  public static void RegisterForType(Types ts, string name)
  {
    if(!all.TryGetValue(name, out var match))
      throw new Exception($"Bindings '{name}' not found - no IUserBindings self-registered under that name");

    var instance = (IUserBindings)Activator.CreateInstance(match.type);
    ts.RegisterBindingsVersion(name, match.version);
    instance.Register(ts);
  }

  //NOTE: a pre-release tag (the "-beta1" in "3.1.0-beta1") opts out of semver ranges -
  //      it must match the required string exactly, since pre-releases carry no
  //      compatibility guarantee even against themselves. Only plain "X.Y.Z" versions
  //      get the usual "same major, registered >= required" check
  static bool IsVersionCompatible(string available, string required)
  {
    if(HasPrereleaseTag(available) || HasPrereleaseTag(required))
      return available == required;

    var a = System.Version.Parse(available);
    var r = System.Version.Parse(required);
    if(a.Major != r.Major)
      return false;
    if(a.Minor != r.Minor)
      return a.Minor > r.Minor;
    return a.Build >= r.Build;
  }

  static bool HasPrereleaseTag(string version)
  {
    return version.Contains('-');
  }

  static string VersionCore(string version)
  {
    int dash = version.IndexOf('-');
    return dash < 0 ? version : version.Substring(0, dash);
  }
}

//NOTE: fans out to N bindings, each Register()'d in the given order
public class CombinedUserBindings : IUserBindings
{
  //List instead of Enumerable to preserve the specified order
  readonly IList<IUserBindings> _bindings;

  public CombinedUserBindings(IList<IUserBindings> bindings)
  {
    _bindings = bindings;
  }

  public void Register(Types ts)
  {
    for(int i = 0; i < _bindings.Count; i++)
      _bindings[i].Register(ts);
  }
}

public class DllBindings : IUserBindings
{
  string dll_path;
  IUserBindings loaded;

  public DllBindings(string dll_path)
  {
    this.dll_path = dll_path;
  }

  public void Register(Types ts)
  {
    EnsureLoaded();
    loaded.Register(ts);
  }

  void EnsureLoaded()
  {
    if(loaded != null)
      return;

    var assembly = LoadAssemblyFromDirOrFile(dll_path);

    //NOTE: Assembly.LoadFrom alone doesn't reliably trigger [ModuleInitializer]s - force it
    System.Runtime.CompilerServices.RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);

    var userbindings_classes = BindingsRegistry.GetForAssembly(assembly).ToList();

    //NOTE: BC fallback for dlls built before self-registration existed
    if(userbindings_classes.Count == 0)
    {
      userbindings_classes = assembly.GetTypes()
        .Where(t => typeof(IUserBindings).IsAssignableFrom(t))
        .ToList();
    }

    if(userbindings_classes.Count == 0)
      throw new Exception(
        $"IUserBindings instance not found in '{dll_path}'. " +
        "Make sure it self-registers with BindingsRegistry (e.g. via a [ModuleInitializer]) and " +
        "was built against the same bhl_front.dll used by this tool " +
        "(e.g. not against bhl_runtime.dll, which defines its own distinct IUserBindings type)."
      );

    try
    {
      var instances = userbindings_classes
        .Select(t => (IUserBindings)Activator.CreateInstance(t))
        .ToList();

      loaded = new CombinedUserBindings(instances);
    }
    catch(Exception e)
    {
      throw new Exception($"Error while registering bindings from '{dll_path}'", e);
    }
  }

  //NOTE: .Net build target can be a directory which actually contains the target dll, e.g. bindings.dll/bindings.dll,
  //      this function takes this into consideration
  static System.Reflection.Assembly LoadAssemblyFromDirOrFile(string path)
  {
    return System.Reflection.Assembly.LoadFrom(
      Directory.Exists(path) ? path + "/" + Path.GetFileName(path) : path
    );
  }
}

public class ScriptedBindings : IUserBindings
{
  List<string> script_paths;
  string func_name;
  bool use_cache;
  string bytecode_file;
  string tmp_dir;

  public ScriptedBindings(
    List<string> script_paths,
    string func_name,
    bool use_cache = false,
    string bytecode_file = null,
    string tmp_dir = null
  )
  {
    this.script_paths = script_paths;
    this.func_name = func_name;
    this.use_cache = use_cache;
    this.bytecode_file = bytecode_file;
    this.tmp_dir = tmp_dir;
  }

  public void Register(Types ts)
  {
#if (BHL_PARSER || UNITY_EDITOR)
    //var sw = System.Diagnostics.Stopwatch.StartNew();
    var vm = CompilationExecutor.CompileAndLoadVM(
      script_paths,
      use_cache: use_cache,
      bytecode_result_file: bytecode_file,
      tmp_dir: tmp_dir
    ).GetAwaiter().GetResult();
    if(vm == null)
      throw new Exception("Failed to initialize scripted bindings");
    //for quick debug
    //Console.WriteLine("Scripted bindings compiled in " + sw.Elapsed.TotalSeconds + " sec");

    //NOTE: any file that itself declares a top-level `func_name` gets it invoked
    foreach(var script_path in script_paths)
    {
      var module_name = Path.GetFileNameWithoutExtension(script_path);
      if(!vm.LoadModule(module_name, out var module))
        continue;

      //NOTE: module-local lookup only, so an import doesn't count as declaring it
      var sym = module.ns.members.Find(func_name);
      if(sym is FuncSymbolScript fss)
        vm.Execute(fss, Val.NewObj(ts, std.bind.TypesSymbol));
    }
#else
    throw new NotImplementedException();
#endif
  }
}


}
