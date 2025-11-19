using System;
using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;
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

public partial class ANTLR_Processor
{
  static CommonTokenStream Stream2Tokens(Stream s, ErrorHandlers handlers)
  {
    var lex = new bhlLexer(new AntlrInputStream(s));

    if(handlers?.lexer_listener != null)
    {
      lex.RemoveErrorListeners();
      lex.AddErrorListener(handlers.lexer_listener);
    }

    return new CommonTokenStream(lex);
  }

  public static bhlParser Stream2Parser(
    Module module,
    CompileErrorsHub err_hub,
    Stream src,
    //can be null
    HashSet<string> defines,
    out ANTLR_Parsed preproc_parsed,
    out CommonTokenStream tokens
  )
  {
    src = ANTLR_Preprocessor.ProcessStream(
      module,
      err_hub,
      src,
      defines,
      out preproc_parsed
    );

    tokens = Stream2Tokens(src, err_hub.handlers);

    var p = new bhlParser(tokens);

    err_hub.handlers?.AttachToParser(p);

    return p;
  }

  public static ANTLR_Parsed Parse(
    Module module,
    CompileErrorsHub err_hub,
    //can be null
    HashSet<string> defines,
    out ANTLR_Parsed preproc_parsed
  )
  {
    using(var sfs = File.OpenRead(module.file_path))
    {
      return Parse(
        module,
        sfs,
        err_hub,
        defines,
        preproc_parsed: out preproc_parsed
      );
    }
  }

  public static ANTLR_Parsed Parse(
    Module module,
    Stream src,
    CompileErrorsHub err_hub,
    //can be null
    HashSet<string> defines,
    out ANTLR_Parsed preproc_parsed
  )
  {
    var parser = Stream2Parser(
      module,
      err_hub,
      src,
      defines,
      out preproc_parsed,
      out var tokens
    );

    //NOTE: parsing happens here
    var parsed = new ANTLR_Parsed(parser, ParseFastWithFallback(tokens, parser));
    return parsed;
  }

  public static ANTLR_Processor ParseAndMakeProcessor(
    Module module,
    FileImports imports_maybe,
    Stream src,
    Types ts,
    CompileErrorsHub err_hub,
    //can be null
    HashSet<string> defines,
    out ANTLR_Parsed preproc_parsed
  )
  {
    var parsed = Parse(
      module,
      src,
      err_hub,
      defines,
      out preproc_parsed
    );

    return new ANTLR_Processor(
      parsed,
      module,
      imports_maybe,
      ts,
      err_hub.errors
    );
  }

  public static bhlParser.ProgramContext ParseFastWithFallback(CommonTokenStream tokens, bhlParser parser)
  {
    var orig_mode = parser.Interpreter.PredictionMode;
    var orig_err_listeners = parser.ErrorListeners;
    var orig_err_handler = parser.ErrorHandler;

    parser.Interpreter.PredictionMode = PredictionMode.SLL;
    parser.RemoveErrorListeners();
    parser.ErrorHandler = new BailErrorStrategy();

    try
    {
      return parser.program();
    }
    catch(ParseCanceledException)
    {
      tokens.Reset();
      parser.Reset();
      foreach(var el in orig_err_listeners)
        parser.AddErrorListener(el);
      parser.ErrorHandler = orig_err_handler;
      parser.Interpreter.PredictionMode = orig_mode;
      return parser.program();
    }
  }

}
