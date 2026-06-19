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
      Console.Write("> ");

      string line;
      try
      {
        line = ReadLine(history);
      }
      catch(Exception)
      {
        break;
      }

      if(line == null)
        break;

      line = line.Trim();
      if(line.Length == 0)
        continue;

      if(history.Count == 0 || history[history.Count - 1] != line)
        history.Add(line);

      try
      {
        var result = session.Eval(line);
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

  // Reads one line with up/down arrow history navigation.
  // Falls back to Console.ReadLine() when stdin is redirected (non-interactive).
  static string ReadLine(List<string> history)
  {
    if(Console.IsInputRedirected)
      return Console.ReadLine();

    var sb = new StringBuilder();
    int historyPos = history.Count; // one past end = the line being typed
    string savedLine = "";          // saves the typed line when the user presses Up

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
          // Ctrl+D (EOT)
          if(key.KeyChar == '\x04')
            return null;
          // Ignore non-printable / special keys (arrows, function keys, etc.)
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
