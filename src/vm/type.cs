using System;
using System.Collections.Generic;

namespace bhl {

// Denotes any named entity which can be located by this name
public interface INamed
{
  string GetName();
}

public interface IType : INamed
{}

// Denotes named entities which are created 'on the fly'
public interface IEphemeral : INamed
{}

// For lazy evaluation of types and forward declarations
public struct Proxy<T> : marshall.IMarshallable, IEquatable<Proxy<T>> where T : class, INamed
{
  public T named;

  //TODO: we need to serialize/unserialize the original resolver
  public INamedResolver resolver;

  string _path;
  public string path 
  { 
    get { return _path; } 
  }

  public Proxy(INamedResolver resolver, string path)
  {
    if(path.Length == 0)
      throw new Exception("Type path is empty");
    if(Types.IsCompoundType(path))
      throw new Exception("Type spec contains illegal characters: '" + path + "'");
    
    this.resolver = resolver;
    named = default(T);
    _path = path;
  }

  public Proxy(T named)
  {
    resolver = null;
    this.named = named;
    _path = (named is Symbol sym) ? sym.GetFullPath() : named.GetName();
  }

  public bool IsEmpty()
  {
    return string.IsNullOrEmpty(_path) && 
           named == null;
  }

  public T Get()
  {
    if(named != null)
      return named;

    if(string.IsNullOrEmpty(_path))
      return default(T);

    named = resolver.ResolveNamedByPath(_path) as T;

    //TODO: some controversary code below - when resolving a symbol,
    //      forcing its full path to re-write the original path
    if(named != null)
      _path = (named is Symbol sym) ? sym.GetFullPath() : named.GetName();

    return named;
  }

  public void Sync(marshall.SyncContext ctx)
  {
    if(ctx.is_read)
      resolver = ((SymbolFactory)ctx.factory).resolver;

    marshall.Marshall.Sync(ctx, ref _path);

    marshall.IMarshallableGeneric mg = null;
    if(!string.IsNullOrEmpty(_path))
    {
      if(!ctx.is_read)
      {   
        var resolved = Get();

        //NOTE: we want to marshall only those types which are not
        //      defined elsewhere otherwise we just want to keep
        //      string reference at them
        if(resolved is IEphemeral)
        {
          mg = resolved as marshall.IMarshallableGeneric;
          if(mg == null)
            throw new Exception("Type is not marshallable: " + (resolved != null ? resolved.GetType().Name + " " : "<null> ") + _path);
        }
      }
    }

    marshall.Marshall.SyncGeneric(ctx, ref mg);
    if(ctx.is_read)
      named = (T)mg;
  }

  public override string ToString() 
  {
    return _path;
  }

  public override bool Equals(object o)
  {
    if(!(o is Proxy<T>))
      return false;
    return this.Equals((Proxy<T>)o);
  }

  public bool Equals(Proxy<T> o)
  {
    if(o.resolver == resolver && o._path == _path)
      return true;

    if(o.named != null && named != null)
      return o.named.Equals(named);
     
    var r = Get();
    if(r != null)
      return r.Equals(o.Get());
    else
      return null == o.Get();
  }

  public override int GetHashCode()
  {
    return _path.GetHashCode();
  }
}

public class RefType : IType, marshall.IMarshallableGeneric, IEquatable<RefType>, IEphemeral
{
  public const uint CLASS_ID = 17;

  public Proxy<IType> subj; 

  string name;
  public string GetName() { return name; }

  public RefType(Proxy<IType> subj)
  {
    this.subj = subj;
    name = "ref " + subj.path;
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

public class TupleType : IType, marshall.IMarshallableGeneric, IEquatable<TupleType>, IEphemeral
{
  public const uint CLASS_ID = 16;

  string name;

  List<Proxy<IType>> items = new List<Proxy<IType>>();

  public string GetName() { return name; }

  public int Count {
    get {
      return items.Count;
    }
  }

  public TupleType(params Proxy<IType>[] items)
  {
    foreach(var item in items)
      this.items.Add(item);
    Update();
  }

  //symbol factory version
  public TupleType()
  {}

  public Proxy<IType> this[int index]
  {
    get { 
      return items[index]; 
    }
  }

  public void Add(Proxy<IType> item)
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
      tmp += items[i].path;
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

  public override string ToString()
  {
    return name;
  }
}

public class FuncSignature : IType, marshall.IMarshallableGeneric, IEquatable<FuncSignature>, IEphemeral
{
  public const uint CLASS_ID = 14; 

  //full type name
  string name;

  public Proxy<IType> ret_type;
  //TODO: include arg names as well since we support named args?
  public List<Proxy<IType>> arg_types = new List<Proxy<IType>>();

  public int default_args_num;

  public bool has_variadic;

  public bool is_coro;

  public string GetName() { return name; }

  public FuncSignature(bool is_coro, Proxy<IType> ret_type, params Proxy<IType>[] arg_types)
  {
    this.is_coro = is_coro;
    this.ret_type = ret_type;
    foreach(var arg_type in arg_types)
      this.arg_types.Add(arg_type);
    Update();
  }

  public FuncSignature(bool is_coro, Proxy<IType> ret_type, List<Proxy<IType>> arg_types)
  {
    this.is_coro = is_coro;
    this.ret_type = ret_type;
    this.arg_types = arg_types;
    Update();
  }

  public FuncSignature(bool is_coro, Proxy<IType> ret_type)
  {
    this.is_coro = is_coro;
    this.ret_type = ret_type;
    Update();
  }

  //symbol factory version
  public FuncSignature()
  {}

  public void AddArg(Proxy<IType> arg_type)
  {
    arg_types.Add(arg_type);
    Update();
  }

  void Update()
  {
    string tmp = "func " + ret_type.path + "("; 
    for(int i=0;i<arg_types.Count;++i)
    {
      if(i > 0)
        tmp += ",";
      if(has_variadic && i == arg_types.Count-1)
        tmp += "...";
      tmp += arg_types[i].path;
    }
    tmp += ")";
    if(is_coro)
      tmp = "coro " + tmp;

    name = tmp;
  }

  public uint ClassId()
  {
    return CLASS_ID;
  }

  public void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref is_coro);
    marshall.Marshall.Sync(ctx, ref ret_type);
    marshall.Marshall.Sync(ctx, arg_types);
    marshall.Marshall.Sync(ctx, ref default_args_num);
    marshall.Marshall.Sync(ctx, ref has_variadic);
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
    //TODO: 'non-coro' function is a subset of a 'coro' one
    if(is_coro && !o.is_coro)
      return false;
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

  public override string ToString() 
  {
    return name;
  }
}

public class Types : INamedResolver
{
  static public BoolSymbol Bool = new BoolSymbol();
  static public StringSymbol String = new StringSymbol();
  static public IntSymbol Int = new IntSymbol();
  static public FloatSymbol Float = new FloatSymbol();
  static public VoidSymbol Void = new VoidSymbol();
  static public AnySymbol Any = new AnySymbol();
  static public VarSymbol Var = new VarSymbol();
  static public NullSymbol Null = new NullSymbol();
  static public ClassSymbolNative ClassType = null;
  static public FuncSymbolNative YieldFunc = null;

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

  public Namespace ns;

  public NativeFuncIndex nfunc_index = new NativeFuncIndex();

  Dictionary<string, Module> modules = new Dictionary<string, Module>(); 

  static Types()
  {
    {
      ClassType = new ClassSymbolNative("Type", 
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
      ClassType.Setup();
    }
  }

  public Types()
  {
    ns = new Namespace(nfunc_index, "", "");

    InitBuiltins();

    RegisterModule(std.MakeModule(this)); 
    RegisterModule(std.io.MakeModule(this)); 
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

  Types(NativeFuncIndex nfunc_index, Namespace ns)
  {
    this.nfunc_index = nfunc_index;
    this.ns = ns;
  }

  public INamed ResolveNamedByPath(string name)
  {
    return ns.ResolveSymbolByPath(name);
  }

  public Types Clone()
  {
    var clone = new Types(new NativeFuncIndex(nfunc_index), ns.Clone());
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
    ns.Define(Var);
    ns.Define(ClassType);

    {
      //it's a builtin non-directly available function
      var fn = new FuncSymbolNative("$yield", this.T("void"),
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
        { 
          return CoroutinePool.New<CoroutineYield>(frm.vm);
        } 
      );
      YieldFunc = fn;
      ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("suspend", FuncAttrib.Coro, this.T("void"), 0, 
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
        { 
          return CoroutineSuspend.Instance;
        } 
      );
      ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("start", this.T("int"),
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
        { 
          var val_ptr = stack.Pop();
          int id = frm.vm.Start((VM.FuncPtr)val_ptr._obj, frm, stack).id;
          val_ptr.Release();
          stack.Push(Val.NewNum(frm.vm, id));
          return null;
        }, 
        new FuncArgSymbol("p", this.TFunc(true/*is coro*/, "void"))
      );
      ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("stop", this.T("void"),
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
        { 
          var fid = (int)stack.PopRelease().num;
          frm.vm.Stop(fid);
          return null;
        }, 
        new FuncArgSymbol("fid", this.T("int"))
      );
      ns.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("type", this.T("Type"),
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
        { 
          var o = stack.Pop();
          stack.Push(Val.NewObj(frm.vm, o.type, ClassType));
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

  static public bool Is(IType type, IType dest_type) 
  {
    if(type == null || dest_type == null)
      return false;
    else if(type.Equals(dest_type))
      return true;
    else if(type is IInstanceType ti && dest_type is IInstanceType di)
    {
      var tset = ti.GetAllRelatedTypesSet();
      var dset = di.GetAllRelatedTypesSet();

      return tset.IsSupersetOf(dset);
    }
    else
      return false;
  }

  static public bool CheckCast(IType dest_type, IType from_type) 
  {
    if(dest_type == from_type)
      return true;

    //special case: we allow to cast from 'any' to any user type
    if(dest_type == Any || from_type == Any)
      return true;

    if((from_type == Float || from_type == Int) && dest_type is EnumSymbol)
      return true;
    if((dest_type == Float || dest_type == Int) && from_type is EnumSymbol)
      return true;

    if(dest_type == String && from_type is EnumSymbol)
      return true;

    IType cast_type = null;
    cast_from_to.TryGetValue(new Tuple<IType, IType>(from_type, dest_type), out cast_type);
    if(cast_type == dest_type)
      return true;

    if(IsInSameHierarchy(from_type, dest_type))
      return true;
    
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

  static public bool IsNumeric(IType type)
  {
    return type == Int ||
           type == Float;
  }

#if BHL_FRONT
  static public IType MatchTypes(Dictionary<Tuple<IType, IType>, IType> table, WrappedParseTree lhs, WrappedParseTree rhs) 
  {
    IType result;
    if(!table.TryGetValue(new Tuple<IType, IType>(lhs.eval_type, rhs.eval_type), out result))
      throw new SemanticError(rhs, "incompatible types: '" + lhs.eval_type?.GetFullPath() + "' and '" + rhs.eval_type?.GetFullPath() + "'");
    return result;
  }

  static bool CanAssignTo(IType lhs, IType rhs) 
  {
    if(rhs == null || lhs == null)
      return false;

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
    if(!CanAssignTo(lhs.eval_type, rhs.eval_type)) 
      throw new SemanticError(rhs, "incompatible types: '" + lhs.eval_type?.GetFullPath() + "' and '" + rhs.eval_type?.GetFullPath() + "'");
  }

  //NOTE: SemanticError(..) is attached to rhs, not lhs. 
  //      Usually this is the case when expression (rhs) is passed
  //      to a func arg (lhs) and it this case it makes sense
  //      to report about the site where it's actually passed (rhs),
  //      not where it's defined (lhs)
  public void CheckAssign(IType lhs, WrappedParseTree rhs) 
  {
    if(!CanAssignTo(lhs, rhs.eval_type)) 
      throw new SemanticError(rhs, "incompatible types: '" + lhs?.GetFullPath() + "' and '" + rhs.eval_type?.GetFullPath() + "'");
  }

  public void CheckAssign(WrappedParseTree lhs, IType rhs) 
  {
    if(!CanAssignTo(lhs.eval_type, rhs)) 
      throw new SemanticError(lhs, "incompatible types: '" + lhs.eval_type?.GetFullPath() + "' and '" + rhs?.GetFullPath() + "'");
  }

  static public void CheckCast(WrappedParseTree dest, WrappedParseTree from) 
  {
    var dest_type = dest.eval_type;
    var from_type = from.eval_type;

    if(!CheckCast(dest_type, from_type))
      throw new SemanticError(dest, "incompatible types for casting: '" + dest_type?.GetFullPath() + "' and '" + from_type?.GetFullPath() + "'");
  }

  public IType CheckBinOp(WrappedParseTree lhs, WrappedParseTree rhs) 
  {
    if(!IsBinOpCompatible(lhs.eval_type))
      throw new SemanticError(lhs, "operator is not overloaded");

    if(!IsBinOpCompatible(rhs.eval_type))
      throw new SemanticError(rhs, "operator is not overloaded");

    return MatchTypes(bin_op_res_type, lhs, rhs);
  }

  public IType CheckBinOpOverload(IScope scope, WrappedParseTree lhs, WrappedParseTree rhs, FuncSymbol op_func) 
  {
    var op_func_arg_type = op_func.signature.arg_types[1];
    CheckAssign(op_func_arg_type.Get(), rhs);
    return op_func.GetReturnType();
  }

  public IType CheckRtlBinOp(WrappedParseTree lhs, WrappedParseTree rhs) 
  {
    if(!IsNumeric(lhs.eval_type))
      throw new SemanticError(lhs, "operator is not overloaded");

    if(!IsNumeric(rhs.eval_type))
      throw new SemanticError(rhs, "operator is not overloaded");

    MatchTypes(rtl_op_res_type, lhs, rhs);

    return Bool;
  }

  public IType CheckEqBinOp(WrappedParseTree lhs, WrappedParseTree rhs) 
  {
    if(lhs.eval_type == rhs.eval_type)
      return Bool;

    if(lhs.eval_type is ClassSymbol && rhs.eval_type is ClassSymbol)
      return Bool;

    //TODO: add INullableType?
    if(((lhs.eval_type is ClassSymbol || lhs.eval_type is InterfaceSymbol || lhs.eval_type is FuncSignature) && rhs.eval_type == Null) ||
        ((rhs.eval_type is ClassSymbol || rhs.eval_type is InterfaceSymbol || rhs.eval_type is FuncSignature) && lhs.eval_type == Null))
      return Bool;

    MatchTypes(eq_op_res_type, lhs, rhs);

    return Bool;
  }

  public IType CheckUnaryMinus(WrappedParseTree a) 
  {
    if(!(a.eval_type == Int || a.eval_type == Float)) 
      throw new SemanticError(a, "must be numeric type");

    return a.eval_type;
  }

  public IType CheckBitOp(WrappedParseTree lhs, WrappedParseTree rhs) 
  {
    if(lhs.eval_type != Int) 
      throw new SemanticError(lhs, "must be int type");

    if(rhs.eval_type != Int)
      throw new SemanticError(rhs, "must be int type");

    return Int;
  }

  public IType CheckLogicalOp(WrappedParseTree lhs, WrappedParseTree rhs) 
  {
    if(lhs.eval_type != Bool) 
      throw new SemanticError(lhs, "must be bool type");

    if(rhs.eval_type != Bool)
      throw new SemanticError(rhs, "must be bool type");

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
