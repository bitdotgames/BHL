using System;
using System.Text;
using bhlsp;

public class BHLSPC
{
  public static void Main(string[] args)
  {
    Console.OutputEncoding = new UTF8Encoding();

    var stdin = Console.OpenStandardInput();
    var stdout = Console.OpenStandardOutput();
    
    var connection = new BHLSPConnectionStdIO(stdout, stdin);
    
    var rpc = new BHLSPJsonRpc();
    rpc.AttachRpcService(new BHLSPGeneralJsonRpcService());
    rpc.AttachRpcService(new BHLSPTextDocumentSynchronizationJsonRpcService());
    rpc.AttachRpcService(new BHLSPTextDocumentSignatureHelpJsonRpcService());
    rpc.AttachRpcService(new BHLSPTextDocumentGoToJsonRpcService());
    
    BHLSPServer server = new BHLSPServer(connection, rpc);

#if BHLSP_DEBUG
    BHLSPLogger.CleanUpLogFile();
#endif
      
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