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

    public static Val Encode(VM vm, VM.Fiber fb)
    {
      var val = Val.NewObj(vm, fb, Types.FiberRef);
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

    public int Count => fb.result.Count;

    public FiberResult(Fiber fb)
    {
      this.fb = fb;
    }

    public Val Pop()
    {
      return fb.result.Pop();
    }

    public Val PopRelease()
    {
      return fb.result.PopRelease();
    }
  }

  public class Fiber : ITask
  {
    public VM vm;

    internal FuncAddr func_addr;
    public FuncAddr FuncAddr => func_addr;

    internal FiberRef parent;
    public FiberRef Parent => parent;

    internal List<FiberRef> children = new List<FiberRef>();

    public IReadOnlyList<FiberRef> Children => children;

    //NOTE: -1 means it's in released state,
    //      public only for inspection
    public int refs;

    internal int id;
    public int Id => id;

    internal bool stop_guard;

    public ExecState exec = new ExecState();

    public ValStack result = new ValStack(Frame.MAX_STACK);

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
      while(fb.result.Count > 0)
      {
        var val = fb.result.Pop();
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

      fb.Clear();
      fb.vm.fibers_pool.stack.Push(fb);
    }

    //NOTE: use New() instead
    internal Fiber(VM vm)
    {
      this.vm = vm;
    }

    internal void Attach(Frame frm)
    {
      frm.fb = this;
      exec.ip = frm.start_ip;
      exec.frames.Push(frm);
      exec.regions.Push(new Region(frm, frm.defers));
      exec.stack = frm.stack;
    }

    internal void ExitScopes()
    {
      if(exec.frames.Count > 0)
      {
        if(exec.coroutine != null)
        {
          CoroutinePool.Del(exec.frames.Peek(), exec, exec.coroutine);
          exec.coroutine = null;
        }

        for(int i = exec.frames.Count; i-- > 0;)
        {
          var frm = exec.frames[i];
          frm.ExitScope(null, exec);
        }

        //NOTE: we need to release frames only after we actually exited their scopes
        for(int i = exec.frames.Count; i-- > 0;)
        {
          var frm = exec.frames[i];
          frm.Release();
        }

        exec.frames.Clear();
      }

      exec.regions.Clear();
    }

    internal void Clear()
    {
      ExitScopes();
    }

    internal void AddChild(Fiber fb)
    {
      fb.parent.Set(this);
      children.Add(new FiberRef(fb));
    }

    public void Retain()
    {
      if(refs == -1)
        throw new Exception("Invalid state(-1)");
      ++refs;
    }

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

    static void GetCalls(VM.ExecState exec, List<VM.Frame> calls)
    {
      for(int i = 0; i < exec.frames.Count; ++i)
        calls.Add(exec.frames[i]);
    }

    public void GetStackTrace(List<VM.TraceItem> info)
    {
      var calls = new List<VM.Frame>();
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

    static bool TryGetTraceInfo(ICoroutine i, ref int ip, List<VM.Frame> calls)
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
    return Start(addr, new StackList<Val>());
  }

  public Fiber Start(FuncAddr addr, StackList<Val> args)
  {
    return Start(addr, args.Count, args);
  }

  //NOTE: args passed to the Fiber will be released during actual func call, this is what happens:
  //       1) args put into stack
  //       2) ArgVar opcode pops arg from the stack, copies the value and releases the popped arg
  public Fiber Start(FuncAddr addr, FuncArgsInfo args_info, StackList<Val> args, FiberOptions opts = 0)
  {
    var fb = Fiber.New(this);
    fb.func_addr = addr;
    Register(fb, null, opts);

    var frame = Frame.New(this);

    //checking native call
    if(addr.fsn != null)
    {
      frame.Init(fb, fb.result, addr.module, null, null, null, VM.EXIT_FRAME_IP);

      PassArgsAndAttach(addr.fsn, fb, frame, fb.result, args_info, args);
    }
    else
    {
      frame.Init(fb, fb.result, addr.module, addr.ip);

      PassArgsAndAttach(fb, frame, Val.NewInt(this, args_info.bits), args);
    }

    if(opts.HasFlag(FiberOptions.Retain))
      fb.Retain();
    return fb;
  }

  public Fiber Start2(FuncAddr addr, FuncArgsInfo args_info, StackList<Val2> args, FiberOptions opts = 0)
  {
    var fb = Fiber.New(this);
    fb.func_addr = addr;
    Register(fb, null, opts);

    var frame = Frame.New(this);

    //checking native call
    if(addr.fsn != null)
    {
      frame.Init(fb, fb.result, addr.module, null, null, null, VM.EXIT_FRAME_IP);

      //PassArgsAndAttach(addr.fsn, fb, frame, fb.result, args_info, args);
    }
    else
    {
      frame.Init(fb, fb.result, addr.module, addr.ip);

      PassArgsAndAttach2(fb, frame, Val2.NewInt(this, args_info.bits), args);
    }

    if(opts.HasFlag(FiberOptions.Retain))
      fb.Retain();
    return fb;
  }

  [Obsolete("Use Start(FuncAddr, FuncArgsInfo args_info, StackList<Val> args) instead.")]
  public Fiber Start(FuncAddr addr, uint cargs_bits = 0, params Val[] args)
  {
    return Start(addr, cargs_bits, new StackList<Val>(args));
  }

  public Fiber Start(FuncPtr ptr, Frame curr_frame, FiberOptions opts = 0)
  {
    return Start(ptr, curr_frame, new StackList<Val>(), opts);
  }

  public Fiber Start(FuncPtr ptr, Frame curr_frame, StackList<Val> args, FiberOptions opts = 0)
  {
    var fb = Fiber.New(this);
    fb.func_addr = ptr.func_addr;
    Register(fb, curr_frame.fb, opts);

    var frame = ptr.MakeFrame(this, fb, fb.result);

    var args_info = new FuncArgsInfo(args.Count);

    if(ptr.native != null)
      PassArgsAndAttach(ptr.native, fb, frame, fb.result, args_info, args);
    else
      PassArgsAndAttach(fb, frame, Val.NewInt(this, args_info.bits), args);

    return fb;
  }

  static void PassArgsAndAttach(
    FuncSymbolNative fsn,
    Fiber fb,
    Frame frame,
    ValStack curr_stack,
    FuncArgsInfo args_info,
    StackList <Val> args
  )
  {
    for(int i = args.Count; i-- > 0;)
    {
      var arg = args[i];
      curr_stack.Push(arg);
    }

    fb.Attach(frame);
    //overriding exec.stack with passed curr_stack
    fb.exec.stack = curr_stack;

    //passing args info as argument
    fb.exec.coroutine = fsn.cb(frame, curr_stack, args_info, ref fb.status);
    //NOTE: before executing a coroutine VM will increment ip optimistically
    //      but we need it to remain at the same position so that it points at
    //      the fake return opcode
    if(fb.exec.coroutine != null)
      --fb.exec.ip;
  }

  static void PassArgsAndAttach(
    Fiber fb,
    Frame frame,
    Val args_info,
    StackList <Val> args
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

  static void PassArgsAndAttach2(
    Fiber fb,
    Frame frame,
    Val2 args_info,
    StackList <Val2> args
  )
  {
    var stack = fb.exec.stack2;
    for(int i = args.Count; i-- > 0;)
    {
      ref Val2 v = ref stack.Push();
      v = args[i];
    }

    {
      //passing args info as a stack variable
      ref Val2 v = ref stack.Push();
      v = args_info;
    }

    fb.Attach(frame);
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

  static bool _Stop(Fiber fb)
  {
    if(fb.IsStopped())
      return false;
    fb.stop_guard = true;

    fb.ExitScopes();

    //NOTE: we assign Fiber ip to a special value which is just one value after STOP_IP
    //      this way Fiber breaks its current Frame execution loop.
    fb.exec.ip = STOP_IP + 1;

    fb.Release();

    return true;
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
    VM.Frame frm;

    Val args_info_val;

    public ScriptExecutor(VM vm)
    {
      this.vm = vm;

      //NOTE: manually creating Fiber
      fb = new VM.Fiber(vm);
      //just for consistency with refcounting
      fb.Retain();

      //NOTE: manually creating Frame
      frm = new VM.Frame(vm);
      //just for consistency with refcounting
      frm.Retain();

      args_info_val = new Val(vm);
      args_info_val.num = 0;
      //let's own it forever
      args_info_val.Retain();
    }

    public FiberResult Execute(FuncSymbolScript fs, FuncArgsInfo args_info, StackList<Val> args)
    {
      var addr = new FuncAddr(fs);

      fb.func_addr = addr;
      fb.stop_guard = false;
      while(fb.result.Count > 0)
      {
        var val = fb.result.Pop();
        val.Release();
      }

      fb.Retain();

      frm.Retain();

      frm.Init(fb, fb.result, addr.module, addr.ip);

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

    public FiberResult Execute2(FuncSymbolScript fs, FuncArgsInfo args_info, StackList<Val2> args)
    {
      var addr = new FuncAddr(fs);

      fb.func_addr = addr;
      fb.stop_guard = false;
      while(fb.result.Count > 0)
      {
        var val = fb.result.Pop();
        val.Release();
      }

      fb.Retain();

      frm.Retain();

      frm.Init(fb, fb.result, addr.module, addr.ip);

      var stack = fb.exec.stack2;
      for(int i = args.Count; i-- > 0;)
      {
        ref Val2 v = ref stack.Push();
        v = args[i];
      }

      {
        //passing args info as stack variable
        ref Val2 v = ref stack.Push();
        v._num = args_info.bits;
      }

      fb.Attach(frm);

      if(vm.Tick(fb))
        throw new Exception($"Not expected to be running: {fs}");

      //let's clear stuff
      frm.Clear();

      return new FiberResult(fb);
    }
  }

  Stack<ScriptExecutor> script_executors = new Stack<ScriptExecutor>();

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public FiberResult Execute(FuncSymbolScript fs)
  {
    return Execute(fs, 0u, new StackList<Val>());
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public FiberResult Execute2(FuncSymbolScript fs)
  {
    return Execute2(fs, 0u, new StackList<Val2>());
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public FiberResult Execute(FuncSymbolScript fs, StackList<Val> args)
  {
    return Execute(fs, args.Count, args);
  }

  public FiberResult Execute(FuncSymbolScript fs, FuncArgsInfo args_info, StackList<Val> args)
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

  public FiberResult Execute2(FuncSymbolScript fs, FuncArgsInfo args_info, StackList<Val2> args)
  {
    ScriptExecutor executor;
    if(script_executors.Count == 0)
      executor = new ScriptExecutor(this);
    else
      executor = script_executors.Pop();
    var res = executor.Execute2(fs, args_info, args);
    script_executors.Push(executor);
    return res;
  }
}

}
