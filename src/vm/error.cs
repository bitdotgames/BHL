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
#if BHL_FRONT
  public static string ToJson(ICompileError ie)
  {
    return string.Format(@"{{""error"": ""{0}"", ""file"": ""{1}"", ""line"": {2}, ""column"" : {3}, ""stack_trace"" : ""{4}"" }}", 
      MakeJsonSafe(ie.text),
      ie.file.Replace("\\", "/"),
      ie.line, 
      ie.char_pos,
      MakeJsonSafe(ie.stack_trace)
    );
  }
#endif

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
    if(string.IsNullOrEmpty(msg))
      return "";
    msg = msg.Replace("\\", " ");
    msg = msg.Replace("\n", " ");
    msg = msg.Replace("\r", " ");
    msg = msg.Replace("\"", "\\\""); 
    return msg;
  }

  public static string ShowErrorPlace(string source, int line, int char_pos)
  {
    var lines = source.Split('\n');
    if(line <= lines.Length)
      return lines[line-1] + "\n" + new String('-', char_pos) + "^";
    else
      return "???";
  }
}

public class SymbolError : Exception
#if BHL_FRONT
                           , ICompileError
#endif
{
  public Symbol symbol { get; }

  public string text { get; }

  public string stack_trace { get { return StackTrace; } }

  public int line { 
    get { 
#if BHL_FRONT
      if(symbol.parsed != null)
        return symbol.parsed.line;
#endif
      return 0; 
    } 
  }
  public int char_pos { 
    get { 
#if BHL_FRONT
      if(symbol.parsed != null)
        return symbol.parsed.char_pos;
#endif
      return 0; 
    } 
  }
  public string file { 
    get { 
#if BHL_FRONT
      if(symbol.parsed != null)
        return symbol.parsed.file;
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
