using System;
using System.IO;
using System.Text;
using System.Threading;

namespace bhl.lsp {

public class LogTextWriter : TextWriter
{
  public override Encoding Encoding => throw new NotImplementedException();
  static readonly string home = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/tmp/";
  static readonly ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim();
  static bool done = false;
  
  public void CleanUpLogFile()
  {
    if(done)
      return;
    
    done = true;
    var log = home + Path.DirectorySeparatorChar + ".bhlsplog";
    cacheLock.EnterWriteLock();
    
    try
    {
      File.Delete(log);
      using(StreamWriter w = File.AppendText(log))
      {
        w.WriteLine($"{DateTime.Now} Logging for LSP started");
      }
    }
    finally
    {
      cacheLock.ExitWriteLock();
    }
  }

  public override void Write(string message)
  {
    var log = home + Path.DirectorySeparatorChar + ".bhlsplog";
    cacheLock.EnterWriteLock();
    
    try
    {
      using(StreamWriter w = File.AppendText(log))
      {
        w.Write(message);
      }
    }
    finally
    {
      cacheLock.ExitWriteLock();
    }
  }

  public override void WriteLine(string message)
  {
    var log = home + Path.DirectorySeparatorChar + ".bhlsplog";
    cacheLock.EnterWriteLock();
    try
    {
      using(StreamWriter w = File.AppendText(log))
      {
        w.WriteLine($"{DateTime.Now} {message}");
      }
    }
    finally
    {
      cacheLock.ExitWriteLock();
    }
  }
}

public class Logger
{
  static LogTextWriter Log { get; set; } = new LogTextWriter();

  public static void WriteLine(object value)
  {
    if(value == null)
      Log.WriteLine();
    else
      Log.WriteLine(value is IFormattable f ? f.ToString(null, Log.FormatProvider) : value.ToString());
  }

  public static void CleanUpLogFile()
  {
    Log.CleanUpLogFile();
  }
}

}
