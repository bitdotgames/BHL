using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
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
          BHLSPC.Logger.WriteLine(e);
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
      
#if BHLSP_DEBUG_JSON_LOG
      BHLSPC.Logger.WriteLine($"--> {json}");
#endif
      
      string response = rpc.HandleMessage(json);
      if(!string.IsNullOrEmpty(response))
      {
#if BHLSP_DEBUG_JSON_LOG
        BHLSPC.Logger.WriteLine($"<-- {response}");
#endif
        connection.Write(response);
      }
      
      return true;
    }
  }
}