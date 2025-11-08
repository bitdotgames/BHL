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
    exec.regions[exec.regions_count++]
      = new VM.Region(exec.frames_count - 1, null, min_ip: ip, max_ip: max_ip);
    //3. and execute it
    exec.vm.Execute(
      exec,
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

//TODO: do we actually need this one?
public class SeqBlock : Coroutine, IInspectableCoroutine
{
  public VM.ExecState exec = new VM.ExecState();
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
    exec.vm = ext_exec.vm;
    exec.fiber = ext_exec.fiber;
    exec.stack = ext_exec.stack;
    exec.ip = min_ip;
    exec.regions[exec.regions_count++] =
      new VM.Region(-1, defers, min_ip: min_ip, max_ip: max_ip);
  }

  public override void Tick(VM.ExecState ext_exec)
  {
    ext_exec.vm.Execute(exec);
    ext_exec.ip = exec.ip;
  }

  public override void Cleanup(VM.ExecState _)
  {
    ExitScope(exec, defers);
  }

  public static void ExitScope(VM.ExecState exec, VM.DeferSupport defers)
  {
    if(exec.coroutine != null)
    {
      CoroutinePool.Del(exec, exec.coroutine);
      exec.coroutine = null;
    }

    //we exit the scope for all dangling frames
    for(int i = exec.frames_count; i-- > 0;)
    {
      ref var frame = ref exec.frames[i];
      if(frame.defers.count > 0)
        frame.defers.ExitScope(exec);
    }
    exec.frames_count = 0;
    exec.regions_count = 0;

    if(defers.count > 0)
      defers.ExitScope(exec);
  }
}

public class ParalBranchBlock : Coroutine, IInspectableCoroutine
{
  public int min_ip;
  public int max_ip;
  public VM.ExecState exec = new VM.ExecState();
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

    int new_frame_idx = exec.frames_count;
    ref var new_frame = ref exec.PushFrame();
    //let's copy ext_exec's frame data
    new_frame = ext_exec.frames[ext_exec.frames_count - 1];

    exec.regions[exec.regions_count++] =
      new VM.Region(new_frame_idx, defers, min_ip: min_ip, max_ip: max_ip);
  }

  public override void Tick(VM.ExecState ext_exec)
  {
    var local_stack = exec.stack;
    int ext_sp_backup = ext_exec.stack.sp;
    //TODO: the code below is not efficient
    //let's push our values onto the external stack
    exec.stack = ext_exec.stack;
    for(int i = 0; i < local_stack.sp; i++)
      exec.stack.Push(local_stack.vals[i]);

    ext_exec.vm.Execute(exec);
    ext_exec.status = exec.status;

    //let's restore external stack's sp
    ext_exec.stack.sp = ext_sp_backup;
    //let's restore our own stack
    exec.stack = local_stack;

    if(exec.status == BHS.SUCCESS)
    {
      //TODO: why doing this if there's a similar code in parent paral block
      //if the execution didn't "jump out" of the block (e.g. break) proceed to the ip after block
      if(exec.ip > min_ip && exec.ip < max_ip)
        ext_exec.ip = max_ip + 1;
      //otherwise just assign ext_ip the last ip result (this is needed for break, continue)
      else
        ext_exec.ip = exec.ip;
    }
  }

  public override void Cleanup(VM.ExecState _)
  {
    SeqBlock.ExitScope(exec, defers);

    //NOTE: let's clean the local stack
    while(exec.stack.sp > 0)
    {
      exec.stack.Pop(out var val);
      val._refc?.Release();
    }
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
      throw new NotImplementedException();
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
