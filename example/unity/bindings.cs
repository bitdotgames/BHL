using System;
using System.Runtime.CompilerServices;

namespace bhl {

//NOTE: registers a real "unity" module (via Types.RegisterModule) instead of writing
//      straight into the global namespace - see example/bindings for that flat style.
//      Scripts need `import "unity"` to reach unity.Vector3/unity.Mathf.Floor/etc
public class UnityBindings : IUserBindings
{
  public class Vector3
  {
    public float x, y, z;
  }

  //NOTE: module initializers aren't guaranteed to fire under IL2CPP, so Unity gets its
  //      own reliable hooks instead - RuntimeInitializeOnLoadMethod for Player/Play mode,
  //      InitializeOnLoadMethod so it also happens in the Editor outside Play
#if UNITY_5_3_OR_NEWER
#if UNITY_EDITOR
  [UnityEditor.InitializeOnLoadMethod]
#endif
  [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
  internal static void Init()
#else
  [ModuleInitializer]
  internal static void Init()
#endif
  {
    BindingsRegistry.Register("unity", typeof(UnityBindings), "1.0.0");
  }

  //must be present due to loading class instance from dll requirements
  public UnityBindings()
  {}

  public void Register(Types types)
  {
    var m = new ModuleDeclared("unity");
    var ns = m.ns.Nest("unity");

    {
      var vec3 = new ClassSymbolNative(new Origin(), "Vector3", typeof(Vector3),
        delegate(VM.ExecState exec, ref Val v, IType type) { v.SetObj(new Vector3(), type); }
      );
      ns.Define(vec3);

      vec3.Define(new FieldSymbol(new Origin(), "x", Types.Float,
        delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld) { v.SetFlt(((Vector3)ctx.obj).x); },
        delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld)
        {
          var vec = (Vector3)ctx.obj;
          vec.x = (float)v.num;
          ctx.SetObj(vec, ctx.type);
        }
      ));
      vec3.Define(new FieldSymbol(new Origin(), "y", Types.Float,
        delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld) { v.SetFlt(((Vector3)ctx.obj).y); },
        delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld)
        {
          var vec = (Vector3)ctx.obj;
          vec.y = (float)v.num;
          ctx.SetObj(vec, ctx.type);
        }
      ));
      vec3.Define(new FieldSymbol(new Origin(), "z", Types.Float,
        delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld) { v.SetFlt(((Vector3)ctx.obj).z); },
        delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld)
        {
          var vec = (Vector3)ctx.obj;
          vec.z = (float)v.num;
          ctx.SetObj(vec, ctx.type);
        }
      ));

      vec3.Setup();
    }

    {
      var mathf = ns.Nest("Mathf");

      var fn = new FuncSymbolNative(new Origin(), "Floor", Types.Float,
        delegate(VM.ExecState exec, FuncArgsInfo args_info)
        {
          float f = exec.stack.Pop();
          exec.stack.Push((double)Math.Floor(f));
          return null;
        },
        new FuncArgSymbol("f", Types.Float)
      );
      mathf.Define(fn);
    }

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

      ns.Define(fn);
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
      ns.Define(fn);
    }

    types.RegisterModule(m);
  }
}

} //namespace bhl
