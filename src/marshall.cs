using MsgPack;
using System.IO;
using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {
namespace marshall {

public enum ErrorCode
{
  SUCCESS                = 0,
  NO_RESERVED_SPACE      = 1,
  DANGLING_DATA          = 2,
  IO_READ                = 3,
  TYPE_MISMATCH          = 4,
  INVALID_POS            = 5,
  BAD_CLASS_ID           = 6,
}

public class Error : Exception
{
  public ErrorCode code { get; }

  public Error(ErrorCode code, string msg = "")
    : base($"({code}) {msg}")
  {
    this.code = code;
  }
}

public interface IFactory
{
  IMarshallableGeneric CreateById(uint id);
}

public struct SyncContext
{
  public bool is_read;
  public IReader reader;
  public IWriter writer;
  public IFactory factory;

  public static SyncContext NewReader(IReader reader, IFactory factory = null)
  {
    var ctx = new SyncContext() {
      is_read = true,
      reader = reader,
      writer = null,
      factory = factory
    };
    return ctx;
  }

  public static SyncContext NewWriter(IWriter writer, IFactory factory = null)
  {
    var ctx = new SyncContext() {
      is_read = false,
      reader = null,
      writer = writer,
      factory = factory
    };
    return ctx;
  }
}

public interface IMarshallable 
{
  int GetFieldsNum();
  void Sync(SyncContext ctx);
}

public interface IMarshallableGeneric : IMarshallable 
{
  uint ClassId();
}

public interface IReader 
{
  void ReadI8(ref sbyte v);
  void ReadU8(ref byte v);
  void ReadI16(ref short v);
  void ReadU16(ref ushort v);
  void ReadI32(ref int v);
  void ReadU32(ref uint v);
  void ReadU64(ref ulong v);
  void ReadI64(ref long v);
  void ReadFloat(ref float v);
  void ReadBool(ref bool v);
  void ReadDouble(ref double v);
  void ReadString(ref string v);
  void ReadRaw(ref byte[] v, ref int vlen);
  int BeginContainer(); 
  void EndContainer(); 
}

public interface IWriter 
{
  void WriteI8(sbyte v);
  void WriteU8(byte v);
  void WriteI16(short v);
  void WriteU16(ushort v);
  void WriteI32(int v);
  void WriteU32(uint v);
  void WriteI64(long v);
  void WriteU64(ulong v);
  void WriteFloat(float v);
  void WriteBool(bool v);
  void WriteDouble(double v);
  void WriteString(string v);
  void BeginContainer(int len); 
  void EndContainer(); 
}

public static class Marshall 
{
  static public void Sync(SyncContext ctx, ref string v)
  {
    if(ctx.is_read)
      ctx.reader.ReadString(ref v);
    else
      ctx.writer.WriteString(v);
  }

  static public void Sync(SyncContext ctx, ref long v)
  {
    if(ctx.is_read)
      ctx.reader.ReadI64(ref v);
    else
      ctx.writer.WriteI64(v);
  }

  static public void Sync(SyncContext ctx, ref int v)
  {
    if(ctx.is_read)
      ctx.reader.ReadI32(ref v);
    else
      ctx.writer.WriteI32(v);
  }

  static public void Sync(SyncContext ctx, ref short v)
  {
    if(ctx.is_read)
      ctx.reader.ReadI16(ref v);
    else
      ctx.writer.WriteI16(v);
  }

  static public void Sync(SyncContext ctx, ref sbyte v)
  {
    if(ctx.is_read)
      ctx.reader.ReadI8(ref v);
    else
      ctx.writer.WriteI8(v);
  }

  static public void Sync(SyncContext ctx, ref ulong v)
  {
    if(ctx.is_read)
      ctx.reader.ReadU64(ref v);
    else
      ctx.writer.WriteU64(v);
  }

  static public void Sync(SyncContext ctx, ref ushort v)
  {
    if(ctx.is_read)
      ctx.reader.ReadU16(ref v);
    else
      ctx.writer.WriteU16(v);
  }

  static public void Sync(SyncContext ctx, ref uint v)
  {
    if(ctx.is_read)
      ctx.reader.ReadU32(ref v);
    else
      ctx.writer.WriteU32(v);
  }

  static public void Sync(SyncContext ctx, ref byte v)
  {
    if(ctx.is_read)
      ctx.reader.ReadU8(ref v);
    else
      ctx.writer.WriteU8(v);
  }

  static public void Sync(SyncContext ctx, ref bool v)
  {
    if(ctx.is_read)
      ctx.reader.ReadBool(ref v);
    else
      ctx.writer.WriteBool(v);
  }

  static public void Sync(SyncContext ctx, ref float v)
  {
    if(ctx.is_read)
      ctx.reader.ReadFloat(ref v);
    else
      ctx.writer.WriteFloat(v);
  }

  static public void Sync(SyncContext ctx, ref double v)
  {
    if(ctx.is_read)
      ctx.reader.ReadDouble(ref v);
    else
      ctx.writer.WriteDouble(v);
  }

  static int BeginArray<T>(SyncContext ctx, List<T> v)
  {
    if(ctx.is_read)
    {
      int size = ctx.reader.BeginContainer();

      if(v.Capacity < size) 
        v.Capacity = size;

      return size;
    }
    else
    {
      int size = v == null ? 0 : v.Count;
      ctx.writer.BeginContainer(size);
      return size;
    }
  }

  static void EndArray(SyncContext ctx, IList v)
  {
    if(ctx.is_read)
      ctx.reader.EndContainer();
    else
      ctx.writer.EndContainer();
  }

  static public void Sync(SyncContext ctx, List<string> v)
  {
    int size = BeginArray(ctx, v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? "" : v[i];
      Sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    EndArray(ctx, v);
  }

  static public void Sync(SyncContext ctx, List<long> v)
  {
    int size = BeginArray(ctx, v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(long) : v[i];
      Sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    EndArray(ctx, v);
  }

  static public void Sync(SyncContext ctx, List<int> v)
  {
    int size = BeginArray(ctx, v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(int) : v[i];
      Sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    EndArray(ctx, v);
  }

  static public void Sync(SyncContext ctx, List<short> v)
  {
    int size = BeginArray(ctx, v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(short) : v[i];
      Sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    EndArray(ctx, v);
  }

  static public void Sync(SyncContext ctx, List<sbyte> v)
  {
    int size = BeginArray(ctx, v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(sbyte) : v[i];
      Sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    EndArray(ctx, v);
  }

  static public void Sync(SyncContext ctx, List<ulong> v)
  {
    int size = BeginArray(ctx, v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(ulong) : v[i];
      Sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    EndArray(ctx, v);
  }

  static public void Sync(SyncContext ctx, List<uint> v)
  {
    int size = BeginArray(ctx, v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(uint) : v[i];
      Sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    EndArray(ctx, v);
  }

  static public void Sync(SyncContext ctx, List<ushort> v)
  {
    int size = BeginArray(ctx, v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(ushort) : v[i];
      Sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    EndArray(ctx, v);
  }

  static public void Sync(SyncContext ctx, List<byte> v)
  {
    int size = BeginArray(ctx, v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(byte) : v[i];
      Sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    EndArray(ctx, v);
  }

  static public void Sync(SyncContext ctx, List<bool> v)
  {
    int size = BeginArray(ctx, v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(bool) : v[i];
      Sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    EndArray(ctx, v);
  }

  static public void Sync(SyncContext ctx, List<float> v)
  {
    int size = BeginArray(ctx, v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(float) : v[i];
      Sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    EndArray(ctx, v);
  }

  static public void Sync(SyncContext ctx, List<double> v)
  {
    int size = BeginArray(ctx, v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(double) : v[i];
      Sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    EndArray(ctx, v);
  }
  
  static public void Sync<T>(SyncContext ctx, List<T> v) where T : IMarshallable, new()
  {
    int size = BeginArray(ctx, v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? new T() : v[i];
      Sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    EndArray(ctx, v);
  }

  static public void SyncGeneric(SyncContext ctx, ref IMarshallableGeneric v)
  {
    if(ctx.is_read)
    {
      ctx.reader.BeginContainer();

      uint clid = 0;
      ctx.reader.ReadU32(ref clid);
      
      //check for null
      if(clid != 0xFFFFFFFF)
      {
        v = ctx.factory.CreateById(clid);
        if(v == null) 
          throw new Error(ErrorCode.BAD_CLASS_ID, "Could not create object with class id: " + clid);

        v.Sync(ctx);
      }
      else
      {
        v = null;
      }
      ctx.reader.EndContainer();
    }
    else
    {
      if(v == null)
      {
        ctx.writer.BeginContainer(1/*class id*/);
        ctx.writer.WriteU32(0xFFFFFFFF);
        ctx.writer.EndContainer();
      }
      else
      {
        ctx.writer.BeginContainer(v.GetFieldsNum() + 1/*class id*/);
        ctx.writer.WriteU32(v.ClassId());
        v.Sync(ctx);
        ctx.writer.EndContainer();
      }
    }
  }

  static public void SyncGeneric<T>(SyncContext ctx, List<T> v, System.Action<IMarshallableGeneric> item_cb = null) where T : IMarshallableGeneric
  {
    int size = BeginArray(ctx, v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = (IMarshallableGeneric)(ctx.is_read ? (IMarshallable)null : v[i]);
      SyncGeneric(ctx, ref tmp);
      item_cb?.Invoke(tmp);
      if(ctx.is_read)
        v.Add((T)tmp);
    }
    EndArray(ctx, v);
  }

  static public void Sync<T>(SyncContext ctx, ref T v) where T : IMarshallable
  {
    if(ctx.is_read)
    {
      ctx.reader.BeginContainer();
      v.Sync(ctx);
      ctx.reader.EndContainer();  
    }
    else
    {
      ctx.writer.BeginContainer(v.GetFieldsNum());
      v.Sync(ctx);
      ctx.writer.EndContainer();
    }
  }
}

public class MsgPackDataWriter : IWriter 
{
  MsgPackWriter io;
  Stack<int> space = new Stack<int>();

  public MsgPackDataWriter(Stream stream) 
  {
    Reset(stream);
  }

  public void Reset(Stream stream)
  {
    io = new MsgPackWriter(stream);
    space.Clear();
    space.Push(1);
  }

  void DecSpace() 
  {
    if(space.Count == 0)
      throw new Error(ErrorCode.NO_RESERVED_SPACE);
    
    int left = space.Pop();
    left--;
    if(left < 0)
      throw new Error(ErrorCode.NO_RESERVED_SPACE);
    
    space.Push(left);
  }

  public void BeginContainer(int size) 
  {
    DecSpace();
    space.Push(size);
    io.WriteArrayHeader(size);
  }

  public void EndContainer() 
  {
    if(space.Count <= 1)
      throw new Error(ErrorCode.NO_RESERVED_SPACE);
    
    int left = space.Pop();
    if(left != 0)
      throw new Error(ErrorCode.DANGLING_DATA, "Left items: " + left);
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
  MsgPackReader io;

  struct StructPos
  {
    public uint max;
    public uint curr;

    public StructPos(uint length)
    {
      curr = 0;
      max = length - 1;
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
    io = new MsgPackReader(stream);
  }

  public void SetPos(long pos)
  {
    stream.Position = pos;
    structs_pos.Clear();
  }

  void Next()
  {
    if(!SpacePositionValid())
      throw new Error(ErrorCode.INVALID_POS);

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
        case TypePrefixes.Float:
          v = io.ValueFloat;
          break;
        case TypePrefixes.Double:
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
        case TypePrefixes.Float:
          v = (double)io.ValueFloat;
          break;
        case TypePrefixes.Double:
          v = io.ValueDouble;
          break;
        case TypePrefixes.UInt64:
          v = (double)io.ValueUnsigned64;
          break;
        case TypePrefixes.Int64:
          v = (double)io.ValueSigned64;    
          break;
        default:
          throw new Error(ErrorCode.TYPE_MISMATCH, "Got type: " + io.Type); 
      }
    }
  }

  public void ReadRaw(ref byte[] v, ref int vlen) 
  {
    Next();

    if(!io.IsRaw()) 
      throw new Error(ErrorCode.TYPE_MISMATCH, "Got type: " + io.Type); 

    vlen = (int)io.Length;
    if(v.Length < vlen)
      Array.Resize(ref v, vlen);
    io.ReadValueRaw(v, 0, vlen);
  }

  public void ReadString(ref string v) 
  {
    Next();

    if(!io.IsRaw()) 
      throw new Error(ErrorCode.TYPE_MISMATCH, "Got type: " + io.Type); 

    //TODO: use shared buffer for strings loading
    byte[] strval = new byte[io.Length];
    io.ReadValueRaw(strval, 0, strval.Length);
    v = System.Text.Encoding.UTF8.GetString(strval);
  }

  public int BeginContainer() 
  {
    if(!SpacePositionValid())
      throw new Error(ErrorCode.INVALID_POS);

    if(!io.Read()) 
      throw new Error(ErrorCode.IO_READ);

    if(!io.IsArray())
      throw new Error(ErrorCode.TYPE_MISMATCH, "Got type: " + io.Type); 

    uint len = io.Length;
    structs_pos.Push(new StructPos(len));
    return (int)len;
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
      //NOTE: if value is a raw string we need to read all of its data
      if(io.IsRaw())
        io.ReadRawString();
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
    return (int)ap.max - (int)ap.curr;
  }

  bool SpacePositionValid()
  {
    return SpaceLeft() >= 0;
  }

  void MoveSpaceCursor()
  {
    if(structs_pos.Count > 0)
    {
      var ap = structs_pos.Pop();
      ap.curr++;
      structs_pos.Push(ap);
    }
  }
}

} // namespace marshall
} // namespace bhl
