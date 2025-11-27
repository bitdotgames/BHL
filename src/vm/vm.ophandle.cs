using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace bhl
{

public partial class VM
{
  static readonly unsafe delegate*<VM, ExecState, ref Region, ref Frame, byte*, void>[] op_handlers =
    new delegate*<VM, ExecState, ref Region, ref Frame, byte*, void>[(int)Opcodes.MAX];

  static VM()
  {
    InitOpcodeHandlers();
  }

  static unsafe void InitOpcodeHandlers()
  {
    //binary ops
    op_handlers[(int)Opcodes.Add] = &OpcodeAdd;
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
    op_handlers[(int)Opcodes.SetAttrInplace] = &OpcodeSetAttrInplace;

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
  unsafe static void OpcodeEqualString(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    l_operand.type = Types.Bool;
    l_operand.num = (string)r_operand.obj == (string)l_operand.obj ? 1 : 0;
    l_operand.obj = null;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeEqualScalar(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
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
    l_operand = res;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeLT(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    //TODO: do we really need to set type for scalar values?
    l_operand.type = Types.Bool;
    l_operand.num = l_operand.num < r_operand.num ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeLTE(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    //TODO: do we really need to set type for scalar values?
    l_operand.type = Types.Bool;
    l_operand.num = l_operand.num <= r_operand.num ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGT(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    //TODO: do we really need to set type for scalar values?
    l_operand.type = Types.Bool;
    l_operand.num = l_operand.num > r_operand.num ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeGTE(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];

    //TODO: do we really need to set type for scalar values?
    l_operand.type = Types.Bool;
    l_operand.num = l_operand.num >= r_operand.num ? 1 : 0;
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

    //TODO: what about specialized opcodes for each constant type?
    switch(cn.type)
    {
      case ConstType.INT:
      {
        v.type = Types.Int;
        v.num = cn.num;
      }
        break;
      case ConstType.BOOL:
      {
        v.type = Types.Bool;
        v.num = cn.num;
      }
        break;
      case ConstType.STR:
      {
        v.type = Types.String;
        v.obj = cn.str;
      }
        break;
      case ConstType.FLT:
      {
        v.type = Types.Float;
        v.num = cn.num;
      }
        break;
      case ConstType.NIL:
      {
        v = Null;
      }
        break;
      default:
        throw new Exception("Bad type");
    }
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
  unsafe static void OpcodeGetVarScalar(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);

    ref Val new_val = ref exec.stack.Push();
    ref var source = ref frame.locals.vals[frame.locals_offset + local_idx];
    //TODO: can we get rid of type setting?
    new_val.type = source.type;
    new_val.num = source.num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSetVar(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);

    exec.stack.Pop(out var new_val);
    ref var current = ref frame.locals.vals[frame.locals_offset + local_idx];
    current._refc?.Release();
    current = new_val;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  unsafe static void OpcodeSetVarScalar(VM vm, ExecState exec, ref Region region, ref Frame frame, byte* bytes)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);

    ref var new_val = ref exec.stack.vals[--exec.stack.sp];
    ref var dest = ref frame.locals.vals[frame.locals_offset + local_idx];
    //TODO: can we get rid of type setting?
    dest.type = new_val.type;
    dest.num = new_val.num;
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
  unsafe static void OpcodeMakeRef(VM vm, ExecState exec, ref Region region, ref Frame frame,
    byte* bytes)
  {
    int local_idx = Bytecode.Decode8(bytes, ref exec.ip);

    ref var curr = ref frame.locals.vals[frame.locals_offset + local_idx];

    //replacing existing val with ValRef if it's not already a ValRef
    //(this a special case required e.g for loop variables)
    if(curr.type != Types.ValRef || curr._refc == null /*since we don't clear type, let's check for _refc as well*/)
    {
      var vr_val = new Val();
      vr_val.type = Types.ValRef;
      var vr = ValRef.New(vm);
      //NOTE: we wrap an existing value
      vr.val = curr;
      vr_val._refc = vr;
      curr = vr_val;
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
    exec.stack.Push(Val.NewObj(ptr, Types.FuncPtr));
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

      //we need to move only passed arguments and move them in reverse order
      //so that they don't overlap during 'movement'
      for(int i = args_num - arg_idx; i-- > 0;)
      {
        ref var tmp = ref exec.stack.vals[frame.locals_offset + arg_idx + i];
        exec.stack.vals[frame.locals_offset + arg_idx + i + 1] = tmp;
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
}

}
