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
  CallNative      = 0x11,
  GetFunc         = 0x12,
  GetFuncNative   = 0x13,
  GetFuncFromVar  = 0x14,
  GetFuncImported = 0x15,
  SetMVar         = 0x20,
  SetMVarInplace  = 0x21,
  GetMVar         = 0xA,
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
  DefArg          = 0x3E, 
  TypeCast        = 0x3F,
  Block           = 0x40,
  New             = 0x41,
  Lambda          = 0x42,
  UseUpval        = 0x43,
  InitFrame       = 0x44,
  Inc             = 0x45,
  ClassBegin      = 0x46,
  ClassMember     = 0x47,
  ClassEnd        = 0x48,
  Import          = 0x49,
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

  public bool IsEqual(Const o)
  {
    return type == o.type && 
           num == o.num && 
           str == o.str;
  }
}

public class Compiler : AST_Visitor
{
  public class OpDefinition
  {
    public Opcodes name;
    public int[] operand_width; //each array item represents the size of the operand in bytes
  }

  AST ast;
  FileModule module;
  public FileModule Module {
    get {
      return module;
    }
  }

  GlobalScope globs;
  public GlobalScope Globs {
    get {
      return globs;
    }
  }

  LocalScope symbols;

  List<Const> constants = new List<Const>();
  public List<Const> Constants {
    get {
      return constants;
    }
  }

  Bytecode initcode = new Bytecode();
  Bytecode bytecode = new Bytecode();
  List<Bytecode> code_stack = new List<Bytecode>() { null };
  List<AST_Block> ctrl_blocks = new List<AST_Block>();
  List<AST_FuncDecl> func_decls = new List<AST_FuncDecl>();
  HashSet<AST_Block> block_has_defers = new HashSet<AST_Block>();

  Dictionary<string, uint> func2ip = new Dictionary<string, uint>();
  public Dictionary<string, uint> Func2Ip {
    get {
      return func2ip;
    }
  }

  Dictionary<uint, string> imports = new Dictionary<uint, string>();
  
  Dictionary<byte, OpDefinition> opcode_decls = new Dictionary<byte, OpDefinition>();

  public Compiler(GlobalScope globs = null, AST ast = null, FileModule module = null)
  {
    DeclareOpcodes();

    this.globs = globs;
    this.symbols = new LocalScope(globs);
    this.ast = ast;
    this.module = module;

    UseByteCode();
  }

  //NOTE: public for testing purposes only
  public Compiler UseByteCode()
  {
    if(code_stack.Count != 1)
      throw new Exception("Invalid state");
    code_stack[0] = bytecode;
    return this;
  }

  //NOTE: public for testing purposes only
  public Compiler UseInitCode()
  {
    if(code_stack.Count != 1)
      throw new Exception("Invalid state");
    code_stack[0] = initcode;
    return this;
  }

  public void Compile()
  {
    Visit(ast);
  }

  Bytecode PeekCode()
  {
    return this.code_stack[this.code_stack.Count-1];
  }

  uint PushCode()
  {
    code_stack.Add(new Bytecode());

    return (uint)code_stack[0].Length;
  }
 
  Bytecode PopCode(bool auto_append = true)
  {
    var curr_scope = PeekCode();
    if(auto_append)
      code_stack[0].Write(curr_scope);
    code_stack.RemoveAt(code_stack.Count-1);
    return curr_scope;
  }

  int AddConstant(AST_Literal lt)
  {
    return AddConstant(new Const(lt));
  }

  int AddConstant(Const new_cn)
  {
    for(int i = 0 ; i < constants.Count; ++i)
    {
      var cn = constants[i];
      if(cn.IsEqual(new_cn))
        return i;
    }
    constants.Add(new_cn);
    return constants.Count-1;
  }

  //TODO: use implicit conversion in Const ctor?
  int AddConstant(string str)
  {
    return AddConstant(new Const(str));
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
        operand_width = new int[] { 2 /*local idx*/, 1 /*type*/ }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.ArgVar,
        operand_width = new int[] { 2 /*local idx*/ }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.SetVar,
        operand_width = new int[] { 2 /*local idx*/ }
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
        name = Opcodes.SetMVarInplace,
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
        name = Opcodes.GetFunc,
        operand_width = new int[] { 4/*ip*/ }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.GetFuncNative,
        operand_width = new int[] { 4/*idx*/, 4/*type idx*/ }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.GetFuncFromVar,
        operand_width = new int[] { 4/*idx*/ }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.GetFuncImported,
        operand_width = new int[] { 4/*module idx*/, 4/*func idx*/ }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.Call,
        operand_width = new int[] { 4/*args bits*/ }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.CallNative,
        operand_width = new int[] { 4/*args bits*/ }
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
        operand_width = new int[] { 1, 2 }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.New,
        operand_width = new int[] { 4 }
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
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.ClassBegin,
        operand_width = new int[] { 4/*type idx*/, 4/*parent type idx*/ }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.ClassMember,
        operand_width = new int[] { 4/*nname*/, 4/*ntype*/ }
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.ClassEnd
      }
    );
    DeclareOpcode(
      new OpDefinition()
      {
        name = Opcodes.Import,
        operand_width = new int[] { 4/*literal idx*/}
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

  //NOTE: public for testing purposes only
  public Compiler Emit(Opcodes op, int[] operands = null)
  {
    var curr_scope = PeekCode();
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
            throw new Exception("Operand value(1 byte) is out of bounds: " + op_val);
          buf.Write((byte)op_val);
        break;
        case 2:
          if(ushort.MinValue > op_val || op_val > ushort.MaxValue)
            throw new Exception("Operand value(2 bytes) is out of bounds: " + op_val);
          buf.Write((ushort)op_val);
        break;
        case 4:
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
    int patch_pos = PeekCode().Position;
    //body
    Visit(ast.children[idx+1]);
    return patch_pos;
  }

  void PatchJumpOffsetToCurrPos(int jump_opcode_pos)
  {
    var curr_scope = PeekCode();
    int offset = curr_scope.Position - jump_opcode_pos;
    if(offset < 0 || Bytecode.GetBytesRequired((uint)offset) > 1) 
      throw new Exception("Invalid offset: " + offset);
    curr_scope.PatchAt(jump_opcode_pos - 1, (byte)offset);
  }

  public byte[] GetByteCode()
  {
    return bytecode.GetBytes();
  }

  public byte[] GetInitCode()
  {
    return initcode.GetBytes();
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
    for(int i=0;i<ast.module_names.Count;++i)
    {
      imports.Add(ast.module_ids[i], ast.module_names[i]);
      int module_idx = AddConstant(ast.module_names[i]);

      UseInitCode();
      Emit(Opcodes.Import, new int[] { module_idx });
      UseByteCode();
    }
  }

  public override void DoVisit(AST_FuncDecl ast)
  {
    func_decls.Add(ast);
    uint ip = PushCode();
    func2ip.Add(ast.name, ip);
    if(ast.local_vars_num > 0)
      Emit(Opcodes.InitFrame, new int[] { (int)ast.local_vars_num });
    if(ast.default_args_num > 0)
      Emit(Opcodes.ArgVar, new int[] { (int)ast.local_vars_num-1 });
    VisitChildren(ast);
    Emit(Opcodes.Return);
    PopCode();
    func_decls.RemoveAt(func_decls.Count-1);
  }

  public override void DoVisit(AST_LambdaDecl ast)
  {
    PushCode();
    //NOTE: since lambda's body can appear anywhere in the 
    //      compiled code we skip it by uncoditional jump over it
    Emit(Opcodes.Jump, new int[] {(int)Bytecode.GetMaxValueForBytes(2)/*dummy placeholder*/});
    VisitChildren(ast);
    Emit(Opcodes.Return);
    var bytecode = PopCode(auto_append: false);

    long jump_pos = bytecode.Length - 2;
    if(jump_pos > Bytecode.GetMaxValueForBytes(2))
      throw new Exception("Too large lambda body");
    //let's patch the jump placeholder with the actual jump position
    bytecode.PatchAt(1, (ushort)jump_pos, max_bytes: 2, gap_filler: (byte)Opcodes.Nop);

    uint ip = 2;//taking into account 'jump out of lambda'
    for(int i=0;i < code_stack.Count;++i)
      ip += (uint)code_stack[i].Length;
    func2ip.Add(ast.name, ip);

    PeekCode().Write(bytecode);

    Emit(Opcodes.Lambda, new int[] {(int)ip, (int)ast.local_vars_num});
    foreach(var p in ast.uses)
      Emit(Opcodes.UseUpval, new int[]{(int)p.upsymb_idx, (int)p.symb_idx});
  }

  public override void DoVisit(AST_ClassDecl ast)
  {
    UseInitCode();

    var name = ast.Name();
    //TODO:?
    //CheckNameIsUnique(name);

    var parent = symbols.Resolve(ast.ParentName()) as ClassSymbol;

    var cl = new ClassSymbolScript(name, ast, parent);
    symbols.Define(cl);

    Emit(Opcodes.ClassBegin, new int[] { AddConstant(name.s), (int)(parent == null ? -1 : AddConstant(parent.GetName().s)) });
    for(int i=0;i<ast.children.Count;++i)
    {
      var child = ast.children[i];
      var vd = child as AST_VarDecl;
      if(vd != null)
      {
        cl.Define(new FieldSymbolScript(vd.name, vd.type));
        //TODO: Use Constant mechanism for actual string name storage.
        //      This should be useful for reflection.
        Emit(Opcodes.ClassMember, new int[] { AddConstant(vd.type), AddConstant(vd.name) });
      }
    }
    Emit(Opcodes.ClassEnd);

    UseByteCode();
  }

  public override void DoVisit(AST_EnumDecl ast)
  {
    throw new Exception("Not supported : " + ast);
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
            last_jmp_op_pos = PeekCode().Position;
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
        int block_ip = PeekCode().Position;
        int cond_op_pos = EmitConditionAndBody(ast, 0);
        Emit(Opcodes.LoopJump, new int[] { PeekCode().Position - block_ip
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
    code_stack.Add(block_code);
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

    code_stack.RemoveAt(code_stack.Count-1);
    ctrl_blocks.RemoveAt(ctrl_blocks.Count-1);

    if(need_to_enter_block)
      Emit(Opcodes.Block, new int[] { (int)ast.type, block_code.Position});
    PeekCode().Write(block_code);
  }

  void VisitDefer(AST_Block ast)
  {
    var parent_block = ctrl_blocks.Count > 0 ? ctrl_blocks[ctrl_blocks.Count-1] : null;
    if(parent_block != null)
      block_has_defers.Add(parent_block);

    var block_code = new Bytecode();
    code_stack.Add(block_code);
    VisitChildren(ast);
    code_stack.RemoveAt(code_stack.Count-1);

    Emit(Opcodes.Block, new int[] { (int)ast.type, block_code.Position});
    PeekCode().Write(block_code);
  }

  public override void DoVisit(AST_TypeCast ast)
  {
    VisitChildren(ast);
    Emit(Opcodes.TypeCast, new int[] { AddConstant(ast.type) });
  }

  public override void DoVisit(AST_New ast)
  {
    Emit(Opcodes.New, new int[] { AddConstant(ast.type) });
    VisitChildren(ast);
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
          Emit(Opcodes.GetFunc, new int[] {(int)offset});
          Emit(Opcodes.Call, new int[] {(int)ast.cargs_bits});
        }
        else if(globs.Resolve(ast.name) is FuncSymbolNative fsymb)
        {
          int func_idx = globs.GetMembers().IndexOf(fsymb);
          if(func_idx == -1)
            throw new Exception("Func '" + ast.name + "' idx not found in symbols");
          VisitChildren(ast);
          Emit(Opcodes.GetFuncNative, new int[] {(int)func_idx, 0});
          Emit(Opcodes.CallNative, new int[] {(int)ast.cargs_bits});
        }
        else if(ast.nname2 != module.id)
        {
          VisitChildren(ast);

          var import_name = imports[ast.nname2];

          int module_idx = AddConstant(import_name);
          if(module_idx > ushort.MaxValue)
            throw new Exception("Can't encode module literal in ushort: " + module_idx);
          int func_idx = AddConstant(ast.name);
          if(func_idx > ushort.MaxValue)
            throw new Exception("Can't encode func literal in ushort: " + func_idx);

          Emit(Opcodes.GetFuncImported, new int[] {(int)module_idx, (int)func_idx});
          Emit(Opcodes.Call, new int[] {(int)ast.cargs_bits});
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

        Emit(Opcodes.GetMVar, new int[] { AddConstant(ast.scope_type), memb_idx});
      }
      break;
      case EnumCall.MVARW:
      {
        var class_symb = symbols.Resolve(ast.scope_type) as ClassSymbol;
        if(class_symb == null)
          throw new Exception("Class type not found: " + ast.scope_type);
        int memb_idx = class_symb.members.FindStringKeyIndex(ast.name);
        if(memb_idx == -1)
          throw new Exception("Member '" + ast.name + "' not found in class: " + ast.scope_type);

        VisitChildren(ast);

        Emit(Opcodes.SetMVar, new int[] { AddConstant(ast.scope_type), memb_idx});
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
        Emit(Opcodes.GetFuncNative, new int[] {memb_idx, (int)ast.scope_ntype});
        Emit(Opcodes.CallNative, new int[] {0});
      }
      break;
      case EnumCall.ARR_IDX:
      {
        Emit(Opcodes.GetFuncNative, new int[] {GenericArrayTypeSymbol.IDX_At, GenericArrayTypeSymbol.INT_CLASS_TYPE});
        Emit(Opcodes.CallNative, new int[] {0});
      }
      break;
      case EnumCall.ARR_IDXW:
      {
        Emit(Opcodes.GetFuncNative, new int[] {GenericArrayTypeSymbol.IDX_SetAt, GenericArrayTypeSymbol.INT_CLASS_TYPE});
        Emit(Opcodes.CallNative, new int[] {0});
      }
      break;
      case EnumCall.FUNC_PTR_POP:
      {
        Emit(Opcodes.Call, new int[] {0});
      }
      break;
      case EnumCall.FUNC_PTR:
      {
        Emit(Opcodes.GetFuncFromVar, new int[] {(int)ast.symb_idx});
        Emit(Opcodes.Call, new int[] {0});
      }
      break;
      default:
        throw new Exception("Not supported call: " + ast.type);
    }
  }

  public override void DoVisit(AST_Return ast)
  {
    VisitChildren(ast);
    Emit(Opcodes.ReturnVal);
  }

  public override void DoVisit(AST_Break ast)
  {
    throw new Exception("Not supported : " + ast);
  }

  public override void DoVisit(AST_PopValue ast)
  {
    throw new Exception("Not supported : " + ast);
  }

  public override void DoVisit(AST_Literal ast)
  {
    Emit(Opcodes.Constant, new int[] { AddConstant(ast) });
  }

  public override void DoVisit(AST_BinaryOpExp ast)
  {
    VisitChildren(ast);

    switch(ast.type)
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
        throw new Exception("Not supported binary type: " + ast.type);
    }
  }

  public override void DoVisit(AST_UnaryOpExp ast)
  {
    VisitChildren(ast);

    switch(ast.type)
    {
      case EnumUnaryOp.NOT:
        Emit(Opcodes.UnaryNot);
      break;
      case EnumUnaryOp.NEG:
        Emit(Opcodes.UnaryNeg);
      break;
      default:
        throw new Exception("Not supported unary type: " + ast.type);
    }
  }

  public byte MapToValType(uint ntype)
  {
    if(ntype == SymbolTable.symb_int.GetName().n1 || ntype == SymbolTable.symb_float.GetName().n1)
      return Val.NUMBER;
    else if(ntype == SymbolTable.symb_string.GetName().n1)
      return Val.STRING;
    else if(ntype == SymbolTable.symb_bool.GetName().n1)
      return Val.BOOL;
    else
      return Val.OBJ;
  }

  public override void DoVisit(AST_VarDecl ast)
  {
    //checking of there are default args
    if(ast.is_func_arg && ast.children.Count > 0)
    {                  
      var func_decl = func_decls[func_decls.Count-1];
      Emit(Opcodes.DefArg, new int[] { (int)ast.symb_idx - func_decl.required_args_num, 0 /*dummy placeholder for jump position*/ });
      var pos = PeekCode().Position;
      VisitChildren(ast);
      PatchJumpOffsetToCurrPos(pos);
    }

    if(!ast.is_func_arg)
    {
      byte type = MapToValType(ast.ntype);
      Emit(Opcodes.DeclVar, new int[] { (int)ast.symb_idx, (int)type });
    }
    else
      Emit(Opcodes.ArgVar, new int[] { (int)ast.symb_idx });
  }

  public override void DoVisit(bhl.AST_JsonObj ast)
  {
    Emit(Opcodes.New, new int[] { (int)ast.ntype });
    VisitChildren(ast);
  }

  public override void DoVisit(bhl.AST_JsonArr ast)
  {
    var arr_symb = symbols.Resolve(ast.ntype) as ArrayTypeSymbol;
    if(arr_symb == null)
      throw new Exception("Could not find class binding: " + ast.ntype);

    Emit(Opcodes.New, new int[] { (int)ast.ntype });

    for(int i=0;i<ast.children.Count;++i)
    {
      var c = ast.children[i];

      //checking if there's an explicit add to array operand
      if(c is AST_JsonArrAddItem)
      {
        Emit(Opcodes.GetFuncNative, new int[] {GenericArrayTypeSymbol.IDX_AddInplace, (int)ast.ntype});
        Emit(Opcodes.CallNative, new int[] {0});
      }
      else
        Visit(c);
    }

    //adding last item item
    if(ast.children.Count > 0)
    {
      Emit(Opcodes.GetFuncNative, new int[] {GenericArrayTypeSymbol.IDX_AddInplace, (int)ast.ntype});
      Emit(Opcodes.CallNative, new int[] {0});
    }
  }

  public override void DoVisit(bhl.AST_JsonPair ast)
  {
    VisitChildren(ast);
    Emit(Opcodes.SetMVarInplace, new int[] { (int)ast.scope_ntype, (int)ast.symb_idx });
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
