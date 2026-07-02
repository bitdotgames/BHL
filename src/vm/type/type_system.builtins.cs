using System;

namespace bhl
{

public partial class Types : INamedResolver, IProxyTypeCache
{
  static public BoolSymbol Bool = new BoolSymbol();
  static public StringSymbol String = new StringSymbol();
  static public IntSymbol Int = new IntSymbol();
  static public FloatSymbol Float = new FloatSymbol();
  static public VoidSymbol Void = new VoidSymbol();
  static public AnySymbol Any = new AnySymbol();
  //it's an emphemeral type, exists only to designate FuncPtr values in runtime
  static public FuncPtrType FuncPtr = new FuncPtrType();
  //it's an emphemeral type, exists only to designate ValRef values in runtime
  static public ValRefType ValRef = new ValRefType();

  static public ClassSymbolNative Type =
    new ClassSymbolNative(new Origin(), "Type",
      delegate(VM.ExecState exec, ref Val v, IType type) { v.SetObj(null, type); }
    );

  static public ClassSymbolNative FiberRef =
    new ClassSymbolNative(new Origin(), "FiberRef",
      delegate(VM.ExecState exec, ref Val v, IType type) { v.SetObj(null, type); }
    );

  //NOTE: These are types which are parametrized with Any types. They are mostly used when
  //      it's required to set a type of a generic ValList
  static public GenericArrayTypeSymbol Array = new GenericArrayTypeSymbol(new Origin(), Any);
  static public GenericMapTypeSymbol Map = new GenericMapTypeSymbol(new Origin(), Any, Any);

  static public VarSymbol Var = new VarSymbol();
  static public NullSymbol Null = new NullSymbol();

  //NOTE: each symbol belongs to a Module but there are also global static symbols,
  //      for them we have a special static global Module
  static ModuleDeclared static_module = new ModuleDeclared();

  static void InitBuiltins()
  {
    static_module.ns.Define(Int);
    static_module.ns.Define(Float);
    static_module.ns.Define(Bool);
    static_module.ns.Define(Void);
    static_module.ns.Define(Any);
    static_module.ns.Define(Var);

    SetupGenericArrayType();
    SetupGenericMapType();
    SetupStringSymbol();
    SetupClassType();
    SetupClassFiberRef();

    Prelude.Define(static_module);
  }

  static void SetupGenericArrayType()
  {
    static_module.ns.Define(Array);
    Array.Setup();
  }

  static void SetupGenericMapType()
  {
    static_module.ns.Define(Map);
    static_module.ns.Define(Map.enumerator_type);
    Map.Setup();
  }

  static void SetupStringSymbol()
  {
    static_module.ns.Define(String);

    {
      var fld = new FieldSymbol(new Origin(), "Count", Int,
        delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol _) { v.SetInt(ctx.str.Length); },
        null
      );
      String.Define(fld);
    }

    {
      var m = new FuncSymbolNative(new Origin(), "At", String,
        (VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          int idx = exec.stack.PopFast();
          ref var self = ref exec.stack.Peek();
          self = self.str[idx].ToString();
          return null;
        },
        new FuncArgSymbol("i", Int)
      );
      String.Define(m);
    }

    {
      var m = new FuncSymbolNative(new Origin(), "IndexOf", Int,
        (VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          string s = exec.stack.PopFast();
          ref var self = ref exec.stack.Peek();
          self = self.str.IndexOf(s);
          return null;
        },
        new FuncArgSymbol("s", String)
      );
      String.Define(m);
    }

    String.Setup();
  }

  static void SetupClassType()
  {
    static_module.ns.Define(Type);

    {
      var fld = new FieldSymbol(new Origin(), "Name", String,
        delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol _)
        {
          var t = (IType)ctx.obj;
          v.SetStr(t.GetName());
        },
        null //no setter
      );
      Type.Define(fld);
    }
    Type.Setup();
  }

  static void SetupClassFiberRef()
  {
    static_module.ns.Define(FiberRef);

    {
      var fld = new FieldSymbol(new Origin(), "IsRunning", Bool,
        delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol _)
        {
          var fb_ref = new VM.FiberRef(ctx);
          v.SetBool(fb_ref.IsRunning);
        },
        null //no setter
      );
      FiberRef.Define(fld);
    }

    FiberRef.Setup();
  }
}

}
