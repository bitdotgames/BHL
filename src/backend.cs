using System;
using System.Collections;
using System.Collections.Generic;
using fbhl;

namespace bhl {

public class Interpreter : AST_Visitor
{
  static Interpreter _instance;
  public static Interpreter instance 
  {
    get {
      if(_instance == null)
        _instance = new Interpreter();
      return _instance;
    }
  }

  public class ReturnException : Exception {}
  public class BreakException : Exception {}
  //not supported
  //public class ContinueException : Exception {}

  public delegate void ClassCreator(ref DynVal res);
  public delegate void FieldGetter(DynVal v, ref DynVal res);
  public delegate void FieldSetter(ref DynVal v, DynVal nv);
  public delegate void FieldRef(DynVal v, out DynVal res);
  public delegate BehaviorTreeNode FuncNodeCreator(); 

  FastStack<BehaviorTreeInternalNode> node_stack = new FastStack<BehaviorTreeInternalNode>(128);
  BehaviorTreeInternalNode curr_node;

  public DynVal last_member_ctx;

  FastStack<DynValDict> mstack = new FastStack<DynValDict>(128);
  DynValDict curr_mem;
  public DynValDict glob_mem = new DynValDict();

  public struct JsonCtx
  {
    public const int OBJ = 1;
    public const int ARR = 2;

    public int type;
    public uint scope_type;
    public uint name_or_idx;

    public JsonCtx(int type, uint scope_type, uint name_or_idx)
    {
      this.type = type;
      this.scope_type = scope_type;
      this.name_or_idx = name_or_idx;
    }
  }
  FastStack<JsonCtx> jcts = new FastStack<JsonCtx>(128);

  public IModuleLoader module_loader;
  //NOTE: key is a module id, value is a file path
  public Dictionary<ulong,string> loaded_modules = new Dictionary<ulong,string>();

  public BaseScope symbols;

  public FastStack<DynVal> stack = new FastStack<DynVal>(256);
  public FastStack<AST_Call> call_stack = new FastStack<AST_Call>(128);
  public FastStack<fbhl.AST_Call> call_fstack = new FastStack<fbhl.AST_Call>(128);

  public void Init(BaseScope symbols, IModuleLoader module_loader)
  {
    node_stack.Clear();
    curr_node = null;
    last_member_ctx = null;
    mstack.Clear();
    glob_mem.Clear();
    curr_mem = null;
    loaded_modules.Clear();
    stack.Clear();
    call_stack.Clear();
    call_fstack.Clear();

    this.symbols = symbols;
    this.module_loader = module_loader;
  }

  public struct Result
  {
    public BHS status;
    public DynVal[] vals;

    public DynVal val 
    {
      get {
        return vals != null ? vals[0] : null;
      }
    }
  }

  public Result ExecNode(BehaviorTreeNode node, int ret_vals = 1, bool keep_running = true)
  {
    Result res = new Result();

    res.status = BHS.NONE;
    while(true)
    {
      res.status = node.run();
      if(res.status != BHS.RUNNING)
        break;

      if(!keep_running)
        break;
    }
    if(ret_vals > 0)
    {
      res.vals = new DynVal[ret_vals];
      for(int i=0;i<ret_vals;++i)
        res.vals[i] = PopValue();
    }
    else
      res.vals = null;
    return res;
  }

  public void Interpret(AST_Module ast)
  {
    Visit(ast);
  }

  public void Interpret(fbhl.AST_Module ast)
  {
    Visit(ast);
  }

  public void LoadModule(HashedName mod_id)
  {
    if(mod_id.n == 0)
      return;

    if(loaded_modules.ContainsKey(mod_id.n))
      return;

    if(module_loader == null)
      throw new Exception("Module loader is not set");

    var mod_ast = module_loader.LoadModule(mod_id);
    if(mod_ast == null)
      throw new Exception("Could not load module: " + mod_id);

    loaded_modules.Add(mod_id.n, mod_ast.name);

    Interpret(mod_ast);
  }

  public void LoadModule(string module)
  {
    LoadModule(Util.GetModuleId(module));
  }

  public FuncSymbol ResolveFuncSymbol(AST_Call ast, fbhl.AST_Call fast = default(fbhl.AST_Call))
  {
    if(ast != null)
      return ResolveFuncSymbol(ast.type, ast.Name(), ast.scope_ntype);
    else
      return ResolveFuncSymbol((EnumCall)fast.Type, fast.Name(), fast.ScopeNtype);
  }

  FuncSymbol ResolveFuncSymbol(EnumCall type, HashedName name, uint scope_ntype)
  {
    if(type == EnumCall.FUNC)
      return symbols.resolve(name) as FuncSymbol;
    else if(type == EnumCall.MFUNC)
      return ResolveClassMember(scope_ntype, name) as FuncSymbol;
    else
      return null;
  }

  public FuncNode GetFuncNode(FuncSymbol symb)
  {
    var fsast = symb as FuncSymbolAST;
    if(fsast != null)
      return new FuncNodeAST(fsast.decl, fsast.fdecl, null);
    else if(symb is FuncBindSymbol)
      return new FuncNodeBind(symb as FuncBindSymbol, null);
    else
      throw new Exception("Bad func call type");
  }

  public FuncNode GetFuncNode(AST_Call ast, fbhl.AST_Call fast = default(fbhl.AST_Call))
  {
    var symb = ResolveFuncSymbol(ast, fast);
    return GetFuncNode(symb);
  }

  public FuncNode GetFuncNode(HashedName name)
  {
    var symb = symbols.resolve(name) as FuncSymbol;
    return GetFuncNode(symb);
  }

  public FuncNode GetFuncNode(string module_name, string func_name)
  {
    LoadModule(module_name);
    return GetFuncNode(Util.GetFuncId(module_name, func_name));
  }

  Symbol ResolveClassMember(HashedName class_type, HashedName name)
  {
    var cl = symbols.resolve(class_type) as ClassSymbol;
    if(cl == null)
      throw new Exception("Class binding not found: " + class_type); 

    var cl_member = cl.ResolveMember(name);
    if(cl_member == null)
      throw new Exception("Member not found: " + name);
    return cl_member;
  }

  //NOTE: usually used in symbols
  public FuncArgsInfo GetFuncArgsInfo()
  {
    if(call_stack.Count > 0)
      return new FuncArgsInfo(call_stack.Peek().cargs_bits);
    else
      return new FuncArgsInfo(call_fstack.Peek().CargsBits);
  }

  //NOTE: caching exceptions for less allocations
  static ReturnException return_exception = new ReturnException();
  static BreakException break_exception = new BreakException();

  public void JumpReturn()
  {
    throw return_exception;
  }

  public void JumpBreak()
  {
    throw break_exception;
  }

  //TODO: implement some day
  //public void JumpContinue()
  //{
  //  throw new ContinueException();
  //}

  public void PushScope(DynValDict mem)
  {
    mstack.Push(mem);
    curr_mem = mem;
  }

  public void PopScope()
  {
    mstack.Pop();
    curr_mem = mstack.Count > 0 ? mstack.Peek() : null;
  }

  public void SetScopeValue(HashedName name, DynVal val)
  {
    curr_mem.Set(name, val);
  }

  public DynVal GetScopeValue(HashedName name)
  {
    DynVal val;
    bool ok = curr_mem.TryGet(name, out val);
    //NOTE: trying glob_mem if not found
    if(!ok)
      ok = glob_mem.TryGet(name, out val);
    if(!ok)
      throw new Exception("No such variable " + name + " in scope");
    return val;
  }

  public DynVal TryGetScopeValue(HashedName name)
  {
    DynVal val;
    bool ok = curr_mem.TryGet(name, out val);
    //NOTE: trying glob_mem if not found
    if(!ok)
      glob_mem.TryGet(name, out val);
    return val;
  }

  public void PushNode(BehaviorTreeInternalNode node, bool attach_as_child = true)
  {
    node_stack.Push(node);
    if(curr_node != null && attach_as_child)
      curr_node.addChild(node);
    curr_node = node;
  }

  public BehaviorTreeInternalNode PopNode()
  {
    var node = node_stack.Pop();
    curr_node = (node_stack.Count > 0 ? node_stack.Peek() : null); 
    return node;
  }

  public void PushValue(DynVal v)
  {
    v.RefMod(RefOp.INC | RefOp.USR_INC);
    stack.Push(v);
  }

  public DynVal PopValue()
  {
    var v = stack.PopFast();
    v.RefMod(RefOp.USR_DEC_NO_DEL | RefOp.DEC);
    return v;
  }

  public DynVal PopValueNoDel()
  {
    var v = stack.PopFast();
    v.RefMod(RefOp.USR_DEC_NO_DEL | RefOp.DEC_NO_DEL);
    return v;
  }

  public DynVal PopRef()
  {
    return PopValueNoDel();
  }

  public void PopValues(int amount)
  {
    for(int i=0;i<amount;++i)
      PopValue();
  }

  public bool PeekValue(ref DynVal res)
  {
    if(stack.Count == 0)
      return false;

    res = stack.Peek();
    return true;
  }

  public DynVal PeekValue()
  {
    return stack.Peek();
  }

  void CheckFuncIsUnique(HashedName name)
  {
    var s = symbols.resolve(name) as FuncSymbol;
    if(s != null)
      throw new Exception("Function is already defined: " + name);
  }

  void CheckNameIsUnique(HashedName name)
  {
    var s = symbols.resolve(name);
    var cs = s as ClassSymbol;
    if(cs != null)
      throw new Exception("Class is already defined: " + name);
    var es = s as EnumSymbol;
    if(es != null)
      throw new Exception("Enum is already defined: " + name);
  }

  ///////////////////////////////////////////////////////////

  public void Visit(fbhl.AST_Interim ast)
  {
    for(int c=0;c<ast.ChildrenLength;++c)
      Visit(ast.Children(c));
  }

  public void Visit(fbhl.AST_Module ast)
  {
    PushScope(glob_mem);

    var g = new GroupNode();
    PushNode(g, attach_as_child: false);
    for(int c=0;c<ast.ChildrenLength;++c)
      Visit(ast.Children(c));
    PopNode();

    //NOTE: we need to run it for globals initialization
    var status = g.run();
    if(status != BHS.SUCCESS)
      throw new Exception("Global initialization error: " + status);

    PopScope();
  }

  public void Visit(fbhl.AST_Import ast)
  {
    for(int i=0;i<ast.ModulesLength;++i)
      LoadModule(ast.Modules(i));
  }

  public void Visit(fbhl.AST_FuncDecl ast)
  {
    var name = ast.Name; 
    //regular func
    if(ast.Lambda == null)
    {
      CheckFuncIsUnique(name);
      var fn = new FuncSymbolAST(symbols, ast);
      symbols.define(fn);
    }
    //lambda
    else
    {
      var lmb = symbols.resolve(name) as LambdaSymbol;
      if(lmb == null)
      {
        CheckFuncIsUnique(name);
        lmb = new LambdaSymbol(symbols, ast);
        symbols.define(lmb);
      }

      curr_node.addChild(new PushFuncCtxNode(lmb));
    }
  }

  public void Visit(fbhl.AST_EnumDecl ast)
  {
    var name = ast.Name();
    CheckNameIsUnique(name);

    var es = new EnumSymbolAST(name);
    symbols.define(es);
  }

  public void Visit(fbhl.AST_Block ast)
  {
    var type = ast.Type;

    if(type == fbhl.EnumBlock.FUNC)
    {
      if(!(curr_node is FuncNode))
        throw new Exception("Current node is not a func");
      for(int c=0;c<ast.ChildrenLength;++c)
        Visit(ast.Children(c));
    }
    else if(type == fbhl.EnumBlock.SEQ)
    {
      PushNode(new SequentialNode());
      for(int c=0;c<ast.ChildrenLength;++c)
        Visit(ast.Children(c));
      PopNode();
    }
    else if(type == fbhl.EnumBlock.GROUP)
    {
      PushNode(new GroupNode());
      for(int c=0;c<ast.ChildrenLength;++c)
        Visit(ast.Children(c));
      PopNode();
    }
    else if(type == fbhl.EnumBlock.SEQ_)
    {
      PushNode(new SequentialNode_());
      for(int c=0;c<ast.ChildrenLength;++c)
        Visit(ast.Children(c));
      PopNode();
    }
    else if(type == fbhl.EnumBlock.PARAL)
    {
      PushNode(new ParallelNode());
      for(int c=0;c<ast.ChildrenLength;++c)
        Visit(ast.Children(c));
      PopNode();
    }
    else if(type == fbhl.EnumBlock.PARAL_ALL)
    {
      PushNode(new ParallelAllNode());
      for(int c=0;c<ast.ChildrenLength;++c)
        Visit(ast.Children(c));
      PopNode();
    }
    else if(type == fbhl.EnumBlock.UNTIL_FAILURE)
    {
      PushNode(new MonitorFailureNode(BHS.FAILURE));
      for(int c=0;c<ast.ChildrenLength;++c)
        Visit(ast.Children(c));
      PopNode();
    }
    else if(type == fbhl.EnumBlock.UNTIL_FAILURE_)
    {
      PushNode(new MonitorFailureNode(BHS.SUCCESS));
      for(int c=0;c<ast.ChildrenLength;++c)
        Visit(ast.Children(c));
      PopNode();
    }
    else if(type == fbhl.EnumBlock.UNTIL_SUCCESS)
    {
      PushNode(new MonitorSuccessNode());
      for(int c=0;c<ast.ChildrenLength;++c)
        Visit(ast.Children(c));
      PopNode();
    }
    else if(type == fbhl.EnumBlock.NOT)
    {
      PushNode(new InvertNode());
      for(int c=0;c<ast.ChildrenLength;++c)
        Visit(ast.Children(c));
      PopNode();
    }
    else if(type == fbhl.EnumBlock.PRIO)
    {
      PushNode(new PriorityNode());
      for(int c=0;c<ast.ChildrenLength;++c)
        Visit(ast.Children(c));
      PopNode();
    }
    else if(type == fbhl.EnumBlock.FOREVER)
    {
      PushNode(new ForeverNode());
      for(int c=0;c<ast.ChildrenLength;++c)
        Visit(ast.Children(c));
      PopNode();
    }
    else if(type == fbhl.EnumBlock.DEFER)
    {
      PushNode(new DeferNode());
      for(int c=0;c<ast.ChildrenLength;++c)
        Visit(ast.Children(c));
      PopNode();
    }
    else if(type == fbhl.EnumBlock.IF)
    {
      PushNode(new IfNode());
      for(int c=0;c<ast.ChildrenLength;++c)
        Visit(ast.Children(c));
      PopNode();
    }
    else if(type == fbhl.EnumBlock.WHILE)
    {
      PushNode(new LoopNode());
      for(int c=0;c<ast.ChildrenLength;++c)
        Visit(ast.Children(c));
      PopNode();
    }
    else if(type == fbhl.EnumBlock.EVAL)
    {
      PushNode(new EvalNode());
      for(int c=0;c<ast.ChildrenLength;++c)
        Visit(ast.Children(c));
      PopNode();
    }
    else
      throw new Exception("Unknown block type: " + type);
  }

  public void Visit(fbhl.AST_LiteralNum ast)
  {
    curr_node.addChild(new LiteralNumNode(ast.Nval));
  }

  public void Visit(fbhl.AST_LiteralStr ast)
  {
    curr_node.addChild(new LiteralStrNode(ast.Sval));
  }

  public void Visit(fbhl.AST_LiteralBool ast)
  {
    curr_node.addChild(new LiteralBoolNode(ast.Bval));
  }

  public void Visit(fbhl.AST_LiteralNil ast)
  {
    curr_node.addChild(new LiteralNilNode());
  }

  public void Visit(fbhl.AST_VarDecl ast)
  {
    //NOTE: expression comes first
    for(int c=0;c<ast.ChildrenLength;++c)
      Visit(ast.Children(c));
    curr_node.addChild(new VarAccessNode(ast.Name(), VarAccessNode.DECL));
  }

  public void Visit(fbhl.AST_Call ast)
  {           
    var type = ast.Type;

    if(type == fbhl.EnumCall.VAR)
    {
      curr_node.addChild(new VarAccessNode(ast.Name()));
    }
    else if(type == fbhl.EnumCall.VARW)
    {
      curr_node.addChild(new VarAccessNode(ast.Name(), VarAccessNode.WRITE));
    }
    else if(type == fbhl.EnumCall.MVAR)
    {
      curr_node.addChild(new MVarAccessNode(ast.ScopeNtype, ast.Name()));
    }
    else if(type == fbhl.EnumCall.MVARW)
    {
      curr_node.addChild(new MVarAccessNode(ast.ScopeNtype, ast.Name(), MVarAccessNode.WRITE));
    }
    else if(type == fbhl.EnumCall.MVARREF)
    {
      curr_node.addChild(new MVarAccessNode(ast.ScopeNtype, ast.Name(), MVarAccessNode.READ_REF));
    }
    else if(type == fbhl.EnumCall.FUNC || type == fbhl.EnumCall.MFUNC)
    {
      AddFuncCallNode(null, ast);
    }
    else if(type == fbhl.EnumCall.FUNC_PTR || type == fbhl.EnumCall.FUNC_PTR_POP)
    {
      curr_node.addChild(new CallFuncPtr(null, ast));
    }
    else if(type == fbhl.EnumCall.FUNC2VAR)
    {
      var s = symbols.resolve(ast.nname()) as FuncSymbol;
      if(s == null)
        throw new Exception("Could not find func:" + ast.Name());
      curr_node.addChild(new PushFuncCtxNode(s));
    }
    else if(type == fbhl.EnumCall.ARR_IDX)
    {
      var bnd = symbols.resolve(ast.ScopeNtype) as ArrayTypeSymbol;
      if(bnd == null)
        throw new Exception("Could not find class binding: " + ast.ScopeNtype);

      curr_node.addChild(bnd.Create_At());
    }
    else if(type == fbhl.EnumCall.ARR_IDXW)
    {
      var bnd = symbols.resolve(ast.ScopeNtype) as ArrayTypeSymbol;
      if(bnd == null)
        throw new Exception("Could not find class binding: " + ast.ScopeNtype);

      curr_node.addChild(bnd.Create_SetAt());
    }
    else 
      throw new Exception("Unsupported call type: " + type);
  }

  public void Visit(fbhl.AST_Return ast)
  {
    //NOTE: expression comes first
    for(int c=0;c<ast.ChildrenLength;++c)
      Visit(ast.Children(c));
    curr_node.addChild(new ReturnNode());
  }

  public void Visit(fbhl.AST_BinaryOpExp ast)
  {
    //NOTE: checking if it's a short circuit expression
    var type = (EnumBinaryOp)ast.Type;
    if(type == EnumBinaryOp.AND || type == EnumBinaryOp.OR)
    {
      PushNode(new LogicOpNode(type));
        PushNode(new GroupNode());
        Visit(ast.Children(0));
        PopNode();
        PushNode(new GroupNode());
        Visit(ast.Children(1));
        PopNode();
      PopNode();
    }
    else
    {
      //NOTE: expression comes first
      for(int c=0;c<ast.ChildrenLength;++c)
        Visit(ast.Children(c));
      curr_node.addChild(new BinaryOpNode(type));
    }
  }

  public void Visit(fbhl.AST_TypeCast ast)
  {
    for(int c=0;c<ast.ChildrenLength;++c)
      Visit(ast.Children(c));
    curr_node.addChild(new TypeCastNode(ast.Ntype));
  }

  public void Visit(fbhl.AST_New ast)
  {
    curr_node.addChild(new ConstructNode(ast.Name()));
  }

  public void Visit(fbhl.AST_ClassDecl ast)
  {
    var name = ast.Name();
    CheckNameIsUnique(name);

    var parent = symbols.resolve(ast.ParentName()) as ClassSymbol;

    var cl = new ClassSymbolAST(name, parent);
    symbols.define(cl);

    for(int i=0;i<ast.ChildrenLength;++i)
    {
      var child = ast.Children(i);
      var vd = child.Value.V<fbhl.AST_VarDecl>();
      if(vd != null)
      {
        cl.define(new FieldSymbolAST(vd.Value.Name, vd.Value.Ntype));
      }
    }
  }

  public void Visit(fbhl.AST_JsonObj ast)
  {
    curr_node.addChild(new ConstructNode(ast.Ntype));

    for(int c=0;c<ast.ChildrenLength;++c)
      Visit(ast.Children(c));
  }

  public void Visit(fbhl.AST_JsonArr ast)
  {
    var bnd = symbols.resolve(ast.Ntype) as ArrayTypeSymbol;
    if(bnd == null)
      throw new Exception("Could not find class binding: " + ast.Ntype);

    curr_node.addChild(bnd.Create_New());

    for(int i=0;i<ast.ChildrenLength;++i)
    {
      var c = ast.Children(i);

      //checking if there's an explicit add to array operand
      if(c.Value.VType == fbhl.AST_OneOf.AST_JsonArrAddItem)
      {
        var n = (Array_AddNode)bnd.Create_Add(); 
        n.push_arr = true;
        curr_node.addChild(n);
      }
      else
      {
        var jc = new JsonCtx(JsonCtx.ARR, ast.Ntype, (uint)i);
        jcts.Push(jc);
        Visit(c);
        jcts.Pop();
      }
    }

    //adding last item item
    if(ast.ChildrenLength > 0)
    {
      var n = (Array_AddNode)bnd.Create_Add(); 
      n.push_arr = true;
      curr_node.addChild(n);
    }
  }

  public void Visit(fbhl.AST_JsonPair ast)
  {
    var jc = new JsonCtx(JsonCtx.OBJ, ast.ScopeNtype, ast.Nname);
    jcts.Push(jc);

    for(int c=0;c<ast.ChildrenLength;++c)
      Visit(ast.Children(c));
    curr_node.addChild(new MVarAccessNode(ast.ScopeNtype, ast.Name(), MVarAccessNode.WRITE_PUSH_CTX));

    jcts.Pop();
  }

  public void Visit(fbhl.AST_Selector? sel)
  {
    if(sel == null)
      return;

    switch(sel.Value.VType)
    {
      case fbhl.AST_OneOf.AST_Interim:
        Visit(sel.Value.V<fbhl.AST_Interim>().Value);
        break;
      case fbhl.AST_OneOf.AST_Block:
        Visit(sel.Value.V<fbhl.AST_Block>().Value);
        break;
      case fbhl.AST_OneOf.AST_Import:
        Visit(sel.Value.V<fbhl.AST_Import>().Value);
        break;
      case fbhl.AST_OneOf.AST_Call:
        Visit(sel.Value.V<fbhl.AST_Call>().Value);
        break;
      case fbhl.AST_OneOf.AST_VarDecl:
        Visit(sel.Value.V<fbhl.AST_VarDecl>().Value);
        break;
      case fbhl.AST_OneOf.AST_FuncDecl:
        Visit(sel.Value.V<fbhl.AST_FuncDecl>().Value);
        break;
      case fbhl.AST_OneOf.AST_ClassDecl:
        Visit(sel.Value.V<fbhl.AST_ClassDecl>().Value);
        break;
      case fbhl.AST_OneOf.AST_EnumDecl:
        Visit(sel.Value.V<fbhl.AST_EnumDecl>().Value);
        break;
      case fbhl.AST_OneOf.AST_LiteralNum:
        Visit(sel.Value.V<fbhl.AST_LiteralNum>().Value);
        break;
      case fbhl.AST_OneOf.AST_LiteralStr:
        Visit(sel.Value.V<fbhl.AST_LiteralStr>().Value);
        break;
      case fbhl.AST_OneOf.AST_LiteralBool:
        Visit(sel.Value.V<fbhl.AST_LiteralBool>().Value);
        break;
      case fbhl.AST_OneOf.AST_LiteralNil:
        Visit(sel.Value.V<fbhl.AST_LiteralNil>().Value);
        break;
      case fbhl.AST_OneOf.AST_Return:
        Visit(sel.Value.V<fbhl.AST_Return>().Value);
        break;
      case fbhl.AST_OneOf.AST_BinaryOpExp:
        Visit(sel.Value.V<fbhl.AST_BinaryOpExp>().Value);
        break;
      case fbhl.AST_OneOf.AST_New:
        Visit(sel.Value.V<fbhl.AST_New>().Value);
        break;
      case fbhl.AST_OneOf.AST_TypeCast:
        Visit(sel.Value.V<fbhl.AST_TypeCast>().Value);
        break;
      case fbhl.AST_OneOf.AST_JsonObj:
        Visit(sel.Value.V<fbhl.AST_JsonObj>().Value);
        break;
      case fbhl.AST_OneOf.AST_JsonArr:
        Visit(sel.Value.V<fbhl.AST_JsonArr>().Value);
        break;
      case fbhl.AST_OneOf.AST_JsonPair:
        Visit(sel.Value.V<fbhl.AST_JsonPair>().Value);
        break;
      default:
        throw new Exception("Not handled type: " + sel.Value.VType);
    }
  }

  ///////////////////////////////////////////////////////////

  public override void DoVisit(AST_Interim ast)
  {
    VisitChildren(ast);
  }

  public override void DoVisit(AST_Module ast)
  {
    PushScope(glob_mem);

    var g = new GroupNode();
    PushNode(g, attach_as_child: false);
    VisitChildren(ast);
    PopNode();

    //NOTE: we need to run it for globals initialization
    var status = g.run();
    if(status != BHS.SUCCESS)
      throw new Exception("Global initialization error: " + status);

    PopScope();
  }

  public override void DoVisit(AST_Import ast)
  {
    for(int i=0;i<ast.modules.Count;++i)
      LoadModule(ast.modules[i]);
  }

  public override void DoVisit(AST_FuncDecl ast)
  {
    var name = ast.Name(); 
    CheckFuncIsUnique(name);

    var fn = new FuncSymbolAST(symbols, ast);
    symbols.define(fn);
  }

  public override void DoVisit(AST_LambdaDecl ast)
  {
    //if there's such a lambda symbol already we re-use it
    var name = ast.Name(); 
    var lmb = symbols.resolve(name) as LambdaSymbol;
    if(lmb == null)
    {
      CheckFuncIsUnique(name);
      lmb = new LambdaSymbol(symbols, ast);
      symbols.define(lmb);
    }

    curr_node.addChild(new PushFuncCtxNode(lmb));
  }

  public override void DoVisit(AST_ClassDecl ast)
  {
    var name = ast.Name();
    CheckNameIsUnique(name);

    var parent = symbols.resolve(ast.ParentName()) as ClassSymbol;

    var cl = new ClassSymbolAST(name, parent);
    symbols.define(cl);

    for(int i=0;i<ast.children.Count;++i)
    {
      var child = ast.children[i];
      var vd = child as AST_VarDecl;
      if(vd != null)
      {
        cl.define(new FieldSymbolAST(vd.name, vd.ntype));
      }
    }
  }

  public override void DoVisit(AST_EnumDecl ast)
  {
    var name = ast.Name();
    CheckNameIsUnique(name);

    var es = new EnumSymbolAST(name);
    symbols.define(es);
  }

  public override void DoVisit(AST_EnumItem ast)
  {}

  public override void DoVisit(AST_Block ast)
  {
    if(ast.type == EnumBlock.FUNC)
    {
      if(!(curr_node is FuncNode))
        throw new Exception("Current node is not a func");
      VisitChildren(ast);
    }
    else if(ast.type == EnumBlock.SEQ)
    {
      PushNode(new SequentialNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.GROUP)
    {
      PushNode(new GroupNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.SEQ_)
    {
      PushNode(new SequentialNode_());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.PARAL)
    {
      PushNode(new ParallelNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.PARAL_ALL)
    {
      PushNode(new ParallelAllNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.UNTIL_FAILURE)
    {
      PushNode(new MonitorFailureNode(BHS.FAILURE));
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.UNTIL_FAILURE_)
    {
      PushNode(new MonitorFailureNode(BHS.SUCCESS));
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.UNTIL_SUCCESS)
    {
      PushNode(new MonitorSuccessNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.NOT)
    {
      PushNode(new InvertNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.PRIO)
    {
      PushNode(new PriorityNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.FOREVER)
    {
      PushNode(new ForeverNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.DEFER)
    {
      PushNode(new DeferNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.IF)
    {
      PushNode(new IfNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.WHILE)
    {
      PushNode(new LoopNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.EVAL)
    {
      PushNode(new EvalNode());
      VisitChildren(ast);
      PopNode();
    }
    else
      throw new Exception("Unknown block type: " + ast.type);
  }

  public override void DoVisit(AST_TypeCast ast)
  {
    VisitChildren(ast);
    curr_node.addChild(new TypeCastNode(ast.ntype));
  }

  public override void DoVisit(AST_New ast)
  {
    curr_node.addChild(new ConstructNode(ast.Name()));
  }

  public override void DoVisit(AST_Inc ast)
  {
    curr_node.addChild(new IncNode(ast.nname));
  }

  public override void DoVisit(AST_Call ast)
  {           
    if(ast.type == EnumCall.VAR)
    {
      curr_node.addChild(new VarAccessNode(ast.Name()));
    }
    else if(ast.type == EnumCall.VARW)
    {
      curr_node.addChild(new VarAccessNode(ast.Name(), VarAccessNode.WRITE));
    }
    else if(ast.type == EnumCall.MVAR)
    {
      curr_node.addChild(new MVarAccessNode(ast.scope_ntype, ast.Name()));
    }
    else if(ast.type == EnumCall.MVARW)
    {
      curr_node.addChild(new MVarAccessNode(ast.scope_ntype, ast.Name(), MVarAccessNode.WRITE));
    }
    else if(ast.type == EnumCall.MVARREF)
    {
      curr_node.addChild(new MVarAccessNode(ast.scope_ntype, ast.Name(), MVarAccessNode.READ_REF));
    }
    else if(ast.type == EnumCall.FUNC || ast.type == EnumCall.MFUNC)
    {
      AddFuncCallNode(ast);
    }
    else if(ast.type == EnumCall.FUNC_PTR || ast.type == EnumCall.FUNC_PTR_POP)
    {
      curr_node.addChild(new CallFuncPtr(ast));
    }
    else if(ast.type == EnumCall.FUNC2VAR)
    {
      var s = symbols.resolve(ast.nname()) as FuncSymbol;
      if(s == null)
        throw new Exception("Could not find func:" + ast.Name());
      curr_node.addChild(new PushFuncCtxNode(s));
    }
    else if(ast.type == EnumCall.ARR_IDX)
    {
      var bnd = symbols.resolve(ast.scope_ntype) as ArrayTypeSymbol;
      if(bnd == null)
        throw new Exception("Could not find class binding: " + ast.scope_ntype);

      curr_node.addChild(bnd.Create_At());
    }
    else if(ast.type == EnumCall.ARR_IDXW)
    {
      var bnd = symbols.resolve(ast.scope_ntype) as ArrayTypeSymbol;
      if(bnd == null)
        throw new Exception("Could not find class binding: " + ast.scope_ntype);

      curr_node.addChild(bnd.Create_SetAt());
    }
    else 
      throw new Exception("Unsupported call type: " + ast.type);
  }

  void AddFuncCallNode(AST_Call ast, fbhl.AST_Call fast = default(fbhl.AST_Call))
  {
    var symb = ResolveFuncSymbol(ast, fast);
    var fbind_symb = symb as FuncBindSymbol;

    //special case if it's bind symbol
    if(fbind_symb != null)
    {
      bool has_args = false;
      if(ast != null)
        has_args = ast.cargs_bits > 0 || fbind_symb.def_args_num > 0;
      else
        has_args = fast.CargsBits > 0 || fbind_symb.def_args_num > 0;

      if(has_args)
        curr_node.addChild(new FuncBindCallNode(ast, fast));
      //special case if it's a simple bind symbol call without any args
      else
        curr_node.addChild(fbind_symb.func_creator());
    }
    else
    {
      curr_node.addChild(new FuncCallNode(ast, fast));  
    }
  }

  public override void DoVisit(AST_Return ast)
  {
    //NOTE: expression comes first
    VisitChildren(ast);
    curr_node.addChild(new ReturnNode());
  }

  public override void DoVisit(AST_Break ast)
  {
    curr_node.addChild(new BreakNode());
  }

  public override void DoVisit(AST_PopValue ast)
  {
    curr_node.addChild(new PopValueNode());
  }

  public override void DoVisit(AST_Literal ast)
  {
    curr_node.addChild(new LiteralNode(ast));
  }

  public override void DoVisit(AST_BinaryOpExp ast)
  {
    //NOTE: checking if it's a short circuit expression
    if(ast.type == EnumBinaryOp.AND || ast.type == EnumBinaryOp.OR)
    {
      PushNode(new LogicOpNode(ast.type));
        PushNode(new GroupNode());
        Visit(ast.children[0]);
        PopNode();
        PushNode(new GroupNode());
        Visit(ast.children[1]);
        PopNode();
      PopNode();
    }
    else
    {
      //NOTE: expression comes first
      VisitChildren(ast);
      curr_node.addChild(new BinaryOpNode(ast.type));
    }
  }

  public override void DoVisit(AST_UnaryOpExp ast)
  {
    //NOTE: expression comes first
    VisitChildren(ast);
    curr_node.addChild(new UnaryOpNode(ast.type));
  }

  public override void DoVisit(AST_VarDecl ast)
  {
    //NOTE: expression comes first
    VisitChildren(ast);
    curr_node.addChild(new VarAccessNode(ast.Name(), VarAccessNode.DECL));
  }

  public override void DoVisit(AST_JsonObj ast)
  {
    curr_node.addChild(new ConstructNode(new HashedName(ast.ntype)));

    VisitChildren(ast);
  }

  public override void DoVisit(AST_JsonArr ast)
  {
    var bnd = symbols.resolve(ast.ntype) as ArrayTypeSymbol;
    if(bnd == null)
      throw new Exception("Could not find class binding: " + ast.ntype);

    curr_node.addChild(bnd.Create_New());

    for(int i=0;i<ast.children.Count;++i)
    {
      var c = ast.children[i];

      //checking if there's an explicit add to array operand
      if(c is AST_JsonArrAddItem)
      {
        var n = (Array_AddNode)bnd.Create_Add(); 
        n.push_arr = true;
        curr_node.addChild(n);
      }
      else
      {
        var jc = new JsonCtx(JsonCtx.ARR, ast.ntype, (uint)i);
        jcts.Push(jc);
        Visit(c);
        jcts.Pop();
      }
    }

    //adding last item item
    if(ast.children.Count > 0)
    {
      var n = (Array_AddNode)bnd.Create_Add(); 
      n.push_arr = true;
      curr_node.addChild(n);
    }
  }
  
  //NOTE: just a dummy, it's actually processed during AST_JsonArr's traversal
  public override void DoVisit(AST_JsonArrAddItem ast)
  {}

  public override void DoVisit(AST_JsonPair ast)
  {
    var jc = new JsonCtx(JsonCtx.OBJ, ast.scope_ntype, ast.nname);
    jcts.Push(jc);

    VisitChildren(ast);
    curr_node.addChild(new MVarAccessNode(ast.scope_ntype, ast.Name(), MVarAccessNode.WRITE_PUSH_CTX));

    jcts.Pop();
  }
}

//NOTE: this class represents sort of a 'pointer' to the 
//      function and it also stores captured variables from
//      the enclosing context
public class FuncCtx : DynValRefcounted
{
  //NOTE: -1 means it's in released state,
  //      public only for inspection
  public int refs;

  public FuncSymbol fs;
  //NOTE: this memory scope is used for 'use' variables, it's then
  //      copied to concrete memory scope of the function.
  public DynValDict mem = new DynValDict();
  public FuncNode fnode;
  public bool fnode_busy;

  private FuncCtx(FuncSymbol fs)
  {
    this.fs = fs;
  }

  public FuncNode EnsureNode()
  {
    fnode_busy = true;

    if(fnode != null)
      return fnode;

    ++nodes_created;

    var fast = fs as FuncSymbolAST;
    if(fast != null)
      fnode = new FuncNodeAST(fast.decl, fast.fdecl, this);
    else if(fs is LambdaSymbol)
      fnode = new FuncNodeLambda(this);
    else if(fs is FuncBindSymbol)
      fnode = new FuncNodeBind(fs as FuncBindSymbol, this);
    else
      throw new Exception("Unknown symbol type");

    return fnode;
  }

  public FuncCtx Clone()
  {
    if(refs == -1)
      throw new Exception("Invalid state");

    var dup = FuncCtx.New(fs);

    //NOTE: need to properly set use params
    if(fs is LambdaSymbol)
    {
      var ldecl = (fs as LambdaSymbol).decl as AST_LambdaDecl;
      for(int i=0;i<ldecl.useparams.Count;++i)
      {
        var up = ldecl.useparams[i];
        var val = mem.Get(up.Name());
        dup.mem.Set(up.Name(), up.IsRef() ? val : val.ValueClone());
      }
    }

    return dup;
  }

  public FuncCtx AutoClone()
  {
    if(fnode_busy)
      return Clone(); 
    return this;
  }

  public void Retain()
  {
    if(refs == -1)
      throw new Exception("Invalid state");
    ++refs;

    //Console.WriteLine("FREF INC: " + refs + " " + this.GetHashCode() + " " + Environment.StackTrace);
  }

  public void Release(bool can_del = true)
  {
    if(refs == -1)
      throw new Exception("Invalid state");
    if(refs == 0)
      throw new Exception("Double free");

    --refs;

    //Console.WriteLine("FREF DEC: " + refs + " " + this.GetHashCode() + " " + Environment.StackTrace);

    if(can_del)
      TryDel();
  }

  public bool TryDel()
  {
    if(refs != 0)
      return false;
    
    Del(this);
    return true;
  }

  //////////////////////////////////////////////

  static Dictionary<FuncSymbol, Stack<FuncCtx>> pool = new Dictionary<FuncSymbol, Stack<FuncCtx>>();
  static int pool_hit;
  static int pool_miss;
  static int pool_free;
  static int nodes_created;

  public static FuncCtx New(FuncSymbol fs)
  {
    Stack<FuncCtx> stack;
    if(!pool.TryGetValue(fs, out stack))
    {
      stack = new Stack<FuncCtx>();
      pool.Add(fs, stack);
    }

    if(stack.Count == 0)
    {
      ++pool_miss;
      var fct = new FuncCtx(fs);
      return fct;
    }
    else
    {
      ++pool_hit;
      --pool_free;
      var fct = stack.Pop();
      if(fct.refs != -1)
        throw new Exception("Expected to be released, refs " + fct.refs);
      fct.refs = 0;
      return fct;
    }
  }

  static public void Del(FuncCtx fct)
  {
    if(fct.refs != 0)
      throw new Exception("Freeing invalid object, refs " + fct.refs);

    //NOTE: actually there must be an existing stack, throw an exception if not?
    Stack<FuncCtx> stack;
    if(!pool.TryGetValue(fct.fs, out stack))
    {
      stack = new Stack<FuncCtx>();
      pool.Add(fct.fs, stack);
    }

    fct.refs = -1;
    fct.mem.Clear();
    //NOTE: we don't reset fnode on purpose, 
    //      so that it will be reused on the next pool request
    //fct.fnode = null;
    fct.fnode_busy = false;
    ++pool_free;
    stack.Push(fct);
  }

  static public void PoolClear()
  {
    nodes_created = 0;
    pool_free = 0;
    pool_hit = 0;
    pool_miss = 0;
    pool.Clear();
  }

  static public int PoolHits
  {
    get { return pool_hit; } 
  }

  static public int PoolMisses
  {
    get { return pool_miss; } 
  }

  static public int PoolCount
  {
    get { return pool_miss; }
  }

  static public int PoolCountFree
  {
    get { return pool_free; }
  }

  static public int NodesCreated
  {
    get { return nodes_created; }
  }
}

} //namespace bhl
