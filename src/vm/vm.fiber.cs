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

    public static ValOld Encode(VM vm, VM.Fiber fb)
    {
      var val = ValOld.NewObj(vm, fb, Types.FiberRef);
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

    //TODO: get rid of this one?
    public BHS status;

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
    }

    internal void Attach(FrameOld frm)
    {
      frm.fb = this;
      exec.ip = frm.start_ip;
      exec.frames_old.Push(frm);
      exec.regions[exec.regions_count++] = new Region(frm, -1, frm.defers);
      exec.stack_old = frm.stack;
    }

    internal void Attach(ref Frame frame, int frame_idx)
    {
      //do we need this?
      //frame2.fb = this;
      exec.ip = frame.start_ip;
      //already pushed
      //exec.frames.Push(frm);
      var region = new Region(frame_old: null, frame_idx: frame_idx, defer_support: null);
      exec.regions[exec.regions_count++] = region;
      //really?
      //exec.stack = frm.stack;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ExitScopes()
    {
      if(exec.frames_count > 0)
      {
        if(exec.coroutine != null)
        {
          CoroutinePool.Del(ref exec.frames[exec.frames_count-1], exec, exec.coroutine);
          exec.coroutine = null;
        }

        for(int i = exec.frames_count; i-- > 0;)
        {
          ref var frm = ref exec.frames[i];
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

    internal void ExitScopesOld()
    {
      if(exec.frames_old.Count > 0)
      {
        if(exec.coroutine != null)
        {
          CoroutinePool.DelOld(exec.frames_old.Peek(), exec, exec.coroutine);
          exec.coroutine = null;
        }

        for(int i = exec.frames_old.Count; i-- > 0;)
        {
          var frm = exec.frames_old[i];
          frm.ExitScope(null, exec);
        }

        //NOTE: we need to release frames only after we actually exited their scopes
        for(int i = exec.frames_old.Count; i-- > 0;)
        {
          var frm = exec.frames_old[i];
          frm.Release();
        }

        exec.frames_old.Clear();
      }

      exec.regions_count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CleanStack()
    {
      while(exec.stack.sp > 0)
        exec.stack.PopRelease();
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
      var calls = new List<VM.FrameOld>();
      int coroutine_ip = -1;
      GetCalls(exec, calls);
      TryGetTraceInfo(exec.coroutine, ref coroutine_ip, calls);

      for(int i = 0; i < calls.Count; ++i)
      {
        var frm = calls[i];

        var item = new TraceItem();

        //NOTE: information about frame ip is taken from the 'next' frame, however
        //      for the last frame we have a special case. In this case there's no
        //      'next' frame and we should consider taking ip from Fiber or an active
        //      coroutine
        if(i == calls.Count - 1)
        {
          item.ip = coroutine_ip == -1 ? frm.fb.exec.ip : coroutine_ip;
        }
        else
        {
          //NOTE: retrieving last ip for the current Frame which
          //      turns out to be return_ip assigned to the next Frame
          var next = calls[i + 1];
          item.ip = next.return_ip;
        }

        if(frm.module != null)
        {
          var fsymb = frm.module.TryMapIp2Func(calls[i].start_ip);
          //NOTE: if symbol is missing it's a lambda
          if(fsymb == null)
            item.file = frm.module.name + ".bhl";
          else
            item.file = fsymb._module.name + ".bhl";
          item.func = fsymb == null ? "?" : fsymb.name;
          item.line = frm.module.compiled.ip2src_line.TryMap(item.ip);
        }
        else
        {
          item.file = "?";
          item.func = "?";
        }

        info.Insert(0, item);
      }
    }

    public string GetStackTrace(Error.TraceFormat format = Error.TraceFormat.Compact)
    {
      var trace = new List<TraceItem>();
      GetStackTrace(trace);
      return Error.ToString(trace, format);
    }

    static bool TryGetTraceInfo(ICoroutine i, ref int ip, List<VM.FrameOld> calls)
    {
      if(i is SeqBlock si)
      {
        GetCalls(si.exec, calls);
        if(!TryGetTraceInfo(si.exec.coroutine, ref ip, calls))
          ip = si.exec.ip;
        return true;
      }
      else if(i is ParalBranchBlock bi)
      {
        GetCalls(bi.exec, calls);
        if(!TryGetTraceInfo(bi.exec.coroutine, ref ip, calls))
          ip = bi.exec.ip;
        return true;
      }
      else if(i is ParalBlock pi && pi.i < pi.branches.Count)
        return TryGetTraceInfo(pi.branches[pi.i], ref ip, calls);
      else if(i is ParalAllBlock pai && pai.i < pai.branches.Count)
        return TryGetTraceInfo(pai.branches[pai.i], ref ip, calls);
      else
        return false;
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

  public Fiber StartOld(FuncAddr addr, StackList<ValOld> args)
  {
    return StartOld(addr, new FuncArgsInfo(args.Count), args);
  }

  //NOTE: args passed to the Fiber will be released during actual func call, this is what happens:
  //       1) args put into stack
  //       2) ArgVar opcode pops arg from the stack, copies the value and releases the popped arg
  public Fiber StartOld(FuncAddr addr, FuncArgsInfo args_info, StackList<ValOld> args, FiberOptions opts = 0)
  {
    var fb = Fiber.New(this);
    fb.func_addr = addr;
    Register(fb, null, opts);

    var frame = FrameOld.New(this);

    //checking native call
    if(addr.fsn != null)
    {
      frame.Init(fb, fb.result_old, addr.module, null, null, null, VM.EXIT_FRAME_IP);

      PassArgsAndAttach(addr.fsn, fb, frame, fb.result_old, args_info, args);
    }
    else
    {
      frame.Init(fb, fb.result_old, addr.module, addr.ip);

      PassArgsAndAttach(fb, frame, ValOld.NewInt(this, args_info.bits), args);
    }

    if(opts.HasFlag(FiberOptions.Retain))
      fb.Retain();
    return fb;
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
      throw new NotImplementedException();
      //frame.Init(fb, fb.result, addr.module, null, null, null, VM.EXIT_FRAME_IP);

      //PassArgsAndAttach(addr.fsn, fb, frame, fb.result, args_info, args);
    }
    else
    {
      frame.Init(/*fb, fb.result, */addr.module, addr.ip);

      PassArgsAndAttach(fb, ref frame, frame_idx, Val.NewInt(args_info.bits), args);
    }

    if(opts.HasFlag(FiberOptions.Retain))
      fb.Retain();
    return fb;
  }

  [Obsolete("Use Start(FuncAddr, FuncArgsInfo args_info, StackList<Val> args) instead.")]
  public Fiber Start(FuncAddr addr, uint cargs_bits = 0, params ValOld[] args)
  {
    return StartOld(addr, new FuncArgsInfo(cargs_bits), new StackList<ValOld>(args));
  }

  public Fiber Start(FuncPtr ptr, FrameOld curr_frame, FiberOptions opts = 0)
  {
    return Start(ptr, curr_frame, new StackList<ValOld>(), opts);
  }

  public Fiber Start(FuncPtr ptr, FrameOld curr_frame, StackList<ValOld> args, FiberOptions opts = 0)
  {
    var fb = Fiber.New(this);
    fb.func_addr = ptr.func_addr;
    Register(fb, curr_frame.fb, opts);

    var frame = ptr.MakeFrame(this, fb, fb.result_old);

    var args_info = new FuncArgsInfo(args.Count);

    if(ptr.native != null)
      PassArgsAndAttach(ptr.native, fb, frame, fb.result_old, args_info, args);
    else
      PassArgsAndAttach(fb, frame, ValOld.NewInt(this, args_info.bits), args);

    return fb;
  }

  static void PassArgsAndAttach(
    FuncSymbolNative fsn,
    Fiber fb,
    FrameOld frame,
    ValOldStack curr_stack,
    FuncArgsInfo args_info,
    StackList <ValOld> args
  )
  {
    for(int i = args.Count; i-- > 0;)
    {
      var arg = args[i];
      curr_stack.Push(arg);
    }

    fb.Attach(frame);
    //overriding exec.stack with passed curr_stack
    fb.exec.stack_old = curr_stack;

    //passing args info as argument
    fb.exec.coroutine = fsn.cb(fb.exec, fb.exec.stack, args_info, ref fb.status);
    //NOTE: before executing a coroutine VM will increment ip optimistically
    //      but we need it to remain at the same position so that it points at
    //      the fake return opcode
    if(fb.exec.coroutine != null)
      --fb.exec.ip;
  }

  static void PassArgsAndAttach(
    Fiber fb,
    FrameOld frame,
    ValOld args_info,
    StackList <ValOld> args
  )
  {
    for(int i = args.Count; i-- > 0;)
    {
      var arg = args[i];
      frame.stack.Push(arg);
    }

    //passing args info as stack variable
    frame.stack.Push(args_info);

    fb.Attach(frame);
  }

  static void PassArgsAndAttach(
    Fiber fb,
    ref Frame frame,
    int frame_idx2,
    Val args_info,
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
      v = args_info;
    }

    fb.Attach(ref frame, frame_idx2);
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

    public FiberResult ExecuteOld(FuncSymbolScript fs, FuncArgsInfo args_info, StackList<ValOld> args)
    {
      var addr = new FuncAddr(fs);

      fb.func_addr = addr;
      fb.stop_guard = false;
      while(fb.result_old.Count > 0)
      {
        var val = fb.result_old.Pop();
        val.Release();
      }

      fb.Retain();

      frm.Retain();

      frm.Init(fb, fb.result_old, addr.module, addr.ip);

      for(int i = args.Count; i-- > 0;)
      {
        var arg = args[i];
        frm.stack.Push(arg);
      }

      //passing args info as stack variable
      args_info_val.Retain();
      args_info_val._num = args_info.bits;
      frm.stack.Push(args_info_val);

      fb.Attach(frm);

      if(vm.Tick(fb))
        throw new Exception($"Not expected to be running: {fs}");

      //let's clear stuff
      frm.Clear();

      return new FiberResult(fb);
    }

    public ValStack Execute(FuncSymbolScript fs, FuncArgsInfo args_info, StackList<Val> args)
    {
      var addr = new FuncAddr(fs);

      fb.func_addr = addr;
      fb.stop_guard = false;
      //let's clean the stack from previous non popped results
      fb.CleanStack();
      fb.Retain();

      ref var frame = ref fb.exec.PushFrame();
      frame.Init(addr.module, addr.ip);

      var stack = fb.exec.stack;
      for(int i = args.Count; i-- > 0;)
      {
        ref Val v = ref stack.Push();
        v = args[i];
      }

      {
        //passing args info as stack variable
        ref Val v = ref stack.Push();
        v._num = args_info.bits;
      }

      fb.Attach(ref frame, fb.exec.frames_count - 1);

      if(vm.Tick(fb))
        throw new Exception($"Not expected to be running: {fs}");

      return fb.exec.stack;
    }
  }

  //TODO: use pre-allocated array
  Stack<ScriptExecutor> script_executors = new Stack<ScriptExecutor>();

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public FiberResult ExecuteOld(FuncSymbolScript fs)
  {
    return ExecuteOld(fs, new FuncArgsInfo(0u), new StackList<ValOld>());
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ValStack Execute(FuncSymbolScript fs)
  {
    return Execute(fs, new FuncArgsInfo(0u), new StackList<Val>());
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public FiberResult ExecuteOld(FuncSymbolScript fs, StackList<ValOld> args)
  {
    return ExecuteOld(fs, new FuncArgsInfo(args.Count), args);
  }

  public FiberResult ExecuteOld(FuncSymbolScript fs, FuncArgsInfo args_info, StackList<ValOld> args)
  {
    ScriptExecutor executor;
    if(script_executors.Count == 0)
      executor = new ScriptExecutor(this);
    else
      executor = script_executors.Pop();
    var res = executor.ExecuteOld(fs, args_info, args);
    script_executors.Push(executor);
    return res;
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

  //public void Execute2(FuncSymbolScript fs, FuncArgsInfo args_info, StackList<Val2> args)
  //{
  //  var fb = VM.Fiber.New(this);

  //  var addr = new FuncAddr(fs);
  //  fb.func_addr = addr;

  //  ref var frame2 = ref fb.exec.PushFrame2();
  //  frame2.Init(addr.module, addr.ip);

  //  var stack = fb.exec.stack2;
  //  for(int i = args.Count; i-- > 0;)
  //  {
  //    ref Val2 v = ref stack.Push();
  //    v = args[i];
  //  }

  //  {
  //    //passing args info as stack variable
  //    ref Val2 v = ref stack.Push();
  //    v._num = args_info.bits;
  //  }

  //  fb.Attach2(ref frame2, fb.exec.frames2_count - 1);

  //  if(Tick(fb))
  //    throw new Exception($"Not expected to be running: {fs}");

  //  //not needed, Tick(..) does it
  //  //fb.Release();
  //}
}

}
