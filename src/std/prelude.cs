namespace bhl {

public static class Prelude
{
  static public FuncSymbolNative YieldFunc = null;

  public static void Define(Types ts)
  {
    {
      //NOTE: it's a builtin non-directly available function
      var fn = new FuncSymbolNative(new Origin(), "$yield", ts.T("void"),
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
        { 
          return CoroutinePool.New<CoroutineYield>(frm.vm);
        } 
      );
      YieldFunc = fn;
      ts.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "suspend", FuncAttrib.Coro, ts.T("void"), 0, 
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
        { 
          //TODO: use static instance for this case?
          return CoroutinePool.New<CoroutineSuspend>(frm.vm);
        } 
      );
      ts.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "start", ts.T("int"),
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
      var fn = new FuncSymbolNative(new Origin(), "stop", ts.T("void"),
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
        { 
          var fid = (int)stack.PopRelease().num;
          frm.vm.Stop(fid);
          return null;
        }, 
        new FuncArgSymbol("fid", ts.T("int"))
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

}
