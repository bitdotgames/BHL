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
      this.id = (int)val._num;
      this.fiber = (VM.Fiber)val._obj;
    }

    public static Val Encode(VM.Fiber fb)
    {
      var val = Val.NewObj(fb, Types.FiberRef);
      //let's encode FiberRef into Val
      val._num = fb.id;
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

      fb.exec.Reset();
      fb.vm.fibers_pool.stack.Push(fb);
    }

    //NOTE: use New() instead
    internal Fiber(VM vm)
    {
      this.vm = vm;
      exec.vm = vm;
      exec.fiber = this;
    }

    //TODO: Probably not the best name. This routine is called both in case
    //      of normal completion and in case of interruption.
    internal void AfterTickOrStop()
    {
      if(IsStopped())
        return;
      stop_guard = true;

      exec.Reset();

      //NOTE: we assign Fiber ip to a special value which is just one value after STOP_IP
      //      this way Fiber breaks its current Frame execution loop.
      exec.ip = STOP_IP + 1;

      if(status == BHS.FAILURE)
        CleanStack();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CleanStack()
    {
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Tick()
    {
      return vm.Tick(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ITask.Stop()
    {
      Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Stop(bool with_children = false)
    {
      vm.Stop(this, with_children);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void StopChildren()
    {
      vm.StopChildren(this);
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
    Retain = 2,
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
      frame.InitWithModule(addr.module, VM.EXIT_FRAME_IP);
      fb.exec.PushFrameRegion(addr.fsn, ref frame, frame_idx, args);
    }
    else
    {
      frame.InitWithModule(addr.module, addr.ip);
      fb.exec.PushFrameRegion(ref frame, frame_idx, args);
    }

    if(opts.HasFlag(FiberOptions.Retain))
      fb.Retain();
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

  public void Detach(Fiber fb)
  {
    fibers.Remove(fb);
  }

  public void Attach(Fiber fb)
  {
    if(fibers.IndexOf(fb) == -1)
      fibers.Add(fb);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void Register(Fiber fb, Fiber parent, FiberOptions opts)
  {
    fb.id = ++fibers_ids;
    fb.exec.status = BHS.NONE;
    if(!opts.HasFlag(FiberOptions.Detach))
      fibers.Add(fb);
    parent?.AddChild(fb);
  }

  public void Stop(Fiber fb, bool with_children = false)
  {
    if(!fb.IsStopped())
    {
      //TODO: uncomment once all tests pass
      //try
      {
        fb.AfterTickOrStop();
        fb.CleanStack();
        fb.Release();
      }
      //catch(Exception e)
      //{
      //  var trace = new List<VM.TraceItem>();
      //  try
      //  {
      //    fb.GetStackTrace(trace);
      //  }
      //  catch(Exception)
      //  {
      //  }

      //  throw new Error(trace, e);
      //}
    }

    if(with_children)
      StopChildren(fb);
  }

  public void StopChildren(Fiber fb)
  {
    foreach(var child_ref in fb.children)
    {
      var child = child_ref.Get();
      if(child != null)
        Stop(child, true);
    }
  }

  public class ScriptExecutor
  {
    VM vm;

    //NOTE: we manually create and own these
    VM.Fiber fb;

    public ScriptExecutor(VM vm)
    {
      this.vm = vm;

      //NOTE: manually creating Fiber
      fb = new VM.Fiber(vm);
      //just for consistency with refcounting
      fb.Retain();
    }

    public ValStack Execute(FuncSymbolScript fs, FuncArgsInfo args_info, StackList<Val> args)
    {
      var addr = new FuncAddr(fs);

      fb.func_addr = addr;
      fb.stop_guard = false;
      //let's clean the stack from previous non popped results
      fb.CleanStack();
      fb.Retain();

      int frame_idx = fb.exec.frames_count;
      ref var frame = ref fb.exec.PushFrame();
      frame.args_info = args_info;
      frame.InitWithModule(addr.module, addr.ip);

      var stack = fb.exec.stack;
      //NOTE: we push arguments using their 'natural' order since
      //      they are located exactly in this order in Frame's
      //      local arguments (stack is a part of contiguous memory)
      for(int i = 0; i < args.Count; ++i)
      {
        ref Val v = ref stack.Push();
        v = args[i];
      }

      fb.exec.PushFrameRegion(ref frame, frame_idx);

      if(vm.Tick(fb))
        throw new Exception($"Not expected to be running: {fs}");

      return fb.exec.stack;
    }
  }

  //TODO: use pre-allocated array
  Stack<ScriptExecutor> script_executors = new Stack<ScriptExecutor>();

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
    ScriptExecutor executor;
    if(script_executors.Count == 0)
      executor = new ScriptExecutor(this);
    else
      executor = script_executors.Pop();
    var res = executor.Execute(fs, args_info, args);
    script_executors.Push(executor);
    return res;
  }
}

}
