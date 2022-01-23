using System;
using System.Threading.Tasks;

namespace bhlsp
{
  public class BHLSPServer
  {
    private IBHLSPConnection connection;
    private IBHLSPJsonRpc rpc;

    public BHLSPServer(IBHLSPConnection connection, IBHLSPJsonRpc rpc)
    {
      this.connection = connection;
      this.rpc = rpc;
    }
    
    public async Task Listen()
    {
      while (true)
      {
        try
        {
          var success = await ReadAndHandle();
          if (!success)
            break;
        }

#if BHLSP_DEBUG
        catch(Exception e)
        {
          BHLSPLogger.WriteLine(e);
        }
#else
        catch
        {
          // ignored
        }
#endif
      }
    }
    
    async Task<bool> ReadAndHandle()
    {
      string json = await connection.Read();
      
      string response = rpc.HandleMessage(json);
      if(!string.IsNullOrEmpty(response))
        connection.Write(response);
      
      return true;
    }
  }
}