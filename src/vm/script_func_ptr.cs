using System;
using System.Collections.Generic;

namespace bhl {

public class ScriptFuncPtr
{
  ScriptFuncSource pool;

  VM vm;
  public VM VM => vm;

  FuncSymbolScript func_symbol;
  public FuncSymbolScript Symbol => func_symbol;
  public Module Module => func_symbol._module;

  VM.FuncAddr addr;
  public VM.FuncAddr Addr => addr;

  //NOTE: we manually create and own these
  VM.Fiber fb;
  VM.Frame fb_frm0;
  VM.Frame frm;

  public VM.Fiber Fiber => fb;
  public FixedStack<Val> Result => fb.result;
  public BHS Status => fb.status;

  public bool IsStopped => fb.IsStopped();

  //for simple refcounting
  int refs;

  public ScriptFuncPtr(FuncSymbolScript fs, VM.FuncAddr addr, ScriptFuncSource pool = null)
  {
    this.pool = pool;

    this.func_symbol = fs;

    this.addr = addr;

    //NOTE: manually creating 0 Frame
    fb_frm0 = new VM.Frame(null);
    //just for consistency with refcounting
    fb_frm0.Retain();

    //NOTE: manually creating Fiber
    fb = new VM.Fiber(null);
    //just for consistency with refcounting
    fb.Retain();
    fb.func_addr = addr;

    //NOTE: manually creating Frame
    frm = new VM.Frame(null);
    //just for consistency with refcounting
    frm.Retain();
  }

  public void GetStackTrace(List<VM.TraceItem> info)
  {
    fb.GetStackTrace(info);
  }

  public void Retain()
  {
    if(refs == -1)
      throw new Exception("Invalid state");

    ++refs;
  }

  public void Release()
  {
    if(refs == -1)
      throw new Exception("Invalid state");

    if(refs > 0)
      --refs;

    TryClear();
  }

  public void Reset(VM vm, Val args_info, StackList<Val> args)
  {
    this.vm = vm;

    frm.vm = vm;
    fb_frm0.vm = vm;
    fb.vm = vm;

    fb.Retain();

    fb_frm0.Retain();
    //0 index frame used for return values consistency
    fb.exec.frames.Push(fb_frm0);

    frm.Retain();

    frm.Init(fb, fb_frm0, fb_frm0._stack, addr.module, addr.ip);

    for(int i = args.Count; i-- > 0;)
    {
      var arg = args[i];
      frm._stack.Push(arg);
    }

    //passing args info as stack variable
    args_info.Retain();
    frm._stack.Push(args_info);

    fb.Attach(frm);

    //NOTE: no extra references default mode,
    //      will be auto cleared upon last tick
    refs = 0;
  }

  public bool Tick()
  {
    bool is_running = vm.Tick(fb);

    if(!is_running)
      TryClear();

    return is_running;
  }

  public void Execute()
  {
    if(Tick())
      throw new Exception($"Not expected to be running: {func_symbol}");
  }

  public void Stop()
  {
    vm.Stop(fb);

    TryClear();
  }

  //NOTE: auto clearing happens only if there are no extra references
  void TryClear()
  {
    if(refs != 0)
      return;

    //let's clear stuff
    frm.Clear();
    fb_frm0.Clear();

    //let's return itself into cache
    pool?.cache.Push(this);

    refs = -1;
  }
}

public class ScriptFuncSource
{
  FuncSymbolScript func_symbol;

  VM.FuncAddr addr;

  Val args_info;

  internal Stack<ScriptFuncPtr> cache = new Stack<ScriptFuncPtr>();
  int miss;

  public int IdleCount {
    get { return cache.Count; }
  }

  public int BusyCount {
    get { return miss - IdleCount; }
  }

  public ScriptFuncSource(VM vm, VM.SymbolSpec spec, int args_num)
    : this(spec.LoadModuleSymbol(vm), args_num)
  {}

  public ScriptFuncSource(VM.ModuleSymbol ms, int args_num)
  {
    //NOTE: only script functions are supported
    func_symbol = (FuncSymbolScript)ms.symbol;

    if(func_symbol.signature.arg_types.Count != args_num)
      throw new Exception($"Arguments amount doesn't match func signature {func_symbol}, got: {args_num}, expected: {func_symbol.signature.arg_types.Count}");

    addr = new VM.FuncAddr() {
      module = ms.module,
      fs = func_symbol,
      ip = func_symbol.ip_addr
    };

    args_info = new Val(null);
    args_info.num = args_num;
    //let's own it forever
    args_info.Retain();
  }

  public ScriptFuncPtr Request(VM vm, StackList<Val> args)
  {
    ScriptFuncPtr ptr = null;

    if(cache.Count == 0)
    {
      ptr = new ScriptFuncPtr(func_symbol, addr, this);
      ++miss;
    }
    else
      ptr = cache.Pop();

    ptr.Reset(vm, args_info, args);

    return ptr;
  }
}

}
