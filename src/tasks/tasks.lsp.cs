using System;
using System.Text;
using System.Threading;
using Mono.Options;
using Microsoft.Extensions.Logging;
using bhl.lsp;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using ThreadTask = System.Threading.Tasks.Task;

#pragma warning disable CS8981

namespace bhl {

public static partial class Tasks
{
  [Task]
  public static async ThreadTask lsp(Taskman tm, string[] args)
  {
    string log_file_path = "";

    var p = new OptionSet
    {
      { "log-file=", "log file path",
        v => log_file_path = v }
    };
    
    p.Parse(args);

    var logger_conf = new LoggerConfiguration()
      .Enrich.FromLogContext()
      .MinimumLevel.Verbose();
    
    if (!string.IsNullOrEmpty(log_file_path))
      logger_conf = logger_conf.WriteTo.File(log_file_path/*, rollingInterval: RollingInterval.Day*/);
    else
      logger_conf = logger_conf.WriteTo.Console();
    
    Log.Logger = logger_conf.CreateLogger();

    var workspace = new Workspace();

    var cts = new CancellationTokenSource();

    var server = await Server.CreateAsync(Log.Logger, workspace, cts.Token);
    //server.AttachAllServices();

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

}
