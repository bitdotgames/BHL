using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace bhl {

public class MyBindings : IUserBindings
{
  //NOTE: "example" matches this entry's key in bhl.proj's `bindings` dict
  [ModuleInitializer]
  internal static void Init()
  {
    BindingsRegistry.Register("example", typeof(MyBindings));
  }

  //must be present due to loading class instance from dll requirements
  public MyBindings()
  {}

  public void Register(Types types)
  {
    {
      var fn = new FuncSymbolNative(new Origin(), "Trace", Types.Void,
        delegate(VM.ExecState exec, FuncArgsInfo args_info)
        {
#if !BHL_FRONT
          var str = exec.stack.Pop().str;
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
        delegate(VM.ExecState exec, FuncArgsInfo args_info)
        {
#if !BHL_FRONT
          var rnd = new Random();
          double val = rnd.NextDouble();
          exec.stack.Push(val);
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
