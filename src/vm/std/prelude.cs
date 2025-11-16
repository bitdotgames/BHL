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
        (VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          return CoroutinePool.New<CoroutineYield>(exec.vm);
        }
      );
      YieldFunc = fn;
      m.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "suspend", FuncAttrib.Coro, Types.Void, 0,
        (VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          //TODO: use static instance for this case?
          return CoroutinePool.New<CoroutineSuspend>(exec.vm);
        }
      );
      m.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "wait", FuncAttrib.Coro, Types.Void, 0,
        (VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          return CoroutinePool.New<CoroutineWait>(exec.vm);
        },
        new FuncArgSymbol("ms", Types.Int)
      );
      m.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "start", m.ts.T(Types.FiberRef),
        (VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          var val_ptr = exec.stack.Pop();
          var fb = exec.vm.Start(
            exec, (VM.FuncPtr)val_ptr.obj,
            ref exec.frames[exec.frames_count - 1],
            default
            );
          val_ptr._refc.Release();
          exec.stack.Push(VM.FiberRef.AsVal(fb));
          return null;
        },
        new FuncArgSymbol("p", m.TFunc(true /*is coro*/, "void"))
      );
      m.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "stop", Types.Void,
        (VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          var val = exec.stack.Pop();
          var fb_ref = new VM.FiberRef(val);
          var fb = fb_ref.Get();
          //NOTE: we don't release it since we don't own it (it's a weak ref FiberRef)
          fb?.Stop();
          return null;
        },
        new FuncArgSymbol("fb", m.ts.T(Types.FiberRef))
      );
      m.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "debugger", Types.Void,
        (VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          System.Diagnostics.Debugger.Break();
          return null;
        }
      );
      m.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "__dump_opcodes_on", Types.Void,
        (VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          return null;
        }
      );
      DumpOpcodesOn = fn;
      m.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "__dump_opcodes_off", Types.Void,
        (VM.ExecState exec, FuncArgsInfo args_info) =>
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
  public override void Tick(VM.ExecState exec)
  {
    exec.status = BHS.RUNNING;
  }
}

class CoroutineYield : Coroutine
{
  bool first_time = true;

  public override void Tick(VM.ExecState exec)
  {
    if(first_time)
    {
      exec.status = BHS.RUNNING;
      first_time = false;
    }
  }

  public override void Cleanup(VM.ExecState exec)
  {
    first_time = true;
  }
}

class CoroutineWait : Coroutine
{
  int end_stamp = -1;

  public override void Tick(VM.ExecState exec)
  {
    if(end_stamp == -1)
    {
      int ms = exec.stack.PopFast();
      end_stamp = System.Environment.TickCount + ms;
    }

    if(end_stamp > System.Environment.TickCount)
      exec.status = BHS.RUNNING;
  }

  public override void Cleanup(VM.ExecState exec)
  {
    end_stamp = -1;
  }
}

}
