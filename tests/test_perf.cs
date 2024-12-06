using System;
using bhl;
using Xunit;

public class TestPerf : BHL_TestBase
{
  [Fact]
  public void TestFibonacci()
  {
    string bhl = @"

    func int fib(int x)
    {
      if(x == 0) {
        return 0
      } else {
        if(x == 1) {
          return 1
        } else {
          return fib(x - 1) + fib(x - 2)
        }
      }
    }

    func int test() 
    {
      int x = 15 
      return fib(x)
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      var fb = vm.Start("test");
      Assert.False(vm.Tick());
      stopwatch.Stop();
      Assert.Equal(610, fb.result.PopRelease().num);
      Console.WriteLine("fib ticks: {0}", stopwatch.ElapsedTicks);
    }

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      var fb = vm.Start("test");
      Assert.False(vm.Tick());
      stopwatch.Stop();
      Assert.Equal(610, fb.result.PopRelease().num);
      Console.WriteLine("fib ticks2: {0}", stopwatch.ElapsedTicks);
    }

    CommonChecks(vm);
  }
}
