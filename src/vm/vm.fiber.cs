using System;
using System.Collections.Generic;

namespace bhl {

public partial class VM : INamedResolver
{
  public struct FiberRef
  {
    int id;
    Fiber fiber;

    public FiberRef(Fiber fiber)
    {
      this.id = (fiber?.id ?? 0);
      this.fiber = fiber;
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

    internal int tick;

    internal ExecState exec = new ExecState();

    public VM.Frame frame0 {
      get {
        return exec.frames[0];
      }
    }
    public FixedStack<Val> result = new FixedStack<Val>(Frame.MAX_STACK);
    
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
      fb.result.Clear();
      fb.parent.Clear();
      fb.children.Clear();

      //0 index frame used for return values consistency
      fb.exec.frames.Push(Frame.New(vm));

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
      exec.regions.Push(new Region(frm, frm));
      exec.stack = frm._stack;
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

        //we need to copy 0 index frame returned values 
        {
          var frame0 = exec.frames[0];
          for(int c=0;c<frame0._stack.Count;++c)
            result.Push(frame0._stack[c]);
          //let's clear the frame's stack so that values 
          //won't be released below
          frame0._stack.Clear();
        }

        for(int i=exec.frames.Count;i-- > 0;)
        {
          var frm = exec.frames[i];
          frm.ExitScope(null, exec);
        }

        //NOTE: we need to release frames only after we actually exited their scopes
        for(int i=exec.frames.Count;i-- > 0;)
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

      tick = 0;
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

    public bool IsStopped()
    {
      return exec.ip >= STOP_IP;
    }

    static void GetCalls(VM.ExecState exec, List<VM.Frame> calls, int offset = 0)
    {
      for(int i=offset;i<exec.frames.Count;++i)
        calls.Add(exec.frames[i]);
    }

    public void GetStackTrace(List<VM.TraceItem> info)
    {
      var calls = new List<VM.Frame>();
      int coroutine_ip = -1; 
      GetCalls(exec, calls, offset: 1/*let's skip 0 fake frame*/);
      TryGetTraceInfo(exec.coroutine, ref coroutine_ip, calls);

      for(int i=0;i<calls.Count;++i)
      {
        var frm = calls[i];

        var item = new TraceItem(); 

        //NOTE: information about frame ip is taken from the 'next' frame, however 
        //      for the last frame we have a special case. In this case there's no
        //      'next' frame and we should consider taking ip from Fiber or an active
        //      coroutine
        if(i == calls.Count-1)
        {
          item.ip = coroutine_ip == -1 ? frm.fb.exec.ip : coroutine_ip;
        }
        else
        {
          //NOTE: retrieving last ip for the current Frame which 
          //      turns out to be return_ip assigned to the next Frame
          var next = calls[i+1];
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

    public string GetStackTrace()
    {
      var trace = new List<TraceItem>();
      GetStackTrace(trace);
      return Error.ToString(trace);
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

    bool ITask.Tick()
    {
      return vm.Tick(this);
    }

    void ITask.Stop()
    {
      vm.Stop(this);
    }
  }
  
  int fibers_ids = 0;
  List<Fiber> fibers = new List<Fiber>();
  public Fiber last_fiber = null;

  
  public Fiber Start(string func, params Val[] args)
  {
    return Start(func, FuncArgsInfo.GetBits(args?.Length ?? 0), args);
  }

  public Fiber Start(string func, FuncArgsInfo args_info, params Val[] args)
  {
    return Start(func, args_info.bits, args);
  }

  public Fiber Start(string func, uint cargs_bits, params Val[] args)
  {
    return Start(func, cargs_bits, new StackList<Val>(args));
  }

  public Fiber Start(string func, StackList<Val> args)
  {
    return Start(func, FuncArgsInfo.GetBits(args.Count), args);
  }

  public Fiber Start(string func, FuncArgsInfo args_info, StackList<Val> args)
  {
    return Start(func, args_info.bits, args);
  }

  public Fiber Start(string func, uint cargs_bits, StackList<Val> args)
  {
    if(!TryFindFuncAddr(func, out var addr))
      return null;

    return Start(addr, cargs_bits, args);
  }

  public Fiber Start(FuncAddr addr, uint cargs_bits = 0, params Val[] args)
  {
    return Start(addr, cargs_bits, new StackList<Val>(args));
  }

  public Fiber Start(FuncAddr addr, StackList<Val> args)
  {
    return Start(addr, FuncArgsInfo.GetBits(args.Count), args);
  }
  
  public Fiber Start(FuncSymbolScript fs, StackList<Val> args)
  {
    var addr = new FuncAddr() { module = fs._module, fs = fs, ip = fs.ip_addr };
    return Start(addr, FuncArgsInfo.GetBits(args.Count), args);
  }

  public Fiber Start(FuncAddr addr, uint cargs_bits, StackList<Val> args)
  {
    var fb = Fiber.New(this);
    fb.func_addr = addr;
    Register(fb);

    var frame = Frame.New(this);

    var frame0 = fb.frame0;

    //checking native call
    if(addr.fsn != null)
    {
      frame.Init(fb, frame0, frame0._stack, addr.module, null, null, null, VM.EXIT_FRAME_IP);

      //NOTE: we use frame0's stack not new frame's stack as a hack for simplicity
      //     related to returning all result values from the call. In 'normal' flow
      //     there's a special opcode responsible for returning the certain amount of values
      PassArgsAndAttach(addr.fsn, fb, frame, frame0._stack, new FuncArgsInfo(cargs_bits), args);
    }
    else
    {
      frame.Init(fb, frame0, frame0._stack, addr.module, addr.ip);

      PassArgsAndAttach(fb, frame, frame._stack, Val.NewInt(this, cargs_bits), args);
    }

    return fb;
  }

  public Fiber Start(FuncPtr ptr, Frame curr_frame, ValStack curr_stack)
  {
    return Start(ptr, curr_frame, curr_stack, new StackList<Val>());
  }

  public Fiber Start(FuncPtr ptr, Frame curr_frame, ValStack curr_stack, params Val[] args)
  {
    return Start(ptr, curr_frame, curr_stack, new StackList<Val>(args));
  }

  public Fiber Start(FuncPtr ptr, Frame curr_frame, ValStack curr_stack, StackList<Val> args)
  {
    var fb = Fiber.New(this);
    fb.func_addr = ptr.func_addr;
    Register(fb, curr_frame.fb);

    var frame = ptr.MakeFrame(this, curr_frame, curr_stack);  

    if(ptr.native != null)
    {
      //TODO: shouldn't be this done in ptr.MakeFrame(..)?
      frame.Init(fb, curr_frame, curr_stack, null, null, null, null, VM.EXIT_FRAME_IP);

      //NOTE: we use curr_stack not new fake frame's stack for simplicity
      //     related to returning all result values from the call. In 'normal' flow
      //     there's a special opcode responsible for returning the certain amount of values
      PassArgsAndAttach(ptr.native, fb, frame, curr_stack, new FuncArgsInfo(args.Count), args);
    }
    else
    {
      //NOTE: frame is already initialized in ptr.MakeFrame(..)

      PassArgsAndAttach(fb, frame, frame._stack, Val.NewInt(this, (uint)args.Count), args);
    }

    return fb;
  }

  internal static void PassArgsAndAttach(
    FuncSymbolNative fsn,
    Fiber fb, 
    Frame frame, 
    ValStack curr_stack,
    FuncArgsInfo args_info,
    StackList <Val> args
  )
  {
    for(int i=args.Count;i-- > 0;)
    {
      var arg = args[i];
      curr_stack.Push(arg);
    }
    fb.Attach(frame);
    fb.exec.stack = curr_stack; 
    
    //passing args info as argument
    fb.exec.coroutine = fsn.cb(frame, curr_stack, args_info, ref fb.status);
    //NOTE: before executing a coroutine VM will increment ip optimistically
    //      but we need it to remain at the same position so that it points at
    //      the fake return opcode
    if(fb.exec.coroutine != null)
      --fb.exec.ip;
  }

  internal static void PassArgsAndAttach(
    Fiber fb, 
    Frame frame, 
    ValStack curr_stack,
    Val args_info,
    StackList <Val> args
  )
  {
    for(int i = args.Count; i-- > 0;)
    {
      var arg = args[i];
      curr_stack.Push(arg);
    }

    //passing args info as stack variable
    curr_stack.Push(args_info);

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

  void Register(Fiber fb, Fiber parent = null)
  {
    fb.id = ++fibers_ids;
    fibers.Add(fb);
    parent?.AddChild(fb);
  }

  public void Stop(Fiber fb)
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
      {}
      throw new Error(trace, e);
    }
  }

  public void StopChildren(Fiber fb)
  {
    foreach(var child_ref in fb.children)
    {
      var child = child_ref.Get();
      if(child != null)
      {
        StopChildren(child);
        _Stop(child);
      }
    }
  }

  internal void _Stop(Fiber fb)
  {
    if(fb.IsStopped())
      return;

    fb.ExitScopes();

    fb.Release();
    //NOTE: we assing Fiber ip to a special value which is just one value after STOP_IP
    //      this way Fiber breaks its current Frame execution loop.
    fb.exec.ip = STOP_IP + 1;
  }

  public void Stop(int fid)
  {
    var fb = FindFiber(fid);
    if(fb == null)
      return;
    Stop(fb);
  }

  public Fiber FindFiber(int fid)
  {
    for(int i=0;i<fibers.Count;++i)
      if(fibers[i].id == fid)
        return fibers[i];
    return null;
  }

  public class ScriptExecutor
  {
    VM vm;
  
    //NOTE: we manually create and own these
    VM.Fiber fb;
    VM.Frame fb_frm0;
    VM.Frame frm;

    Val args_info;
  
    public ScriptExecutor(VM vm)
    {
      this.vm = vm;
  
      //NOTE: manually creating 0 Frame
      fb_frm0 = new VM.Frame(vm);
      //just for consistency with refcounting
      fb_frm0.Retain();
  
      //NOTE: manually creating Fiber
      fb = new VM.Fiber(vm);
      //just for consistency with refcounting
      fb.Retain();
  
      //NOTE: manually creating Frame
      frm = new VM.Frame(vm);
      //just for consistency with refcounting
      frm.Retain();

      args_info = new Val(vm); 
      args_info.num = 0;
      //let's own it forever
      args_info.Retain();
    }
  
    public FiberResult Execute(FuncSymbolScript fs, uint args_bits, StackList<Val> args)
    {
      var addr = new FuncAddr() { 
        module = fs._module, 
        fs = fs, 
        ip = fs.ip_addr 
      };

      fb.func_addr = addr;
  
      fb.Retain();
  
      fb_frm0.Retain();
      //0 index frame used for return values consistency
      fb.exec.frames.Push(fb_frm0);
  
      frm.Retain();
  
      frm.Init(fb, fb_frm0, fb_frm0._stack, addr.module, addr.ip);
  
      for(int i = args.Count; i-- > 0;)
      {
        var arg = args[i];
        frm._stack.Push(arg);
      }
  
      //passing args info as stack variable
      args_info.Retain();
      args_info._num = args_bits;
      frm._stack.Push(args_info);
  
      fb.Attach(frm);

      if(vm.Tick(fb))
        throw new Exception($"Not expected to be running: {fs}");
      
      //let's clear stuff
      frm.Clear();
      fb_frm0.Clear();

      return new FiberResult(fb);
    }
  }

  ScriptExecutor script_executor;

  public FiberResult Execute(FuncSymbolScript fs)
  {
    return script_executor.Execute(fs, 0, new StackList<Val>());
  }

  public FiberResult Execute(FuncSymbolScript fs, StackList<Val> args)
  {
    return script_executor.Execute(fs, FuncArgsInfo.GetBits(args.Count), args);
  }
}

}