using System;

namespace bhl
{

public static class Prelude
{
  static public FuncSymbolNative YieldFunc = null;

  static public FuncSymbolNative DumpOpcodesOn = null;
  static public FuncSymbolNative DumpOpcodesOff = null;

  public static void Define(Module m)
  {
    {
      //NOTE: it's a builtin non-directly available function
      var fn = new FuncSymbolNative(new Origin(), "$yield", Types.Void,
        (VM.ExecState exec, ValStack stack, FuncArgsInfo args_info, ref BHS status) =>
        {
          return CoroutinePool.New<CoroutineYield>(exec.vm);
        }
      );
      YieldFunc = fn;
      m.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "suspend", FuncAttrib.Coro, Types.Void, 0,
        (VM.ExecState exec, ValStack stack, FuncArgsInfo args_info, ref BHS status) =>
        {
          //TODO: use static instance for this case?
          return CoroutinePool.New<CoroutineSuspend>(exec.vm);
        }
      );
      m.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "wait", FuncAttrib.Coro, Types.Void, 0,
        (VM.ExecState exec, ValStack stack, FuncArgsInfo args_info, ref BHS status) =>
        {
          return CoroutinePool.New<CoroutineWait>(exec.vm);
        },
        new FuncArgSymbol("ms", Types.Int)
      );
      m.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "start", m.ts.T(Types.FiberRef),
        (VM.ExecState exec, ValStack stack, FuncArgsInfo args_info, ref BHS status) =>
        {
          throw new NotImplementedException();
          //var val_ptr = stack.Pop();
          //var fb = exec.vm.Start((VM.FuncPtr)val_ptr._obj, null);
          //val_ptr.Release();
          //stack.Push(VM.FiberRef.Encode(exec.vm, fb));
          return null;
        },
        new FuncArgSymbol("p", m.TFunc(true /*is coro*/, "void"))
      );
      m.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "stop", Types.Void,
        (VM.ExecState exec, ValStack stack, FuncArgsInfo args_info, ref BHS status) =>
        {
          throw new NotImplementedException();
          //var val = stack.Pop();
          //var fb_ref = new VM.FiberRef(val);
          //var fb = fb_ref.Get();
          //fb?.Stop();
          //fb?.Release();
          return null;
        },
        new FuncArgSymbol("fb", m.ts.T(Types.FiberRef))
      );
      m.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "debugger", Types.Void,
        (VM.ExecState exec, ValStack stack, FuncArgsInfo args_info, ref BHS status) =>
        {
          System.Diagnostics.Debugger.Break();
          return null;
        }
      );
      m.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "__dump_opcodes_on", Types.Void,
        (VM.ExecState exec, ValStack stack, FuncArgsInfo args_info, ref BHS status) =>
        {
          return null;
        }
      );
      DumpOpcodesOn = fn;
      m.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "__dump_opcodes_off", Types.Void,
        (VM.ExecState exec, ValStack stack, FuncArgsInfo args_info, ref BHS status) =>
        {
          return null;
        }
      );
      DumpOpcodesOff = fn;
      m.ns.Define(fn);
    }
  }
}

class CoroutineSuspend : Coroutine
{
  public override void Tick(VM.FrameOld frm, VM.ExecState exec, ref BHS status)
  {
    status = BHS.RUNNING;
  }
}

class CoroutineYield : Coroutine
{
  bool first_time = true;

  public override void Tick(VM.FrameOld frm, VM.ExecState exec, ref BHS status)
  {
    if(first_time)
    {
      status = BHS.RUNNING;
      first_time = false;
    }
  }

  public override void Cleanup(VM.FrameOld frm, VM.ExecState exec)
  {
    first_time = true;
  }
}

class CoroutineWait : Coroutine
{
  int end_stamp = -1;

  public override void Tick(VM.FrameOld frm, VM.ExecState exec, ref BHS status)
  {
    if(end_stamp == -1)
    {
      int ms = (int)exec.stack_old.PopRelease()._num;
      end_stamp = System.Environment.TickCount + ms;
    }

    if(end_stamp > System.Environment.TickCount)
      status = BHS.RUNNING;
  }

  public override void Cleanup(VM.FrameOld frm, VM.ExecState exec)
  {
    end_stamp = -1;
  }
}

}
