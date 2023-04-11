using System;

namespace bhl.lsp {

public class Server
{
  IConnection connection;
  IJsonRpc rpc;
  Logger logger;

  public Server(Logger logger, IConnection connection, IJsonRpc rpc)
  {
    this.logger = logger;
    this.connection = connection;
    this.rpc = rpc;
  }
  
  public void Start()
  {
    logger.Log(1, "Starting BHL LSP server...");

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
      }
    }
  }
  
  bool ReadAndHandle()
  {
    try
    {
      string json = connection.Read();
      if(string.IsNullOrEmpty(json))
        return false;
    
      string response = rpc.Handle(json);

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
