//#define DEBUG_STACK
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

  FixedStack<BehaviorTreeInternalNode> node_stack = new FixedStack<BehaviorTreeInternalNode>(128);
  BehaviorTreeInternalNode curr_node;

  FixedStack<DynValDict> mstack = new FixedStack<DynValDict>(129);
  DynValDict curr_mem;
  public DynValDict glob_mem = new DynValDict();

  public IModuleLoader module_loader;
  //NOTE: key is a module id, value is a file path
  public Dictionary<ulong,string> loaded_modules = new Dictionary<ulong,string>();

  public BaseScope symbols;

  public struct StackValue
  {
    public DynVal dv;
#if DEBUG_STACK
    public FuncBaseCallNode func_ctx;
#endif
    public BehaviorTreeNode node_ctx;

    public override string ToString() 
    {
      return dv + " " + dv.GetHashCode() + 
#if DEBUG_STACK
        ", func: " + (func_ctx != null ? "" + func_ctx.ast.Name() : "null") +
#endif
        ", node: " + (node_ctx != null ? "" + node_ctx.GetType().Name  : "null");
    }
  }

  public FixedStack<StackValue> stack = new FixedStack<StackValue>(256);
  public FixedStack<FuncBaseCallNode> call_stack = new FixedStack<FuncBaseCallNode>(255);
  //NOTE: this one is used for marking stack values with proper node ctx, 
  //      this is used in paral nodes where stack values interleaving may happen
  public FixedStack<BehaviorTreeNode> node_ctx_stack = new FixedStack<BehaviorTreeNode>(131);
#if DEBUG_STACK
  //NOTE: this one is used for marking stack values with proper func ctx so that 
  //      this info can be retrieved for debug purposes
  public FastStack<FuncBaseCallNode> func_ctx_stack = new FastStack<FuncBaseCallNode>(128);
#endif

  public void Init(BaseScope symbols, IModuleLoader module_loader)
  {
    node_stack.Clear();
    node_ctx_stack.Clear();
    curr_node = null;
    mstack.Clear();
    glob_mem.Clear();
    curr_mem = null;
    loaded_modules.Clear();
    stack.Clear();
    call_stack.Clear();
#if DEBUG_STACK
    func_ctx_stack.Clear();
#endif

    this.symbols = symbols;
    this.module_loader = module_loader;
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
      return symbols.Resolve(ast.Name()) as FuncSymbol;
    else if(ast.type == EnumCall.MFUNC)
      return ResolveClassMember(ast.scope_ntype, ast.Name()) as FuncSymbol;
    else
      return null;
  }

  public FuncNode GetFuncNode(AST_Call ast)
  {
    var symb = ResolveFuncSymbol(ast);
    return FuncCtx.MakeFuncNode(symb);
  }

  public FuncNode GetFuncNode(HashedName name)
  {
    var symb = symbols.Resolve(name) as FuncSymbol;
    return FuncCtx.MakeFuncNode(symb);
  }

  public FuncNode GetFuncNode(string module_name, string func_name)
  {
    LoadModule(module_name);
    return GetFuncNode(Util.GetFuncId(module_name, func_name));
  }

  public FuncUserCallNode GetFuncCallNode(HashedName name)
  {
    return new FuncUserCallNode(GetFuncNode(name));
  }

  public FuncUserCallNode GetFuncCallNode(string module_name, string func_name)
  {
    return new FuncUserCallNode(GetFuncNode(module_name, func_name));
  }

  Symbol ResolveClassMember(HashedName class_type, HashedName name)
  {
    var cl = symbols.Resolve(class_type) as ClassSymbol;
    if(cl == null)
      throw new Exception("Class binding not found: " + class_type + ", member: " + name); 

    var cl_member = cl.ResolveMember(name);
    if(cl_member == null)
      throw new Exception("Member not found: " + name);
    return cl_member;
  }

  //NOTE: usually used in symbols
  public FuncArgsInfo GetFuncArgsInfo()
  {
    return new FuncArgsInfo(call_stack.Peek().ast.cargs_bits);
  }

  public struct CallStackInfo
  {
    public uint module_id;
    public string module_name;

    public string func_name;
    public uint func_id;
    public int func_hash;

    public uint line_num;

    static public CallStackInfo Make(FuncBaseCallNode n)
    {
      var item = new CallStackInfo();
      item.module_id = 0;
      item.module_name = "";
      item.func_id = 0;
      item.func_name = "?";
      item.func_hash = 0;
      item.line_num = 0;

      if(n == null)
        return item;

      //checking special case for FuncBaseCallNode (e.g. lambda call)
      if(n is FuncUserCallNode)
      {
        var fuc = n as FuncUserCallNode;
        if(fuc.children.Count > 0 && fuc.children[0] is FuncNodeScript)
        {
          var fast = fuc.children[0] as FuncNodeScript;
          item.module_id = fast.decl.nname2; 
          item.func_name = fast.decl.name;
          item.func_id = fast.decl.nname1;
          item.func_hash = fast.decl.GetHashCode(); 
        }
      }
      else if(n.ast != null)
      {
        item.module_id = n.ast.nname2;
        item.func_name = n.ast.name;
        item.func_id = n.ast.nname1;
        item.func_hash = n.ast.GetHashCode();
        item.line_num = n.ast.line_num;
      }

      return item;
    }
  }

  public void GetCallStackInfo(List<CallStackInfo> result)
  {
    //NOTE: we transform call stack into more convenient for the user format
    for(int i=call_stack.Count;i-- > 1;)
    {
      FuncBaseCallNode c;
      call_stack.TryGetAt(i-1, out c);

      var item = CallStackInfo.Make(c);

      string module_name;
      if(loaded_modules.TryGetValue(item.module_id, out module_name))
        item.module_name = module_name;

      FuncBaseCallNode c_prev;
      call_stack.TryGetAt(i, out c_prev);
      var item_prev = CallStackInfo.Make(c_prev);

      item.line_num = item_prev.line_num;

      result.Add(item);
    }
  }

  public string GetStackTrace()
  {
    var info = new List<Interpreter.CallStackInfo>();
    GetCallStackInfo(info);

    string bhl_stack = "";
    for(int i=0;i<info.Count;++i)
    {
      var item = info[i];
      bhl_stack += (string.IsNullOrEmpty(item.func_name) ? item.func_id.ToString() : item.func_name) + " () (at "  + (string.IsNullOrEmpty(item.module_name) ? item.module_id.ToString() : item.module_name + ".bhl") + ":" + item.line_num + ") " + item.func_hash + "\n"; 
    }
    return bhl_stack;
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
    mstack.Pop(null);
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
    if(!ok)
      throw new Exception("No such variable " + name + " in scope");
    return val;
  }

  public DynVal GetGlobalValue(HashedName name)
  {
    DynVal val;
    bool ok = glob_mem.TryGet(name, out val);
    if(!ok)
      throw new Exception("No such variable " + name + " in global scope");
    return val;
  }

  public void SetGlobalValue(HashedName name, DynVal val)
  {
    glob_mem.Set(name, val);
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
    var node = node_stack.Pop(null);
    curr_node = node_stack.Count > 0 ? node_stack.Peek() : null; 
    return node;
  }

  public void PushStackParalCtx(BehaviorTreeNode n)
  {
    node_ctx_stack.Push(n);
  }

  public void PopStackParalCtx()
  {
    node_ctx_stack.Pop(null);
  }

  public void PushValue(DynVal v)
  {
    v.RefMod(RefOp.INC | RefOp.USR_INC);

    var sv = new StackValue();
    sv.dv = v;
#if DEBUG_STACK
    sv.func_ctx = func_ctx_stack.Count > 0 ? func_ctx_stack.Peek() : null; 
#endif
    sv.node_ctx = node_ctx_stack.Count > 0 ? node_ctx_stack.Peek() : null;

    stack.Push(sv);
  }

  public DynVal PopValue()
  {
    var sv = stack.Pop();
    sv.dv.RefMod(RefOp.USR_DEC_NO_DEL | RefOp.DEC);
    return sv.dv;
  }

  public DynVal PopRef()
  {
    var sv = stack.Pop();
    sv.dv.RefMod(RefOp.USR_DEC_NO_DEL | RefOp.DEC_NO_DEL);
    return sv.dv;
  }

  public DynVal PopValueEx(int mode)
  {
    var sv = stack.Pop();
    sv.dv.RefMod(mode);
    return sv.dv;
  }

  public void PopValuesUntilMark(DynVal stack_mark, BehaviorTreeNode paral_ctx)
  {
    for(int i=stack.Count;i-- > 0;)
    {
      var sv = stack[i];
      if(sv.dv == stack_mark)
        break;
      if(sv.node_ctx != paral_ctx)
        continue;
      sv.dv.RefMod(RefOp.USR_DEC_NO_DEL | RefOp.DEC);
      stack.RemoveAt(i);
    }
  }

  public bool PeekValue(ref DynVal res)
  {
    if(stack.Count == 0)
      return false;

    res = stack.Peek().dv;
    return true;
  }

  public DynVal PeekValue()
  {
    return stack.Peek().dv;
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
    for(int i=0;i<ast.module_ids.Count;++i)
      LoadModule(ast.module_ids[i]);
  }

  void CheckFuncIsUnique(HashedName name)
  {
    var s = symbols.Resolve(name) as FuncSymbol;
    if(s != null)
      throw new Exception("Function is already defined: " + name);
  }

  void CheckNameIsUnique(HashedName name)
  {
    var s = symbols.Resolve(name);
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

    var fn = new FuncSymbolScript(symbols, ast);
    symbols.Define(fn);
  }

  public override void DoVisit(AST_LambdaDecl ast)
  {
    //if there's such a lambda symbol already we re-use it
    var name = ast.Name(); 
    var lmb = symbols.Resolve(name) as LambdaSymbol;
    if(lmb == null)
    {
      CheckFuncIsUnique(name);
      lmb = new LambdaSymbol(symbols, ast);
      symbols.Define(lmb);
    }

    curr_node.addChild(new PushFuncCtxNode(lmb));
  }

  public override void DoVisit(AST_ClassDecl ast)
  {
    var name = ast.Name();
    CheckNameIsUnique(name);

    var parent = symbols.Resolve(ast.ParentName()) as ClassSymbol;

    var cl = new ClassSymbolScript(name, ast, parent);
    symbols.Define(cl);

    for(int i=0;i<ast.children.Count;++i)
    {
      var child = ast.children[i];
      var vd = child as AST_VarDecl;
      if(vd != null)
      {
        cl.Define(new FieldSymbolScript(vd.name, vd.ntype));
      }
    }
  }

  public override void DoVisit(AST_EnumDecl ast)
  {
    var name = ast.Name();
    CheckNameIsUnique(name);

    var es = new EnumSymbolScript(name);
    symbols.Define(es);
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
      curr_node.addChild(new VarAccessNode(ast.Name(), VarAccessNode.READ));
    }
    else if(ast.type == EnumCall.VARW)
    {
      curr_node.addChild(new VarAccessNode(ast.Name(), VarAccessNode.WRITE));
    }
    else if(ast.type == EnumCall.GVAR)
    {
      curr_node.addChild(new VarAccessNode(ast.Name(), VarAccessNode.GREAD));
    }
    else if(ast.type == EnumCall.GVARW)
    {
      curr_node.addChild(new VarAccessNode(ast.Name(), VarAccessNode.GWRITE));
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
      var s = symbols.Resolve(ast.nname()) as FuncSymbol;
      if(s == null)
        throw new Exception("Could not find func:" + ast.Name());
      curr_node.addChild(new PushFuncCtxNode(s));
    }
    else if(ast.type == EnumCall.ARR_IDX)
    {
      var bnd = symbols.Resolve(ast.scope_ntype) as ArrayTypeSymbol;
      if(bnd == null)
        throw new Exception("Could not find class binding: " + ast.scope_ntype);

      curr_node.addChild(bnd.Create_At());
    }
    else if(ast.type == EnumCall.ARR_IDXW)
    {
      var bnd = symbols.Resolve(ast.scope_ntype) as ArrayTypeSymbol;
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
    var fbind_symb = symb as FuncSymbolNative;

    //special case if it's bind symbol
    if(fbind_symb != null)
    {
      bool has_args = ast.cargs_bits > 0 || fbind_symb.def_args_num > 0;

      if(has_args)
        curr_node.addChild(new NativeFuncCallNode(ast));
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

  public override void DoVisit(AST_Continue node)
  {
    if(!node.jump_marker)
      throw new Exception("Not implemented");
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
    var bnd = symbols.Resolve(node.ntype) as ArrayTypeSymbol;
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
        Visit(c);
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
    VisitChildren(node);
    curr_node.addChild(new MVarAccessNode(node.scope_ntype, node.Name(), MVarAccessNode.WRITE_PUSH_CTX));
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
  public abstract void DoVisit(AST_Continue node);
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
    else if(node is AST_Continue)
      DoVisit(node as AST_Continue);
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
  //NOTE: fields public below only for inspection

  //NOTE: -1 means it's in released state,
  //      public only for inspection
  public int refs;

  public FuncSymbol fs;
  //NOTE: this memory scope is used for 'use' variables, it's then
  //      copied to concrete memory scope of the function.
  public DynValDict mem = new DynValDict();
  public FuncNode fnode;
  public FuncUserCallNode fcall_node;
  public bool fnode_busy;

  private FuncCtx(FuncSymbol fs)
  {
    this.fs = fs;
  }

  public FuncNode GetNode()
  {
    fnode_busy = true;

    if(fnode != null)
      return fnode;

    ++nodes_created;

    fnode = MakeFuncNode(fs, this);

    return fnode;
  }

  public FuncUserCallNode GetCallNode()
  {
    fnode_busy = true;

    if(fcall_node != null)
      return fcall_node;

    fcall_node = new FuncUserCallNode(GetNode());

    return fcall_node;
  }

  public static FuncNode MakeFuncNode(FuncSymbol fs, FuncCtx fct = null)
  {
    if(fs is FuncSymbolScript)
      return new FuncNodeScript((fs as FuncSymbolScript).decl, fct);
    else if(fs is LambdaSymbol)
      return new FuncNodeLambda(fct);
    else if(fs is FuncSymbolNative)
      return new FuncNodeNative(fs as FuncSymbolNative, fct);
    else
      throw new Exception("Unknown symbol type");
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
      for(int i=0;i<ldecl.uses.Count;++i)
      {
        var up = ldecl.uses[i];
        var val = mem.Get(up.Name());
        dup.mem.Set(up.Name(), up.is_ref ? val : val.ValueClone());
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

    //Console.WriteLine("FREF NEW " + Environment.StackTrace);
    
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

    //Console.WriteLine("FREF DEL " + Environment.StackTrace);

    //NOTE: actually there must be an existing stack, throw an exception if not?
    Stack<FuncCtx> stack;
    if(!pool.TryGetValue(fct.fs, out stack))
    {
      stack = new Stack<FuncCtx>();
      pool.Add(fct.fs, stack);
    }

    fct.refs = -1;
    fct.mem.Clear();
    //NOTE: we don't reset cached func node on purpose, 
    //      so that it will be reused on the next pool request
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
