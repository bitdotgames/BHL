using System;
using System.Threading;
using System.IO;
using bhl;

public class Example
{
  public static void Main(string[] args)
  {
    var types = new TypeSystem();
    var bnd = new MyBindings();
    bnd.Register(types);

    var bytes = new MemoryStream(File.ReadAllBytes("tmp/bhl.bytes"));
    var mi = new ModuleImporter(bytes);

    var vm = new VM(types, mi);
    vm.LoadModule("unit");
    vm.Start("Unit");

    //NOTE: emulating update game loop
    Time.dt = 0.016f;
    while(true)
    {
      vm.Tick();
      Thread.Sleep(16);
    }
  }
}
