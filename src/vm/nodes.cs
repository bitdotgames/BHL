//#define DEBUG_STACK
using System;
using System.Collections.Generic;

#pragma warning disable CS8981

namespace bhl {

public enum BHS
{
  NONE    = 0, 
  SUCCESS = 1, 
  FAILURE = 2, 
  RUNNING = 3,
}

public interface IBehaviorTreeNode
{
  BHS last_status { get; set; } 

  //NOTE: methods below should never be called directly
  // This method will be invoked before the node is executed for the first time.
  void Init();
  // This method will be invoked once if node was running and then stopped running.
  void Deinit();
  // This method is invoked when the node should be run.
  BHS Execute();
  // This method is invoked when the node goes out of the scope
  void Defer();
}

public static class NodeExtensions
{
  public static BHS Run(this IBehaviorTreeNode node)
  {
    if(node.last_status != BHS.RUNNING)
      node.Init();
    node.last_status = node.Execute();
    if(node.last_status != BHS.RUNNING)
      node.Deinit();
    return node.last_status;
  }

  public static void Stop(this IBehaviorTreeNode node)
  {
    if(node.last_status == BHS.RUNNING)
      node.Deinit();
    if(node.last_status != BHS.NONE)
      node.Defer();
    node.last_status = BHS.NONE;
  }
}

public abstract class BehaviorTreeNode : IBehaviorTreeNode, ITask
{
  BHS _last_status = BHS.NONE; 
  public BHS last_status { 
    get {
      return _last_status;
    }
    set {
      _last_status = value;
    }
  }

  public abstract void Init();
  public abstract void Deinit();
  public abstract BHS Execute();
  public abstract void Defer();

  bool ITask.Tick()
  {
    return this.Run() == BHS.RUNNING;
  }

  void ITask.Stop()
  {
    //being explicit to avoid possible recursion
    NodeExtensions.Stop(this);
  }
}

public abstract class BehaviorTreeTerminalNode : BehaviorTreeNode
{
  public override void Init()
  {}

  public override void Deinit()
  {}

  public override void Defer()
  {}

  public override BHS Execute()
  {
    return BHS.SUCCESS;
  }
}

public abstract class BehaviorTreeInternalNode : BehaviorTreeNode
{
  public List<IBehaviorTreeNode> children = new List<IBehaviorTreeNode>();

  public virtual BehaviorTreeInternalNode AddChild(IBehaviorTreeNode new_child)
  {
    children.Add(new_child);
    return this;
  }

  protected void DeinitAndDeferChildren(int start, int len)
  {
    for(int i=start;i >= 0 && i < (start+len) && i < children.Count; ++i)
    {
      var c = children[i];
      if(c.last_status == BHS.RUNNING)
      {
        c.Deinit();
        c.last_status = BHS.SUCCESS;
      }
    }

    //NOTE: traversing children in the reverse order
    //TODO: traverse from start+len index
    for(int i=children.Count;i-- > 0;)
    {
      var c = children[i];
      if(c.last_status != BHS.NONE)
      {
        c.Defer();
        c.last_status = BHS.NONE;
      }
    }
  }

  protected void DeferChildren(int start)
  {
    //NOTE: traversing children in the reverse order
    for(int i=start;i-- > 0;)
    {
      var c = children[i];

      if(c.last_status != BHS.NONE)
      {
        if(c.last_status == BHS.RUNNING)
          throw new Exception("Node is RUNNING during defer");

        c.Defer();
        c.last_status = BHS.NONE;
      }
    }
  }

  protected void DeinitChildren(int start, int len)
  {
    for(int i=start;i >= 0 && i < (start+len) && i < children.Count; ++i)
    {
      var c = children[i];
      if(c.last_status == BHS.RUNNING)
      {
        c.Deinit();
        c.last_status = BHS.SUCCESS;
      }
    }
  }

  protected void StopChildren()
  {
    //NOTE: traversing children in the reverse order
    for(int i=children.Count;i-- > 0;)
    {
      var c = children[i];
      c.Stop();
    }
  }
}

//NOTE: Scope node is a base building block for nodes with scope, e.g seq { .. }.
//      Once the node is deinited it stops all its children in reverse order. 
//      Stop invokes deinit and defer.
public abstract class ScopeNode : BehaviorTreeInternalNode
{
  //NOTE: normally you never really want to override this one
  public override void Deinit()
  {
    //NOTE: Trying to deinit all of its children may be suboptimal, so
    //      it makes sense to override it for specific cases
    DeinitAndDeferChildren(0, children.Count);
  }

  //NOTE: normally you never really want to override this one
  public override void Defer()
  {}
}

public abstract class BehaviorTreeDecoratorNode : BehaviorTreeInternalNode
{
  public override void Init()
  {
    if(children.Count != 1)
      throw new Exception("One child is expected, given " + children.Count);
  }

  public override BHS Execute()
  {
    return children[0].Run();
  }

  public override void Deinit()
  {
    DeinitChildren(0, 1);
  } 

  public override void Defer()
  {
    DeferChildren(children.Count);
  }

  public void SetSlave(IBehaviorTreeNode node)
  {
    if(children.Count == 0)
      this.AddChild(node);
    //NOTE: should we stop the current one?
    else
      children[0] = node;
  }
}

//////////////////////////////////////////

public class suspend : BehaviorTreeNode
{
  public override BHS Execute()
  {
    return BHS.RUNNING;
  }
  public override void Init(){}
  public override void Deinit(){}
  public override void Defer(){}
}

//////////////////////////////////////////

public class yield : BehaviorTreeNode
{
  public override BHS Execute()
  {
    return last_status == BHS.NONE ? BHS.RUNNING : BHS.SUCCESS;
  }
  public override void Init(){}
  public override void Deinit(){}
  public override void Defer(){}
}

//////////////////////////////////////////
               
public class nop : BehaviorTreeNode
{
  public override BHS Execute()
  {
    return BHS.SUCCESS;
  }
  public override void Init(){}
  public override void Deinit(){}
  public override void Defer(){}
}

//////////////////////////////////////////

public class fail : BehaviorTreeNode
{
  public override BHS Execute()
  {
    return BHS.FAILURE;
  }
  public override void Init(){}
  public override void Deinit(){}
  public override void Defer(){}
}

/////////////////////////////////////////////////////////////

public class SequentialNode : ScopeNode
{
  protected int curr_pos = 0;

  public override void Init()
  {
    curr_pos = 0;
  }

  public override void Deinit()
  {
    DeinitAndDeferChildren(curr_pos, 1);
  }

  public override BHS Execute()
  {
    BHS status = BHS.SUCCESS;
    while(curr_pos < children.Count)
    {
      var current = children[curr_pos];
      status = current.Run();
      if(status == BHS.SUCCESS)
        ++curr_pos;
      else
        break;
    } 
    return status;
  }
}

public class SequentialNode_ : SequentialNode
{
  public override BHS Execute()
  {
    BHS status = base.Execute();
    if(status == BHS.FAILURE)
      status = BHS.SUCCESS;
    return status;
  }
}

public class ParallelNode : ScopeNode
{
  public override void Init() 
  {}

  public override BHS Execute()
  {
    for(int i=0;i<children.Count;++i)
    {
      var current = children[i];

      if(current.last_status == BHS.NONE || 
          current.last_status == BHS.RUNNING)
      {
        BHS status = current.Run();
        if(status != BHS.RUNNING)
          return status;
      }
    }

    return BHS.RUNNING;
  }

  public override BehaviorTreeInternalNode AddChild(IBehaviorTreeNode new_child)
  {
    //force DEFER to keep running
    var d = new_child as DeferNode;
    if(d != null)
      d.result = BHS.RUNNING;
    
    children.Add(new_child);
    return this;
  }
}

public class ParallelAllNode : ScopeNode
{
  public override void Init() 
  {}

  public override BHS Execute()
  {
    bool all_success = true;
    for(int i=0;i<children.Count;++i)
    {
      var current = children[i];

      if(current.last_status == BHS.NONE || 
          current.last_status == BHS.RUNNING)
      {
        BHS status = current.Run();

        if(status == BHS.FAILURE)
          return BHS.FAILURE;

        if(status == BHS.RUNNING)
          all_success = false;
      }
    }

    if(all_success)
      return BHS.SUCCESS;

    return BHS.RUNNING;
  }
}

public class PriorityNode : ScopeNode
{
  private int curr_pos = -1;

  public override void Init()
  {
    curr_pos = -1;
  }

  public override BHS Execute()
  {
    BHS status;

    //there's one still running
    if(curr_pos != -1)
    {
      var current = children[curr_pos];
      status = current.Run();
      if(status == BHS.RUNNING)
        return BHS.RUNNING;
      else if(status == BHS.SUCCESS)
      {
        curr_pos = -1;
        return BHS.SUCCESS;
      }
      else if(status == BHS.FAILURE)
      {
        curr_pos++;
        if(curr_pos == (int)children.Count)
        {
          curr_pos = -1;
          return BHS.FAILURE;
        }
      }
    }
    else
    {
      curr_pos = 0;
    }

    if(children.Count == 0)
      return BHS.SUCCESS;

    //first run
    {
      var current = children[curr_pos];
      //keep trying children until one doesn't fail
      while(true)
      {
        status = current.Run();
        if(status != BHS.FAILURE)
          break;
      
        curr_pos++;
        if(curr_pos == (int)children.Count) //all of the children failed
        {
          curr_pos = -1;
          return BHS.FAILURE;
        }
        current = children[curr_pos];
      }
    }

    return status;
  }

  public override BehaviorTreeInternalNode AddChild(IBehaviorTreeNode new_child)
  {
    //force DEFER to proceed to the next node
    var d = new_child as DeferNode;
    if(d != null)
      d.result = BHS.FAILURE;
    
    children.Add(new_child);
    return this;
  }
}

public class ForeverNode : SequentialNode
{
  public override BHS Execute()
  {
    var status = base.Execute();
    //NOTE: we need to stop child in order to make it 
    //      reset its status and re-init on the next run,
    //      also we force last_status to be BHS.RUNNING since
    //      logic in stop *needs* that even though we officially return
    //      running status *below* that call 
    this.last_status = BHS.RUNNING;
    if(status != BHS.RUNNING)
      this.Stop();

    return BHS.RUNNING;
  }
}

public class MonitorSuccessNode : SequentialNode
{
  public override BHS Execute()
  {
    var status = base.Execute();

    if(status == BHS.SUCCESS)
      return BHS.SUCCESS;

    //NOTE: we need to stop children in order to make them 
    //      reset its status and re-init on the next run
    if(status == BHS.FAILURE)
      this.Stop();

    return BHS.RUNNING;
  }
}

public class MonitorFailureNode : SequentialNode
{
  public override BHS Execute()
  {
    var status = base.Execute();
    if(status == BHS.FAILURE)
      return BHS.SUCCESS;

    //NOTE: we need to stop children in order to make them 
    //      reset its status and re-init on the next run
    if(status == BHS.SUCCESS)
      this.Stop();

    return BHS.RUNNING;
  }
}

public class DeferNode : BehaviorTreeInternalNode
{
  public BHS result = BHS.SUCCESS; 

  public override void Init()
  {}

  //NOTE: does nothing on purpose because 
  //      action happens in its defer
  public override void Deinit()
  {}

  public override void Defer()
  {
    for(int i=0;i<children.Count;++i)
      children[i].Run();

    DeferChildren(children.Count);
  }

  public override BHS Execute()
  {
    return result;
  }
}

public class InvertNode : BehaviorTreeDecoratorNode
{
  public override BHS Execute()
  {
    BHS status = base.Execute();

    if(status == BHS.FAILURE)
      return BHS.SUCCESS;
    else if(status == BHS.SUCCESS)
      return BHS.FAILURE;

    return status;
  }
}

}
