using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace bhl.lsp {

//public class ParserAnalyzer : bhlBaseVisitor<object>
//{
//  public override object VisitExpMulDivMod(bhlParser.ExpMulDivModContext ctx)
//  {
//    var op = ctx.operatorMulDivMod();
//    var op_exp_left = ctx.exp(0);
//    var op_exp_right = ctx.exp(1);
//    
//    if(op != null)
//      AddSemanticToken(op.Start.StartIndex, op.Stop.StopIndex, spec.SemanticTokenTypes.@operator);
//
//    if(op_exp_left != null)
//      Visit(op_exp_left);
//    
//    if(op_exp_right != null)
//      Visit(op_exp_right);
//    
//    return null;
//  }
//  
//  public override object VisitExpCompare(bhlParser.ExpCompareContext ctx)
//  {
//    var op = ctx.operatorComparison();
//    var op_exp_left = ctx.exp(0);
//    var op_exp_right = ctx.exp(1);
//    
//    if(op != null)
//      AddSemanticToken(op.Start.StartIndex, op.Stop.StopIndex, spec.SemanticTokenTypes.@operator);
//
//    if(op_exp_left != null)
//      Visit(op_exp_left);
//    
//    if(op_exp_right != null)
//      Visit(op_exp_right);
//    
//    return null;
//  }
//  
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
//  public override object VisitExpUnary(bhlParser.ExpUnaryContext ctx)
//  {
//    var op = ctx.operatorUnary();
//    AddSemanticToken(op.Start.StartIndex, op.Stop.StopIndex, spec.SemanticTokenTypes.@operator);
//    Visit(ctx.exp());
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
//  public override object VisitType(bhlParser.TypeContext ctx)
//  {
//    var fn_type = ctx.funcType();
//    if(fn_type != null && fn_type.types() is bhlParser.TypesContext types)
//    {
//      foreach(var refType in types.refType())
//      {
//        var refNameIsRef = refType.isRef();
//        //TODO: parse the whole nsName()
//        var refNameName = refType.type()?.nsName()?.dotName().NAME();
//        if(refNameName != null)
//        {
//          if(refNameIsRef != null)
//            AddSemanticToken(refNameIsRef.Start.StartIndex, refNameName.Symbol.StartIndex-1, spec.SemanticTokenTypes.keyword);
//        
//          AddSemanticTokenTypeName(refNameName);
//        }
//      }
//    }
//    return null;
//  }
//  
//  public override object VisitVarPostIncDec(bhlParser.VarPostIncDecContext ctx)
//  {
//    CommonPostIncDec(ctx.callPostIncDec());
//    return null;
//  }
//
//  void CommonPostIncDec(bhlParser.CallPostIncDecContext ctx)
//  {
//    ////TODO: take into account the whole name
//    //var callPostOperatorName = ctx.dotName().NAME();
//    //if(callPostOperatorName != null)
//    //  AddSemanticToken(callPostOperatorName, spec.SemanticTokenTypes.variable);
//    //
//    //var decrementOperator = ctx.decrementOperator();
//    //var incrementOperator = ctx.incrementOperator();
//    //
//    //if(decrementOperator != null)
//    //  AddSemanticToken(decrementOperator.Start.StartIndex, decrementOperator.Stop.StopIndex, spec.SemanticTokenTypes.@operator);
//    //
//    //if(incrementOperator != null)
//    //  AddSemanticToken(incrementOperator.Start.StartIndex, incrementOperator.Stop.StopIndex, spec.SemanticTokenTypes.@operator);
//  }
//}
}
