using System;
using System.Text;
using Mono.Options;
using bhl.lsp;
using bhl.lsp.proto;

namespace bhl {

public class LSP : ICmd
{
  public void Run(string[] args)
  {
    var inc_path = new IncludePath();

    string log_file_path = "";

    var p = new OptionSet
    {
      { "inc-path=", "source include directories separated by ;",
        v => inc_path = new IncludePath(v.Split(';')) },
      { "log-file=", "log file path",
        v => log_file_path = v }
    };
    
    p.Parse(args);

    var ts = new Types();
    var workspace = new Workspace();
    workspace.Init(ts, inc_path);

    ILogWriter log_writer = 
      string.IsNullOrEmpty(log_file_path) ? 
        (ILogWriter)new ConsoleLogger() : 
        (ILogWriter)new FileLogger(log_file_path);

    var logger = new Logger(1, log_writer);

    Console.OutputEncoding = new UTF8Encoding();

    var stdin = Console.OpenStandardInput();
    var stdout = Console.OpenStandardOutput();
    
    var connection = new ConnectionStdIO(stdout, stdin);
    
    var rpc = new JsonRpc(logger);
    rpc.AttachService(new LifecycleService(logger, workspace));
    rpc.AttachService(new TextDocumentSynchronizationService(logger, workspace));
    rpc.AttachService(new TextDocumentSignatureHelpService(workspace));
    rpc.AttachService(new TextDocumentGoToService(workspace));
    rpc.AttachService(new TextDocumentHoverService(workspace));
    rpc.AttachService(new TextDocumentSemanticTokensService(workspace));
    
    var server = new Server(logger, connection, rpc);
    
    try
    {
      server.Start();
    }
    catch (Exception e)
    {
      logger.Log(0, e.Message);
      Environment.Exit(-1);
    }
  }
}

}
