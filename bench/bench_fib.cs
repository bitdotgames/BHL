using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using bhl;

public class BenchFibonacci : BHL_TestBase
{
  VM vm;
  FuncSymbolScript fs;

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

    func test() 
    {
      int x = 15 
      fib(x)
    }
    ";

    vm = MakeVM(bhl);
    fs = 
      (FuncSymbolScript)new VM.SymbolSpec(TestModuleName, "test").LoadModuleSymbol(vm).symbol;
  }

  [Benchmark]
  public void FibonacciSimple()
  {
    vm.Execute(fs);
  }
}

public class BenchFibonacciImported : BHL_TestBase
{
  VM vm;
  FuncSymbolScript fs;

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

    func test() 
    {
      int x = 15 
      fib1(x)
    }
    ";

    vm = MakeVM(new Dictionary<string, string>() {
        {"fib1.bhl", fib1},
        {"fib2.bhl", fib2},
        {"test.bhl", test},
      }
    );

    vm.LoadModule("test");
    fs = 
      (FuncSymbolScript)new VM.SymbolSpec("test", "test").LoadModuleSymbol(vm).symbol;
  }

  [Benchmark]
  public void FibonacciImported()
  {
    vm.Execute(fs);
  }
}

public class BenchFibonacciMethodImported : BHL_TestBase
{
  VM vm;
  FuncSymbolScript fs;

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

    vm = MakeVM(new Dictionary<string, string>() {
        {"fib1.bhl", fib1},
        {"fib2.bhl", fib2},
        {"test.bhl", test},
      }
    );

    vm.LoadModule("test");
    fs = 
      (FuncSymbolScript)new VM.SymbolSpec("test", "test").LoadModuleSymbol(vm).symbol;
  }

  [Benchmark]
  public void FibonacciMethodImported()
  {
    vm.Execute(fs);
  }
}

public class BenchFibonacciInterfaceImported : BHL_TestBase
{
  VM vm;
  FuncSymbolScript fs;

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

    vm = MakeVM(new Dictionary<string, string>() {
        {"fib1.bhl", fib1},
        {"test.bhl", test},
      }
    );

    vm.LoadModule("test");
    fs = 
      (FuncSymbolScript)new VM.SymbolSpec("test", "test").LoadModuleSymbol(vm).symbol;
  }

  [Benchmark]
  public void FibonacciInterfaceImported()
  {
    vm.Execute(fs);
  }
}
