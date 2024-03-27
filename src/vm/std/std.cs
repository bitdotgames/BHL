using System;

namespace bhl {

public static class std 
{
  static public Module MakeModule(Types ts)
  {
    var m = new Module(ts, "std");

    var std = m.ns.Nest("std");

    {
      var fn = new FuncSymbolNative(new Origin(), "GetType", ts.T(Types.ClassType),
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
        { 
          var o = stack.Pop();
          stack.Push(Val.NewObj(frm.vm, o.type, Types.ClassType));
          o.Release();
          return null;
        }, 
        new FuncArgSymbol("o", ts.T("any"))
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
        var fn = new FuncSymbolNative(new Origin(), "Write", ts.T("void"),
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
          { 
            var s = stack.PopRelease().str;
            Console.Write(s);
            return null;
          }, 
          new FuncArgSymbol("s", ts.T("string"))
        );
        io.Define(fn);
      }

      {
        var fn = new FuncSymbolNative(new Origin(), "WriteLine", ts.T("void"),
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
          { 
            var s = stack.PopRelease().str;
            Console.WriteLine(s);
            return null;
          }, 
          new FuncArgSymbol("s", ts.T("string"))
        );
        io.Define(fn);
      }
      return m;
    }
  }
}

} //namespace bhl
