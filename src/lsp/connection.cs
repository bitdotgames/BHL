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
  
  readonly byte[] separator = { CR, LF };
  
  readonly StreamReader input;
  readonly Stream output;
  
  public ConnectionStdIO(Stream output, Stream input)
  {
    this.input = new StreamReader(input);
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

class StreamReader
{
  const int MAX_BUFFER_CHUNK_SIZE = 1024;
  
  Stream stream;
  List<byte[]> bufferChunks;
  int headPos;
  int tailPos;
  int lengthRead;

  public StreamReader(Stream input)
  {
    stream = input;
    bufferChunks = new List<byte[]>();
  }

  public byte[] ReadToSeparator(byte[] separator)
  {
    int separatorLength = separator.Length;
    int lengthBuffered = lengthRead;
    int bufferIndex = 0;
    int bufferPos = headPos;
    
    while(true)
    {
      while(lengthBuffered < separatorLength)
      {
        if(!stream.CanRead)
          return Slice(lengthRead, 0);
        
        lengthBuffered += FillBuffer();
      }

      int count;
      if(FindSeparator(separator, bufferIndex, bufferPos, out count))
        return Slice(count, separatorLength);
      
      lengthBuffered -= count;
      bufferIndex = bufferChunks.Count - 1;
      bufferPos = tailPos - separatorLength + 1;
      
      if(tailPos < separatorLength)
      {
        bufferIndex -= 1;
        bufferPos += MAX_BUFFER_CHUNK_SIZE;
      }
    }
  }

  int FillBuffer()
  {
    byte[] lastBuffer;
    if(bufferChunks.Count > 0 && tailPos < MAX_BUFFER_CHUNK_SIZE)
      lastBuffer = bufferChunks[bufferChunks.Count - 1];
    else
    {
      lastBuffer = new byte[MAX_BUFFER_CHUNK_SIZE];
      bufferChunks.Add(lastBuffer);
      tailPos = 0;
    }

    //commented for investigation
    //try
    //{
      var length = stream.Read(lastBuffer, tailPos, lastBuffer.Length - tailPos);
      lengthRead += length;
      tailPos += length;
      
      return length;
    //}
    //catch
    //{
    //  return 0;
    //}
  }

  bool FindSeparator(byte[] separator, int bufferIndex, int bufferPos, out int count)
  {
    count = 0;
    var separatorLength = separator.Length;
    var bufferIndexMin = bufferIndex;
    var bufferIndexMax = tailPos < separatorLength ? bufferChunks.Count - 1 : bufferChunks.Count;
    
    for(var i = bufferIndexMin; i < bufferIndexMax; i++)
    {
      var posMin = i == bufferIndexMin ? bufferPos : 0;
      var posMax = i == bufferIndexMax - 1
          ? tailPos < separatorLength ? MAX_BUFFER_CHUNK_SIZE + tailPos - separatorLength + 1 : tailPos - separatorLength + 1
          : MAX_BUFFER_CHUNK_SIZE;
      
      for(var j = posMin; j < posMax; j++)
      {
        if(IsSeparatorMatched(separator, i, j))
          return true;
        
        count++;
      }
    }

    return false;
  }

  bool IsSeparatorMatched(byte[] separator, int bufferIndex, int bufferPos)
  {
    for(var i = 0; i < separator.Length; i++)
    {
      var b = bufferPos + i < MAX_BUFFER_CHUNK_SIZE
          ? bufferChunks[bufferIndex][bufferPos + i]
          : bufferChunks[bufferIndex + 1][bufferPos + i - MAX_BUFFER_CHUNK_SIZE];
      
      if(b != separator[i])
        return false;
    }

    return true;
  }

  byte[] Slice(int copyLength, int chopLength)
  {
    var dest = new byte[copyLength];
    int destPos = 0;
    bool copying = true;
    var newBufferChunks = new List<byte[]>();
    int newHeadPos = 0;
    int newLengthRead = 0;
    
    foreach(var src in bufferChunks)
    {
      int bufferPos = src == bufferChunks[0] ? headPos : 0;
      int bufferLen = src == bufferChunks[bufferChunks.Count - 1] ? tailPos : src.Length;
      
      if(copying)
      {
        int srcPos = bufferPos;
        int srcLen = bufferLen - srcPos;
        int destLen = dest.Length - destPos;
        int copyLen = srcLen < destLen ? srcLen : destLen;
        
        Array.Copy(src, srcPos, dest, destPos, copyLen);
        
        destPos += copyLen;
        if(destPos == dest.Length)
        {
          copying = false;
          newHeadPos = srcPos + copyLen + chopLength;
          newLengthRead = bufferLen - newHeadPos;
          
          if(newHeadPos < bufferLen)
            newBufferChunks.Add(src);
          else
            newHeadPos -= bufferLen;
        }
      }
      else
      {
        newBufferChunks.Add(src);
        newLengthRead += bufferLen;
      }
    }

    bufferChunks = newBufferChunks;
    headPos = newHeadPos;
    lengthRead = newLengthRead;
    
    return dest;
  }

  public byte[] ReadBytes(int count)
  {
    while(lengthRead < count)
    {
      if(!stream.CanRead)
        return Slice(lengthRead, 0);
      
      FillBuffer();
    }

    return Slice(count, 0);
  }
}

}
