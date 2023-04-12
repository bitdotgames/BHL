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
  
  public CodeIndex index { get; private set; } = new CodeIndex();

  public ANTLR_Processor proc { get; private set; }

  List<TerminalNodeImpl> nodes = new List<TerminalNodeImpl>();

  public BHLDocument(Uri uri)
  {
    this.uri = uri;
  }
  
  public void Update(string text, ANTLR_Processor proc)
  {
    this.proc = proc;

    index.Update(text);

    nodes.Clear();
    GetTerminalNodes(proc.parsed.parse_tree, nodes);
  }

  public TerminalNodeImpl FindTerminalNode(SourcePos pos)
  {
    return FindTerminalNode(pos.line, pos.column);
  }

  public TerminalNodeImpl FindTerminalNode(int line, int character)
  {
    return FindTerminalNodeByByteIndex(index.CalcByteIndex(line, character));
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

  public Symbol FindSymbol(SourcePos pos)
  {
    var node = FindTerminalNode(pos);
    if(node == null)
      return null;

    //Console.WriteLine("NODE " + node.GetType().Name + " " + node.GetText() + " " + node.GetHashCode() + "; parent " + node.Parent.GetType().Name + " " + node.Parent.GetText());

    var annotated = proc.FindAnnotated(node.Parent);
    if(annotated == null)
      return null;

    //Console.WriteLine("SYMB " + annotated.lsp_symbol + " " + annotated.lsp_symbol.GetType().Name);

    return annotated.lsp_symbol;
  }

  static T GoUpUntil<T>(TerminalNodeImpl node) where T : class,IParseTree
  {
    IParseTree tmp = node;
    while(tmp.Parent != null)
    {
      if(tmp is T)
        break;
      tmp = tmp.Parent;
    }
    return tmp as T;
  }

  public FuncSymbol FindFuncByCallStatement(TerminalNodeImpl node)
  {
    var ctx = GoUpUntil<bhlParser.ChainExpContext>(node);

    if(ctx != null)
    {
      ////Console.WriteLine("CTX " + ctx.GetText());
      //var chain = new ANTLR_Processor.ExpChain(ctx);
      ////Console.WriteLine("NAME " + chain.name_ctx.GetText());
      //var annotated = proc.FindAnnotated(chain.name_ctx);
      ////Console.WriteLine("SYMB " + annotated.lsp_symbol);
      ////Console.WriteLine("CHAIN" + chain.items.At(chain.items.Count-1).GetText());
      //return annotated?.lsp_symbol as FuncSymbol;
    }

    return null;
  }

  public static void GetTerminalNodes(IParseTree tree, List<TerminalNodeImpl> nodes)
  {
    //Console.WriteLine("TREE " + tree.GetType().Name + " " + tree.GetText());
    if(tree is TerminalNodeImpl tn)
      nodes.Add(tn);

    if(tree is ParserRuleContext rule && rule.children != null)
    {
      for(int i = rule.children.Count; i-- > 0;)
        GetTerminalNodes(rule.children[i], nodes);
    }
  }
}

}
