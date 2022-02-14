using System;
using System.IO;

namespace bhl {

//NOTE: this class must be first in the assembly
public class MyBindings : UserBindings
{ 
  public MyBindings()
  {}

  public override void Register(TypeSystem types)
  {
    {
      var fn = new FuncSymbolNative("Trace", types.Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
        {
#if !BHL_FRONT
          var str = frm.stack.PopRelease().str;
          Console.WriteLine(str);
#endif
          return null;
        },
        new FuncArgSymbol("str", types.Type("string"))
        );

      types.globs.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("Rand", types.Type("float"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
        {
#if !BHL_FRONT
          var rnd = new Random();
          var val = rnd.NextDouble(); 
          frm.stack.Push(Val.NewNum(frm.vm, val));
#endif
          return null;
        }
      );
      types.globs.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("Wait", types.Type("void"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          { return new WaitNode(); },
          new FuncArgSymbol("t", types.Type("float"))
        );

      types.globs.Define(fn);
    }
  }
}


public static class Time
{
  public static float dt;
}

public class WaitNode : ICoroutine
{
  bool first_time = true;
  float time_left;

  public void Tick(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> frames, ref BHS status)
  {
    if(first_time)
    {
      status = BHS.RUNNING;
      time_left = (float)frm.stack.PopRelease().num;
      first_time = false;
    }
    else 
    {
      time_left -= Time.dt;
      if(time_left > 0)
        status = BHS.RUNNING; 
    }
  }

  public void Cleanup(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> frames)
  {
    first_time = true;
  }
}

} //namespace bhl
