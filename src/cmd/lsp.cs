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

    var workspace = new Workspace(logger);

    Console.OutputEncoding = new UTF8Encoding();

    var stdin = Console.OpenStandardInput();
    var stdout = Console.OpenStandardOutput();
    
    var connection = new ConnectionStdIO(stdout, stdin);
    
    var rpc = new RpcServer(logger, connection);
    rpc.AttachService(new LifecycleService(workspace));
    rpc.AttachService(new DiagnosticService(workspace, rpc));
    rpc.AttachService(new TextDocumentSynchronizationService(workspace));
    rpc.AttachService(new TextDocumentSignatureHelpService(workspace));
    rpc.AttachService(new TextDocumentGoToService(workspace));
    rpc.AttachService(new TextDocumentFindReferencesService(workspace));
    rpc.AttachService(new TextDocumentHoverService(workspace));
    rpc.AttachService(new TextDocumentSemanticTokensService(workspace));
    
    try
    {
      rpc.Start();
    }
    catch (Exception e)
    {
      logger.Log(0, e.Message);
      Environment.Exit(-1);
    }
  }
}

}
