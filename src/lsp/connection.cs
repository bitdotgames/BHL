using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace bhl.lsp {

public interface IConnection
{
  Task<string> Read();
  Task Write(string json);
}

public class ConnectionStdIO : IConnection
{
  const int SeparatorLen = 2;
  
  private readonly Logger logger;
  private readonly Stream input;
  private readonly Stream output;

  private byte[] buffer = new byte[1024];
  private int buffer_available_len = 0;
  
  public ConnectionStdIO(Logger logger, Stream output, Stream input)
  {
    this.logger = logger;

    this.input = input;
    this.output = output;
  }

  public Task<string> Read()
  {
    int content_len = 0;

    var header_bytes = ReadToSeparator();
    
    while(header_bytes.Length != 0)
    {
      logger.Log(1, "HEADER BYTES: " + header_bytes.Length);
      
      string header_line = Encoding.ASCII.GetString(header_bytes);
      logger.Log(1, "HEADER LINE: '" + header_line + "'");
      int position = header_line.IndexOf(": ", StringComparison.Ordinal);
      if(position >= 0)
      {
        string key = header_line.Substring(0, position);
        string value = header_line.Substring(position + 2);
        
        logger.Log(1, "HEADER: '" + key  + "' '" + value + "'");
        
        if(string.Equals(key, "Content-Length", StringComparison.Ordinal))
          int.TryParse(value, out content_len);
      }
      
      header_bytes = ReadToSeparator();
    }
    
    if(content_len == 0)
      return Task.FromResult(string.Empty);
    
    var bytes = ReadBytes(content_len);
    return Task.FromResult(Encoding.UTF8.GetString(bytes));
  }

  byte[] ReadToSeparator()
  {
    while(true)
    {
      logger.Log(1, "BUF CAP " + buffer.Length + " AVAIL " + buffer_available_len);
      
      if(buffer_available_len > 0)
      {
        int separator_pos = FindSeparatorPos(new Span<byte>(buffer, 0, buffer_available_len));
        if(separator_pos != -1)
        {
          var result = new byte[separator_pos];
          Buffer.BlockCopy(buffer, 0, result, 0, result.Length);
          
          int buffer_leftover_pos = separator_pos + SeparatorLen;
          int buffer_leftover = buffer_available_len - buffer_leftover_pos;
          logger.Log(1, "SEPARATOR " + separator_pos + " LEFTOVER " + buffer_leftover + " PREV " + buffer_available_len + " RES " + result.Length);
          //let's copy leftover to the beginning
          Buffer.BlockCopy(buffer, buffer_leftover_pos, buffer, 0, buffer_leftover);
          buffer_available_len = buffer_leftover;
          return result;
        }
      }

      //let's double the buffer if it's not enough 
      if(buffer.Length - buffer_available_len == 0)
      {
        var temp = new byte[buffer.Length * 2];
        Buffer.BlockCopy(buffer, 0, temp, 0, buffer.Length);
        buffer = temp;
        logger.Log(1, "BUF INC " + buffer.Length);
      }

      logger.Log(1, "BUF TRY READ " + (buffer.Length - buffer_available_len));
      int read_len = input.Read(buffer, buffer_available_len, buffer.Length - buffer_available_len);
      if(read_len == 0)
        throw new Exception("TODO: handle gracefully");
      logger.Log(1, "BUF DID READ " + read_len);

      buffer_available_len += read_len;
    }
  }

  byte[] ReadBytes(int total_len)
  {
    var result = new byte[total_len];

    FillFromBuffer(ref total_len, result);
    
    while(total_len > 0)
    {
      int read_len = input.Read(result, result.Length - total_len, total_len);
      logger.Log(1, "BUF RLEN " + read_len + " OFFSET " + (result.Length - total_len) + " LEN " + total_len + " CAP " + result.Length);
      if (read_len == 0)
        throw new Exception("TODO: handle gracefully");

      total_len -= read_len;
    }

    return result;
  }

  void FillFromBuffer(ref int len, byte[] result)
  {
    if(buffer_available_len == 0)
      return;
    
    if(len <= buffer_available_len)
    {
      logger.Log(1, "AVALABLE FULL " + len + " VS " + buffer_available_len);
      //let's copy from the buffer to result
      Buffer.BlockCopy(buffer, 0, result, 0, len);
      //let's copy leftover to the beginning
      Buffer.BlockCopy(buffer, len, buffer, 0, buffer_available_len - len);
      buffer_available_len -= len;
      len = 0;
    }
    else
    {
      logger.Log(1, "AVALABLE PART " + len + " VS " + buffer_available_len);
      //let's copy from the buffer to result
      Buffer.BlockCopy(buffer, 0, result, 0, buffer_available_len);
      len -= buffer_available_len;
      buffer_available_len = 0;
    }
  }

  static int FindSeparatorPos(Span<byte> bytes)
  {
    for(int i = 0; i < bytes.Length - 1; ++i)
    {
      if(bytes[i] == 13/* \r */ && 
         bytes[i + 1] == 10/* \n */)
        return i;
    }

    return -1;
  }
  
  public Task Write(string json)
  {
    var utf8 = Encoding.UTF8.GetBytes(json);
    using(var writer = new StreamWriter(output, Encoding.ASCII, 1024, true))
    {
      writer.Write($"Content-Length: {utf8.Length}\r\n");
      writer.Write("\r\n");
      writer.Flush();
    }
      
    output.Write(utf8, 0, utf8.Length);
    output.Flush();

    return Task.CompletedTask;
  }
}

}
