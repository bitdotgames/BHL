using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace bhl;

public class ANTLR_Parsed
{
  public Parser parser { get; private set; }
  public ITokenStream tokens { get; private set; }
  public IParseTree parse_tree { get; private set; }

  public ANTLR_Parsed(Parser parser, IParseTree root)
  {
    this.parser = parser;
    this.tokens = parser.TokenStream;
    parse_tree = root;
  }

  public override string ToString()
  {
    return PrintTree(parser, parse_tree);
  }

  public static string PrintTree(Parser parser, IParseTree root)
  {
    var sb = new System.Text.StringBuilder();
    PrintTree(root, sb, 0, parser.RuleNames);
    return sb.ToString();
  }

  public static void PrintTree(IParseTree root, System.Text.StringBuilder sb, int offset, IList<String> rule_names)
  {
    for(int i = 0; i < offset; i++)
      sb.Append("  ");

    sb.Append(Trees.GetNodeText(root, rule_names)).Append(" (" + root.GetType().Name + ")").Append("\n");
    if(root is ParserRuleContext prc)
    {
      if(prc.children != null)
      {
        foreach(var child in prc.children)
        {
          if(child is IErrorNode)
            sb.Append("!");
          PrintTree(child, sb, offset + 1, rule_names);
        }
      }
    }
  }
}

public class AnnotatedParseTree
{
  public IParseTree tree;
  public Module module;
  public ITokenStream tokens;
  public IType eval_type;
  public Symbol lsp_symbol;

  public SourceRange range
  {
    get { return new SourceRange(tree.SourceInterval, tokens); }
  }

  public string file
  {
    get { return module.file_path; }
  }
}

