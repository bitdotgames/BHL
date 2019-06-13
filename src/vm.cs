using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

public class VM
{
	List<Compiler.Constant> constants;
	byte[] instructions;
	double[] globals;
	Dictionary<string, uint> func_buff;
	uint ip;

	Stack<double> vm_stack = new Stack<double>();

	Stack<uint> frames = new Stack<uint>();

	public void ChangeCurrentFrame(uint val)
	{
		frames.Pop();
		frames.Push(val);
	}

	public VM(byte[] instructions, List<Compiler.Constant> constants, Dictionary<string, uint> func_buff)
	{
		this.instructions = instructions;
		this.constants = constants;
		this.func_buff = func_buff;
		globals = new double[65000];
	}

	public void Exec(string func)
	{
		ip = func_buff[func];
		frames.Push(ip);
		Run();
	}

	public void Run()
	{
		Opcodes opcode;
		var frame_ip = frames.Peek() - 1;
        while(frames.Count > 0)
		{
			frame_ip++;
			opcode = (Opcodes)instructions[frame_ip];

			switch(opcode)
			{
				case Opcodes.Constant:
				    frame_ip++;
					int const_idx = instructions[frame_ip];

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
				    frame_ip++;
					int glob_idx = instructions[frame_ip];

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
					 frame_ip = frames.Peek();
				break;
				case Opcodes.FuncCall:
					frame_ip++;
				    uint f_ip = instructions[frame_ip];
			        ChangeCurrentFrame(frame_ip);
					frames.Push(f_ip);
					frame_ip = frames.Peek() - 1;
				break;
			}
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
