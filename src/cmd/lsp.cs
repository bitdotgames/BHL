using System;
using System.Text;
using Mono.Options;
using bhl.lsp;

namespace bhl {

public class LSPCmd : ICmd
{
  public void Run(string[] args)
  {
    string log_file_path = "";

    var p = new OptionSet
    {
      { "log-file=", "log file path",
        v => log_file_path = v }
    };
    
    p.Parse(args);

    ILogWriter log_writer = 
      string.IsNullOrEmpty(log_file_path) ? 
        (ILogWriter)new ConsoleLogger() : 
        (ILogWriter)new FileLogger(log_file_path);

    var logger = new Logger(1, log_writer);

    var workspace = new Workspace();

    Console.OutputEncoding = new UTF8Encoding();

    var stdin = Console.OpenStandardInput();
    var stdout = Console.OpenStandardOutput();
    
    var connection = new ConnectionStdIO(stdout, stdin);
    
    var srv = new Server(logger, connection, workspace);
    
    try
    {
      srv.AttachAllServices();
      srv.Start();
    }
    catch (Exception e)
    {
      logger.Log(0, e.Message);
      Environment.Exit(-1);
    }
  }
}

}
