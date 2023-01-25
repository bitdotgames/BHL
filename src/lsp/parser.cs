using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace bhl.lsp {

public class Parser : bhlBaseVisitor<object>
{
  public readonly List<string> imports = new List<string>();
  public readonly Dictionary<string, bhlParser.FuncDeclContext> funcDecls = new Dictionary<string, bhlParser.FuncDeclContext>();
  public readonly Dictionary<string, bhlParser.ClassDeclContext> classDecls = new Dictionary<string, bhlParser.ClassDeclContext>();
  public readonly List<uint> dataSemanticTokens = new List<uint>();
  
  int next_idx;

  BHLDocument document;

  public void Parse(BHLDocument document)
  {
    this.document = document;
    next_idx = 0;
    
    imports.Clear();
    funcDecls.Clear();
    classDecls.Clear();
    dataSemanticTokens.Clear();
    
    VisitProgram(document.ToParser().program());
  }
  
  public override object VisitProgram(bhlParser.ProgramContext ctx)
  {
    for(var i=0;i<ctx.progblock().Length;++i)
      Visit(ctx.progblock()[i]);
    
    return null;
  }

  public override object VisitClassDecl(bhlParser.ClassDeclContext ctx)
  {
    var class_name = ctx.NAME();

    if(class_name != null)
    {
      var classDeclNameText = class_name.GetText();
      if(!classDecls.ContainsKey(classDeclNameText))
        classDecls.Add(classDeclNameText, ctx);
    }
    
    AddSemanticToken(ctx.Start.StartIndex, class_name.Symbol.StartIndex - 1, spec.SemanticTokenTypes.keyword);
    
    AddSemanticToken(class_name, spec.SemanticTokenTypes.@class);
    
    if(ctx.extensions() != null)
    {
      for(int i=0;i<ctx.extensions().nsName().Length;++i)
      {
        var ext_name = ctx.extensions().nsName()[i];
        AddSemanticToken(ext_name.dotName().NAME(), spec.SemanticTokenTypes.@class);
      }
    }
    
    foreach(var classMember in ctx.classBlock().classMembers().classMember())
    {
      var cl_mem_var_decl = classMember.fldDeclare()?.varDeclare();
      if(cl_mem_var_decl != null)
      {
        Visit(cl_mem_var_decl.type());
        
        AddSemanticToken(
          cl_mem_var_decl.NAME(), 
          spec.SemanticTokenTypes.variable,
          spec.SemanticTokenModifiers.definition, 
          spec.SemanticTokenModifiers.@static
        );
      }
    }

    return null;
  }
  
  public override object VisitFuncDecl(bhlParser.FuncDeclContext ctx)
  {
    var func_name = ctx.NAME();
    var ret_type = ctx.retType();
    var func_params = ctx.funcParams();
    var func_block = ctx.funcBlock();

    if(func_name != null)
    {
      var funcDeclNameText = func_name.GetText();
      if(!funcDecls.ContainsKey(funcDeclNameText))
        funcDecls.Add(funcDeclNameText, ctx);
    }
    
    var keyword_stop_idx = ret_type?.Start.StartIndex ?? (func_name?.Symbol.StartIndex ?? 0);

    AddSemanticToken(ctx.Start.StartIndex, Math.Max(ctx.Start.StartIndex, keyword_stop_idx - 1),
      spec.SemanticTokenTypes.keyword);
    
    if(ret_type != null)
    {
      foreach(var t in ret_type.type())
      {
        if(t.exception != null)
          continue;
        
        Visit(t);
      }
    }
    
    if(func_name != null)
    {
      AddSemanticToken(
        func_name, 
        spec.SemanticTokenTypes.function,
        spec.SemanticTokenModifiers.definition, 
        spec.SemanticTokenModifiers.@static
      );
    }

    if(func_params != null)
    {
      foreach(var funcParamDeclare in func_params.funcParamDeclare())
        VisitFuncParamDeclare(funcParamDeclare);
    }

    if(func_block != null)
      Visit(func_block);
    
    return null;
  }
  
  public override object VisitFuncBlock(bhlParser.FuncBlockContext ctx)
  {
    var fn_block = ctx.block();
    if(fn_block != null)
      Visit(fn_block);
    
    return null;
  }
  
  public override object VisitBlock(bhlParser.BlockContext ctx)
  {
    foreach(var item in ctx.statement())
      Visit(item);
    
    return null;
  }
  
  public override object VisitExpTypeCast(bhlParser.ExpTypeCastContext ctx)
  {
    var cast_type = ctx.type();
    if(cast_type != null)
      Visit(cast_type);
    
    var cast_exp = ctx.exp();
    if(cast_exp != null)
      Visit(cast_exp);
    
    return null;
  }
  
  public override object VisitLambdaCall(bhlParser.LambdaCallContext ctx)
  {
    var fn_lmb = ctx.funcLambda();
    if(fn_lmb != null)
      Visit(fn_lmb);
    return null;
  }
  
  public override object VisitExpLambda(bhlParser.ExpLambdaContext ctx)
  {
    var fn_lmb = ctx.funcLambda();
    if(fn_lmb != null)
      Visit(fn_lmb);
    return null;
  }

  public override object VisitFuncLambda(bhlParser.FuncLambdaContext ctx)
  {
    AddSemanticToken(ctx.Start.StartIndex, ctx.Start.StartIndex+3, spec.SemanticTokenTypes.keyword);
      
    var ret_type = ctx.retType();
    var fn_params = ctx.funcParams();
    var fn_block = ctx.funcBlock();
    var chain_exp = ctx.chainExp();

    if(ret_type != null)
    {
      foreach(var t in ret_type.type())
      {
        if(t.exception != null)
          continue;
        
        Visit(t);
      }
    }
      
    if(fn_params != null)
    {
      foreach(var funcParamDeclare in fn_params.funcParamDeclare())
        VisitFuncParamDeclare(funcParamDeclare);
    }
      
    if(fn_block != null)
      Visit(fn_block);
      
    if(chain_exp != null)
    {
      foreach(var chainExpItem in chain_exp)
        Visit(chainExpItem);
    }
    
    return null;
  }
  
  public override object VisitChainExp(bhlParser.ChainExpContext ctx)
  {
    if(ctx.callArgs() is bhlParser.CallArgsContext callArgs)
      Visit(callArgs);
    else if(ctx.memberAccess() is bhlParser.MemberAccessContext memberAccess)
      Visit(memberAccess);
    else if(ctx.arrAccess() is bhlParser.ArrAccessContext arrAccess)
      Visit(arrAccess);
    
    return null;
  }
  
  public override object VisitForeach(bhlParser.ForeachContext ctx)
  {
    var foreach_exp = ctx.foreachExp();
    var foreach_block = ctx.block();
    
    if(foreach_exp != null)
    {
      AddSemanticToken(ctx.Start.StartIndex, foreach_exp.Start.StartIndex-1, spec.SemanticTokenTypes.keyword);
      
      var exp = foreach_exp.exp();
      if(exp != null)
        Visit(exp);
      
      //TODO: support multi-declares
      var var_or_decl = foreach_exp.varOrDeclares().varOrDeclare()[0];
      var var_decl = var_or_decl?.varDeclare();
      var var_or_decl_name = var_or_decl?.NAME();
      
      if(var_or_decl_name != null)
        AddSemanticToken(var_or_decl_name, spec.SemanticTokenTypes.variable);
      else if(var_decl != null)
        VisitVarDeclare(var_decl);
    }
    
    if(foreach_block != null)
      Visit(foreach_block);
    
    return null;
  }
  
  public override object VisitBreak(bhlParser.BreakContext ctx)
  {
    AddSemanticToken(ctx.Start.StartIndex, ctx.Stop.StopIndex, spec.SemanticTokenTypes.keyword);
    return null;
  }
  
  public override object VisitYield(bhlParser.YieldContext ctx)
  {
    AddSemanticToken(ctx.Start.StartIndex, ctx.Stop.StopIndex-2, spec.SemanticTokenTypes.keyword);
    return null;
  }
  
  public override object VisitYieldWhile(bhlParser.YieldWhileContext ctx)
  {
    var yield_while_exp = ctx.exp();
    if(yield_while_exp != null)
    {
      AddSemanticToken(ctx.Start.StartIndex, yield_while_exp.Start.StartIndex-2, spec.SemanticTokenTypes.keyword);
      Visit(yield_while_exp);
    }
    return null;
  }
  
  public override object VisitDefer(bhlParser.DeferContext ctx)
  {
    var defer_block = ctx.block();
    
    if(defer_block != null)
    {
      AddSemanticToken(ctx.Start.StartIndex, defer_block.Start.StartIndex-1, spec.SemanticTokenTypes.keyword);
      Visit(defer_block);
    }
    
    return null;
  }
  
  public override object VisitReturn(bhlParser.ReturnContext ctx)
  {
    var exps = ctx.returnVal()?.exps();
    if(exps != null)
    {
      AddSemanticToken(ctx.Start.StartIndex, exps.Start.StartIndex-1, spec.SemanticTokenTypes.keyword);
      foreach(var exp in exps.exp())
        Visit(exp);
    }
    
    return null;
  }
  
  public override object VisitExpMulDivMod(bhlParser.ExpMulDivModContext ctx)
  {
    var op = ctx.operatorMulDivMod();
    var op_exp_left = ctx.exp(0);
    var op_exp_right = ctx.exp(1);
    
    if(op != null)
      AddSemanticToken(op.Start.StartIndex, op.Stop.StopIndex, spec.SemanticTokenTypes.@operator);

    if(op_exp_left != null)
      Visit(op_exp_left);
    
    if(op_exp_right != null)
      Visit(op_exp_right);
    
    return null;
  }
  
  public override object VisitExpCompare(bhlParser.ExpCompareContext ctx)
  {
    var op = ctx.operatorComparison();
    var op_exp_left = ctx.exp(0);
    var op_exp_right = ctx.exp(1);
    
    if(op != null)
      AddSemanticToken(op.Start.StartIndex, op.Stop.StopIndex, spec.SemanticTokenTypes.@operator);

    if(op_exp_left != null)
      Visit(op_exp_left);
    
    if(op_exp_right != null)
      Visit(op_exp_right);
    
    return null;
  }
  
  public override object VisitFor(bhlParser.ForContext ctx)
  {
    var for_exp = ctx.forExp();
    var for_block = ctx.block();

    if(for_exp != null)
    {
      AddSemanticToken(ctx.Start.StartIndex, for_exp.Start.StartIndex-1, spec.SemanticTokenTypes.keyword);
      
      var forStmts = for_exp.forPre()?.forStmts()?.forStmt();
      var forCondExp = for_exp.forCond()?.exp();
      var forPostIterStmts = for_exp.forPostIter()?.forStmts()?.forStmt();

      if(forStmts != null)
      {
        foreach(var forStmt in forStmts)
        {
          var varsDeclareOrCallExps = forStmt.varsDeclareOrCallExps();
          if(varsDeclareOrCallExps != null)
          {
            var varDeclareOrCallExp = varsDeclareOrCallExps.varDeclareOrCallExp();
            if(varDeclareOrCallExp != null)
            {
              foreach(var varDeclareOrCallExpItem in varDeclareOrCallExp)
              {
                var varDeclare = varDeclareOrCallExpItem.varDeclare();
                var callExp = varDeclareOrCallExpItem.callExp();

                if(varDeclare != null)
                  Visit(varDeclare);
        
                if(callExp != null)
                  Visit(callExp);
              }
            }
            
            var forStmtAssignExp = forStmt.assignExp().exp();
            if(forStmtAssignExp != null)
              Visit(forStmtAssignExp);
          }
          else
          {
            var callPostOperators = forStmt.callPostIncDec();
            if(callPostOperators != null)
              CommonPostIncDec(callPostOperators);
          }
        }
      }

      if(forCondExp != null)
        Visit(forCondExp);
      
      if(forPostIterStmts != null)
      {
        foreach(var forPostIterStmt in forPostIterStmts)
        {
          var varsDeclareOrCallExps = forPostIterStmt.varsDeclareOrCallExps();
          if(varsDeclareOrCallExps != null)
          {
            var varDeclareOrCallExp = varsDeclareOrCallExps.varDeclareOrCallExp();
            if(varDeclareOrCallExp != null)
            {
              foreach(var varDeclareOrCallExpItem in varDeclareOrCallExp)
              {
                var varDeclare = varDeclareOrCallExpItem.varDeclare();
                var callExp = varDeclareOrCallExpItem.callExp();

                if(varDeclare != null)
                  Visit(varDeclare);
        
                if(callExp != null)
                  Visit(callExp);
              }
            }
            
            var forStmtAssignExp = forPostIterStmt.assignExp().exp();
            if(forStmtAssignExp != null)
              Visit(forStmtAssignExp);
          }
          else
          {
            var callPostOperators = forPostIterStmt.callPostIncDec();
            if(callPostOperators != null)
              CommonPostIncDec(callPostOperators);
          }
        }
      }
    }
    
    if(for_block != null)
      Visit(for_block);
    
    return null;
  }
  
  public override object VisitExpAddSub(bhlParser.ExpAddSubContext ctx)
  {
    var op = ctx.operatorAddSub();
    var op_exp_left = ctx.exp(0);
    var op_exp_right = ctx.exp(1);
    
    if(op != null)
      AddSemanticToken(op.Start.StartIndex, op.Stop.StopIndex, spec.SemanticTokenTypes.@operator);

    if(op_exp_left != null)
      Visit(op_exp_left);
    
    if(op_exp_right != null)
      Visit(op_exp_right);
    
    return null;
  }
  
  public override object VisitWhile(bhlParser.WhileContext ctx)
  {
    var while_exp = ctx.exp();
    var while_block = ctx.block();

    if(while_exp != null)
    {
      AddSemanticToken(ctx.Start.StartIndex, while_exp.Start.StartIndex-2, spec.SemanticTokenTypes.keyword);
      Visit(while_exp);
    }

    if(while_block != null)
      Visit(while_block);
    
    return null;
  }
  
  public override object VisitDeclAssign(bhlParser.DeclAssignContext ctx)
  {
    var var_decl_or_call = ctx.varsDeclareAssign().varsDeclareOrCallExps()?.varDeclareOrCallExp();
    var assign_exp = ctx.varsDeclareAssign().assignExp()?.exp();

    if(var_decl_or_call != null)
    {
      foreach(var item in var_decl_or_call)
      {
        var var_decl = item.varDeclare();
        var call_exp = item.callExp();

        if(var_decl != null)
          Visit(var_decl);
        
        if(call_exp != null)
          Visit(call_exp);
      }
    }
    
    if(assign_exp != null)
      Visit(assign_exp);
    
    return null;
  }
  
  public override object VisitSymbCall(bhlParser.SymbCallContext ctx)
  {
    Visit(ctx.callExp());
    return null;
  }
  
  public override object VisitExpCall(bhlParser.ExpCallContext ctx)
  {
    Visit(ctx.callExp());
    return null;
  }
  
  public override object VisitIf(bhlParser.IfContext ctx)
  {
    var mainIf = ctx.mainIf();
    var elseIf = ctx.elseIf();
    var @else = ctx.@else();

    if(mainIf != null)
    {
      var mainIfExp = mainIf.exp();
      var mainIfBlock = mainIf.block();
      
      if(mainIfExp != null)
      {
        AddSemanticToken(mainIf.Start.StartIndex, mainIfExp.Start.StartIndex-2, spec.SemanticTokenTypes.keyword);
        Visit(mainIfExp);
      }

      if(mainIfBlock != null)
        Visit(mainIfBlock);
    }

    if(elseIf != null)
    {
      foreach(var elseIfItem in elseIf)
      {
        var elseIfItemExp = elseIfItem.exp();
        AddSemanticToken(elseIfItem.Start.StartIndex, elseIfItemExp.Start.StartIndex-2, spec.SemanticTokenTypes.keyword);
        Visit(elseIfItemExp);

        var elseIfItemBlock = elseIfItem.block();
        if(elseIfItemBlock != null)
          Visit(elseIfItemBlock);
      }
    }

    if(@else != null)
    {
      var elseBlock = @else.block();
      if(elseBlock != null)
      {
        AddSemanticToken(@else.Start.StartIndex, elseBlock.Start.StartIndex - 1, spec.SemanticTokenTypes.keyword);
        Visit(elseBlock);
      }
    }
    
    return null;
  }
  
  public override object VisitVarDecl(bhlParser.VarDeclContext ctx)
  {
    VisitVarDeclare(ctx.varDeclare());
    return null;
  }

  public override object VisitVarDeclare(bhlParser.VarDeclareContext ctx)
  {
    var var_decl_type = ctx.type();
    var var_decl_name = ctx.NAME();
      
    if(var_decl_type != null)
      Visit(var_decl_type);
      
    if(var_decl_name != null)
      AddSemanticToken(var_decl_name, spec.SemanticTokenTypes.variable);
    
    return null;
  }
  
  public override object VisitFuncParamDeclare(bhlParser.FuncParamDeclareContext ctx)
  {
    var isRef = ctx.isRef();
    var type = ctx.type();
    var name = ctx.NAME();
    var assignExp = ctx.assignExp();
    
    if(isRef != null)
    {
      var refStopIdx = type?.Start.StartIndex ?? (name?.Symbol.StartIndex ?? 0);
      AddSemanticToken(ctx.Start.StartIndex, refStopIdx - 1, spec.SemanticTokenTypes.keyword);
    }

    if(type != null)
      Visit(type);

    AddSemanticToken(name, spec.SemanticTokenTypes.parameter);
    
    if(assignExp != null)
      Visit(assignExp.exp());
    
    return null;
  }
  
  public override object VisitExpLiteralNull(bhlParser.ExpLiteralNullContext ctx)
  {
    AddSemanticToken(ctx.Start.StartIndex, ctx.Stop.StopIndex, spec.SemanticTokenTypes.keyword);
    return null;
  }
  
  public override object VisitExpLiteralFalse(bhlParser.ExpLiteralFalseContext ctx)
  {
    AddSemanticToken(ctx.Start.StartIndex, ctx.Stop.StopIndex, spec.SemanticTokenTypes.keyword);
    return null;
  }
  
  public override object VisitExpLiteralTrue(bhlParser.ExpLiteralTrueContext ctx)
  {
    AddSemanticToken(ctx.Start.StartIndex, ctx.Stop.StopIndex, spec.SemanticTokenTypes.keyword);
    return null;
  }
  
  public override object VisitExpLiteralNum(bhlParser.ExpLiteralNumContext ctx)
  {
    AddSemanticToken(ctx.Start.StartIndex, ctx.Stop.StopIndex, spec.SemanticTokenTypes.number);
    return null;
  }
  
  public override object VisitExpUnary(bhlParser.ExpUnaryContext ctx)
  {
    var op = ctx.operatorUnary();
    AddSemanticToken(op.Start.StartIndex, op.Stop.StopIndex, spec.SemanticTokenTypes.@operator);
    Visit(ctx.exp());
    return null;
  }
  
  public override object VisitExpLiteralStr(bhlParser.ExpLiteralStrContext ctx)
  {
    AddSemanticToken(ctx.Start.StartIndex, ctx.Stop.StopIndex, spec.SemanticTokenTypes.@string);
    return null;
  }
  
  public override object VisitExpJsonObj(bhlParser.ExpJsonObjContext ctx)
  {
    Visit(ctx.jsonObject());
    return null;
  }
  
  public override object VisitJsonObject(bhlParser.JsonObjectContext ctx)
  {
    var newExp = ctx.newExp();
    if(newExp != null)
      VisitNewExp(newExp);

    var jsonPair = ctx.jsonPair();
    if(jsonPair != null)
    {
      foreach(var item in jsonPair)
      {
        var item_name = item.NAME();
        var item_value = item.jsonValue();
        
        if(item_name != null)
          AddSemanticToken(item_name, spec.SemanticTokenTypes.variable);
        
        if(item_value != null)
          Visit(item_value.exp());
      }
    }
    
    return null;
  }
  
  public override object VisitNewExp(bhlParser.NewExpContext ctx)
  {
    var exp_type = ctx.type();
      
    AddSemanticToken(ctx.Start.StartIndex, exp_type.Start.StartIndex - 1, spec.SemanticTokenTypes.keyword);
    Visit(exp_type);
    
    return null;
  }
  
  public override object VisitCallExp(bhlParser.CallExpContext ctx)
  {
    var name = ctx.NAME();
    var exp = ctx.chainExp();
    
    if(exp != null)
    {
      foreach(var item in exp)
      {
        if(item.callArgs() is bhlParser.CallArgsContext callArgs)
        {
          if(name != null)
          {
            AddSemanticToken(name, spec.SemanticTokenTypes.function);
            name = null;
          }

          Visit(callArgs);
        }
        else if(item.memberAccess() is bhlParser.MemberAccessContext memberAccess)
        {
          if(name != null)
            AddSemanticToken(name, spec.SemanticTokenTypes.variable);
          
          name = memberAccess.NAME();

          Visit(memberAccess);
        }
        else if(item.arrAccess() is bhlParser.ArrAccessContext arrAccess)
        {
          if(name != null)
            AddSemanticToken(name, spec.SemanticTokenTypes.variable);
          
          name = null;

          Visit(arrAccess);
        }
      }
      
      if(name != null)
        AddSemanticToken(name, spec.SemanticTokenTypes.variable);
    }
    return null;
  }

  public override object VisitArrAccess(bhlParser.ArrAccessContext ctx)
  {
    return null;
  }
  
  public override object VisitMemberAccess(bhlParser.MemberAccessContext ctx)
  {
    return null;
  }
  
  public override object VisitCallArgs(bhlParser.CallArgsContext ctx)
  {
    foreach(var item in ctx.callArg())
    {
      var arg_name = item.NAME();
      if(arg_name != null)
        AddSemanticToken(arg_name, spec.SemanticTokenTypes.parameter);
            
      var arg_is_ref = item.isRef();
      if(arg_is_ref != null)
        AddSemanticToken(arg_is_ref.Start.StartIndex, arg_is_ref.Stop.StopIndex, spec.SemanticTokenTypes.keyword);

      var arg_exp = item.exp();
      if(arg_exp != null)
        Visit(item.exp());
    }
    return null;
  }
  
  public override object VisitVarDeclareAssign(bhlParser.VarDeclareAssignContext ctx)
  {
    var var_decl = ctx.varDeclare();
    var assign_exp = ctx.assignExp();
    
    if(var_decl != null)
      Visit(var_decl);

    if(assign_exp != null)
      Visit(assign_exp.exp());
    
    return null;
  }
  
  public override object VisitImports(bhlParser.ImportsContext ctx)
  {
    foreach(var mimport in ctx.mimport())
    {
      var normalstring = mimport.NORMALSTRING();
      
      var import = normalstring.GetText();
      import = import.Substring(1, import.Length-2); // removing quotes
      imports.Add(import);
      
      AddSemanticToken(mimport.Start.StartIndex, normalstring.Symbol.StartIndex - 1, spec.SemanticTokenTypes.keyword);
      AddSemanticToken(normalstring, spec.SemanticTokenTypes.@string);
    }
    
    return null;
  }

  public override object VisitExpTypeof(bhlParser.ExpTypeofContext ctx)
  {
    /*var typeIdType = ctx.typeid()?.type();
    if(typeIdType != null)
    {
      AddSemanticToken(ctx.Start.StartIndex, typeIdType.Start.StartIndex-2, spec.SemanticTokenTypes.keyword);
      Visit(typeIdType);
    }*/
    
    return null;
  }
  
  public override object VisitType(bhlParser.TypeContext ctx)
  {
    //TODO: parse the whole nsName()
    AddSemanticTokenTypeName(ctx.nsName()?.dotName().NAME());

    var fn_type = ctx.funcType();
    if(fn_type != null && fn_type.types() is bhlParser.TypesContext types)
    {
      foreach(var refType in types.refType())
      {
        var refNameIsRef = refType.isRef();
        //TODO: parse the whole nsName()
        var refNameName = refType.type()?.nsName()?.dotName().NAME();
        if(refNameName != null)
        {
          if(refNameIsRef != null)
            AddSemanticToken(refNameIsRef.Start.StartIndex, refNameName.Symbol.StartIndex-1, spec.SemanticTokenTypes.keyword);
        
          AddSemanticTokenTypeName(refNameName);
        }
      }
    }
    return null;
  }
  
  public override object VisitExpTernaryIf(bhlParser.ExpTernaryIfContext ctx)
  {
    var ternary_if = ctx.ternaryIfExp();
    if(ternary_if != null)
    {
      var if_exp = ctx.exp();
      var if_exp_left = ternary_if.exp(0);
      var if_exp_right = ternary_if.exp(1);

      if(if_exp != null)
        Visit(if_exp);
      
      if(if_exp_left != null)
        Visit(if_exp_left);
      
      if(if_exp_right != null)
        Visit(if_exp_right);
    }
    
    return null;
  }
  
  public override object VisitVarPostIncDec(bhlParser.VarPostIncDecContext ctx)
  {
    CommonPostIncDec(ctx.callPostIncDec());
    return null;
  }

  void CommonPostIncDec(bhlParser.CallPostIncDecContext ctx)
  {
    ////TODO: take into account the whole name
    //var callPostOperatorName = ctx.dotName().NAME();
    //if(callPostOperatorName != null)
    //  AddSemanticToken(callPostOperatorName, spec.SemanticTokenTypes.variable);
    //
    //var decrementOperator = ctx.decrementOperator();
    //var incrementOperator = ctx.incrementOperator();
    //
    //if(decrementOperator != null)
    //  AddSemanticToken(decrementOperator.Start.StartIndex, decrementOperator.Stop.StopIndex, spec.SemanticTokenTypes.@operator);
    //
    //if(incrementOperator != null)
    //  AddSemanticToken(incrementOperator.Start.StartIndex, incrementOperator.Stop.StopIndex, spec.SemanticTokenTypes.@operator);
  }

  public override object VisitContinue(bhlParser.ContinueContext ctx)
  {
    AddSemanticToken(ctx.Start.StartIndex, ctx.Stop.StopIndex, spec.SemanticTokenTypes.keyword);
    return null;
  }
  
  bool IsTypeKeyword(string typeName)
  {
    return Types.Int.name    == typeName ||
           Types.Float.name  == typeName ||
           Types.String.name == typeName ||
           Types.Bool.name   == typeName ||
           Types.Any.name    == typeName ||
           Types.Null.name   == typeName ||
           Types.Void.name   == typeName;
  }

  private void AddSemanticTokenTypeName(ITerminalNode node)
  {
    if(node == null)
      return;
    
    if(IsTypeKeyword(node.GetText()))
      AddSemanticToken(node, spec.SemanticTokenTypes.keyword);
    else
      AddSemanticToken(node, spec.SemanticTokenTypes.type);
  }
  
  void AddSemanticToken(ITerminalNode node, string tokenType, params string[] tokenModifiers)
  {
    if(node == null)
      return;
    
    AddSemanticToken(node.Symbol.StartIndex, node.Symbol.StopIndex, tokenType, tokenModifiers);
  }

  void AddSemanticToken(int start_idx, int stop_idx, string token_type, params string[] token_modifiers)
  {
    if(start_idx < 0 || stop_idx < 0)
      return;
    
    if(string.IsNullOrEmpty(token_type))
      return;
  
    var tidx = Array.IndexOf(BHLSemanticTokens.token_types, token_type);
    if(tidx < 0)
      return;
    
    var next_start_pos = document.Code.GetIndexPosition(next_idx);
    var line_column_symb_pos = document.Code.GetIndexPosition(start_idx);

    var diff_line = line_column_symb_pos.line - next_start_pos.line;
    var diff_column = diff_line != 0 ? line_column_symb_pos.column : line_column_symb_pos.column - next_start_pos.column;

    int bitTokenModifiers = 0;
    for(int i = 0; i < token_modifiers.Length; i++)
    {
      var idx = Array.IndexOf(BHLSemanticTokens.modifiers, token_modifiers[i]);
      bitTokenModifiers |= (int)Math.Pow(2, idx);
    }
    
    // line
    dataSemanticTokens.Add((uint)diff_line);
    // startChar
    dataSemanticTokens.Add((uint)diff_column);
    // length
    dataSemanticTokens.Add((uint)(stop_idx - start_idx + 1));
    // tokenType
    dataSemanticTokens.Add((uint)tidx);
    // tokenModifiers
    dataSemanticTokens.Add((uint)bitTokenModifiers);

    next_idx = start_idx;
  }
}

}
