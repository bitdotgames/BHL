using System;
using System.IO;
using System.Collections.Generic;

namespace bhl {

public class ModuleCompiler : AST_Visitor
{
  AST ast;
  CompiledModule compiled;

  Module module;
  public Module Module {
    get {
      return module;
    }
  }

  TypeSystem types;
  public TypeSystem Types {
    get {
      return types;
    }
  }

  List<Const> constants = new List<Const>();
  public List<Const> Constants {
    get {
      return constants;
    }
  }

  List<Instruction> init = new List<Instruction>();
  List<Instruction> code = new List<Instruction>();
  List<Instruction> head = null;

  Stack<AST_Block> ctrl_blocks = new Stack<AST_Block>();
  Stack<AST_Block> loop_blocks = new Stack<AST_Block>();

  internal struct BlockJump
  {
    internal AST_Block block;
    internal Instruction jump_op;
  }
  List<BlockJump> non_patched_breaks = new List<BlockJump>();
  List<BlockJump> non_patched_continues = new List<BlockJump>();
  Dictionary<AST_Block, Instruction> continue_jump_markers = new Dictionary<AST_Block, Instruction>();

  Stack<FuncSymbol> func_decls = new Stack<FuncSymbol>();
  HashSet<AST_Block> block_has_defers = new HashSet<AST_Block>();

  internal struct Jump
  {
    internal Instruction jump_op;
    internal Instruction dst;
    internal int operand_idx;
  }
  List<Jump> jumps = new List<Jump>();

  Dictionary<uint, string> imports = new Dictionary<uint, string>();
  
  static Dictionary<byte, Definition> opcode_decls = new Dictionary<byte, Definition>();

  public class Definition
  {
    public Opcodes name { get; }
     //each array item represents the size of the operand in bytes
    public int[] operand_width { get; }
    public int size { get; }

    public Definition(Opcodes name, params int[] operand_width)
    {
      this.name = name;
      this.operand_width = operand_width;

      size = 1;//op itself
      for(int i=0;i<operand_width.Length;++i)
        size += operand_width[i];
    }
  }

  public class Instruction
  {
    public Definition def { get; }
    public Opcodes op { get; }
    public int[] operands { get; }
    public int line_num;
    //It's set automatically during final code traversal
    //and reflects an actual absolute instruction byte position in
    //a module. Used for post patching of jump instructions.
    public int pos;

    public Instruction(Opcodes op)
    {
      def = LookupOpcode(op);
      this.op = op;
      operands = new int[def.operand_width.Length];
    }

    public Instruction SetOperand(int idx, int op_val)
    {
      if(def.operand_width == null || def.operand_width.Length <= idx)
        throw new Exception("Invalid operand for opcode:" + op + ", at index:" + idx);

      int width = def.operand_width[idx];

      switch(width)
      {
        case 1:
          if(op_val < sbyte.MinValue || op_val > sbyte.MaxValue)
            throw new Exception("Operand value(1 byte) is out of bounds: " + op_val);
        break;
        case 2:
          if(op_val < short.MinValue || op_val > short.MaxValue)
            throw new Exception("Operand value(2 bytes) is out of bounds: " + op_val);
        break;
        case 3:
          if(op_val < -8388607 || op_val > 8388607)
            throw new Exception("Operand value(3 bytes) is out of bounds: " + op_val);
        break;
        case 4:
        break;
        default:
          throw new Exception("Not supported operand width: " + width + " for opcode:" + op);
      }

      operands[idx] = op_val;

      return this;
    }
    
    public void Write(Bytecode code)
    {
      code.Write8((uint)op);

      for(int i=0;i<operands.Length;++i)
      {
        int op_val = operands[i];
        int width = def.operand_width[i];
        switch(width)
        {
          case 1:
            code.Write8((uint)op_val);
          break;
          case 2:
            code.Write16((uint)op_val);
          break;
          case 3:
            code.Write24((uint)op_val);
          break;
          case 4:
            code.Write32((uint)op_val);
          break;
          default:
            throw new Exception("Not supported operand width: " + width + " for opcode:" + op);
        }
      }
    }
  }

  static ModuleCompiler()
  {
    DeclareOpcodes();
  }

  public ModuleCompiler(TypeSystem types, FrontendResult fres)
  {
    this.types = types;
    module = fres.module;
    ast = fres.ast;

    UseInit();
  }

  //NOTE: for testing purposes only
  public ModuleCompiler()
  {
    types = new TypeSystem();
    module = new Module(types.globs, new ModulePath("", ""));

    UseInit();
  }

  //NOTE: public for testing purposes only
  public ModuleCompiler UseCode()
  {
    head = code;
    return this;
  }

  //NOTE: public for testing purposes only
  public ModuleCompiler UseInit()
  {
    head = init;
    return this;
  }

  public CompiledModule Compile()
  {
    if(compiled == null)
    {
      //special case for tests where we
      //don't have any AST
      if(ast != null)
        Visit(ast);

      byte[] init_bytes;
      byte[] code_bytes;
      Dictionary<int, int> ip2src_line;
      Bake(out init_bytes, out code_bytes, out ip2src_line);

      compiled = new CompiledModule(
        module.id,
        module.name, 
        module.symbols,
        constants, 
        init_bytes,
        code_bytes,
        ip2src_line
      );    
    }

    return compiled;
  }

  int GetCodeSize()
  {
    int size = 0;
    for(int i=0;i<head.Count;++i)
      size += head[i].def.size;
    return size;
  }

  int GetPosition(Instruction inst)
  {
    int pos = 0;
    bool found = false;
    for(int i=0;i<head.Count;++i)
    {
      var tmp = head[i];
      pos += tmp.def.size;
      if(inst == tmp)
      {
        found = true;
        break;
      }
    }
    if(!found)
      throw new Exception("Instruction was not found: " + inst.op);
    return pos;
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

  static void DeclareOpcodes()
  {
    DeclareOpcode(
      new Definition(
        Opcodes.Constant,
        3/*const idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.And
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Or
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.BitAnd
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.BitOr
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Mod
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Add
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Sub
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Div
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Mul
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Equal
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.NotEqual
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.LT
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.LTE
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.GT
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.GTE
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.UnaryNot
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.UnaryNeg
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.DeclVar,
        1 /*local idx*/, 3 /*type string idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.ArgVar,
        1 /*local idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.SetVar,
        1 /*local idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.GetVar,
        1 /*local idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.SetGVar,
        3/*idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.GetGVar,
        3/*idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.SetGVarImported,
        3/*module name idx*/, 3/*idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.GetGVarImported,
        3/*module name idx*/, 3/*idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.ArgRef,
        1 /*local idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.SetAttr,
        3/*class type idx*/, 2/*member idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.SetAttrInplace,
        3/*class type idx*/, 2/*member idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.GetAttr,
        3/*class type idx*/, 2/*member idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.RefAttr,
        3/*class type idx*/, 2/*member idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.GetFunc,
        3/*module ip*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.GetFuncNative,
        3/*globs idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.FuncPtrToTop,
        4/*args bits*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.GetFuncFromVar,
        1/*local idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.GetFuncImported,
        3/*func name idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Call,
        3/*func ip*/, 4/*args bits*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.CallNative,
        3/*globs idx*/, 4/*args bits*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.CallImported,
        3/*func name idx*/, 4/*args bits*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.CallMethod,
        2/*class member idx*/, 3/*type literal idx*/, 4/*args bits*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.CallMethodNative,
        2/*class member idx*/, 3/*type literal idx*/, 4/*args bits*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.CallPtr,
        4/*args bits*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Lambda,
        2/*rel.offset skip lambda pos*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.UseUpval,
        1/*upval src idx*/, 1/*local dst idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.InitFrame,
        1/*total local vars*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Block,
        1/*type*/, 2/*len*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Return
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.ReturnVal,
        1/*returned amount*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Pop
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Jump,
        2 /*rel.offset*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.JumpZ,
        2/*rel.offset*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.JumpPeekZ,
        2/*rel.offset*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.JumpPeekNZ,
        2/*rel.offset*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Break,
        2 /*rel.offset*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Continue,
        2 /*rel.offset*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.DefArg,
        1/*local scope idx*/, 2/*jump out of def.arg calc pos*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.New,
        3/*type idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.TypeCast,
        3/*type idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Inc,
        1/*local idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Dec,
        1/*local idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Import,
        4/*name idx*/
      )
    );
  }

  static void DeclareOpcode(Definition def)
  {
    opcode_decls.Add((byte)def.name, def);
  }

  static public Definition LookupOpcode(Opcodes op)
  {
    Definition def;
    if(!opcode_decls.TryGetValue((byte)op, out def))
       throw new Exception("No such opcode definition: " + op);
    return def;
  }

  Instruction Peek()
  {
    return head[head.Count-1];
  }

  void Pop()
  {
    head.RemoveAt(head.Count-1);
  }

  Instruction Emit(Opcodes op, int[] operands = null, int line_num = 0)
  {
    var inst = new Instruction(op);
    if(inst.operands.Length != (operands == null ? 0 : operands.Length))
      throw new Exception("Invalid operands amount for opcode:" + op);
    for(int i=0;i<operands?.Length;++i)
      inst.SetOperand(i, operands[i]);
    inst.line_num = line_num;
    head.Add(inst);
    return inst;
  }

  //NOTE: for testing purposes only
  public ModuleCompiler EmitThen(Opcodes op, params int[] operands)
  {
    Emit(op, operands);
    return this;
  }

  //NOTE: returns jump instruction to be patched later
  Instruction EmitConditionPlaceholderAndBody(AST_Block ast, int idx)
  {
    //condition
    Visit(ast.children[idx]);
    var jump_op = Emit(Opcodes.JumpZ, new int[] { 0 });
    Visit(ast.children[idx+1]);
    return jump_op;
  }

  void PatchBreaks(AST_Block block)
  {
    for(int i=non_patched_breaks.Count;i-- > 0;)
    {
      var npb = non_patched_breaks[i];
      if(npb.block == block)
      {
        AddJumpFromTo(npb.jump_op, Peek());
        non_patched_breaks.RemoveAt(i);
      }
    }
  }

  void PatchContinues(AST_Block block)
  {
    for(int i=non_patched_continues.Count;i-- > 0;)
    {
      var npc = non_patched_continues[i];
      if(npc.block == block)
      {
        AddJumpFromTo(npc.jump_op, continue_jump_markers[npc.block]);
        non_patched_continues.RemoveAt(i);
      }
    }
    continue_jump_markers.Clear();
  }

  void AddJumpFromTo(Instruction jump_op, Instruction dst, int operand_idx = 0)
  {
    jumps.Add(new Jump() {
      jump_op = jump_op,
      dst = dst,
      operand_idx = operand_idx
    });
  }

  void PatchJumps()
  {
    //1. let's calculate absolute positions of all instructions
    int pos = 0;
    for(int i=0;i<code.Count;++i)
    {
      pos += code[i].def.size;
      code[i].pos = pos; 
    }

    //2. let's patch jump candidates
    for(int i=0;i<jumps.Count;++i)
    {
      var jp = jumps[i];
      int offset = jp.dst.pos - jp.jump_op.pos;
      jp.jump_op.SetOperand(jp.operand_idx, offset);
    }
  }

  public void Bake(out byte[] init_bytes, out byte[] code_bytes, out Dictionary<int, int> ip2src_line)
  {
    PatchJumps();

    {
      var bytecode = new Bytecode();
      for(int i=0;i<init.Count;++i)
        init[i].Write(bytecode);
      init_bytes = bytecode.GetBytes();
    }

    {
      var bytecode = new Bytecode();
      for(int i=0;i<code.Count;++i)
        code[i].Write(bytecode);
      code_bytes = bytecode.GetBytes();
    }

    ip2src_line = new Dictionary<int, int>();
    int pos = 0;
    //TODO: should we take into account init code?
    for(int i=0;i<code.Count;++i)
    {
      pos += code[i].def.size;
      ip2src_line.Add(pos-1, code[i].line_num);
    }
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

      Emit(Opcodes.Import, new int[] { module_idx });
    }
  }

  public override void DoVisit(AST_FuncDecl ast)
  {
    UseCode();

    var fsymb = (FuncSymbolScript)module.symbols.Resolve(ast.name);

    func_decls.Push(fsymb);

    fsymb.ip_addr = GetCodeSize();

    Emit(Opcodes.InitFrame, new int[] { fsymb.local_vars_num + 1/*cargs bits*/});
    VisitChildren(ast);
    Emit(Opcodes.Return);

    func_decls.Pop();
  }

  public override void DoVisit(AST_LambdaDecl ast)
  {
    var lmbd_op = Emit(Opcodes.Lambda, new int[] { 0 /*patched later*/});
    //skipping lambda opcode
    Emit(Opcodes.InitFrame, new int[] { (int)ast.local_vars_num + 1/*cargs bits*/});
    VisitChildren(ast);
    Emit(Opcodes.Return);
    AddJumpFromTo(lmbd_op, Peek());

    foreach(var p in ast.upvals)
      Emit(Opcodes.UseUpval, new int[]{(int)p.upsymb_idx, (int)p.symb_idx});
  }

  public override void DoVisit(AST_ClassDecl ast)
  {
    //UseInit();

    //ClassSymbol parent = null;
    //if(!string.IsNullOrEmpty(ast.parent))
    //  parent = (ClassSymbol)symbols.Resolve(ast.parent);

    //curr_class_symb = new ClassSymbolScript(ast.name, ast, parent);
    //symbols.Define(curr_class_symb);

    //Emit(Opcodes.ClassBegin, new int[] { AddConstant(ast.name), (int)(parent == null ? -1 : AddConstant(parent.name)) });
    //for(int i=0;i<ast.children.Count;++i)
    //{
    //  var child = ast.children[i];

    //  if(child is AST_VarDecl vd)
    //  {
    //    curr_class_symb.Define(new FieldSymbolScript(vd.name, /*tmp hackk*/TypeSystem.IsCompoundType(vd.type) ? types.Type("void") : types.Type(vd.type)));
    //    Emit(Opcodes.ClassMember, new int[] { AddConstant(vd.type), AddConstant(vd.name), (int)vd.symb_idx });
    //  }
    //  else if(child is AST_FuncDecl fd)
    //  {
    //    Visit(fd);
    //  }
    //}
    //Emit(Opcodes.ClassEnd);
    //curr_class_symb = null;
  }

  public override void DoVisit(AST_EnumDecl ast)
  {
    //symbols.Define(new EnumSymbolScript(ast.name));
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

        Instruction last_jmp_op = null;
        int i = 0;
        //NOTE: amount of children is even if there's no 'else'
        for(;i < ast.children.Count; i += 2)
        {
          //break if there's only 'else leftover' left
          if((i + 2) > ast.children.Count)
            break;

          var if_op = EmitConditionPlaceholderAndBody(ast, i);
          if(last_jmp_op != null)
            AddJumpFromTo(last_jmp_op, Peek());

          //check if uncoditional jump out of 'if body' is required,
          //it's required only if there are other 'else if' or 'else'
          if(ast.children.Count > 2)
          {
            last_jmp_op = Emit(Opcodes.Jump, new int[] { 0 });
            AddJumpFromTo(if_op, Peek());
          }
          else
            AddJumpFromTo(if_op, Peek());
        }
        //check fo 'else leftover'
        if(i != ast.children.Count)
        {
          Visit(ast.children[i]);
          AddJumpFromTo(last_jmp_op, Peek());
        }
      break;
      case EnumBlock.WHILE:
      {
        if(ast.children.Count != 2)
         throw new Exception("Unexpected amount of children (must be 2)");

        loop_blocks.Push(ast);

        var begin_op = Peek();

        var cond_op = EmitConditionPlaceholderAndBody(ast, 0);

        //to the beginning of the loop
        var jump_op = Emit(Opcodes.Jump, new int[] { 0 });
        AddJumpFromTo(jump_op, begin_op);

        //patch 'jump out of the loop' position
        AddJumpFromTo(cond_op, Peek());

        PatchContinues(ast);

        PatchBreaks(ast);

        loop_blocks.Pop();
      }
      break;
      case EnumBlock.FUNC:
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
    var parent_block = ctrl_blocks.Count > 0 ? ctrl_blocks.Peek() : null;

    bool parent_is_paral =
      parent_block != null &&
      (parent_block.type == EnumBlock.PARAL ||
       parent_block.type == EnumBlock.PARAL_ALL);

    bool is_paral = 
      ast.type == EnumBlock.PARAL || 
      ast.type == EnumBlock.PARAL_ALL;

    ctrl_blocks.Push(ast);

    var block_op = Emit(Opcodes.Block, new int[] { (int)ast.type, 0/*patched later*/});

    for(int i=0;i<ast.children.Count;++i)
    {
      var child = ast.children[i];

      //NOTE: let's automatically wrap all children with sequence if 
      //      they are inside paral block
      if(is_paral && 
          (!(child is AST_Block child_block) || 
           (child_block.type != EnumBlock.SEQ && child_block.type != EnumBlock.DEFER)))
      {
        var seq_child = new AST_Block();
        seq_child.type = EnumBlock.SEQ;
        seq_child.children.Add(child);
        child = seq_child;
      }

      Visit(child);
    }

    ctrl_blocks.Pop();

    bool need_block = is_paral || parent_is_paral || block_has_defers.Contains(ast);

    if(need_block)
      AddJumpFromTo(block_op, Peek(), operand_idx: 1);
    else
      head.Remove(block_op); 
  }

  void VisitDefer(AST_Block ast)
  {
    var parent_block = ctrl_blocks.Count > 0 ? ctrl_blocks.Peek() : null;
    if(parent_block != null)
      block_has_defers.Add(parent_block);

    var block_op = Emit(Opcodes.Block, new int[] { (int)ast.type, 0/*patched later*/});
    VisitChildren(ast);
    AddJumpFromTo(block_op, Peek(), operand_idx: 1);
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

  public override void DoVisit(AST_Dec ast)
  {
    Emit(Opcodes.Dec, new int[] { (int)ast.symb_idx });
  }

  public override void DoVisit(AST_Call ast)
  {  
    switch(ast.type)
    {
      case EnumCall.VARW:
      {
        Emit(Opcodes.SetVar, new int[] {ast.symb_idx}, ast.line_num);
      }
      break;
      case EnumCall.VAR:
      {
        Emit(Opcodes.GetVar, new int[] {ast.symb_idx}, ast.line_num);
      }
      break;
      case EnumCall.GVAR:
      {
        if(ast.module_id != module.path.id)
        {
          var import_name = imports[ast.module_id];
          int module_idx = AddConstant(import_name);
          Emit(Opcodes.GetGVarImported, new int[] {module_idx, ast.symb_idx}, ast.line_num);
        }
        else 
        {
          Emit(Opcodes.GetGVar, new int[] {ast.symb_idx}, ast.line_num);
        }
      }
      break;
      case EnumCall.GVARW:
      {
        if(ast.module_id != module.path.id)
        {
          var import_name = imports[ast.module_id];
          int module_idx = AddConstant(import_name);
          Emit(Opcodes.SetGVarImported, new int[] {module_idx, ast.symb_idx}, ast.line_num);
        }
        else 
        {
          Emit(Opcodes.SetGVar, new int[] {ast.symb_idx}, ast.line_num);
        }
      }
      break;
      case EnumCall.FUNC:
      {
        VisitChildren(ast);
        var instr = EmitGetFuncAddr(ast);
        //let's optimize some primitive calls
        if(instr.op == Opcodes.GetFunc)
        {
          Pop();
          Emit(Opcodes.Call, new int[] {instr.operands[0], (int)ast.cargs_bits}, ast.line_num);
        }
        else if(instr.op == Opcodes.GetFuncNative)
        {
          Pop();
          Emit(Opcodes.CallNative, new int[] {instr.operands[0], (int)ast.cargs_bits}, ast.line_num);
        }
        else if(instr.op == Opcodes.GetFuncImported)
        {
          Pop();
          Emit(Opcodes.CallImported, new int[] {instr.operands[0], (int)ast.cargs_bits}, ast.line_num);
        }
        else
          Emit(Opcodes.CallPtr, new int[] {(int)ast.cargs_bits}, ast.line_num);
      }
      break;
      case EnumCall.MVAR:
      {
        if(ast.symb_idx == -1)
          throw new Exception("Member '" + ast.name + "' idx is not valid: " + ast.scope_type);

        VisitChildren(ast);

        Emit(Opcodes.GetAttr, new int[] { AddConstant(ast.scope_type), ast.symb_idx}, ast.line_num);
      }
      break;
      case EnumCall.MVARW:
      {
        if(ast.symb_idx == -1)
          throw new Exception("Member '" + ast.name + "' idx is not valid: " + ast.scope_type);

        VisitChildren(ast);

        Emit(Opcodes.SetAttr, new int[] { AddConstant(ast.scope_type), ast.symb_idx}, ast.line_num);
      }
      break;
      case EnumCall.MFUNC:
      {
        var class_symb = module.symbols.Resolve(ast.scope_type) as ClassSymbol;
        if(class_symb == null)
          throw new Exception("Class type not found: " + ast.scope_type);

        var mfunc = class_symb.members.TryAt(ast.symb_idx) as FuncSymbol;
        if(mfunc == null)
          throw new Exception("Class method '" + ast.name + "' not found in type '" + ast.scope_type + "' by index " + ast.symb_idx);

        VisitChildren(ast);
        
        if(mfunc is FuncSymbolScript)
          Emit(Opcodes.CallMethod, new int[] {ast.symb_idx, AddConstant(ast.scope_type), (int)ast.cargs_bits}, ast.line_num);
        else
          Emit(Opcodes.CallMethodNative, new int[] {ast.symb_idx, AddConstant(ast.scope_type), (int)ast.cargs_bits}, ast.line_num);
      }
      break;
      case EnumCall.MVARREF:
      {
        if(ast.symb_idx == -1)
          throw new Exception("Member '" + ast.name + "' idx is not valid: " + ast.scope_type);

        VisitChildren(ast);

        Emit(Opcodes.RefAttr, new int[] {AddConstant(ast.scope_type), ast.symb_idx}, ast.line_num);
      }
      break;
      case EnumCall.ARR_IDX:
      {
        var arr_symb = module.symbols.Resolve(ast.scope_type) as ArrayTypeSymbol;
        Emit(Opcodes.CallMethodNative, new int[] { ((IScopeIndexed)arr_symb.Resolve("At")).scope_idx, AddConstant(arr_symb.name), 1 }, ast.line_num);
      }
      break;
      case EnumCall.ARR_IDXW:
      {
        var arr_symb = module.symbols.Resolve(ast.scope_type) as ArrayTypeSymbol;
        Emit(Opcodes.CallMethodNative, new int[] { ((IScopeIndexed)arr_symb.Resolve("SetAt")).scope_idx, AddConstant(arr_symb.name), 3 }, ast.line_num);
      }
      break;
      case EnumCall.LMBD:
      {
        VisitChildren(ast);
        Emit(Opcodes.FuncPtrToTop, new int[] {(int)ast.cargs_bits}, ast.line_num);
        Emit(Opcodes.CallPtr, new int[] {(int)ast.cargs_bits}, ast.line_num);
      }
      break;
      case EnumCall.FUNC_VAR:
      {
        VisitChildren(ast);
        Emit(Opcodes.GetFuncFromVar, new int[] {ast.symb_idx}, ast.line_num);
        Emit(Opcodes.CallPtr, new int[] {(int)ast.cargs_bits}, ast.line_num);
      }
      break;
      case EnumCall.FUNC_MVAR:
      {
        VisitChildren(ast);
        Emit(Opcodes.FuncPtrToTop, new int[] {(int)ast.cargs_bits}, ast.line_num);
        Emit(Opcodes.CallPtr, new int[] {(int)ast.cargs_bits}, ast.line_num);
      }
      break;
      case EnumCall.GET_ADDR:
      {
        EmitGetFuncAddr(ast);
      }
      break;
      default:
        throw new Exception("Not supported call: " + ast.type);
    }
  }

  Instruction EmitGetFuncAddr(AST_Call ast)
  {
    var func_symb = module.symbols.Resolve(ast.name) as FuncSymbolScript;
    if(func_symb != null)
    {
      return Emit(Opcodes.GetFunc, new int[] {func_symb.decl.ip_addr}, ast.line_num);
    }
    else if(types.Resolve(ast.name) is FuncSymbolNative fsymb)
    {
      int func_idx = types.globs.GetMembers().IndexOf(fsymb);
      if(func_idx == -1)
        throw new Exception("Func '" + ast.name + "' idx not found in symbols");
      return Emit(Opcodes.GetFuncNative, new int[] {(int)func_idx}, ast.line_num);
    }
    else if(ast.module_id != module.path.id)
    {
      int func_idx = AddConstant(ast.name);

      return Emit(Opcodes.GetFuncImported, new int[] {func_idx}, ast.line_num);
    }
    else
      throw new Exception("Func '" + ast.name + "' code not found");
  }

  public override void DoVisit(AST_Return ast)
  {
    VisitChildren(ast);
    Emit(Opcodes.ReturnVal, new int[] { ast.num });
  }

  public override void DoVisit(AST_Break ast)
  {
    var jump_op = Emit(Opcodes.Break, new int[] { 0 /*patched later*/});
    non_patched_breaks.Add(
      new BlockJump() 
        { block = loop_blocks.Peek(), 
          jump_op = jump_op
        }
    );
  }

  public override void DoVisit(AST_Continue ast)
  {
    var loop_block = loop_blocks.Peek();
    if(ast.jump_marker)
    {
      continue_jump_markers.Add(loop_block, Peek());
    }
    else
    {
      var jump_op = Emit(Opcodes.Continue, new int[] { 0 /*patched later*/});
      non_patched_continues.Add(
        new BlockJump() 
          { block = loop_block, 
            jump_op = jump_op
          }
      );
    }
  }

  public override void DoVisit(AST_PopValue ast)
  {
    Emit(Opcodes.Pop);
  }

  public override void DoVisit(AST_Literal ast)
  {
    int literal_idx = AddConstant(ast);
    Emit(Opcodes.Constant, new int[] { literal_idx });
  }

  public override void DoVisit(AST_BinaryOpExp ast)
  {
    switch(ast.type)
    {
      case EnumBinaryOp.AND:
      {
        Visit(ast.children[0]);
        var jump_op = Emit(Opcodes.JumpPeekZ, new int[] { 0 /*patched later*/});
        Visit(ast.children[1]);
        Emit(Opcodes.And);
        AddJumpFromTo(jump_op, Peek());
      }
      break;
      case EnumBinaryOp.OR:
      {
        Visit(ast.children[0]);
        var jump_op = Emit(Opcodes.JumpPeekNZ, new int[] { 0 /*patched later*/});
        Visit(ast.children[1]);
        Emit(Opcodes.Or);
        AddJumpFromTo(jump_op, Peek());
      }
      break;
      case EnumBinaryOp.BIT_AND:
        VisitChildren(ast);
        Emit(Opcodes.BitAnd);
      break;
      case EnumBinaryOp.BIT_OR:
        VisitChildren(ast);
        Emit(Opcodes.BitOr);
      break;
      case EnumBinaryOp.MOD:
        VisitChildren(ast);
        Emit(Opcodes.Mod);
      break;
      case EnumBinaryOp.ADD:
        VisitChildren(ast);
        Emit(Opcodes.Add);
      break;
      case EnumBinaryOp.SUB:
        VisitChildren(ast);
        Emit(Opcodes.Sub);
      break;
      case EnumBinaryOp.DIV:
        VisitChildren(ast);
        Emit(Opcodes.Div);
      break;
      case EnumBinaryOp.MUL:
        VisitChildren(ast);
        Emit(Opcodes.Mul);
      break;
      case EnumBinaryOp.EQ:
        VisitChildren(ast);
        Emit(Opcodes.Equal);
      break;
      case EnumBinaryOp.NQ:
        VisitChildren(ast);
        Emit(Opcodes.NotEqual);
      break;
      case EnumBinaryOp.LT:
        VisitChildren(ast);
        Emit(Opcodes.LT);
      break;
      case EnumBinaryOp.LTE:
        VisitChildren(ast);
        Emit(Opcodes.LTE);
      break;
      case EnumBinaryOp.GT:
        VisitChildren(ast);
        Emit(Opcodes.GT);
      break;
      case EnumBinaryOp.GTE:
        VisitChildren(ast);
        Emit(Opcodes.GTE);
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

  public override void DoVisit(AST_VarDecl ast)
  {
    //checking of there are default args
    if(ast.is_func_arg && ast.children.Count > 0)
    {                  
      var fsymb = func_decls.Peek();
      var arg_op = Emit(Opcodes.DefArg, new int[] { (int)ast.symb_idx - fsymb.GetRequiredArgsNum(), 0 /*patched later*/ });
      VisitChildren(ast);
      AddJumpFromTo(arg_op, Peek(), operand_idx: 1);
    }

    if(!ast.is_func_arg)
    {
      Emit(Opcodes.DeclVar, new int[] { (int)ast.symb_idx, AddConstant(ast.type) });
    }
    //check if it's not a module scope var (global)
    else if(func_decls.Count > 0)
    {
      if(ast.is_ref)
        Emit(Opcodes.ArgRef, new int[] { (int)ast.symb_idx });
      else
        Emit(Opcodes.ArgVar, new int[] { (int)ast.symb_idx });
    }
    else
    {
      Emit(Opcodes.DeclVar, new int[] { (int)ast.symb_idx, AddConstant(ast.type)});
    }
  }

  public override void DoVisit(bhl.AST_JsonObj ast)
  {
    Emit(Opcodes.New, new int[] { AddConstant(ast.type) }, ast.line_num);
    VisitChildren(ast);
  }

  public override void DoVisit(bhl.AST_JsonArr ast)
  {
    var arr_symb = module.symbols.Resolve(ast.type) as ArrayTypeSymbol;
    if(arr_symb == null)
      throw new Exception("Could not find class binding: " + ast.type);

    Emit(Opcodes.New, new int[] { AddConstant(ast.type) }, ast.line_num);

    for(int i=0;i<ast.children.Count;++i)
    {
      var c = ast.children[i];

      //checking if there's an explicit add to array operand
      if(c is AST_JsonArrAddItem)
      {
        Emit(Opcodes.CallMethodNative, new int[] { ((IScopeIndexed)arr_symb.Resolve("$AddInplace")).scope_idx, AddConstant(ast.type), 1 });
      }
      else
        Visit(c);
    }

    //adding last item item
    if(ast.children.Count > 0)
    {
      Emit(Opcodes.CallMethodNative, new int[] { ((IScopeIndexed)arr_symb.Resolve("$AddInplace")).scope_idx, AddConstant(ast.type), 1 });
    }
  }

  public override void DoVisit(bhl.AST_JsonArrAddItem ast)
  {
    //NOTE: it's handled above
  }

  public override void DoVisit(bhl.AST_JsonPair ast)
  {
    VisitChildren(ast);
    Emit(Opcodes.SetAttrInplace, new int[] { AddConstant(ast.scope_type), (int)ast.symb_idx });
  }

#endregion
}

} //namespace bhl
