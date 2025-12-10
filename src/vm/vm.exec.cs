//#define ENABLE_IL2CPP
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
#if ENABLE_IL2CPP
    unsafe delegate void OpHandler(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes);
    static readonly OpHandler[] op_handlers =  new OpHandler[(int)Opcodes.MAX];
#else
    static readonly unsafe delegate*<VM, ExecState, ref Region, ref Frame, byte*, void>[] op_handlers =
      new delegate*<VM, ExecState, ref Region, ref Frame, byte*, void>[(int)Opcodes.MAX];
#endif

    public BHS status = BHS.NONE;

    public VM vm;
    public Fiber fiber;
    public ExecState parent;

    internal int ip;
    internal Coroutine coroutine;

    internal Region[] regions;
    internal int regions_count = 0;

    public ValStack stack;

    public Frame[] frames;
    public int frames_count = 0;

    public int self_val_idx;
    public ValStack self_val_vals;

    static ExecState()
    {
      InitOpcodeHandlers();
    }

    static unsafe void InitOpcodeHandlers()
    {
#if ENABLE_IL2CPP
      op_handlers[(int)Opcodes.Add] = OpcodeAdd;
      op_handlers[(int)Opcodes.Concat] = OpcodeConcat;
      op_handlers[(int)Opcodes.Sub] = OpcodeSub;
      op_handlers[(int)Opcodes.Div] = OpcodeDiv;
      op_handlers[(int)Opcodes.Mul] = OpcodeMul;
      op_handlers[(int)Opcodes.Equal] = OpcodeEqual;
      op_handlers[(int)Opcodes.EqualScalar] = OpcodeEqualScalar;
      op_handlers[(int)Opcodes.EqualString] = OpcodeEqualString;
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
      op_handlers[(int)Opcodes.GetVarScalar] = OpcodeGetVarScalar;
      op_handlers[(int)Opcodes.SetVarScalar] = OpcodeSetVarScalar;
      op_handlers[(int)Opcodes.DeclVar] = OpcodeDeclVar;

      op_handlers[(int)Opcodes.MakeRef] = OpcodeMakeRef;
      op_handlers[(int)Opcodes.SetRef] = OpcodeSetRef;
      op_handlers[(int)Opcodes.GetRef] = OpcodeGetRef;

      op_handlers[(int)Opcodes.GetAttr] = OpcodeGetAttr;
      op_handlers[(int)Opcodes.SetAttr] = OpcodeSetAttr;
      op_handlers[(int)Opcodes.GetVarAttr] = OpcodeGetVarAttr;
      op_handlers[(int)Opcodes.SetVarAttr] = OpcodeSetVarAttr;
      op_handlers[(int)Opcodes.SetAttrPeek] = OpcodeSetAttrPeek;
      op_handlers[(int)Opcodes.GetRefAttr] = OpcodeGetRefAttr;
      op_handlers[(int)Opcodes.SetRefAttr] = OpcodeSetRefAttr;
      op_handlers[(int)Opcodes.GetGVarAttr] = OpcodeGetGVarAttr;
      op_handlers[(int)Opcodes.SetGVarAttr] = OpcodeSetGVarAttr;

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
      op_handlers[(int)Opcodes.CallVarMethodNative] = OpcodeCallVarMethodNative;
      op_handlers[(int)Opcodes.CallGVarMethodNative] = OpcodeCallGVarMethodNative;

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

#else
      //binary ops
      op_handlers[(int)Opcodes.Add] = &OpcodeAdd;
      op_handlers[(int)Opcodes.Concat] = &OpcodeConcat;
      op_handlers[(int)Opcodes.Sub] = &OpcodeSub;
      op_handlers[(int)Opcodes.Div] = &OpcodeDiv;
      op_handlers[(int)Opcodes.Mul] = &OpcodeMul;
      op_handlers[(int)Opcodes.Equal] = &OpcodeEqual;
      op_handlers[(int)Opcodes.EqualScalar] = &OpcodeEqualScalar;
      op_handlers[(int)Opcodes.EqualString] = &OpcodeEqualString;
      op_handlers[(int)Opcodes.LT] = &OpcodeLT;
      op_handlers[(int)Opcodes.LTE] = &OpcodeLTE;
      op_handlers[(int)Opcodes.GT] = &OpcodeGT;
      op_handlers[(int)Opcodes.GTE] = &OpcodeGTE;
      op_handlers[(int)Opcodes.And] = &OpcodeAnd;
      op_handlers[(int)Opcodes.Or] = &OpcodeOr;
      op_handlers[(int)Opcodes.BitAnd] = &OpcodeBitAnd;
      op_handlers[(int)Opcodes.BitOr] = &OpcodeBitOr;
      op_handlers[(int)Opcodes.BitShr] = &OpcodeBitShr;
      op_handlers[(int)Opcodes.BitShl] = &OpcodeBitShl;
      op_handlers[(int)Opcodes.Mod] = &OpcodeMod;
      //unary ops
      op_handlers[(int)Opcodes.UnaryNot] = &OpcodeUnaryNot;
      op_handlers[(int)Opcodes.UnaryNeg] = &OpcodeUnaryNeg;
      op_handlers[(int)Opcodes.UnaryBitNot] = &OpcodeUnaryBitNot;

      op_handlers[(int)Opcodes.Constant] = &OpcodeConstant;

      op_handlers[(int)Opcodes.TypeCast] = &OpcodeTypeCast;
      op_handlers[(int)Opcodes.TypeAs] = &OpcodeTypeAs;
      op_handlers[(int)Opcodes.TypeIs] = &OpcodeTypeIs;
      op_handlers[(int)Opcodes.Typeof] = &OpcodeTypeof;

      //NOTE: Of these 2 only Opcode.Inc is used for incrementing a hidden
      //      variable in foreach loop. Opcode.Dec is not used anywhere.
      //      Maybe they could be used for actual operators for some restricted cases.
      op_handlers[(int)Opcodes.Inc] = &OpcodeInc;
      op_handlers[(int)Opcodes.Dec] = &OpcodeDec;

      op_handlers[(int)Opcodes.ArrIdx] = &OpcodeArrIdx;
      op_handlers[(int)Opcodes.ArrIdxW] = &OpcodeArrIdxW;
      op_handlers[(int)Opcodes.ArrAddInplace] = &OpcodeArrAddInplace;

      op_handlers[(int)Opcodes.MapIdx] = &OpcodeMapIdx;
      op_handlers[(int)Opcodes.MapIdxW] = &OpcodeMapIdxW;
      op_handlers[(int)Opcodes.MapAddInplace] = &OpcodeMapAddInplace;

      op_handlers[(int)Opcodes.GetVar] = &OpcodeGetVar;
      op_handlers[(int)Opcodes.SetVar] = &OpcodeSetVar;
      op_handlers[(int)Opcodes.GetVarScalar] = &OpcodeGetVarScalar;
      op_handlers[(int)Opcodes.SetVarScalar] = &OpcodeSetVarScalar;
      op_handlers[(int)Opcodes.DeclVar] = &OpcodeDeclVar;

      op_handlers[(int)Opcodes.MakeRef] = &OpcodeMakeRef;
      op_handlers[(int)Opcodes.SetRef] = &OpcodeSetRef;
      op_handlers[(int)Opcodes.GetRef] = &OpcodeGetRef;

      op_handlers[(int)Opcodes.GetAttr] = &OpcodeGetAttr;
      op_handlers[(int)Opcodes.SetAttr] = &OpcodeSetAttr;
      op_handlers[(int)Opcodes.GetVarAttr] = &OpcodeGetVarAttr;
      op_handlers[(int)Opcodes.SetVarAttr] = &OpcodeSetVarAttr;
      op_handlers[(int)Opcodes.SetAttrPeek] = &OpcodeSetAttrPeek;
      op_handlers[(int)Opcodes.GetRefAttr] = &OpcodeGetRefAttr;
      op_handlers[(int)Opcodes.SetRefAttr] = &OpcodeSetRefAttr;
      op_handlers[(int)Opcodes.GetGVarAttr] = &OpcodeGetGVarAttr;
      op_handlers[(int)Opcodes.SetGVarAttr] = &OpcodeSetGVarAttr;

      op_handlers[(int)Opcodes.GetGVar] = &OpcodeGetGVar;
      op_handlers[(int)Opcodes.SetGVar] = &OpcodeSetGVar;

      op_handlers[(int)Opcodes.Nop] = &OpcodeNop;

      op_handlers[(int)Opcodes.Return] = &OpcodeReturn;

      op_handlers[(int)Opcodes.GetFuncLocalPtr] = &OpcodeGetFuncLocalPtr;
      op_handlers[(int)Opcodes.GetFuncPtr] = &OpcodeGetFuncPtr;
      op_handlers[(int)Opcodes.GetFuncNativePtr] = &OpcodeGetFuncNativePtr;
      op_handlers[(int)Opcodes.GetFuncIpPtr] = &OpcodeGetFuncIpPtr;

      op_handlers[(int)Opcodes.CallLocal] = &OpcodeCallLocal;
      op_handlers[(int)Opcodes.CallGlobNative] = &OpcodeCallGlobNative;
      op_handlers[(int)Opcodes.CallNative] = &OpcodeCallNative;
      op_handlers[(int)Opcodes.Call] = &OpcodeCall;
      op_handlers[(int)Opcodes.CallMethod] = &OpcodeCallMethod;
      op_handlers[(int)Opcodes.CallMethodNative] = &OpcodeCallMethodNative;
      op_handlers[(int)Opcodes.CallMethodVirt] = &OpcodeCallMethodVirt;
      op_handlers[(int)Opcodes.CallMethodIface] = &OpcodeCallMethodIface;
      op_handlers[(int)Opcodes.CallMethodIfaceNative] = &OpcodeCallMethodIfaceNative;
      op_handlers[(int)Opcodes.CallFuncPtr] = &OpcodeCallFuncPtr;
      op_handlers[(int)Opcodes.CallFuncPtrInv] = &OpcodeCallFuncPtrInv;
      op_handlers[(int)Opcodes.CallVarMethodNative] = &OpcodeCallVarMethodNative;
      op_handlers[(int)Opcodes.CallGVarMethodNative] = &OpcodeCallGVarMethodNative;

      op_handlers[(int)Opcodes.Frame] = &OpcodeEnterFrame;

      op_handlers[(int)Opcodes.SetUpval] = &OpcodeSetUpval;

      op_handlers[(int)Opcodes.Pop] = &OpcodePop;
      op_handlers[(int)Opcodes.Jump] = &OpcodeJump;
      op_handlers[(int)Opcodes.JumpZ] = &OpcodeJumpZ;
      op_handlers[(int)Opcodes.JumpPeekZ] = &OpcodeJumpPeekZ;
      op_handlers[(int)Opcodes.JumpPeekNZ] = &OpcodeJumpPeekNZ;

      op_handlers[(int)Opcodes.DefArg] = &OpcodeDefArg;

      op_handlers[(int)Opcodes.Defer] = &OpcodeDefer;
      op_handlers[(int)Opcodes.Scope] = &OpcodeScope;
      op_handlers[(int)Opcodes.Paral] = &OpcodeParal;
      op_handlers[(int)Opcodes.ParalAll] = &OpcodeParalAll;

      op_handlers[(int)Opcodes.New] = &OpcodeNew;

#endif
    }

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
      ExecState top = this;
      while(top.parent != null)
        top = top.parent;

      top.GetStackTraceFromTop(info);
    }

    void GetStackTraceFromTop(List<VM.TraceItem> info)
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
          //NOTE: if symbol is missing let's write at least the module
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

          for(int r = regions_count; r-- > frame.region_offset_idx;)
          {
            ref var tmp_region = ref regions[r];
            if(tmp_region.defers != null && tmp_region.defers.count > 0)
              tmp_region.defers.ExitScope(this);
          }
          regions_count = frame.region_offset_idx;
          --frames_count;
          frame.ReleaseLocals();
        }
        frames_count = 0;
      }
      regions_count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void PushFrameRegion(ref Frame frame, int frame_idx)
    {
      ip = frame.start_ip;
      frame.region_offset_idx = regions_count;
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
    public void Execute(int region_stop_idx = 0)
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
      //TODO: move this to some opcode handler
      //3. exit frame requested
      else if(ip == EXIT_FRAME_IP)
      {
        //exiting all regions which belong to the frame
        for(int i = regions_count; i-- > frame.region_offset_idx;)
        {
          ref var tmp_region = ref regions[i];
          if(tmp_region.defers != null && tmp_region.defers.count > 0)
            tmp_region.defers.ExitScope(this);
        }
        regions_count = frame.region_offset_idx;
        frame.ReleaseLocals();

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref Val GetSelfRef()
    {
      ref var tmp = ref self_val_vals.vals[self_val_idx];
      if(tmp.type != Types.ValRef)
        return ref tmp;
      else
      {
        ValRef vr = (ValRef)tmp._refc;
        return ref vr.val;
      }
    }
  }

  ExecState[] script_executors = new ExecState[] { new (), new () };
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
    frame.region_offset_idx = exec.regions_count;
    exec.PushRegion(frame_idx);
    //since ip will be incremented below we decrement it intentionally here
    exec.ip = frame.start_ip - 1;
  }

  //NOTE: returns whether further execution should be stopped and
  //      status returned immediately (e.g in case of RUNNING or FAILURE)
  //      returns false if execution completed immediately
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
    v.type = type;
    //TODO: make type responsible for default extra initialization
    //      of the value
    if(type == Types.String)
      v.obj = "";
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
  public ValStack Execute(FuncSymbolScript fs, FuncArgsInfo args_info, StackList<Val> args)
  {
    return Execute(fs, args_info, ref args);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ValStack Execute(FuncSymbolScript fs, FuncArgsInfo args_info, ref StackList<Val> args)
  {
    if(++script_executor_idx == script_executors_count)
    {
      if(script_executors_count == script_executors.Length)
        Array.Resize(ref script_executors, script_executors_count << 1);
      script_executors[script_executors_count++] = new ExecState();
    }
    var exec = script_executors[script_executor_idx];
    var res = Execute(exec, fs, args_info, ref args);
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

    try
    {
      exec.Execute();
    }
    catch(Exception e)
    {
      var trace = new List<VM.TraceItem>();
      try
      {
        exec.GetStackTrace(trace);
      }
      catch(Exception)
      {
      }
      throw new Error(trace, e);
    }

    if(exec.status == BHS.RUNNING)
      throw new Exception($"Not expected to be running: {fs}");

    return stack;
  }
}

}
