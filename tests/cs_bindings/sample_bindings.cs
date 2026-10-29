//NOTE: generated-like C# bindings registration in the canonical shape CsSourceBindings reads
//      (see docs/csharp-bindings.md). It's compiled into the tests as is, natives included, and
//      also read from this file without compiling - TestCsBindings compares both
#pragma warning disable CS0162
using System;
using System.Collections.Generic;
using bhl;

namespace bhl.test_cs_bindings
{

#if !BHL_FRONT
public class SamplePoint
{
  public float x;
  public float y;
}
#endif

static public partial class SampleCsBindings
{
  public static void Register(Types types)
  {
    RegisterBegin(types);
    RegisterEnd(types);
  }

  public static void RegisterBegin(Types types)
  {
    Register_0(types);
    Register_1(types);
  }

  public static void RegisterEnd(Types types)
  {
    var point = (ClassSymbolNative)types.ns.Resolve("SamplePoint");
    point.Setup();
    var widget = (InterfaceSymbolNative)types.ns.Resolve("ISampleWidget");
    widget.Setup();
    var button = (ClassSymbolNative)types.ns.Resolve("SampleButton");
    button.Setup();
  }

  public static void Register_0(Types types)
  {
    {
      static void ctor_SamplePoint(VM.ExecState exec, ref Val v, IType type)
      {
#if !BHL_FRONT
        v = Val.NewObj(new SamplePoint(), type);
#endif
      }
      var cl = new ClassSymbolNative(new Origin(), "SamplePoint",
        new ProxyType(),
        proxy_implements: null,
        creator: ctor_SamplePoint
#if !BHL_FRONT
        , native_type: typeof(SamplePoint)
#endif
      );
      types.ns.Define(cl);
      {
        static void get_SamplePoint_x(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld)
        {
#if !BHL_FRONT
          v = Val.NewFlt(((SamplePoint)ctx.obj).x);
#endif
        }
        static void set_SamplePoint_x(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld)
        {
#if !BHL_FRONT
          ((SamplePoint)ctx.obj).x = (float)v.num;
#endif
        }
        cl.Define(new FieldSymbol(new Origin(), "x", types.T("float"),
          //getter
          get_SamplePoint_x
          ,
          //setter
          set_SamplePoint_x
        ));
      }
      {
        static void get_SamplePoint_len(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld)
        {
#if !BHL_FRONT
          var p = (SamplePoint)ctx.obj;
          v = Val.NewFlt((float)Math.Sqrt(p.x * p.x + p.y * p.y));
#endif
        }
        cl.Define(new FieldSymbol(new Origin(), "len", types.T("float"),
          //getter
          get_SamplePoint_len
          ,
          //setter not allowed
          null
        ));
      }
      {
#if !BHL_FRONT
        static Coroutine SamplePoint_Scale(VM.ExecState exec, FuncArgsInfo args_info)
        {
          return null;
        }
#endif
        var fn = new FuncSymbolNative(new Origin(), "Scale",
          FuncAttrib.None,
          types.T("SamplePoint"),
          0,
#if !BHL_FRONT
          SamplePoint_Scale
#else
          null
#endif
          ,
          new FuncArgSymbol("k", types.T("float"), false)
        );
        cl.Define(fn);
      }
    }

    {
      var cl = new ClassSymbolNative(new Origin(), "SampleButton",
        new ProxyType(),
        proxy_implements: new List<ProxyType>() {
          types.T("ISampleWidget"),
        },
        //constructor is not allowed
        creator: null
      );
      types.ns.Define(cl);
      {
        var fn = new FuncSymbolNative(new Origin(), "Refresh",
          FuncAttrib.None,
          types.T("void"),
          0,
          null
        );
        cl.Define(fn);
      }
      {
        var fn = new FuncSymbolNative(new Origin(), "Same",
          FuncAttrib.None | FuncAttrib.Static,
          types.T("bool"),
          0,
          null,
          new FuncArgSymbol("a", types.T("SampleButton"), false), new FuncArgSymbol("b", types.T("SampleButton"), false)
        );
        cl.Define(fn);
      }
    }

    {
      const string enumName = "SampleColor";
      var en = new EnumSymbolNative(new Origin(), enumName
        , null
      );
      System.Type nativeType = null;
      types.ns.Define(en);
      RegisterEnumValue(en, nativeType, enumName, "Red", (int)1);
      RegisterEnumValue(en, nativeType, enumName, "Green", (int)2);
      RegisterEnumValue(en, nativeType, enumName, "Blue", (int)-3);
    }

    {
      var ifs = new InterfaceSymbolNative(new Origin(), "ISampleWidget", proxy_inherits: null
        , native_type: null
        , funcs: new FuncSymbol[] {
          new FuncSymbolNative(new Origin(), "Refresh",
            FuncAttrib.None,
            types.T("void"),
            0,
            null
          ),
        }
      );
      types.ns.Define(ifs);
    }
  }

  public static void Register_1(Types types)
  {
    {
      var fn = new FuncSymbolNative(new Origin(), "SampleSum",
        FuncAttrib.None,
        types.T("int"),
        1,
        null,
        new FuncArgSymbol("a", types.T("int"), false), new FuncArgSymbol("b", types.T("int"), false)
      );
      types.ns.Define(fn);
    }
    {
      var fn = new FuncSymbolNative(new Origin(), "SampleWait", FuncAttrib.Coro,
        types.T("void"),
        0,
        null,
        new FuncArgSymbol("sec", types.T("float"), false)
      );
      types.ns.Define(fn);
    }
    {
      var fn = new FuncSymbolNative(new Origin(), "SamplePair",
        FuncAttrib.None,
        types.TTuple(types.T("string"), types.TArr(types.T("SamplePoint"))),
        0,
        null,
        new FuncArgSymbol("cb", types.TFunc(false, types.T("void")), false)
      );
      types.ns.Define(fn);
    }

    var ui = types.ns.Nest("SampleUI");
    {
      var fn = new FuncSymbolNative(new Origin(), "Open",
        FuncAttrib.None,
        types.T("bool"),
        0,
        null,
        new FuncArgSymbol("name", types.T("string"), false), new FuncArgSymbol("color", types.T("SampleColor"), false)
      );
      ui.Define(fn);
    }

    if(types.ns.Resolve("List_SamplePoint") == null)
    {
      var lst = new NativeListTypeSymbol<object>(
        new Origin(),
        "List_SamplePoint",
#if !BHL_FRONT
        (v) => v.obj, (t, n) => Val.NewObj(n, t.Get()),
#else
        null, null,
#endif
        types.T("SamplePoint")
      );
      lst.Setup();
      types.ns.Define(lst);
    }
  }

  static void RegisterEnumValue(EnumSymbolNative en, System.Type nativeType, string enumName, string valName, int val)
  {
    en.Define(new EnumItemSymbol(new Origin(), valName, val));
  }
}

}
