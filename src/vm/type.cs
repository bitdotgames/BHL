using System;
using System.Collections.Generic;

namespace bhl {

public interface IType 
{
  string GetName();
}

// For lazy evaluation of types and forward declarations
// TypeProxy is used instead of IType
public struct TypeProxy : marshall.IMarshallable, IEquatable<TypeProxy>
{
  ISymbolResolver resolver;
  IType type;
  string _spec;
  public string spec 
  { 
    get { return _spec; } 
    private set { _spec = value; }
  }

  public TypeProxy(ISymbolResolver resolver, string spec)
  {
    if(spec.Length == 0)
      throw new Exception("Type spec is empty");
    if(Types.IsCompoundType(spec))
      throw new Exception("Type spec contains illegal characters: '" + spec + "'");
    
    this.resolver = resolver;
    type = null;
    _spec = spec;
  }

  public TypeProxy(IType type)
  {
    resolver = null;
    if(type != null)
      _spec = (type is Symbol sym) ? sym.GetFullPath() : type.GetName();
    else 
      _spec = null;
    this.type = type;
  }

  public static implicit operator TypeProxy(BuiltInSymbolType s)
  {
    return new TypeProxy((IType)s);
  }

  public bool IsEmpty()
  {
    return string.IsNullOrEmpty(_spec) && 
           type == null;
  }

  public IType Get()
  {
    if(type != null)
      return type;

    if(string.IsNullOrEmpty(_spec))
      return null;

    type = (bhl.IType)resolver.ResolveSymbolByPath(_spec);
    return type;
  }

  public void Sync(marshall.SyncContext ctx)
  {
    if(ctx.is_read)
      resolver = ((SymbolFactory)ctx.factory).resolver;

    marshall.Marshall.Sync(ctx, ref _spec);

    marshall.IMarshallableGeneric mg = null;
    if(!string.IsNullOrEmpty(_spec))
    {
      if(!ctx.is_read)
      {   
        var resolved = Get();
        //TODO: make this check more robust
        bool is_weak_ref = 
          resolved is Symbol symb && 
          (symb is BuiltInSymbolType ||
           symb is ClassSymbolNative ||
           symb is ClassSymbolScript ||
           symb is InterfaceSymbolNative ||
           symb is InterfaceSymbolScript ||
           symb is EnumSymbol ||
           (symb is ArrayTypeSymbol && !(symb is GenericArrayTypeSymbol))
           );

        //NOTE: we want to marshall only those types which are not
        //      defined elsewhere otherwise we just want to keep
        //      string reference at them
        if(!is_weak_ref)
        {
          mg = resolved as marshall.IMarshallableGeneric;
          if(mg == null)
            throw new Exception("Type is not marshallable: " + (resolved != null ? resolved.GetType().Name + " " : "<null> ") + _spec);
        }
      }
    }

    marshall.Marshall.SyncGeneric(ctx, ref mg);
    if(ctx.is_read)
      type = (IType)mg;
  }

  public override string ToString() 
  {
    return _spec;
  }

  public override bool Equals(object o)
  {
    if(!(o is TypeProxy))
      return false;
    return this.Equals((TypeProxy)o);
  }

  public bool Equals(TypeProxy o)
  {
    if(o.type != null && type != null)
      return o.type.Equals(type);
     
    //Console.WriteLine(o._spec + " VS " + _spec + " : " + o.resolver?.GetType().Name + " VS " + resolver?.GetType().Name);
    if(o.resolver == resolver && o._spec == _spec)
      return true;

    var r = Get();
    if(r != null)
      return r.Equals(o.Get());
    else
      return Get() == null;
  }

  public override int GetHashCode()
  {
    return _spec.GetHashCode();
  }
}

public class RefType : IType, marshall.IMarshallableGeneric, IEquatable<RefType>
{
  public const uint CLASS_ID = 17;

  public TypeProxy subj; 

  string name;
  public string GetName() { return name; }

  public RefType(TypeProxy subj)
  {
    this.subj = subj;
    name = "ref " + subj.spec;
  }

  //marshall factory version
  public RefType()
  {}

  public uint ClassId()
  {
    return CLASS_ID;
  }

  public void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref subj);
  }

  public override bool Equals(object o)
  {
    if(!(o is RefType))
      return false;
    return this.Equals((RefType)o);
  }

  public bool Equals(RefType o)
  {
    if(ReferenceEquals(o, null))
      return false;
    if(ReferenceEquals(this, o))
      return true;
    return subj.Equals(o.subj);
  }

  public override int GetHashCode()
  {
    return name.GetHashCode();
  }
}

public class TupleType : IType, marshall.IMarshallableGeneric, IEquatable<TupleType>
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
      tmp += items[i].spec;
    }

    name = tmp;
  }

  public uint ClassId()
  {
    return CLASS_ID;
  }

  public void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, items);
    if(ctx.is_read)
      Update();
  }

  public override bool Equals(object o)
  {
    if(!(o is TupleType))
      return false;
    return this.Equals((TupleType)o);
  }

  public bool Equals(TupleType o)
  {
    if(ReferenceEquals(o, null))
      return false;
    if(ReferenceEquals(this, o))
      return true;
    if(items.Count != o.items.Count)
      return false;
    for(int i=0;i<items.Count;++i)
      if(!items[i].Equals(o.items[i]))
        return false;
    return true;
  }

  public override int GetHashCode()
  {
    return name.GetHashCode();
  }
}

public class FuncSignature : IType, marshall.IMarshallableGeneric, IEquatable<FuncSignature>
{
  public const uint CLASS_ID = 14; 

  //full type name
  string name;

  public TypeProxy ret_type;
  //TODO: include arg names as well since we support named args?
  public List<TypeProxy> arg_types = new List<TypeProxy>();

  public int default_args_num;

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
    string tmp = "func " + ret_type.spec + "("; 
    for(int i=0;i<arg_types.Count;++i)
    {
      if(i > 0)
        tmp += ",";
      tmp += arg_types[i].spec;
    }
    tmp += ")";

    name = tmp;
  }

  public uint ClassId()
  {
    return CLASS_ID;
  }

  public void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref ret_type);
    marshall.Marshall.Sync(ctx, arg_types);
    marshall.Marshall.Sync(ctx, ref default_args_num);
    if(ctx.is_read)
      Update();
  }

  public override bool Equals(object o)
  {
    if(!(o is FuncSignature))
      return false;
    return this.Equals((FuncSignature)o);
  }

  public bool Equals(FuncSignature o)
  {
    if(ReferenceEquals(o, null))
      return false;
    if(ReferenceEquals(this, o))
      return true;
    if(!ret_type.Equals(o.ret_type))
      return false;
    if(arg_types.Count != o.arg_types.Count)
      return false;
    for(int i=0;i<arg_types.Count;++i)
      if(!arg_types[i].Equals(o.arg_types[i]))
        return false;
    return true;
  }

  public override int GetHashCode()
  {
    return name.GetHashCode();
  }
}

public class Types : ISymbolResolver
{
  static public BoolSymbol Bool = new BoolSymbol();
  static public StringSymbol String = new StringSymbol();
  static public IntSymbol Int = new IntSymbol();
  static public FloatSymbol Float = new FloatSymbol();
  static public VoidSymbol Void = new VoidSymbol();
  static public AnySymbol Any = new AnySymbol();
  static public NullSymbol Null = new NullSymbol();
  static public ClassSymbolNative ClassType = null;

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

  public Namespace ns;

  //NOTE: used for global symbol indices (e.g native funcs)
  public Symbol2Index gindex = new Symbol2Index();

  static Types()
  {
    {
      ClassType = new ClassSymbolNative("Type", null, 
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          v.SetObj(null, type);
        }
      );

      {
        var fld = new FieldSymbol("Name", String, 
          delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol _)
          {
            var t = (IType)ctx.obj;
            v.SetStr(t.GetName());
          },
          null
        );
        ClassType.Define(fld);
      }
    }
  }

  public Types()
  {
    ns = new Namespace(gindex, "", "");

    InitBuiltins();

    std.Init(this);
  }

  Types(Symbol2Index native_indices, Namespace ns)
  {
    this.gindex = native_indices;
    this.ns = ns;
  }

  public Symbol ResolveSymbolByPath(string name)
  {
    return ns.ResolveSymbolByPath(name);
  }

  public Types Clone()
  {
    var clone = new Types(gindex.Clone(), ns.Clone());
    return clone;
  }

  void InitBuiltins() 
  {
    ns.Define(Int);
    ns.Define(Float);
    ns.Define(Bool);
    ns.Define(String);
    ns.Define(Void);
    ns.Define(Any);
    ns.Define(ClassType);

    {
      var fn = new FuncSymbolNative("suspend", this.T("void"), 
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          return CoroutineSuspend.Instance;
        } 
      );
      ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("yield", this.T("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          return CoroutinePool.New<CoroutineYield>(frm.vm);
        } 
      );
      ns.Define(fn);
    }

    //TODO: this one is controversary, it's defined for BC for now
    {
      var fn = new FuncSymbolNative("fail", this.T("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          status = BHS.FAILURE;
          return null;
        } 
      );
      ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("start", this.T("int"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          var val_ptr = frm.stack.Pop();
          int id = frm.vm.Start((VM.FuncPtr)val_ptr._obj, frm).id;
          val_ptr.Release();
          frm.stack.Push(Val.NewNum(frm.vm, id));
          return null;
        }, 
        new FuncArgSymbol("p", this.TFunc("void"))
      );
      ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("stop", this.T("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          var fid = (int)frm.stack.PopRelease().num;
          frm.vm.Stop(fid);
          return null;
        }, 
        new FuncArgSymbol("fid", this.T("int"))
      );
      ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("type", this.T("Type"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          var o = frm.stack.Pop();
          frm.stack.Push(Val.NewObj(frm.vm, o.type, ClassType));
          o.Release();
          return null;
        }, 
        new FuncArgSymbol("o", this.T("any"))
      );
      ns.Define(fn);
    }
  }

  static public bool IsCompoundType(string name)
  {
    for(int i=0;i<name.Length;++i)
    {
      char c = name[i];
      if(!(Char.IsLetterOrDigit(c) || c == '_' || c == '.'))
        return true;
    }
    return false;
  }

  static public bool Is(IType a, IType b) 
  {
    if(a == b)
      return true;
    //TODO: looks a bit like a hack, e.g. what about namespace short names clashing?
    else if(a.GetName() == b.GetName())
      return true;
    else if(a is IInstanceType ai && b is IInstanceType bi)
    {
      var aset = ai.GetAllRelatedTypesSet();
      var bset = bi.GetAllRelatedTypesSet();
      
      return aset.IsSupersetOf(bset);
    }
    else
      return false;
  }

  static public bool IsInSameHierarchy(IType a, IType b) 
  {
    return Is(a, b) || Is(b, a);
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

  static public bool CanAssignTo(IType rhs, IType lhs) 
  {
    return rhs == lhs || 
           lhs == Any ||
           (is_subset_of.Contains(new Tuple<IType, IType>(rhs, lhs))) ||
           (lhs is IInstanceType && rhs == Null) ||
           (lhs is FuncSignature && rhs == Null) || 
           Is(rhs, lhs)
           ;
  }

  public void CheckAssign(WrappedParseTree lhs, WrappedParseTree rhs) 
  {
    if(!CanAssignTo(rhs.eval_type, lhs.eval_type)) 
      throw new SemanticError(lhs, "incompatible types");
  }

  public void CheckAssign(IType lhs, WrappedParseTree rhs) 
  {
    if(!CanAssignTo(rhs.eval_type, lhs)) 
      throw new SemanticError(rhs, "incompatible types");
  }

  public void CheckAssign(WrappedParseTree lhs, IType rhs) 
  {
    if(!CanAssignTo(rhs, lhs.eval_type)) 
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

    if(IsInSameHierarchy(rtype, ltype))
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
    var op_func_arg_type = op_func.signature.arg_types[0];
    CheckAssign(op_func_arg_type.Get(), b);
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
    if(((a.eval_type is ClassSymbol || a.eval_type is InterfaceSymbol || a.eval_type is FuncSignature) && b.eval_type == Null) ||
        ((b.eval_type is ClassSymbol || b.eval_type is InterfaceSymbol || b.eval_type is FuncSignature) && a.eval_type == Null))
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
