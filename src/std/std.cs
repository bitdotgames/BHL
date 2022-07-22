using System;

namespace bhl {

public static class std 
{
  static public Module MakeModule(Types ts)
  {
    var m = new Module(ts, "std", null);

    var io = m.ns.Nest("std").Nest("io");

    {
      var fn = new FuncSymbolNative("Write", ts.T("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          var s = frm.stack.PopRelease().str;
          Console.Write(s);
          return null;
        }, 
        new FuncArgSymbol("s", ts.T("string"))
      );
      io.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("WriteLine", ts.T("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          var s = frm.stack.PopRelease().str;
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

} //namespace bhl
