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
      string result = string.Empty;

      int length = 0;
      
      try
      {
        length = await input.ReadAsync(buffer, 0, buffer.Length);
      }
#if BHLSP_DEBUG
      catch(Exception e)
      {
        BHLSPLogger.WriteLine(e);
#else
      catch
      {
#endif
      }
      
      int offset = 0;
      
      while(ReadToSeparator(offset, length, out int count))
        offset += count + separator.Length;

      if(offset > 0)
      {
        var header = Encoding.ASCII.GetString(buffer, 0, offset);
        var lenPos = header.IndexOf(": ", StringComparison.Ordinal);
        int contentLength = 0;
        
        if(lenPos >= 0)
        {
          var name = header.Substring(0, lenPos);
          var value = header.Substring(lenPos + 2);
          if(string.Equals(name, "Content-Length", StringComparison.Ordinal))
            int.TryParse(value, out contentLength);
        }

        result = Encoding.UTF8.GetString(buffer, offset, length - offset);
        contentLength -= length - offset;

        while(contentLength > 0)
        {
          int readLen = contentLength <= buffer.Length ? contentLength : buffer.Length;
          length = await input.ReadAsync(buffer, 0, readLen);
          
          if(contentLength >= length)
            result += Encoding.UTF8.GetString(buffer, 0, length);
          else
            result += Encoding.UTF8.GetString(buffer, 0, contentLength);
          
          contentLength -= length;
        }
      }
      
      return result;
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
    
    public void Write(string json)
    {
      var utf8 = Encoding.UTF8.GetBytes(json);
      lock(outputLock)
      {
        using(var writer = new StreamWriter(output, Encoding.ASCII, 1024, true))
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