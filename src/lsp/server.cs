using System;

namespace bhl.lsp {

public class Server
{
  Workspace workspace;
  Connection connection;
  IJsonRpc rpc;

  public Server(Workspace workspace, Connection connection, IJsonRpc rpc)
  {
    this.workspace = workspace;
    this.connection = connection;
    this.rpc = rpc;
  }
  
  public void Start()
  {
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
        Logger.WriteLine(e);
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
      Logger.WriteLine($"HandleMessage done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
    
    try
    {
      if(!string.IsNullOrEmpty(response))
        connection.Write(response);
    }
    catch(Exception e)
    {
      Logger.WriteLine(e);
      return false;
    }
    
    return true;
  }
}

}
