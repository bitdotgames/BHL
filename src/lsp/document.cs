using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace bhl.lsp;

public class BHLDocument
{
  public DocumentUri Uri { get; private set; }

  public CodeIndex Index { get; private set; } = new CodeIndex();

  public ANTLR_Processor Processed { get; private set; }

  List<TerminalNodeImpl> _termNodes = new List<TerminalNodeImpl>();

  public BHLDocument(DocumentUri uri)
  {
    this.Uri = uri;
  }

  public void Update(string text, ANTLR_Processor proc)
  {
    this.Processed = proc;

    Index.Update(text);

    _termNodes.Clear();
    GetTerminalNodes(proc.parsed.parse_tree, _termNodes);
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
    //TODO: use binary search?
    foreach(var node in _termNodes)
    {
      if(node.Symbol.StartIndex <= idx && node.Symbol.StopIndex >= idx)
        return node;
    }

    return null;
  }

  public Symbol FindSymbol(SourcePos pos)
  {
    return FindSymbol(pos.line, pos.column);
  }

  public Symbol FindSymbol(int line, int character)
  {
    var node = FindTerminalNode(line, character);
    if(node == null)
      return null;

    //Logger.current.Log(0, "NODE " + node.GetType().Name + " " + node.GetText() + " " + node.GetHashCode() + "; parent " + node.Parent.GetType().Name + " " + node.Parent.GetText());

    var annotated = Processed.FindAnnotated(node);
    if(annotated == null)
      return null;

    //Logger.current.Log(0, "SYMB " + annotated.lsp_symbol + " " + annotated.lsp_symbol?.GetType().Name);

    return annotated.lsp_symbol;
  }

  static T GoUpUntil<T>(TerminalNodeImpl node) where T : class, IParseTree
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
