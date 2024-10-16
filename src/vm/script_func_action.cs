using System;

namespace bhl {

public class ScriptFuncActionBase
{
  protected VM.FuncAddr addr;

  //NOTE: we manually create and own these 
  protected VM.Fiber fb;
  protected VM.Frame fb_frm0;
  protected VM.Frame frm;

  public struct Result
  {
    VM.Fiber fb;

    public int Count => fb.result.Count;

    public Result(VM.Fiber fb)
    {
      this.fb = fb;
    }

    public Val Pop()
    {
      return fb.result.Pop();
    }

    public Val PopRelease()
    {
      return fb.result.PopRelease();
    }
  }

  public ScriptFuncActionBase(VM vm, VM.SymbolSpec spec, int args_num)
    : this(spec.LoadModuleSymbol(vm), args_num)
  {}

  public ScriptFuncActionBase(VM.ModuleSymbol ms, int args_num)
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

    //NOTE: manually creating 0 VM.Frame
    fb_frm0 = new VM.Frame(null);
    //just for consistency with refcounting
    fb_frm0.Retain();

    //NOTE: manually creating VM.Fiber
    fb = new VM.Fiber(null);
    //just for consistency with refcounting
    fb.Retain();
    fb.func_addr = addr;

    //NOTE: manually creating VM.Frame
    frm = new VM.Frame(null);
    //just for consistency with refcounting
    frm.Retain();

    frm.Init(fb, fb_frm0, fb_frm0._stack, addr.module, addr.ip);
  }

  protected void _Prepare()
  {
    fb.Retain();

    fb_frm0.Retain();
    //0 index frame used for return values consistency
    fb.exec.frames.Push(fb_frm0);

    frm.Retain();
  }

  protected void _Execute(VM vm)
  {
    frm.vm = vm;
    fb_frm0.vm = vm;
    fb.vm = vm;

    fb.Attach(frm);

    if(vm.Tick(fb))
      throw new Exception("Not expected state to be running");

    //let's clear stuff
    frm.Clear();
    fb_frm0.Clear();
  }
}

public class ScriptFuncAction : ScriptFuncActionBase
{
  public ScriptFuncAction(VM vm, VM.SymbolSpec spec, int args_num)
    : base(vm, spec, args_num)
  {}

  public Result Execute(VM vm)
  {
    return Execute(vm, 0, new StackList<Val>());
  }

  public Result Execute(VM vm, StackList<Val> args)
  {
    return Execute(vm, (uint)args.Count, args);
  }

  public Result Execute(VM vm, uint cargs_bits, StackList<Val> args)
  {
    _Prepare();

    for(int i = args.Count; i-- > 0;)
    {
      var arg = args[i];
      frm._stack.Push(arg);
    }

    //passing args info as stack variable
    frm._stack.Push(Val.NewInt(vm, cargs_bits));

    _Execute(vm);

    return new Result(fb);
  }
}

public class ScriptFuncAction0 : ScriptFuncActionBase
{
  Val args_info;

  public ScriptFuncAction0(VM vm, VM.SymbolSpec spec)
    : base(vm, spec, 0)
  {
    args_info = new Val(null); 
    args_info.num = 0;
    //let's own it forever
    args_info.Retain();
  }

  public Result Execute(VM vm)
  {
    _Prepare();

    //passing args info as stack variable
    args_info.Retain();
    frm._stack.Push(args_info);

    _Execute(vm);

    return new Result(fb);
  }
}

public class ScriptFuncAction1 : ScriptFuncActionBase
{
  Val args_info;

  public ScriptFuncAction1(VM vm, VM.SymbolSpec spec)
    : base(vm, spec, 1)
  {
    args_info = new Val(null); 
    args_info.num = 1;

    //let's own it forever
    args_info.Retain();
  }

  public Result Execute(VM vm, Val arg1)
  {
    _Prepare();

    frm._stack.Push(arg1);

    //passing args info as stack variable
    args_info.Retain();
    frm._stack.Push(args_info);

    _Execute(vm);

    return new Result(fb);
  }
}

public class ScriptFuncAction2 : ScriptFuncActionBase
{
  Val args_info;

  public ScriptFuncAction2(VM vm, VM.SymbolSpec spec)
    : base(vm, spec, 2)
  {
    args_info = new Val(null); 
    args_info.num = 2;

    //let's own it forever
    args_info.Retain();
  }

  public Result Execute(VM vm, Val arg1, Val arg2)
  {
    _Prepare();

    frm._stack.Push(arg2);
    frm._stack.Push(arg1);

    //passing args info as stack variable
    args_info.Retain();
    frm._stack.Push(args_info);

    _Execute(vm);

    return new Result(fb);
  }
}

public class ScriptFuncAction3 : ScriptFuncActionBase
{
  Val args_info;

  public ScriptFuncAction3(VM vm, VM.SymbolSpec spec)
    : base(vm, spec, 3)
  {
    args_info = new Val(null); 
    args_info.num = 3;

    //let's own it forever
    args_info.Retain();
  }

  public Result Execute(VM vm, Val arg1, Val arg2, Val arg3)
  {
    _Prepare();

    frm._stack.Push(arg3);
    frm._stack.Push(arg2);
    frm._stack.Push(arg1);

    //passing args info as stack variable
    args_info.Retain();
    frm._stack.Push(args_info);

    _Execute(vm);

    return new Result(fb);
  }
}

public class ScriptFuncAction4 : ScriptFuncActionBase
{
  Val args_info;

  public ScriptFuncAction4(VM vm, VM.SymbolSpec spec)
    : base(vm, spec, 4)
  {
    args_info = new Val(null); 
    args_info.num = 4;

    //let's own it forever
    args_info.Retain();
  }

  public Result Execute(VM vm, Val arg1, Val arg2, Val arg3, Val arg4)
  {
    _Prepare();

    frm._stack.Push(arg4);
    frm._stack.Push(arg3);
    frm._stack.Push(arg2);
    frm._stack.Push(arg1);

    //passing args info as stack variable
    args_info.Retain();
    frm._stack.Push(args_info);

    _Execute(vm);

    return new Result(fb);
  }
}

public class ScriptFuncAction5 : ScriptFuncActionBase
{
  Val args_info;

  public ScriptFuncAction5(VM vm, VM.SymbolSpec spec)
    : base(vm, spec, 5)
  {
    args_info = new Val(null); 
    args_info.num = 5;

    //let's own it forever
    args_info.Retain();
  }

  public Result Execute(VM vm, Val arg1, Val arg2, Val arg3, Val arg4, Val arg5)
  {
    _Prepare();

    frm._stack.Push(arg5);
    frm._stack.Push(arg4);
    frm._stack.Push(arg3);
    frm._stack.Push(arg2);
    frm._stack.Push(arg1);

    //passing args info as stack variable
    args_info.Retain();
    frm._stack.Push(args_info);

    _Execute(vm);

    return new Result(fb);
  }
}

public class ScriptFuncAction6 : ScriptFuncActionBase
{
  Val args_info;

  public ScriptFuncAction6(VM vm, VM.SymbolSpec spec)
    : base(vm, spec, 6)
  {
    args_info = new Val(null); 
    args_info.num = 6;

    //let's own it forever
    args_info.Retain();
  }

  public Result Execute(VM vm, Val arg1, Val arg2, Val arg3, Val arg4, Val arg5, Val arg6)
  {
    _Prepare();

    frm._stack.Push(arg6);
    frm._stack.Push(arg5);
    frm._stack.Push(arg4);
    frm._stack.Push(arg3);
    frm._stack.Push(arg2);
    frm._stack.Push(arg1);

    //passing args info as stack variable
    args_info.Retain();
    frm._stack.Push(args_info);

    _Execute(vm);

    return new Result(fb);
  }
}

public class ScriptFuncAction7 : ScriptFuncActionBase
{
  Val args_info;

  public ScriptFuncAction7(VM vm, VM.SymbolSpec spec)
    : base(vm, spec, 7)
  {
    args_info = new Val(null); 
    args_info.num = 7;

    //let's own it forever
    args_info.Retain();
  }

  public Result Execute(VM vm, Val arg1, Val arg2, Val arg3, Val arg4, Val arg5, Val arg6, Val arg7)
  {
    _Prepare();

    frm._stack.Push(arg7);
    frm._stack.Push(arg6);
    frm._stack.Push(arg5);
    frm._stack.Push(arg4);
    frm._stack.Push(arg3);
    frm._stack.Push(arg2);
    frm._stack.Push(arg1);

    //passing args info as stack variable
    args_info.Retain();
    frm._stack.Push(args_info);

    _Execute(vm);

    return new Result(fb);
  }
}

public class ScriptFuncAction8 : ScriptFuncActionBase
{
  Val args_info;

  public ScriptFuncAction8(VM vm, VM.SymbolSpec spec)
    : base(vm, spec, 8)
  {
    args_info = new Val(null); 
    args_info.num = 8;

    //let's own it forever
    args_info.Retain();
  }

  public Result Execute(VM vm, Val arg1, Val arg2, Val arg3, Val arg4, Val arg5, Val arg6, Val arg7, Val arg8)
  {
    _Prepare();

    frm._stack.Push(arg8);
    frm._stack.Push(arg7);
    frm._stack.Push(arg6);
    frm._stack.Push(arg5);
    frm._stack.Push(arg4);
    frm._stack.Push(arg3);
    frm._stack.Push(arg2);
    frm._stack.Push(arg1);

    //passing args info as stack variable
    args_info.Retain();
    frm._stack.Push(args_info);

    _Execute(vm);

    return new Result(fb);
  }
}

public class ScriptFuncAction9 : ScriptFuncActionBase
{
  Val args_info;

  public ScriptFuncAction9(VM vm, VM.SymbolSpec spec)
    : base(vm, spec, 9)
  {
    args_info = new Val(null); 
    args_info.num = 9;

    //let's own it forever
    args_info.Retain();
  }

  public Result Execute(VM vm, Val arg1, Val arg2, Val arg3, Val arg4, Val arg5, Val arg6, Val arg7, Val arg8, Val arg9)
  {
    _Prepare();

    frm._stack.Push(arg9);
    frm._stack.Push(arg8);
    frm._stack.Push(arg7);
    frm._stack.Push(arg6);
    frm._stack.Push(arg5);
    frm._stack.Push(arg4);
    frm._stack.Push(arg3);
    frm._stack.Push(arg2);
    frm._stack.Push(arg1);

    //passing args info as stack variable
    args_info.Retain();
    frm._stack.Push(args_info);

    _Execute(vm);

    return new Result(fb);
  }
}

public class ScriptFuncAction10 : ScriptFuncActionBase
{
  Val args_info;

  public ScriptFuncAction10(VM vm, VM.SymbolSpec spec)
    : base(vm, spec, 10)
  {
    args_info = new Val(null); 
    args_info.num = 10;

    //let's own it forever
    args_info.Retain();
  }

  public Result Execute(VM vm, Val arg1, Val arg2, Val arg3, Val arg4, Val arg5, Val arg6, Val arg7, Val arg8, Val arg9, Val arg10)
  {
    _Prepare();

    frm._stack.Push(arg10);
    frm._stack.Push(arg9);
    frm._stack.Push(arg8);
    frm._stack.Push(arg7);
    frm._stack.Push(arg6);
    frm._stack.Push(arg5);
    frm._stack.Push(arg4);
    frm._stack.Push(arg3);
    frm._stack.Push(arg2);
    frm._stack.Push(arg1);

    //passing args info as stack variable
    args_info.Retain();
    frm._stack.Push(args_info);

    _Execute(vm);

    return new Result(fb);
  }
}

}
