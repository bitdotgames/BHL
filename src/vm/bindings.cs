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

//NOTE: optional extension - implement IN ADDITION to IUserBindings to split registration
//      into a declare-only phase (safe for LSP, no native dependency) and a phase that
//      attaches the real implementation (FuncSymbolNative.cb, ClassSymbolNative.AttachNative()).
//      Register()'s default body needs Unity's API Compatibility Level at .NET Standard
//      2.1 (2021.2+) for default interface methods; on older targets just implement
//      Register() yourself instead of relying on the default
public interface IUserBindingsExtended : IUserBindings
{
  void DeclareTypes(Types ts);
  void AttachDelegates(Types ts);

  void IUserBindings.Register(Types ts)
  {
    DeclareTypes(ts);
    AttachDelegates(ts);
  }
}

public class EmptyUserBindings : IUserBindingsExtended
{
  public void DeclareTypes(Types ts)
  {
  }

  public void AttachDelegates(Types ts)
  {
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
      throw new Exception("Bindings module name must not be empty");
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
}

//NOTE: fans out to N bindings, mixing plain IUserBindings (AttachDelegates() is a no-op
//      for those - Register() already did everything in DeclareTypes()) and IUserBindingsExtended
public class CombinedUserBindings : IUserBindingsExtended
{
  //List instead of Enumerable to preserve the specified order
  readonly IList<IUserBindings> _bindings;

  public CombinedUserBindings(IList<IUserBindings> bindings)
  {
    _bindings = bindings;
  }

  public void DeclareTypes(Types ts)
  {
    for(int i = 0; i < _bindings.Count; i++)
    {
      if(_bindings[i] is IUserBindingsExtended split)
        split.DeclareTypes(ts);
      else
        _bindings[i].Register(ts);
    }
  }

  public void AttachDelegates(Types ts)
  {
    for(int i = 0; i < _bindings.Count; i++)
      if(_bindings[i] is IUserBindingsExtended split)
        split.AttachDelegates(ts);
  }
}

public class DllBindings : IUserBindingsExtended
{
  string dll_path;
  IUserBindingsExtended loaded;

  public DllBindings(string dll_path)
  {
    this.dll_path = dll_path;
  }

  public void DeclareTypes(Types ts)
  {
    EnsureLoaded();
    loaded.DeclareTypes(ts);
  }

  public void AttachDelegates(Types ts)
  {
    EnsureLoaded();
    loaded.AttachDelegates(ts);
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

public class ScriptedBindings : IUserBindingsExtended
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

  public void DeclareTypes(Types ts)
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

  public void AttachDelegates(Types ts)
  {
    //NOTE: .bhl bindings are pure script — they can only declare types/functions,
    //      never attach a native implementation
  }
}


}
