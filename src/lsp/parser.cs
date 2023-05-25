using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace bhl.lsp {

//public class ParserAnalyzer : bhlBaseVisitor<object>
//{
//  public override object VisitFuncParamDeclare(bhlParser.FuncParamDeclareContext ctx)
//  {
//    var isRef = ctx.isRef();
//    var type = ctx.type();
//    var name = ctx.NAME();
//    var assignExp = ctx.assignExp();
//    
//    if(isRef != null)
//    {
//      var refStopIdx = type?.Start.StartIndex ?? (name?.Symbol.StartIndex ?? 0);
//      AddSemanticToken(ctx.Start.StartIndex, refStopIdx - 1, spec.SemanticTokenTypes.keyword);
//    }
//
//    if(type != null)
//      Visit(type);
//
//    AddSemanticToken(name, spec.SemanticTokenTypes.parameter);
//    
//    if(assignExp != null)
//      Visit(assignExp.exp());
//    
//    return null;
//  }
//  
//  public override object VisitJsonObject(bhlParser.JsonObjectContext ctx)
//  {
//    var newExp = ctx.newExp();
//    if(newExp != null)
//      VisitNewExp(newExp);
//
//    var jsonPair = ctx.jsonPair();
//    if(jsonPair != null)
//    {
//      foreach(var item in jsonPair)
//      {
//        var item_name = item.NAME();
//        var item_value = item.jsonValue();
//        
//        if(item_name != null)
//          AddSemanticToken(item_name, spec.SemanticTokenTypes.variable);
//        
//        if(item_value != null)
//          Visit(item_value.exp());
//      }
//    }
//    
//    return null;
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
//  public override object VisitCallArgs(bhlParser.CallArgsContext ctx)
//  {
//    foreach(var item in ctx.callArg())
//    {
//      var arg_name = item.NAME();
//      if(arg_name != null)
//        AddSemanticToken(arg_name, spec.SemanticTokenTypes.parameter);
//            
//      var arg_is_ref = item.isRef();
//      if(arg_is_ref != null)
//        AddSemanticToken(arg_is_ref.Start.StartIndex, arg_is_ref.Stop.StopIndex, spec.SemanticTokenTypes.keyword);
//
//      var arg_exp = item.exp();
//      if(arg_exp != null)
//        Visit(item.exp());
//    }
//    return null;
//  }
//  
//  public override object VisitImports(bhlParser.ImportsContext ctx)
//  {
//    foreach(var mimport in ctx.mimport())
//    {
//      var normalstring = mimport.NORMALSTRING();
//      
//      var import = normalstring.GetText();
//      import = import.Substring(1, import.Length-2); // removing quotes
//      imports.Add(import);
//      
//      AddSemanticToken(mimport.Start.StartIndex, normalstring.Symbol.StartIndex - 1, spec.SemanticTokenTypes.keyword);
//      AddSemanticToken(normalstring, spec.SemanticTokenTypes.@string);
//    }
//    
//    return null;
//  }
//  
//}
}
