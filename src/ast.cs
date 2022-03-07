using System;
using System.Collections.Generic;

namespace bhl {

public abstract class AST_Visitor
{
  public abstract void DoVisit(AST_Interim node);
  public abstract void DoVisit(AST_Import node);
  public abstract void DoVisit(AST_Module node);
  public abstract void DoVisit(AST_VarDecl node);
  public abstract void DoVisit(AST_FuncDecl node);
  public abstract void DoVisit(AST_LambdaDecl node);
  public abstract void DoVisit(AST_ClassDecl node);
  public abstract void DoVisit(AST_EnumDecl node);
  public abstract void DoVisit(AST_Block node);
  public abstract void DoVisit(AST_TypeCast node);
  public abstract void DoVisit(AST_Call node);
  public abstract void DoVisit(AST_Return node);
  public abstract void DoVisit(AST_Break node);
  public abstract void DoVisit(AST_Continue node);
  public abstract void DoVisit(AST_PopValue node);
  public abstract void DoVisit(AST_Literal node);
  public abstract void DoVisit(AST_BinaryOpExp node);
  public abstract void DoVisit(AST_UnaryOpExp node);
  public abstract void DoVisit(AST_New node);
  public abstract void DoVisit(AST_Inc node);
  public abstract void DoVisit(AST_Dec node);
  public abstract void DoVisit(AST_JsonObj node);
  public abstract void DoVisit(AST_JsonArr node);
  public abstract void DoVisit(AST_JsonArrAddItem node);
  public abstract void DoVisit(AST_JsonPair node);

  public void Visit(IAST node)
  {
    if(node == null)
      throw new Exception("NULL node");

    if(node is AST_Interim)
      DoVisit(node as AST_Interim);
    else if(node is AST_Block)
      DoVisit(node as AST_Block);
    else if(node is AST_Literal)
      DoVisit(node as AST_Literal);
    else if(node is AST_Call)
      DoVisit(node as AST_Call);
    else if(node is AST_VarDecl)
      DoVisit(node as AST_VarDecl);
    else if(node is AST_LambdaDecl)
      DoVisit(node as AST_LambdaDecl);
    //NOTE: base class must be handled after AST_LambdaDecl
    else if(node is AST_FuncDecl)
      DoVisit(node as AST_FuncDecl);
    else if(node is AST_ClassDecl)
      DoVisit(node as AST_ClassDecl);
    else if(node is AST_EnumDecl)
      DoVisit(node as AST_EnumDecl);
    else if(node is AST_TypeCast)
      DoVisit(node as AST_TypeCast);
    else if(node is AST_Return)
      DoVisit(node as AST_Return);
    else if(node is AST_Break)
      DoVisit(node as AST_Break);
    else if(node is AST_Continue)
      DoVisit(node as AST_Continue);
    else if(node is AST_PopValue)
      DoVisit(node as AST_PopValue);
    else if(node is AST_BinaryOpExp)
      DoVisit(node as AST_BinaryOpExp);
    else if(node is AST_UnaryOpExp)
      DoVisit(node as AST_UnaryOpExp);
    else if(node is AST_New)
      DoVisit(node as AST_New);
    else if(node is AST_Inc)
      DoVisit(node as AST_Inc);
    else if(node is AST_Dec)
      DoVisit(node as AST_Dec);
    else if(node is AST_JsonObj)
      DoVisit(node as AST_JsonObj);
    else if(node is AST_JsonArr)
      DoVisit(node as AST_JsonArr);
    else if(node is AST_JsonArrAddItem)
      DoVisit(node as AST_JsonArrAddItem);
    else if(node is AST_JsonPair)
      DoVisit(node as AST_JsonPair);
    else if(node is AST_Import)
      DoVisit(node as AST_Import);
    else if(node is AST_Module)
      DoVisit(node as AST_Module);
    else 
      throw new Exception("Not known type: " + node.GetType().Name);
  }

  public void VisitChildren(AST node)
  {
    if(node == null)
      return;
    var children = node.children;
    for(int i=0;i<children.Count;++i)
      Visit(children[i]);
  }
}

public interface IAST
{}

public class AST : IAST
{
  public List<IAST> children = new List<IAST>();
}

public class AST_Interim : AST 
{
}

public class AST_Import : IAST
{
  public List<string> module_names = new List<string>();
}

public class AST_Module : AST 
{
  public uint id;
  public string name = "";
}

public enum EnumUnaryOp 
{
  NEG = 1,
  NOT = 2,
}

public class AST_UnaryOpExp : AST 
{
  public EnumUnaryOp type = new EnumUnaryOp();
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
  NQ = 13,
  BIT_OR = 14,
  BIT_AND = 15,
}

public class AST_BinaryOpExp  : AST 
{
  public EnumBinaryOp type = new EnumBinaryOp();
}

public class AST_Inc : IAST
{
  public uint symb_idx;
}

public class AST_Dec : IAST
{
  public uint symb_idx;
}

public class AST_New : AST 
{
  public string type = "";
}

public class AST_FuncDecl : AST 
{
  public string type = "";
  public string name = "";
}

public class AST_ClassDecl : AST 
{
  public string name = "";
  public string parent = "";
}

public class AST_EnumItem : IAST
{
  public string name;
  public int value;
}

public class AST_EnumDecl : AST 
{
  public string name = "";
}

public class AST_UpVal : IAST
{
  public string name = "";
  public uint symb_idx;
  public uint upsymb_idx;
}

public class AST_LambdaDecl : AST_FuncDecl 
{
  public int local_vars_num;
  public List<AST_UpVal> upvals = new List<AST_UpVal>();
}

public class AST_TypeCast : AST 
{
  public string type = "";
}

public enum EnumCall 
{
  VAR             = 1,
  VARW            = 2,
  MVAR            = 10,
  MVARW           = 11,
  MVARREF         = 12,
  FUNC            = 3,
  MFUNC           = 30,
  ARR_IDX         = 4,
  ARR_IDXW        = 40,
  GET_ADDR        = 5,
  FUNC_VAR        = 6,
  FUNC_MVAR        = 7,
  LMBD            = 8,
  GVAR            = 50,
  GVARW           = 51,
}

public class AST_Call  : AST 
{
  public EnumCall type = new EnumCall();
  public string name = "";
  public string module_name = "";
  public uint cargs_bits;
  public int line_num;
  public int symb_idx;
  public string scope_type = "";
}

public class AST_Return  : AST 
{
  public int num;
}

public class AST_Break : IAST
{
}

public class AST_Continue : IAST
{
  public bool jump_marker;
}

public enum EnumLiteral 
{
  NUM = 1,
  BOOL = 2,
  STR = 3,
  NIL = 4,
}

public class AST_Literal : IAST
{
  public EnumLiteral type = new EnumLiteral();
  public double nval;
  public string sval = "";
}

public class AST_VarDecl : AST 
{
  public string name = "";
  public string type = "";
  public uint symb_idx;
  public bool is_func_arg;
  public bool is_ref;
}

public enum EnumBlock 
{
  FUNC = 0,
  SEQ = 1,
  DEFER = 2,
  PARAL = 3,
  PARAL_ALL = 4,
  IF = 7,
  WHILE = 8,
  FOR = 9,
  DOWHILE = 10,
}

public class AST_Block : AST 
{
  public EnumBlock type = new EnumBlock();
}

public class AST_JsonObj : AST 
{
  public string type = "";
  public int line_num;
}

public class AST_JsonArr : AST 
{
  public string type;
  public int line_num;
}

public class AST_JsonArrAddItem : IAST
{
}

public class AST_JsonPair : AST 
{
  public string name = "";
  public uint symb_idx;
  public string scope_type = "";
}

public class AST_PopValue : IAST
{
}

static public class AST_Util
{
  static public List<IAST> GetChildren(this IAST self)
  {
    var ast = self as AST;
    return ast == null ? null : ast.children;
  }

  static public void AddChild(this IAST self, IAST c)
  {
    if(c == null)
      return;
    var ast = self as AST;
    ast.children.Add(c);
  }

  static public void AddChild(this AST self, IAST c)
  {
    if(c == null)
      return;
    self.children.Add(c);
  }

  static public void AddChildren(this AST self, IAST b)
  {
    if(b is AST c)
    {
      for(int i=0;i<c.children.Count;++i)
        self.AddChild(c.children[i]);
    }
  }

  static public AST NewInterimChild(this AST self)
  {
    var c = new AST_Interim();
    self.AddChild(c);
    return c;
  }

  ////////////////////////////////////////////////////////

  static public AST_Module New_Module(string name)
  {
    var n = new AST_Module();
    n.name = name;
    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_Import New_Imports()
  {
    var n = new AST_Import();
    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_FuncDecl New_FuncDecl(string name, string type)
  {
    var n = new AST_FuncDecl();
    Init_FuncDecl(n, name, type);
    return n;
  }

  static void Init_FuncDecl(AST_FuncDecl n, string name, string type)
  {
    n.type = type;
    n.name = name;
    //fparams
    n.NewInterimChild();
    //block
    n.NewInterimChild();
  }

  static public AST fparams(this AST_FuncDecl n)
  {
    return n.GetChildren()[0] as AST;
  }

  static public AST block(this AST_FuncDecl n)
  {
    return n.GetChildren()[1] as AST;
  }

  static public int GetDefaultArgsNum(this AST_FuncDecl n)
  {
    var fparams = n.fparams();
    if(fparams.children.Count == 0)
      return 0;

    int num = 0;
    for(int i=0;i<fparams.children.Count;++i)
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

  ////////////////////////////////////////////////////////

  static public AST_LambdaDecl New_LambdaDecl(string name, string type)
  {
    var n = new AST_LambdaDecl();
    Init_FuncDecl(n, name, type);

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_ClassDecl New_ClassDecl(string name, string parent)
  {
    var n = new AST_ClassDecl();
    n.name = name;
    n.parent = parent;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_EnumDecl New_EnumDecl(string name)
  {
    var n = new AST_EnumDecl();
    n.name = name;

    return n;
  }

  ////////////////////////////////////////////////////////
  
  static public AST_UnaryOpExp New_UnaryOpExp(EnumUnaryOp type)
  {
    var n = new AST_UnaryOpExp();
    n.type = type;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_BinaryOpExp New_BinaryOpExp(EnumBinaryOp type)
  {
    var n = new AST_BinaryOpExp();
    n.type = type;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_TypeCast New_TypeCast(string type)
  {
    var n = new AST_TypeCast();
    n.type = type;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_UpVal New_UpVal(string name, int symb_idx, int upsymb_idx)
  {
    var n = new AST_UpVal();
    n.name = name;
    n.symb_idx = (uint)symb_idx;
    n.upsymb_idx = (uint)upsymb_idx;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_Call New_Call(EnumCall type, int line_num, string name = "", string module_name = "", ClassSymbol scope_symb = null, int symb_idx = -1)
  {
    return New_Call(type, line_num, name, scope_symb != null ? scope_symb.name : "", symb_idx, module_name);
  }

  static public AST_Call New_Call(EnumCall type, int line_num, VariableSymbol symb, ClassSymbol scope_symb = null)
  {
    return New_Call(type, line_num, symb.name, scope_symb != null ? scope_symb.name : "", symb.scope_idx, symb.module_name);
  }

  static public AST_Call New_Call(EnumCall type, int line_num, string name, string scope_type, int symb_idx = -1, string module_name = "")
  {
    var n = new AST_Call();
    n.type = type;
    n.name = name;
    n.scope_type = scope_type;
    n.line_num = line_num;
    n.symb_idx = symb_idx;
    n.module_name = module_name;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_Return New_Return()
  {
    return new AST_Return();
  }

  static public AST_Break New_Break()
  {
    return new AST_Break();
  }
  
  static public AST_Continue New_Continue(bool jump_marker = false)
  {
    return new AST_Continue() {
      jump_marker = jump_marker
    };
  }
  
  ////////////////////////////////////////////////////////

  static public AST_PopValue New_PopValue()
  {
    return new AST_PopValue();
  }

  ////////////////////////////////////////////////////////

  static public AST_New New_New(ClassSymbol type)
  {
    var n = new AST_New();
    n.type = type.name;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_Literal New_Literal(EnumLiteral type)
  {
    var n = new AST_Literal();
    n.type = type;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_VarDecl New_VarDecl(VariableSymbol symb, bool is_ref)
  {
    return New_VarDecl(symb.name, is_ref, symb is FuncArgSymbol, symb.type.name, symb.scope_idx);
  }

  static public AST_VarDecl New_VarDecl(string name, bool is_ref, bool is_func_arg, string type, int symb_idx)
  {
    var n = new AST_VarDecl();
    n.name = name;
    n.type = type;
    n.is_ref = is_ref;
    n.is_func_arg = is_func_arg;
    n.symb_idx = (uint)symb_idx;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_Inc New_Inc(VariableSymbol symb)
  {
    var n = new AST_Inc();
    n.symb_idx = (uint)symb.scope_idx;

    return n;
  }
  
  ////////////////////////////////////////////////////////

  static public AST_Dec New_Dec(VariableSymbol symb)
  {
    var n = new AST_Dec();
    n.symb_idx = (uint)symb.scope_idx;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_Block New_Block(EnumBlock type)
  {
    var n = new AST_Block();
    n.type = type;
    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_JsonObj New_JsonObj(string root_type_name, int line_num)
  {
    var n = new AST_JsonObj();
    n.type = root_type_name;
    n.line_num = line_num;
    return n;
  }

  static public AST_JsonArr New_JsonArr(ArrayTypeSymbol arr_type, int line_num)
  {
    var n = new AST_JsonArr();
    n.type = arr_type.name;
    n.line_num = line_num;
    return n;
  }

  static public AST_JsonArrAddItem New_JsonArrAddItem()
  {
    var n = new AST_JsonArrAddItem();
    return n;
  }

  static public AST_JsonPair New_JsonPair(string scope_type, string name, int symb_idx)
  {
    var n = new AST_JsonPair();
    n.scope_type = scope_type;
    n.name = name;
    n.symb_idx = (uint)symb_idx;

    return n;
  }
}

} // namespace bhl
