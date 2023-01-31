using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace bhl.lsp {

public static class BHLSemanticTokens
{
  public static string[] token_types = 
  {
    spec.SemanticTokenTypes.@class,
    spec.SemanticTokenTypes.function,
    spec.SemanticTokenTypes.variable,
    spec.SemanticTokenTypes.number,
    spec.SemanticTokenTypes.@string,
    spec.SemanticTokenTypes.type,
    spec.SemanticTokenTypes.keyword
  };
  
  public static string[] modifiers = 
  {
    spec.SemanticTokenModifiers.declaration,   // 1
    spec.SemanticTokenModifiers.definition,    // 2
    spec.SemanticTokenModifiers.@readonly,     // 4
    spec.SemanticTokenModifiers.@static,       // 8
    spec.SemanticTokenModifiers.deprecated,    // 16
    spec.SemanticTokenModifiers.@abstract,     // 32
    spec.SemanticTokenModifiers.async,         // 64
    spec.SemanticTokenModifiers.modification,  // 128
    spec.SemanticTokenModifiers.documentation, // 256
    spec.SemanticTokenModifiers.defaultLibrary // 512
  };
}

public class BHLDocument
{
  public Uri uri { get; private set; }
  
  public Code code { get; private set; } = new Code();

  public ANTLR_Processor proc { get; private set; }

  List<TerminalNodeImpl> nodes = new List<TerminalNodeImpl>();

  public BHLDocument(Uri uri)
  {
    this.uri = uri;
  }
  
  public void Update(string text, ANTLR_Processor proc)
  {
    this.proc = proc;

    code.Update(text);

    nodes.Clear();
    GetTerminalNodes(proc.parsed.prog, nodes);
  }

  public TerminalNodeImpl FindTerminalNode(Code.Position pos)
  {
    return FindTerminalNode(pos.line, pos.column);
  }

  public TerminalNodeImpl FindTerminalNode(int line, int character)
  {
    return FindTerminalNodeByByteIndex(code.CalcByteIndex(line, character));
  }

  public TerminalNodeImpl FindTerminalNodeByByteIndex(int idx)
  {
    //TODO: use binary search?
    foreach(var node in nodes)
    {
      if(node.Symbol.StartIndex <= idx && node.Symbol.StopIndex >= idx)
        return node;  
    }
    return null;
  }

  public Symbol FindSymbol(Code.Position pos)
  {
    var node = FindTerminalNode(pos);
    if(node == null)
      return null;

    //Console.WriteLine("NODE " + node.GetType().Name + " " + node.GetText() + " " + node.Parent.GetType().Name + " " + node.Parent.GetText());

    var annotated = proc.FindAnnotated(node.Parent);
    if(annotated == null)
      return null;

    //Console.WriteLine("SYMB " + annotated.lsp_symbol + " " + annotated.lsp_symbol.GetType().Name);

    return annotated.lsp_symbol;
  }

  bhlParser GetParser()
  {
    var ais = new AntlrInputStream(code.Text.ToStream());
    var lex = new bhlLexer(ais);
    var tokens = new CommonTokenStream(lex);
    var parser = new bhlParser(tokens);
    
    lex.RemoveErrorListeners();
    parser.RemoveErrorListeners();

    return parser;
  }

  public static void GetTerminalNodes(IParseTree tree, List<TerminalNodeImpl> nodes)
  {
    //Console.WriteLine("TREE " + tree.GetType().Name + " " + tree.GetText());
    if(tree is TerminalNodeImpl tn)
      nodes.Add(tn);

    if(tree is ParserRuleContext rule)
    {
      for(int i = rule.children.Count; i-- > 0;)
        GetTerminalNodes(rule.children[i], nodes);
    }
  }
}

}
