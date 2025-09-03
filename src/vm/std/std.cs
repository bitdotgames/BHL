using System;

#pragma warning disable CS8981

namespace bhl {

public static class std 
{
  class CoroutineNextTrue : Coroutine
  {
    bool first_time = true;
  
    public override void Tick(VM.Frame frm, VM.ExecState exec, ref BHS status)
    {
      if(first_time)
      {
        status = BHS.RUNNING;
        first_time = false;
      }
      else
        exec.stack.Push(frm.vm.True);
    }
  
    public override void Cleanup(VM.Frame frm, VM.ExecState exec)
    {
      first_time = true;
    }
  }

  static public Module MakeModule(Types ts)
  {
    var m = new Module(ts, "std");

    var std = m.ns.Nest("std");

    {
      var fn = new FuncSymbolNative(new Origin(), "GetType", ts.T(Types.Type),
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
        { 
          var o = stack.Pop();
          stack.Push(Val.NewObj(frm.vm, o.type, Types.Type));
          o.Release();
          return null;
        }, 
        new FuncArgSymbol("o", ts.T("any"))
      );
      std.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "Is", Types.Bool,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
        {
          var type = (IType)stack.PopRelease()._obj;
          var o = stack.Pop();
          stack.Push(Val.NewBool(frm.vm, Types.Is(o, type)));
          o.Release();
          return null;
        },
        new FuncArgSymbol("o", ts.T("any")),
        new FuncArgSymbol("type", ts.T(Types.Type))
      );
      std.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "NextTrue", FuncAttrib.Coro, Types.Bool, 0,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
        { 
          return CoroutinePool.New<CoroutineNextTrue>(frm.vm);
        }
      );
      std.Define(fn);
    }

    return m;
  }

  public static class io 
  {
    static public Module MakeModule(Types ts)
    {
      var m = new Module(ts, "std/io");

      var io = m.ns.Nest("std").Nest("io");

      {
        var fn = new FuncSymbolNative(new Origin(), "Write", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
          { 
            var s = stack.PopRelease().str;
            Console.Write(s);
            return null;
          }, 
          new FuncArgSymbol("s", Types.String)
        );
        io.Define(fn);
      }

      {
        var fn = new FuncSymbolNative(new Origin(), "WriteLine", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
          { 
            var s = stack.PopRelease().str;
            Console.WriteLine(s);
            return null;
          }, 
          new FuncArgSymbol("s", Types.String)
        );
        io.Define(fn);
      }
      return m;
    }
  }
}

}
