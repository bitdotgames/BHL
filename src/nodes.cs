//#define DEBUG_STACK
using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

public enum BHS
{
  NONE    = 0, 
  SUCCESS = 1, 
  FAILURE = 2, 
  RUNNING = 3
}

public interface BehaviorVisitable
{
  void accept(BehaviorVisitor v);
}

public interface BehaviorVisitor
{
  void push();
  void pop();

  void visit(BehaviorTreeNode node);
}

//NOTE: this one probably should be an interface but for simplicity
//      and efficiency reasons it is not
public abstract class BehaviorTreeNode : BehaviorVisitable
{
  //NOTE: semi-private, public for fast access
  public BHS currStatus; 
  
  public BehaviorTreeNode()
  {
    currStatus = BHS.NONE;
  }

  public virtual void accept(BehaviorVisitor v)
  {
    v.visit(this);
  }

  //NOTE: this method is heavily used for inlining, 
  //      don't change its contents if you are not sure
  public BHS run()
  {
    if(currStatus != BHS.RUNNING)
      init();
    currStatus = execute();
    if(currStatus != BHS.RUNNING)
      deinit();
    return currStatus;
  }

  public void stop()
  {
    if(currStatus == BHS.RUNNING)
      deinit();
    if(currStatus != BHS.NONE)
      defer();
    currStatus = BHS.NONE;
  }

  public virtual string inspect() { return ""; }

  //NOTE: methods below should never be called directly
  // This method will be invoked before the node is executed for the first time.
  public abstract void init();
  // This method will be invoked once if node was running and then stopped running.
  public abstract void deinit();
  // This method is invoked when the node should be run.
  public abstract BHS execute();
  // This method is invoked when the node goes out of the scope
  public abstract void defer();
}

public abstract class BehaviorTreeTerminalNode : BehaviorTreeNode
{
  public override void init()
  {}

  public override void deinit()
  {}

  public override void defer()
  {}

  public override BHS execute()
  {
    return BHS.SUCCESS;
  }
}

public abstract class BehaviorTreeInternalNode : BehaviorTreeNode
{
  public List<BehaviorTreeNode> children = new List<BehaviorTreeNode>();

  public override void accept(BehaviorVisitor v)
  {
    v.visit(this);

    v.push();

    for(int i=0;i<children.Count;++i)
      children[i].accept(v);

    v.pop();
  }

  public virtual BehaviorTreeInternalNode addChild(BehaviorTreeNode new_child)
  {
    children.Add(new_child);
    return this;
  }

  protected void deinitAndDeferChildren(int start, int len)
  {
    for(int i=start;i >= 0 && i < (start+len) && i < children.Count; ++i)
    {
      var c = children[i];
      if(c.currStatus == BHS.RUNNING)
      {
        c.deinit();
        c.currStatus = BHS.SUCCESS;
      }
    }

    //NOTE: traversing children in the reverse order
    //TODO: traverse from start+len index
    for(int i=children.Count;i-- > 0;)
    {
      var c = children[i];
      if(c.currStatus != BHS.NONE)
      {
        c.defer();
        c.currStatus = BHS.NONE;
      }
    }
  }

  protected void deferChildren(int start)
  {
    //NOTE: traversing children in the reverse order
    for(int i=start;i-- > 0;)
    {
      var c = children[i];

      if(c.currStatus != BHS.NONE)
      {
        if(c.currStatus == BHS.RUNNING)
          throw new Exception("Node is RUNNING during defer: " + Interpreter.instance.GetStackTrace());

        c.defer();
        c.currStatus = BHS.NONE;
      }
    }
  }

  protected void deinitChildren(int start, int len)
  {
    for(int i=start;i >= 0 && i < (start+len) && i < children.Count; ++i)
    {
      var c = children[i];
      if(c.currStatus == BHS.RUNNING)
      {
        c.deinit();
        c.currStatus = BHS.SUCCESS;
      }
    }
  }

  protected void stopChildren()
  {
    //NOTE: traversing children in the reverse order
    for(int i=children.Count;i-- > 0;)
    {
      var c = children[i];
      c.stop();
    }
  }
}

//NOTE: Scope node is a base building block for nodes with scope, e.g seq { .. }.
//      Once the node is deinited it stops all its children in reverse order. 
//      Stop invokes deinit and defer.
public abstract class ScopeNode : BehaviorTreeInternalNode
{
  //NOTE: normally you never really want to override this one
  override public void deinit()
  {
    //NOTE: Trying to deinit all of its children may be suboptimal, so
    //      it makes sense to override it for specific cases
    deinitAndDeferChildren(0, children.Count);
  }

  //NOTE: normally you never really want to override this one
  override public void defer()
  {}
}

//NOTE: Group node is a node which behaves like a terminal node however
//      contains multiple children. 
public class GroupNode : BehaviorTreeInternalNode
{
  protected int currentPosition = 0;

  override public void init()
  {
    currentPosition = 0;
  }

  override public BHS execute()
  {
    BHS status = BHS.SUCCESS;
    while(currentPosition < children.Count)
    {
      var currentTask = children[currentPosition];
      //status = currentTask.run();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.init();
      status = currentTask.execute();
      currentTask.currStatus = status;
      if(status != BHS.RUNNING)
        currentTask.deinit();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(status == BHS.SUCCESS)
        ++currentPosition;
      else
        break;
    } 
    return status;
  }

  override public void deinit()
  {
    deinitChildren(currentPosition, 1);
  }

  override public void defer()
  {
    deferChildren(children.Count);
  }
}

public abstract class BehaviorTreeDecoratorNode : BehaviorTreeInternalNode
{
  public override void init()
  {
    if(children.Count != 1)
      throw new Exception("One child is expected, given " + children.Count);
  }

  public override BHS execute()
  {
    //BHS status = children[0].run();
    BHS status;
    var currentTask = children[0];
    ////////////////////FORCING CODE INLINE////////////////////////////////
    if(currentTask.currStatus != BHS.RUNNING)
      currentTask.init();
    status = currentTask.execute();
    currentTask.currStatus = status;
    if(status != BHS.RUNNING)
      currentTask.deinit();
    ////////////////////FORCING CODE INLINE////////////////////////////////
    return status;
  }

  override public void deinit()
  {
    deinitChildren(0, 1);
  } 

  override public void defer()
  {
    deferChildren(children.Count);
  }

  public void setSlave(BehaviorTreeNode node)
  {
    if(children.Count == 0)
      this.addChild(node);
    //NOTE: should we stop the current one?
    else
      children[0] = node;
  }
}

//////////////////////////////////////////

public class suspend : BehaviorTreeNode
{
  override public BHS execute()
  {
    return BHS.RUNNING;
  }
  override public void init(){}
  override public void deinit(){}
  override public void defer(){}
}

//////////////////////////////////////////

public class yield : BehaviorTreeNode
{
  override public BHS execute()
  {
    return currStatus == BHS.NONE ? BHS.RUNNING : BHS.SUCCESS;
  }
  override public void init(){}
  override public void deinit(){}
  override public void defer(){}
}

//////////////////////////////////////////
               
public class nop : BehaviorTreeNode
{
  override public BHS execute()
  {
    return BHS.SUCCESS;
  }
  override public void init(){}
  override public void deinit(){}
  override public void defer(){}
}

//////////////////////////////////////////

public class fail : BehaviorTreeNode
{
  override public BHS execute()
  {
    return BHS.FAILURE;
  }
  override public void init(){}
  override public void deinit(){}
  override public void defer(){}
}

/////////////////////////////////////////////////////////////

public class check : BehaviorTreeNode
{
  override public BHS execute()
  {
    var val = Interpreter.instance.PopValue();
    return val._num == 0 ? BHS.FAILURE : BHS.SUCCESS;
  }
  override public void init(){}
  override public void deinit(){}
  override public void defer(){}
}

/////////////////////////////////////////////////////////////

public class SequentialNode : ScopeNode
{
  protected int currentPosition = 0;

  override public void init()
  {
    currentPosition = 0;
  }

  override public void deinit()
  {
    deinitAndDeferChildren(currentPosition, 1);
  }

  override public BHS execute()
  {
    BHS status = BHS.SUCCESS;
    while(currentPosition < children.Count)
    {
      var currentTask = children[currentPosition];
      //status = currentTask.run();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.init();
      status = currentTask.execute();
      currentTask.currStatus = status;
      if(status != BHS.RUNNING)
        currentTask.deinit();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(status == BHS.SUCCESS)
        ++currentPosition;
      else
        break;
    } 
    return status;
  }
}

public class SequentialNode_ : SequentialNode
{
  override public BHS execute()
  {
    //var status = base.execute();
    ////////////////////FORCING CODE INLINE////////////////////////////////
    BHS status = BHS.SUCCESS;
    while(currentPosition < children.Count)
    {
      var currentTask = children[currentPosition];
      //status = currentTask.run();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.init();
      status = currentTask.execute();
      currentTask.currStatus = status;
      if(status != BHS.RUNNING)
        currentTask.deinit();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(status == BHS.SUCCESS)
        ++currentPosition;
      else
        break;
    } 
    ////////////////////FORCING CODE INLINE////////////////////////////////
    if(status == BHS.FAILURE)
      status = BHS.SUCCESS;
    return status;
  }
}

public abstract class FuncBaseCallNode : GroupNode
{
  public AST_Call ast;

  //TODO: this one is for inspecting purposes only
  public BHS lastExecuteStatus;

  DynVal stack_mark;
  BehaviorTreeNode stack_paral_ctx;

  public FuncBaseCallNode(AST_Call ast)
  {
    this.ast = ast;
  }

  override public BHS execute()
  {
    var interp = Interpreter.instance;

#if DEBUG_STACK
    interp.func_ctx_stack.Push(this);
#endif

    BHS status = BHS.SUCCESS;
    //var status = base.execute();
    ////////////////////FORCING CODE INLINE////////////////////////////////
    while(currentPosition < children.Count)
    {
      var currentTask = children[currentPosition];
      //status = currentTask.run();
      ////////////////////FORCING CODE INLINE////////////////////////////////

      //NOTE: the last node is actually the func call so
      //      we push it on to the call stack when it's executed
      bool is_func_call = currentPosition == children.Count-1;
      if(is_func_call)
        interp.call_stack.Push(this);

        if(currentTask.currStatus != BHS.RUNNING)
          currentTask.init();

        status = currentTask.execute();
        currentTask.currStatus = status;  
        if(status != BHS.RUNNING)
          currentTask.deinit();

      //NOTE: only when it's actual func call we pop it from the call stack
      if(is_func_call)
        interp.call_stack.Dec();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(status == BHS.SUCCESS)
        ++currentPosition;
      else
        break;
    } 
    ////////////////////FORCING CODE INLINE////////////////////////////////

#if DEBUG_STACK
    interp.func_ctx_stack.DecFast();
#endif

    lastExecuteStatus = status;

    return status;
  }

  override public void init()
  {
    var interp = Interpreter.instance;

    stack_paral_ctx = interp.node_ctx_stack.Count > 0 ? interp.node_ctx_stack.Peek() : null;
    stack_mark = interp.stack.Count > 0 ? interp.stack.Peek().dv : null;

    base.init();
  }

  override public void deinit()
  {
    deinitChildren(currentPosition, 1);

    //NOTE: checking if we need to clean the values stack due to 
    //      non successul execution of the node
    if(currStatus != BHS.SUCCESS)
    {
      var interp = Interpreter.instance;
      if(interp.stack.Count > 0)
        interp.PopValuesUntilMark(stack_mark, stack_paral_ctx);
    }

    stack_paral_ctx = null;
    stack_mark = null;
  } 

  override public string inspect() 
  {
    return "" + (ast != null ? ast.Name() : "");
  }
}

public class FuncUserCallNode : FuncBaseCallNode
{
  public FuncUserCallNode(FuncNode node)
    : base(null)
  {
    this.addChild(node);
  }

  //TODO: add support for default args overriding
  public void SetArgs(params DynVal[] args)
  {
    var interp = Interpreter.instance;

    var fn = children[0] as FuncNode;

    if(!fn.args_info.SetArgsNum(args.Length))
      throw new Exception("Too many arguments");

    for(int i=0;i<args.Length;++i)
    {
      var arg = args[i];
      interp.PushValue(arg);
    }
  }

  public FuncNode GetFuncNode()
  {
    return children[0] as FuncNode;
  }
}

public class FuncCallNode : FuncBaseCallNode
{
  const int IDX_FIRST_TIME = -2;
  const int IDX_DETACHED   = -1;

  int idx_in_pool = IDX_FIRST_TIME;

  public FuncCallNode(AST_Call ast)
    : base(ast)
  {}

  void VisitCallArgs(Interpreter interp, FuncNode fnode)
  {
    var args_info = new FuncArgsInfo(ast.cargs_bits);
    int passed_args_num = args_info.CountArgs();
    int total_args_num = fnode.GetTotalArgsNum();
    int required_args_num = fnode.GetRequiredArgsNum();

    //actually passed args counter
    int p = 0;
    for(int i=0;i<total_args_num;++i)
    {
      int default_arg_idx = i - required_args_num;

      //checking if default argument should be used
      if(default_arg_idx >= 0 && args_info.IsDefaultArgUsed(default_arg_idx))
      {
        var decl_arg = fnode.GetDeclArg(required_args_num + default_arg_idx);
        if(decl_arg.children.Count == 0)
          throw new Exception("Bad default arg at idx " + (required_args_num + default_arg_idx) + " func " + fnode.GetName());
        interp.Visit(decl_arg.children[0]);
      }
      else if(p < passed_args_num)
      {
        interp.Visit(ast.children[p]);
        ++p;
      }
    }
  }

  override public void init()
  {
    if(idx_in_pool == IDX_FIRST_TIME)
    {
      var interp = Interpreter.instance;

      var pi = PoolRequest(ast);

      interp.PushNode(this);
      VisitCallArgs(interp, pi.fnode);
      this.addChild(pi.fnode);
      interp.PopNode();

      idx_in_pool = pi.idx;
    }
    else if(idx_in_pool == IDX_DETACHED)
    {
      var pi = PoolRequest(ast);

      children[children.Count-1] = pi.fnode;

      idx_in_pool = pi.idx;
    }

    base.init();
  }

  override public void deinit()
  {
    base.deinit();

    if(idx_in_pool >= 0)
    {
      //NOTE: we need to make sure pool was not cleared somewhere in between
      if(idx_in_pool < pool.Count)
      {
        var pi = pool[idx_in_pool];
        if(pi.fnode == children[children.Count-1])
          PoolFree(pi);
      }
      idx_in_pool = IDX_DETACHED;
    }
  }

  override public void defer()
  {
    //NOTE: we don't take into account the last func node, since
    //      it's already detached
    deferChildren(children.Count-1);
  }

  ///////////////////////////////////////////////////////////////////
  static int free_count = 0;
  static int last_pool_id = 0;

  struct PoolItem
  {
    public int id;
    public int idx;
    public int next_free;

    public AST_Call ast;
    public FuncNode fnode;

    public PoolItem(AST_Call _ast)
    {
      id = 0;
      idx = -1;
      next_free = -1;
      ast = _ast;
      fnode = null;
    }

    public bool IsEmpty()
    {
      return ast == null;
    }

    public void Clear()
    {
      idx = -1;
      next_free = -1;
      ast = null;
      fnode = null;
    }
  }

  static Dictionary<ulong, int> func2last_free = new Dictionary<ulong, int>();
  static List<PoolItem> pool = new List<PoolItem>();

  static int pool_hit;
  static int pool_miss;

  static PoolItem PoolRequest(AST_Call ast)
  {
    ulong pool_id = ast.FuncId(); 

    int idx_in_pool = -1;
    if(func2last_free.TryGetValue(pool_id, out idx_in_pool) && idx_in_pool != -1)
    {
      var pi = pool[idx_in_pool];

      if(pi.fnode.currStatus == BHS.RUNNING)
        throw new Exception("Bad status: " + pi.fnode.currStatus);
      ++pool_hit;
      --free_count;

      func2last_free[pool_id] = pi.next_free;

      pi.next_free = -1;
      pool[idx_in_pool] = pi;
      //setting actual number of passed arguments
      pi.fnode.args_info = new FuncArgsInfo(ast.cargs_bits);
      return pi;
    }

    {
      var pi = new PoolItem(ast);

      InitPoolItem(ref pi);

      pi.idx = pool.Count;
      pi.next_free = -1;
      func2last_free[pool_id] = -1;
      pool.Add(pi);

      ++pool_miss;

      //setting actual number of passed arguments
      pi.fnode.args_info = new FuncArgsInfo(ast.cargs_bits);
      return pi;
    }
  }

  static void PoolFree(PoolItem pi)
  {
    if(pi.fnode.currStatus == BHS.RUNNING)
      throw new Exception("Bad status: " + pi.fnode.currStatus);

    ulong pool_id = pi.ast.FuncId();
    int last_free = func2last_free[pool_id];
    pi.next_free = last_free;
    pool[pi.idx] = pi;
    func2last_free[pool_id] = pi.idx;
    ++free_count;
  }

  static void InitPoolItem(ref PoolItem pi)
  {
    var interp = Interpreter.instance;

    pi.id = ++last_pool_id;
    FuncNode fnode = interp.GetFuncNode(pi.ast);
    if(!(fnode is FuncNodeScript))
      throw new Exception("Not expected type of node");
    pi.fnode = fnode;
  }

  static public void PoolClear()
  {
    free_count = 0;
    func2last_free.Clear();
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
    get { return pool.Count; }
  }

  static public int PoolCountFree
  {
    get { return free_count; }
  }
}

public class NativeFuncCallNode : FuncBaseCallNode
{
  public NativeFuncCallNode(AST_Call ast)
    : base(ast)
  {}

  override public void init()
  {
    //checking if it's a first time
    if(children.Count == 0)
    {
      var interp = Interpreter.instance;
      var symb = interp.ResolveFuncSymbol(ast) as FuncSymbolNative;
      interp.PushNode(this);

      var args_info = new FuncArgsInfo(ast.cargs_bits);

      int cargs_num = args_info.CountArgs();
      for(int i=0;i<cargs_num;++i)
        interp.Visit(ast.children[i]);

      this.addChild(symb.func_creator());

      interp.PopNode();
    }

    base.init();
  }
}

public class ParallelNode : ScopeNode
{
  override public void init() 
  {}

  override public BHS execute()
  {
    var interp = Interpreter.instance;

    for(int i=0;i<children.Count;++i)
    {
      var currentTask = children[i];

      if(currentTask.currStatus == BHS.NONE || currentTask.currStatus == BHS.RUNNING)
      {
        interp.PushStackParalCtx(currentTask);

        //BHS status = currentTask.run();
        BHS status;
        ////////////////////FORCING CODE INLINE////////////////////////////////
        if(currentTask.currStatus != BHS.RUNNING)
          currentTask.init();
        try
        {
          status = currentTask.execute();
        }
        catch(Interpreter.ReturnException)
        {
          interp.PopStackParalCtx();
          throw;
        }
        catch(Interpreter.BreakException)
        {
          interp.PopStackParalCtx();
          throw;
        }
        currentTask.currStatus = status;
        if(status != BHS.RUNNING)
          currentTask.deinit();
        ////////////////////FORCING CODE INLINE////////////////////////////////

        interp.PopStackParalCtx();

        if(status != BHS.RUNNING)
          return status;
      }
    }

    return BHS.RUNNING;
  }

  override public BehaviorTreeInternalNode addChild(BehaviorTreeNode new_child)
  {
    //force DEFER to keep running
    DeferNode d = new_child as DeferNode;
    if(d != null)
      d.result = BHS.RUNNING;
    
    children.Add(new_child);
    return this;
  }
}

public class ParallelAllNode : ScopeNode
{
  override public void init() 
  {}

  override public BHS execute()
  {
    var interp = Interpreter.instance;

    bool sawAllSuccess = true;
    for(int i=0;i<children.Count;++i)
    {
      var currentTask = children[i];

      if(currentTask.currStatus == BHS.NONE || currentTask.currStatus == BHS.RUNNING)
      {
        interp.PushStackParalCtx(currentTask);

        //BHS status = currentTask.run();
        BHS status;
        ////////////////////FORCING CODE INLINE////////////////////////////////
        if(currentTask.currStatus != BHS.RUNNING)
          currentTask.init();
        try
        {
          status = currentTask.execute();
        }
        catch(Interpreter.ReturnException)
        {
          interp.PopStackParalCtx();
          throw;
        }
        catch(Interpreter.BreakException)
        {
          interp.PopStackParalCtx();
          throw;
        }
        currentTask.currStatus = status;
        if(status != BHS.RUNNING)
          currentTask.deinit();
        ////////////////////FORCING CODE INLINE////////////////////////////////
        
        interp.PopStackParalCtx();

        if(status == BHS.FAILURE)
          return BHS.FAILURE;

        if(status == BHS.RUNNING)
          sawAllSuccess = false;
      }
    }

    if(sawAllSuccess)
      return BHS.SUCCESS;

    return BHS.RUNNING;
  }
}

public class PriorityNode : ScopeNode
{
  private int currentPosition = -1;

  override public void init()
  {
    currentPosition = -1;
  }

  override public BHS execute()
  {
    BHS status;

    //there's one still running
    if(currentPosition != -1)
    {
      var currentTask = children[currentPosition];
      //status = currentTask.run();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.init();
      status = currentTask.execute();
      currentTask.currStatus = status;
      if(status != BHS.RUNNING)
        currentTask.deinit();
      ////////////////////FORCING CODE INLINE////////////////////////////////

      if(status == BHS.RUNNING)
        return BHS.RUNNING;
      else if(status == BHS.SUCCESS)
      {
        currentPosition = -1;
        return BHS.SUCCESS;
      }
      else if(status == BHS.FAILURE)
      {
        currentPosition++;
        if(currentPosition == (int)children.Count)
        {
          currentPosition = -1;
          return BHS.FAILURE;
        }
      }
    }
    else
    {
      currentPosition = 0;
    }

    if(children.Count == 0)
      return BHS.SUCCESS;

    //first run
    {
      BehaviorTreeNode currentTask = children[currentPosition];
       //keep trying children until one doesn't fail
      while(true)
      {
        //status = currentTask.run();
        ////////////////////FORCING CODE INLINE////////////////////////////////
        if(currentTask.currStatus != BHS.RUNNING)
          currentTask.init();
        status = currentTask.execute();
        currentTask.currStatus = status;
        if(status != BHS.RUNNING)
          currentTask.deinit();
        ////////////////////FORCING CODE INLINE////////////////////////////////
        if(status != BHS.FAILURE)
          break;
      
        currentPosition++;
        if(currentPosition == (int)children.Count) //all of the children failed
        {
          currentPosition = -1;
          return BHS.FAILURE;
        }
        currentTask = children[currentPosition];
      }
    }

    return status;
  }

  override public BehaviorTreeInternalNode addChild(BehaviorTreeNode new_child)
  {
    //force DEFER to proceed to the next node
    DeferNode d = new_child as DeferNode;
    if(d != null)
      d.result = BHS.FAILURE;
    
    children.Add(new_child);
    return this;
  }
}

public class ForeverNode : SequentialNode
{
  override public BHS execute()
  {
    //var status = base.execute();
    ////////////////////FORCING CODE INLINE////////////////////////////////
    BHS status = BHS.SUCCESS;

    try
    {
      while(currentPosition < children.Count)
      {
        var currentTask = children[currentPosition];
        //status = currentTask.run();
        ////////////////////FORCING CODE INLINE////////////////////////////////
        if(currentTask.currStatus != BHS.RUNNING)
          currentTask.init();
        status = currentTask.execute();
        currentTask.currStatus = status;
        if(status != BHS.RUNNING)
          currentTask.deinit();
        ////////////////////FORCING CODE INLINE////////////////////////////////
        if(status == BHS.SUCCESS)
          ++currentPosition;
        else
          break;
      } 
    }
    catch(Interpreter.BreakException)
    {
      currentPosition = 0;
      currStatus = BHS.SUCCESS;
      stop();
      return BHS.SUCCESS;
    }

    if(status != BHS.RUNNING)
      currentPosition = 0;
    ////////////////////FORCING CODE INLINE////////////////////////////////
    //NOTE: we need to stop child in order to make it 
    //      reset its status and re-init on the next run,
    //      also we force currStatus to be BHS.RUNNING since
    //      logic in stop *needs* that even though we officially return
    //      running status *below* that call 
    currStatus = BHS.RUNNING;
    if(status != BHS.RUNNING)
      stop();

    return BHS.RUNNING;
  }
}

public class MonitorSuccessNode : SequentialNode
{
  override public BHS execute()
  {
    //var status = base.execute();
    ////////////////////FORCING CODE INLINE////////////////////////////////
    BHS status = BHS.SUCCESS;
    while(currentPosition < children.Count)
    {
      var currentTask = children[currentPosition];
      //status = currentTask.run();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.init();
      status = currentTask.execute();
      currentTask.currStatus = status;
      if(status != BHS.RUNNING)
        currentTask.deinit();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(status == BHS.SUCCESS)
        ++currentPosition;
      else
        break;
    } 
    if(status != BHS.RUNNING)
      currentPosition = 0;
    ////////////////////FORCING CODE INLINE////////////////////////////////

    if(status == BHS.SUCCESS)
      return BHS.SUCCESS;

    //NOTE: we need to stop children in order to make them 
    //      reset its status and re-init on the next run
    if(status == BHS.FAILURE)
      base.stop();

    return BHS.RUNNING;
  }
}

public class MonitorFailureNode : SequentialNode
{
  override public BHS execute()
  {
    //var status = base.execute();
    ////////////////////FORCING CODE INLINE////////////////////////////////
    BHS status = BHS.SUCCESS;
    while(currentPosition < children.Count)
    {
      var currentTask = children[currentPosition];
      //status = currentTask.run();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.init();
      status = currentTask.execute();
      currentTask.currStatus = status;
      if(status != BHS.RUNNING)
        currentTask.deinit();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(status == BHS.SUCCESS)
        ++currentPosition;
      else
        break;
    } 
    if(status != BHS.RUNNING)
      currentPosition = 0;
    ////////////////////FORCING CODE INLINE////////////////////////////////
    if(status == BHS.FAILURE)
      return BHS.SUCCESS;

    //NOTE: we need to stop children in order to make them 
    //      reset its status and re-init on the next run
    if(status == BHS.SUCCESS)
      base.stop();

    return BHS.RUNNING;
  }
}

public class DeferNode : BehaviorTreeInternalNode
{
  public BHS result = BHS.SUCCESS; 

  override public void init()
  {}

  //NOTE: does nothing on purpose because 
  //      action happens in its defer
  override public void deinit()
  {}

  override public void defer()
  {
    for(int i=0;i<children.Count;++i)
      children[i].run();

    deferChildren(children.Count);
  }

  override public BHS execute()
  {
    return result;
  }
}

public class LogicOpNode : GroupNode
{
  EnumBinaryOp type;

  public LogicOpNode(AST_BinaryOpExp node)
  {
    this.type = node.type;
  }

  public override void init()
  {
    if(children.Count != 2)
      throw new Exception("Bad children count: " + children.Count);

    currentPosition = 0;
  }

  override public BHS execute()
  {
    var interp = Interpreter.instance;

    while(true)
    {
      var selected = children[currentPosition];

      //return selected.run();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(selected.currStatus != BHS.RUNNING)
        selected.init();
      BHS status = selected.execute();
      selected.currStatus = status;
      if(selected.currStatus != BHS.RUNNING)
        selected.deinit();
      ////////////////////FORCING CODE INLINE////////////////////////////////

      if(status == BHS.RUNNING || status == BHS.FAILURE)
        return status;

      var v = interp.PopValue();

      if(type == EnumBinaryOp.OR)
      {
        if(v.bval == true)
        {
          interp.PushValue(DynVal.NewBool(true));
          return BHS.SUCCESS;
        }

        if(currentPosition == 0)
          ++currentPosition;
        else
        {
          interp.PushValue(DynVal.NewBool(false));
          return BHS.SUCCESS;
        }
      }
      else if(type == EnumBinaryOp.AND)
      {
        if(v.bval == false)
        {
          interp.PushValue(DynVal.NewBool(false));
          return BHS.SUCCESS;
        }

        if(currentPosition == 0)
          ++currentPosition;
        else 
        {
          interp.PushValue(DynVal.NewBool(true));
          return BHS.SUCCESS;
        }
      }
      else
        throw new Exception("Unsupported logic op:" + type);
    }
  }
}

public class IfNode : ScopeNode
{
  int curr_pos = -1;
  BehaviorTreeNode selected;

  override public void init()
  {
    if(children.Count < 2)
      throw new Exception("Bad children count: " + children.Count);

    selected = null;
    curr_pos = 0;
  }

  override public BHS execute()
  {
    var interp = Interpreter.instance;

    //NOTE: if there's no node yet, find appropriate one
    while(selected == null)
    {
      var cond = children[curr_pos];
      var status = cond.run();

      if(status == BHS.RUNNING || status == BHS.FAILURE)
        return status;

      var v = interp.PopValue();
      if(v.bval == false)
      {
        curr_pos += 2;

        if(curr_pos >= children.Count-1) //all of the children failed
        {
          var has_else = children.Count % 2 != 0;
          if(has_else)
            selected = children[curr_pos];
          else
            return BHS.SUCCESS;
        }
      }
      else
        selected = children[curr_pos + 1];
    }

    {
      //return selected.run();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(selected.currStatus != BHS.RUNNING)
        selected.init();
      BHS status = selected.execute();
      selected.currStatus = status;
      if(selected.currStatus != BHS.RUNNING)
        selected.deinit();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      return status;
    }
  }

  public override string inspect()
  {
    return "<- " + (curr_pos+1) + "x";
  }
}

public class LoopNode : ScopeNode
{
  BHS last_body_status;

  override public void init()
  {
    last_body_status = BHS.NONE;
    if(children.Count != 2)
      throw new Exception("Bad children count: " + children.Count);
  }

  override public BHS execute()
  {
    var cond = children[0];
    var body = children[1];
    var interp = Interpreter.instance;

    while(true)
    {
      //NOTE: allowing to recalculate the condition only if loop's body
      //      is not in RUNNING state (this way we don't recalculate condition
      //      when yielding from while loop)
      if(last_body_status != BHS.RUNNING)
      {
        var status = cond.run();
        if(status == BHS.RUNNING || status == BHS.FAILURE)
          return status;

        var v = interp.PopValue();

        if(v.bval == false)
          return BHS.SUCCESS;
      }

      try
      {
        last_body_status = body.run();
        if(last_body_status == BHS.RUNNING || last_body_status == BHS.FAILURE)
          return last_body_status;
      }
      catch(Interpreter.BreakException)
      {
        cond.stop();
        body.stop();
        return BHS.SUCCESS;
      }
    }
  }
}

public class InvertNode : BehaviorTreeDecoratorNode
{
  override public BHS execute()
  {
    BHS status = base.execute();

    if(status == BHS.FAILURE)
      return BHS.SUCCESS;
    else if(status == BHS.SUCCESS)
      return BHS.FAILURE;

    return status;
  }
}

public class ReturnNode : BehaviorTreeTerminalNode
{
  public override void init() 
  {
    var interp = Interpreter.instance;
    interp.JumpReturn();
  }
}

public class BreakNode : BehaviorTreeTerminalNode
{
  public override void init() 
  {
    var interp = Interpreter.instance;
    interp.JumpBreak();
  }
}

//TODO:
//public class ContinueNode : BehaviorTreeTerminalNode
//{
//  public override void init() 
//  {
//    var interp = Interpreter.instance;
//    interp.JumpContinue();
//  }
//}

public class PopValueNode : BehaviorTreeTerminalNode
{
  public override void init() 
  {
    var interp = Interpreter.instance;
    interp.PopValueEx(RefOp.USR_DEC | RefOp.DEC);
  }

  public override string inspect()
  {
    return "<-";
  }
}

public class PushValueNode : BehaviorTreeTerminalNode
{
  DynVal dv;

  public PushValueNode(DynVal dv)
  {
    this.dv = dv;
  }

  public override void init() 
  {
    var interp = Interpreter.instance;
    interp.PushValue(dv);
  }

  public override string inspect()
  {
    return "->";
  }
}

public class DupValueNode : BehaviorTreeTerminalNode
{
  public override void init() 
  {
    var interp = Interpreter.instance;
    interp.PushValue(interp.PeekValue());
  }

  public override string inspect()
  {
    return "->";
  }
}

public class ConstructNode : BehaviorTreeTerminalNode
{
  HashedName ntype;

  public ConstructNode(HashedName ntype)
  {
    this.ntype = ntype;
  }

  public override void init() 
  {
    var interp = Interpreter.instance;

    var cls = interp.symbols.Resolve(ntype) as ClassSymbol;
    if(cls == null)
      throw new Exception("Could not find class symbol: " + ntype);

    if(cls.creator == null)
      throw new Exception("Class doesn't have a creator: " + ntype);

    var val = DynVal.New(); 
    cls.creator(ref val);
    interp.PushValue(val);
  }

  public override string inspect()
  {
    return ntype + " ->";
  }
}

public class CallFuncPtr : FuncBaseCallNode
{
  public CallFuncPtr(AST_Call ast)
    : base(ast)
  {}

  public override void init() 
  {
    var interp = Interpreter.instance;
    var val = ast.type == EnumCall.LMBD ? interp.PopValue() : interp.GetScopeValue(ast.Name()); 

    var fct = ((FuncCtx)val.obj);
    //NOTE: Func ctx may be shared and we need to make sure 
    //      we get a *non used* version of the func node. For 
    //      this we could use FuncCtx.AutoClone() method but it tries
    //      to be as safe as possible and may allocate extra
    //      memory. However here we know exactly what we are doing
    //      and we can use more specific heuristics in order
    //      to avoid extra memory allocations
    if(fct.fnode != null && fct.fnode.currStatus == BHS.RUNNING)
      fct = fct.Clone();
    fct.Retain();
    var func_node = fct.GetNode();

    //NOTE: traversing arg.nodes only for the first time
    if(children.Count == 0)
    {
      interp.PushNode(this);
      int cargs_num = new FuncArgsInfo(ast.cargs_bits).CountArgs();
      for(int i=0;i<cargs_num;++i)
        interp.Visit(ast.children[i]);
      interp.PopNode();

      children.Add(func_node);
    }
    else
      children[children.Count-1] = func_node;

    base.init();
  }

  override public void deinit()
  {
    base.deinit();

    var func_node = ((FuncNode)children[children.Count-1]);
    func_node.fct.Release();
  } 

  public override string inspect()
  {
    if(ast.type == EnumCall.LMBD)
      return "<-";
    else
      return ""+ast.name;
  }
}

public class LiteralNode : BehaviorTreeTerminalNode
{
  AST_Literal node;

  public LiteralNode(AST_Literal node)
  {
    this.node = node;
  }

  public override void init() 
  {
    var interp = Interpreter.instance;

    if(node.type == bhl.EnumLiteral.NUM)
      interp.PushValue(DynVal.NewNum(node.nval));
    else if(node.type == bhl.EnumLiteral.BOOL)
      interp.PushValue(DynVal.NewBool(node.nval == 1));
    else if(node.type == bhl.EnumLiteral.STR)
      interp.PushValue(DynVal.NewStr(node.sval));
    else if(node.type == bhl.EnumLiteral.NIL)
      interp.PushValue(DynVal.NewNil());
    else
      throw new Exception("Bad literal:" + node.type);
  }

  public override string inspect()
  {
    return "(" + node.nval + "," + node.sval + ") ->";
  }
}

public class UnaryOpNode : BehaviorTreeTerminalNode
{
  AST_UnaryOpExp node;

  public UnaryOpNode(AST_UnaryOpExp node)
  {
    this.node = node;
  }

  public override void init()
  {
    var interp = Interpreter.instance;
    var a = interp.PopValue();

    if(node.type == EnumUnaryOp.NEG)
    {
      interp.PushValue(DynVal.NewNum(-a.num));
    }
    else if(node.type == EnumUnaryOp.NOT)
    {
      interp.PushValue(DynVal.NewBool(!a.bval));
    }
    else
      throw new Exception("Unsupported unary op:" + node.type);
  }

  public override string inspect()
  {
    return "(" + node.type + ") <- ->";
  }
}

public class BinaryOpNode : BehaviorTreeTerminalNode
{
  EnumBinaryOp type;

  public BinaryOpNode(AST_BinaryOpExp node)
  {
    this.type = node.type;
  }

  public override void init()
  {
    var interp = Interpreter.instance;
    var b = interp.PopValue();
    var a = interp.PopValue();

    //Console.WriteLine("BINARY OP " + node.type + " " + a + "," + b);

    if(type == EnumBinaryOp.ADD)
    {
      if(a.type == DynVal.STRING || b.type == DynVal.STRING)
        interp.PushValue(DynVal.NewStr(a._str + b._str));
      else
        interp.PushValue(DynVal.NewNum(a._num + b._num));
    }
    else if(type == EnumBinaryOp.EQ)
    {
      interp.PushValue(DynVal.NewBool(a.IsEqual(b)));
    }
    else if(type == EnumBinaryOp.NQ)
    {
      interp.PushValue(DynVal.NewBool(!a.IsEqual(b)));
    }
    else if(type == EnumBinaryOp.LT)
    {
      interp.PushValue(DynVal.NewBool(a._num < b._num));
    }
    else if(type == EnumBinaryOp.LTE)
    {
      interp.PushValue(DynVal.NewBool(a._num <= b._num));
    }
    else if(type == EnumBinaryOp.GT)
    {
      interp.PushValue(DynVal.NewBool(a._num > b._num));
    }
    else if(type == EnumBinaryOp.GTE)
    {
      interp.PushValue(DynVal.NewBool(a._num >= b._num));
    }
    else if(type == EnumBinaryOp.SUB)
    {
      interp.PushValue(DynVal.NewNum(a._num - b._num));
    }
    else if(type == EnumBinaryOp.MUL)
    {
      interp.PushValue(DynVal.NewNum(a._num * b._num));
    }
    else if(type == EnumBinaryOp.DIV)
    {
      interp.PushValue(DynVal.NewNum(a._num / b._num));
    }
    else if(type == EnumBinaryOp.MOD)
    {
      interp.PushValue(DynVal.NewNum((int)a._num % (int)b._num));
    }
    else if(type == EnumBinaryOp.BIT_AND)
    {
      interp.PushValue(DynVal.NewNum((int)a._num & (int)b._num));
    }
    else if(type == EnumBinaryOp.BIT_OR)
    {
      interp.PushValue(DynVal.NewNum((int)a._num | (int)b._num));
    }
    else
      throw new Exception("Unsupported binary op:" + type);
  }

  public override string inspect()
  {
    return "(" + type + ") <- <- ->";
  }
}

public class TypeCastNode : BehaviorTreeTerminalNode
{
  AST_TypeCast node;

  public TypeCastNode(AST_TypeCast node)
  {
    this.node = node;
  }

  public override void init()
  {
    var interp = Interpreter.instance;

    var val = interp.PopValue();
    var res = val.ValueClone();

    //TODO: add better casting support
    if(node.ntype == SymbolTable.symb_int.name.n)
    {
      res.SetNum((int)val.num);
    }
    else if(node.ntype == SymbolTable.symb_string.name.n && val.type != DynVal.STRING)
    {
      res.SetStr("" + val.num);
    }

    interp.PushValue(res);
  }

  public override string inspect()
  {
    return "(" + node.type + ") <- ->";
  }
}

public class VarAccessNode : BehaviorTreeTerminalNode
{
  public const int READ      = 0;
  public const int WRITE     = 1;
  public const int DECL      = 2;
  public const int GREAD     = 3;
  public const int GWRITE    = 4;

  HashedName name;
  public int mode = READ;

  public VarAccessNode(HashedName name, int mode)
  {
    this.name = name;
    this.mode = mode;
  }

  public override void init()
  {
    var interp = Interpreter.instance;

    if(mode == WRITE)
    {
      var val = interp.PopValue().ValueClone();
      //Console.WriteLine("WRITE " + val + " " + val.GetHashCode());
      interp.SetScopeValue(name, val);
      val.RefMod(RefOp.TRY_DEL);
    }
    else if(mode == READ)
    {
      var val = interp.GetScopeValue(name);
      //Console.WriteLine("READ " + val + " " + val.GetHashCode());
      interp.PushValue(val);
    }
    else if(mode == GREAD)
    {
      var val = interp.GetGlobalValue(name);
      //Console.WriteLine("GREAD " + val + " " + val.GetHashCode());
      interp.PushValue(val);
    }
    else if(mode == GWRITE)
    {
      var val = interp.PopValue().ValueClone();
      //Console.WriteLine("GWRITE " + val + " " + val.GetHashCode());
      interp.SetGlobalValue(name, val);
      val.RefMod(RefOp.TRY_DEL);
    }
    else if(mode == DECL)
    {
      //NOTE: declaring new variable only if it wasn't declared before, 
      //      this may happen in a loop
      var val = interp.TryGetScopeValue(name);
      if(val == null)
        interp.SetScopeValue(name, DynVal.NewNil());
      else
        val.SetNil();
    }
    else
      throw new Exception("Unknown mode: " + mode);
  }

  public override string inspect()
  {
    string str = name + " ";
    if(mode == WRITE)
      str += "<- =";
    else if(mode == READ)
      str += "->";
    else if(mode == DECL)
      str += "=";
    return str;
  }
}

public class MVarAccessNode : BehaviorTreeTerminalNode
{
  public const int READ                = 1;
  public const int WRITE               = 2;
  public const int WRITE_PUSH_CTX      = 3;
  public const int WRITE_INV_ARGS      = 4;
  public const int READ_PUSH_CTX       = 5;
  public const int READ_REF            = 6;

  uint scope_ntype;
  HashedName name;

  public int mode = READ;

  //NOTE: caching class member symbol
  Symbol cls_member;
  bool need_super_class_bind_obj; 

  public MVarAccessNode(uint scope_ntype, HashedName name, int mode = READ)
  {
    this.scope_ntype = scope_ntype;
    this.name = name;
    this.mode = mode;
  }

  public override void init()
  {
    var interp = Interpreter.instance;
    
    if(cls_member == null)
    {
      var cls = interp.symbols.Resolve(scope_ntype) as ClassSymbol;
      if(cls == null)
        throw new Exception("Class binding not found: " + scope_ntype); 

      cls_member = cls.ResolveMember(name.n);
      if(cls_member == null)
        throw new Exception("Member not found: " + name);

      //TODO: move this check to frontend layer
      if(cls is ClassSymbolScript && cls_member.scope is ClassSymbolNative)
        need_super_class_bind_obj = true;
    }

    var var_symb = (VariableSymbol)cls_member;
    if(var_symb == null)
      throw new Exception("Not a variable symbol: " + name);

    if(mode == READ || mode == READ_PUSH_CTX || mode == READ_REF)
    {
      var ctx = mode == READ_PUSH_CTX ? interp.PeekValue() : interp.PopValue();

      if(var_symb is FieldSymbol)
      {
        if(need_super_class_bind_obj)
          ctx = ((DynValDict)ctx.obj).Get(0);

        if(mode == READ_REF)
        {
          DynVal val;
          (var_symb as FieldSymbol).getref(ctx, out val);
          interp.PushValue(val);
          //NOTE: this can be an operation for the temp. object,
          //      we need to take care of that
          ctx.RefMod(RefOp.USR_TRY_DEL);
        }
        else
        {
          var val = DynVal.New();
          (var_symb as FieldSymbol).getter(ctx, ref val);
          interp.PushValue(val);
          //NOTE: this can be an operation for the temp. object,
          //      we need to take care of that
          ctx.RefMod(RefOp.USR_TRY_DEL);
        }
      }
      else
        throw new Exception("Not implemented");
    }
    else if(mode == WRITE || mode == WRITE_PUSH_CTX || mode == WRITE_INV_ARGS)
    {
      DynVal val = null;
      DynVal ctx = null;

      if(mode == WRITE_PUSH_CTX || mode == WRITE_INV_ARGS)
      {
        val = interp.PopRef();
        ctx = mode == WRITE_PUSH_CTX ? interp.PeekValue() : interp.PopValue();
      }
      else
      {
        ctx = interp.PopValue();
        val = interp.PopRef();
      }

      if(var_symb is FieldSymbol)
      {
        if(need_super_class_bind_obj)
          ctx = ((DynValDict)ctx.obj).Get(0);

        (var_symb as FieldSymbol).setter(ref ctx, val);
        val.RefMod(RefOp.TRY_DEL);
      }
      else
        throw new Exception("Not implemented");
    }
  }

  public override string inspect()
  {
    string str = scope_ntype + "," + name + " "; 

    if(mode == WRITE)
      str += "<-c <-v =";
    else if(mode == WRITE_INV_ARGS)
      str += "<-v <-c =";
    else if(mode == WRITE_PUSH_CTX)
      str += "<-v <-c = c->";
    else if(mode == READ)
      str += "<-c v->";
    else if(mode == READ_REF)
      str += "<-c ref v->";
    else if(mode == READ_PUSH_CTX)
      str += "<-c c-> v->";

    return str;
  }
}

public class IncNode : BehaviorTreeTerminalNode
{
  HashedName name;

  public IncNode(HashedName name)
  {
    this.name = name;
  }

  public override void init()
  {
    var interp = Interpreter.instance;
    var val = interp.GetScopeValue(name);
    val.num++;
  }

  public override string inspect()
  {
    return "" + name; 
  }
}

public class DecNode : BehaviorTreeTerminalNode
{
  HashedName name;

  public DecNode(HashedName name)
  {
    this.name = name;
  }

  public override void init()
  {
    var interp = Interpreter.instance;
    var val = interp.GetScopeValue(name);
    val.num--;
  }

  public override string inspect()
  {
    return "" + name; 
  }
}

abstract public class FuncNode : SequentialNode
{
  public FuncCtx fct;
  public FuncArgsInfo args_info;

  public virtual AST GetDeclArg(int i)
  {
    return null;
  }

  public virtual int GetTotalArgsNum()
  {
    return 0;
  }

  public virtual int GetDefaultArgsNum()
  {
    return 0;
  }

  public int GetRequiredArgsNum()
  {
    return GetTotalArgsNum() - GetDefaultArgsNum();
  }

  public virtual HashedName GetName()
  {
    return new HashedName();
  }
}

public class FuncNodeScript : FuncNode
{
  public AST_FuncDecl decl;

  bool inflated = false;

  protected DynValDict mem = new DynValDict();

  public FuncNodeScript(AST_FuncDecl decl, FuncCtx fct)
  {
    this.fct = fct;
    this.decl = decl;
  }

  public override int GetTotalArgsNum()
  {
    var fparams = decl.fparams();
    return fparams.children.Count;
  }

  public override int GetDefaultArgsNum()
  {
    return decl.GetDefaultArgsNum();
  }

  public override AST GetDeclArg(int i)
  {
    var children = decl.fparams().GetChildren();
    return children.Count == 0 ? null : children[i] as AST;
  }

  public override HashedName GetName()
  {
    return decl.Name();
  }

  public void Inflate()
  {
    if(inflated)
      return;

    var interp = Interpreter.instance;
    interp.PushNode(this);
    interp.VisitChildren(decl.block());
    interp.PopNode();
    inflated = true;
  }

  public override void init()
  {
    var interp = Interpreter.instance;

    Inflate();

    var fparams = decl.fparams();
    var func_args = fparams.children.Count == 0 ? 0 : fparams.children.Count;

    //NOTE: setting args passed to func
    for(int i=func_args;i-- > 0;)
    {
      var fparam = (AST_VarDecl)fparams.children[i];
      var fparam_name = fparam.Name();

      var fparam_val = fparam.is_ref ? interp.PopRef() : interp.PopValue().ValueClone();
      //Console.WriteLine(fparam_name + "=" + fparam_val + (fparam.IsRef() ? " ref " : " ") + fparam_val.GetHashCode());
      mem.Set(fparam_name, fparam_val);

      fparam_val.RefMod(RefOp.TRY_DEL);
    }

    base.init();
  }

  public override void deinit()
  {
    var interp = Interpreter.instance;

    //NOTE: let defer have access to the memory
    interp.PushScope(mem);
    base.deinit();
    interp.PopScope();

    mem.Clear();
  }

  public override BHS execute() 
  {
    var interp = Interpreter.instance;

    interp.PushScope(mem);

    BHS status;
    try
    {
      //status = base.execute();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      status = BHS.SUCCESS;
      while(currentPosition < children.Count)
      {
        var currentTask = children[currentPosition];
        //status = currentTask.run();
        ////////////////////FORCING CODE INLINE////////////////////////////////
        if(currentTask.currStatus != BHS.RUNNING)
          currentTask.init();
        status = currentTask.execute();
        currentTask.currStatus = status;
        if(status != BHS.RUNNING)
          currentTask.deinit();
        ////////////////////FORCING CODE INLINE////////////////////////////////
        if(status == BHS.SUCCESS)
          ++currentPosition;
        else
          break;
      } 
      if(status != BHS.RUNNING)
        currentPosition = 0;
      ////////////////////FORCING CODE INLINE////////////////////////////////
    }
    catch(Interpreter.ReturnException)
    {
      //Console.WriteLine("CATCH RETURN " + decl.nname + " " + e.val);
      status = BHS.SUCCESS;
      this.stop();
    }

    interp.PopScope();

    return status;
  }

  public override string inspect()
  {
    return decl.Name() + "(<- x " + this.args_info.CountArgs() + ")";
  }
}

public class MethodNodeScript : FuncNodeScript
{
  private readonly HashedName this_keyword_hashed_name = new HashedName("this");

  public MethodNodeScript(AST_FuncDecl decl, FuncCtx fct)
    : base(decl, fct)
  { }

  public override void init()
  {
    base.init();
    var obj_this = Interpreter.instance.PopValue().obj;
    mem.Set(this_keyword_hashed_name, DynVal.NewObj(obj_this));
  }
}

public class FuncNodeLambda : FuncNodeScript
{
  public FuncNodeLambda(FuncCtx fct)
    : base((fct.fs as LambdaSymbol).decl, fct)
  {}

  public override void init()
  {
    this.mem.CopyFrom(fct.mem);

    base.init();
  }

  public override string inspect()
  {
    return this.decl.Name() + " use " + ((AST_LambdaDecl)this.decl).upvals.Count + "x =";
  }
}

//NOTE: this one is ugly and probably should not event exist 
//      but it does just for the consistency
public class FuncNodeNative : FuncNode
{
  public FuncSymbolNative symb;

  public FuncNodeNative(FuncSymbolNative symb, FuncCtx fct)
  {
    this.fct = fct;
    this.symb = symb;

    if(symb.func_creator == null)
      throw new Exception("Function binding is not found: " + symb.name);
    this.addChild(symb.func_creator());
  }

  public override void init() 
  {
    if(this.args_info.CountArgs() > symb.GetMembers().Count)
      throw new Exception("Too many args for func " + symb.name);

    base.init();
  }

  public override HashedName GetName()
  {
    return symb.name;
  }

  public override string inspect()
  {
    return symb.name + "(<- x " + this.args_info.CountArgs() + ") ";
  }
}

public class PushFuncCtxNode : BehaviorTreeTerminalNode
{
  FuncSymbol fs;
  FuncCtx fct;

  public PushFuncCtxNode(FuncSymbol fs)
  {
    this.fs = fs;
  }

  public override void init()
  {
    //Console.WriteLine("PUSH CTX " + this.GetHashCode());

    var interp = Interpreter.instance;

    fct = FuncCtx.New(fs);
    //NOTE: we really want FuncCtx to be alive while
    //      the node using this func ctx is still active.
    //      See also defer() below
    fct.Retain();

    var lmb = fs as LambdaSymbol;
    if(lmb != null)
    {
      //Console.WriteLine("PUSH LCTX " + this.GetHashCode() + " " + ldecl.useparams.Count);
      //setting use params to its own memory scope
      for(int i=0;i<lmb.decl.upvals.Count;++i)
      {
        var up = lmb.decl.upvals[i];
        var val = interp.GetScopeValue(up.Name());
        fct.mem.Set(up.Name(), val);
      }
    }

    var fdv = DynVal.NewObj(fct);
    //Util.Debug("PUSH FCTX: " + fct.decl.Name());
    interp.PushValue(fdv);
  }

  public override void defer()
  {
    fct.Release();
    fct = null;
  }

  public override string inspect()
  {
    return fs.name + " ->";
  }
}

public class SimpleFunctorNode : BehaviorTreeTerminalNode
{
  public delegate BHS Functor();

  Functor fn;
  HashedName name;

  public SimpleFunctorNode(Functor fn, HashedName name/*for debug*/)
  {
    this.fn = fn;
    this.name = name;
  }

  public override BHS execute()
  {
    return fn();
  }

  public override string inspect()
  {
    return name.s;
  }
}

//////////////////////////////////////////////////////

public class Array_NewNode : BehaviorTreeTerminalNode
{
  public override void init()
  {
    var interp = Interpreter.instance;
    var val = DynVal.NewObj(DynValList.New());
    interp.PushValue(val);
  }

  public override string inspect()
  {
    return "->";
  }
}

public class Array_NewNodeT<T> : BehaviorTreeTerminalNode where T : new()
{
  public override void init()
  {
    var interp = Interpreter.instance;
    var arr = DynVal.NewObj(ArrayTypeSymbolT<T>.Creator());
    interp.PushValue(arr);
  }

  public override string inspect()
  {
    return "->";
  }
}

public class Array_AddNode : BehaviorTreeTerminalNode
{
  public bool push_arr = false;

  public override void init()
  {
    var interp = Interpreter.instance;

    //NOTE: args are in reverse order in stack
    var val = interp.PopValue().ValueClone();
    var arr = push_arr ? interp.PeekValue() : interp.PopValue();

    var lst = arr.obj as DynValList;
    if(lst == null)
      throw new UserError("Not a DynValList: " + (arr.obj != null ? arr.obj.GetType().Name : ""+arr));
    lst.Add(val);
    //NOTE: this can be an operation for the temp. array,
    //      we need to try del the array if so
    lst.TryDel();
  }

  public override string inspect()
  {
    if(push_arr)
      return "<- <- ->";
    else
      return "<- <-";
  }
}

public class Array_AddNodeT<T> : Array_AddNode where T : new()
{
  public override void init()
  {
    var interp = Interpreter.instance;

    //NOTE: args are in reverse order in stack
    var val = interp.PopValue();
    var arr = push_arr ? interp.PeekValue() : interp.PopValue();

    var lst = (arr.obj as IList<T>);
    if(lst == null)
      throw new UserError("Not an array:" + (arr.obj != null ? arr.obj.GetType().Name : ""));
    T obj = new T();
    ArrayTypeSymbolT<T>.Convert(val, ref obj);
    lst.Add(obj);
  }
}

public class Array_AtNode : BehaviorTreeTerminalNode
{
  public override void init()
  {
    var interp = Interpreter.instance;

    //NOTE: args are in reverse order in stack
    var idx = interp.PopValue();
    var arr = interp.PopValue();

    var lst = arr.obj as DynValList;
    if(lst == null)
      throw new UserError("Not a DynValList: " + (arr.obj != null ? arr.obj.GetType().Name : ""+arr));

    var res = lst[(int)idx.num]; 
    interp.PushValue(res);
    //NOTE: this can be an operation for the temp. array,
    //      we need to try del the array if so
    lst.TryDel();
  }

  public override string inspect()
  {
    return "<- <- ->";
  }
}

public class Array_SetAtNode : BehaviorTreeTerminalNode
{
  public override void init()
  {
    var interp = Interpreter.instance;

    var idx = interp.PopValue();
    var arr = interp.PopValue();
    var val = interp.PopValue().ValueClone();

    var lst = arr.obj as DynValList;
    if(lst == null)
      throw new UserError("Not a DynValList: " + (arr.obj != null ? arr.obj.GetType().Name : ""+arr));

    lst[(int)idx.num] = val; 
    //NOTE: this can be an operation for the temp. array,
    //      we need to try del the array if so
    lst.TryDel();
  }

  public override string inspect()
  {
    return "<- <- <-";
  }
}

public class Array_SetAtNodeT<T> : BehaviorTreeTerminalNode where T : new()
{
  public override void init()
  {
    var interp = Interpreter.instance;

    //NOTE: args are in reverse order in stack
    var arr = interp.PopValue();
    var idx = interp.PopValue();
    var val = interp.PopValue();

    var lst = (arr.obj as IList<T>);
    if(lst == null)
      throw new UserError("Not a List<" + typeof(T).Name + ">: " + (arr.obj != null ? arr.obj.GetType().Name : ""));

    T obj = new T();
    ArrayTypeSymbolT<T>.Convert(val, ref obj);
    lst[(int)idx.num] = obj;
  }

  public override string inspect()
  {
    return "<- <- <-";
  }
}

public class Array_AtNodeT<T> : BehaviorTreeTerminalNode
{
  public override void init()
  {
    var interp = Interpreter.instance;

    //NOTE: args are in reverse order in stack
    var idx = interp.PopValue();
    var arr = interp.PopValue();

    var lst = (arr.obj as IList<T>);
    if(lst == null)
      throw new UserError("Not a List<" + typeof(T).Name + ">: " + (arr.obj != null ? arr.obj.GetType().Name : ""));

    var res = lst[(int)idx.num]; 
    var val = DynVal.NewObj(res);
    interp.PushValue(val);
  }

  public override string inspect()
  {
    return "<- <- ->";
  }
}

public class Array_RemoveAtNode : BehaviorTreeTerminalNode
{
  public override void init()
  {
    var interp = Interpreter.instance;

    //NOTE: args are in reverse order in stack
    var idx = interp.PopValue();
    var arr = interp.PopValue();

    var lst = arr.obj as DynValList;
    if(lst == null)
      throw new UserError("Not a DynValList: " + (arr.obj != null ? arr.obj.GetType().Name : ""+arr));

    lst.RemoveAt((int)idx.num); 
    //NOTE: this can be an operation for the temp. array,
    //      we need to try del the array if so
    lst.TryDel();
  }

  public override string inspect()
  {
    return "<- <-";
  }
}

public class Array_RemoveAtNodeT : BehaviorTreeTerminalNode
{
  public override void init()
  {
    var interp = Interpreter.instance;

    //NOTE: args are in reverse order in stack
    var idx = interp.PopValue();
    var arr = interp.PopValue();

    var lst = arr.obj as IList;
    if(lst == null)
      throw new UserError("Not an array");

    lst.RemoveAt((int)idx.num); 
  }

  public override string inspect()
  {
    return "<- <-";
  }
}

public class Array_ClearNode : BehaviorTreeTerminalNode
{
  public override void init()
  {
    var interp = Interpreter.instance;

    var arr = interp.PopValue();

    var lst = arr.obj as DynValList;
    if(lst == null)
      throw new UserError("Not an array");
    lst.Clear();
    //NOTE: this can be an operation for the temp. array,
    //      we need to try del the array if so
    lst.TryDel();
  }

  public override string inspect()
  {
    return "<-";
  }
}

public class Array_ClearNodeT : BehaviorTreeTerminalNode
{
  public override void init()
  {
    var interp = Interpreter.instance;

    var arr = interp.PopValue();

    var lst = arr.obj as IList;
    if(lst == null)
      throw new UserError("Not an array");
    lst.Clear();
  }

  public override string inspect()
  {
    return "<-";
  }
}

} //namespace bhl
