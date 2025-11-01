using System;

namespace bhl
{

public static class VMExtensions
{
  static public VM.Fiber Start(this VM vm, string func)
  {
    return vm.Start(func, new FuncArgsInfo(0u), new StackList<Val>());
  }

  static public VM.Fiber Start(this VM vm, string func, Val arg1)
  {
    return vm.Start(func, new FuncArgsInfo(1u), new StackList<Val>(arg1));
  }

  static public VM.Fiber Start(this VM vm, string func, Val arg1, Val arg2)
  {
    return vm.Start(func, new FuncArgsInfo(2u), new StackList<Val>(arg1, arg2));
  }

  static public VM.Fiber Start(this VM vm, string func, Val arg1, Val arg2, Val arg3)
  {
    return vm.Start(func, new FuncArgsInfo(3u), new StackList<Val>(arg1, arg2, arg3));
  }

  static public VM.Fiber Start(this VM vm, string func, StackList<Val> args)
  {
    return vm.Start(func, new FuncArgsInfo(args.Count), args);
  }

  static public VM.Fiber Start(this VM vm, string func, FuncArgsInfo args_info, StackList<Val> args,
    VM.FiberOptions opts = 0)
  {
    if(!vm.TryFindFuncAddr(func, out var addr))
      return null;

    return vm.Start(addr, args_info, args, opts);
  }

  static public VM.Fiber Star(this VM vm, FuncSymbolScript fs, StackList<Val> args, VM.FiberOptions opts = 0)
  {
    var addr = new VM.FuncAddr() { module = fs._module, fs = fs, ip = fs.ip_addr };
    return vm.Start(addr, new FuncArgsInfo(args.Count), args, opts);
  }

  static public void Alloc(this Pool<ValOld> pool, VM vm, int num)
  {
    for(int i = 0; i < num; ++i)
    {
      ++pool.miss;
      var tmp = new ValOld(vm);
      pool.stack.Push(tmp);
    }
  }

  static public string Dump(this Pool<ValOld> pool)
  {
    string res = "=== Val POOL ===\n";
    res += "busy:" + pool.BusyCount + " idle:" + pool.IdleCount + "\n";

    var dvs = new ValOld[pool.stack.Count];
    pool.stack.CopyTo(dvs, 0);
    for(int i = dvs.Length; i-- > 0;)
    {
      var v = dvs[i];
      res += v + " (refs:" + v._refs + ") " + v.GetHashCode() + "\n";
    }

    return res;
  }

  static public VM.ModuleSymbol LoadModuleSymbol(this VM.SymbolSpec spec, VM vm)
  {
    var err = vm.TryLoadModuleSymbol(spec, out var ms);
    if(err != 0)
      throw new Exception($"Error loading symbol by spec '{spec}': {err}");
    return ms;
  }

  static public FuncSymbol LoadFuncSymbol(this VM.SymbolSpec spec, VM vm)
  {
    return (FuncSymbol)spec.LoadModuleSymbol(vm).symbol;
  }
}

}
