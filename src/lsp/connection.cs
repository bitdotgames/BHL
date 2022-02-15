using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace bhlsp
{
  public interface IBHLSPConnection
  {
    string Read();
    void Write(string json);
  }
  
  public class BHLSPConnectionStdIO : IBHLSPConnection
  {
    private const byte CR = 13;
    private const byte LF = 10;
    
    private readonly byte[] separator = { CR, LF };
    
    private readonly BHLSPStreamReader input;
    private readonly Stream output;
    
    public BHLSPConnectionStdIO(Stream output, Stream input)
    {
      this.input = new BHLSPStreamReader(input);
      this.output = output;
    }

    public string Read()
    {
      var contentLength = 0;
      var headerBytes = input.ReadToSeparator(separator);
      
      while(headerBytes.Length != 0)
      {
        var headerLine = Encoding.ASCII.GetString(headerBytes);
        var position = headerLine.IndexOf(": ", StringComparison.Ordinal);
        if(position >= 0)
        {
          var name = headerLine.Substring(0, position);
          var value = headerLine.Substring(position + 2);
          
          if(string.Equals(name, "Content-Length", StringComparison.Ordinal))
            int.TryParse(value, out contentLength);
        }
        
        headerBytes = input.ReadToSeparator(separator);
      }
      
      if(contentLength == 0)
        return string.Empty;
      
      var contentBytes = input.ReadBytes(contentLength);
      return Encoding.UTF8.GetString(contentBytes);
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

  class BHLSPStreamReader
  {
    private const int bufferSize = 1024;
    
    private Stream stream;
    private List<byte[]> buffers;
    private int headPos;
    private int tailPos;
    private int lengthRead;

    public BHLSPStreamReader(Stream input)
    {
      stream = input;
      buffers = new List<byte[]>();
    }

    public byte[] ReadToSeparator(byte[] separator)
    {
      var separatorLength = separator.Length;
      var lengthPrepared = lengthRead;
      var bufferIndex = 0;
      var bufferPos = headPos;
      
      while(true)
      {
        while(lengthPrepared < separatorLength)
        {
          if(!stream.CanRead)
            return Slice(lengthRead, 0);
          
          lengthPrepared += FillBuffer();
        }

        int count;
        if(FindSeparator(separator, bufferIndex, bufferPos, out count))
          return Slice(count, separatorLength);
        
        lengthPrepared -= count;
        bufferIndex = buffers.Count - 1;
        bufferPos = tailPos - separatorLength + 1;
        
        if(tailPos < separatorLength)
        {
          bufferIndex -= 1;
          bufferPos += bufferSize;
        }
      }
    }

    private int FillBuffer()
    {
      byte[] lastBuffer;
      if(buffers.Count > 0 && tailPos < bufferSize)
        lastBuffer = buffers[buffers.Count - 1];
      else
      {
        lastBuffer = new byte[bufferSize];
        buffers.Add(lastBuffer);
        tailPos = 0;
      }

      try
      {
        var length = stream.Read(lastBuffer, tailPos, lastBuffer.Length - tailPos);
        lengthRead += length;
        tailPos += length;
        
        return length;
      }
      catch
      {
        return 0;
      }
    }

    private bool FindSeparator(byte[] separator, int bufferIndex, int bufferPos, out int count)
    {
      count = 0;
      var separatorLength = separator.Length;
      var bufferIndexMin = bufferIndex;
      var bufferIndexMax = tailPos < separatorLength ? buffers.Count - 1 : buffers.Count;
      
      for(var i = bufferIndexMin; i < bufferIndexMax; i++)
      {
        var posMin = i == bufferIndexMin ? bufferPos : 0;
        var posMax = i == bufferIndexMax - 1
            ? tailPos < separatorLength ? bufferSize + tailPos - separatorLength + 1 : tailPos - separatorLength + 1
            : bufferSize;
        
        for(var j = posMin; j < posMax; j++)
        {
          if(IsSeparatorMatched(separator, i, j))
            return true;
          
          count++;
        }
      }

      return false;
    }

    private bool IsSeparatorMatched(byte[] separator, int bufferIndex, int bufferPos)
    {
      for(var i = 0; i < separator.Length; i++)
      {
        var b = bufferPos + i < bufferSize
            ? buffers[bufferIndex][bufferPos + i]
            : buffers[bufferIndex + 1][bufferPos + i - bufferSize];
        
        if(b != separator[i])
          return false;
      }

      return true;
    }

    private byte[] Slice(int copyLength, int chopLength)
    {
      var dest = new byte[copyLength];
      var destPos = 0;
      var copying = true;
      var newBuffers = new List<byte[]>();
      var newHeadPos = 0;
      var newLengthRead = 0;
      
      foreach(var src in buffers)
      {
        var bufferPos = src == buffers[0] ? headPos : 0;
        var bufferLen = src == buffers[buffers.Count - 1] ? tailPos : src.Length;
        
        if(copying)
        {
          var srcPos = bufferPos;
          var srcLen = bufferLen - srcPos;
          var destLen = dest.Length - destPos;
          var copyLen = srcLen < destLen ? srcLen : destLen;
          
          Array.Copy(src, srcPos, dest, destPos, copyLen);
          
          destPos += copyLen;
          if(destPos == dest.Length)
          {
            copying = false;
            newHeadPos = srcPos + copyLen + chopLength;
            if(newHeadPos < bufferLen)
            {
              newBuffers.Add(src);
              newLengthRead = bufferLen - newHeadPos;
            }
            else
            {
              newLengthRead = bufferLen - newHeadPos;
              newHeadPos -= bufferLen;
            }
          }
        }
        else
        {
          newBuffers.Add(src);
          newLengthRead += bufferLen;
        }
      }

      buffers = newBuffers;
      headPos = newHeadPos;
      lengthRead = newLengthRead;
      
      return dest;
    }

    public byte[] ReadBytes(int count)
    {
      while(lengthRead < count)
      {
        if (!stream.CanRead)
          return Slice(lengthRead, 0);
        
        FillBuffer();
      }

      return Slice(count, 0);
    }
  }
}