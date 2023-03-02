using System;
using System.Collections.Generic;

namespace bhl {

public abstract class Symbol : INamed, marshall.IMarshallableGeneric 
{
  public string name;

  // All symbols know what scope contains them
  public IScope scope;
#if BHL_FRONT
  // Location in parse tree, can be null if it's a native symbol
  public AnnotatedParseTree parsed;
#endif

  public Symbol(string name) 
  { 
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

  public abstract void Sync(marshall.SyncContext ctx);
}

public abstract class BuiltInSymbolType : Symbol, IType 
{
  public BuiltInSymbolType(string name) 
    : base(name) 
  {}

  //contains no data
  public override void Sync(marshall.SyncContext ctx)
  {
  }

  public static implicit operator Proxy<IType>(BuiltInSymbolType s)
  {
    return new Proxy<IType>(s);
  }
}

public class IntSymbol : BuiltInSymbolType
{
  public const uint CLASS_ID = 1;

  public IntSymbol()
    : base("int")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }
}

public class BoolSymbol : BuiltInSymbolType
{
  public const uint CLASS_ID = 2;

  public BoolSymbol()
    : base("bool")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }
}

public class StringSymbol : BuiltInSymbolType
{
  public const uint CLASS_ID = 3;

  public StringSymbol()
    : base("string")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }
}

public class FloatSymbol : BuiltInSymbolType
{
  public const uint CLASS_ID = 4;

  public FloatSymbol()
    : base("float")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }
}

public class VoidSymbol : BuiltInSymbolType
{
  public const uint CLASS_ID = 5;

  public VoidSymbol()
    : base("void")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }
}

public class AnySymbol : BuiltInSymbolType
{
  public const uint CLASS_ID = 6;

  public AnySymbol()
    : base("any")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }
}

public class NullSymbol : BuiltInSymbolType
{
  public const uint CLASS_ID = 7;

  public NullSymbol()
    : base("null")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }
}

public class VarSymbol : BuiltInSymbolType
{
  public const uint CLASS_ID = 8;

  public VarSymbol()
    : base("var")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }
}

public abstract class InterfaceSymbol : Symbol, IScope, IInstanceType, ISymbolsEnumerable
{
  internal SymbolsStorage members;

  public TypeSet<InterfaceSymbol> inherits = new TypeSet<InterfaceSymbol>();

  HashSet<IInstanceType> related_types;

#if BHL_FRONT
  public InterfaceSymbol(
    AnnotatedParseTree parsed, 
    string name
  )
    : this(name)
  {
    this.parsed = parsed;
  }
#endif

  public InterfaceSymbol(string name)
    : base(name)
  {
    this.members = new SymbolsStorage(this);
  }

  //marshall factory version
  public InterfaceSymbol()
    : this(null)
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

  public ISymbolsEnumerator GetSymbolsEnumerator() { return members; }

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

  public HashSet<IInstanceType> GetAllRelatedTypesSet()
  {
    if(related_types == null)
    {
      related_types = new HashSet<IInstanceType>();
      related_types.Add(this);
      for(int i=0;i<inherits.Count;++i)
      {
        var ext = (IInstanceType)inherits[i];
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
  
  public InterfaceSymbolScript(string name)
    : base(name)
  {}

#if BHL_FRONT
  public InterfaceSymbolScript(
    AnnotatedParseTree parsed, 
    string name
  )
    : this(name)
  {
    this.parsed = parsed;
  }
#endif

  //marshall factory version
  public InterfaceSymbolScript() 
    : this(null)
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.Sync(ctx, ref inherits); 
    marshall.Marshall.Sync(ctx, ref members); 
  }
}

public class InterfaceSymbolNative : InterfaceSymbol
{
  IList<Proxy<IType>> proxy_inherits;
  FuncSymbol[] funcs;

  public InterfaceSymbolNative(
    string name, 
    IList<Proxy<IType>> proxy_inherits,
    params FuncSymbol[] funcs
  )
    : base(name)
  {
    this.proxy_inherits = proxy_inherits;
    this.funcs = funcs;
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
          throw new Exception("Inherited interface not found" + pi.path);

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

  public override void Sync(marshall.SyncContext ctx)
  {
    throw new NotImplementedException();
  }
}

public abstract class ClassSymbol : Symbol, IScope, IInstanceType, ISymbolsEnumerable
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

  HashSet<IInstanceType> related_types;

  //contains only 'local' members without taking into account inheritance
  internal SymbolsStorage members;
  //all 'flattened' members including all parents
  internal SymbolsStorage _all_members;

  internal bool _resolve_only_decl_members;

  public VM.ClassCreator creator;

#if BHL_FRONT
  public ClassSymbol(
    AnnotatedParseTree parsed, 
    string name, 
    VM.ClassCreator creator = null
  )
    : this(name, creator)
  {
    this.parsed = parsed;
  }

#endif

  public ClassSymbol(
    string name, 
    VM.ClassCreator creator = null
  )
    : base(name)
  {
    this.members = new SymbolsStorage(this);

    this.creator = creator;
  }

  public void SetSuperAndInterfaces(ClassSymbol super_class, IList<InterfaceSymbol> implements = null)
  {
    if(super_class != null)
      _super_class = new Proxy<ClassSymbol>(super_class);

    if(implements != null && implements.Count > 0)
      SetImplementedInterfaces(implements);
  }

  public ISymbolsEnumerator GetSymbolsEnumerator() { return _all_members; }

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
    if(sym is FuncSymbol fs && IsBinaryOp(fs.name))
      CheckBinaryOpOverload(fs);

    if(sym is FuncSymbolNative fsn && fsn.attribs.HasFlag(FuncAttrib.Static))
      this.GetNamespace().nfunc_index.Index(fsn);

    if(sym is FieldSymbol fld && fld.attribs.HasFlag(FieldAttrib.Static)) 
    {
      if(this is ClassSymbolNative)
      {
        //let's create fake static get/set native functions
        var static_get = new FuncSymbolNative(GetNativeStaticFieldGetFuncName(fld), fld.type,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
        {
          var res = Val.New(frm.vm);
          fld.getter(frm, null, ref res, fld);
          stack.Push(res);
          return null;
        });
        this.GetNamespace().nfunc_index.Index(static_get);

        var static_set = new FuncSymbolNative(GetNativeStaticFieldSetFuncName(fld), fld.type,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
        {
          Val ctx = null;
          var val = stack.Pop();
          fld.setter(frm, ref ctx, val, fld);
          val.Release();
          return null;
        });
        this.GetNamespace().nfunc_index.Index(static_set);
      }
      else
        this.GetNamespace().module_vars.Index(fld);
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
        _vtable[i] = fssv.overrides[fssv.overrides.Count-1];
    }

    //interfaces lookup table
    _itable = new Dictionary<InterfaceSymbol, List<FuncSymbol>>();

    var all = new HashSet<IInstanceType>();
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

  public HashSet<IInstanceType> GetAllRelatedTypesSet()
  {
    if(related_types == null)
    {
      related_types = new HashSet<IInstanceType>();
      related_types.Add(this);
      if(super_class != null)
        related_types.UnionWith(super_class.GetAllRelatedTypesSet());
      for(int i=0;i<implements.Count;++i)
      {
        if(!related_types.Contains(implements[i]))
          related_types.UnionWith(implements[i].GetAllRelatedTypesSet());
      }
    }
    return related_types;
  }
}

public abstract class ArrayTypeSymbol : ClassSymbol
{
  public readonly FuncSymbolNative FuncArrIdx = null;
  public readonly FuncSymbolNative FuncArrIdxW = null;

  public Proxy<IType> item_type;

  public ArrayTypeSymbol(string name, Proxy<IType> item_type)     
    : base(name)
  {
    this.item_type = item_type;

    this.creator = CreateArr;

    //NOTE: must be first member of the class
    {
      var fn = new FuncSymbolNative("Add", Types.Void, Add,
        new FuncArgSymbol("o", item_type)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("RemoveAt", Types.Void, RemoveAt,
        new FuncArgSymbol("idx", Types.Int)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("Clear", Types.Void, Clear);
      this.Define(fn);
    }

    {
      var vs = new FieldSymbol("Count", Types.Int, GetCount, null);
      this.Define(vs);
    }

    {
      var fn = new FuncSymbolNative("IndexOf", Types.Int, IndexOf,
        new FuncArgSymbol("o", item_type)
      );
      this.Define(fn);
    }

    {
      //hidden system method not available directly
      FuncArrIdx = new FuncSymbolNative("$ArrIdx", item_type, ArrIdx);
    }

    {
      //hidden system method not available directly
      FuncArrIdxW = new FuncSymbolNative("$ArrIdxW", Types.Void, ArrIdxW);
    }

    Setup();
  }

  public ArrayTypeSymbol(Proxy<IType> item_type) 
    : this("[]" + item_type.path, item_type)
  {}

  public abstract void CreateArr(VM.Frame frame, ref Val v, IType type);
  public abstract void GetCount(VM.Frame frame, Val ctx, ref Val v, FieldSymbol fld);
  public abstract ICoroutine Add(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract ICoroutine ArrIdx(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract ICoroutine ArrIdxW(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract ICoroutine RemoveAt(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract ICoroutine IndexOf(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract ICoroutine Clear(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
}

public class GenericArrayTypeSymbol : ArrayTypeSymbol, IEquatable<GenericArrayTypeSymbol>, IEphemeral
{
  public const uint CLASS_ID = 10; 

  public GenericArrayTypeSymbol(Proxy<IType> item_type) 
    : base(item_type)
  {}

  //marshall factory version
  public GenericArrayTypeSymbol()
    : this(new Proxy<IType>())
  {}

  static IList<Val> AsList(Val arr)
  {
    var lst = arr.obj as IList<Val>;
    if(lst == null)
      throw new Exception("Not a ValList: " + (arr.obj != null ? arr.obj.GetType().Name : ""+arr));
    return lst;
  }

  public override void CreateArr(VM.Frame frm, ref Val v, IType type)
  {
    v.SetObj(ValList.New(frm.vm), type);
  }

  public override void GetCount(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
  {
    var lst = AsList(ctx);
    v.SetNum(lst.Count);
  }
  
  public override ICoroutine Add(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var val = stack.Pop();
    var arr = stack.Pop();
    var lst = AsList(arr);
    lst.Add(val);
    val.Release();
    arr.Release();
    return null;
  }

  public override ICoroutine IndexOf(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var val = stack.Pop();
    var arr = stack.Pop();
    var lst = AsList(arr);

    int idx = -1;
    for(int i=0;i<lst.Count;++i)
    {
      if(lst[i].IsValueEqual(val))
      {
        idx = i;
        break;
      }
    }

    val.Release();
    arr.Release();
    stack.Push(Val.NewInt(frm.vm, idx));
    return null;
  }

  //NOTE: follows special Opcodes.ArrIdx conventions
  public override ICoroutine ArrIdx(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)stack.PopRelease().num;
    var arr = stack.Pop();
    var lst = AsList(arr);
    var res = lst[idx]; 
    stack.PushRetain(res);
    arr.Release();
    return null;
  }

  //NOTE: follows special Opcodes.ArrIdxW conventions
  public override ICoroutine ArrIdxW(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)stack.PopRelease().num;
    var arr = stack.Pop();
    var val = stack.Pop();
    var lst = AsList(arr);
    lst[idx] = val;
    val.Release();
    arr.Release();
    return null;
  }

  public override ICoroutine RemoveAt(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)stack.PopRelease().num;
    var arr = stack.Pop();
    var lst = AsList(arr);
    lst.RemoveAt(idx); 
    arr.Release();
    return null;
  }

  public override ICoroutine Clear(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var arr = stack.Pop();
    var lst = AsList(arr);
    lst.Clear();
    arr.Release();
    return null;
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref item_type);

    if(ctx.is_read)
      name = "[]" + item_type.path;
  }

  public override bool Equals(object o)
  {
    if(!(o is GenericArrayTypeSymbol))
      return false;
    return this.Equals((GenericArrayTypeSymbol)o);
  }

  public bool Equals(GenericArrayTypeSymbol o)
  {
    if(ReferenceEquals(o, null))
      return false;
    if(ReferenceEquals(this, o))
      return true;
    return item_type.Equals(o.item_type);
  }

  public override int GetHashCode()
  {
    return name.GetHashCode();
  }
}

public class ArrayTypeSymbolT<T> : ArrayTypeSymbol where T : new()
{
  public delegate IList<T> CreatorCb();
  public static CreatorCb Creator;

  public ArrayTypeSymbolT(string name, Proxy<IType> item_type, CreatorCb creator) 
    : base(name, item_type)
  {
    Creator = creator;
  }

  public ArrayTypeSymbolT(Proxy<IType> item_type, CreatorCb creator) 
    : base("[]" + item_type.path, item_type)
  {}

  public override void CreateArr(VM.Frame frm, ref Val v, IType type)
  {
    v.SetObj(Creator(), type);
  }

  public override void GetCount(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
  {
    v.SetNum(((IList<T>)ctx.obj).Count);
  }
  
  public override ICoroutine Add(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var val = stack.Pop();
    var arr = stack.Pop();
    var lst = (IList<T>)arr.obj;
    lst.Add((T)val.obj);
    val.Release();
    arr.Release();
    return null;
  }

  public override ICoroutine IndexOf(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var val = stack.Pop();
    var arr = stack.Pop();
    var lst = (IList<T>)arr.obj;
    int idx = lst.IndexOf((T)val.obj);
    val.Release();
    arr.Release();
    stack.Push(Val.NewInt(frm.vm, idx));
    return null;
  }

  //NOTE: follows special Opcodes.ArrIdx conventions
  public override ICoroutine ArrIdx(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)stack.PopRelease().num;
    var arr = stack.Pop();
    var lst = (IList<T>)arr.obj;
    var res = Val.NewObj(frame.vm, lst[idx], item_type.Get());
    stack.Push(res);
    arr.Release();
    return null;
  }

  //NOTE: follows special Opcodes.ArrIdxW conventions
  public override ICoroutine ArrIdxW(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)stack.PopRelease().num;
    var arr = stack.Pop();
    var val = stack.Pop();
    var lst = (IList<T>)arr.obj;
    lst[idx] = (T)val.obj;
    val.Release();
    arr.Release();
    return null;
  }

  public override ICoroutine RemoveAt(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)stack.PopRelease().num;
    var arr = stack.Pop();
    var lst = (IList<T>)arr.obj;
    lst.RemoveAt(idx); 
    arr.Release();
    return null;
  }

  public override ICoroutine Clear(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)stack.PopRelease().num;
    var arr = stack.Pop();
    var lst = (IList<T>)arr.obj;
    lst.Clear();
    arr.Release();
    return null;
  }

  public override uint ClassId()
  {
    throw new NotImplementedException();
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    throw new NotImplementedException();
  }
}

public abstract class MapTypeSymbol : ClassSymbol
{
  public readonly FuncSymbolNative FuncMapIdx = null;
  public readonly FuncSymbolNative FuncMapIdxW = null;

  public Proxy<IType> key_type;
  public Proxy<IType> val_type;

  public ClassSymbol enumerator_type = new ClassSymbolNative("Enumerator");

  public MapTypeSymbol(Proxy<IType> key_type, Proxy<IType> val_type)     
    : base("[" + key_type.path + "]" + val_type.path)
  {
    this.key_type = key_type;
    this.val_type = val_type;

    this.creator = CreateMap;

    //NOTE: must be first member of the class
    {
      var fn = new FuncSymbolNative("Add", Types.Void, Add,
        new FuncArgSymbol("key", key_type),
        new FuncArgSymbol("val", val_type)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("Remove", Types.Void, Remove,
        new FuncArgSymbol("key", key_type)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("Clear", Types.Void, Clear);
      this.Define(fn);
    }

    {
      var vs = new FieldSymbol("Count", Types.Int, GetCount, null);
      this.Define(vs);
    }

    {
      var fn = new FuncSymbolNative("Contains", Types.Bool, Contains,
        new FuncArgSymbol("key", key_type)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("TryGet", new Proxy<IType>(new TupleType(Types.Bool, val_type)), TryGet,
        new FuncArgSymbol("key", key_type)
      );
      this.Define(fn);
    }

    {
      //hidden system method not available directly
      var vs = new FieldSymbol("$Enumerator", new Proxy<IType>(enumerator_type), GetEnumerator, null);
      this.Define(vs);
    }

    {
      //hidden system method not available directly
      FuncMapIdx = new FuncSymbolNative("$MapIdx", val_type, MapIdx);
    }

    {
      //hidden system method not available directly
      FuncMapIdxW = new FuncSymbolNative("$MapIdxW", Types.Void, MapIdxW);
    }

    {
      var vs = new FieldSymbol("Next", Types.Bool, EnumeratorNext, null);
      enumerator_type.Define(vs);
    }

    {
      var fn = new FuncSymbolNative("Current", new Proxy<IType>(new TupleType(key_type, val_type)), EnumeratorCurrent
      );
      enumerator_type.Define(fn);
    }

    Setup();
    enumerator_type.Setup();
  }

  public abstract void CreateMap(VM.Frame frame, ref Val v, IType type);
  public abstract void GetCount(VM.Frame frame, Val ctx, ref Val v, FieldSymbol fld);
  public abstract void GetEnumerator(VM.Frame frame, Val ctx, ref Val v, FieldSymbol fld);
  public abstract ICoroutine Add(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract ICoroutine MapIdx(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract ICoroutine MapIdxW(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract ICoroutine Remove(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract ICoroutine Contains(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract ICoroutine TryGet(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract ICoroutine Clear(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);

  public abstract void EnumeratorNext(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld);
  public abstract ICoroutine EnumeratorCurrent(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
}

public class GenericMapTypeSymbol : MapTypeSymbol, IEquatable<GenericMapTypeSymbol>, IEphemeral
{
  public const uint CLASS_ID = 21; 

  public GenericMapTypeSymbol(Proxy<IType> key_type, Proxy<IType> val_type)     
    : base(key_type, val_type)
  {}
  
  //marshall factory version
  public GenericMapTypeSymbol()
    : this(new Proxy<IType>(), new Proxy<IType>())
  {}

  static ValMap AsMap(Val arr)
  {
    var map = arr.obj as ValMap;
    if(map == null)
      throw new Exception("Not a ValMap: " + (arr.obj != null ? arr.obj.GetType().Name : ""+arr));
    return map;
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref key_type);
    marshall.Marshall.Sync(ctx, ref val_type);

    if(ctx.is_read)
      name = "[" + key_type.path + "]" + val_type.path;
  }

  public override bool Equals(object o)
  {
    if(!(o is GenericMapTypeSymbol))
      return false;
    return this.Equals((GenericMapTypeSymbol)o);
  }

  public bool Equals(GenericMapTypeSymbol o)
  {
    if(ReferenceEquals(o, null))
      return false;
    if(ReferenceEquals(this, o))
      return true;
    return key_type.Equals(o.key_type) && 
           val_type.Equals(o.val_type);
  }

  public override int GetHashCode()
  {
    return name.GetHashCode();
  }

  public override void CreateMap(VM.Frame frm, ref Val v, IType type)
  {
    v.SetObj(ValMap.New(frm.vm), type);
  }

  public override void GetCount(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
  {
    var m = AsMap(ctx);
    v.SetNum(m.Count);
  }

  public override void GetEnumerator(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
  {
    var m = AsMap(ctx);
    v.SetObj(new ValMap.Enumerator(m), enumerator_type);
  }

  public override ICoroutine Add(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var val = stack.Pop();
    var key = stack.Pop();
    var v = stack.Pop();
    var map = AsMap(v);
    //TODO: maybe we should rather user C# Dictionary.Add(k, v)?
    map[key] = val;
    key.Release();
    val.Release();
    v.Release();
    return null;
  }

  public override ICoroutine Remove(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var key = stack.Pop();
    var v = stack.Pop();
    var map = AsMap(v);
    map.Remove(key);
    key.Release();
    v.Release();
    return null;
  }

  public override ICoroutine Contains(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var key = stack.Pop();
    var v = stack.Pop();
    var map = AsMap(v);
    bool yes = map.ContainsKey(key);
    key.Release();
    v.Release();
    stack.Push(Val.NewBool(frame.vm, yes));
    return null;
  }

  public override ICoroutine TryGet(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var key = stack.Pop();
    var v = stack.Pop();
    var map = AsMap(v);
    Val val;
    bool yes = map.TryGetValue(key, out val);
    key.Release();
    v.Release();
    if(yes)
      stack.PushRetain(val);
    else
      stack.Push(Val.New(frame.vm)); /*just dummy value*/
    stack.Push(Val.NewBool(frame.vm, yes));
    return null;
  }

  public override ICoroutine Clear(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var v = stack.Pop();
    var map = AsMap(v);
    map.Clear();
    v.Release();
    return null;
  }

  //NOTE: follows special Opcodes.MapIdx conventions
  public override ICoroutine MapIdx(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var key = stack.Pop();
    var v = stack.Pop();
    var map = AsMap(v);
    var res = map[key]; 
    stack.PushRetain(res);
    key.Release();
    v.Release();
    return null;
  }

  //NOTE: follows special Opcodes.MapIdxW conventions
  public override ICoroutine MapIdxW(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var key = stack.Pop();
    var v = stack.Pop();
    var val = stack.Pop();
    var map = AsMap(v);
    map[key] = val;
    key.Release();
    val.Release();
    v.Release();
    return null;
  }

  public override void EnumeratorNext(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
  {
    var en = (ValMap.Enumerator)ctx._obj;
    bool ok = en.MoveNext();
    v.SetBool(ok);
  }

  public override ICoroutine EnumeratorCurrent(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var v = stack.Pop();
    var en = (ValMap.Enumerator)v._obj;
    var key = (Val)en.Key; 
    var val = (Val)en.Value; 
    stack.PushRetain(val);
    stack.PushRetain(key);
    v.Release();
    return null;
  }
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
  public VariableSymbol(AnnotatedParseTree parsed, string name, Proxy<IType> type) 
    : this(name, type) 
  {
    this.parsed = parsed;
  }
#endif

  public VariableSymbol(string name, Proxy<IType> type) 
    : base(name) 
  {
    this.type = type;
  }

  //marshall factory version
  public VariableSymbol()
    : base("")
  {}
  
  public IType GetIType()
  {
    return type.Get();
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.Sync(ctx, ref type);
    marshall.Marshall.Sync(ctx, ref _scope_idx);
  }
}

public class FuncArgSymbol : VariableSymbol
{
  public bool is_ref;

#if BHL_FRONT
  public FuncArgSymbol(AnnotatedParseTree parsed, string name, Proxy<IType> type, bool is_ref = false)
    : this(name, type, is_ref)
  {
    this.parsed = parsed;
  }
#endif

  public FuncArgSymbol(string name, Proxy<IType> type, bool is_ref = false)
    : base(name, type)
  {
    this.is_ref = is_ref;
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

  public FieldSymbol(string name, Proxy<IType> type, FieldGetter getter = null, FieldSetter setter = null, FieldRef getref = null) 
    : this(name, 0, type, getter, setter, getref)
  {
  }

  public FieldSymbol(string name, FieldAttrib attribs, Proxy<IType> type, FieldGetter getter = null, FieldSetter setter = null, FieldRef getref = null) 
    : base(name, type)
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

#if BHL_FRONT
  public FieldSymbolScript(AnnotatedParseTree parsed, string name, Proxy<IType> type) 
    : this(name, type)
  {
    this.parsed = parsed;
  }
#endif

  public FieldSymbolScript(string name, Proxy<IType> type) 
    : base(name, type, null, null, null)
  {
    this.getter = Getter;
    this.setter = Setter;
    this.getref = Getref;
  }
  //marshall factory version
  public FieldSymbolScript()
    : this("", new Proxy<IType>())
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
public enum FuncAttrib : byte
{
  None         = 0,
  Virtual      = 1,
  Override     = 2,
  Static       = 4,
  VariadicArgs = 8,
  Coro        = 16,
}

public abstract class FuncSymbol : Symbol, ITyped, IScope, IScopeIndexed, ISymbolsEnumerable
{
  FuncSignature _signature;
  public FuncSignature signature {
    get {
      return _signature;
    }
    set {
      _signature = value;

      if(_signature.is_coro)
        _attribs |= (byte)FuncAttrib.Coro;
      else
        _attribs &= (byte)~FuncAttrib.Coro;

      if(_signature.has_variadic)
        _attribs |= (byte)FuncAttrib.VariadicArgs;
      else
        _attribs &= (byte)~FuncAttrib.VariadicArgs;
    }
  }

  internal SymbolsStorage members;

  internal FuncSymbolVirtual _virtual;

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
      signature.is_coro = value.HasFlag(FuncAttrib.Coro);
      signature.has_variadic = value.HasFlag(FuncAttrib.VariadicArgs);
    }
  }

#if BHL_FRONT
  public FuncSymbol(
    AnnotatedParseTree parsed, 
    string name, 
    FuncSignature sig
  ) 
    : this(name, sig)
  {
    this.parsed = parsed;
  }
#endif

  public FuncSymbol(
    string name, 
    FuncSignature sig
  ) 
    : base(name)
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

  public ISymbolsEnumerator GetSymbolsEnumerator() { return members; }

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

  public SymbolsStorage GetArgs()
  {
    //let's skip hidden 'this' argument which is stored at 0 idx
    int this_offset = (scope is ClassSymbolScript) ? 1 : 0;

    var args = new SymbolsStorage(this);
    for(int i=0;i<signature.arg_types.Count;++i)
      args.Add((FuncArgSymbol)members[this_offset + i]);
    return args;
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

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.Sync(ctx, ref _attribs);
    marshall.Marshall.Sync(ctx, ref _signature);
    marshall.Marshall.Sync(ctx, ref _scope_idx);
  }

  public override string ToString()
  {
    string s = "";
    s += "func ";
    s += signature.ret_type.Get().GetName() + " ";
    s += name;
    s += "(";
    foreach(var arg in signature.arg_types)
      s += arg.path + ",";
    s = s.TrimEnd(',');
    s += ")";
    return s;
  }
}

public class FuncSymbolScript : FuncSymbol
{
  public const uint CLASS_ID = 13; 

  //cached value of CompiledModule, it's set upon module loading in VM and 
  internal CompiledModule _module;

  //used for up values resolving during parsing
  internal LocalScope _current_scope;

  public int local_vars_num;
  public int default_args_num;
  public int ip_addr;

#if BHL_FRONT
  public FuncSymbolScript(
    AnnotatedParseTree parsed, 
    FuncSignature sig,
    string name,
    int default_args_num = 0,
    int ip_addr = -1
  ) 
    : base(name, sig)
  {
    this.name = name;
    this.default_args_num = default_args_num;
    this.ip_addr = ip_addr;
    this.parsed = parsed;
  }
#endif

  //symbol factory version
  public FuncSymbolScript()
    : base(null, new FuncSignature())
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

  public FuncSymbolVirtual(string name, FuncSignature signature, int default_args_num = 0)
    : base(name, signature)
  {
    this.default_args_num = default_args_num;
  }

  public FuncSymbolVirtual(FuncSymbolScript proto) 
    : this(proto.name, proto.signature, proto.default_args_num)
  {
    //NOTE: directly adding arguments avoiding Define
    for(int m=0;m<proto.members.Count;++m)
      members.Add(proto.members[m]);
#if BHL_FRONT
    this.parsed = proto.parsed;
#endif
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

  public void AddOverride(ClassSymbol owner, FuncSymbol fs, bool strict = true)
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
}

#if BHL_FRONT
public class LambdaSymbol : FuncSymbolScript
{
  List<AST_UpVal> upvals;

  List<FuncSymbolScript> fdecl_stack;

  public LambdaSymbol(
    AnnotatedParseTree parsed, 
    string name,
    FuncSignature sig,
    List<AST_UpVal> upvals,
    List<FuncSymbolScript> fdecl_stack
  ) 
    : base(parsed, sig, name, 0, -1)
  {
    this.upvals = upvals;
    this.fdecl_stack = fdecl_stack;
  }

  public VariableSymbol AddUpValue(VariableSymbol src)
  {
    var local = new VariableSymbol(src.parsed, src.name, src.type);

    //NOTE: we want to avoid possible recursion during resolve
    //      checks that's why we use a 'raw' version
    this._current_scope.DefineWithoutEnclosingChecks(local);

    var up = new AST_UpVal(
      local.name, 
      local.scope_idx, 
      src.scope_idx,
      //TODO: should be the line of its usage
      src.parsed != null ? src.parsed.line : 0
    ); 
    upvals.Add(up);

    return local;
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
      var decl = fdecl_stack[i];

      if(decl._current_scope != null)
      {
        var res = decl._current_scope.ResolveWithFallback(name);
        //checking if it's a variable and not a global one
        if(res is VariableSymbol vs && !(vs.scope is Namespace))
          return AssignUpValues(vs, i+1, my_idx);
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
    VariableSymbol res = null;
    //now let's put this result into all nested lambda scopes 
    for(int j=from_idx;j<=to_idx;++j)
    {
      if(fdecl_stack[j] is LambdaSymbol lmb)
      {
        vs = lmb.AddUpValue(vs);
        if(res == null)
          res = vs;
      }
    }
    return res;
  }
}
#endif

public class FuncSymbolNative : FuncSymbol
{
  public delegate ICoroutine Cb(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status); 
  public Cb cb;

  int default_args_num;

  public FuncSymbolNative(
    string name, 
    Proxy<IType> ret_type, 
    Cb cb,
    params FuncArgSymbol[] args
  ) 
    : this(name, ret_type, 0, cb, args)
  {}

  public FuncSymbolNative(
    string name, 
    Proxy<IType> ret_type, 
    int def_args_num,
    Cb cb,
    params FuncArgSymbol[] args
  ) 
    : this(name, FuncAttrib.None, ret_type, def_args_num, cb, args)
  {}

  public FuncSymbolNative(
    string name, 
    FuncAttrib attribs,
    Proxy<IType> ret_type, 
    int default_args_num,
    Cb cb,
    params FuncArgSymbol[] args
  ) 
    : base(name, new FuncSignature(attribs.HasFlag(FuncAttrib.Coro), ret_type))
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
}

public class ClassSymbolNative : ClassSymbol
{
  Proxy<IType> proxy_super_class;
  IList<Proxy<IType>> proxy_implements;

  public ClassSymbolNative(
    string name, 
    VM.ClassCreator creator = null
  )
    : this(name, new Proxy<IType>(), null, creator)
  {}

  public ClassSymbolNative(
    string name, 
    IList<Proxy<IType>> proxy_implements,
    VM.ClassCreator creator = null
  )
    : this(name, new Proxy<IType>(), proxy_implements, creator)
  {}

  public ClassSymbolNative(
    string name, 
    Proxy<IType> proxy_super_class,
    VM.ClassCreator creator = null
  )
    : this(name, proxy_super_class, null, creator)
  {}

  public ClassSymbolNative(
    string name, 
    Proxy<IType> proxy_super_class,
    IList<Proxy<IType>> proxy_implements,
    VM.ClassCreator creator = null
  )
    : base(name, creator)
  {
    this.proxy_super_class = proxy_super_class;
    this.proxy_implements = proxy_implements;
  }

  public override void Setup()
  {
    ClassSymbol super_class = null;
    if(!proxy_super_class.IsEmpty())
    {
      super_class = proxy_super_class.Get() as ClassSymbol;
      if(super_class == null)
        throw new Exception("Parent class is not found: " + proxy_super_class.path);
    }

    List<InterfaceSymbol> implements = null;
    if(proxy_implements != null)
    {
      implements = new List<InterfaceSymbol>();
      foreach(var pi in proxy_implements)
      {
        var iface = pi.Get() as InterfaceSymbol;
        if(iface == null) 
          throw new Exception("Implemented interface not found" + pi.path);
        implements.Add(iface);
      }
    }

    SetSuperAndInterfaces(super_class, implements);

    base.Setup();
  }

  public override uint ClassId()
  {
    throw new NotImplementedException();
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    throw new NotImplementedException();
  }
}

public class ClassSymbolScript : ClassSymbol
{
  public const uint CLASS_ID = 11;

  public ClassSymbolScript(string name)
    : base(name)
  {
    this.creator = ClassCreator;
  }

#if BHL_FRONT
  public ClassSymbolScript(
    AnnotatedParseTree parsed, 
    string name
  )
    : this(name)
  {
    this.parsed = parsed;
  }
#endif

  //marshall factory version
  public ClassSymbolScript() 
    : this(null)
  {}

  void ClassCreator(VM.Frame frm, ref Val data, IType type)
  {
    //TODO: add handling of native super class
    //if(super_class is ClassSymbolNative cn)
    //{
    //}

    //NOTE: object's raw data is a list
    var vl = ValList.New(frm.vm);
    data.SetObj(vl, type);
    
    for(int i=0;i<_all_members.Count;++i)
    {
      var m = _all_members[i];
      //NOTE: Members contain all kinds of symbols: methods and attributes,
      //      however we need to properly setup attributes only. 
      //      Other members will be initialized with special case 'nil' value.
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

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.Sync(ctx, ref _super_class);
    marshall.Marshall.Sync(ctx, ref members);
    marshall.Marshall.Sync(ctx, ref implements); 
  }
}

public class EnumSymbol : Symbol, IScope, IType, ISymbolsEnumerable
{
  public SymbolsStorage members;

#if BHL_FRONT
  public EnumSymbol(AnnotatedParseTree parsed, string name)
    : this(name)
  {
    this.parsed = parsed;
  }
#endif

  public EnumSymbol(string name)
     : base(name)
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
    members.Add(sym);
  }

  public ISymbolsEnumerator GetSymbolsEnumerator() { return members; }

  public EnumItemSymbol FindValue(string name)
  {
    return Resolve(name) as EnumItemSymbol;
  }

  public override uint ClassId()
  {
    throw new NotImplementedException();
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    throw new NotImplementedException();
  }
}

public class EnumSymbolScript : EnumSymbol
{
  public const uint CLASS_ID = 12;

#if BHL_FRONT
  public EnumSymbolScript(AnnotatedParseTree parsed, string name)
    : base(parsed, name)
  {}
#endif

  public EnumSymbolScript(string name)
    : base(name)
  {}

  //marshall factory version
  public EnumSymbolScript()
    : base(null)
  {}

#if BHL_FRONT
  //0 - OK, 1 - duplicate key, 2 - duplicate value
  public int TryAddItem(AnnotatedParseTree parsed, string name, int val)
  {
    for(int i=0;i<members.Count;++i)
    {
      var m = (EnumItemSymbol)members[i];
      if(m.val == val)
        return 2;
      else if(m.name == name)
        return 1;
    }

    var item = new EnumItemSymbol(parsed, name, val);
    //TODO: should be set by SymbolsDictionary
    item.scope = this;
    members.Add(item);
    return 0;
  }
#endif

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

public class EnumItemSymbol : Symbol, IType 
{
  public const uint CLASS_ID = 15;

  public EnumSymbol owner {
    get {
      return scope as EnumSymbol;
    }
  }
  public int val;

#if BHL_FRONT
  public EnumItemSymbol(AnnotatedParseTree parsed, string name, int val = 0) 
    : this(name, val) 
  {
    this.parsed = parsed;
  }
#endif

  public EnumItemSymbol(string name, int val = 0) 
    : base(name) 
  {
    this.val = val;
  }

  //marshall factory version
  public EnumItemSymbol() 
    : base(null)
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.Sync(ctx, ref val);
  }
}

public class SymbolsStorage : marshall.IMarshallable, ISymbolsEnumerator
{
  IScope scope;
  List<Symbol> list = new List<Symbol>();

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
    foreach(var s in list)
      if(s.name == name)
        return s;
    return null;
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
  }

  public void RemoveAt(int index)
  {
    var s = list[index];
    if(s.scope == scope)
      s.scope = null;
    list.RemoveAt(index);
  }

  public Symbol TryAt(int index)
  {
    if(index < 0 || index >= list.Count)
      return null;
    return list[index];
  }

  public int IndexOf(Symbol s)
  {
    return list.IndexOf(s);
  }

  public int IndexOf(string name)
  {
    for(int i=0;i<list.Count;++i)
    {
      if(list[i].name == name)
        return i;
    }
    return -1;
  }

  public bool Replace(Symbol what, Symbol subst)
  {
    int idx = IndexOf(what);
    if(idx == -1)
      return false;
    
    list[idx] = subst;

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
  }

  public void Sync(marshall.SyncContext ctx) 
  {
    marshall.Marshall.SyncGeneric(ctx, list);

    if(ctx.is_read)
      foreach(var tmp in list)
        tmp.scope = scope;
  }

  public void UnionWith(SymbolsStorage o)
  {
    for(int i=0;i<o.Count;++i)
      Add(o[i]);
  }
}

public class TypeSet<T> : marshall.IMarshallable where T : class, IType
{
  //TODO: since TypeProxy implements custom Equals we could use HashSet here
  List<Proxy<T>> list = new List<Proxy<T>>();

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
      var s = tp.Get();
      if(s == null)
        throw new Exception("Type not found: " + tp.path);
      return s;
    }
  }

  public TypeSet()
  {}

  public bool Add(T t)
  {
    return Add(new Proxy<T>(t));
  }

  public bool Add(Proxy<T> tp)
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

  public void Sync(marshall.SyncContext ctx) 
  {
    marshall.Marshall.Sync(ctx, list);
  }
}

public class Indexer<T> where T : INamed, IScopeIndexed 
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
    if(sym.scope_idx == -1)
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

public class VarIndex : Indexer<VariableSymbol> {}

public class NativeFuncIndex : Indexer<FuncSymbolNative> 
{
  public NativeFuncIndex()
  {}

  public NativeFuncIndex(NativeFuncIndex other)
  {
    index.AddRange(other.index);
  }
}

public class SymbolFactory : marshall.IFactory
{
  public Types types;
  public INamedResolver resolver;

  public SymbolFactory(Types types, INamedResolver res) 
  {
    this.types = types;
    this.resolver = res; 
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
        return new Namespace(types.nfunc_index);
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
