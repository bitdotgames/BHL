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
    
    var p = new OptionSet
    {
      { "root=", "bhl root dir",
        v => Workspace.self.AddRoot(v, false) }
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
    rpc.AttachRpcService(new GeneralJsonRpcService());
    rpc.AttachRpcService(new TextDocumentSynchronizationJsonRpcService());
    rpc.AttachRpcService(new TextDocumentSignatureHelpJsonRpcService());
    rpc.AttachRpcService(new TextDocumentGoToJsonRpcService());
    rpc.AttachRpcService(new TextDocumentHoverJsonRpcService());
    rpc.AttachRpcService(new TextDocumentSemanticTokensJsonRpcService());
    
    Server server = new Server(connection, rpc);
    
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
