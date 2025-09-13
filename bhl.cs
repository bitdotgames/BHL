using System.Reflection;
using ThreadTask = System.Threading.Tasks.Task;

namespace bhl.taskman;

public static class BHLBuild
{
  public static async ThreadTask Main(string[] args)
  {
    var tm = new Taskman(typeof(Tasks));
    try
    {
      await tm.Run(args);
    }
    catch(TargetInvocationException e)
    {
      if(e.InnerException is ShellException se)
        System.Environment.Exit(se.code);
      else
        throw;
    }
  }
}
