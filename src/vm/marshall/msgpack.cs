
using System.Buffers;
using System.Collections.Generic;
using System.IO;

namespace bhl.marshall {
  
public class MsgPackDataWriter : IWriter
{
  Stream stream;
  bhl.MsgPack.MsgPackWriter io;
  Stack<int> space = new Stack<int>();

  public Stream Stream => stream;

  public MsgPackDataWriter(Stream stream) 
  {
    Reset(stream);
  }

  public void Reset(Stream stream)
  {
    this.stream = stream;
    io = new bhl.MsgPack.MsgPackWriter(stream);
    space.Clear();
    space.Push(1);
  }

  void DecSpace() 
  {
    if(space.Count == 0)
      throw new Error(ErrorCode.NO_RESERVED_SPACE);
    
    int left = space.Pop();
    //special 'unspecified' case
    if(left != -1)
    {
      left--;
      if(left < 0)
        throw new Error(ErrorCode.NO_RESERVED_SPACE);
    }
    space.Push(left);
  }

  public void BeginContainer(int size) 
  {
    DecSpace();
    space.Push(size);
    //special 'unspecified' case
    if(size == -1)
      io.Write(-1);
    else
      io.WriteArrayHeader(size);
  }

  public void EndContainer() 
  {
    if(space.Count <= 1)
      throw new Error(ErrorCode.NO_RESERVED_SPACE);
    
    int left = space.Pop();
    //special 'unspecifed' case 
    if(left == -1)
      return;
    if(left != 0)
      throw new Error(ErrorCode.DANGLING_DATA, "Not written items amount: " + left);
  }
  
  public void WriteI8(sbyte v) 
  {
    io.Write(v);
    DecSpace();
  }
 
  public void WriteU8(byte v) 
  {
    io.Write(v);
    DecSpace();
  }
  
  public void WriteI16(short v) 
  {
    io.Write(v);
    DecSpace();
  }

  public void WriteU16(ushort v) 
  {
    io.Write(v);
    DecSpace();
  }

  public void WriteI32(int v) 
  {
    io.Write(v);
    DecSpace();
  }

  public void WriteU32(uint v) 
  {
    io.Write(v);
    DecSpace();
  }

  public void WriteU64(ulong v) 
  {
    io.Write(v);
    DecSpace();
  }

  public void WriteI64(long v) 
  {
    io.Write(v);
    DecSpace();
  }

  public void WriteBool(bool v) 
  {
    io.Write(v ? 1 : 0);
    DecSpace();
  }

  public void WriteFloat(float v) 
  {
    io.Write(v);
    DecSpace();
  }

  public void WriteDouble(double v) 
  {
    io.Write(v);
    DecSpace();
  }

  public void WriteString(string v) 
  {
    if(v == null) 
      io.Write("");
    else
      io.Write(v);
    DecSpace();
  }

  public void WriteNil()
  {
    io.WriteNil();
    DecSpace();
  }
}

public class MsgPackDataReader : IReader 
{
  Stream stream;
  bhl.MsgPack.MsgPackReader io;
  
  public Stream Stream => stream;

  struct StructPos
  {
    public int max;
    public int curr;

    public StructPos(int max)
    {
      this.max = max;
      curr = 0;
    }
  }

  Stack<StructPos> structs_pos = new Stack<StructPos>();
  
  public MsgPackDataReader(Stream _stream) 
  { 
    Reset(_stream);
  }

  public void Reset(Stream _stream)
  {
    stream = _stream;
    structs_pos.Clear();
    io = new bhl.MsgPack.MsgPackReader(stream);
  }

  public void SetPos(long pos)
  {
    stream.Position = pos;
    structs_pos.Clear();
  }

  void Next()
  {
    if(!io.Read()) 
      throw new Error(ErrorCode.IO_READ);
    
    MoveSpaceCursor();
  }

  int NextInt() 
  {
    Next();

    if(io.IsSigned())
      return io.ValueSigned;
    else if(io.IsUnsigned())
      return (int)io.ValueUnsigned;
    else
      throw new Error(ErrorCode.TYPE_MISMATCH, "Got type: " + io.Type); 
  }
   
  bool NextBool() 
  {
    Next();
    
    if(io.IsUnsigned())
      return io.ValueUnsigned != 0;
    else if(io.IsSigned())
      return (uint)io.ValueSigned != 0;
    else if(io.IsBoolean())
      return (bool)io.ValueBoolean;
    else
      throw new Error(ErrorCode.TYPE_MISMATCH, "Got type: " + io.Type); 
  }

  long NextLong() 
  {
    Next();

    if(io.IsUnsigned())
      return (long)io.ValueUnsigned;
    else if(io.IsSigned())
      return io.ValueSigned;
    else if(io.IsUnsigned64())
      return (long)io.ValueUnsigned64;
    else if(io.IsSigned64())
      return io.ValueSigned64;
    else
      throw new Error(ErrorCode.TYPE_MISMATCH, "Got type: " + io.Type); 
  }
  
  public void ReadI8(ref sbyte v) 
  {
    v = (sbyte)NextInt();
  }
  
  public void ReadI16(ref short v) 
  {
    v = (short)NextInt();
  }
  
  public void ReadI32(ref int v) 
  {
    v = NextInt();
  }
  
  public void ReadU8(ref byte v) 
  {
    v = (byte)NextInt();
  }
  
  public void ReadU16(ref ushort v) 
  {
    v = (ushort)NextInt();
  }
  
  public void ReadU32(ref uint v) 
  {
    v = (uint)NextInt();
  }

  public void ReadU64(ref ulong v) 
  {
    v = (ulong)NextLong();
  }

  public void ReadI64(ref long v) 
  {
    v = NextLong();
  }

  public void ReadBool(ref bool v) 
  {
    v = NextBool();
  }

  public void ReadFloat(ref float v) 
  {
    Next();

    if(io.IsUnsigned()) 
    {
      v = io.ValueUnsigned;
    } 
    else if(io.IsSigned()) 
    {
      v = io.ValueSigned;
    } 
    else 
    {
      switch(io.Type)
      {
        case bhl.MsgPack.TypePrefixes.Float:
          v = io.ValueFloat;
          break;
        case bhl.MsgPack.TypePrefixes.Double:
          var tmp = io.ValueDouble;
          //TODO:
          //if(tmp > float.MaxValue || tmp < float.MinValue)
          //{
          //  return ErrorCode.TYPE_DONT_MATCH; 
          //}
          v = (float) tmp;
          break;
        default:
          throw new Error(ErrorCode.TYPE_MISMATCH, "Got type: " + io.Type); 
      }
    }
  }

  public void ReadDouble(ref double v) 
  {
    Next();

    if(io.IsUnsigned()) 
    {
      v = io.ValueUnsigned;
    } 
    else if(io.IsSigned()) 
    {
      v = io.ValueSigned;
    } 
    else 
    {
      switch(io.Type)
      {
        case bhl.MsgPack.TypePrefixes.Float:
          v = (double)io.ValueFloat;
          break;
        case bhl.MsgPack.TypePrefixes.Double:
          v = io.ValueDouble;
          break;
        case bhl.MsgPack.TypePrefixes.UInt64:
          v = (double)io.ValueUnsigned64;
          break;
        case bhl.MsgPack.TypePrefixes.Int64:
          v = (double)io.ValueSigned64;    
          break;
        default:
          throw new Error(ErrorCode.TYPE_MISMATCH, "Got type: " + io.Type); 
      }
    }
  }

  public void ReadRawBegin(ref int vlen) 
  {
    Next();

    if(!io.IsRaw()) 
      throw new Error(ErrorCode.TYPE_MISMATCH, "Got type: " + io.Type); 

    vlen = (int)io.Length;
  }

  public void ReadRawEnd(byte[] v) 
  {
    if(!io.IsRaw()) 
      throw new Error(ErrorCode.TYPE_MISMATCH, "Got type: " + io.Type); 

    io.ReadValueRaw(v, 0, (int)io.Length);
  }

  public void ReadString(ref string v) 
  {
    Next();

    if(!io.IsRaw()) 
      throw new Error(ErrorCode.TYPE_MISMATCH, "Got type: " + io.Type); 

    var strbuf = ArrayPool<byte>.Shared.Rent((int)io.Length);
    io.ReadValueRaw(strbuf, 0, (int)io.Length);
    v = System.Text.Encoding.UTF8.GetString(strbuf, 0, (int)io.Length);
    ArrayPool<byte>.Shared.Return(strbuf);
  }

  public int BeginContainer() 
  {
    if(!io.Read()) 
      throw new Error(ErrorCode.IO_READ);

    if(!io.IsArray())
    {
      //special 'unspecified' case
      if(io.IsSigned() && io.ValueSigned == -1)
      {
        structs_pos.Push(new StructPos(-1));
        return -1;
      }
      else
        throw new Error(ErrorCode.TYPE_MISMATCH, "Got type: " + io.Type); 
    }

    int len = (int)io.Length;
    structs_pos.Push(new StructPos(len));
    return len;
  } 

  public void GetArraySize(ref int v) 
  {
    if(!io.IsArray())
      throw new Error(ErrorCode.TYPE_MISMATCH, "Got type: " + io.Type); 

    v = (int)io.Length;
  }

  void SkipField()
  {
    if(!io.Read())
      return;

    if(!io.IsArray() && !io.IsMap())
    {
      //NOTE: if value is raw we need to read all of its data
      if(io.IsRaw())
      {
        var buf = ArrayPool<byte>.Shared.Rent((int)io.Length);
        io.ReadValueRaw(buf, 0, (int)io.Length);
        ArrayPool<byte>.Shared.Return(buf);
      }

      return;
    }
    
    uint array_len = io.Length; 
    for(uint i=0; i<array_len; ++i)
      SkipField();
  }

  void SkipTrailingFields() 
  {
    while(SpaceLeft() > -1)
    {
      SkipField();
      MoveSpaceCursor();
    }
  }

  public void EndContainer() 
  {
    SkipTrailingFields();
    structs_pos.Pop();
    MoveSpaceCursor();
  }

  int SpaceLeft()
  {
    if(structs_pos.Count == 0)
      return 0;
    var ap = structs_pos.Peek();
    //special 'unspecified' case
    if(ap.max == -1)
      return -1;
    return ap.max - ap.curr - 1;
  }

  void MoveSpaceCursor()
  {
    if(structs_pos.Count > 0)
    {
      var ap = structs_pos.Pop();
      ap.curr++;
      //for debug
      //Console.WriteLine(new String('>', structs_pos.Count) + " CURSOR " + ap.curr + " /" + ap.max + " level: " + structs_pos.Count);
      structs_pos.Push(ap);
    }
  }
}

}
