using System;

#pragma warning disable CS8981

namespace bhl
{

public static class std
{
  class CoroutineNextTrue : Coroutine
  {
    bool first_time = true;

    public override void Tick(VM.ExecState exec)
    {
      if(first_time)
      {
        exec.status = BHS.RUNNING;
        first_time = false;
      }
      else
        exec.stack.Push(VM.True);
    }

    public override void Destruct(VM.ExecState exec)
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
        (VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          ref var o = ref exec.stack.Peek();
          o._refc?.Release();
          o = Val.NewObj(o.type, Types.Type);
          return null;
        },
        new FuncArgSymbol("o", ts.T("any"))
      );
      std.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "Is", Types.Bool,
        (VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          ref var type = ref exec.stack.PopFast();
          ref var o = ref exec.stack.Peek();
          var refc = o._refc;
          o = Types.Is(o, (IType)type.obj);
          //let's be nice and clean the stack value
          type.obj = null;
          refc?.Release();
          return null;
        },
        new FuncArgSymbol("o", ts.T("any")),
        new FuncArgSymbol("type", ts.T(Types.Type))
      );
      std.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "NextTrue", FuncAttrib.Coro, Types.Bool, 0,
        (VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          return CoroutinePool.New<CoroutineNextTrue>(exec.vm);
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
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            string s = exec.stack.PopFast();
            Console.Write(s);
            return null;
          },
          new FuncArgSymbol("s", Types.String)
        );
        io.Define(fn);
      }

      {
        var fn = new FuncSymbolNative(new Origin(), "WriteLine", Types.Void,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            string s = exec.stack.PopFast();
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
