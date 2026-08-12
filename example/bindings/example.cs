using System;
using System.Threading;
using System.IO;
using bhl;

public class Example
{
  public static void Main(string[] args)
  {
    Console.WriteLine("Example started");

    //NOTE: mirrors bhl.proj's `bindings` dict (see that file) just enough to demonstrate
    //      the pattern - a real Unity player build wouldn't need to ship/parse bhl.proj
    //      itself at runtime, just declare the same module names it uses elsewhere.
    //      "example" is what ties this to MyBindings' self-registration under that same name
    var proj = new ProjectConf();
    proj.DeclareBindingsModule("example");

    var types = new Types();
    proj.LoadRuntimeBindings().Register(types);

    var bytes = new MemoryStream(File.ReadAllBytes(args[0]));

    var vm = new VM(types, new ModuleLoader(types, bytes));
    vm.LoadModule("example");
    vm.Start("Unit");

    //NOTE: emulating update game loop
    Time.dt = 0.016f;
    float time_accum = 0;
    while(true)
    {
      vm.Tick();
      Thread.Sleep((int)(Time.dt * 1000));
      time_accum += Time.dt;
      //let's quit after 10 seconds
      if(time_accum > 10)
        break;
    }

    Console.WriteLine("Example finished");
  }
}
