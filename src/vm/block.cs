using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace bhl
{

public enum BlockType
{
  FUNC      = 0,
  SEQ       = 1,
  DEFER     = 2,
  PARAL     = 3,
  PARAL_ALL = 4,
  IF        = 7,
  WHILE     = 8,
  FOR       = 9,
  DOWHILE   = 10,
}

public struct DeferBlock
{
  public int ip;
  public int max_ip;

  internal void Execute(VM.ExecState exec)
  {
    //1. let's remeber the original ip in order to restore it once
    //   the execution of this block is done (defer block can be
    //   located anywhere in the code)
    int ip_orig = exec.ip;
    exec.ip = this.ip;

    //2. let's create the execution region
    exec.PushRegion(exec.frames_count - 1, min_ip: ip, max_ip: max_ip);
    //3. and execute it
    exec.Execute(
      //NOTE: we re-use the existing exec.stack but limit the execution
      //      only up to the defer code block
      exec.regions_count - 1
    );
    if(exec.status != BHS.SUCCESS)
      throw new Exception("Defer execution invalid status: " + exec.status);

    exec.ip = ip_orig;
  }

  public override string ToString()
  {
    return "Defer block: " + ip + " " + max_ip;
  }
}

public class ParalBranchBlock : Coroutine, IInspectableCoroutine
{
  public int min_ip;
  public int max_ip;
  public VM.ExecState exec =
    new VM.ExecState(regions_capacity: 32, frames_capacity: 32, stack_capacity: 128);
  public VM.DeferSupport defers = new VM.DeferSupport();

  public int Count
  {
    get { return 0; }
  }

  public ICoroutine At(int i)
  {
    return exec.coroutine;
  }

  public void Init(VM.ExecState ext_exec, int min_ip, int max_ip)
  {
    this.min_ip = min_ip;
    this.max_ip = max_ip;

    exec.vm = ext_exec.vm;
    exec.fiber = ext_exec.fiber;
    exec.ip = min_ip;

    //TODO: this is ugly, can we reference current frame from ext_exec instead?
    //creating a frame copy just because a region must reference a frame
    ref var frame_copy = ref exec.PushFrame();
    //let's copy ext_exec's frame data
    frame_copy = ext_exec.frames[ext_exec.frames_count - 1];

    ref var region = ref exec.PushRegion(0, min_ip: min_ip, max_ip: max_ip);
    region.defers = defers;
  }

  public override void Tick(VM.ExecState ext_exec)
  {
    exec.Execute();

    ext_exec.status = exec.status;

    if(exec.status == BHS.SUCCESS)
    {
      //TODO: why doing this if there's a similar code in parent paral block?
      //if the execution didn't "jump out" of the block (e.g. break) proceed to the ip after the block
      if(exec.ip > min_ip && exec.ip < max_ip)
        ext_exec.ip = max_ip + 1;
      //otherwise just assign ext_ip the last ip result (this is needed for break, continue)
      else
        ext_exec.ip = exec.ip;
    }
  }

  //TODO: this Cleanup is executed by Paral/ParalAll
  public override void Cleanup(VM.ExecState ext_exec)
  {
    if(exec.coroutine != null)
    {
      CoroutinePool.Del(exec, exec.coroutine);
      exec.coroutine = null;
    }

    //we exit the scope for all dangling frames which were not exited normally
    for(int i = exec.frames_count; i-- > 1/*let's ignore the copied frame*/;)
    {
      ref var frame = ref exec.frames[i];

      for(int r = exec.regions_count; r-- > frame.regions_mark;)
      {
        ref var tmp_region = ref exec.regions[i];
        if(tmp_region.defers != null && tmp_region.defers.count > 0)
          tmp_region.defers.ExitScope(exec);
      }
      frame.CleanLocals(exec.stack);
    }

    if(defers.count > 0)
      defers.ExitScope(exec);

    //Let's detect if there was a return and there was no other frames
    //except the 'copied one'. In this case we need to copy dangling
    //expected amount of returned vars designated by copied frame.
    //Otherwise, normal procedure of return happens within ExecState.Execute(..)
    if(exec.frames_count == 1 && exec.ip == VM.EXIT_FRAME_IP)
    {
      ref var frame_copy = ref exec.frames[exec.frames_count - 1];
      for(int i = 0; i < frame_copy.return_vars_num; ++i)
      {
        ref var val = ref exec.stack.vals[i];
        ext_exec.stack.Push(val);
        val._refc = null;
        val._obj = null;
      }
    }

    exec.stack.sp = 0;
    exec.regions_count = 0;
    exec.frames_count = 0;
  }
}

public class ParalBlock : Coroutine, IInspectableCoroutine
{
  public int min_ip;
  public int max_ip;
  public int i;
  public List<Coroutine> branches = new List<Coroutine>();
  public VM.DeferSupport defers = new VM.DeferSupport();

  public int Count
  {
    get { return branches.Count; }
  }

  public ICoroutine At(int i)
  {
    return branches[i];
  }

  public void Init(int min_ip, int max_ip)
  {
    this.min_ip = min_ip;
    this.max_ip = max_ip;
    i = 0;
    branches.Clear();
    defers.count = 0;
  }

  public override void Tick(VM.ExecState exec)
  {
    exec.ip = min_ip;

    exec.status = BHS.RUNNING;

    for(i = 0; i < branches.Count; ++i)
    {
      var branch = branches[i];
      branch.Tick(exec);
      if(exec.status != BHS.RUNNING)
      {
        CoroutinePool.Del(exec, branch);
        branches.RemoveAt(i);
        //if the execution didn't "jump out" of the block (e.g. break) proceed to the ip after the block
        if(exec.ip > min_ip && exec.ip < max_ip)
          exec.ip = max_ip + 1;
        break;
      }
    }
  }

  public override void Cleanup(VM.ExecState exec)
  {
    //NOTE: let's preserve the current branch index during cleanup routine,
    //      this is useful for stack trace retrieval
    for(i = 0; i < branches.Count; ++i)
      CoroutinePool.Del(exec, branches[i]);
    branches.Clear();

    if(defers.count > 0)
      defers.ExitScope(exec);
  }
}

public class ParalAllBlock : Coroutine, IInspectableCoroutine
{
  public int min_ip;
  public int max_ip;
  public int i;
  public List<Coroutine> branches = new List<Coroutine>();
  public VM.DeferSupport defers = new VM.DeferSupport();

  public int Count
  {
    get { return branches.Count; }
  }

  public ICoroutine At(int i)
  {
    return branches[i];
  }

  public void Init(int min_ip, int max_ip)
  {
    this.min_ip = min_ip;
    this.max_ip = max_ip;
    i = 0;
    branches.Clear();
    defers.count = 0;
  }

  public override void Tick(VM.ExecState exec)
  {
    exec.ip = min_ip;

    for(i = 0; i < branches.Count;)
    {
      var branch = branches[i];
      branch.Tick(exec);
      //let's check if we "jumped out" of the block (e.g return, break)
      //NOTE: commented is a leftover from previous implementation, is it obsolete?
      if(/*frm.refs == -1  ||*/ exec.ip < (min_ip - 1) || exec.ip > (max_ip + 1))
      {
        CoroutinePool.Del(exec, branch);
        branches.RemoveAt(i);
        exec.status = BHS.SUCCESS;
        return;
      }

      if(exec.status == BHS.SUCCESS)
      {
        CoroutinePool.Del(exec, branch);
        branches.RemoveAt(i);
      }
      else if(exec.status == BHS.FAILURE)
      {
        CoroutinePool.Del(exec, branch);
        branches.RemoveAt(i);
        return;
      }
      else
        ++i;
    }

    if(branches.Count > 0)
      exec.status = BHS.RUNNING;
    //if the execution didn't "jump out" of the block (e.g. break) proceed to the ip after this block
    else if(exec.ip > min_ip && exec.ip < max_ip)
      exec.ip = max_ip + 1;
  }

  public override void Cleanup(VM.ExecState exec)
  {
    //NOTE: let's preserve the current branch index during cleanup routine,
    //      this is useful for stack trace retrieval
    for(i = 0; i < branches.Count; ++i)
      CoroutinePool.Del(exec, branches[i]);
    branches.Clear();

    if(defers.count > 0)
      defers.ExitScope(exec);
  }
}

}
