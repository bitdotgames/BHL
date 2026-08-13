using System;
using System.Collections.Generic;
using System.IO;
using bhl;

public class Example
{
  public static void Main(string[] args)
  {
    Console.WriteLine("Postprocessing example started");

    string dir = args[0];

    var postproc = new ReplaceCallWithConstPostProcessor("GetAnswer", 42);

    var vm = CompilationExecutor.CompileAndLoadVM(
      new List<string> { Path.Combine(dir, "example.bhl") },
      tmp_dir: Path.Combine(dir, "tmp"),
      postproc: postproc,
      verbosity: 1
    ).GetAwaiter().GetResult();

    if(vm == null)
      Environment.Exit(1);

    vm.Execute("Main");

    Console.WriteLine("Example finished");
  }
}
