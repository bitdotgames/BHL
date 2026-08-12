using System;
using System.IO;
using bhl;

public class Example
{
  public static void Main(string[] args)
  {
    Console.WriteLine("Unity alike bindings example started");

    var vm = VM.FromBytecode(new MemoryStream(File.ReadAllBytes(args[0])));
    vm.LoadModule("example");
    vm.Start("Main");

    while(vm.Tick()) {}

    Console.WriteLine("Example finished");
  }
}
