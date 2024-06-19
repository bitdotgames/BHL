using System;
using System.Collections.Generic;
using bhl.marshall;

namespace bhl {

// Denotes any named entity which can be located by this name
public interface INamed
{
  string GetName();
}

public interface IType : INamed
{}

public interface ITypeRefIndexable
{
  void IndexTypeRefs(TypeRefIndex refs);
}

// Denotes types which are created 'on the fly'
public interface IEphemeralType : IType, ITypeRefIndexable, IMarshallableGeneric
{}

public interface IInstantiable : IType, IScope 
{
  HashSet<IInstantiable> GetAllRelatedTypesSet();
}

// For lazy evaluation of types and forward declarations
public struct ProxyType : IMarshallable, IEquatable<ProxyType>
{
  public IType resolved;
  
  public INamedResolver resolver;
  //NOTE: for symbols it's a full absolute path from the very top namespace
  public string path;

  public ProxyType(INamedResolver resolver, string path)
  {
    if(path.Length == 0)
      throw new Exception("Type path is empty");
    if(Types.IsCompoundType(path))
      throw new Exception("Type spec contains illegal characters: '" + path + "'");
    
    this.resolver = resolver;
    this.path = path;
    
    resolved = null;
  }

  public ProxyType(IType obj)
  {
    resolver = null;
    path = null;
    
    resolved = null;
    
    SetResolved(obj);
  }

  public bool IsEmpty()
  {
    return string.IsNullOrEmpty(path) && 
           resolved == null;
  }

  public bool IsNull()
  {
    return resolved == null && 
           resolver == null && path == null;
  }

  public void Clear()
  {
    path = null;
    resolved = null;
  }

  public IType Get()
  {
    if(resolved != null)
      return resolved;

    if(!string.IsNullOrEmpty(path))
      SetResolved((IType)resolver.ResolveNamedByPath(path));

    return resolved;
  }

  void SetResolved(IType resolved)
  {
    this.resolved = resolved;
    //TODO: some smelly code below - after resolving we re-write the original path
    //      with the normalized one, this is useful for cases when comparing proxies
    //      pointing to types withing namespaces, e.g: func(fns.Item) vs func(Item)
    if(resolved != null)
      path = GetNormalizedPath(resolved);
  }

  static string GetNormalizedPath(IType obj)
  {
    //for symbols full path is used
    return (obj is Symbol sym) ? sym.GetFullPath() : obj.GetName();
  }

  public void Sync(marshall.SyncContext ctx)
  {
    if(ctx.is_read)
      resolver = ((SymbolFactory)ctx.factory).resolver;

    bool is_ephemeral = false;
    if(!ctx.is_read)
      is_ephemeral = Get() is IEphemeralType;
    
    marshall.Marshall.Sync(ctx, ref is_ephemeral);
    
    if(is_ephemeral)
    {
      var eph = ctx.is_read ? null : Get() as IMarshallableGeneric;
      marshall.Marshall.SyncGeneric(ctx, ref eph);
      if(ctx.is_read)
        SetResolved((IType)eph);
    }
    else
      marshall.Marshall.Sync(ctx, ref path);
  }

  public override string ToString()
  {
    if(string.IsNullOrEmpty(path))
      Get();
    return path;
  }

  public override bool Equals(object o)
  {
    if(!(o is ProxyType))
      return false;
    return this.Equals((ProxyType)o);
  }

  public bool Equals(ProxyType o)
  {
    if(resolved != null && o.resolved != null)
      return o.resolved.Equals(resolved);
    else if(resolver != null && o.resolver == resolver && o.path == path)
      return true;
     
    //OK nothing worked, let's resolve the type
    Get();

    if(resolved != null)
      return resolved.Equals(o.Get());
    else //null check
      return null == o.Get();
  }

  public override int GetHashCode()
  {
    if(resolved != null)
      return resolved.GetHashCode();
    
    return path?.GetHashCode() ?? 0;
  }
}

public class RefType : IEphemeralType, IEquatable<RefType>
{
  public const uint CLASS_ID = 17;

  public ProxyType subj; 

  string name;
  public string GetName() { return name; }

  public RefType(ProxyType subj)
  {
    this.subj = subj;
    Update();
  }

  //marshall factory version
  public RefType()
  {}

  public uint ClassId()
  {
    return CLASS_ID;
  }

  void Update()
  {
    name = "ref " + subj;
  }

  public void IndexTypeRefs(TypeRefIndex refs)
  {
    refs.Index(subj);
  }
  
  public void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.SyncRef(ctx, ref subj);
    if(ctx.is_read)
      Update();
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
  
  public override string ToString()
  {
    return name;
  }
}

public class TupleType : IEphemeralType, IEquatable<TupleType>
{
  public const uint CLASS_ID = 16;

  string name;

  List<ProxyType> items = new List<ProxyType>();

  public string GetName() { return name; }

  public int Count {
    get {
      return items.Count;
    }
  }

  public TupleType(params ProxyType[] items)
  {
    foreach(var item in items)
      this.items.Add(item);
    Update();
  }

  //symbol factory version
  public TupleType()
  {}

  public ProxyType this[int index]
  {
    get { 
      return items[index]; 
    }
  }

  public void Add(ProxyType item)
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
      tmp += items[i];
    }

    name = tmp;
  }

  public uint ClassId()
  {
    return CLASS_ID;
  }

  public void IndexTypeRefs(TypeRefIndex refs)
  {
    refs.Index(items);
  }
  
  public void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.SyncRefs(ctx, items);
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

public class FuncSignature : IEphemeralType, IEquatable<FuncSignature>
{
  public const uint CLASS_ID = 14; 

  //full type name
  string name;

  public ProxyType ret_type;
  //TODO: include arg names as well since we support named args?
  public List<ProxyType> arg_types = new List<ProxyType>();

  byte _attribs = 0;
  public FuncSignatureAttrib attribs {
    get {
      return (FuncSignatureAttrib)_attribs;
    }
    set {
      _attribs = (byte)value;
    }
  }

  public string GetName() { return name; }

  public FuncSignature(ProxyType ret_type, params ProxyType[] arg_types)
    : this(0, ret_type, arg_types)
  {}

  public FuncSignature(FuncSignatureAttrib attribs, ProxyType ret_type, params ProxyType[] arg_types)
  {
    this.attribs = attribs;
    this.ret_type = ret_type;
    foreach(var arg_type in arg_types)
      this.arg_types.Add(arg_type);
    Update();
  }

  public FuncSignature(FuncSignatureAttrib attribs, ProxyType ret_type, List<ProxyType> arg_types)
  {
    this.attribs = attribs;
    this.ret_type = ret_type;
    this.arg_types = arg_types;
    Update();
  }

  //symbol factory version
  public FuncSignature()
  {}

  public void AddArg(ProxyType arg_type)
  {
    arg_types.Add(arg_type);
    Update();
  }

  void Update()
  {
    string buf = 
      "func " + ret_type + "("; 
    if(attribs.HasFlag(FuncSignatureAttrib.Coro))
      buf = "coro " + buf;
    for(int i=0;i<arg_types.Count;++i)
    {
      if(i > 0)
        buf += ",";
      if(attribs.HasFlag(FuncSignatureAttrib.VariadicArgs) && i == arg_types.Count-1)
        buf += "...";
      buf += arg_types[i];
    }
    buf += ")";

    name = buf;
  }

  public uint ClassId()
  {
    return CLASS_ID;
  }

  public void IndexTypeRefs(TypeRefIndex refs)
  {
    refs.Index(ret_type);
    refs.Index(arg_types);
  }

  public void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref _attribs);
    marshall.Marshall.SyncRef(ctx, ref ret_type);
    marshall.Marshall.SyncRefs(ctx, arg_types);
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
    if(attribs.HasFlag(FuncSignatureAttrib.Coro) && !o.attribs.HasFlag(FuncSignatureAttrib.Coro))
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
  static public ClassSymbolNative Type = 
    new ClassSymbolNative(new Origin(), "Type", 
         delegate(VM.Frame frm, ref Val v, IType type) 
         { 
           v.SetObj(null, type);
         }
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
  public Namespace ns {
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
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol _)
        {
          v.SetInt(ctx.str.Length);
        },
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
          var t = (IType)ctx.obj;
          v.SetStr(t.GetName());
        },
        null
      );
      Type.Define(fld);
    }
    Type.Setup();
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

  public INamed ResolveNamedByPath(string name)
  {
    return ns.ResolveSymbolByPath(name);
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
    else if(type is IInstantiable ti && dest_type is IInstantiable di)
    {
      var tset = ti.GetAllRelatedTypesSet();
      var dset = di.GetAllRelatedTypesSet();

      return tset.IsSupersetOf(dset);
    }
    else
      return false;
  }

  static public bool Is(Val v, IType dest_type) 
  {
    if(v?.obj != null && dest_type is INativeType ndi)
      return ndi.GetNativeType().IsAssignableFrom(v.obj.GetType());
    else
      return Is(v?.type, dest_type);
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
  static public IType MatchTypes(Dictionary<Tuple<IType, IType>, IType> table, AnnotatedParseTree lhs, AnnotatedParseTree rhs, CompileErrors errors) 
  {
    IType result;
    if(!table.TryGetValue(new Tuple<IType, IType>(lhs.eval_type, rhs.eval_type), out result))
      errors.Add(new ParseError(rhs, "incompatible types: '" + lhs.eval_type.GetFullPath() + "' and '" + rhs.eval_type.GetFullPath() + "'"));
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
      errors.Add(new ParseError(rhs, "incompatible types: '" + lhs.eval_type.GetFullPath() + "' and '" + rhs.eval_type.GetFullPath() + "'"));
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
      errors.Add(MakeIncompatibleTypesError(rhs, lhs.GetFullPath(), rhs.eval_type.GetFullPath()));
      return false;
    }
    return true;
  }

  public bool CheckAssign(AnnotatedParseTree lhs, IType rhs, CompileErrors errors) 
  {
    if(!CanAssignTo(lhs.eval_type, rhs)) 
    {
      errors.Add(MakeIncompatibleTypesError(lhs, lhs.eval_type.GetFullPath(), rhs.GetFullPath()));
      return false;
    }
    return true;
  }

  static ParseError MakeIncompatibleTypesError(AnnotatedParseTree pt, string lhs_path, string rhs_path)
  {
    return new ParseError(pt, "incompatible types: '" + lhs_path + "' and '" + rhs_path + "'");
  }

  static public bool CheckCast(AnnotatedParseTree dest, AnnotatedParseTree from, CompileErrors errors) 
  {
    var dest_type = dest.eval_type;
    var from_type = from.eval_type;

    if(!CheckCast(dest_type, from_type))
    {
      errors.Add(new ParseError(dest, "incompatible types for casting: '" + dest_type.GetFullPath() + "' and '" + from_type.GetFullPath() + "'"));
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

  public IType CheckBinOpOverload(IScope scope, AnnotatedParseTree lhs, AnnotatedParseTree rhs, FuncSymbol op_func, CompileErrors errors) 
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
    if(((lhs.eval_type is ClassSymbol || lhs.eval_type is InterfaceSymbol || lhs.eval_type is FuncSignature) && rhs.eval_type == Null) ||
        ((rhs.eval_type is ClassSymbol || rhs.eval_type is InterfaceSymbol || rhs.eval_type is FuncSignature) && lhs.eval_type == Null))
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


} //namespace bhl
