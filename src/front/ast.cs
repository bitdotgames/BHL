#if (BHL_PARSER || UNITY_EDITOR)

using System;
using System.IO;
using System.Collections.Generic;
using Antlr4.Runtime.Tree;


namespace bhl
{

public enum BlockType
{
  FUNC      = 0,
  SEQ       = 1,
  DEFER     = 2,
  PARAL     = 3,
  PARAL_ALL = 4,
  IF        = 7,
  WHILE     = 8,
  FOR       = 9,
  DOWHILE   = 10,
}

public abstract class AST_Visitor
{
  public abstract void DoVisit(AST_Interim ast);
  public abstract void DoVisit(AST_Import ast);
  public abstract void DoVisit(AST_Module ast);
  public abstract void DoVisit(AST_VarDecl ast);
  public abstract void DoVisit(AST_FuncDecl ast);
  public abstract void DoVisit(AST_LambdaDecl ast);
  public abstract void DoVisit(AST_ClassDecl ast);
  public abstract void DoVisit(AST_Block ast);
  public abstract void DoVisit(AST_TypeCast ast);
  public abstract void DoVisit(AST_TypeAs ast);
  public abstract void DoVisit(AST_TypeIs ast);
  public abstract void DoVisit(AST_Typeof ast);
  public abstract void DoVisit(AST_Call ast);
  public abstract void DoVisit(AST_Return ast);
  public abstract void DoVisit(AST_Break ast);
  public abstract void DoVisit(AST_Continue ast);
  public abstract void DoVisit(AST_Yield ast);
  public abstract void DoVisit(AST_PopValue ast);
  public abstract void DoVisit(AST_Literal ast);
  public abstract void DoVisit(AST_BinaryOpExp ast);
  public abstract void DoVisit(AST_UnaryOpExp ast);
  public abstract void DoVisit(AST_New ast);
  public abstract void DoVisit(AST_Inc ast);
  public abstract void DoVisit(AST_Dec ast);
  public abstract void DoVisit(AST_JsonObj ast);
  public abstract void DoVisit(AST_JsonArr ast);
  public abstract void DoVisit(AST_JsonArrAddItem ast);
  public abstract void DoVisit(AST_JsonMap ast);
  public abstract void DoVisit(AST_JsonMapAddItem ast);
  public abstract void DoVisit(AST_JsonPair ast);

  bool error_dumped = false;
  protected bool visit_children = true;

  public void Visit(IAST ast)
  {
    if(ast == null)
      throw new Exception("NULL node");

    try
    {
      ast.Accept(this);
    }
    catch(Exception)
    {
      //let's dump error only once for the concrete last AST being processed
      //
      if(!error_dumped)
      {
        Console.Error.WriteLine("Error while visiting AST:");
        AST_Dumper.Dump(ast, visit_children: false, writer: Console.Error);
        error_dumped = true;
      }
      throw;
    }
  }

  public void VisitChildren(AST_Tree ast)
  {
    if(ast == null || !visit_children)
      return;
    var children = ast.children;
    for(int i = 0; i < children.Count; ++i)
      Visit(children[i]);
  }
}

public interface IAST
{
  //NOTE: each node dispatches to its own DoVisit overload, so adding a node is a compile
  //      error until it's handled - there's no order-sensitive type test to get wrong
  void Accept(AST_Visitor v);
}

public abstract class AST_Tree : IAST
{
  public List<IAST> children = new List<IAST>();

  public abstract void Accept(AST_Visitor v);
}

public class AST_Interim : AST_Tree
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);
}

public class AST_Import : IAST
{
  public void Accept(AST_Visitor v) => v.DoVisit(this);

  public List<string> module_names = new List<string>();
}

public class AST_Module : AST_Tree
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);

  public string name;

  public AST_Module(string name)
  {
    this.name = name;
  }
}

public enum EnumUnaryOp
{
  NEG = 1,
  NOT = 2,
  BIT_NOT = 3
}

public class AST_UnaryOpExp : AST_Tree
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);

  public EnumUnaryOp type = new EnumUnaryOp();

  public AST_UnaryOpExp(EnumUnaryOp type)
  {
    this.type = type;
  }
}

public enum EnumBinaryOp
{
  AND = 1,
  OR = 2,
  ADD = 3,
  SUB = 4,
  MUL = 5,
  DIV = 6,
  MOD = 7,
  GT = 8,
  LT = 9,
  GTE = 10,
  LTE = 11,
  EQ = 12,
  NEQ = 13,
  BIT_OR = 14,
  BIT_AND = 15,
  BIT_SHR = 16,
  BIT_SHL = 17,
  EQ_SCLR = 18,
  EQ_STR  = 19,
  CONCAT  = 20
}

public class AST_BinaryOpExp  : AST_Tree
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);

  public EnumBinaryOp type = new EnumBinaryOp();
  public int line_num;

  public AST_BinaryOpExp(EnumBinaryOp type, int line_num)
  {
    this.type = type;
    this.line_num = line_num;
  }
}

public class AST_Inc : IAST
{
  public void Accept(AST_Visitor v) => v.DoVisit(this);

  public VariableSymbol symbol;

  public AST_Inc(VariableSymbol symbol)
  {
    this.symbol = symbol;
  }
}

public class AST_Dec : IAST
{
  public void Accept(AST_Visitor v) => v.DoVisit(this);

  public VariableSymbol symbol;

  public AST_Dec(VariableSymbol symbol)
  {
    this.symbol = symbol;
  }
}

public class AST_New : AST_Tree
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);

  public IType type;
  public int line_num;

  public AST_New(IType type, int line_num)
  {
    this.type = type;
    this.line_num = line_num;
  }
}

public class AST_FuncDecl : AST_Tree
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);

  public FuncSymbolScript symbol;
  public int last_line_num;

  public AST_FuncDecl(FuncSymbolScript symbol, int last_line_num)
  {
    this.symbol = symbol;
    this.last_line_num = last_line_num;
    //fparams
    this.NewInterimChild();
    //block
    this.NewInterimChild();
  }
}

public class AST_ClassDecl : AST_Tree
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);

  public ClassSymbolScript symbol;

  public AST_ClassDecl(ClassSymbolScript symbol)
  {
    this.symbol = symbol;
  }

  public AST_FuncDecl FindFuncDecl(FuncSymbolScript fs)
  {
    foreach(var c in children)
      if(c is AST_FuncDecl fd && fd.symbol == fs)
        return fd;
    return null;
  }
}

public class AST_UpVal
{
  public VariableSymbol local;
  public VariableSymbol upval;

  public string name => local.name;
  public int local_idx => local.scope_idx;

  public int upval_idx => upval.scope_idx;

  public UpvalMode mode;

  public int line_num;

  public AST_UpVal(VariableSymbol local, VariableSymbol upval, UpvalMode mode, int line_num)
  {
    this.local = local;
    this.upval = upval;
    this.mode = mode;
    this.line_num = line_num;
  }
}

public class AST_LambdaDecl : AST_FuncDecl
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);

  public bool called_in_place;
  public List<AST_UpVal> upvals = new List<AST_UpVal>();

  public AST_LambdaDecl(LambdaSymbol symbol, bool called_in_place, List<AST_UpVal> upvals, int last_line_num)
    : base(symbol, last_line_num)
  {
    this.called_in_place = called_in_place;
    this.upvals = upvals;
  }
}

public class AST_TypeCast : AST_Tree
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);

  public IType type;
  public bool force_type;
  public IType hint_exp_type;
  public int line_num;

  public AST_TypeCast(IType type, bool force_type, int line_num)
  {
    this.type = type;
    this.force_type = force_type;
    this.line_num = line_num;
  }
}

public class AST_TypeAs : AST_Tree
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);

  public IType type;
  public bool force_type;
  public int line_num;

  public AST_TypeAs(IType type, bool force_type, int line_num)
  {
    this.type = type;
    this.force_type = force_type;
    this.line_num = line_num;
  }
}

public class AST_TypeIs : AST_Tree
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);

  public IType type;
  public int line_num;

  public AST_TypeIs(IType type, int line_num)
  {
    this.type = type;
    this.line_num = line_num;
  }
}

public enum EnumCall
{
  VAR             = 1,
  VARW            = 2,
  //TODO: do we really need this special case when we declare a var and write to it?
  //      (currently it's used for declaration of refs)
  VARWDCL         = 10,
  FUNC            = 3,
  MFUNC           = 30,
  ARR_IDX         = 4,
  ARR_IDXW        = 40,
  MAP_IDX         = 9,
  MAP_IDXW        = 90,
  GET_ADDR        = 5,
  FUNC_PTR_VAR    = 6, //func pointer from existing named variable
  //TODO: do we really need to designate these 2 below?
  FUNC_PTR_MVAR   = 7, //func pointer from class member
  FUNC_PTR_RES    = 8  //func pointer from any operation return result
}

public class AST_Call  : AST_Tree
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);

  public EnumCall type = new EnumCall();
  public int line_num;
  public Symbol symbol;
  public bool pass_as_ref; //e.g. foo(ref a)
  public ITerminalNode node;
  public VariableSymbol ctx_var = null; //currently used to mark local 'self' variable index for invoked methods

  public int symb_idx
  {
    get { return symbol is IScopeIndexed ? ((IScopeIndexed)symbol).scope_idx : -1; }
  }

  public ModuleDeclared module
  {
    get
    {
      if(symbol == null)
        return null;
      var ns = symbol.scope.GetNamespace();
      return ns?.module;
    }
  }

  public uint cargs_bits;

  public AST_Call(
    EnumCall type,
    int line_num,
    Symbol symbol,
    uint cargs_bits = 0,
    ITerminalNode node = null,
    bool pass_as_ref = false
  )
  {
    this.type = type;
    this.line_num = line_num;
    this.symbol = symbol;
    this.cargs_bits = cargs_bits;
    this.pass_as_ref = pass_as_ref;
    this.node = node;
  }
}

public class AST_Return  : AST_Tree
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);

  public int num;
  public int line_num;

  public AST_Return(int line_num)
  {
    this.line_num = line_num;
  }
}

public class AST_Typeof : IAST
{
  public void Accept(AST_Visitor v) => v.DoVisit(this);

  public IType type;

  public AST_Typeof(IType type)
  {
    this.type = type;
  }
}

public class AST_Break : IAST
{
  public void Accept(AST_Visitor v) => v.DoVisit(this);

  public int line_num;

  public AST_Break(int line_num = 0)
  {
    this.line_num = line_num;
  }
}

public class AST_Continue : IAST
{
  public void Accept(AST_Visitor v) => v.DoVisit(this);

  public bool jump_marker;
  public int line_num;

  public AST_Continue(bool jump_marker = false, int line_num = 0)
  {
    this.jump_marker = jump_marker;
    this.line_num = line_num;
  }
}

public class AST_Yield : IAST
{
  public void Accept(AST_Visitor v) => v.DoVisit(this);

  public int line_num;

  public AST_Yield(int line_num)
  {
    this.line_num = line_num;
  }
}

public class AST_Literal : IAST
{
  public void Accept(AST_Visitor v) => v.DoVisit(this);

  public ConstType type = new ConstType();
  public double nval;
  public string sval = "";

  public AST_Literal(ConstType type)
  {
    this.type = type;
  }
}

public class AST_VarDecl : AST_Tree
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);

  public VariableSymbol symb;
  public IType type => symb?.type.Get();
  public int symb_idx;
  public int line_num;
  //NOTE: set for a func param declaring a default value, whose expression is this node's
  //      only child - don't infer it back from the children, a transform adding one for
  //      any other reason would silently change the DefArg bytecode (see GetDefaultArgsNum)
  public bool has_default;

  public AST_VarDecl(VariableSymbol symb, int line_num)
    : this(symb, symb.scope_idx, line_num)
  {}

  //class static field version
  public AST_VarDecl(FieldSymbol symb, int global_idx, int line_num)
    : this((VariableSymbol)symb, global_idx, line_num)
  {}

  private AST_VarDecl(VariableSymbol symb, int symb_idx, int line_num)
  {
    this.symb = symb;
    this.symb_idx = symb_idx;
    this.line_num = line_num;
  }
}

public class AST_Block : AST_Tree
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);

  public BlockType type = new BlockType();
  //NOTE: only set for blocks the user introduces with a keyword (defer, paral, paral_all),
  //      so the debugger can stop on that line; structural blocks emit no opcode of their own
  public int line_num;

  public AST_Block(BlockType type, int line_num = 0)
  {
    this.type = type;
    this.line_num = line_num;
  }
}

public class AST_JsonObj : AST_Tree
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);

  public IType type;
  public int line_num;

  public AST_JsonObj(IType type, int line_num)
  {
    this.type = type;
    this.line_num = line_num;
  }
}

public class AST_JsonArr : AST_Tree
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);

  public IType type;
  public int line_num;

  public AST_JsonArr(IType type, int line_num)
  {
    this.type = type;
    this.line_num = line_num;
  }
}

public class AST_JsonArrAddItem : IAST
{
  public void Accept(AST_Visitor v) => v.DoVisit(this);
}

public class AST_JsonMap : AST_Tree
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);

  public IType type;
  public int line_num;

  public AST_JsonMap(IType type, int line_num)
  {
    this.type = type;
    this.line_num = line_num;
  }
}

public class AST_JsonMapAddItem : IAST
{
  public void Accept(AST_Visitor v) => v.DoVisit(this);
}

public class AST_JsonPair : AST_Tree
{
  public override void Accept(AST_Visitor v) => v.DoVisit(this);

  public IType scope_type;
  public string name;
  public int symb_idx;
  public int line_num;

  public AST_JsonPair(IType scope_type, string name, int symb_idx, int line_num)
  {
    this.scope_type = scope_type;
    this.name = name;
    this.symb_idx = symb_idx;
    this.line_num = line_num;
  }
}

public class AST_PopValue : IAST
{
  public void Accept(AST_Visitor v) => v.DoVisit(this);
}

public class AST_Dumper : AST_Visitor
{
  TextWriter writer;

  public override void DoVisit(AST_Interim node)
  {
    writer.Write("(");
    VisitChildren(node);
    writer.Write(")");
  }

  public override void DoVisit(AST_Module node)
  {
    writer.Write("(MODULE " + node.name);
    VisitChildren(node);
    writer.Write(")");
  }

  public override void DoVisit(AST_Import node)
  {
    writer.Write("(IMPORT ");
    foreach(var m in node.module_names)
      writer.Write("" + m);
    writer.Write(")");
  }

  public override void DoVisit(AST_FuncDecl node)
  {
    writer.Write("(FUNC ");
    writer.Write(node.symbol.name);
    VisitChildren(node);
    writer.Write(")");
  }

  public override void DoVisit(AST_LambdaDecl node)
  {
    writer.Write("(LMBD ");
    writer.Write(node.symbol.name);
    if(node.upvals.Count > 0)
      writer.Write(" USE:");
    for(int i = 0; i < node.upvals.Count; ++i)
      writer.Write(" " + node.upvals[i].name + "(" + node.upvals[i].mode + ")");
    VisitChildren(node);
    writer.Write(")");
  }

  public override void DoVisit(AST_ClassDecl node)
  {
    writer.Write("(CLASS ");
    writer.Write(node.symbol.name);
    VisitChildren(node);
    writer.Write(")");
  }

  public override void DoVisit(AST_Block node)
  {
    writer.Write("(BLK ");
    writer.Write(node.type + " {");
    VisitChildren(node);
    writer.Write("})");
  }

  public override void DoVisit(AST_TypeCast node)
  {
    writer.Write("(CAST ");
    writer.Write(node.type.GetName() + " ");
    VisitChildren(node);
    writer.Write(")");
  }

  public override void DoVisit(AST_TypeAs node)
  {
    writer.Write("(AS ");
    writer.Write(node.type.GetName() + " ");
    VisitChildren(node);
    writer.Write(")");
  }

  public override void DoVisit(AST_TypeIs node)
  {
    writer.Write("(IS ");
    writer.Write(node.type.GetName() + " ");
    VisitChildren(node);
    writer.Write(")");
  }

  public override void DoVisit(AST_Call node)
  {
    writer.Write("(CALL ");
    writer.Write(node.type + " " + node.symbol?.name);
    VisitChildren(node);
    writer.Write(")");
  }

  public override void DoVisit(AST_Inc node)
  {
    writer.Write("(INC ");
    writer.Write(node.symbol.name + " " + node.symbol.scope_idx);
    writer.Write(")");
  }

  public override void DoVisit(AST_Dec node)
  {
    writer.Write("(DEC ");
    writer.Write(node.symbol.name + " " + node.symbol.scope_idx);
    writer.Write(")");
  }

  public override void DoVisit(AST_Return node)
  {
    writer.Write("(RET " + node.num);
    VisitChildren(node);
    writer.Write(")");
  }

  public override void DoVisit(AST_Typeof node)
  {
    writer.Write("(TYPEOF " + node.type.GetName() + ")");
  }

  public override void DoVisit(AST_Break node)
  {
    writer.Write("(BRK)");
  }

  public override void DoVisit(AST_Continue node)
  {
    writer.Write("(CONT)");
  }

  public override void DoVisit(AST_Yield node)
  {
    writer.Write("(YIELD)");
  }

  public override void DoVisit(AST_PopValue node)
  {
    writer.Write("(POP)");
  }

  public override void DoVisit(AST_Literal node)
  {
    if(node.type == ConstType.INT)
      writer.Write(" (INT " + node.nval + ")");
    else if(node.type == ConstType.FLT)
      writer.Write(" (FLT " + node.nval + ")");
    else if(node.type == ConstType.BOOL)
      writer.Write(" (BOOL " + node.nval + ")");
    else if(node.type == ConstType.STR)
      writer.Write(" (STR '" + node.sval + "')");
    else if(node.type == ConstType.NIL)
      writer.Write(" (NULL)");
  }

  public override void DoVisit(AST_BinaryOpExp node)
  {
    writer.Write("(BOP " + node.type);
    VisitChildren(node);
    writer.Write(")");
  }

  public override void DoVisit(AST_UnaryOpExp node)
  {
    writer.Write("(UOP " + node.type);
    VisitChildren(node);
    writer.Write(")");
  }

  public override void DoVisit(AST_New node)
  {
    writer.Write("(NEW " + node.type.GetName());
    VisitChildren(node);
    writer.Write(")");
  }

  public override void DoVisit(AST_VarDecl node)
  {
    writer.Write("(VARDECL " + node.symb.name);
    VisitChildren(node);
    writer.Write(")");
  }

  public override void DoVisit(AST_JsonObj node)
  {
    writer.Write("(JOBJ " + node.type.GetName());
    VisitChildren(node);
    writer.Write(")");
  }

  public override void DoVisit(AST_JsonArr node)
  {
    writer.Write("(JARR ");
    VisitChildren(node);
    writer.Write(")");
  }

  public override void DoVisit(AST_JsonMap node)
  {
    writer.Write("(JMAP ");
    VisitChildren(node);
    writer.Write(")");
  }

  public override void DoVisit(AST_JsonPair node)
  {
    writer.Write("(JPAIR " + node.name);
    VisitChildren(node);
    writer.Write(")");
  }

  public override void DoVisit(AST_JsonArrAddItem node)
  {
    writer.Write("(JARDD)");
  }

  public override void DoVisit(AST_JsonMapAddItem node)
  {
    writer.Write("(JMADD)");
  }

  public static void Dump(IAST ast, bool visit_children = true, TextWriter writer = null)
  {
    writer ??= Console.Out;

    var dumper = new AST_Dumper(writer, visit_children);
    dumper.Visit(ast);
    writer.WriteLine("\n=============");
  }

  public AST_Dumper(TextWriter writer, bool visit_children = true)
  {
    this.writer = writer;
    this.visit_children = visit_children;
  }
}

public static class AST_Extensions
{
  public static void Append(this AST_Tree dst, AST_Tree src)
  {
    for(int i = 0; i < src.children.Count; ++i)
    {
      dst.children.Add(src.children[i]);
    }
  }

  static public List<IAST> GetChildren(this IAST self)
  {
    var ast = self as AST_Tree;
    return ast == null ? null : ast.children;
  }

  static public IAST At(this IAST self, int idx)
  {
    var ast = self as AST_Tree;
    return ast == null ? null : ast.children[idx];
  }

  static public void AddChild(this IAST self, IAST c)
  {
    if(c == null)
      return;
    var ast = self as AST_Tree;
    ast.children.Add(c);
  }

  static public void AddChild(this AST_Tree self, IAST c)
  {
    if(c == null)
      return;
    self.children.Add(c);
  }

  static public void AddChildren(this AST_Tree self, IAST b)
  {
    if(b is AST_Tree c)
    {
      for(int i = 0; i < c.children.Count; ++i)
        self.AddChild(c.children[i]);
    }
  }

  static public AST_Tree NewInterimChild(this AST_Tree self)
  {
    var c = new AST_Interim();
    self.AddChild(c);
    return c;
  }

  static public AST_Tree fparams(this AST_FuncDecl n)
  {
    return n.GetChildren()[0] as AST_Tree;
  }

  static public AST_Tree block(this AST_FuncDecl n)
  {
    return n.GetChildren()[1] as AST_Tree;
  }

  //NOTE: a control block's children are positional, and the order is NOT the same per type -
  //      'while' holds [cond, body] but 'do while' holds [body, cond], so children[0] means
  //      opposite things. Go through these instead of indexing children by hand
  static void AssertTwoChildren(AST_Block n)
  {
    if(n.children.Count != 2)
      throw new Exception("Unexpected amount of children (must be 2): " + n.type);
  }

  static public IAST cond(this AST_Block n)
  {
    AssertTwoChildren(n);
    return n.children[n.type == BlockType.DOWHILE ? 1 : 0];
  }

  static public IAST body(this AST_Block n)
  {
    AssertTwoChildren(n);
    return n.children[n.type == BlockType.DOWHILE ? 0 : 1];
  }

  //NOTE: an 'if' holds [cond, body] per branch, with the 'else' body as a trailing odd one
  static public int BranchCount(this AST_Block n)
  {
    return n.children.Count / 2;
  }

  static public IAST BranchCond(this AST_Block n, int i)
  {
    return n.children[i * 2];
  }

  static public IAST BranchBody(this AST_Block n, int i)
  {
    return n.children[i * 2 + 1];
  }

  static public IAST ElseBody(this AST_Block n)
  {
    return (n.children.Count % 2) == 1 ? n.children[n.children.Count - 1] : null;
  }

  static public int GetDefaultArgsNum(this AST_FuncDecl n)
  {
    var fparams = n.fparams();

    int num = 0;
    for(int i = 0; i < fparams.children.Count; ++i)
    {
      if(fparams.children[i] is AST_VarDecl vd && vd.has_default)
        ++num;
    }

    return num;
  }

  static public int GetTotalArgsNum(this AST_FuncDecl n)
  {
    var fparams = n.fparams();
    if(fparams.children.Count == 0)
      return 0;
    int num = fparams.children.Count;
    return num;
  }
}
}

#endif
