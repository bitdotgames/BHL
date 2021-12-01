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
      var fn = new FuncSymbolSimpleNative("Trace", globs.Type("void"),
        delegate()
        {
#if !BHL_FRONT
          var interp = Interpreter.instance;
          var str = interp.PopValue().str;
          Console.WriteLine(str);
#endif
          return BHS.SUCCESS;
        }
      );
      fn.Define(new FuncArgSymbol("str", globs.Type("string")));

      globs.Define(fn);
    }

    {
      var fn = new FuncSymbolSimpleNative("Rand", globs.Type("float"),
        delegate()
        {
#if !BHL_FRONT
          var interp = Interpreter.instance;
          var rnd = new Random();
          var val = rnd.NextDouble(); 
          interp.PushValue(DynVal.NewNum(val));
#endif
          return BHS.SUCCESS;
        }
      );
      globs.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("Wait", globs.Type("void"),
          delegate() { return new WaitNode(); }
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

public class WaitNode : BehaviorTreeTerminalNode
{
  float time_left;

  public override void init()
  {
    var interp = Interpreter.instance;
    time_left = (float)interp.PopValue().num;
  }

  public override BHS execute()
  {
    time_left -= Time.dt;

    return time_left <= 0 ? BHS.SUCCESS : BHS.RUNNING;
  }
}

} //namespace bhl
