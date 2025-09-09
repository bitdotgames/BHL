using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mono.Options;
using Microsoft.Extensions.Logging;
using bhl.lsp;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using ThreadTask = System.Threading.Tasks.Task;

#pragma warning disable CS8981

namespace bhl
{

public static partial class Tasks
{
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

    var logger_conf = new LoggerConfiguration()
      .Enrich.FromLogContext()
      .MinimumLevel.Verbose();

    if(!string.IsNullOrEmpty(log_file_path))
      logger_conf = logger_conf.WriteTo.File(log_file_path /*, rollingInterval: RollingInterval.Day*/);
    else
      logger_conf = logger_conf.WriteTo.Console();

    Log.Logger = logger_conf.CreateLogger();

    var workspace = new Workspace();

    var cts = new CancellationTokenSource();

    var server = await Server.CreateAsync(
      Log.Logger,
      new LoggingStream(Console.OpenStandardInput(), Log.Logger, "IN:"),
      new LoggingStream(Console.OpenStandardOutput(), Log.Logger, "OUT:"),
      workspace,
      cts.Token
    );

    try
    {
      await server.WaitForExit;
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

}