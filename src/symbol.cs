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

  public Type Get()
  {
    if(type != null)
      return type;

    if(name.n == 0)
      return null;

    type = (bhl.Type)bindings.resolve(name);
    if(type == null)
      throw new Exception("Bad type: '" + name + "'");
    return type;
  }

  public Type TryGet()
  {
    if(type != null)
      return type;

    if(name.n == 0)
      return null;

    type = (bhl.Type)bindings.resolve(name);
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
  protected ClassSymbol super_class;
  public SymbolsDictionary members = new SymbolsDictionary();

  public Interpreter.ClassCreator creator;

  public ClassSymbol(
    WrappedNode n, 
    HashedName name, 
    TypeRef super_class, 
    Scope enclosing_scope, 
    Interpreter.ClassCreator creator = null
  )
    : base(n, name, enclosing_scope)
  {
    this.super_class = super_class == null ? null : (ClassSymbol)super_class.Get();
    this.creator = creator;
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

  public override void define(Symbol sym) 
  {
    if(super_class != null && super_class.GetMembers().Contains(sym.name))
      throw new UserError(sym.Location() + ": already defined symbol '" + sym.name.s + "'"); 

    base.define(sym);
  }

  public HashedName GetName() { return name; }
  public int GetTypeIndex() { return SymbolTable.tUSER; }

  public override SymbolsDictionary GetMembers() { return members; }

  public override string ToString() 
  {
    return "class "+name+":{"+
            string.Join(",", members.GetStringKeys().ToArray())+"}";
  }
}

abstract public class ArrayTypeSymbol : ClassSymbol
{
  public TypeRef original;

#if BHL_FRONT
  public ArrayTypeSymbol(BaseScope scope, bhlParser.TypeContext node)
    : this(scope, new TypeRef(scope, node.NAME().GetText()))
  {}
#endif

  public ArrayTypeSymbol(BaseScope scope, TypeRef original) 
    : base(null, original.name.s + "[]", new TypeRef(), null)
  {
    this.original = original;

    this.creator = CreateArr;

    {
      var fn = new FuncBindSymbol("Add", scope.type("void"), Create_Add);
      fn.define(new FuncArgSymbol("o", original));
      this.define(fn);
    }

    {
      var fn = new FuncBindSymbol("At", original, Create_At);
      fn.define(new FuncArgSymbol("idx", scope.type("int")));
      this.define(fn);
    }

    {
      var fn = new FuncBindSymbol("RemoveAt", scope.type("void"), Create_RemoveAt);
      fn.define(new FuncArgSymbol("idx", scope.type("int")));
      this.define(fn);
    }

    {
      var vs = new bhl.FieldSymbol("Count", scope.type("int"), Create_Count,
        //read only property
        null
      );
      this.define(vs);
    }
  }

  public abstract void CreateArr(ref DynVal v);
  public abstract void Create_Count(bhl.DynVal ctx, ref bhl.DynVal v);
  public abstract BehaviorTreeNode Create_New();
  public abstract BehaviorTreeNode Create_Add();
  public abstract BehaviorTreeNode Create_At();
  public abstract BehaviorTreeNode Create_SetAt();
  public abstract BehaviorTreeNode Create_RemoveAt();
}

//NOTE: this one is used as a fallback for all arrays which
//      were not explicitely re-defined 
public class GenericArrayTypeSymbol : ArrayTypeSymbol
{
  public static readonly HashedName GENERIC_CLASS_TYPE = new HashedName("[]"); 

#if BHL_FRONT
  public GenericArrayTypeSymbol(BaseScope scope, bhlParser.TypeContext node)
    : base(scope, node)
  {}
#endif

  public GenericArrayTypeSymbol(BaseScope scope, TypeRef original) 
    : base(scope, original)
  {}

  public GenericArrayTypeSymbol(BaseScope scope) 
    : base(scope, new TypeRef(scope, ""))
  {}

  public override HashedName Type()
  {
    return GENERIC_CLASS_TYPE;
  }

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

  public ArrayTypeSymbolT(BaseScope scope, TypeRef original, CreatorCb creator, ConverterCb converter = null) 
    : base(scope, original)
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
}

public class VariableSymbol : Symbol 
{
  public VariableSymbol(WrappedNode n, HashedName name, TypeRef type) 
    : base(n, name, type) 
  {}
}

public class FuncArgSymbol : VariableSymbol
{
  public bool is_ref;

  public FuncArgSymbol(HashedName name, TypeRef type, bool is_ref = false)
    : base(null, name, type)
  {
    this.is_ref = is_ref;
  }
}

public class FieldSymbol : VariableSymbol
{
  public Interpreter.FieldGetter getter;
  public Interpreter.FieldSetter setter;

  public FieldSymbol(HashedName name, TypeRef type, Interpreter.FieldGetter getter, Interpreter.FieldSetter setter = null) 
    : base(null, name, type)
  {
    this.getter = getter;
    this.setter = setter;
  }
}

public class FieldSymbolAST : FieldSymbol
{
  public FieldSymbolAST(HashedName name, HashedName type) 
    : base(name, new TypeRef(null, type), null, null)
  {
    this.getter = Getter;
    this.setter = Setter;
  }

  void Getter(DynVal ctx, ref DynVal v)
  {
    var m = (ClassStorage)ctx.obj;
    v.ValueCopyFrom(m.Get(name));
  }

  void Setter(ref DynVal ctx, DynVal v)
  {
    var m = (ClassStorage)ctx.obj;
    var tmp = v.ValueClone();
    m.Set(name, tmp);
    tmp.RefMod(RefOp.TRY_DEL);
  }
}

public abstract class ScopedSymbol : Symbol, Scope 
{
  protected Scope enclosing_scope;

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

  public virtual Symbol resolve(HashedName name) 
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
      return pscope.resolve(name);

    return null;
  }

  public virtual void define(Symbol sym) 
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

  public abstract SymbolsDictionary GetMembers();
}

public class MultiType : Type
{
  public HashedName name;

  public List<TypeRef> items = new List<TypeRef>();

  public int GetTypeIndex() { return SymbolTable.tUSER; }
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

  public int GetTypeIndex() { return SymbolTable.tUSER; }
  public HashedName GetName() { return name; }

  public FuncType()
  {}

#if BHL_FRONT
  public FuncType(BaseScope scope, bhlParser.TypeContext node)
  {
    ret_type = scope.type(node.NAME().GetText());
    var fnames = node.fnargs().names();
    if(fnames != null)
    {
      for(int i=0;i<fnames.refName().Length;++i)
      {
        var name = fnames.refName()[i];
        var arg_type = scope.type(name.NAME().GetText());
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
        ft.arg_types.Add(parent.type(vd.type()));
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

  public void AddUseParam(Symbol s, bool is_ref)
  {
    var up = AST_Util.New_UseParam(s.name, is_ref); 
    decl.useparams.Add(up);
    this.define(s);
  }

  public override Symbol resolve(HashedName name) 
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
      return pscope.resolve(name);

    return null;
  }

  Symbol ResolveUpvalue(HashedName name)
  {
    int idx = FindMyIdxInStack();
    if(idx == -1)
      throw new Exception("Not found");

    for(int i=idx;i-- > 0;)
    {
      var decl = fdecl_stack[i];

      //NOTE: only variable symbols are considered
      Symbol res = null;
      decl.GetMembers().TryGetValue(name, out res);
      if(res != null && res is VariableSymbol)
      {
        DefineInStack(res, i+1, idx);
        return res;
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

  void DefineInStack(Symbol res, int from_idx, int to_idx)
  {
    //now let's put this result into all nested lambda scopes 
    for(int j=from_idx;j<=to_idx;++j)
    {
      var lmb = fdecl_stack[j] as LambdaSymbol;
      if(lmb == null)
        continue;
      lmb.AddUseParam(res, false/*not a ref*/);
    }
  }
}

public class FuncSymbolAST : FuncSymbol
{
  public AST_FuncDecl decl;
#if BHL_FRONT
  //NOTE: storing fparams so it can be accessed later for misc things, e.g. default args
  public bhlParser.FuncParamsContext fparams;
#endif

  //frontend version
#if BHL_FRONT
  public FuncSymbolAST(
    BaseScope parent, 
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
          var type = parent.type(vd.type());
          type.is_ref = vd.isRef() != null;
          ft.arg_types.Add(type);
        }
      }
      ft.Update();
    }
  }
#endif

  //backend version
  public FuncSymbolAST(BaseScope parent, AST_FuncDecl decl)
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

public class FuncBindSymbol : FuncSymbol
{
  public Interpreter.FuncNodeCreator func_creator;

  public int def_args_num;

  public FuncBindSymbol(
    HashedName name, 
    TypeRef ret_type, 
    Interpreter.FuncNodeCreator func_creator, 
    int def_args_num = 0
  ) 
    : base(null, name, new FuncType(ret_type), null)
  {
    this.func_creator = func_creator;
    this.def_args_num = def_args_num;
  }

  public override int GetTotalArgsNum() { return GetMembers().Count; }
  public override int GetDefaultArgsNum() { return def_args_num; }

  public override void define(Symbol sym) 
  {
    base.define(sym);

    //NOTE: for bind funcs every defined symbol is assumed to be an argument
    DefineArg(sym.name);

    var ft = GetFuncType();
    ft.arg_types.Add(sym.type);
    ft.Update();
  }
}

public class SimpleFuncBindSymbol : FuncBindSymbol
{
  public SimpleFuncBindSymbol(HashedName name, TypeRef ret_type, SimpleFunctorNode.Functor fn, int def_args_num = 0) 
    : base(name, ret_type, delegate() { return new SimpleFunctorNode(fn, name); }, def_args_num )
  {}
}

public class ConfNodeSymbol : FuncBindSymbol
{
  public Interpreter.ConfigGetter conf_getter;

  public ConfNodeSymbol(HashedName name, TypeRef ret_type, Interpreter.FuncNodeCreator func_creator, Interpreter.ConfigGetter conf_getter) 
    : base(name, ret_type, func_creator)
  {
    this.conf_getter = conf_getter;
  }

  //NOTE: very controverse, but makes porting and usage much cleaner
  public override int GetDefaultArgsNum() { return 1; }
}

public class ClassBindSymbol : ClassSymbol
{
  public ClassBindSymbol(HashedName name, Interpreter.ClassCreator creator)
    : base(null, name, null, null, creator)
  {}

  public ClassBindSymbol(HashedName name, TypeRef super_class, Interpreter.ClassCreator creator)
    : base(null, name, super_class, null, creator)
  {}
}

public class ClassSymbolAST : ClassSymbol
{
  public AST_ClassDecl decl;

  public ClassSymbolAST(HashedName name, AST_ClassDecl decl, ClassSymbol parent = null)
    : base(null, name, parent == null ? null : new TypeRef(parent), null, null)
  {
    this.decl = decl;
    this.creator = ClassCreator;
  }

  void ClassCreator(ref DynVal res)
  {
    ClassStorage s = null;
    if(super_class != null)
    {
      super_class.creator(ref res);
      s = (ClassStorage)res.obj;
    }
    else
    {
      s = ClassStorage.New();
      res.SetObj(s);
    }

    for(int i=0;i<members.Count;++i)
    {
      var m = members[i];
      var dv = DynVal.New();
      //NOTE: proper default init of built-in types
      if(m.type.name.IsEqual(SymbolTable._float.type.name))
        dv.SetNum(0);
      else if(m.type.name.IsEqual(SymbolTable._int.type.name))
        dv.SetNum(0);
      else if(m.type.name.IsEqual(SymbolTable._string.type.name))
        dv.SetStr("");
      else if(m.type.name.IsEqual(SymbolTable._boolean.type.name))
        dv.SetBool(false);

      s.Set(m.name, dv);
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
  public int GetTypeIndex() { return SymbolTable.tENUM; }

  public override SymbolsDictionary GetMembers() { return members; }

  public EnumItemSymbol FindValue(HashedName name)
  {
    return base.resolve(name) as EnumItemSymbol;
  }

  public override string ToString() 
  {
    return "enum "+name+":{"+
            string.Join(",", members.GetStringKeys().ToArray())+"}";
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
  public const int tUSER      = 0; // user-defined type
  public const int tBOOLEAN   = 1;
  public const int tSTRING    = 2;
  public const int tINT       = 3;
  public const int tFLOAT     = 4;
  public const int tVOID      = 5;
  public const int tENUM      = 6;
  public const int tANY       = 7;

  static public BuiltInTypeSymbol _boolean = new BuiltInTypeSymbol("bool", tBOOLEAN);
  static public BuiltInTypeSymbol _string = new BuiltInTypeSymbol("string", tSTRING);
  static public BuiltInTypeSymbol _int = new BuiltInTypeSymbol("int", tINT);
  static public BuiltInTypeSymbol _float = new BuiltInTypeSymbol("float", tFLOAT);
  static public BuiltInTypeSymbol _void = new BuiltInTypeSymbol("void", tVOID);
  static public BuiltInTypeSymbol _enum = new BuiltInTypeSymbol("enum", tENUM);
  static public BuiltInTypeSymbol _any = new BuiltInTypeSymbol("any", tANY);
  static public BuiltInTypeSymbol _null = new BuiltInTypeSymbol("null", tUSER);

  // Arithmetic types defined in order from narrowest to widest
  public static Type[] indexToType = new Type[] {
      // 0,  1,        2,       3,    4,      5,     6       7
      null, _boolean, _string, _int, _float, _void,  _enum, _any
  };

  // Map t1 op t2 to result type (_void implies illegal)
  public static Type[,] arithmeticResultType = new Type[,] {
      /*          struct  boolean  string   int     float    void    enum    any */
      /*struct*/  {_void, _void,   _void,   _void,  _void,   _void,  _void,  _void},
      /*boolean*/ {_void, _void,   _void,   _void,  _void,   _void,  _void,  _void},
      /*string*/  {_void, _void,   _string, _void,  _void,   _void,  _void,  _void},
      /*int*/     {_void, _void,   _void,   _int,   _float,  _void,  _void,  _void},
      /*float*/   {_void, _void,   _void,   _float, _float,  _void,  _void,  _void},
      /*void*/    {_void, _void,   _void,   _void,  _void,   _void,  _void,  _void},
      /*enum*/    {_void, _void,   _void,   _void,  _void,   _void,  _void,  _void},
      /*any*/     {_void, _void,   _void,   _void,  _void,   _void,  _void,  _void}
  };

  public static Type[,] relationalResultType = new Type[,] {
      /*          struct  boolean string    int       float     void    enum    any */
      /*struct*/  {_void, _void,  _void,    _void,    _void,    _void,  _void,  _void},
      /*boolean*/ {_void, _void,  _void,    _void,    _void,    _void,  _void,  _void},
      /*string*/  {_void, _void,  _boolean, _void,    _void,    _void,  _void,  _void},
      /*int*/     {_void, _void,  _void,    _boolean, _boolean, _void,  _void,  _void},
      /*float*/   {_void, _void,  _void,    _boolean, _boolean, _void,  _void,  _void},
      /*void*/    {_void, _void,  _void,    _void,    _void,    _void,  _void,  _void},
      /*enum*/    {_void, _void,  _void,    _void,    _void,    _void,  _void,  _void},
      /*any*/     {_void, _void,   _void,   _void,    _void,    _void,  _void,  _void}
  };

  public static Type[,] equalityResultType = new Type[,] {
      /*           struct boolean   string    int       float     void    enum     any */
      /*struct*/  {_boolean, _void,    _void,    _void,    _void,   _void,  _void,  _void},
      /*boolean*/ {_void,   _boolean, _void,    _void,    _void,    _void,  _void,  _void},
      /*string*/  {_void,   _void,    _boolean, _void,    _void,    _void,  _void,  _void},
      /*int*/     {_void,   _void,    _void,    _boolean, _boolean, _void,  _void,  _void},
      /*float*/   {_void,   _void,    _void,    _boolean, _boolean, _void,  _void,  _void},
      /*void*/    {_void,   _void,    _void,    _void,    _void,    _void,  _void,  _void},
      /*enum*/    {_void,   _void,    _void,    _void,    _void,    _void,  _void,  _void},
      /*any*/     {_void,   _void,    _void,    _void,    _void,    _void,  _void,  _void}
  };

  // Indicate whether a type needs a promotion to a wider type.
  //  If not null, implies promotion required.  Null does NOT imply
  //  error--it implies no promotion.  This works for
  //  arithmetic, equality, and relational operators in bhl
  // 
  public static Type[,] promoteFromTo = new Type[,] {
      /*          struct  boolean  string  int     float    void   enum   any*/
      /*struct*/  {null,  null,    null,   null,   null,    null,  null,  null},
      /*boolean*/ {null,  null,    null,   null,   null,    null,  null,  null},
      /*string*/  {null,  null,    null,   null,   null,    null,  null,  null},
      /*int*/     {null,  null,    null,   null,   _float,  null,  null,  null},
      /*float*/   {null,  null,    null,   null,    null,   null,  null,  null},
      /*void*/    {null,  null,    null,   null,    null,   null,  null,  null},
      /*enum*/    {null,  null,    null,   null,    null,   null,  null,  null},
      /*any*/     {null,  null,    null,   null,    null,   null,  null,  null}
  };

  public static Type[,] castFromTo = new Type[,] {
      /*          struct  boolean  string   int     float   void   enum   any*/
      /*struct*/  {null,  null,    null,    null,   null,   null,  null,  _any},
      /*boolean*/ {null,  null,    _string, _int,   _float, null,  null,  _any},
      /*string*/  {null,  null,    _string, null,   null,   null,  null,  _any},
      /*int*/     {null,  _boolean,_string, _int,   _float, null,  null,  _any},
      /*float*/   {null,  _boolean,_string, _int,   _float, null,  _enum,  _any},
      /*void*/    {null,  null,    null,    null,   null,   null,  null,  null},
      /*enum*/    {null,  null,    _string, _int,   _float,   null,  null,  _any},
      /*any*/     {null,  _boolean,_string, _int,   null,   null,  null,  _any}
  };

  static public GlobalScope CreateBuiltins()
  {
    var globals = new GlobalScope();
    InitBuiltins(globals);
    return globals;
  }

  static public void InitBuiltins(GlobalScope globals) 
  {
    foreach(Type t in indexToType) 
    {
      if(t != null) 
      {
        var blt = (BuiltInTypeSymbol)t; 
        globals.define(blt);
      }
    }

    //for all generic arrays
    globals.define(new GenericArrayTypeSymbol(globals));

    {
      var fn = new FuncBindSymbol("RUNNING", globals.type("void"),
        delegate() { return new AlwaysRunning(); } 
      );
      globals.define(fn);
    }

    {
      var fn = new FuncBindSymbol("YIELD", globals.type("void"),
        delegate() { return new YieldOnce(); } 
      );
      globals.define(fn);
    }

    {
      var fn = new FuncBindSymbol("SUCCESS", globals.type("void"),
        delegate() { return new AlwaysSuccess(); } 
      );

      globals.define(fn);
    }

    {
      var fn = new FuncBindSymbol("FAILURE", globals.type("void"),
        delegate() { return new AlwaysFailure(); } 
      );

      globals.define(fn);
    }
  }

  static public Type GetResultType(Type[,] typeTable, WrappedNode a, WrappedNode b) 
  {
    if(a.eval_type == b.eval_type)
      return a.eval_type;

    int ta = a.eval_type.GetTypeIndex(); // type index of left operand
    int tb = b.eval_type.GetTypeIndex(); // type index of right operand
    Type result = typeTable[ta,tb];    // operation result type
    if(result == _void) 
    {
      throw new UserError(
        a.Location()+", "+ b.Location()+" have incompatible types"
      );
    }
    else 
    {
      a.promote_to_type = promoteFromTo[ta,tb];
      b.promote_to_type = promoteFromTo[tb,ta];
    }
    return result;
  }

  static public bool CanAssignTo(Type valueType, Type destType, Type promotion) 
  {
    return valueType == destType || 
           promotion == destType || 
           destType == _any ||
           (destType is ClassSymbol && valueType == _null) ||
           (destType is FuncType && valueType == _null) || 
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
    rhs.promote_to_type = promoteFromTo[trhs,tlhs];
    if(!CanAssignTo(rhs.eval_type, lhs.eval_type, rhs.promote_to_type)) 
    {
      throw new UserError(
        lhs.Location()+", "+
        rhs.Location()+" have incompatible types "
      );
    }
  }

  static public void CheckAssign(Type lhs, WrappedNode rhs) 
  {
    int tlhs = lhs.GetTypeIndex(); // promote right to left type?
    int trhs = rhs.eval_type.GetTypeIndex();
    rhs.promote_to_type = promoteFromTo[trhs,tlhs];
    if(!CanAssignTo(rhs.eval_type, lhs, rhs.promote_to_type)) 
    {
      throw new UserError(
        lhs.GetName().s+", "+
        rhs.Location()+" have incompatible types"
      );
    }
  }

  static public void CheckAssign(WrappedNode lhs, Type rhs) 
  {
    int tlhs = lhs.eval_type.GetTypeIndex(); // promote right to left type?
    int trhs = rhs.GetTypeIndex();
    var promote_to_type = promoteFromTo[trhs,tlhs];
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
    if(trhs == SymbolTable.tANY && tlhs == SymbolTable.tUSER)
      return;

    //special case: we allow to cast from 'any' numeric type to enum
    if((trhs == SymbolTable.tFLOAT || 
        trhs == SymbolTable.tINT) && 
        tlhs == SymbolTable.tENUM)
      return;

    var cast_type = castFromTo[trhs,tlhs];

    if(cast_type == type.eval_type)
      return;

    if(IsInSameClassHierarchy(exp.eval_type, type.eval_type))
      return;
    
    throw new UserError(
      type.Location()+", "+
      exp.Location()+" have incompatible types for casting "
    );
  }

  static public Type Bop(WrappedNode a, WrappedNode b) 
  {
    return GetResultType(arithmeticResultType, a, b);
  }

  static public Type Relop(WrappedNode a, WrappedNode b) 
  {
    GetResultType(relationalResultType, a, b);
    // even if the operands are incompatible, the type of
    // this operation must be boolean
    return _boolean;
  }

  static public Type Eqop(WrappedNode a, WrappedNode b) 
  {
    GetResultType(equalityResultType, a, b);
    return _boolean;
  }

  static public Type Uminus(WrappedNode a) 
  {
    if(!(a.eval_type == _int || a.eval_type == _float)) 
      throw new UserError(a.Location()+" must have int/float type");

    return a.eval_type;
  }

  static public Type Bitop(WrappedNode a, WrappedNode b) 
  {
    if(a.eval_type != _int) 
      throw new UserError(a.Location()+" must have int type");

    if(b.eval_type != _int)
      throw new UserError(b.Location()+" must have int type");

    return _int;
  }

  static public Type Lop(WrappedNode a, WrappedNode b) 
  {
    if(a.eval_type != _boolean) 
      throw new UserError(a.Location()+" must have bool type");

    if(b.eval_type != _boolean)
      throw new UserError(b.Location()+" must have bool type");

    return _boolean;
  }

  static public Type Unot(WrappedNode a) 
  {
    if(a.eval_type != _boolean) 
      throw new UserError(a.Location()+" must have boolean type");

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
    if(hash2symb.ContainsKey(key.n))
      return true;
    return str2symb.ContainsKey(key.s);
  }

  public bool TryGetValue(HashedName key, out Symbol val)
  {
    if(hash2symb.TryGetValue(key.n, out val))
      return true;
    return str2symb.TryGetValue(key.s, out val);
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
