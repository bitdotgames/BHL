using System;
using System.IO;

namespace bhl {

//NOTE: this class must be first in the assembly
public class MyBindings : IUserBindings
{ 
  //must be present due to loading class instance from dll requirements
  public MyBindings()
  {}

  public void Register(Types types)
  {
    {
      var fn = new FuncSymbolNative(new Origin(), "Trace", Types.Void,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
        {
#if !BHL_FRONT
          var str = stack.PopRelease().str;
          Console.WriteLine(str);
#endif
          return null;
        },
        new FuncArgSymbol("str", Types.String)
        );

      types.ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "Rand", Types.Float,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
        {
#if !BHL_FRONT
          var rnd = new Random();
          var val = rnd.NextDouble(); 
          stack.Push(Val.NewFlt(frm.vm, val));
#endif
          return null;
        }
      );
      types.ns.Define(fn);
    }
  }
}


public static class Time
{
  public static float dt;
}

} //namespace bhl
