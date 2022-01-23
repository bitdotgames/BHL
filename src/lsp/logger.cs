using System;
using System.IO;
using System.Text;
using System.Threading;

namespace bhlsp
{
  public class BHLSPLogTextWriter : TextWriter
  {
    public override Encoding Encoding => throw new NotImplementedException();
    private static readonly string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
    private static readonly ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim();
    private static bool done = false;
    
    public void CleanUpLogFile()
    {
      if (done)
        return;
      
      done = true;
      var log = home + System.IO.Path.DirectorySeparatorChar + ".bhlsplog";
      cacheLock.EnterWriteLock();
      
      try
      {
        File.Delete(log);
        using (StreamWriter w = File.AppendText(log))
        {
          w.WriteLine($"{DateTime.Now} Logging for BHLSPC started");
        }
      }
      finally
      {
        cacheLock.ExitWriteLock();
      }
    }

    public override void Write(string message)
    {
      var log = home + System.IO.Path.DirectorySeparatorChar + ".bhlsplog";
      cacheLock.EnterWriteLock();
      
      try
      {
        using (StreamWriter w = File.AppendText(log))
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
      var log = home + System.IO.Path.DirectorySeparatorChar + ".bhlsplog";
      cacheLock.EnterWriteLock();
      try
      {
        using (StreamWriter w = File.AppendText(log))
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

  public class BHLSPLogger
  {
    public static void WriteLine(object value)
    {
      if (value == null)
        Log.WriteLine();
      else
      {
        IFormattable f = value as IFormattable;
        if (f != null)
          Log.WriteLine(f.ToString(null, Log.FormatProvider));
        else
          Log.WriteLine(value.ToString());
      }
    }

    public static void CleanUpLogFile()
    {
      Log.CleanUpLogFile();
    }
    
    static BHLSPLogTextWriter Log { get; set; } = new BHLSPLogTextWriter();
  }
}