using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace bhl
{

public static class VMExtensions
{
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public VM.Fiber Start(this VM vm, string func, VM.FiberOptions opts = 0)
  {
    return vm.Start(func, new FuncArgsInfo(0u), new StackList<Val>(), opts);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public VM.Fiber Start(this VM vm, string func, Val arg1, VM.FiberOptions opts = 0)
  {
    return vm.Start(func, new FuncArgsInfo(1u), new StackList<Val>(arg1), opts);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public VM.Fiber Start(this VM vm, string func, Val arg1, Val arg2, VM.FiberOptions opts = 0)
  {
    return vm.Start(func, new FuncArgsInfo(2u), new StackList<Val>(arg1, arg2), opts);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public VM.Fiber Start(this VM vm, string func, Val arg1, Val arg2, Val arg3, VM.FiberOptions opts = 0)
  {
    return vm.Start(func, new FuncArgsInfo(3u), new StackList<Val>(arg1, arg2, arg3), opts);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public VM.Fiber Start(this VM vm, string func, StackList<Val> args, VM.FiberOptions opts = 0)
  {
    return vm.Start(func, new FuncArgsInfo(args.Count), args, opts);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public VM.Fiber Start(this VM vm, string func, FuncArgsInfo args_info, StackList<Val> args,
    VM.FiberOptions opts = 0)
  {
    if(!vm.TryFindFuncAddr(func, out var addr))
      throw new Exception("Could not find function: " + func);

    return vm.Start(addr, args_info, args, opts);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public VM.Fiber Start(this VM vm, FuncSymbolScript fs, StackList<Val> args, VM.FiberOptions opts = 0)
  {
    var addr = new VM.FuncAddr() { module = fs._module, fs = fs, ip = fs._ip_addr };
    return vm.Start(addr, new FuncArgsInfo(args.Count), args, opts);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public ValStack Execute(this VM vm, string func, Val arg1)
  {
    return vm.Execute(func, new FuncArgsInfo(1u), new StackList<Val>(arg1));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public ValStack Execute(this VM vm, string func, FuncArgsInfo args_info, StackList<Val> args)
  {
    if(!vm.TryFindFuncAddr(func, out var addr))
      throw new Exception("Could not find function: " + func);

    return vm.Execute(addr.fs, args_info, ref args);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public ValStack Execute(this VM vm, FuncSymbolScript fs, Val arg1)
  {
    return vm.Execute(fs, new FuncArgsInfo(1u), new StackList<Val>(arg1));
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
  static public string GetStackTrace(this VM.ExecState exec, VM.Error.TraceFormat format = VM.Error.TraceFormat.Compact)
  {
    var trace = new List<VM.TraceItem>();
    exec.GetStackTrace(trace);
    return VM.Error.ToString(trace, format);
  }

  static public string GetStackTrace(this VM.Fiber fiber, VM.Error.TraceFormat format = VM.Error.TraceFormat.Compact)
  {
    return fiber.exec.GetStackTrace(format);
  }
}

}
