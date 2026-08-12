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

//NOTE: fingerprints the symbol shape an IUserBindings declares - lets a compiled .bhc
//      detect at load time that the bindings it was compiled against have drifted
//      (see BindingsRegistry.RegisterRequiredBindings)
public static class BindingsHash
{
  //NOTE: a single XOR-folded CRC32, not a set - baseline signatures always appear
  //      exactly once in any Types(), so XOR-ing it out below cancels them cleanly
  static readonly uint baseline_crc = ComputeCrc(new Types().ns);

  public static string Compute(IUserBindings bindings)
  {
    var ts = new Types();
    bindings.Register(ts);
    return (ComputeCrc(ts.ns) ^ baseline_crc).ToString("x8");
  }

  //NOTE: XOR-folding each symbol's own CRC32 is order-independent, so the walk below
  //      needs no sorting or intermediate collection to combine them deterministically
  static uint ComputeCrc(Symbol root)
  {
    uint crc = 0;
    Collect(root, ref crc);
    return crc;
  }

  //NOTE: recurses into any nested scope (Namespace, ClassSymbol, ...) so class
  //      methods/fields are fingerprinted too, not just top-level declarations
  static void Collect(Symbol root, ref uint crc)
  {
    if(root is not IEnumerable<Symbol> scope)
      return;

    foreach(var m in scope)
    {
      crc ^= Hash.CRC32(root.GetName() + "::" + m);
      Collect(m, ref crc);
    }
  }
}

//NOTE: implementations self-register here (typically via a [ModuleInitializer] calling
//      Register("name", typeof(X))) instead of being found via assembly-wide reflection,
//      which Unity/IL2CPP's stripper tends to break. `name` matches bhl.proj's `bindings`
//      dict key, letting ProjectConf.LoadRuntimeBindings() find it at runtime
public static class BindingsRegistry
{
  static readonly List<(string name, Type type)> all = new List<(string name, Type type)>();

  public static void Register(string name, Type type)
  {
    if(string.IsNullOrEmpty(name))
      throw new Exception("Bindings entry name must not be empty");
    if(!typeof(IUserBindings).IsAssignableFrom(type))
      throw new Exception($"{type} does not implement {nameof(IUserBindings)}");

    if(!all.Contains((name, type)))
      all.Add((name, type));
  }

  public static IEnumerable<Type> GetAll()
  {
    return all.Select(e => e.type);
  }

  public static IEnumerable<Type> GetForAssembly(System.Reflection.Assembly assembly)
  {
    return all.Where(e => e.type.Assembly == assembly).Select(e => e.type);
  }

  //NOTE: a name with nothing registered under it is skipped, not an error
  public static IEnumerable<Type> GetByNames(IEnumerable<string> names)
  {
    foreach(var name in names)
      foreach(var e in all)
        if(e.name == name)
          yield return e.type;
  }

  //NOTE: instantiates and fans out to everything currently self-registered - for a driver
  //      with no compile-time knowledge of which bindings classes exist (e.g. a generic
  //      Unity Editor integration), as opposed to RegisterRequiredBindings' name-filtered,
  //      hash-checked counterpart used on the Player/runtime side
  public static IUserBindings CreateCombined()
  {
    return new CombinedUserBindings(
      GetAll().Select(t => (IUserBindings)Activator.CreateInstance(t)).ToList()
    );
  }

  //NOTE: reads loader.RequiredBindings and registers matches into `ts`, so a caller with
  //      just a .bhc doesn't need a ProjectConf/bhl.proj to know its dependencies. Both a
  //      missing binding and a hash mismatch (see BindingsHash) are hard failures here,
  //      right where the name is known - letting either pass silently just relocates the
  //      failure to a much more confusing spot later (or, if nothing loaded happens to
  //      reference it, no failure at all despite the binding being silently missing)
  public static void RegisterRequiredBindings(Types ts, ModuleLoader loader)
  {
    foreach(var (name, expected_hash) in loader.RequiredBindings)
    {
      var type = GetByNames(new[] { name }).FirstOrDefault();
      if(type == null)
        throw new Exception(
          $"Required bindings '{name}' not found - no IUserBindings self-registered " +
          $"under that name (renamed? assembly not loaded?)"
        );

      var instance = (IUserBindings)Activator.CreateInstance(type);

      var actual_hash = BindingsHash.Compute(instance);
      if(actual_hash != expected_hash)
        throw new Exception(
          $"Bindings '{name}' hash mismatch: compiled against a different shape " +
          $"than what's registered now (expected {expected_hash}, got {actual_hash})"
        );

      instance.Register(ts);
    }
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
