using System;
using System.Collections.Generic;

namespace bhl {

using marshall;

public interface IType 
{
  string GetName();
}

// For lazy evaluation of types and forward declarations
// TypeProxy is used instead of IType
public struct TypeProxy : IMarshallable
{
  TypeSystem types;
  IType type;
  string _name;
  public string name 
  { 
    get { return _name; } 
    private set { _name = value; }
  }

  public TypeProxy(TypeSystem ts, string name)
  {
    this.types = ts;
    type = null;
    _name = name;
  }

  public TypeProxy(IType type)
  {
    types = null;
    _name = type.GetName();
    this.type = type;
  }

  public bool IsEmpty()
  {
    return string.IsNullOrEmpty(_name) && 
           type == null;
  }

  public IType Get()
  {
    if(type != null)
      return type;

    if(string.IsNullOrEmpty(_name))
      return null;

    type = (bhl.IType)types.Resolve(_name);
    return type;
  }

  public int GetFieldsNum()
  {
    return 1 /*name*/ + 
           1 /*marshallable type or null*/;
  }

  public void Sync(SyncContext ctx)
  {
    if(ctx.is_read)
      types = ((SymbolFactory)ctx.factory).types;
    else if(string.IsNullOrEmpty(_name))
      throw new Exception("TypeProxy name is empty");

    Marshall.Sync(ctx, ref _name);

    IMarshallableGeneric mg = null;
    if(ctx.is_read)
    {
      Marshall.SyncGeneric(ctx, ref mg);
      type = (IType)mg;
    }
    else
    {   
      var resolved = Get();
      bool defined_in_module = resolved is Symbol symb && symb.scope is ModuleScope;

      //NOTE: we want to determine if it's an existing symbol and if so
      //      we don't want to serialize it by passing null instead of it
      if(!defined_in_module)
      {
        mg = resolved as IMarshallableGeneric;
        if(mg == null)
          throw new Exception("Type is not marshallable: " + (resolved != null ? resolved.GetType().Name + " " : "<null> ") + _name);
      }
      Marshall.SyncGeneric(ctx, ref mg);
    }
  }
}

public class RefType : IType
{
  public TypeProxy subj { get; private set; }

  public string GetName() { return "ref " + subj.name; }

  public RefType(TypeProxy subj)
  {
    this.subj = subj;
  }
}

public class TupleType : IType, IMarshallableGeneric
{
  public const uint CLASS_ID = 16;

  string name;

  List<TypeProxy> items = new List<TypeProxy>();

  public string GetName() { return name; }

  public int Count {
    get {
      return items.Count;
    }
  }

  public TupleType(params TypeProxy[] items)
  {
    foreach(var item in items)
      this.items.Add(item);
    Update();
  }

  //symbol factory version
  public TupleType()
  {}

  public TypeProxy this[int index]
  {
    get { 
      return items[index]; 
    }
  }

  public void Add(TypeProxy item)
  {
    items.Add(item);
    Update();
  }

  void Update()
  {
    string tmp = "";
    for(int i=0;i<items.Count;++i)
    {
      if(i > 0)
        tmp += ",";
      tmp += items[i].name;
    }

    name = tmp;
  }

  public uint ClassId()
  {
    return CLASS_ID;
  }

  public int GetFieldsNum()
  {
    return 1;
  }

  public void Sync(SyncContext ctx)
  {
    Marshall.Sync(ctx, items);
    if(ctx.is_read)
      Update();
  }
}

public class FuncSignature : IType, IMarshallableGeneric
{
  public const uint CLASS_ID = 14; 

  public string name;

  public TypeProxy ret_type;
  public List<TypeProxy> arg_types = new List<TypeProxy>();

  public string GetName() { return name; }

  public FuncSignature(TypeProxy ret_type, params TypeProxy[] arg_types)
  {
    this.ret_type = ret_type;
    foreach(var arg_type in arg_types)
      this.arg_types.Add(arg_type);
    Update();
  }

  public FuncSignature(TypeProxy ret_type, List<TypeProxy> arg_types)
  {
    this.ret_type = ret_type;
    this.arg_types = arg_types;
    Update();
  }

  public FuncSignature(TypeProxy ret_type)
  {
    this.ret_type = ret_type;
    Update();
  }

  //symbol factory version
  public FuncSignature()
  {}

  public void AddArg(TypeProxy arg_type)
  {
    arg_types.Add(arg_type);
    Update();
  }

  void Update()
  {
    string tmp = ret_type.name + "^("; 
    for(int i=0;i<arg_types.Count;++i)
    {
      if(i > 0)
        tmp += ",";
      tmp += arg_types[i].name;
    }
    tmp += ")";

    name = tmp;
  }

  public uint ClassId()
  {
    return CLASS_ID;
  }

  public int GetFieldsNum()
  {
    return 2;
  }

  public void Sync(SyncContext ctx)
  {
    Marshall.Sync(ctx, ref ret_type);
    Marshall.Sync(ctx, arg_types);
    if(ctx.is_read)
      Update();
  }
}

public class TypeSystem
{
  static public BoolSymbol Bool = new BoolSymbol();
  static public StringSymbol String = new StringSymbol();
  static public IntSymbol Int = new IntSymbol();
  static public FloatSymbol Float = new FloatSymbol();
  static public VoidSymbol Void = new VoidSymbol();
  static public AnySymbol Any = new AnySymbol();
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
  static Dictionary<Tuple<IType, IType>, IType> promote_from_to = new Dictionary<Tuple<IType, IType>, IType>() 
  {
    { new Tuple<IType, IType>(Int, Float), Float },
  };

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
#endif

  public GlobalScope globs = new GlobalScope();

  List<Scope> sources = new List<Scope>();

  public TypeSystem()
  {
    InitBuiltins(globs);

    AddSource(globs);
  }

  public void AddSource(Scope other)
  {
    if(sources.Contains(other))
      return;
    sources.Add(other);
  }

  void InitBuiltins(GlobalScope globs) 
  {
    globs.Define(Int);
    globs.Define(Float);
    globs.Define(Bool);
    globs.Define(String);
    globs.Define(Void);
    globs.Define(Any);

    //TODO: probably we should get rid of this general fallback, 
    //      once we have proper serialization of types this one 
    //      won't be needed
    globs.Define(new GenericArrayTypeSymbol(this, new TypeProxy(this, "")));

    {
      var fn = new FuncSymbolNative("suspend", Type("void"), 
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          return CoroutineSuspend.Instance;
        } 
      );
      globs.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("yield", Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          return CoroutinePool.New<CoroutineYield>(frm.vm);
        } 
      );
      globs.Define(fn);
    }

    //TODO: this one is controversary, it's defined for BC for now
    {
      var fn = new FuncSymbolNative("fail", Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          status = BHS.FAILURE;
          return null;
        } 
      );
      globs.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("start", Type("int"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          var val_ptr = frm.stack.Pop();
          int id = frm.vm.Start((VM.FuncPtr)val_ptr._obj, frm).id;
          val_ptr.Release();
          frm.stack.Push(Val.NewNum(frm.vm, id));
          return null;
        }, 
        new FuncArgSymbol("p", TypeFunc("void"))
      );
      globs.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("stop", Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          var fid = (int)frm.stack.PopRelease().num;
          frm.vm.Stop(fid);
          return null;
        }, 
        new FuncArgSymbol("fid", Type("int"))
      );
      globs.Define(fn);
    }
  }

  public Symbol Resolve(string name) 
  {
    foreach(var src in sources)
    {
      var s = src.Resolve(name);
      if(s != null)
        return s;
    }
    return null;
  }

  public struct TypeArg
  {
    public string name;
    public TypeProxy tp;

    public static implicit operator TypeArg(string name)
    {
      return new TypeArg(name);
    }

    public static implicit operator TypeArg(TypeProxy tp)
    {
      return new TypeArg(tp);
    }

    public TypeArg(string name)
    {
      this.name = name;
      this.tp = default(TypeProxy);
    }

    public TypeArg(TypeProxy tp)
    {
      this.name = null;
      this.tp = tp;
    }
  }

  public TypeProxy Type(TypeArg tn)
  {
    if(!tn.tp.IsEmpty())
      return tn.tp;
    else
      return Type(tn.name);
  }

  public TypeProxy TypeArr(TypeArg tn)
  {           
    return Type(new GenericArrayTypeSymbol(this, Type(tn)));
  }

  public TypeProxy TypeFunc(TypeArg ret_type, params TypeArg[] arg_types)
  {           
    var sig = new FuncSignature(Type(ret_type));
    foreach(var arg_type in arg_types)
      sig.AddArg(Type(arg_type));
    return Type(sig);
  }

  public TypeProxy TypeTuple(params TypeArg[] types)
  {
    var tuple = new TupleType();
    foreach(var type in types)
      tuple.Add(Type(type));
    return Type(tuple);
  }

  public TypeProxy Type(string name)
  {
    if(name.Length == 0 || IsCompoundType(name))
      throw new Exception("Type name contains illegal characters: '" + name + "'");
    
    return new TypeProxy(this, name);
  }

  public TypeProxy Type(IType t)
  {
    return new TypeProxy(t);
  }

  static public bool IsCompoundType(string name)
  {
    for(int i=0;i<name.Length;++i)
    {
      char c = name[i];
      if(!(Char.IsLetterOrDigit(c) || c == '_'))
        return true;
    }
    return false;
  }

  static public bool CanAssignTo(IType src, IType dst, IType promotion) 
  {
    return src == dst || 
           promotion == dst || 
           dst == Any ||
           (dst is ClassSymbol && src == Null) ||
           (dst is FuncSignature && src == Null) || 
           src.GetName() == dst.GetName() ||
           IsChildClass(src, dst)
           ;
  }

  static public bool IsChildClass(IType t, IType pt) 
  {
    var cl = t as ClassSymbol;
    if(cl == null)
      return false;
    var pcl = pt as ClassSymbol;
    if(pcl == null)
      return false;
    return cl.IsSubclassOf(pcl);
  }

  static public bool IsInSameClassHierarchy(IType a, IType b) 
  {
    var ca = a as ClassSymbol;
    if(ca == null)
      return false;
    var cb = b as ClassSymbol;
    if(cb == null)
      return false;
    return cb.IsSubclassOf(ca) || ca.IsSubclassOf(cb);
  }

  static public bool IsBinOpCompatible(IType type)
  {
    return type == Bool || 
           type == String ||
           type == Int ||
           type == Float;
  }

  static public bool IsRtlOpCompatible(IType type)
  {
    return type == Int ||
           type == Float;
  }

#if BHL_FRONT
  static public IType MatchTypes(Dictionary<Tuple<IType, IType>, IType> table, WrappedParseTree a, WrappedParseTree b) 
  {
    IType result;
    if(!table.TryGetValue(new Tuple<IType, IType>(a.eval_type, b.eval_type), out result))
      throw new SemanticError(a, "incompatible types");
    return result;
  }

  public void CheckAssign(WrappedParseTree lhs, WrappedParseTree rhs) 
  {
    IType promote_to_type = null;
    promote_from_to.TryGetValue(new Tuple<IType, IType>(rhs.eval_type, lhs.eval_type), out promote_to_type);
    if(!CanAssignTo(rhs.eval_type, lhs.eval_type, promote_to_type)) 
      throw new SemanticError(lhs, "incompatible types");
  }

  public void CheckAssign(IType lhs, WrappedParseTree rhs) 
  {
    IType promote_to_type = null;
    promote_from_to.TryGetValue(new Tuple<IType, IType>(rhs.eval_type, lhs), out promote_to_type);
    if(!CanAssignTo(rhs.eval_type, lhs, promote_to_type)) 
      throw new SemanticError(rhs, "incompatible types");
  }

  public void CheckAssign(WrappedParseTree lhs, IType rhs) 
  {
    IType promote_to_type = null;
    promote_from_to.TryGetValue(new Tuple<IType, IType>(rhs, lhs.eval_type), out promote_to_type);
    if(!CanAssignTo(rhs, lhs.eval_type, promote_to_type)) 
      throw new SemanticError(lhs, "incompatible types");
  }

  public void CheckCast(WrappedParseTree type, WrappedParseTree exp) 
  {
    var ltype = type.eval_type;
    var rtype = exp.eval_type;

    if(ltype == rtype)
      return;

    //special case: we allow to cast from 'any' to any user type
    if(ltype == Any || rtype == Any)
      return;

    if((rtype == Float || rtype == Int) && ltype is EnumSymbol)
      return;
    if((ltype == Float || ltype == Int) && rtype is EnumSymbol)
      return;

    if(ltype == String && rtype is EnumSymbol)
      return;

    IType cast_type = null;
    cast_from_to.TryGetValue(new Tuple<IType, IType>(rtype, ltype), out cast_type);
    if(cast_type == ltype)
      return;

    if(IsInSameClassHierarchy(rtype, ltype))
      return;
    
    throw new SemanticError(type, "incompatible types for casting");
  }

  public IType CheckBinOp(WrappedParseTree a, WrappedParseTree b) 
  {
    if(!IsBinOpCompatible(a.eval_type))
      throw new SemanticError(a, "operator is not overloaded");

    if(!IsBinOpCompatible(b.eval_type))
      throw new SemanticError(b, "operator is not overloaded");

    return MatchTypes(bin_op_res_type, a, b);
  }

  public IType CheckBinOpOverload(IScope scope, WrappedParseTree a, WrappedParseTree b, FuncSymbol op_func) 
  {
    var op_func_arg = op_func.GetArgs()[0];
    CheckAssign(op_func_arg.type.Get(), b);
    return op_func.GetReturnType();
  }

  public IType CheckRtlBinOp(WrappedParseTree a, WrappedParseTree b) 
  {
    if(!IsRtlOpCompatible(a.eval_type))
      throw new SemanticError(a, "operator is not overloaded");

    if(!IsRtlOpCompatible(b.eval_type))
      throw new SemanticError(b, "operator is not overloaded");

    MatchTypes(rtl_op_res_type, a, b);

    return Bool;
  }

  public IType CheckEqBinOp(WrappedParseTree a, WrappedParseTree b) 
  {
    if(a.eval_type == b.eval_type)
      return Bool;

    if(a.eval_type is ClassSymbol && b.eval_type is ClassSymbol)
      return Bool;

    //TODO: add INullableType?
    if(((a.eval_type is ClassSymbol || a.eval_type is FuncSignature) && b.eval_type == Null) ||
        ((b.eval_type is ClassSymbol || b.eval_type is FuncSignature) && a.eval_type == Null))
      return Bool;

    MatchTypes(eq_op_res_type, a, b);

    return Bool;
  }

  public IType CheckUnaryMinus(WrappedParseTree a) 
  {
    if(!(a.eval_type == Int || a.eval_type == Float)) 
      throw new SemanticError(a, "must be numeric type");

    return a.eval_type;
  }

  public IType CheckBitOp(WrappedParseTree a, WrappedParseTree b) 
  {
    if(a.eval_type != Int) 
      throw new SemanticError(a, "must be int type");

    if(b.eval_type != Int)
      throw new SemanticError(b, "must be int type");

    return Int;
  }

  public IType CheckLogicalOp(WrappedParseTree a, WrappedParseTree b) 
  {
    if(a.eval_type != Bool) 
      throw new SemanticError(a, "must be bool type");

    if(b.eval_type != Bool)
      throw new SemanticError(b, "must be bool type");

    return Bool;
  }

  public IType CheckLogicalNot(WrappedParseTree a) 
  {
    if(a.eval_type != Bool) 
      throw new SemanticError(a, "must be bool type");

    return a.eval_type;
  }
#endif
}


} //namespace bhl
