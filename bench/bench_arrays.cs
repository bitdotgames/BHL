using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using bhl;

//NOTE: a BHL array vs a native list (a NativeListTypeSymbol proxy) on both sides of the bindings:
//      iterated by BHL, passed to a native function and read there through IList<T>
[MemoryDiagnoser]
public class BenchArrays : BHL_TestBase
{
  const int N = 1000;

  VM vm;
  FuncSymbolScript bhl_foreach_bhl_arr;
  FuncSymbolScript bhl_foreach_native_list;
  FuncSymbolScript bhl_native_sum_bhl_arr;
  FuncSymbolScript bhl_native_sum_native_list;

  ValList bhl_arr;
  List<int> native_list = new List<int>();
  NativeListTypeSymbol<int> list_int;

  public BenchArrays()
  {
    string bhl = @"
    func int ForeachBhlArray()
    {
      []int a = GetBhlArray()
      int s = 0
      foreach(int v in a) {
        s = s + v
      }
      return s
    }

    func int ForeachNativeList()
    {
      []int a = GetNativeList()
      int s = 0
      foreach(int v in a) {
        s = s + v
      }
      return s
    }

    func int NativeSumBhlArray()
    {
      return NativeSum(GetBhlArray())
    }

    func int NativeSumNativeList()
    {
      return NativeSum(GetNativeList())
    }
    ";

    for(int i = 0; i < N; ++i)
      native_list.Add(i);

    IType arr_type = null;
    vm = MakeVM(bhl, ts =>
    {
      list_int = NativeListTypeSymbol.DefineOnce<int>(ts.ns, "List_int", (v) => (int)v.num, (itype, n) => Val.NewInt(n), Types.Int);

      ts.ns.Define(new FuncSymbolNative(new Origin(), "GetBhlArray", ts.TArr("int"),
        (exec, args_info) =>
        {
          arr_type ??= ts.TArr("int").Get();
          bhl_arr.Retain();
          exec.stack.Push(Val.NewObj(bhl_arr, arr_type));
          return null;
        }
      ));

      ts.ns.Define(new FuncSymbolNative(new Origin(), "GetNativeList", ts.TArr("int"),
        (exec, args_info) =>
        {
          var v = new Val();
          list_int.SetList(ref v, native_list);
          exec.stack.Push(v);
          return null;
        }
      ));

      //NOTE: as generated bindings read an IList<int> argument
      ts.ns.Define(new FuncSymbolNative(new Origin(), "NativeSum", ts.T("int"),
        (exec, args_info) =>
        {
          var dv = exec.stack.Pop();
          IList<int> xs;
          if(dv.obj is ValList vl)
            xs = ValList<int>.New(vl, list_int.Val2Native, dv.type);
          else
            xs = (IList<int>)dv.obj;
          dv._refc?.Retain();
          dv._refc?.Release();

          exec.stack.Push(Val.NewInt(SumIndexed(xs)));

          (xs as IRefcounted)?.Release();
          return null;
        },
        new FuncArgSymbol("xs", ts.TArr("int"))
      ));
    });

    bhl_arr = ValList.New(vm);
    for(int i = 0; i < N; ++i)
      bhl_arr.Add(Val.NewInt(i));

    bhl_foreach_bhl_arr = Find("ForeachBhlArray");
    bhl_foreach_native_list = Find("ForeachNativeList");
    bhl_native_sum_bhl_arr = Find("NativeSumBhlArray");
    bhl_native_sum_native_list = Find("NativeSumNativeList");

    //NOTE: all the variants sum the same items
    int expected = N * (N - 1) / 2;
    foreach(var fs in new[] { bhl_foreach_bhl_arr, bhl_foreach_native_list, bhl_native_sum_bhl_arr, bhl_native_sum_native_list })
    {
      int res = (int)vm.Execute(fs).Pop().num;
      if(res != expected)
        throw new Exception($"{fs.name}: {res} != {expected}");
    }
  }

  FuncSymbolScript Find(string name)
  {
    if(!vm.TryFindFuncAddr(name, out var addr))
      throw new Exception(name + " not found");
    return addr.fs;
  }

  static int SumIndexed(IList<int> xs)
  {
    int s = 0;
    for(int i = 0; i < xs.Count; ++i)
      s += xs[i];
    return s;
  }

  static int SumForeach(IList<int> xs)
  {
    int s = 0;
    foreach(var x in xs)
      s += x;
    return s;
  }

  //////////////////////////// BHL iterates

  [Benchmark]
  public int BhlForeachBhlArray() => (int)vm.Execute(bhl_foreach_bhl_arr).Pop().num;

  [Benchmark]
  public int BhlForeachNativeList() => (int)vm.Execute(bhl_foreach_native_list).Pop().num;

  //////////////////////////// BHL passes the array to a native function

  [Benchmark]
  public int BhlNativeSumBhlArray() => (int)vm.Execute(bhl_native_sum_bhl_arr).Pop().num;

  [Benchmark]
  public int BhlNativeSumNativeList() => (int)vm.Execute(bhl_native_sum_native_list).Pop().num;

  //////////////////////////// C# reads IList<int>: an adapter of a BHL array or a native list

  [Benchmark(Baseline = true)]
  public int CsIndexNativeList() => SumIndexed(native_list);

  [Benchmark]
  public int CsIndexAdapter()
  {
    var xs = ValList<int>.New(bhl_arr, list_int.Val2Native);
    bhl_arr.Retain();
    int s = SumIndexed(xs);
    xs.Release();
    return s;
  }

  [Benchmark]
  public int CsForeachNativeList() => SumForeach(native_list);

  [Benchmark]
  public int CsForeachAdapter()
  {
    var xs = ValList<int>.New(bhl_arr, list_int.Val2Native);
    bhl_arr.Retain();
    int s = SumForeach(xs);
    xs.Release();
    return s;
  }
}
