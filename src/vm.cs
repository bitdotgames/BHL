using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

public class VM
{
	List<Compiler.Constant> constants;
	byte[] instructions;

	Stack<double> vm_stack = new Stack<double>();

	public VM(byte[] instructions, List<Compiler.Constant> constants)
	{
		this.instructions = instructions;
		this.constants = constants;
	}

	public void Run()
	{
		Opcodes opcode;
		for(int i = 0 ; i< instructions.Length; ++i)
		{
			opcode = (Opcodes)instructions[i];

			switch(opcode)
			{
				case Opcodes.Constant:
				    i++;
					int const_idx = instructions[i];

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
				default:
					Console.WriteLine("NO OPCODE");
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

	public double GetStackTop()
	{
		return vm_stack.Peek();
	}
}

} //namespace bhl
