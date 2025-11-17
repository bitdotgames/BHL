using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace bhl
{

public partial class VM : INamedResolver
{
  public struct FiberRef
  {
    int id;
    Fiber fiber;

    public bool IsRunning  => !(Get()?.IsStopped() ?? true);

    public FiberRef(Fiber fiber)
    {
      this.id = (fiber?.id ?? 0);
      this.fiber = fiber;
    }

    public FiberRef(Val val)
    {
      this.id = (int)val.num;
      this.fiber = (VM.Fiber)val.obj;
    }

    public static Val AsVal(VM.Fiber fb)
    {
      var val = Val.NewObj(fb, Types.FiberRef);
      //let's encode FiberRef into Val
      val.num = fb.id;
      return val;
    }

    public Fiber Get()
    {
      return (fiber?.id ?? 0) == id ? fiber : null;
    }

    public void Set(Fiber fiber)
    {
      this.id = (fiber?.id ?? 0);
      this.fiber = fiber;
    }

    public void Clear()
    {
      this.fiber = null;
    }
  }

  public class Fiber : ITask
  {
    public readonly VM vm;

    internal FuncAddr func_addr;
    public FuncAddr FuncAddr => func_addr;

    internal FiberRef parent;
    public FiberRef Parent => parent;

    internal readonly List<FiberRef> children = new List<FiberRef>();
    public IReadOnlyList<FiberRef> Children => children;

    //NOTE: -1 means it's in released state,
    //      public only for inspection
    public int refs;

    internal int id;
    public int Id => id;

    internal bool stop_guard;

    public readonly ExecState exec = new ExecState();

    public ValStack Stack => exec.stack;

    public BHS status => exec.status;

    static public Fiber New(VM vm)
    {
      Fiber fb;
      if(vm.fibers_pool.stack.Count == 0)
      {
        ++vm.fibers_pool.miss;
        fb = new Fiber(vm);
      }
      else
      {
        ++vm.fibers_pool.hits;
        fb = vm.fibers_pool.stack.Pop();

        if(fb.refs != -1)
          throw new Exception("Expected to be released, refs " + fb.refs);
      }

      fb.refs = 1;
      fb.stop_guard = false;
      fb.parent.Clear();
      fb.children.Clear();

      return fb;
    }

    static void Del(Fiber fb)
    {
      if(fb.refs != 0)
        throw new Exception("Freeing invalid object, refs " + fb.refs);

      fb.refs = -1;

      fb.exec.ExitFrames();
      fb.vm.fibers_pool.stack.Push(fb);
    }

    //NOTE: use New() instead
    Fiber(VM vm)
    {
      this.vm = vm;
      exec.vm = vm;
      exec.fiber = this;
    }

    void _AfterStop()
    {
      if(IsStopped())
        return;
      stop_guard = true;

      exec.ExitFrames();

      //NOTE: we assign Fiber ip to a special value which is just one value after STOP_IP
      //      this way Fiber breaks its current Frame execution loop.
      exec.ip = STOP_IP + 1;

      if(status == BHS.FAILURE)
        exec.stack.ClearAndRelease();
    }

    internal void AddChild(Fiber fb)
    {
      fb.parent.Set(this);
      children.Add(new FiberRef(fb));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Retain()
    {
      if(refs == -1)
        throw new Exception("Invalid state(-1)");
      ++refs;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Release()
    {
      if(refs == -1)
        throw new Exception("Invalid state(-1)");
      if(refs == 0)
        throw new Exception("Double free(0)");

      --refs;
      if(refs == 0)
        Del(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsStopped()
    {
      return stop_guard || exec.ip >= STOP_IP;
    }

    public void GetStackTrace(List<VM.TraceItem> info)
    {
      exec.GetStackTrace(info);
    }

    public string GetStackTrace(Error.TraceFormat format = Error.TraceFormat.Compact)
    {
      var trace = new List<TraceItem>();
      GetStackTrace(trace);
      return Error.ToString(trace, format);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool ITask.Tick()
    {
      return Tick();
    }

    //Returns true if we are still running
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Tick()
    {
      if(IsStopped())
        return false;

      try
      {
        exec.Execute();

        //Checking if there's no running coroutine
        if(status != BHS.RUNNING)
          _AfterStop();
      }
      catch(Exception e)
      {
        var trace = new List<VM.TraceItem>();
        try
        {
          GetStackTrace(trace);
        }
        catch(Exception)
        {
        }
        throw new Error(trace, e);
      }

      return !IsStopped();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ITask.Stop()
    {
      Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Stop(bool with_children = false)
    {
      if(!IsStopped())
      {
        try
        {
          _AfterStop();
          exec.stack.ClearAndRelease();
        }
        catch(Exception e)
        {
          var trace = new List<VM.TraceItem>();
          try
          {
            GetStackTrace(trace);
          }
          catch(Exception)
          {
          }

          throw new Error(trace, e);
        }
      }

      if(with_children)
        StopChildren();
    }

    //NOTE: we don't release children, since we don't own them
    //      (either VM owns them and will Release them since they are stopped,
    //       or it will be user's responsibility)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void StopChildren()
    {
      foreach(var child_ref in children)
      {
        var child = child_ref.Get();
        child?.Stop(with_children: true);
      }
    }
  }

  int fibers_ids = 0;
  List<Fiber> fibers = new List<Fiber>();
  public IReadOnlyList<Fiber> Fibers => fibers;
  public Fiber last_fiber = null;

  [Flags]
  public enum FiberOptions
  {
    Detach = 1,
  }

  public Fiber Start(FuncAddr addr)
  {
    return Start(addr, new FuncArgsInfo(0u), new StackList<Val>());
  }

  public Fiber Start(FuncAddr addr, FuncArgsInfo args_info, StackList<Val> args, FiberOptions opts = 0)
  {
    var fb = Fiber.New(this);
    fb.func_addr = addr;
    Register(fb, null, opts);

    int frame_idx = fb.exec.frames_count;
    ref var frame = ref fb.exec.PushFrame();
    frame.args_info = args_info;

    //checking native call
    if(addr.fsn != null)
    {
      //let's exit immediately
      frame.start_ip = VM.EXIT_FRAME_IP;
      fb.exec.PushFrameRegion(addr.fsn, ref frame, frame_idx, args);
    }
    else
    {
      frame.InitWithModule(addr.module, addr.ip);
      fb.exec.PushFrameRegion(ref frame, frame_idx, args);
    }

    return fb;
  }

  public Fiber Start(
    VM.ExecState origin_exec,
    FuncPtr ptr,
    ref Frame origin_frame,
    StackList<Val> args,
    FiberOptions opts = 0
    )
  {
    var new_fiber = Fiber.New(this);
    new_fiber.func_addr = ptr.func_addr;
    Register(new_fiber, origin_exec.fiber, opts);

    int new_frame_idx = new_fiber.exec.frames_count;
    ref var new_frame = ref new_fiber.exec.PushFrame();
    new_frame.args_info = new FuncArgsInfo(args.Count);
    ptr.InitFrame(new_fiber.exec, ref origin_frame, ref new_frame);

    if(ptr.native != null)
      new_fiber.exec.PushFrameRegion(ptr.native, ref new_frame, new_frame_idx, args);
    else
      new_fiber.exec.PushFrameRegion(ref new_frame, new_frame_idx, args);

    return new_fiber;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void Register(Fiber fb, Fiber parent, FiberOptions opts)
  {
    fb.id = ++fibers_ids;
    fb.exec.status = BHS.NONE;
    if(!opts.HasFlag(FiberOptions.Detach))
    {
      fibers.Add(fb);
      parent?.AddChild(fb);
    }
  }

  StackArray<ExecState> script_executors = new (
    new ExecState[] { new (), new () }
    );
  int script_executor_idx = -1;
  int script_executors_count = 0;

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ValStack Execute(FuncSymbolScript fs)
  {
    return Execute(fs, new FuncArgsInfo(0u), new StackList<Val>());
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ValStack Execute(FuncSymbolScript fs, StackList<Val> args)
  {
    return Execute(fs, new FuncArgsInfo(args.Count), args);
  }

  public ValStack Execute(FuncSymbolScript fs, FuncArgsInfo args_info, StackList<Val> args)
  {
    if(++script_executor_idx == script_executors_count)
    {
      ref var tmp = ref script_executors.Push();
      tmp = new ExecState();
      ++script_executors_count;
    }
    var exec = script_executors.Values[script_executor_idx];
    var res = Execute(exec, fs, args_info, args);
    --script_executor_idx;
    return res;
  }

  ValStack Execute(ExecState exec, FuncSymbolScript fs, FuncArgsInfo args_info, StackList<Val> args)
  {
    exec.vm = this;

    var stack = exec.stack;

    //NOTE: let's clean the stack from previous any non popped results
    //      (we keep it around just in case someone forgot to pop it after successful execution)
    stack.ClearAndRelease();

    //NOTE: we push arguments using their 'natural' order since
    //      they are located exactly in this order in Frame's
    //      local arguments (stack is a part of contiguous memory)
    for(int i = 0; i < args.Count; ++i)
    {
      ref Val v = ref stack.Push();
      v = args[i];
    }

    int frame_idx = exec.frames_count;
    ref var frame = ref exec.PushFrame();
    frame.args_info = args_info;
    frame.InitWithModule(fs._module, fs._ip_addr);
    exec.PushFrameRegion(ref frame, frame_idx);

    exec.Execute();
    if(exec.status == BHS.RUNNING)
      throw new Exception($"Not expected to be running: {fs}");

    return stack;
  }
}

}
