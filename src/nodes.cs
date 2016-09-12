using System;
using System.IO;
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

public enum BhvPolicy 
{
  SUCCEED_ON_ONE = 0, 
  SUCCEED_ON_ALL = 1
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

public abstract class BehaviorTreeNode : BehaviorVisitable
{
  //NOTE: semi-private, public for inlining
  public BHS currStatus; 
  //TODO: this one is for inspecting purposes only
  public BHS lastExecuteStatus; 
  
  public BehaviorTreeNode()
  {
    currStatus = BHS.NONE;
    lastExecuteStatus = currStatus;
  }

  public virtual void accept(BehaviorVisitor v)
  {
    v.visit(this);
  }

  //NOTE: this method is heavily used for inlining, 
  //      don't change its contents if you are not sure
  public BHS run(object agent)
  {
    if(currStatus != BHS.RUNNING)
      init(agent);
    currStatus = execute(agent);
    lastExecuteStatus = currStatus;
    if(currStatus != BHS.RUNNING)
      deinit(agent);
    return currStatus;
  }

  public virtual void stop(object agent)
  {
    if(currStatus == BHS.RUNNING)
      deinit(agent);
    if(currStatus != BHS.NONE)
      defer(agent);
    currStatus = BHS.NONE;
  }

  public virtual string inspect(object agent) { return ""; }

  public BHS getStatus() { return currStatus; }
  public BHS getExecuteStatus() { return lastExecuteStatus; }
  public void resetExecuteStatus() { lastExecuteStatus = currStatus; }

  //NOTE: methods below should never be called directly
  // This method will be invoked before the node is executed for the first time.
  public abstract void init(object agent);
  // This method will be invoked once if node was running and then stopped running.
  public abstract void deinit(object agent);
  // This method is invoked when the node should be run.
  public abstract BHS execute(object agent);
  // This method is invoked when the node goes out of the scope
  public abstract void defer(object agent);
}

public abstract class BehaviorTreeTerminalNode : BehaviorTreeNode
{
  public override void init(object agent)
  {}

  public override void deinit(object agent)
  {}

  public override void defer(object agent)
  {}

  public override BHS execute(object agent)
  {
    return BHS.SUCCESS;
  }
}

public abstract class BehaviorTreeInternalNode : BehaviorTreeNode
{
  public List<BehaviorTreeNode> children = new List<BehaviorTreeNode>();

  public BehaviorTreeInternalNode()
  {}

  ~BehaviorTreeInternalNode()
  {}

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

  //NOTE: normally you never really want to override this one
  override public void deinit(object agent)
  {
    stopChildren(agent);
  }

  //NOTE: normally you never really want to override this one
  override public void defer(object agent)
  {}

  protected void stopChildren(object agent)
  {
    //NOTE: stopping children in the reverse order
    for(int i=children.Count;i-- > 0;)
      children[i].stop(agent);
  }
}

public abstract class BehaviorTreeDecoratorNode : BehaviorTreeInternalNode
{
  public override void init(object agent)
  {
    if(children.Count != 1)
      throw new Exception("One child is expected");
  }

  public override BHS execute(object agent)
  {
    //BHS status = children[0].run(agent);
    BHS status;
    var currentTask = children[0];
    ////////////////////FORCING CODE INLINE////////////////////////////////
    if(currentTask.currStatus != BHS.RUNNING)
      currentTask.init(agent);
    status = currentTask.execute(agent);
    currentTask.currStatus = status;
    currentTask.lastExecuteStatus = currentTask.currStatus;
    if(currentTask.currStatus != BHS.RUNNING)
      currentTask.deinit(agent);
    ////////////////////FORCING CODE INLINE////////////////////////////////
    return status;
  }

  override public void deinit(object agent)
  {
    //NOTE: we don't stop children here because this node behaves NOT like
    //      a block node but rather emulating a terminal node
  } 

  override public void defer(object agent)
  {
    stopChildren(agent);
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

public class AlwaysRunning : BehaviorTreeNode
{
  override public BHS execute(object agent)
  {
    return BHS.RUNNING;
  }
  override public void init(object agent){}
  override public void deinit(object agent){}
  override public void defer(object agent){}
}

//////////////////////////////////////////

public class YieldOnce : BehaviorTreeNode
{
  bool yielded = false;

  override public BHS execute(object agent)
  {
    if(!yielded)
    {
      yielded = true;
      return BHS.RUNNING;
    }
    else
      return BHS.SUCCESS;
  }
  override public void init(object agent)
  {
    yielded = false;
  }
  override public void deinit(object agent){}
  override public void defer(object agent){}
}

//////////////////////////////////////////

public class AlwaysSuccess : BehaviorTreeNode
{
  override public BHS execute(object agent)
  {
    return BHS.SUCCESS;
  }
  override public void init(object agent){}
  override public void deinit(object agent){}
  override public void defer(object agent){}
}

//////////////////////////////////////////

public class AlwaysFailure : BehaviorTreeNode
{
  override public BHS execute(object agent)
  {
    return BHS.FAILURE;
  }
  override public void init(object agent){}
  override public void deinit(object agent){}
  override public void defer(object agent){}
}


/////////////////////////////////////////////////////////////

public class SequentialNode : BehaviorTreeInternalNode
{
  protected int currentPosition = 0;

  override public void init(object agent)
  {
    currentPosition = 0;
  }

  override public BHS execute(object agent)
  {
    BHS status = BHS.SUCCESS;
    while(currentPosition < children.Count)
    {
      var currentTask = children[currentPosition];
      //status = currentTask.run(agent);
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.init(agent);
      status = currentTask.execute(agent);
      currentTask.currStatus = status;
      currentTask.lastExecuteStatus = currentTask.currStatus;
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.deinit(agent);
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
  override public BHS execute(object agent)
  {
    //var status = base.execute(agent);
    ////////////////////FORCING CODE INLINE////////////////////////////////
    BHS status = BHS.SUCCESS;
    while(currentPosition < children.Count)
    {
      var currentTask = children[currentPosition];
      //status = currentTask.run(agent);
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.init(agent);
      status = currentTask.execute(agent);
      currentTask.currStatus = status;
      currentTask.lastExecuteStatus = currentTask.currStatus;
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.deinit(agent);
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
      status = BHS.SUCCESS;
    return status;
  }
}

public class GroupNode : SequentialNode
{
  override public void deinit(object agent)
  {
    //NOTE: we don't stop children here because this node behaves NOT like
    //      a block node but rather emulating a terminal node
  }

  override public void defer(object agent)
  {
    stopChildren(agent);
  }
}

public class FuncCallNode : SequentialNode 
{
  const int FUNC_INIT     = 0;
  const int FUNC_READY    = 1;
  const int FUNC_DETACHED = 2;

  int func_status = FUNC_INIT;

  AST_Call node;

  public FuncCallNode(AST_Call node)
  {
    this.node = node;
  }

  void InflateUserFunc(Interpreter interp, PoolItem pi)
  {
    pi.fnode.args_num = node.cargs_num;
    var default_args_num = pi.fnode.DefaultArgsNum();

    //1. func args 
    for(int i=0;i<node.cargs_num;++i)
      interp.Visit(node.children[i]);

    //2. evaluating default args
    for(int i=0;i<default_args_num;++i)
    {
      var decl_arg = pi.fnode.DeclArg(node.cargs_num + i);
      if(decl_arg.children.Count == 0)
        throw new Exception("Bad default arg at idx " + (node.cargs_num + i) + " func " + pi.fnode.GetName());
      interp.Visit(decl_arg.children[0]);
    }
  }

  void Inflate()
  {
    if(func_status == FUNC_INIT)
    {
      func_status = FUNC_READY;

      var interp = Interpreter.instance;

      interp.PushNode(this);

      var pi = PoolRequest(node);

      InflateUserFunc(interp, pi);

      pool_item = pi;
      this.addChild(pool_item.bnode);

      interp.PopNode();
    }
    else if(func_status == FUNC_DETACHED)
    {
      func_status = FUNC_READY;

      var pi = PoolRequest(node);
      pi.fnode.args_num = node.cargs_num;

      children[children.Count-1] = pi.bnode;
      pool_item = pi;
    }
  }

  override public BHS execute(object agent)
  {
    var interp = Interpreter.instance;

    interp.PushFuncArgsNum(node.cargs_num);

    //var status = base.execute(agent);
    ////////////////////FORCING CODE INLINE////////////////////////////////
    BHS status = BHS.SUCCESS;
    while(currentPosition < children.Count)
    {
      var currentTask = children[currentPosition];
      //status = currentTask.run(agent);
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.init(agent);
      status = currentTask.execute(agent);
      currentTask.currStatus = status;
      currentTask.lastExecuteStatus = currentTask.currStatus;
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.deinit(agent);
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(status == BHS.SUCCESS)
        ++currentPosition;
      else
        break;
    } 
    if(status != BHS.RUNNING)
      currentPosition = 0;
    ////////////////////FORCING CODE INLINE////////////////////////////////

    interp.PopFuncArgsNum();

    return status;
  }

  override public void init(object agent)
  {
    Inflate();

    base.init(agent);
  }

  override public void deinit(object agent)
  {
    //NOTE: we don't stop children here because this node behaves NOT like
    //      a block node but rather emulating a terminal node
  }

  override public void defer(object agent)
  {
    stopChildren(agent);

    if(!pool_item.IsEmpty() && pool_item.IsCached())
    {
      if(pool_item.bnode.getStatus() != BHS.NONE)
        throw new Exception("Bad status: " + pool_item.bnode.getStatus());
      PoolFree(pool_item);
      pool_item.Clear();
      func_status = FUNC_DETACHED;
    }
  }

  override public string inspect(object agent) 
  {
    return "" + node.Name();
  }

  ///////////////////////////////////////////////////////////////////
  static public bool PoolUse = true;

  PoolItem pool_item;
  static int free_count = 0;
  static int last_pool_id = 0;
  static int valid_pool_id = -1;

  struct PoolItem
  {
    public int id;
    public int idx;
    public int next_free;

    public AST_Call ast;
    public FuncNode fnode;
    public BehaviorTreeNode bnode;

    public PoolItem(AST_Call _ast)
    {
      id = 0;
      idx = -1;
      next_free = -1;
      ast = _ast;
      fnode = null;
      bnode = null;
    }

    public bool IsEmpty()
    {
      return ast == null;
    }

    public bool IsCached()
    {
      return idx != -1;
    }

    public void Clear()
    {
      idx = -1;
      next_free = -1;
      ast = null;
      fnode = null;
      bnode = null;
    }
  }

  static Dictionary<ulong, int> func2last_free = new Dictionary<ulong, int>();
  static List<PoolItem> pool = new List<PoolItem>();

  static int pool_hit;
  static int pool_miss;

  static PoolItem PoolRequest(AST_Call node)
  {
    ulong pool_id = node.FuncId(); 

    if(PoolUse)
    {
      int pool_idx = -1;
      if(func2last_free.TryGetValue(pool_id, out pool_idx) && pool_idx != -1)
      {
        var pi = pool[pool_idx];

        if(pi.bnode.getStatus() != BHS.NONE)
          throw new Exception("Bad status: " + pi.bnode.getStatus());
        ++pool_hit;
        --free_count;

        func2last_free[pool_id] = pi.next_free;

        pi.next_free = -1;
        pool[pool_idx] = pi;
        return pi;
      }
    }

    {
      var pi = new PoolItem(node);

      bool can_cache = InitPoolItem(ref pi);

      //NOTE: only userland funcs are put into cache
      if(PoolUse && can_cache)
      {
        pi.idx = pool.Count;
        pi.next_free = -1;
        func2last_free[pool_id] = -1;
        pool.Add(pi);
      }

      ++pool_miss;

      return pi;
    }
  }

  static void PoolFree(PoolItem pi)
  {
    if(!PoolUse)
      return;

    //NOTE: in case pool was cleared
    if(pi.id <= valid_pool_id)
      return;

    ulong pool_id = pi.ast.FuncId();
    int last_free = func2last_free[pool_id];
    pi.next_free = last_free;
    pool[pi.idx] = pi;
    func2last_free[pool_id] = pi.idx;
    ++free_count;
  }

  static bool InitPoolItem(ref PoolItem pi)
  {
    var interp = Interpreter.instance;

    pi.id = ++last_pool_id;
    var fnode = interp.GetFuncNode(pi.ast);
    pi.fnode = fnode;
    var fbnd = fnode as FuncNodeBinding; 
    //NOTE: optimization for C# func binding
    pi.bnode = fbnd != null ? fbnd.CreateBindingNode() : fnode;
    return fbnd == null;
  }

  static public void PoolClear()
  {
    valid_pool_id = last_pool_id;
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
    get {
      return free_count;
    }
  }
}

public class ParallelNode : BehaviorTreeInternalNode
{
  private BhvPolicy succeedPolicy;

  public ParallelNode(BhvPolicy policy)
  {
    succeedPolicy = policy;
  }

  override public void init(object agent) 
  {}

  override public BHS execute(object agent)
  {
    bool sawAllSuccess = true;
    for(int i=0;i<children.Count;++i)
    {
      var currentTask = children[i];

      if(currentTask.getStatus() == BHS.NONE || currentTask.getStatus() == BHS.RUNNING)
      {
        //BHS status = currentTask.run(agent);
        BHS status;
        ////////////////////FORCING CODE INLINE////////////////////////////////
        if(currentTask.currStatus != BHS.RUNNING)
          currentTask.init(agent);
        status = currentTask.execute(agent);
        currentTask.currStatus = status;
        currentTask.lastExecuteStatus = currentTask.currStatus;
        if(currentTask.currStatus != BHS.RUNNING)
          currentTask.deinit(agent);
        ////////////////////FORCING CODE INLINE////////////////////////////////
        if(status == BHS.FAILURE)
          return BHS.FAILURE;

        if(status == BHS.SUCCESS && succeedPolicy == BhvPolicy.SUCCEED_ON_ONE)
          return BHS.SUCCESS;

        if(status == BHS.RUNNING)
          sawAllSuccess = false;
      }
    }

    if(succeedPolicy == BhvPolicy.SUCCEED_ON_ALL && sawAllSuccess)
      return BHS.SUCCESS;

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

public class PriorityNode : BehaviorTreeInternalNode
{
  private int currentPosition = -1;

  override public void init(object agent)
  {
    currentPosition = -1;
  }

  override public BHS execute(object agent)
  {
    BHS status;

    if(currentPosition != -1) //there's one still running
    {
      var currentTask = children[currentPosition];
      //status = currentTask.run(agent);
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.init(agent);
      status = currentTask.execute(agent);
      currentTask.currStatus = status;
      currentTask.lastExecuteStatus = currentTask.currStatus;
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.deinit(agent);
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

    {
      BehaviorTreeNode currentTask = children[currentPosition];
       //keep trying children until one doesn't fail
      while(true)
      {
        //status = currentTask.run(agent);
        ////////////////////FORCING CODE INLINE////////////////////////////////
        if(currentTask.currStatus != BHS.RUNNING)
          currentTask.init(agent);
        status = currentTask.execute(agent);
        currentTask.currStatus = status;
        currentTask.lastExecuteStatus = currentTask.currStatus;
        if(currentTask.currStatus != BHS.RUNNING)
          currentTask.deinit(agent);
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

public class EvalNode : SequentialNode
{
  override public BHS execute(object agent)
  {
    //var status = base.execute(agent);
    ////////////////////FORCING CODE INLINE////////////////////////////////
    BHS status = BHS.SUCCESS;
    while(currentPosition < children.Count)
    {
      var currentTask = children[currentPosition];
      //status = currentTask.run(agent);
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.init(agent);
      status = currentTask.execute(agent);
      currentTask.currStatus = status;
      currentTask.lastExecuteStatus = currentTask.currStatus;
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.deinit(agent);
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(status == BHS.SUCCESS)
        ++currentPosition;
      else
        break;
    } 
    if(status != BHS.RUNNING)
      currentPosition = 0;
    ////////////////////FORCING CODE INLINE////////////////////////////////

    if(status != BHS.RUNNING)
    {
      var interp = Interpreter.instance;
      interp.PushValue(new DynVal(status == BHS.SUCCESS ? true : false));
      //since we are inside eval block we should not propagate failures
      if(status == BHS.FAILURE)
        status = BHS.SUCCESS;
    }

    return status;
  }

  public override string inspect(object agent)
  {
    return "->";
  }
}

public class ForeverNode : SequentialNode
{
  override public BHS execute(object agent)
  {
    //var status = base.execute(agent);
    ////////////////////FORCING CODE INLINE////////////////////////////////
    BHS status = BHS.SUCCESS;
    while(currentPosition < children.Count)
    {
      var currentTask = children[currentPosition];
      //status = currentTask.run(agent);
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.init(agent);
      status = currentTask.execute(agent);
      currentTask.currStatus = status;
      currentTask.lastExecuteStatus = currentTask.currStatus;
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.deinit(agent);
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(status == BHS.SUCCESS)
        ++currentPosition;
      else
        break;
    } 
    if(status != BHS.RUNNING)
      currentPosition = 0;
    ////////////////////FORCING CODE INLINE////////////////////////////////
    //NOTE: we need to stop child in order to make it 
    //      reset its status and re-init on the next run
    if(status != BHS.RUNNING)
      base.stop(agent);

    return BHS.RUNNING;
  }
}

public class MonitorSuccessNode : SequentialNode
{
  override public BHS execute(object agent)
  {
    //var status = base.execute(agent);
    ////////////////////FORCING CODE INLINE////////////////////////////////
    BHS status = BHS.SUCCESS;
    while(currentPosition < children.Count)
    {
      var currentTask = children[currentPosition];
      //status = currentTask.run(agent);
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.init(agent);
      status = currentTask.execute(agent);
      currentTask.currStatus = status;
      currentTask.lastExecuteStatus = currentTask.currStatus;
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.deinit(agent);
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
      base.stop(agent);

    return BHS.RUNNING;
  }
}

public class MonitorFailureNode : SequentialNode
{
  private BHS status_on_failure;

  public MonitorFailureNode(BHS _status_on_failure = BHS.FAILURE)
  {
    status_on_failure = _status_on_failure;
  }
  
  override public BHS execute(object agent)
  {
    //var status = base.execute(agent);
    ////////////////////FORCING CODE INLINE////////////////////////////////
    BHS status = BHS.SUCCESS;
    while(currentPosition < children.Count)
    {
      var currentTask = children[currentPosition];
      //status = currentTask.run(agent);
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.init(agent);
      status = currentTask.execute(agent);
      currentTask.currStatus = status;
      currentTask.lastExecuteStatus = currentTask.currStatus;
      if(currentTask.currStatus != BHS.RUNNING)
        currentTask.deinit(agent);
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
      return status_on_failure;

    //NOTE: we need to stop children in order to make them 
    //      reset its status and re-init on the next run
    if(status == BHS.SUCCESS)
      base.stop(agent);

    return BHS.RUNNING;
  }
}

public class DeferNode : BehaviorTreeInternalNode
{
  public BHS result = BHS.SUCCESS; 

  override public void init(object agent)
  {}

  //NOTE: does nothing on purpose
  override public void deinit(object agent)
  {}

  override public void defer(object agent)
  {
    for(int i=0;i<children.Count;++i)
      children[i].run(agent);

    stopChildren(agent);
  }

  override public BHS execute(object agent)
  {
    return result;
  }
}

public class IfNode : BehaviorTreeInternalNode
{
  int curr_pos = -1;
  BehaviorTreeNode  selected;

  override public void init(object agent)
  {
    if(children.Count < 2)
      throw new Exception("Bad children count: " + children.Count);

    selected = null;
    curr_pos = 0;
  }

  override public BHS execute(object agent)
  {
    var interp = Interpreter.instance;

    //NOTE: if there's no node yet, find appropriate one
    while(selected == null)
    {
      var cond = children[curr_pos];
      var status = cond.run(agent);

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
      //return selected.run(agent);
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(selected.currStatus != BHS.RUNNING)
        selected.init(agent);
      BHS status = selected.execute(agent);
      selected.currStatus = status;
      selected.lastExecuteStatus = selected.currStatus;
      if(selected.currStatus != BHS.RUNNING)
        selected.deinit(agent);
      ////////////////////FORCING CODE INLINE////////////////////////////////
      return status;
    }
  }

  public override string inspect(object agent)
  {
    return "<- " + (curr_pos+1) + "x";
  }
}

public class LoopNode : BehaviorTreeInternalNode
{
  override public void init(object agent)
  {
    if(children.Count != 2)
      throw new Exception("Bad children count: " + children.Count);
  }

  override public BHS execute(object agent)
  {
    var cond = children[0];
    var body = children[1];
    var interp = Interpreter.instance;

    while(true)
    {
      var status = cond.run(agent);
      if(status == BHS.RUNNING || status == BHS.FAILURE)
        return status;

      var v = interp.PopValue();

      if(v.bval == false)
        return BHS.SUCCESS;

      try
      {
        var body_status = body.run(agent);
        if(body_status == BHS.RUNNING || body_status == BHS.FAILURE)
          return body_status;
      }
      catch(Interpreter.BreakException)
      {
        cond.stop(agent);
        body.stop(agent);
        return BHS.SUCCESS;
      }
    }
  }
}

public class InvertNode : BehaviorTreeDecoratorNode
{
  override public BHS execute(object agent)
  {
    BHS status = base.execute(agent);

    if(status == BHS.FAILURE)
      return BHS.SUCCESS;
    else if(status == BHS.SUCCESS)
      return BHS.FAILURE;

    return status;
  }
}


public class ReturnNode : BehaviorTreeTerminalNode
{
  public override void init(object agent) 
  {
    var interp = Interpreter.instance;
    interp.JumpReturn();
  }
}

public class BreakNode : BehaviorTreeTerminalNode
{
  public override void init(object agent) 
  {
    var interp = Interpreter.instance;
    interp.JumpBreak();
  }
}

//public class ContinueNode : BehaviorTreeTerminalNode
//{
//  public override void init(object agent) 
//  {
//    var interp = Interpreter.instance;
//    interp.JumpContinue();
//  }
//}

public class PopValueNode : BehaviorTreeTerminalNode
{
  public override void init(object agent) 
  {
    var interp = Interpreter.instance;
    interp.PopValue();
  }

  public override string inspect(object agent)
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

  public override void init(object agent) 
  {
    var interp = Interpreter.instance;
    interp.PushValue(dv);
  }

  public override string inspect(object agent)
  {
    return "->";
  }
}

public class ResetConfigNode : BehaviorTreeTerminalNode
{
  public bool push = false;
  public BehaviorTreeNode conf_node;
  public ConfNodeSymbol conf_symb;

  public ResetConfigNode(ConfNodeSymbol conf_symb, BehaviorTreeNode conf_node, bool push = false)
  {
    this.conf_symb = conf_symb;
    this.conf_node = conf_node;
    this.push = push;
  }

  public override void init(object agent) 
  {
    var dv = new DynVal();
    conf_symb.conf_getter(conf_node, ref dv, true/*reset*/);

    if(push)
    {
      var interp = Interpreter.instance;
      interp.PushValue(dv);
    }
  }

  public override string inspect(object agent)
  {
    return push ? "->" : "";
  }
}

public class ConstructNode : BehaviorTreeTerminalNode
{
  HashedName ntype;

  public ConstructNode(HashedName ntype)
  {
    this.ntype = ntype;
  }

  public override void init(object agent) 
  {
    var interp = Interpreter.instance;

    var bnd = interp.bindings.FindBinding<ClassSymbol>(ntype.n);
    if(bnd == null)
      throw new Exception("Could not find class binding: " + ntype);

    if(bnd.creator == null)
      throw new Exception("Class binding doesn't have creator: " + ntype);

    var val = new DynVal(); 
    bnd.creator(ref val);
    interp.PushValue(val);
  }

  public override string inspect(object agent)
  {
    return ntype + " ->";
  }
}

public class CallVarFuncPtr : BehaviorTreeDecoratorNode
{
  HashedName name;

  public CallVarFuncPtr(HashedName name)
  {
    this.name = name;
  }

  public override void init(object agent) 
  {
    var interp = Interpreter.instance;
    var fct = (FuncCtx)interp.GetScopeValue(name).obj;

    var func_node = fct.GetNode();
    this.setSlave(func_node);

    base.init(agent);
  }
}

public class LiteralNode : BehaviorTreeTerminalNode
{
  AST_Literal node;

  public LiteralNode(AST_Literal node)
  {
    this.node = node;
  }

  public override void init(object agent) 
  {
    var interp = Interpreter.instance;

    if(node.type == bhl.EnumLiteral.NUM)
      interp.PushValue(new DynVal(node.nval));
    else if(node.type == bhl.EnumLiteral.BOOL)
      interp.PushValue(new DynVal(node.nval == 1));
    else if(node.type == bhl.EnumLiteral.STR)
      interp.PushValue(new DynVal(node.sval));
    else if(node.type == bhl.EnumLiteral.NIL)
    {
      var dv = new DynVal();
      dv.obj = null;
      interp.PushValue(dv);
    }
    else
      throw new Exception("Bad literal:" + node.type);
  }

  public override string inspect(object agent)
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

  public override void init(object agent)
  {
    var interp = Interpreter.instance;
    var a = interp.PopValue();

    if(node.type == EnumUnaryOp.NEG)
    {
      interp.PushValue(new DynVal(-a.num));
    }
    else if(node.type == EnumUnaryOp.NOT)
    {
      interp.PushValue(new DynVal(!a.bval));
    }
    else
      throw new Exception("Unsupported unary op:" + node.type);
  }

  public override string inspect(object agent)
  {
    return "(" + node.type + ") <- ->";
  }
}

public class BinaryOpNode : BehaviorTreeTerminalNode
{
  AST_BinaryOpExp node;

  public BinaryOpNode(AST_BinaryOpExp node)
  {
    this.node = node;
  }

  public override void init(object agent)
  {
    var interp = Interpreter.instance;
    var b = interp.PopValue();
    var a = interp.PopValue();

    //Console.WriteLine("BINARY OP " + node.type + " " + a + "," + b);

    if(node.type == EnumBinaryOp.AND)
    {
      //TODO: what about short-circuit behavior of '&&' ?
      interp.PushValue(new DynVal(a.bval && b.bval));
    }
    else if(node.type == EnumBinaryOp.OR)
    {
      interp.PushValue(new DynVal(a.bval || b.bval));
    }
    else if(node.type == EnumBinaryOp.ADD)
    {
      if(a.type == DynVal.STRING && a.type == b.type)
        interp.PushValue(new DynVal(a.str + b.str));
      else
        interp.PushValue(new DynVal(a.num + b.num));
    }
    else if(node.type == EnumBinaryOp.EQ)
    {
      interp.PushValue(new DynVal(a.IsEqual(b)));
    }
    else if(node.type == EnumBinaryOp.NQ)
    {
      interp.PushValue(new DynVal(!a.IsEqual(b)));
    }
    else if(node.type == EnumBinaryOp.LT)
    {
      interp.PushValue(new DynVal(a.num < b.num));
    }
    else if(node.type == EnumBinaryOp.LTE)
    {
      interp.PushValue(new DynVal(a.num <= b.num));
    }
    else if(node.type == EnumBinaryOp.GT)
    {
      interp.PushValue(new DynVal(a.num > b.num));
    }
    else if(node.type == EnumBinaryOp.GTE)
    {
      interp.PushValue(new DynVal(a.num >= b.num));
    }
    else if(node.type == EnumBinaryOp.SUB)
    {
      interp.PushValue(new DynVal(a.num - b.num));
    }
    else if(node.type == EnumBinaryOp.MUL)
    {
      interp.PushValue(new DynVal(a.num * b.num));
    }
    else if(node.type == EnumBinaryOp.DIV)
    {
      interp.PushValue(new DynVal(a.num / b.num));
    }
    else if(node.type == EnumBinaryOp.MOD)
    {
      interp.PushValue(new DynVal((int)a.num % (int)b.num));
    }
    else if(node.type == EnumBinaryOp.BIT_AND)
    {
      interp.PushValue(new DynVal((int)a.num & (int)b.num));
    }
    else if(node.type == EnumBinaryOp.BIT_OR)
    {
      interp.PushValue(new DynVal((int)a.num | (int)b.num));
    }
    else
      throw new Exception("Unsupported binary op:" + node.type);
  }

  public override string inspect(object agent)
  {
    return "(" + node.type + ") <- <- ->";
  }
}

public class TypeCastNode : BehaviorTreeTerminalNode
{
  AST_TypeCast node;

  public TypeCastNode(AST_TypeCast node)
  {
    this.node = node;
  }

  public override void init(object agent)
  {
    var interp = Interpreter.instance;
    var val = interp.PopValue();

    //TODO: add better casting support
    if(node.ntype == SymbolTable._int.nname)
    {
      val.Set((int)val.num);
    }
    else if(node.ntype == SymbolTable._string.nname && val.type != DynVal.STRING)
    {
      val.Set("" + val.num);
    }

    interp.PushValue(val);
  }

  public override string inspect(object agent)
  {
    return "(" + node.type + ") <- ->";
  }
}

public class VarAccessNode : BehaviorTreeTerminalNode
{
  public const int READ      = 0;
  public const int WRITE     = 1;
  public const int DECL      = 2;
  public const int DECL_REF  = 3;

  HashedName name;
  public int mode = READ;

  public VarAccessNode(HashedName name, int mode = READ)
  {
    this.name = name;
    this.mode = mode;
  }

  public override void init(object agent)
  {
    var interp = Interpreter.instance;

    if(mode == WRITE)
    {
      var val = interp.PopValue();
      interp.SetScopeValue(name, val);
    }
    else if(mode == READ)
    {
      var val = interp.GetScopeValue(name);
      interp.PushValue(val);
    }
    else if(mode == DECL)
    {
      var val = new DynVal();
      interp.SetScopeValue(name, val);
    }
    else if(mode == DECL_REF)
    {
      var val = new DynVal();
      interp.SetScopeValue(name, val);
    }
    else
      throw new Exception("Unknown mode: " + mode);
  }

  public override string inspect(object agent)
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
  public const int READ           = 1;
  public const int WRITE          = 2;
  public const int WRITE_PUSH_CTX = 3;
  public const int WRITE2         = 4;
  public const int READ_PUSH_CTX  = 5;

  uint scope_ntype;
  HashedName name;

  public int mode = READ;

  Symbol bnd_member;

  public MVarAccessNode(uint scope_ntype, HashedName name, int mode = READ)
  {
    this.scope_ntype = scope_ntype;
    this.name = name;
    this.mode = mode;
  }

  public override void init(object agent)
  {
    var interp = Interpreter.instance;
    
    //TODO: support user classes as well
    if(bnd_member == null)
    {
      var bnd = interp.bindings.FindBinding<ClassSymbol>(scope_ntype);
      if(bnd == null)
        throw new Exception("Class binding not found: " + scope_ntype); 

      bnd_member = bnd.findMember(name.n);
      if(bnd_member == null)
        throw new Exception("Member not found: " + name);
    }

    if(mode == READ || mode == READ_PUSH_CTX)
    {
      var var_symb = (VariableSymbol)bnd_member;
      if(var_symb == null)
        throw new Exception("Not a variable symbol: " + name);

      var ctx = interp.PopValue();

      var val = new DynVal();

      if(var_symb is FieldSymbol)
        (var_symb as FieldSymbol).getter(ctx, ref val);
      else
        throw new Exception("Not implemented");

      if(mode == READ_PUSH_CTX)
        interp.PushValue(ctx);

      interp.PushValue(val);
    }
    else if(mode == WRITE || mode == WRITE_PUSH_CTX || mode == WRITE2)
    {
      var var_symb = (VariableSymbol)bnd_member;
      if(var_symb == null)
        throw new Exception("Not a variable symbol: " + name);

      DynVal val = new DynVal();
      DynVal ctx = new DynVal();

      if(mode == WRITE_PUSH_CTX || mode == WRITE2)
      {
        val = interp.PopValue();
        ctx = interp.PopValue();
      }
      else
      {
        ctx = interp.PopValue();
        val = interp.PopValue();
      }

      if(var_symb is FieldSymbol)
      {
        (var_symb as FieldSymbol).setter(ref ctx, val);
        val.TryRelease();
      }
      else
        throw new Exception("Not implemented");

      if(mode == WRITE_PUSH_CTX)
        interp.PushValue(ctx);
    }
  }

  public override string inspect(object agent)
  {
    string str = scope_ntype + "," + name + " "; 

    if(mode == WRITE)
      str += "<-c <-v =";
    else if(mode == WRITE2)
      str += "<-v <-c =";
    else if(mode == WRITE_PUSH_CTX)
      str += "<-v <-c = c->";
    else if(mode == READ)
      str += "<-c v->";
    else if(mode == READ_PUSH_CTX)
      str += "<-c c-> v->";

    return str;
  }
}

public class FuncNode : SequentialNode
{
  public bool has_void_value;
  public int args_num;

  public void SetArgs(params DynVal[] args)
  {
    var interp = Interpreter.instance;

    args_num = args.Length;

    for(int i=0;i<args.Length;++i)
    {
      var arg = args[i];
      interp.PushValue(arg);
    }
  }

  public virtual int DeclArgsNum()
  {
    return 0;
  }

  public virtual AST DeclArg(int i)
  {
    return null;
  }

  public int DefaultArgsNum()
  {
    return DeclArgsNum() - args_num;
  }

  public virtual string GetName()
  {
    return "";
  }
}

public class FuncNodeAST : FuncNode
{
  public AST_FuncDecl decl;

  bool inflated = false;

  protected MemoryScope mem = new MemoryScope();

  public FuncNodeAST(AST_FuncDecl decl)
  {
    this.decl = decl;
    this.has_void_value = decl.ntype == SymbolTable._void.nname;
  }

  public override int DeclArgsNum()
  {
    var fparams = decl.fparams();
    return fparams.children.Count == 0 ? 0 : fparams.children[0].children.Count;
  }

  public override AST DeclArg(int i)
  {
    var fparams = decl.fparams();
    return fparams.children.Count == 0 ? null : fparams.children[0].children[i];
  }

  public override string GetName()
  {
    return decl.name;
  }

  public void Inflate()
  {
    if(inflated)
      return;

    var interp = Interpreter.instance;
    interp.PushNode(this, false/*don't attach to current*/);
    interp.VisitChildren(decl.block());
    interp.PopNode();
    inflated = true;
  }

  public override void init(object agent)
  {
    var interp = Interpreter.instance;

    Inflate();

    var fparams = decl.fparams();
    var func_args = fparams.children.Count == 0 ? 0 : fparams.children[0].children.Count;

    //Util.Debug("CALL FUNC AST INIT " + func_args);

    //setting args passed to func
    for(int i=func_args;i-- > 0;)
    {
      var fparam = (AST_VarDecl)fparams.children[0].children[i];
      var fparam_name = fparam.Name();
      var fparam_val = interp.PopValue();

      //Util.Debug(fparam_name + "=" + fparam_val);
      mem.Set(fparam_name, fparam_val);
    }

    base.init(agent);
  }

  public override void deinit(object agent)
  {
    var interp = Interpreter.instance;

    //NOTE: let defer have access to the memory
    interp.PushScope(mem);
    base.deinit(agent);
    interp.PopScope();

    mem.Clear();
  }

  public override BHS execute(object agent) 
  {
    var interp = Interpreter.instance;

    interp.PushScope(mem);

    BHS status;
    try
    {
      //status = base.execute(agent);
      ////////////////////FORCING CODE INLINE////////////////////////////////
      status = BHS.SUCCESS;
      while(currentPosition < children.Count)
      {
        var currentTask = children[currentPosition];
        //status = currentTask.run(agent);
        ////////////////////FORCING CODE INLINE////////////////////////////////
        if(currentTask.currStatus != BHS.RUNNING)
          currentTask.init(agent);
        status = currentTask.execute(agent);
        currentTask.currStatus = status;
        currentTask.lastExecuteStatus = currentTask.currStatus;
        if(currentTask.currStatus != BHS.RUNNING)
          currentTask.deinit(agent);
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
      this.stop(agent);
    }

    interp.PopScope();

    return status;
  }

  public override string inspect(object agent)
  {
    return decl.Name() + "(<- x " + this.args_num + ")" + (has_void_value ? "" : "(->)");
  }
}

public class FuncNodeLambda : FuncNodeAST
{
  public FuncCtx ctx;

  public FuncNodeLambda(FuncCtx ctx)
    : base(ctx.fr.decl)
  {
    this.ctx = ctx;
  }

  public override void init(object agent)
  {
    this.mem.CopyFrom(ctx.mem);

    base.init(agent);
  }

  public override string inspect(object agent)
  {
    return this.decl.Name() + " use " + ((AST_LambdaDecl)this.decl).useparams.Count + "x =";
  }
}

public class PushFuncCtxNode : BehaviorTreeTerminalNode
{
  FuncRef fr;
  FuncCtx fct;

  public PushFuncCtxNode(AST_FuncDecl decl, FuncBindSymbol fbnd)
  {
    fr = new FuncRef(decl, fbnd);
  }

  public override void init(object agent)
  {
    var interp = Interpreter.instance;

    fct = FuncCtx.PoolRequest(fr);
    //NOTE: explicitely increasing refs number during function call
    //      so that func ctx is alive while function is called
    fct.IncRefs();

    var ldecl = fr.decl as AST_LambdaDecl;
    if(ldecl != null)
    {
      //copying use params to its own memory scope
      for(int i=0;i<ldecl.useparams.Count;++i)
      {
        var up = ldecl.useparams[i];
        var val = interp.GetScopeValue(up.Name());
        fct.mem.Set(up.Name(), val);
      }
    }

    var fdv = new DynVal();
    fdv.obj = fct;
    //Util.Debug("PUSH FCTX: " + fct.decl.Name() + " " + agent.GetHashCode());
    interp.PushValue(fdv);
  }

  public override void defer(object agent)
  {
    //NOTE: decreasing refs on defer
    fct.DecRefs();
    fct = null;
  }

  public override string inspect(object agent)
  {
    return fr.Name() + " ->";
  }
}

public class FuncNodeBinding : FuncNode
{
  public FuncBindSymbol symb;

  public FuncNodeBinding(FuncBindSymbol symb)
  {
    this.symb = symb;

    this.has_void_value = symb.type == SymbolTable._void;

    this.addChild(CreateBindingNode());
  }

  public BehaviorTreeNode CreateBindingNode()
  {
    if(symb.func_creator == null)
      throw new Exception("Function binding is not found: " + symb.nname);

    return symb.func_creator();
  }

  public override void init(object agent) 
  {
    if(this.args_num > symb.GetMembers().Count)
      throw new Exception("Too many args for func " + symb.nname);

    base.init(agent);
  }

  public override string GetName()
  {
    return symb.name;
  }

  public override string inspect(object agent)
  {
    return symb.name + "(<- x " + this.args_num + ") ";
  }
}

public class SimpleFunctorNode : BehaviorTreeTerminalNode
{
  public delegate BHS Functor(object agent);

  Functor fn;
  string name;

  public SimpleFunctorNode(Functor fn, string name/*for debug*/)
  {
    this.fn = fn;
    this.name = name;
  }

  public override BHS execute(object agent)
  {
    return fn(agent);
  }

  public override string inspect(object agent)
  {
    return name;
  }
}

//////////////////////////////////////////////////////

public class Array_NewNode : BehaviorTreeTerminalNode
{
  public override void init(object agent)
  {
    var interp = Interpreter.instance;
    var val = DynValList.PoolRequest().ToDynVal();
    interp.PushValue(val);
  }

  public override string inspect(object agent)
  {
    return "->";
  }
}

public class Array_NewNodeT<T> : BehaviorTreeTerminalNode
{
  public override void init(object agent)
  {
    var interp = Interpreter.instance;
    var arr = new DynVal();
    arr.obj = new List<T>();
    interp.PushValue(arr);
  }

  public override string inspect(object agent)
  {
    return "->";
  }
}

public class Array_AddNode : BehaviorTreeTerminalNode
{
  public bool push_arr = false;

  public override void init(object agent)
  {
    var interp = Interpreter.instance;

    //NOTE: args are in reverse order in stack
    var val = interp.PopValue();
    var arr = interp.PopValue();

    var lst = arr.obj as DynValList;
    if(lst == null)
      throw new UserError("Not a DynValList: " + (arr.obj != null ? arr.obj.GetType().Name : ""));
    val.IncRefs();
    lst.Add(val);

    if(push_arr)
      interp.PushValue(arr);
  }

  public override string inspect(object agent)
  {
    if(push_arr)
      return "<- <- ->";
    else
      return "<- <-";
  }
}

public class Array_AddNodeT<T> : Array_AddNode where T : new()
{
  public override void init(object agent)
  {
    var interp = Interpreter.instance;

    //NOTE: args are in reverse order in stack
    var val = interp.PopValue();
    var arr = interp.PopValue(dec_refs: !push_arr);

    var lst = (arr.obj as List<T>);
    if(lst == null)
      throw new UserError("Not an array:" + (arr.obj != null ? arr.obj.GetType().Name : ""));
    T obj = new T();
    ArrayTypeSymbolT<T>.Convert(val, ref obj);
    lst.Add(obj);

    if(push_arr)
      interp.PushValue(arr);
  }
}

public class Array_AtNode : BehaviorTreeTerminalNode
{
  public override void init(object agent)
  {
    var interp = Interpreter.instance;

    //NOTE: args are in reverse order in stack
    var idx = interp.PopValue();
    var arr = interp.PopValue();

    var lst = arr.obj as DynValList;
    if(lst == null)
      throw new UserError("Not a DynValList: " + (arr.obj != null ? arr.obj.GetType().Name : ""));

    var res = lst[(int)idx.num]; 
    interp.PushValue(res);
  }

  public override string inspect(object agent)
  {
    return "<- <- ->";
  }
}

public class Array_AtNodeT<T> : BehaviorTreeTerminalNode
{
  public override void init(object agent)
  {
    var interp = Interpreter.instance;

    //NOTE: args are in reverse order in stack
    var idx = interp.PopValue();
    var arr = interp.PopValue();

    var lst = (arr.obj as List<T>);
    if(lst == null)
      throw new UserError("Not a List<" + typeof(T).Name + ">: " + (arr.obj != null ? arr.obj.GetType().Name : ""));

    var res = lst[(int)idx.num]; 
    var val = new DynVal();
    val.obj = res;
    interp.PushValue(val);
  }

  public override string inspect(object agent)
  {
    return "<- <- ->";
  }
}

public class Array_RemoveAtNode : BehaviorTreeTerminalNode
{
  public override void init(object agent)
  {
    var interp = Interpreter.instance;

    //NOTE: args are in reverse order in stack
    var idx = interp.PopValue();
    var arr = interp.PopValue();

    var lst = (arr.obj as IList);
    if(lst == null)
      throw new UserError("Not an array");

    lst.RemoveAt((int)idx.num); 
  }

  public override string inspect(object agent)
  {
    return "<- <-";
  }
}

} //namespace bhl
