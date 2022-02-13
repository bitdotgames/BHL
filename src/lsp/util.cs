using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace bhlsp
{
  public static class BHLSPUtil
  {
    public static IEnumerable<IParseTree> DFS(IParseTree root)
    {
      Stack<IParseTree> toVisit = new Stack<IParseTree>();
      Stack<IParseTree> visitedAncestors = new Stack<IParseTree>();
      toVisit.Push(root);
      while(toVisit.Count > 0)
      {
        IParseTree node = toVisit.Peek();
        if(node.ChildCount > 0)
        {
          if(visitedAncestors.Count == 0 || visitedAncestors.Peek() != node)
          {
            visitedAncestors.Push(node);

            if(node as TerminalNodeImpl == null)
            {
              ParserRuleContext internal_node = node as ParserRuleContext;
              int child_count = internal_node.children.Count;
              for(int i = child_count - 1; i >= 0; --i)
              {
                IParseTree o = internal_node.children[i];
                toVisit.Push(o);
              }
              
              continue;
            }
          }
          
          visitedAncestors.Pop();
        }
        
        yield return node;
        
        toVisit.Pop();
      }
    }
    
    public static IEnumerable<BHLTextDocument> ForEachBhlDocuments(BHLTextDocument root = null)
    {
      if(root != null)
      {
        foreach(BHLTextDocument doc in BHLSPWorkspace.self.ForEachBhlImports(root))
        {
          yield return doc;
        }
      }
      
      foreach(var doc in BHLSPWorkspace.self.ForEachDocuments())
      {
        if(doc is BHLTextDocument bhlDocument)
          yield return bhlDocument;
      }
    }
    
    public static List<ParameterInformation> GetInfoParams(bhlParser.FuncDeclContext funcDecl)
    {
      List<ParameterInformation> funcParameters = new List<ParameterInformation>();

      if(funcDecl.funcParams() is bhlParser.FuncParamsContext funcParams)
      {
        var funcParamDeclares = funcParams.funcParamDeclare();
        for (int k = 0; k < funcParamDeclares.Length; k++)
        {
          var fpd = funcParamDeclares[k];
          if(fpd.exception != null)
            continue;

          var fpdl = $"{(fpd.isRef() != null ? "ref " : "")}{fpd.type().NAME().GetText()} {fpd.NAME().GetText()}";
          if(fpd.assignExp() is bhlParser.AssignExpContext assignExp)
            fpdl += assignExp.GetText();
          
          funcParameters.Add(new ParameterInformation
          {
            label = fpdl,
            documentation = ""
          });
        }
      }
      
      return funcParameters;
    }
  }
}