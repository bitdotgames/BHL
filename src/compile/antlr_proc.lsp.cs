using System;
using System.Collections.Generic;
using Antlr4.Runtime.Tree;

namespace bhl;

public partial class ANTLR_Processor
{
  //NOTE: synchronized with class below
  public enum SemanticToken
  {
    Class    = 0,
    Function = 1,
    Variable = 2,
    Number   = 3,
    String   = 4,
    Type     = 5,
    Keyword  = 6,
    Property = 7,
    Operator = 8,
    Parameter = 9,
  }

  [Flags]
  public enum SemanticModifier
  {
    Declaration = 1,
    Definition  = 2,
    Readonly    = 4,
    Static      = 8,
  }

  public static class SemanticTokens
  {
    public static string[] token_types =
    {
      "class",
      "function",
      "variable",
      "number",
      "string",
      "type",
      "keyword",
      "property",
      "operator",
      "parameter"
    };

    public static string[] modifiers =
    {
      "declaration",   // 1
      "definition",    // 2
      "readonly",      // 4
      "static",        // 8
    };
  }

  public class SemanticTokenNode
  {
    public int idx;
    public int line;
    public int column;
    public int len;
    public SemanticToken type_idx;
    public SemanticModifier mods;
  }

  public List<SemanticTokenNode> semantic_tokens = new List<SemanticTokenNode>();

  List<uint> encoded_semantic_tokens = new List<uint>();

  void LSP_SetSymbol(AnnotatedParseTree ann, Symbol s)
  {
    if(s is VariableSymbol vs && vs._upvalue != null)
    {
      //we need to find the 'most top one'
      var top = vs._upvalue;
      while(top._upvalue != null)
        top = top._upvalue;
      ann.lsp_symbol = top;
    }
    else
      ann.lsp_symbol = s;
  }

  void LSP_SetSymbol(IParseTree t, Symbol s)
  {
    LSP_SetSymbol(Annotate(t), s);
  }

  void LSP_AddSemanticToken(ITerminalNode token, SemanticToken idx, SemanticModifier mods = 0)
  {
    if(token == null)
      return;

    semantic_tokens.Add(new SemanticTokenNode()
    {
      idx = token.Symbol.StartIndex,
      line = token.Symbol.Line,
      column = token.Symbol.Column,
      len = token.Symbol.StopIndex - token.Symbol.StartIndex + 1,
      type_idx = idx,
      mods = mods
    });
    encoded_semantic_tokens.Clear();
  }

  void LSP_AddSemanticToken(IParseTree tree, SemanticToken idx, SemanticModifier mods = 0)
  {
    if(tree == null)
      return;

    var interval = tree.SourceInterval;
    var a = tokens.Get(interval.a);
    var b = tokens.Get(interval.b);

    //let's ignore any invalid stuff
    if(a.Line <= 0 || b.Line <= 0 ||
       a.Column <= 0 || b.Column <= 0)
      return;

    if(a.Line != b.Line)
      throw new Exception("Multiline semantic tokens not supported");

    semantic_tokens.Add(new SemanticTokenNode()
    {
      idx = a.StartIndex,
      line = a.Line,
      column = a.Column,
      len = b.StopIndex - a.StartIndex +  1,
      type_idx = idx,
      mods = mods
    });
  }
}
