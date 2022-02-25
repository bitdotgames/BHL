//#define DEBUG_STACK
using System;
using System.Collections.Generic;

namespace bhl {

public enum BHS
{
  NONE    = 0, 
  SUCCESS = 1, 
  FAILURE = 2, 
  RUNNING = 3
}

public interface IBehaviorTreeNode
{
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

public abstract class BehaviorTreeNode : IBehaviorTreeNode
{
  //NOTE: semi-private, public for fast access
  public BHS last_status = BHS.NONE; 
  
  //NOTE: this method is heavily used for inlining, 
  //      don't change its contents if you are not sure
  public BHS Run()
  {
    if(last_status != BHS.RUNNING)
      Init();
    last_status = Execute();
    if(last_status != BHS.RUNNING)
      Deinit();
    return last_status;
  }

  public void Stop()
  {
    if(last_status == BHS.RUNNING)
      Deinit();
    if(last_status != BHS.NONE)
      Defer();
    last_status = BHS.NONE;
  }

  public abstract void Init();
  public abstract void Deinit();
  public abstract BHS Execute();
  public abstract void Defer();
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
  public List<BehaviorTreeNode> children = new List<BehaviorTreeNode>();

  public virtual BehaviorTreeInternalNode AddChild(BehaviorTreeNode new_child)
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
  override public void Deinit()
  {
    //NOTE: Trying to deinit all of its children may be suboptimal, so
    //      it makes sense to override it for specific cases
    DeinitAndDeferChildren(0, children.Count);
  }

  //NOTE: normally you never really want to override this one
  override public void Defer()
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
    //BHS status = children[0].run();
    BHS status;
    var current = children[0];
    ////////////////////FORCING CODE INLINE////////////////////////////////
    if(current.last_status != BHS.RUNNING)
      current.Init();
    status = current.Execute();
    current.last_status = status;
    if(status != BHS.RUNNING)
      current.Deinit();
    ////////////////////FORCING CODE INLINE////////////////////////////////
    return status;
  }

  override public void Deinit()
  {
    DeinitChildren(0, 1);
  } 

  override public void Defer()
  {
    DeferChildren(children.Count);
  }

  public void setSlave(BehaviorTreeNode node)
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
  override public BHS Execute()
  {
    return BHS.RUNNING;
  }
  override public void Init(){}
  override public void Deinit(){}
  override public void Defer(){}
}

//////////////////////////////////////////

public class yield : BehaviorTreeNode
{
  override public BHS Execute()
  {
    return last_status == BHS.NONE ? BHS.RUNNING : BHS.SUCCESS;
  }
  override public void Init(){}
  override public void Deinit(){}
  override public void Defer(){}
}

//////////////////////////////////////////
               
public class nop : BehaviorTreeNode
{
  override public BHS Execute()
  {
    return BHS.SUCCESS;
  }
  override public void Init(){}
  override public void Deinit(){}
  override public void Defer(){}
}

//////////////////////////////////////////

public class fail : BehaviorTreeNode
{
  override public BHS Execute()
  {
    return BHS.FAILURE;
  }
  override public void Init(){}
  override public void Deinit(){}
  override public void Defer(){}
}

/////////////////////////////////////////////////////////////

public class SequentialNode : ScopeNode
{
  protected int curr_pos = 0;

  override public void Init()
  {
    curr_pos = 0;
  }

  override public void Deinit()
  {
    DeinitAndDeferChildren(curr_pos, 1);
  }

  override public BHS Execute()
  {
    BHS status = BHS.SUCCESS;
    while(curr_pos < children.Count)
    {
      var current = children[curr_pos];
      //status = currentTask.run();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(current.last_status != BHS.RUNNING)
        current.Init();
      status = current.Execute();
      current.last_status = status;
      if(status != BHS.RUNNING)
        current.Deinit();
      ////////////////////FORCING CODE INLINE////////////////////////////////
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
  override public BHS Execute()
  {
    //var status = base.execute();
    ////////////////////FORCING CODE INLINE////////////////////////////////
    BHS status = BHS.SUCCESS;
    while(curr_pos < children.Count)
    {
      var current = children[curr_pos];
      //status = currentTask.run();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(current.last_status != BHS.RUNNING)
        current.Init();
      status = current.Execute();
      current.last_status = status;
      if(status != BHS.RUNNING)
        current.Deinit();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(status == BHS.SUCCESS)
        ++curr_pos;
      else
        break;
    } 
    ////////////////////FORCING CODE INLINE////////////////////////////////
    if(status == BHS.FAILURE)
      status = BHS.SUCCESS;
    return status;
  }
}

public class ParallelNode : ScopeNode
{
  override public void Init() 
  {}

  override public BHS Execute()
  {
    for(int i=0;i<children.Count;++i)
    {
      var current = children[i];

      if(current.last_status == BHS.NONE || current.last_status == BHS.RUNNING)
      {
        //BHS status = currentTask.run();
        BHS status;
        ////////////////////FORCING CODE INLINE////////////////////////////////
        if(current.last_status != BHS.RUNNING)
          current.Init();
        status = current.Execute();
        current.last_status = status;
        if(status != BHS.RUNNING)
          current.Deinit();
        ////////////////////FORCING CODE INLINE////////////////////////////////

        if(status != BHS.RUNNING)
          return status;
      }
    }

    return BHS.RUNNING;
  }

  override public BehaviorTreeInternalNode AddChild(BehaviorTreeNode new_child)
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
  override public void Init() 
  {}

  override public BHS Execute()
  {
    bool all_success = true;
    for(int i=0;i<children.Count;++i)
    {
      var current = children[i];

      if(current.last_status == BHS.NONE || current.last_status == BHS.RUNNING)
      {
        //BHS status = currentTask.run();
        BHS status;
        ////////////////////FORCING CODE INLINE////////////////////////////////
        if(current.last_status != BHS.RUNNING)
          current.Init();
        status = current.Execute();
        current.last_status = status;
        if(status != BHS.RUNNING)
          current.Deinit();
        ////////////////////FORCING CODE INLINE////////////////////////////////

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

  override public void Init()
  {
    curr_pos = -1;
  }

  override public BHS Execute()
  {
    BHS status;

    //there's one still running
    if(curr_pos != -1)
    {
      var current = children[curr_pos];
      //status = currentTask.run();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(current.last_status != BHS.RUNNING)
        current.Init();
      status = current.Execute();
      current.last_status = status;
      if(status != BHS.RUNNING)
        current.Deinit();
      ////////////////////FORCING CODE INLINE////////////////////////////////

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
      BehaviorTreeNode current = children[curr_pos];
       //keep trying children until one doesn't fail
      while(true)
      {
        //status = currentTask.run();
        ////////////////////FORCING CODE INLINE////////////////////////////////
        if(current.last_status != BHS.RUNNING)
          current.Init();
        status = current.Execute();
        current.last_status = status;
        if(status != BHS.RUNNING)
          current.Deinit();
        ////////////////////FORCING CODE INLINE////////////////////////////////
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

  override public BehaviorTreeInternalNode AddChild(BehaviorTreeNode new_child)
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
  override public BHS Execute()
  {
    //var status = base.execute();
    ////////////////////FORCING CODE INLINE////////////////////////////////
    BHS status = BHS.SUCCESS;

    while(curr_pos < children.Count)
    {
      var current = children[curr_pos];
      //status = currentTask.run();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(current.last_status != BHS.RUNNING)
        current.Init();
      status = current.Execute();
      current.last_status = status;
      if(status != BHS.RUNNING)
        current.Deinit();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(status == BHS.SUCCESS)
        ++curr_pos;
      else
        break;
    } 

    if(status != BHS.RUNNING)
      curr_pos = 0;
    ////////////////////FORCING CODE INLINE////////////////////////////////
    //NOTE: we need to stop child in order to make it 
    //      reset its status and re-init on the next run,
    //      also we force currStatus to be BHS.RUNNING since
    //      logic in stop *needs* that even though we officially return
    //      running status *below* that call 
    base.last_status = BHS.RUNNING;
    if(status != BHS.RUNNING)
      Stop();

    return BHS.RUNNING;
  }
}

public class MonitorSuccessNode : SequentialNode
{
  override public BHS Execute()
  {
    //var status = base.execute();
    ////////////////////FORCING CODE INLINE////////////////////////////////
    BHS status = BHS.SUCCESS;
    while(curr_pos < children.Count)
    {
      var current = children[curr_pos];
      //status = currentTask.run();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(current.last_status != BHS.RUNNING)
        current.Init();
      status = current.Execute();
      current.last_status = status;
      if(status != BHS.RUNNING)
        current.Deinit();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(status == BHS.SUCCESS)
        ++curr_pos;
      else
        break;
    } 
    if(status != BHS.RUNNING)
      curr_pos = 0;
    ////////////////////FORCING CODE INLINE////////////////////////////////

    if(status == BHS.SUCCESS)
      return BHS.SUCCESS;

    //NOTE: we need to stop children in order to make them 
    //      reset its status and re-init on the next run
    if(status == BHS.FAILURE)
      base.Stop();

    return BHS.RUNNING;
  }
}

public class MonitorFailureNode : SequentialNode
{
  override public BHS Execute()
  {
    //var status = base.execute();
    ////////////////////FORCING CODE INLINE////////////////////////////////
    BHS status = BHS.SUCCESS;
    while(curr_pos < children.Count)
    {
      var current = children[curr_pos];
      //status = currentTask.run();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(current.last_status != BHS.RUNNING)
        current.Init();
      status = current.Execute();
      current.last_status = status;
      if(status != BHS.RUNNING)
        current.Deinit();
      ////////////////////FORCING CODE INLINE////////////////////////////////
      if(status == BHS.SUCCESS)
        ++curr_pos;
      else
        break;
    } 
    if(status != BHS.RUNNING)
      curr_pos = 0;
    ////////////////////FORCING CODE INLINE////////////////////////////////
    if(status == BHS.FAILURE)
      return BHS.SUCCESS;

    //NOTE: we need to stop children in order to make them 
    //      reset its status and re-init on the next run
    if(status == BHS.SUCCESS)
      base.Stop();

    return BHS.RUNNING;
  }
}

public class DeferNode : BehaviorTreeInternalNode
{
  public BHS result = BHS.SUCCESS; 

  override public void Init()
  {}

  //NOTE: does nothing on purpose because 
  //      action happens in its defer
  override public void Deinit()
  {}

  override public void Defer()
  {
    for(int i=0;i<children.Count;++i)
      children[i].Run();

    DeferChildren(children.Count);
  }

  override public BHS Execute()
  {
    return result;
  }
}

public class InvertNode : BehaviorTreeDecoratorNode
{
  override public BHS Execute()
  {
    BHS status = base.Execute();

    if(status == BHS.FAILURE)
      return BHS.SUCCESS;
    else if(status == BHS.SUCCESS)
      return BHS.FAILURE;

    return status;
  }
}

} //namespace bhl
