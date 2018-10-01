using System;
using System.Collections.Generic;
using System.IO;

namespace TODO {

public class AST_Base {}

public class AST : AST_Base 
{
  public List<AST_Base> children = new List<AST_Base>();
}

public class AST_Interim : AST {}

public class AST_Import : AST_Base 
{
  public List<uint> modules = new List<uint>();
}

public class AST_Module : AST 
{
  public uint nname;
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

public class AST_BinaryOpExp : AST 
{
  public EnumBinaryOp type = new EnumBinaryOp();
}

public class AST_Inc : AST_Base 
{
  public uint nname;
}

public class AST_New : AST 
{
  public uint ntype;
  public string type = "";
}

public class AST_FuncDecl : AST 
{
  public uint ntype;
  public string type = "";
  public uint nname1;
  public uint nname2;
  public string name = "";
}

public class AST_ClassDecl : AST 
{
  public uint nname;
  public string name = "";
  public uint nparent;
  public string parent = "";

}

public class AST_EnumItem : AST_Base 
{
  public uint nname;
  public int value;
}

public class AST_EnumDecl : AST 
{
  public uint nname;
  public string name = "";
}

public class AST_UseParam
{
  public uint nname;
  public string name = "";
}

public class AST_LambdaDecl : AST_FuncDecl 
{
  public List<AST_UseParam> useparams = new List<AST_UseParam>();
}

public class AST_TypeCast : AST 
{
  public uint ntype;
  public string type = "";
}

public enum EnumCall 
{
  VAR = 1,
  VARW = 2,
  MVAR = 10,
  MVARW = 11,
  MVARREF = 12,
  FUNC = 3,
  MFUNC = 30,
  ARR_IDX = 4,
  ARR_IDXW = 40,
  FUNC2VAR = 5,
  FUNC_PTR = 6,
  FUNC_PTR_POP = 7,
}

public class AST_Call : AST 
{
  public EnumCall type = new EnumCall();
  public uint nname1;
  public uint nname2;
  public string name = "";
  public uint cargs_bits;
  public uint scope_ntype;
  public uint line_num;
}

public class AST_Return : AST {}

public class AST_Break : AST_Base {} 

public enum EnumLiteral 
{
  NUM = 1,
  BOOL = 2,
  STR = 3,
  NIL = 4,
}

public class AST_Literal : AST_Base 
{
  public EnumLiteral type = new EnumLiteral();
  public double nval;
  public string sval = "";
}

public class AST_VarDecl : AST 
{
  public uint nname;
  public string name = "";
  public uint ntype;
}

public enum EnumBlock 
{
  FUNC = 0,
  SEQ = 1,
  DEFER = 2,
  PARAL = 3,
  PARAL_ALL = 4,
  PRIO = 5,
  FOREVER = 6,
  IF = 7,
  WHILE = 8,
  FOR = 9,
  UNTIL_FAILURE = 10,
  UNTIL_FAILURE_ = 11,
  UNTIL_SUCCESS = 12,
  NOT = 13,
  SEQ_ = 14,
  EVAL = 15,
  GROUP = 16,
}

public class AST_Block : AST 
{
  public EnumBlock type = new EnumBlock();
}

public class AST_JsonObj : AST 
{
  public uint ntype;
}

public class AST_JsonArr : AST 
{
  public uint ntype;
}

public class AST_JsonArrAddItem : AST_Base {}

public class AST_JsonPair : AST 
{
  public uint nname;
  public string name = "";
  public uint scope_ntype;
}

public class AST_PopValue : AST_Base {}

}

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
  public abstract void DoVisit(AST_EnumItem node);
  public abstract void DoVisit(AST_Block node);
  public abstract void DoVisit(AST_TypeCast node);
  public abstract void DoVisit(AST_Call node);
  public abstract void DoVisit(AST_Return node);
  public abstract void DoVisit(AST_Break node);
  public abstract void DoVisit(AST_PopValue node);
  public abstract void DoVisit(AST_Literal node);
  public abstract void DoVisit(AST_BinaryOpExp node);
  public abstract void DoVisit(AST_UnaryOpExp node);
  public abstract void DoVisit(AST_New node);
  public abstract void DoVisit(AST_Inc node);
  public abstract void DoVisit(AST_JsonObj node);
  public abstract void DoVisit(AST_JsonArr node);
  public abstract void DoVisit(AST_JsonArrAddItem node);
  public abstract void DoVisit(AST_JsonPair node);

  public void Visit(AST_Base node)
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
    else if(node is AST_EnumItem)
      DoVisit(node as AST_EnumItem);
    else if(node is AST_TypeCast)
      DoVisit(node as AST_TypeCast);
    else if(node is AST_Return)
      DoVisit(node as AST_Return);
    else if(node is AST_Break)
      DoVisit(node as AST_Break);
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


public class AST2FB : AST_Visitor
{
  FlatBuffers.FlatBufferBuilder fbb;

  struct ChildSelector
  {
    internal fbhl.AST_OneOf type;
    internal int offset;

    public ChildSelector(fbhl.AST_OneOf type, int offset)
    {
      this.type = type;
      this.offset = offset;
    }
  }
  Stack<List<ChildSelector>> child_sel_stack = new Stack<List<ChildSelector>>();

  public AST2FB()
  {
    this.fbb = new FlatBuffers.FlatBufferBuilder(1);
  }

  public override void DoVisit(AST_Module node)
  {
    DeclChildren();

    VisitChildren(node);
    
    fbhl.AST_Module.CreateAST_Module(
      fbb, 
      node.nname,
      fbb.CreateString(node.name),
      fbhl.AST_Module.CreateChildrenVector(fbb, EndChildren())
    );

    //Console.WriteLine("FBB SIZE " + fbb.Offset);
  }

  public override void DoVisit(AST_Interim node)
  {
    DeclChildren();

    VisitChildren(node);

    NewChild(fbhl.AST_Interim.CreateAST_Interim(
      fbb, 
      fbhl.AST_Interim.CreateChildrenVector(fbb, EndChildren())
    ));
  }

  public override void DoVisit(AST_Import node)
  {
    NewChild(fbhl.AST_Import.CreateAST_Import(
      fbb, 
      fbhl.AST_Import.CreateModulesVector(fbb, node.modules.ToArray())
    ));
  }

  public override void DoVisit(AST_FuncDecl node)
  {
    DeclChildren();

    VisitChildren(node);

    var children = fbhl.AST_FuncDecl.CreateChildrenVector(fbb, EndChildren());

    NewChild(MakeFuncDecl(node, children));
  }

  FlatBuffers.Offset<fbhl.AST_FuncDecl> MakeFuncDecl(AST_FuncDecl node, FlatBuffers.VectorOffset children)
  {
    var stype = new FlatBuffers.StringOffset();
    if(!string.IsNullOrEmpty(node.type))
      stype = fbb.CreateString(node.type);

    var sname = new FlatBuffers.StringOffset();
    if(!string.IsNullOrEmpty(node.name))
      sname = fbb.CreateString(node.name);

    fbhl.AST_FuncDecl.StartAST_FuncDecl(fbb);
    fbhl.AST_FuncDecl.AddNtype(fbb, node.ntype);
    if(!string.IsNullOrEmpty(node.type))
      fbhl.AST_FuncDecl.AddType(fbb, stype);
    fbhl.AST_FuncDecl.AddNname1(fbb, node.nname1);
    fbhl.AST_FuncDecl.AddNname2(fbb, node.nname2);
    fbhl.AST_FuncDecl.AddChildren(fbb, children);
    if(!string.IsNullOrEmpty(node.name))
      fbhl.AST_FuncDecl.AddName(fbb, sname);
    return fbhl.AST_FuncDecl.EndAST_FuncDecl(fbb);
  }

  public override void DoVisit(AST_LambdaDecl node)
  {
    DeclChildren();

    VisitChildren(node);

    var children = fbhl.AST_FuncDecl.CreateChildrenVector(fbb, EndChildren());  
    
    var base_decl = MakeFuncDecl(node, children);

    fbhl.AST_LambdaDecl.StartAST_LambdaDecl(fbb);
    fbhl.AST_LambdaDecl.AddBase(fbb, base_decl);
    NewChild(fbhl.AST_LambdaDecl.EndAST_LambdaDecl(fbb));
  }

  public override void DoVisit(AST_ClassDecl node)
  {
    DeclChildren();

    VisitChildren(node);

    var children = fbhl.AST_ClassDecl.CreateChildrenVector(fbb, EndChildren());  

    var sname = new FlatBuffers.StringOffset();
    if(!string.IsNullOrEmpty(node.name))
      sname = fbb.CreateString(node.name);

    var sparent = new FlatBuffers.StringOffset();
    if(!string.IsNullOrEmpty(node.parent))
      sparent = fbb.CreateString(node.parent);

    fbhl.AST_ClassDecl.StartAST_ClassDecl(fbb);
    fbhl.AST_ClassDecl.AddNname(fbb, node.nname);
    if(!string.IsNullOrEmpty(node.name))
      fbhl.AST_ClassDecl.AddName(fbb, sname);
    fbhl.AST_ClassDecl.AddNparent(fbb, node.nparent);
    if(!string.IsNullOrEmpty(node.parent))
      fbhl.AST_ClassDecl.AddParent(fbb, sparent);
    fbhl.AST_ClassDecl.AddChildren(fbb, children);
    NewChild(fbhl.AST_ClassDecl.EndAST_ClassDecl(fbb));
  }

  public override void DoVisit(AST_EnumDecl node)
  {
    VisitChildren(node);
  }

  public override void DoVisit(AST_EnumItem node)
  {
  }

  public override void DoVisit(AST_Block node)
  {
    DeclChildren();

    VisitChildren(node);

    NewChild(fbhl.AST_Block.CreateAST_Block(
      fbb, 
      (fbhl.EnumBlock)node.type,
      fbhl.AST_Return.CreateChildrenVector(fbb, EndChildren())
    ));
  }

  public override void DoVisit(AST_TypeCast node)
  {
    DeclChildren();

    VisitChildren(node);

    var children = fbhl.AST_TypeCast.CreateChildrenVector(fbb, EndChildren());

    var stype = new FlatBuffers.StringOffset();
    if(!string.IsNullOrEmpty(node.type))
      stype = fbb.CreateString(node.type);

    fbhl.AST_TypeCast.StartAST_TypeCast(fbb);
    fbhl.AST_TypeCast.AddNtype(fbb, node.ntype);
    if(!string.IsNullOrEmpty(node.type))
      fbhl.AST_TypeCast.AddType(fbb, stype);
    fbhl.AST_TypeCast.AddChildren(fbb, children);
    NewChild(fbhl.AST_TypeCast.EndAST_TypeCast(fbb));
  }

  public override void DoVisit(AST_Call node)
  {
    DeclChildren();

    VisitChildren(node);

    var children = fbhl.AST_Call.CreateChildrenVector(fbb, EndChildren());

    var sname = new FlatBuffers.StringOffset();
    if(!string.IsNullOrEmpty(node.name))
      sname = fbb.CreateString(node.name);

    fbhl.AST_Call.StartAST_Call(fbb);
    fbhl.AST_Call.AddType(fbb, (fbhl.EnumCall)node.type);
    fbhl.AST_Call.AddNname1(fbb, node.nname1);
    fbhl.AST_Call.AddNname2(fbb, node.nname2);
    if(!string.IsNullOrEmpty(node.name))
      fbhl.AST_Call.AddName(fbb, sname);
    fbhl.AST_Call.AddCargsBits(fbb, node.cargs_bits);
    if(node.scope_ntype != 0)
      fbhl.AST_Call.AddScopeNtype(fbb, node.scope_ntype);
    fbhl.AST_Call.AddLineNum(fbb, node.line_num);
    fbhl.AST_Call.AddChildren(fbb, children);
    NewChild(fbhl.AST_Call.EndAST_Call(fbb));
  }

  public override void DoVisit(AST_Inc node)
  {
    fbhl.AST_Inc.StartAST_Inc(fbb);
    NewChild(fbhl.AST_Inc.EndAST_Inc(fbb));
  }

  public override void DoVisit(AST_Return node)
  {
    DeclChildren();

    VisitChildren(node);

    NewChild(fbhl.AST_Return.CreateAST_Return(
      fbb, 
      fbhl.AST_Return.CreateChildrenVector(fbb, EndChildren())
    ));
  }

  public override void DoVisit(AST_Break node)
  {
    fbhl.AST_Break.StartAST_Break(fbb);
    NewChild(fbhl.AST_Break.EndAST_Break(fbb));
  }

  public override void DoVisit(AST_PopValue node)
  {
    fbhl.AST_PopValue.StartAST_PopValue(fbb);
    NewChild(fbhl.AST_PopValue.EndAST_PopValue(fbb));
  }

  public override void DoVisit(AST_Literal node)
  {
    var soff = new FlatBuffers.StringOffset();
    if(node.type == EnumLiteral.STR)
      soff = fbb.CreateString(node.sval);

    fbhl.AST_Literal.StartAST_Literal(fbb);
    fbhl.AST_Literal.AddType(fbb, (fbhl.EnumLiteral)node.type);
    if(node.type == EnumLiteral.STR)
      fbhl.AST_Literal.AddNval(fbb, node.nval);
    else
      fbhl.AST_Literal.AddSval(fbb, soff);
    NewChild(fbhl.AST_Literal.EndAST_Literal(fbb));
  }

  public override void DoVisit(AST_BinaryOpExp node)
  {
    DeclChildren();

    VisitChildren(node);

    NewChild(fbhl.AST_BinaryOpExp.CreateAST_BinaryOpExp(
      fbb, 
      (fbhl.EnumBinaryOp)node.type,
      fbhl.AST_BinaryOpExp.CreateChildrenVector(fbb, EndChildren())
    ));
  }

  public override void DoVisit(AST_UnaryOpExp node)
  {
    DeclChildren();

    VisitChildren(node);

    NewChild(fbhl.AST_UnaryOpExp.CreateAST_UnaryOpExp(
      fbb, 
      (fbhl.EnumUnaryOp)node.type,
      fbhl.AST_UnaryOpExp.CreateChildrenVector(fbb, EndChildren())
    ));
  }

  public override void DoVisit(AST_New node)
  {
    VisitChildren(node);
  }

  public override void DoVisit(AST_VarDecl node)
  {
    VisitChildren(node);
  }

  public override void DoVisit(AST_JsonObj node)
  {
    DeclChildren();

    VisitChildren(node);

    NewChild(fbhl.AST_JsonObj.CreateAST_JsonObj(
      fbb, 
      node.ntype,
      fbhl.AST_JsonObj.CreateChildrenVector(fbb, EndChildren())
    ));
  }

  public override void DoVisit(AST_JsonArr node)
  {
    DeclChildren();

    VisitChildren(node);

    NewChild(fbhl.AST_JsonArr.CreateAST_JsonArr(
      fbb, 
      node.ntype,
      fbhl.AST_JsonArr.CreateChildrenVector(fbb, EndChildren())
    ));
  }

  public override void DoVisit(AST_JsonPair node)
  {
    VisitChildren(node);
  }

  public override void DoVisit(AST_JsonArrAddItem node)
  {
    fbhl.AST_JsonArrAddItem.StartAST_JsonArrAddItem(fbb);
    NewChild(fbhl.AST_JsonArrAddItem.EndAST_JsonArrAddItem(fbb));
  }

  ///////////////////////////////////////////////////////////

  void DeclChildren()
  {
    child_sel_stack.Push(new List<ChildSelector>());
  }

  FlatBuffers.Offset<fbhl.AST_Selector>[] EndChildren()
  {
    var selectors = child_sel_stack.Pop();

    var res = new FlatBuffers.Offset<fbhl.AST_Selector>[selectors.Count];

    for(int s=0;s<selectors.Count;++s)
    {
      var sel = selectors[s];
      fbhl.AST_Selector.StartAST_Selector(fbb);
      fbhl.AST_Selector.AddVType(fbb, sel.type);
      fbhl.AST_Selector.AddV(fbb, sel.offset);

      res[s] = fbhl.AST_Selector.EndAST_Selector(fbb);
    }

    return res;
  }

  void NewChild<T>(FlatBuffers.Offset<T> offset) where T : struct
  {
    child_sel_stack.Peek().Add(MatchToSelector(offset));
  }

  ChildSelector MatchToSelector<T>(FlatBuffers.Offset<T> offset) where T : struct
  {
    var t = typeof(T);

    if(t == typeof(fbhl.AST_Literal))
      return new ChildSelector(fbhl.AST_OneOf.AST_Literal, offset.Value);
    else if(t == typeof(fbhl.AST_Break))
      return new ChildSelector(fbhl.AST_OneOf.AST_Break, offset.Value);
    else if(t == typeof(fbhl.AST_UnaryOpExp))
      return new ChildSelector(fbhl.AST_OneOf.AST_UnaryOpExp, offset.Value);
    else if(t == typeof(fbhl.AST_BinaryOpExp))
      return new ChildSelector(fbhl.AST_OneOf.AST_BinaryOpExp, offset.Value);
    else if(t == typeof(fbhl.AST_PopValue))
      return new ChildSelector(fbhl.AST_OneOf.AST_PopValue, offset.Value);
    else if(t == typeof(fbhl.AST_Interim))
      return new ChildSelector(fbhl.AST_OneOf.AST_Interim, offset.Value);
    else if(t == typeof(fbhl.AST_Call))
      return new ChildSelector(fbhl.AST_OneOf.AST_Call, offset.Value);
    else if(t == typeof(fbhl.AST_Block))
      return new ChildSelector(fbhl.AST_OneOf.AST_Return, offset.Value);
    else if(t == typeof(fbhl.AST_Return))
      return new ChildSelector(fbhl.AST_OneOf.AST_Return, offset.Value);
    else if(t == typeof(fbhl.AST_Import))
      return new ChildSelector(fbhl.AST_OneOf.AST_Import, offset.Value);
    else if(t == typeof(fbhl.AST_FuncDecl))
      return new ChildSelector(fbhl.AST_OneOf.AST_FuncDecl, offset.Value);
    else if(t == typeof(fbhl.AST_LambdaDecl))
      return new ChildSelector(fbhl.AST_OneOf.AST_LambdaDecl, offset.Value);
    else if(t == typeof(fbhl.AST_ClassDecl))
      return new ChildSelector(fbhl.AST_OneOf.AST_ClassDecl, offset.Value);
    else if(t == typeof(fbhl.AST_Inc))
      return new ChildSelector(fbhl.AST_OneOf.AST_Inc, offset.Value);
    else if(t == typeof(fbhl.AST_TypeCast))
      return new ChildSelector(fbhl.AST_OneOf.AST_TypeCast, offset.Value);
    else if(t == typeof(fbhl.AST_JsonObj))
      return new ChildSelector(fbhl.AST_OneOf.AST_JsonObj, offset.Value);
    else if(t == typeof(fbhl.AST_JsonArr))
      return new ChildSelector(fbhl.AST_OneOf.AST_JsonArr, offset.Value);
    else if(t == typeof(fbhl.AST_JsonArrAddItem))
      return new ChildSelector(fbhl.AST_OneOf.AST_JsonArrAddItem, offset.Value);
    else
      throw new Exception("Unhandled offset type: " + typeof(T).Name);
  }
}

public class AST_Dumper : AST_Visitor
{
  public override void DoVisit(AST_Interim node)
  {
    Console.Write("(");
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_Module node)
  {
    Console.Write("(MODULE " + node.nname);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_Import node)
  {
    Console.Write("(IMPORT ");
    foreach(var m in node.modules)
      Console.Write("" + m);
    Console.Write(")");
  }

  public override void DoVisit(AST_FuncDecl node)
  {
    Console.Write("(FUNC ");
    Console.Write(node.type + " " + node.name + " " + node.nname());
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_LambdaDecl node)
  {
    Console.Write("(LMBD ");
    Console.Write(node.type + " " + node.nname() + " USE:");
    for(int i=0;i<node.useparams.Count;++i)
      Console.Write(" " + node.useparams[i].nname);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_ClassDecl node)
  {
    Console.Write("(CLASS ");
    Console.Write(node.name + " " + node.nname);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_EnumDecl node)
  {
    Console.Write("(ENUM ");
    Console.Write(node.name + " " + node.nname);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_EnumItem node)
  {
    Console.Write("(EITEM ");
    Console.Write(")");
  }

  public override void DoVisit(AST_Block node)
  {
    Console.Write("(BLOCK ");
    Console.Write(node.type + " ");
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_TypeCast node)
  {
    Console.Write("(CAST ");
    Console.Write(node.type + " ");
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_Call node)
  {
    Console.Write("(CALL ");
    Console.Write(node.type + " " + node.name + " " + node.nname());
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_Inc node)
  {
    Console.Write("(INC ");
    Console.Write(node.nname);
    Console.Write(")");
  }

  public override void DoVisit(AST_Return node)
  {
    Console.Write("(RET ");
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_Break node)
  {
    Console.Write("(BRK ");
    Console.Write(")");
  }

  public override void DoVisit(AST_PopValue node)
  {
    Console.Write("(POP ");
    Console.Write(")");
  }

  public override void DoVisit(AST_Literal node)
  {
    if(node.type == EnumLiteral.NUM)
      Console.Write(" (" + node.nval + ")");
    else if(node.type == EnumLiteral.BOOL)
      Console.Write(" (" + node.nval + ")");
    else if(node.type == EnumLiteral.STR)
      Console.Write(" ('" + node.sval + "')");
  }

  public override void DoVisit(AST_BinaryOpExp node)
  {
    Console.Write("(BOP " + node.type);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_UnaryOpExp node)
  {
    Console.Write("(UOP " + node.type);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_New node)
  {
    Console.Write("(NEW " + node.type + " " + node.ntype);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_VarDecl node)
  {
    Console.Write("(VARDECL " + node.name + " " + node.nname);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_JsonObj node)
  {
    Console.Write("(JOBJ " + node.ntype);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_JsonArr node)
  {
    Console.Write("(JARR ");
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_JsonArrAddItem node)
  {
    Console.Write("(JARRADD ");
    Console.Write(")");
  }

  public override void DoVisit(AST_JsonPair node)
  {
    Console.Write("(JPAIR " + node.name + " " + node.nname);
    VisitChildren(node);
    Console.Write(")");
  }
}

static public class AST_Util
{
  static public List<AST_Base> GetChildren(this AST_Base self)
  {
    var ast = self as AST;
    return ast == null ? null : ast.children;
  }

  static public void AddChild(this AST_Base self, AST_Base c)
  {
    if(c == null)
      return;
    var ast = self as AST;
    ast.children.Add(c);
  }

  static public void AddChild(this AST self, AST_Base c)
  {
    if(c == null)
      return;
    self.children.Add(c);
  }

  static public AST NewInterimChild(this AST self)
  {
    var c = new AST_Interim();
    self.AddChild(c);
    return c;
  }

  ////////////////////////////////////////////////////////

  static public AST_Module New_Module(uint nname, string name)
  {
    var n = new AST_Module();
    n.nname = nname;
    if(Util.DEBUG)
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

  static public AST_FuncDecl New_FuncDecl(HashedName name, HashedName type)
  {
    var n = new AST_FuncDecl();
    Init_FuncDecl(n, name, type);
    return n;
  }

  static void Init_FuncDecl(AST_FuncDecl n, HashedName name, HashedName type)
  {
    n.ntype = (uint)type.n; 
    if(Util.DEBUG)
      n.type = type.s;

    n.nname1 = name.n1;
    //module id
    n.nname2 = name.n2;

    if(Util.DEBUG)
      n.name = name.s;

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

  static public HashedName Name(this AST_FuncDecl n)
  {
    return (n == null) ? new HashedName(0, "?") : new HashedName(n.nname1, n.nname2, n.name);
  }

  static public ulong nname(this AST_FuncDecl n)
  {
    return ((ulong)n.nname2 << 31) | ((ulong)n.nname1);
  }

  ////////////////////////////////////////////////////////

  static public AST_LambdaDecl New_LambdaDecl(HashedName name, HashedName type)
  {
    var n = new AST_LambdaDecl();
    Init_FuncDecl(n, name, type);

    return n;
  }

  static public HashedName Name(this AST_LambdaDecl n)
  {
    return new HashedName(n.nname1, n.nname2, n.name);
  }

  ////////////////////////////////////////////////////////

  static public AST_ClassDecl New_ClassDecl(HashedName name, HashedName parent)
  {
    var n = new AST_ClassDecl();

    n.nname = (uint)name.n;
    if(Util.DEBUG)
      n.name = name.s;

    n.nparent = (uint)parent.n;
    if(Util.DEBUG)
      n.parent = parent.s;

    return n;
  }

  static public HashedName Name(this AST_ClassDecl n)
  {
    return (n == null) ? new HashedName(0, "?") : new HashedName(n.nname, n.name);
  }

  static public HashedName ParentName(this AST_ClassDecl n)
  {
    return (n == null) ? new HashedName(0, "?") : new HashedName(n.nparent, n.parent);
  }

  ////////////////////////////////////////////////////////

  static public AST_EnumDecl New_EnumDecl(HashedName name)
  {
    var n = new AST_EnumDecl();

    n.nname = (uint)name.n;
    if(Util.DEBUG)
      n.name = name.s;

    return n;
  }

  static public HashedName Name(this AST_EnumDecl n)
  {
    return (n == null) ? new HashedName(0, "?") : new HashedName(n.nname, n.name);
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

  static public AST_TypeCast New_TypeCast(HashedName type)
  {
    var n = new AST_TypeCast();
    n.ntype = (uint)type.n;
    if(Util.DEBUG)
      n.type = type.s;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_UseParam New_UseParam(HashedName name, bool is_ref)
  {
    var n = new AST_UseParam();
    n.nname = (uint)name.n | (is_ref ? 1u << 29 : 0u);
    if(Util.DEBUG)
      n.name = name.s;

    return n;
  }

  static public bool IsRef(this AST_UseParam n)
  {
    return (n.nname & (1u << 29)) != 0; 
  }

  static public HashedName Name(this AST_UseParam n)
  {
    return new HashedName(n.nname & 0xFFFFFFF, n.name);
  }

  ////////////////////////////////////////////////////////

  static public AST_Call New_Call(EnumCall type, int line_num, HashedName name = new HashedName(), ClassSymbol scope_symb = null)
  {
    return New_Call(type, line_num, name, scope_symb != null ? (uint)scope_symb.Type().n : 0);
  }

  static public AST_Call New_Call(EnumCall type, int line_num, HashedName name, uint scope_ntype)
  {
    var n = new AST_Call();
    n.type = type;
    n.nname1 = name.n1;
    n.nname2 = name.n2;
    if(Util.DEBUG)
      n.name = name.s;
    n.scope_ntype = scope_ntype;
    n.line_num = (uint)line_num;

    return n;
  }

  static public HashedName Name(this AST_Call n)
  {
    return new HashedName(n.nname(), n.name);
  }

  static public ulong nname(this AST_Call n)
  {
    return ((ulong)n.nname2 << 31) | ((ulong)n.nname1);
  }

  static public ulong FuncId(this AST_Call n)
  {
    if(n.nname2 == 0)
      return ((ulong)n.scope_ntype << 31) | ((ulong)n.nname1);
    else
      return ((ulong)n.nname2 << 31) | ((ulong)n.nname1);
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
  
  ////////////////////////////////////////////////////////

  static public AST_PopValue New_PopValue()
  {
    return new AST_PopValue();
  }

  ////////////////////////////////////////////////////////

  static public AST_New New_New(ClassSymbol type)
  {
    var n = new AST_New();
    var type_name = type.Type(); 
    n.ntype = (uint)type_name.n;
    if(Util.DEBUG)
      n.type = type_name.s;

    return n;
  }

  static public HashedName Name(this AST_New n)
  {
    return new HashedName(n.ntype, n.type);
  }

  ////////////////////////////////////////////////////////

  static public AST_Literal New_Literal(EnumLiteral type)
  {
    var n = new AST_Literal();
    n.type = type;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_VarDecl New_VarDecl(HashedName name, bool is_ref, uint ntype)
  {
    var n = new AST_VarDecl();
    n.nname = (uint)name.n | (is_ref ? 1u << 29 : 0u);
    if(Util.DEBUG)
      n.name = name.s;
    n.ntype = ntype;

    return n;
  }

  static public HashedName Name(this AST_VarDecl n)
  {
    return new HashedName(n.nname & 0xFFFFFFF, n.name);
  }

  static public bool IsRef(this AST_VarDecl n)
  {
    return (n.nname & (1u << 29)) != 0; 
  }

  ////////////////////////////////////////////////////////

  static public AST_Inc New_Inc(HashedName name)
  {
    var n = new AST_Inc();
    n.nname = (uint)name.n;

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

  static public AST_JsonObj New_JsonObj(HashedName root_type_name)
  {
    var n = new AST_JsonObj();
    n.ntype = (uint)root_type_name.n;
    return n;
  }

  static public AST_JsonArr New_JsonArr(ArrayTypeSymbol arr_type)
  {
    var n = new AST_JsonArr();
    n.ntype = (uint)arr_type.Type().n;
    return n;
  }

  static public AST_JsonArrAddItem New_JsonArrAddItem()
  {
    var n = new AST_JsonArrAddItem();
    return n;
  }

  static public AST_JsonPair New_JsonPair(HashedName scope_type, HashedName name)
  {
    var n = new AST_JsonPair();
    n.scope_ntype = (uint)scope_type.n;
    n.nname = (uint)name.n;
    if(Util.DEBUG)
      n.name = name.s;

    return n;
  }

  static public HashedName Name(this AST_JsonPair n)
  {
    return new HashedName(n.nname, n.name);
  }
}

} //namespace bhl

