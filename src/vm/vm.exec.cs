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
    Frame frame, ref Frame2 frame2, byte* bytes, ref BHS status
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

  public delegate void ClassCreator(VM.Frame frm, ref Val res, IType type);

  public struct Region
  {
    public Frame frame;

    public int frame2_idx;

    public List<DeferBlock> defer_support;

    //NOTE: if current ip is not within *inclusive* range of these values
    //      the frame context execution is considered to be done
    public int min_ip;
    public int max_ip;

    public Region(
      Frame frame,
      List<DeferBlock> defer_support,
      int min_ip = -1,
      int max_ip = STOP_IP)
    {
      this.frame = frame;
      this.defer_support = defer_support;
      this.min_ip = min_ip;
      this.max_ip = max_ip;
    }
  }

  public class ExecState
  {
    internal int ip;
    internal Coroutine coroutine;
    internal FixedStack<Region> regions = new FixedStack<Region>(32);
    internal FixedStack<Frame> frames = new FixedStack<Frame>(256);
    public ValStack stack;

    //public int sp;
    public Val2Stack stack2 = new Val2Stack();
    public Const[] constants2 = new Const[16];

    public Frame2[] frames2 = new Frame2[256];
    public int frames2_count = 0;

    [MethodImpl (MethodImplOptions.AggressiveInlining)]
    public ref Frame2 PushFrame2()
    {
      if(frames2_count == frames2.Length)
        Array.Resize(ref frames2, frames2_count << 1);

      return ref frames2[frames2_count++];
    }

    [MethodImpl (MethodImplOptions.AggressiveInlining)]
    public ref Frame2 PopFrame2()
    {
      return ref frames2[frames2_count--];
    }

    public delegate void LocalFunc(
      VM vm, ExecState exec, ref Region region,
      ref Frame2 frame, ref BHS status
    );
    public LocalFunc[] funcs2 = new LocalFunc[16];
  }

  //fake frame used for module's init code
  Frame init_frame;
  ExecState init_exec = new ExecState();

  //special case 'null' value
  Val null_val = null;

  public Val Null
  {
    get
    {
      null_val.Retain();
      return null_val;
    }
  }

  Val true_val = null;

  public Val True
  {
    get
    {
      true_val.Retain();
      return true_val;
    }
  }

  Val false_val = null;

  public Val False
  {
    get
    {
      false_val.Retain();
      return false_val;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal BHS Execute(ExecState exec, int exec_waterline_idx = 0)
  {
    var status = BHS.SUCCESS;

    while(exec.regions.Count > exec_waterline_idx && status == BHS.SUCCESS)
      status = ExecuteOnce(exec);

    return status;
  }

  unsafe BHS ExecuteOnce(ExecState exec)
  {
    ref var region = ref exec.regions.Peek();
    //var curr_frame = region.frame;
    //TODO: looks like frame2_idx is not really needed since we always need the top frame?
    ref var frame2 = ref exec.frames2[region.frame2_idx];

#if BHL_DEBUG
    Console.WriteLine("EXEC TICK " + curr_frame.fb.tick + " " + exec.GetHashCode() + ":" + exec.regions.Count + ":" + exec.frames.Count + " (" + curr_frame.GetHashCode() + "," + curr_frame.fb.id + ") IP " + exec.ip + "(min:" + item.min_ip + ", max:" + item.max_ip + ")" + (exec.ip > -1 && exec.ip < curr_frame.bytecode.Length ? " OP " + (Opcodes)curr_frame.bytecode[exec.ip] : " OP ? ") + " CORO " + exec.coroutine?.GetType().Name + "(" + exec.coroutine?.GetHashCode() + ")" + " DEFERABLE " + item.defer_support?.GetType().Name + "(" + item.defer_support?.GetHashCode() + ") " + curr_frame.bytecode.Length /* + " " + curr_frame.fb.GetStackTrace()*/ /* + " " + Environment.StackTrace*/);
#endif

    //NOTE: if there's an active coroutine it has priority over simple 'code following' via ip
    if(exec.coroutine != null)
      return ExecuteCoroutine(null, exec);

    if(exec.ip < region.min_ip || exec.ip > region.max_ip)
    {
      exec.regions.Pop();
      return BHS.SUCCESS;
    }

    if(exec.ip == EXIT_FRAME_IP)
    {
      //curr_frame.ExitScope(null, exec);

      //exec.frames.Pop();
      exec.regions.Pop();
      exec.ip = frame2.return_ip + 1;
      //exec.ip = curr_frame.return_ip + 1;
      //exec.stack = curr_frame.return_stack;

      //TODO: isn't it equal frame2?
      ref var tmp = ref exec.frames2[exec.frames2_count--];
      tmp.Deinit();
      //curr_frame.Release();

      return BHS.SUCCESS;
    }

    var status = BHS.SUCCESS;

    var bc = frame2.bytecode;
    var opcode = bc[exec.ip];

    op_handlers[opcode](
      this,
      exec,
      ref region,
      null,
      ref frame2,
      bc,
      ref status
    );

    ++exec.ip;
    return status;
  }

  void ExecInitCode(Module module)
  {
    var bytecode = module.compiled.initcode;
    if(bytecode == null || bytecode.Length == 0)
      return;

    init_exec.ip = 0;
    init_exec.stack = init_frame.stack;
    init_frame.Init(null, null, module, module.compiled.constants, module.compiled.type_refs_resolved, bytecode, 0);
    init_exec.regions.Push(new VM.Region(init_frame, null, 0, bytecode.Length - 1));
    //NOTE: here's the trick, init frame operates on global vars instead of locals
    init_frame.locals = init_frame.module.gvar_vals;

    //fixed (byte* p = init_frame.bytecode)
    {
      while(init_exec.regions.Count > 0)
      {
        var status = ExecuteOnce(init_exec/*, p*/);
        if(status == BHS.RUNNING)
          throw new Exception("Invalid state in init mode: " + status);
      }
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
    var fb = Start(addr, 0, default, FiberOptions.Detach);
    if(Tick(fb))
      throw new Exception("Module '" + module.name + "' init function is still running");
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  Coroutine ProcBlockOpcode(
    ExecState exec,
    Frame curr_frame,
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
    Frame curr_frame,
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
        seq.Init(curr_frame, exec.stack, ip + 1, ip + size);
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
  void Call(ExecState exec, Frame new_frame, uint args_bits)
  {
    int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
    for(int i = 0; i < args_num; ++i)
      new_frame.stack.Push(exec.stack.Pop());
    new_frame.stack.Push(Val.NewInt(this, args_bits));

    //let's remember ip to return to
    new_frame.return_ip = exec.ip;
    exec.stack = new_frame.stack;
    exec.frames.Push(new_frame);
    exec.regions.Push(new Region(new_frame, new_frame.defers));
    //since ip will be incremented below we decrement it intentionally here
    exec.ip = new_frame.start_ip - 1;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void Call2(ExecState exec, ref Frame2 frame2, int frame_idx2, uint args_bits)
  {
    var stack = exec.stack2;

    ref Val2 v = ref stack.Push();
    v.type = Types.Int;
    v._num = args_bits;

    //let's remember ip to return to
    frame2.return_ip = exec.ip;
    //exec.frames.Push(new_frame);
    exec.regions.Push(new Region(null, null) { frame2_idx =  frame_idx2 });
    //since ip will be incremented below we decrement it intentionally here
    exec.ip = frame2.start_ip - 1;
  }

  //NOTE: returns whether further execution should be stopped and status returned immediately (e.g in case of RUNNING or FAILURE)
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static bool CallNative(Frame curr_frame, ValStack curr_stack, FuncSymbolNative native, uint args_bits, out BHS status,
    ref Coroutine coroutine)
  {
    status = BHS.SUCCESS;
    var new_coroutine = native.cb(curr_frame, curr_stack, new FuncArgsInfo(args_bits), ref status);

    if(new_coroutine != null)
    {
      //NOTE: since there's a new coroutine we want to skip ip incrementing
      //      which happens below and proceed right to the execution of
      //      the new coroutine
      coroutine = new_coroutine;
      return true;
    }
    else if(status != BHS.SUCCESS)
      return true;
    else
      return false;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static BHS ExecuteCoroutine(Frame curr_frame, ExecState exec)
  {
    var status = BHS.SUCCESS;
    //NOTE: optimistically stepping forward so that for simple
    //      bindings you won't forget to do it
    ++exec.ip;
    exec.coroutine.Tick(curr_frame, exec, ref status);

    if(status == BHS.RUNNING)
    {
      --exec.ip;
      return status;
    }
    else if(status == BHS.FAILURE)
    {
      CoroutinePool.Del(curr_frame, exec, exec.coroutine);
      exec.coroutine = null;

      exec.ip = EXIT_FRAME_IP - 1;
      exec.regions.Pop();

      return status;
    }
    else if(status == BHS.SUCCESS)
    {
      CoroutinePool.Del(curr_frame, exec, exec.coroutine);
      exec.coroutine = null;

      return status;
    }
    else
      throw new Exception("Bad status: " + status);
  }

  //TODO: make it more universal and robust
  void HandleTypeCast(ExecState exec, IType cast_type, bool force_type)
  {
    var new_val = Val.New(this);
    var val = exec.stack.Pop();

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

    val.Release();

    exec.stack.Push(new_val);
  }

  void HandleTypeAs(ExecState exec, IType cast_type, bool force_type)
  {
    var val = exec.stack.Pop();

    if(Types.Is(val, cast_type))
    {
      var new_val = Val.New(this);
      new_val.ValueCopyFrom(val);
      if(force_type)
        new_val.type = cast_type;
      new_val._refc?.Retain();
      exec.stack.Push(new_val);
    }
    else
      exec.stack.Push(Null);

    val.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void HandleTypeIs(ExecState exec, IType type)
  {
    var val = exec.stack.Pop();
    exec.stack.Push(Val.NewBool(this, Types.Is(val, type)));
    val.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void HandleNew(Frame curr_frame, ValStack stack, IType type)
  {
    var cls = type as ClassSymbol;
    if(cls == null)
      throw new Exception("Not a class symbol: " + type);

    var val = Val.New(this);
    cls.creator(curr_frame, ref val, cls);
    stack.Push(val);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal Val MakeDefaultVal(IType type)
  {
    var v = Val.New(this);
    InitDefaultVal(type, v);
    return v;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal void InitDefaultVal(IType type, Val v)
  {
    //TODO: make type responsible for default initialization
    //      of the value
    if(type == Types.Int)
      v.SetNum(0);
    else if(type == Types.Float)
      v.SetFlt((double)0);
    else if(type == Types.String)
      v.SetStr("");
    else if(type == Types.Bool)
      v.SetBool(false);
    else
      v.type = type;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void _OpcodeAdd(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    //TODO: add Opcodes.Concat?
    if((r_operand.type == Types.String) && (l_operand.type == Types.String))
      stack.Push(Val.NewStr(vm, (string)l_operand._obj + (string)r_operand._obj));
    else
      stack.Push(Val.NewFlt(vm, l_operand._num + r_operand._num));

    r_operand.Release();
    l_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeAdd(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack2;

    ref Val2 r_operand = ref stack.vals[--stack.sp];
    ref Val2 l_operand = ref stack.vals[stack.sp - 1];
    l_operand._num += r_operand._num;
    //r_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void _OpcodeSub(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    stack.Push(Val.NewFlt(vm, l_operand._num - r_operand._num));

    r_operand.Release();
    l_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSub(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack2;

    ref Val2 r_operand = ref stack.vals[--stack.sp];
    ref Val2 l_operand = ref stack.vals[stack.sp - 1];
    l_operand._num -= r_operand._num;
    //r_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeDiv(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    stack.Push(Val.NewFlt(vm, l_operand._num / r_operand._num));

    r_operand.Release();
    l_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeMul(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    stack.Push(Val.NewFlt(vm, l_operand._num * r_operand._num));

    r_operand.Release();
    l_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void _OpcodeEqual(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    stack.Push(Val.NewBool(vm, l_operand.IsValueEqual(r_operand)));

    r_operand.Release();
    l_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeEqual(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack2;

    ref Val2 r_operand = ref stack.vals[--stack.sp];
    ref Val2 l_operand = ref stack.vals[stack.sp - 1];
    l_operand.type = Types.Bool;
    l_operand._num = l_operand._num == r_operand._num ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeNotEqual(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    stack.Push(Val.NewBool(vm, !l_operand.IsValueEqual(r_operand)));

    r_operand.Release();
    l_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeLT(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    stack.Push(Val.NewBool(vm, l_operand._num < r_operand._num));

    r_operand.Release();
    l_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeLTE(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    stack.Push(Val.NewBool(vm, l_operand._num <= r_operand._num));

    r_operand.Release();
    l_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGT(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    stack.Push(Val.NewBool(vm, l_operand._num > r_operand._num));

    r_operand.Release();
    l_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGTE(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    stack.Push(Val.NewBool(vm, l_operand._num >= r_operand._num));

    r_operand.Release();
    l_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeAnd(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    stack.Push(Val.NewBool(vm, l_operand._num == 1 && r_operand._num == 1));

    r_operand.Release();
    l_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeOr(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    stack.Push(Val.NewBool(vm, l_operand._num == 1 || r_operand._num == 1));

    r_operand.Release();
    l_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeBitAnd(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    stack.Push(Val.NewNum(vm, (int)l_operand._num & (int)r_operand._num));

    r_operand.Release();
    l_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeBitOr(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    stack.Push(Val.NewNum(vm, (int)l_operand._num | (int)r_operand._num));

    r_operand.Release();
    l_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeBitShr(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    stack.Push(Val.NewNum(vm, (int)l_operand._num >> (int)r_operand._num));

    r_operand.Release();
    l_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeBitShl(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    stack.Push(Val.NewNum(vm, (int)l_operand._num << (int)r_operand._num));

    r_operand.Release();
    l_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeMod(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    stack.Push(Val.NewFlt(vm, l_operand._num % r_operand._num));

    r_operand.Release();
    l_operand.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeUnaryNot(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var operand = stack.PopRelease().num;

    stack.Push(Val.NewBool(vm, operand != 1));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeUnaryNeg(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var operand = stack.PopRelease().num;

    stack.Push(Val.NewFlt(vm, operand * -1));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeUnaryBitNot(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var stack = exec.stack;
    var operand = stack.PopRelease().num;

    stack.Push(Val.NewNum(vm, ~((int)operand)));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void _OpcodeConstant(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int const_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    var cn = curr_frame.constants[const_idx];
    var cv = cn.ToVal(vm);
    exec.stack.Push(cv);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeConstant(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int const_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    var cn = frame2.constants[const_idx];

    ref Val2 v = ref exec.stack2.Push();
    v.type = Types.Int;
    v._num = cn.num;
    //v.SetInt(cn.num);
    //++exec.sp;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeTypeCast(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int cast_type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    bool force_type = (int)Bytecode.Decode8(bytes, ref exec.ip) == 1;

    var cast_type = curr_frame.type_refs[cast_type_idx];

    vm.HandleTypeCast(exec, cast_type, force_type);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeTypeAs(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int cast_type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    bool force_type = (int)Bytecode.Decode8(bytes, ref exec.ip) == 1;
    var as_type = curr_frame.type_refs[cast_type_idx];

    vm.HandleTypeAs(exec, as_type, force_type);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeTypeIs(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int cast_type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    var as_type = curr_frame.type_refs[cast_type_idx];

    vm.HandleTypeIs(exec, as_type);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeTypeof(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    var type = curr_frame.type_refs[type_idx];

    exec.stack.Push(Val.NewObj(vm, type, Types.Type));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeInc(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int var_idx = (int)Bytecode.Decode8(bytes, ref exec.ip);
    ++curr_frame.locals[var_idx]._num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeDec(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int var_idx = (int)Bytecode.Decode8(bytes, ref exec.ip);
    --curr_frame.locals[var_idx]._num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeArrIdx(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var self = exec.stack[exec.stack.Count - 2];
    var class_type = (ArrayTypeSymbol)self.type;

    int idx = (int)exec.stack.PopRelease().num;
    var arr = exec.stack.Pop();

    var res = class_type.ArrGetAt(arr, idx);

    exec.stack.Push(res);
    arr.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeArrIdxW(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var self = exec.stack[exec.stack.Count - 2];
    var class_type = (ArrayTypeSymbol)self.type;

    int idx = (int)exec.stack.PopRelease().num;
    var arr = exec.stack.Pop();
    var val = exec.stack.Pop();

    class_type.ArrSetAt(arr, idx, val);

    val.Release();
    arr.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeArrAddInplace(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var self = exec.stack[exec.stack.Count - 2];
    self.Retain();
    var class_type = (ArrayTypeSymbol)self.type;
    var _ = BHS.SUCCESS;
    //NOTE: Add must be at 0 index
    ((FuncSymbolNative)class_type._all_members[0]).cb(curr_frame, exec.stack, new FuncArgsInfo(), ref _);
    exec.stack.Push(self);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeMapIdx(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var self = exec.stack[exec.stack.Count - 2];
    var class_type = (MapTypeSymbol)self.type;

    var key = exec.stack.Pop();
    var map = exec.stack.Pop();

    class_type.MapTryGet(map, key, out var res);

    exec.stack.PushRetain(res);
    key.Release();
    map.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeMapIdxW(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var self = exec.stack[exec.stack.Count - 2];
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
  unsafe static void OpcodeMapAddInplace(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    var self = exec.stack[exec.stack.Count - 3];
    self.Retain();
    var class_type = (MapTypeSymbol)self.type;
    var _ = BHS.SUCCESS;
    //NOTE: Add must be at 0 index
    ((FuncSymbolNative)class_type._all_members[0]).cb(curr_frame, exec.stack, new FuncArgsInfo(), ref _);
    exec.stack.Push(self);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void _OpcodeGetVar(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int local_idx = (int)Bytecode.Decode8(bytes, ref exec.ip);
    exec.stack.PushRetain(curr_frame.locals[local_idx]);
  }

  unsafe static void OpcodeGetVar(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);

    ref Val2 v = ref exec.stack2.Push();
    v._num = exec.stack2.vals[frame2.locals_idx2 + local_idx]._num;

    //exec.stack.PushRetain(curr_frame.locals[local_idx]);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSetVar(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int local_idx = (int)Bytecode.Decode8(bytes, ref exec.ip);
    var new_val = exec.stack.Pop();
    curr_frame.locals.Assign(vm, local_idx, new_val);
    new_val.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void _OpcodeArgVar(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);
    var arg_val = exec.stack.Pop();
    var loc_var = Val.New(vm);
    loc_var.ValueCopyFrom(arg_val);
    loc_var._refc?.Retain();
    curr_frame.locals[local_idx] = loc_var;
    arg_val.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeArgVar(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);

    //it's already grown in init frame
    //++exec.stack2.sp;

    //var arg_val = exec.stack.Pop();
    //var loc_var = Val.New(vm);
    //loc_var.ValueCopyFrom(arg_val);
    //loc_var._refc?.Retain();
    //curr_frame.locals[local_idx] = loc_var;
    //arg_val.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeArgRef(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int local_idx = (int)Bytecode.Decode8(bytes, ref exec.ip);
    curr_frame.locals[local_idx] = exec.stack.Pop();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeDeclVar(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int local_idx = (int)Bytecode.Decode8(bytes, ref exec.ip);
    int type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    var type = curr_frame.type_refs[type_idx];

    var curr = curr_frame.locals[local_idx];
    //NOTE: handling case when variables are 're-declared' within the nested loop
    if(curr != null)
      curr.Release();
    curr_frame.locals[local_idx] = vm.MakeDefaultVal(type);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetAttr(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int fld_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    var obj = exec.stack.Pop();
    var class_symb = (ClassSymbol)obj.type;
    var res = Val.New(vm);
    var field_symb = (FieldSymbol)class_symb._all_members[fld_idx];
    field_symb.getter(curr_frame, obj, ref res, field_symb);
    //NOTE: we retain only the payload since we make the copy of the value
    //      and the new res already has refs = 1 while payload's refcount
    //      is not incremented
    res._refc?.Retain();
    exec.stack.Push(res);
    obj.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeRefAttr(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int fld_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    var obj = exec.stack.Pop();
    var class_symb = (ClassSymbol)obj.type;
    var field_symb = (FieldSymbol)class_symb._all_members[fld_idx];
    Val res;
    field_symb.getref(curr_frame, obj, out res, field_symb);
    exec.stack.PushRetain(res);
    obj.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSetAttr(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int fld_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);

    var obj = exec.stack.Pop();
    var class_symb = (ClassSymbol)obj.type;
    var val = exec.stack.Pop();
    var field_symb = (FieldSymbol)class_symb._all_members[fld_idx];
    field_symb.setter(curr_frame, ref obj, val, field_symb);
    val.Release();
    obj.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSetAttrInplace(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int fld_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    var val = exec.stack.Pop();
    var obj = exec.stack.Peek();
    var class_symb = (ClassSymbol)obj.type;
    var field_symb = (FieldSymbol)class_symb._all_members[fld_idx];
    field_symb.setter(curr_frame, ref obj, val, field_symb);
    val.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetGVar(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int var_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    exec.stack.PushRetain(curr_frame.module.gvar_vals[var_idx]);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSetGVar(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int var_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    var new_val = exec.stack.Pop();
    curr_frame.module.gvar_vals.Assign(vm, var_idx, new_val);
    new_val.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeNop(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeReturn(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    exec.ip = EXIT_FRAME_IP - 1;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void _OpcodeReturnVal(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int ret_num = Bytecode.Decode8(bytes, ref exec.ip);

    int stack_offset = exec.stack.Count;
    for(int i = 0; i < ret_num; ++i)
      curr_frame.return_stack.Push(exec.stack[stack_offset - ret_num + i]);
    exec.stack.Count -= ret_num;

    exec.ip = EXIT_FRAME_IP - 1;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeReturnVal(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int ret_num = Bytecode.Decode8(bytes, ref exec.ip);

    var stack = exec.stack2;

    int ret_start_offset = stack.sp - ret_num;
    //releasing all locals
    for(int i = frame2.locals_idx2; i < ret_start_offset; ++i)
      stack.vals[i].Release();

    int new_sp = frame2.locals_idx2 + ret_num;

    //moving returned values up
    Array.Copy(stack.vals, ret_start_offset, stack.vals, frame2.locals_idx2, ret_num);

    stack.sp = new_sp;

    //int stack_offset = exec.stack.Count;
    //for(int i = 0; i < ret_num; ++i)
    //  curr_frame.return_stack.Push(exec.stack[stack_offset - ret_num + i]);
    //exec.stack.Count -= ret_num;

    exec.ip = EXIT_FRAME_IP - 1;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetFuncLocalPtr(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int func_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);

    var func_symb = curr_frame.module.func_index.index[func_idx];

    var ptr = FuncPtr.New(vm);
    ptr.Init(curr_frame.module, func_symb.ip_addr);
    exec.stack.Push(Val.NewObj(vm, ptr, func_symb.signature));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetFuncPtr(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int import_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    int func_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);

    var func_mod = curr_frame.module._imported[import_idx];
    var func_symb = func_mod.func_index.index[func_idx];

    var ptr = FuncPtr.New(vm);
    ptr.Init(func_mod, func_symb.ip_addr);
    exec.stack.Push(Val.NewObj(vm, ptr, func_symb.signature));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetFuncNativePtr(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int import_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    int func_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);

    //NOTE: using convention where built-in global module is always at index 0
    //      and imported modules are at (import_idx + 1)
    var func_mod = import_idx == 0 ? vm.types.module : curr_frame.module._imported[import_idx - 1];
    var nfunc_symb = func_mod.nfunc_index.index[func_idx];

    var ptr = FuncPtr.New(vm);
    ptr.Init(nfunc_symb);
    exec.stack.Push(Val.NewObj(vm, ptr, nfunc_symb.signature));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGetFuncPtrFromVar(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int local_var_idx = Bytecode.Decode8(bytes, ref exec.ip);
    var val = curr_frame.locals[local_var_idx];
    val.Retain();
    exec.stack.Push(val);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeLastArgToTop(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    //NOTE: we need to move arg (e.g. func ptr) to the top of the stack
    //      so that it fullfills Opcode.Call requirements
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);
    int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
    int arg_idx = exec.stack.Count - args_num - 1;
    var arg = exec.stack[arg_idx];
    exec.stack.RemoveAt(arg_idx);
    exec.stack.Push(arg);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void _OpcodeCallLocal(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int func_ip = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    var frm = Frame.New(vm);
    frm.Init(curr_frame, exec.stack, func_ip);
    vm.Call(exec, frm, args_bits);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallLocal(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int func_ip = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    //var frm = Frame.New(vm);
    //frm.Init(curr_frame, exec.stack, func_ip);
    ref var new_frame = ref exec.PushFrame2();
    new_frame.Init(frame2, /*exec.stack,*/ func_ip);
    vm.Call2(exec, ref new_frame, exec.frames2_count - 1, args_bits);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallGlobNative(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int func_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    var nfunc_symb = vm.types.module.nfunc_index[func_idx];

    if(CallNative(curr_frame, exec.stack, nfunc_symb, args_bits, out var _status, ref exec.coroutine))
    {
      status = _status;
      //let's cancel ip incrementing
      --exec.ip;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallNative(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int import_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    int func_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    //NOTE: using convention where built-in global module is always at index 0
    //      and imported modules are at (import_idx + 1)
    var func_mod = import_idx == 0 ? vm.types.module : curr_frame.module._imported[import_idx - 1];
    var nfunc_symb = func_mod.nfunc_index[func_idx];

    if(CallNative(curr_frame, exec.stack, nfunc_symb, args_bits, out var _status, ref exec.coroutine))
    {
      status = _status;
      //let's cancel ip incrementing
      --exec.ip;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCall(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int import_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    int func_ip = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    var func_mod = curr_frame.module._imported[import_idx];

    var frm = Frame.New(vm);
    frm.Init(curr_frame.fb, exec.stack, func_mod, func_ip);
    vm.Call(exec, frm, args_bits);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallMethod(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int func_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    //TODO: use a simpler schema where 'self' is passed on the top
    int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
    int self_idx = exec.stack.Count - args_num - 1;
    var self = exec.stack[self_idx];
    exec.stack.RemoveAt(self_idx);

    var class_type = (ClassSymbolScript)self.type;
    var func_symb = (FuncSymbolScript)class_type._all_members[func_idx];

    var frm = Frame.New(vm);
    frm.Init(curr_frame.fb, exec.stack, func_symb._module, func_symb.ip_addr);

    frm.locals.Count = 1;
    frm.locals[0] = self;

    vm.Call(exec, frm, args_bits);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallMethodNative(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int func_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
    int self_idx = exec.stack.Count - args_num - 1;
    var self = exec.stack[self_idx];

    var class_type = (ClassSymbol)self.type;
    var func_symb = (FuncSymbolNative)class_type._all_members[func_idx];

    if(CallNative(curr_frame, exec.stack, func_symb, args_bits, out var _status, ref exec.coroutine))
    {
      status = _status;
      //let's cancel ip incrementing
      --exec.ip;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallMethodVirt(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int virt_func_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    //TODO: use a simpler schema where 'self' is passed on the top
    int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
    int self_idx = exec.stack.Count - args_num - 1;
    var self = exec.stack[self_idx];
    exec.stack.RemoveAt(self_idx);

    var class_type = (ClassSymbol)self.type;
    var func_symb = (FuncSymbolScript)class_type._vtable[virt_func_idx];

    var frm = Frame.New(vm);
    frm.Init(curr_frame.fb, exec.stack, func_symb._module, func_symb.ip_addr);

    frm.locals.Count = 1;
    frm.locals[0] = self;

    vm.Call(exec, frm, args_bits);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallMethodIface(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int iface_func_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    int iface_type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    //TODO: use a simpler schema where 'self' is passed on the top
    int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
    int self_idx = exec.stack.Count - args_num - 1;
    var self = exec.stack[self_idx];
    exec.stack.RemoveAt(self_idx);

    var iface_symb = (InterfaceSymbol)curr_frame.type_refs[iface_type_idx];
    var class_type = (ClassSymbol)self.type;
    var func_symb = (FuncSymbolScript)class_type._itable[iface_symb][iface_func_idx];

    var frm = Frame.New(vm);
    frm.Init(curr_frame.fb, exec.stack, func_symb._module, func_symb.ip_addr);

    frm.locals.Count = 1;
    frm.locals[0] = self;

    vm.Call(exec, frm, args_bits);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallMethodIfaceNative(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int iface_func_idx = (int)Bytecode.Decode16(bytes, ref exec.ip);
    int iface_type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    var iface_symb = (InterfaceSymbol)curr_frame.type_refs[iface_type_idx];
    var func_symb = (FuncSymbolNative)iface_symb.members[iface_func_idx];

    if(CallNative(curr_frame, exec.stack, func_symb, args_bits, out var _status, ref exec.coroutine))
    {
      status = _status;
      //let's cancel ip incrementing
      --exec.ip;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeCallFuncPtr(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    uint args_bits = Bytecode.Decode32(bytes, ref exec.ip);

    var val_ptr = exec.stack.Pop();
    var ptr = (FuncPtr)val_ptr._obj;

    //checking if it's a native call
    if(ptr.native != null)
    {
      bool return_status =
        CallNative(curr_frame, exec.stack, ptr.native, args_bits, out var _status, ref exec.coroutine);
      val_ptr.Release();
      if(return_status)
      {
        status = _status;
        //let's cancel ip incrementing
        --exec.ip;
      }
    }
    else
    {
      var frm = ptr.MakeFrame(vm, curr_frame.fb, exec.stack);
      val_ptr.Release();
      vm.Call(exec, frm, args_bits);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void _OpcodeInitFrame(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int local_vars_num = Bytecode.Decode8(bytes, ref exec.ip);
    var args_bits = exec.stack.Pop();
    curr_frame.locals.Count = local_vars_num;
    //NOTE: we need to store arg info bits locally so that
    //      this information will be available to func
    //      args related opcodes
    curr_frame.locals[local_vars_num - 1] = args_bits;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeInitFrame(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int local_vars_num = Bytecode.Decode8(bytes, ref exec.ip);

    var stack = exec.stack2;

    //ref Val2 args_bits = ref stack.vals[--stack.sp];
    ref Val2 args_bits = ref stack.vals[stack.sp - 1];

    frame2.args_bits2 = (uint)args_bits._num;
    frame2.locals_idx2 = stack.sp - local_vars_num;
    stack.Add(local_vars_num);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeLambda(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    short offset = (short)Bytecode.Decode16(bytes, ref exec.ip);
    var ptr = FuncPtr.New(vm);
    ptr.Init(curr_frame, exec.ip + 1);
    exec.stack.Push(Val.NewObj(vm, ptr, Types.Any /*TODO: should be a FuncPtr type*/));

    exec.ip += offset;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeUseUpval(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int up_idx = (int)Bytecode.Decode8(bytes, ref exec.ip);
    int local_idx = (int)Bytecode.Decode8(bytes, ref exec.ip);
    var mode = (UpvalMode)Bytecode.Decode8(bytes, ref exec.ip);

    var addr = (FuncPtr)exec.stack.Peek()._obj;

    //TODO: amount of local variables must be known ahead and
    //      initialized during Frame initialization
    //NOTE: we need to reflect the updated max amount of locals,
    //      otherwise they might not be cleared upon Frame exit
    addr.upvals.Count = local_idx + 1;

    var upval = curr_frame.locals[up_idx];
    if(mode == UpvalMode.COPY)
    {
      var copy = Val.New(vm);
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
  unsafe static void _OpcodePop(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    exec.stack.PopRelease();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodePop(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    exec.stack2.vals[--exec.stack2.sp].Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeJump(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    short offset = (short)Bytecode.Decode16(bytes, ref exec.ip);
    exec.ip += offset;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void _OpcodeJumpZ(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int offset = (int)Bytecode.Decode16(bytes, ref exec.ip);
    if(exec.stack.PopRelease().bval == false)
      exec.ip += offset;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeJumpZ(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int offset = (int)Bytecode.Decode16(bytes, ref exec.ip);

    ref Val2 v = ref exec.stack2.vals[--exec.stack2.sp];

    if(v._num == 0)
      exec.ip += offset;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeJumpPeekZ(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int offset = (int)Bytecode.Decode16(bytes, ref exec.ip);
    var v = exec.stack.Peek();
    if(v.bval == false)
      exec.ip += offset;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeJumpPeekNZ(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int offset = (int)Bytecode.Decode16(bytes, ref exec.ip);
    var v = exec.stack.Peek();
    if(v.bval == true)
      exec.ip += offset;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeDefArg(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    byte def_arg_idx = Bytecode.Decode8(bytes, ref exec.ip);
    int jump_pos = (int)Bytecode.Decode16(bytes, ref exec.ip);
    uint args_bits = (uint)curr_frame.locals[curr_frame.locals.Count - 1]._num;
    var args_info = new FuncArgsInfo(args_bits);
    //Console.WriteLine("DEF ARG: " + def_arg_idx + ", jump pos " + jump_pos + ", used " + args_info.IsDefaultArgUsed(def_arg_idx) + " " + args_bits);
    //NOTE: if default argument is not used we need to jump out of default argument calculation code
    if(!args_info.IsDefaultArgUsed(def_arg_idx))
      exec.ip += jump_pos;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeBlock(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
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
  unsafe static void OpcodeNew(VM vm, ExecState exec, ref Region region, Frame curr_frame, ref Frame2 frame2, byte* bytes, ref BHS status)
  {
    int type_idx = (int)Bytecode.Decode24(bytes, ref exec.ip);
    var type = curr_frame.type_refs[type_idx];
    vm.HandleNew(curr_frame, exec.stack, type);
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
