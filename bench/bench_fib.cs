using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using bhl;

public class BenchFibonacci : BHL_TestBase
{
  VM.Fiber fb;

  public BenchFibonacci()
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
    fb = vm.Start("test");
  }

  [Benchmark]
  public void FibonacciSimple()
  {
    fb.Tick();
  }
}

public class BenchFibonacciImported : BHL_TestBase
{
  VM.Fiber fb;

  public BenchFibonacciImported()
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

    vm.LoadModule("test");
    fb = vm.Start("test");
  }

  [Benchmark]
  public void FibonacciImported()
  {
    fb.Tick();
  }
}

public class BenchFibonacciMethodImported : BHL_TestBase
{
  VM.Fiber fb;

  public BenchFibonacciMethodImported()
  {
    string fib1 = @"
    import ""fib2""

    class Fib1
    {
      func int fib1(int x, Fib2 f2)
      {
        if(x == 0) {
          return 0
        } else {
          if(x == 1) {
            return 1
          } else {
            return f2.fib2(x - 1, this) + f2.fib2(x - 2, this)
          }
        }
      }
    }
    ";

    string fib2 = @"
    import ""fib1""

    class Fib2
    {
      func int fib2(int x, Fib1 f1)
      {
        if(x == 0) {
          return 0
        } else {
          if(x == 1) {
            return 1
          } else {
            return f1.fib1(x - 1, this) + f1.fib1(x - 2, this)
          }
        }
      }
     }
    ";

    string test = @"
    import ""fib1""
    import ""fib2""

    func int test() 
    {
      var f1 = new Fib1
      var f2 = new Fib2
      int x = 15 
      return f1.fib1(x, f2)
    }
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"fib1.bhl", fib1},
        {"fib2.bhl", fib2},
        {"test.bhl", test},
      }
    );

    vm.LoadModule("test");
    fb = vm.Start("test");
  }

  [Benchmark]
  public void FibonacciMethodImported()
  {
    fb.Tick();
  }
}

public class BenchFibonacciInterfaceImported : BHL_TestBase
{
  VM.Fiber fb;

  public BenchFibonacciInterfaceImported()
  {
    string fib1 = @"
    interface IFib 
    {
      func int fib(int x, IFib f)
    }

    class Fib : IFib
    {
      func int fib(int x, IFib f)
      {
        if(x == 0) {
          return 0
        } else {
          if(x == 1) {
            return 1
          } else {
            return f.fib(x - 1, this) + f.fib(x - 2, this)
          }
        }
      }
    }
    ";

    string test = @"
    import ""fib1""

    func int test() 
    {
      IFib f = new Fib
      int x = 15 
      return f.fib(x, f)
    }
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"fib1.bhl", fib1},
        {"test.bhl", test},
      }
    );

    vm.LoadModule("test");
    fb = vm.Start("test");
  }

  [Benchmark]
  public void FibonacciInterfaceImported()
  {
    fb.Tick();
  }
}
