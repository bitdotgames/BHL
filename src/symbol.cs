using System;
using System.Collections;
using System.Collections.Generic;

#if BHL_FRONT
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
#endif

namespace bhl {

public interface IType 
{
  string GetName();
}

public class TypeRef
{
  IType _type;
  public IType type { 
    get {
      return Get();
    }
  }
  public string name { get ; private set;}
  public bool is_ref;
  TypeSystem ts;
#if BHL_FRONT
  //NOTE: parse location of the type
  public IParseTree parsed;
#endif

  public TypeRef(TypeSystem ts, string name)
  {
    this.ts = ts;
    this.name = name;
    this._type = null;
  }

  public TypeRef(IType type)
  {
    this.name = type.GetName();
    this._type = type;
  }

  public IType Get()
  {
    if(_type != null)
      return _type;

    if(string.IsNullOrEmpty(name))
      return null;

    _type = (bhl.IType)ts.Resolve(name);
    return _type;
  }
}

#if BHL_FRONT
public class WrappedParseTree
{
  public IParseTree tree;
  public ITokenStream tokens;
  public IType eval_type;

  public string Location()
  {
    var interval = tree.SourceInterval;
    var begin = tokens.Get(interval.a);

    string line = string.Format("@({0},{1})", begin.Line, begin.Column);
    return line;
  }
}
#endif

public class Symbol 
{
  public string name;
  public TypeRef type;
  // All symbols know what scope contains them
  public IScope scope;
#if BHL_FRONT
  // Location in parse tree, can be null if it's a native symbol
  public WrappedParseTree parsed;
#endif

  public Symbol(string name, TypeRef type) 
  { 
    this.name = name; 
    this.type = type;
  }

  public override string ToString() 
  {
    return '<' + name + ':' + type + '>';
  }

  public string Location()
  {
#if BHL_FRONT
    if(parsed != null)
      return parsed.Location();
#endif
    return name + "(?)";
  }
}

// A symbol to represent built in types such int, float primitive types
public class BuiltInTypeSymbol : Symbol, IType 
{
  public BuiltInTypeSymbol(string name) 
    : base(name, null/*set below*/) 
  {
    this.type = new TypeRef(this);
  }

  public string GetName() { return name; }
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
    this.type = new TypeRef(this);
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

  public virtual string Type()
  {
    return this.name;
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
      throw new UserError(sym.Location() + ": already defined symbol '" + sym.name + "'"); 

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
  public TypeRef item_type;

  public ArrayTypeSymbol(TypeSystem ts, string name, TypeRef item_type)     
    : base(name, super_class: null)
  {
    this.item_type = item_type;

    this.creator = CreateArr;

    {
      var fn = new FuncSymbolNative("Add", ts.Type("void"), Add);
      fn.Define(new FuncArgSymbol("o", item_type));
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("At", item_type, At);
      fn.Define(new FuncArgSymbol("idx", ts.Type("int")));
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("SetAt", item_type, SetAt);
      fn.Define(new FuncArgSymbol("idx", ts.Type("int")));
      fn.Define(new FuncArgSymbol("o", item_type));
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("RemoveAt", ts.Type("void"), RemoveAt);
      fn.Define(new FuncArgSymbol("idx", ts.Type("int")));
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
      var fn = new FuncSymbolNative("$AddInplace", ts.Type("void"), AddInplace);
      fn.Define(new FuncArgSymbol("o", item_type));
      this.Define(fn);
    }
  }

  public ArrayTypeSymbol(TypeSystem ts, TypeRef item_type) 
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

//NOTE: This one is used as a fallback for all arrays which
//      were not explicitely re-defined. Fallback happens during
//      compilation phase in Scope.Type(..) method
//     
public class GenericArrayTypeSymbol : ArrayTypeSymbol
{
  public static readonly string CLASS_TYPE = "[]";

  public override string Type()
  {
    return CLASS_TYPE;
  }

  public GenericArrayTypeSymbol(TypeSystem ts, TypeRef item_type) 
    : base(ts, item_type)
  {}

  static IList<Val> AsList(Val arr)
  {
    var lst = arr.obj as IList<Val>;
    if(lst == null)
      throw new UserError("Not a ValList: " + (arr.obj != null ? arr.obj.GetType().Name : ""+arr));
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
    int idx = (int)frame.stack.PopRelease().num;
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

  public ArrayTypeSymbolT(TypeSystem ts, string name, TypeRef item_type, CreatorCb creator) 
    : base(ts, name, item_type)
  {
    Creator = creator;
  }

  public ArrayTypeSymbolT(TypeSystem ts, TypeRef item_type, CreatorCb creator) 
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

  public VariableSymbol(WrappedParseTree parsed, string name, TypeRef type) 
    : this(name, type) 
  {
    this.parsed = parsed;
  }
#endif

  public VariableSymbol(string name, TypeRef type) 
    : base(name, type) 
  {}
}

public class FuncArgSymbol : VariableSymbol
{
  public bool is_ref;

#if BHL_FRONT
  public FuncArgSymbol(WrappedParseTree parsed, string name, TypeRef type, bool is_ref = false)
    : this(name, type, is_ref)
  {
    this.parsed = parsed;
  }
#endif

  public FuncArgSymbol(string name, TypeRef type, bool is_ref = false)
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

  public FieldSymbol(string name, TypeRef type, VM.FieldGetter getter = null, VM.FieldSetter setter = null, VM.FieldRef getref = null) 
    : base(name, type)
  {
    this.getter = getter;
    this.setter = setter;
    this.getref = getref;
  }
}

public class FieldSymbolScript : FieldSymbol
{
  public FieldSymbolScript(TypeSystem ts, string name, string type) 
    : base(name, ts.Type(type), null, null, null)
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
    : base(name, type: null)
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
      throw new UserError(sym.Location() + ": already defined symbol '" + sym.name + "'"); 

    if(sym is IScopeIndexed si && si.scope_idx == -1)
      si.scope_idx = members.Count; 
    sym.scope = this; // track the scope in each symbol

    members.Add(sym);
  }
}

public class MultiType : IType
{
  public string name;

  public List<TypeRef> items = new List<TypeRef>();

  public string GetName() { return name; }

  public void Update()
  {
    string tmp = "";
    //TODO: can this be more optimal?
    for(int i=0;i<items.Count;++i)
    {
      if(i > 0)
        tmp += ",";
      tmp += items[i].name;
    }

    name = tmp;
  }
}

public class FuncType : IType
{
  public string name;

  public TypeRef ret_type;
  public List<TypeRef> arg_types = new List<TypeRef>();

  public string GetName() { return name; }

  public FuncType(TypeRef ret_type, List<TypeRef> arg_types)
  {
    this.ret_type = ret_type;
    this.arg_types = arg_types;
    Update();
  }

  public FuncType(TypeRef ret_type)
  {
    this.ret_type = ret_type;
    Update();
  }

  public void Update()
  {
    string tmp = ret_type.name + "^("; 
    for(int i=0;i<arg_types.Count;++i)
    {
      if(i > 0)
        tmp += ",";
      if(arg_types[i].is_ref)
        tmp += "ref ";
      tmp += arg_types[i].name;
    }
    tmp += ")";

    name = tmp;
  }
}

//TODO: why there's a separate FuncType?  
public class FuncSymbol : EnclosingSymbol, IScopeIndexed
{
  SymbolsDictionary members = new SymbolsDictionary();
  SymbolsDictionary args = new SymbolsDictionary();

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
  public bool return_statement_found = false;

  public FuncSymbol(WrappedParseTree parsed, string name, FuncType type) 
    : this(name, type)
  {
    this.parsed = parsed;
  }

  public virtual IParseTree GetDefaultArgsExprAt(int idx) { return null; }

#endif

  public FuncSymbol(string name, FuncType type) 
    : base(name)
  {
    this.type = new TypeRef(type);
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

  public FuncType GetFuncType()
  {
    return (FuncType)this.type.Get();
  }

  public IType GetReturnType()
  {
    return GetFuncType().ret_type.Get();
  }

  public SymbolsDictionary GetArgs()
  {
    return args;
  }

  public void DefineArg(string name) 
  {
    Symbol sym = null;
    if(!members.TryGetValue(name, out sym))
      throw new Exception("No such member: " + name);
    var fsym = sym as FuncArgSymbol;
    if(fsym == null)
      throw new Exception("Bad symbol");
    args.Add(fsym);
  }

  public virtual int GetTotalArgsNum() { return 0; }
  public virtual int GetDefaultArgsNum() { return 0; }
  public int GetRequiredArgsNum() { return GetTotalArgsNum() - GetDefaultArgsNum(); } 

  public bool IsArgRefAt(int idx) 
  {
    var farg = members[idx] as FuncArgSymbol;
    return farg != null && farg.is_ref;
  }
}

#if BHL_FRONT
public class LambdaSymbol : FuncSymbol
{
  public AST_LambdaDecl decl;

  List<FuncSymbol> fdecl_stack;

  public LambdaSymbol(
    TypeSystem ts,
    WrappedParseTree parsed, 
    bhlParser.FuncLambdaContext lmb_ctx,
    AST_LambdaDecl decl, 
    TypeRef ret_type,
    List<FuncSymbol> fdecl_stack
  ) 
    : base(parsed, decl.name, new FuncType(ret_type))
  {
    this.decl = decl;
    this.fdecl_stack = fdecl_stack;

    var ft = GetFuncType();
    var fparams = lmb_ctx.funcParams();
    if(fparams != null)
    {
      for(int i=0;i<fparams.funcParamDeclare().Length;++i)
      {
        var vd = fparams.funcParamDeclare()[i];
        ft.arg_types.Add(ts.Type(vd.type()));
      }
    }
    ft.Update();
  }

  public VariableSymbol AddUpValue(VariableSymbol src)
  {
    var local = new VariableSymbol(src.parsed, src.name, src.type);

    this.Define(local);

    var up = AST_Util.New_UpVal(local.name, local.scope_idx, src.scope_idx); 
    decl.upvals.Add(up);

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

public class FuncSymbolScript : FuncSymbol
{
  public AST_FuncDecl decl;

#if BHL_FRONT
  //storing fparams so it can be accessed later for misc things, e.g. default args
  public bhlParser.FuncParamsContext fparams;

  public FuncSymbolScript(
    TypeSystem ts,
    AST_FuncDecl decl, 
    WrappedParseTree parsed, 
    TypeRef ret_type, 
    bhlParser.FuncParamsContext fparams
  ) 
    : this(ts, decl, ret_type)
  {
    this.parsed = parsed;
    this.fparams = fparams;

    if(parsed != null)
    {
      var ft = GetFuncType();
      if(fparams != null)
      {
        for(int i=0;i<fparams.funcParamDeclare().Length;++i)
        {
          var vd = fparams.funcParamDeclare()[i];
          var type = ts.Type(vd.type());
          type.is_ref = vd.isRef() != null;
          ft.arg_types.Add(type);
        }
      }
      ft.Update();
    }
  }

  public override IParseTree GetDefaultArgsExprAt(int idx) 
  { 
    if(fparams == null)
      return null; 

    var vdecl = fparams.funcParamDeclare()[idx];
    var vinit = vdecl.assignExp(); 
    return vinit;
  }
#endif

  public FuncSymbolScript(TypeSystem ts, AST_FuncDecl decl, TypeRef ret_type = null)
    : base(decl.name, new FuncType(ret_type == null ? ts.Type(decl.type) : ret_type))
  {
    this.decl = decl;
  }

  public override int GetTotalArgsNum() { return decl.GetTotalArgsNum(); }
  public override int GetDefaultArgsNum() { return decl.GetDefaultArgsNum(); }
}

public class FuncSymbolNative : FuncSymbol
{
  public delegate ICoroutine Cb(VM.Frame frm, FuncArgsInfo args_info, ref BHS status); 
  public Cb cb;

  int def_args_num;

  public FuncSymbolNative(
    string name, 
    TypeRef ret_type, 
    Cb cb
  ) 
    : this(name, ret_type, 0, cb)
  {}

  public FuncSymbolNative(
    string name, 
    TypeRef ret_type, 
    int def_args_num,
    Cb cb
  ) 
    : base(name, new FuncType(ret_type))
  {
    this.cb = cb;
    this.def_args_num = def_args_num;
  }

  public override int GetTotalArgsNum() { return GetMembers().Count; }
  public override int GetDefaultArgsNum() { return def_args_num; }

  public override void Define(Symbol sym) 
  {
    base.Define(sym);

    if(sym is FuncArgSymbol)
    {
      DefineArg(sym.name);

      var ft = GetFuncType();
      ft.arg_types.Add(sym.type);
      ft.Update();
    }
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
      throw new UserError("Operator overload must have exactly one argument");

    if(s.GetReturnType() == TypeSystem.Void)
      throw new UserError("Operator overload return value can't be void");

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
    this.type = new TypeRef(this);
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
    : base(name, new TypeRef(owner)) 
  {
    this.owner = owner;
    this.val = val;
  }
  public string GetName() { return owner.name; }
}

public class TypeSystem
{
  static public BuiltInTypeSymbol Bool = new BuiltInTypeSymbol("bool");
  static public BuiltInTypeSymbol String = new BuiltInTypeSymbol("string");
  static public BuiltInTypeSymbol Int = new BuiltInTypeSymbol("int");
  static public BuiltInTypeSymbol Float = new BuiltInTypeSymbol("float");
  static public BuiltInTypeSymbol Void = new BuiltInTypeSymbol("void");
  static public BuiltInTypeSymbol Enum = new BuiltInTypeSymbol("enum");
  static public BuiltInTypeSymbol Any = new BuiltInTypeSymbol("any");
  static public BuiltInTypeSymbol Null = new BuiltInTypeSymbol("null");

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

  List<Scope> links = new List<Scope>();

  Dictionary<string, TypeRef> type_cache = new Dictionary<string, TypeRef>();

  public TypeSystem()
  {
    InitBuiltins(globs);
  }

  public void Link(Scope other)
  {
    if(links.Contains(other))
      return;
    links.Add(other);
  }

  void InitBuiltins(GlobalScope globs) 
  {
    globs.Define(Int);
    globs.Define(Float);
    globs.Define(Bool);
    globs.Define(String);
    globs.Define(Void);
    globs.Define(Any);

    //for all generic arrays
    globs.Define(new GenericArrayTypeSymbol(this, new TypeRef(this, "")));

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
        } 
      );
      fn.Define(new FuncArgSymbol("p", Type("void^()")));
      globs.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("stop", Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          var fid = (int)frm.stack.PopRelease().num;
          frm.vm.Stop(fid);
          return null;
        } 
      );
      fn.Define(new FuncArgSymbol("fid", Type("int")));
      globs.Define(fn);
    }
  }

  public SymbolsDictionary GetMembers()
  {
    return globs.GetMembers();
  }

  public Symbol Resolve(string name) 
  {
    var s = globs.Resolve(name);
    if(s != null)
      return s;

    foreach(var lnk in links)
    {
      s = lnk.Resolve(name);
      if(s != null)
        return s;
    }
    return null;
  }

  public void Define(Symbol sym)
  {
    foreach(var lnk in links)
    {
      if(lnk.Resolve(sym.name) != null)
        throw new UserError(sym.Location() + " : already defined symbol '" + sym.name + "'"); 
    }

    globs.Define(sym);
  }

#if BHL_FRONT
  public TypeRef Type(bhlParser.TypeContext parsed)
  {
    var str = parsed.GetText();
    var type = Resolve(str) as IType;

    if(type == null && parsed != null)
    {    
      if(parsed.fnargs() != null)
        type = GetFuncType(parsed);

      //NOTE: if array type was not explicitely defined we fallback to GenericArrayTypeSymbol
      if(parsed.ARR() != null)
      {
        //checking if it's an array of func ptrs
        if(type != null)
          type = new GenericArrayTypeSymbol(this, new TypeRef(type));
        else
          type = new GenericArrayTypeSymbol(this, Type(parsed.NAME().GetText()));
      }
    }

    var tr = type != null ? new TypeRef(type) : new TypeRef(this, str);
    tr.parsed = parsed;
    return tr;
  }

  public TypeRef Type(bhlParser.RetTypeContext parsed)
  {
    var str = parsed == null ? "void" : parsed.GetText();
    var type = Resolve(str) as IType;

    if(type == null && parsed != null)
    {    
      if(parsed.type().Length > 1)
      {
        var mtype = new MultiType();
        for(int i=0;i<parsed.type().Length;++i)
          mtype.items.Add(this.Type(parsed.type()[i]));
        mtype.Update();
        type = mtype;
      }
      else
        return this.Type(parsed.type()[0]);
    }

    var tr = type != null ? new TypeRef(type) : new TypeRef(this, str);
    tr.parsed = parsed;
    return tr;
  }
#endif

  public TypeRef Type(string name)
  {
    if(name.Length == 0)
      throw new Exception("Bad type: '" + name + "'");

    TypeRef tr;
    if(type_cache.TryGetValue(name, out tr))
      return tr;
    
    //let's check if the type was already explicitely defined
    var t = Resolve(name) as IType;
    if(t != null)
    {
      tr = new TypeRef(t);
    }
    else
    {
#if BHL_FRONT
      if(TypeSystem.IsCompoundType(name))
      {
        var node = Frontend.ParseType(name);
        if(node == null)
          throw new Exception("Bad type: '" + name + "'");

        if(node.type().Length == 1)
          tr = this.Type(node.type()[0]);
        else
          tr = this.Type(node);
      }
      else
#endif
        tr = new TypeRef(this, name);
    }

    type_cache.Add(name, tr);
    
    return tr;
  }

  public TypeRef Type(IType t)
  {
    return new TypeRef(t);
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
           (dst is FuncType && src == Null) || 
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
  public FuncType GetFuncType(bhlParser.TypeContext ctx)
  {
    var fnargs = ctx.fnargs();

    string ret_type_str = ctx.NAME().GetText();
    if(fnargs.ARR() != null)
      ret_type_str += "[]";
    
    var ret_type = Type(ret_type_str);

    var arg_types = new List<TypeRef>();
    var fnames = fnargs.names();
    if(fnames != null)
    {
      for(int i=0;i<fnames.refName().Length;++i)
      {
        var name = fnames.refName()[i];
        var arg_type = Type(name.NAME().GetText());
        arg_type.is_ref = name.isRef() != null; 
        arg_types.Add(arg_type);
      }
    }

    return new FuncType(ret_type, arg_types);
  }

  static public IType MatchTypes(Dictionary<Tuple<IType, IType>, IType> table, WrappedParseTree a, WrappedParseTree b) 
  {
    IType result;
    if(!table.TryGetValue(new Tuple<IType, IType>(a.eval_type, b.eval_type), out result))
    {
      throw new UserError(
        a.Location()+" : incompatible types"
      );
    }
    return result;
  }

  public void CheckAssign(WrappedParseTree lhs, WrappedParseTree rhs) 
  {
    IType promote_to_type = null;
    promote_from_to.TryGetValue(new Tuple<IType, IType>(rhs.eval_type, lhs.eval_type), out promote_to_type);
    if(!CanAssignTo(rhs.eval_type, lhs.eval_type, promote_to_type)) 
    {
      throw new UserError(
        lhs.Location()+" : incompatible types"
      );
    }
  }

  public void CheckAssign(IType lhs, WrappedParseTree rhs) 
  {
    IType promote_to_type = null;
    promote_from_to.TryGetValue(new Tuple<IType, IType>(rhs.eval_type, lhs), out promote_to_type);
    if(!CanAssignTo(rhs.eval_type, lhs, promote_to_type)) 
    {
      throw new UserError(
        rhs.Location()+" : incompatible types"
      );
    }
  }

  public void CheckAssign(WrappedParseTree lhs, IType rhs) 
  {
    IType promote_to_type = null;
    promote_from_to.TryGetValue(new Tuple<IType, IType>(rhs, lhs.eval_type), out promote_to_type);
    if(!CanAssignTo(rhs, lhs.eval_type, promote_to_type)) 
    {
      throw new UserError(
        lhs.Location()+" : incompatible types"
      );
    }
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
    
    throw new UserError(
      type.Location()+" : incompatible types for casting"
    );
  }

  public IType CheckBinOp(WrappedParseTree a, WrappedParseTree b) 
  {
    if(!IsBinOpCompatible(a.eval_type))
      throw new UserError(
        a.Location()+" operator is not overloaded"
      );

    if(!IsBinOpCompatible(b.eval_type))
      throw new UserError(
        b.Location()+" operator is not overloaded"
      );

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
      throw new UserError(
        a.Location()+" : operator is not overloaded"
      );

    if(!IsRtlOpCompatible(b.eval_type))
      throw new UserError(
        b.Location()+" : operator is not overloaded"
      );

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
    if(((a.eval_type is ClassSymbol || a.eval_type is FuncType) && b.eval_type == Null) ||
        ((b.eval_type is ClassSymbol || b.eval_type is FuncType) && a.eval_type == Null))
      return Bool;

    MatchTypes(eq_op_res_type, a, b);

    return Bool;
  }

  public IType CheckUnaryMinus(WrappedParseTree a) 
  {
    if(!(a.eval_type == Int || a.eval_type == Float)) 
      throw new UserError(a.Location()+" : must be numeric type");

    return a.eval_type;
  }

  public IType CheckBitOp(WrappedParseTree a, WrappedParseTree b) 
  {
    if(a.eval_type != Int) 
      throw new UserError(a.Location()+" : must be int type");

    if(b.eval_type != Int)
      throw new UserError(b.Location()+" : must be int type");

    return Int;
  }

  public IType CheckLogicalOp(WrappedParseTree a, WrappedParseTree b) 
  {
    if(a.eval_type != Bool) 
      throw new UserError(a.Location()+" : must be bool type");

    if(b.eval_type != Bool)
      throw new UserError(b.Location()+" : must be bool type");

    return Bool;
  }

  public IType CheckLogicalNot(WrappedParseTree a) 
  {
    if(a.eval_type != Bool) 
      throw new UserError(a.Location()+" : must be bool type");

    return a.eval_type;
  }
#endif
}

public class SymbolsDictionary
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
}

} //namespace bhl
