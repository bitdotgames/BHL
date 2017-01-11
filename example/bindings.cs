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
      var fn = new SimpleFuncBindSymbol("Trace", globs.type("void"),
        delegate(object agent)
        {
#if !BHL_FRONT
          var interp = Interpreter.instance;
          var str = interp.PopValue().str;
          Console.WriteLine(str);
#endif
          return BHS.SUCCESS;
        }
      );
      fn.define(new FuncArgSymbol("str", globs.type("string")));

      globs.define(fn);
    }

    {
      var fn = new SimpleFuncBindSymbol("Rand", globs.type("float"),
        delegate(object agent)
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
      globs.define(fn);
    }

    {
      var fn = new FuncBindSymbol("Wait", globs.type("void"),
          delegate() { return new WaitNode(); }
      );
      fn.define(new FuncArgSymbol("t", globs.type("float")));

      globs.define(fn);
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

  public override void init(object agent)
  {
    var interp = Interpreter.instance;
    time_left = (float)interp.PopValue().num;
  }

  public override BHS execute(object agent)
  {
    time_left -= Time.dt;

    return time_left <= 0 ? BHS.SUCCESS : BHS.RUNNING;
  }
}

} //namespace bhl
