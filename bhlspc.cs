using System;
using System.Text;
using Mono.Options;
using bhlsp;

public class BHLSPC
{
  public static void Main(string[] args)
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
    rpc.AttachRpcService(new BHLSPTextDocumentFindReferencesJsonRpcService());
    
    BHLSPServer server = new BHLSPServer(connection, rpc);
    
    try
    {
      server.Listen().Wait();
    }
#if BHLSP_DEBUG
    catch (AggregateException ex)
    {
      BHLSPLogger.WriteLine(ex.InnerExceptions[0]);
      Environment.Exit(-1);
#else
    catch
    {
#endif
      Environment.Exit(-1);
    }
  }
}