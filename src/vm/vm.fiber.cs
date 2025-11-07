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

    public FiberRef(ValOld val)
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

  public struct FiberResult
  {
    Fiber fb;

    public int Count => fb.result_old.Count;

    public FiberResult(Fiber fb)
    {
      this.fb = fb;
    }

    public ValOld Pop()
    {
      return fb.result_old.Pop();
    }

    public ValOld PopRelease()
    {
      return fb.result_old.PopRelease();
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

    public ValOldStack result_old = new ValOldStack(FrameOld.MAX_STACK);

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
      //releasing non reclaimed results
      while(fb.result_old.Count > 0)
      {
        var val = fb.result_old.Pop();
        val.Release();
      }

      fb.parent.Clear();
      fb.children.Clear();

      return fb;
    }

    static void Del(Fiber fb)
    {
      if(fb.refs != 0)
        throw new Exception("Freeing invalid object, refs " + fb.refs);

      fb.refs = -1;

      fb.ExitScopes();
      fb.vm.fibers_pool.stack.Push(fb);
    }

    //NOTE: use New() instead
    internal Fiber(VM vm)
    {
      this.vm = vm;
      exec.vm = vm;
      exec.fiber = this;
    }

    internal void Attach(ref Frame frame, int frame_idx)
    {
      exec.ip = frame.start_ip;
      var region = new Region(frame_idx: frame_idx, defers: frame.defers);
      exec.regions[exec.regions_count++] = region;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ExitScopes()
    {
      if(exec.frames_count > 0)
      {
        if(exec.coroutine != null)
        {
          CoroutinePool.Del(exec, exec.coroutine);
          exec.coroutine = null;
        }

        for(int i = exec.frames_count; i-- > 0;)
        {
          ref var frm = ref exec.frames[i];
          frm.Exit(exec.stack);
          //TODO:
          //if(frm.defers_count > 0)
          //{
          //  DeferBlock.ExitScope(exec, frm.defers, frm.defers_count);
          //  frm.defers_count = 0;
          //}
        }
        exec.frames_count = 0;
      }

      exec.regions_count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CleanStack()
    {
      while(exec.stack.sp > 0)
      {
        exec.stack.Pop(out var val);
        val._refc?.Release();
      }
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

    static void GetCalls(VM.ExecState exec, List<VM.FrameOld> calls)
    {
      for(int i = 0; i < exec.frames_old.Count; ++i)
        calls.Add(exec.frames_old[i]);
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

    //checking native call
    if(addr.fsn != null)
    {
      frame.Init(addr.module, VM.EXIT_FRAME_IP);

      PassArgsAndAttach(addr.fsn, fb, ref frame, frame_idx, args_info, args);
    }
    else
    {
      frame.Init(addr.module, addr.ip);

      PassArgsAndAttach(fb, ref frame, frame_idx, args_info, args);
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
    ptr.InitFrame(new_fiber.exec, ref origin_frame, ref new_frame);

    var args_info = new FuncArgsInfo(args.Count);

    if(ptr.native != null)
      PassArgsAndAttach(ptr.native, new_fiber, ref new_frame, new_frame_idx, args_info, args);
    else
      PassArgsAndAttach(new_fiber, ref new_frame, new_frame_idx, args_info, args);

    return new_fiber;
  }

  static void PassArgsAndAttach(
    Fiber fb,
    ref Frame frame,
    int frame_idx,
    FuncArgsInfo args_info,
    StackList <Val> args
  )
  {
    var stack = fb.exec.stack;
    for(int i = args.Count; i-- > 0;)
    {
      ref Val v = ref stack.Push();
      v = args[i];
    }

    {
      //passing args info as a stack variable
      ref Val v = ref stack.Push();
      v = Val.NewInt(args_info.bits);
    }

    fb.Attach(ref frame, frame_idx);
  }

  static void PassArgsAndAttach(
    FuncSymbolNative fsn,
    Fiber fb,
    ref Frame frame,
    int frame_idx,
    FuncArgsInfo args_info,
    StackList <Val> args
  )
  {
    var stack = fb.exec.stack;
    for(int i = args.Count; i-- > 0;)
    {
      ref Val v = ref stack.Push();
      v = args[i];
    }

    frame.args_info = args_info;

    fb.Attach(ref frame, frame_idx);

    //passing args info as argument
    fb.exec.coroutine = fsn.cb(fb.exec, args_info);
    //NOTE: before executing a coroutine VM will increment ip optimistically
    //      but we need it to remain at the same position so that it points at
    //      the fake return opcode
    if(fb.exec.coroutine != null)
      --fb.exec.ip;
    else
      //NOTE: let's consider all values on stack after callback execution
      //      as returned arguments, this way they won't be cleared upon Frame exiting
      frame.return_args_num = fb.exec.stack.sp;
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
    if(!opts.HasFlag(FiberOptions.Detach))
      fibers.Add(fb);
    parent?.AddChild(fb);
  }

  public void Stop(Fiber fb, bool with_children = false)
  {
    if(!fb.IsStopped())
    {
      try
      {
        _Stop(fb);
      }
      catch(Exception e)
      {
        var trace = new List<VM.TraceItem>();
        try
        {
          fb.GetStackTrace(trace);
        }
        catch(Exception)
        {
        }

        throw new Error(trace, e);
      }
    }

    if(with_children)
      StopChildren(fb);
  }

  static void _Stop(Fiber fb)
  {
    if(fb.IsStopped())
      return;
    fb.stop_guard = true;

    fb.ExitScopes();

    //NOTE: we assign Fiber ip to a special value which is just one value after STOP_IP
    //      this way Fiber breaks its current Frame execution loop.
    fb.exec.ip = STOP_IP + 1;

    fb.Release();

    if(fb.status == BHS.FAILURE)
      fb.CleanStack();
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
    VM.FrameOld frm;

    ValOld args_info_val;

    public ScriptExecutor(VM vm)
    {
      this.vm = vm;

      //NOTE: manually creating Fiber
      fb = new VM.Fiber(vm);
      //just for consistency with refcounting
      fb.Retain();

      //NOTE: manually creating Frame
      frm = new VM.FrameOld(vm);
      //just for consistency with refcounting
      frm.Retain();

      args_info_val = new ValOld(vm);
      args_info_val.num = 0;
      //let's own it forever
      args_info_val.Retain();
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
      frame.Init(addr.module, addr.ip);

      var stack = fb.exec.stack;
      //NOTE: we push arguments using their 'natural' order since
      //      they are located exactly in this order in Frame's
      //      local arguments (stack is a part of contiguous memory)
      for(int i = 0; i < args.Count; ++i)
      {
        ref Val v = ref stack.Push();
        v = args[i];
      }

      {
        //passing args info as stack variable
        ref Val v = ref stack.Push();
        v._num = args_info.bits;
      }

      fb.Attach(ref frame, frame_idx);

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
