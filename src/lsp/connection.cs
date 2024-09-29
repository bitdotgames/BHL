using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace bhl.lsp {

public interface IConnection
{
  string Read();
  void Write(string json);
}

public class ConnectionStdIO : IConnection
{
  const byte CR = 13;
  const byte LF = 10;
  
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

  public string Read()
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
      
      //headerBytes = reader.ReadToSeparator(separator);
      header_bytes = ReadToSeparator();
    }
    
    if(content_len == 0)
      return string.Empty;
    
    var bytes = ReadBytes(content_len);
    return Encoding.UTF8.GetString(bytes);
  }

  byte[] ReadToSeparator()
  {
    while(true)
    {
      logger.Log(1, "BUF CAP " + buffer.Length + " AVAIL " + buffer_available_len);
      
      if (buffer_available_len > 0)
      {
        int separator_pos = FindSeparatorPos(new Span<byte>(buffer, 0, buffer_available_len));
        if (separator_pos != -1)
        {
          var result = new byte[separator_pos];
          Buffer.BlockCopy(buffer, 0, result, 0, result.Length);
          
          int buffer_leftover_pos = separator_pos + 2;
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
      if (read_len == 0)
        throw new Exception("TODO: handle gracefully");
      logger.Log(1, "BUF DID READ " + read_len);

      buffer_available_len += read_len;
    }
  }

  byte[] ReadBytes(int len)
  {
    var result = new byte[len];
    int offset = 0;

    if (buffer_available_len > 0)
    {
      if (len <= buffer_available_len)
      {
        logger.Log(1, "AVALABLE FULL " + len + " VS " + buffer_available_len);
        //let's copy from the buffer to result
        Buffer.BlockCopy(buffer, 0, result, 0, len);
        //let's copy leftover to the beginning
        Buffer.BlockCopy(buffer, len, buffer, 0, buffer_available_len - len);
        buffer_available_len -= len;
        return result;
      }
      else
      {
        logger.Log(1, "AVALABLE PART " + len + " VS " + buffer_available_len);
        //let's copy from the buffer to result
        Buffer.BlockCopy(buffer, 0, result, 0, buffer_available_len);
        offset += buffer_available_len;
        len -= buffer_available_len;
        buffer_available_len = 0;
      }
    }
    
    while (len > 0)
    {
      int read_len = input.Read(result, offset, result.Length - offset);
      logger.Log(1, "BUF RLEN " + read_len + " OFFSET " + offset + " LEN " + len + " CAP " + result.Length);
      if (read_len == 0)
        throw new Exception("TODO: handle gracefully");

      len -= read_len;
      offset += read_len;
    }

    return result;
  }

  static int FindSeparatorPos(Span<byte> bytes)
  {
    for (int i = 0; i < bytes.Length - 1; ++i)
    {
      if (bytes[i] == CR && bytes[i + 1] == LF)
        return i;
    }

    return -1;
  }
  
  public void Write(string json)
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
  }
}

}
