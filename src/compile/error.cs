using System;
using System.Collections.Generic;
using System.IO;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Antlr4.Runtime.Dfa;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Sharpen;

namespace bhl {

public interface ICompileError
{
  string text { get; }
  string stack_trace { get; }
  string file { get; }
  SourceRange range { get; }
}

public class CompileErrors : List<ICompileError> 
{ 
  public override string ToString() 
  {
    string str = "";
    for(int i=0;i<Count;++i)
      str += "#" + (i+1) + " " + this[i] + "\n";
    return str;
  }

  public void Dump()
  {
    Console.WriteLine(this);
  }
}

public class CompileErrorsException : Exception
{
  public CompileErrors errors;

  public CompileErrorsException(CompileErrors errors)
    : base("Compile errors (" + errors.Count + "):\n" + errors)
  {
    this.errors = errors;
  }

  public void Dump()
  {
    errors.Dump();
  }
}

public class SyntaxError : Exception, ICompileError
{
  public string text { get; }
  public string stack_trace { get { return StackTrace; } }
  public SourceRange range { get; }
  public string file { get; }

  public SyntaxError(string file, SourceRange range, string msg)
    : base(ErrorUtils.MakeMessage(file, range, msg))
  {
    this.text = msg;
    this.range = range;
    this.file = file;
  }
}

public class BuildError : Exception, ICompileError
{
  public string text { get; }
  public string stack_trace { get { return StackTrace; } }
  public SourceRange range { get { return new SourceRange(); } }
  public string file { get; }

  public BuildError(string file, string msg)
    : base(ErrorUtils.MakeMessage(file, new SourceRange(), msg))
  {
    this.text = msg;
    this.file = file;
  }

  public BuildError(string file, Exception inner)
    : base(ErrorUtils.MakeMessage(file, new SourceRange(), inner.Message), inner)
  {
    this.text = inner.Message;
    this.file = file;
  }
}

public class SemanticError : Exception, ICompileError
{
  public string text { get; }
  public string stack_trace { get { return StackTrace; } }

  public SourceRange range { 
    get {
      return new SourceRange(place.SourceInterval, tokens);
    }
  }

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

  public SemanticError(AnnotatedParseTree w, string msg)
    : this(w.module, w.tree, w.tokens, msg)
  {}
}

public class ErrorHandlers
{
  public IAntlrErrorListener<int> lexer_listener;
  public IParserErrorListener parser_listener; 
  public IAntlrErrorStrategy error_strategy;

  static public ErrorHandlers MakeStandard(string file, CompileErrors errors)
  {
    var eh = new ErrorHandlers();
    eh.lexer_listener = new ErrorLexerListener(file, errors); 
    eh.parser_listener = new ErrorParserListener(file, errors);
    eh.error_strategy = new ErrorStrategy();
    return eh;
  }
}

public class ErrorLexerListener : IAntlrErrorListener<int>
{
  CompileErrors errors;
  string file_path;

  public ErrorLexerListener(string file_path, CompileErrors errors)
  {
    this.file_path = file_path;
    this.errors = errors;
  }

  public void SyntaxError(TextWriter tw, IRecognizer recognizer, int offendingSymbol, int line, int char_pos, string msg, RecognitionException e)
  {
    errors.Add(new SyntaxError(file_path, new SourceRange(line, char_pos), msg));
  }
}

public class ErrorStrategy : DefaultErrorStrategy
{
  public override void Sync(Antlr4.Runtime.Parser parser) 
  {
    //NOTE: base sync is 'smart' and removes extra tokens
    base.Sync(parser);
  }
}

public class ErrorParserListener : IParserErrorListener
{
  CompileErrors errors;
  string file_path;

  public ErrorParserListener(string file_path, CompileErrors errors)
  {
    this.file_path = file_path;
    this.errors = errors;
  }

  public void SyntaxError(TextWriter tw, IRecognizer recognizer, IToken offendingSymbol, int line, int char_pos, string msg, RecognitionException e)
  {
    errors.Add(new SyntaxError(file_path, new SourceRange(line, char_pos), msg));
  }

  public void ReportAmbiguity(Antlr4.Runtime.Parser recognizer, DFA dfa, int startIndex, int stopIndex, bool exact, BitSet ambigAlts, ATNConfigSet configs)
  {}
  public void ReportAttemptingFullContext(Antlr4.Runtime.Parser recognizer, DFA dfa, int startIndex, int stopIndex, BitSet conflictingAlts, SimulatorState conflictState)
  {}
  public void ReportContextSensitivity(Antlr4.Runtime.Parser recognizer, DFA dfa, int startIndex, int stopIndex, int prediction, SimulatorState acceptState)
  {}
}

} //namespace bhl
