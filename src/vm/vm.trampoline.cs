using System;
using System.Collections.Generic;

namespace bhl {

public partial class VM : INamedResolver
{
  public class Trampoline
  {
    VM vm;

    FuncAddr addr;

    //NOTE: we manually create and own these 
    Fiber fb;
    Frame fb_frm0;
    Frame frm;

    public Trampoline(VM vm, SymbolSpec spec)
    {
      var err = vm.TryLoadModuleSymbol(spec, out var ms);
      if(err != 0)
        throw new Exception($"Error loading symbol by spec '{spec}': {err}");

      this.vm = vm;

      //NOTE: only script functions are supported
      var fs = (FuncSymbolScript)ms.symbol;

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

    public Fiber Execute()
    {
      return Execute(0, new StackList<Val>());
    }

    public Fiber Execute(uint cargs_bits, StackList<Val> args)
    {
      fb.Retain();

      fb_frm0.Retain();
      //0 index frame used for return values consistency
      fb.exec.frames.Push(fb_frm0);

      frm.Retain();

      for(int i = args.Count; i-- > 0;)
      {
        var arg = args[i];
        frm._stack.Push(arg);
      }

      //passing args info as stack variable
      frm._stack.Push(Val.NewInt(vm, cargs_bits));

      fb.Attach(frm);

      bool is_running = vm.Tick(fb);
      if(is_running)
        throw new Exception("Not expected state");

      //let's clear stuff
      frm.Clear();
      fb_frm0.Clear();

      return fb;
    }
  }
}

}
