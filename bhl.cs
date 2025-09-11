using System.Reflection;
using ThreadTask = System.Threading.Tasks.Task;

namespace bhl;

public static class BHLBuild
{
  public static async ThreadTask Main(string[] args)
  {
    var tm = new taskman.Taskman(typeof(taskman.Tasks));
    try
    {
      await tm.Run(args);
    }
    catch(TargetInvocationException e)
    {
      if(e.InnerException is taskman.ShellException se)
        System.Environment.Exit(se.code);
      else
        throw;
    }
  }
}
