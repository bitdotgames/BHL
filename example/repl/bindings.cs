using System;

namespace bhl {

//NOTE: discoverable by reflection so example.cs's driver finds it via BindingsRegistry with
//      no compile-time reference needed (e.g. a reusable Editor integration shouldn't have to
//      know about game-specific bindings classes). No required_bindings/hash-check embedding
//      needed either, since the process compiling it is also the one running it.
//      [Preserve] guards against IL2CPP/Mono stripping
#if UNITY_5_3_OR_NEWER
[UnityEngine.Scripting.Preserve]
#endif
[BhlBinding("unity", "1.0.0")]
public class UnityBindings : IUserBindings
{
  public class Vector3
  {
    public float x, y, z;
  }

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
          var str = exec.stack.Pop().str;
          Console.WriteLine(str);
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
          var rnd = new Random();
          double val = rnd.NextDouble();
          exec.stack.Push(val);
          return null;
        }
      );
      ns.Define(fn);
    }

    types.RegisterModule(m);
  }
}

} //namespace bhl
