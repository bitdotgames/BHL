using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace bhl.lsp {

public class Code
{
  public struct Position
  {
    public int line;
    public int column;

    public Position(int line, int column)
    {
      this.line = line;
      this.column = column;
    }

    public spec.Position ToSpec()
    {
      return new spec.Position {line = (uint)line, character = (uint)column};
    }

    public static implicit operator spec.Position(Position pos)
    {
      return pos.ToSpec();
    }
  }

  public string Text { get; private set; }

  List<int> line2byte_offset = new List<int>();

  public void Update(string text)
  {
    this.Text = text;

    line2byte_offset.Clear();
    
    if(text.Length > 0)
      line2byte_offset.Add(0);

    for(int i = 0; i < text.Length; i++)
    {
      if(text[i] == '\r')
      {
        if(i + 1 < text.Length && text[i + 1] == '\n')
          i++;
        line2byte_offset.Add(i + 1);
      }
      else if(text[i] == '\n')
        line2byte_offset.Add(i + 1);
    }
  }

  public int CalcByteIndex(int line, int column)
  {
    if(line2byte_offset.Count > 0 && line < line2byte_offset.Count)
      return line2byte_offset[line] + column;

    return -1;
  }
  
  public int CalcByteIndex(int line)
  {
    if(line2byte_offset.Count > 0 && line < line2byte_offset.Count)
      return line2byte_offset[line];

    return -1;
  }
  
  public Position GetIndexPosition(int index)
  {
    if(index >= Text.Length)
      return new Position(-1, -1); 

    // Binary search.
    int low = 0;
    int high = line2byte_offset.Count - 1;
    int i = 0;
    
    while(low <= high)
    {
      i = (low + high) / 2;
      var v = line2byte_offset[i];
      if(v < index) 
        low = i + 1;
      else if(v > index) 
        high = i - 1;
      else 
        break;
    }
    
    var min = low <= high ? i : high;
    return new Position(min, index - line2byte_offset[min]);
  }
}

public static class Util
{
  public static IEnumerable<IParseTree> IterateNodes(IParseTree root)
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
  
  public static List<spec.ParameterInformation> GetInfoParams(bhlParser.FuncDeclContext funcDecl)
  {
    var fn_params = new List<spec.ParameterInformation>();

    if(funcDecl.funcParams() is bhlParser.FuncParamsContext funcParams)
    {
      var fn_param_decls = funcParams.funcParamDeclare();
      for (int k = 0; k < fn_param_decls.Length; k++)
      {
        var fpd = fn_param_decls[k];
        if(fpd.exception != null)
          continue;

        var fpdl = $"{(fpd.isRef() != null ? "ref " : "")}{fpd.type().nsName().GetText()} {fpd.NAME().GetText()}";
        if(fpd.assignExp() is bhlParser.AssignExpContext assignExp)
          fpdl += assignExp.GetText();
        
        fn_params.Add(new spec.ParameterInformation
        {
          label = fpdl,
          documentation = ""
        });
      }
    }
    
    return fn_params;
  }
}

}
