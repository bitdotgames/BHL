using System;
using System.IO;

namespace bhl.lsp {

public interface ILogger
{
  void Log(int level, string msg);
}

public class ConsoleLogger : ILogger
{
  public void Log(int level, string msg)
  {
    Console.WriteLine($"{DateTime.Now} {msg}");
  }
}

public class FileLogger : ILogger
{
  string file_path;

  public FileLogger(string file_path)
  {
    this.file_path = file_path;
  }

  public void Log(int level, string msg)
  {
    using(StreamWriter w = File.AppendText(file_path))
    {
      w.WriteLine($"{DateTime.Now} {msg}");
    }
  }
}

}
