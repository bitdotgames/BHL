using System;

namespace bhl {

public static class std 
{
  static public void Init(Types ts)
  {
    var io = ts.ns.Nest("std").Nest("io");

    {
      var fn = new FuncSymbolNative("Write", ts.ns.T("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          var s = frm.stack.PopRelease().str;
          Console.Write(s);
          return null;
        }, 
        new FuncArgSymbol("s", ts.ns.T("string"))
      );
      io.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("WriteLine", ts.ns.T("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          var s = frm.stack.PopRelease().str;
          Console.WriteLine(s);
          return null;
        }, 
        new FuncArgSymbol("s", ts.ns.T("string"))
      );
      io.Define(fn);
    }
  }
}

} //namespace bhl
