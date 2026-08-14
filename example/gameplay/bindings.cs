using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace bhl {

[BhlBinding("example", "1.0.0")]
public class MyBindings : IUserBindings
{
  //NOTE: "example" must match bindings.bhl's RegisterVersion("example", ...) call (its
  //      compile-time stand-in). Module initializers aren't guaranteed under IL2CPP, so
  //      Unity gets its own hooks instead (Play mode + Editor outside Play)
#if UNITY_5_3_OR_NEWER
#if UNITY_EDITOR
  [UnityEditor.InitializeOnLoadMethod]
#endif
  [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
#else
  [ModuleInitializer]
#endif
  internal static void Init() => BindingsRegistry.Register<MyBindings>();

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
