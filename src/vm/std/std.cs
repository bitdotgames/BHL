using System;

#pragma warning disable CS8981

namespace bhl
{

public static class std
{
  class CoroutineNextTrue : Coroutine
  {
    bool first_time = true;

    public override void Tick(VM.FrameOld frm, VM.ExecState exec)
    {
      if(first_time)
      {
        exec.status = BHS.RUNNING;
        first_time = false;
      }
      else
        exec.stack_old.Push(frm.vm.TrueOld);
    }

    public override void Cleanup(VM.FrameOld frm, VM.ExecState exec)
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
        (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          var o = exec.stack.Pop();
          exec.stack.Push(Val.NewObj(o.type, Types.Type));
          o.Release();
          return null;
        },
        new FuncArgSymbol("o", ts.T("any"))
      );
      std.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "Is", Types.Bool,
        (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          var type = (IType)exec.stack.PopRelease()._obj;
          var o = exec.stack.Pop();
          exec.stack.Push(Types.Is(o, type));
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
        (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          return CoroutinePool.New<CoroutineNextTrue>(vm);
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
          (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            string s = exec.stack.Pop();
            Console.Write(s);
            return null;
          },
          new FuncArgSymbol("s", Types.String)
        );
        io.Define(fn);
      }

      {
        var fn = new FuncSymbolNative(new Origin(), "WriteLine", Types.Void,
          (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            string s = exec.stack.Pop();
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
