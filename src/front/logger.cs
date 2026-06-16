#if (BHL_FRONT || BHL_PARSER)

using System;
using System.IO;


namespace bhl
{

public interface ILog
{
  void Write(DateTime time, int level, string msg);
  void Error(DateTime time, string msg);
}

public class NoLogger : ILog
{
  public void Write(DateTime time, int level, string msg)
  {
  }

  public void Error(DateTime time, string msg)
  {
  }
}

public class ConsoleLogger : ILog
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

public class FileLogger : ILog
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
      w.WriteLine("[LOG " + level + "] [" + time.ToString("yyyy-MM-dd HH:mm:ss.f") + "] " + msg);
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
  ILog writer;
  int max_level; //it's a max allowed level, the lower, the less verbose

  public Logger(int max_level, ILog writer)
  {
    this.max_level = max_level;
    this.writer = writer;
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

#endif
