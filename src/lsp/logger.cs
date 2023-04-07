using System;
using System.IO;

namespace bhl.lsp {

public interface ILogWriter
{
  void Write(string msg);
}

public class NoLogger : ILogWriter
{
  public void Write(string msg)
  {}
}

public class ConsoleLogger : ILogWriter
{
  public void Write(string msg)
  {
    Console.WriteLine(msg);
  }
}

public class FileLogger : ILogWriter
{
  string file_path;

  public FileLogger(string file_path)
  {
    this.file_path = file_path;
  }

  public void Write(string msg)
  {
    using(StreamWriter w = File.AppendText(file_path))
    {
      w.WriteLine(msg);
    }
  }
}

public class Logger
{
  ILogWriter driver;
  int max_level;

  public Logger(int max_level, ILogWriter driver)
  {
    this.max_level = max_level;
    this.driver = driver;
  }
  
  public void Log(int level, string msg)
  {
    if(level > max_level)
      return;

    driver.Write($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.f")}] {msg}");
  }
}

}
