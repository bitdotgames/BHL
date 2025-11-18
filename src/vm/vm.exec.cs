using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace bhl
{

public enum UpvalMode
{
  STRONG = 0,
  COPY   = 1
}

public partial class VM
{
  public unsafe delegate void BytecodeHandler(
    VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes
  );

  static readonly BytecodeHandler[] op_handlers = new BytecodeHandler[(int)Opcodes.MAX];

  static VM()
  {
    InitOpcodeHandlers();
  }

  //NOTE: This class represents an active execution unit in VM.
  //      VM needs an active ExecState during Tick-ing.
  //
  //      ExecState contains stack of Frames and code regions,
  //      continuous Val stack
  //
  //      (Fiber contains an ExecState, each paral branch
  //      contains its own ExecState)
  public class ExecState
  {
    public BHS status = BHS.NONE;

    public VM vm;
    public Fiber fiber;

    internal int ip;
    internal Coroutine coroutine;

    internal Region[] regions;
    internal int regions_count = 0;

    public ValStack stack;

    public Frame[] frames;
    public int frames_count = 0;

    public ExecState(
      int regions_capacity = 64,
      int frames_capacity = 256,
      int stack_capacity = 512
      )
    {
      regions = new Region[regions_capacity];
      frames = new Frame[frames_capacity];
      stack = new ValStack(stack_capacity);
    }

    [MethodImpl (MethodImplOptions.AggressiveInlining)]
    public ref Frame PushFrame()
    {
      if(frames_count == frames.Length)
        Array.Resize(ref frames, frames_count << 1);

      return ref frames[frames_count++];
    }

    [MethodImpl (MethodImplOptions.AggressiveInlining)]
    public ref Region PushRegion(int frame_idx, int min_ip = -1, int max_ip = STOP_IP)
    {
      if(regions_count == regions.Length)
        Array.Resize(ref regions, regions_count << 1);

      ref var region = ref regions[regions_count++];
      region.frame_idx = frame_idx;
      region.min_ip = min_ip;
      region.max_ip = max_ip;
      return ref region;
    }

    static void TraverseCallStack(
      List<VM.Frame> calls,
      ExecState exec,
      ref ExecState deepest,
      ICoroutine coro,
      int frame_offset = 0
      )
    {
      if(exec != null)
      {
        for(int i = frame_offset; i < exec.frames_count; ++i)
          calls.Add(exec.frames[i]);

        deepest = exec;
      }

      if(coro is ParalBranchBlock bi)
        TraverseCallStack(calls, bi.exec, ref deepest, bi.exec.coroutine, 1);
      else if(coro is ParalBlock pi && pi.i < pi.branches.Count)
        TraverseCallStack(calls, null, ref deepest, pi.branches[pi.i], frame_offset);
      else if(coro is ParalAllBlock pai && pai.i < pai.branches.Count)
        TraverseCallStack(calls, null, ref deepest, pai.branches[pai.i], frame_offset);
    }

    public void GetStackTrace(List<VM.TraceItem> info)
    {
      var calls = new List<VM.Frame>();
      ExecState deepest = null;
      TraverseCallStack(calls, this, ref deepest, coroutine);

      for(int i = 0; i < calls.Count; ++i)
      {
        var frm = calls[i];

        var item = new TraceItem();

        //NOTE: information about frame ip is taken from the 'next' frame, however
        //      for the last frame we have a special case. In this case there's no
        //      'next' frame and we should consider using current ip
        if(i == calls.Count - 1)
        {
          item.ip = deepest.ip;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ExitFrames()
    {
      if(frames_count > 0)
      {
        if(coroutine != null)
        {
          CoroutinePool.Del(this, coroutine);
          coroutine = null;
        }

        for(int i = frames_count; i-- > 0;)
        {
          ref var frame = ref frames[i];

          for(int r = regions_count; r-- > frame.regions_mark;)
          {
            ref var tmp_region = ref regions[r];
            if(tmp_region.defers != null && tmp_region.defers.count > 0)
              tmp_region.defers.ExitScope(this);
          }
          regions_count = frame.regions_mark;
          --frames_count;
          frame.CleanLocals();
        }
        frames_count = 0;
      }
      regions_count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void PushFrameRegion(ref Frame frame, int frame_idx)
    {
      ip = frame.start_ip;
      frame.regions_mark = regions_count;
      PushRegion(frame_idx);
    }

    internal void PushFrameRegion(ref Frame frame, int frame_idx, StackList <Val> args)
    {
      //NOTE: since variables will be set as local variables
      //      we traverse them in natural order
      for(int i = 0; i < args.Count; ++i)
      {
        ref Val v = ref stack.Push();
        v = args[i];
      }

      PushFrameRegion(ref frame, frame_idx);
    }

    internal void PushFrameRegion(
      FuncSymbolNative fsn,
      ref Frame frame,
      int frame_idx,
      StackList <Val> args
    )
    {
      for(int i = args.Count; i-- > 0;)
      {
        ref Val v = ref stack.Push();
        v = args[i];
      }

      frame.return_vars_num = fsn.GetReturnedArgsNum();
      frame.locals_vars_num = 0;

      PushFrameRegion(ref frame, frame_idx);

      //passing args info as argument
      coroutine = fsn.cb(this, frame.args_info);
      //NOTE: before executing a coroutine VM will increment ip optimistically
      //      but we need it to remain at the same position so that it points at
      //      the fake return opcode
      if(coroutine != null)
        --ip;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Execute(int region_stop_idx = 0)
    {
      status = BHS.SUCCESS;

      while(regions_count > region_stop_idx && status == BHS.SUCCESS)
        ExecuteOnce();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal unsafe void ExecuteOnce()
    {
      ref var region = ref regions[regions_count - 1];
      ref var frame = ref frames[region.frame_idx];

      //1. if there's an active coroutine it has priority over simple 'code following' via ip
      if(coroutine != null)
      {
        ExecuteCoroutine(ref region, this);
      }
      //2. are we out of the current region?
      else if(ip < region.min_ip || ip > region.max_ip)
      {
        if(region.defers != null && region.defers.count > 0)
          region.defers.ExitScope(this);
        --regions_count;
      }
      //3. exit frame requested
      else if(ip == EXIT_FRAME_IP)
      {
        //exiting all regions which belong to the frame
        for(int i = regions_count; i-- > frame.regions_mark;)
        {
          ref var tmp_region = ref regions[i];
          if(tmp_region.defers != null && tmp_region.defers.count > 0)
            tmp_region.defers.ExitScope(this);
        }
        regions_count = frame.regions_mark;
        frame.CleanLocals();

        if(frame.return_vars_num > 0)
          frame.ReturnVars(stack);
        //stack pointer now at the last returned value
        stack.sp = frame.locals_offset + frame.return_vars_num;
        --frames_count;

        ip = frame.return_ip + 1;
      }
      else
      {
        var bc = frame.bytecode;
        var opcode = bc[ip];
        //NOTE: temporary casting for better debug info
        //var _opcode = (Opcodes)opcode;

        op_handlers[opcode](vm, this, ref region,  ref frame, bc);

        ++ip;
      }
    }
  }

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
    //TODO: make type responsible for default initialization
    //      of the value
    if(type == Types.Int)
      v.SetNum(0);
    else if(type == Types.Float)
      v.SetFlt(0);
    else if(type == Types.String)
      v.SetStr("");
    else if(type == Types.Bool)
      v.SetBool(false);
    else
      v.type = type;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeAdd(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];
    //TODO: add separate opcode Concat for strings
    if(l_operand.type == Types.String)
      l_operand.obj = (string)l_operand.obj + (string)r_operand.obj;
    else
      l_operand.num += r_operand.num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSub(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];
    l_operand.num -= r_operand.num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeDiv(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];
    l_operand.num /= r_operand.num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeMul(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];
    l_operand.num *= r_operand.num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeEqualLite(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    l_operand.type = Types.Bool;
    l_operand.num = r_operand.num == l_operand.num &&
                     (string)r_operand.obj  == (string)l_operand.obj? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeEqualNum(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    l_operand.type = Types.Bool;
    l_operand.num = r_operand.num == l_operand.num ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeEqual(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    var res = new Val { type = Types.Bool, num = l_operand.IsDataEqual(ref r_operand) ? 1 : 0 };
    if(r_operand._refc != null)
    {
      r_operand._refc.Release();
      r_operand._refc = null;
    }
    r_operand.obj = null;

    l_operand._refc?.Release();
    //TODO: do we really need to replace the whole struct? we can set only the required field
    l_operand = res;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeLT(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    l_operand = new Val { type = Types.Bool, num = l_operand.num < r_operand.num ? 1 : 0 };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeLTE(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    l_operand = new Val { type = Types.Bool, num = l_operand.num <= r_operand.num ? 1 : 0 };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGT(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    l_operand = new Val { type = Types.Bool, num = l_operand.num > r_operand.num ? 1 : 0 };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGTE(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    l_operand = new Val { type = Types.Bool, num = l_operand.num >= r_operand.num ? 1 : 0 };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeAnd(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    //resulting operand is Bool as well, so we don't replace it
    l_operand.num = l_operand.num == 1 && r_operand.num == 1 ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeOr(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    //resulting operand is Bool as well, so we don't replace it
    l_operand.num = l_operand.num == 1 || r_operand.num == 1 ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeBitAnd(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    //resulting operand is Int as well, so we don't replace it
    l_operand.num = (int)l_operand.num & (int)r_operand.num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeBitOr(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    //resulting operand is Int as well, so we don't replace it
    l_operand.num = (int)l_operand.num | (int)r_operand.num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeBitShr(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    //resulting operand is Int as well, so we don't replace it
    l_operand.num = (int)l_operand.num >> (int)r_operand.num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeBitShl(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    //resulting operand is Int as well, so we don't replace it
    l_operand.num = (int)l_operand.num << (int)r_operand.num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeMod(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    //resulting operand is Int as well, so we don't replace it
    l_operand.num %= r_operand.num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeUnaryNot(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val val = ref stack.vals[stack.sp - 1];
    //resulting operand is Bool as well, so we don't replace it
    val.num = val.num != 1 ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeUnaryNeg(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val val = ref stack.vals[stack.sp - 1];
    //resulting operand is Int as well, so we don't replace it
    val.num *= -1;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeUnaryBitNot(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val val = ref stack.vals[stack.sp - 1];
    //resulting operand is Int as well, so we don't replace it
    val.num = ~((int)val.num);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeConstant(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int const_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    var cn = frame.constants[const_idx];

    ref Val v = ref exec.stack.Push();
    //TODO: we might have specialized opcodes for different variable types?
    cn.FillVal(ref v);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeTypeCast(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int cast_type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    bool force_type = Bytecode.Decode8(bytes, ref exec.ip) == 1;

    var cast_type = frame.type_refs[cast_type_idx];

    ref var val = ref exec.stack.Peek();

    if(cast_type == Types.Int)
    {
      val._refc?.Release();
      val = Val.NewNum((long)val.num);
    }
    else if(cast_type == Types.String && val.type != Types.String)
    {
      val._refc?.Release();
      val = Val.NewStr(
        val.num.ToString(System.Globalization.CultureInfo.InvariantCulture)
        );
    }
    else
    {
      //NOTE: extra type check in case cast type is instantiable object (e.g class)
      if(val.obj != null && cast_type is IInstantiable && !Types.Is(val, cast_type))
        throw new Exception("Invalid type cast: type '" + val.type + "' can't be cast to '" + cast_type + "'");
      if(force_type)
        val.type = cast_type;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeTypeAs(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int cast_type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    bool force_type = Bytecode.Decode8(bytes, ref exec.ip) == 1;
    var as_type = frame.type_refs[cast_type_idx];

    ref var val = ref exec.stack.Peek();

    if(Types.Is(val, as_type))
    {
      if(force_type)
        val.type = as_type;
    }
    else
    {
      val._refc?.Release();
      val = Null;
    }

  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeTypeIs(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int cast_type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    var as_type = frame.type_refs[cast_type_idx];

    ref var val = ref exec.stack.Peek();
    var refc = val._refc;
    val = Types.Is(val, as_type);
    refc?.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeTypeof(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    var type = frame.type_refs[type_idx];

    exec.stack.Push(Val.NewObj(type, Types.Type));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeInc(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int var_idx = Bytecode.Decode8(bytes, ref exec.ip);
    ++frame.locals.vals[frame.locals_offset + var_idx].num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeDec(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int var_idx = Bytecode.Decode8(bytes, ref exec.ip);
    --frame.locals.vals[frame.locals_offset + var_idx].num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeArrIdx(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int idx = exec.stack.PopFast();
    ref var arr = ref exec.stack.Peek();

    var class_type = (ArrayTypeSymbol)arr.type;
    var res = class_type.ArrGetAt(arr, idx);

    arr._refc?.Release();
    //let's replace with the result
    arr = res;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeArrIdxW(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int idx = exec.stack.PopFast();
    exec.stack.Pop(out var arr);
    exec.stack.Pop(out var val);

    var class_type = (ArrayTypeSymbol)arr.type;
    class_type.ArrSetAt(arr, idx, val);

    val._refc?.Release();
    arr._refc?.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeArrAddInplace(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    //taking copy not ref since during Pop in binding operator stack will be cleared
    var self = exec.stack.vals[exec.stack.sp - 2];
    self._refc?.Retain();
    var class_type = (ArrayTypeSymbol)self.type;
    //NOTE: Add must be at 0 index
    ((FuncSymbolNative)class_type._all_members[0]).cb(exec, default);
    exec.stack.Push(self);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeMapIdx(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    exec.stack.Pop(out var key);
    exec.stack.Pop(out var map);

    var class_type = (MapTypeSymbol)map.type;
    class_type.MapTryGet(map, key, out var res);

    res._refc?.Retain();
    exec.stack.Push(res);

    key._refc?.Release();
    map._refc?.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeMapIdxW(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    exec.stack.Pop(out var key);
    exec.stack.Pop(out var map);
    exec.stack.Pop(out var val);

    var class_type = (MapTypeSymbol)map.type;
    class_type.MapSet(map, key, val);

    key._refc?.Release();
    val._refc?.Release();
    map._refc?.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeMapAddInplace(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    //taking copy not ref since during Pop in binding operator stack will be cleared
    var self = exec.stack.vals[exec.stack.sp - 3];
    self._refc?.Retain();
    var class_type = (MapTypeSymbol)self.type;
    //NOTE: Add must be at 0 index
    ((FuncSymbolNative)class_type._all_members[0]).cb(exec, default);
    exec.stack.Push(self);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetVar(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);

    ref Val new_val = ref exec.stack.Push();
    new_val = frame.locals.vals[frame.locals_offset + local_idx];
    new_val._refc?.Retain();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetVarNum(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);

    ref Val new_val = ref exec.stack.Push();
    new_val.num = frame.locals.vals[frame.locals_offset + local_idx].num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSetVar(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);

    //TODO: we copy the whole value (we can have specialized opcodes for numbers)

    exec.stack.Pop(out var new_val);
    ref var current = ref frame.locals.vals[frame.locals_offset + local_idx];
    current._refc?.Release();
    current = new_val;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSetVarNum(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);

    ref var new_val = ref exec.stack.vals[--exec.stack.sp];
    frame.locals.vals[frame.locals_offset + local_idx].num = new_val.num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeDeclVar(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);
    int type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);

    var type = frame.type_refs[type_idx];

    ref var curr = ref frame.locals.vals[frame.locals_offset + local_idx];
    //NOTE: handling case when variables are 're-declared' within the nested loop
    curr._refc?.Release();

    InitDefaultVal(type, ref curr);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeDeclRef(VM vm, ExecState exec, ref Region region, ref Frame frame,
    byte* bytes)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);

    ref var curr = ref frame.locals.vals[frame.locals_offset + local_idx];

    //replacing existing val with ValRef if it's not already a ValRef
    //(this a special case required e.g for loop variables)
    //TODO: we'd better check the type for robustness?
    if(curr._refc == null)
    {
      var new_val = new Val();
      var vr = ValRef.New(vm);
      //NOTE: we wrap an existing value
      vr.val = curr;
      new_val._refc = vr;
      curr = new_val;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSetRef(VM vm, ExecState exec, ref Region region, ref Frame frame,
    byte* bytes)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);

    exec.stack.Pop(out var new_val);

    ref var val_ref_holder = ref frame.locals.vals[frame.locals_offset + local_idx];
    var val_ref = (ValRef)val_ref_holder._refc;
    val_ref.val._refc?.Release();
    val_ref.val = new_val;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetRef(VM vm, ExecState exec, ref Region region, ref Frame frame,
    byte* bytes)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);

    ref var val_ref_holder = ref frame.locals.vals[frame.locals_offset + local_idx];
    var val_ref = (ValRef)val_ref_holder._refc;

    ref Val v = ref exec.stack.Push();
    v = val_ref.val;
    v._refc?.Retain();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetAttr(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int fld_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);

    ref var obj = ref exec.stack.Peek();
    var class_symb = (ClassSymbol)obj.type;

    var field_symb = (FieldSymbol)class_symb._all_members[fld_idx];
    var res = new Val();
    field_symb.getter(exec, obj, ref res, field_symb);

    res._refc?.Retain();
    obj._refc?.Release();
    //let's replace the value on the stack
    obj = res;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSetAttr(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int fld_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);

    exec.stack.Pop(out var obj);
    exec.stack.Pop(out var val);

    var class_symb = (ClassSymbol)obj.type;
    var field_symb = (FieldSymbol)class_symb._all_members[fld_idx];
    field_symb.setter(exec, ref obj, val, field_symb);

    val._refc?.Release();
    obj._refc?.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSetAttrInplace(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int fld_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);

    exec.stack.Pop(out var val);
    ref var obj = ref exec.stack.Peek();

    var class_symb = (ClassSymbol)obj.type;
    var field_symb = (FieldSymbol)class_symb._all_members[fld_idx];
    field_symb.setter(exec, ref obj, val, field_symb);

    val._refc?.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetGVar(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int var_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);

    ref var val_ref_holder = ref frame.module.gvars.vals[var_idx];
    var val_ref = (ValRef)val_ref_holder._refc;

    ref Val v = ref exec.stack.Push();
    v = val_ref.val;
    v._refc?.Retain();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSetGVar(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int var_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);

    exec.stack.Pop(out var new_val);

    ref var val_ref_holder = ref frame.module.gvars.vals[var_idx];
    var val_ref = (ValRef)val_ref_holder._refc;
    val_ref.val._refc?.Release();
    val_ref.val = new_val;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeNop(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeReturn(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    exec.ip = EXIT_FRAME_IP - 1;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetFuncLocalPtr(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int func_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);

    var func_symb = frame.module.func_index.index[func_idx];

    var ptr = FuncPtr.New(vm);
    ptr.Init(frame.module, func_symb._ip_addr);
    exec.stack.Push(Val.NewObj(ptr, func_symb.signature));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetFuncIpPtr(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int func_ip = (int)Bytecode.Decode24(bytes, ref exec.ip);

    var ptr = FuncPtr.New(vm);
    ptr.Init(frame.module, func_ip);
    exec.stack.Push(Val.NewObj(ptr, Types.Any /*TODO: should be a FuncPtr type*/));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetFuncPtr(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int import_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    int func_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);

    var func_mod = frame.module._imported[import_idx];
    var func_symb = func_mod.func_index.index[func_idx];

    var ptr = FuncPtr.New(vm);
    ptr.Init(func_mod, func_symb._ip_addr);
    exec.stack.Push(Val.NewObj(ptr, func_symb.signature));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetFuncNativePtr(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int import_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    int func_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);

    //NOTE: using convention where built-in global module is always at index 0
    //      and imported modules are at (import_idx + 1)
    var func_mod = import_idx == 0 ? vm.types.module : frame.module._imported[import_idx - 1];
    var nfunc_symb = func_mod.nfunc_index.index[func_idx];

    var ptr = FuncPtr.New(vm);
    ptr.Init(nfunc_symb);
    exec.stack.Push(Val.NewObj(ptr, nfunc_symb.signature));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallLocal(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int func_ip = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    var args_info = new FuncArgsInfo(args_bits);
    int new_frame_idx = exec.frames_count;
    ref var new_frame = ref exec.PushFrame();
    new_frame.args_info = args_info;
    new_frame.InitWithOrigin(frame, func_ip);
    CallFrame(exec, ref new_frame, new_frame_idx);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallGlobNative(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int func_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    var nfunc_symb = vm.types.module.nfunc_index[func_idx];

    if(CallNative(exec, nfunc_symb, args_bits))
    {
      //let's cancel ip incrementing
      --exec.ip;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallNative(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int import_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    int func_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    //NOTE: using convention where built-in global module is always at index 0
    //      and imported modules are at (import_idx + 1)
    var func_mod = import_idx == 0 ? vm.types.module : frame.module._imported[import_idx - 1];
    var nfunc_symb = func_mod.nfunc_index[func_idx];

    if(CallNative(exec, nfunc_symb, args_bits))
    {
      //let's cancel ip incrementing
      --exec.ip;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCall(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int import_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    int func_ip = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    var func_mod = frame.module._imported[import_idx];

    int new_frame_idx = exec.frames_count;
    ref var new_frame = ref exec.PushFrame();
    new_frame.args_info = new FuncArgsInfo(args_bits);
    new_frame.InitWithModule(func_mod, func_ip);
    CallFrame(exec, ref new_frame, new_frame_idx);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallMethod(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int func_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    var args_info = new FuncArgsInfo(args_bits);
    int self_idx = exec.stack.sp - args_info.CountArgs() - 1;
    ref var self = ref exec.stack.vals[self_idx];
    //NOTE: taking into account that 'this' is on the stack,
    //      args_bits doesn't include it we have to take it into
    //      account so that during EnterFrame local offsets are
    //      properly calculated
    --exec.stack.sp;

    var class_type = (ClassSymbolScript)self.type;
    var func_symb = (FuncSymbolScript)class_type._all_members[func_idx];

    int new_frame_idx = exec.frames_count;
    ref var new_frame = ref exec.PushFrame();
    new_frame.args_info = args_info;
    new_frame.InitWithModule(func_symb._module, func_symb._ip_addr);
    CallFrame(exec, ref new_frame, new_frame_idx);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallMethodNative(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int func_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
    int self_idx = exec.stack.sp - args_num - 1;
    ref var self = ref exec.stack.vals[self_idx];

    var class_type = (ClassSymbol)self.type;
    var func_symb = (FuncSymbolNative)class_type._all_members[func_idx];

    if(CallNative(exec, func_symb, args_bits))
    {
      //let's cancel ip incrementing
      --exec.ip;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallMethodVirt(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int virt_func_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    var args_info = new FuncArgsInfo(args_bits);
    int self_idx = exec.stack.sp - args_info.CountArgs() - 1;
    ref var self = ref exec.stack.vals[self_idx];
    //NOTE: taking into account that 'this' is on the stack,
    //      args_bits doesn't include it we have to take it into
    //      account so that during EnterFrame local offsets are
    //      properly calculated
    --exec.stack.sp;

    var class_type = (ClassSymbol)self.type;
    var func_symb = (FuncSymbolScript)class_type._vtable[virt_func_idx];

    int new_frame_idx = exec.frames_count;
    ref var new_frame = ref exec.PushFrame();
    new_frame.args_info = args_info;
    new_frame.InitWithModule(func_symb._module, func_symb._ip_addr);
    CallFrame(exec, ref new_frame, new_frame_idx);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallMethodIface(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int iface_func_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    int iface_type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    var args_info = new FuncArgsInfo(args_bits);
    int self_idx = exec.stack.sp - args_info.CountArgs() - 1;
    ref var self = ref exec.stack.vals[self_idx];
    //NOTE: taking into account that 'this' is on the stack,
    //      args_bits doesn't include it we have to take it into
    //      account so that during EnterFrame local offsets are
    //      properly calculated
    --exec.stack.sp;

    var iface_symb = (InterfaceSymbol)frame.type_refs[iface_type_idx];
    var class_type = (ClassSymbol)self.type;
    var func_symb = (FuncSymbolScript)class_type._itable[iface_symb][iface_func_idx];

    int new_frame_idx = exec.frames_count;
    ref var new_frame = ref exec.PushFrame();
    new_frame.args_info = args_info;
    new_frame.InitWithModule(func_symb._module, func_symb._ip_addr);
    CallFrame(exec, ref new_frame, new_frame_idx);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallMethodIfaceNative(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int iface_func_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    int iface_type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    var iface_symb = (InterfaceSymbol)frame.type_refs[iface_type_idx];
    var func_symb = (FuncSymbolNative)iface_symb.members[iface_func_idx];

    if(CallNative(exec, func_symb, args_bits))
    {
      //let's cancel ip incrementing
      --exec.ip;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallFuncPtr(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    exec.stack.Pop(out var val_ptr);
    var ptr = (FuncPtr)val_ptr.obj;

    //checking if it's a native call
    if(ptr.native != null)
    {
      bool return_status = CallNative(exec, ptr.native, args_bits);
      if(return_status)
      {
        //let's cancel ip incrementing
        --exec.ip;
      }
    }
    else
    {
      int new_frame_idx = exec.frames_count;
      ref var new_frame = ref exec.PushFrame();
      new_frame.args_info = new FuncArgsInfo(args_bits);
      ptr.InitFrame(exec, ref frame, ref new_frame);
      CallFrame(exec, ref new_frame, new_frame_idx);
    }
    ptr.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallFuncPtrInv(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
    int ptr_idx = exec.stack.sp - args_num - 1;
    var ptr = (FuncPtr)exec.stack.vals[ptr_idx].obj;

    if(args_num > 0)
    {
      //moving args up the stack, replacing ptr
      for(int i = 0; i < exec.stack.sp - ptr_idx - 1; ++i)
        exec.stack.vals[ptr_idx + i] = exec.stack.vals[ptr_idx + 1 + i];
      //alternative version
      //Array.Copy(
      //  exec.stack.vals,
      //  ptr_idx + 1,
      //  exec.stack.vals,
      //  ptr_idx,
      //  exec.stack.sp - ptr_idx - 1
      //);
      ref var tail = ref exec.stack.vals[--exec.stack.sp];
      tail.obj = null;
      tail._refc = null;
    }

    //checking if it's a native call
    if(ptr.native != null)
    {
      bool return_status = CallNative(exec, ptr.native, args_bits);
      if(return_status)
      {
        //let's cancel ip incrementing
        --exec.ip;
      }
    }
    else
    {
      int new_frame_idx = exec.frames_count;
      ref var new_frame = ref exec.PushFrame();
      new_frame.args_info = new FuncArgsInfo(args_bits);
      ptr.InitFrame(exec, ref frame, ref new_frame);
      CallFrame(exec, ref new_frame, new_frame_idx);
    }
    ptr.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeEnterFrame(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int locals_vars_num = Bytecode.Decode8(bytes, ref exec.ip);
    int return_vars_num = Bytecode.Decode8(bytes, ref exec.ip);

    frame.locals_vars_num = locals_vars_num;
    frame.return_vars_num = return_vars_num;

    //NOTE: it's assumed that refcounted args are pushed with refcounted values
    var stack = exec.stack;

    int args_num = frame.args_info.CountArgs();

    frame.locals_offset = stack.sp - args_num;
    frame.locals = stack;

    //let's reserve space for local variables, however passed variables are
    //already on the stack, let's take that into account
    int rest_local_vars_num = locals_vars_num - args_num;
    stack.Reserve(rest_local_vars_num);
    //temporary stack lives after local variables
    stack.sp += rest_local_vars_num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSetUpval(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int frame_local_idx = Bytecode.Decode8(bytes, ref exec.ip);
    int func_ptr_local_idx = Bytecode.Decode8(bytes, ref exec.ip);
    var mode = (UpvalMode)Bytecode.Decode8(bytes, ref exec.ip);

    var addr = (FuncPtr)exec.stack.vals[exec.stack.sp - 1].obj;

    ref var upval = ref addr.upvals.Push();
    upval.frame_local_idx = func_ptr_local_idx;

    ref var val = ref frame.locals.vals[frame.locals_offset + frame_local_idx];
    if(mode == UpvalMode.STRONG)
      val._refc?.Retain();
    upval.val = val;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodePop(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    ref var val = ref exec.stack.vals[--exec.stack.sp];
    if(val._refc != null)
    {
      val._refc.Release();
      val._refc = null;
    }
    val.obj = null;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeJump(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    short offset = (short)Bytecode.Decode16(bytes, ref exec.ip);
    exec.ip += offset;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeJumpZ(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int offset = (int)Bytecode.Decode16(bytes, ref exec.ip);

    ref Val v = ref exec.stack.vals[--exec.stack.sp];

    if(v.num == 0)
      exec.ip += offset;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeJumpPeekZ(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int offset = (int)Bytecode.Decode16(bytes, ref exec.ip);
    if(exec.stack.vals[exec.stack.sp - 1].num != 1)
      exec.ip += offset;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeJumpPeekNZ(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int offset = (int)Bytecode.Decode16(bytes, ref exec.ip);
    if(exec.stack.vals[exec.stack.sp - 1].num == 1)
      exec.ip += offset;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeDefArg(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    byte arg_idx = Bytecode.Decode8(bytes, ref exec.ip);
    byte def_arg_idx = Bytecode.Decode8(bytes, ref exec.ip);
    int jump_add = (int)Bytecode.Decode16(bytes, ref exec.ip);
    //if default argument is set we need to insert a slot into the stack
    //where it will be set below
    if(frame.args_info.IsDefaultArgUsed(def_arg_idx))
    {
      int args_num = frame.args_info.CountArgs();
      int pos_idx = frame.locals_offset + arg_idx;

      //we need to move only passed arguments and move them in reverse order
      //so that they don't overlap during 'movement'
      for(int i = args_num - pos_idx; i-- > 0;)
      {
        ref var tmp = ref exec.stack.vals[pos_idx + i];
        exec.stack.vals[pos_idx + i + 1] = tmp;
        //need to nullify the refcounted so it's not invoked somehow
        tmp.obj = null;
        tmp._refc = null;
      }

      //alternative version
      //Array.Copy(
      //  exec.stack.vals,
      //  pos_idx,
      //  exec.stack.vals,
      //  pos_idx + 1,
      //  args_num - pos_idx
      //);
    }
    //...otherwise we need to jump out of default argument calculation code
    else
      exec.ip += jump_add;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeScope(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int size = (int)Bytecode.Decode16(bytes, ref exec.ip);

    exec.PushRegion(region.frame_idx, exec.ip + 1, exec.ip + size);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeParal(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int size = (int)Bytecode.Decode16(bytes, ref exec.ip);

    var paral = CoroutinePool.New<ParalBlock>(vm);
    paral.Init(exec.ip + 1, exec.ip + size);

    int tmp_ip = exec.ip;
    while(tmp_ip < (exec.ip + size))
    {
      ++tmp_ip;
      FetchBlock(
        ref tmp_ip,
        exec, bytes,
        paral.branches,
        paral.defers,
        out var tmp_size
      );
      tmp_ip += tmp_size;
    }

    //NOTE: since there's a new coroutine we want to skip ip incrementing
    //      which happens below and proceed right to the execution of
    //      the new coroutine
    exec.coroutine = paral;
    //let's cancel ip incrementing
    --exec.ip;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeParalAll(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int size = (int)Bytecode.Decode16(bytes, ref exec.ip);

    var paral = CoroutinePool.New<ParalAllBlock>(vm);
    paral.Init(exec.ip + 1, exec.ip + size);

    int tmp_ip = exec.ip;
    while(tmp_ip < (exec.ip + size))
    {
      ++tmp_ip;
      FetchBlock(
        ref tmp_ip,
        exec, bytes,
        paral.branches,
        paral.defers,
        out var tmp_size
      );

      tmp_ip += tmp_size;
    }

    //NOTE: since there's a new coroutine we want to skip ip incrementing
    //      which happens below and proceed right to the execution of
    //      the new coroutine
    exec.coroutine = paral;
    //let's cancel ip incrementing
    --exec.ip;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static unsafe void FetchBlock(
    ref int ip,
    ExecState exec,
    byte* bytes,
    List<Coroutine> branches,
    VM.DeferSupport defers,
    out int size
  )
  {
    var type = (Opcodes)bytes[ip];
    size = (int)Bytecode.Decode16(bytes, ref ip);

    if(type == Opcodes.Scope)
    {
      var br = CoroutinePool.New<ParalBranchBlock>(exec.vm);
      br.Init(exec, ip + 1, ip + size);
      branches.Add(br);
    }
    else if(type == Opcodes.Paral)
    {
      var paral = CoroutinePool.New<ParalBlock>(exec.vm);
      paral.Init(ip + 1, ip + size);
      branches.Add(paral);
    }
    else if(type == Opcodes.ParalAll)
    {
      var paral = CoroutinePool.New<ParalAllBlock>(exec.vm);
      paral.Init(ip + 1, ip + size);
      branches.Add(paral);
    }
    else if(type == Opcodes.Defer)
    {
      ref var d = ref defers.Add();
      d.ip = ip + 1;
      d.max_ip = ip + size;
    }
    else
      throw new Exception("Not supported block type: " + type);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeDefer(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int size = (int)Bytecode.Decode16(bytes, ref exec.ip);

    region.defers ??= new DeferSupport();

    ref var d = ref region.defers.Add();
    d.ip = exec.ip + 1;
    d.max_ip = exec.ip + size;

    //NOTE: we need to skip the defer block
    exec.ip += size;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeNew(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    var type = frame.type_refs[type_idx];

    if(type is not ClassSymbol cls)
      throw new Exception("Not a class symbol: " + type);

    //NOTE: we don't increment refcounted here since the new instance
    //      is not attached to any variable and is expected to have refs == 1
    ref var val = ref exec.stack.Push();
    cls.creator(exec, ref val, cls);
  }

  unsafe static void InitOpcodeHandlers()
  {
    //binary ops
    op_handlers[(int)Opcodes.Add] = OpcodeAdd;
    op_handlers[(int)Opcodes.Sub] = OpcodeSub;
    op_handlers[(int)Opcodes.Div] = OpcodeDiv;
    op_handlers[(int)Opcodes.Mul] = OpcodeMul;
    op_handlers[(int)Opcodes.EqualLite] = OpcodeEqualLite;
    op_handlers[(int)Opcodes.Equal] = OpcodeEqual;
    op_handlers[(int)Opcodes.LT] = OpcodeLT;
    op_handlers[(int)Opcodes.LTE] = OpcodeLTE;
    op_handlers[(int)Opcodes.GT] = OpcodeGT;
    op_handlers[(int)Opcodes.GTE] = OpcodeGTE;
    op_handlers[(int)Opcodes.And] = OpcodeAnd;
    op_handlers[(int)Opcodes.Or] = OpcodeOr;
    op_handlers[(int)Opcodes.BitAnd] = OpcodeBitAnd;
    op_handlers[(int)Opcodes.BitOr] = OpcodeBitOr;
    op_handlers[(int)Opcodes.BitShr] = OpcodeBitShr;
    op_handlers[(int)Opcodes.BitShl] = OpcodeBitShl;
    op_handlers[(int)Opcodes.Mod] = OpcodeMod;
    //unary ops
    op_handlers[(int)Opcodes.UnaryNot] = OpcodeUnaryNot;
    op_handlers[(int)Opcodes.UnaryNeg] = OpcodeUnaryNeg;
    op_handlers[(int)Opcodes.UnaryBitNot] = OpcodeUnaryBitNot;

    op_handlers[(int)Opcodes.Constant] = OpcodeConstant;

    op_handlers[(int)Opcodes.TypeCast] = OpcodeTypeCast;
    op_handlers[(int)Opcodes.TypeAs] = OpcodeTypeAs;
    op_handlers[(int)Opcodes.TypeIs] = OpcodeTypeIs;
    op_handlers[(int)Opcodes.Typeof] = OpcodeTypeof;

    //NOTE: Of these 2 only Opcode.Inc is used for incrementing a hidden
    //      variable in foreach loop. Opcode.Dec is not used anywhere.
    //      Maybe they could be used for actual operators for some restricted cases.
    op_handlers[(int)Opcodes.Inc] = OpcodeInc;
    op_handlers[(int)Opcodes.Dec] = OpcodeDec;

    op_handlers[(int)Opcodes.ArrIdx] = OpcodeArrIdx;
    op_handlers[(int)Opcodes.ArrIdxW] = OpcodeArrIdxW;
    op_handlers[(int)Opcodes.ArrAddInplace] = OpcodeArrAddInplace;

    op_handlers[(int)Opcodes.MapIdx] = OpcodeMapIdx;
    op_handlers[(int)Opcodes.MapIdxW] = OpcodeMapIdxW;
    op_handlers[(int)Opcodes.MapAddInplace] = OpcodeMapAddInplace;

    op_handlers[(int)Opcodes.GetVar] = OpcodeGetVar;
    op_handlers[(int)Opcodes.SetVar] = OpcodeSetVar;
    op_handlers[(int)Opcodes.DeclVar] = OpcodeDeclVar;

    op_handlers[(int)Opcodes.DeclRef] = OpcodeDeclRef;
    op_handlers[(int)Opcodes.SetRef] = OpcodeSetRef;
    op_handlers[(int)Opcodes.GetRef] = OpcodeGetRef;

    op_handlers[(int)Opcodes.GetAttr] = OpcodeGetAttr;
    op_handlers[(int)Opcodes.SetAttr] = OpcodeSetAttr;
    op_handlers[(int)Opcodes.SetAttrInplace] = OpcodeSetAttrInplace;

    op_handlers[(int)Opcodes.GetGVar] = OpcodeGetGVar;
    op_handlers[(int)Opcodes.SetGVar] = OpcodeSetGVar;

    op_handlers[(int)Opcodes.Nop] = OpcodeNop;

    op_handlers[(int)Opcodes.Return] = OpcodeReturn;

    op_handlers[(int)Opcodes.GetFuncLocalPtr] = OpcodeGetFuncLocalPtr;
    op_handlers[(int)Opcodes.GetFuncPtr] = OpcodeGetFuncPtr;
    op_handlers[(int)Opcodes.GetFuncNativePtr] = OpcodeGetFuncNativePtr;
    op_handlers[(int)Opcodes.GetFuncIpPtr] = OpcodeGetFuncIpPtr;

    op_handlers[(int)Opcodes.CallLocal] = OpcodeCallLocal;
    op_handlers[(int)Opcodes.CallGlobNative] = OpcodeCallGlobNative;
    op_handlers[(int)Opcodes.CallNative] = OpcodeCallNative;
    op_handlers[(int)Opcodes.Call] = OpcodeCall;
    op_handlers[(int)Opcodes.CallMethod] = OpcodeCallMethod;
    op_handlers[(int)Opcodes.CallMethodNative] = OpcodeCallMethodNative;
    op_handlers[(int)Opcodes.CallMethodVirt] = OpcodeCallMethodVirt;
    op_handlers[(int)Opcodes.CallMethodIface] = OpcodeCallMethodIface;
    op_handlers[(int)Opcodes.CallMethodIfaceNative] = OpcodeCallMethodIfaceNative;
    op_handlers[(int)Opcodes.CallFuncPtr] = OpcodeCallFuncPtr;
    op_handlers[(int)Opcodes.CallFuncPtrInv] = OpcodeCallFuncPtrInv;

    op_handlers[(int)Opcodes.Frame] = OpcodeEnterFrame;

    op_handlers[(int)Opcodes.SetUpval] = OpcodeSetUpval;

    op_handlers[(int)Opcodes.Pop] = OpcodePop;
    op_handlers[(int)Opcodes.Jump] = OpcodeJump;
    op_handlers[(int)Opcodes.JumpZ] = OpcodeJumpZ;
    op_handlers[(int)Opcodes.JumpPeekZ] = OpcodeJumpPeekZ;
    op_handlers[(int)Opcodes.JumpPeekNZ] = OpcodeJumpPeekNZ;

    op_handlers[(int)Opcodes.DefArg] = OpcodeDefArg;

    op_handlers[(int)Opcodes.Defer] = OpcodeDefer;
    op_handlers[(int)Opcodes.Scope] = OpcodeScope;
    op_handlers[(int)Opcodes.Paral] = OpcodeParal;
    op_handlers[(int)Opcodes.ParalAll] = OpcodeParalAll;

    op_handlers[(int)Opcodes.New] = OpcodeNew;
  }
}

}
