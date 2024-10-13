using System;
using System.Collections.Generic;

namespace bhl {

public partial class VM : INamedResolver
{
  public class Trampoline
  {
    VM vm;

    FuncAddr addr;

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
      //just for consistency
      fb_frm0.Retain();

      //NOTE: manually creating Fiber
      fb = new Fiber(vm);
      //just for consistency
      fb.Retain();
      fb.func_addr = addr;

      //NOTE: manually creating Frame
      frm = new Frame(vm);
      //just for consistency
      frm.Retain();

      frm.Init(fb, fb_frm0, fb_frm0._stack, addr.module, addr.ip);
    }

    public Fiber Start(uint cargs_bits = 0)
    {
      fb.Retain();

      fb_frm0.Retain();
      //0 index frame used for return values consistency
      fb.exec.frames.Push(fb_frm0);

      frm.Retain();

      //TODO: pass args
      //for(int i = args.Count; i-- > 0;)
      //{
      //  var arg = args[i];
      //  frame._stack.Push(arg);
      //}

      //passing args info as stack variable
      frm._stack.Push(Val.NewInt(vm, cargs_bits));

      fb.Attach(frm);

      return fb;
    }
  }
}

}
