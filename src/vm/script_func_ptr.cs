using System;
using System.Collections.Generic;

namespace bhl {

public class ScriptFuncPtr
{
  ScriptFuncSource pool;

  VM vm;

  VM.FuncAddr addr;

  //NOTE: we manually create and own these 
  VM.Fiber fb;
  VM.Frame fb_frm0;
  VM.Frame frm;

  public FixedStack<Val> Result => fb.result;

  public bool IsStopped => fb.IsStopped();

  public ScriptFuncPtr(ScriptFuncSource pool, VM.FuncAddr addr)
  {
    this.pool = pool;

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

  internal void Init(VM vm, Val args_info, StackList<Val> args)
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
  }

  public bool Tick()
  {
    bool is_running = vm.Tick(fb);

    if(!is_running)
    {
      //let's clear stuff
      frm.Clear();
      fb_frm0.Clear();

      //let's return itself into cache
      pool.cache.Push(this);
    }

    return is_running;
  }
}

public class ScriptFuncSource
{
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
    var fs = (FuncSymbolScript)ms.symbol;

    if(fs.signature.arg_types.Count != args_num)
      throw new Exception($"Arguments amount doesn't match func signature {fs}, got: {args_num}, expected: {fs.signature.arg_types.Count}");

    addr = new VM.FuncAddr() {
      module = ms.module,
      fs = fs,
      ip = fs.ip_addr
    };

    args_info = new Val(null);
    args_info.num = args_num;
    //let's own it forever
    args_info.Retain();
  }

  public ScriptFuncPtr Request(VM vm, StackList<Val> args)
  {
    ScriptFuncPtr cached = null;

    if(cache.Count == 0)
    {
      cached = new ScriptFuncPtr(this, addr);
      ++miss;
    }
    else
      cached = cache.Pop();

    cached.Init(vm, args_info, args);

    return cached;
  }
}

}
