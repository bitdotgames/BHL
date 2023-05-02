using System;
using System.IO;

namespace bhl {

public interface ILogWriter
{
  void Write(DateTime time, int level, string msg);
  void Error(DateTime time, string msg);
}

public class NoLogger : ILogWriter
{
  public void Write(DateTime time, int level, string msg) {}
  public void Error(DateTime time, string msg) {}
}

public class ConsoleLogger : ILogWriter
{
  public void Write(DateTime time, int level, string msg)
  {
    Console.WriteLine(msg);
  }

  public void Error(DateTime time, string msg)
  {
    Console.Error.WriteLine(msg);
  }
}

public class FileLogger : ILogWriter
{
  string file_path;

  public FileLogger(string file_path)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(file_path));

    this.file_path = file_path;
  }

  public void Write(DateTime time, int level, string msg)
  {
    using(StreamWriter w = File.AppendText(file_path))
    {
      w.WriteLine("[LOG "+level+"] [" + time.ToString("yyyy-MM-dd HH:mm:ss.f") + "] " + msg);
    }
  }

  public void Error(DateTime time, string msg)
  {
    using(StreamWriter w = File.AppendText(file_path))
    {
      w.WriteLine("[ERROR] [" + time.ToString("yyyy-MM-dd HH:mm:ss.f") + "] " + msg);
    }
  }
}

public class Logger
{
  ILogWriter writer;
  int max_level;

  //NOTE: for quick debugging, only in places where 
  //      logger is not passed (and not supposed to)
  public static Logger current { get; private set; }

  public Logger(int max_level, ILogWriter writer)
  {
    this.max_level = max_level;
    this.writer = writer;

    current = this;
  }
  
  public void Log(int level, string msg)
  {
    if(level > max_level)
      return;

    writer.Write(DateTime.Now, level, msg);
  }

  public void Error(string msg)
  {
    writer.Error(DateTime.Now, msg);
  }
}

}
