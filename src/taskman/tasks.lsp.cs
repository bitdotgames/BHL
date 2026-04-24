using System;
using System.IO;
using System.Runtime.InteropServices;
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

  static Stream OpenStdinStream()  => Console.OpenStandardInput();
  static Stream OpenStdoutStream() => Console.OpenStandardOutput();

  [Task(verbose: false)]
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

    var silent = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BHL_SILENT"));

    if(!string.IsNullOrEmpty(log_file_path))
      logger_conf = logger_conf.WriteTo.File(log_file_path /*, rollingInterval: RollingInterval.Day*/);
    else if(!silent)
      logger_conf = logger_conf.WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose);

    if(!silent || !string.IsNullOrEmpty(log_file_path))
      Log.Logger = logger_conf.CreateLogger();

    Stream stdin = OpenStdinStream();
    Stream stdout = OpenStdoutStream();

    StdioMonitor.StartWatcher(() =>
    {
      Log.Logger.Error("IO closed, exiting");
      Environment.Exit(0);
    }, cancellationToken: cts.Token);

    try
    {
      var server = await bhl.lsp.ServerFactory.CreateAsync(
        Log.Logger,
        new LoggingStream(stdin, Log.Logger, "IN:"),
        new LoggingStream(stdout, Log.Logger, "OUT:"),
        new Types(),
        new Workspace(),
        cts.Token
      );

      await Task.WhenAny(server.WaitForExit, Task.Delay(Timeout.Infinite, cts.Token));
    }
    catch(OperationCanceledException)
    {
      Environment.Exit(0);
    }
    catch (Exception e)
    {
      Log.Logger.Error(e, "Unhandled exception");
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
    if(read > 0)
    {
      var text = Encoding.UTF8.GetString(buffer, offset, read);
      _logger.Debug("{Prefix} {Payload}", _prefix, text);
    }
    return read;
  }

  public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    => Task.Run(() => Read(buffer, offset, count), cancellationToken);

  public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
  {
    return new ValueTask<int>(Task.Run(() =>
    {
      if(System.Runtime.InteropServices.MemoryMarshal.TryGetArray<byte>(buffer, out var seg))
        return Read(seg.Array, seg.Offset, seg.Count);
      var tmp = new byte[buffer.Length];
      int n = Read(tmp, 0, tmp.Length);
      tmp.AsMemory(0, n).CopyTo(buffer);
      return n;
    }, cancellationToken));
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

public static class StdioMonitor
{
  /// <summary>
  /// Starts a background task that periodically checks whether stdin or stdout
  /// has been closed by the client (e.g. Neovim).
  /// When detected, invokes <paramref name="onDisconnected"/>.
  /// </summary>
  public static void StartWatcher(Action onDisconnected, int pollIntervalMs = 500,
    CancellationToken? cancellationToken = null)
  {
    var token = cancellationToken ?? CancellationToken.None;

    new Thread(() =>
    {
      while (!token.IsCancellationRequested)
      {
        try
        {
          if (IsInputClosed() || IsOutputClosed())
          {
            onDisconnected();
            return;
          }
        }
        catch
        {
          // Swallow exceptions to avoid crashing the watcher.
          onDisconnected();
          return;
        }

        Thread.Sleep(pollIntervalMs);
      }
    })
    {
      IsBackground = true,
      Name = "StdioMonitor"
    }.Start();
  }

  // ------------------------------------------------------------
  // Cross-platform detection helpers
  // ------------------------------------------------------------

  private static bool IsInputClosed()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return IsHandleClosedWin(STD_INPUT_HANDLE);
    else
      return IsFdClosedUnix(0); // stdin fd = 0
  }

  private static bool IsOutputClosed()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return IsHandleClosedWin(STD_OUTPUT_HANDLE);
    else
      return IsFdClosedUnix(1); // stdout fd = 1
  }

  // -------------------- Windows --------------------
  private const int STD_INPUT_HANDLE = -10;
  private const int STD_OUTPUT_HANDLE = -11;
  private const int ERROR_BROKEN_PIPE = 109;
  private const int ERROR_NO_DATA = 232;

  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern IntPtr GetStdHandle(int nStdHandle);

  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern bool PeekNamedPipe(
    IntPtr hNamedPipe,
    IntPtr lpBuffer,
    uint nBufferSize,
    IntPtr lpBytesRead,
    IntPtr lpTotalBytesAvail,
    IntPtr lpBytesLeftThisMsg);

  static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

  private static bool IsHandleClosedWin(int stdHandle)
  {
    var ptr = GetStdHandle(stdHandle);
    if (ptr == IntPtr.Zero || ptr == INVALID_HANDLE_VALUE)
      return true;

    // PeekNamedPipe works on both anonymous and named pipes and correctly
    // distinguishes "data available" from "pipe broken", unlike WaitForSingleObject
    // which signals on both conditions.
    bool ok = PeekNamedPipe(ptr, IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
    if (!ok)
    {
      int err = Marshal.GetLastWin32Error();
      return err == ERROR_BROKEN_PIPE || err == ERROR_NO_DATA;
    }
    return false;
  }

  // -------------------- Unix --------------------
  [StructLayout(LayoutKind.Sequential)]
  private struct pollfd
  {
    public int fd;
    public short events;
    public short revents;
  }

  private const short POLLIN = 0x001;
  private const short POLLHUP = 0x010;

  [DllImport("libc", SetLastError = true)]
  private static extern int poll([In, Out] pollfd[] fds, uint nfds, int timeout);

  private static bool IsFdClosedUnix(int fd)
  {
    var fds = new pollfd[1];
    fds[0].fd = fd;
    fds[0].events = POLLIN | POLLHUP;
    int ret = poll(fds, 1, 0);
    return ret >= 0 && (fds[0].revents & POLLHUP) != 0;
  }

}
