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

  // Returns true when input has more '{' than '}' (ignoring strings and // comments).
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

  // Reads one line with cursor movement and up/down arrow history navigation.
  // Falls back to Console.ReadLine() when stdin is redirected (non-interactive).
  static string ReadLine(List<string> history)
  {
    if(Console.IsInputRedirected)
      return Console.ReadLine();

    var sb = new StringBuilder();
    int cur = 0;                    // cursor position within sb (0 = before first char)
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
          if(cur > 0)
          {
            sb.Remove(cur - 1, 1);
            cur--;
            Console.Write('\b');
            RedrawTail(sb, cur);
          }
          break;

        case ConsoleKey.Delete:
          if(cur < sb.Length)
          {
            sb.Remove(cur, 1);
            RedrawTail(sb, cur);
          }
          break;

        case ConsoleKey.LeftArrow:
          if(cur > 0) { cur--; Console.Write('\b'); }
          break;

        case ConsoleKey.RightArrow:
          if(cur < sb.Length) Console.Write(sb[cur++]);
          break;

        case ConsoleKey.Home:
          while(cur > 0) { Console.Write('\b'); cur--; }
          break;

        case ConsoleKey.End:
          while(cur < sb.Length) Console.Write(sb[cur++]);
          break;

        case ConsoleKey.UpArrow:
          if(historyPos > 0)
          {
            if(historyPos == history.Count) savedLine = sb.ToString();
            historyPos--;
            cur = SetLine(sb, cur, history[historyPos]);
          }
          break;

        case ConsoleKey.DownArrow:
          if(historyPos < history.Count)
          {
            historyPos++;
            cur = SetLine(sb, cur, historyPos == history.Count ? savedLine : history[historyPos]);
          }
          break;

        default:
          if(key.KeyChar == '\x04') return null; // Ctrl+D
          if(key.KeyChar >= ' ')
          {
            sb.Insert(cur, key.KeyChar);
            cur++;
            // write the new char + tail, then backtrack to cur
            Console.Write(sb.ToString(cur - 1, sb.Length - cur + 1));
            for(int i = sb.Length - cur; i > 0; i--)
              Console.Write('\b');
          }
          break;
      }
    }
  }

  // Rewrites sb[from..end] + a trailing space (clears any deleted character),
  // then moves the terminal cursor back to position 'from'.
  static void RedrawTail(StringBuilder sb, int from)
  {
    Console.Write(sb.ToString(from, sb.Length - from));
    Console.Write(' ');
    for(int i = sb.Length - from + 1; i > 0; i--)
      Console.Write('\b');
  }

  // Replaces the current line content with 'text', returns new cursor pos (end of text).
  static int SetLine(StringBuilder sb, int cur, string text)
  {
    // Move terminal cursor to start of input
    for(int i = 0; i < cur; i++) Console.Write('\b');
    // Overwrite with new text
    Console.Write(text);
    // Erase any characters left over from the old (longer) content
    int extra = sb.Length - text.Length;
    for(int i = 0; i < extra; i++) Console.Write(' ');
    for(int i = 0; i < extra; i++) Console.Write('\b');
    sb.Clear();
    sb.Append(text);
    return text.Length;
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
