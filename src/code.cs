using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl
{
    public class Code
    {
        private struct Instructions
        {
            public byte[] instructions;
        }
        public struct Opcode
        {
            public byte opcode;
        }

        private struct Definition
        {
            public string name;
            public UInt16[] operand_width;
        }

        Opcode Op_Constant = new Opcode() { opcode = 1 };

        private Dictionary<Opcode, Definition> definitions = new Dictionary<Opcode, Definition>();

        public void SetDefinitions()
        {
            definitions.Add(Op_Constant,
                new Definition()
                {
                    name = "Op_Constant",
                    operand_width = new UInt16[] { 2 }//just like in the book
                });
        }

        private Definition Lookup(Opcode op)
        {
            var def = definitions[op];

            if (def.Equals(null))
                return new Definition() { name = "Exception", operand_width = null };//looks like shit;
            return def;
        }

        public byte[] Make(Opcode op, UInt16[] operands)//?
        {
            var def = Lookup(op);

            if (def.operand_width == null)
            {
                return new byte[0];
            }
            UInt16 instruction_length = 1;

            foreach (var d in def.operand_width)
            {
                instruction_length += d;
            }

            var instruction = new byte[instruction_length];//dynamic array

            instruction[0] = op.opcode;

            UInt16 offset = 1;

            for (int i = 0; i < operands.Length; i++)
            {
                var width = def.operand_width[i];

                switch (width)
                {
                    case 2:
                        PutUint(instruction, operands[i], offset);// where, what, offset//littlEndian
                        break;
                }

                offset += width;
            }

            return instruction;
        }

        private void PutUint(byte[] insert, UInt16 ui, UInt16 offset)
        {
            var ar = BitConverter.GetBytes(Convert.ToUInt16(ui));
            ar.CopyTo(insert, offset);
        }

        public byte[] GetResult()
        {
            SetDefinitions();
            return Make(new Opcode() { opcode = 1 }, new UInt16[] { 2 });
        }
    }
}