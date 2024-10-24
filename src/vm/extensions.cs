
namespace bhl {

public static class VMExtensions
{
  static public VM.Fiber Start(this VM vm, string func)
  {
    return vm.Start(func, 0u, new StackList<Val>());
  }

  static public VM.Fiber Start(this VM vm, string func, Val arg1)
  {
    return vm.Start(func, 1u, new StackList<Val>(arg1));
  }

  static public VM.Fiber Start(this VM vm, string func, Val arg1, Val arg2)
  {
    return vm.Start(func, 2u, new StackList<Val>(arg1, arg2));
  }

  static public VM.Fiber Start(this VM vm, string func, Val arg1, Val arg2, Val arg3)
  {
    return vm.Start(func, 3u, new StackList<Val>(arg1, arg2, arg3));
  }

  static public VM.Fiber Start(this VM vm, string func, StackList<Val> args)
  {
    return vm.Start(func, args.Count, args);
  }

  static public VM.Fiber Start(this VM vm, string func, FuncArgsInfo args_info, StackList<Val> args, VM.FiberOptions opts = 0)
  {
    if(!vm.TryFindFuncAddr(func, out var addr))
      return null;

    return vm.Start(addr, args_info, args, opts);
  }

  static public VM.Fiber Start(this VM vm, FuncSymbolScript fs, StackList<Val> args, VM.FiberOptions opts = 0)
  {
    var addr = new VM.FuncAddr() { module = fs._module, fs = fs, ip = fs.ip_addr };
    return vm.Start(addr, args.Count, args, opts);
  }
}

}