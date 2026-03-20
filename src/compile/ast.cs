using System;
using System.IO;
using System.Collections.Generic;
using Antlr4.Runtime.Tree;

namespace bhl;

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
      if(ast is AST_Interim _1)
        DoVisit(_1);
      else if(ast is AST_Block _2)
        DoVisit(_2);
      else if(ast is AST_Literal _3)
        DoVisit(_3);
      else if(ast is AST_Call _4)
        DoVisit(_4);
      else if(ast is AST_VarDecl _5)
        DoVisit(_5);
      else if(ast is AST_LambdaDecl _6)
        DoVisit(_6);
      //NOTE: base class must be handled after AST_LambdaDecl
      else if(ast is AST_FuncDecl _7)
        DoVisit(_7);
      else if(ast is AST_ClassDecl _8)
        DoVisit(_8);
      else if(ast is AST_TypeCast _10)
        DoVisit(_10);
      else if(ast is AST_Return _11)
        DoVisit(_11);
      else if(ast is AST_Break _12)
        DoVisit(_12);
      else if(ast is AST_Continue _13)
        DoVisit(_13);
      else if(ast is AST_PopValue _14)
        DoVisit(_14);
      else if(ast is AST_BinaryOpExp _15)
        DoVisit(_15);
      else if(ast is AST_UnaryOpExp _16)
        DoVisit(_16);
      else if(ast is AST_New _17)
        DoVisit(_17);
      else if(ast is AST_Inc _18)
        DoVisit(_18);
      else if(ast is AST_Dec _19)
        DoVisit(_19);
      else if(ast is AST_JsonObj _20)
        DoVisit(_20);
      else if(ast is AST_JsonArr _21)
        DoVisit(_21);
      else if(ast is AST_JsonArrAddItem _22)
        DoVisit(_22);
      else if(ast is AST_JsonPair _23)
        DoVisit(_23);
      else if(ast is AST_Import _24)
        DoVisit(_24);
      else if(ast is AST_Module _25)
        DoVisit(_25);
      else if(ast is AST_TypeAs _26)
        DoVisit(_26);
      else if(ast is AST_TypeIs _27)
        DoVisit(_27);
      else if(ast is AST_Typeof _28)
        DoVisit(_28);
      else if(ast is AST_JsonMap _29)
        DoVisit(_29);
      else if(ast is AST_JsonMapAddItem _30)
        DoVisit(_30);
      else if(ast is AST_Yield _31)
        DoVisit(_31);
      else
        throw new Exception("Not known type: " + ast.GetType().Name);
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
}

public abstract class AST_Tree : IAST
{
  public List<IAST> children = new List<IAST>();
}

public class AST_Interim : AST_Tree
{
}

public class AST_Import : IAST
{
  public List<string> module_names = new List<string>();
}

public class AST_Module : AST_Tree
{
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
  public VariableSymbol symbol;

  public AST_Inc(VariableSymbol symbol)
  {
    this.symbol = symbol;
  }
}

public class AST_Dec : IAST
{
  public VariableSymbol symbol;

  public AST_Dec(VariableSymbol symbol)
  {
    this.symbol = symbol;
  }
}

public class AST_New : AST_Tree
{
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

public class AST_UpVal : IAST
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

  public Module module
  {
    get
    {
      if(symbol == null)
        return null;
      var ns = symbol.scope.GetNamespace();
      return ns == null ? null : ns.module;
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
  public int num;
  public int line_num;

  public AST_Return(int line_num)
  {
    this.line_num = line_num;
  }
}

public class AST_Typeof : IAST
{
  public IType type;

  public AST_Typeof(IType type)
  {
    this.type = type;
  }
}

public class AST_Break : IAST
{
}

public class AST_Continue : IAST
{
  public bool jump_marker;

  public AST_Continue(bool jump_marker = false)
  {
    this.jump_marker = jump_marker;
  }
}

public class AST_Yield : IAST
{
  public int line_num;

  public AST_Yield(int line_num)
  {
    this.line_num = line_num;
  }
}

public class AST_Literal : IAST
{
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
  public VariableSymbol symb;
  public IType type => symb?.type.Get();
  public int symb_idx;
  public int line_num;

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
  public BlockType type = new BlockType();

  public AST_Block(BlockType type)
  {
    this.type = type;
  }
}

public class AST_JsonObj : AST_Tree
{
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
}

public class AST_JsonMap : AST_Tree
{
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
}

public class AST_JsonPair : AST_Tree
{
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

  static public int GetDefaultArgsNum(this AST_FuncDecl n)
  {
    var fparams = n.fparams();
    if(fparams.children.Count == 0)
      return 0;

    int num = 0;
    for(int i = 0; i < fparams.children.Count; ++i)
    {
      var fc = fparams.children[i].GetChildren();
      if(fc != null && fc.Count > 0)
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
