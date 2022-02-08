using System;
using System.IO;

namespace bhl {

//NOTE: this class must be first in the assembly
public class MyBindings : UserBindings
{ 
  public MyBindings()
  {}

  public override void Register(GlobalScope globs)
  {
    {
      var fn = new FuncSymbolNative("Trace", globs.Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
        {
#if !BHL_FRONT
          var str = frm.stack.PopRelease().str;
          Console.WriteLine(str);
#endif
          return null;
        }
      );
      fn.Define(new FuncArgSymbol("str", globs.Type("string")));

      globs.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("Rand", globs.Type("float"),
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
      globs.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("Wait", globs.Type("void"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          { return new WaitNode(); }
      );
      fn.Define(new FuncArgSymbol("t", globs.Type("float")));

      globs.Define(fn);
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

  public void Cleanup(VM.Frame frm)
  {
    first_time = true;
  }
}

} //namespace bhl
