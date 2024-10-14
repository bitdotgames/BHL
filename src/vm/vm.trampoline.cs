using System;
using System.Collections.Generic;

namespace bhl {

public partial class VM : INamedResolver
{
  public class TrampolineBase
  {
    protected VM vm;

    protected FuncAddr addr;

    //NOTE: we manually create and own these 
    protected Fiber fb;
    protected Frame fb_frm0;
    protected Frame frm;

    public struct Result
    {
      Fiber fb;

      public int Count => fb.result.Count;

      public Result(Fiber fb)
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

    public TrampolineBase(VM vm, SymbolSpec spec, int args_num)
    {
      var err = vm.TryLoadModuleSymbol(spec, out var ms);
      if(err != 0)
        throw new Exception($"Error loading symbol by spec '{spec}': {err}");

      this.vm = vm;

      //NOTE: only script functions are supported
      var fs = (FuncSymbolScript)ms.symbol;

      if(fs.signature.arg_types.Count != args_num)
        throw new Exception($"Arguments amount doesn't match func signature, got: {args_num}, expected: {fs.signature.arg_types.Count} in {spec}");

      addr = new FuncAddr() {
        module = ms.module,
        fs = fs,
        ip = fs.ip_addr
      };

      //NOTE: manually creating 0 Frame
      fb_frm0 = new Frame(vm);
      //just for consistency with refcounting
      fb_frm0.Retain();

      //NOTE: manually creating Fiber
      fb = new Fiber(vm);
      //just for consistency with refcounting
      fb.Retain();
      fb.func_addr = addr;

      //NOTE: manually creating Frame
      frm = new Frame(vm);
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

    protected void _Execute()
    {
      fb.Attach(frm);

      bool is_running = vm.Tick(fb);
      if(is_running)
        throw new Exception("Not expected state");

      //let's clear stuff
      frm.Clear();
      fb_frm0.Clear();
    }
  }

  public class Trampoline : TrampolineBase
  {
    public Trampoline(VM vm, SymbolSpec spec, int args_num)
      : base(vm, spec, args_num)
    {}

    public Result Execute()
    {
      return Execute(0, new StackList<Val>());
    }

    public Result Execute(StackList<Val> args)
    {
      return Execute((uint)args.Count, args);
    }

    public Result Execute(uint cargs_bits, StackList<Val> args)
    {
      _Prepare();

      for(int i = args.Count; i-- > 0;)
      {
        var arg = args[i];
        frm._stack.Push(arg);
      }

      //passing args info as stack variable
      frm._stack.Push(Val.NewInt(vm, cargs_bits));

      _Execute();

      return new Result(fb);
    }
  }

  public class Trampoline0 : TrampolineBase
  {
    Val args_info;

    public Trampoline0(VM vm, SymbolSpec spec)
      : base(vm, spec, 0)
    {
      args_info = new Val(vm); 
      args_info.num = 0;

      //let's own it forever
      args_info.Retain();
    }

    public Result Execute()
    {
      _Prepare();

      //passing args info as stack variable
      args_info.Retain();
      frm._stack.Push(args_info);

      _Execute();

      return new Result(fb);
    }
  }

  public class Trampoline1 : TrampolineBase
  {
    Val args_info;

    public Trampoline1(VM vm, SymbolSpec spec)
      : base(vm, spec, 1)
    {
      args_info = new Val(vm); 
      args_info.num = 1;

      //let's own it forever
      args_info.Retain();
    }

    public Result Execute(Val arg1)
    {
      _Prepare();

      frm._stack.Push(arg1);

      //passing args info as stack variable
      args_info.Retain();
      frm._stack.Push(args_info);

      _Execute();

      return new Result(fb);
    }
  }

  public class Trampoline2 : TrampolineBase
  {
    Val args_info;

    public Trampoline2(VM vm, SymbolSpec spec)
      : base(vm, spec, 2)
    {
      args_info = new Val(vm); 
      args_info.num = 2;

      //let's own it forever
      args_info.Retain();
    }

    public Result Execute(Val arg1, Val arg2)
    {
      _Prepare();

      frm._stack.Push(arg2);
      frm._stack.Push(arg1);

      //passing args info as stack variable
      args_info.Retain();
      frm._stack.Push(args_info);

      _Execute();

      return new Result(fb);
    }
  }
}

}
