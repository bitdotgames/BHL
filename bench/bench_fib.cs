using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Diagnosers;
using bhl;
using MoonSharp.Interpreter;
using NLua;

[EventPipeProfiler(EventPipeProfile.CpuSampling)]
public class BenchFibonacciAssorted : BHL_TestBase
{
  VM bhl_vm;
  FuncSymbolScript bhl_fib;

  Script lua_vm;
  DynValue lua_fib;

  Lua nlua_state;
  LuaFunction nlua_fib;

  //FuncSymbolScript fs_imported;
  //FuncSymbolScript fs_class_imported;
  //FuncSymbolScript fs_interface_imported;

  //public BenchFibonacciImported()
  //{
  //  string fib1 = @"
  //  import ""fib2""

  //  func int fib1(int x)
  //  {
  //    if(x == 0) {
  //      return 0
  //    } else {
  //      if(x == 1) {
  //        return 1
  //      } else {
  //        return fib2(x - 1) + fib2(x - 2)
  //      }
  //    }
  //  }

  //  class Fib1
  //  {
  //    func int fib1(int x, Fib2 f2)
  //    {
  //      if(x == 0) {
  //        return 0
  //      } else {
  //        if(x == 1) {
  //          return 1
  //        } else {
  //          return f2.fib2(x - 1, this) + f2.fib2(x - 2, this)
  //        }
  //      }
  //    }
  //  }

  //  interface IFib
  //  {
  //    func int fib(int x, IFib f)
  //  }

  //  class Fib : IFib
  //  {
  //    func int fib(int x, IFib f)
  //    {
  //      if(x == 0) {
  //        return 0
  //      } else {
  //        if(x == 1) {
  //          return 1
  //        } else {
  //          return f.fib(x - 1, this) + f.fib(x - 2, this)
  //        }
  //      }
  //    }
  //  }
  //  ";

  //  string fib2 = @"
  //  import ""fib1""

  //  func int fib2(int x)
  //  {
  //    if(x == 0) {
  //      return 0
  //    } else {
  //      if(x == 1) {
  //        return 1
  //      } else {
  //        return fib1(x - 1) + fib1(x - 2)
  //      }
  //    }
  //  }

  //  class Fib2
  //  {
  //    func int fib2(int x, Fib1 f1)
  //    {
  //      if(x == 0) {
  //        return 0
  //      } else {
  //        if(x == 1) {
  //          return 1
  //        } else {
  //          return f1.fib1(x - 1, this) + f1.fib1(x - 2, this)
  //        }
  //      }
  //    }
  //   }
  //  ";

  //  string test = @"
  //  import ""fib1""
  //  import ""fib2""

  //  func int fib(int x)
  //  {
  //    if(x == 0) {
  //      return 0
  //    } else {
  //      if(x == 1) {
  //        return 1
  //      } else {
  //        return fib(x - 1) + fib(x - 2)
  //      }
  //    }
  //  }

  //  func test_simple()
  //  {
  //    int x = 15
  //    fib(x)
  //  }

  //  func test_imported()
  //  {
  //    int x = 15
  //    fib1(x)
  //  }

  //  func test_class_imported()
  //  {
  //    int x = 15
  //    var f1 = new Fib1
  //    var f2 = new Fib2
  //    f1.fib1(x, f2)
  //  }

  //  func test_interface_imported()
  //  {
  //    int x = 15
  //    IFib f = new Fib
  //    f.fib(x, f)
  //  }
  //  ";

  //  vm = MakeVM(new Dictionary<string, string>() {
  //      {"fib1.bhl", fib1},
  //      {"fib2.bhl", fib2},
  //      {"test.bhl", test},
  //    }
  //  ).GetAwaiter().GetResult();

  //  vm.LoadModule("test");
  //  fs_simple =
  //    (FuncSymbolScript)new VM.SymbolSpec("test", "test_simple").LoadFuncSymbol(vm);
  //  fs_imported =
  //    (FuncSymbolScript)new VM.SymbolSpec("test", "test_imported").LoadFuncSymbol(vm);
  //  fs_class_imported =
  //    (FuncSymbolScript)new VM.SymbolSpec("test", "test_class_imported").LoadFuncSymbol(vm);
  //  fs_interface_imported =
  //    (FuncSymbolScript)new VM.SymbolSpec("test", "test_interface_imported").LoadFuncSymbol(vm);
  //}

  public BenchFibonacciAssorted()
  {
    string test = @"
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

    string lua = @"
    function fib(n)
        if n == 0 then
            return 0
        else
            if n == 1 then
              return 1
            else
              return fib(n - 1) + fib(n - 2)
            end
        end
    end
    ";

    bhl_vm = MakeVM(new Dictionary<string, string>() {
        {"test.bhl", test},
      }
    ).GetAwaiter().GetResult();

    bhl_vm.LoadModule("test");
    bhl_vm.TryFindFuncAddr("fib", out var addr);
    bhl_fib = addr.fs;

    lua_vm = new Script();
    lua_vm.DoString(lua);
    lua_fib = lua_vm.Globals.Get("fib");

    nlua_state = new Lua();
    nlua_state.DoString(lua);
    nlua_fib = nlua_state["fib"] as LuaFunction;
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

  //NOTE: what a CLR backend would emit for a 'coro func int fib(int)' - an IEnumerator with
  //      a return channel, driven by the caller. See spikes/clr/
  public class Ret { public double val; }

  public struct Tick {}

  static IEnumerator<Tick> fib_coro(double x, Ret ret)
  {
    if(x == 0) { ret.val = 0; yield break; }
    if(x == 1) { ret.val = 1; yield break; }

    var r1 = new Ret();
    var co1 = fib_coro(x - 1, r1);
    try { while(co1.MoveNext()) yield return default(Tick); }
    finally { co1.Dispose(); }

    var r2 = new Ret();
    var co2 = fib_coro(x - 2, r2);
    try { while(co2.MoveNext()) yield return default(Tick); }
    finally { co2.Dispose(); }

    ret.val = r1.val + r2.val;
  }

  [Benchmark]
  public void FibonacciCoroStyleCs()
  {
    var ret = new Ret();
    var co = fib_coro(15, ret);
    while(co.MoveNext())
    {}
    co.Dispose();
  }

  [Benchmark]
  public void FibonacciBHL()
  {
    bhl_vm.Execute(bhl_fib, 15);
  }

  [Benchmark]
  public void FibonacciMoonsharp()
  {
    lua_vm.Call(lua_fib, DynValue.NewNumber(15));
  }

  [Benchmark]
  public void FibonacciNLua()
  {
    nlua_fib.Call(15);
  }
}
