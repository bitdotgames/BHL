using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using bhl;
using bhl.marshall;

public class TestPerf : BHL_TestBase
{
  [IsTested()]
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
      AssertFalse(vm.Tick());
      stopwatch.Stop();
      AssertEqual(fb.result.PopRelease().num, 610);
      Console.WriteLine("fib ticks: {0}", stopwatch.ElapsedTicks);
    }

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      var fb = vm.Start("test");
      AssertFalse(vm.Tick());
      stopwatch.Stop();
      AssertEqual(fb.result.PopRelease().num, 610);
      Console.WriteLine("fib ticks2: {0}", stopwatch.ElapsedTicks);
    }

    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFibonacciImported()
  {
    string fib1 = @"
    import ""fib2""

    func int fib1(int x)
    {
      if(x == 0) {
        return 0
      } else {
        if(x == 1) {
          return 1
        } else {
          return fib2(x - 1) + fib2(x - 2)
        }
      }
    }
    ";

    string fib2 = @"
    import ""fib1""

    func int fib2(int x)
    {
      if(x == 0) {
        return 0
      } else {
        if(x == 1) {
          return 1
        } else {
          return fib1(x - 1) + fib1(x - 2)
        }
      }
    }
    ";

    string test = @"
    import ""fib1""

    func int test() 
    {
      int x = 15 
      return fib1(x)
    }
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"fib1.bhl", fib1},
        {"fib2.bhl", fib2},
        {"test.bhl", test},
      }
    );

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      vm.LoadModule("test");
      var fb = vm.Start("test");
      AssertFalse(vm.Tick());
      stopwatch.Stop();
      AssertEqual(fb.result.PopRelease().num, 610);
      Console.WriteLine("fib imp ticks: {0}", stopwatch.ElapsedTicks);
    }

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      vm.LoadModule("test");
      var fb = vm.Start("test");
      AssertFalse(vm.Tick());
      stopwatch.Stop();
      AssertEqual(fb.result.PopRelease().num, 610);
      Console.WriteLine("fib imp ticks2: {0}", stopwatch.ElapsedTicks);
    }

    CommonChecks(vm);
  }
}
