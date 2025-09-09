using System;
using System.IO;

#if BHL_FRONT
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
#endif

namespace bhl
{

public static class ErrorUtils
{
#if BHL_FRONT
  public static string ToJson(ICompileError ie)
  {
    return string.Format(
      @"{{""error"": ""{0}"", ""file"": ""{1}"", ""line"": {2}, ""column"" : {3}, ""line2"": {4}, ""column2"" : {5}, ""stack_trace"" : ""{6}"" }}",
      MakeJsonSafe(ie.text),
      ie.file.Replace("\\", "/"),
      ie.range.start.line,
      ie.range.start.column,
      ie.range.end.line,
      ie.range.end.column,
      MakeJsonSafe(ie.stack_trace)
    );
  }
#endif

  public static string MakeMessage(string file, SourceRange range, string msg)
  {
    //NOTE: showing only the beginning of range
    return string.Format("{0}@({1},{2}) : {3}", file, range.start.line, range.start.column,
      (msg.Length > 200 ? msg.Substring(0, 100) + "..." + msg.Substring(msg.Length - 100) : msg));
  }

#if BHL_FRONT
  public static string MakeMessage(Module module, IParseTree place, ITokenStream tokens, string msg)
  {
    var range = new SourceRange(place.SourceInterval, tokens);
    return MakeMessage(module.file_path, range, msg);
  }
#endif

  public static string MakeMessage(Symbol symb, string msg)
  {
#if BHL_FRONT
    if(symb?.origin?.parsed != null)
      return MakeMessage(symb.origin.parsed.module, symb.origin.parsed.tree, symb.origin.parsed.tokens, msg);
    else
#endif
      return MakeMessage("", new SourceRange(), msg);
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

  public static string ShowErrorPlace(string source, SourceRange range)
  {
    //TODO: take into account end source pos as well
    int line = range.start.line;
    int char_pos = range.start.column;

    var lines = source.Split('\n');
    if(line > 0 && line <= lines.Length)
    {
      string hint = lines[line - 1];
      //replacing tabs with spaces
      hint = hint.Replace("\t", "    ") + "\n";
      for(int c = 0; c < char_pos; ++c)
        hint += "-";

      hint += "^";
      return hint;
    }
    else
      return "??? @(" + line + ":" + char_pos + ")";
  }

  public static void OutputError(string file, int line, int char_pos, string text)
  {
    if(File.Exists(file))
    {
      Console.Error.WriteLine(ShowErrorPlace(File.ReadAllText(file), new SourceRange(line, char_pos)));
      Console.Error.WriteLine("bhl: " + file + ":" + line + ":" + char_pos + ": " + text);
    }
    else //it can be a generic error non related to any source .bhl file
      Console.Error.WriteLine(text);
  }
}

public class SymbolError : Exception
#if BHL_FRONT
  , ICompileError
#endif
{
  public Symbol symbol { get; }

  public string text { get; }

  public string stack_trace
  {
    get { return StackTrace; }
  }

  public SourceRange range
  {
    get { return symbol.origin?.source_range ?? new SourceRange(); }
  }

  public string file
  {
    get { return symbol.origin?.source_file ?? ""; }
  }

  public SymbolError(Symbol symbol, string text)
    : base(ErrorUtils.MakeMessage(symbol, text))
  {
    this.text = text;
    this.symbol = symbol;
  }
}

}