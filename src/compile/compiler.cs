using System;
using System.Collections.Generic;

//NOTE: removing related warnings due to CLSCompliant annotations in ANTLR generated code
[assembly: CLSCompliant(false)]

namespace bhl;

public class ModuleCompiler : AST_Visitor
{
  AST_Tree ast;
  Module interim;
  Module result;

  Types ts
  {
    get { return interim.ts;  }
  }

  List<Const> constants = new List<Const>();
  TypeRefIndex type_refs = new TypeRefIndex();
  List<string> imports = new List<string>();
  List<Instruction> init = new List<Instruction>();
  List<Instruction> code = new List<Instruction>();
  List<Instruction> head = null;

  internal class LambdaCode
  {
    internal LambdaSymbol symbol;
    internal List<Instruction> code;
  }
  List<List<LambdaCode>> lambda_stacks = new List<List<LambdaCode>>();
  int lambdas_stack_idx = -1;

  IScope curr_scope;

  Stack<FuncSymbol> func_decls = new Stack<FuncSymbol>();
  HashSet<AST_Block> ctrl_block_has_defers = new HashSet<AST_Block>();

  Stack<List<AST_Block>> func_ctrl_blocks = new Stack<List<AST_Block>>();

  List<AST_Block> ctrl_blocks
  {
    get { return func_ctrl_blocks.Peek(); }
  }

  Stack<AST_Block> loop_blocks = new Stack<AST_Block>();

  internal struct BlockJump
  {
    internal AST_Block block;
    internal Instruction jump_op;
  }

  List<BlockJump> non_patched_breaks = new List<BlockJump>();
  List<BlockJump> non_patched_continues = new List<BlockJump>();
  Dictionary<AST_Block, Instruction> continue_jump_markers = new Dictionary<AST_Block, Instruction>();

  internal struct Offset
  {
    internal Instruction src;
    internal Instruction dst;
    internal int operand_idx;
  }

  List<Offset> offsets = new List<Offset>();

  internal struct Patch
  {
    internal Instruction inst;
    internal System.Action<Instruction> cb;
  }

  List<Patch> patches = new List<Patch>();

  bool dump_opcodes;

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

      size = 1; //op itself
      for(int i = 0; i < operand_width.Length; ++i)
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

    public override string ToString()
    {
      return op.ToString();
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

      for(int i = 0; i < operands.Length; ++i)
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

  public ModuleCompiler(ANTLR_Processor.Result proc_res)
  {
    interim = proc_res.module;
    ast = proc_res.ast;
    curr_scope = interim.ns;

    UseInit();
  }

  //NOTE: for testing purposes only
  public ModuleCompiler()
  {
    interim = new Module(new Types());
    curr_scope = interim.ns;

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

  void PushLambdaCode(LambdaSymbol symbol)
  {
    List<LambdaCode> stack = null;
    if(lambda_stacks.Count == 0)
    {
      stack = new List<LambdaCode>();
      lambda_stacks.Add(stack);
    }
    else
      stack = lambda_stacks[^1];

    var instructions = new List<Instruction>();
    var code = new LambdaCode { code = instructions, symbol = symbol };
    lambdas_stack_idx++;
    stack.Add(code);
    head = instructions;
  }

  void PopLambdaCode()
  {
    --lambdas_stack_idx;
    if(lambdas_stack_idx == -1)
    {
      lambda_stacks.Add(new List<LambdaCode>());
      head = code;
    }
    else
      head = lambda_stacks[^1][lambdas_stack_idx].code;
  }

  void WriteLambdaCode()
  {
    foreach(var stack in lambda_stacks)
    {
      foreach(var lmb_code in stack)
      {
        lmb_code.symbol._ip_addr = GetCodeSize();
        foreach(var instr in lmb_code.code)
          head.Add(instr);
      }
    }
    lambda_stacks.Clear();
  }

  public void Compile_VisitAST()
  {
    //special case for tests where we
    //don't have any AST
    if(ast != null)
      Visit(ast);
  }

  public Module Compile_Finish()
  {
    int init_func_idx = FindInitFuncIdx();

    byte[] init_bytes;
    byte[] code_bytes;
    Ip2SrcLine ip2src_line;
    GetByteCode(out init_bytes, out code_bytes, out ip2src_line);

    interim.InitWithCompiled(
      new CompiledModule(
        init_func_idx,
        imports,
        interim.gvar_index.Count,
        constants.ToArray(),
        type_refs,
        init_bytes,
        code_bytes,
        ip2src_line
      )
    );
    return interim;
  }

  public Module Compile()
  {
    if(result == null)
    {
      Compile_VisitAST();
      Compile_PatchInstructions();
      result = Compile_Finish();
    }

    return result;
  }

  int GetCodeSize()
  {
    int size = 0;
    for(int i = 0; i < head.Count; ++i)
      size += head[i].def.size;
    return size;
  }

  int GetPosition(Instruction inst)
  {
    int pos = 0;
    bool found = false;
    for(int i = 0; i < head.Count; ++i)
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
    return AddConstant(new Const(lt.type, lt.nval, lt.sval));
  }

  int AddConstant(Const new_cn)
  {
    for(int i = 0 ; i < constants.Count; ++i)
    {
      var cn = constants[i];
      if(cn.Equals(new_cn))
        return i;
    }

    constants.Add(new_cn);
    return constants.Count - 1;
  }

  int AddTypeRef(IType itype)
  {
    var tp = new ProxyType(itype);
    type_refs.Index(tp);
    return type_refs.GetIndex(tp);
  }

  int AddConstant(string str)
  {
    return AddConstant(new Const(str));
  }

  static void DeclareOpcodes()
  {
    DeclareOpcode(
      new Definition(
        Opcodes.Constant,
        3 /*const idx*/
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
        Opcodes.BitShr
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.BitShl
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
        Opcodes.EqualLite
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Equal
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
        Opcodes.UnaryBitNot
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.DeclVar,
        1 /*local idx*/, 3 /*type idx*/
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
        Opcodes.SetVarScalar,
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
        Opcodes.GetVarScalar,
        1 /*local idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.SetGVar,
        3 /*idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.GetGVar,
        3 /*idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.SetAttr,
        2 /*member idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.SetAttrInplace,
        2 /*member idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.GetAttr,
        2 /*member idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.GetFuncLocalPtr,
        3 /*func idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.GetFuncPtr,
        2 /*imported module idx*/, 3 /*func idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.GetFuncNativePtr,
        2 /*imported module idx*/, 3 /*func idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.GetFuncIpPtr,
        3 /*func ip*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.CallLocal,
        3 /*func ip*/, 4 /*args bits*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.CallGlobNative,
        3 /*func idx*/, 4 /*args bits*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.CallNative,
        2 /*imported module idx*/, 3 /*module's func idx*/, 4 /*args bits*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Call,
        2 /*imported module idx*/, 3 /*module's func ip*/, 4 /*args bits*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.CallMethod,
        2 /*class member idx*/, 4 /*args bits*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.CallMethodNative,
        2 /*class member idx*/, 4 /*args bits*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.CallMethodIface,
        2 /*class member idx*/, 3 /*type idx*/, 4 /*args bits*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.CallMethodIfaceNative,
        2 /*class member idx*/, 3 /*type idx*/, 4 /*args bits*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.CallMethodVirt,
        2 /*class member idx*/, 4 /*args bits*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.CallFuncPtr,
        4 /*args bits*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.CallFuncPtrInv,
        4 /*args bits*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.SetUpval,
        1 /*upval src idx*/, 1 /*local dst idx*/, 1 /*flags*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Frame,
        1 /*total local vars*/, 1 /*returned args num*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Defer,
        2 /*len*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Scope,
        2 /*len*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Paral,
        2 /*len*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.ParalAll,
        2 /*len*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Return
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Nop
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
        2 /*rel.offset*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.JumpPeekZ,
        2 /*rel.offset*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.JumpPeekNZ,
        2 /*rel.offset*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.DefArg,
        1 /*arg idx*/, 1 /*default arg idx*/, 2 /*jump out of def.arg calc pos*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.New,
        3 /*type idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.TypeCast,
        3 /*type idx*/, 1 /*force type*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.TypeAs,
        3 /*type idx*/, 1 /*force type*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.TypeIs,
        3 /*type idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Typeof,
        3 /*type idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Inc,
        1 /*local idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.Dec,
        1 /*local idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.ArrIdx
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.ArrIdxW
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.ArrAddInplace
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.MapIdx
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.MapIdxW
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.MapAddInplace
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.DeclRef,
        1 /*local idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.GetRef,
        1 /*local idx*/
      )
    );
    DeclareOpcode(
      new Definition(
        Opcodes.SetRef,
        1 /*local idx*/
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
    return head[head.Count - 1];
  }

  void Pop()
  {
    RemovePatches(head[head.Count - 1]);
    head.RemoveAt(head.Count - 1);
  }

  Instruction Emit(Opcodes op, int[] operands = null, int line_num = 0)
  {
    var inst = new Instruction(op);
    if(inst.operands.Length != (operands == null ? 0 : operands.Length))
      throw new Exception("Invalid operands amount for opcode:" + op);
    for(int i = 0; i < operands?.Length; ++i)
      inst.SetOperand(i, operands[i]);
    inst.line_num = line_num;
    head.Add(inst);
    if(dump_opcodes)
      Console.WriteLine(inst.ToString());
    return inst;
  }

  //NOTE: for testing purposes only
  public ModuleCompiler EmitChain(Opcodes op, params int[] operands)
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
    Visit(ast.children[idx + 1]);
    return jump_op;
  }

  void PatchBreaks(AST_Block block)
  {
    for(int i = non_patched_breaks.Count; i-- > 0;)
    {
      var npb = non_patched_breaks[i];
      if(npb.block == block)
      {
        AddOffsetFromTo(npb.jump_op, Peek());
        non_patched_breaks.RemoveAt(i);
      }
    }
  }

  void PatchContinues(AST_Block block)
  {
    for(int i = non_patched_continues.Count; i-- > 0;)
    {
      var npc = non_patched_continues[i];
      if(npc.block == block)
      {
        AddOffsetFromTo(npc.jump_op, continue_jump_markers[npc.block]);
        non_patched_continues.RemoveAt(i);
      }
    }

    continue_jump_markers.Clear();
  }

  void PatchLater(Instruction inst, System.Action<Instruction> cb)
  {
    patches.Add(new Patch()
    {
      inst = inst,
      cb = cb
    });
  }

  void RemovePatches(Instruction inst)
  {
    for(int i = patches.Count; i-- > 0;)
    {
      if(patches[i].inst == inst)
      {
        patches.RemoveAt(i);
        return;
      }
    }
  }

  void AddOffsetFromTo(Instruction src, Instruction dst, int operand_idx = 0)
  {
    offsets.Add(new Offset()
    {
      src = src,
      dst = dst,
      operand_idx = operand_idx
    });
  }

  bool HasOffsetsTo(Instruction dst)
  {
    foreach(var offset in offsets)
      if(offset.dst == dst)
        return true;
    return false;
  }

  void PatchOffsets()
  {
    //1. let's calculate absolute positions of all instructions
    int pos = 0;
    for(int i = 0; i < code.Count; ++i)
    {
      pos += code[i].def.size;
      code[i].pos = pos;
    }

    //2. let's patch jump candidates
    for(int i = 0; i < offsets.Count; ++i)
    {
      var jp = offsets[i];
      if(jp.dst.pos <= 0)
        throw new Exception("Invalid position of destination instruction: " + jp.dst.def.name);

      if(jp.src.pos <= 0)
        throw new Exception("Invalid position of source instruction: " + jp.src.def.name);

      int offset = jp.dst.pos - jp.src.pos;

      jp.src.SetOperand(jp.operand_idx, offset);
    }
  }

  public void Compile_PatchInstructions()
  {
    for(int i = 0; i < patches.Count; ++i)
    {
      var p = patches[i];
      p.cb(p.inst);
    }

    PatchOffsets();
  }

  void GetByteCode(out byte[] init_bytes, out byte[] code_bytes, out Ip2SrcLine ip2src_line)
  {
    {
      var bytecode = new Bytecode();
      for(int i = 0; i < init.Count; ++i)
        init[i].Write(bytecode);
      init_bytes = bytecode.GetBytes();
    }

    {
      var bytecode = new Bytecode();
      for(int i = 0; i < code.Count; ++i)
        code[i].Write(bytecode);
      code_bytes = bytecode.GetBytes();
    }

    ip2src_line = new Ip2SrcLine();
    int pos = 0;
    for(int i = 0; i < code.Count; ++i)
    {
      pos += code[i].def.size;
      ip2src_line.Add(pos - 1, code[i].line_num);
    }
  }

  int FindInitFuncIdx()
  {
    var fs = curr_scope.Resolve("init") as FuncSymbol;
    if(fs == null)
      return -1;
    return fs.scope == curr_scope && fs.attribs.HasFlag(FuncAttrib.Static) ? fs.scope_idx : -1;
  }

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
    for(int i = 0; i < ast.module_names.Count; ++i)
    {
      if(!imports.Contains(ast.module_names[i]))
        imports.Add(ast.module_names[i]);
    }
  }

  public override void DoVisit(AST_FuncDecl ast)
  {
    UseCode();

    func_decls.Push(ast.symbol);
    EmitFuncDecl(ast);
    func_decls.Pop();

    WriteLambdaCode();

    UseInit();
  }

  void EmitFuncDecl(AST_FuncDecl ast)
  {
    var fsymb = ast.symbol;

    fsymb._ip_addr = GetCodeSize();

    Emit(Opcodes.Frame,
      new int[] { fsymb._local_vars_num, fsymb.GetReturnedArgsNum() },
      ast.symbol.origin.source_line
    );
    VisitChildren(ast);

    //let's insert return only if the previous instruction is not return as well
    if(Peek().op != Opcodes.Return)
      Emit(Opcodes.Return, null, ast.last_line_num);
  }

  public override void DoVisit(AST_LambdaDecl ast)
  {
    var lambda = (LambdaSymbol)ast.symbol;
    //NOTE: pushing here, processing all lambda pieces
    //      of code in AST_FuncDecl processing
    PushLambdaCode(lambda);
    EmitFuncDecl(ast);
    PopLambdaCode();

    var lambda_op = Emit(Opcodes.GetFuncIpPtr, new int[] { -1 /*patched later*/ }, ast.last_line_num);
    PatchLater(lambda_op, (inst) => inst.operands[0] = lambda._ip_addr);
    foreach(var up in lambda.upvals)
      Emit(Opcodes.SetUpval, new int[] {(int)up.upsymb_idx, (int)up.symb_idx, (int)up.mode}, up.line_num);
  }

  void EmitLamdbdaPtrInit(LambdaSymbol lambda, int line)
  {
  }

  public override void DoVisit(AST_ClassDecl ast)
  {
    var scope_bak = curr_scope;
    curr_scope = ast.symbol;

    VisitChildren(ast);

    curr_scope = scope_bak;
  }

  public override void DoVisit(AST_Block ast)
  {
    switch(ast.type)
    {
      case BlockType.IF:

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
        for(; i < ast.children.Count; i += 2)
        {
          //break if there's only 'else leftover' left
          if((i + 2) > ast.children.Count)
            break;

          var if_op = EmitConditionPlaceholderAndBody(ast, i);
          if(last_jmp_op != null)
            AddOffsetFromTo(last_jmp_op, Peek());

          //check if uncoditional jump out of 'if body' is required,
          //it's required only if there are other 'else if' or 'else'
          if(ast.children.Count > 2)
          {
            last_jmp_op = Emit(Opcodes.Jump, new int[] { 0 });
            AddOffsetFromTo(if_op, Peek());
          }
          else
            AddOffsetFromTo(if_op, Peek());
        }

        //check fo 'else leftover'
        if(i != ast.children.Count)
        {
          Visit(ast.children[i]);
          AddOffsetFromTo(last_jmp_op, Peek());
        }

        break;
      case BlockType.WHILE:
      {
        if(ast.children.Count != 2)
          throw new Exception("Unexpected amount of children (must be 2)");

        loop_blocks.Push(ast);

        var begin_op = Peek();

        var cond_op = EmitConditionPlaceholderAndBody(ast, 0);

        //to the beginning of the loop
        var jump_op = Emit(Opcodes.Jump, new int[] { 0 });
        AddOffsetFromTo(jump_op, begin_op);

        //patch 'jump out of the loop' position
        AddOffsetFromTo(cond_op, Peek());

        PatchContinues(ast);
        PatchBreaks(ast);

        loop_blocks.Pop();
      }
        break;
      case BlockType.DOWHILE:
      {
        if(ast.children.Count != 2)
          throw new Exception("Unexpected amount of children (must be 2)");

        loop_blocks.Push(ast);

        var begin_op = Peek();

        Visit(ast.children[0]);
        Visit(ast.children[1]);
        var cond_op = Emit(Opcodes.JumpZ, new int[] { 0 });

        //to the beginning of the loop
        var jump_op = Emit(Opcodes.Jump, new int[] { 0 });
        AddOffsetFromTo(jump_op, begin_op);

        //patch 'jump out of the loop' position
        AddOffsetFromTo(cond_op, Peek());

        PatchContinues(ast);

        PatchBreaks(ast);

        loop_blocks.Pop();
      }
        break;
      case BlockType.FUNC:
      {
        func_ctrl_blocks.Push(new List<AST_Block>());
        VisitChildren(ast);
        func_ctrl_blocks.Pop();
      }
        break;
      case BlockType.SEQ:
      case BlockType.PARAL:
      case BlockType.PARAL_ALL:
      {
        VisitControlBlock(ast);
      }
        break;
      case BlockType.DEFER:
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
    var parent_block = ctrl_blocks.Count > 0 ? ctrl_blocks[ctrl_blocks.Count - 1] : null;

    bool parent_is_paral =
      parent_block != null &&
      (parent_block.type == BlockType.PARAL ||
       parent_block.type == BlockType.PARAL_ALL);

    bool is_paral =
      ast.type == BlockType.PARAL ||
      ast.type == BlockType.PARAL_ALL;

    ctrl_blocks.Add(ast);

    Opcodes block_type_op;
    if(ast.type == BlockType.PARAL)
      block_type_op = Opcodes.Paral;
    else if(ast.type == BlockType.PARAL_ALL)
      block_type_op = Opcodes.ParalAll;
    else if(ast.type == BlockType.SEQ)
      block_type_op = Opcodes.Scope;
    else
      throw new Exception("Not supported block type: " + ast.type);

    var block_op = Emit(block_type_op, new int[] { 0 /*patched later*/});

    for(int i = 0; i < ast.children.Count; ++i)
    {
      var child = ast.children[i];

      //NOTE: let's automatically wrap all children with sequence if
      //      they are inside paral block
      if(is_paral &&
         (!(child is AST_Block child_block) ||
          (child_block.type != BlockType.SEQ && child_block.type != BlockType.DEFER)))
      {
        var seq_child = new AST_Block(BlockType.SEQ);
        seq_child.children.Add(child);
        ast.children[i] = seq_child;
        child = seq_child;
      }

      Visit(child);
    }

    ctrl_blocks.RemoveAt(ctrl_blocks.Count - 1);

    bool need_block =
      is_paral ||
      parent_is_paral ||
      ctrl_block_has_defers.Contains(ast) ||
      HasOffsetsTo(block_op);

    if(need_block)
      AddOffsetFromTo(block_op, Peek());
    else
      head.Remove(block_op);
  }

  void VisitDefer(AST_Block ast)
  {
    var parent_block = ctrl_blocks.Count > 0 ? ctrl_blocks[ctrl_blocks.Count - 1] : null;
    if(parent_block != null)
      ctrl_block_has_defers.Add(parent_block);

    var block_op = Emit(Opcodes.Defer, new int[] { 0 /*patched later*/});
    VisitChildren(ast);
    AddOffsetFromTo(block_op, Peek());
  }

  public override void DoVisit(AST_TypeCast ast)
  {
    VisitChildren(ast);

    if(CastCanBeOmitted(ast.type, ast.hint_exp_type))
      return;

    Emit(Opcodes.TypeCast, new int[] { AddTypeRef(ast.type), ast.force_type ? 1 : 0 }, ast.line_num);
  }

  static bool CastCanBeOmitted(IType cast_type, IType exp_type)
  {
    //TODO: add test for this case
    //if(cast_type != null && cast_type == exp_type)
    //  return true;

    if(cast_type == Types.Int && exp_type is EnumSymbol)
      return true;

    return false;
  }

  public override void DoVisit(AST_TypeAs ast)
  {
    VisitChildren(ast);
    Emit(Opcodes.TypeAs, new int[] { AddTypeRef(ast.type), ast.force_type ? 1 : 0 }, ast.line_num);
  }

  public override void DoVisit(AST_TypeIs ast)
  {
    VisitChildren(ast);
    Emit(Opcodes.TypeIs, new int[] { AddTypeRef(ast.type) }, ast.line_num);
  }

  public override void DoVisit(AST_Typeof ast)
  {
    Emit(Opcodes.Typeof, new int[] { AddTypeRef(ast.type) });
  }

  public override void DoVisit(AST_New ast)
  {
    Emit(Opcodes.New, new int[] { AddTypeRef(ast.type) }, ast.line_num);
    VisitChildren(ast);
  }

  public override void DoVisit(AST_Inc ast)
  {
    Emit(Opcodes.Inc, new int[] { ast.symbol.scope_idx });
  }

  public override void DoVisit(AST_Dec ast)
  {
    Emit(Opcodes.Dec, new int[] { ast.symbol.scope_idx });
  }

  public override void DoVisit(AST_Call ast)
  {
    bool is_global = ast.symbol?.scope is Namespace;
    var field_symb = ast.symbol as FieldSymbol;
    bool is_static = field_symb != null && field_symb.attribs.HasFlag(FieldAttrib.Static);
    if(is_static)
      is_global = true;
    var vs = ast.symbol as VariableSymbol;
    bool is_ref_origin = vs?._is_ref_decl ?? false;
    bool is_ref = vs?._is_ref ?? false;

    switch(ast.type)
    {
      case EnumCall.VAR:
      {
        if(is_global)
        {
          //NOTE: native static fields are implemented as native functions
          if(is_static && field_symb.scope is ClassSymbolNative cs)
          {
            var cs_mod = cs.GetModule();
            var get_var_op = Emit(Opcodes.CallNative,
              new int[] { 0, cs_mod.nfunc_index.IndexOf(cs.GetNativeStaticFieldGetFuncName(field_symb)), 0 }, ast.line_num);
            TrySetFuncInstructionImportModule(get_var_op, cs_mod);
          }
          else
            //NOTE: we use local module gvars index instead of symbol's scope index, since it can be an imported symbol
            Emit(Opcodes.GetGVar, new int[] {interim.gvar_index.IndexOf(ast.symbol)}, ast.line_num);
        }
        else if(field_symb != null)
        {
          if(ast.symb_idx == -1)
            throw new Exception("Member '" + ast.symbol?.name + "' idx is not valid");
          VisitChildren(ast);
          Emit(Opcodes.GetAttr, new int[] {ast.symb_idx}, ast.line_num);
        }
        else if(is_ref && !ast.pass_as_ref)
          Emit(Opcodes.GetRef, new int[] {ast.symb_idx}, ast.line_num);
        else
          Emit(Opcodes.GetVar, //safe general version
          //Emit(!is_ref && ast.symbol is ITyped typed && Types.IsScalar(typed.GetIType()) ? Opcodes.GetVarScalar : Opcodes.GetVar,
            new int[] {ast.symb_idx}, ast.line_num);
      }
        break;
      case EnumCall.VARW:
      case EnumCall.VARWDCL:
      {
        if(ast.type == EnumCall.VARWDCL && is_ref_origin)
          Emit(Opcodes.DeclRef, new int[] {ast.symb_idx}, ast.line_num);

        if(is_global)
        {
          //NOTE: native static fields are implemented as native functions
          if(is_static && field_symb.scope is ClassSymbolNative cs)
          {
            var cs_mod = cs.GetModule();
            var set_var_op = Emit(Opcodes.CallNative,
              new int[] { 0, cs_mod.nfunc_index.IndexOf(cs.GetNativeStaticFieldSetFuncName(field_symb)), 0 }, ast.line_num);
            TrySetFuncInstructionImportModule(set_var_op, cs_mod);
          }
          else
            //NOTE: we use local module gvars index instead of symbol's scope index, since it can be an imported symbol
            Emit(Opcodes.SetGVar, new int[] {interim.gvar_index.IndexOf(ast.symbol)}, ast.line_num);
        }
        else if(field_symb != null)
        {
          if(ast.symb_idx == -1)
            throw new Exception("Member '" + ast.symbol?.name + "' idx is not valid");
          VisitChildren(ast);
          Emit(Opcodes.SetAttr, new int[] {ast.symb_idx}, ast.line_num);
        }
        else if(is_ref)
          Emit(Opcodes.SetRef, new int[] {ast.symb_idx}, ast.line_num);
        else
          Emit(Opcodes.SetVar, //safe general version
          //Emit(!is_ref && ast.symbol is ITyped typed && Types.IsScalar(typed.GetIType()) ? Opcodes.SetVarScalar : Opcodes.SetVar,
            new int[] {ast.symb_idx}, ast.line_num);
      }
        break;
      case EnumCall.FUNC:
      {
        VisitChildren(ast);
        var instr = EmitGetFuncAddr(ast);
        //let's optimize some primitive calls
        if(instr.op == Opcodes.GetFuncLocalPtr)
        {
          Pop();
          var fsymb = (FuncSymbolScript)ast.symbol;
          var call_op = Emit(Opcodes.CallLocal, new int[] {-1 /*patched later*/, (int)ast.cargs_bits}, ast.line_num);
          PatchLater(call_op, (inst) => inst.operands[0] = fsymb._ip_addr);
        }
        else if(instr.op == Opcodes.GetFuncPtr)
        {
          var fsymb = (FuncSymbolScript)ast.symbol;
          var fmod = fsymb.GetModule();
          Pop();
          var call_op = Emit(Opcodes.Call, new int[] {instr.operands[0], -1 /*patched later*/, (int)ast.cargs_bits},
            ast.line_num);
          //imports list is filled later so we need to take that into account
          PatchLater(call_op, (inst) =>
          {
            inst.operands[1] = fsymb._ip_addr;
            if(inst.operands[1] == -1)
              throw new Exception("Could not link func '" + fsymb.name + "' from module '" + fmod.name + "'");
          });
        }
        else if(instr.op == Opcodes.GetFuncNativePtr)
        {
          Pop();

          if(ast.symbol == Prelude.DumpOpcodesOn)
          {
            Console.WriteLine("=== Opcodes dump start");
            dump_opcodes = true;
          }
          else if(ast.symbol == Prelude.DumpOpcodesOff)
          {
            Console.WriteLine("=== Opcodes dump end");
            dump_opcodes = false;
          }
          //let's check if it's a builtin native function
          else if(instr.operands[0] == 0)
            Emit(Opcodes.CallGlobNative, new int[] {instr.operands[1], (int)ast.cargs_bits}, ast.line_num);
          else
            Emit(Opcodes.CallNative, new int[] {instr.operands[0], instr.operands[1], (int)ast.cargs_bits},
              ast.line_num);
        }
        else
          Emit(Opcodes.CallFuncPtr, new int[] {(int)ast.cargs_bits}, ast.line_num);
      }
        break;
      case EnumCall.MFUNC:
      {
        var instance_type = ast.symbol.scope as IInstantiable;
        if(instance_type == null)
          throw new Exception("Not instance type: " + ast.symbol.name);

        var mfunc = ast.symbol as FuncSymbol;
        if(mfunc == null)
          throw new Exception("Class method '" + ast.symbol?.name + "' not found in type '" + instance_type.GetName() +
                              "' by index " + ast.symb_idx);

        VisitChildren(ast);

        if(instance_type is InterfaceSymbolScript)
          Emit(Opcodes.CallMethodIface, new int[] {ast.symb_idx, AddTypeRef(instance_type), (int)ast.cargs_bits},
            ast.line_num);
        else if(instance_type is InterfaceSymbolNative)
          Emit(Opcodes.CallMethodIfaceNative, new int[] {ast.symb_idx, AddTypeRef(instance_type), (int)ast.cargs_bits},
            ast.line_num);
        else
        {
          if(mfunc is FuncSymbolScript)
          {
            if(mfunc._virtual != null)
              Emit(Opcodes.CallMethodVirt, new int[] {mfunc._virtual.scope_idx, (int)ast.cargs_bits}, ast.line_num);
            else
              Emit(Opcodes.CallMethod, new int[] {ast.symb_idx, (int)ast.cargs_bits}, ast.line_num);
          }
          else if(mfunc is FuncSymbolNative)
            Emit(Opcodes.CallMethodNative, new int[] {ast.symb_idx, (int)ast.cargs_bits}, ast.line_num);
          else
            throw new Exception("Unsupported type: " + mfunc.GetType().Name);
        }
      }
        break;
      case EnumCall.ARR_IDX:
      {
        Emit(Opcodes.ArrIdx, null, ast.line_num);
      }
        break;
      case EnumCall.ARR_IDXW:
      {
        Emit(Opcodes.ArrIdxW, null, ast.line_num);
      }
        break;
      case EnumCall.MAP_IDX:
      {
        Emit(Opcodes.MapIdx, null, ast.line_num);
      }
        break;
      case EnumCall.MAP_IDXW:
      {
        Emit(Opcodes.MapIdxW, null, ast.line_num);
      }
        break;
      case EnumCall.FUNC_PTR_VAR:
      {
        VisitChildren(ast);
        Emit(ast.symbol is VariableSymbol var_symb && var_symb._is_ref ? Opcodes.GetRef : Opcodes.GetVar, new int[] {ast.symb_idx}, ast.line_num);
        Emit(Opcodes.CallFuncPtr, new int[] {(int)ast.cargs_bits}, ast.line_num);
      }
        break;
      case EnumCall.FUNC_PTR_RES:
      case EnumCall.FUNC_PTR_MVAR:
      {
        VisitChildren(ast);
        //if there are no args let's just call the regular opcode
        Emit(ast.cargs_bits == 0 ? Opcodes.CallFuncPtr : Opcodes.CallFuncPtrInv, new int[] {(int)ast.cargs_bits}, ast.line_num);
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
    var func_symb = ast.symbol as FuncSymbol;
    if(func_symb == null)
      throw new Exception("Symbol '" + ast.symbol?.name + "' is not a func");

    if(func_symb is FuncSymbolNative func_symb_native)
    {
      var fmod = func_symb.GetModule();
      int func_idx = fmod.nfunc_index.IndexOf(func_symb_native);
      if(func_idx == -1)
        throw new Exception("Not found function '" + func_symb.name + "' index in module '" +  fmod.name + "'");
      var get_ptr_op = Emit(Opcodes.GetFuncNativePtr, new int[] { 0, func_idx }, ast.line_num);
      TrySetFuncInstructionImportModule(get_ptr_op, fmod);
      return get_ptr_op;
    }
    else
    {
      var func_symb_script = (FuncSymbolScript)func_symb;
      //check if it's a local function
      if(func_symb_script.GetModule() == interim)
      {
        var fmod = func_symb_script.GetModule();
        int func_idx = fmod.func_index.IndexOf(func_symb_script);
        if(func_idx == -1)
          throw new Exception("Not found function '" + func_symb_script.name + "' index in module '" +  fmod.name +
                              "'");
        return Emit(Opcodes.GetFuncLocalPtr, new int[] { func_idx }, ast.line_num);
      }
      else
      {
        var fmod = func_symb_script.GetModule();
        int func_idx = fmod.func_index.IndexOf(func_symb_script);
        if(func_idx == -1)
          throw new Exception("Not found function '" + func_symb_script.name + "' index in module '" +  fmod.name +
                              "'");
        int mod_idx = imports.IndexOf(fmod.name);
        if(mod_idx == -1)
          throw new Exception("Not found module '" + fmod.name + "' imported index");
        return Emit(Opcodes.GetFuncPtr, new int[] { mod_idx, func_idx }, ast.line_num);
      }
    }
  }

  //NOTE: we need to patch only in case the native function is imported from some module,
  //      for built-in native functions we don't do that
  void TrySetFuncInstructionImportModule(Instruction inst_op, Module mod)
  {
    if(inst_op.operands[1] == -1)
      throw new Exception("Invalid func index for module '" +  mod.name + "'");

    //if module is not global it must be imported
    if(ts.IsImported(mod))
    {
      int mod_idx = imports.IndexOf(mod.name);
      if(mod_idx == -1)
        throw new Exception("Not found module '" + mod.name + "' imported index");
      //NOTE: using convention where built-in module is always at index 0
      //      and imported modules are at (mod_idx + 1)
      inst_op.operands[0] = mod_idx + 1;
    }
    else
      inst_op.operands[0] = 0;
  }

  public override void DoVisit(AST_Return ast)
  {
    VisitChildren(ast);
    //let's insert return only if the previous instruction is not return as well
    if(Peek().op != Opcodes.Return)
      Emit(Opcodes.Return, null, ast.line_num);
  }

  public override void DoVisit(AST_Break ast)
  {
    var jump_op = Emit(Opcodes.Jump, new int[] { 0 /*patched later*/});
    non_patched_breaks.Add(
      new BlockJump()
      {
        block = loop_blocks.Peek(),
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
      var jump_op = Emit(Opcodes.Jump, new int[] { 0 /*patched later*/});
      non_patched_continues.Add(
        new BlockJump()
        {
          block = loop_block,
          jump_op = jump_op
        }
      );
    }
  }

  public override void DoVisit(AST_Yield ast)
  {
    //TODO: do we need a separate opcode for that?
    Emit(Opcodes.CallGlobNative, new int[] {ts.module.nfunc_index.IndexOf(Prelude.YieldFunc), 0}, ast.line_num);
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
        Emit(Opcodes.And, null, ast.line_num);
        AddOffsetFromTo(jump_op, Peek());
      }
        break;
      case EnumBinaryOp.OR:
      {
        Visit(ast.children[0]);
        var jump_op = Emit(Opcodes.JumpPeekNZ, new int[] { 0 /*patched later*/});
        Visit(ast.children[1]);
        Emit(Opcodes.Or, null, ast.line_num);
        AddOffsetFromTo(jump_op, Peek());
      }
        break;
      case EnumBinaryOp.BIT_AND:
        VisitChildren(ast);
        Emit(Opcodes.BitAnd, null, ast.line_num);
        break;
      case EnumBinaryOp.BIT_OR:
        VisitChildren(ast);
        Emit(Opcodes.BitOr, null, ast.line_num);
        break;
      case EnumBinaryOp.BIT_SHR:
        VisitChildren(ast);
        Emit(Opcodes.BitShr, null, ast.line_num);
        break;
      case EnumBinaryOp.BIT_SHL:
        VisitChildren(ast);
        Emit(Opcodes.BitShl, null, ast.line_num);
        break;
      case EnumBinaryOp.MOD:
        VisitChildren(ast);
        Emit(Opcodes.Mod, null, ast.line_num);
        break;
      case EnumBinaryOp.ADD:
      {
        VisitChildren(ast);
        Emit(Opcodes.Add, null, ast.line_num);
      }
        break;
      case EnumBinaryOp.SUB:
        VisitChildren(ast);
        Emit(Opcodes.Sub, null, ast.line_num);
        break;
      case EnumBinaryOp.DIV:
        VisitChildren(ast);
        Emit(Opcodes.Div, null, ast.line_num);
        break;
      case EnumBinaryOp.MUL:
        VisitChildren(ast);
        Emit(Opcodes.Mul, null, ast.line_num);
        break;
      case EnumBinaryOp.EQ:
        VisitChildren(ast);
        Emit(Opcodes.EqualLite, null, ast.line_num);
        break;
      case EnumBinaryOp.EQEX:
        VisitChildren(ast);
        Emit(Opcodes.Equal, null, ast.line_num);
        break;
      case EnumBinaryOp.LT:
        VisitChildren(ast);
        Emit(Opcodes.LT, null, ast.line_num);
        break;
      case EnumBinaryOp.LTE:
        VisitChildren(ast);
        Emit(Opcodes.LTE, null, ast.line_num);
        break;
      case EnumBinaryOp.GT:
        VisitChildren(ast);
        Emit(Opcodes.GT, null, ast.line_num);
        break;
      case EnumBinaryOp.GTE:
        VisitChildren(ast);
        Emit(Opcodes.GTE, null, ast.line_num);
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
      case EnumUnaryOp.BIT_NOT:
        Emit(Opcodes.UnaryBitNot);
        break;
      default:
        throw new Exception("Not supported unary type: " + ast.type);
    }
  }

  public override void DoVisit(AST_VarDecl ast)
  {
    bool is_func_arg = ast.symb is FuncArgSymbol;

    //checking if there are default args
    if(is_func_arg)
    {
      if(ast.children.Count > 0)
      {
        var curr_func = func_decls.Peek();
        var arg_op = Emit(Opcodes.DefArg, new int[]
          { ast.symb_idx,
            //let's take into account 'this' special case, which is
            //stored at 0 idx and is not part of func args
            //(which are stored in the very beginning)
            ast.symb_idx - curr_func.GetRequiredArgsNum() - (curr_func.scope is ClassSymbol ? 1 : 0),
            0 /*offset patched later*/
          });
        //might need to visit default arguments init code
        VisitChildren(ast);
        if(ast.symb._is_ref_decl)
        {
          //in case it's a reference we must make sure it's properly wrapped before it's set
          //(we can't put the code in the top because default arguments opcode inserts gaps
          //into local variables thus affecting them)
          Emit(Opcodes.DeclRef, new int[] { ast.symb_idx });
          Emit(Opcodes.SetRef, new int[] { ast.symb_idx });
          Emit(Opcodes.Jump, new int[] { 2 /*DeclRef opcode below*/ });
        }
        else
        {
          Emit(Opcodes.SetVar, new int[] { ast.symb_idx });
        }
        //we want to jump out of default arguments related code
        AddOffsetFromTo(arg_op, Peek(), operand_idx: 2);
      }

      if(ast.symb._is_ref_decl)
        Emit(Opcodes.DeclRef, new int[] { ast.symb_idx });
    }
    else
    {
      if(ast.symb._is_ref_decl)
        Emit(Opcodes.DeclRef, new int[] { ast.symb_idx });
      else
        Emit(Opcodes.DeclVar, new int[] { ast.symb_idx, AddTypeRef(ast.type) });
    }
  }

  public override void DoVisit(bhl.AST_JsonObj ast)
  {
    Emit(Opcodes.New, new int[] { AddTypeRef(ast.type) }, ast.line_num);
    VisitChildren(ast);
  }

  public override void DoVisit(bhl.AST_JsonArr ast)
  {
    var arr_symb = ast.type as ArrayTypeSymbol;
    if(arr_symb == null)
      throw new Exception("Could not find class binding: " + ast.type.GetName());

    Emit(Opcodes.New, new int[] { AddTypeRef(ast.type) }, ast.line_num);

    for(int i = 0; i < ast.children.Count; ++i)
    {
      var c = ast.children[i];

      //checking if there's an explicit add to array operand
      if(c is AST_JsonArrAddItem)
        Emit(Opcodes.ArrAddInplace);
      else
        Visit(c);
    }

    //adding last item item
    if(ast.children.Count > 0)
      Emit(Opcodes.ArrAddInplace);
  }

  public override void DoVisit(bhl.AST_JsonArrAddItem ast)
  {
    //NOTE: it's handled above
  }

  public override void DoVisit(bhl.AST_JsonMap ast)
  {
    var map_symb = ast.type as MapTypeSymbol;
    if(map_symb == null)
      throw new Exception("Could not find class binding: " + ast.type.GetName());

    Emit(Opcodes.New, new int[] { AddTypeRef(ast.type) }, ast.line_num);

    for(int i = 0; i < ast.children.Count; ++i)
    {
      var c = ast.children[i];

      //checking if there's an explicit add to array operand
      if(c is AST_JsonMapAddItem)
        Emit(Opcodes.MapAddInplace);
      else
        Visit(c);
    }

    //adding last item item
    if(ast.children.Count > 0)
      Emit(Opcodes.MapAddInplace);
  }

  public override void DoVisit(bhl.AST_JsonMapAddItem ast)
  {
    //NOTE: it's handled above
  }

  public override void DoVisit(bhl.AST_JsonPair ast)
  {
    VisitChildren(ast);
    Emit(Opcodes.SetAttrInplace, new int[] { (int)ast.symb_idx }, ast.line_num);
  }

  public static void Dump(byte[] bs)
  {
    string res = "";

    Definition op = null;
    int op_size = 0;

    for(int i = 0; i < bs?.Length; i++)
    {
      res += string.Format("{1:00} 0x{0:x2} {0}", bs[i], i);
      if(op != null)
      {
        --op_size;
        if(op_size == 0)
          op = null;
      }
      else
      {
        op = LookupOpcode((Opcodes)bs[i]);
        op_size = PredictOpcodeSize(op, bs, i);
        res += "(" + op.name.ToString() + ")";
        if(op_size == 0)
          op = null;
      }

      res += "\n";
    }

    Console.WriteLine(res);
  }

  public static int PredictOpcodeSize(Definition op, byte[] bytes, int start_pos)
  {
    if(op.operand_width == null)
      return 0;
    int pos = start_pos;
    foreach(int ow in op.operand_width)
      Bytecode.Decode(bytes, ow, ref pos);
    return pos - start_pos;
  }
}
