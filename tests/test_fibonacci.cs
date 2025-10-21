using System;
using System.Text;
using bhl;
using Xunit;

public class TestFibonacci : BHL_TestBase
{
  [Fact]
  public void Test()
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
    ";

    var vm = MakeVM(bhl, show_bytes: true);
    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      var fb = vm.Start2("fib", Val2.NewInt(vm, 15));
      Assert.False(vm.Tick());
      Console.WriteLine("fib ticks: {0}", stopwatch.ElapsedTicks);
      Assert.Equal(610, fb.exec.stack2.PopRelease().num);
    }

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      var fb = vm.Start2("fib", Val2.NewInt(vm, 15));
      Assert.False(vm.Tick());
      Console.WriteLine("fib ticks2: {0}", stopwatch.ElapsedTicks);
      Assert.Equal(610, fb.exec.stack2.PopRelease().num);
    }
  }
}

