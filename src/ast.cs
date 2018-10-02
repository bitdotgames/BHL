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

namespace fbhl {

public static class Extensions
{
  static public bhl.HashedName Name(this AST_VarDecl n)
  {
    return new bhl.HashedName(n.Nname & 0xFFFFFFF, n.Name);
  }

  static public bool IsRef(this AST_VarDecl n)
  {
    return (n.Nname & (1u << 29)) != 0; 
  }

  static public bhl.HashedName Name(this AST_FuncDecl n)
  {
    return new bhl.HashedName(n.Nname1, n.Nname2, n.Name);
  }

  static public ulong nname(this AST_FuncDecl n)
  {
    return ((ulong)n.Nname2 << 31) | ((ulong)n.Nname1);
  }

  static public bhl.HashedName Name(this AST_Call n)
  {
    return new bhl.HashedName(n.nname(), n.Name);
  }

  static public ulong nname(this AST_Call n)
  {
    return ((ulong)n.Nname2 << 31) | ((ulong)n.Nname1);
  }

  static public ulong FuncId(this AST_Call n)
  {
    if(n.Nname2 == 0)
      return ((ulong)n.ScopeNtype << 31) | ((ulong)n.Nname1);
    else
      return ((ulong)n.Nname2 << 31) | ((ulong)n.Nname1);
  }

  static public int GetDefaultArgsNum(this AST_FuncDecl n)
  {
    var fparams = n.Children(0).Value.V<fbhl.AST_Interim>().Value;

    int num = 0;
    for(int i=0;i<fparams.ChildrenLength;++i)
    {
      var fc = fparams.Children(i).Value.V<fbhl.AST_VarDecl>();
      if(fc != null && fc.Value.ChildrenLength > 0)
        ++num;
    }
    return num;
  }

  static public bhl.HashedName Name(this AST_New n)
  {
    return new bhl.HashedName(n.Ntype, n.Type);
  }

  static public bhl.HashedName Name(this AST_ClassDecl n)
  {
    return new bhl.HashedName(n.Nname, n.Name);
  }

  static public bhl.HashedName ParentName(this AST_ClassDecl n)
  {
    return new bhl.HashedName(n.Nparent, n.Parent);
  }

  static public bhl.HashedName Name(this AST_JsonPair n)
  {
    return new bhl.HashedName(n.Nname, n.Name);
  }

  static public bool IsRef(this UseParam n)
  {
    return (n.Nname & (1u << 29)) != 0; 
  }

  static public bhl.HashedName Name(this UseParam n)
  {
    return new bhl.HashedName(n.Nname & 0xFFFFFFF, n.Name);
  }

  static public bhl.HashedName Name(this AST_EnumDecl n)
  {
    return new bhl.HashedName(n.Nname, n.Name);
  }
}

} //namespace fbhl

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
    //NOTE: base class AST_FuncDecl must be handled after AST_LambdaDecl
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
  public FlatBuffers.FlatBufferBuilder fbb;

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
    fbb = new FlatBuffers.FlatBufferBuilder(128);
  }

  public MemoryStream GetStream()
  {
    return fbb.DataBuffer.ToMemoryStream(0, fbb.Offset);
  }

  public byte[] GetBytes()
  {
    return fbb.DataBuffer.ToSizedArray();
  }

  void DebugStats(AST_Base node)
  {
    //Console.WriteLine("NODE " + node.GetType().Name + " " + fbb.Offset);
  }

  public override void DoVisit(AST_Module node)
  {
    StartSelectorVector();
    VisitChildren(node);
    var children = EndSelectorVector();

    var name = MakeString(node.name);

    fbhl.AST_Module.StartAST_Module(fbb);
    fbhl.AST_Module.AddNname(fbb, node.nname);
    if(name != null)
      fbhl.AST_Module.AddName(fbb, name.Value);
    if(children != null)
      fbhl.AST_Module.AddChildren(fbb, children.Value);
    var m = fbhl.AST_Module.EndAST_Module(fbb);

    fbhl.AST_Module.FinishAST_ModuleBuffer(fbb, m);

    DebugStats(node);
  }

  public override void DoVisit(AST_Interim node)
  {
    StartSelectorVector();
    VisitChildren(node);
    var children = EndSelectorVector();

    fbhl.AST_Interim.StartAST_Interim(fbb);
    if(children != null)
      fbhl.AST_Interim.AddChildren(fbb, children.Value);
    AddSelector(fbhl.AST_Interim.EndAST_Interim(fbb));

    DebugStats(node);
  }

  public override void DoVisit(AST_Import node)
  {
    AddSelector(fbhl.AST_Import.CreateAST_Import(
      fbb, 
      fbhl.AST_Import.CreateModulesVector(fbb, node.modules.ToArray())
    ));

    DebugStats(node);
  }

  public override void DoVisit(AST_FuncDecl node)
  {
    StartSelectorVector();
    VisitChildren(node);
    var children = EndSelectorVector();

    AddSelector(MakeFuncDecl(node, children));

    DebugStats(node);
  }

  FlatBuffers.Offset<fbhl.AST_FuncDecl> MakeFuncDecl(AST_FuncDecl node, FlatBuffers.VectorOffset? children, FlatBuffers.Offset<fbhl.Lambda>? lambda = null)
  {
    var type = MakeString(node.type);

    var name = MakeString(node.name);

    fbhl.AST_FuncDecl.StartAST_FuncDecl(fbb);
    fbhl.AST_FuncDecl.AddNtype(fbb, node.ntype);
    if(type != null)
      fbhl.AST_FuncDecl.AddType(fbb, type.Value);
    fbhl.AST_FuncDecl.AddNname1(fbb, node.nname1);
    fbhl.AST_FuncDecl.AddNname2(fbb, node.nname2);
    if(children != null)
      fbhl.AST_FuncDecl.AddChildren(fbb, children.Value);
    if(name != null)
      fbhl.AST_FuncDecl.AddName(fbb, name.Value);
    if(lambda != null)
      fbhl.AST_FuncDecl.AddLambda(fbb, lambda.Value);
    return fbhl.AST_FuncDecl.EndAST_FuncDecl(fbb);
  }

  public override void DoVisit(AST_LambdaDecl node)
  {
    StartSelectorVector();
    VisitChildren(node);
    var children = EndSelectorVector();  

    FlatBuffers.VectorOffset? useparams = null;
    if(node.useparams.Count > 0)
    {
      var tmp = new List<FlatBuffers.Offset<fbhl.UseParam>>();
      for(int i=0;i<node.useparams.Count;++i)
        tmp.Add(MakeUseParam(node.useparams[i]));

      useparams = fbhl.Lambda.CreateUseparamsVector(fbb, tmp.ToArray());
    }

    fbhl.Lambda.StartLambda(fbb);
    if(useparams != null)
      fbhl.Lambda.AddUseparams(fbb, useparams.Value);
    var lambda = fbhl.Lambda.EndLambda(fbb);

    var func_decl = MakeFuncDecl(node, children, lambda);

    AddSelector(func_decl);

    DebugStats(node);
  }

  FlatBuffers.Offset<fbhl.UseParam> MakeUseParam(AST_UseParam up)
  {
    var name = MakeString(up.name);

    fbhl.UseParam.StartUseParam(fbb);
    fbhl.UseParam.AddNname(fbb, up.nname);
    if(name != null)
      fbhl.UseParam.AddName(fbb, name.Value);

    return fbhl.UseParam.EndUseParam(fbb);
  }

  public override void DoVisit(AST_ClassDecl node)
  {
    StartSelectorVector();
    VisitChildren(node);
    var children = EndSelectorVector();  

    var name = MakeString(node.name);

    var parent = MakeString(node.parent);

    fbhl.AST_ClassDecl.StartAST_ClassDecl(fbb);
    fbhl.AST_ClassDecl.AddNname(fbb, node.nname);
    if(name != null)
      fbhl.AST_ClassDecl.AddName(fbb, name.Value);
    fbhl.AST_ClassDecl.AddNparent(fbb, node.nparent);
    if(parent != null)
      fbhl.AST_ClassDecl.AddParent(fbb, parent.Value);
    if(children != null)
      fbhl.AST_ClassDecl.AddChildren(fbb, children.Value);
    AddSelector(fbhl.AST_ClassDecl.EndAST_ClassDecl(fbb));

    DebugStats(node);
  }

  public override void DoVisit(AST_EnumDecl node)
  {
    var name = MakeString(node.name);

    //EnumItem is a struct not a table so it's inlined
    fbhl.AST_EnumDecl.StartItemsVector(fbb, node.children.Count);
    for(int i=0;i<node.children.Count;++i)
    {
      var tmp = (AST_EnumItem)node.children[i];
      fbhl.EnumItem.CreateEnumItem(fbb, tmp.nname, tmp.value);
    }
    var items = fbb.EndVector();

    fbhl.AST_EnumDecl.StartAST_EnumDecl(fbb);
    fbhl.AST_EnumDecl.AddNname(fbb, node.nname);
    if(name != null)
      fbhl.AST_EnumDecl.AddName(fbb, name.Value);
    fbhl.AST_EnumDecl.AddItems(fbb, items);
    AddSelector(fbhl.AST_EnumDecl.EndAST_EnumDecl(fbb));

    DebugStats(node);
  }

  public override void DoVisit(AST_EnumItem node)
  {
    //doing nothing here, it's handled above
  }

  public override void DoVisit(AST_Block node)
  {
    StartSelectorVector();
    VisitChildren(node);
    var children = EndSelectorVector();

    fbhl.AST_Block.StartAST_Block(fbb);
    fbhl.AST_Block.AddType(fbb, (fbhl.EnumBlock)node.type);
    if(children != null)
      fbhl.AST_Block.AddChildren(fbb, children.Value);
    AddSelector(fbhl.AST_Block.EndAST_Block(fbb));

    DebugStats(node);
  }

  public override void DoVisit(AST_TypeCast node)
  {
    StartSelectorVector();
    VisitChildren(node);
    var children = EndSelectorVector();

    var type = MakeString(node.type);

    fbhl.AST_TypeCast.StartAST_TypeCast(fbb);
    fbhl.AST_TypeCast.AddNtype(fbb, node.ntype);
    if(type != null)
      fbhl.AST_TypeCast.AddType(fbb, type.Value);
    if(children != null)
      fbhl.AST_TypeCast.AddChildren(fbb, children.Value);
    AddSelector(fbhl.AST_TypeCast.EndAST_TypeCast(fbb));
  }

  public override void DoVisit(AST_Call node)
  {
    StartSelectorVector();
    VisitChildren(node);
    var children = EndSelectorVector();

    var name = MakeString(node.name);

    fbhl.AST_Call.StartAST_Call(fbb);
    fbhl.AST_Call.AddType(fbb, (fbhl.EnumCall)node.type);
    fbhl.AST_Call.AddNname1(fbb, node.nname1);
    fbhl.AST_Call.AddNname2(fbb, node.nname2);
    if(name != null)
      fbhl.AST_Call.AddName(fbb, name.Value);
    fbhl.AST_Call.AddCargsBits(fbb, node.cargs_bits);
    if(node.scope_ntype != 0)
      fbhl.AST_Call.AddScopeNtype(fbb, node.scope_ntype);
    fbhl.AST_Call.AddLineNum(fbb, node.line_num);
    if(children != null)
      fbhl.AST_Call.AddChildren(fbb, children.Value);
    AddSelector(fbhl.AST_Call.EndAST_Call(fbb));

    DebugStats(node);
  }

  public override void DoVisit(AST_Inc node)
  {
    fbhl.AST_Inc.StartAST_Inc(fbb);
    AddSelector(fbhl.AST_Inc.EndAST_Inc(fbb));
  }

  public override void DoVisit(AST_Return node)
  {
    StartSelectorVector();
    VisitChildren(node);
    var children = EndSelectorVector();

    fbhl.AST_Return.StartAST_Return(fbb);
    if(children != null)
      fbhl.AST_Return.AddChildren(fbb, children.Value);
    AddSelector(fbhl.AST_Return.EndAST_Return(fbb));

    DebugStats(node);
  }

  public override void DoVisit(AST_Break node)
  {
    fbhl.AST_Break.StartAST_Break(fbb);
    AddSelector(fbhl.AST_Break.EndAST_Break(fbb));
  }

  public override void DoVisit(AST_PopValue node)
  {
    fbhl.AST_PopValue.StartAST_PopValue(fbb);
    AddSelector(fbhl.AST_PopValue.EndAST_PopValue(fbb));
  }

  public override void DoVisit(AST_Literal node)
  {
    if(node.type == EnumLiteral.STR)
    {
      AddSelector(fbhl.AST_LiteralStr.CreateAST_LiteralStr(fbb, fbb.CreateString(node.sval)));
    }
    else if(node.type == EnumLiteral.NUM)
    {
      AddSelector(fbhl.AST_LiteralNum.CreateAST_LiteralNum(fbb, node.nval));
    }
    else if(node.type == EnumLiteral.BOOL)
    {
      AddSelector(fbhl.AST_LiteralBool.CreateAST_LiteralBool(fbb, node.nval == 1));
    }
    else if(node.type == EnumLiteral.NIL)
    {
      fbhl.AST_LiteralNil.StartAST_LiteralNil(fbb);
      AddSelector(fbhl.AST_LiteralNil.EndAST_LiteralNil(fbb));
    }

    DebugStats(node);
  }

  public override void DoVisit(AST_BinaryOpExp node)
  {
    StartSelectorVector();

    VisitChildren(node);

    AddSelector(fbhl.AST_BinaryOpExp.CreateAST_BinaryOpExp(
      fbb, 
      (fbhl.EnumBinaryOp)node.type,
      EndSelectorVector().Value
    ));
  }

  public override void DoVisit(AST_UnaryOpExp node)
  {
    StartSelectorVector();

    VisitChildren(node);

    AddSelector(fbhl.AST_UnaryOpExp.CreateAST_UnaryOpExp(
      fbb, 
      (fbhl.EnumUnaryOp)node.type,
      EndSelectorVector().Value
    ));
  }

  public override void DoVisit(AST_New node)
  {
    StartSelectorVector();
    VisitChildren(node);
    var children = EndSelectorVector();

    var type = MakeString(node.type);

    fbhl.AST_New.StartAST_New(fbb);
    fbhl.AST_New.AddNtype(fbb, node.ntype);
    if(type != null)
      fbhl.AST_New.AddType(fbb, type.Value);
    if(children != null)
      fbhl.AST_New.AddChildren(fbb, children.Value);
    AddSelector(fbhl.AST_New.EndAST_New(fbb));
  }

  public override void DoVisit(AST_VarDecl node)
  {
    StartSelectorVector();
    VisitChildren(node);
    var children = EndSelectorVector();

    var name = MakeString(node.name);

    fbhl.AST_VarDecl.StartAST_VarDecl(fbb);
    fbhl.AST_VarDecl.AddNname(fbb, node.nname);
    if(name != null)
      fbhl.AST_VarDecl.AddName(fbb, name.Value);
    fbhl.AST_VarDecl.AddNtype(fbb, node.ntype);
    if(children != null)
      fbhl.AST_VarDecl.AddChildren(fbb, children.Value);
    AddSelector(fbhl.AST_VarDecl.EndAST_VarDecl(fbb));

    DebugStats(node);
  }

  public override void DoVisit(AST_JsonObj node)
  {
    StartSelectorVector();
    VisitChildren(node);
    var children = EndSelectorVector();

    fbhl.AST_JsonObj.StartAST_JsonObj(fbb);
    fbhl.AST_JsonObj.AddNtype(fbb, node.ntype);
    if(children != null)
      fbhl.AST_JsonObj.AddChildren(fbb, children.Value);
    AddSelector(fbhl.AST_JsonObj.EndAST_JsonObj(fbb));

    DebugStats(node);
  }

  public override void DoVisit(AST_JsonArr node)
  {
    StartSelectorVector();
    VisitChildren(node);
    var children = EndSelectorVector();

    fbhl.AST_JsonArr.StartAST_JsonArr(fbb);
    fbhl.AST_JsonArr.AddNtype(fbb, node.ntype);
    if(children != null)
      fbhl.AST_JsonArr.AddChildren(fbb, children.Value);
    AddSelector(fbhl.AST_JsonArr.EndAST_JsonArr(fbb));

    DebugStats(node);
  }

  public override void DoVisit(AST_JsonPair node)
  {
    StartSelectorVector();
    VisitChildren(node);
    var children = EndSelectorVector();

    var name = MakeString(node.name);

    fbhl.AST_JsonPair.StartAST_JsonPair(fbb);
    fbhl.AST_JsonPair.AddNname(fbb, node.nname);
    if(name != null)
      fbhl.AST_JsonPair.AddName(fbb, name.Value);
    fbhl.AST_JsonPair.AddScopeNtype(fbb, node.scope_ntype);
    if(children != null)
      fbhl.AST_JsonPair.AddChildren(fbb, children.Value);
    AddSelector(fbhl.AST_JsonPair.EndAST_JsonPair(fbb));
  }

  public override void DoVisit(AST_JsonArrAddItem node)
  {
    fbhl.AST_JsonArrAddItem.StartAST_JsonArrAddItem(fbb);
    AddSelector(fbhl.AST_JsonArrAddItem.EndAST_JsonArrAddItem(fbb));
  }

  ///////////////////////////////////////////////////////////

  void StartSelectorVector()
  {
    child_sel_stack.Push(new List<ChildSelector>());
  }

  FlatBuffers.StringOffset? MakeString(string str)
  {
    if(string.IsNullOrEmpty(str))
      return null;

    return fbb.CreateString(str);
  }

  FlatBuffers.VectorOffset? EndSelectorVector()
  {
    var selectors = child_sel_stack.Pop();
    if(selectors.Count == 0)
      return null;

    var res = new FlatBuffers.Offset<fbhl.AST_Selector>[selectors.Count];

    for(int s=0;s<selectors.Count;++s)
    {
      var sel = selectors[s];

      fbhl.AST_Selector.StartAST_Selector(fbb);
      fbhl.AST_Selector.AddVType(fbb, sel.type);
      fbhl.AST_Selector.AddV(fbb, sel.offset);

      res[s] = fbhl.AST_Selector.EndAST_Selector(fbb);
    }

    return fbb.CreateVectorOfTables(res);
  }

  void AddSelector<T>(FlatBuffers.Offset<T> offset) where T : struct
  {
    child_sel_stack.Peek().Add(MatchToSelector(offset));
  }

  static Dictionary<System.Type, fbhl.AST_OneOf> type2selector = new Dictionary<System.Type, fbhl.AST_OneOf>() {
    { typeof(fbhl.AST_LiteralStr),    fbhl.AST_OneOf.AST_LiteralStr},
    { typeof(fbhl.AST_LiteralNum),    fbhl.AST_OneOf.AST_LiteralNum},
    { typeof(fbhl.AST_LiteralBool),   fbhl.AST_OneOf.AST_LiteralBool},
    { typeof(fbhl.AST_LiteralNil),    fbhl.AST_OneOf.AST_LiteralNil},
    { typeof(fbhl.AST_Break),         fbhl.AST_OneOf.AST_Break},
    { typeof(fbhl.AST_Block),         fbhl.AST_OneOf.AST_Block},
    { typeof(fbhl.AST_UnaryOpExp),    fbhl.AST_OneOf.AST_UnaryOpExp},
    { typeof(fbhl.AST_BinaryOpExp),   fbhl.AST_OneOf.AST_BinaryOpExp},   
    { typeof(fbhl.AST_PopValue),      fbhl.AST_OneOf.AST_PopValue},      
    { typeof(fbhl.AST_Interim),       fbhl.AST_OneOf.AST_Interim},       
    { typeof(fbhl.AST_Call),          fbhl.AST_OneOf.AST_Call},          
    { typeof(fbhl.AST_New),           fbhl.AST_OneOf.AST_New},           
    { typeof(fbhl.AST_Return),        fbhl.AST_OneOf.AST_Return},        
    { typeof(fbhl.AST_Import),        fbhl.AST_OneOf.AST_Import},        
    { typeof(fbhl.AST_VarDecl),       fbhl.AST_OneOf.AST_VarDecl},       
    { typeof(fbhl.AST_FuncDecl),      fbhl.AST_OneOf.AST_FuncDecl},      
    { typeof(fbhl.AST_ClassDecl),     fbhl.AST_OneOf.AST_ClassDecl},     
    { typeof(fbhl.AST_EnumDecl),      fbhl.AST_OneOf.AST_EnumDecl},     
    { typeof(fbhl.AST_Inc),           fbhl.AST_OneOf.AST_Inc},           
    { typeof(fbhl.AST_TypeCast),      fbhl.AST_OneOf.AST_TypeCast},      
    { typeof(fbhl.AST_JsonObj),       fbhl.AST_OneOf.AST_JsonObj},       
    { typeof(fbhl.AST_JsonArr),       fbhl.AST_OneOf.AST_JsonArr},       
    { typeof(fbhl.AST_JsonPair),      fbhl.AST_OneOf.AST_JsonPair},      
    { typeof(fbhl.AST_JsonArrAddItem),fbhl.AST_OneOf.AST_JsonArrAddItem},
  };

  ChildSelector MatchToSelector<T>(FlatBuffers.Offset<T> offset) where T : struct
  {
    var t = typeof(T);

    fbhl.AST_OneOf s;
    if(type2selector.TryGetValue(t, out s))
      return new ChildSelector(s, offset.Value);

    throw new Exception("Unhandled selector type: " + t.Name);
  }
}

public class AST_Dumper : AST_Visitor
{
  public override void DoVisit(AST_Interim node)
  {
    Console.Write("(I ");
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
    Console.Write("(BLOCK_");
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
      Console.Write(" (LIT " + node.nval + ")");
    else if(node.type == EnumLiteral.BOOL)
      Console.Write(" (LIT " + node.nval + ")");
    else if(node.type == EnumLiteral.STR)
      Console.Write(" (LIT '" + node.sval + "')");
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

