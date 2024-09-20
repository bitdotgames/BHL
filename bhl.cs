using System.Reflection;

namespace bhl {

public static class BHLBuild
{
  //TODO: this is currently required for external UPM packaging,
  //      UPM package must be built as an external build step 
  static readonly string[] VM_SRC = new string[]
  {
    "src/vm/*.cs",
    "src/vm/type/*.cs",
    "src/vm/scope/*.cs",
    "src/vm/symbol/*.cs",
    "src/vm/std/*.cs",
    "src/vm/util/*.cs",
    "src/vm/marshall/*.cs",
    "src/vm/msgpack/*.cs",
  };

  public static void Main(string[] args)
  {
    var tm = new Taskman(typeof(Tasks));
    try
    {
      tm.Run(args);
    }
    catch (TargetInvocationException e)
    {
      if (e.InnerException is ShellException)
        System.Environment.Exit((e.InnerException as ShellException).code);
      else
        throw;
    }
  }
}
  
}