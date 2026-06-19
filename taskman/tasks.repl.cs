using System;
using System.Collections.Generic;
using System.Text;
using ThreadTask = System.Threading.Tasks.Task;

#pragma warning disable CS8981

namespace bhl.taskman;

public static partial class Tasks
{
  [Task(verbose: false)]
  public static ThreadTask repl(Taskman tm, string[] args)
  {
    var vm = new VM(new Types());
    var session = new ReplSession(vm);
    var history = new List<string>();

    Console.WriteLine("BHL REPL " + Version.Name + " (Ctrl+D or Ctrl+C to exit)");

    while(true)
    {
      string input = ReadInput(history);
      if(input == null)
        break;

      input = input.Trim();
      if(input.Length == 0)
        continue;

      if(history.Count == 0 || history[history.Count - 1] != input)
        history.Add(input);

      try
      {
        var result = session.Eval(input);
        if(result.Length == 1)
          Console.WriteLine(ValToString(result[0]));
        else if(result.Length > 1)
        {
          var sb = new StringBuilder("(");
          for(int i = 0; i < result.Length; ++i)
          {
            if(i > 0) sb.Append(", ");
            sb.Append(ValToString(result[i]));
          }
          sb.Append(")");
          Console.WriteLine(sb.ToString());
        }
      }
      catch(Exception e)
      {
        Console.WriteLine("error: " + e.Message);
      }
    }

    return ThreadTask.CompletedTask;
  }

  // Collects one logical input block, spanning multiple lines when braces are open.
  static string ReadInput(List<string> history)
  {
    var lines = new List<string>();

    while(true)
    {
      Console.Write(lines.Count == 0 ? "> " : "... ");

      string line;
      try { line = ReadLine(history); }
      catch { return null; }

      if(line == null)
        return lines.Count > 0 ? string.Join("\n", lines) : null;

      lines.Add(line);

      if(!HasOpenBraces(string.Join("\n", lines)))
        return string.Join("\n", lines);
    }
  }

  // Returns true when the input contains more '{' than '}' (ignoring strings and // comments).
  static bool HasOpenBraces(string input)
  {
    int depth = 0;
    bool inString = false;

    for(int i = 0; i < input.Length; i++)
    {
      char c = input[i];

      if(inString)
      {
        if(c == '\\' && i + 1 < input.Length) { i++; continue; }
        if(c == '"') inString = false;
        continue;
      }

      if(c == '"') { inString = true; continue; }

      // skip // line comments
      if(c == '/' && i + 1 < input.Length && input[i + 1] == '/')
      {
        while(i < input.Length && input[i] != '\n') i++;
        continue;
      }

      if(c == '{') depth++;
      else if(c == '}') depth--;
    }

    return depth > 0;
  }

  // Reads one line with up/down arrow history navigation.
  // Falls back to Console.ReadLine() when stdin is redirected (non-interactive).
  static string ReadLine(List<string> history)
  {
    if(Console.IsInputRedirected)
      return Console.ReadLine();

    var sb = new StringBuilder();
    int historyPos = history.Count;
    string savedLine = "";

    while(true)
    {
      ConsoleKeyInfo key;
      try { key = Console.ReadKey(intercept: true); }
      catch { return null; }

      switch(key.Key)
      {
        case ConsoleKey.Enter:
          Console.WriteLine();
          return sb.ToString();

        case ConsoleKey.Backspace:
          if(sb.Length > 0)
          {
            sb.Length--;
            Console.Write("\b \b");
          }
          break;

        case ConsoleKey.UpArrow:
          if(historyPos > 0)
          {
            if(historyPos == history.Count)
              savedLine = sb.ToString();
            historyPos--;
            ReplaceConsoleLine(sb, history[historyPos]);
          }
          break;

        case ConsoleKey.DownArrow:
          if(historyPos < history.Count)
          {
            historyPos++;
            ReplaceConsoleLine(sb, historyPos == history.Count ? savedLine : history[historyPos]);
          }
          break;

        default:
          if(key.KeyChar == '\x04') return null; // Ctrl+D
          if(key.KeyChar >= ' ')
          {
            sb.Append(key.KeyChar);
            Console.Write(key.KeyChar);
          }
          break;
      }
    }
  }

  static void ReplaceConsoleLine(StringBuilder sb, string newContent)
  {
    for(int i = 0; i < sb.Length; i++)
      Console.Write("\b \b");
    sb.Clear();
    sb.Append(newContent);
    Console.Write(newContent);
  }

  static string ValToString(Val v)
  {
    if(v.type == Types.Int)    return ((long)v.num).ToString();
    if(v.type == Types.Float)  return v.num.ToString("G");
    if(v.type == Types.Bool)   return v.num != 0 ? "true" : "false";
    if(v.type == Types.String) return v.str;
    if(v.obj != null)          return v.obj.GetType().Name;
    return "null";
  }
}
