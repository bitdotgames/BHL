using System;
using System.Threading;
using System.IO;
using bhl;

public class Example
{
  public static void Main(string[] args)
  {
    Console.WriteLine("Bindings gameplay example started");

    var vm = VM.FromBytecode(new MemoryStream(File.ReadAllBytes(args[0])));
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
