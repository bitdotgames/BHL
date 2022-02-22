using System;
using System.Threading;
using System.IO;
using bhl;

public class Example
{
  public static void Main(string[] args)
  {
    Console.WriteLine("Example started");

    var types = new Types();
    var bnd = new MyBindings();
    bnd.Register(types);

    var bytes = new MemoryStream(File.ReadAllBytes("tmp/bhl.bytes"));
    var mi = new ModuleImporter(types, bytes);

    var vm = new VM(types, mi);
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
      if(time_accum > 10)
        break;
    }

    Console.WriteLine("Example finished");
  }
}
