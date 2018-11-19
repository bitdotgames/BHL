using System;
using System.Collections;
using System.Collections.Generic;

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

  public void Init(BaseScope symbols, IModuleLoader module_loader)
  {
    node_stack.Clear();
    curr_node = null;
    mstack.Clear();
    glob_mem.Clear();
    curr_mem = null;
    loaded_modules.Clear();
    stack.Clear();
    call_stack.Clear();

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

  //TODO: this one really should not be here
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

  public AST LoadModule(HashedName mod_id)
  {
    if(mod_id.n == 0)
      return null;

    if(loaded_modules.ContainsKey(mod_id.n))
      return null;

    if(module_loader == null)
      throw new Exception("Module loader is not set");

    var mod_ast = module_loader.LoadModule(mod_id);
    if(mod_ast == null)
      throw new Exception("Could not load module: " + mod_id);

    loaded_modules.Add(mod_id.n, mod_ast.name);

    Interpret(mod_ast);

    return mod_ast;
  }

  public AST LoadModule(string module)
  {
    return LoadModule(Util.GetModuleId(module));
  }

  public FuncSymbol ResolveFuncSymbol(AST_Call ast)
  {
    if(ast.type == EnumCall.FUNC)
      return symbols.resolve(ast.Name()) as FuncSymbol;
    else if(ast.type == EnumCall.MFUNC)
      return ResolveClassMember(ast.scope_ntype, ast.Name()) as FuncSymbol;
    else
      return null;
  }

  public FuncNode GetFuncNode(FuncSymbol symb)
  {
    if(symb is FuncSymbolAST)
      return new FuncNodeAST((symb as FuncSymbolAST).decl, null);
    else if(symb is FuncBindSymbol)
      return new FuncNodeBind(symb as FuncBindSymbol, null);
    else
      throw new Exception("Bad func call type");
  }

  public FuncNode GetFuncNode(AST_Call ast)
  {
    var symb = ResolveFuncSymbol(ast);
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
    return new FuncArgsInfo(call_stack.Peek().cargs_bits);
  }

  public struct CallStackInfo
  {
    public uint module_id;
    public string module_name;

    public string func_name;
    public uint func_id;

    public uint line_num;
  }

  public void GetCallStackInfo(List<CallStackInfo> result)
  {
    //NOTE: we transform call stack into more convenient for the user format
    for(int i=call_stack.Count;i-- > 1;)
    {
      var cs = call_stack[i-1]; 

      string module_name = "";
      loaded_modules.TryGetValue(cs.nname2, out module_name);

      var item = new CallStackInfo() 
      {
        module_id = cs.nname2,
        module_name = module_name,

        func_id = cs.nname1,
        func_name = cs.name, 

        line_num = call_stack[i].line_num
      };
      result.Add(item);
    }
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

  public void PushNode(BehaviorTreeInternalNode node)
  {
    node_stack.Push(node);
    curr_node = node;
  }

  public void PushAttachNode(BehaviorTreeInternalNode node)
  {
    node_stack.Push(node);
    if(curr_node != null)
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

  public DynVal PopRef()
  {
    var v = stack.PopFast();
    v.RefMod(RefOp.USR_DEC_NO_DEL | RefOp.DEC_NO_DEL);
    return v;
  }

  public DynVal PopValueEx(int mode)
  {
    var v = stack.PopFast();
    v.RefMod(mode);
    return v;
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

  ///////////////////////////////////////////////////////////

  public override void DoVisit(AST_Interim ast)
  {
    VisitChildren(ast);
  }

  public override void DoVisit(AST_Module ast)
  {
    PushScope(glob_mem);

    var g = new GroupNode();
    PushNode(g);
    VisitChildren(ast);
    PopNode();

    var status = g.run();
    if(status != BHS.SUCCESS)
      throw new Exception("Global initialization bad status: " + status);

    PopScope();
  }

  public override void DoVisit(AST_Import ast)
  {
    for(int i=0;i<ast.modules.Count;++i)
      LoadModule(ast.modules[i]);
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

    var cl = new ClassSymbolAST(name, ast, parent);
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
      PushAttachNode(new SequentialNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.GROUP)
    {
      PushAttachNode(new GroupNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.SEQ_)
    {
      PushAttachNode(new SequentialNode_());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.PARAL)
    {
      PushAttachNode(new ParallelNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.PARAL_ALL)
    {
      PushAttachNode(new ParallelAllNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.UNTIL_FAILURE)
    {
      PushAttachNode(new MonitorFailureNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.UNTIL_SUCCESS)
    {
      PushAttachNode(new MonitorSuccessNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.NOT)
    {
      PushAttachNode(new InvertNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.PRIO)
    {
      PushAttachNode(new PriorityNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.FOREVER)
    {
      PushAttachNode(new ForeverNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.DEFER)
    {
      PushAttachNode(new DeferNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.IF)
    {
      PushAttachNode(new IfNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.WHILE)
    {
      PushAttachNode(new LoopNode());
      VisitChildren(ast);
      PopNode();
    }
    else if(ast.type == EnumBlock.EVAL)
    {
      PushAttachNode(new EvalNode());
      VisitChildren(ast);
      PopNode();
    }
    else
      throw new Exception("Unknown block type: " + ast.type);
  }

  public override void DoVisit(AST_TypeCast ast)
  {
    VisitChildren(ast);
    curr_node.addChild(new TypeCastNode(ast));
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

  void AddFuncCallNode(AST_Call ast)
  {
    var symb = ResolveFuncSymbol(ast);
    var fbind_symb = symb as FuncBindSymbol;

    //special case if it's bind symbol
    if(fbind_symb != null)
    {
      bool has_args = ast.cargs_bits > 0 || fbind_symb.def_args_num > 0;

      if(has_args)
        curr_node.addChild(new FuncBindCallNode(ast));
      //special case if it's a simple bind symbol call without any args
      else
        curr_node.addChild(fbind_symb.func_creator());
    }
    else
    {
      curr_node.addChild(new FuncCallNode(ast));  
    }
  }

  bool IsCallToSelf(AST_Call ast, uint ntype)
  {
    //Console.WriteLine("CALL TYPE: " + ast.type + " " + ast.Name());

    if(ast.type == EnumCall.MVAR && ast.scope_ntype == ntype)
      return true;

    if(ast.type == EnumCall.MFUNC && ast.scope_ntype == ntype)
      return true;

    return false;
  }

  public override void DoVisit(AST_Return node)
  {
    //NOTE: expression comes first
    VisitChildren(node);
    curr_node.addChild(new ReturnNode());
  }

  public override void DoVisit(AST_Break node)
  {
    curr_node.addChild(new BreakNode());
  }

  public override void DoVisit(AST_PopValue node)
  {
    curr_node.addChild(new PopValueNode());
  }

  public override void DoVisit(AST_Literal node)
  {
    curr_node.addChild(new LiteralNode(node));
  }

  public override void DoVisit(AST_BinaryOpExp node)
  {
    //NOTE: checking if it's a short circuit expression
    if(node.type == EnumBinaryOp.AND || node.type == EnumBinaryOp.OR)
    {
      PushAttachNode(new LogicOpNode(node));
        PushAttachNode(new GroupNode());
        Visit(node.children[0]);
        PopNode();
        PushAttachNode(new GroupNode());
        Visit(node.children[1]);
        PopNode();
      PopNode();
    }
    else
    {
      //NOTE: expression comes first
      VisitChildren(node);
      curr_node.addChild(new BinaryOpNode(node));
    }
  }

  public override void DoVisit(AST_UnaryOpExp node)
  {
    //NOTE: expression comes first
    VisitChildren(node);
    curr_node.addChild(new UnaryOpNode(node));
  }

  public override void DoVisit(AST_VarDecl node)
  {
    //NOTE: expression comes first
    VisitChildren(node);
    curr_node.addChild(new VarAccessNode(node.Name(), VarAccessNode.DECL));
  }

  public override void DoVisit(bhl.AST_JsonObj node)
  {
    curr_node.addChild(new ConstructNode(new HashedName(node.ntype)));

    VisitChildren(node);
  }

  public override void DoVisit(bhl.AST_JsonArr node)
  {
    var bnd = symbols.resolve(node.ntype) as ArrayTypeSymbol;
    if(bnd == null)
      throw new Exception("Could not find class binding: " + node.ntype);

    curr_node.addChild(bnd.Create_New());

    for(int i=0;i<node.children.Count;++i)
    {
      var c = node.children[i];

      //checking if there's an explicit add to array operand
      if(c is AST_JsonArrAddItem)
      {
        var n = (Array_AddNode)bnd.Create_Add(); 
        n.push_arr = true;
        curr_node.addChild(n);
      }
      else
      {
        var jc = new JsonCtx(JsonCtx.ARR, node.ntype, (uint)i);
        jcts.Push(jc);
        Visit(c);
        jcts.Pop();
      }
    }

    //adding last item item
    if(node.children.Count > 0)
    {
      var n = (Array_AddNode)bnd.Create_Add(); 
      n.push_arr = true;
      curr_node.addChild(n);
    }
  }

  public override void DoVisit(bhl.AST_JsonPair node)
  {
    var jc = new JsonCtx(JsonCtx.OBJ, node.scope_ntype, node.nname);
    jcts.Push(jc);

    VisitChildren(node);
    curr_node.addChild(new MVarAccessNode(node.scope_ntype, node.Name(), MVarAccessNode.WRITE_PUSH_CTX));

    jcts.Pop();
  }
}

public abstract class AST_Visitor
{
  public abstract void DoVisit(AST_Interim node);
  public abstract void DoVisit(AST_Import node);
  public abstract void DoVisit(AST_Module node);
  public abstract void DoVisit(AST_VarDecl node);
  public abstract void DoVisit(AST_FuncDecl node);
  public abstract void DoVisit(AST_LambdaDecl node);
  public abstract void DoVisit(AST_ClassDecl node);
  public abstract void DoVisit(AST_EnumDecl node);
  public abstract void DoVisit(AST_Block node);
  public abstract void DoVisit(AST_TypeCast node);
  public abstract void DoVisit(AST_Call node);
  public abstract void DoVisit(AST_Return node);
  public abstract void DoVisit(AST_Break node);
  public abstract void DoVisit(AST_PopValue node);
  public abstract void DoVisit(AST_Literal node);
  public abstract void DoVisit(AST_BinaryOpExp node);
  public abstract void DoVisit(AST_UnaryOpExp node);
  public abstract void DoVisit(AST_New node);
  public abstract void DoVisit(AST_Inc node);
  public abstract void DoVisit(AST_JsonObj node);
  public abstract void DoVisit(AST_JsonArr node);
  public abstract void DoVisit(AST_JsonPair node);

  public void Visit(AST_Base node)
  {
    if(node == null)
      throw new Exception("NULL node");

    if(node is AST_Interim)
      DoVisit(node as AST_Interim);
    else if(node is AST_Block)
      DoVisit(node as AST_Block);
    else if(node is AST_Literal)
      DoVisit(node as AST_Literal);
    else if(node is AST_Call)
      DoVisit(node as AST_Call);
    else if(node is AST_VarDecl)
      DoVisit(node as AST_VarDecl);
    else if(node is AST_LambdaDecl)
      DoVisit(node as AST_LambdaDecl);
    //NOTE: base class must be handled after AST_LambdaDecl
    else if(node is AST_FuncDecl)
      DoVisit(node as AST_FuncDecl);
    else if(node is AST_ClassDecl)
      DoVisit(node as AST_ClassDecl);
    else if(node is AST_EnumDecl)
      DoVisit(node as AST_EnumDecl);
    else if(node is AST_TypeCast)
      DoVisit(node as AST_TypeCast);
    else if(node is AST_Return)
      DoVisit(node as AST_Return);
    else if(node is AST_Break)
      DoVisit(node as AST_Break);
    else if(node is AST_PopValue)
      DoVisit(node as AST_PopValue);
    else if(node is AST_BinaryOpExp)
      DoVisit(node as AST_BinaryOpExp);
    else if(node is AST_UnaryOpExp)
      DoVisit(node as AST_UnaryOpExp);
    else if(node is AST_New)
      DoVisit(node as AST_New);
    else if(node is AST_Inc)
      DoVisit(node as AST_Inc);
    else if(node is AST_JsonObj)
      DoVisit(node as AST_JsonObj);
    else if(node is AST_JsonArr)
      DoVisit(node as AST_JsonArr);
    else if(node is AST_JsonPair)
      DoVisit(node as AST_JsonPair);
    else if(node is AST_Import)
      DoVisit(node as AST_Import);
    else if(node is AST_Module)
      DoVisit(node as AST_Module);
    else 
      throw new Exception("Not known type: " + node.GetType().Name);
  }

  public void VisitChildren(AST node)
  {
    if(node == null)
      return;
    var children = node.children;
    for(int i=0;i<children.Count;++i)
      Visit(children[i]);
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

    if(fs is FuncSymbolAST)
      fnode = new FuncNodeAST((fs as FuncSymbolAST).decl, this);
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
