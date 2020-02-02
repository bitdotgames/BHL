using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

public class Frame
{
  public uint ip;
  public Stack<DynVal> num_stack;
  public List<DynVal> locals;

  public Frame()
  {
    ip = 0;
    num_stack = new Stack<DynVal>();
    locals = new List<DynVal>();
  }
}

public class VM
{
  List<DynVal> constants;
  byte[] instructions;
  Dictionary<string, uint> func_buff;

  Stack<DynVal> vm_stack = new Stack<DynVal>();

  Stack<Frame> frames = new Stack<Frame>();
  Frame curr_frame;

  public VM(byte[] instructions, List<DynVal> constants, Dictionary<string, uint> func_buff)
  {
    this.instructions = instructions;
    this.constants = constants;
    this.func_buff = func_buff;
  }

  public void Exec(string func)
  {
    var fr = new Frame();
    fr.ip = func_buff[func];
    frames.Push(fr);
    Run();
  }

  public void Run()
  {
    Opcodes opcode;
    curr_frame = frames.Peek();
    while(frames.Count > 0)
    {
      opcode = (Opcodes)instructions[curr_frame.ip];

      switch(opcode)
      {
        case Opcodes.Constant:
          curr_frame.ip++;
          int const_idx = instructions[curr_frame.ip];

          if(const_idx >= constants.Count)
            throw new Exception("Index out of constant pool: " + const_idx);

          curr_frame.num_stack.Push(constants[const_idx]);
        break;
        case Opcodes.New:
          curr_frame.num_stack.Push(DynVal.NewObj(DynValList.New()));
          curr_frame.ip++;
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
        case Opcodes.Greather:
        case Opcodes.Less:
        case Opcodes.GreatherOrEqual:
        case Opcodes.LessOrEqual:
          ExecuteBinaryOperation(opcode);
        break;
        case Opcodes.UnaryNot:
        case Opcodes.UnaryNeg:
          ExecuteUnaryOperation(opcode);
        break;
        case Opcodes.SetVar:
        case Opcodes.GetVar:
          ExecuteVariablesOperation(opcode);
        break;
        case Opcodes.ReturnVal:
          var ret_val = curr_frame.num_stack.Peek();
          frames.Pop();
          if(frames.Count > 0)
          {
            curr_frame = frames.Peek();
            curr_frame.num_stack.Push(ret_val);
          }
          else
          {
            vm_stack.Push(ret_val);
          }
        break;
        case Opcodes.MethodCall:
          curr_frame.ip++;
          var builtin = instructions[curr_frame.ip];

          ExecuteBuiltInArrayFunc((BuiltInArray) builtin);
        break;
        case Opcodes.IdxGet:// move to -At- method?
          var idx = curr_frame.num_stack.Pop();
          var arr = curr_frame.num_stack.Pop();
          var lst = GetAsList(arr);

          var res = lst[(int)idx.num]; 
          curr_frame.num_stack.Push(res);
          //NOTE: this can be an operation for the temp. array,
          //      we need to try del the array if so
          lst.TryDel();
        break;
        case Opcodes.FuncCall:
          curr_frame.ip++;

          var fr = new Frame();
          fr.ip = instructions[curr_frame.ip];

          curr_frame.ip++;

          if(instructions[curr_frame.ip] != 0)//has any input variables
          {
            for(int i = 0; i < instructions[curr_frame.ip]; ++i)
              fr.num_stack.Push(curr_frame.num_stack.Pop());
          }

          frames.Push(fr);
          curr_frame = frames.Peek();
        continue;
        case Opcodes.Jump:
          curr_frame.ip++;
          curr_frame.ip = curr_frame.ip + instructions[curr_frame.ip];
        break;
        case Opcodes.LoopJump:
          curr_frame.ip++;
          curr_frame.ip = curr_frame.ip - instructions[curr_frame.ip];
        break;
        case Opcodes.CondJump:
          curr_frame.ip++;
          if(curr_frame.num_stack.Pop().bval == false)
            curr_frame.ip = curr_frame.ip + instructions[curr_frame.ip];
          curr_frame.ip++;
        continue;
      }
      curr_frame.ip++;
    }
  }

  void ExecuteVariablesOperation(Opcodes op)
  {
    curr_frame.ip++;
    int local_idx = instructions[curr_frame.ip];
    switch(op)
    {
      case Opcodes.SetVar:
        if(local_idx >= curr_frame.locals.Count)
          curr_frame.locals.Add(curr_frame.num_stack.Pop());
        else
          curr_frame.locals[local_idx] = curr_frame.num_stack.Pop();
      break;
      case Opcodes.GetVar:
        if(local_idx >= curr_frame.locals.Count)
          throw new Exception("Index out of locals pool: " + local_idx);
        curr_frame.num_stack.Push(curr_frame.locals[local_idx]);
      break;
    }
  }

  void ExecuteUnaryOperation(Opcodes op)
  {
    var operand = curr_frame.num_stack.Pop();
    switch(op)
    {
      case Opcodes.UnaryNot:
        curr_frame.num_stack.Push(DynVal.NewBool(operand._num != 1));
      break;
      case Opcodes.UnaryNeg:
        curr_frame.num_stack.Push(DynVal.NewNum(operand._num * -1));
      break;
    }
  }

  DynValList GetAsList(DynVal arr)
  {
    var lst = arr.obj as DynValList;
    if(lst == null)
      throw new UserError("Not a DynValList: " + (arr.obj != null ? arr.obj.GetType().Name : ""+arr));
    return lst;
  }

  void ExecuteBuiltInArrayFunc(BuiltInArray func)
  {
    DynVal idx;
    DynVal arr;
    DynVal val;
    DynValList lst;
    switch(func)
    {
      case BuiltInArray.Add:
        val = curr_frame.num_stack.Pop();
        arr = curr_frame.num_stack.Peek();
        lst = GetAsList(arr);

        lst.Add(val);
      break;
      case BuiltInArray.RemoveAt:
        idx = curr_frame.num_stack.Pop();
        arr = curr_frame.num_stack.Pop();
        lst = GetAsList(arr);

        lst.RemoveAt((int)idx.num); 
      break;
      case BuiltInArray.SetAt:
        idx = curr_frame.num_stack.Pop();
        arr = curr_frame.num_stack.Pop();
        val = curr_frame.num_stack.Pop();
        lst = GetAsList(arr);

        lst[(int)idx.num] = val;
      break;
      case BuiltInArray.Count:
        arr = curr_frame.num_stack.Peek();
        lst = GetAsList(arr);
        curr_frame.num_stack.Push(DynVal.NewNum(lst.Count));
      break;
      default:
        throw new Exception("Unknown method -> " + func);
    }
  }

  void ExecuteBinaryOperation(Opcodes op)
  {
    var stk = curr_frame.num_stack;

    var r_opertand = stk.Pop();
    var l_opertand = stk.Pop();

    switch(op)
    {
      case Opcodes.Add:
        if((r_opertand._type == (byte)EnumLiteral.STR) 
          &&(l_opertand._type == (byte)EnumLiteral.STR))
          stk.Push(DynVal.NewStr(l_opertand._str + r_opertand._str));
        else
          stk.Push(DynVal.NewNum(l_opertand._num + r_opertand._num));
      break;
      case Opcodes.Sub:
        stk.Push(DynVal.NewNum(l_opertand._num - r_opertand._num));
      break;
      case Opcodes.Div:
        stk.Push(DynVal.NewNum(l_opertand._num / r_opertand._num));
      break;
      case Opcodes.Mul:
        stk.Push(DynVal.NewNum(l_opertand._num * r_opertand._num));
      break;
      case Opcodes.Equal:
        stk.Push(DynVal.NewBool(l_opertand._num == r_opertand._num));
      break;
      case Opcodes.NotEqual:
        stk.Push(DynVal.NewBool(l_opertand._num != r_opertand._num));
      break;
      case Opcodes.Greather:
        stk.Push(DynVal.NewBool(l_opertand._num > r_opertand._num));
      break;
      case Opcodes.Less:
        stk.Push(DynVal.NewBool(l_opertand._num < r_opertand._num));
      break;
      case Opcodes.GreatherOrEqual:
        stk.Push(DynVal.NewBool(l_opertand._num >= r_opertand._num));
      break;
      case Opcodes.LessOrEqual:
        stk.Push(DynVal.NewBool(l_opertand._num <= r_opertand._num));
      break;
      case Opcodes.And:
        stk.Push(DynVal.NewBool(l_opertand._num == 1 && r_opertand._num == 1));
      break;
      case Opcodes.Or:
        stk.Push(DynVal.NewBool(l_opertand._num == 1 || r_opertand._num == 1));
      break;
      case Opcodes.BitAnd:
        stk.Push(DynVal.NewNum((int)l_opertand._num & (int)r_opertand._num));
      break;
      case Opcodes.BitOr:
        stk.Push(DynVal.NewNum((int)l_opertand._num | (int)r_opertand._num));
      break;
      case Opcodes.Mod:
        stk.Push(DynVal.NewNum((int)l_opertand._num % (int)r_opertand._num));
      break;
    }
  }

  public DynVal GetStackTop()//for test purposes
  {
    return vm_stack.Peek();
  }

  public void ShowFullStack()//for test purposes
  {
    Console.WriteLine(" VM STACK :");
    foreach(var v in vm_stack)
    {
      Console.WriteLine(" \t " + v);
    }
  }
}

} //namespace bhl
