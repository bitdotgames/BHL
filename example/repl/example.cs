using System;
using System.Collections.Generic;
using System.IO;
using bhl;

public class Example
{
  public static void Main(string[] args)
  {
    Console.WriteLine("REPL example started");

    string dir = args[0];

    var vm = CompilationExecutor.CompileAndLoadVM(
      new List<string> { Path.Combine(dir, "example.bhl") },
      tmp_dir: Path.Combine(dir, "tmp"),
      bindings: BindingsRegistry.CreateCombined(),
      verbosity: 1
    ).GetAwaiter().GetResult();

    if(vm == null)
      Environment.Exit(1);

    //NOTE: Main() is a plain, non-coro function - it runs to completion in one call,
    //      no fiber/tick loop needed (that's only required for functions that yield)
    vm.Execute("Main", new FuncArgsInfo(0u), new StackList<Val>());

    string[] preamble = { "unity.Vector3 v = new unity.Vector3{x: 3, y: 4, z: 0}" };
    string expr = "unity.Mathf.Floor(v.x + v.y + v.z)";

    foreach(var stmt in preamble)
      Console.WriteLine($"repl> {stmt}");
    Console.WriteLine($"repl> {expr}");

    var result = vm.EvalExpression(expr, preamble: preamble);
    Console.WriteLine($"repl< {(float)result[0]}");

    Console.WriteLine("Example finished");
  }
}
