using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace bhlsp
{
  public class BHLSPServer
  {
    byte[] buffer = new byte[4096];
    
    private const byte CR = (byte)13;
    private const byte LF = (byte)10;
    private readonly byte[] separator = { CR, LF };
    
    private Stream input;
    private Stream output;

    private BHLSPServerTarget target;
    
    private readonly object outputLock = new object();
    
    public BHLSPServer(Stream output, Stream input)
    {
      this.input = input;
      this.output = output;

      target = new BHLSPServerTarget(this);
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
        catch (Exception e)
        {
          BHLSPC.Logger.WriteLine(e);
        }
      }
    }
    
    async Task<bool> ReadAndHandle()
    {
      string json = await Read();
      
      var msg = JsonConvert.DeserializeObject<MessageTest>(json); //TODO: Batch Mode
      
      if(msg == null)
        return false;

      if(msg.IsMessage)
      {
        foreach(var method in target.GetType().GetMethods())
        {
          if(IsJsonRpcMethod(method, msg.method))
          {  
            method.Invoke(target, new object[] {msg.id, msg.@params});
          }
        }
      }
      
      return true;
    }

    private bool IsJsonRpcMethod(MethodInfo method, string name)
    {
      foreach(var attribute in method.GetCustomAttributes(true))
      {
        if(attribute is JsonRpcMethodAttribute jsonRpcMethod && jsonRpcMethod.Method == name)
          return true;
      }
      
      return false;
    }

    private async Task<string> Read()
    {
      int length = await FillBufferAsync();
      int offset = 0;
      
      //skip header
      while (ReadToSeparator(offset, length, out int count))
        offset += count + separator.Length;
      
      return Encoding.UTF8.GetString(buffer, offset, length - offset);
    }

    private bool ReadToSeparator(int offset, int length, out int count)
    {
      count = 0;
      
      for (int i = offset; i < length; i++)
      {
        if(IsSeparatorMatched(i, length))
          return true;
        
        count++;
      }
      
      return false;
    }
    
    private bool IsSeparatorMatched(int offset, int len)
    {
      for (var i = 0; i < separator.Length; i++)
      {
        var b = offset + i < len ? buffer[offset + i] : buffer[offset];
        if (b != separator[i])
          return false;
      }
      
      return true;
    }
    
    async Task<int> FillBufferAsync()
    {
      try
      {
        int length = await input.ReadAsync(buffer, 0, buffer.Length);
        return length;
      }
      catch
      {
        return 0;
      }
    }
    
    private void Write(string json)
    {
      var utf8 = Encoding.UTF8.GetBytes(json);
      lock (outputLock)
      {
        using (var writer = new StreamWriter(output, Encoding.ASCII, 1024, true))
        {
          writer.Write($"Content-Length: {utf8.Length}\r\n");
          writer.Write("\r\n");
          writer.Flush();
        }
        
        output.Write(utf8, 0, utf8.Length);
        output.Flush();
      }
    }
  }
  
  [AttributeUsage(AttributeTargets.Method)]
  public class JsonRpcMethodAttribute : Attribute
  {
    private string method;

    public JsonRpcMethodAttribute(string method)
    {
      this.method = method;
    }

    public string Method => method;
  }
  
  internal class BHLSPServerTarget
  {
    private BHLSPServer server;
    
    public BHLSPServerTarget(BHLSPServer server)
    {
      this.server = server;
    }
    
    [JsonRpcMethod("initialize")]
    public object Initialize(string id, JToken args)
    {
      BHLSPC.Logger.WriteLine("--> Initialize");
      BHLSPC.Logger.WriteLine(args.ToString());
      
      //var init_params = arg.ToObject<InitializeParams>();
      
      return null;
    }
  }

  internal class MessageTest
  {
    public string jsonrpc { get; set; }

    public string id { get; set; }

    public string method { get; set; }

    public JToken @params { get; set; }

    public bool IsMessage => jsonrpc == "2.0";
  }
}