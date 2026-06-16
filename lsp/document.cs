using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace bhl.lsp;

public class BHLDocument
{
  public DocumentUri Uri { get; private set; }

  public CodeIndex Index { get; private set; } = new CodeIndex();

  public ANTLR_Processor Processed { get; private set; }

  public List<TerminalNodeImpl> TermNodes { get; } = new List<TerminalNodeImpl>();

  public string Text { get; private set; } = "";

  public BHLDocument(DocumentUri uri)
  {
    this.Uri = uri;
  }

  public void Update(string text, ANTLR_Processor proc)
  {
    this.Processed = proc;
    this.Text = text;

    Index.Update(text);

    TermNodes.Clear();
    GetTerminalNodes(proc.parsed.parse_tree, TermNodes);
  }

  public TerminalNodeImpl FindTerminalNode(SourcePos pos)
  {
    return FindTerminalNode(pos.line, pos.column);
  }

  public TerminalNodeImpl FindTerminalNode(int line, int character)
  {
    return FindTerminalNodeByByteIndex(Index.CalcByteIndex(line, character));
  }

  public TerminalNodeImpl FindTerminalNodeByByteIndex(int idx)
  {
    foreach(var node in TermNodes)
    {
      if(node.Symbol.StartIndex > idx)
        break;
      if(node.Symbol.StopIndex >= idx)
        return node;
    }

    return null;
  }

  public Symbol FindSymbol(Position pos)
  {
    return FindSymbol(pos.Line, pos.Character);
  }

  public Symbol FindSymbol(int line, int character)
  {
    var node = FindTerminalNode(line, character);
    if(node == null)
      return null;

    //Log.Logger.Debug("NODE " + node.GetType().Name + " " + node.GetText() + " " + node.GetHashCode() + "; parent " +
    //                 node.Parent.GetType().Name + " " + node.Parent.GetText() + " line: " + line + ", character: " + character);

    var annotated = Processed.FindAnnotated(node);
    if(annotated == null)
      return null;

    //Log.Logger.Debug("SYMB " + annotated.lsp_symbol + " " + annotated.lsp_symbol?.GetType().Name);

    return annotated.lsp_symbol;
  }

  public static void GetTerminalNodes(IParseTree tree, List<TerminalNodeImpl> nodes)
  {
    //Log.Logger.Debug("TREE " + tree.GetType().Name + " " + tree.GetText());
    if(tree is TerminalNodeImpl tn)
      nodes.Add(tn);

    if(tree is ParserRuleContext rule && rule.children != null)
    {
      for(int i = 0; i < rule.children.Count; i++)
        GetTerminalNodes(rule.children[i], nodes);
    }
  }
}
