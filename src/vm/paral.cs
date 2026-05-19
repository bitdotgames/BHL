using System.Collections.Generic;

namespace bhl
{

public class ParalBranchBlock : Coroutine
{
  int min_ip;
  int max_ip;

  //NOTE: each branch has its own ExecState — its own evaluation stack, ip, and
  //      regions/frames arrays — so branches tick independently without clobbering
  //      each other's expression evaluation.
  internal VM.ExecState exec = new VM.ExecState();

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
    //NOTE: shallow struct copy of the parent frame — value fields (ip, offsets, etc.)
    //      become independent, but reference fields (locals, bytecode, module) are
    //      intentionally shared: a paral branch runs in the same function scope, so
    //      all branches read and write the same local variables. This shared-mutable
    //      access is a language feature, not a bug.
    frame_copy = ext_exec.frames[ext_exec.frames_count - 1];
    //NOTE: must be reset — the branch tracks its own region stack from scratch,
    //      independently of the parent's regions.
    frame_copy.region_offset_idx = 0;
    //NOTE: bound the branch's execution to its bytecode slice.
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
      //NOTE: frames[0] is the borrowed copy of the parent's frame — its locals
      //      belong to the parent and must not be released here.
      //      frames[1+] are genuine sub-frames pushed by function calls inside
      //      the branch; those are owned by the branch and must be released.
      if(i > 0)
        frame.ReleaseLocals();
    }

    //NOTE: if the branch exited via a return (EXIT_FRAME_IP), transfer any return
    //      values from the branch's eval stack onto the parent's eval stack.
    //      return_vars_num is available because it was captured from the parent
    //      frame via the struct copy in Init.
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
