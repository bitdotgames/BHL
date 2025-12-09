using System.Collections.Generic;

namespace bhl
{

public class ParalBranchBlock : Coroutine
{
  int min_ip;
  int max_ip;
  internal VM.ExecState exec =
    new VM.ExecState(regions_capacity: 32, frames_capacity: 32, stack_capacity: 128);

  public void Init(VM.ExecState ext_exec, int min_ip, int max_ip)
  {
    this.min_ip = min_ip;
    this.max_ip = max_ip;

    exec.parent = ext_exec;
    exec.vm = ext_exec.vm;
    exec.fiber = ext_exec.fiber;
    exec.ip = min_ip;

    //TODO: this is ugly, can we reference current frame from ext_exec instead?
    //Creating a frame copy just because a region must reference a frame
    ref var frame_copy = ref exec.PushFrame();
    //let's copy ext_exec's frame data
    frame_copy = ext_exec.frames[ext_exec.frames_count - 1];
    frame_copy.region_offset_idx = 0;
    exec.PushRegion(0, min_ip: min_ip, max_ip: max_ip);
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
  public override void Destruct(VM.ExecState ext_exec)
  {
    if(exec.coroutine != null)
    {
      CoroutinePool.Del(exec, exec.coroutine);
      exec.coroutine = null;
    }

    //we exit the scope for all dangling frames which were not exited normally
    for(int i = exec.frames_count; i-- > 0;)
    {
      ref var frame = ref exec.frames[i];

      for(int r = exec.regions_count; r-- > frame.region_offset_idx;)
      {
        ref var tmp_region = ref exec.regions[r];
        if(tmp_region.defers != null && tmp_region.defers.count > 0)
          tmp_region.defers.ExitScope(exec);
      }
      exec.regions_count = frame.region_offset_idx;
      --exec.frames_count;
      //we don't own the copied frame locals
      if(i > 0)
        frame.ReleaseLocals();
    }

    //NOTE: We need to push on to the external exec all the returned vars
    //      from the copied frame if it has any
    ref var frame_copy = ref exec.frames[0];
    if(exec.ip == VM.EXIT_FRAME_IP && frame_copy.return_vars_num > 0)
    {
      for(int i = 0; i < frame_copy.return_vars_num; ++i)
      {
        ref var val = ref exec.stack.vals[i];
        ext_exec.stack.Push(val);
        val._refc = null;
        val.obj = null;
      }
    }

    exec.stack.sp = 0;
    exec.regions_count = 0;
    exec.frames_count = 0;

    //NOTE: let's leave it here for a while for a quick debug
    //for(int i = 0; i < exec.regions.Length; ++i)
    //{
    //  if(exec.regions[i].defers?.count > 0)
    //  {
    //    var defers = exec.regions[i].defers;
    //    string debug_info = "";
    //    for(int j = 0; j < defers.count; ++j)
    //    {
    //      debug_info += j + ") " + defers.blocks[j].ip + " ";

    //      try
    //      {
    //        var frame = exec.frames[exec.regions[i].frame_idx];
    //        debug_info += frame.module.name + " " + frame.module.compiled.ip2src_line.TryMap(defers.blocks[j].ip);
    //      }
    //      catch(Exception e)
    //      {}

    //      debug_info += ";";
    //    }
    //    throw new Exception("Unclean!!! " + i + " VS " + exec.regions.Length + " defers: " +
    //                        debug_info);
    //  }
    //}
  }
}

public class ParalBlock : Coroutine
{
  int min_ip;
  int max_ip;
  internal int i;
  internal List<Coroutine> branches = new List<Coroutine>();
  internal VM.DeferSupport defers = new VM.DeferSupport();

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

  public override void Destruct(VM.ExecState exec)
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

public class ParalAllBlock : Coroutine
{
  int min_ip;
  int max_ip;
  internal int i;
  internal List<Coroutine> branches = new List<Coroutine>();
  internal VM.DeferSupport defers = new VM.DeferSupport();

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

  public override void Destruct(VM.ExecState exec)
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
