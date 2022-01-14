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
  HashedName GetName();
  int GetTypeIndex();
}

public class TypeRef
{
  public BaseScope bindings;

  public Type type;
  public bool is_ref;
  public HashedName name;
#if BHL_FRONT
  //NOTE: parse location of the type
  public IParseTree node;
#endif

  public TypeRef()
  {}

  public TypeRef(BaseScope bindings, HashedName name)
  {
    this.bindings = bindings;
    this.name = name;
    this.type = null;
    this.is_ref = false;
#if BHL_FRONT
    this.node = null;
#endif
  }

  public TypeRef(Type type)
  {
    this.bindings = null;
    this.name = type.GetName();
    this.type = type;
    this.is_ref = false;
#if BHL_FRONT
    this.node = null;
#endif
  }

  public bool IsEmpty()
  {
    return type == null && name.n == 0;
  }

  public Type Get(BaseScope symbols = null)
  {
    if(type != null)
      return type;

    if(name.n == 0)
      return null;

    if(symbols != null)
      type = (bhl.Type)symbols.Resolve(name);
    else
      type = (bhl.Type)bindings?.Resolve(name);

    return type;
  }
}

public class WrappedNode
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
    string ts = "";
    if(eval_type != null) 
      ts = ":<"+eval_type.GetName().s+">";

    var interval = tree.SourceInterval;
    var begin = tokens.Get(interval.a);

    string line = string.Format("@({0},{1}) ", begin.Line, begin.Column);
    return line + tree.GetText() + ts;
  }

  public string LocationAfter()
  {
    string ts = "";
    if(eval_type != null) 
      ts = ":<"+eval_type.GetName()+">";

    var interval = tree.SourceInterval;
    var end = tokens.Get(interval.b);

    string line = string.Format("@({0},{1}) ", end.Line, end.Column);
    return line + tree.GetText() + ts;
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
  // Location in parse tree, can be null if it's a binding
  public WrappedNode node;
  // All symbols at least have a name
  public HashedName name;
  public TypeRef type;
  // All symbols know what scope contains them
  public Scope scope;
  // Symbols also know their 'scope level', 
  // e.g. for  { { int a = 1 } } scope level will be 2
  public int scope_level;
  public bool is_out_of_scope;

  public Symbol(WrappedNode node, HashedName name) 
  { 
    this.node = node;
    this.name = name; 
  }

  public Symbol(WrappedNode node, HashedName name, TypeRef type) 
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
    if(node != null)
      return node.Location();
    return name + "(?)";
  }
}

// A symbol to represent built in types such int, float primitive types
public class BuiltInTypeSymbol : Symbol, Type 
{
  public int type_index;

  public BuiltInTypeSymbol(HashedName name, int type_index) 
    : base(null, name) 
  {
    this.type = new TypeRef(this);
    this.type_index = type_index;
  }

  public HashedName GetName() { return name; }
  public int GetTypeIndex() { return type_index; }
}

public class ClassSymbol : ScopedSymbol, Scope, Type 
{
  internal ClassSymbol super_class;

  public SymbolsDictionary members = new SymbolsDictionary();

  public Interpreter.ClassCreator creator;
  public VM.ClassCreator VM_creator;

  public ClassSymbol(
    WrappedNode n, 
    HashedName name, 
    TypeRef super_class_ref, 
    Scope enclosing_scope, 
    Interpreter.ClassCreator creator = null,
    VM.ClassCreator VM_creator = null
  )
    : base(n, name, enclosing_scope)
  {
    if(super_class_ref != null && !super_class_ref.IsEmpty())
    {
      this.super_class = (ClassSymbol)super_class_ref.Get();
      if(super_class == null)
        throw new UserError("parent class not resolved for " + GetName()); 
    }

    this.creator = creator;
    this.VM_creator = VM_creator;

    //NOTE: this looks at the moment a bit like a hack:
    //      We define parent members in the current class
    //      scope as well. We do this since we want to  
    //      address members in VM simply by numeric integer
    if(VM_creator != null && super_class != null)
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

  public virtual HashedName Type()
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

  public override Scope GetParentScope() 
  {
    if(super_class == null) 
      return this.enclosing_scope; // globals

    return super_class;
  }

  public Symbol ResolveMember(HashedName name)
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
      throw new UserError(sym.Location() + ": already defined symbol '" + sym.name.s + "'"); 

    base.Define(sym);

    if(sym is VariableSymbol vs)
      vs.scope_idx = members.FindStringKeyIndex(sym.name.s);
  }

  public HashedName GetName() { return name; }
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

#if BHL_FRONT
  public ArrayTypeSymbol(BaseScope scope, bhlParser.TypeContext node)
    : this(scope, new TypeRef(scope, node.NAME().GetText()))
  {}
#endif

  //NOTE: indices below are synchronized with an actual order 
  //      of class members initialized later
  public const int IDX_Add        = 0;
  public const int IDX_At         = 1;
  public const int IDX_SetAt      = 2;
  public const int IDX_RemoveAt   = 3;
  public const int IDX_Clear      = 4;
  public const int IDX_Count      = 5;
  public const int IDX_AddInplace = 6;

  public ArrayTypeSymbol(BaseScope scope, TypeRef item_type) 
    : base(null, item_type.name.s + "[]", new TypeRef(), null)
  {
    this.item_type = item_type;

    this.creator = CreateArr;
    this.VM_creator = VM_CreateArr;

    {
      var fn = new FuncSymbolNative("Add", scope.Type("void"), Create_Add, VM_Add);
      fn.Define(new FuncArgSymbol("o", item_type));
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("At", item_type, Create_At, VM_At);
      fn.Define(new FuncArgSymbol("idx", scope.Type("int")));
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("SetAt", item_type, null, VM_SetAt);
      fn.Define(new FuncArgSymbol("idx", scope.Type("int")));
      fn.Define(new FuncArgSymbol("o", item_type));
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("RemoveAt", scope.Type("void"), Create_RemoveAt, VM_RemoveAt);
      fn.Define(new FuncArgSymbol("idx", scope.Type("int")));
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("Clear", scope.Type("void"), null, VM_Clear);
      this.Define(fn);
    }

    {
      var vs = new FieldSymbol("Count", scope.Type("int"), Create_Count, null, null, VM_GetCount, null);
      this.Define(vs);
    }

    {
      var fn = new FuncSymbolNative("$AddInplace", scope.Type("void"), null, VM_AddInplace);
      fn.Define(new FuncArgSymbol("o", item_type));
      this.Define(fn);
    }

  }

  public abstract void CreateArr(ref DynVal v);
  public abstract void Create_Count(bhl.DynVal ctx, ref bhl.DynVal v);
  public abstract BehaviorTreeNode Create_New();
  public abstract BehaviorTreeNode Create_Add();
  public abstract BehaviorTreeNode Create_At();
  public abstract BehaviorTreeNode Create_SetAt();
  public abstract BehaviorTreeNode Create_RemoveAt();
  public abstract BehaviorTreeNode Create_Clear();

  public abstract void VM_CreateArr(VM.Frame frame, ref Val v);
  public abstract void VM_GetCount(Val ctx, ref Val v);
  public abstract IInstruction VM_Add(VM.Frame frame, FuncArgsInfo args_info, ref BHS status);
  public abstract IInstruction VM_At(VM.Frame frame, FuncArgsInfo args_info, ref BHS status);
  public abstract IInstruction VM_SetAt(VM.Frame frame, FuncArgsInfo args_info, ref BHS status);
  public abstract IInstruction VM_RemoveAt(VM.Frame frame, FuncArgsInfo args_info, ref BHS status);
  public abstract IInstruction VM_Clear(VM.Frame frame, FuncArgsInfo args_info, ref BHS status);
  public abstract IInstruction VM_AddInplace(VM.Frame frame, FuncArgsInfo args_info, ref BHS status);
}

//NOTE: This one is used as a fallback for all arrays which
//      were not explicitely re-defined. Fallback happens during
//      compilation phase in BaseScope.Type(..) method
//     
public class GenericArrayTypeSymbol : ArrayTypeSymbol
{
  public static readonly HashedName CLASS_TYPE = new HashedName("[]"); 
  public static int INT_CLASS_TYPE = (int)CLASS_TYPE.n1;

  public override HashedName Type()
  {
    return CLASS_TYPE;
  }

#if BHL_FRONT
  public GenericArrayTypeSymbol(BaseScope scope, bhlParser.TypeContext node)
    : base(scope, node)
  {}
#endif

  public GenericArrayTypeSymbol(BaseScope scope, TypeRef item_type) 
    : base(scope, item_type)
  {}

  public GenericArrayTypeSymbol(BaseScope scope) 
    : base(scope, new TypeRef(scope, ""))
  {}

  public override void CreateArr(ref DynVal v)
  {
    v.SetObj(DynValList.New());
  }

  public override void Create_Count(bhl.DynVal ctx, ref bhl.DynVal v)
  {
    var lst = ctx.obj as DynValList;
    if(lst == null)
      throw new UserError("Not a DynValList: " + (ctx.obj != null ? ctx.obj.GetType().Name : ""));
    v.SetNum(lst.Count);
    //NOTE: this can be an operation for the temp. array,
    //      we need to try del the array if so
    lst.TryDel();
  }

  public override BehaviorTreeNode Create_New()
  {
    return new Array_NewNode();
  }

  public override BehaviorTreeNode Create_Add()
  {
    return new Array_AddNode();
  }

  public override BehaviorTreeNode Create_At()
  {
    return new Array_AtNode();
  }

  public override BehaviorTreeNode Create_SetAt()
  {
    return new Array_SetAtNode();
  }

  public override BehaviorTreeNode Create_RemoveAt()
  {
    return new Array_RemoveAtNode();
  }

  public override BehaviorTreeNode Create_Clear()
  {
    return new Array_ClearNode();
  }

  static IList<Val> AsList(Val arr)
  {
    var lst = arr.obj as IList<Val>;
    if(lst == null)
      throw new UserError("Not a ValList: " + (arr.obj != null ? arr.obj.GetType().Name : ""+arr));
    return lst;
  }

  public override void VM_CreateArr(VM.Frame frm, ref Val v)
  {
    v.SetObj(ValList.New(frm.vm));
  }

  public override void VM_GetCount(Val ctx, ref Val v)
  {
    var lst = AsList(ctx);
    v.SetNum(lst.Count);
  }
  
  public override IInstruction VM_Add(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    var val = frame.stack.Pop();
    var arr = frame.stack.Pop();
    var lst = AsList(arr);
    lst.Add(val);
    val.Release();
    arr.Release();
    return null;
  }

  public override IInstruction VM_AddInplace(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    var val = frame.stack.Pop();
    var arr = frame.stack.Peek();
    var lst = AsList(arr);
    lst.Add(val);
    val.Release();
    return null;
  }

  public override IInstruction VM_At(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)frame.stack.PopRelease().num;
    var arr = frame.stack.Pop();
    var lst = AsList(arr);
    var res = lst[idx]; 
    frame.stack.PushRetain(res);
    arr.Release();
    return null;
  }

  public override IInstruction VM_SetAt(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
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

  public override IInstruction VM_RemoveAt(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)frame.stack.PopRelease().num;
    var arr = frame.stack.Pop();
    var lst = AsList(arr);
    lst.RemoveAt(idx); 
    arr.Release();
    return null;
  }

  public override IInstruction VM_Clear(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
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
  public delegate void ConverterCb(DynVal dv, ref T res);
  public static ConverterCb Convert;

  public delegate IList<T> CreatorCb();
  public static CreatorCb Creator;

  static public void DefaultConverter(DynVal dv, ref T res)
  {
    //TODO: is there a non-allocating way to achieve the same?
    if(typeof(T).IsEnum)
      res = (T)Enum.ToObject(typeof(T), (int)dv.num);
    else
      res = (T)dv.obj;
  }

  public ArrayTypeSymbolT(BaseScope scope, TypeRef item_type, CreatorCb creator, ConverterCb converter = null) 
    : base(scope, item_type)
  {
    Convert = converter == null ? DefaultConverter : converter;

    Creator = creator;
  }

  public override void CreateArr(ref DynVal v)
  {
    v.obj = Creator();
  }

  public override void Create_Count(bhl.DynVal ctx, ref bhl.DynVal v)
  {
    var lst = (IList)ctx.obj;
    v.SetNum(lst.Count);
  }

  public override BehaviorTreeNode Create_New()
  {
    return new Array_NewNodeT<T>();
  }

  public override BehaviorTreeNode Create_Add()
  {
    return new Array_AddNodeT<T>();
  }

  public override BehaviorTreeNode Create_At()
  {
    return new Array_AtNodeT<T>();
  }

  public override BehaviorTreeNode Create_SetAt()
  {
    return new Array_SetAtNodeT<T>();
  }

  public override BehaviorTreeNode Create_RemoveAt()
  {
    return new Array_RemoveAtNodeT();
  }

  public override BehaviorTreeNode Create_Clear()
  {
    return new Array_ClearNodeT();
  }

  public override void VM_CreateArr(VM.Frame frm, ref Val v)
  {
    throw new Exception("Not implemented");
  }

  public override void VM_GetCount(Val ctx, ref Val v)
  {
    throw new Exception("Not implemented");
  }
  
  public override IInstruction VM_Add(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    throw new Exception("Not implemented");
  }

  public override IInstruction VM_AddInplace(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    throw new Exception("Not implemented");
  }

  public override IInstruction VM_At(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    throw new Exception("Not implemented");
  }

  public override IInstruction VM_SetAt(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    throw new Exception("Not implemented");
  }

  public override IInstruction VM_RemoveAt(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    throw new Exception("Not implemented");
  }

  public override IInstruction VM_Clear(VM.Frame frame, FuncArgsInfo args_info, ref BHS status)
  {
    throw new Exception("Not implemented");
  }
}

public class VariableSymbol : Symbol 
{
  public int scope_idx = -1;

  public VariableSymbol up_src;

  public VariableSymbol(WrappedNode n, HashedName name, TypeRef type) 
    : base(n, name, type) 
  {}
}

public class FuncArgSymbol : VariableSymbol
{
  public bool is_ref;

  public FuncArgSymbol(WrappedNode n, HashedName name, TypeRef type, bool is_ref = false)
    : base(n, name, type)
  {
    this.is_ref = is_ref;
  }

  public FuncArgSymbol(HashedName name, TypeRef type, bool is_ref = false)
    : this(null, name, type, is_ref)
  {}
}

public class FieldSymbol : VariableSymbol
{
  public Interpreter.FieldGetter getter;
  public Interpreter.FieldSetter setter;
  public Interpreter.FieldRef getref;

  public VM.FieldGetter VM_getter;
  public VM.FieldSetter VM_setter;
  public VM.FieldRef VM_getref;

  public FieldSymbol(HashedName name, TypeRef type, Interpreter.FieldGetter getter, Interpreter.FieldSetter setter = null, Interpreter.FieldRef getref = null, VM.FieldGetter VM_getter = null, VM.FieldSetter VM_setter = null, VM.FieldRef VM_getref = null) 
    : base(null, name, type)
  {
    this.getter = getter;
    this.setter = setter;
    this.getref = getref;

    this.VM_getter = VM_getter;
    this.VM_setter = VM_setter;
    this.VM_getref = VM_getref;
  }
}

public class FieldSymbolScript : FieldSymbol
{
  public int VM_idx;

  public FieldSymbolScript(HashedName name, HashedName type, int VM_idx = -1) 
    : base(name, new TypeRef(null, type), null, null, null)
  {
    this.getter = Getter;
    this.setter = Setter;
    this.getref = Getref;

    this.VM_idx = VM_idx;
    this.VM_getter = VM_Getter;
    this.VM_setter = VM_Setter;
    this.VM_getref = VM_Getref;
  }

  void Getter(DynVal ctx, ref DynVal v)
  {
    var m = (DynValDict)ctx.obj;
    v.ValueCopyFrom(m.Get(name));
  }

  void Setter(ref DynVal ctx, DynVal v)
  {
    var m = (DynValDict)ctx.obj;
    var tmp = v.ValueClone();
    m.Set(name, tmp);
    tmp.RefMod(RefOp.TRY_DEL);
  }

  void Getref(DynVal ctx, out DynVal v)
  {
    var m = (DynValDict)ctx.obj;
    v = m.Get(name);
  }

  void VM_Getter(Val ctx, ref Val v)
  {
    var m = (IList<Val>)ctx.obj;
    v.ValueCopyFrom(m[VM_idx]);
  }

  void VM_Setter(ref Val ctx, Val v)
  {
    var m = (IList<Val>)ctx.obj;
    var curr = m[VM_idx];
    for(int i=0;i<curr._refs;++i)
    {
      v.RefMod(RefOp.USR_INC);
      curr.RefMod(RefOp.USR_DEC);
    }
    curr.ValueCopyFrom(v);
  }

  void VM_Getref(Val ctx, out Val v)
  {
    var m = (IList<Val>)ctx.obj;
    v = m[VM_idx];
  }
}

public abstract class ScopedSymbol : Symbol, Scope 
{
  protected Scope enclosing_scope;

  public abstract SymbolsDictionary GetMembers();

  public ScopedSymbol(WrappedNode n, HashedName name, TypeRef type, Scope enclosing_scope) 
    : base(n, name, type)
  {
    this.enclosing_scope = enclosing_scope;
  }

  public ScopedSymbol(WrappedNode n, HashedName name, Scope enclosing_scope) 
    : base(n, name)
  {
    this.enclosing_scope = enclosing_scope;
  }

  public virtual Symbol Resolve(HashedName name) 
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

    var pscope = GetParentScope();
    if(pscope != null)
      return pscope.Resolve(name);

    return null;
  }

  public virtual void Define(Symbol sym) 
  {
    var members = GetMembers(); 
    if(members.Contains(sym.name))
      throw new UserError(sym.Location() + ": already defined symbol '" + sym.name.s + "'"); 

    members.Add(sym);
    sym.scope = this; // track the scope in each symbol
  }

  public virtual Scope GetParentScope() { return GetEnclosingScope(); }
  public virtual Scope GetEnclosingScope() { return enclosing_scope; }

  public HashedName GetScopeName() { return name; }
}

public class MultiType : Type
{
  public HashedName name;

  public List<TypeRef> items = new List<TypeRef>();

  public int GetTypeIndex() { return SymbolTable.TIDX_USER; }
  public HashedName GetName() { return name; }

  public void Update()
  {
    string tmp = "";
    //TODO: guess, this can be more optimal?
    for(int i=0;i<items.Count;++i)
    {
      if(i > 0)
        tmp += ",";
      tmp += items[i].name.s;
    }

    name = new HashedName(tmp);
  }
}

public class FuncType : Type
{
  public HashedName name;

  public TypeRef ret_type;
  public List<TypeRef> arg_types = new List<TypeRef>();

  public int GetTypeIndex() { return SymbolTable.TIDX_USER; }
  public HashedName GetName() { return name; }

  public FuncType()
  {}

#if BHL_FRONT
  public FuncType(BaseScope scope, bhlParser.TypeContext node)
  {
    var fnargs = node.fnargs();

    string ret_type_str = node.NAME().GetText();
    if(fnargs.ARR() != null)
      ret_type_str += "[]";
    
    ret_type = scope.Type(ret_type_str);

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
    Update();
  }
#endif

  public FuncType(TypeRef ret_type)
  {
    this.ret_type = ret_type;
    Update();
  }

  public void Update()
  {
    string tmp = ret_type.name.s + "^("; 
    for(int i=0;i<arg_types.Count;++i)
    {
      if(i > 0)
        tmp += ",";
      if(arg_types[i].is_ref)
        tmp += "ref ";
      tmp += arg_types[i].name.s;
    }
    tmp += ")";

    name = new HashedName(tmp);
  }
}

public class FuncSymbol : ScopedSymbol
{
  SymbolsDictionary members = new SymbolsDictionary();
  SymbolsDictionary args = new SymbolsDictionary();

  public bool return_statement_found = false;

  public FuncSymbol(WrappedNode n, HashedName name, FuncType type, Scope parent) 
    : base(n, name, new TypeRef(type), parent)
  {}

  public override SymbolsDictionary GetMembers() { return members; }

  public FuncType GetFuncType()
  {
    return (FuncType)this.type.Get();
  }

  public Type GetReturnType()
  {
    return GetFuncType().ret_type.Get();
  }

  public SymbolsDictionary GetArgs()
  {
    return args;
  }

  public void DefineArg(HashedName name) 
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

  void CalcVariableScopeIdx(VariableSymbol sym)
  {
    //let's ignore already assigned ones
    if(sym.scope_idx != -1)
      return;
    int c = 0;
    for(int i=0;i<GetMembers().Count;++i)
      if(GetMembers()[i] is VariableSymbol)
        ++c;
    sym.scope_idx = c; 
  }

  public override void Define(Symbol sym)
  {
    if(sym is VariableSymbol vs)
      CalcVariableScopeIdx(vs);
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
    BaseScope parent,
    AST_LambdaDecl decl, 
    List<FuncSymbol> fdecl_stack, 
    WrappedNode n, 
    HashedName name, 
    TypeRef ret_type, 
    bhlParser.FuncLambdaContext lmb_ctx
  ) 
    : base(n, name, new FuncType(ret_type), parent)
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
        ft.arg_types.Add(parent.Type(vd.type()));
      }
    }
    ft.Update();
  }
#endif

  //backend version
  public LambdaSymbol(BaseScope parent, AST_LambdaDecl decl) 
    : base(null, decl.Name(), new FuncType(), parent)
  {
    this.decl = decl;
    this.fdecl_stack = null;
  }

  public VariableSymbol AddUseParam(VariableSymbol src, bool is_ref)
  {
    var local = new VariableSymbol(src.node, src.name, src.type);
    local.up_src = src;

    this.Define(local);

    var up = AST_Util.New_UseParam(local.name, is_ref, local.scope_idx, src.scope_idx); 
    decl.uses.Add(up);

    return local;
  }

  public override Symbol Resolve(HashedName name) 
  {
    Symbol s = null;
    GetMembers().TryGetValue(name, out s);
    if(s != null)
      return s;

    s = ResolveUpvalue(name);
    if(s != null)
      return s;

    var pscope = GetParentScope();
    if(pscope != null)
      return pscope.Resolve(name);

    return null;
  }

  Symbol ResolveUpvalue(HashedName name)
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
        vs = lmb.AddUseParam(vs, is_ref: false);
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
    LocalScope locals,
    Scope parent, 
    AST_FuncDecl decl, 
    WrappedNode n, 
    HashedName name, 
    TypeRef ret_type, 
    bhlParser.FuncParamsContext fparams
  ) 
    : base(n, name, new FuncType(ret_type), parent)
  {
    this.decl = decl;
    this.fparams = fparams;

    if(n != null)
    {
      var ft = GetFuncType();
      if(fparams != null)
      {
        for(int i=0;i<fparams.funcParamDeclare().Length;++i)
        {
          var vd = fparams.funcParamDeclare()[i];
          var type = locals.Type(vd.type());
          type.is_ref = vd.isRef() != null;
          ft.arg_types.Add(type);
        }
      }
      ft.Update();
    }
  }
#endif

  //backend version
  public FuncSymbolScript(Scope parent, AST_FuncDecl decl)
    : base(null, decl.Name(), new FuncType(), parent)
  {
    this.decl = decl;
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
  public Interpreter.FuncNodeCreator func_creator;

  public delegate IInstruction VM_Cb(VM.Frame frm, FuncArgsInfo args_info, ref BHS status); 
  public VM_Cb VM_cb;

  public int def_args_num;

  public FuncSymbolNative(
    HashedName name, 
    TypeRef ret_type, 
    Interpreter.FuncNodeCreator func_creator, 
    VM_Cb VM_cb = null, 
    int def_args_num = 0
  ) 
    : base(null, name, new FuncType(ret_type), null)
  {
    this.func_creator = func_creator;
    this.VM_cb = VM_cb;
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

public class FuncSymbolSimpleNative : FuncSymbolNative
{
  public FuncSymbolSimpleNative(HashedName name, TypeRef ret_type, SimpleFunctorNode.Functor fn, int def_args_num = 0) 
    : base(name, ret_type, delegate() { return new SimpleFunctorNode(fn, name); }, null, def_args_num)
  {}
}

public class ClassSymbolNative : ClassSymbol
{
  public ClassSymbolNative(HashedName name, Interpreter.ClassCreator creator, VM.ClassCreator VM_creator = null)
    : base(null, name, null, null, creator, VM_creator)
  {}

  public ClassSymbolNative(HashedName name, TypeRef super_class, Interpreter.ClassCreator creator, VM.ClassCreator VM_creator = null)
    : base(null, name, super_class, null, creator, VM_creator)
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

  public ClassSymbolScript(HashedName name, AST_ClassDecl decl, ClassSymbol parent = null)
    : base(null, name, parent == null ? null : new TypeRef(parent), null, null)
  {
    this.decl = decl;
    this.creator = ClassCreator;
    this.VM_creator = VM_ClassCreator;
  }

  void ClassCreator(ref DynVal res)
  {
    DynValDict tb = null;
    if(super_class != null)
    {
      super_class.creator(ref res);
      if(super_class is ClassSymbolNative)
      {
        tb = DynValDict.New();
        tb.Set(0, DynVal.NewObj(res.obj));
        res.SetObj(tb);
      }
      else
      tb = (DynValDict)res.obj;
    }
    else
    {
      tb = DynValDict.New();
      res.SetObj(tb);
    }
    //NOTE: storing class name hash in _num attribute
    res._num = decl.nname; 

    for(int i=0;i<members.Count;++i)
    {
      var m = members[i];
      var dv = DynVal.New();
      //NOTE: proper default init of built-in types
      if(m.type.name.IsEqual(SymbolTable.symb_float.type.name))
        dv.SetNum(0);
      else if(m.type.name.IsEqual(SymbolTable.symb_int.type.name))
        dv.SetNum(0);
      else if(m.type.name.IsEqual(SymbolTable.symb_string.type.name))
        dv.SetStr("");
      else if(m.type.name.IsEqual(SymbolTable.symb_bool.type.name))
        dv.SetBool(false);
      else 
      {
        var t = m.type.Get();
        if(t is EnumSymbol)
          dv.SetNum(0);
        else
          dv.SetNil();
      }

      tb.Set(m.name, dv);
    }
  }

  void VM_ClassCreator(VM.Frame frm, ref Val res)
  {
    IList<Val> vl = null;
    if(super_class != null)
    {
      super_class.VM_creator(frm, ref res);
      vl = (IList<Val>)res.obj;
    }
    else
    {
      vl = ValList.New(res.vm);
      res.SetObj(vl);
    }
    //TODO: this should be more robust
    //NOTE: storing class name hash in _num attribute
    res._num = decl.nname; 

    for(int i=0;i<members.Count;++i)
    {
      var m = members[i];
      var v = Val.New(res.vm);
      //NOTE: proper default init of built-in types
      if(m.type.name.IsEqual(SymbolTable.symb_float.type.name) || 
         m.type.name.IsEqual(SymbolTable.symb_int.type.name))
        v.SetNum(0);
      else if(m.type.name.IsEqual(SymbolTable.symb_string.type.name))
        v.SetStr("");
      else if(m.type.name.IsEqual(SymbolTable.symb_bool.type.name))
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

  public EnumSymbol(WrappedNode n, HashedName name, Scope enclosing_scope)
      : base(n, name, enclosing_scope)
  {}

  public HashedName GetName() { return name; }
  public int GetTypeIndex() { return SymbolTable.TIDX_ENUM; }

  public override SymbolsDictionary GetMembers() { return members; }

  public EnumItemSymbol FindValue(HashedName name)
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
  public EnumSymbolScript(HashedName name)
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
      else if(m.name.s == name)
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

  public EnumItemSymbol(WrappedNode n, EnumSymbol en, string name, int val = 0) 
    : base(n, name) 
  {
    this.en = en;
    this.val = val;
  }

  public HashedName GetName() { return en.name; }
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
        delegate() { return new suspend(); } 
      );
      globals.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("yield", globals.Type("void"),
        delegate() { return new yield(); } 
      );
      globals.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("nop", globals.Type("void"),
        delegate() { return new nop(); } 
      );

      globals.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("fail", globals.Type("void"),
        delegate() { return new fail(); } 
      );

      globals.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("check", globals.Type("void"),
        delegate() { return new check(); } 
      );
      fn.Define(new FuncArgSymbol("cond", globals.Type("bool")));

      globals.Define(fn);
    }
  }

  static public GlobalScope VM_CreateBuiltins()
  {
    var globals = new GlobalScope();
    VM_InitBuiltins(globals);
    return globals;
  }

  static public void VM_InitBuiltins(GlobalScope globals) 
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
      var fn = new FuncSymbolNative("suspend", globals.Type("void"), null,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          return CoroutineSuspend.Instance;
        } 
      );
      globals.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("yield", globals.Type("void"), null,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          return Instructions.New<CoroutineYield>(frm.vm);
        } 
      );
      globals.Define(fn);
    }

    //TODO: this one is controversary, it's defined for BC for now
    {
      var fn = new FuncSymbolNative("fail", globals.Type("void"), null,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) 
        { 
          status = BHS.FAILURE;
          return null;
        } 
      );
      globals.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("start", globals.Type("int"), null,
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
      var fn = new FuncSymbolNative("stop", globals.Type("void"), null,
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

  static public Type GetResultType(Type[,] typeTable, WrappedNode a, WrappedNode b) 
  {
    if(a.eval_type == b.eval_type)
      return a.eval_type;

    int ta = a.eval_type.GetTypeIndex(); // type index of left operand
    int tb = b.eval_type.GetTypeIndex(); // type index of right operand

    Type result = typeTable[ta,tb];    // operation result type
    if(result == symb_void) 
    {
      throw new UserError(
        a.Location()+", "+ b.Location()+" have incompatible types"// + ta + " " + tb
      );
    }
    else 
    {
      a.promote_to_type = promote_from_to[ta,tb];
      b.promote_to_type = promote_from_to[tb,ta];
    }
    return result;
  }

  static public bool CanAssignTo(Type valueType, Type destType, Type promotion) 
  {
    return valueType == destType || 
           promotion == destType || 
           destType == symb_any ||
           (destType is ClassSymbol && valueType == symb_null) ||
           (destType is FuncType && valueType == symb_null) || 
           valueType.GetName().n == destType.GetName().n ||
           IsChildClass(valueType, destType)
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

  static public void CheckAssign(WrappedNode lhs, WrappedNode rhs) 
  {
    int tlhs = lhs.eval_type.GetTypeIndex(); // promote right to left type?
    int trhs = rhs.eval_type.GetTypeIndex();
    rhs.promote_to_type = promote_from_to[trhs,tlhs];
    if(!CanAssignTo(rhs.eval_type, lhs.eval_type, rhs.promote_to_type)) 
    {
      throw new UserError(
        lhs.Location()+", "+
        rhs.Location()+" have incompatible types"
      );
    }
  }

  static public void CheckAssign(Type lhs, WrappedNode rhs) 
  {
    int tlhs = lhs.GetTypeIndex(); // promote right to left type?
    int trhs = rhs.eval_type.GetTypeIndex();
    rhs.promote_to_type = promote_from_to[trhs,tlhs];
    if(!CanAssignTo(rhs.eval_type, lhs, rhs.promote_to_type)) 
    {
      throw new UserError(
        lhs.GetName().s+", "+
        rhs.Location()+" have incompatible types"// + rhs.eval_type + " vs " + lhs
      );
    }
  }

  static public void CheckAssign(WrappedNode lhs, Type rhs) 
  {
    int tlhs = lhs.eval_type.GetTypeIndex(); // promote right to left type?
    int trhs = rhs.GetTypeIndex();
    var promote_to_type = promote_from_to[trhs,tlhs];
    if(!CanAssignTo(rhs, lhs.eval_type, promote_to_type)) 
    {
      throw new UserError(
        lhs.Location()+", "+
        rhs.GetName().s+" have incompatible types "
      );
    }
  }

  static public void CheckCast(WrappedNode type, WrappedNode exp) 
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
      type.Location()+", "+
      exp.Location()+" have incompatible types for casting "
    );
  }

  static public bool IsBopCompatibleType(Type type)
  {
    return type == symb_bool || 
           type == symb_string ||
           type == symb_int ||
           type == symb_float;
  }

  static public Type Bop(WrappedNode a, WrappedNode b) 
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

  static public Type BopOverload(WrappedNode a, WrappedNode b, FuncSymbol op_func) 
  {
    var op_func_arg = op_func.GetArgs()[0];
    CheckAssign(op_func_arg.type.Get(), b);
    return op_func.GetReturnType();
  }

  static public bool IsRelopCompatibleType(Type type)
  {
    return type == symb_int ||
           type == symb_float;
  }

  static public Type Relop(WrappedNode a, WrappedNode b) 
  {
    if(!IsRelopCompatibleType(a.eval_type))
      throw new UserError(
        a.Location()+" operator is not overloaded"
      );

    if(!IsRelopCompatibleType(b.eval_type))
      throw new UserError(
        b.Location()+" operator is not overloaded"
      );

    GetResultType(relational_res_type, a, b);
    // even if the operands are incompatible, the type of
    // this operation must be boolean
    return symb_bool;
  }

  static public Type Eqop(WrappedNode a, WrappedNode b) 
  {
    GetResultType(equality_res_type, a, b);
    return symb_bool;
  }

  static public Type Uminus(WrappedNode a) 
  {
    if(!(a.eval_type == symb_int || a.eval_type == symb_float)) 
      throw new UserError(a.Location()+" must be numeric type");

    return a.eval_type;
  }

  static public Type Bitop(WrappedNode a, WrappedNode b) 
  {
    if(a.eval_type != symb_int) 
      throw new UserError(a.Location()+" must be int type");

    if(b.eval_type != symb_int)
      throw new UserError(b.Location()+" must be int type");

    return symb_int;
  }

  static public Type Lop(WrappedNode a, WrappedNode b) 
  {
    if(a.eval_type != symb_bool) 
      throw new UserError(a.Location()+" must be bool type");

    if(b.eval_type != symb_bool)
      throw new UserError(b.Location()+" must be bool type");

    return symb_bool;
  }

  static public Type Unot(WrappedNode a) 
  {
    if(a.eval_type != symb_bool) 
      throw new UserError(a.Location()+" must be bool type");

    return a.eval_type;
  }

  // 'this' and 'super' need to know about enclosing class
  //static public ClassSymbol GetEnclosingClass(Scope s) 
  //{
  //  // walk upwards from s looking for a class
  //  while(s != null) 
  //  {
  //    if (s is ClassSymbol) return (ClassSymbol)s;
  //    s = s.GetParentScope();
  //  }
  //  return null;
  //}
}

public class SymbolsDictionary
{
  //NOTE: Functions names are hashed with module id prepended in front but essentially
  //      their string names are considered to be unique across all the global namespace.
  //      Sometimes during parsing we need to lookup the function by the string name only
  //      and at that time we don't have module id at hand(i.e we can't build the proper
  //      ulong hash). For this reason we keep two dictionaries: by hash and by string.
  //      However someday we really need to get rid of the str2symb.
  Dictionary<ulong, Symbol> hash2symb = new Dictionary<ulong, Symbol>();
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

  public bool Contains(HashedName key)
  {
    if(str2symb.ContainsKey(key.s))
      return true;
    return hash2symb.ContainsKey(key.n);
  }

  public bool TryGetValue(HashedName key, out Symbol val)
  {
    if(str2symb.TryGetValue(key.s, out val))
      return true;
    return hash2symb.TryGetValue(key.n, out val);
  }

  public Symbol Find(HashedName key)
  {
    Symbol s = null;
    TryGetValue(key, out s);
    return s;
  }

  public void Add(Symbol s)
  {
    // Dictionary operation first, so exception thrown if key already exists.
    if(!string.IsNullOrEmpty(s.name.s))
      str2symb.Add(s.name.s, s);
    hash2symb.Add(s.name.n, s);
    list.Add(s);
  }

  public void RemoveAt(int index)
  {
    var s = list[index];
    if(!string.IsNullOrEmpty(s.name.s))
      str2symb.Remove(s.name.s);
    hash2symb.Remove(s.name.n);
    list.RemoveAt(index);
  }

  public int IndexOf(Symbol s)
  {
    return list.IndexOf(s);
  }

  public int IndexOf(HashedName key)
  {
    var s = Find(key);
    if(s == null)
      return -1;
    return IndexOf(s);
  }

  public void Clear()
  {
    str2symb.Clear();
    hash2symb.Clear();
    list.Clear();
  }

  public List<string> GetStringKeys()
  {
    List<string> res = new List<string>();
    for(int i=0;i<list.Count;++i)
      res.Add(list[i].name.s);
    return res;
  }

  public int FindStringKeyIndex(string key)
  {
    for(int i=0;i<list.Count;++i)
    {
      if(list[i].name.s == key)
        return i;
    }
    return -1;
  }
}

} //namespace bhl
