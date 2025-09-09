using System;
using System.Collections.Generic;
using bhl.marshall;

namespace bhl
{

public class Types : INamedResolver
{
  static public BoolSymbol Bool = new BoolSymbol();
  static public StringSymbol String = new StringSymbol();
  static public IntSymbol Int = new IntSymbol();
  static public FloatSymbol Float = new FloatSymbol();
  static public VoidSymbol Void = new VoidSymbol();
  static public AnySymbol Any = new AnySymbol();

  static public ClassSymbolNative Type =
    new ClassSymbolNative(new Origin(), "Type",
      delegate(VM.Frame frm, ref Val v, IType type) { v.SetObj(null, type); }
    );

  static public ClassSymbolNative FiberRef =
    new ClassSymbolNative(new Origin(), "FiberRef",
      delegate(VM.Frame frm, ref Val v, IType type) { v.SetObj(null, type); }
    );

  //NOTE: These are types which are parametrized with Any types. They are mostly used when
  //      it's required to set a type of a generic ValList
  static public GenericArrayTypeSymbol Array = new GenericArrayTypeSymbol(new Origin(), Any);
  static public GenericMapTypeSymbol Map = new GenericMapTypeSymbol(new Origin(), Any, Any);

  static public VarSymbol Var = new VarSymbol();
  static public NullSymbol Null = new NullSymbol();

#if BHL_FRONT
  static Dictionary<Tuple<IType, IType>, IType> bin_op_res_type = new Dictionary<Tuple<IType, IType>, IType>()
  {
    { new Tuple<IType, IType>(String, String), String },
    { new Tuple<IType, IType>(Int, Int),       Int },
    { new Tuple<IType, IType>(Int, Float),     Float },
    { new Tuple<IType, IType>(Float, Float),   Float },
    { new Tuple<IType, IType>(Float, Int),     Float },
  };

  static Dictionary<Tuple<IType, IType>, IType> rtl_op_res_type = new Dictionary<Tuple<IType, IType>, IType>()
  {
    { new Tuple<IType, IType>(String, String), Bool },
    { new Tuple<IType, IType>(Int, Int),       Bool },
    { new Tuple<IType, IType>(Int, Float),     Bool },
    { new Tuple<IType, IType>(Float, Float),   Bool },
    { new Tuple<IType, IType>(Float, Int),     Bool },
  };

  static Dictionary<Tuple<IType, IType>, IType> eq_op_res_type = new Dictionary<Tuple<IType, IType>, IType>()
  {
    { new Tuple<IType, IType>(String, String), Bool },
    { new Tuple<IType, IType>(Int, Int),       Bool },
    { new Tuple<IType, IType>(Int, Float),     Bool },
    { new Tuple<IType, IType>(Float, Float),   Bool },
    { new Tuple<IType, IType>(Float, Int),     Bool },
    { new Tuple<IType, IType>(Any, Any),       Bool },
    { new Tuple<IType, IType>(Null, Any),      Bool },
    { new Tuple<IType, IType>(Any, Null),      Bool },
  };

  // Indicate whether a type supports a promotion to a wider type.
  static HashSet<Tuple<IType, IType>> is_subset_of = new HashSet<Tuple<IType, IType>>()
  {
    { new Tuple<IType, IType>(Int, Float) },
  };
#endif

  static Dictionary<Tuple<IType, IType>, IType> cast_from_to = new Dictionary<Tuple<IType, IType>, IType>()
  {
    { new Tuple<IType, IType>(Bool,   String),    String },
    { new Tuple<IType, IType>(Bool,   Int),       Int    },
    { new Tuple<IType, IType>(Bool,   Float),     Float  },
    { new Tuple<IType, IType>(Bool,   Any),       Any    },
    { new Tuple<IType, IType>(String, String),    String },
    { new Tuple<IType, IType>(String, Any),       Any    },
    { new Tuple<IType, IType>(Int,    Bool),      Bool   },
    { new Tuple<IType, IType>(Int,    String),    String },
    { new Tuple<IType, IType>(Int,    Int),       Int    },
    { new Tuple<IType, IType>(Int,    Float),     Float  },
    { new Tuple<IType, IType>(Int,    Any),       Any    },
    { new Tuple<IType, IType>(Float,  Bool),      Bool   },
    { new Tuple<IType, IType>(Float,  String),    String },
    { new Tuple<IType, IType>(Float,  Int),       Int    },
    { new Tuple<IType, IType>(Float,  Float),     Float  },
    { new Tuple<IType, IType>(Float,  Any),       Any    },
    { new Tuple<IType, IType>(Any,    Bool),      Bool   },
    { new Tuple<IType, IType>(Any,    String),    String },
    { new Tuple<IType, IType>(Any,    Int),       Int    },
    { new Tuple<IType, IType>(Any,    Float),     Float  },
    { new Tuple<IType, IType>(Any,    Any),       Any    },
  };

  //global module
  public Module module;

  public Namespace ns
  {
    get { return module.ns;  }
  }

  //NOTE: each symbol belongs to a Module but there are also global static symbols,
  //      for them we have a special static global Module 
  static Module static_module = new Module(null);

  internal Dictionary<string, Module> modules = new Dictionary<string, Module>();

  static Types()
  {
    InitBuiltins();

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
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol _) { v.SetInt(ctx.str.Length); },
        null
      );
      String.Define(fld);
    }

    {
      var m = new FuncSymbolNative(new Origin(), "At", String,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
        {
          int idx = (int)stack.PopRelease().num;
          string self = stack.PopRelease().str;
          stack.Push(Val.NewStr(frm.vm, self[idx].ToString()));
          return null;
        },
        new FuncArgSymbol("i", Int)
      );
      String.Define(m);
    }

    {
      var m = new FuncSymbolNative(new Origin(), "IndexOf", Int,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
        {
          string s = stack.PopRelease().str;
          string self = stack.PopRelease().str;
          stack.Push(Val.NewInt(frm.vm, self.IndexOf(s)));
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
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol _)
        {
          var t = (IType)ctx._obj;
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
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol _)
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

  public Types()
  {
    module = new Module(this, "");

    CopyFromStaticModule();

    RegisterModule(std.MakeModule(this));
    RegisterModule(std.io.MakeModule(this));
  }

  public bool IsImported(Module m)
  {
    return !(static_module == m || module == m);
  }

  public IEnumerable<Module> GetModules()
  {
    yield return static_module;

    foreach(var kv in modules)
      yield return kv.Value;
  }

  void CopyFromStaticModule()
  {
    //NOTE: dumb copy of all items from the static module
    module.nfunc_index.index.AddRange(static_module.nfunc_index.index);
    ns.members.UnionWith(static_module.ns.members);
  }

  public void RegisterModule(Module m)
  {
    modules.Add(m.name, m);
  }

  public Module FindRegisteredModule(string name)
  {
    Module m;
    modules.TryGetValue(name, out m);
    return m;
  }

  public INamed ResolveNamedByPath(NamePath path)
  {
    return ns.ResolveSymbolByPath(path);
  }

  static void InitBuiltins()
  {
    static_module.ns.Define(Int);
    static_module.ns.Define(Float);
    static_module.ns.Define(Bool);
    static_module.ns.Define(Void);
    static_module.ns.Define(Any);
    static_module.ns.Define(Var);
  }

  static public bool IsCompoundType(string name)
  {
    for(int i = 0; i < name.Length; ++i)
    {
      char c = name[i];
      if(!(Char.IsLetterOrDigit(c) || c == '_' || c == '.'))
        return true;
    }

    return false;
  }

  static public bool Is(IType type, IType dest_type)
  {
    if(type == null || dest_type == null)
      return false;

    //quick shortcut
    if(dest_type == Any)
      return true;

    //NOTE: using Equals() since we might deal with ephemeral types
    if(type.Equals(dest_type))
      return true;

    //NOTE: special case for array type checked against '[]any'
    //NOTE: using Equals() since Array is an ephemeral type
    if(type is ArrayTypeSymbol && dest_type.Equals(Array))
      return true;

    //NOTE: special case for array type checked against '[any]any'
    //NOTE: using Equals() since Map is an ephemeral type
    if(type is MapTypeSymbol && dest_type.Equals(Map))
      return true;

    if(type is IInstantiable ti && dest_type is IInstantiable di)
    {
      var tset = ti.GetAllRelatedTypesSet();
      var dset = di.GetAllRelatedTypesSet();

      return tset.IsSupersetOf(dset);
    }

    return false;
  }

  static public bool Is(Val val, IType dest_type)
  {
    //NOTE: special handling of native types - we need to go through C# type system.
    //      Make sense only if there's C# object is present - this way we can use C# reflection
    //      by getting the type of the object
    if(dest_type is INativeType dest_intype)
    {
      var val_type = val?.type;

      if(val_type is INativeType val_intype)
      {
        var dest_ntype = dest_intype.GetNativeType();
        var nobj = val_intype.GetNativeObject(val);
        return dest_ntype?.IsAssignableFrom(
          nobj?.GetType() ?? val_intype.GetNativeType()
        ) ?? false;
      }
      //special case for 'any' type being cast to native type 
      else if(val_type == Types.Any)
      {
        var dest_ntype = dest_intype.GetNativeType();
        if(val.is_null && dest_ntype != null)
          return true;
        var nobj = val._obj;
        return dest_ntype?.IsAssignableFrom(nobj?.GetType()) ?? false;
      }
      else
        return false;
    }
    else
      return Is(val?.type, dest_type);
  }

  static public bool CheckCastIsPossible(IType dest_type, IType from_type)
  {
    //optimization shortcut
    if(dest_type == from_type)
      return true;

    //enum special cases
    if((from_type == Float || from_type == Int) && dest_type is EnumSymbol)
      return true;
    if((dest_type == Float || dest_type == Int) && from_type is EnumSymbol)
      return true;
    if(dest_type == String && from_type is EnumSymbol)
      return true;

    cast_from_to.TryGetValue(new Tuple<IType, IType>(from_type, dest_type), out var cast_type);
    if(cast_type == dest_type)
      return true;

    //going the 'heavy path', checking if types are convertible one to another
    if(Is(from_type, dest_type) || Is(dest_type, from_type))
      return true;

    return false;
  }

  static public bool IsBinOpCompatible(IType type)
  {
    return type == Bool ||
           type == String ||
           type == Int ||
           type == Float;
  }

  static public bool IsNumeric(IType type)
  {
    return type == Int ||
           type == Float;
  }

#if BHL_FRONT
  static public IType MatchTypes(Dictionary<Tuple<IType, IType>, IType> table, AnnotatedParseTree lhs,
    AnnotatedParseTree rhs, CompileErrors errors)
  {
    IType result;
    if(!table.TryGetValue(new Tuple<IType, IType>(lhs.eval_type, rhs.eval_type), out result))
      errors.Add(new ParseError(rhs,
        "incompatible types: '" + lhs.eval_type.GetFullTypePath() + "' and '" + rhs.eval_type.GetFullTypePath() + "'"));
    return result;
  }

  static bool CanAssignTo(IType lhs, IType rhs)
  {
    if(rhs == null || lhs == null)
      return false;

    return rhs == lhs ||
           lhs == Any ||
           (lhs == Types.Int && rhs is EnumSymbol) ||
           (is_subset_of.Contains(new Tuple<IType, IType>(rhs, lhs))) ||
           (lhs is IInstantiable && rhs == Null) ||
           (lhs is FuncSignature && rhs == Null) ||
           Is(rhs, lhs)
      ;
  }

  public bool CheckAssign(AnnotatedParseTree lhs, AnnotatedParseTree rhs, CompileErrors errors)
  {
    if(!CanAssignTo(lhs.eval_type, rhs.eval_type))
    {
      errors.Add(new ParseError(rhs,
        "incompatible types: '" + lhs.eval_type.GetFullTypePath() + "' and '" + rhs.eval_type.GetFullTypePath() + "'"));
      return false;
    }

    return true;
  }

  //NOTE: SemanticError(..) is attached to rhs, not lhs. 
  //      Usually this is the case when expression (rhs) is passed
  //      to a func arg (lhs) and it this case it makes sense
  //      to report about the site where it's actually passed (rhs),
  //      not where it's defined (lhs)
  public bool CheckAssign(IType lhs, AnnotatedParseTree rhs, CompileErrors errors)
  {
    if(!CanAssignTo(lhs, rhs.eval_type))
    {
      errors.Add(MakeIncompatibleTypesError(rhs, lhs.GetFullTypePath().ToString(),
        rhs.eval_type.GetFullTypePath().ToString()));
      return false;
    }

    return true;
  }

  public bool CheckAssign(AnnotatedParseTree lhs, IType rhs, CompileErrors errors)
  {
    if(!CanAssignTo(lhs.eval_type, rhs))
    {
      errors.Add(MakeIncompatibleTypesError(lhs, lhs.eval_type.GetFullTypePath().ToString(),
        rhs.GetFullTypePath().ToString()));
      return false;
    }

    return true;
  }

  static ParseError MakeIncompatibleTypesError(AnnotatedParseTree pt, string lhs_path, string rhs_path)
  {
    return new ParseError(pt, "incompatible types: '" + lhs_path + "' and '" + rhs_path + "'");
  }

  static public bool CheckCastIsPossible(AnnotatedParseTree dest, AnnotatedParseTree from, CompileErrors errors)
  {
    var dest_type = dest.eval_type;
    var from_type = from.eval_type;

    if(!CheckCastIsPossible(dest_type, from_type))
    {
      errors.Add(new ParseError(dest,
        "incompatible types for casting: '" + dest_type.GetFullTypePath() + "' and '" + from_type.GetFullTypePath() +
        "'"));
      return false;
    }

    return true;
  }

  public IType CheckBinOp(AnnotatedParseTree lhs, AnnotatedParseTree rhs, CompileErrors errors)
  {
    if(!IsBinOpCompatible(lhs.eval_type))
    {
      errors.Add(new ParseError(lhs, "operator is not overloaded"));
      return null;
    }

    if(!IsBinOpCompatible(rhs.eval_type))
    {
      errors.Add(new ParseError(rhs, "operator is not overloaded"));
      return null;
    }

    return MatchTypes(bin_op_res_type, lhs, rhs, errors);
  }

  public IType CheckBinOpOverload(IScope scope, AnnotatedParseTree lhs, AnnotatedParseTree rhs, FuncSymbol op_func,
    CompileErrors errors)
  {
    var op_func_arg_type = op_func.signature.arg_types[1];
    CheckAssign(op_func_arg_type.Get(), rhs, errors);
    return op_func.GetReturnType();
  }

  public IType CheckRelationalBinOp(AnnotatedParseTree lhs, AnnotatedParseTree rhs, CompileErrors errors)
  {
    if(!IsNumeric(lhs.eval_type))
    {
      errors.Add(new ParseError(lhs, "operator is not overloaded"));
      return Bool;
    }

    if(!IsNumeric(rhs.eval_type))
    {
      errors.Add(new ParseError(rhs, "operator is not overloaded"));
      return Bool;
    }

    MatchTypes(rtl_op_res_type, lhs, rhs, errors);

    return Bool;
  }

  public IType CheckEqBinOp(AnnotatedParseTree lhs, AnnotatedParseTree rhs, CompileErrors errors)
  {
    if(lhs.eval_type == rhs.eval_type)
      return Bool;

    if(lhs.eval_type is ClassSymbol && rhs.eval_type is ClassSymbol)
      return Bool;

    //TODO: add INullableType?
    if(((lhs.eval_type is ClassSymbol || lhs.eval_type is InterfaceSymbol || lhs.eval_type is FuncSignature) &&
        rhs.eval_type == Null) ||
       ((rhs.eval_type is ClassSymbol || rhs.eval_type is InterfaceSymbol || rhs.eval_type is FuncSignature) &&
        lhs.eval_type == Null))
      return Bool;

    if((lhs.eval_type == Types.Int && rhs.eval_type is EnumSymbol) ||
       (lhs.eval_type is EnumSymbol && rhs.eval_type == Types.Int))
      return Bool;

    MatchTypes(eq_op_res_type, lhs, rhs, errors);

    return Bool;
  }

  public IType CheckUnaryMinus(AnnotatedParseTree a, CompileErrors errors)
  {
    if(!(a.eval_type == Int || a.eval_type == Float))
      errors.Add(new ParseError(a, "must be numeric type"));

    return a.eval_type;
  }

  public IType CheckBitNot(AnnotatedParseTree a, CompileErrors errors)
  {
    if(a.eval_type != Int)
      errors.Add(new ParseError(a, "must be int type"));

    return Int;
  }

  public IType CheckBitOp(AnnotatedParseTree lhs, AnnotatedParseTree rhs, CompileErrors errors)
  {
    if(lhs.eval_type != Int)
      errors.Add(new ParseError(lhs, "must be int type"));

    if(rhs.eval_type != Int)
      errors.Add(new ParseError(rhs, "must be int type"));

    return Int;
  }

  public IType CheckLogicalOp(AnnotatedParseTree lhs, AnnotatedParseTree rhs, CompileErrors errors)
  {
    if(lhs.eval_type != Bool)
      errors.Add(new ParseError(lhs, "must be bool type"));

    if(rhs.eval_type != Bool)
      errors.Add(new ParseError(rhs, "must be bool type"));

    return Bool;
  }

  public IType CheckLogicalNot(AnnotatedParseTree a, CompileErrors errors)
  {
    if(a.eval_type != Bool)
      errors.Add(new ParseError(a, "must be bool type"));

    return a.eval_type;
  }
#endif
}

}