using System.Reflection;

namespace bhl {

public static class BHLBuild
{
  //TODO: This is currently required as a hint for external UPM packaging only.
  //      This code should be removed once UPM package is built automatically
  //      as an external build step. 
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