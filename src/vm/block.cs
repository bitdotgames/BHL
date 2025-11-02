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
  public VM.FrameOld frm;
  public int ip;
  public int max_ip;

  public DeferBlock(VM.FrameOld frm, int ip, int max_ip)
  {
    this.frm = frm;
    this.ip = ip;
    this.max_ip = max_ip;
  }

  void Execute(VM.ExecState exec)
  {
    //1. let's remeber the original ip in order to restore it once
    //   the execution of this block is done (defer block can be
    //   located anywhere in the code)
    int ip_orig = exec.ip;
    exec.ip = this.ip;

    //2. let's create the execution region
    exec.regions[exec.regions_count++]
      = new VM.Region(-1, null, min_ip: ip, max_ip: max_ip);
    //3. and execute it
    frm.vm.Execute(
      exec,
      //NOTE: we re-use the existing exec.stack but limit the execution
      //      only up to the defer code block
      exec.regions_count - 1
    );
    if(exec.status != BHS.SUCCESS)
      throw new Exception("Defer execution invalid status: " + exec.status);

    exec.ip = ip_orig;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal static void ExitScope(VM.ExecState exec, List<DeferBlock> defers)
  {
    if(defers.Count == 0)
      return;

    var coro_orig = exec.coroutine;
    for(int i = defers.Count; i-- > 0;)
    {
      var d = defers[i];
      exec.coroutine = null;
      //TODO: do we need ensure that status is SUCCESS?
      /*var status = */
      d.Execute(exec);
    }

    exec.coroutine = coro_orig;
    defers.Clear();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal static void ExitScope(VM.ExecState exec, DeferBlock[] defers, int defers_count)
  {
    var coro_orig = exec.coroutine;
    for(int i = defers_count; i-- > 0;)
    {
      exec.coroutine = null;
      defers[i].Execute(exec);
    }
    exec.coroutine = coro_orig;
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
  public List<DeferBlock> defers = new List<DeferBlock>(2);

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

  public void Cleanup(VM.ExecState _)
  {
    ExitScope(exec, defers);
  }

  public static void ExitScope(VM.ExecState exec, List<DeferBlock> defers)
  {
    if(exec.coroutine != null)
    {
      CoroutinePool.Del(exec, exec.coroutine);
      exec.coroutine = null;
    }

    //NOTE: 1. first we exit the scope for all dangling frames
    for(int i = exec.frames_old.Count; i-- > 0;)
      DeferBlock.ExitScope(exec, exec.frames_old[i].defers);

    //NOTE: 2. then we release frames only after exiting them
    for(int i = exec.frames_old.Count; i-- > 0;)
      exec.frames_old[i].Release();
    exec.frames_old.Clear();
    exec.regions_count = 0;

    DeferBlock.ExitScope(exec, defers);
  }
}

public class ParalBranchBlock : Coroutine, IInspectableCoroutine
{
  public int min_ip;
  public int max_ip;
  public ValOldStack stack = new ValOldStack(VM.FrameOld.MAX_STACK);
  public VM.ExecState exec = new VM.ExecState();
  public List<DeferBlock> defers = new List<DeferBlock>(1);

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
    exec.stack_old = stack;
    exec.regions[exec.regions_count++] =
      new VM.Region(-1, defers, min_ip: min_ip, max_ip: max_ip);
  }

  public override void Tick(VM.ExecState ext_exec)
  {
    exec.vm.Execute(exec);

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
    for(int i = stack.Count; i-- > 0;)
    {
      var val = stack[i];
      val._refc?.Release();
      val._refc = null;
    }

    stack.Clear();
  }
}

public class ParalBlock : Coroutine, IInspectableCoroutine
{
  public int min_ip;
  public int max_ip;
  public int i;
  public List<Coroutine> branches = new List<Coroutine>();
  public List<DeferBlock> defers = new List<DeferBlock>(1);

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
    defers.Clear();
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

  public void Cleanup(VM.ExecState exec)
  {
    //NOTE: let's preserve the current branch index during cleanup routine,
    //      this is useful for stack trace retrieval
    for(i = 0; i < branches.Count; ++i)
      CoroutinePool.Del(exec, branches[i]);
    branches.Clear();
    DeferBlock.ExitScope(exec, defers);
  }
}

public class ParalAllBlock : Coroutine, IInspectableCoroutine
{
  public int min_ip;
  public int max_ip;
  public int i;
  public List<Coroutine> branches = new List<Coroutine>();
  public List<DeferBlock> defers = new List<DeferBlock>(1);

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
    defers.Clear();
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
    DeferBlock.ExitScope(exec, defers);
  }
}

}
