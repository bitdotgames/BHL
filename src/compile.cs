using System;
using System.IO;
using System.Collections.Generic;

namespace bhl {

public enum Opcodes
{
  Constant        = 0x1,
  Add             = 0x2,
  Sub             = 0x3,
  Div             = 0x4,
  Mul             = 0x5,
  SetVar          = 0x6,
  GetVar          = 0x7,
  FuncCall        = 0x8,
  SetMVar         = 0x9,
  GetMVar         = 0xA,
  MethodCall      = 0xB,
  Return          = 0xC,
  ReturnVal       = 0xD,
  Jump            = 0xE,
  CondJump        = 0xF,
  LoopJump        = 0x10,
  UnaryNot        = 0x11,
  UnaryNeg        = 0x12,
  And             = 0x13,
  Or              = 0x14,
  Mod             = 0x15,
  BitOr           = 0x16,
  BitAnd          = 0x17,
  Equal           = 0x18,
  NotEqual        = 0x19,
  Less            = 0x1A,
  Greater         = 0x1B,
  LessOrEqual     = 0x1C,
  GreaterOrEqual  = 0x1D,
  DefArg          = 0x1E, //opcode for skipping func def args
  TypeCast        = 0x1F,
  ArrNew          = 0x20,
}

public enum SymbolScope
{
  Global = 1
}

public struct SymbolView
{
  public string name;
  public SymbolScope scope;
  public int index;
}

public class SymbolViewTable
{
  Dictionary<string, SymbolView> store = new Dictionary<string, SymbolView>();

  public SymbolView Define(string name)
  {
    var s = new SymbolView()
    {
      name = name,
      index = store.Count,
      scope  = SymbolScope.Global
    };

    if(!store.ContainsKey(name))
    {
      store[name] = s;
      return s;
    }
    else
    {
      return store[name];
    } 
  }

  public SymbolView Resolve(string name)
  {
    SymbolView s;
    if(!store.TryGetValue(name, out s))
     throw new Exception("No such symbol in table " + name);
    return s;
  }
}

public class Const
{
  public EnumLiteral type;
  public double num;
  public string str;

  public Const(AST_Literal lt)
  {
    type = lt.type;
    num = lt.nval;
    str = lt.sval;
  }

  public Const(double num)
  {
    type = EnumLiteral.NUM;
    this.num = num;
    str = "";
  }

  public Const(string str)
  {
    type = EnumLiteral.STR;
    this.str = str;
    num = 0;
  }

  public Const(bool v)
  {
    type = EnumLiteral.BOOL;
    num = v ? 1 : 0;
    this.str = "";
  }

  static public Const NewNil()
  {
    var c = new Const(0);
    c.type = EnumLiteral.NIL;
    c.num = 0;
    c.str = "";
    return c;
  }

  public Val ToVal()
  {
    if(type == EnumLiteral.NUM)
      return Val.NewNum(num);
    else if(type == EnumLiteral.BOOL)
      return Val.NewBool(num == 1);
    else if(type == EnumLiteral.STR)
      return Val.NewStr(str);
    else if(type == EnumLiteral.NIL)
      return Val.NewNil();
    else
      throw new Exception("Bad type");
  }
}

public class Compiler : AST_Visitor
{
  public class OpDefinition
  {
    public Opcodes name;
    public int[] operand_width; //each array item represents the size of the operand in bytes
  }

  BaseScope symbols;
  public BaseScope Symbols {
    get {
      return symbols;
    }
  }

  List<Const> constants = new List<Const>();
  public List<Const> Constants {
    get {
      return constants;
    }
  }

  List<Bytecode> scopes = new List<Bytecode>();

  List<SymbolViewTable> symbol_views = new List<SymbolViewTable>();

  Dictionary<string, uint> func2offset = new Dictionary<string, uint>();
  public Dictionary<string, uint> Func2Offset {
    get {
      return func2offset;
    }
  }
  
  Dictionary<byte, OpDefinition> opcode_decls = new Dictionary<byte, OpDefinition>();

  public Compiler(BaseScope symbols)
  {
    DeclareOpcodes();

    this.symbols = symbols;
    scopes.Add(new Bytecode());
    symbol_views.Add(new SymbolViewTable());
  }

  public void Compile(AST ast)
  {
    Visit(ast);
  }

  Bytecode GetCurrentScope()
  {
    return this.scopes[this.scopes.Count-1];
  }

  SymbolViewTable GetCurrentSymbolView()
  {
    return this.symbol_views[this.symbol_views.Count-1];
  }

  void EnterNewScope()
  {
    scopes.Add(new Bytecode());
    symbol_views.Add(new SymbolViewTable());
  }
 
  long LeaveCurrentScope()
  {
    var bytecode = scopes[0];

    long index = bytecode.Length;
    var curr_scope = GetCurrentScope();
    var curr_table = GetCurrentSymbolView();
    bytecode.Write(curr_scope);
    scopes.Remove(curr_scope);
    symbol_views.Remove(curr_table);

    return index;
  }

  int AddConstant(AST_Literal lt)
  {
    for(int i = 0 ; i < constants.Count; ++i)
    {
      var cn = constants[i];

      if(lt.type == cn.type && cn.num == lt.nval && cn.str == lt.sval)
        return i;
    }
    constants.Add(new Const(lt));
    return constants.Count-1;
  }

  void DeclareOpcodes()
  {
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.Constant,
        operand_width = new int[] { 2 }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.And
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.Or
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.BitAnd
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.BitOr
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.Mod
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.Add
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.Sub
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.Div
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.Mul
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.Equal
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.NotEqual
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.Greater
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.Less
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.GreaterOrEqual
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.LessOrEqual
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.UnaryNot
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.UnaryNeg
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.SetVar,
        operand_width = new int[] { 2 }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.GetVar,
        operand_width = new int[] { 2 }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.SetMVar,
        operand_width = new int[] { 4, 2 }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.GetMVar,
        operand_width = new int[] { 4, 2 }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.FuncCall,
        operand_width = new int[] { 4, 1 }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.MethodCall,
        operand_width = new int[] { 4, 2 }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.Return
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.ReturnVal
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.Jump,
        operand_width = new int[] { 2 }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.LoopJump,
        operand_width = new int[] { 2 }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.CondJump,
        operand_width = new int[] { 2 }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.DefArg,
        operand_width = new int[] { 2 }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.ArrNew
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.TypeCast,
        operand_width = new int[] { 4 }
      }
    );
  }

  void DeclareOpcode(OpDefinition def)
  {
    opcode_decls.Add((byte)def.name, def);
  }

  public OpDefinition LookupOpcode(Opcodes op)
  {
    OpDefinition def;
    if(!opcode_decls.TryGetValue((byte)op, out def))
       throw new Exception("No such opcode definition: " + op);
    return def;
  }

  public Compiler Emit(Opcodes op, int[] operands = null)
  {
    var curr_scope = GetCurrentScope();
    Emit(curr_scope, op, operands);
    return this;
  }

  void Emit(Bytecode buf, Opcodes op, int[] operands = null)
  {
    var def = LookupOpcode(op);

    buf.Write((byte)op);

    if(def.operand_width != null && (operands == null || operands.Length != def.operand_width.Length))
      throw new Exception("Invalid number of operands for opcode:" + op + ", expected:" + def.operand_width.Length);

    for(int i = 0; operands != null && i < operands.Length; ++i)
    {
      int width = def.operand_width[i];
      int op_val = operands[i];

      switch(width)
      {
        case 1:
          if(byte.MinValue > op_val || op_val > byte.MaxValue)
            throw new Exception("Operand value is out of bounds: " + op_val);
          buf.Write((byte)op_val);
        break;
        case 2:
          if(ushort.MinValue > op_val || op_val > ushort.MaxValue)
            throw new Exception("Operand value is out of bounds: " + op_val);
          buf.Write((ushort)op_val);
        break;
        case 4:
          if(uint.MinValue > op_val || (uint)op_val > uint.MaxValue)
            throw new Exception("Operand value is out of bounds: " + op_val);
          buf.Write((uint)op_val);
        break;
        default:
          throw new Exception("Not supported operand width: " + width + " for opcode:" + op);
      }
    }
  }

  //NOTE: returns position of the opcode argument for 
  //      the jump index to be patched later
  int EmitConditionAndBody(AST_Block ast, int idx)
  {
    //condition
    Visit(ast.children[idx]);
    Emit(Opcodes.CondJump, new int[] { 0 /*dummy placeholder*/});
    int patch_pos = GetCurrentScope().Position;
    //body
    Visit(ast.children[idx+1]);
    return patch_pos;
  }

  void PatchJumpOffsetToCurrPos(int jump_opcode_pos)
  {
    var curr_scope = GetCurrentScope();
    int offset = curr_scope.Position - jump_opcode_pos;
    if(offset < 0 || offset >= 241) 
      throw new Exception("Invalid offset: " + offset);
    curr_scope.PatchByteAt(jump_opcode_pos - 1, (byte)offset);
  }

  public byte[] GetBytes()
  {
    return scopes[0].GetBytes();
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
    EnterNewScope();
    VisitChildren(ast);
    Emit(Opcodes.Return);

    func2offset.Add(ast.name, (uint)LeaveCurrentScope());
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
    switch(ast.type)
    {
      case EnumBlock.IF:

        //if()
        // ^-- to be patched with position out of 'if body'
        //{
        // ...
        // jmp_opcode
        // ^-- to be patched with position out of 'if body'
        //}
        //else if()
        // ^-- to be patched with position out of 'if body'
        //{
        // ...
        // jmp_opcode
        // ^-- to be patched with position out of 'if body'
        //}
        //else
        //{
        //}

        int last_jmp_op_pos = -1;
        int i = 0;
        //NOTE: amount of children is even if there's no 'else'
        for(;i < ast.children.Count; i += 2)
        {
          //break if there's only 'else leftover' left
          if((i + 2) > ast.children.Count)
            break;

          int if_op_pos = EmitConditionAndBody(ast, i);
          if(last_jmp_op_pos != -1)
            PatchJumpOffsetToCurrPos(last_jmp_op_pos);

          //check if uncoditional jump out of 'if body' is required,
          //it's required only if there are other 'else if' or 'else'
          if(ast.children.Count > 2)
          {
            Emit(Opcodes.Jump, new int[] { 0 /*dummy placeholder*/});
            last_jmp_op_pos = GetCurrentScope().Position;
            PatchJumpOffsetToCurrPos(if_op_pos);
          }
          else
            PatchJumpOffsetToCurrPos(if_op_pos);
        }
        //check fo 'else leftover'
        if(i != ast.children.Count)
        {
          Visit(ast.children[i]);
          PatchJumpOffsetToCurrPos(last_jmp_op_pos);
        }
      break;
      case EnumBlock.WHILE:
      {
        int block_ip = GetCurrentScope().Position;
        int cond_op_pos = EmitConditionAndBody(ast, 0);
        Emit(Opcodes.LoopJump, new int[] { GetCurrentScope().Position - block_ip
                                           + LookupOpcode(Opcodes.LoopJump).operand_width[0] });
        PatchJumpOffsetToCurrPos(cond_op_pos);
      }
      break;
      default:
        //there will be behaviour node blocks
        VisitChildren(ast);
      break;
    }
  }

  public override void DoVisit(AST_TypeCast ast)
  {
    VisitChildren(ast);
    Emit(Opcodes.TypeCast, new int[] {(int)ast.ntype});
  }

  public override void DoVisit(AST_New ast)
  {
    switch(ast.type)
    {
      case "[]":
        Emit(Opcodes.ArrNew);
        VisitChildren(ast);
      break;
      default:
        throw new Exception("Not supported: " + ast.Name());
        //VisitChildren(ast);
    }
  }

  public override void DoVisit(AST_Inc ast)
  {
  }

  public override void DoVisit(AST_Call ast)
  {  
    SymbolView sv;
    switch(ast.type)
    {
      case EnumCall.VARW:
        sv = GetCurrentSymbolView().Define(ast.name);
        Emit(Opcodes.SetVar, new int[] { sv.index });
      break;
      case EnumCall.VAR:
        sv = GetCurrentSymbolView().Resolve(ast.name);
        Emit(Opcodes.GetVar, new int[] { sv.index });
      break;
      case EnumCall.FUNC:
        uint offset;
        func2offset.TryGetValue(ast.name, out offset);
        VisitChildren(ast);
        Emit(Opcodes.FuncCall, new int[] {(int)offset, (int)ast.cargs_bits});//figure out how cargs works
      break;
      case EnumCall.MVAR:
      {
        var class_symb = symbols.Resolve(ast.scope_ntype) as ClassSymbol;
        if(class_symb == null)
          throw new Exception("Class type not found: " + ast.scope_ntype);
        int memb_idx = class_symb.members.FindStringKeyIndex(ast.name);
        if(memb_idx == -1)
          throw new Exception("Member '" + ast.name + "' not found in class: " + ast.scope_ntype);

        VisitChildren(ast);

        //TODO: instead of scope_ntype it rather should be an index?
        Emit(Opcodes.GetMVar, new int[] { (int)ast.scope_ntype, memb_idx});
      }
      break;
      case EnumCall.MFUNC:
      {
        var class_symb = symbols.Resolve(ast.scope_ntype) as ClassSymbol;
        if(class_symb == null)
          throw new Exception("Class type not found: " + ast.scope_ntype);
        int memb_idx = class_symb.members.FindStringKeyIndex(ast.name);
        if(memb_idx == -1)
          throw new Exception("Member '" + ast.name + "' not found in class: " + ast.scope_ntype);

        VisitChildren(ast);
        //TODO: instead of scope_ntype it rather should be an index?
        Emit(Opcodes.MethodCall, new int[] {(int)ast.scope_ntype, memb_idx});
      }
      break;
      case EnumCall.ARR_IDX:
        Emit(Opcodes.MethodCall, new int[] { GenericArrayTypeSymbol.VM_Type, GenericArrayTypeSymbol.VM_AtIdx});
      break;
      case EnumCall.ARR_IDXW:
        Emit(Opcodes.MethodCall, new int[] { GenericArrayTypeSymbol.VM_Type, GenericArrayTypeSymbol.VM_SetIdx});
      break;
      default:
        throw new Exception("Not supported call: " + ast.type);
    }
  }

  public override void DoVisit(AST_Return node)
  {
    VisitChildren(node);
    Emit(Opcodes.ReturnVal);
  }

  public override void DoVisit(AST_Break node)
  {
  }

  public override void DoVisit(AST_PopValue node)
  {
  }

  public override void DoVisit(AST_Literal node)
  {
    Emit(Opcodes.Constant, new int[] { AddConstant(node) });
  }

  public override void DoVisit(AST_BinaryOpExp node)
  {
    VisitChildren(node);

    switch(node.type)
    {
      case EnumBinaryOp.AND:
        Emit(Opcodes.And);
      break;
      case EnumBinaryOp.OR:
        Emit(Opcodes.Or);
      break;
      case EnumBinaryOp.BIT_AND:
        Emit(Opcodes.BitAnd);
      break;
      case EnumBinaryOp.BIT_OR:
        Emit(Opcodes.BitOr);
      break;
      case EnumBinaryOp.MOD:
        Emit(Opcodes.Mod);
      break;
      case EnumBinaryOp.ADD:
        Emit(Opcodes.Add);
      break;
      case EnumBinaryOp.SUB:
        Emit(Opcodes.Sub);
      break;
      case EnumBinaryOp.DIV:
        Emit(Opcodes.Div);
      break;
      case EnumBinaryOp.MUL:
        Emit(Opcodes.Mul);
      break;
      case EnumBinaryOp.EQ:
        Emit(Opcodes.Equal);
      break;
      case EnumBinaryOp.NQ:
        Emit(Opcodes.NotEqual);
      break;
      case EnumBinaryOp.GT:
        Emit(Opcodes.Greater);
      break;
      case EnumBinaryOp.LT:
        Emit(Opcodes.Less);
      break;
      case EnumBinaryOp.GTE:
        Emit(Opcodes.GreaterOrEqual);
      break;
      case EnumBinaryOp.LTE:
        Emit(Opcodes.LessOrEqual);
      break;
      default:
        throw new Exception("Not supported binary type: " + node.type);
    }
  }

  public override void DoVisit(AST_UnaryOpExp node)
  {
    VisitChildren(node);

    switch(node.type)
    {
      case EnumUnaryOp.NOT:
        Emit(Opcodes.UnaryNot);
      break;
      case EnumUnaryOp.NEG:
        Emit(Opcodes.UnaryNeg);
      break;
      default:
        throw new Exception("Not supported unary type: " + node.type);
    }
  }

  public override void DoVisit(AST_VarDecl node)
  {
    if(node.children.Count > 0)
    {
      var index = 0;
      Emit(Opcodes.DefArg, new int[] { index });
      var pointer = GetCurrentScope().Position;
      VisitChildren(node);
      PatchJumpOffsetToCurrPos(pointer);
    }

    SymbolView s = GetCurrentSymbolView().Define(node.name);
    Emit(Opcodes.SetVar, new int[] { s.index });
  }

  public override void DoVisit(bhl.AST_JsonObj node)
  {
  }

  public override void DoVisit(bhl.AST_JsonArr node)
  {
    throw new Exception("Not supported : " + node);
  }

  public override void DoVisit(bhl.AST_JsonPair node)
  {
  }

#endregion
}

public class Bytecode
{
  public ushort Position { get { return (ushort)stream.Position; } }
  public long Length { get { return stream.Length; } }

  MemoryStream stream = new MemoryStream();

  public void Reset(byte[] buffer, int size)
  {
    stream.SetLength(0);
    stream.Write(buffer, 0, size);
    stream.Position = 0;
  }

  public byte[] GetBytes()
  {
    return stream.ToArray();
  }

  public static uint Decode(byte[] bytecode, ref uint ip)
  {
    int decoded = 0;

    ++ip;
    var A0 = bytecode[ip];

    if(A0 < 241)
    {
      decoded = A0;
    }
    else if(A0 > 240 && A0 < 248)
    {
      ++ip;
      var A1 = bytecode[ip];
      decoded = 240 + 256 * (A0 - 241) + A1;
    }
    else if(A0 == 249)
    { 
      ++ip;
      var A1 = bytecode[ip];
      ++ip;
      var A2 = bytecode[ip];

      decoded = 2288 + (256 * A1) + A2;
    }
    else if(A0 >= 250 && A0 <= 251)
    {
      //A1..A3/A4/A5/A6/A7/A8 as a from 3 to 8 -byte big-ending integer
      int len = 3 + A0 % 250;
      for(int i = 1; i <= len; ++i)
      {
        ++ip;
        decoded |= bytecode[ip] << (i-1) * 8; 
      }
    }
    else
      throw new Exception("Not supported code: " + A0);

    return (uint)decoded;
  }

  public void Write(byte value)
  {
    stream.WriteByte(value);
  }

  public void Write(sbyte value)
  {
    stream.WriteByte((byte)value);
  }

  // http://sqlite.org/src4/doc/trunk/www/varint.wiki
  public void Write(uint value)
  {
    if(value <= 65535)
    {
      Write((ushort)value);
      return;
    }
    if(value <= 67823)
    {
      Write((byte)249);
      Write((byte)((value - 2288) / 256));
      Write((byte)((value - 2288) % 256));
      return;
    }
    if(value <= 16777215)
    {
      Write((byte)250);
      Write((byte)(value & 0xFF));
      Write((byte)((value >> 8) & 0xFF));
      Write((byte)((value >> 16) & 0xFF));
      return;
    }

    // all other values of uint
    Write((byte)251);
    Write((byte)(value & 0xFF));
    Write((byte)((value >> 8) & 0xFF));
    Write((byte)((value >> 16) & 0xFF));
    Write((byte)((value >> 24) & 0xFF));
  }

  //TODO: do we need to support that?
  //public void Write(ulong value)
  //{
  //  if(value <= 16777215)
  //  {
  //    Write((uint)value);
  //    return;
  //  }
  //  if(value <= 4294967295)
  //  {
  //    Write((byte)251);
  //    Write((byte)(value & 0xFF));
  //    Write((byte)((value >> 8) & 0xFF));
  //    Write((byte)((value >> 16) & 0xFF));
  //    Write((byte)((value >> 24) & 0xFF));
  //    return;
  //  }
  //  if(value <= 1099511627775)
  //  {
  //    Write((byte)252);
  //    Write((byte)(value & 0xFF));
  //    Write((byte)((value >> 8) & 0xFF));
  //    Write((byte)((value >> 16) & 0xFF));
  //    Write((byte)((value >> 24) & 0xFF));
  //    Write((byte)((value >> 32) & 0xFF));
  //    return;
  //  }
  //  if(value <= 281474976710655)
  //  {
  //    Write((byte)253);
  //    Write((byte)(value & 0xFF));
  //    Write((byte)((value >> 8) & 0xFF));
  //    Write((byte)((value >> 16) & 0xFF));
  //    Write((byte)((value >> 24) & 0xFF));
  //    Write((byte)((value >> 32) & 0xFF));
  //    Write((byte)((value >> 40) & 0xFF));
  //    return;
  //  }
  //  if(value <= 72057594037927935)
  //  {
  //    Write((byte)254);
  //    Write((byte)(value & 0xFF));
  //    Write((byte)((value >> 8) & 0xFF));
  //    Write((byte)((value >> 16) & 0xFF));
  //    Write((byte)((value >> 24) & 0xFF));
  //    Write((byte)((value >> 32) & 0xFF));
  //    Write((byte)((value >> 40) & 0xFF));
  //    Write((byte)((value >> 48) & 0xFF));
  //    return;
  //  }

  //  // all others
  //  {
  //    Write((byte)255);
  //    Write((byte)(value & 0xFF));
  //    Write((byte)((value >> 8) & 0xFF));
  //    Write((byte)((value >> 16) & 0xFF));
  //    Write((byte)((value >> 24) & 0xFF));
  //    Write((byte)((value >> 32) & 0xFF));
  //    Write((byte)((value >> 40) & 0xFF));
  //    Write((byte)((value >> 48) & 0xFF));
  //    Write((byte)((value >> 56) & 0xFF));
  //  }
  //}

  public void Write(char value)
  {
    Write((byte)value);
  }

  public void Write(short value)
  {
    Write((byte)(value & 0xff));
    Write((byte)((value >> 8) & 0xff));
  }

  public void Write(ushort value)
  {
    if(value <= 240)
    {
      Write((byte)value);
      return;
    }
    if(value <= 2287)
    {
      Write((byte)((value - 240) / 256 + 241));
      Write((byte)((value - 240) % 256));
      return;
    }

    {
      Write((byte)249);
      Write((byte)((value - 2288) / 256));
      Write((byte)((value - 2288) % 256));
      return;
    }
  }

  //NOTE: do we really need non-packed versions?
  //public void Write(int value)
  //{
  //  // little endian...
  //  Write((byte)(value & 0xff));
  //  Write((byte)((value >> 8) & 0xff));
  //  Write((byte)((value >> 16) & 0xff));
  //  Write((byte)((value >> 24) & 0xff));
  //}

  //public void Write(uint value)
  //{
  //  Write((byte)(value & 0xff));
  //  Write((byte)((value >> 8) & 0xff));
  //  Write((byte)((value >> 16) & 0xff));
  //  Write((byte)((value >> 24) & 0xff));
  //}

  //public void Write(long value)
  //{
  //  Write((byte)(value & 0xff));
  //  Write((byte)((value >> 8) & 0xff));
  //  Write((byte)((value >> 16) & 0xff));
  //  Write((byte)((value >> 24) & 0xff));
  //  Write((byte)((value >> 32) & 0xff));
  //  Write((byte)((value >> 40) & 0xff));
  //  Write((byte)((value >> 48) & 0xff));
  //  Write((byte)((value >> 56) & 0xff));
  //}

  //public void Write(ulong value)
  //{
  //  Write((byte)(value & 0xff));
  //  Write((byte)((value >> 8) & 0xff));
  //  Write((byte)((value >> 16) & 0xff));
  //  Write((byte)((value >> 24) & 0xff));
  //  Write((byte)((value >> 32) & 0xff));
  //  Write((byte)((value >> 40) & 0xff));
  //  Write((byte)((value >> 48) & 0xff));
  //  Write((byte)((value >> 56) & 0xff));
  //}

  public void Write(float value)
  {
    byte[] bytes = BitConverter.GetBytes(value);
    Write(bytes, bytes.Length);
  }

  public void Write(double value)
  {
    byte[] bytes = BitConverter.GetBytes(value);
    Write(bytes, bytes.Length);
  }

  public void Write(bool value)
  {
    stream.WriteByte((byte)(value ? 1 : 0));
  }

  public void Write(Bytecode buffer)
  {
    buffer.WriteTo(stream);
  }

  void WriteTo(MemoryStream buffer_stream)
  {
    stream.WriteTo(buffer_stream);
  }

  public void Write(byte[] buffer, int count)
  {
    stream.Write(buffer, 0, count);
  }

  public void Write(byte[] buffer, int offset, int count)
  {
    stream.Write(buffer, offset, count);
  }

  public void PatchByteAt(int pos, byte value)
  {
    long orig_pos = stream.Position;
    stream.Position = pos;
    stream.WriteByte(value);
    stream.Position = orig_pos;
  }
}

} //namespace bhl
