using System;
using System.Collections;
using System.Collections.Generic;

#if BHL_FRONT
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
#endif

namespace bhl {

public interface Type 
{
  string GetName();
  int GetTypeIndex();
}

public class TypeRef
{
  public Type type;
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

  public TypeRef(Type type)
  {
    this.name = type.GetName();
    this.type = type;
    this.is_ref = false;
  }

  public TypeRef(Type type, string name)
  {
    this.type = type;
    this.name = name;
    this.is_ref = false;
  }

  public bool IsEmpty()
  {
    return type == null && string.IsNullOrEmpty(name);
  }

  public Type Get(Scope symbols)
  {
    if(type != null)
      return type;

    if(string.IsNullOrEmpty(name))
      return null;

    type = (bhl.Type)symbols.Resolve(name);

    return type;
  }
}

public class ParserWrappedNode
{
#if BHL_FRONT
  public IParseTree tree;
  public ITokenStream tokens;
  public Frontend builder;
#endif

  public Scope scope;
  public Symbol symbol;
  public Type eval_type;
  //TODO: why keep it?
  public Type promote_to_type;

#if BHL_FRONT
  public string Location()
  {
    var interval = tree.SourceInterval;
    var begin = tokens.Get(interval.a);

    string line = string.Format("@({0},{1})", begin.Line, begin.Column);
    return line;
  }

#else
  public string Location()
  {
    return "";
  }

  public string LocationAfter()
  {
    return "";
  }
#endif
}

public class Symbol 
{
  // All symbols at least have a name
  public string name;
  // Location in parse tree, can be null if it's a native symbol
  public ParserWrappedNode parsed;
  public TypeRef type;
  // All symbols know what scope contains them
  public Scope scope;
  // Symbols also know their 'scope level', 
  // e.g. for  { { int a = 1 } } scope level will be 2
  public int scope_level;
  public bool is_out_of_scope;

  public Symbol(ParserWrappedNode node, string name) 
  { 
    this.parsed = node;
    this.name = name; 
  }

  public Symbol(ParserWrappedNode node, string name, TypeRef type) 
    : this(node, name) 
  { 
    this.type = type; 
  }

  public override string ToString() 
  {
    string s = "";
    if(scope != null) 
      s = scope.GetScopeName() + ".";
    return '<' + s + name + ':' + type + '>';
  }

  public string Location()
  {
    if(parsed != null)
      return parsed.Location();
    return name + "(?)";
  }
}

// A symbol to represent built in types such int, float primitive types
public class BuiltInTypeSymbol : Symbol, Type 
{
  public int type_index;

  public BuiltInTypeSymbol(string name, int type_index) 
    : base(null, name) 
  {
    this.type = new TypeRef(this);
    this.type_index = type_index;
  }

  public string GetName() { return name; }
  public int GetTypeIndex() { return type_index; }
}

public class ClassSymbol : ScopedSymbol, Scope, Type 
{
  internal ClassSymbol super_class;

  public SymbolsDictionary members = new SymbolsDictionary();

  public VM.ClassCreator creator;

  public ClassSymbol(
    ParserWrappedNode n, 
    string name, 
    ClassSymbol super_class, 
    Scope enclosing_scope, 
    VM.ClassCreator creator = null
  )
    : base(n, name, enclosing_scope)
  {
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

  public override Scope GetFallbackScope() 
  {
    if(super_class == null) 
      return this.enclosing_scope; // globals

    return super_class;
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
  public int GetTypeIndex() { return SymbolTable.TIDX_USER; }

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

  public ArrayTypeSymbol(BaseScope scope, string name, TypeRef item_type)     
    : base(null, name, null, null)
  {
    this.item_type = item_type;

    this.creator = CreateArr;

    {
      var fn = new FuncSymbolNative("Add", scope.Type("void"), Add);
      fn.Define(new FuncArgSymbol("o", item_type));
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("At", item_type, At);
      fn.Define(new FuncArgSymbol("idx", scope.Type("int")));
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("SetAt", item_type, SetAt);
      fn.Define(new FuncArgSymbol("idx", scope.Type("int")));
      fn.Define(new FuncArgSymbol("o", item_type));
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("RemoveAt", scope.Type("void"), RemoveAt);
      fn.Define(new FuncArgSymbol("idx", scope.Type("int")));
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("Clear", scope.Type("void"), Clear);
      this.Define(fn);
    }

    {
      var vs = new FieldSymbol("Count", scope.Type("int"), GetCount, null);
      this.Define(vs);
    }

    {
      var fn = new FuncSymbolNative("$AddInplace", scope.Type("void"), AddInplace);
      fn.Define(new FuncArgSymbol("o", item_type));
      this.Define(fn);
    }
  }

  public ArrayTypeSymbol(BaseScope scope, TypeRef item_type) 
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
//      compilation phase in BaseScope.Type(..) method
//     
public class GenericArrayTypeSymbol : ArrayTypeSymbol
{
  public static readonly string CLASS_TYPE = "[]";

  public override string Type()
  {
    return CLASS_TYPE;
  }

  public GenericArrayTypeSymbol(BaseScope scope, TypeRef item_type) 
    : base(scope, item_type)
  {}

  public GenericArrayTypeSymbol(BaseScope scope) 
    : base(scope, new TypeRef(""))
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

  public ArrayTypeSymbolT(BaseScope scope, string name, TypeRef item_type, CreatorCb creator) 
    : base(scope, name, item_type)
  {
    Creator = creator;
  }

  public ArrayTypeSymbolT(BaseScope scope, TypeRef item_type, CreatorCb creator) 
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
  public int scope_idx = -1;
  public uint module_id;

  public VariableSymbol(ParserWrappedNode n, string name, TypeRef type) 
    : base(n, name, type) 
  {}

  public void CalcVariableScopeIdx(Scope scope)
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

  public FuncArgSymbol(ParserWrappedNode n, string name, TypeRef type, bool is_ref = false)
    : base(n, name, type)
  {
    this.is_ref = is_ref;
  }

  public FuncArgSymbol(string name, TypeRef type, bool is_ref = false)
    : this(null, name, type, is_ref)
  {}
}

public class FieldSymbol : VariableSymbol
{
  public VM.FieldGetter getter;
  public VM.FieldSetter setter;
  public VM.FieldRef getref;

  public FieldSymbol(string name, TypeRef type, VM.FieldGetter getter = null, VM.FieldSetter setter = null, VM.FieldRef getref = null) 
    : base(null, name, type)
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

public abstract class ScopedSymbol : Symbol, Scope 
{
  protected Scope enclosing_scope;

  public abstract SymbolsDictionary GetMembers();

  public ScopedSymbol(ParserWrappedNode n, string name, TypeRef type, Scope enclosing_scope) 
    : base(n, name, type)
  {
    this.enclosing_scope = enclosing_scope;
  }

  public ScopedSymbol(ParserWrappedNode n, string name, Scope enclosing_scope) 
    : base(n, name)
  {
    this.enclosing_scope = enclosing_scope;
  }

  public virtual Symbol Resolve(string name) 
  {
    Symbol s = null;
    GetMembers().TryGetValue(name, out s);
    if(s != null)
    {
      if(s.is_out_of_scope)
        return null;
      else
        return s;
    }

    var pscope = GetFallbackScope();
    if(pscope != null)
      return pscope.Resolve(name);

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

  public virtual Scope GetFallbackScope() { return GetOriginScope(); }
  public virtual Scope GetOriginScope() { return enclosing_scope; }

  public string GetScopeName() { return name; }
}

public class MultiType : Type
{
  public string name;

  public List<TypeRef> items = new List<TypeRef>();

  public int GetTypeIndex() { return SymbolTable.TIDX_USER; }
  public string GetName() { return name; }

  public void Update()
  {
    string tmp = "";
    //TODO: guess, this can be more optimal?
    for(int i=0;i<items.Count;++i)
    {
      if(i > 0)
        tmp += ",";
      tmp += items[i].name;
    }

    name = tmp;
  }
}

public class FuncType : Type
{
  public string name;

  public TypeRef ret_type;
  public List<TypeRef> arg_types = new List<TypeRef>();

  public int GetTypeIndex() { return SymbolTable.TIDX_USER; }
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

public class FuncSymbol : ScopedSymbol
{
  SymbolsDictionary members = new SymbolsDictionary();
  SymbolsDictionary args = new SymbolsDictionary();

  public uint module_id;

  public bool return_statement_found = false;

  public FuncSymbol(ParserWrappedNode n, string name, FuncType type, Scope enclosing_scope) 
    : base(n, name, new TypeRef(type), enclosing_scope)
  {}

  public override SymbolsDictionary GetMembers() { return members; }

  public FuncType GetFuncType()
  {
    return (FuncType)this.type.Get(this);
  }

  public Type GetReturnType()
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

#if BHL_FRONT
  public virtual IParseTree GetDefaultArgsExprAt(int idx) { return null; }
#endif

  public override void Define(Symbol sym)
  {
    if(sym is VariableSymbol vs)
      vs.CalcVariableScopeIdx(this);
    base.Define(sym);
  }
}

public class LambdaSymbol : FuncSymbol
{
  public AST_LambdaDecl decl;

  List<FuncSymbol> fdecl_stack;

#if BHL_FRONT
  //frontend version
  public LambdaSymbol(
    BaseScope enclosing_scope,
    AST_LambdaDecl decl, 
    List<FuncSymbol> fdecl_stack, 
    ParserWrappedNode n, 
    uint module_id,
    TypeRef ret_type, 
    bhlParser.FuncLambdaContext lmb_ctx
  ) 
    : base(n, decl.name, new FuncType(ret_type), enclosing_scope)
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
        ft.arg_types.Add(enclosing_scope.Type(vd.type()));
      }
    }
    ft.Update();
  }
#endif

  //backend version
  public LambdaSymbol(BaseScope enclosing_scope, AST_LambdaDecl decl) 
    : base(null, decl.name, new FuncType(new TypeRef(decl.type)), enclosing_scope)
  {
    this.decl = decl;
    this.fdecl_stack = null;
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

    var pscope = GetFallbackScope();
    if(pscope != null)
      return pscope.Resolve(name);

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
      if(res is VariableSymbol vs && !res.is_out_of_scope)
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

public class FuncSymbolScript : FuncSymbol
{
  public AST_FuncDecl decl;
#if BHL_FRONT
  //NOTE: storing fparams so it can be accessed later for misc things, e.g. default args
  public bhlParser.FuncParamsContext fparams;
#endif

  //frontend version
#if BHL_FRONT
  public FuncSymbolScript(
    ModuleScope mscope,
    AST_FuncDecl decl, 
    ParserWrappedNode n, 
    TypeRef ret_type, 
    bhlParser.FuncParamsContext fparams
  ) 
    : base(n, decl.name, new FuncType(ret_type), mscope)
  {
    this.decl = decl;
    this.module_id = decl.module_id;
    this.fparams = fparams;

    if(n != null)
    {
      var ft = GetFuncType();
      if(fparams != null)
      {
        for(int i=0;i<fparams.funcParamDeclare().Length;++i)
        {
          var vd = fparams.funcParamDeclare()[i];
          var type = mscope.Type(vd.type());
          type.is_ref = vd.isRef() != null;
          ft.arg_types.Add(type);
        }
      }
      ft.Update();
    }
  }
#endif

  //backend version
  public FuncSymbolScript(Scope enclosing_scope, AST_FuncDecl decl)
    : base(null, decl.name, new FuncType(new TypeRef(decl.type)), enclosing_scope)
  {
    this.decl = decl;
    this.module_id = decl.module_id;
  }

  public override int GetTotalArgsNum() { return decl.GetTotalArgsNum(); }
  public override int GetDefaultArgsNum() { return decl.GetDefaultArgsNum(); }
#if BHL_FRONT
  public override IParseTree GetDefaultArgsExprAt(int idx) 
  { 
    if(fparams == null)
      return null; 

    var vdecl = fparams.funcParamDeclare()[idx];
    var vinit = vdecl.assignExp(); 
    return vinit;
  }
#endif
}

public class FuncSymbolNative : FuncSymbol
{
  public delegate ICoroutine Cb(VM.Frame frm, FuncArgsInfo args_info, ref BHS status); 
  public Cb cb;

  public int def_args_num;

  public FuncSymbolNative(
    string name, 
    TypeRef ret_type, 
    Cb cb = null, 
    int def_args_num = 0
  ) 
    : base(null, name, new FuncType(ret_type), null)
  {
    this.cb = cb;
    this.def_args_num = def_args_num;
  }

  public override int GetTotalArgsNum() { return GetMembers().Count; }
  public override int GetDefaultArgsNum() { return def_args_num; }

  public override void Define(Symbol sym) 
  {
    base.Define(sym);

    //NOTE: for bind funcs every defined symbol is assumed to be an argument
    DefineArg(sym.name);

    var ft = GetFuncType();
    ft.arg_types.Add(sym.type);
    ft.Update();
  }
}

public class ClassSymbolNative : ClassSymbol
{
  public ClassSymbolNative(string name, VM.ClassCreator creator = null)
    : base(null, name, null, null, creator)
  {}

  public ClassSymbolNative(string name, ClassSymbol super_class, VM.ClassCreator creator = null)
    : base(null, name, super_class, null, creator)
  {}

  public void OverloadBinaryOperator(FuncSymbol s)
  {
    if(s.GetArgs().Count != 1)
      throw new UserError("Operator overload must have exactly one argument");

    if(s.GetReturnType() == SymbolTable.symb_void)
      throw new UserError("Operator overload return value can't be void");

    Define(s);
  }
}

public class ClassSymbolScript : ClassSymbol
{
  public AST_ClassDecl decl;

  public ClassSymbolScript(string name, AST_ClassDecl decl, ClassSymbol parent = null)
    : base(null, name, parent, null)
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
      if(m.type.name == SymbolTable.symb_float.type.name || 
         m.type.name == SymbolTable.symb_int.type.name)
        v.SetNum(0);
      else if(m.type.name == SymbolTable.symb_string.type.name)
        v.SetStr("");
      else if(m.type.name == SymbolTable.symb_bool.type.name)
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

public class EnumSymbol : ScopedSymbol, Scope, Type 
{
  public SymbolsDictionary members = new SymbolsDictionary();

  public EnumSymbol(ParserWrappedNode n, string name, Scope enclosing_scope)
      : base(n, name, enclosing_scope)
  {}

  public string GetName() { return name; }
  public int GetTypeIndex() { return SymbolTable.TIDX_ENUM; }

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
    : base(null, name, null)
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

    var item = new EnumItemSymbol(null, this, name, val);
    members.Add(item);
    return 0;
  }
}

public class EnumItemSymbol : Symbol, Type 
{
  public EnumSymbol en;
  public int val;

  public EnumItemSymbol(ParserWrappedNode n, EnumSymbol en, string name, int val = 0) 
    : base(n, name) 
  {
    this.en = en;
    this.val = val;
  }

  public string GetName() { return en.name; }
  public int GetTypeIndex() { return en.GetTypeIndex(); }
}

static public class SymbolTable 
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

  static public BuiltInTypeSymbol symb_bool = new BuiltInTypeSymbol("bool", TIDX_BOOLEAN);
  static public BuiltInTypeSymbol symb_string = new BuiltInTypeSymbol("string", TIDX_STRING);
  static public BuiltInTypeSymbol symb_int = new BuiltInTypeSymbol("int", TIDX_INT);
  static public BuiltInTypeSymbol symb_float = new BuiltInTypeSymbol("float", TIDX_FLOAT);
  static public BuiltInTypeSymbol symb_void = new BuiltInTypeSymbol("void", TIDX_VOID);
  static public BuiltInTypeSymbol symb_enum = new BuiltInTypeSymbol("enum", TIDX_ENUM);
  static public BuiltInTypeSymbol symb_any = new BuiltInTypeSymbol("any", TIDX_ANY);
  static public BuiltInTypeSymbol symb_null = new BuiltInTypeSymbol("null", TIDX_USER);

  // Arithmetic types defined in order from narrowest to widest
  public static Type[] index2type = new Type[] {
      // 0,  1,        2,           3,        4,          5,          6          7
      null, symb_bool, symb_string, symb_int, symb_float, symb_void,  symb_enum, symb_any
  };

  public static uint[] hash2type = new uint[] {
      // 0_null  1_bool   2_string   3_int     4_float    5_void    6_enum     7_any
      97254479, 92354194, 247378601, 72473265, 161832597, 41671150, 247377489, 83097524 
  };

  // Map t1 op t2 to result type (_void implies illegal)
  public static Type[,] arithmetic_res_type = new Type[,] {
      /*          struct  boolean  string   int     float    void    enum    any */
      /*struct*/  {symb_void, symb_void,   symb_void,   symb_void,  symb_void,   symb_void,  symb_void,  symb_void},
      /*boolean*/ {symb_void, symb_void,   symb_void,   symb_void,  symb_void,   symb_void,  symb_void,  symb_void},
      /*string*/  {symb_void, symb_void,   symb_string, symb_void,  symb_void,   symb_void,  symb_void,  symb_void},
      /*int*/     {symb_void, symb_void,   symb_void,   symb_int,   symb_float,  symb_void,  symb_void,  symb_void},
      /*float*/   {symb_void, symb_void,   symb_void,   symb_float, symb_float,  symb_void,  symb_void,  symb_void},
      /*void*/    {symb_void, symb_void,   symb_void,   symb_void,  symb_void,   symb_void,  symb_void,  symb_void},
      /*enum*/    {symb_void, symb_void,   symb_void,   symb_void,  symb_void,   symb_void,  symb_void,  symb_void},
      /*any*/     {symb_void, symb_void,   symb_void,   symb_void,  symb_void,   symb_void,  symb_void,  symb_void}
  };

  public static Type[,] relational_res_type = new Type[,] {
      /*          struct  boolean string    int       float     void    enum    any */
      /*struct*/  {symb_void, symb_void,  symb_void,    symb_void,    symb_void,    symb_void,  symb_void,  symb_void},
      /*boolean*/ {symb_void, symb_void,  symb_void,    symb_void,    symb_void,    symb_void,  symb_void,  symb_void},
      /*string*/  {symb_void, symb_void,  symb_bool, symb_void,    symb_void,    symb_void,  symb_void,  symb_void},
      /*int*/     {symb_void, symb_void,  symb_void,    symb_bool, symb_bool, symb_void,  symb_void,  symb_void},
      /*float*/   {symb_void, symb_void,  symb_void,    symb_bool, symb_bool, symb_void,  symb_void,  symb_void},
      /*void*/    {symb_void, symb_void,  symb_void,    symb_void,    symb_void,    symb_void,  symb_void,  symb_void},
      /*enum*/    {symb_void, symb_void,  symb_void,    symb_void,    symb_void,    symb_void,  symb_void,  symb_void},
      /*any*/     {symb_void, symb_void,   symb_void,   symb_void,    symb_void,    symb_void,  symb_void,  symb_void}
  };

  public static Type[,] equality_res_type = new Type[,] {
      /*           struct boolean   string    int       float     void    enum     any */
      /*struct*/  {symb_bool,symb_void,    symb_void,    symb_void,    symb_void,    symb_void,  symb_void,  symb_void},
      /*boolean*/ {symb_void,   symb_bool, symb_void,    symb_void,    symb_void,    symb_void,  symb_void,  symb_void},
      /*string*/  {symb_void,   symb_void,    symb_bool, symb_void,    symb_void,    symb_void,  symb_void,  symb_void},
      /*int*/     {symb_void,   symb_void,    symb_void,    symb_bool, symb_bool, symb_void,  symb_void,  symb_void},
      /*float*/   {symb_void,   symb_void,    symb_void,    symb_bool, symb_bool, symb_void,  symb_void,  symb_void},
      /*void*/    {symb_void,   symb_void,    symb_void,    symb_void,    symb_void,    symb_void,  symb_void,  symb_void},
      /*enum*/    {symb_void,   symb_void,    symb_void,    symb_void,    symb_void,    symb_void,  symb_void,  symb_void},
      /*any*/     {symb_bool,symb_void,    symb_void,    symb_void,    symb_void,    symb_void,  symb_void,  symb_void}
  };

  // Indicate whether a type needs a promotion to a wider type.
  //  If not null, implies promotion required.  Null does NOT imply
  //  error--it implies no promotion.  This works for
  //  arithmetic, equality, and relational operators in bhl
  // 
  public static Type[,] promote_from_to = new Type[,] {
      /*          struct  boolean  string  int     float    void   enum   any*/
      /*struct*/  {null,  null,    null,   null,   null,    null,  null,  null},
      /*boolean*/ {null,  null,    null,   null,   null,    null,  null,  null},
      /*string*/  {null,  null,    null,   null,   null,    null,  null,  null},
      /*int*/     {null,  null,    null,   null,   symb_float,  null,  null,  null},
      /*float*/   {null,  null,    null,   null,    null,   null,  null,  null},
      /*void*/    {null,  null,    null,   null,    null,   null,  null,  null},
      /*enum*/    {null,  null,    null,   null,    null,   null,  null,  null},
      /*any*/     {null,  null,    null,   null,    null,   null,  null,  null}
  };

  public static Type[,] cast_from_to = new Type[,] {
      /*          struct  boolean  string   int     float   void   enum   any*/
      /*struct*/  {null,  null,    null,    null,   null,   null,  null,  symb_any},
      /*boolean*/ {null,  null,    symb_string, symb_int,   symb_float, null,  null,  symb_any},
      /*string*/  {null,  null,    symb_string, null,   null,   null,  null,  symb_any},
      /*int*/     {null,  symb_bool,symb_string, symb_int,   symb_float, null,  null,  symb_any},
      /*float*/   {null,  symb_bool,symb_string, symb_int,   symb_float, null,  symb_enum,  symb_any},
      /*void*/    {null,  null,    null,    null,   null,   null,  null,  null},
      /*enum*/    {null,  null,    symb_string, symb_int,   symb_float,   null,  null,  symb_any},
      /*any*/     {null,  symb_bool,symb_string, symb_int,   null,   null,  null,  symb_any}
  };

  static public GlobalScope CreateBuiltins()
  {
    var globals = new GlobalScope();
    InitBuiltins(globals);
    return globals;
  }

  static public void InitBuiltins(GlobalScope globals) 
  {
    foreach(Type t in index2type) 
    {
      if(t != null) 
      {
        var blt = (BuiltInTypeSymbol)t; 
        globals.Define(blt);
      }
    }

    //for all generic arrays
    globals.Define(new GenericArrayTypeSymbol(globals));

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

  static public Type GetResultType(Type[,] typeTable, ParserWrappedNode a, ParserWrappedNode b) 
  {
    if(a.eval_type == b.eval_type)
      return a.eval_type;

    int ta = a.eval_type.GetTypeIndex(); // type index of left operand
    int tb = b.eval_type.GetTypeIndex(); // type index of right operand

    Type result = typeTable[ta,tb];    // operation result type
    if(result == symb_void) 
    {
      throw new UserError(
        a.Location()+" : incompatible types"
      );
    }
    else 
    {
      a.promote_to_type = promote_from_to[ta,tb];
      b.promote_to_type = promote_from_to[tb,ta];
    }
    return result;
  }

  static public bool CanAssignTo(Type src, Type dst, Type promotion) 
  {
    return src == dst || 
           promotion == dst || 
           dst == symb_any ||
           (dst is ClassSymbol && src == symb_null) ||
           (dst is FuncType && src == symb_null) || 
           src.GetName() == dst.GetName() ||
           IsChildClass(src, dst)
           ;
  }

  static public bool IsChildClass(Type t, Type pt) 
  {
    var cl = t as ClassSymbol;
    if(cl == null)
      return false;
    var pcl = pt as ClassSymbol;
    if(pcl == null)
      return false;
    return cl.IsSubclassOf(pcl);
  }

  static public bool IsInSameClassHierarchy(Type a, Type b) 
  {
    var ca = a as ClassSymbol;
    if(ca == null)
      return false;
    var cb = b as ClassSymbol;
    if(cb == null)
      return false;
    return cb.IsSubclassOf(ca) || ca.IsSubclassOf(cb);
  }

  static public void CheckAssign(ParserWrappedNode lhs, ParserWrappedNode rhs) 
  {
    int tlhs = lhs.eval_type.GetTypeIndex(); // promote right to left type?
    int trhs = rhs.eval_type.GetTypeIndex();
    rhs.promote_to_type = promote_from_to[trhs,tlhs];
    if(!CanAssignTo(rhs.eval_type, lhs.eval_type, rhs.promote_to_type)) 
    {
      throw new UserError(
        lhs.Location()+" : incompatible types"
      );
    }
  }

  static public void CheckAssign(Type lhs, ParserWrappedNode rhs) 
  {
    int tlhs = lhs.GetTypeIndex(); // promote right to left type?
    int trhs = rhs.eval_type.GetTypeIndex();
    rhs.promote_to_type = promote_from_to[trhs,tlhs];
    if(!CanAssignTo(rhs.eval_type, lhs, rhs.promote_to_type)) 
    {
      throw new UserError(
        rhs.Location()+" : incompatible types"
      );
    }
  }

  static public void CheckAssign(ParserWrappedNode lhs, Type rhs) 
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

  static public void CheckCast(ParserWrappedNode type, ParserWrappedNode exp) 
  {
    int tlhs = type.eval_type.GetTypeIndex();
    int trhs = exp.eval_type.GetTypeIndex();

    //special case: we allow to cast from 'any' to any user type
    if(trhs == SymbolTable.TIDX_ANY && tlhs == SymbolTable.TIDX_USER)
      return;

    //special case: we allow to cast from 'any' numeric type to enum
    if((trhs == SymbolTable.TIDX_FLOAT || 
        trhs == SymbolTable.TIDX_INT) && 
        tlhs == SymbolTable.TIDX_ENUM)
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

  static public bool IsBopCompatibleType(Type type)
  {
    return type == symb_bool || 
           type == symb_string ||
           type == symb_int ||
           type == symb_float;
  }

  static public Type Bop(ParserWrappedNode a, ParserWrappedNode b) 
  {
    if(!IsBopCompatibleType(a.eval_type))
      throw new UserError(
        a.Location()+" operator is not overloaded"
      );

    if(!IsBopCompatibleType(b.eval_type))
      throw new UserError(
        b.Location()+" operator is not overloaded"
      );

    return GetResultType(arithmetic_res_type, a, b);
  }

  static public Type BopOverload(Scope scope, ParserWrappedNode a, ParserWrappedNode b, FuncSymbol op_func) 
  {
    var op_func_arg = op_func.GetArgs()[0];
    CheckAssign(op_func_arg.type.Get(scope), b);
    return op_func.GetReturnType();
  }

  static public bool IsRelopCompatibleType(Type type)
  {
    return type == symb_int ||
           type == symb_float;
  }

  static public Type Relop(ParserWrappedNode a, ParserWrappedNode b) 
  {
    if(!IsRelopCompatibleType(a.eval_type))
      throw new UserError(
        a.Location()+" : operator is not overloaded"
      );

    if(!IsRelopCompatibleType(b.eval_type))
      throw new UserError(
        b.Location()+" : operator is not overloaded"
      );

    GetResultType(relational_res_type, a, b);
    // even if the operands are incompatible, the type of
    // this operation must be boolean
    return symb_bool;
  }

  static public Type Eqop(ParserWrappedNode a, ParserWrappedNode b) 
  {
    GetResultType(equality_res_type, a, b);
    return symb_bool;
  }

  static public Type Uminus(ParserWrappedNode a) 
  {
    if(!(a.eval_type == symb_int || a.eval_type == symb_float)) 
      throw new UserError(a.Location()+" : must be numeric type");

    return a.eval_type;
  }

  static public Type Bitop(ParserWrappedNode a, ParserWrappedNode b) 
  {
    if(a.eval_type != symb_int) 
      throw new UserError(a.Location()+" : must be int type");

    if(b.eval_type != symb_int)
      throw new UserError(b.Location()+" : must be int type");

    return symb_int;
  }

  static public Type Lop(ParserWrappedNode a, ParserWrappedNode b) 
  {
    if(a.eval_type != symb_bool) 
      throw new UserError(a.Location()+" : must be bool type");

    if(b.eval_type != symb_bool)
      throw new UserError(b.Location()+" : must be bool type");

    return symb_bool;
  }

  static public Type Unot(ParserWrappedNode a) 
  {
    if(a.eval_type != symb_bool) 
      throw new UserError(a.Location()+" : must be bool type");

    return a.eval_type;
  }
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
