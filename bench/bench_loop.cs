using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using bhl;

// A plain counted loop doing scalar arithmetic - the shape most gameplay code actually has,
// and the one fib doesn't cover: 'i < n' compares and '+' against constants.
public class BenchLoop : BHL_TestBase
{
  VM vm;
  FuncSymbolScript loop;

  [GlobalSetup]
  public void Setup()
  {
    string test = @"
    func int loop(int n)
    {
      int s = 0
      for(int i = 0; i < n; i++) {
        s = s + i * 2 - 1
      }
      return s
    }
    ";

    vm = MakeVM(new Dictionary<string, string>() { {"test.bhl", test} }).GetAwaiter().GetResult();
    vm.LoadModule("test");
    vm.TryFindFuncAddr("loop", out var addr);
    loop = addr.fs;
  }

  static int loop_cs(int n)
  {
    int s = 0;
    for(int i = 0; i < n; i++)
      s = s + i * 2 - 1;
    return s;
  }

  [Benchmark(Baseline = true)]
  public void LoopDotNet()
  {
    loop_cs(1000);
  }

  [Benchmark]
  public void LoopBHL()
  {
    vm.Execute(loop, 1000);
  }
}
