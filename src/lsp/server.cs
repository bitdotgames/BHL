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
    
    public void Start()
    {
      while (true)
      {
        try
        {
          var success = ReadAndHandle();
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
    
    bool ReadAndHandle()
    {
      string json = connection.Read();
      if(string.IsNullOrEmpty(json))
        return false;
      
#if BHLSP_DEBUG
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();
#endif
      
      string response = rpc.HandleMessage(json);
      
#if BHLSP_DEBUG
        sw.Stop();
        BHLSPLogger.WriteLine($"HandleMessage done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
#endif
      
      try
      {
        if(!string.IsNullOrEmpty(response))
          connection.Write(response);
      }

#if BHLSP_DEBUG
      catch(Exception e)
      {
        BHLSPLogger.WriteLine(e);
        return false;
      }
#else
      catch
      {
        return false;
      }
#endif
      
      return true;
    }
  }
}