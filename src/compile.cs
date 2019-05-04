using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace bhl {

public class Compiler : AST_Visitor
{
  List<byte[]> instructions = new List<byte[]>();
  List<object> constants = new List<object>();

  static readonly byte Op_Constant = 1;
  static readonly byte Op_Add      = 2;

  struct Definition
  {
    public string name;
    public UInt16[] operand_width;
  }

  Dictionary<byte, Definition> definitions = new Dictionary<byte, Definition>();

  public Compiler()
  {
    SetDefinitions();
  }

  public byte[] Compile(AST ast)
  {
    Visit(ast);

    byte[] buff = new byte[0];
    foreach(var ins in instructions)
    {
      buff = buff.Concat(ins).ToArray();
    }
    Console.WriteLine("code ... " + BitConverter.ToString(buff));

    return buff;
  }

  int AddConstant(object obj)
  {
    constants.Add(obj);
    return constants.Count-1;
  }

  int AddInstruction(byte[] ins)
  {
    instructions.Add(ins);
    return instructions.Count-1;
  }

  int Emit(byte op, UInt16[] operands)
  {
    var ins = Make(op, operands);
    var pos = AddInstruction(ins);
    return pos;
  }

  void SetDefinitions()
  {
    definitions.Add(Op_Constant,
                    new Definition()
                    {
                      name = "Op_Constant",
                      operand_width = new UInt16[] { 2 }
                    }
    );
    definitions.Add(Op_Add,
                    new Definition()
                    {
                      name = "Op_Add",
                      operand_width = new UInt16[] { 0 }
                    }
    );
  }

  Definition Lookup(byte op)
  {
    Definition def;
    if(!definitions.TryGetValue(op, out def))
       return new Definition() { name = "Exception", operand_width = null };//looks like shit;
    return def;
  }

  public byte[] Make(byte op, UInt16[] operands)//?
  {
    var def = Lookup(op);
    if(def.operand_width == null)
      return new byte[0];

    UInt16 instruction_length = 1;
    foreach(var d in def.operand_width)
      instruction_length += d;

    var instruction = new byte[instruction_length];//dynamic array
    instruction[0] = op;
    UInt16 offset = 1;
    for(int i = 0; i < operands.Length; i++)
    {
      var width = def.operand_width[i];
      switch(width)
      {
        case 2:
         PutUint(instruction, operands[i], offset);// where, what, offset//littlEndian
        break;
      }
      offset += width;
    }
    return instruction;
  }

  void PutUint(byte[] insert, UInt16 ui, UInt16 offset)
  {
    var ar = BitConverter.GetBytes(Convert.ToUInt16(ui));
    ar.CopyTo(insert, offset);
  }

  public byte[] GetResult()
  {
    return Make(1, new UInt16[] { 2 });
  }

#region Visits

  public override void DoVisit(AST_Interim ast)
  {
    VisitChildren(ast);
  }

  public override void DoVisit(AST_Module ast)
  {
    VisitChildren(ast);
  }

  public override void DoVisit(AST_Import ast)
  {
  }

  public override void DoVisit(AST_FuncDecl ast)
  {
    //Console.WriteLine("FUNC " + ast.name + " :");
    VisitChildren(ast);
  }

  public override void DoVisit(AST_LambdaDecl ast)
  {
  }

  public override void DoVisit(AST_ClassDecl ast)
  {
  }

  public override void DoVisit(AST_EnumDecl ast)
  {
  }

  public override void DoVisit(AST_Block ast)
  {
    VisitChildren(ast);
  }

  public override void DoVisit(AST_TypeCast ast)
  {
  }

  public override void DoVisit(AST_New ast)
  {
  }

  public override void DoVisit(AST_Inc ast)
  {
  }

  public override void DoVisit(AST_Call ast)
  {           
  }

  public override void DoVisit(AST_Return node)
  {
    VisitChildren(node);
  }

  public override void DoVisit(AST_Break node)
  {
  }

  public override void DoVisit(AST_PopValue node)
  {
  }

  public override void DoVisit(AST_Literal node)
  {
    var e = Emit(Op_Constant, new UInt16[] { Convert.ToUInt16(node.nval) });
    var res = instructions[e];
    Console.WriteLine("\tLITERAL " + node.nval + " - " + BitConverter.ToString(res));
  }

  public override void DoVisit(AST_BinaryOpExp node)
  {
    byte[] res = null;
    switch(node.type)
    {
      case EnumBinaryOp.ADD:
        res = Make(Op_Add, new UInt16[0]);
      break;
    }

    Console.WriteLine("\tBIN OP " + node.type + " - " + BitConverter.ToString(res));
    VisitChildren(node);
  }

  public override void DoVisit(AST_UnaryOpExp node)
  {
  }

  public override void DoVisit(AST_VarDecl node)
  {
  }

  public override void DoVisit(bhl.AST_JsonObj node)
  {
  }

  public override void DoVisit(bhl.AST_JsonArr node)
  {
  }

  public override void DoVisit(bhl.AST_JsonPair node)
  {
  }

#endregion
}

} //namespace bhl
