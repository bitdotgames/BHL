using System;
using System.Collections.Generic;

namespace bhl {

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

public interface IDeferSupport
{
  void RegisterDefer(DeferBlock cb);
}

public struct DeferBlock
{
  public VM.Frame frm;
  public int ip;
  public int max_ip;

  public DeferBlock(VM.Frame frm, int ip, int max_ip)
  {
    this.frm = frm;
    this.ip = ip;
    this.max_ip = max_ip;
  }

  BHS Execute(VM.ExecState exec)
  {
    //1. let's remeber the original ip in order to restore it once
    //   the execution of this block is done (defer block can be 
    //   located anywhere in the code)
    int ip_orig = exec.ip;
    exec.ip = this.ip;

    //2. let's create the execution region
    exec.regions.Push(new VM.Region(frm, null, min_ip: ip, max_ip: max_ip));
    //3. and execute it
    var status = frm.vm.Execute(
      exec, 
      //NOTE: we re-use the existing exec.stack but limit the execution 
      //      only up to the defer code block
      exec.regions.Count-1 
    );
    if(status != BHS.SUCCESS)
      throw new Exception("Defer execution invalid status: " + status);

    exec.ip = ip_orig;

    return status;
  }

  static internal void ExitScope(List<DeferBlock> defers, VM.ExecState exec)
  {
    if(defers.Count == 0)
      return;

    var coro_orig = exec.coroutine;
    for(int i=defers.Count;i-- > 0;)
    {
      var d = defers[i];
      exec.coroutine = null;
      //TODO: do we need ensure that status is SUCCESS?
      /*var status = */d.Execute(exec);
    }
    exec.coroutine = coro_orig;
    defers.Clear();
  }

  public override string ToString()
  {
    return "Defer block: " + ip + " " + max_ip;
  }
}

public class SeqBlock : Coroutine, IDeferSupport, IInspectableCoroutine
{
  public VM.ExecState exec = new VM.ExecState();
  public List<DeferBlock> defers = new List<DeferBlock>(2);

  public int Count {
    get {
      return 0;
    }
  }

  public ICoroutine At(int i) 
  {
    return exec.coroutine;
  }

  public void Init(VM.Frame frm, ValStack stack, int min_ip, int max_ip)
  {
    exec.stack = stack;
    exec.ip = min_ip;
    exec.regions.Push(new VM.Region(frm, defers, min_ip: min_ip, max_ip: max_ip));
  }

  public override void Tick(VM.Frame frm, VM.ExecState ext_exec, ref BHS status)
  {
    status = frm.vm.Execute(exec);
    ext_exec.ip = exec.ip;
  }

  public override void Cleanup(VM.Frame frm, VM.ExecState _)
  {
    ExitScope(frm, exec, defers);
  }

  public static void ExitScope(VM.Frame frm, VM.ExecState exec, List<DeferBlock> defers)
  {
    if(exec.coroutine != null)
    {
      CoroutinePool.Del(frm, exec, exec.coroutine);
      exec.coroutine = null;
    }

    //NOTE: 1. first we exit the scope for all dangling frames
    for(int i=exec.frames.Count;i-- > 0;)
      exec.frames[i].ExitScope(null, exec);

    //NOTE: 2. then we release frames only after exiting them
    for(int i=exec.frames.Count;i-- > 0;)
      exec.frames[i].Release();
    exec.frames.Clear();
    exec.regions.Clear();

    DeferBlock.ExitScope(defers, exec);
  }

  public void RegisterDefer(DeferBlock dfb)
  {
    defers.Add(dfb);
  }
}

public class ParalBranchBlock : Coroutine, IDeferSupport, IInspectableCoroutine
{
  public int min_ip;
  public int max_ip;
  public ValStack stack = new ValStack(VM.Frame.MAX_STACK);
  public VM.ExecState exec = new VM.ExecState();
  public List<DeferBlock> defers = new List<DeferBlock>(1);

  public int Count {
    get {
      return 0;
    }
  }

  public ICoroutine At(int i) 
  {
    return exec.coroutine;
  }

  public void Init(VM.Frame frm, int min_ip, int max_ip)
  {
    this.min_ip = min_ip;
    this.max_ip = max_ip;
    exec.ip = min_ip;
    exec.stack = stack;
    exec.regions.Push(new VM.Region(frm, defers, min_ip: min_ip, max_ip: max_ip));
  }

  public override void Tick(VM.Frame frm, VM.ExecState ext_exec, ref BHS status)
  {
    status = frm.vm.Execute(exec);

    if(status == BHS.SUCCESS)
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

  public override void Cleanup(VM.Frame frm, VM.ExecState _)
  {
    SeqBlock.ExitScope(frm, exec, defers);

    //NOTE: let's clean the local stack
    for(int i=stack.Count;i-- > 0;)
    {
      var val = stack[i];
      val.Release();
    }
    stack.Clear();
  }

  public void RegisterDefer(DeferBlock dfb)
  {
    defers.Add(dfb);
  }
}

public class ParalBlock : Coroutine, IBranchyCoroutine, IDeferSupport, IInspectableCoroutine
{
  public int min_ip;
  public int max_ip;
  public int i;
  public List<Coroutine> branches = new List<Coroutine>();
  public List<DeferBlock> defers = new List<DeferBlock>(1);

  public int Count {
    get {
      return branches.Count;
    }
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

  public override void Tick(VM.Frame frm, VM.ExecState exec, ref BHS status)
  {
    exec.ip = min_ip;

    status = BHS.RUNNING;

    for(i=0;i<branches.Count;++i)
    {
      var branch = branches[i];
      branch.Tick(frm, exec, ref status);
      if(status != BHS.RUNNING)
      {
        CoroutinePool.Del(frm, exec, branch);
        branches.RemoveAt(i);
        //if the execution didn't "jump out" of the block (e.g. break) proceed to the ip after the block
        if(exec.ip > min_ip && exec.ip < max_ip)
          exec.ip = max_ip + 1;
        break;
      }
    }
  }

  public override void Cleanup(VM.Frame frm, VM.ExecState exec)
  {
    //NOTE: let's preserve the current branch index during cleanup routine,
    //      this is useful for stack trace retrieval
    for(i=0;i<branches.Count;++i)
      CoroutinePool.Del(frm, exec, branches[i]);
    branches.Clear();
    DeferBlock.ExitScope(defers, exec);
  }

  public void Attach(ICoroutine coro)
  {
    branches.Add((Coroutine)coro);
  }

  public void RegisterDefer(DeferBlock dfb)
  {
    defers.Add(dfb);
  }
}

public class ParalAllBlock : Coroutine, IBranchyCoroutine, IDeferSupport, IInspectableCoroutine
{
  public int min_ip;
  public int max_ip;
  public int i;
  public List<Coroutine> branches = new List<Coroutine>();
  public List<DeferBlock> defers = new List<DeferBlock>(1);

  public int Count {
    get {
      return branches.Count;
    }
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

  public override void Tick(VM.Frame frm, VM.ExecState exec, ref BHS status)
  {
    exec.ip = min_ip;
    
    for(i=0;i<branches.Count;)
    {
      var branch = branches[i];
      branch.Tick(frm, exec, ref status);
      //let's check if we "jumped out" of the block (e.g return, break)
      if(frm.refs == -1 /*return executed*/ || exec.ip < (min_ip-1) || exec.ip > (max_ip+1))
      {
        CoroutinePool.Del(frm, exec, branch);
        branches.RemoveAt(i);
        status = BHS.SUCCESS;
        return;
      }
      if(status == BHS.SUCCESS)
      {
        CoroutinePool.Del(frm, exec, branch);
        branches.RemoveAt(i);
      }
      else if(status == BHS.FAILURE)
      {
        CoroutinePool.Del(frm, exec, branch);
        branches.RemoveAt(i);
        return;
      }
      else
        ++i;
    }

    if(branches.Count > 0)
      status = BHS.RUNNING;
    //if the execution didn't "jump out" of the block (e.g. break) proceed to the ip after this block
    else if(exec.ip > min_ip && exec.ip < max_ip)
      exec.ip = max_ip + 1;
  }

  public override void Cleanup(VM.Frame frm, VM.ExecState exec)
  {
    //NOTE: let's preserve the current branch index during cleanup routine,
    //      this is useful for stack trace retrieval
    for(i=0;i<branches.Count;++i)
      CoroutinePool.Del(frm, exec, branches[i]);
    branches.Clear();
    DeferBlock.ExitScope(defers, exec);
  }

  public void Attach(ICoroutine coro)
  {
    branches.Add((Coroutine)coro);
  }

  public void RegisterDefer(DeferBlock dfb)
  {
    defers.Add(dfb);
  }
}

}
