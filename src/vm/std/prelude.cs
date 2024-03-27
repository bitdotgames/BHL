namespace bhl {

public static class Prelude
{
  static public FuncSymbolNative YieldFunc = null;

  public static void Define(Types ts)
  {
    {
      //NOTE: it's a builtin non-directly available function
      var fn = new FuncSymbolNative(new Origin(), "$yield", Types.Void,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
        { 
          return CoroutinePool.New<CoroutineYield>(frm.vm);
        } 
      );
      YieldFunc = fn;
      ts.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "suspend", FuncAttrib.Coro, Types.Void, 0,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
        { 
          //TODO: use static instance for this case?
          return CoroutinePool.New<CoroutineSuspend>(frm.vm);
        } 
      );
      ts.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "wait", FuncAttrib.Coro, Types.Void, 0,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
        { 
          return CoroutinePool.New<CoroutineWait>(frm.vm);
        }, 
        new FuncArgSymbol("ms", Types.Int)
      );
      ts.ns.Define(fn);
    }

    //TODO: return an actual Fiber object
    {
      var fn = new FuncSymbolNative(new Origin(), "start", Types.Int,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
        { 
          var val_ptr = stack.Pop();
          int id = frm.vm.Start((VM.FuncPtr)val_ptr._obj, frm, stack).id;
          val_ptr.Release();
          stack.Push(Val.NewNum(frm.vm, id));
          return null;
        }, 
        new FuncArgSymbol("p", ts.TFunc(true/*is coro*/, "void"))
      );
      ts.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "stop", Types.Void,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
        { 
          var fid = (int)stack.PopRelease().num;
          frm.vm.Stop(fid);
          return null;
        }, 
        new FuncArgSymbol("fid", Types.Int)
      );
      ts.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "debugger", ts.T("void"),
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
        { 
          System.Diagnostics.Debugger.Break();
          return null;
        } 
      );
      ts.ns.Define(fn);
    }
  }
}

class CoroutineSuspend : Coroutine
{
  public override void Tick(VM.Frame frm, VM.ExecState exec, ref BHS status)
  {
    status = BHS.RUNNING;
  }
}

class CoroutineYield : Coroutine
{
  bool first_time = true;

  public override void Tick(VM.Frame frm, VM.ExecState exec, ref BHS status)
  {
    if(first_time)
    {
      status = BHS.RUNNING;
      first_time = false;
    }
  }

  public override void Cleanup(VM.Frame frm, VM.ExecState exec)
  {
    first_time = true;
  }
}

class CoroutineWait : Coroutine
{
  int end_stamp = -1;

  public override void Tick(VM.Frame frm, VM.ExecState exec, ref BHS status)
  {

    if(end_stamp == -1)
      end_stamp = System.Environment.TickCount + (int)exec.stack.PopRelease()._num; 

    if(end_stamp > System.Environment.TickCount)
      status = BHS.RUNNING;
  }

  public override void Cleanup(VM.Frame frm, VM.ExecState exec) 
  {
    end_stamp = -1;
  }  
}

}
