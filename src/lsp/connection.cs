using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace bhlsp
{
  public interface IBHLSPConnection
  {
    Task<string> Read();
    void Write(string json);
  }
  
  public class BHLSPConnectionStdIO : IBHLSPConnection
  {
    byte[] buffer = new byte[4096];
    
    private const byte CR = (byte)13;
    private const byte LF = (byte)10;
    
    private readonly byte[] separator = { CR, LF };
    
    private Stream input;
    private Stream output;
    
    private readonly object outputLock = new object();
    
    public BHLSPConnectionStdIO(Stream output, Stream input)
    {
      this.input = input;
      this.output = output;
    }
    
    public async Task<string> Read()
    {
      int length = await FillBufferAsync();
      int offset = 0;
      
      //skip header
      while(ReadToSeparator(offset, length, out int count))
        offset += count + separator.Length;
      
      return Encoding.UTF8.GetString(buffer, offset, length - offset);
    }

    private bool ReadToSeparator(int offset, int length, out int count)
    {
      count = 0;
      
      for(int i = offset; i < length; i++)
      {
        if(IsSeparatorMatched(i, length))
          return true;
        
        count++;
      }
      
      return false;
    }
    
    private bool IsSeparatorMatched(int offset, int len)
    {
      for(var i = 0; i < separator.Length; i++)
      {
        var b = offset + i < len ? buffer[offset + i] : buffer[offset];
        if(b != separator[i])
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
#if BHLSP_DEBUG
      catch(Exception e)
      {
        BHLSPLogger.WriteLine(e);
#else
      catch
      {
#endif
        return 0;
      }
    }
    
    public void Write(string json)
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
}