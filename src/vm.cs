using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

public class Frame
{
  public uint ip;
  public Stack<double> num_stack;
  public List<double> locals;

  public Frame()
  {
    ip = 0;
    num_stack = new Stack<double>();
    locals = new List<double>();
  }
}

public class VM
{
  List<Compiler.Constant> constants;
  byte[] instructions;
  Dictionary<string, uint> func_buff;

  Stack<double> vm_stack = new Stack<double>();

  Stack<Frame> frames = new Stack<Frame>();
  Frame curr_frame;

  public VM(byte[] instructions, List<Compiler.Constant> constants, Dictionary<string, uint> func_buff)
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

          curr_frame.num_stack.Push(constants[const_idx].nval);
        break;
        case Opcodes.UnaryNot:
        case Opcodes.UnaryNeg:
          ExecuteUnaryOperation(opcode);
        break;
        case Opcodes.Add:
        case Opcodes.Sub:
        case Opcodes.Div:
        case Opcodes.Mul:
        case Opcodes.Equal:
        case Opcodes.NotEqual:
        case Opcodes.Greather:
        case Opcodes.Less:
        case Opcodes.GreatherOrEqual:
        case Opcodes.LessOrEqual:
          ExecuteBinaryOperation(opcode);
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
          if(curr_frame.num_stack.Pop() == 0)
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
    var opertand = curr_frame.num_stack.Pop();
    switch(op)
    {
      case Opcodes.UnaryNot:
        if((opertand >= 0) && (opertand <= 1))
          curr_frame.num_stack.Push(opertand < 1 ? 1:0);
      break;
      case Opcodes.UnaryNeg:
        curr_frame.num_stack.Push(opertand * -1);
      break;
    }
  }

  void ExecuteBinaryOperation(Opcodes op)
  {
    var r_opertand = curr_frame.num_stack.Pop();
    var l_opertand = curr_frame.num_stack.Pop();

    switch(op)
    {
      case Opcodes.Add:
        curr_frame.num_stack.Push(l_opertand + r_opertand);
      break;
      case Opcodes.Sub:
        curr_frame.num_stack.Push(l_opertand - r_opertand);
      break;
      case Opcodes.Div:
        curr_frame.num_stack.Push(l_opertand / r_opertand);
      break;
      case Opcodes.Mul:
        curr_frame.num_stack.Push(l_opertand * r_opertand);
      break;
      case Opcodes.Equal:
        curr_frame.num_stack.Push(l_opertand == r_opertand ? 1:0);
      break;
      case Opcodes.NotEqual:
        curr_frame.num_stack.Push(l_opertand != r_opertand ? 1:0);
      break;
      case Opcodes.Greather:
        curr_frame.num_stack.Push(l_opertand > r_opertand ? 1:0);
      break;
      case Opcodes.Less:
        curr_frame.num_stack.Push(l_opertand < r_opertand ? 1:0);
      break;
      case Opcodes.GreatherOrEqual:
        curr_frame.num_stack.Push(l_opertand >= r_opertand ? 1:0);
      break;
      case Opcodes.LessOrEqual:
        curr_frame.num_stack.Push(l_opertand <= r_opertand ? 1:0);
      break;
    }
  }

  public double GetStackTop()//for test purposes
  {
    return vm_stack.Peek();
  }
}

} //namespace bhl
