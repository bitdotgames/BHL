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
    if(e is ISourceError se)
    {
      return string.Format(@"{{""error"": ""{0}"", ""file"": ""{1}"", ""line"": {2}, ""column"" : {3} }}", 
        MakeJsonSafe(se.msg),
        se.file.Replace("\\", "/"),
        se.line, 
        se.char_pos
      );
    }
    else
      return string.Format(@"{{""error"": ""{0}""}}", MakeJsonSafe(e.Message));
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

public interface ISourceError
{
  string msg { get; }
  string file { get; }
  int line { get; }
  int char_pos { get; }
}

#if BHL_FRONT
public class SyntaxError : Exception, ISourceError
{
  public string msg { get; }
  public int line { get; }
  public int char_pos { get; }
  public string file { get; }

  public SyntaxError(string file, int line, int char_pos, string msg)
    : base(MakeFullMessage(line, char_pos, msg))
  {
    this.msg = msg;
    this.line = line;
    this.char_pos = char_pos;
    this.file = file;
  }

  static string MakeFullMessage(int line, int char_pos, string msg)
  {
    return string.Format("@({0},{1}) : {2}", line, char_pos, (msg.Length > 200 ? msg.Substring(0, 100) + "..." + msg.Substring(msg.Length-100) : msg));
  }
}

public class BuildError : Exception, ISourceError
{
  public string msg { get; }
  public int line { get { return 0; } }
  public int char_pos { get { return 0; } }
  public string file { get; }

  public BuildError(string file, string msg)
    : base(msg)
  {
    this.msg = msg;
    this.file = file;
  }

  public BuildError(string file, Exception e)
    : base(e.Message, e)
  {
    this.msg = e.Message;
    this.file = file;
  }
}

public class SemanticError : Exception, ISourceError
{
  public string msg { get; }
  public int line { get { return tokens.Get(place.SourceInterval.a).Line; } }
  public int char_pos { get { return tokens.Get(place.SourceInterval.a).Column; } }
  public string file { get { return module.file_path; } }

  public Module module { get; }
  public IParseTree place { get; }
  public ITokenStream tokens { get; }

  public SemanticError(Module module, IParseTree place, ITokenStream tokens, string msg)
    : base(MakeFullMessage(place, tokens, msg))
  {
    this.msg = msg;
    this.module = module;
    this.place = place;
    this.tokens = tokens;
  }

  public SemanticError(WrappedParseTree w, string msg)
    : this(w.module, w.tree, w.tokens, msg)
  {}

  static string MakeFullMessage(IParseTree place, ITokenStream tokens, string msg)
  {
    var interval = place.SourceInterval;
    var begin = tokens.Get(interval.a);
    return string.Format("@({0},{1}) : {2}", begin.Line, begin.Column, msg);
  }
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

public class SymbolError : Exception
{
  public string msg { get; }
  public Symbol symbol { get; }

  public SymbolError(Symbol symb, string msg)
    : base(MakeFullMessage(symb, msg))
  {
    this.msg = msg;
    this.symbol = symb;
  }

  static string MakeFullMessage(Symbol symb, string msg)
  {
#if BHL_FRONT
    if(symb.parsed != null)
    {
      var interval = symb.parsed.tree.SourceInterval;
      var begin = symb.parsed.tokens.Get(interval.a);
      return string.Format("@({0},{1}) : {2}", begin.Line, begin.Column, msg);
    }
    else
#endif
    return string.Format("@(?,?) : {0}", msg);
  }
}

} //namespace bhl
