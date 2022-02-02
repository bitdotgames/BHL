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
  //TODO: get rid of this rigid method
  int GetTypeIndex();
}

public class TypeRef
{
  public IType type;
  public bool is_ref;
  public string name;
#if BHL_FRONT
  //NOTE: parse location of the type
  public IParseTree parsed;
#endif

  public TypeRef(string name)
  {
    this.name = name;
    this.type = null;
    this.is_ref = false;
  }

  public TypeRef(IType type)
  {
    this.name = type.GetName();
    this.type = type;
    this.is_ref = false;
  }

  public TypeRef(IType type, string name)
  {
    this.type = type;
    this.name = name;
    this.is_ref = false;
  }

  public bool IsEmpty()
  {
    return type == null && string.IsNullOrEmpty(name);
  }

  public IType Get(IScope symbols)
  {
    if(type != null)
      return type;

    if(string.IsNullOrEmpty(name))
      return null;

    type = (bhl.IType)symbols.Resolve(name);

    return type;
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
  public int type_index;

  public BuiltInTypeSymbol(string name, int type_index) 
    : base(name, null/*set below*/) 
  {
    this.type = new TypeRef(this);
    this.type_index = type_index;
  }

  public string GetName() { return name; }
  public int GetTypeIndex() { return type_index; }
}

public class ClassSymbol : EnclosingSymbol, IScope, IType 
{
  internal ClassSymbol super_class;

  public SymbolsDictionary members = new SymbolsDictionary();

  public VM.ClassCreator creator;

#if BHL_FRONT
  public ClassSymbol(
    IScope origin, 
    WrappedParseTree parsed, 
    string name, 
    ClassSymbol super_class, 
    VM.ClassCreator creator = null
  )
    : this(origin, name, super_class, creator)
  {
    this.parsed = parsed;
  }
#endif

  public ClassSymbol(
    IScope origin, 
    string name, 
    ClassSymbol super_class, 
    VM.ClassCreator creator = null
  )
    : base(origin, name)
  {
    this.type = new TypeRef(this);
    this.super_class = super_class;
    this.creator = creator;

    //NOTE: this looks at the moment a bit like a hack:
    //      We define parent members in the current class
    //      scope as well. We do this since we want to  
    //      address members in VM simply by numeric integer
    if(super_class != null)
    {
      for(int i=0;i<super_class.GetMembers().Count;++i)
      {
        var sym = super_class.GetMembers()[i];
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
    return super_class == null ? origin : super_class;
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

    if(sym is VariableSymbol vs)
      vs.scope_idx = members.FindStringKeyIndex(sym.name);
  }

  public string GetName() { return name; }
  public int GetTypeIndex() { return TypeSystem.TIDX_USER; }

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

  //NOTE: indices below are synchronized with an actual order 
  //      of class members initialized later
  public const int IDX_Add        = 0;
  public const int IDX_At         = 1;
  public const int IDX_SetAt      = 2;
  public const int IDX_RemoveAt   = 3;
  public const int IDX_Clear      = 4;
  public const int IDX_Count      = 5;
  public const int IDX_AddInplace = 6;

  public ArrayTypeSymbol(Scope origin, string name, TypeRef item_type)     
    : base(origin, name, super_class: null)
  {
    this.item_type = item_type;

    this.creator = CreateArr;

    {
      var fn = new FuncSymbolNative("Add", origin.Type("void"), Add);
      fn.Define(new FuncArgSymbol("o", item_type));
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("At", item_type, At);
      fn.Define(new FuncArgSymbol("idx", origin.Type("int")));
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("SetAt", item_type, SetAt);
      fn.Define(new FuncArgSymbol("idx", origin.Type("int")));
      fn.Define(new FuncArgSymbol("o", item_type));
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("RemoveAt", origin.Type("void"), RemoveAt);
      fn.Define(new FuncArgSymbol("idx", origin.Type("int")));
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("Clear", origin.Type("void"), Clear);
      this.Define(fn);
    }

    {
      var vs = new FieldSymbol("Count", origin.Type("int"), GetCount, null);
      this.Define(vs);
    }

    {
      var fn = new FuncSymbolNative("$AddInplace", origin.Type("void"), AddInplace);
      fn.Define(new FuncArgSymbol("o", item_type));
      this.Define(fn);
    }
  }

  public ArrayTypeSymbol(Scope scope, TypeRef item_type) 
    : this(scope, item_type.name + "[]", item_type)
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

  public GenericArrayTypeSymbol(Scope origin, TypeRef item_type) 
    : base(origin, item_type)
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

  public ArrayTypeSymbolT(Scope scope, string name, TypeRef item_type, CreatorCb creator) 
    : base(scope, name, item_type)
  {
    Creator = creator;
  }

  public ArrayTypeSymbolT(Scope scope, TypeRef item_type, CreatorCb creator) 
    : base(scope, item_type.name + "[]", item_type)
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
    var res = Val.NewObj(frame.vm, lst[idx]);
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

public class VariableSymbol : Symbol 
{
  //if variable is global in a module it stores its id
  public uint module_id;
  //index of variable in a scope
  public int scope_idx = -1;

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

  public void CalcVariableScopeIdx(IScope scope)
  {
    //let's ignore already assigned ones
    if(scope_idx != -1)
      return;
    int c = 0;
    for(int i=0;i<scope.GetMembers().Count;++i)
      if(scope.GetMembers()[i] is VariableSymbol)
        ++c;
    scope_idx = c; 
  }
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
  public int idx;

  public FieldSymbolScript(string name, string type, int idx = -1) 
    : base(name, new TypeRef(type), null, null, null)
  {
    this.idx = idx;
    this.getter = Getter;
    this.setter = Setter;
    this.getref = Getref;
  }

  void Getter(Val ctx, ref Val v)
  {
    var m = (IList<Val>)ctx.obj;
    v.ValueCopyFrom(m[idx]);
  }

  void Setter(ref Val ctx, Val v)
  {
    var m = (IList<Val>)ctx.obj;
    var curr = m[idx];
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
    v = m[idx];
  }
}

public abstract class EnclosingSymbol : Symbol, IScope 
{
  protected IScope origin;

  abstract public SymbolsDictionary GetMembers();

#if BHL_FRONT
  public EnclosingSymbol(IScope origin, WrappedParseTree parsed, string name) 
    : this(origin, name)
  {
    this.parsed = parsed;
  }
#endif

  //TODO: why passing origin if it can be set once 
  //      the symbol is actually defined in a scope?
  public EnclosingSymbol(IScope origin, string name) 
    : base(name, type: null)
  {
    this.origin = origin;
  }

  public virtual IScope GetFallbackScope() { return origin; }

  public virtual Symbol Resolve(string name) 
  {
    Symbol s = null;
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

    var fback = GetFallbackScope();
    if(fback != null)
      return fback.Resolve(name);

    return null;
  }

  public virtual void Define(Symbol sym) 
  {
    var members = GetMembers(); 
    if(members.Contains(sym.name))
      throw new UserError(sym.Location() + ": already defined symbol '" + sym.name + "'"); 

    members.Add(sym);
    sym.scope = this; // track the scope in each symbol
  }
}

public class MultiType : IType
{
  public string name;

  public List<TypeRef> items = new List<TypeRef>();

  public int GetTypeIndex() { return TypeSystem.TIDX_USER; }
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

  public int GetTypeIndex() { return TypeSystem.TIDX_USER; }
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

public class FuncSymbol : EnclosingSymbol
{
  SymbolsDictionary members = new SymbolsDictionary();
  SymbolsDictionary args = new SymbolsDictionary();

#if BHL_FRONT
  public bool return_statement_found = false;

  public FuncSymbol(WrappedParseTree parsed, IScope origin, string name, FuncType type) 
    : this(origin, name, type)
  {
    this.parsed = parsed;
  }

  public virtual IParseTree GetDefaultArgsExprAt(int idx) { return null; }

#endif

  public FuncSymbol(IScope origin, string name, FuncType type) 
    : base(origin, name)
  {
    this.type = new TypeRef(type);
  }

  public override SymbolsDictionary GetMembers() { return members; }

  public FuncType GetFuncType()
  {
    return (FuncType)this.type.Get(this);
  }

  public IType GetReturnType()
  {
    return GetFuncType().ret_type.Get(this);
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

  public override void Define(Symbol sym)
  {
    if(sym is VariableSymbol vs)
      vs.CalcVariableScopeIdx(this);
    base.Define(sym);
  }
}

#if BHL_FRONT
public class LambdaSymbol : FuncSymbol
{
  public AST_LambdaDecl decl;

  List<FuncSymbol> fdecl_stack;

  public LambdaSymbol(
    WrappedParseTree parsed, 
    bhlParser.FuncLambdaContext lmb_ctx,
    Scope origin,
    AST_LambdaDecl decl, 
    TypeRef ret_type,
    List<FuncSymbol> fdecl_stack
  ) 
    : base(parsed, origin, decl.name, new FuncType(ret_type))
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
        ft.arg_types.Add(origin.Type(vd.type()));
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
    Scope origin,
    AST_FuncDecl decl, 
    WrappedParseTree parsed, 
    TypeRef ret_type, 
    bhlParser.FuncParamsContext fparams
  ) 
    : this(origin, decl, ret_type)
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
          var type = origin.Type(vd.type());
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

  public FuncSymbolScript(IScope origin, AST_FuncDecl decl, TypeRef ret_type = null)
    : base(origin, decl.name, new FuncType(ret_type == null ? new TypeRef(decl.type) : ret_type))
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
    : base(null, name, new FuncType(ret_type))
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
    : base(null, name, super_class, creator)
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
    : base(null, name, super_class)
  {
    this.decl = decl;
    this.creator = ClassCreator;
  }

  void ClassCreator(VM.Frame frm, ref Val res)
  {
    IList<Val> vl = null;
    if(super_class != null)
    {
      super_class.creator(frm, ref res);
      vl = (IList<Val>)res.obj;
    }
    else
    {
      vl = ValList.New(res.vm);
      res.SetObj(vl);
    }
    //TODO: this should be more robust
    //NOTE: storing class name hash in _num attribute
    res._num = Hash.CRC28(decl.name); 

    for(int i=0;i<members.Count;++i)
    {
      var m = members[i];
      var v = Val.New(res.vm);
      //NOTE: proper default init of built-in types
      if(m.type.name == TypeSystem.Float.type.name || 
         m.type.name == TypeSystem.Int.type.name)
        v.SetNum(0);
      else if(m.type.name == TypeSystem.String.type.name)
        v.SetStr("");
      else if(m.type.name == TypeSystem.Bool.type.name)
        v.SetBool(false);
      else 
      {
        if(m.type.Get(frm.vm.Symbols) is EnumSymbol)
          v.SetNum(0);
        else
          v.SetObj(null);
      }

      vl.Add(v);
      v.Release();
    }
  }
}

public class EnumSymbol : EnclosingSymbol, IType
{
  public SymbolsDictionary members = new SymbolsDictionary();

#if BHL_FRONT
  public EnumSymbol(IScope origin, WrappedParseTree parsed, string name)
    : this(origin, name)
  {
    this.parsed = parsed;
  }
#endif

  public EnumSymbol(IScope origin, string name)
      : base(origin, name)
  {
    this.type = new TypeRef(this);
  }

  public string GetName() { return name; }
  public int GetTypeIndex() { return TypeSystem.TIDX_ENUM; }

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
  public EnumSymbolScript(IScope origin, string name)
    : base(origin, name)
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
  public int GetTypeIndex() { return owner.GetTypeIndex(); }
}

static public class TypeSystem
{
  // arithmetic types defined in order from narrowest to widest
  public const int TIDX_USER      = 0; // user-defined type
  public const int TIDX_BOOLEAN   = 1;
  public const int TIDX_STRING    = 2;
  public const int TIDX_INT       = 3;
  public const int TIDX_FLOAT     = 4;
  public const int TIDX_VOID      = 5;
  public const int TIDX_ENUM      = 6;
  public const int TIDX_ANY       = 7;

  static public BuiltInTypeSymbol Bool = new BuiltInTypeSymbol("bool", TIDX_BOOLEAN);
  static public BuiltInTypeSymbol String = new BuiltInTypeSymbol("string", TIDX_STRING);
  static public BuiltInTypeSymbol Int = new BuiltInTypeSymbol("int", TIDX_INT);
  static public BuiltInTypeSymbol Float = new BuiltInTypeSymbol("float", TIDX_FLOAT);
  static public BuiltInTypeSymbol Void = new BuiltInTypeSymbol("void", TIDX_VOID);
  static public BuiltInTypeSymbol Enum = new BuiltInTypeSymbol("enum", TIDX_ENUM);
  static public BuiltInTypeSymbol Any = new BuiltInTypeSymbol("any", TIDX_ANY);
  static public BuiltInTypeSymbol Null = new BuiltInTypeSymbol("null", TIDX_USER);

  // Arithmetic types defined in order from narrowest to widest
  public static IType[] index2type = new IType[] {
      // 0,  1,   2,      3,   4,     5,     6     7
      null, Bool, String, Int, Float, Void,  Enum, Any
  };

  // Map t1 op t2 to result type (_void implies illegal)
  public static IType[,] arithmetic_res_type = new IType[,] {
      /*          struct bool    string  int    float   void   enum   any */
      /*struct*/  {Void, Void,   Void,   Void,  Void,   Void,  Void,  Void},
      /*bool*/    {Void, Void,   Void,   Void,  Void,   Void,  Void,  Void},
      /*string*/  {Void, Void,   String, Void,  Void,   Void,  Void,  Void},
      /*int*/     {Void, Void,   Void,   Int,   Float,  Void,  Void,  Void},
      /*float*/   {Void, Void,   Void,   Float, Float,  Void,  Void,  Void},
      /*void*/    {Void, Void,   Void,   Void,  Void,   Void,  Void,  Void},
      /*enum*/    {Void, Void,   Void,   Void,  Void,   Void,  Void,  Void},
      /*any*/     {Void, Void,   Void,   Void,  Void,   Void,  Void,  Void}
  };

  public static IType[,] relational_res_type = new IType[,] {
      /*          struct bool   string   int     float     void    enum    any */
      /*struct*/  {Void, Void,  Void,    Void,    Void,    Void,  Void,  Void},
      /*bool*/    {Void, Void,  Void,    Void,    Void,    Void,  Void,  Void},
      /*string*/  {Void, Void,  Bool,    Void,    Void,    Void,  Void,  Void},
      /*int*/     {Void, Void,  Void,    Bool,    Bool,    Void,  Void,  Void},
      /*float*/   {Void, Void,  Void,    Bool,    Bool,    Void,  Void,  Void},
      /*void*/    {Void, Void,  Void,    Void,    Void,    Void,  Void,  Void},
      /*enum*/    {Void, Void,  Void,    Void,    Void,    Void,  Void,  Void},
      /*any*/     {Void, Void,  Void,    Void,    Void,    Void,  Void,  Void}
  };

  public static IType[,] equality_res_type = new IType[,] {
      /*          struct   bool     string   int      float    void   enum   any */
      /*struct*/  {Bool,   Void,    Void,    Void,    Void,    Void,  Void,  Void},
      /*bool*/    {Void,   Bool,    Void,    Void,    Void,    Void,  Void,  Void},
      /*string*/  {Void,   Void,    Bool,    Void,    Void,    Void,  Void,  Void},
      /*int*/     {Void,   Void,    Void,    Bool,    Bool,    Void,  Void,  Void},
      /*float*/   {Void,   Void,    Void,    Bool,    Bool,    Void,  Void,  Void},
      /*void*/    {Void,   Void,    Void,    Void,    Void,    Void,  Void,  Void},
      /*enum*/    {Void,   Void,    Void,    Void,    Void,    Void,  Void,  Void},
      /*any*/     {Bool,   Void,    Void,    Void,    Void,    Void,  Void,  Void}
  };

  // Indicate whether a type needs a promotion to a wider type.
  //  If not null, implies promotion required.  Null does NOT imply
  //  error--it implies no promotion.  This works for
  //  arithmetic, equality, and relational operators in bhl
  // 
  public static IType[,] promote_from_to = new IType[,] {
      /*          struct  bool     string  int     float    void   enum   any*/
      /*struct*/  {null,  null,    null,   null,   null,    null,  null,  null},
      /*bool*/    {null,  null,    null,   null,   null,    null,  null,  null},
      /*string*/  {null,  null,    null,   null,   null,    null,  null,  null},
      /*int*/     {null,  null,    null,   null,   Float,  null,  null,  null},
      /*float*/   {null,  null,    null,   null,    null,   null,  null,  null},
      /*void*/    {null,  null,    null,   null,    null,   null,  null,  null},
      /*enum*/    {null,  null,    null,   null,    null,   null,  null,  null},
      /*any*/     {null,  null,    null,   null,    null,   null,  null,  null}
  };

  public static IType[,] cast_from_to = new IType[,] {
      /*          struct  bool     string   int     float   void   enum   any*/
      /*struct*/  {null,  null,    null,    null,   null,   null,  null,  Any},
      /*bool*/    {null,  null,    String,  Int,    Float,  null,  null,  Any},
      /*string*/  {null,  null,    String,  null,   null,   null,  null,  Any},
      /*int*/     {null,  Bool,    String,  Int,    Float,  null,  null,  Any},
      /*float*/   {null,  Bool,    String,  Int,    Float,  null,  Enum,  Any},
      /*void*/    {null,  null,    null,    null,   null,   null,  null,  null},
      /*enum*/    {null,  null,    String,  Int,    Float,  null,  null,  Any},
      /*any*/     {null,  Bool,    String,  Int,    null,   null,  null,  Any}
  };

  static public GlobalScope CreateBuiltins()
  {
    var globals = new GlobalScope();
    InitBuiltins(globals);
    return globals;
  }

  static public void InitBuiltins(GlobalScope globals) 
  {
    foreach(IType t in index2type) 
    {
      if(t != null) 
      {
        var blt = (BuiltInTypeSymbol)t; 
        globals.Define(blt);
      }
    }

    //for all generic arrays
    globals.Define(new GenericArrayTypeSymbol(globals, new TypeRef("")));

    {
      var fn = new FuncSymbolNative("suspend", globals.Type("void"), 
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          return CoroutineSuspend.Instance;
        } 
      );
      globals.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("yield", globals.Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          return CoroutinePool.New<CoroutineYield>(frm.vm);
        } 
      );
      globals.Define(fn);
    }

    //TODO: this one is controversary, it's defined for BC for now
    {
      var fn = new FuncSymbolNative("fail", globals.Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          status = BHS.FAILURE;
          return null;
        } 
      );
      globals.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("start", globals.Type("int"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          var val_ptr = frm.stack.Pop();
          int id = frm.vm.Start((VM.FuncPtr)val_ptr._obj, frm).id;
          val_ptr.Release();
          frm.stack.Push(Val.NewNum(frm.vm, id));
          return null;
        } 
      );
      fn.Define(new FuncArgSymbol("p", globals.Type("void^()")));
      globals.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("stop", globals.Type("void"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          var fid = (int)frm.stack.PopRelease().num;
          frm.vm.Stop(fid);
          return null;
        } 
      );
      fn.Define(new FuncArgSymbol("fid", globals.Type("int")));
      globals.Define(fn);
    }
  }

  static bool IsBuiltin(Symbol s)
  {
    return ((s is BuiltInTypeSymbol) || 
            (s is ArrayTypeSymbol)
           );
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
  static public FuncType GetFuncType(Scope scope, bhlParser.TypeContext ctx)
  {
    var fnargs = ctx.fnargs();

    string ret_type_str = ctx.NAME().GetText();
    if(fnargs.ARR() != null)
      ret_type_str += "[]";
    
    var ret_type = scope.Type(ret_type_str);

    var arg_types = new List<TypeRef>();
    var fnames = fnargs.names();
    if(fnames != null)
    {
      for(int i=0;i<fnames.refName().Length;++i)
      {
        var name = fnames.refName()[i];
        var arg_type = scope.Type(name.NAME().GetText());
        arg_type.is_ref = name.isRef() != null; 
        arg_types.Add(arg_type);
      }
    }

    return new FuncType(ret_type, arg_types);
  }

  static public IType GetResultType(IType[,] table, WrappedParseTree a, WrappedParseTree b) 
  {
    if(a.eval_type == b.eval_type)
      return a.eval_type;

    int ta = a.eval_type.GetTypeIndex(); // type index of left operand
    int tb = b.eval_type.GetTypeIndex(); // type index of right operand

    IType result = table[ta,tb];    // operation result type
    if(result == Void) 
    {
      throw new UserError(
        a.Location()+" : incompatible types"
      );
    }
    return result;
  }

  static public void CheckAssign(WrappedParseTree lhs, WrappedParseTree rhs) 
  {
    int tlhs = lhs.eval_type.GetTypeIndex(); // promote right to left type?
    int trhs = rhs.eval_type.GetTypeIndex();
    var promote_to_type = promote_from_to[trhs,tlhs];
    if(!CanAssignTo(rhs.eval_type, lhs.eval_type, promote_to_type)) 
    {
      throw new UserError(
        lhs.Location()+" : incompatible types"
      );
    }
  }

  static public void CheckAssign(IType lhs, WrappedParseTree rhs) 
  {
    int tlhs = lhs.GetTypeIndex(); // promote right to left type?
    int trhs = rhs.eval_type.GetTypeIndex();
    var promote_to_type = promote_from_to[trhs,tlhs];
    if(!CanAssignTo(rhs.eval_type, lhs, promote_to_type)) 
    {
      throw new UserError(
        rhs.Location()+" : incompatible types"
      );
    }
  }

  static public void CheckAssign(WrappedParseTree lhs, IType rhs) 
  {
    int tlhs = lhs.eval_type.GetTypeIndex(); // promote right to left type?
    int trhs = rhs.GetTypeIndex();
    var promote_to_type = promote_from_to[trhs,tlhs];
    if(!CanAssignTo(rhs, lhs.eval_type, promote_to_type)) 
    {
      throw new UserError(
        lhs.Location()+" : incompatible types"
      );
    }
  }

  static public void CheckCast(WrappedParseTree type, WrappedParseTree exp) 
  {
    int tlhs = type.eval_type.GetTypeIndex();
    int trhs = exp.eval_type.GetTypeIndex();

    //special case: we allow to cast from 'any' to any user type
    if(trhs == TIDX_ANY && tlhs == TIDX_USER)
      return;

    //special case: we allow to cast from 'any' numeric type to enum
    if((trhs == TIDX_FLOAT || 
        trhs == TIDX_INT) && 
        tlhs == TIDX_ENUM)
      return;

    var cast_type = cast_from_to[trhs,tlhs];

    if(cast_type == type.eval_type)
      return;

    if(IsInSameClassHierarchy(exp.eval_type, type.eval_type))
      return;
    
    throw new UserError(
      type.Location()+" : incompatible types for casting"
    );
  }

  static public IType TypeForBinOp(WrappedParseTree a, WrappedParseTree b) 
  {
    if(!IsBinOpCompatible(a.eval_type))
      throw new UserError(
        a.Location()+" operator is not overloaded"
      );

    if(!IsBinOpCompatible(b.eval_type))
      throw new UserError(
        b.Location()+" operator is not overloaded"
      );

    return GetResultType(arithmetic_res_type, a, b);
  }

  static public IType TypeForBinOpOverload(IScope scope, WrappedParseTree a, WrappedParseTree b, FuncSymbol op_func) 
  {
    var op_func_arg = op_func.GetArgs()[0];
    CheckAssign(op_func_arg.type.Get(scope), b);
    return op_func.GetReturnType();
  }

  static public IType TypeForRtlOp(WrappedParseTree a, WrappedParseTree b) 
  {
    if(!IsRtlOpCompatible(a.eval_type))
      throw new UserError(
        a.Location()+" : operator is not overloaded"
      );

    if(!IsRtlOpCompatible(b.eval_type))
      throw new UserError(
        b.Location()+" : operator is not overloaded"
      );

    GetResultType(relational_res_type, a, b);
    // even if the operands are incompatible, the type of
    // this operation must be boolean
    return Bool;
  }

  static public IType TypeForEqOp(WrappedParseTree a, WrappedParseTree b) 
  {
    GetResultType(equality_res_type, a, b);
    return Bool;
  }

  static public IType TypeForUnaryMinus(WrappedParseTree a) 
  {
    if(!(a.eval_type == Int || a.eval_type == Float)) 
      throw new UserError(a.Location()+" : must be numeric type");

    return a.eval_type;
  }

  static public IType TypeForBitOp(WrappedParseTree a, WrappedParseTree b) 
  {
    if(a.eval_type != Int) 
      throw new UserError(a.Location()+" : must be int type");

    if(b.eval_type != Int)
      throw new UserError(b.Location()+" : must be int type");

    return Int;
  }

  static public IType TypeForLogicalOp(WrappedParseTree a, WrappedParseTree b) 
  {
    if(a.eval_type != Bool) 
      throw new UserError(a.Location()+" : must be bool type");

    if(b.eval_type != Bool)
      throw new UserError(b.Location()+" : must be bool type");

    return Bool;
  }

  static public IType TypeForLogicalNot(WrappedParseTree a) 
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
