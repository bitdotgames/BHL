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

    {
      var fn = new FuncSymbolNative(new Origin(), "Wait", FuncAttrib.Coro, Types.Void, 0,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
          { return CoroutinePool.New<WaitNode>(frm.vm); },
          new FuncArgSymbol("t", Types.Float)
        );

      types.ns.Define(fn);
    }
  }
}


public static class Time
{
  public static float dt;
}

public class WaitNode : Coroutine
{
  bool first_time = true;
  float time_left;

  public override void Tick(VM.Frame frm, VM.ExecState exec, ref BHS status)
  {
    if(first_time)
    {
      status = BHS.RUNNING;
      time_left = (float)exec.stack.PopRelease().num;
      first_time = false;
    }
    else 
    {
      time_left -= Time.dt;
      if(time_left > 0)
        status = BHS.RUNNING; 
    }
  }

  public override void Cleanup(VM.Frame frm, VM.ExecState exec)
  {
    first_time = true;
  }
}

} //namespace bhl
