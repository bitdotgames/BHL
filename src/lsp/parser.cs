using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace bhl.lsp {

//public class ParserAnalyzer : bhlBaseVisitor<object>
//{
//  public override object VisitFuncParamDeclare(bhlParser.FuncParamDeclareContext ctx)
//  {
//    AddSemanticToken(name, spec.SemanticTokenTypes.parameter);
//  }
//  
//  public override object VisitCallExp(bhlParser.CallExpContext ctx)
//  {
//    var name = ctx.NAME();
//    var exp = ctx.chainExp();
//    
//    if(exp != null)
//    {
//      foreach(var item in exp)
//      {
//        if(item.callArgs() is bhlParser.CallArgsContext callArgs)
//        {
//          if(name != null)
//          {
//            AddSemanticToken(name, spec.SemanticTokenTypes.function);
//            name = null;
//          }
//
//          Visit(callArgs);
//        }
//        else if(item.memberAccess() is bhlParser.MemberAccessContext memberAccess)
//        {
//          if(name != null)
//            AddSemanticToken(name, spec.SemanticTokenTypes.variable);
//          
//          name = memberAccess.NAME();
//
//          Visit(memberAccess);
//        }
//        else if(item.arrAccess() is bhlParser.ArrAccessContext arrAccess)
//        {
//          if(name != null)
//            AddSemanticToken(name, spec.SemanticTokenTypes.variable);
//          
//          name = null;
//
//          Visit(arrAccess);
//        }
//      }
//      
//      if(name != null)
//        AddSemanticToken(name, spec.SemanticTokenTypes.variable);
//    }
//    return null;
//  }
//  
//}
}
