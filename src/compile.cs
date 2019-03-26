using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

public class Compiler : AST_Visitor
{
  public byte[] Compile(AST ast)
  {
    Visit(ast);
    return null;
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
  }

  public override void DoVisit(AST_FuncDecl ast)
  {
    VisitChildren(ast);
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
    VisitChildren(ast);
  }

  public override void DoVisit(AST_TypeCast ast)
  {
  }

  public override void DoVisit(AST_New ast)
  {
  }

  public override void DoVisit(AST_Inc ast)
  {
  }

  public override void DoVisit(AST_Call ast)
  {           
  }

  public override void DoVisit(AST_Return node)
  {
    VisitChildren(node);
  }

  public override void DoVisit(AST_Break node)
  {
  }

  public override void DoVisit(AST_PopValue node)
  {
  }

  public override void DoVisit(AST_Literal node)
  {
    Console.WriteLine("LITERAL " + node.nval);
  }

  public override void DoVisit(AST_BinaryOpExp node)
  {
    Console.WriteLine("BIN OP " + node.type);
    VisitChildren(node);
  }

  public override void DoVisit(AST_UnaryOpExp node)
  {
  }

  public override void DoVisit(AST_VarDecl node)
  {
  }

  public override void DoVisit(bhl.AST_JsonObj node)
  {
  }

  public override void DoVisit(bhl.AST_JsonArr node)
  {
  }

  public override void DoVisit(bhl.AST_JsonPair node)
  {
  }
}

} //namespace bhl
