#if BHL_JIT
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace bhl
{

public static class BHLJit
{
  public const int JIT_THRESHOLD = 100;

  // Static slot table used by generated IL to resolve self-recursive calls.
  // Index is stable for the lifetime of the process.
  internal static Func<double, double>[] _slots = new Func<double, double>[64];
  internal static int _slot_count;

  static readonly FieldInfo _slots_field =
    typeof(BHLJit).GetField(nameof(_slots), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

  static readonly MethodInfo _invoke1 =
    typeof(Func<double, double>).GetMethod("Invoke");

  // ---------------------------------------------------------------
  // Public API
  // ---------------------------------------------------------------

  public static void TryCompile(FuncSymbolScript fs)
  {
    if (!RuntimeFeature.IsDynamicCodeSupported)
      return;
    if (fs._jit_func != null)
      return;
    if (!CanJit(fs))
      return;

    int slot = _slot_count++;
    if (slot >= _slots.Length)
      Array.Resize(ref _slots, _slots.Length << 1);

    var func = Compile(fs, slot);
    if (func == null)
    {
      --_slot_count;
      return;
    }

    _slots[slot] = func;
    fs._jit_func = func;
  }

  // ---------------------------------------------------------------
  // Eligibility check
  // ---------------------------------------------------------------

  static bool CanJit(FuncSymbolScript fs)
  {
    if (fs._module == null || fs._ip_addr < 0)
      return false;

    var sig = fs.signature;

    // No coroutines
    if ((sig.attribs & FuncSignatureAttrib.Coro) != 0)
      return false;

    // Exactly 1 scalar argument
    if (sig.arg_types.Count != 1)
      return false;
    if (!Types.IsScalar(sig.arg_types[0].Get() as IType))
      return false;

    // Exactly 1 scalar return value
    if (sig.GetReturnedArgsNum() != 1)
      return false;
    if (!Types.IsScalar(sig.return_type.Get() as IType))
      return false;

    return true;
  }

  // ---------------------------------------------------------------
  // Instruction record
  // ---------------------------------------------------------------

  struct Instr
  {
    public int    ip;
    public Opcodes op;
    public int    size;         // total byte size incl. opcode byte
    public int[]  operands;     // decoded operand values (little-endian, signed where relevant)
  }

  // ---------------------------------------------------------------
  // Bytecode scanner
  // ---------------------------------------------------------------

  static List<Instr> Scan(byte[] bc, int start_ip, out bool unsupported)
  {
    unsupported = false;
    var all_instrs = new List<Instr>(32);
    var seen = new HashSet<int>(32); // set of decoded IPs

    // Pass 1: linear scan collecting all instructions until we've seen
    // a Return whose next IP is past all reachable jump targets.
    int ip = start_ip;
    int last_return_next_ip = -1;
    while (ip < bc.Length)
    {
      var op = (Opcodes)bc[ip];

      switch (op)
      {
        case Opcodes.Frame:
        case Opcodes.GetVarScalar:
        case Opcodes.SetVarScalar:
        case Opcodes.Constant:
        case Opcodes.EqualScalar:
        case Opcodes.Add:
        case Opcodes.Sub:
        case Opcodes.Mul:
        case Opcodes.Div:
        case Opcodes.Mod:
        case Opcodes.UnaryNeg:
        case Opcodes.LT:
        case Opcodes.LTE:
        case Opcodes.GT:
        case Opcodes.GTE:
        case Opcodes.Jump:
        case Opcodes.JumpZ:
        case Opcodes.CallLocal:
        case Opcodes.Return:
          break;
        default:
          unsupported = true;
          return null;
      }

      var def  = ModuleCompiler.LookupOpcode(op);
      var ops  = ReadOperands(bc, ip, def);
      int next = ip + def.size;

      seen.Add(ip);
      all_instrs.Add(new Instr { ip = ip, op = op, size = def.size, operands = ops });

      if (op == Opcodes.Return)
        last_return_next_ip = next;

      ip = next;

      // Stop only after a Return or unconditional Jump (both end a basic block), once
      // all JumpZ targets have been scanned.  Stopping at the JumpZ target itself
      // would miss the Return that follows it.
      if (last_return_next_ip >= 0 &&
          (op == Opcodes.Return || op == Opcodes.Jump))
      {
        bool covered = true;
        foreach (var instr in all_instrs)
        {
          if (instr.op == Opcodes.JumpZ) // JumpZ targets are always live
          {
            int target = JumpTarget(instr);
            if (!seen.Contains(target)) { covered = false; break; }
          }
        }
        if (covered) break;
      }
    }

    // Pass 2: filter unreachable instructions (dead code after Return/Jump)
    // BHL compiler emits Jump after Return for else-branch merging; we skip those.
    var all_targets = new HashSet<int>();
    foreach (var instr in all_instrs)
      if (instr.op == Opcodes.Jump || instr.op == Opcodes.JumpZ)
        all_targets.Add(JumpTarget(instr));

    var reachable = new List<Instr>(all_instrs.Count);
    bool live = true;
    foreach (var instr in all_instrs)
    {
      bool is_target = all_targets.Contains(instr.ip);
      if (!live && !is_target)
        continue;  // dead code — skip

      reachable.Add(instr);
      live = instr.op != Opcodes.Return && instr.op != Opcodes.Jump;
    }

    return reachable;
  }

  static int[] ReadOperands(byte[] bc, int ip, ModuleCompiler.Definition def)
  {
    var ops  = new int[def.operand_width.Length];
    int rpos = ip + 1;
    for (int i = 0; i < def.operand_width.Length; i++)
    {
      int w = def.operand_width[i];
      ops[i] = w switch
      {
        1 => (sbyte)bc[rpos],
        // Jump offset is signed (short), all other 2-byte operands can stay unsigned
        2 => (short)(bc[rpos] | (bc[rpos + 1] << 8)),
        3 => (int)((uint)(bc[rpos] | (bc[rpos + 1] << 8) | (bc[rpos + 2] << 16))),
        4 => (int)(bc[rpos] | (uint)bc[rpos + 1] << 8 | (uint)bc[rpos + 2] << 16 | (uint)bc[rpos + 3] << 24),
        _ => throw new Exception("Invalid operand width: " + w)
      };
      rpos += w;
    }
    return ops;
  }

  // Jump/JumpZ: target = ip_after_instr + signed_offset
  static int JumpTarget(Instr instr) =>
    instr.ip + instr.size + instr.operands[0];

  // ---------------------------------------------------------------
  // Stack-depth analysis
  // ---------------------------------------------------------------

  // Returns max stack depth, or -1 if tracking fails
  static int MaxStackDepth(List<Instr> instrs, int start_ip)
  {
    int sp = 0, max = 0;
    foreach (var instr in instrs)
    {
      switch (instr.op)
      {
        case Opcodes.Frame:           break;
        case Opcodes.GetVarScalar:    sp++; if (sp > max) max = sp; break;
        case Opcodes.Constant:        sp++; if (sp > max) max = sp; break;
        case Opcodes.SetVarScalar:    sp--; break;
        case Opcodes.EqualScalar:
        case Opcodes.Add: case Opcodes.Sub:
        case Opcodes.Mul: case Opcodes.Div: case Opcodes.Mod:
        case Opcodes.LT:  case Opcodes.LTE:
        case Opcodes.GT:  case Opcodes.GTE:
          sp--;  break;  // pop 2 push 1
        case Opcodes.UnaryNeg:        break;  // pop 1 push 1
        case Opcodes.JumpZ:           sp--; break;  // pop condition
        case Opcodes.Jump:            break;
        case Opcodes.CallLocal:
        {
          int args_num = instr.operands[1] & (int)FuncArgsInfo.ARGS_NUM_MASK;
          sp -= args_num;
          sp++;  // 1 return value
          if (sp > max) max = sp;
          break;
        }
        case Opcodes.Return:          break;
      }
    }
    return max;
  }

  // ---------------------------------------------------------------
  // IL emission
  // ---------------------------------------------------------------

  static Func<double, double> Compile(FuncSymbolScript fs, int slot)
  {
    var bc        = fs._module.compiled.bytecode;
    var constants = fs._module.compiled.constants;

    var instrs = Scan(bc, fs._ip_addr, out bool unsupported);
    if (unsupported || instrs == null || instrs.Count == 0)
      return null;

    // Validate: all CallLocals must be self-recursive with 1 arg
    foreach (var instr in instrs)
    {
      if (instr.op == Opcodes.CallLocal)
      {
        int target_ip = instr.operands[0]; // func_ip (absolute)
        int args_num  = instr.operands[1] & (int)FuncArgsInfo.ARGS_NUM_MASK;
        if (target_ip != fs._ip_addr || args_num != 1)
          return null;  // non-self or multi-arg call — bail out
      }
    }

    int max_depth = MaxStackDepth(instrs, fs._ip_addr);
    if (max_depth <= 0) max_depth = 1;

    // DynamicMethod: takes one double, returns one double
    var dm = new DynamicMethod(
      "bhl$" + fs.name,
      typeof(double),
      new[] { typeof(double) },
      typeof(BHLJit),
      skipVisibility: true
    );

    var il = dm.GetILGenerator();

    // Stack slot locals (s0, s1, ...)
    var stk = new LocalBuilder[max_depth];
    for (int i = 0; i < max_depth; i++)
      stk[i] = il.DeclareLocal(typeof(double));

    // Labels for jump targets + expected vsp when each label is reached
    var labels     = new Dictionary<int, Label>();
    var label_vsp  = new Dictionary<int, int>();
    foreach (var instr in instrs)
      if (instr.op == Opcodes.Jump || instr.op == Opcodes.JumpZ)
      {
        int t = JumpTarget(instr);
        if (!labels.ContainsKey(t))
          labels[t] = il.DefineLabel();
        // vsp at the branch target = vsp after the branch opcode itself
        // JumpZ pops 1 (condition), Jump doesn't change vsp — computed later during emit
      }

    int vsp = 0;  // compile-time virtual stack pointer

    foreach (var instr in instrs)
    {
      // Restore vsp to the value known at this label (set when the branch was emitted)
      if (label_vsp.TryGetValue(instr.ip, out int saved_vsp))
        vsp = saved_vsp;

      // Place label if this ip is a jump target
      if (labels.TryGetValue(instr.ip, out var lbl))
        il.MarkLabel(lbl);
      switch (instr.op)
      {
        case Opcodes.Frame:
          vsp = 0;
          break;

        case Opcodes.GetVarScalar:
          // Only supports local[0] = function argument
          if (instr.operands[0] != 0) return null;
          il.Emit(OpCodes.Ldarg_0);
          il.Emit(OpCodes.Stloc, stk[vsp++]);
          break;

        case Opcodes.SetVarScalar:
          if (instr.operands[0] != 0) return null;
          il.Emit(OpCodes.Ldloc, stk[--vsp]);
          il.Emit(OpCodes.Starg_S, (byte)0);
          break;

        case Opcodes.Constant:
        {
          var c = constants[instr.operands[0]];
          il.Emit(OpCodes.Ldc_R8, c.num);
          il.Emit(OpCodes.Stloc, stk[vsp++]);
          break;
        }

        case Opcodes.EqualScalar:
        {
          var r = stk[--vsp];
          var l = stk[vsp - 1];
          il.Emit(OpCodes.Ldloc, l);
          il.Emit(OpCodes.Ldloc, r);
          il.Emit(OpCodes.Ceq);
          il.Emit(OpCodes.Conv_R8);
          il.Emit(OpCodes.Stloc, l);
          break;
        }

        case Opcodes.Add:  EmitBinOp(il, stk, ref vsp, OpCodes.Add);  break;
        case Opcodes.Sub:  EmitBinOp(il, stk, ref vsp, OpCodes.Sub);  break;
        case Opcodes.Mul:  EmitBinOp(il, stk, ref vsp, OpCodes.Mul);  break;
        case Opcodes.Div:  EmitBinOp(il, stk, ref vsp, OpCodes.Div);  break;
        case Opcodes.Mod:  EmitBinOp(il, stk, ref vsp, OpCodes.Rem);  break;

        case Opcodes.LT:   EmitCmp(il, stk, ref vsp, OpCodes.Clt);  break;
        case Opcodes.GT:   EmitCmp(il, stk, ref vsp, OpCodes.Cgt);  break;
        case Opcodes.LTE:  EmitCmpNot(il, stk, ref vsp, OpCodes.Cgt); break;
        case Opcodes.GTE:  EmitCmpNot(il, stk, ref vsp, OpCodes.Clt); break;

        case Opcodes.UnaryNeg:
        {
          var l = stk[vsp - 1];
          il.Emit(OpCodes.Ldloc, l);
          il.Emit(OpCodes.Neg);
          il.Emit(OpCodes.Stloc, l);
          break;
        }

        case Opcodes.JumpZ:
        {
          var cond  = stk[--vsp];
          int t = JumpTarget(instr);
          label_vsp[t] = vsp;  // condition popped; record vsp for the target
          il.Emit(OpCodes.Ldloc, cond);
          il.Emit(OpCodes.Conv_I4);
          il.Emit(OpCodes.Brfalse, labels[t]);
          break;
        }

        case Opcodes.Jump:
        {
          int t = JumpTarget(instr);
          label_vsp[t] = vsp;
          il.Emit(OpCodes.Br, labels[t]);
          break;
        }

        case Opcodes.CallLocal:
        {
          // Self-recursive call validated above; arg is top of stack
          var arg_slot = stk[vsp - 1];
          il.Emit(OpCodes.Ldsfld, _slots_field);
          il.Emit(OpCodes.Ldc_I4, slot);
          il.Emit(OpCodes.Ldelem_Ref);
          il.Emit(OpCodes.Castclass, typeof(Func<double, double>));
          il.Emit(OpCodes.Ldloc, arg_slot);
          il.Emit(OpCodes.Call, _invoke1);
          il.Emit(OpCodes.Stloc, arg_slot);
          // vsp unchanged: pop 1 arg, push 1 result
          break;
        }

        case Opcodes.Return:
          il.Emit(OpCodes.Ldloc, stk[vsp - 1]);
          il.Emit(OpCodes.Ret);
          break;
      }
    }

    return (Func<double, double>)dm.CreateDelegate(typeof(Func<double, double>));
  }

  // ---------------------------------------------------------------
  // IL helpers
  // ---------------------------------------------------------------

  static void EmitBinOp(ILGenerator il, LocalBuilder[] stk, ref int vsp, OpCode op)
  {
    var r = stk[--vsp];
    var l = stk[vsp - 1];
    il.Emit(OpCodes.Ldloc, l);
    il.Emit(OpCodes.Ldloc, r);
    il.Emit(op);
    il.Emit(OpCodes.Stloc, l);
  }

  // Emit comparison: result = (l <cmp> r) ? 1.0 : 0.0
  static void EmitCmp(ILGenerator il, LocalBuilder[] stk, ref int vsp, OpCode cmp_op)
  {
    var r = stk[--vsp];
    var l = stk[vsp - 1];
    il.Emit(OpCodes.Ldloc, l);
    il.Emit(OpCodes.Ldloc, r);
    il.Emit(cmp_op);
    il.Emit(OpCodes.Conv_R8);
    il.Emit(OpCodes.Stloc, l);
  }

  // Emit negated comparison: result = !(l <cmp> r) ? 1.0 : 0.0
  static void EmitCmpNot(ILGenerator il, LocalBuilder[] stk, ref int vsp, OpCode cmp_op)
  {
    var r = stk[--vsp];
    var l = stk[vsp - 1];
    il.Emit(OpCodes.Ldloc, l);
    il.Emit(OpCodes.Ldloc, r);
    il.Emit(cmp_op);
    il.Emit(OpCodes.Ldc_I4_0);
    il.Emit(OpCodes.Ceq);
    il.Emit(OpCodes.Conv_R8);
    il.Emit(OpCodes.Stloc, l);
  }
}

}
#endif
