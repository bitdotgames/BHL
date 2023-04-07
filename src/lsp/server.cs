using System;

namespace bhl.lsp {

public class Server
{
  IConnection connection;
  IJsonRpc rpc;
  ILogger logger;

  public Server(ILogger logger, IConnection connection, IJsonRpc rpc)
  {
    this.logger = logger;
    this.connection = connection;
    this.rpc = rpc;
  }
  
  public void Start()
  {
    logger.Log(0, "Starting BHL LSP server...");

    while(true)
    {
      try
      {
        bool success = ReadAndHandle();
        if(!success)
          break;
      }
      catch(Exception e)
      {
        logger.Log(0, e.Message);
        // ignored
      }
    }
  }
  
  bool ReadAndHandle()
  {
    string json = connection.Read();
    if(string.IsNullOrEmpty(json))
      return false;
    
      var sw = new System.Diagnostics.Stopwatch();
      sw.Start();
    
    string response = rpc.Handle(json);
    
    sw.Stop();
    logger.Log(1, $"HandleMessage done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
    
    try
    {
      if(!string.IsNullOrEmpty(response))
        connection.Write(response);
    }
    catch(Exception e)
    {
      logger.Log(0, e.Message);
      return false;
    }
    
    return true;
  }
}

}
