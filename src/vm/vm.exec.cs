using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using bhl.marshall;

namespace bhl
{

public enum UpvalMode
{
  STRONG = 0,
  COPY   = 1
}

public partial class VM
{
  StackArray<ExecState> script_executors = new (
    new ExecState[] { new (), new () }
  );
  int script_executor_idx = -1;
  int script_executors_count = 0;

  //NOTE: why -2? we reserve some space before int.MaxValue so that
  //      increasing some ip couple of times after it was assigned
  //      a 'STOP_IP' value won't overflow int.MaxValue
  public const int STOP_IP = int.MaxValue - 2;
  public const int EXIT_FRAME_IP = STOP_IP - 1;

  public delegate void ClassCreator(VM.ExecState exec, ref Val res, IType type);

  public class DeferSupport
  {
    public DeferBlock[] blocks = new DeferBlock[1];
    public int count;

    [MethodImpl (MethodImplOptions.AggressiveInlining)]
    public ref DeferBlock Add()
    {
      if(count == blocks.Length)
        Array.Resize(ref blocks, count << 1);
      return ref blocks[count++];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitScope(VM.ExecState exec)
    {
      var coro_orig = exec.coroutine;
      for(int i = count; i-- > 0;)
      {
        exec.coroutine = null;
        blocks[i].Execute(exec);
      }
      exec.coroutine = coro_orig;
      count = 0;
    }
  }

  public struct Region
  {
    public int frame_idx;

    public DeferSupport defers;

    //NOTE: if current ip is not within *inclusive* range of these values
    //      the frame context execution is considered to be done
    public int min_ip;
    public int max_ip;
  }

  ExecState init_exec = new ExecState();

  public static readonly Val Null = Val.NewObj(null, Types.Null);
  public static readonly Val True = Val.NewBool(true);
  public static readonly Val False = Val.NewBool(false);

  void ExecInitByteCode(Module module)
  {
    if((module.compiled.initcode?.Length ?? 0) == 0)
      return;

    init_exec.vm = this;
    init_exec.status = BHS.SUCCESS;
    init_exec.ip = 0;
    ref var init_frame = ref init_exec.PushFrame();
    init_frame.InitForModuleInit(module);
    //NOTE: here's the trick, init frame operates on global vars instead of locals
    init_exec.stack = module.gvars;
    //NOTE: need to setup the temporary stack offset
    init_exec.stack.sp = module.local_gvars_num;
    init_frame.locals = init_exec.stack;
    init_frame.locals_offset = 0;
    init_exec.PushRegion(0, 0, module.compiled.initcode.Length - 1);

    while(init_exec.regions_count > 0)
    {
      init_exec.ExecuteOnce();
      if(init_exec.status == BHS.RUNNING)
        throw new Exception("Invalid state in init mode: " + init_exec.status);
    }
    --init_exec.frames_count;
    if(init_exec.frames_count > 0)
      throw new Exception("Invalid amount of frames in init mode: " + init_exec.frames_count);
  }

  void ExecModuleInitFunc(Module module)
  {
    if(module.compiled.init_func_idx == -1)
      return;

    var fs = (FuncSymbolScript)module.ns.members[module.compiled.init_func_idx];
    Execute(fs);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static internal void CallFrame(ExecState exec, ref Frame frame, int frame_idx)
  {
    //let's remember ip to return to
    frame.return_ip = exec.ip;
    frame.regions_mark = exec.regions_count;
    exec.PushRegion(frame_idx);
    //since ip will be incremented below we decrement it intentionally here
    exec.ip = frame.start_ip - 1;
  }

  //NOTE: returns whether further execution should be stopped and status returned immediately (e.g in case of RUNNING or FAILURE)
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static bool CallNative(ExecState exec, FuncSymbolNative native, uint args_bits)
  {
    var new_coroutine = native.cb(exec, new FuncArgsInfo(args_bits));

    if(new_coroutine != null)
    {
      //NOTE: since there's a new coroutine we want to skip ip incrementing
      //      which happens below and proceed right to the execution of
      //      the new coroutine
      exec.coroutine = new_coroutine;
      return true;
    }
    else if(exec.status != BHS.SUCCESS)
      return true;
    else
      return false;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void ExecuteCoroutine(ref Region region, ExecState exec)
  {
    exec.status = BHS.SUCCESS;
    //NOTE: optimistically stepping forward so that for simple
    //      bindings you won't forget to do it
    ++exec.ip;
    exec.coroutine.Tick(exec);

    if(exec.status == BHS.RUNNING)
    {
      --exec.ip;
    }
    else if(exec.status == BHS.FAILURE)
    {
      CoroutinePool.Del(exec, exec.coroutine);
      exec.coroutine = null;

      exec.ip = EXIT_FRAME_IP - 1;

      if(region.defers != null && region.defers.count > 0)
        region.defers.ExitScope(exec);
      --exec.regions_count;
    }
    else if(exec.status == BHS.SUCCESS)
    {
      CoroutinePool.Del(exec, exec.coroutine);
      exec.coroutine = null;
    }
    else
      throw new Exception("Bad status: " + exec.status);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal static void InitDefaultVal(IType type, ref Val v)
  {
    v = default;
    if(type is IMarshallableGeneric img)
      v.type_id = img.ClassId();
    // TODO
    // if(type == Types.String)
    //   v.obj = "";
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ValStack Execute(FuncSymbolScript fs)
  {
    var vals = new StackList<Val>();
    return Execute(fs, new FuncArgsInfo(0u), ref vals);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ValStack Execute(FuncSymbolScript fs, StackList<Val> args)
  {
    return Execute(fs, new FuncArgsInfo(args.Count), ref args);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ValStack Execute(FuncSymbolScript fs, Val arg1)
  {
    return Execute(fs, new FuncArgsInfo(1), arg1);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ValStack Execute(FuncSymbolScript fs, FuncArgsInfo args_info, StackList<Val> args)
  {
    return Execute(fs, args_info, ref args);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ValStack Execute(FuncSymbolScript fs, FuncArgsInfo args_info, ref StackList<Val> args)
  {
    if(++script_executor_idx == script_executors_count)
    {
      ref var tmp = ref script_executors.Push();
      tmp = new ExecState();
      ++script_executors_count;
    }
    var exec = script_executors.Values[script_executor_idx];
    var res = Execute(exec, fs, args_info, ref args);
    --script_executor_idx;
    return res;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ValStack Execute(FuncSymbolScript fs, FuncArgsInfo args_info, Val arg1)
  {
    if(++script_executor_idx == script_executors_count)
    {
      ref var tmp = ref script_executors.Push();
      tmp = new ExecState();
      ++script_executors_count;
    }
    var exec = script_executors.Values[script_executor_idx];
    var res = Execute(exec, fs, args_info, arg1);
    --script_executor_idx;
    return res;
  }

  ValStack Execute(ExecState exec, FuncSymbolScript fs, FuncArgsInfo args_info,  /*less copying*/ref StackList<Val> args)
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

  ValStack Execute(ExecState exec, FuncSymbolScript fs, FuncArgsInfo args_info, Val arg1)
  {
    exec.vm = this;

    var stack = exec.stack;

    //NOTE: let's clean the stack from previous any non popped results
    //      (we keep it around just in case someone forgot to pop it after successful execution)
    stack.ClearAndRelease();

    stack.Push(arg1);

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
