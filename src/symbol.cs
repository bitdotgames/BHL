using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

#if BHL_FRONT
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
#endif

namespace bhl {

public interface Type 
{
  string GetName();
  uint GetNname();
  int GetTypeIndex();
}

public struct TypeRef
{
  static public GlobalScope bindings;

  public Type type;
  public string name;
#if BHL_FRONT
  //NOTE: parse location of the type
  public bhlParser.TypeContext node;
#endif

  public static bool IsComplexType(string name)
  {
    return name.IndexOf("^") != -1;
  }

  public TypeRef(string name)
  {
    this.name = name;
    this.type = null;
    this.node = null;

#if BHL_FRONT
    if(IsComplexType(name))
    {
      var tmp = AST_Builder.ParseType(name);
      if(tmp == null)
        throw new Exception("Bad type: " + name);

      if(tmp.fnargs() != null)
        this.type = new FuncType(tmp);
    }
#endif
  }

  public TypeRef(Type type)
  {
    this.name = type.GetName();
    this.type = type;
#if BHL_FRONT
    this.node = null;
#endif
  }

#if BHL_FRONT
  public TypeRef(bhlParser.TypeContext node)
  {
    this.name = node.GetText();

    if(node.fnargs() != null)
      this.type = new FuncType(node);
    else
      this.type = null;
    this.node = node;
  }

  public static implicit operator TypeRef(bhlParser.TypeContext node)
  {
    return new TypeRef(node);
  }
#endif

  public static implicit operator TypeRef(string name)
  {
    return new TypeRef(name);
  }

  public static implicit operator TypeRef(FuncType ft)
  {
    return new TypeRef(ft);
  }

  public static implicit operator TypeRef(ClassSymbol cl)
  {
    return new TypeRef(cl);
  }

  public static implicit operator TypeRef(EnumSymbol en)
  {
    return new TypeRef(en);
  }

  public Type Get()
  {
    if(type != null)
      return type;

    if(name == null)
      return null;

    type = bindings.type(name);
    return type;
  }
}

public class WrappedNode
{
#if BHL_FRONT
  public IParseTree tree;
  public ITokenStream tokens;
  public AST_Builder builder;
#endif

  public Scope scope;
  public Symbol symbol;
  public Type eval_type;
  public Type promote_to_type;

#if BHL_FRONT
  public string Location()
  {
    string ts = "";
    if(eval_type != null) 
      ts = ":<"+eval_type.GetName()+">";

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
  public string name;
  // Hashed symbol name
  public uint nname;
  public TypeRef type;
  // All symbols know what scope contains them.
  public Scope scope;

  public Symbol(WrappedNode node, string name) 
  { 
    this.node = node;
    this.name = name; 
    this.nname = Hash.CRC28(name);
  }

  public Symbol(WrappedNode node, string name, TypeRef type) 
    : this(node, name) 
  { 
    this.type = type; 
  }

  public string GetName() { return name; }
  public uint GetNname() { return nname; }

  public HashedName Name() { return new HashedName(nname, name); }

  public override string ToString() 
  {
    string s = "";
    if(scope != null) 
      s = scope.GetScopeName()+".";
    return '<'+s+GetName()+":"+type.name+'>';
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

  public BuiltInTypeSymbol(string name, int type_index) 
    : base(null, name) 
  {
    this.type_index = type_index;
  }

  public int GetTypeIndex() { return type_index; }
}

public class ArrayTypeSymbol : ClassSymbol
{
  public TypeRef original;

  public ArrayTypeSymbol(GlobalScope globs, TypeRef original) 
    : base(null, original.name + "[]", new TypeRef(), null)
  {
    this.original = original;

    this.creator = CreateArr;

    {
      var fn = new FuncBindSymbol("Add", "void", Create_Add);
      fn.define(new FuncArgSymbol("o", original));
      this.define(fn);
    }

    {
      var fn = new FuncBindSymbol("At", original, Create_At);
      fn.define(new FuncArgSymbol("idx", "int"));
      this.define(fn);
    }

    {
      var fn = new FuncBindSymbol("RemoveAt", "void", Create_RemoveAt);
      fn.define(new FuncArgSymbol("idx", "int"));
      this.define(fn);
    }

    {
      var vs = new bhl.FieldSymbol("Count", "int",
        delegate(bhl.DynVal ctx, ref bhl.DynVal v)
        {
          var lst = (IList)ctx.obj;
          v.SetNum(lst.Count);
        },
        //read only property
        null
      );
      this.define(vs);
    }
  }

  public virtual void CreateArr(ref DynVal v)
  {
    v.SetObj(DynValList.New());
  }

  public virtual BehaviorTreeNode Create_New()
  {
    return new Array_NewNode();
  }

  public virtual BehaviorTreeNode Create_Add()
  {
    return new Array_AddNode();
  }

  public virtual BehaviorTreeNode Create_At()
  {
    return new Array_AtNode();
  }

  public virtual BehaviorTreeNode Create_RemoveAt()
  {
    return new Array_RemoveAtNode();
  }

  public new int GetTypeIndex() { return original.Get().GetTypeIndex(); }
}

public class ArrayTypeSymbolT<T> : ArrayTypeSymbol where T : new()
{
  public delegate void ConverterCb(DynVal dv, ref T res);
  public static ConverterCb Convert;

  static public void DefaultConverter(DynVal dv, ref T res)
  {
    //TODO: is there a non-allocating way to achieve the same?
    if(typeof(T).IsEnum)
      res = (T)Enum.ToObject(typeof(T), (int)dv.num);
    else
      res = (T)dv.obj;
  }

  public ArrayTypeSymbolT(GlobalScope globs, TypeRef original, ConverterCb converter = null) 
    : base(globs, original)
  {
    Convert = converter == null ? DefaultConverter : converter;
  }

  public override void CreateArr(ref DynVal v)
  {
    v.obj = new List<T>();
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
}

public class VariableSymbol : Symbol 
{
  public VariableSymbol(WrappedNode n, string name, TypeRef type) 
    : base(n, name, type) 
  {}
}

public class FuncArgSymbol : VariableSymbol
{
  public bool is_ref;

  public FuncArgSymbol(string name, TypeRef type, bool is_ref = false)
    : base(null, name, type)
  {
    this.is_ref = is_ref;
  }
}

public class FieldSymbol : VariableSymbol
{
  public Interpreter.FieldGetter getter;
  public Interpreter.FieldSetter setter;

  public FieldSymbol(string name, TypeRef type, Interpreter.FieldGetter getter, Interpreter.FieldSetter setter = null) 
    : base(null, name, type)
  {
    this.getter = getter;
    this.setter = setter;
  }
}

public abstract class ScopedSymbol : Symbol, Scope 
{
  protected Scope enclosing_scope;

  public ScopedSymbol(WrappedNode n, string name, TypeRef type, Scope enclosing_scope) 
    : base(n, name, type)
  {
    this.enclosing_scope = enclosing_scope;
  }
  public ScopedSymbol(WrappedNode n, string name, Scope enclosing_scope) 
    : base(n, name)
  {
    this.enclosing_scope = enclosing_scope;
  }

  public virtual Symbol resolve(string name) 
  {
    Symbol s = (Symbol)GetMembers()[name];
    if(s != null)
      return s;

    var pscope = GetParentScope();
    if(pscope != null)
      return pscope.resolve(name);

    return null;
  }

  public virtual void define(Symbol sym) 
  {
    var members = GetMembers(); 
    if(members.Contains(sym.name))
      throw new UserError(sym.Location() + ": Already defined symbol '" + sym.name + "'"); 

    members.Add(sym.name, sym);
    sym.scope = this; // track the scope in each symbol
  }

  public virtual Scope GetParentScope() { return GetEnclosingScope(); }
  public virtual Scope GetEnclosingScope() { return enclosing_scope; }

  public String GetScopeName() { return name; }

  // Indicate how subclasses store scope members. Allows us to
  // factor out common code in this class.
  public abstract OrderedDictionary GetMembers();
}

public class FuncType : Type
{
  public string name;
  public uint nname;
  public TypeRef ret_type;
  public List<TypeRef> arg_types = new List<TypeRef>();

  public int GetTypeIndex() { return SymbolTable.tUSER; }
  public string GetName() { return name; }
  public uint GetNname() { return nname; }

  public FuncType()
  {}

#if BHL_FRONT
  public FuncType(bhlParser.TypeContext node)
  {
    ret_type = node.NAME().GetText();
    var fnames = node.fnargs().names();
    if(fnames != null)
    {
      for(int i=0;i<fnames.NAME().Length;++i)
      {
        var name = fnames.NAME()[i];
        arg_types.Add(name.GetText());
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

  public bool IsCompatible(FuncType o)
  {
    return nname == o.nname;

    //if(type.name != o.type.name)
    //  return false;

    //if(arg_types.Count != o.arg_types.Count)
    //  return false;

    //for(int i=0;i<arg_types.Count;++i)
    //{
    //  if(arg_types[i].name != o.arg_types[i].name)
    //    return false;
    //}

    //return true;
  }

  public void Update()
  {
    name = ret_type.name + "^("; 
    for(int i=0;i<arg_types.Count;++i)
    {
      if(i > 0)
        name += ",";
      name += arg_types[i].name;
    }
    name += ")";

    nname = Hash.CRC28(name);
  }
}

public class FuncSymbol : ScopedSymbol
{
  OrderedDictionary members = new OrderedDictionary();
  OrderedDictionary args = new OrderedDictionary();

  public bool visitings_args = false;
  public bool return_statement_found = false;

  public FuncSymbol(WrappedNode n, string name, FuncType type, Scope parent) 
    : base(n, name, type, parent)
  {}

  public override OrderedDictionary GetMembers() { return members; }

  public FuncType GetFuncType()
  {
    return (FuncType)this.type.Get();
  }

  public OrderedDictionary GetArgs()
  {
    return args;
  }

  public void DefineArg(string name) 
  {
    var sym = (FuncArgSymbol)members[name];
    args.Add(name, sym);
  }

  public virtual ulong GetCallId() { return nname; }

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

  public new string GetName() 
  {
    return name;
  }
}

public class FuncDecl
{
  public AST_FuncDecl node;
  public FuncSymbol symbol;

  public FuncDecl(AST_FuncDecl node, FuncSymbol symbol)
  {
    this.node = node;
    this.symbol = symbol;
  }

  public bool IsLambda()
  {
    return node is AST_LambdaDecl;
  }

  public void AddUseParam(Symbol s, bool is_ref)
  {
    var up = AST_Util.New_UseParam(s.name, is_ref); 
    (node as AST_LambdaDecl).useparams.Add(up);
    symbol.define(s);
  }
}

public class LambdaSymbol : FuncSymbol
{
  public AST_LambdaDecl decl;

  List<FuncDecl> fdecl_stack;

  public LambdaSymbol(AST_LambdaDecl decl, List<FuncDecl> fdecl_stack, WrappedNode n, string name, TypeRef ret_type, Scope parent) 
    : base(n, name, new FuncType(ret_type), parent)
  {
    this.decl = decl;
    this.fdecl_stack = fdecl_stack;
#if BHL_FRONT
    var ft = GetFuncType();
    var ctx = (bhlParser.ExpLambdaContext)n.tree;
    var fparams = ctx.funcLambda().funcParams();
    if(fparams != null)
    {
      for(int i=0;i<fparams.varDeclare().Length;++i)
      {
        var vd = fparams.varDeclare()[i];
        ft.arg_types.Add(vd.type());
      }
    }
    ft.Update();
#endif
  }

  public override Symbol resolve(string name) 
  {
    var s = (Symbol)GetMembers()[name];
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

  Symbol ResolveUpvalue(string name)
  {
    int idx = FindMyIdxInStack();
    if(idx == -1)
      throw new Exception("Not found");

    for(int i=idx;i-- > 0;)
    {
      var decl = fdecl_stack[i];

      //NOTE: only variable symbols are considered
      var res = (Symbol)decl.symbol.GetMembers()[name] as VariableSymbol;

      if(res != null)
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
      if(fdecl_stack[i].symbol == this)
        return i;
    }
    return -1;
  }

  void DefineInStack(Symbol res, int from_idx, int to_idx)
  {
    //now let's put this result into all nested lambda scopes 
    for(int j=from_idx;j<=to_idx;++j)
    {
      var decl = fdecl_stack[j];
      if(!decl.IsLambda())
        continue;
      decl.AddUseParam(res, false/*not a ref*/);
    }
  }

  public override ulong GetCallId() { return decl.nname(); }
}

public class FuncSymbolAST : FuncSymbol
{
  public AST_FuncDecl decl;
#if BHL_FRONT
  //NOTE: storing fparams so it can be accessed later for misc things, e.g. default args
  public bhlParser.FuncParamsContext fparams;
#endif

  public FuncSymbolAST(AST_FuncDecl decl, WrappedNode n, string name, TypeRef ret_type, Scope parent
#if BHL_FRONT
      , bhlParser.FuncParamsContext fparams
#endif
      ) 
    : base(n, name, new FuncType(ret_type), parent)
  {
    this.decl = decl;
#if BHL_FRONT
    this.fparams = fparams;

    var ft = GetFuncType();
    if(fparams != null)
    {
      for(int i=0;i<fparams.varDeclare().Length;++i)
      {
        var vd = fparams.varDeclare()[i];
        ft.arg_types.Add(vd.type());
      }
    }
    ft.Update();
#endif
  }

  public override int GetTotalArgsNum() { return decl.GetTotalArgsNum(); }
  public override int GetDefaultArgsNum() { return decl.GetDefaultArgsNum(); }
  public override ulong GetCallId() { return decl.nname(); }
#if BHL_FRONT
  public override IParseTree GetDefaultArgsExprAt(int idx) 
  { 
    if(fparams == null)
      return null; 

    var vdecl = fparams.varDeclare()[idx];
    var vinit = vdecl.initVar(); 
    return vinit;
  }
#endif
}

public class FuncBindSymbol : FuncSymbol
{
  public Interpreter.FuncNodeCreator func_creator;

  public int def_args_num;

  public FuncBindSymbol(string name, TypeRef ret_type, Interpreter.FuncNodeCreator func_creator, int def_args_num = 0) 
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
  public SimpleFuncBindSymbol(string name, TypeRef ret_type, SimpleFunctorNode.Functor fn, int def_args_num = 0) 
    : base(name, ret_type, delegate() { return new SimpleFunctorNode(fn, name); }, def_args_num )
  {}
}

public class ConfNodeSymbol : FuncBindSymbol
{
  public Interpreter.ConfigGetter conf_getter;

  public ConfNodeSymbol(string name, TypeRef ret_type, Interpreter.FuncNodeCreator func_creator, Interpreter.ConfigGetter conf_getter) 
    : base(name, ret_type, func_creator)
  {
    this.conf_getter = conf_getter;
  }

  //NOTE: very controverse, but makes porting and usage much cleaner
  public override int GetDefaultArgsNum() { return 1; }
}

public class ClassSymbol : ScopedSymbol, Scope, Type 
{
  ClassSymbol super_class;
  public OrderedDictionary members = new OrderedDictionary();
  Dictionary<ulong, Symbol> hashed_symbols = new Dictionary<ulong, Symbol>();

  public Interpreter.ClassCreator creator;

  public ClassSymbol(WrappedNode n, string name, TypeRef super_class, Scope enclosing_scope, Interpreter.ClassCreator creator = null)
    : base(n, name, enclosing_scope)
  {
    this.super_class = (ClassSymbol)super_class.Get();
    this.creator = creator;
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

  public Symbol resolveMember(ulong nname)
  {
    Symbol sym;
    if(hashed_symbols.TryGetValue(nname, out sym))
      return sym;

    if(super_class != null)
      return super_class.resolveMember(nname);

    return null;
  }

  public override void define(Symbol sym) 
  {
    base.define(sym);

    hashed_symbols.Add(sym.nname, sym);
  }

  public int GetTypeIndex() { return SymbolTable.tUSER; }

  public override OrderedDictionary GetMembers() { return members; }

  public override string ToString() 
  {
    return "class "+name+":{"+
            string.Join(",", members.GetStringKeys().ToArray())+"}";
  }
}

public class ClassBindSymbol : ClassSymbol
{
  public ClassBindSymbol(string name, Interpreter.ClassCreator creator)
    : base(null, name, new TypeRef(), null, creator)
  {}

  public ClassBindSymbol(string name, string parent, Interpreter.ClassCreator creator)
    : base(null, name, new TypeRef(parent), null, creator)
  {}
}

public class EnumSymbol : ScopedSymbol, Scope, Type 
{
  Dictionary<uint, Symbol> hashed_symbols = new Dictionary<uint, Symbol>();
  public OrderedDictionary members = new OrderedDictionary();

  public EnumSymbol(WrappedNode n, string name, Scope enclosing_scope)
      : base(n, name, enclosing_scope)
  {}

  public int GetTypeIndex() { return SymbolTable.tENUM; }

  public override OrderedDictionary GetMembers() { return members; }

  public override void define(Symbol sym) 
  {
    base.define(sym);
    hashed_symbols.Add(sym.nname, sym);
  }

  public EnumItemSymbol findValue(uint nname)
  {
    Symbol sym;
    if(hashed_symbols.TryGetValue(nname, out sym))
      return sym as EnumItemSymbol;
    return null;
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

    //defining array symbols after built-in symbols were already defined
    foreach(Type t in indexToType) 
    {
      if(t != null) 
      {
        var blt = (BuiltInTypeSymbol)t; 
        globals.define(new ArrayTypeSymbol(globals, new TypeRef(blt)));
      }
    }

    {
      var fn = new FuncBindSymbol("RUNNING", "void",
        delegate() { return new AlwaysRunning(); } 
      );
      globals.define(fn);
    }

    {
      var fn = new FuncBindSymbol("YIELD", "void",
        delegate() { return new YieldOnce(); } 
      );
      globals.define(fn);
    }

    {
      var fn = new FuncBindSymbol("SUCCESS", "void",
        delegate() { return new AlwaysSuccess(); } 
      );

      globals.define(fn);
    }

    {
      var fn = new FuncBindSymbol("FAILURE", "void",
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
           IsCompatibleFuncTypes(valueType, destType) ||
           IsChildClass(valueType, destType)
           ;
  }

  static public bool IsCompatibleFuncTypes(Type a, Type b) 
  {
    var fa = a as FuncType;
    if(fa == null)
      return false;
    var fb = b as FuncType;
    if(fb == null)
      return false;
    return fa.IsCompatible(fb);
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

  //NOTE: version for bindings
  static public void CheckAssign(Type lhs, WrappedNode rhs) 
  {
    int tlhs = lhs.GetTypeIndex(); // promote right to left type?
    int trhs = rhs.eval_type.GetTypeIndex();
    rhs.promote_to_type = promoteFromTo[trhs,tlhs];
    if(!CanAssignTo(rhs.eval_type, lhs, rhs.promote_to_type)) 
    {
      throw new UserError(
        lhs.GetName()+", "+
        rhs.Location()+" have incompatible types"
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
    if((trhs == SymbolTable.tFLOAT || trhs == SymbolTable.tINT) && tlhs == SymbolTable.tENUM)
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

} //namespace bhl
