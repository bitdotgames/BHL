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
          throw new Exception("Node is RUNNING during defer");

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

public class ParallelNode : ScopeNode
{
  override public void init() 
  {}

  override public BHS execute()
  {
    for(int i=0;i<children.Count;++i)
    {
      var currentTask = children[i];

      if(currentTask.currStatus == BHS.NONE || currentTask.currStatus == BHS.RUNNING)
      {
        //BHS status = currentTask.run();
        BHS status;
        ////////////////////FORCING CODE INLINE////////////////////////////////
        if(currentTask.currStatus != BHS.RUNNING)
          currentTask.init();
        status = currentTask.execute();
        currentTask.currStatus = status;
        if(status != BHS.RUNNING)
          currentTask.deinit();
        ////////////////////FORCING CODE INLINE////////////////////////////////

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
    bool sawAllSuccess = true;
    for(int i=0;i<children.Count;++i)
    {
      var currentTask = children[i];

      if(currentTask.currStatus == BHS.NONE || currentTask.currStatus == BHS.RUNNING)
      {
        //BHS status = currentTask.run();
        BHS status;
        ////////////////////FORCING CODE INLINE////////////////////////////////
        if(currentTask.currStatus != BHS.RUNNING)
          currentTask.init();
        status = currentTask.execute();
        currentTask.currStatus = status;
        if(status != BHS.RUNNING)
          currentTask.deinit();
        ////////////////////FORCING CODE INLINE////////////////////////////////

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

} //namespace bhl
