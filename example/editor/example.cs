using System;
using System.Collections.Generic;
using System.IO;
using bhl;

public class Example
{
  public static void Main(string[] args)
  {
    Console.WriteLine("SDK alike example started");

    string dir = args[0];

    var vm = CompilationExecutor.CompileAndLoadVM(
      new List<string> { Path.Combine(dir, "example.bhl") },
      tmp_dir: Path.Combine(dir, "tmp"),
      bindings: BindingsRegistry.CreateCombined(),
      verbosity: 1
    ).GetAwaiter().GetResult();

    if(vm == null)
      Environment.Exit(1);

    vm.Start("Main");
    while(vm.Tick()) {}

    Console.WriteLine("Example finished");
  }
}
