using System;
using System.Collections.Generic;
using System.IO;

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

public class DllBindings : IUserBindings
{
  string dll_path;

  public DllBindings(string dll_path)
  {
    this.dll_path = dll_path;
  }

  public void Register(Types ts)
  {
    var assembly = LoadAssemblyFromDirOrFile(dll_path);
    var types = assembly.GetTypes();

    Type userbindings_class = null;
    foreach(var type in types)
    {
      if(typeof(IUserBindings).IsAssignableFrom(type))
      {
        userbindings_class = type;
        break;
      }
    }

    if(userbindings_class == null)
      throw new Exception($"IUserBindings instance not found in '{dll_path}'");

    try
    {
      var userbindings = Activator.CreateInstance(userbindings_class) as IUserBindings;
      userbindings.Register(ts);
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

  public ScriptedBindings(
    List<string> script_paths,
    string func_name,
    bool use_cache = false,
    string bytecode_file = null
  )
  {
    this.script_paths = script_paths;
    this.func_name = func_name;
    this.use_cache = use_cache;
    this.bytecode_file = bytecode_file;
  }

  public void Register(Types ts)
  {
#if BHL_FRONT
    //var sw = System.Diagnostics.Stopwatch.StartNew();
    var vm = CompilationExecutor.CompileAndLoadVM(
      script_paths,
      use_cache: use_cache,
      bytecode_result_file: bytecode_file
    ).GetAwaiter().GetResult();
    if(vm == null)
      throw new Exception("Failed to initialize scripted bindings");
    //for quick debug
    //Console.WriteLine("Scripted bindings compiled in " + sw.Elapsed.TotalSeconds + " sec");
    vm.Execute(func_name, Val.NewObj(ts, std.bind.TypesSymbol));
#else
    throw new NotImplementedException();
#endif
  }
}


}
