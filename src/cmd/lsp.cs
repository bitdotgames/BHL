using System;
using System.Text;
using Mono.Options;
using bhl.lsp;

namespace bhl {

public class LSP : ICmd
{
  public void Run(string[] args)
  {
#if BHLSP_DEBUG
    Logger.CleanUpLogFile();
#endif
    
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
#if BHLSP_DEBUG
    catch(OptionException e)
    {
      Logger.WriteLine(e);
#else
    catch
    {
      // ignored
#endif
    }

    Console.OutputEncoding = new UTF8Encoding();

    var stdin = Console.OpenStandardInput();
    var stdout = Console.OpenStandardOutput();
    
    var connection = new ConnectionStdIO(stdout, stdin);
    
    var rpc = new JsonRpc();
    rpc.AttachService(new GeneralJsonRpcService(workspace));
    rpc.AttachService(new TextDocumentSynchronizationJsonRpcService(workspace));
    rpc.AttachService(new TextDocumentSignatureHelpJsonRpcService(workspace));
    rpc.AttachService(new TextDocumentGoToJsonRpcService(workspace));
    rpc.AttachService(new TextDocumentHoverJsonRpcService(workspace));
    rpc.AttachService(new TextDocumentSemanticTokensJsonRpcService(workspace));
    
    var server = new Server(workspace, connection, rpc);
    
    try
    {
      server.Start();
    }
#if BHLSP_DEBUG
    catch (Exception e)
    {
      Logger.WriteLine(e);
#else
    catch
    {
#endif
      Environment.Exit(-1);
    }
  }
}

}
