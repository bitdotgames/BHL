using System;
using System.Text;
using Mono.Options;
using bhlsp;

namespace bhl {

public class LSP : ICmd
{
  public void Run(string[] args)
  {
#if BHLSP_DEBUG
    BHLSPLogger.CleanUpLogFile();
#endif
    
    var p = new OptionSet
    {
      { "root=", "bhl root dir",
        v => BHLSPWorkspace.self.AddRoot(v, false) }
    };
    
    try
    {
      p.Parse(args);
    }
#if BHLSP_DEBUG
    catch(OptionException e)
    {
      BHLSPLogger.WriteLine(e);
#else
    catch
    {
      // ignored
#endif
    }

    Console.OutputEncoding = new UTF8Encoding();

    var stdin = Console.OpenStandardInput();
    var stdout = Console.OpenStandardOutput();
    
    var connection = new BHLSPConnectionStdIO(stdout, stdin);
    
    var rpc = new BHLSPJsonRpc();
    rpc.AttachRpcService(new BHLSPGeneralJsonRpcService());
    rpc.AttachRpcService(new BHLSPTextDocumentSynchronizationJsonRpcService());
    rpc.AttachRpcService(new BHLSPTextDocumentSignatureHelpJsonRpcService());
    rpc.AttachRpcService(new BHLSPTextDocumentGoToJsonRpcService());
    rpc.AttachRpcService(new BHLSPTextDocumentHoverJsonRpcService());
    rpc.AttachRpcService(new BHLSPTextDocumentSemanticTokensJsonRpcService());
    
    BHLSPServer server = new BHLSPServer(connection, rpc);
    
    try
    {
      server.Start();
    }
#if BHLSP_DEBUG
    catch (Exception e)
    {
      BHLSPLogger.WriteLine(e);
#else
    catch
    {
#endif
      Environment.Exit(-1);
    }
  }
}

}
