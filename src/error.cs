using System;
using System.IO;

#if BHL_FRONT
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Antlr4.Runtime.Dfa;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Sharpen;
#endif

namespace bhl {

public static class ErrorUtils 
{
  public static string ToJson(Exception e)
  {
    if(e is IError se)
    {
      return string.Format(@"{{""error"": ""{0}"", ""file"": ""{1}"", ""line"": {2}, ""column"" : {3} }}", 
        MakeJsonSafe(se.text),
        se.file.Replace("\\", "/"),
        se.line, 
        se.char_pos
      );
    }
    else
      return string.Format(@"{{""error"": ""{0}""}}", MakeJsonSafe(e.Message));
  }

  public static string MakeMessage(string file, int line, int char_pos, string msg)
  {
    return string.Format("{0}@({1},{2}) : {3}", file, line, char_pos, (msg.Length > 200 ? msg.Substring(0, 100) + "..." + msg.Substring(msg.Length-100) : msg));
  }

#if BHL_FRONT
  public static string MakeMessage(Module module, IParseTree place, ITokenStream tokens, string msg)
  {
    var interval = place.SourceInterval;
    var begin = tokens.Get(interval.a);
    return MakeMessage(module.file_path, begin.Line, begin.Column, msg);
  }
#endif

  public static string MakeMessage(Symbol symb, string msg)
  {
#if BHL_FRONT
    if(symb.parsed != null)
      return MakeMessage(symb.parsed.module, symb.parsed.tree, symb.parsed.tokens, msg);
    else
#endif
    return MakeMessage("", 0, 0, msg);
  }

  static string MakeJsonSafe(string msg)
  {
    msg = msg.Replace("\\", " ");
    msg = msg.Replace("\n", " ");
    msg = msg.Replace("\r", " ");
    msg = msg.Replace("\"", "\\\""); 
    return msg;
  }
}

public interface IError
{
  string text { get; }
  string file { get; }
  int line { get; }
  int char_pos { get; }
}

#if BHL_FRONT
public class SyntaxError : Exception, IError
{
  public string text { get; }
  public int line { get; }
  public int char_pos { get; }
  public string file { get; }

  public SyntaxError(string file, int line, int char_pos, string msg)
    : base(ErrorUtils.MakeMessage(file, line, char_pos, msg))
  {
    this.text = msg;
    this.line = line;
    this.char_pos = char_pos;
    this.file = file;
  }
}

public class BuildError : Exception, IError
{
  public string text { get; }
  public int line { get { return 0; } }
  public int char_pos { get { return 0; } }
  public string file { get; }

  public BuildError(string file, string msg)
    : base(ErrorUtils.MakeMessage(file, 0, 0, msg))
  {
    this.text = msg;
    this.file = file;
  }

  public BuildError(string file, Exception inner)
    : base(ErrorUtils.MakeMessage(file, 0, 0, inner.Message), inner)
  {
    this.text = inner.Message;
    this.file = file;
  }
}

public class SemanticError : Exception, IError
{
  public string text { get; }
  public int line { get { return tokens.Get(place.SourceInterval.a).Line; } }
  public int char_pos { get { return tokens.Get(place.SourceInterval.a).Column; } }
  public string file { get { return module.file_path; } }

  public Module module { get; }
  public IParseTree place { get; }
  public ITokenStream tokens { get; }

  public SemanticError(Module module, IParseTree place, ITokenStream tokens, string msg)
    : base(ErrorUtils.MakeMessage(module, place, tokens, msg))
  {
    this.text = msg;
    this.module = module;
    this.place = place;
    this.tokens = tokens;
  }

  public SemanticError(WrappedParseTree w, string msg)
    : this(w.module, w.tree, w.tokens, msg)
  {}
}

public class ErrorLexerListener : IAntlrErrorListener<int>
{
  string file_path;

  public ErrorLexerListener(string file_path)
  {
    this.file_path = file_path;
  }

  public virtual void SyntaxError(TextWriter tw, IRecognizer recognizer, int offendingSymbol, int line, int char_pos, string msg, RecognitionException e)
  {
    throw new SyntaxError(file_path, line, char_pos, msg);
  }
}

public class ErrorStrategy : DefaultErrorStrategy
{
  public override void Sync(Parser recognizer) {}
}

public class ErrorParserListener : IParserErrorListener
{
  string file_path;

  public ErrorParserListener(string file_path)
  {
    this.file_path = file_path;
  }

  public virtual void SyntaxError(TextWriter tw, IRecognizer recognizer, IToken offendingSymbol, int line, int char_pos, string msg, RecognitionException e)
  {
    throw new SyntaxError(file_path, line, char_pos, msg);
  }

  public virtual void ReportAmbiguity(Parser recognizer, DFA dfa, int startIndex, int stopIndex, bool exact, BitSet ambigAlts, ATNConfigSet configs)
  {}
  public virtual void ReportAttemptingFullContext(Parser recognizer, DFA dfa, int startIndex, int stopIndex, BitSet conflictingAlts, SimulatorState conflictState)
  {}
  public virtual void ReportContextSensitivity(Parser recognizer, DFA dfa, int startIndex, int stopIndex, int prediction, SimulatorState acceptState)
  {}
}

#endif

public class SymbolError : Exception, IError
{
  public Symbol symbol { get; }

  public string text { get; }

  public int line { 
    get { 
#if BHL_FRONT
      if(symbol.parsed != null)
        return symbol.parsed.tokens.Get(symbol.parsed.tree.SourceInterval.a).Line;  
#endif
      return 0; 
    } 
  }
  public int char_pos { 
    get { 
#if BHL_FRONT
      if(symbol.parsed != null)
        return symbol.parsed.tokens.Get(symbol.parsed.tree.SourceInterval.a).Column;  
#endif
      return 0; 
    } 
  }
  public string file { 
    get { 
#if BHL_FRONT
      if(symbol.parsed != null)
        return symbol.parsed.module.file_path;
#endif
      return ""; 
    } 
  }

  public SymbolError(Symbol symbol, string text)
    : base(ErrorUtils.MakeMessage(symbol, text))
  {
    this.text = text;
    this.symbol = symbol;
  }
}

} //namespace bhl
