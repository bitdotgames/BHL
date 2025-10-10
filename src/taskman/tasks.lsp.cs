using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mono.Options;
using Serilog;
using bhl.lsp;
using ThreadTask = System.Threading.Tasks.Task;

#pragma warning disable CS8981

namespace bhl.taskman;

public static partial class Tasks
{
  public static CancellationTokenSource CreateShutdownTokenSource()
  {
    var cts = new CancellationTokenSource();

    // Handle Ctrl+C (SIGINT)
    Console.CancelKeyPress += (sender, e) =>
    {
      e.Cancel = true; // prevent immediate termination
      Log.Logger.Debug("SIGINT received, shutting down...");
      cts.Cancel();
    };

    // Handle SIGTERM / process exit
    AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
    {
      Log.Logger.Debug("SIGTERM / ProcessExit received, shutting down...");
      cts.Cancel();
    };

    return cts;
  }

  [Task]
  public static async ThreadTask lsp(Taskman tm, string[] args)
  {
    string log_file_path = "";

    var p = new OptionSet
    {
      {
        "log-file=", "log file path",
        v => log_file_path = v
      }
    };

    p.Parse(args);

    var cts = CreateShutdownTokenSource();

    var logger_conf = new LoggerConfiguration()
      .Enrich.FromLogContext()
      .MinimumLevel.Verbose();

    if(!string.IsNullOrEmpty(log_file_path))
      logger_conf = logger_conf.WriteTo.File(log_file_path /*, rollingInterval: RollingInterval.Day*/);
    else
      logger_conf = logger_conf.WriteTo.Console();

    Log.Logger = logger_conf.CreateLogger();

    Console.OutputEncoding = new UTF8Encoding();

    try
    {
      var server = await bhl.lsp.ServerFactory.CreateAsync(
        Log.Logger,
        new LoggingStream(new ErrorWatchableStream(Console.OpenStandardInput(), (e) => cts.Cancel()), Log.Logger, "IN:"),
        new LoggingStream(new ErrorWatchableStream(Console.OpenStandardOutput(), (e) => cts.Cancel()), Log.Logger, "OUT:"),
        new Types(),
        new Workspace(),
        cts.Token
      );

      await Task.WhenAny(server.WaitForExit, Task.Delay(Timeout.Infinite, cts.Token));
    }
    catch(OperationCanceledException e)
    {
      Environment.Exit(0);
    }
    catch (Exception e)
    {
      Log.Logger.Error(e.Message);
      Environment.Exit(-1);
    }
  }
}

public class LoggingStream : Stream
{
  private readonly Stream _inner;
  private readonly Serilog.ILogger _logger;
  private readonly string _prefix;

  public LoggingStream(Stream inner, Serilog.ILogger logger, string prefix)
  {
    _inner = inner;
    _logger = logger;
    _prefix = prefix;
  }

  public override void Write(byte[] buffer, int offset, int count)
  {
    var text = Encoding.UTF8.GetString(buffer, offset, count);
    _logger.Verbose("{Prefix} {Payload}", _prefix, text);
    _inner.Write(buffer, offset, count);
  }

  public override int Read(byte[] buffer, int offset, int count)
  {
    int read = _inner.Read(buffer, offset, count);
    if (read > 0)
    {
      var text = Encoding.UTF8.GetString(buffer, offset, read);
      _logger.Debug("{Prefix} {Payload}", _prefix, text);
    }

    return read;
  }

  public override bool CanRead => _inner.CanRead;
  public override bool CanSeek => _inner.CanSeek;
  public override bool CanWrite => _inner.CanWrite;
  public override long Length => _inner.Length;

  public override long Position
  {
    get => _inner.Position;
    set => _inner.Position = value;
  }

  public override void Flush() => _inner.Flush();
  public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
  public override void SetLength(long value) => _inner.SetLength(value);
}

class ErrorWatchableStream : Stream
{
  private readonly Stream _inner;
  private readonly Action<Exception?> _onException;
  private bool _notified;

  public ErrorWatchableStream(Stream inner, Action<Exception?> onException)
  {
    _inner = inner;
    _onException = onException;
  }

  private void Notify(Exception? ex = null)
  {
    if (_notified)
      return;
    _notified = true;
    try
    {
      _onException(ex);
    }
    catch
    {
      // ignore handler exceptions
    }
  }

  public override int Read(byte[] buffer, int offset, int count)
  {
    try
    {
      return _inner.Read(buffer, offset, count);
    }
    catch (Exception ex)
    {
      Notify(ex);
      throw;
    }
  }

  public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
  {
    try
    {
      return await _inner.ReadAsync(buffer, cancellationToken);
    }
    catch (Exception ex)
    {
      Notify(ex);
      throw;
    }
  }

  public override void Write(byte[] buffer, int offset, int count)
  {
    try
    {
      _inner.Write(buffer, offset, count);
    }
    catch (Exception ex)
    {
      Notify(ex);
      throw;
    }
  }

  public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
  {
    try
    {
      await _inner.WriteAsync(buffer, cancellationToken);
    }
    catch (Exception ex)
    {
      Notify(ex);
      throw;
    }
  }

  public override void Flush()
  {
    try
    {
      _inner.Flush();
    }
    catch (Exception ex)
    {
      Notify(ex);
      throw;
    }
  }

  public override async Task FlushAsync(CancellationToken cancellationToken)
  {
    try
    {
      await _inner.FlushAsync(cancellationToken);
    }
    catch (Exception ex)
    {
      Notify(ex);
      throw;
    }
  }

  protected override void Dispose(bool disposing)
  {
    if (disposing)
    {
      Notify();
      _inner.Dispose();
    }
    base.Dispose(disposing);
  }

  public override void Close()
  {
    Notify();
    _inner.Close();
    base.Close();
  }

  public override bool CanRead => _inner.CanRead;
  public override bool CanSeek => _inner.CanSeek;
  public override bool CanWrite => _inner.CanWrite;
  public override long Length => _inner.Length;
  public override long Position { get => _inner.Position; set => _inner.Position = value; }
  public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
  public override void SetLength(long value) => _inner.SetLength(value);
}

