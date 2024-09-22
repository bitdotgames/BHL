using System.Reflection;

namespace bhl {

public static class BHLBuild
{
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
