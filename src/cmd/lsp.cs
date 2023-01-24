using System;
using System.Text;
using Mono.Options;
using bhl.lsp;

namespace bhl {

public class LSP : ICmd
{
  public void Run(string[] args)
  {
    Logger.CleanUpLogFile();
    
    var workspace = new Workspace();

    var p = new OptionSet
    {
      { "root=", "bhl root dir",
        v => workspace.AddRoot(v, false) }
    };
    
    try
    {
      p.Parse(args);
    }
    catch(OptionException e)
    {
      Logger.WriteLine(e);
    }

    Console.OutputEncoding = new UTF8Encoding();

    var stdin = Console.OpenStandardInput();
    var stdout = Console.OpenStandardOutput();
    
    var connection = new ConnectionStdIO(stdout, stdin);
    
    var rpc = new JsonRpc();
    rpc.AttachService(new GeneralService(workspace));
    rpc.AttachService(new TextDocumentSynchronizationService(workspace));
    rpc.AttachService(new TextDocumentSignatureHelpService(workspace));
    rpc.AttachService(new TextDocumentGoToService(workspace));
    rpc.AttachService(new TextDocumentHoverService(workspace));
    rpc.AttachService(new TextDocumentSemanticTokensService(workspace));
    
    var server = new Server(connection, rpc);
    
    try
    {
      server.Start();
    }
    catch (Exception e)
    {
      Logger.WriteLine(e);
      Environment.Exit(-1);
    }
  }
}

}
