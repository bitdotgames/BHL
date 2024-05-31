using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

public abstract class Symbol : INamed, marshall.IMarshallableGeneric 
{
  public string name;

  // All symbols know what scope contains them
  public IScope scope;

  // Information about origin where symbol is defined: file and line, parse tree
  public Origin origin;

  public Symbol(Origin origin, string name) 
  { 
    this.origin = origin;
    this.name = name; 
  }

  public string GetName()
  {
    return name;
  }

  public override string ToString() 
  {
    return name;
  }

  public abstract uint ClassId();

  public abstract void IndexTypeRefs(marshall.SyncContext ctx);
  public abstract void Sync(marshall.SyncContext ctx);
}

public class IntSymbol : Symbol, IType
{
  public const uint CLASS_ID = 1;

  public IntSymbol()
    : base(new Origin(), "int")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }
  
  //contains no data
  public override void IndexTypeRefs(marshall.SyncContext ctx)
  {}
  public override void Sync(marshall.SyncContext ctx)
  {}

  //for convenience
  public static implicit operator Proxy<IType>(IntSymbol s)
  {
    return new Proxy<IType>(s);
  }
}

public class BoolSymbol : Symbol, IType
{
  public const uint CLASS_ID = 2;

  public BoolSymbol()
    : base(new Origin(), "bool")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  //contains no data
  public override void IndexTypeRefs(marshall.SyncContext ctx)
  {}
  public override void Sync(marshall.SyncContext ctx)
  {}

  //for convenience
  public static implicit operator Proxy<IType>(BoolSymbol s)
  {
    return new Proxy<IType>(s);
  }
}

public class StringSymbol : ClassSymbolNative
{
  public const uint CLASS_ID = 3;

  public StringSymbol()
    : base(new Origin(), "string", native_type: typeof(string))
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }
  
  //contains no data
  public override void IndexTypeRefs(marshall.SyncContext ctx)
  {}
  public override void Sync(marshall.SyncContext ctx)
  {}

  //for convenience
  public static implicit operator Proxy<IType>(StringSymbol s)
  {
    return new Proxy<IType>(s);
  }
}

public class FloatSymbol : Symbol, IType
{
  public const uint CLASS_ID = 4;

  public FloatSymbol()
    : base(new Origin(), "float")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  //contains no data
  public override void IndexTypeRefs(marshall.SyncContext ctx)
  {}
  public override void Sync(marshall.SyncContext ctx)
  {}

  //for convenience
  public static implicit operator Proxy<IType>(FloatSymbol s)
  {
    return new Proxy<IType>(s);
  }
}

public class VoidSymbol : Symbol, IType
{
  public const uint CLASS_ID = 5;

  public VoidSymbol()
    : base(new Origin(), "void")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  //contains no data
  public override void IndexTypeRefs(marshall.SyncContext ctx)
  {}
  public override void Sync(marshall.SyncContext ctx)
  {}

  //for convenience
  public static implicit operator Proxy<IType>(VoidSymbol s)
  {
    return new Proxy<IType>(s);
  }
}

public class AnySymbol : Symbol, IType
{
  public const uint CLASS_ID = 6;

  public AnySymbol()
    : base(new Origin(), "any")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  //contains no data
  public override void IndexTypeRefs(marshall.SyncContext ctx)
  {}
  public override void Sync(marshall.SyncContext ctx)
  {}

  //for convenience
  public static implicit operator Proxy<IType>(AnySymbol s)
  {
    return new Proxy<IType>(s);
  }
}

//NOTE: for better 'null ref. error' reports we extend the ClassSymbolScript  
public class NullSymbol : ClassSymbolScript
{
  public new const uint CLASS_ID = 7;

  public NullSymbol()
    : base(new Origin(), "null")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  //contains no data
  public override void IndexTypeRefs(marshall.SyncContext ctx)
  {}
  public override void Sync(marshall.SyncContext ctx)
  {}
}

public class VarSymbol : Symbol
{
  public const uint CLASS_ID = 8;

  public VarSymbol()
    : base(new Origin(), "var")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  //contains no data
  public override void IndexTypeRefs(marshall.SyncContext ctx)
  {}
  public override void Sync(marshall.SyncContext ctx)
  {}
}

public abstract class InterfaceSymbol : Symbol, IInstantiable, IEnumerable<Symbol>
{
  internal SymbolsStorage members;

  public TypeSet<InterfaceSymbol> inherits = new TypeSet<InterfaceSymbol>();

  HashSet<IInstantiable> related_types;

  public InterfaceSymbol(Origin origin, string name)
    : base(origin, name)
  {
    this.members = new SymbolsStorage(this);
  }

  //marshall factory version
  public InterfaceSymbol()
    : this(null, null)
  {}

  public void Define(Symbol sym)
  {
    if(!(sym is FuncSymbol))
      throw new Exception("Only function symbols supported, given " + sym?.GetType().Name);
    
    members.Add(sym);
  }

  public Symbol Resolve(string name) 
  {
    var sym =  members.Find(name);
    if(sym != null)
      return sym;

    for(int i=0;i<inherits.Count;++i)
    {
      var tmp = inherits[i].Resolve(name);
      if(tmp != null)
        return tmp;
    }

    return null;
  }

  public IScope GetFallbackScope() { return scope; }

  public IEnumerator<Symbol> GetEnumerator() { return members.GetEnumerator(); }
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public void SetInherits(IList<InterfaceSymbol> inherits)
  {
    if(inherits == null)
      return;

    foreach(var ext in inherits)
      this.inherits.Add(ext);
  }

  public FuncSymbol FindMethod(string name)
  {
    return Resolve(name) as FuncSymbol;
  }

  public HashSet<IInstantiable> GetAllRelatedTypesSet()
  {
    if(related_types == null)
    {
      related_types = new HashSet<IInstantiable>();
      related_types.Add(this);
      for(int i=0;i<inherits.Count;++i)
      {
        var ext = (IInstantiable)inherits[i];
        if(!related_types.Contains(ext))
          related_types.UnionWith(ext.GetAllRelatedTypesSet());
      }
    }
    return related_types;
  }
}

public class InterfaceSymbolScript : InterfaceSymbol
{
  public const uint CLASS_ID = 18;
  
  public InterfaceSymbolScript(Origin origin, string name)
    : base(origin, name)
  {}

  //marshall factory version
  public InterfaceSymbolScript() 
    : this(null, null)
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void IndexTypeRefs(marshall.SyncContext ctx)
  {
    inherits.IndexTypeRefs(ctx);
    foreach(var m in members)
      m.IndexTypeRefs(ctx);
    
  }
  
  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.Sync(ctx, ref inherits); 
    marshall.Marshall.Sync(ctx, ref members); 
  }
}

public class InterfaceSymbolNative : InterfaceSymbol, INativeType
{
  IList<Proxy<IType>> proxy_inherits;
  FuncSymbol[] funcs;
  System.Type native_type;

  public InterfaceSymbolNative(
    Origin origin,
    string name, 
    IList<Proxy<IType>> proxy_inherits,
    params FuncSymbol[] funcs
  )
    : base(origin, name)
  {
    this.proxy_inherits = proxy_inherits;
    this.funcs = funcs;
  }

  public InterfaceSymbolNative(
    Origin origin,
    string name, 
    IList<Proxy<IType>> proxy_inherits,
    System.Type native_type,
    params FuncSymbol[] funcs
  )
    : this(origin, name, proxy_inherits, funcs)
  {
    this.native_type = native_type;
  }

  public System.Type GetNativeType()
  {
    return native_type;
  }
  
  public void Setup()
  {
    List<InterfaceSymbol> inherits = null;
    if(proxy_inherits != null && proxy_inherits.Count > 0)
    {
      inherits = new List<InterfaceSymbol>();
      foreach(var pi in proxy_inherits)
      {
        var iface = pi.Get() as InterfaceSymbol;
        if(iface == null) 
          throw new Exception("Inherited interface not found" + pi);

        inherits.Add(iface);
      }
      SetInherits(inherits);
    }

    foreach(var func in funcs)
      Define(func);
  }

  public override uint ClassId()
  {
    throw new NotImplementedException();
  }

  public override void IndexTypeRefs(marshall.SyncContext ctx)
  {
    foreach(var i in proxy_inherits)
      i.IndexTypeRefs(ctx);
  }
  
  public override void Sync(marshall.SyncContext ctx)
  {
    throw new NotImplementedException();
  }
}

public abstract class ClassSymbol : Symbol, IInstantiable, IEnumerable<Symbol>
{
  public ClassSymbol super_class {
    get {
      return _super_class.Get();
    }
  }

  internal Proxy<ClassSymbol> _super_class;

  public TypeSet<InterfaceSymbol> implements = new TypeSet<InterfaceSymbol>();

  //contains mapping of implemented interface methods indices 
  //to actual class methods indices:
  //  [IFoo][0=>3,1=>1,1=>0]
  //  [IBar][0=>2,1=>0]
  internal Dictionary<InterfaceSymbol, List<FuncSymbol>> _itable;
  internal Dictionary<int, FuncSymbol> _vtable;

  HashSet<IInstantiable> related_types;

  //contains only 'local' members without taking into account inheritance
  internal SymbolsStorage members;
  //all 'flattened' members including all parents
  internal SymbolsStorage _all_members;

  // we want to prevent resolving of attributes and methods at some point
  // since they might collide with types. For example:
  // class Foo {
  //   a.A a <-- here attribute 'a' will prevent proper resolving of 'a.A' type  
  // }
  // setting this attribute controls this behavior
  internal bool _resolve_only_decl_members;

  public VM.ClassCreator creator;

  public ClassSymbol(
    Origin origin,
    string name, 
    VM.ClassCreator creator = null
  )
    : base(origin, name)
  {
    this.members = new SymbolsStorage(this);

    this.creator = creator;
  }

  public void SetSuperClassAndInterfaces(ClassSymbol super_class, IList<InterfaceSymbol> implements = null)
  {
    if(super_class != null)
      _super_class = new Proxy<ClassSymbol>(super_class);

    if(implements != null && implements.Count > 0)
      SetImplementedInterfaces(implements);
  }

  //NOTE: only once the class is Setup we have valid members iterator
  public IEnumerator<Symbol> GetEnumerator() { return _all_members.GetEnumerator(); }
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public IScope GetFallbackScope() 
  {
    return this.scope;
  }

  public string GetNativeStaticFieldGetFuncName(FieldSymbol fld)
  {
    return "$__" + ((Symbol)this).GetFullPath() + "_get_" + fld.name;
  }

  public string GetNativeStaticFieldSetFuncName(FieldSymbol fld)
  {
    return "$__" + ((Symbol)this).GetFullPath() + "_set_" + fld.name;
  }

  static public bool IsBinaryOp(string op)
  {
    if(op == "+")
      return true;
    else if(op == "-")
      return true;
    else if(op == "==")
      return true;
    else if(op == "!=")
      return true;
    else if(op == "*")
      return true;
    else if(op == "/")
      return true;
    else if(op == "%")
      return true;
    else if(op == ">")
      return true;
    else if(op == ">=")
      return true;
    else if(op == "<")
      return true;
    else if(op == "<=")
      return true;
    else
      return false; 
  }

  public void Define(Symbol sym) 
  {
    if(sym is FuncSymbol fs)
    {
      if(IsBinaryOp(fs.name))
        CheckBinaryOpOverload(fs);

      if(fs is FuncSymbolScript fss)
        this.GetModule().func_index.Index(fss);
      //NOTE: for now indexing at the module level only static native methods
      //      due to possible unavailability of module for 'generic classes'
      //      created on the fly (array, map)
      else if(fs is FuncSymbolNative fsn && fs.attribs.HasFlag(FuncAttrib.Static))
        this.GetModule().nfunc_index.Index(fsn);
    }
    else if(sym is FieldSymbol fld && fld.attribs.HasFlag(FieldAttrib.Static)) 
    {
      if(this is ClassSymbolNative)
      {
        //let's create fake static get/set native functions
        var static_get = new FuncSymbolNative(
        new Origin(),
        GetNativeStaticFieldGetFuncName(fld), fld.type,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
        {
          var res = Val.New(frm.vm);
          fld.getter(frm, null, ref res, fld);
          stack.Push(res);
          return null;
        });
        this.GetModule().nfunc_index.Index(static_get);

        var static_set = new FuncSymbolNative(
        new Origin(),
        GetNativeStaticFieldSetFuncName(fld), fld.type,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
        {
          Val ctx = null;
          var val = stack.Pop();
          fld.setter(frm, ref ctx, val, fld);
          val.Release();
          return null;
        });
        this.GetModule().nfunc_index.Index(static_set);
      }
      else
        this.GetModule().gvar_index.Index(fld);
    }

    //NOTE: we don't check if there are any parent symbols with the same name, 
    //      they will be checked once the class is finally setup
    members.Add(sym);
  }

  static void CheckBinaryOpOverload(FuncSymbol fs)
  {
    if(!fs.attribs.HasFlag(FuncAttrib.Static))
      throw new SymbolError(fs, "operator overload must be static");

    if(fs.GetTotalArgsNum() != 2)
      throw new SymbolError(fs, "operator overload must have exactly 2 arguments");

    if(fs.GetReturnType() == Types.Void)
      throw new SymbolError(fs, "operator overload return value can't be void");
  }

  public Symbol Resolve(string name) 
  {
    var sym = DoResolve(name);
    if(_resolve_only_decl_members && (sym is VariableSymbol || sym is FuncSymbol))
      return null;
    return sym;
  }

  Symbol DoResolve(string name)
  {
    var sym = members.Find(name);
    if(sym != null)
      return sym;
    if(super_class != null)
    {
      var super_sym = super_class.Resolve(name);
      if(super_sym != null)
        return super_sym;
    }
    return null;
  }

  internal void SetImplementedInterfaces(IList<InterfaceSymbol> implements)
  {
    this.implements.Clear();
    foreach(var imp in implements)
      this.implements.Add(imp);
  }

  public virtual void Setup()
  {
    ValidateInterfaces();

    SetupAllMembers();

    SetupVirtTables();
  }

  void ValidateInterfaces()
  {
    for(int i=0;i<implements.Count;++i)
      ValidateImplementation(implements[i]);
  }

  void ValidateImplementation(InterfaceSymbol iface)
  {
    var set = iface.GetAllRelatedTypesSet();
    foreach(var item in set)
    {
      var item_if = (InterfaceSymbol)item;
      for(int i=0;i<item_if.members.Count;++i)
      {
        var m = (FuncSymbol)item_if.members[i];
        var func_symb = Resolve(m.name) as FuncSymbol;
        if(func_symb == null || !Types.Is(func_symb.signature, m.signature))
          throw new SymbolError(this, "class '" + name + "' doesn't implement interface '" + iface.name + "' method '" + m + "'");
      }
    }
  }

  void SetupAllMembers()
  {
    if(_all_members != null)
      return;

    _all_members = new SymbolsStorage(this);

    DoSetupMembers(this);
  }

  void DoSetupMembers(ClassSymbol curr_class)
  {
    if(curr_class.super_class != null)
      DoSetupMembers(curr_class.super_class);

    for(int i=0;i<curr_class.members.Count;++i)
    {
      var sym = curr_class.members[i];

      //NOTE: we need to recalculate attribute index taking account all 
      //      parent classes
      if(sym is IScopeIndexed si && !sym.IsStatic())
        si.scope_idx = _all_members.Count; 

      if(sym is FuncSymbolScript fss)
      {
        if(fss.attribs.HasFlag(FuncAttrib.Virtual))
        {
          if(fss.default_args_num > 0)
            throw new SymbolError(sym, "virtual methods are not allowed to have default arguments");

          var vsym = new FuncSymbolVirtual(fss);
          vsym.AddOverride(curr_class, fss);
          _all_members.Add(vsym);
        }
        else if(fss.attribs.HasFlag(FuncAttrib.Override))
        {
          if(fss.default_args_num > 0)
            throw new SymbolError(sym, "virtual methods are not allowed to have default arguments");

          var vsym = _all_members.Find(sym.name) as FuncSymbolVirtual;
          if(vsym == null)
            throw new SymbolError(sym, "no base virtual method to override");

          vsym.AddOverride(curr_class, fss); 
        }
        else
          _all_members.Add(fss);
      }
      else
        _all_members.Add(sym);
    }
  }

  void SetupVirtTables()
  {
    if(_vtable != null)
      return;

    //virtual methods lookup table
    _vtable = new Dictionary<int, FuncSymbol>();
    
    for(int i=0;i<_all_members.Count;++i)
    {
      if(_all_members[i] is FuncSymbolVirtual fssv)
        _vtable[i] = fssv.GetTopOverride();
    }

    //interfaces lookup table
    _itable = new Dictionary<InterfaceSymbol, List<FuncSymbol>>();

    var all = new HashSet<IInstantiable>();
    if(super_class != null)
    {
      for(int i=0;i<super_class.implements.Count;++i)
        all.UnionWith(super_class.implements[i].GetAllRelatedTypesSet());
    }
    for(int i=0;i<implements.Count;++i)
      all.UnionWith(implements[i].GetAllRelatedTypesSet());

    foreach(var imp in all)
    {
      var ifs2fn = new List<FuncSymbol>();
      var ifs = (InterfaceSymbol)imp;
      _itable.Add(ifs, ifs2fn);

      for(int midx=0;midx<ifs.members.Count;++midx)
      {
        var m = ifs.members[midx];
        var symb = Resolve(m.name) as FuncSymbol;
        if(symb == null)
          throw new Exception("No such method '" + m.name + "' in class '" + this.name + "'");
        ifs2fn.Add(symb);
      }
    }
  }

  public HashSet<IInstantiable> GetAllRelatedTypesSet()
  {
    if(related_types == null)
      related_types = CollectAllRelatedTypesSet();
    return related_types;
  }

  protected virtual HashSet<IInstantiable> CollectAllRelatedTypesSet()
  {
    var related_types = new HashSet<IInstantiable>();
    related_types.Add(this);
    if(super_class != null)
      related_types.UnionWith(super_class.GetAllRelatedTypesSet());
    for(int i=0;i<implements.Count;++i)
    {
      if(!related_types.Contains(implements[i]))
        related_types.UnionWith(implements[i].GetAllRelatedTypesSet());
    }
    return related_types;
  }
}

public interface IModuleIndexed
{
  //index in a module
  int module_idx { get; set; }
}

public interface IScopeIndexed
{
  //index in an enclosing scope
  int scope_idx { get; set; }
}

public interface ITyped
{
  IType GetIType();
}

public class VariableSymbol : Symbol, ITyped, IScopeIndexed
{
  public const uint CLASS_ID = 8;

  public Proxy<IType> type;

  int _scope_idx = -1;
  public int scope_idx {
    get {
      return _scope_idx;
    }
    set {
      _scope_idx = value;
    }
  }

#if BHL_FRONT
  //referenced upvalue, keep in mind it can be a 'local' variable from the   
  //outer lambda wrapping the current one
  internal VariableSymbol _upvalue;
#endif

  public VariableSymbol(Origin origin, string name, Proxy<IType> type) 
    : base(origin, name) 
  {
    this.type = type;
  }

  //marshall factory version
  public VariableSymbol()
    : base(null, "")
  {}

#if BHL_FRONT
  public IType GuessType() 
  {
    return origin?.parsed == null ? type.Get() : origin.parsed.eval_type;  
  }
#endif
  
  public IType GetIType()
  {
    return type.Get();
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void IndexTypeRefs(marshall.SyncContext ctx)
  {
    type.IndexTypeRefs(ctx);
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.SyncRef(ctx, ref type);
    marshall.Marshall.Sync(ctx, ref _scope_idx);
  }

  public override string ToString()
  {
    return type + " " + name;
  }
}

public class GlobalVariableSymbol : VariableSymbol
{
  new public const uint CLASS_ID = 22;

  public bool is_local;

  public GlobalVariableSymbol(Origin origin, string name, Proxy<IType> type) 
    : base(origin, name, type) 
  {}
  
  //marshall factory version
  public GlobalVariableSymbol()
    : base(null, "", new Proxy<IType>())
  {}

  public override void Sync(marshall.SyncContext ctx)
  {
    base.Sync(ctx);

    marshall.Marshall.Sync(ctx, ref is_local);
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }
}

public class FuncArgSymbol : VariableSymbol
{
  new public const uint CLASS_ID = 19;

  public bool is_ref;

  public FuncArgSymbol(Origin origin, string name, Proxy<IType> type, bool is_ref = false)
    : base(origin, name, type)
  {
    this.is_ref = is_ref;
  }

  //convenience version where we don't care about exact origin (e.g native
  //func bindings)
  public FuncArgSymbol(string name, Proxy<IType> type, bool is_ref = false)
    : this(new Origin(), name, type, is_ref)
  {}

  //marshall factory version
  public FuncArgSymbol()
    : this(null, "", new Proxy<IType>())
  {}

  public override void Sync(marshall.SyncContext ctx)
  {
    base.Sync(ctx);

    marshall.Marshall.Sync(ctx, ref is_ref);
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }
}

[System.Flags]
public enum FieldAttrib : byte
{
  None        = 0,
  Static      = 1,
}

public class FieldSymbol : VariableSymbol
{
  public delegate void FieldGetter(VM.Frame frm, Val v, ref Val res, FieldSymbol fld);
  public delegate void FieldSetter(VM.Frame frm, ref Val v, Val nv, FieldSymbol fld);
  public delegate void FieldRef(VM.Frame frm, Val v, out Val res, FieldSymbol fld);

  public FieldGetter getter;
  public FieldSetter setter;
  public FieldRef getref;

  protected byte _attribs = 0;
  public FieldAttrib attribs {
    get {
      return (FieldAttrib)_attribs;
    }
    set {
      _attribs = (byte)value;
    }
  }

  public FieldSymbol(Origin origin, string name, Proxy<IType> type, FieldGetter getter = null, FieldSetter setter = null, FieldRef getref = null) 
    : this(origin, name, 0, type, getter, setter, getref)
  {
  }

  public FieldSymbol(Origin origin, string name, FieldAttrib attribs, Proxy<IType> type, FieldGetter getter = null, FieldSetter setter = null, FieldRef getref = null) 
    : base(origin, name, type)
  {
    this.attribs = attribs;

    this.getter = getter;
    this.setter = setter;
    this.getref = getref;
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    base.Sync(ctx);

    marshall.Marshall.Sync(ctx, ref _attribs);
  }
}

public class FieldSymbolScript : FieldSymbol
{
  new public const uint CLASS_ID = 9;

  public FieldSymbolScript(Origin origin, string name, Proxy<IType> type) 
    : base(origin, name, type, null, null, null)
  {
    this.getter = Getter;
    this.setter = Setter;
    this.getref = Getref;
  }
  //marshall factory version
  public FieldSymbolScript()
    : this(null, "", new Proxy<IType>())
  {}

  void Getter(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
  {
    var m = (IList<Val>)ctx.obj;
    v.ValueCopyFrom(m[scope_idx]);
  }

  void Setter(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
  {
    var m = (IList<Val>)ctx.obj;
    var curr = m[scope_idx];
    for(int i=0;i<curr._refs;++i)
    {
      v.RefMod(RefOp.USR_INC);
      curr.RefMod(RefOp.USR_DEC);
    }
    curr.ValueCopyFrom(v);
  }

  void Getref(VM.Frame frm, Val ctx, out Val v, FieldSymbol fld)
  {
    var m = (IList<Val>)ctx.obj;
    v = m[scope_idx];
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }
}

[System.Flags]
public enum FuncSignatureAttrib : byte
{
  None           = 0,
  Coro           = 1,
  VariadicArgs   = 2,
  //it's a mask which includes all enum bits, 
  //can be used e.g to convert FuncAttrib into FuncSignatureAttrib
  FuncAttribMask = 3
}

//NOTE: it's a superset of FuncSignatureAttrib
[System.Flags]
public enum FuncAttrib : byte
{
  None         = 0,
  Coro         = 1,
  VariadicArgs = 2,
  Virtual      = 4,
  Override     = 8,
  Static       = 16,
}

public abstract class FuncSymbol : Symbol, ITyped, IScope, 
  IScopeIndexed, IModuleIndexed, IEnumerable<Symbol>
{
  FuncSignature _signature;
  public FuncSignature signature {
    get {
      return _signature;
    }
    set {
      _signature = value;

      _signature.attribs.SetFuncAttrib(ref _attribs);
    }
  }

  internal SymbolsStorage members;

  internal FuncSymbolVirtual _virtual;

  int _module_idx = -1;
  public int module_idx {
    get {
      return _module_idx;
    }
    set {
      _module_idx = value;
    }
  }

  int _scope_idx = -1;
  public int scope_idx {
    get {
      return _scope_idx;
    }
    set {
      _scope_idx = value;
    }
  }

  protected byte _attribs = 0;
  public FuncAttrib attribs {
    get {
      return (FuncAttrib)_attribs;
    }
    set {
      _attribs = (byte)value;
      signature.attribs = value.ToFuncSignatureAttrib();
    }
  }

  public FuncSymbol(
    Origin origin,
    string name, 
    FuncSignature sig
  ) 
    : base(origin, name)
  {
    this.members = new SymbolsStorage(this);
    this.signature = sig;
  }

  public IType GetIType()
  {
    return signature;
  }

  public virtual Symbol Resolve(string name) 
  {
    return members.Find(name);
  }

  public virtual void Define(Symbol sym)
  {
    members.Add(sym);
  }

  public IEnumerator<Symbol> GetEnumerator() { return members.GetEnumerator(); }
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  internal struct EnforceThisScope : IScope
  {
    ClassSymbol cs;

    internal EnforceThisScope(ClassSymbolScript cs)
    {
      this.cs = cs;
    }

    public Symbol Resolve(string name)
    {
      var symb = cs.Resolve(name);

      //NOTE: let's force members to be prefixed with 'this'
      if(symb is VariableSymbol)
        return null;
      else if(symb is FuncSymbol)
        return null;

      return symb;
    }

    public void Define(Symbol sym)
    {
      throw new NotImplementedException();
    }

    public IScope GetFallbackScope()
    {
      return cs.scope;
    }
  }

  public IScope GetFallbackScope() 
  { 
    //NOTE: If declared as a class method we force the fallback
    //      scope to be special wrapper scope. This way we
    //      force the class members to be prefixed with 'this.'
    if(!attribs.HasFlag(FuncAttrib.Static) && scope is ClassSymbolScript cs)
      return new EnforceThisScope(cs);
    else
      return this.scope; 
  }

  public IType GetReturnType()
  {
    return signature.ret_type.Get();
  }

  public int GetTotalArgsNum() { return signature.arg_types.Count; }
  public abstract int GetDefaultArgsNum();
  public int GetRequiredArgsNum() { return GetTotalArgsNum() - GetDefaultArgsNum(); } 

  public bool HasDefaultArgAt(int i)
  {
    if(i >= GetTotalArgsNum())
      return false;
    return i >= (GetDefaultArgsNum() - GetDefaultArgsNum());
  }

  public FuncArgSymbol TryGetArg(int idx)
  {
    idx = idx + ((scope is ClassSymbolScript) ? 1 : 0);
    if(idx < 0 || idx >= members.Count) 
      return null;
    return (FuncArgSymbol)members[idx];
  }

  public FuncArgSymbol GetArg(int idx)
  {
    int this_offset = (scope is ClassSymbolScript) ? 1 : 0;
    return (FuncArgSymbol)members[this_offset + idx];
  }

  public int FindArgIdx(string name)
  {
    int idx = members.IndexOf(name);
    if(idx == -1)
      return idx;
    int this_offset = (scope is ClassSymbolScript) ? 1 : 0;
    return idx - this_offset;
  }

  public override void IndexTypeRefs(marshall.SyncContext ctx)
  {
    _signature.IndexTypeRefs(ctx);
    foreach(var m in members)
      m.IndexTypeRefs(ctx);
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.Sync(ctx, ref _attribs);
    marshall.Marshall.Sync(ctx, ref _signature);
    marshall.Marshall.Sync(ctx, ref _scope_idx);
    marshall.Marshall.Sync(ctx, ref members);
  }

  public override string ToString()
  {
    string buf = 
      "func " + signature.ret_type + " " + name +"("; 
    if(signature.attribs.HasFlag(FuncSignatureAttrib.Coro))
      buf = "coro " + buf;
    for(int i=0;i<signature.arg_types.Count;++i)
    {
      if(i > 0)
        buf += ",";
      if(signature.attribs.HasFlag(FuncSignatureAttrib.VariadicArgs) && i == signature.arg_types.Count-1)
        buf += "...";
      buf += signature.arg_types[i] + " " + members[i].name;
    }
    buf += ")";

    return buf;
  }
}

public class FuncSymbolScript : FuncSymbol
{
  public const uint CLASS_ID = 13; 

  //cached value of Module, it's set upon module loading in VM and 
  internal Module _module;

  //used for up values resolving during parsing
  internal LocalScope _current_scope;

  public int local_vars_num;
  public int default_args_num;
  public int ip_addr;

  public FuncSymbolScript(
    Origin origin,
    FuncSignature sig,
    string name,
    int default_args_num = 0,
    int ip_addr = -1
  ) 
    : base(origin, name, sig)
  {
    this.name = name;
    this.default_args_num = default_args_num;
    this.ip_addr = ip_addr;
  }

  //symbol factory version
  public FuncSymbolScript()
    : base(null, null, new FuncSignature())
  {}

  public void ReserveThisArgument(ClassSymbolScript class_scope)
  {
    var this_symb = new FuncArgSymbol("this", new Proxy<IType>(class_scope));
    Define(this_symb);
  }

  public override void Define(Symbol sym) 
  {
    if(!(sym is FuncArgSymbol))
      throw new Exception("Only func arguments allowed, got: " + sym.GetType().Name);

    ++local_vars_num;

    base.Define(sym);
  }

  public override int GetDefaultArgsNum() { return default_args_num; }

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    base.Sync(ctx);

    marshall.Marshall.Sync(ctx, ref local_vars_num);
    marshall.Marshall.Sync(ctx, ref default_args_num);
    marshall.Marshall.Sync(ctx, ref ip_addr);
  }
}

public class FuncSymbolVirtual : FuncSymbol
{
  protected int default_args_num;

  public List<FuncSymbol> overrides = new List<FuncSymbol>();
  public List<Proxy<ClassSymbol>> owners = new List<Proxy<ClassSymbol>>(); 

  public FuncSymbolVirtual(Origin origin, string name, FuncSignature signature, int default_args_num = 0)
    : base(origin, name, signature)
  {
    this.default_args_num = default_args_num;
  }

  public FuncSymbolVirtual(FuncSymbolScript proto) 
    : this(proto.origin, proto.name, proto.signature, proto.default_args_num)
  {
    //NOTE: directly adding arguments avoiding Define
    for(int m=0;m<proto.members.Count;++m)
      members.Add(proto.members[m]);
    this.origin = proto.origin;
  }

  public override int GetDefaultArgsNum() { return default_args_num; }

  public override uint ClassId()
  {
    throw new NotImplementedException();
  }

  public override void Define(Symbol sym) 
  {
    throw new NotImplementedException();
  }

  public void AddOverride(ClassSymbol owner, FuncSymbol fs)
  {
    //NOTE: we call fs.signature(signature) but not signature.Equals(fs.signature) on purpose,
    //      since we want to allow 'non-coro' functions to be a subset(compatible) of 'coro' ones
    if(!fs.signature.Equals(signature))
      throw new SymbolError(fs, "virtual method signature doesn't match the base one: '" + signature + "' and  '" + fs.signature + "'");

    fs._virtual = this;

    owners.Add(new Proxy<ClassSymbol>(owner));
    overrides.Add(fs);
  }

  public FuncSymbol FindOverride(ClassSymbol owner)
  {
    for(int i=0;i<owners.Count;++i)
    {
      if(owners[i].Get() == owner)
        return overrides[i];
    }
    return null;
  }

  public FuncSymbol GetTopOverride()
  {
    return overrides[overrides.Count-1];
  }
}

#if BHL_FRONT
public class LambdaSymbol : FuncSymbolScript
{
  List<AST_UpVal> upvals;

  List<FuncSymbolScript> fdecl_stack;

  Dictionary<VariableSymbol, UpvalMode> captures;

  public LambdaSymbol(
    AnnotatedParseTree parsed, 
    string name,
    FuncSignature sig,
    List<AST_UpVal> upvals,
    Dictionary<VariableSymbol, UpvalMode> captures,
    List<FuncSymbolScript> fdecl_stack
  ) 
    : base(parsed, sig, name, 0, -1)
  {
    this.upvals = upvals;
    this.captures = captures;
    this.fdecl_stack = fdecl_stack;
  }

  VariableSymbol AddUpValue(VariableSymbol src)
  {
    var local = new VariableSymbol(src.origin, src.name, src.type);

    local._upvalue = src;

    //NOTE: we want to avoid possible recursion during resolve
    //      checks that's why we use a non-checking version
    this._current_scope.DefineWithoutEnclosingChecks(local);

    var upval = new AST_UpVal(
      local.name, 
      local.scope_idx, 
      src.scope_idx,
      //TODO: should be the line of its usage              
      //(in case of 'this' there's no associated parse tree, 
      // so we pass a line number)
      src.origin.source_line
    ); 
    upval.mode = DetectCaptureMode(src);
    upvals.Add(upval);

    return local;
  }

  UpvalMode DetectCaptureMode(VariableSymbol s)
  {
    UpvalMode m;
    captures.TryGetValue(s, out m);
    return m;
  }

  public override Symbol Resolve(string name) 
  {
    var s = base.Resolve(name);
    if(s != null)
      return s;

    return ResolveUpValue(name);
  }

  Symbol ResolveUpValue(string name)
  {
    int my_idx = FindMyIdxInStack();
    if(my_idx == -1)
      return null;

    for(int i=my_idx;i-- > 0;)
    {
      var fdecl = fdecl_stack[i];

      if(fdecl._current_scope != null)
      {
        //let's try to find a variable inside a function
        var res = fdecl._current_scope.ResolveWithFallback(name);
        //checking if it's a variable and not a global one
        if(res is VariableSymbol vs && !(vs.scope is Namespace))
        {
          var local = AssignUpValues(vs, i+1, my_idx);
          //NOTE: returning local instead of an original variable since we need 
          //      proper index of the local variable in the local frame
          return local;
        }
      }
    }

    return null;
  }

  int FindMyIdxInStack()
  {
    for(int i=0;i<fdecl_stack.Count;++i)
    {
      if(fdecl_stack[i] == this)
        return i;
    }
    return -1;
  }

  VariableSymbol AssignUpValues(VariableSymbol vs, int from_idx, int to_idx)
  {
    VariableSymbol most_nested = null;
    //now let's put this result into all nested lambda scopes 
    for(int j=from_idx;j<=to_idx;++j)
    {
      if(fdecl_stack[j] is LambdaSymbol lmb)
      {
        //NOTE: for nested lambdas a 'local' variable may become an 'upvalue'
        vs = lmb.AddUpValue(vs);
        if(most_nested == null)
          most_nested = vs;
      }
    }
    return most_nested;
  }
}
#endif

public class Origin
{
#if BHL_FRONT
  public AnnotatedParseTree parsed;
#endif
  public string native_file_path;
  public int native_line;

  public string source_file { 
    get {
#if BHL_FRONT
      if(parsed != null)
        return parsed.file;
#endif
      return native_file_path;
    }
  }

  public int source_line {
    get {
      return source_range.start.line; 
    }
  }

  public SourceRange source_range {
    get {
#if BHL_FRONT
      if(parsed != null)
        return parsed.range;
#endif
      return new SourceRange(new SourcePos(native_line, 1));
    }
  }

#if BHL_FRONT
  public Origin(AnnotatedParseTree ptree)
  {
    this.parsed = ptree;
  }

  public static implicit operator Origin(AnnotatedParseTree ptree)
  {
    return new Origin(ptree);
  }
#endif

  public Origin(
    [System.Runtime.CompilerServices.CallerFilePath] string file_path = "",
    [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
  )
  {
    this.native_file_path = file_path;
    this.native_line = line;
  }
}

public class FuncSymbolNative : FuncSymbol
{
  public delegate Coroutine Cb(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status); 
  public Cb cb;

  int default_args_num;

  public FuncSymbolNative(
    Origin cinfo,
    string name, 
    Proxy<IType> ret_type, 
    Cb cb,
    params FuncArgSymbol[] args
  ) 
    : this(cinfo, name, ret_type, 0, cb, args)
  {}

  public FuncSymbolNative(
    Origin cinfo,
    string name, 
    Proxy<IType> ret_type, 
    int def_args_num,
    Cb cb,
    params FuncArgSymbol[] args
  ) 
    : this(cinfo, name, FuncAttrib.None, ret_type, def_args_num, cb, args)
  {}

  public FuncSymbolNative(
    Origin origin,
    string name, 
    FuncAttrib attribs,
    Proxy<IType> ret_type, 
    int default_args_num,
    Cb cb,
    params FuncArgSymbol[] args
  ) 
    : base(origin, name, new FuncSignature(attribs.ToFuncSignatureAttrib(), ret_type))
  {
    this.attribs = attribs;

    this.cb = cb;
    this.default_args_num = default_args_num;

    foreach(var arg in args)
    {
      base.Define(arg);
      signature.AddArg(arg.type);
    }
  }

  public override int GetDefaultArgsNum() { return default_args_num; }

  public override void Define(Symbol sym) 
  {
    throw new Exception("Defining symbols here is not allowed");
  }

  public override uint ClassId()
  {
    throw new NotImplementedException();
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    throw new NotImplementedException();
  }

  public override string ToString()
  {
    return "[native] " + base.ToString();
  }
}

public interface INativeType
{
  System.Type GetNativeType();
}

public class ClassSymbolNative : ClassSymbol, INativeType
{
  //NOTE: during Setup() routine will be resolved
  Proxy<IType> tmp_super_class;
  IList<Proxy<IType>> tmp_implements;

  System.Type native_type;

  public ClassSymbolNative(
    Origin origin,
    string name, 
    VM.ClassCreator creator = null,
    System.Type native_type = null
  )
    : this(origin, name, new Proxy<IType>(), null, creator, native_type)
  {}

  public ClassSymbolNative(
    Origin origin,
    string name, 
    IList<Proxy<IType>> proxy_implements,
    VM.ClassCreator creator = null,
    System.Type native_type = null
  )
    : this(origin, name, new Proxy<IType>(), proxy_implements, creator, native_type)
  {}

  public ClassSymbolNative(
    Origin origin,
    string name, 
    Proxy<IType> proxy_super_class,
    VM.ClassCreator creator = null,
    System.Type native_type = null
  )
    : this(origin, name, proxy_super_class, null, creator, native_type)
  {}

  public ClassSymbolNative(
    Origin origin,
    string name, 
    Proxy<IType> proxy_super_class,
    IList<Proxy<IType>> proxy_implements,
    VM.ClassCreator creator = null,
    System.Type native_type = null
  )
    : base(origin, name, creator)
  {
    this.tmp_super_class = proxy_super_class;
    this.tmp_implements = proxy_implements;
    this.native_type = native_type;
  }

  public System.Type GetNativeType()
  {
    return native_type;
  }

  public override void Setup()
  {
    ResolveTmpSuperClassAndInterfaces(this);

    base.Setup();
  }

  static void ResolveTmpSuperClassAndInterfaces(ClassSymbolNative curr_class)
  {
    if(curr_class.tmp_super_class.IsEmpty() && curr_class.tmp_implements == null)
      return;

    ClassSymbol super_class = null;
    if(!curr_class.tmp_super_class.IsEmpty())
    {
      super_class = curr_class.tmp_super_class.Get() as ClassSymbol;
      if(super_class == null)
        throw new Exception("Parent class is not found: " + curr_class.tmp_super_class);

      //let's reset tmp member once it's resolved
      curr_class.tmp_super_class.Clear();
    }

    List<InterfaceSymbol> implements = null;
    if(curr_class.tmp_implements != null)
    {
      implements = new List<InterfaceSymbol>();
      foreach(var pi in curr_class.tmp_implements)
      {
        var iface = pi.Get() as InterfaceSymbol;
        if(iface == null) 
          throw new Exception("Implemented interface not found: " + pi);
        implements.Add(iface);
      }

      //let's reset tmp member once it's resolved
      curr_class.tmp_implements = null;
    }

    if(super_class != null)
      ResolveTmpSuperClassAndInterfaces((ClassSymbolNative)super_class);

    curr_class.SetSuperClassAndInterfaces(super_class, implements);
  }

  public override uint ClassId()
  {
    throw new NotImplementedException();
  }

  public override void IndexTypeRefs(marshall.SyncContext ctx)
  {}
  
  public override void Sync(marshall.SyncContext ctx)
  {
    throw new NotImplementedException();
  }

  public override string ToString()
  {
    return "[native] " + base.ToString();
  }
}

public class ClassSymbolScript : ClassSymbol
{
  public const uint CLASS_ID = 11;

  public ClassSymbolScript(Origin origin, string name)
    : base(origin, name)
  {
    this.creator = ClassCreator;
  }

  //marshall factory version
  public ClassSymbolScript() 
    : this(null, null)
  {}

  void ClassCreator(VM.Frame frm, ref Val data, IType type)
  {
    //NOTE: object's raw data is a list
    var vl = ValList.New(frm.vm);
    data.SetObj(vl, type);
    
    for(int i=0;i<_all_members.Count;++i)
    {
      var m = _all_members[i];
      //NOTE: Members contain all kinds of symbols: methods and attributes,
      //      however we need to properly setup attributes only. 
      //      Other members will be initialized with special case 'nil' value.
      //TODO:
      //      Maybe we should store attributes and methods separately someday. 
      if(m is VariableSymbol vs)
      {
        var mtype = vs.type.Get();
        var v = frm.vm.MakeDefaultVal(mtype);
        //adding directly, bypassing Retain call
        vl.lst.Add(v);
      }
      else
        vl.Add(frm.vm.Null);
    }
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void IndexTypeRefs(marshall.SyncContext ctx)
  {
    _super_class.IndexTypeRefs(ctx);
    foreach(var m in members)
      m.IndexTypeRefs(ctx);
    implements.IndexTypeRefs(ctx);
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.SyncRef(ctx, ref _super_class);
    marshall.Marshall.Sync(ctx, ref members);
    marshall.Marshall.Sync(ctx, ref implements); 
  }
}

public abstract class EnumSymbol : Symbol, IScope, IType, IEnumerable<Symbol>
{
  internal SymbolsStorage members;

  public EnumSymbol(Origin origin, string name)
     : base(origin, name)
  {
    this.members = new SymbolsStorage(this);
  }

  public IScope GetFallbackScope() { return scope; }

  public Symbol Resolve(string name) 
  {
    return members.Find(name);
  }

  public void Define(Symbol sym)
  {
    if(!(sym is EnumItemSymbol))
      throw new Exception("Invalid item");
    members.Add(sym);
  }

  public IEnumerator<Symbol> GetEnumerator() { return members.GetEnumerator(); }
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public EnumItemSymbol FindValue(string name)
  {
    return Resolve(name) as EnumItemSymbol;
  }

  public override uint ClassId()
  {
    throw new NotImplementedException();
  }

  public override void IndexTypeRefs(marshall.SyncContext ctx)
  {
  }
  
  public override void Sync(marshall.SyncContext ctx)
  {
    throw new NotImplementedException();
  }
}

public class EnumSymbolScript : EnumSymbol
{
  public const uint CLASS_ID = 12;

  public EnumSymbolScript(Origin origin, string name)
    : base(origin, name)
  {}

  //marshall factory version
  public EnumSymbolScript()
    : base(null, null)
  {}

  //0 - OK, 1 - duplicate key, 2 - duplicate value
  public int TryAddItem(Origin origin, string name, int val)
  {
    for(int i=0;i<members.Count;++i)
    {
      var m = (EnumItemSymbol)members[i];
      if(m.val == val)
        return 2;
      else if(m.name == name)
        return 1;
    }

    var item = new EnumItemSymbol(origin, name, val);
    //TODO: should be set by SymbolsDictionary
    item.scope = this;
    members.Add(item);
    return 0;
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.Sync(ctx, ref members);
    if(ctx.is_read)
    {
      for(int i=0;i<members.Count;++i)
      {
        var item = (EnumItemSymbol)members[i];
        item.scope = this;
      }
    }
  }
}

public class EnumSymbolNative : EnumSymbol, INativeType
{
  System.Type native_type;

  public EnumSymbolNative(Origin origin, string name, System.Type native_type)
    : base(origin, name)
  {
    this.native_type = native_type;
  }

  public System.Type GetNativeType()
  {
    return native_type;
  }
}

public class EnumItemSymbol : Symbol, IType 
{
  public const uint CLASS_ID = 15;

  public EnumSymbol owner {
    get {
      return scope as EnumSymbol;
    }
  }
  public int val;

  public EnumItemSymbol(Origin origin, string name, int val = 0) 
    : base(origin, name) 
  {
    this.val = val;
  }

  //marshall factory version
  public EnumItemSymbol() 
    : base(null, null)
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }
  
  public override void IndexTypeRefs(marshall.SyncContext ctx)
  {}

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.Sync(ctx, ref val);
  }
}

public class SymbolsStorage : marshall.IMarshallable, IEnumerable<Symbol>
{
  IScope scope;
  List<Symbol> list = new List<Symbol>();
  //NOTE: used for lookup by name
  Dictionary<string, int> name2idx = new Dictionary<string, int>();

  public int Count
  {
    get {
      return list.Count;
    }
  }

  public Symbol this[int index]
  {
    get {
      return list[index];
    }
  }

  public SymbolsStorage(IScope scope)
  {
    if(scope == null)
      throw new Exception("Scope is null");
    this.scope = scope;
  }

  public bool Contains(string name)
  {
    return Find(name) != null;
  }

  public Symbol Find(string name)
  {
    int idx;
    if(!name2idx.TryGetValue(name, out idx))
      return null;
    return list[idx];
  }

  public void Add(Symbol s)
  {
    if(Find(s.name) != null)
      throw new SymbolError(s, "already defined symbol '" + s.name + "'"); 

    //only assingning scope if it's not assigned yet
    if(s.scope == null)
      s.scope = scope;

    if(s is IScopeIndexed si && si.scope_idx == -1)
      si.scope_idx = list.Count;

    list.Add(s);
    name2idx.Add(s.name, list.Count-1);
  }

  public void RemoveAt(int index)
  {
    var s = list[index];
    if(s.scope == scope)
      s.scope = null;
    list.RemoveAt(index);
    name2idx.Remove(s.name);
  }

  public Symbol TryAt(int index)
  {
    if(index < 0 || index >= list.Count)
      return null;
    return list[index];
  }

  public int IndexOf(Symbol s)
  {
    //TODO: use lookup by name instead?
    return list.IndexOf(s);
  }

  public int IndexOf(string name)
  {
    int idx;
    if(!name2idx.TryGetValue(name, out idx))
      return -1;
    return idx;
  }

  public bool Replace(Symbol what, Symbol subst)
  {
    int idx = IndexOf(what);
    if(idx == -1)
      return false;
    
    list[idx] = subst;

    name2idx.Remove(what.name);
    name2idx[subst.name] = idx;

    return true;
  }

  public void Clear()
  {
    foreach(var s in list)
    {
      if(s.scope == scope)
        s.scope = null;
    }
    list.Clear();
    name2idx.Clear();
  }

  public void IndexTypeRefs(marshall.SyncContext ctx)
  {
    foreach(var item in list)
      item.IndexTypeRefs(ctx);
  }

  public void Sync(marshall.SyncContext ctx) 
  {
    marshall.Marshall.SyncGeneric(ctx, list);

    if(ctx.is_read)
    {
      for(int idx=0;idx<list.Count;++idx)
      {
        var tmp = list[idx];
        tmp.scope = scope;
        name2idx.Add(tmp.name, idx);
      }
    }
  }

  public void UnionWith(SymbolsStorage o)
  {
    for(int i=0;i<o.Count;++i)
      Add(o[i]);
  }

  public IEnumerator<Symbol> GetEnumerator()
  {
    return list.GetEnumerator();
  }
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class TypeSet<T> : marshall.IMarshallable where T : class, IType
{
  //TODO: since TypeProxy implements custom Equals we could use HashSet here
  List<Proxy<IType>> list = new List<Proxy<IType>>();

  public int Count
  {
    get {
      return list.Count;
    }
  }

  public T this[int index]
  {
    get {
      var tp = list[index];
      var s = (T)tp.Get();
      if(s == null)
        throw new Exception("Type not found: " + tp);
      return s;
    }
  }

  public TypeSet()
  {}

  public bool Add(T t)
  {
    return Add(new Proxy<IType>(t));
  }

  public bool Add(Proxy<IType> tp)
  {
    foreach(var item in list)
    {
      if(item.Equals(tp))
        return false;
    }
    list.Add(tp);
    return true;
  }

  public void Clear()
  {
    list.Clear();
  }

  public void IndexTypeRefs(marshall.SyncContext ctx)
  {
    foreach(var item in list)
      item.IndexTypeRefs(ctx);
  }

  public void Sync(marshall.SyncContext ctx) 
  {
    marshall.Marshall.SyncRefs(ctx, list);
  }
}

public class TypeRefIndex
{
  internal List<Proxy<IType>> refs = new List<Proxy<IType>>();

  public int Count {
    get {
      return refs.Count;
    }
  }
  
  public int Add(Proxy<IType> v)
  {
    int idx = Find(v);
    if(idx != -1)
      return idx;

    refs.Add(v);
    return refs.Count - 1;
  }

  public int Find(Proxy<IType> v)
  {
    for(int i = 0; i < refs.Count; ++i)
    {
      if(refs[i].Equals(v))
        return i;
    }
    return -1;
  }

  public int Get(Proxy<IType> v)
  {
    int idx = Find(v);
    if(idx == -1)
      throw new Exception("Not found index for type '" + v + "'");
    return idx;
  }

  public Proxy<IType> Get(int idx)
  {
    return refs[idx];
  }
}

public class ModuleIndexer<T> where T : Symbol, IModuleIndexed
{
  internal List<T> index = new List<T>();
  
  public int Count {
    get {
      return index.Count;
    }
  }
  public void Index(T sym)
  {
    if(index.IndexOf(sym) != -1)
      return;
    sym.module_idx = index.Count;
    index.Add(sym);
  }

  public T this[int i]
  {
    get {
      return index[i];
    }
  }

  public int IndexOf(T s)
  {
    return index.IndexOf(s);
  }
  
  public int IndexOf(string name)
  {
    for(int i=0;i<index.Count;++i)
      if(index[i].name == name)
        return i;
    return -1;
  }
}

public class ScopeIndexer<T> where T : Symbol, IScopeIndexed 
{
  internal List<T> index = new List<T>();

  public int Count {
    get {
      return index.Count;
    }
  }

  public void Index(T sym)
  {
    if(index.IndexOf(sym) != -1)
      return;
    //TODO: do we really need this check?
    //if(sym.scope_idx == -1)
      sym.scope_idx = index.Count;
    index.Add(sym);
  }

  public T this[int i]
  {
    get {
      return index[i];
    }
  }

  public int IndexOf(INamed s)
  {
    return index.IndexOf((T)s);
  }

  public int IndexOf(string name)
  {
    for(int i=0;i<index.Count;++i)
      if(index[i].GetName() == name)
        return i;
    return -1;
  }
}

public class FuncNativeModuleIndexer : ModuleIndexer<FuncSymbolNative> {}
public class FuncModuleIndexer : ModuleIndexer<FuncSymbolScript> {}

public class VarScopeIndexer : ScopeIndexer<VariableSymbol> {}

public class NativeFuncScopeIndexer : ScopeIndexer<FuncSymbolNative> 
{
  public NativeFuncScopeIndexer()
  {}

  public NativeFuncScopeIndexer(NativeFuncScopeIndexer other)
  {
    index.AddRange(other.index);
  }
}

public class SymbolFactory : marshall.IFactory
{
  public Types types;
  public INamedResolver resolver;

  public SymbolFactory(Types types, INamedResolver resolver) 
  {
    this.types = types;
    this.resolver = resolver; 
  }

  public marshall.IMarshallableGeneric CreateById(uint id) 
  {
    switch(id)
    {
      case IntSymbol.CLASS_ID:
        return Types.Int;
      case FloatSymbol.CLASS_ID:
        return Types.Float;
      case StringSymbol.CLASS_ID:
        return Types.String;
      case BoolSymbol.CLASS_ID:
        return Types.Bool;
      case AnySymbol.CLASS_ID:
        return Types.Any;
      case VoidSymbol.CLASS_ID:
        return Types.Void;
      case VariableSymbol.CLASS_ID:
        return new VariableSymbol(); 
      case GlobalVariableSymbol.CLASS_ID:
        return new GlobalVariableSymbol(); 
      case FuncArgSymbol.CLASS_ID:
        return new FuncArgSymbol(); 
      case FieldSymbolScript.CLASS_ID:
        return new FieldSymbolScript(); 
      case GenericArrayTypeSymbol.CLASS_ID:
        return new GenericArrayTypeSymbol(); 
      case GenericMapTypeSymbol.CLASS_ID:
        return new GenericMapTypeSymbol(); 
      case ClassSymbolScript.CLASS_ID:
        return new ClassSymbolScript();
      case InterfaceSymbolScript.CLASS_ID:
        return new InterfaceSymbolScript();
      case EnumSymbolScript.CLASS_ID:
        return new EnumSymbolScript();
      case EnumItemSymbol.CLASS_ID:
        return new EnumItemSymbol();
      case FuncSymbolScript.CLASS_ID:
        return new FuncSymbolScript();
      case FuncSignature.CLASS_ID:
        return new FuncSignature();
      case RefType.CLASS_ID:
        return new RefType();
      case TupleType.CLASS_ID:
        return new TupleType();
      case Namespace.CLASS_ID:
        return new Namespace();
      default:
        return null;
    }
  }
}

public static class SymbolExtensions
{
  static public bool IsStatic(this Symbol symb)
  {
    return 
      (symb is FuncSymbol fs && fs.attribs.HasFlag(FuncAttrib.Static)) ||
      (symb is FieldSymbol fds && fds.attribs.HasFlag(FieldAttrib.Static))
      ;
  }
}

} //namespace bhl
