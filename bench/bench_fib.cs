using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using bhl;

public class BenchFibonacciImported : BHL_TestBase
{
  VM vm;
  FuncSymbolScript fs_simple;
  FuncSymbolScript fs_imported;
  FuncSymbolScript fs_class_imported;
  FuncSymbolScript fs_interface_imported;

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

    func test_simple() 
    {
      int x = 15 
      fib(x)
    }

    func test_imported() 
    {
      int x = 15 
      fib1(x)
    }

    func test_class_imported() 
    {
      int x = 15 
      var f1 = new Fib1
      var f2 = new Fib2
      f1.fib1(x, f2)
    }

    func test_interface_imported() 
    {
      int x = 15 
      IFib f = new Fib
      f.fib(x, f)
    }
    ";

    vm = MakeVM(new Dictionary<string, string>() {
        {"fib1.bhl", fib1},
        {"fib2.bhl", fib2},
        {"test.bhl", test},
      }
    );

    vm.LoadModule("test");
    fs_simple = 
      (FuncSymbolScript)new VM.SymbolSpec("test", "test_simple").LoadFuncSymbol(vm);
    fs_imported = 
      (FuncSymbolScript)new VM.SymbolSpec("test", "test_imported").LoadFuncSymbol(vm);
    fs_class_imported = 
      (FuncSymbolScript)new VM.SymbolSpec("test", "test_class_imported").LoadFuncSymbol(vm);
    fs_interface_imported = 
      (FuncSymbolScript)new VM.SymbolSpec("test", "test_interface_imported").LoadFuncSymbol(vm);
  }

  static int fib(int x)
  {
    if(x == 0) {
      return 0;
    } else {
      if(x == 1) {
        return 1;
      } else {
        return fib(x - 1) + fib(x - 2);
      }
    }
  }

  [Benchmark(Baseline = true)]
  public void FibonacciDotNet()
  {
    fib(15);
  }

  [Benchmark]
  public void FibonacciSimple()
  {
    vm.Execute(fs_simple);
  }

  [Benchmark]
  public void FibonacciImported()
  {
    vm.Execute(fs_imported);
  }

  [Benchmark]
  public void FibonacciClassImported()
  {
    vm.Execute(fs_class_imported);
  }

  [Benchmark]
  public void FibonacciInterfaceImported()
  {
    vm.Execute(fs_interface_imported);
  }
}