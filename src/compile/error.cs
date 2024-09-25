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

  public bool FileHasAnyErrors(string file)
  {
    foreach(var item in this)
      if(item.file == file)
        return true;
    return false;
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

  private const int MAX_EXCEPTION_STACK_LEN = 700;

  public BuildError(string file, Exception inner)
    : base(ErrorUtils.MakeMessage(file, new SourceRange(), inner.Message), inner)
  {
    var stack = inner.StackTrace;
    if(stack.Length > MAX_EXCEPTION_STACK_LEN)
      stack = stack.Substring(0, MAX_EXCEPTION_STACK_LEN) + "...";
    this.text = inner.Message + stack;
    this.file = file;
  }
}

public class ParseError : Exception, ICompileError
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

  public ParseError(Module module, IParseTree place, ITokenStream tokens, string msg)
    : base(ErrorUtils.MakeMessage(module, place, tokens, msg))
  {
    this.text = msg;
    this.module = module;
    this.place = place;
    this.tokens = tokens;
  }

  public ParseError(AnnotatedParseTree w, string msg)
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

  public void AttachToParser(Parser p)
  {
    if(parser_listener != null)
    {
      p.RemoveErrorListeners();
      p.AddErrorListener(parser_listener);
    }

    if(error_strategy != null)
      p.ErrorHandler = error_strategy;
  }
}

public class CompileErrorsHub
{
  public CompileErrors errors;
  public ErrorHandlers handlers;

  public CompileErrorsHub(CompileErrors errors, ErrorHandlers handlers)
  {
    this.errors = errors;
    this.handlers = handlers;
  }

  public static CompileErrorsHub MakeEmpty()
  {
    var errs = new CompileErrors();
    return new CompileErrorsHub(
      errs,
      ErrorHandlers.MakeStandard("", errs)
    );
  }

  public static CompileErrorsHub MakeStandard(string file)
  {
     var errs = new CompileErrors();
     return new CompileErrorsHub(
        errs,
        ErrorHandlers.MakeStandard(file, errs)
     );
  }

  public static CompileErrorsHub MakeStandard(string file, CompileErrors errs)
  {
     return new CompileErrorsHub(
        errs,
        ErrorHandlers.MakeStandard(file, errs)
     );
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

  public void ReportAttemptingFullContext(Parser recognizer, DFA dfa, int startIndex, int stopIndex, BitSet conflictingAlts,
    ATNConfigSet configs)
  {}

  public void ReportContextSensitivity(Parser recognizer, DFA dfa, int startIndex, int stopIndex, int prediction,
    ATNConfigSet configs)
  {}
}

}