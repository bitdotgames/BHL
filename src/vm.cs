//#define DEBUG_REFS
using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

public class VM
{
  public class Frame
  {
    public FastStack<Val> stack;
    public List<Val> locals;
    public uint return_ip;

    public Frame(uint ip)
    {
      stack = new FastStack<Val>(32);
      locals = new List<Val>();
      return_ip = ip;
    }

    public void Clear()
    {
      for(int i=locals.Count;i-- > 0;)
      {
        var val = locals[i];
        val.RefMod(RefOp.USR_DEC | RefOp.DEC);
      }
      locals.Clear();
      while(stack.Count > 0)
        PopValue();
    }

    public void SetLocal(int idx, Val v)
    {
      if(locals[idx] != null)
      {
        var prev = locals[idx];
        for(int i=0;i<prev._refs;++i)
        {
          v.RefMod(RefOp.USR_INC);
          prev.RefMod(RefOp.USR_DEC);
        }
        prev.ValueCopyFrom(v);
      }
      else
      {
        v.RefMod(RefOp.INC | RefOp.USR_INC);
        locals[idx] = v;
      }
    }

    public Val GetLocal(int idx)
    {
      return locals[idx];
    }

    public void AddLocal(Val v)
    {
      v.RefMod(RefOp.INC | RefOp.USR_INC);
      locals.Add(v);
    }

    public Val PopValue()
    {
      var val = stack.PopFast();
      val.RefMod(RefOp.USR_DEC_NO_DEL | RefOp.DEC);
      return val;
    }

    public void PushValue(Val v)
    {
      v.RefMod(RefOp.INC | RefOp.USR_INC);
      stack.Push(v);
    }

    public Val PeekValue()
    {
      return stack.Peek();
    }
  }

  uint ip;
  IInstruction instruction;
  List<Const> constants;
  Dictionary<string, uint> func2ip;

  byte[] bytecode;

  BaseScope symbols;
  public BaseScope Symbols {
    get {
      return symbols;
    }
  }

  FastStack<Val> stack = new FastStack<Val>(256);
  public FastStack<Val> Stack {
    get {
      return stack;
    }
  }

  FastStack<Frame> frames = new FastStack<Frame>(256);

  public VM(BaseScope symbols, byte[] bytecode, List<Const> constants, Dictionary<string, uint> func2ip)
  {
    this.symbols = symbols;
    this.bytecode = bytecode;
    this.constants = constants;
    this.func2ip = func2ip;
  }

  public bool TryPushFunc(string func)
  {
    if(frames.Count > 0)
      return false;
    uint ip;
    if(!func2ip.TryGetValue(func, out ip))
      return false;
    var fr = new Frame(ip);
    frames.Push(fr);
    this.ip = ip;
    return true;
  }

  public BHS Execute(ref uint ip, FastStack<Frame> frames, Frame curr_frame, ref IInstruction instruction, uint max_ip = 0)
  { 
    while(frames.Count > 0 || ip < max_ip)
    {
      //Console.WriteLine("TICK " + frames.Count + " " + ip + " " + max_ip);
      var status = BHS.SUCCESS;

      //NOTE: if there's an active instruction it has priority over simple 'code following' via ip
      if(instruction != null)
      {
        status = instruction.Tick(this, curr_frame);
        if(status == BHS.RUNNING || status == BHS.FAILURE)
        {
          //Console.WriteLine("EXECUTOR " + status);
          return status;
        }
        else
        {
          instruction = null;
          ++ip;
        }
      }

      {
        var opcode = (Opcodes)bytecode[ip];
        //Console.WriteLine("OP " + opcode + " " + ip);
        switch(opcode)
        {
          case Opcodes.Constant:
            {
              int const_idx = (int)Bytecode.Decode(bytecode, ref ip);

              if(const_idx >= constants.Count)
                throw new Exception("Index out of constants: " + const_idx);

              var cn = constants[const_idx];
              curr_frame.PushValue(cn.ToVal());
            }
            break;
          case Opcodes.TypeCast:
            {
              uint cast_type = Bytecode.Decode(bytecode, ref ip);
              if(cast_type == SymbolTable.symb_string.name.n)
                curr_frame.PushValue(Val.NewStr(curr_frame.PopValue().num.ToString()));
              else if(cast_type == SymbolTable.symb_int.name.n)
                curr_frame.PushValue(Val.NewNum(curr_frame.PopValue().num));
              else
                throw new Exception("Not supported typecast type: " + cast_type);
            }
            break;
          case Opcodes.Add:
          case Opcodes.Sub:
          case Opcodes.Div:
          case Opcodes.Mod:
          case Opcodes.Mul:
          case Opcodes.And:
          case Opcodes.Or:
          case Opcodes.BitAnd:
          case Opcodes.BitOr:
          case Opcodes.Equal:
          case Opcodes.NotEqual:
          case Opcodes.Greater:
          case Opcodes.Less:
          case Opcodes.GreaterOrEqual:
          case Opcodes.LessOrEqual:
            {
              ExecuteBinaryOperation(opcode, curr_frame);
            }
            break;
          case Opcodes.UnaryNot:
          case Opcodes.UnaryNeg:
            {
              ExecuteUnaryOperation(opcode, curr_frame);
            }
            break;
          case Opcodes.SetVar:
          case Opcodes.GetVar:
            {
              ExecuteVarGetSet(opcode, curr_frame, bytecode, ref ip);
            }
            break;
          case Opcodes.SetMVar:
          case Opcodes.GetMVar:
            {
              ExecuteMVarGetSet(opcode, curr_frame, symbols, bytecode, ref ip);
            }
            break;
          case Opcodes.Return:
            {
              curr_frame.Clear();
              frames.PopFast();
              if(frames.Count > 0)
              {
                curr_frame = frames.Peek();
                ip = curr_frame.return_ip;
              }
            }
            break;
          case Opcodes.ReturnVal:
            {
              var ret_val = curr_frame.stack.PopFast();
              curr_frame.Clear();
              frames.PopFast();
              if(frames.Count > 0)
              {
                curr_frame = frames.Peek();
                ip = curr_frame.return_ip;
                curr_frame.stack.Push(ret_val);
              }
              else
                stack.Push(ret_val);
            }
            break;
          case Opcodes.FuncCall:
            {
              byte func_call_type = (byte)Bytecode.Decode(bytecode, ref ip);

              if(func_call_type == 0)
              {
                uint func_ip = Bytecode.Decode(bytecode, ref ip);

                var fr = new Frame(func_ip);

                uint args_bits = Bytecode.Decode(bytecode, ref ip); 
                var args_info = new FuncArgsInfo(args_bits);
                for(int i = 0; i < args_info.CountArgs(); ++i)
                  fr.PushValue(curr_frame.PopValue().ValueClone());

                //let's remember last ip
                curr_frame.return_ip = ip;
                frames.Push(fr);
                curr_frame = frames.Peek();
                ip = func_ip;
                //NOTE: using continue instead of break since we 
                //      don't want to increment ip 
                continue;
              }
              else if(func_call_type == 1)
              {
                int func_idx = (int)Bytecode.Decode(bytecode, ref ip);
                var func_symb = symbols.GetMembers()[func_idx] as VM_FuncBindSymbol;

                uint args_bits = Bytecode.Decode(bytecode, ref ip); 
                var args_info = new FuncArgsInfo(args_bits);
                for(int i = 0; i < args_info.CountArgs(); ++i)
                  curr_frame.PushValue(curr_frame.PopValue().ValueClone());

                var sub_instruction = func_symb.cb(this, curr_frame);
                if(sub_instruction != null)
                  AttachInstruction(ref instruction, sub_instruction);
                //NOTE: checking if new instruction was added and if so executing it immediately
                if(instruction != null)
                  status = instruction.Tick(this, curr_frame);
              }
            }
            break;
          case Opcodes.MethodCall:
            {
              uint class_type = Bytecode.Decode(bytecode, ref ip);
              var class_symb = symbols.Resolve(class_type) as ClassSymbol;
              if(class_symb == null)
                throw new Exception("Class type not found: " + class_type);

              int method_offset = (int)Bytecode.Decode(bytecode, ref ip);
              ExecuteClassMethod(class_symb, method_offset, curr_frame);
            }
            break;
          case Opcodes.Jump:
            {
              uint offset = Bytecode.Decode(bytecode, ref ip);
              ip = ip + offset;
            }
            break;
          case Opcodes.LoopJump:
            {
              uint offset = Bytecode.Decode(bytecode, ref ip);
              ip = ip - offset;
            }
            break;
          case Opcodes.CondJump:
            {
              //we need to jump only in case of false
              if(curr_frame.PopValue().bval == false)
              {
                uint offset = Bytecode.Decode(bytecode, ref ip);
                ip = ip + offset;
              }
              else
                ++ip;
            }
            break;
          case Opcodes.DefArg:
            {
              uint arg = Bytecode.Decode(bytecode, ref ip);
              if(curr_frame.stack.Count > 0)
                ip = ip + arg;
            }
            break;
          case Opcodes.PushBlock:
            {
              VisitBlock(ref ip, curr_frame, ref instruction);
            }
            break;
          case Opcodes.PopBlock:
            {
              //TODO: add scope cleaning stuff later here
            }
            break;
          case Opcodes.ArrNew:
            {
              curr_frame.PushValue(Val.NewObj(ValList.New()));
            }
            break;
          default:
            throw new Exception("Unsupported opcode: " + opcode);
        }
      }

      if(status == BHS.RUNNING || status == BHS.FAILURE)
        return status;
      else if(status == BHS.SUCCESS)
        ++ip;
    }

    return BHS.SUCCESS;
  }

  static void AttachInstruction(ref IInstruction instruction, IInstruction candidate)
  {
    if(instruction != null)
    {
      if(instruction is IMultiInstruction mi)
        mi.Attach(candidate);
      else
        throw new Exception("Can't attach to current instruction");
    }
    else
      instruction = candidate;
  }

  IInstruction VisitBlock(ref uint ip, Frame curr_frame, ref IInstruction instruction)
  {
    var type = (EnumBlock)Bytecode.Decode(bytecode, ref ip);
    uint size = Bytecode.Decode(bytecode, ref ip);

    if(type == EnumBlock.PARAL) 
    {
      var paral = new ParalInstruction();
      AttachInstruction(ref instruction, paral);
      uint tmp_ip = ip;
      while(tmp_ip < (ip + size))
      {
        ++tmp_ip;
        var opcode = (Opcodes)bytecode[tmp_ip]; 
        if(opcode != Opcodes.PushBlock)
          throw new Exception("Expected PushBlock got " + opcode);
        IInstruction dummy = null;
        var sub = VisitBlock(ref tmp_ip, curr_frame, ref dummy);
        paral.Attach(sub);
        ++tmp_ip;
        opcode = (Opcodes)bytecode[tmp_ip]; 
        if(opcode != Opcodes.PopBlock)
          throw new Exception("Expected PopBlock got " + opcode);
      }
      ip += size;
      return paral;
    }
    else if(type == EnumBlock.SEQ)
    {
      var seq = new SeqInstruction(ip + 1, ip + size);
      AttachInstruction(ref instruction, seq);
      ip += size;
      return seq;
    }
    else
      throw new Exception("Unsupported block type: " + type);
  }

  public BHS Tick()
  {
    if(frames.Count == 0)
      return BHS.SUCCESS;
    var curr_frame = frames.Peek();
    return Execute(ref ip, frames, curr_frame, ref instruction, 0);
  }

  static void ExecuteVarGetSet(Opcodes op, Frame curr_frame, byte[] bytecode, ref uint ip)
  {
    int local_idx = (int)Bytecode.Decode(bytecode, ref ip);
    
    switch(op)
    {
      case Opcodes.SetVar:
        //TODO: code below must not guess, it must follow dumb logic
        if(local_idx < curr_frame.locals.Count)
        {
          if(curr_frame.locals[local_idx] != null)
            curr_frame.locals[local_idx].Release();
          curr_frame.locals[local_idx] = curr_frame.stack.PopFast();
        }
        else 
        {
          if(curr_frame.stack.Count > 0)
            curr_frame.locals.Add(curr_frame.stack.PopFast());
          else
            curr_frame.AddLocal(Val.New());
        }
      break;
      case Opcodes.GetVar:
        if(local_idx >= curr_frame.locals.Count)
          throw new Exception("Var index out of locals: " + local_idx);
        curr_frame.PushValue(curr_frame.GetLocal(local_idx));
      break;
      default:
        throw new Exception("Unsupported opcode: " + op);
    }
  }

  static void ExecuteMVarGetSet(Opcodes op, Frame curr_frame, BaseScope symbols, byte[] bytecode, ref uint ip)
  {
    uint class_type = Bytecode.Decode(bytecode, ref ip);

    var class_symb = symbols.Resolve(class_type) as ClassSymbol;
    if(class_symb == null)
      throw new Exception("Class type not found: " + class_type);

    int local_idx = (int)Bytecode.Decode(bytecode, ref ip);
    
    switch(op)
    {
      case Opcodes.GetMVar:
      {
        if(class_symb is GenericArrayTypeSymbol)
        {
          if(local_idx == GenericArrayTypeSymbol.VM_CountIdx)
          {
            var arr = curr_frame.PopValue();
            var lst = AsList(arr);
            curr_frame.PushValue(Val.NewNum(lst.Count));
            //NOTE: this can be an operation for the temp. array,
            //      we need to try del the array if so
            lst.TryDel();
          }
          else
            throw new Exception("Not supported member idx: " + local_idx);
        }
        else
          throw new Exception("Class not supported: " + class_symb.name);
      }
        //curr_frame.PushValue(curr_frame.GetLocal(local_idx));
      break;
      default:
        throw new Exception("Unsupported opcode: " + op);
    }
  }

  static void ExecuteUnaryOperation(Opcodes op, Frame curr_frame)
  {
    var operand = curr_frame.PopValue();
    switch(op)
    {
      case Opcodes.UnaryNot:
        curr_frame.PushValue(Val.NewBool(operand.num != 1));
      break;
      case Opcodes.UnaryNeg:
        curr_frame.PushValue(Val.NewNum(operand.num * -1));
      break;
    }
  }

  static ValList AsList(Val arr)
  {
    var lst = arr.obj as ValList;
    if(lst == null)
      throw new UserError("Not a ValList: " + (arr.obj != null ? arr.obj.GetType().Name : ""+arr));
    return lst;
  }

  static void ExecuteClassMethod(ClassSymbol symb, int method, Frame curr_frame)
  {
    //TODO: stuff below must be defined inside the symbol
    if(symb is GenericArrayTypeSymbol)
    {
      if(method == GenericArrayTypeSymbol.VM_AddIdx)
      {
        var val = curr_frame.PopValue().ValueClone();
        var arr = curr_frame.PopValue();
        var lst = AsList(arr);
        lst.Add(val);
        //NOTE: this can be an operation for the temp. array,
        //      we need to try del the array if so
        lst.TryDel();
      }
      else if(method == GenericArrayTypeSymbol.VM_RemoveIdx)
      {
        int idx = (int)curr_frame.PopValue().num;
        var arr = curr_frame.PopValue();
        var lst = AsList(arr);
        lst.RemoveAt(idx); 
        //NOTE: this can be an operation for the temp. array,
        //      we need to try del the array if so
        lst.TryDel();
      }
      else if(method == GenericArrayTypeSymbol.VM_SetIdx)
      {
        int idx = (int)curr_frame.PopValue().num;
        var arr = curr_frame.PopValue();
        var val = curr_frame.PopValue().ValueClone();
        var lst = AsList(arr);
        lst[idx] = val;
        //NOTE: this can be an operation for the temp. array,
        //      we need to try del the array if so
        lst.TryDel();
      }
      else if(method == GenericArrayTypeSymbol.VM_AtIdx)
      {
        int idx = (int)curr_frame.PopValue().num;
        var arr = curr_frame.PopValue();
        var lst = AsList(arr);
        var res = lst[idx]; 
        curr_frame.PushValue(res);
        //NOTE: this can be an operation for the temp. array,
        //      we need to try del the array if so
        lst.TryDel();
      }
      else 
        throw new Exception("Not supported method: " + method);
    }
    else
      throw new Exception("Not supported class: " + symb.name);
  }

  static void ExecuteBinaryOperation(Opcodes op, Frame curr_frame)
  {
    var r_operand = curr_frame.PopValue();
    var l_operand = curr_frame.PopValue();

    switch(op)
    {
      case Opcodes.Add:
        if((r_operand._type == Val.STRING) && (l_operand._type == Val.STRING))
          curr_frame.PushValue(Val.NewStr((string)l_operand._obj + (string)r_operand._obj));
        else
          curr_frame.PushValue(Val.NewNum(l_operand._num + r_operand._num));
      break;
      case Opcodes.Sub:
        curr_frame.PushValue(Val.NewNum(l_operand._num - r_operand._num));
      break;
      case Opcodes.Div:
        curr_frame.PushValue(Val.NewNum(l_operand._num / r_operand._num));
      break;
      case Opcodes.Mul:
        curr_frame.PushValue(Val.NewNum(l_operand._num * r_operand._num));
      break;
      case Opcodes.Equal:
        curr_frame.PushValue(Val.NewBool(l_operand._num == r_operand._num));
      break;
      case Opcodes.NotEqual:
        curr_frame.PushValue(Val.NewBool(l_operand._num != r_operand._num));
      break;
      case Opcodes.Greater:
        curr_frame.PushValue(Val.NewBool(l_operand._num > r_operand._num));
      break;
      case Opcodes.Less:
        curr_frame.PushValue(Val.NewBool(l_operand._num < r_operand._num));
      break;
      case Opcodes.GreaterOrEqual:
        curr_frame.PushValue(Val.NewBool(l_operand._num >= r_operand._num));
      break;
      case Opcodes.LessOrEqual:
        curr_frame.PushValue(Val.NewBool(l_operand._num <= r_operand._num));
      break;
      case Opcodes.And:
        curr_frame.PushValue(Val.NewBool(l_operand._num == 1 && r_operand._num == 1));
      break;
      case Opcodes.Or:
        curr_frame.PushValue(Val.NewBool(l_operand._num == 1 || r_operand._num == 1));
      break;
      case Opcodes.BitAnd:
        curr_frame.PushValue(Val.NewNum((int)l_operand._num & (int)r_operand._num));
      break;
      case Opcodes.BitOr:
        curr_frame.PushValue(Val.NewNum((int)l_operand._num | (int)r_operand._num));
      break;
      case Opcodes.Mod:
        curr_frame.PushValue(Val.NewNum((int)l_operand._num % (int)r_operand._num));
      break;
    }
  }

  public Val PopValue()
  {
    var val = stack.PopFast();
    val.RefMod(RefOp.USR_DEC_NO_DEL | RefOp.DEC);
    return val;
  }

  public void PushValue(Val v)
  {
    v.RefMod(RefOp.INC | RefOp.USR_INC);
    stack.Push(v);
  }

  public Val PeekValue()
  {
    return stack.Peek();
  }

  public void ShowFullStack()
  {
    Console.WriteLine(" VM STACK :");
    for(int i=0;i<stack.Count;++i)
    {
      Console.WriteLine("\t" + stack[i]);
    }
  }
}

public interface IInstruction
{
  BHS Tick(VM vm, VM.Frame fr);
}

public interface IMultiInstruction : IInstruction
{
  void Attach(IInstruction ex);
}

class CoroutineSuspend : IInstruction
{
  public BHS Tick(VM vm, VM.Frame fr)
  {
    //Console.WriteLine("SUSPEND");
    return BHS.RUNNING;
  }
}

class CoroutineYield : IInstruction
{
  byte c = 0;

  public BHS Tick(VM vm, VM.Frame fr)
  {
    //Console.WriteLine("YIELD");
    c++;
    return c == 1 ? BHS.RUNNING : BHS.SUCCESS; 
  }
}

public class SeqInstruction : IInstruction
{
  public uint ip;
  public uint max_ip;
  public FastStack<VM.Frame> frames = new FastStack<VM.Frame>(256);
  public IInstruction instruction;

  public SeqInstruction(uint ip, uint max_ip)
  {
    //Console.WriteLine("NEW SEQ " + ip + " " + max_ip);
    this.ip = ip;
    this.max_ip = max_ip;
  }

  public BHS Tick(VM vm, VM.Frame curr_frame)
  {
    //Console.WriteLine("TICK SEQ " + ip);
    return vm.Execute(ref ip, frames, curr_frame, ref instruction, max_ip + 1);
  }
}

public class ParalInstruction : IMultiInstruction
{
  public List<IInstruction> children = new List<IInstruction>();

  public ParalInstruction()
  {
    //Console.WriteLine("NEW PARAL");
  }

  public BHS Tick(VM vm, VM.Frame curr_frame)
  {
    for(int i=0;i<children.Count;++i)
    {
      var current = children[i];
      var status = current.Tick(vm, curr_frame);
      //Console.WriteLine("CHILD " + i + " " + status + " " + current.GetType().Name);
      if(status != BHS.RUNNING)
        return status;
    }

    return BHS.RUNNING;
  }

  public void Attach(IInstruction inst)
  {
    //Console.WriteLine("ADD CHILD");
    children.Add(inst);
  }
}

public interface IValRefcounted
{
  void Retain();
  void Release(bool can_del = true);
  bool TryDel();
}

public class Val
{
  public const byte NONE      = 0;
  public const byte NUMBER    = 1;
  public const byte BOOL      = 2;
  public const byte STRING    = 3;
  public const byte OBJ       = 4;
  public const byte NIL       = 5;

  public bool IsEmpty { get { return type == NONE; } }

  public byte type { get { return _type; } }

  public double num {
    get {
      return _num;
    }
    set {
      SetNum(value);
    }
  }

  public string str {
    get {
      return (string)_obj;
    }
    set {
      SetStr(value);
    }
  }

  public object obj {
    get {
      return _obj;
    }
    set {
      SetObj(value);
    }
  }

  public bool bval {
    get {
      return _num == 1;
    }
    set {
      SetBool(value);
    }
  }

  //NOTE: below members are semi-public, one can use them for 
  //      fast access or non-allocating storage of structs(e.g vectors, quaternions)
  //NOTE: -1 means it's in released state
  public int _refs;
  public byte _type;
  public double _num;
  public object _obj;

  static Queue<Val> pool = new Queue<Val>(64);
  static int pool_miss;
  static int pool_hit;

  //NOTE: use New() instead
  internal Val()
  {}

  static public Val New()
  {
    Val dv;
    if(pool.Count == 0)
    {
      ++pool_miss;
      dv = new Val();
#if DEBUG_REFS
      Console.WriteLine("NEW: " + dv.GetHashCode()/* + " " + Environment.StackTrace*/);
#endif
    }
    else
    {
      ++pool_hit;
      dv = pool.Dequeue();
#if DEBUG_REFS
      Console.WriteLine("HIT: " + dv.GetHashCode()/* + " " + Environment.StackTrace*/);
#endif
      //NOTE: Val is Reset here instead of Del, see notes below 
      dv.Reset();
      dv._refs = 0;
    }
    return dv;
  }

  static public void Del(Val dv)
  {
    //NOTE: we don't Reset Val immediately, giving a caller
    //      a chance to access its properties
    if(dv._refs != 0)
      throw new Exception("Deleting invalid object, refs " + dv._refs);
    dv._refs = -1;

    //NOTE: we'd like to ensure there are some spare values before 
    //      the released one, this way it will be possible to call 
    //      safely New() right after Del() since it won't return
    //      just deleted object
    if(pool.Count == 0)
    {
      ++pool_miss;
      var tmp = new Val(); 
      pool.Enqueue(tmp);
#if DEBUG_REFS
      Console.WriteLine("NSP: " + tmp.GetHashCode()/* + " " + Environment.StackTrace*/);
#endif
    }
    pool.Enqueue(dv);
    if(pool.Count > pool_miss)
      throw new Exception("Unbalanced New/Del " + pool.Count + " " + pool_miss);
  }

  //For proper assignments, like: dst = src (taking into account ref.counting)
  static public Val Assign(Val dst, Val src)
  {
    if(dst != null)
      dst.RefMod(RefOp.DEC | RefOp.USR_DEC);
    if(src != null) 
      src.RefMod(RefOp.INC | RefOp.USR_INC);
    return src;
  }

  //NOTE: refcount is not reset
  void Reset()
  {
    _type = NONE;
    _num = 0;
    _obj = null;
  }

  public void ValueCopyFrom(Val dv)
  {
    _type = dv._type;
    _num = dv._num;
    _obj = dv._obj;
  }

  public Val ValueClone()
  {
    Val dv = New();
    dv.ValueCopyFrom(this);
    return dv;
  }

  //NOTE: see RefOp for constants
  public void RefMod(int op)
  {
    if(_obj != null && _obj is IValRefcounted _refc)
    {
      if((op & RefOp.USR_INC) != 0)
      {
        _refc.Retain();
      }
      else if((op & RefOp.USR_DEC) != 0)
      {
        _refc.Release(true);
      }
      else if((op & RefOp.USR_DEC_NO_DEL) != 0)
      {
        _refc.Release(false);
      }
      else if((op & RefOp.USR_TRY_DEL) != 0)
      {
        _refc.TryDel();
      }
    }

    if((op & RefOp.INC) != 0)
    {
      if(_refs == -1)
        throw new Exception("Invalid state");

      ++_refs;
#if DEBUG_REFS
      Console.WriteLine("INC: " + _refs + " " + this + " " + GetHashCode()/* + " " + Environment.StackTrace*/);
#endif
    } 
    else if((op & RefOp.DEC) != 0)
    {
      if(_refs == -1)
        throw new Exception("Invalid state");
      else if(_refs == 0)
        throw new Exception("Double free");

      --_refs;
#if DEBUG_REFS
      Console.WriteLine("DEC: " + _refs + " " + this + " " + GetHashCode()/* + " " + Environment.StackTrace*/);
#endif

      if(_refs == 0)
        Del(this);
    }
    else if((op & RefOp.DEC_NO_DEL) != 0)
    {
      if(_refs == -1)
        throw new Exception("Invalid state");
      else if(_refs == 0)
        throw new Exception("Double free");

      --_refs;
#if DEBUG_REFS
      Console.WriteLine("DCN: " + _refs + " " + this + " " + GetHashCode()/* + " " + Environment.StackTrace*/);
#endif
    }
    else if((op & RefOp.TRY_DEL) != 0)
    {
#if DEBUG_REFS
      Console.WriteLine("TDL: " + _refs + " " + this + " " + GetHashCode()/* + " " + Environment.StackTrace*/);
#endif

      if(_refs == 0)
        Del(this);
    }
  }

  public void Retain()
  {
    RefMod(RefOp.USR_INC | RefOp.INC);
  }

  public void Release()
  {
    RefMod(RefOp.USR_DEC | RefOp.DEC);
  }

  public void TryDel()
  {
    RefMod(RefOp.TRY_DEL);
  }

  public void RetainReference()
  {
    RefMod(RefOp.INC);
  }

  public void ReleaseReference()
  {
    RefMod(RefOp.DEC);
  }

  static public Val NewStr(string s)
  {
    Val dv = New();
    dv.SetStr(s);
    return dv;
  }

  public void SetStr(string s)
  {
    Reset();
    _type = STRING;
    _obj = s;
  }

  static public Val NewNum(int n)
  {
    Val dv = New();
    dv.SetNum(n);
    return dv;
  }

  public void SetNum(int n)
  {
    Reset();
    _type = NUMBER;
    _num = n;
  }

  static public Val NewNum(double n)
  {
    Val dv = New();
    dv.SetNum(n);
    return dv;
  }

  public void SetNum(double n)
  {
    Reset();
    _type = NUMBER;
    _num = n;
  }

  static public Val NewBool(bool b)
  {
    Val dv = New();
    dv.SetBool(b);
    return dv;
  }

  public void SetBool(bool b)
  {
    Reset();
    _type = BOOL;
    _num = b ? 1.0f : 0.0f;
  }

  static public Val NewObj(object o)
  {
    Val dv = New();
    dv.SetObj(o);
    return dv;
  }

  public void SetObj(object o)
  {
    Reset();
    _type = o == null ? NIL : OBJ;
    _obj = o;
  }

  static public Val NewNil()
  {
    Val dv = New();
    dv.SetNil();
    return dv;
  }

  public void SetNil()
  {
    Reset();
    _type = NIL;
  }

  public bool IsEqual(Val o)
  {
    bool res =
      _type == o.type &&
      _num == o._num &&
      _obj == o._obj
      ;

    return res;
  }

  public override string ToString() 
  {
    string str = "";
    if(type == NUMBER)
      str = _num + ":<NUMBER>";
    else if(type == BOOL)
      str = bval + ":<BOOL>";
    else if(type == STRING)
      str = this.str + ":<STRING>";
    else if(type == OBJ)
      str = _obj.GetType().Name + ":<OBJ>";
    else if(type == NIL)
      str = "<NIL>";
    else if(type == NONE)
      str = "<NONE>";
    else
      str = "Val: type:"+type;

    return str;// + " " + GetHashCode();//for extra debug
  }

  public object ToAny() 
  {
    if(type == NUMBER)
      return (object)_num;
    else if(type == BOOL)
      return (object)bval;
    else if(type == STRING)
      return (string)_obj;
    else if(type == OBJ)
      return _obj;
    else if(type == NIL)
      return null;
    else
      throw new Exception("ToAny(): please support type: " + type);
  }

  static public void PoolAlloc(int num)
  {
    for(int i=0;i<num;++i)
    {
      ++pool_miss;
      var tmp = new Val(); 
      pool.Enqueue(tmp);
    }
  }

  static public void PoolClear()
  {
    pool_hit = 0;
    pool_miss = 0;
    pool.Clear();
  }

  static public string PoolDump()
  {
    string res = "=== Val POOL ===\n";
    res += "total:" + PoolCount + " free:" + PoolCountFree + "\n";
    var dvs = new Val[pool.Count];
    pool.CopyTo(dvs, 0);
    for(int i=dvs.Length;i-- > 0;)
    {
      var v = dvs[i];
      res += v + " (refs:" + v._refs + ") " + v.GetHashCode() + "\n"; 
    }
    return res;
  }

  static public int PoolHits
  {
    get { return pool_hit; } 
  }

  static public int PoolMisses
  {
    get { return pool_miss; } 
  }

  static public int PoolCount
  {
    get { return pool_miss; }
  }

  static public int PoolCountFree
  {
    get { return pool.Count; }
  }
}

public class ValList : IList<Val>, IValRefcounted
{
  //NOTE: exposed to allow manipulations like Reverse(). Use with caution.
  public readonly List<Val> lst = new List<Val>();

  //NOTE: -1 means it's in released state,
  //      public only for inspection
  public int refs;

  //////////////////IList//////////////////

  public int Count { get { return lst.Count; } }

  public bool IsFixedSize { get { return false; } }
  public bool IsReadOnly { get { return false; } }
  public bool IsSynchronized { get { throw new NotImplementedException(); } }
  public object SyncRoot { get { throw new NotImplementedException(); } }

  public static implicit operator Val(ValList lst)
  {
    return Val.NewObj(lst);
  }

  public void Add(Val dv)
  {
    dv.RefMod(RefOp.INC | RefOp.USR_INC);
    lst.Add(dv);
  }

  public void AddRange(IList<Val> list)
  {
    for(int i=0; i<list.Count; ++i)
      Add(list[i]);
  }

  public void RemoveAt(int idx)
  {
    var dv = lst[idx];
    dv.RefMod(RefOp.DEC | RefOp.USR_DEC);
    lst.RemoveAt(idx); 
  }

  public void Clear()
  {
    for(int i=0;i<Count;++i)
      lst[i].RefMod(RefOp.USR_DEC | RefOp.DEC);

    lst.Clear();
  }

  public Val this[int i]
  {
    get {
      return lst[i];
    }
    set {
      var prev = lst[i];
      prev.RefMod(RefOp.DEC | RefOp.USR_DEC);
      value.RefMod(RefOp.INC | RefOp.USR_INC);
      lst[i] = value;
    }
  }

  public int IndexOf(Val dv)
  {
    return lst.IndexOf(dv);
  }

  public bool Contains(Val dv)
  {
    return IndexOf(dv) >= 0;
  }

  public bool Remove(Val dv)
  {
    int idx = IndexOf(dv);
    if(idx < 0)
      return false;
    RemoveAt(idx);
    return true;
  }

  public void CopyTo(Val[] arr, int len)
  {
    throw new NotImplementedException();
  }

  public void Insert(int pos, Val o)
  {
    throw new NotImplementedException();
  }

  public IEnumerator<Val> GetEnumerator()
  {
    throw new NotImplementedException();
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  ///////////////////////////////////////

  public void Retain()
  {
    if(refs == -1)
      throw new Exception("Invalid state");
    ++refs;
    //Console.WriteLine("RETAIN " + refs + " " + GetHashCode() + " " + Environment.StackTrace);
  }

  public void Release(bool can_del = true)
  {
    if(refs == -1)
      throw new Exception("Invalid state");
    if(refs == 0)
      throw new Exception("Double free");

    --refs;
    //Console.WriteLine("RELEASE " + refs + " " + GetHashCode() + " " + Environment.StackTrace);
    if(can_del)
      TryDel();
  }

  public bool TryDel()
  {
    if(refs != 0)
      return false;

    //Console.WriteLine("DEL " + GetHashCode());
    
    Del(this);

    return true;
  }

  public void CopyFrom(ValList lst)
  {
    Clear();
    for(int i=0;i<lst.Count;++i)
      Add(lst[i]);
  }

  ///////////////////////////////////////

  static public Stack<ValList> pool = new Stack<ValList>();
  static int pool_hit;
  static int pool_miss;

  //NOTE: use New() instead
  internal ValList()
  {}

  public static ValList New()
  {
    ValList lst;
    if(pool.Count == 0)
    {
      ++pool_miss;
      lst = new ValList();
    }
    else
    {
      ++pool_hit;
      lst = pool.Pop();

      if(lst.refs != -1)
        throw new Exception("Expected to be released, refs " + lst.refs);
      lst.refs = 0;
    }

    return lst;
  }

  static public void Del(ValList lst)
  {
    if(lst.refs != 0)
      throw new Exception("Freeing invalid object, refs " + lst.refs);

    lst.refs = -1;
    lst.Clear();
    pool.Push(lst);

    if(pool.Count > pool_miss)
      throw new Exception("Unbalanced New/Del");
  }

  static public void PoolClear()
  {
    pool_miss = 0;
    pool_hit = 0;
    pool.Clear();
  }

  static public int PoolHits
  {
    get { return pool_hit; } 
  }

  static public int PoolMisses
  {
    get { return pool_miss; } 
  }

  static public int PoolCount
  {
    get { return pool_miss; }
  }

  static public int PoolCountFree
  {
    get { return pool.Count; }
  }
}

public class ValDict : IValRefcounted
{
  public Dictionary<ulong, Val> vars = new Dictionary<ulong, Val>();

  //NOTE: -1 means it's in released state,
  //      public only for inspection
  public int refs;

  static public Stack<ValDict> pool = new Stack<ValDict>();
  static int pool_hit;
  static int pool_miss;

  //NOTE: use New() instead
  internal ValDict()
  {}

  public void Retain()
  {
    if(refs == -1)
      throw new Exception("Invalid state");
    ++refs;

    //Console.WriteLine("FREF INC: " + refs + " " + this.GetHashCode() + " " + Environment.StackTrace);
  }

  public void Release(bool can_del = true)
  {
    if(refs == -1)
      throw new Exception("Invalid state");
    if(refs == 0)
      throw new Exception("Double free");

    --refs;

    //Console.WriteLine("FREF DEC: " + refs + " " + this.GetHashCode() + " " + Environment.StackTrace);

    if(can_del)
      TryDel();
  }

  public bool TryDel()
  {
    if(refs != 0)
      return false;
    
    Del(this);
    return true;
  }

  public static ValDict New()
  {
    ValDict tb;
    if(pool.Count == 0)
    {
      ++pool_miss;
      tb = new ValDict();
    }
    else
    {
      ++pool_hit;
      tb = pool.Pop();

      if(tb.refs != -1)
        throw new Exception("Expected to be released, refs " + tb.refs);
      tb.refs = 0;
    }

    return tb;
  }

  static public void PoolClear()
  {
    pool_miss = 0;
    pool_hit = 0;
    pool.Clear();
  }

  static public int PoolHits
  {
    get { return pool_hit; } 
  }

  static public int PoolMisses
  {
    get { return pool_miss; } 
  }

  static public int PoolCount
  {
    get { return pool_miss; }
  }

  static public int PoolCountFree
  {
    get { return pool.Count; }
  }

  static public void Del(ValDict tb)
  {
    if(tb.refs != 0)
      throw new Exception("Freeing invalid object, refs " + tb.refs);

    tb.refs = -1;
    tb.Clear();
    pool.Push(tb);

    if(pool.Count > pool_miss)
      throw new Exception("Unbalanced New/Del");
  }

  public void Clear()
  {
    var enm = vars.GetEnumerator();
    try
    {
      while(enm.MoveNext())
      {
        var val = enm.Current.Value;
        val.RefMod(RefOp.USR_DEC | RefOp.DEC);
      }
    }
    finally
    {
      enm.Dispose();
    }

    vars.Clear();
  }

  public void Set(HashedName key, Val val)
  {
    ulong k = key.n; 
    Val prev;
    if(vars.TryGetValue(k, out prev))
    {
      for(int i=0;i<prev._refs;++i)
      {
        val.RefMod(RefOp.USR_INC);
        prev.RefMod(RefOp.USR_DEC);
      }
      prev.ValueCopyFrom(val);
      //Console.WriteLine("VAL SET2 " + prev.GetHashCode());
    }
    else
    {
      //Console.WriteLine("VAL SET1 " + val.GetHashCode());
      vars[k] = val;
      val.RefMod(RefOp.USR_INC | RefOp.INC);
    }
  }

  public bool TryGet(HashedName key, out Val val)
  {
    return vars.TryGetValue(key.n, out val);
  }

  public Val Get(HashedName key)
  {
    return vars[key.n];
  }

  public void CopyFrom(ValDict o)
  {
    var enm = o.vars.GetEnumerator();
    try
    {
      while(enm.MoveNext())
      {
        var key = enm.Current.Key;
        var val = enm.Current.Value;
        Set(key, val);
      }
    }
    finally
    {
      enm.Dispose();
    }
  }
}


} //namespace bhl
