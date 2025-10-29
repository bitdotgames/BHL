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

public partial class VM : INamedResolver
{
  public unsafe delegate void BytecodeHandler(
    VM vm, ExecState exec, ref Region region,
    FrameOld frame_old, ref Frame frame, byte* bytes
  );

  static readonly BytecodeHandler[] op_handlers = new BytecodeHandler[(int)Opcodes.MAX];

  static VM()
  {
    InitOpcodeHandlers();
  }

  //NOTE: why -2? we reserve some space before int.MaxValue so that
  //      increasing some ip couple of times after it was assigned
  //      a 'STOP_IP' value won't overflow int.MaxValue
  public const int STOP_IP = int.MaxValue - 2;
  public const int EXIT_FRAME_IP = STOP_IP - 1;

  public delegate void ClassCreator(VM vm, ref Val res, IType type);

  public struct Region
  {
    public int frame_idx;

    public List<DeferBlock> defer_support;

    //NOTE: if current ip is not within *inclusive* range of these values
    //      the frame context execution is considered to be done
    public int min_ip;
    public int max_ip;

    [MethodImpl (MethodImplOptions.AggressiveInlining)]
    public Region(
      int frame_idx,
      List<DeferBlock> defer_support,
      int min_ip = -1,
      int max_ip = STOP_IP
      )
    {
      this.frame_idx = frame_idx;
      this.defer_support = defer_support;
      this.min_ip = min_ip;
      this.max_ip = max_ip;
    }
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
    public BHS status = BHS.SUCCESS;

    internal int ip;
    internal Coroutine coroutine;

    internal Region[] regions = new Region[32];
    internal int regions_count = 0;

    public ValStack stack = new ValStack();
    //TODO: why is it here?
    public Const[] constants = new Const[16];

    public Frame[] frames = new Frame[256];
    public int frames_count = 0;

    internal FixedStack<FrameOld> frames_old = new FixedStack<FrameOld>(256);
    public ValOldStack stack_old;

    [MethodImpl (MethodImplOptions.AggressiveInlining)]
    public ref Frame PushFrame()
    {
      if(frames_count == frames.Length)
        Array.Resize(ref frames, frames_count << 1);

      return ref frames[frames_count++];
    }

    [MethodImpl (MethodImplOptions.AggressiveInlining)]
    public ref Frame PopFrame()
    {
      return ref frames[frames_count--];
    }

    public delegate void LocalFunc(
      VM vm, ExecState exec, ref Region region,
      ref Frame frame, ref BHS status
    );
    public LocalFunc[] funcs2 = new LocalFunc[16];
  }

  //fake frame used for module's init code
  FrameOld init_frame;
  ExecState init_exec = new ExecState();

  //special case 'null' value
  ValOld null_val = null;
  public ValOld NullOld
  {
    get
    {
      null_val.Retain();
      return null_val;
    }
  }

  ValOld true_val = null;
  public ValOld TrueOld
  {
    get
    {
      true_val.Retain();
      return true_val;
    }
  }

  ValOld false_val = null;
  public ValOld FalseOld
  {
    get
    {
      false_val.Retain();
      return false_val;
    }
  }

  public static readonly Val Null = Val.NewObj(null, Types.Null);
  public static readonly Val True = Val.NewBool(true);
  public static readonly Val False = Val.NewBool(false);


  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal void Execute(ExecState exec, int exec_waterline_idx = 0)
  {
    exec.status = BHS.SUCCESS;

    while(exec.regions_count > exec_waterline_idx && exec.status == BHS.SUCCESS)
      ExecuteOnce(exec);
  }

  unsafe void ExecuteOnce(ExecState exec)
  {
    ref var region = ref exec.regions[exec.regions_count - 1];
    //TODO: looks like frame_idx is not really needed since we always need the top frame?
    ref var frame = ref exec.frames[region.frame_idx];

#if BHL_DEBUG
    Console.WriteLine("EXEC TICK " + curr_frame.fb.tick + " " + exec.GetHashCode() + ":" + exec.regions.Count + ":" + exec.frames.Count + " (" + curr_frame.GetHashCode() + "," + curr_frame.fb.id + ") IP " + exec.ip + "(min:" + item.min_ip + ", max:" + item.max_ip + ")" + (exec.ip > -1 && exec.ip < curr_frame.bytecode.Length ? " OP " + (Opcodes)curr_frame.bytecode[exec.ip] : " OP ? ") + " CORO " + exec.coroutine?.GetType().Name + "(" + exec.coroutine?.GetHashCode() + ")" + " DEFERABLE " + item.defer_support?.GetType().Name + "(" + item.defer_support?.GetHashCode() + ") " + curr_frame.bytecode.Length /* + " " + curr_frame.fb.GetStackTrace()*/ /* + " " + Environment.StackTrace*/);
#endif

    //1. if there's an active coroutine it has priority over simple 'code following' via ip
    if(exec.coroutine != null)
    {
      ExecuteCoroutine(null, exec);
    }
    //2. are we out of the current region?
    else if(exec.ip < region.min_ip || exec.ip > region.max_ip)
    {
      --exec.regions_count;
    }
    //3. exit frame requested
    else if(exec.ip == EXIT_FRAME_IP)
    {
      //for defers
      //frame.ExitScope(null, exec);

      --exec.regions_count;
      exec.ip = frame.return_ip + 1;

      frame.Exit(exec.stack);
      --exec.frames_count;
    }
    else
    {
      var bc = frame.bytecode;
      var opcode = bc[exec.ip];

      op_handlers[opcode](this, exec, ref region, null, ref frame, bc);

      ++exec.ip;
    }
  }

  void ExecInitCode(Module module)
  {
    var bytecode = module.compiled.initcode;
    if(bytecode == null || bytecode.Length == 0)
      return;

    init_exec.status = BHS.SUCCESS;
    init_exec.ip = 0;
    init_exec.stack_old = init_frame.stack;
    init_frame.Init(null, null, module, module.compiled.constants, module.compiled.type_refs_resolved, bytecode, 0);
    init_exec.regions[init_exec.regions_count++] =
      new VM.Region(-1, null, 0, bytecode.Length - 1);
    //NOTE: here's the trick, init frame operates on global vars instead of locals
    init_frame.locals = init_frame.module.gvar_vals;

    while(init_exec.regions_count > 0)
    {
      ExecuteOnce(init_exec);
      if(init_exec.status == BHS.RUNNING)
        throw new Exception("Invalid state in init mode: " + init_exec.status);
    }
  }

  void ExecModuleInitFunc(Module module)
  {
    if(module.compiled.init_func_idx == -1)
      return;

    var fs = (FuncSymbolScript)module.ns.members[module.compiled.init_func_idx];
    var addr = new FuncAddr()
    {
      module = module,
      fs = fs,
      ip = fs.ip_addr
    };
    var fb = StartOld(addr, new FuncArgsInfo(0), default, FiberOptions.Detach);
    if(Tick(fb))
      throw new Exception("Module '" + module.name + "' init function is still running");
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  Coroutine ProcBlockOpcode(
    ExecState exec,
    FrameOld curr_frame,
    List<DeferBlock> defer_support
  )
  {
    var (block_coro, block_paral_branches, block_defer_support) = _ProcBlockOpcode(
      ref exec.ip,
      curr_frame, exec,
      out var block_size,
      defer_support,
      false
    );

    //NOTE: let's process paral block (add branches and defers)
    if(block_paral_branches != null)
    {
      int tmp_ip = exec.ip;
      while(tmp_ip < (exec.ip + block_size))
      {
        ++tmp_ip;

        var (branch_coro, _, _) = _ProcBlockOpcode(
          ref tmp_ip,
          curr_frame,
          exec,
          out var tmp_size,
          block_defer_support,
          true
        );

        if(branch_coro != null)
        {
          block_paral_branches.Add(branch_coro);
          tmp_ip += tmp_size;
        }
      }
    }

    return block_coro;
  }

  (Coroutine, List<Coroutine>, List<DeferBlock>) _ProcBlockOpcode(
    ref int ip,
    FrameOld curr_frame,
    ExecState exec,
    out int size,
    List<DeferBlock> defer_support,
    bool is_paral
  )
  {
    var type = (BlockType)Bytecode.Decode8(curr_frame.bytecode, ref ip);
    size = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);

    if(type == BlockType.SEQ)
    {
      if(is_paral)
      {
        var br = CoroutinePool.New<ParalBranchBlock>(this);
        br.Init(curr_frame, ip + 1, ip + size);
        return (br, null, br.defers);
      }
      else
      {
        var seq = CoroutinePool.New<SeqBlock>(this);
        seq.Init(curr_frame, exec.stack_old, ip + 1, ip + size);
        return (seq, null, seq.defers);
      }
    }
    else if(type == BlockType.PARAL)
    {
      var paral = CoroutinePool.New<ParalBlock>(this);
      paral.Init(ip + 1, ip + size);
      return (paral, paral.branches, paral.defers);
    }
    else if(type == BlockType.PARAL_ALL)
    {
      var paral = CoroutinePool.New<ParalAllBlock>(this);
      paral.Init(ip + 1, ip + size);
      return (paral, paral.branches, paral.defers);
    }
    else if(type == BlockType.DEFER)
    {
      var d = new DeferBlock(curr_frame, ip + 1, ip + size);
      defer_support.Add(d);
      //NOTE: we need to skip the defer block
      ip += size;
      return (null, null, null);
    }
    else
      throw new Exception("Not supported block type: " + type);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void Call(ExecState exec, FrameOld new_frame, uint args_bits)
  {
    int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
    for(int i = 0; i < args_num; ++i)
      new_frame.stack.Push(exec.stack_old.Pop());
    new_frame.stack.Push(ValOld.NewInt(this, args_bits));

    //let's remember ip to return to
    new_frame.return_ip = exec.ip;
    exec.stack_old = new_frame.stack;
    exec.frames_old.Push(new_frame);
    exec.regions[exec.regions_count++] = new Region(-1, new_frame.defers);
    //since ip will be incremented below we decrement it intentionally here
    exec.ip = new_frame.start_ip - 1;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void Call(ExecState exec, ref Frame frame, int frame_idx, uint args_bits)
  {
    var stack = exec.stack;

    //it's assumed passed values are already on the stack
    //and we need to put on the top of the stack args_bits
    ref Val v = ref stack.Push();
    v.type = Types.Int;
    v._num = args_bits;

    //let's remember ip to return to
    frame.return_ip = exec.ip;
    exec.regions[exec.regions_count++] = new Region(frame_idx, null);
    //since ip will be incremented below we decrement it intentionally here
    exec.ip = frame.start_ip - 1;
  }

  //NOTE: returns whether further execution should be stopped and status returned immediately (e.g in case of RUNNING or FAILURE)
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static bool CallNative(FrameOld curr_frame, ValOldStack curr_stack, FuncSymbolNative native, uint args_bits, out BHS status,
    ref Coroutine coroutine)
  {
    status = BHS.SUCCESS;
    //var new_coroutine = native.cb(curr_frame, curr_stack, new FuncArgsInfo(args_bits), ref status);

    //if(new_coroutine != null)
    //{
    //  //NOTE: since there's a new coroutine we want to skip ip incrementing
    //  //      which happens below and proceed right to the execution of
    //  //      the new coroutine
    //  coroutine = new_coroutine;
    //  return true;
    //}
    //else if(status != BHS.SUCCESS)
    //  return true;
    //else
      return false;
  }

  //NOTE: returns whether further execution should be stopped and status returned immediately (e.g in case of RUNNING or FAILURE)
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static bool CallNative(VM vm, ExecState exec, FuncSymbolNative native, uint args_bits)
  {
    var new_coroutine = native.cb(vm, exec, new FuncArgsInfo(args_bits));

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
  static void ExecuteCoroutine(FrameOld curr_frame, ExecState exec)
  {
    exec.status = BHS.SUCCESS;
    //NOTE: optimistically stepping forward so that for simple
    //      bindings you won't forget to do it
    ++exec.ip;
    exec.coroutine.Tick(curr_frame, exec);

    if(exec.status == BHS.RUNNING)
    {
      --exec.ip;
    }
    else if(exec.status == BHS.FAILURE)
    {
      CoroutinePool.DelOld(curr_frame, exec, exec.coroutine);
      exec.coroutine = null;

      exec.ip = EXIT_FRAME_IP - 1;
      --exec.regions_count;
    }
    else if(exec.status == BHS.SUCCESS)
    {
      CoroutinePool.DelOld(curr_frame, exec, exec.coroutine);
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
  unsafe static void _OpcodeAdd(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack_old;
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    //TODO: add Opcodes.Concat?
    if((r_operand.type == Types.String) && (l_operand.type == Types.String))
      stack.Push(ValOld.NewStr(vm, (string)l_operand._obj + (string)r_operand._obj));
    else
      stack.Push(ValOld.NewFlt(vm, l_operand._num + r_operand._num));

    r_operand.Release();
    l_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeAdd(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];
    //TODO: add separate opcode Concat for strings
    if(l_operand.type == Types.String)
      l_operand._obj = (string)l_operand._obj + (string)r_operand._obj;
    else
      l_operand._num += r_operand._num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSub(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];
    l_operand._num -= r_operand._num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeDiv(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];
    l_operand._num /= r_operand._num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeMul(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];
    l_operand._num *= r_operand._num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeEqual(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    l_operand.type = Types.Bool;
    l_operand._num = l_operand.IsValueEqual(ref r_operand) ? 1 : 0;

    //TODO: specialized opcode for simple numbers equality?
    //l_operand._num = l_operand._num == r_operand._num ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeNotEqual(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    l_operand.type = Types.Bool;
    l_operand._num = !l_operand.IsValueEqual(ref r_operand) ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeLT(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    l_operand.type = Types.Bool;
    l_operand._num = l_operand._num < r_operand._num ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeLTE(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    l_operand.type = Types.Bool;
    l_operand._num = l_operand._num <= r_operand._num ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGT(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    l_operand.type = Types.Bool;
    l_operand._num = l_operand._num > r_operand._num ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGTE(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    l_operand.type = Types.Bool;
    l_operand._num = l_operand._num >= r_operand._num ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeAnd(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    //l_operand.type = Types.Bool;
    l_operand._num = l_operand._num == 1 && r_operand._num == 1 ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeOr(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    //l_operand.type = Types.Bool;
    l_operand._num = l_operand._num == 1 || r_operand._num == 1 ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeBitAnd(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    //l_operand.type = Types.Int;
    l_operand._num = (int)l_operand._num & (int)r_operand._num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeBitOr(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    //l_operand.type = Types.Int;
    l_operand._num = (int)l_operand._num | (int)r_operand._num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeBitShr(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    l_operand._num = (int)l_operand._num >> (int)r_operand._num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeBitShl(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    l_operand._num = (int)l_operand._num << (int)r_operand._num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeMod(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    l_operand._num %= r_operand._num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void _OpcodeUnaryNot(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    //var stack = exec.stack_old;
    //var operand = stack.PopRelease().num;
    //stack.Push(ValOld.NewBool(vm, operand != 1));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeUnaryNot(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val val = ref stack.vals[stack.sp - 1];
    val._num = val._num != 1 ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeUnaryNeg(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val val = ref stack.vals[stack.sp - 1];
    val._num *= -1;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeUnaryBitNot(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val val = ref stack.vals[stack.sp - 1];
    val._num = ~((int)val._num);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeConstant(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int const_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    var cn = frame.constants[const_idx];

    ref Val v = ref exec.stack.Push();
    //TODO: we might have specialized opcodes for different variable types
    cn.FillVal(ref v);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeTypeCast(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int cast_type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    bool force_type = Bytecode.Decode8(bytes, ref exec.ip) == 1;

    var cast_type = frame.type_refs[cast_type_idx];

    //TODO: make it more universal and robust
    var new_val = new Val();
    ref var val = ref exec.stack.Pop();

    //TODO: can we do that 'inplace'?
    if(cast_type == Types.Int)
      new_val.SetNum((long)val._num);
    else if(cast_type == Types.String && val.type != Types.String)
      new_val.SetStr(val.num.ToString(System.Globalization.CultureInfo.InvariantCulture));
    else
    {
      //NOTE: extra type check in case cast type is instantiable object (e.g class)
      if(val._obj != null && cast_type is IInstantiable && !Types.Is(val, cast_type))
        throw new Exception("Invalid type cast: type '" + val.type + "' can't be cast to '" + cast_type + "'");
      new_val.ValueCopyFrom(val);
      if(force_type)
        new_val.type = cast_type;
      new_val._refc?.Retain();
    }

    //val.Release();

    exec.stack.Push(new_val);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeTypeAs(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int cast_type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    bool force_type = Bytecode.Decode8(bytes, ref exec.ip) == 1;
    var as_type = curr_frame.type_refs[cast_type_idx];

    //TODO: in case of ref we can simply replace this value
    var val = exec.stack.vals[exec.stack.sp - 1];

    if(Types.Is(val, as_type))
    {
      var new_val = new Val();
      new_val.ValueCopyFrom(val);
      if(force_type)
        new_val.type = as_type;
      new_val._refc?.Retain();
      exec.stack.vals[exec.stack.sp - 1] = new_val;
    }
    else
      exec.stack.vals[exec.stack.sp - 1] = Null;

    //TODO: find out whether we actually need it
    val.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeTypeIs(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int cast_type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    var as_type = frame.type_refs[cast_type_idx];

    var val = exec.stack.Pop();
    exec.stack.Push(Types.Is(val, as_type));
    val.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeTypeof(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    var type = curr_frame.type_refs[type_idx];

    exec.stack_old.Push(ValOld.NewObj(vm, type, Types.Type));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeInc(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int var_idx = Bytecode.Decode8(bytes, ref exec.ip);
    ++exec.stack.vals[frame.locals_offset + var_idx]._num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeDec(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int var_idx = Bytecode.Decode8(bytes, ref exec.ip);
    --exec.stack.vals[frame.locals_offset + var_idx]._num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeArrIdx(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    ref var self = ref exec.stack.vals[exec.stack.sp - 2];
    var class_type = (ArrayTypeSymbol)self.type;

    int idx = exec.stack.Pop();
    var arr = exec.stack.Pop();

    var res = class_type.ArrGetAt(arr, idx);

    exec.stack.Push(res);
    arr.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeArrIdxW(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    ref var self = ref exec.stack.vals[exec.stack.sp - 2];
    var class_type = (ArrayTypeSymbol)self.type;

    int idx = exec.stack.Pop();
    var arr = exec.stack.Pop();
    var val = exec.stack.Pop();

    class_type.ArrSetAt(arr, idx, val);

    val.Release();
    arr.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeArrAddInplace(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    ref var self = ref exec.stack.vals[exec.stack.sp - 2];
    self.Retain();
    var class_type = (ArrayTypeSymbol)self.type;
    //NOTE: Add must be at 0 index
    ((FuncSymbolNative)class_type._all_members[0]).cb(vm, exec, default);
    exec.stack.Push(self);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeMapIdx(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    ref var self = ref exec.stack.vals[exec.stack.sp - 2];
    var class_type = (MapTypeSymbol)self.type;

    var key = exec.stack.Pop();
    var map = exec.stack.Pop();

    class_type.MapTryGet(map, key, out var res);

    exec.stack.PushRetain(res);
    key.Release();
    map.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeMapIdxW(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    ref var self = ref exec.stack.vals[exec.stack.sp - 2];
    var class_type = (MapTypeSymbol)self.type;

    var key = exec.stack.Pop();
    var map = exec.stack.Pop();
    var val = exec.stack.Pop();

    class_type.MapSet(map, key, val);

    key.Release();
    val.Release();
    map.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeMapAddInplace(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    ref var self = ref exec.stack.vals[exec.stack.sp - 3];
    self.Retain();
    var class_type = (MapTypeSymbol)self.type;
    //NOTE: Add must be at 0 index
    ((FuncSymbolNative)class_type._all_members[0]).cb(vm, exec, default);
    exec.stack.Push(self);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void _OpcodeGetVar(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int local_idx = (int)Bytecode.Decode8(bytes, ref exec.ip);
    exec.stack_old.PushRetain(curr_frame.locals[local_idx]);
  }

  unsafe static void OpcodeGetVar(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);

    ref Val v = ref exec.stack.Push();
    //NOTE: we copy the whole value (we can have specialized opcodes for numbers)
    v = exec.stack.vals[frame.locals_offset + local_idx];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void _OpcodeSetVar(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int local_idx = (int)Bytecode.Decode8(bytes, ref exec.ip);
    var new_val = exec.stack_old.Pop();
    curr_frame.locals.Assign(vm, local_idx, new_val);
    new_val.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSetVar(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);

    //NOTE: we copy the whole value (we can have specialized opcodes for numbers)

    ref var new_val = ref exec.stack.Pop();
    //NOTE: Retaining an existing value increments refs counter
    //      and in case of newly created class it's 1 already
    //      so it doesn't make sense?
    //      Our refcounted objects are assumed to have refs counter = 1
    //      when they are created.
    //new_val._refc?.Retain();
    ref var current = ref exec.stack.vals[frame.locals_offset + local_idx];
    current._refc?.Release();
    current = new_val;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void _OpcodeArgVar(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);
    var arg_val = exec.stack_old.Pop();
    var loc_var = ValOld.New(vm);
    loc_var.ValueCopyFrom(arg_val);
    loc_var._refc?.Retain();
    curr_frame.locals[local_idx] = loc_var;
    arg_val.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeArgVar(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    //TODO: get rid of this opcode since we do this during InitFrame

    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);

    //we must 'own' local refcounted objects
    //exec.stack.vals[frame.locals_offset + local_idx]._refc?.Retain();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeArgRef(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);
    //TODO: how do we implement this? The only sane way - address Vals as indices :(
    throw new NotImplementedException();
    //curr_frame.locals[local_idx] = exec.stack_old.Pop();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeDeclVar(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);
    int type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);

    var type = frame.type_refs[type_idx];

    ref var curr = ref exec.stack.vals[frame.locals_offset + local_idx];
    //NOTE: handling case when variables are 're-declared' within the nested loop
    curr._refc?.Release();
    InitDefaultVal(type, ref curr);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetAttr(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int fld_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    ref var obj = ref exec.stack.Pop();
    var class_symb = (ClassSymbol)obj.type;
    var res = new Val();
    var field_symb = (FieldSymbol)class_symb._all_members[fld_idx];
    field_symb.getter(vm, obj, ref res, field_symb);
    //NOTE: we retain only the payload since we make the copy of the value
    //      and the new res already has refs = 1 while payload's refcount
    //      is not incremented
    //TODO: not really needed?
    //res._refc?.Retain();
    exec.stack.Push(res);
    //TODO: not really needed?
    //obj.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeRefAttr(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    throw new NotImplementedException();
    int fld_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);

    ref var obj = ref exec.stack.Pop();
    var class_symb = (ClassSymbol)obj.type;
    var field_symb = (FieldSymbol)class_symb._all_members[fld_idx];
    field_symb.getref(vm, obj, out var res, field_symb);
    exec.stack.PushRetain(res);
    obj.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSetAttr(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int fld_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);

    ref var obj = ref exec.stack.Pop();
    var class_symb = (ClassSymbol)obj.type;
    ref var val = ref exec.stack.Pop();
    var field_symb = (FieldSymbol)class_symb._all_members[fld_idx];
    field_symb.setter(vm, ref obj, val, field_symb);
    //TODO: not really needed?
    //val.Release();
    //obj.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSetAttrInplace(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int fld_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    ref var val = ref exec.stack.Pop();
    ref var obj = ref exec.stack.Peek();
    var class_symb = (ClassSymbol)obj.type;
    var field_symb = (FieldSymbol)class_symb._all_members[fld_idx];
    field_symb.setter(vm, ref obj, val, field_symb);
    //TODO: not really needed?
    //val.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetGVar(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int var_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    exec.stack_old.PushRetain(curr_frame.module.gvar_vals[var_idx]);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSetGVar(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int var_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    var new_val = exec.stack_old.Pop();
    curr_frame.module.gvar_vals.Assign(vm, var_idx, new_val);
    new_val.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeNop(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeReturn(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    exec.ip = EXIT_FRAME_IP - 1;
  }

  //TODO: we don't really need this opcode,
  //      we can encode amount of returned values in InitFrame:
  //      this way OpcodeReturn will be enough
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeReturnVal(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int ret_num = Bytecode.Decode8(bytes, ref exec.ip);

    frame.return_args_num = ret_num;

    exec.ip = EXIT_FRAME_IP - 1;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetFuncLocalPtr(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int func_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);

    var func_symb = curr_frame.module.func_index.index[func_idx];

    var ptr = FuncPtr.New(vm);
    ptr.Init(curr_frame.module, func_symb.ip_addr);
    exec.stack_old.Push(ValOld.NewObj(vm, ptr, func_symb.signature));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetFuncPtr(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int import_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    int func_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);

    var func_mod = curr_frame.module._imported[import_idx];
    var func_symb = func_mod.func_index.index[func_idx];

    var ptr = FuncPtr.New(vm);
    ptr.Init(func_mod, func_symb.ip_addr);
    exec.stack_old.Push(ValOld.NewObj(vm, ptr, func_symb.signature));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetFuncNativePtr(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int import_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    int func_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);

    //NOTE: using convention where built-in global module is always at index 0
    //      and imported modules are at (import_idx + 1)
    var func_mod = import_idx == 0 ? vm.types.module : curr_frame.module._imported[import_idx - 1];
    var nfunc_symb = func_mod.nfunc_index.index[func_idx];

    var ptr = FuncPtr.New(vm);
    ptr.Init(nfunc_symb);
    exec.stack_old.Push(ValOld.NewObj(vm, ptr, nfunc_symb.signature));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetFuncPtrFromVar(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int local_var_idx = Bytecode.Decode8(bytes, ref exec.ip);
    var val = curr_frame.locals[local_var_idx];
    val.Retain();
    exec.stack_old.Push(val);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeLastArgToTop(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    //NOTE: we need to move arg (e.g. func ptr) to the top of the stack
    //      so that it fullfills Opcode.Call requirements
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);
    int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
    int arg_idx = exec.stack_old.Count - args_num - 1;
    var arg = exec.stack_old[arg_idx];
    exec.stack_old.RemoveAt(arg_idx);
    exec.stack_old.Push(arg);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void _OpcodeCallLocal(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int func_ip = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    var frm = FrameOld.New(vm);
    frm.Init(curr_frame, exec.stack_old, func_ip);
    vm.Call(exec, frm, args_bits);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallLocal(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int func_ip = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    ref var new_frame = ref exec.PushFrame();
    new_frame.Init(frame, func_ip);
    vm.Call(exec, ref new_frame, exec.frames_count - 1, args_bits);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallGlobNative(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int func_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    var nfunc_symb = vm.types.module.nfunc_index[func_idx];

    if(CallNative(vm, exec, nfunc_symb, args_bits))
    {
      //let's cancel ip incrementing
      --exec.ip;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallNative(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int import_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    int func_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    //NOTE: using convention where built-in global module is always at index 0
    //      and imported modules are at (import_idx + 1)
    var func_mod = import_idx == 0 ? vm.types.module : curr_frame.module._imported[import_idx - 1];
    var nfunc_symb = func_mod.nfunc_index[func_idx];

    if(CallNative(vm, exec, nfunc_symb, args_bits))
    {
      //let's cancel ip incrementing
      --exec.ip;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCall(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int import_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    int func_ip = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    var func_mod = frame.module._imported[import_idx];

    ref var new_frame = ref exec.PushFrame();
    new_frame.Init(func_mod, func_ip);
    vm.Call(exec, ref new_frame, exec.frames_count - 1, args_bits);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallMethod(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int func_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    //TODO: use a simpler schema where 'self' is passed on the top
    int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
    int self_idx = exec.stack_old.Count - args_num - 1;
    var self = exec.stack_old[self_idx];
    exec.stack_old.RemoveAt(self_idx);

    var class_type = (ClassSymbolScript)self.type;
    var func_symb = (FuncSymbolScript)class_type._all_members[func_idx];

    var frm = FrameOld.New(vm);
    frm.Init(curr_frame.fb, exec.stack_old, func_symb._module, func_symb.ip_addr);

    frm.locals.Count = 1;
    frm.locals[0] = self;

    vm.Call(exec, frm, args_bits);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallMethodNative(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int func_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
    int self_idx = exec.stack_old.Count - args_num - 1;
    var self = exec.stack_old[self_idx];

    var class_type = (ClassSymbol)self.type;
    var func_symb = (FuncSymbolNative)class_type._all_members[func_idx];

    if(CallNative(curr_frame, exec.stack_old, func_symb, args_bits, out var _status, ref exec.coroutine))
    {
      exec.status = _status;
      //let's cancel ip incrementing
      --exec.ip;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallMethodVirt(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int virt_func_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    //TODO: use a simpler schema where 'self' is passed on the top
    int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
    int self_idx = exec.stack_old.Count - args_num - 1;
    var self = exec.stack_old[self_idx];
    exec.stack_old.RemoveAt(self_idx);

    var class_type = (ClassSymbol)self.type;
    var func_symb = (FuncSymbolScript)class_type._vtable[virt_func_idx];

    var frm = FrameOld.New(vm);
    frm.Init(curr_frame.fb, exec.stack_old, func_symb._module, func_symb.ip_addr);

    frm.locals.Count = 1;
    frm.locals[0] = self;

    vm.Call(exec, frm, args_bits);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallMethodIface(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int iface_func_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    int iface_type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    //TODO: use a simpler schema where 'self' is passed on the top
    int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
    int self_idx = exec.stack_old.Count - args_num - 1;
    var self = exec.stack_old[self_idx];
    exec.stack_old.RemoveAt(self_idx);

    var iface_symb = (InterfaceSymbol)curr_frame.type_refs[iface_type_idx];
    var class_type = (ClassSymbol)self.type;
    var func_symb = (FuncSymbolScript)class_type._itable[iface_symb][iface_func_idx];

    var frm = FrameOld.New(vm);
    frm.Init(curr_frame.fb, exec.stack_old, func_symb._module, func_symb.ip_addr);

    frm.locals.Count = 1;
    frm.locals[0] = self;

    vm.Call(exec, frm, args_bits);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallMethodIfaceNative(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int iface_func_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    int iface_type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    var iface_symb = (InterfaceSymbol)curr_frame.type_refs[iface_type_idx];
    var func_symb = (FuncSymbolNative)iface_symb.members[iface_func_idx];

    if(CallNative(curr_frame, exec.stack_old, func_symb, args_bits, out var _status, ref exec.coroutine))
    {
      exec.status = _status;
      //let's cancel ip incrementing
      --exec.ip;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallFuncPtr(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    var val_ptr = exec.stack_old.Pop();
    var ptr = (FuncPtr)val_ptr._obj;

    //checking if it's a native call
    if(ptr.native != null)
    {
      bool return_status =
        CallNative(curr_frame, exec.stack_old, ptr.native, args_bits, out var _status, ref exec.coroutine);
      val_ptr.Release();
      if(return_status)
      {
        exec.status = _status;
        //let's cancel ip incrementing
        --exec.ip;
      }
    }
    else
    {
      var frm = ptr.MakeFrame(vm, curr_frame.fb, exec.stack_old);
      val_ptr.Release();
      vm.Call(exec, frm, args_bits);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void _OpcodeInitFrame(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int local_vars_num = Bytecode.Decode8(bytes, ref exec.ip);
    var args_bits = exec.stack_old.Pop();
    curr_frame.locals.Count = local_vars_num;
    //NOTE: we need to store arg info bits locally so that
    //      this information will be available to func
    //      args related opcodes
    curr_frame.locals[local_vars_num - 1] = args_bits;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeInitFrame(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    //including args info (maybe we don't need that?)
    int local_vars_num = Bytecode.Decode8(bytes, ref exec.ip);

    var stack = exec.stack;

    //let's pop args bits but store it in a Frame
    var args_info = new FuncArgsInfo((uint)stack.vals[--stack.sp]._num);
    frame.args_info = args_info;
    //locals starts at the index of the first pushed argument
    int args_num = args_info.CountArgs();
    frame.locals_offset = stack.sp - args_num;
    frame.return_args_num = 0;

    //let's 'own' passed args
    for(int i = 0; i < args_num; i++)
      exec.stack.vals[frame.locals_offset + i]._refc?.Retain();

    //let's reserve space for local variables
    stack.Reserve(local_vars_num /* - args_num ? */);
    //temporary stack lives after local variables
    stack.sp += local_vars_num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeLambda(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    short offset = (short)Bytecode.Decode16(bytes, ref exec.ip);
    var ptr = FuncPtr.New(vm);
    ptr.Init(curr_frame, exec.ip + 1);
    exec.stack_old.Push(ValOld.NewObj(vm, ptr, Types.Any /*TODO: should be a FuncPtr type*/));

    exec.ip += offset;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeUseUpval(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int up_idx = (int)Bytecode.Decode8(bytes, ref exec.ip);
    int local_idx = (int)Bytecode.Decode8(bytes, ref exec.ip);
    var mode = (UpvalMode)Bytecode.Decode8(bytes, ref exec.ip);

    var addr = (FuncPtr)exec.stack_old.Peek()._obj;

    //TODO: amount of local variables must be known ahead and
    //      initialized during Frame initialization
    //NOTE: we need to reflect the updated max amount of locals,
    //      otherwise they might not be cleared upon Frame exit
    addr.upvals.Count = local_idx + 1;

    var upval = curr_frame.locals[up_idx];
    if(mode == UpvalMode.COPY)
    {
      var copy = ValOld.New(vm);
      copy.ValueCopyFrom(upval);
      addr.upvals[local_idx] = copy;
    }
    else
    {
      upval.Retain();
      addr.upvals[local_idx] = upval;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void _OpcodePop(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    exec.stack_old.PopRelease();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodePop(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    exec.stack.vals[--exec.stack.sp]._refc?.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeJump(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    short offset = (short)Bytecode.Decode16(bytes, ref exec.ip);
    exec.ip += offset;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void _OpcodeJumpZ(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int offset = (int)Bytecode.Decode16(bytes, ref exec.ip);
    if(exec.stack_old.PopRelease().bval == false)
      exec.ip += offset;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeJumpZ(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int offset = (int)Bytecode.Decode16(bytes, ref exec.ip);

    ref Val v = ref exec.stack.vals[--exec.stack.sp];

    if(v._num == 0)
      exec.ip += offset;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeJumpPeekZ(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int offset = (int)Bytecode.Decode16(bytes, ref exec.ip);
    if(exec.stack.vals[exec.stack.sp - 1]._num != 1)
      exec.ip += offset;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeJumpPeekNZ(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int offset = (int)Bytecode.Decode16(bytes, ref exec.ip);
    if(exec.stack.vals[exec.stack.sp - 1]._num == 1)
      exec.ip += offset;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeDefArg(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    byte def_arg_idx = Bytecode.Decode8(bytes, ref exec.ip);
    int jump_pos = (int)Bytecode.Decode16(bytes, ref exec.ip);
    //NOTE: if default argument is not used we need to jump out of default argument calculation code
    if(!frame.args_info.IsDefaultArgUsed(def_arg_idx))
      exec.ip += jump_pos;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeBlock(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    var new_coroutine = vm.ProcBlockOpcode(exec, curr_frame, region.defer_support);
    if(new_coroutine != null)
    {
      //NOTE: since there's a new coroutine we want to skip ip incrementing
      //      which happens below and proceed right to the execution of
      //      the new coroutine
      exec.coroutine = new_coroutine;
      //let's cancel ip incrementing
      --exec.ip;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeNew(VM vm, ExecState exec, ref Region region, FrameOld curr_frame, ref Frame frame, byte* bytes)
  {
    int type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    var type = frame.type_refs[type_idx];

    var cls = type as ClassSymbol;
    if(cls == null)
      throw new Exception("Not a class symbol: " + type);

    ref var val = ref exec.stack.Push();
    cls.creator(vm, ref val, cls);
  }

  unsafe static void InitOpcodeHandlers()
  {
    //binary ops
    op_handlers[(int)Opcodes.Add] = OpcodeAdd;
    op_handlers[(int)Opcodes.Sub] = OpcodeSub;
    op_handlers[(int)Opcodes.Div] = OpcodeDiv;
    op_handlers[(int)Opcodes.Mul] = OpcodeMul;
    op_handlers[(int)Opcodes.Equal] = OpcodeEqual;
    op_handlers[(int)Opcodes.NotEqual] = OpcodeNotEqual;
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

    op_handlers[(int)Opcodes.ArgVar] = OpcodeArgVar;
    op_handlers[(int)Opcodes.ArgRef] = OpcodeArgRef;

    op_handlers[(int)Opcodes.LastArgToTop] = OpcodeLastArgToTop;

    op_handlers[(int)Opcodes.GetAttr] = OpcodeGetAttr;
    op_handlers[(int)Opcodes.RefAttr] = OpcodeRefAttr;
    op_handlers[(int)Opcodes.SetAttr] = OpcodeSetAttr;
    op_handlers[(int)Opcodes.SetAttrInplace] = OpcodeSetAttrInplace;

    op_handlers[(int)Opcodes.GetGVar] = OpcodeGetGVar;
    op_handlers[(int)Opcodes.SetGVar] = OpcodeSetGVar;

    op_handlers[(int)Opcodes.Nop] = OpcodeNop;

    op_handlers[(int)Opcodes.Return] = OpcodeReturn;
    op_handlers[(int)Opcodes.ReturnVal] = OpcodeReturnVal;

    op_handlers[(int)Opcodes.GetFuncLocalPtr] = OpcodeGetFuncLocalPtr;
    op_handlers[(int)Opcodes.GetFuncPtr] = OpcodeGetFuncPtr;
    op_handlers[(int)Opcodes.GetFuncNativePtr] = OpcodeGetFuncNativePtr;
    op_handlers[(int)Opcodes.GetFuncPtrFromVar] = OpcodeGetFuncPtrFromVar;

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

    op_handlers[(int)Opcodes.InitFrame] = OpcodeInitFrame;

    op_handlers[(int)Opcodes.Lambda] = OpcodeLambda;
    op_handlers[(int)Opcodes.UseUpval] = OpcodeUseUpval;

    op_handlers[(int)Opcodes.Pop] = OpcodePop;
    op_handlers[(int)Opcodes.Jump] = OpcodeJump;
    op_handlers[(int)Opcodes.JumpZ] = OpcodeJumpZ;
    op_handlers[(int)Opcodes.JumpPeekZ] = OpcodeJumpPeekZ;
    op_handlers[(int)Opcodes.JumpPeekNZ] = OpcodeJumpPeekNZ;

    op_handlers[(int)Opcodes.DefArg] = OpcodeDefArg;

    op_handlers[(int)Opcodes.Block] = OpcodeBlock;

    op_handlers[(int)Opcodes.New] = OpcodeNew;
  }
}

}
