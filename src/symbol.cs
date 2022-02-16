using System;
using System.Collections.Generic;

namespace bhl {

using marshall;

public abstract class Symbol : IMarshallableGeneric 
{
  public string name;
  public TypeProxy type;
  // All symbols know what scope contains them
  public IScope scope;
#if BHL_FRONT
  // Location in parse tree, can be null if it's a native symbol
  public WrappedParseTree parsed;
#endif

  public Symbol(string name, TypeProxy type) 
  { 
    this.name = name; 
    this.type = type;
  }

  public override string ToString() 
  {
    return '<' + name + ':' + type.name + '>';
  }

  public virtual uint getClassId()
  {
    throw new NotImplementedException();
  }

  public virtual int GetFieldsNum()
  {
    return 2;
  }

  public virtual void Sync(SyncContext ctx)
  {
    Marshall.Sync(ctx, ref name);
    Marshall.Sync(ctx, ref type);
  }
}

public abstract class BuiltInSymbol : Symbol, IType 
{
  public BuiltInSymbol(string name) 
    : base(name, default(TypeProxy)/*set below*/) 
  {
    this.type = new TypeProxy(this);
  }

  public string GetName() { return name; }

  //contains no data
  public override int GetFieldsNum()
  {
    return 0;
  }

  //contains no data
  public override void Sync(SyncContext ctx)
  {
  }
}

public class IntSymbol : BuiltInSymbol
{
  public const int CLASS_ID = 1;

  public IntSymbol()
    : base("int")
  {}

  public override uint getClassId()
  {
    return CLASS_ID;
  }
}

public class BoolSymbol : BuiltInSymbol
{
  public const int CLASS_ID = 2;

  public BoolSymbol()
    : base("bool")
  {}

  public override uint getClassId()
  {
    return CLASS_ID;
  }
}

public class StringSymbol : BuiltInSymbol
{
  public const int CLASS_ID = 3;

  public StringSymbol()
    : base("string")
  {}

  public override uint getClassId()
  {
    return CLASS_ID;
  }
}

public class FloatSymbol : BuiltInSymbol
{
  public const int CLASS_ID = 4;

  public FloatSymbol()
    : base("float")
  {}

  public override uint getClassId()
  {
    return CLASS_ID;
  }
}

public class VoidSymbol : BuiltInSymbol
{
  public const int CLASS_ID = 5;

  public VoidSymbol()
    : base("void")
  {}

  public override uint getClassId()
  {
    return CLASS_ID;
  }
}

public class AnySymbol : BuiltInSymbol
{
  public const int CLASS_ID = 6;

  public AnySymbol()
    : base("any")
  {}

  public override uint getClassId()
  {
    return CLASS_ID;
  }
}

public class NullSymbol : BuiltInSymbol
{
  public const int CLASS_ID = 7;

  public NullSymbol()
    : base("null")
  {}

  public override uint getClassId()
  {
    return CLASS_ID;
  }
}

public class ClassSymbol : EnclosingSymbol, IScope, IType 
{
  internal ClassSymbol super_class;

  public SymbolsDictionary members = new SymbolsDictionary();

  public VM.ClassCreator creator;

#if BHL_FRONT
  public ClassSymbol(
    WrappedParseTree parsed, 
    string name, 
    ClassSymbol super_class, 
    VM.ClassCreator creator = null
  )
    : this(name, super_class, creator)
  {
    this.parsed = parsed;
  }
#endif

  public ClassSymbol(
    string name, 
    ClassSymbol super_class, 
    VM.ClassCreator creator = null
  )
    : base(name)
  {
    this.type = new TypeProxy(this);
    this.super_class = super_class;
    this.creator = creator;

    //NOTE: we define parent members in the current class
    //      scope as well. We do this since we want to  
    //      address its members simply by int index
    if(super_class != null)
    {
      var super_members = super_class.GetMembers();
      for(int i=0;i<super_members.Count;++i)
      {
        var sym = super_members[i];
        //NOTE: using base Define instead of our own version
        //      since we want to avoid 'already defined' checks
        base.Define(sym);
      }
    }
  }

  public bool IsSubclassOf(ClassSymbol p)
  {
    if(this == p)
      return true;

    for(var tmp = super_class; tmp != null; tmp = tmp.super_class)
    {
      if(tmp == p)
        return true;
    }
    return false;
  }

  public override IScope GetFallbackScope() 
  {
    return super_class == null ? this.scope : super_class;
  }

  public Symbol ResolveMember(string name)
  {
    Symbol sym = null;
    members.TryGetValue(name, out sym);
    if(sym != null)
      return sym;

    if(super_class != null)
      return super_class.ResolveMember(name);

    return null;
  }

  public override void Define(Symbol sym) 
  {
    if(super_class != null && super_class.GetMembers().Contains(sym.name))
      throw new SymbolError(sym, "already defined symbol '" + sym.name + "'"); 

    base.Define(sym);
  }

  public string GetName() { return name; }

  public override SymbolsDictionary GetMembers() { return members; }

  public override string ToString() 
  {
    return "class "+name+":{"+
            string.Join(",", members.GetStringKeys().ToArray())+"}";
  }
}

abstract public class ArrayTypeSymbol : ClassSymbol
{
  public TypeProxy item_type;

  public ArrayTypeSymbol(TypeSystem ts, string name, TypeProxy item_type)     
    : base(name, super_class: null)
  {
    this.item_type = item_type;

    this.creator = CreateArr;

    {
      var fn = new FuncSymbolNative("Add", ts.Type("void"), Add,
        new FuncArgSymbol("o", item_type)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("At", item_type, At,
        new FuncArgSymbol("idx", ts.Type("int"))
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("SetAt", item_type, SetAt,
        new FuncArgSymbol("idx", ts.Type("int")),
        new FuncArgSymbol("o", item_type)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("RemoveAt", ts.Type("void"), RemoveAt,
        new FuncArgSymbol("idx", ts.Type("int"))
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("Clear", ts.Type("void"), Clear);
      this.Define(fn);
    }

    {
      var vs = new FieldSymbol("Count", ts.Type("int"), GetCount, null);
      this.Define(vs);
    }

    {
      //hidden system method not available directly
      var fn = new FuncSymbolNative("$AddInplace", ts.Type("void"), AddInplace,
        new FuncArgSymbol("o", item_type)
      );
      this.Define(fn);
    }
  }

  public ArrayTypeSymbol(TypeSystem ts, TypeProxy item_type) 
    : this(ts, item_type.name + "[]", item_type)
  {}

  public abstract void CreateArr(VM.Frame frame, ref Val v);
  public abstract void GetCount(Val ctx, ref Val v);
  public abstract ICoroutine Add(VM.Frame frame, FuncArgsInfo args_info, ref BHS status);
  public abstract ICoroutine At(VM.Frame frame, FuncArgsInfo args_info, ref BHS status);
  public abstract ICoroutine SetAt(VM.Frame frame, FuncArgsInfo args_info, ref BHS status);
  public abstract ICoroutine RemoveAt(VM.Frame frame, FuncArgsInfo args_info, ref BHS status);
  public abstract ICoroutine Clear(VM.Frame frame, FuncArgsInfo args_info, ref BHS status);
  public abstract ICoroutine AddInplace(VM.Frame frame, FuncArgsInfo args_info, ref BHS status);
}

public class GenericArrayTypeSymbol : ArrayTypeSymbol
{
  public static readonly string CLASS_TYPE = "[]";

  public GenericArrayTypeSymbol(TypeSystem ts, TypeProxy item_type) 
    : base(ts, item_type)
  {
    name = CLASS_TYPE;
  }

  static IList<Val> AsList(Val arr)
  {
    var lst = arr.obj as IList<Val>;
    if(lst == null)
      throw new Exception("Not a ValList: " + (arr.obj != null ? arr.obj.GetType().Name : ""+arr));
    return lst;
  }

  public override void CreateArr(VM.Frame frm, ref Val v)
  {
    v.SetObj(ValList.New(frm.vm));
  }

  public override void GetCount(Val ctx, ref Val v)
  {
    var lst = AsList(ctx);
    v.SetNum(lst.Count);
  }
  
  public override ICoroutine Add(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    var val = frame.stack.Pop();
    var arr = frame.stack.Pop();
    var lst = AsList(arr);
    lst.Add(val);
    val.Release();
    arr.Release();
    return null;
  }

  public override ICoroutine AddInplace(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    var val = frame.stack.Pop();
    var arr = frame.stack.Peek();
    var lst = AsList(arr);
    lst.Add(val);
    val.Release();
    return null;
  }

  public override ICoroutine At(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)frame.stack.PopRelease().num;
    var arr = frame.stack.Pop();
    var lst = AsList(arr);
    var res = lst[idx]; 
    frame.stack.PushRetain(res);
    arr.Release();
    return null;
  }

  public override ICoroutine SetAt(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)frame.stack.PopRelease().num;
    var arr = frame.stack.Pop();
    var val = frame.stack.Pop();
    var lst = AsList(arr);
    lst[idx] = val;
    val.Release();
    arr.Release();
    return null;
  }

  public override ICoroutine RemoveAt(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)frame.stack.PopRelease().num;
    var arr = frame.stack.Pop();
    var lst = AsList(arr);
    lst.RemoveAt(idx); 
    arr.Release();
    return null;
  }

  public override ICoroutine Clear(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    var arr = frame.stack.Pop();
    var lst = AsList(arr);
    lst.Clear();
    arr.Release();
    return null;
  }
}

public class ArrayTypeSymbolT<T> : ArrayTypeSymbol where T : new()
{
  public delegate IList<T> CreatorCb();
  public static CreatorCb Creator;

  public ArrayTypeSymbolT(TypeSystem ts, string name, TypeProxy item_type, CreatorCb creator) 
    : base(ts, name, item_type)
  {
    Creator = creator;
  }

  public ArrayTypeSymbolT(TypeSystem ts, TypeProxy item_type, CreatorCb creator) 
    : base(ts, item_type.name + "[]", item_type)
  {}

  public override void CreateArr(VM.Frame frm, ref Val v)
  {
    v.obj = Creator();
  }

  public override void GetCount(Val ctx, ref Val v)
  {
    v.SetNum(((IList<T>)ctx.obj).Count);
  }
  
  public override ICoroutine Add(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    var val = frame.stack.Pop();
    var arr = frame.stack.Pop();
    var lst = (IList<T>)arr.obj;
    lst.Add((T)val.obj);
    val.Release();
    arr.Release();
    return null;
  }

  public override ICoroutine AddInplace(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    var val = frame.stack.Pop();
    var arr = frame.stack.Peek();
    var lst = (IList<T>)arr.obj;
    T obj = (T)val.obj;
    lst.Add(obj);
    val.Release();
    return null;
  }

  public override ICoroutine At(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)frame.stack.PopRelease().num;
    var arr = frame.stack.Pop();
    var lst = (IList<T>)arr.obj;
    var res = Val.NewObj(frame.vm, lst[idx], TypeSystem.Any);
    frame.stack.Push(res);
    arr.Release();
    return null;
  }

  public override ICoroutine SetAt(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)frame.stack.PopRelease().num;
    var arr = frame.stack.Pop();
    var val = frame.stack.Pop();
    var lst = (IList<T>)arr.obj;
    lst[idx] = (T)val.obj;
    val.Release();
    arr.Release();
    return null;
  }

  public override ICoroutine RemoveAt(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)frame.stack.PopRelease().num;
    var arr = frame.stack.Pop();
    var lst = (IList<T>)arr.obj;
    lst.RemoveAt(idx); 
    arr.Release();
    return null;
  }

  public override ICoroutine Clear(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)frame.stack.PopRelease().num;
    var arr = frame.stack.Pop();
    var lst = (IList<T>)arr.obj;
    lst.Clear();
    arr.Release();
    return null;
  }
}

public interface IScopeIndexed
{
  //index in an enclosing scope
  int scope_idx { get; set; }
}

public class VariableSymbol : Symbol, IScopeIndexed
{
  public const uint CLASS_ID = 1982058327;

  //if variable is global in a module it stores its id
  public uint module_id;

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
  //e.g. for  { { int a = 1 } } scope level will be 2
  public int scope_level;
  //once we leave the scope level the variable was defined at   
  //we mark this symbol as 'out of scope'
  public bool is_out_of_scope;

  public VariableSymbol(WrappedParseTree parsed, string name, TypeProxy type) 
    : this(name, type) 
  {
    this.parsed = parsed;
  }
#endif

  public VariableSymbol(string name, TypeProxy type) 
    : base(name, type) 
  {}

  //marshall version
  public VariableSymbol()
    : base("", new TypeProxy(TypeSystem.Void))
  {}

  public override uint getClassId()
  {
    return CLASS_ID;
  }

  public override int GetFieldsNum()
  {
    return base.GetFieldsNum() + 1;
  }

  public override void Sync(SyncContext ctx)
  {
    base.Sync(ctx);

    Marshall.Sync(ctx, ref module_id);
  }
}

public class FuncArgSymbol : VariableSymbol
{
  public bool is_ref;

#if BHL_FRONT
  public FuncArgSymbol(WrappedParseTree parsed, string name, TypeProxy type, bool is_ref = false)
    : this(name, type, is_ref)
  {
    this.parsed = parsed;
  }
#endif

  public FuncArgSymbol(string name, TypeProxy type, bool is_ref = false)
    : base(name, type)
  {
    this.is_ref = is_ref;
  }
}

public class FieldSymbol : VariableSymbol
{
  public VM.FieldGetter getter;
  public VM.FieldSetter setter;
  public VM.FieldRef getref;

  public FieldSymbol(string name, TypeProxy type, VM.FieldGetter getter = null, VM.FieldSetter setter = null, VM.FieldRef getref = null) 
    : base(name, type)
  {
    this.getter = getter;
    this.setter = setter;
    this.getref = getref;
  }
}

public class FieldSymbolScript : FieldSymbol
{
  public FieldSymbolScript(string name, TypeProxy type) 
    : base(name, type, null, null, null)
  {
    this.getter = Getter;
    this.setter = Setter;
    this.getref = Getref;
  }

  void Getter(Val ctx, ref Val v)
  {
    var m = (IList<Val>)ctx.obj;
    v.ValueCopyFrom(m[scope_idx]);
  }

  void Setter(ref Val ctx, Val v)
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

  void Getref(Val ctx, out Val v)
  {
    var m = (IList<Val>)ctx.obj;
    v = m[scope_idx];
  }
}

//TODO: the code in Resolve(..) and Define(..) is quite similar to the one
//      in Scope.Resolve(..) and Scope.Define(..)
public abstract class EnclosingSymbol : Symbol, IScope 
{
  abstract public SymbolsDictionary GetMembers();

#if BHL_FRONT
  public EnclosingSymbol(WrappedParseTree parsed, string name) 
    : this(name)
  {
    this.parsed = parsed;
  }
#endif

  public EnclosingSymbol(string name) 
    : base(name, type: default(TypeProxy))
  {}

  public virtual IScope GetFallbackScope() { return this.scope; }

  public virtual Symbol Resolve(string name) 
  {
    Symbol s;
    GetMembers().TryGetValue(name, out s);
    if(s != null)
    {
#if BHL_FRONT
      if(s is VariableSymbol vs && vs.is_out_of_scope)
        return null;
      else
#endif
        return s;
    }

    var fallback = GetFallbackScope();
    if(fallback != null)
      return fallback.Resolve(name);

    return null;
  }

  public virtual void Define(Symbol sym) 
  {
    var members = GetMembers(); 
    if(members.Contains(sym.name))
      throw new SymbolError(sym, "already defined symbol '" + sym.name + "'"); 

    if(sym is IScopeIndexed si && si.scope_idx == -1)
      si.scope_idx = members.Count; 
    sym.scope = this; // track the scope in each symbol

    members.Add(sym);
  }
}

public abstract class FuncSymbol : EnclosingSymbol, IScopeIndexed
{
  SymbolsDictionary members = new SymbolsDictionary();

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
  public FuncSymbol(WrappedParseTree parsed, string name, FuncSignature sig) 
    : this(name, sig)
  {
    this.parsed = parsed;
  }
#endif

  public FuncSymbol(string name, FuncSignature sig) 
    : base(name)
  {
    this.type = new TypeProxy(sig);
  }

  public override SymbolsDictionary GetMembers() { return members; }

  public override IScope GetFallbackScope() 
  { 
    //NOTE: we forbid resolving class members 
    //      inside methods without 'this.' prefix on purpose
    if(this.scope is ClassSymbol cs)
      return cs.scope;
    else
      return this.scope; 
  }

  public FuncSignature GetSignature()
  {
    return (FuncSignature)this.type.Get();
  }

  public IType GetReturnType()
  {
    return GetSignature().ret_type.Get();
  }

  public FuncArgSymbol GetArg(int idx)
  {
    return (FuncArgSymbol)members[idx];
  }

  public SymbolsDictionary GetArgs()
  {
    var args = new SymbolsDictionary();
    for(int i=0;i<GetSignature().arg_types.Count;++i)
      args.Add(members[i]);
    return args;
  }

  public int GetTotalArgsNum() { return GetSignature().arg_types.Count; }
  public abstract int GetDefaultArgsNum();
  public int GetRequiredArgsNum() { return GetTotalArgsNum() - GetDefaultArgsNum(); } 
}

public class FuncSymbolScript : FuncSymbol
{
  public int local_vars_num;
  public int default_args_num;
  public int ip_addr;

  public FuncSymbolScript(FuncSignature sig, string name, int default_args_num, int local_vars_num)
    : base(name, sig)
  {
    this.name = name;
    this.local_vars_num = local_vars_num;
  }

  public AST_FuncDecl decl;

#if BHL_FRONT
  public FuncSymbolScript(
    WrappedParseTree parsed, 
    TypeSystem ts,
    AST_FuncDecl decl, 
    FuncSignature sig
  ) 
    : this(ts, decl, sig)
  {
    this.parsed = parsed;
  }
#endif

  public FuncSymbolScript(TypeSystem ts, AST_FuncDecl decl, FuncSignature sig)
    : base(decl.name, sig)
  {
    this.decl = decl;

    for(int i=0;i<decl.GetTotalArgsNum();++i)
    {
      var tmp = (AST_Interim)decl.children[0];
      var var_decl = (AST_VarDecl)tmp.children[i];
      //TODO: add proper serialization of types 
      //var arg_type = ts.Type(var_decl.type);
      var arg_type = ts.Type("void");
      if(var_decl.is_ref)
        arg_type = ts.Type(new RefType(arg_type));
      GetSignature().AddArg(arg_type);
    }
  }

  public override int GetDefaultArgsNum() { return decl.GetDefaultArgsNum(); }
}

#if BHL_FRONT
public class LambdaSymbol : FuncSymbolScript
{
  public AST_LambdaDecl ldecl 
  {
    get {
      return (AST_LambdaDecl)decl;
    }
  }

  List<FuncSymbol> fdecl_stack;

  public LambdaSymbol(
    WrappedParseTree parsed, 
    TypeSystem ts,
    AST_LambdaDecl decl, 
    List<FuncSymbol> fdecl_stack,
    FuncSignature sig
  ) 
    : base(parsed, ts, decl, sig)
  {
    this.fdecl_stack = fdecl_stack;
  }

  public VariableSymbol AddUpValue(VariableSymbol src)
  {
    var local = new VariableSymbol(src.parsed, src.name, src.type);

    this.Define(local);

    var up = AST_Util.New_UpVal(local.name, local.scope_idx, src.scope_idx); 
    ldecl.upvals.Add(up);

    return local;
  }

  public override Symbol Resolve(string name) 
  {
    Symbol s = null;
    GetMembers().TryGetValue(name, out s);
    if(s != null)
      return s;

    s = ResolveUpvalue(name);
    if(s != null)
      return s;

    var fback = GetFallbackScope();
    if(fback != null)
      return fback.Resolve(name);

    return null;
  }

  Symbol ResolveUpvalue(string name)
  {
    int my_idx = FindMyIdxInStack();
    if(my_idx == -1)
      throw new Exception("Not found");

    for(int i=my_idx;i-- > 0;)
    {
      var decl = fdecl_stack[i];

      //NOTE: only variable symbols are considered
      Symbol res = null;
      decl.GetMembers().TryGetValue(name, out res);
      if(res is VariableSymbol vs && !vs.is_out_of_scope)
        return AssignUpValues(vs, i+1, my_idx);
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
  public delegate ICoroutine Cb(VM.Frame frm, FuncArgsInfo args_info, ref BHS status); 
  public Cb cb;

  int def_args_num;

  public FuncSymbolNative(
    string name, 
    TypeProxy ret_type, 
    Cb cb,
    params FuncArgSymbol[] args
  ) 
    : this(name, ret_type, 0, cb, args)
  {}

  public FuncSymbolNative(
    string name, 
    TypeProxy ret_type, 
    int def_args_num,
    Cb cb,
    params FuncArgSymbol[] args
  ) 
    : base(name, new FuncSignature(ret_type))
  {
    this.cb = cb;
    this.def_args_num = def_args_num;

    foreach(var arg in args)
    {
      base.Define(arg);
      GetSignature().AddArg(arg.type);
    }
  }

  public override int GetDefaultArgsNum() { return def_args_num; }

  public override void Define(Symbol sym) 
  {
    throw new Exception("Defining symbols here is not allowed");
  }
}

public class ClassSymbolNative : ClassSymbol
{
  public ClassSymbolNative(string name, ClassSymbol super_class, VM.ClassCreator creator = null)
    : base(name, super_class, creator)
  {}

  public void OverloadBinaryOperator(FuncSymbol s)
  {
    if(s.GetArgs().Count != 1)
      throw new SymbolError(s, "operator overload must have exactly one argument");

    if(s.GetReturnType() == TypeSystem.Void)
      throw new SymbolError(s, "operator overload return value can't be void");

    Define(s);
  }
}

public class ClassSymbolScript : ClassSymbol
{
  public AST_ClassDecl decl;

  public ClassSymbolScript(string name, AST_ClassDecl decl, ClassSymbol super_class = null)
    : base(name, super_class)
  {
    this.decl = decl;
    this.creator = ClassCreator;
  }

#if BHL_FRONT
  public ClassSymbolScript(WrappedParseTree parsed, string name, AST_ClassDecl decl, ClassSymbol super_class = null)
    : this(name, decl, super_class)
  {
    this.parsed = parsed;
  }
#endif

  void ClassCreator(VM.Frame frm, ref Val data)
  {
    //TODO: add handling of native super class
    //if(super_class is ClassSymbolNative cn)
    //{
    //}

    //NOTE: object's raw data is a list
    var vl = ValList.New(frm.vm);
    data.SetObj(vl);
    
    for(int i=0;i<members.Count;++i)
    {
      var m = members[i];
      //NOTE: Members contain all kinds of symbols: methods and attributes,
      //      however we need to properly setup attributes only. 
      //      Methods will be initialized with special case 'nil' value.
      //      Maybe we should track data members and methods separately someday. 
      if(m is VariableSymbol)
      {
        var type = m.type.Get();
        var v = frm.vm.MakeDefaultVal(type);
        vl.Add(v);
        //ownership was passed to list, let's release it
        v.Release();
      }
      else
        vl.Add(frm.vm.Null);
    }
  }
}

public class EnumSymbol : EnclosingSymbol, IType
{
  public SymbolsDictionary members = new SymbolsDictionary();

#if BHL_FRONT
  public EnumSymbol(WrappedParseTree parsed, string name)
    : this(name)
  {
    this.parsed = parsed;
  }
#endif

  public EnumSymbol(string name)
      : base(name)
  {
    this.type = new TypeProxy(this);
  }

  public string GetName() { return name; }

  public override SymbolsDictionary GetMembers() { return members; }

  public EnumItemSymbol FindValue(string name)
  {
    return base.Resolve(name) as EnumItemSymbol;
  }

  public override string ToString() 
  {
    return "enum "+name+":{"+
            string.Join(",", members.GetStringKeys().ToArray())+"}";
  }
}

public class EnumSymbolScript : EnumSymbol
{
  public EnumSymbolScript(string name)
    : base(name)
  {}

  //0 - OK, 1 - duplicate key, 2 - duplicate value
  public int TryAddItem(string name, int val)
  {
    for(int i=0;i<members.Count;++i)
    {
      var m = (EnumItemSymbol)members[i];
      if(m.val == val)
        return 2;
      else if(m.name == name)
        return 1;
    }

    var item = new EnumItemSymbol(this, name, val);
    members.Add(item);
    return 0;
  }
}

public class EnumItemSymbol : Symbol, IType 
{
  public EnumSymbol owner;
  public int val;

#if BHL_FRONT
  public EnumItemSymbol(WrappedParseTree parsed, EnumSymbol owner, string name, int val = 0) 
    : this(owner, name, val) 
  {
    this.parsed = parsed;
  }
#endif

  public EnumItemSymbol(EnumSymbol owner, string name, int val = 0) 
    : base(name, new TypeProxy(owner)) 
  {
    this.owner = owner;
    this.val = val;
  }
  public string GetName() { return owner.name; }
}

public class SymbolsDictionary : IMarshallable
{
  Dictionary<string, Symbol> str2symb = new Dictionary<string, Symbol>();
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

  public bool Contains(string key)
  {
    return str2symb.ContainsKey(key);
  }

  public bool TryGetValue(string key, out Symbol val)
  {
    return str2symb.TryGetValue(key, out val);
  }

  public Symbol Find(string key)
  {
    Symbol s = null;
    TryGetValue(key, out s);
    return s;
  }

  public void Add(Symbol s)
  {
    str2symb.Add(s.name, s);
    list.Add(s);
  }

  public void RemoveAt(int index)
  {
    var s = list[index];
    str2symb.Remove(s.name);
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

  public int IndexOf(string key)
  {
    var s = Find(key);
    if(s == null)
      return -1;
    return IndexOf(s);
  }

  public void Clear()
  {
    str2symb.Clear();
    list.Clear();
  }

  public List<string> GetStringKeys()
  {
    var res = new List<string>();
    for(int i=0;i<list.Count;++i)
      res.Add(list[i].name);
    return res;
  }

  public int FindStringKeyIndex(string key)
  {
    for(int i=0;i<list.Count;++i)
    {
      if(list[i].name == key)
        return i;
    }
    return -1;
  }

  public int GetFieldsNum()
  {
    return 1;
  }

  public void Sync(SyncContext ctx) 
  {
    Marshall.SyncGeneric(ctx, list);
    if(ctx.is_read)
    {
      foreach(var s in list)
        str2symb.Add(s.name, s);
    }
  }
}

public class SymbolFactory : IFactory
{
  public TypeSystem types;

  public SymbolFactory(TypeSystem types)
  {
    this.types = types;
  }

  public IMarshallableGeneric CreateById(uint id) 
  {
    switch(id)
    {
      case IntSymbol.CLASS_ID:
        return TypeSystem.Int;
      case StringSymbol.CLASS_ID:
        return TypeSystem.String;
      case VariableSymbol.CLASS_ID:
        return new VariableSymbol(); 
      default:
        return null;
    }
  }
}

} //namespace bhl
