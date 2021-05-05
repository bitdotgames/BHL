using System;
using System.IO;
using System.Collections.Generic;

namespace bhl {

public enum Opcodes
{
  Nop             = 0x0,
  Constant        = 0x1,
  Add             = 0x2,
  Sub             = 0x3,
  Div             = 0x4,
  Mul             = 0x5,
  SetVar          = 0x6,
  GetVar          = 0x7,
  DeclVar         = 0x8,
  ArgVar          = 0x9,
  Call            = 0x10,
  SetMVar         = 0x20,
  GetMVar         = 0xA,
  MethodCall      = 0xB,
  Return          = 0xC,
  ReturnVal       = 0xD,
  Jump            = 0xE,
  CondJump        = 0xF,
  LoopJump        = 0x30,
  UnaryNot        = 0x31,
  UnaryNeg        = 0x32,
  And             = 0x33,
  Or              = 0x34,
  Mod             = 0x35,
  BitOr           = 0x36,
  BitAnd          = 0x37,
  Equal           = 0x38,
  NotEqual        = 0x39,
  Less            = 0x3A,
  Greater         = 0x3B,
  LessOrEqual     = 0x3C,
  GreaterOrEqual  = 0x3D,
  DefArg          = 0x3E, //opcode for skipping func def args
  TypeCast        = 0x3F,
  Block      = 0x40,
  ArrNew          = 0x41,
  Lambda          = 0x42,
  UseUpval        = 0x43,
  InitFrame       = 0x44,
  Inc             = 0x45,
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
  List<AST_Block> ctrl_blocks = new List<AST_Block>();
  HashSet<AST_Block> block_has_defers = new HashSet<AST_Block>();

  Dictionary<string, uint> func2ip = new Dictionary<string, uint>();
  public Dictionary<string, uint> Func2Offset {
    get {
      return func2ip;
    }
  }
  
  Dictionary<byte, OpDefinition> opcode_decls = new Dictionary<byte, OpDefinition>();

  public Compiler(BaseScope symbols)
  {
    DeclareOpcodes();

    this.symbols = symbols;
    scopes.Add(new Bytecode());
  }

  public void Compile(AST ast)
  {
    Visit(ast);
  }

  Bytecode GetCurrentScope()
  {
    return this.scopes[this.scopes.Count-1];
  }

  uint EnterNewScope()
  {
    scopes.Add(new Bytecode());

    return (uint)scopes[0].Length;
  }
 
  Bytecode LeaveCurrentScope(bool auto_append = true)
  {
    var curr_scope = GetCurrentScope();
    if(auto_append)
      scopes[0].Write(curr_scope);
    scopes.RemoveAt(scopes.Count-1);
    return curr_scope;
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
        name = Opcodes.Nop,
      }
    );
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
        name = Opcodes.DeclVar,
        operand_width = new int[] { 2 }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.ArgVar,
        operand_width = new int[] { 2 }
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
        name = Opcodes.Call,
        operand_width = new int[] { 1/*type*/, 4/*idx/or ip*/, 4/*args bits*/ }
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
        name = Opcodes.Lambda,
        //TODO: this can be a 16bit offset relative to the 
        //      current ip position
        operand_width = new int[] { 4 /*closure ip*/, 2/*local vars num*/ }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.UseUpval,
        operand_width = new int[] { 1 /*upval src idx*/, 1 /*local dst idx*/ }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.InitFrame,
        operand_width = new int[] { 1/*total local vars*/ }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.Block,
        operand_width = new int[] { 1/*type*/, 2/*len*/ }
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
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.Inc,
        operand_width = new int[] { 2/*var idx*/ }
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
    Emit(Opcodes.CondJump, new int[] { (int)Bytecode.GetMaxValueForBytes(1) /*dummy placeholder*/});
    int patch_pos = GetCurrentScope().Position;
    //body
    Visit(ast.children[idx+1]);
    return patch_pos;
  }

  void PatchJumpOffsetToCurrPos(int jump_opcode_pos)
  {
    var curr_scope = GetCurrentScope();
    int offset = curr_scope.Position - jump_opcode_pos;
    if(offset < 0 || Bytecode.GetBytesRequired((uint)offset) > 1) 
      throw new Exception("Invalid offset: " + offset);
    curr_scope.PatchAt(jump_opcode_pos - 1, (byte)offset);
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
    uint ip = EnterNewScope();
    func2ip.Add(ast.name, ip);
    if(ast.local_vars_num > 0)
      Emit(Opcodes.InitFrame, new int[] { (int)ast.local_vars_num });
    VisitChildren(ast);
    Emit(Opcodes.Return);
    LeaveCurrentScope();
  }

  public override void DoVisit(AST_LambdaDecl ast)
  {
    EnterNewScope();
    //NOTE: since lambda's body can appear anywhere in the 
    //      compiled code we skip it by uncoditional jump over it
    Emit(Opcodes.Jump, new int[] {(int)Bytecode.GetMaxValueForBytes(2)/*dummy placeholder*/});
    VisitChildren(ast);
    Emit(Opcodes.Return);
    var bytecode = LeaveCurrentScope(auto_append: false);

    long jump_pos = bytecode.Length - 2;
    if(jump_pos > Bytecode.GetMaxValueForBytes(2))
      throw new Exception("Too large lambda body");
    //let's patch the jump placeholder with the actual jump position
    bytecode.PatchAt(1, (ushort)jump_pos, max_bytes: 2, gap_filler: (byte)Opcodes.Nop);

    uint ip = 2;//taking into account 'jump out of lambda'
    for(int i=0;i < scopes.Count;++i)
      ip += (uint)scopes[i].Length;
    func2ip.Add(ast.name, ip);

    GetCurrentScope().Write(bytecode);

    Emit(Opcodes.Lambda, new int[] {(int)ip, (int)ast.local_vars_num});
    foreach(var p in ast.uses)
      Emit(Opcodes.UseUpval, new int[]{(int)p.upsymb_idx, (int)p.symb_idx});
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
      case EnumBlock.FUNC:
      case EnumBlock.GROUP:
      {
        VisitChildren(ast);
      }
      break;
      case EnumBlock.SEQ:
      case EnumBlock.PARAL:
      case EnumBlock.PARAL_ALL:
      {
        VisitControlBlock(ast);
      }
      break;
      case EnumBlock.DEFER:
      {
        VisitDefer(ast);
      }
      break;
      default:
        throw new Exception("Not supported block type: " + ast.type); 
    }
  }

  void VisitControlBlock(AST_Block ast)
  {
    var parent_block = ctrl_blocks.Count > 0 ? ctrl_blocks[ctrl_blocks.Count-1] : null;

    bool parent_is_paral =
      parent_block != null &&
      (parent_block.type == EnumBlock.PARAL ||
       parent_block.type == EnumBlock.PARAL_ALL);

    bool is_paral = 
      ast.type == EnumBlock.PARAL || 
      ast.type == EnumBlock.PARAL_ALL;

    var block_code = new Bytecode();
    scopes.Add(block_code);
    ctrl_blocks.Add(ast);

    for(int i=0;i<ast.children.Count;++i)
    {
      var child = ast.children[i];

      //NOTE: let's automatically wrap all children with sequence if 
      //      they are inside paral block
      if(is_paral && (!(child is AST_Block child_block) || (child_block.type != EnumBlock.SEQ && child_block.type != EnumBlock.DEFER)))
      {
        var seq_child = new AST_Block();
        seq_child.type = EnumBlock.SEQ;
        seq_child.children.Add(child);
        child = seq_child;
      }

      Visit(child);
    }

    bool need_to_enter_block = is_paral || parent_is_paral || block_has_defers.Contains(ast);

    scopes.RemoveAt(scopes.Count-1);
    ctrl_blocks.RemoveAt(ctrl_blocks.Count-1);

    if(need_to_enter_block)
      Emit(Opcodes.Block, new int[] { (int)ast.type, block_code.Position});
    GetCurrentScope().Write(block_code);
  }

  void VisitDefer(AST_Block ast)
  {
    var parent_block = ctrl_blocks.Count > 0 ? ctrl_blocks[ctrl_blocks.Count-1] : null;
    if(parent_block != null)
      block_has_defers.Add(parent_block);

    var block_code = new Bytecode();
    scopes.Add(block_code);
    VisitChildren(ast);
    scopes.RemoveAt(scopes.Count-1);

    Emit(Opcodes.Block, new int[] { (int)ast.type, block_code.Position});
    GetCurrentScope().Write(block_code);
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
    Emit(Opcodes.Inc, new int[] { (int)ast.symb_idx });
  }

  public override void DoVisit(AST_Call ast)
  {  
    switch(ast.type)
    {
      case EnumCall.VARW:
      {
        Emit(Opcodes.SetVar, new int[] { (int)ast.symb_idx });
      }
      break;
      case EnumCall.VAR:
      {
        Emit(Opcodes.GetVar, new int[] { (int)ast.symb_idx });
      }
      break;
      case EnumCall.FUNC:
      {
        uint offset;
        if(func2ip.TryGetValue(ast.name, out offset))
        {
          VisitChildren(ast);
          Emit(Opcodes.Call, new int[] {(int)0, (int)offset, (int)ast.cargs_bits});//figure out how cargs works
        }
        else if(symbols.Resolve(ast.name) is VM_FuncBindSymbol fsymb)
        {
          int func_idx = symbols.GetMembers().IndexOf(fsymb);
          if(func_idx == -1)
            throw new Exception("Func '" + ast.name + "' idx not found in symbols");
          VisitChildren(ast);
          Emit(Opcodes.Call, new int[] {(int)1, (int)func_idx, (int)ast.cargs_bits});//figure out how cargs works
        }
        else
          throw new Exception("Func '" + ast.name + "' code not found");
      }
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
      {
        Emit(Opcodes.MethodCall, new int[] { GenericArrayTypeSymbol.VM_Type, GenericArrayTypeSymbol.VM_AtIdx});
      }
      break;
      case EnumCall.ARR_IDXW:
      {
        Emit(Opcodes.MethodCall, new int[] { GenericArrayTypeSymbol.VM_Type, GenericArrayTypeSymbol.VM_SetIdx});
      }
      break;
      case EnumCall.FUNC_PTR_POP:
      {
        Emit(Opcodes.Call, new int[] {(int)2, 0, 0});
      }
      break;
      case EnumCall.FUNC_PTR:
      {
        Emit(Opcodes.Call, new int[] {(int)3, (int)ast.symb_idx, 0});
      }
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

  public override void DoVisit(AST_VarDecl ast)
  {
    if(ast.is_func_arg && ast.children.Count > 0)
    {
      var index = 0;
      Emit(Opcodes.DefArg, new int[] { index });
      var pointer = GetCurrentScope().Position;
      VisitChildren(ast);
      PatchJumpOffsetToCurrPos(pointer);
    }

    Emit(ast.is_func_arg ? Opcodes.ArgVar : Opcodes.DeclVar, new int[] { (int)ast.symb_idx });
  }

  public override void DoVisit(bhl.AST_JsonObj node)
  {
    throw new Exception("Not supported : " + node);
  }

  public override void DoVisit(bhl.AST_JsonArr node)
  {
    throw new Exception("Not supported : " + node);
  }

  public override void DoVisit(bhl.AST_JsonPair node)
  {
    throw new Exception("Not supported : " + node);
  }

#endregion
}

public class Bytecode
{
  public ushort Position { get { return (ushort)stream.Position; } }
  public long Length { get { return stream.Length; } }

  MemoryStream stream = new MemoryStream();

  static public int GetBytesRequired(uint value)
  {
    if(value <= 240)
      return 1;
    else if(value <= 2287)
      return 2;
    else if(value <= 67823)
      return 3;
    else if(value <= 16777215)
      return 4;
    else 
      return 5;
  }

  static public uint GetMaxValueForBytes(int bytes)
  {
    if(bytes == 1)
      return 240;
    else if(bytes == 2)
      return 2287;
    else if(bytes == 3)
      return 67823;
    else if(bytes == 4)
      return 16777215;
    else if(bytes == 5)
      return uint.MaxValue;
    else 
      return 0;
  }

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

  // http://sqlite.org/src4/doc/trunk/www/varint.wiki
  public int Write(uint value)
  {
    if(value <= 65535)
      return Write((ushort)value);

    if(value <= 67823)
    {
      Write((byte)249);
      Write((byte)((value - 2288) / 256));
      Write((byte)((value - 2288) % 256));
      return 3;
    }

    if(value <= 16777215)
    {
      Write((byte)250);
      Write((byte)(value & 0xFF));
      Write((byte)((value >> 8) & 0xFF));
      Write((byte)((value >> 16) & 0xFF));
      return 4;
    }

    // all other values of uint
    Write((byte)251);
    Write((byte)(value & 0xFF));
    Write((byte)((value >> 8) & 0xFF));
    Write((byte)((value >> 16) & 0xFF));
    Write((byte)((value >> 24) & 0xFF));
    return 5;
  }

  public int Write(ushort value)
  {
    if(value <= 240)
    {
      Write((byte)value);
      return 1;
    }

    if(value <= 2287)
    {
      Write((byte)((value - 240) / 256 + 241));
      Write((byte)((value - 240) % 256));
      return 2;
    }

    Write((byte)249);
    Write((byte)((value - 2288) / 256));
    Write((byte)((value - 2288) % 256));
    return 3;
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

  public void PatchAt(int pos, byte value)
  {
    long orig_pos = stream.Position;
    stream.Position = pos;
    Write(value);
    stream.Position = orig_pos;
  }

  public void PatchAt(int pos, ushort value, int max_bytes, byte gap_filler)
  {
    long orig_pos = stream.Position;
    stream.Position = pos;
    int written_bytes = Write(value);
    for(int i=0;i<(max_bytes-written_bytes);++i)
      Write(gap_filler);
    stream.Position = orig_pos;
  }
}

} //namespace bhl
