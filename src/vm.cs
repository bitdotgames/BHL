using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

public class Frame
{
	public uint ip;
	public Stack<double> num_stack;
	public double[] locals;

	public Frame()
	{
		ip = 0;
		num_stack = new Stack<double>();
		locals = new double[65000];
	}
}	

public class VM
{
	List<Compiler.Constant> constants;
	byte[] instructions;
	double[] globals;
	Dictionary<string, uint> func_buff;

	Stack<double> vm_stack = new Stack<double>();

	Stack<Frame> frames = new Stack<Frame>();

	public VM(byte[] instructions, List<Compiler.Constant> constants, Dictionary<string, uint> func_buff)
	{
		this.instructions = instructions;
		this.constants = constants;
		this.func_buff = func_buff;
		globals = new double[65000];
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
		var curr_frame = frames.Peek();
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

					vm_stack.Push(constants[const_idx].nval);
				break;
				case Opcodes.Add:
				case Opcodes.Sub:
				case Opcodes.Div:
				case Opcodes.Mul:
					ExecuteBinaryOperation(opcode);
				break;
				case Opcodes.SetVar:
				case Opcodes.GetVar:
				    curr_frame.ip++;
					int glob_idx = instructions[curr_frame.ip];

					if(glob_idx >= globals.Length)
						throw new Exception("Index out of globals pool: " + glob_idx);

				    switch(opcode)
				    {
				    	case Opcodes.SetVar:
				    		globals[glob_idx] = vm_stack.Pop();
				    	break;
				    	case Opcodes.GetVar:
				    		vm_stack.Push(globals[glob_idx]);
				    	break;
				    }
				break;
				case Opcodes.ReturnVal:
					frames.Pop();
					if(frames.Count > 0)
					 curr_frame = frames.Peek();
				break;
				case Opcodes.FuncCall:
					curr_frame.ip++;
					var fr = new Frame();
				    fr.ip = instructions[curr_frame.ip];
					frames.Push(fr);
					curr_frame = frames.Peek();
				continue;
			}
			curr_frame.ip++;
		}
	}

	void ExecuteBinaryOperation(Opcodes op)
	{
		var r_opertand = vm_stack.Pop();
		var l_opertand = vm_stack.Pop();

		switch(op)
		{
			case Opcodes.Add:
				vm_stack.Push(l_opertand + r_opertand);
			break;
			case Opcodes.Sub:
				vm_stack.Push(l_opertand - r_opertand);
			break;
			case Opcodes.Div:
				vm_stack.Push(l_opertand / r_opertand);
			break;
			case Opcodes.Mul:
				vm_stack.Push(l_opertand * r_opertand);
			break;
		}
	}

	public double GetStackTop()//for test purposes
	{
		return vm_stack.Peek();
	}
}

} //namespace bhl
