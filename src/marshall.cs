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
}

public class Error : Exception
{
  public ErrorCode err;

  public Error(ErrorCode err)
  {
    this.err = err;
  }
}

public struct SyncContext
{
  public delegate IMarshallable Factory(uint id); 

  public Factory CreateById;

  public bool is_read;
  public IReader reader;
  public IWriter writer;
  public uint opts;

  public static SyncContext NewForRead(IReader reader, Factory factory)
  {
    var ctx = new SyncContext() {
      is_read = true,
      reader = reader,
      writer = null,
      CreateById = factory
    };
    return ctx;
  }

  public static SyncContext NewForWrite(IWriter writer)
  {
    var ctx = new SyncContext() {
      is_read = false,
      reader = null,
      writer = writer
    };
    return ctx;
  }
}

public interface IMarshallable 
{
  uint CLASS_ID();
  int GetFieldsNum();
  void Sync(SyncContext ctx);
}

public interface IReader 
{
  ErrorCode ReadI8(ref sbyte v);
  ErrorCode ReadU8(ref byte v);
  ErrorCode ReadI16(ref short v);
  ErrorCode ReadU16(ref ushort v);
  ErrorCode ReadI32(ref int v);
  ErrorCode ReadU32(ref uint v);
  ErrorCode ReadU64(ref ulong v);
  ErrorCode ReadI64(ref long v);
  ErrorCode ReadFloat(ref float v);
  ErrorCode ReadBool(ref bool v);
  ErrorCode ReadDouble(ref double v);
  ErrorCode ReadString(ref string v);
  ErrorCode ReadRaw(ref byte[] v, ref int vlen);
  ErrorCode BeginArray(); 
  ErrorCode GetArraySize(ref int v);
  ErrorCode EndArray(); 
}

public interface IWriter 
{
  ErrorCode WriteI8(sbyte v);
  ErrorCode WriteU8(byte v);
  ErrorCode WriteI16(short v);
  ErrorCode WriteU16(ushort v);
  ErrorCode WriteI32(int v);
  ErrorCode WriteU32(uint v);
  ErrorCode WriteI64(long v);
  ErrorCode WriteU64(ulong v);
  ErrorCode WriteFloat(float v);
  ErrorCode WriteBool(bool v);
  ErrorCode WriteDouble(double v);
  ErrorCode WriteString(string v);
  ErrorCode BeginArray(int len); 
  ErrorCode EndArray(); 
  ErrorCode End(); 
}

public static class Syncer 
{
  public delegate void LogCb(string text);
  static public LogCb LogError = DefaultLog;
  static public LogCb LogWarn = DefaultLog;
  static public LogCb LogDebug = DefaultLog;

  static void DefaultLog(string s)
  {}

  static void Ensure(ErrorCode err)
  {
    if(err != ErrorCode.SUCCESS)
      throw new Error(err);
  }

  static public void Sync(SyncContext ctx, ref string v)
  {
    if(ctx.is_read)
      Ensure(ctx.reader.ReadString(ref v));
    else
      Ensure(ctx.writer.WriteString(v));
  }

  static public void Sync(SyncContext ctx, ref long v)
  {
    if(ctx.is_read)
      Ensure(ctx.reader.ReadI64(ref v));
    else
      Ensure(ctx.writer.WriteI64(v));
  }

  static public void Sync(SyncContext ctx, ref int v)
  {
    if(ctx.is_read)
      Ensure(ctx.reader.ReadI32(ref v));
    else
      Ensure(ctx.writer.WriteI32(v));
  }

  static public void Sync(SyncContext ctx, ref short v)
  {
    if(ctx.is_read)
      Ensure(ctx.reader.ReadI16(ref v));
    else
      Ensure(ctx.writer.WriteI16(v));
  }

  static public void Sync(SyncContext ctx, ref sbyte v)
  {
    if(ctx.is_read)
      Ensure(ctx.reader.ReadI8(ref v));
    else
      Ensure(ctx.writer.WriteI8(v));
  }

  static public void Sync(SyncContext ctx, ref ulong v)
  {
    if(ctx.is_read)
      Ensure(ctx.reader.ReadU64(ref v));
    else
      Ensure(ctx.writer.WriteU64(v));
  }

  static public void Sync(SyncContext ctx, ref ushort v)
  {
    if(ctx.is_read)
      Ensure(ctx.reader.ReadU16(ref v));
    else
      Ensure(ctx.writer.WriteU16(v));
  }

  static public void Sync(SyncContext ctx, ref uint v)
  {
    if(ctx.is_read)
      Ensure(ctx.reader.ReadU32(ref v));
    else
      Ensure(ctx.writer.WriteU32(v));
  }

  static public void Sync(SyncContext ctx, ref byte v)
  {
    if(ctx.is_read)
      Ensure(ctx.reader.ReadU8(ref v));
    else
      Ensure(ctx.writer.WriteU8(v));
  }

  static public void Sync(SyncContext ctx, ref bool v)
  {
    if(ctx.is_read)
      Ensure(ctx.reader.ReadBool(ref v));
    else
      Ensure(ctx.writer.WriteBool(v));
  }

  static public void Sync(SyncContext ctx, ref float v)
  {
    if(ctx.is_read)
      Ensure(ctx.reader.ReadFloat(ref v));
    else
      Ensure(ctx.writer.WriteFloat(v));
  }

  static public void Sync(SyncContext ctx, ref double v)
  {
    if(ctx.is_read)
      Ensure(ctx.reader.ReadDouble(ref v));
    else
      Ensure(ctx.writer.WriteDouble(v));
  }

  static int BeginArray<T>(SyncContext ctx, List<T> v)
  {
    if(ctx.is_read)
    {
      Ensure(ctx.reader.BeginArray());

      int size = 0;
      Ensure(ctx.reader.GetArraySize(ref size));

      if(v.Capacity < size) 
        v.Capacity = size;

      return size;
    }
    else
    {
      int size = v == null ? 0 : v.Count;
      Ensure(ctx.writer.BeginArray(size));
      return size;
    }
  }

  static void EndArray(SyncContext ctx, IList v)
  {
    if(ctx.is_read)
      Ensure(ctx.reader.EndArray());
    else
      Ensure(ctx.writer.EndArray());
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

  static public void SyncGeneric(SyncContext ctx, ref IMarshallable v)
  {
    if(ctx.is_read)
    {
      Ensure(ctx.reader.BeginArray());

      uint clid = 0;
      Ensure(ctx.reader.ReadU32(ref clid));
      
      v = ctx.CreateById(clid);
      if(v == null) 
      {
        LogError("Could not create struct: " + clid);
        Ensure(ErrorCode.TYPE_MISMATCH);
      }

      v.Sync(ctx);
      Ensure(ctx.reader.EndArray());
    }
    else
    {
      Ensure(ctx.writer.BeginArray(v.GetFieldsNum() + 1/*class id*/));
      Ensure(ctx.writer.WriteU32(v.CLASS_ID()));
      v.Sync(ctx);
      Ensure(ctx.writer.EndArray());
    }
  }

  static public void SyncGeneric<T>(SyncContext ctx, List<T> v) where T : IMarshallable
  {
    int size = BeginArray(ctx, v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = (IMarshallable)(ctx.is_read ? (IMarshallable)null : v[i]);
      SyncGeneric(ctx, ref tmp);
      if(ctx.is_read)
        v.Add((T)tmp);
    }
    EndArray(ctx, v);
  }

  static public void Sync<T>(SyncContext ctx, ref T v) where T : IMarshallable
  {
    if(ctx.is_read)
    {
      Syncer.Ensure(ctx.reader.BeginArray());
      v.Sync(ctx);
      Syncer.Ensure(ctx.reader.EndArray());  
    }
    else
    {
      Syncer.Ensure(ctx.writer.BeginArray(v.GetFieldsNum()));
      v.Sync(ctx);
      Syncer.Ensure(ctx.writer.EndArray());
    }
  }
}

public class MsgPackDataWriter : IWriter 
{
  Stream stream;
  MsgPackWriter io;
  Stack<int> space = new Stack<int>();

  public MsgPackDataWriter(Stream _stream) 
  {
    reset(_stream);
  }

  public void reset(Stream _stream)
  {
    stream = _stream;
    io = new MsgPackWriter(stream);
    space.Clear();
    space.Push(1);
  }

  ErrorCode decSpace() 
  {
    if(space.Count == 0)
      return ErrorCode.NO_RESERVED_SPACE;
    
    int left = space.Pop();
    left--;
    if(left < 0)
      return ErrorCode.NO_RESERVED_SPACE;
    
    space.Push(left);
    return ErrorCode.SUCCESS;    
  }

  public ErrorCode BeginArray(int size) 
  {
    ErrorCode err = decSpace();
    if(err != ErrorCode.SUCCESS)
      return err;
    
    space.Push(size);
    io.WriteArrayHeader(size);
    return ErrorCode.SUCCESS;
  }

  public ErrorCode EndArray() 
  {
    if(space.Count <= 1)
      return ErrorCode.NO_RESERVED_SPACE;
    
    int left = space.Pop();
    if(left != 0)
      return ErrorCode.DANGLING_DATA;
    
    return ErrorCode.SUCCESS;
  }
  
  public ErrorCode End() 
  {
    if(space.Count != 1)
      return ErrorCode.NO_RESERVED_SPACE;
    
    return ErrorCode.SUCCESS;
  }

  public ErrorCode WriteI8(sbyte v) 
  {
    io.Write(v);
    return decSpace();
  }
 
  public ErrorCode WriteU8(byte v) 
  {
    io.Write(v);
    return decSpace();
  }
  
  public ErrorCode WriteI16(short v) 
  {
    io.Write(v);
    return decSpace();
  }

  public ErrorCode WriteU16(ushort v) 
  {
    io.Write(v);
    return decSpace();
  }

  public ErrorCode WriteI32(int v) 
  {
    io.Write(v);
    return decSpace();
  }

  public ErrorCode WriteU32(uint v) 
  {
    io.Write(v);
    return decSpace();
  }

  public ErrorCode WriteU64(ulong v) 
  {
    io.Write(v);
    return decSpace();
  }

  public ErrorCode WriteI64(long v) 
  {
    io.Write(v);
    return decSpace();
  }

  public ErrorCode WriteBool(bool v) 
  {
    io.Write(v ? 1 : 0);
    return decSpace();
  }

  public ErrorCode WriteFloat(float v) 
  {
    io.Write(v);
    return decSpace();
  }

  public ErrorCode WriteDouble(double v) 
  {
    io.Write(v);
    return decSpace();
  }

  public ErrorCode WriteString(string v) 
  {
    if(v == null) 
      io.Write("");
    else
      io.Write(v);
    
    return decSpace();
  }

  public ErrorCode WriteNil()
  {
    io.WriteNil();
    
    return decSpace();
  }
}

public class MsgPackDataReader : IReader 
{
  Stream stream;
  MsgPackReader io;

  struct ArrayPosition
  {
    public uint max;
    public uint curr;

    public ArrayPosition(uint length)
    {
      curr = 0;
      max = length - 1;
    }
  }

  Stack<ArrayPosition> stack = new Stack<ArrayPosition>();
  
  public MsgPackDataReader(Stream _stream) 
  { 
    reset(_stream);
  }

  public void reset(Stream _stream)
  {
    stream = _stream;
    stack.Clear();
    io = new MsgPackReader(stream);
  }

  public void setPos(long pos)
  {
    stream.Position = pos;
    stack.Clear();
  }

  int nextInt(ref ErrorCode err) 
  {
    if(!ArrayPositionValid())
    {
      err = ErrorCode.INVALID_POS;
      return 0;
    }

    if(!io.Read()) 
    {
      err = ErrorCode.IO_READ;
      return 0;
    }
    
    MoveNext();
    
    if(io.IsSigned())
      return io.ValueSigned;
    else if(io.IsUnsigned())
      return (int)io.ValueUnsigned;
    else
    {
      Syncer.LogWarn("Got type: " + io.Type);
      err = ErrorCode.TYPE_MISMATCH; 
      return 0;
    }
  }
   
  uint nextUint(ref ErrorCode err) 
  {
    if(!ArrayPositionValid())
    {
      err = ErrorCode.INVALID_POS;
      return 0;
    }

    if(!io.Read()) 
    {
      err = ErrorCode.IO_READ;
      return 0;
    }
    
    MoveNext();
    
    if(io.IsUnsigned())
      return io.ValueUnsigned;
    else if(io.IsSigned())
      return (uint)io.ValueSigned;
    else
    {
      Syncer.LogWarn("Got type: " + io.Type);
      err = ErrorCode.TYPE_MISMATCH; 
      return 0;
    }
  }

  bool nextBool(ref ErrorCode err) 
  {
    if(!ArrayPositionValid())
    {
      err = ErrorCode.INVALID_POS;
      return false;
    }

    if(!io.Read()) 
    {
      err = ErrorCode.IO_READ;
      return false;
    }
    
    MoveNext();
    
    if(io.IsUnsigned())
      return io.ValueUnsigned != 0;
    else if(io.IsSigned())
      return (uint)io.ValueSigned != 0;
    else if(io.IsBoolean())
      return (bool)io.ValueBoolean;
    else
    {
      Syncer.LogWarn("Got type: " + io.Type);
      err = ErrorCode.TYPE_MISMATCH; 
      return false;
    }
  }

  ulong nextUint64(ref ErrorCode err) 
  {
    if(!ArrayPositionValid())
    {
      err = ErrorCode.INVALID_POS;
      return 0;
    }

    if(!io.Read()) 
    {
      err = ErrorCode.IO_READ;
      return 0;
    }
    
    MoveNext();

    if(io.IsUnsigned())
      return io.ValueUnsigned;
    else if(io.IsSigned())
      return (ulong)io.ValueSigned;
    else if(io.IsUnsigned64())
      return io.ValueUnsigned64;
    else if(io.IsSigned64())
      return (ulong)io.ValueSigned64;
    else
    {
      Syncer.LogWarn("Got type: " + io.Type);
      err = ErrorCode.TYPE_MISMATCH; 
      return 0;
    }
  }

  long nextInt64(ref ErrorCode err) 
  {
    if(!ArrayPositionValid())
    {
      err = ErrorCode.INVALID_POS;
      return 0;
    }

    if(!io.Read()) 
    {
      err = ErrorCode.IO_READ;
      return 0;
    }
    
    MoveNext();

    if(io.IsUnsigned())
      return (long)io.ValueUnsigned;
    else if(io.IsSigned())
      return io.ValueSigned;
    else if(io.IsUnsigned64())
      return (long)io.ValueUnsigned64;
    else if(io.IsSigned64())
      return io.ValueSigned64;
    else
    {
      Syncer.LogWarn("Got type: " + io.Type);
      err = ErrorCode.TYPE_MISMATCH; 
      return 0;
    }
  }
  
  public ErrorCode ReadI8(ref sbyte v) 
  {
    ErrorCode err = 0;
    v = (sbyte) nextInt(ref err);
    return err;
  }
  
  public ErrorCode ReadI16(ref short v) 
  {
    ErrorCode err = 0;
    v = (short) nextInt(ref err);
    return err;
  }
  
  public ErrorCode ReadI32(ref int v) 
  {
    ErrorCode err = 0;
    v = nextInt(ref err);
    return err;
  }
  
  public ErrorCode ReadU8(ref byte v) 
  {
    ErrorCode err = 0;
    v = (byte) nextUint(ref err);
    return err;
  }
  
  public ErrorCode ReadU16(ref ushort v) 
  {
    ErrorCode err = 0;
    v = (ushort) nextUint(ref err);
    return err;
  }
  
  public ErrorCode ReadU32(ref uint v) 
  {
    ErrorCode err = 0;
    v = nextUint(ref err);
    return err;
  }

  public ErrorCode ReadU64(ref ulong v) 
  {
    ErrorCode err = 0;
    v = nextUint64(ref err);
    return err;
  }

  public ErrorCode ReadI64(ref long v) 
  {
    ErrorCode err = 0;
    v = nextInt64(ref err);
    return err;
  }

  public ErrorCode ReadBool(ref bool v) 
  {
    ErrorCode err = 0;
    v = nextBool(ref err);
    return err;
  }

  public ErrorCode ReadFloat(ref float v) 
  {
    if(!ArrayPositionValid())
      return ErrorCode.INVALID_POS;

    if(!io.Read()) 
    {
      return ErrorCode.IO_READ;
    }

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
          //  MetaHelper.LogWarn("Double -> Float bad conversion: " + tmp);
          //  return MetaIoError.TYPE_DONT_MATCH; 
          //}
          v = (float) tmp;
          break;
        default:
          Syncer.LogWarn("Got type: " + io.Type);
          return ErrorCode.TYPE_MISMATCH; 
      }
    }
    MoveNext();
    return ErrorCode.SUCCESS;
  }

  public ErrorCode ReadDouble(ref double v) 
  {
    if(!ArrayPositionValid())
      return ErrorCode.INVALID_POS;

    if(!io.Read()) 
    {
      return ErrorCode.IO_READ;
    }

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
          v = (double) io.ValueFloat;
          break;
        case TypePrefixes.Double:
          v = io.ValueDouble;
          break;
        case TypePrefixes.UInt64:
          v = (double) io.ValueUnsigned64;
          break;
        case TypePrefixes.Int64:
          v = (double) io.ValueSigned64;    
          break;
        default:
          Syncer.LogWarn("Got type: " + io.Type);
          return ErrorCode.TYPE_MISMATCH; 
      }
    }
    MoveNext();
    return ErrorCode.SUCCESS;
  }

  public ErrorCode ReadRaw(ref byte[] v, ref int vlen) 
  {
    if(!ArrayPositionValid())
      return ErrorCode.INVALID_POS;

    if(!io.Read()) 
      return ErrorCode.IO_READ;

    if(!io.IsRaw()) 
    {
      Syncer.LogWarn("Got type: " + io.Type);
      return ErrorCode.TYPE_MISMATCH;
    }

    vlen = (int)io.Length;
    if(v.Length < vlen)
      Array.Resize(ref v, vlen);
    io.ReadValueRaw(v, 0, vlen);
    MoveNext();
    return ErrorCode.SUCCESS;
  }

  public ErrorCode ReadString(ref string v) 
  {
    if(!ArrayPositionValid())
      return ErrorCode.INVALID_POS;

    if(!io.Read()) 
      return ErrorCode.IO_READ;

    if(!io.IsRaw()) 
    {
      Syncer.LogWarn("Got type: " + io.Type);
      return ErrorCode.TYPE_MISMATCH;
    }

    //TODO: use shared buffer for strings loading
    byte[] strval = new byte[io.Length];
    io.ReadValueRaw(strval, 0, strval.Length);
    v = System.Text.Encoding.UTF8.GetString(strval);
    MoveNext();
    return ErrorCode.SUCCESS;
  }

  public ErrorCode BeginArray() 
  {
    if(!ArrayPositionValid())
      return ErrorCode.INVALID_POS;

    if(!io.Read())
      return ErrorCode.IO_READ;

    if(!io.IsArray())
    {
      Syncer.LogWarn("Got type: " + io.Type);
      return ErrorCode.TYPE_MISMATCH;
    }

    ArrayPosition ap = new ArrayPosition(io.Length);
    stack.Push(ap);
    return ErrorCode.SUCCESS;
  } 

  public ErrorCode GetArraySize(ref int v) 
  {
    if(!io.IsArray())
    {
      Syncer.LogWarn("Got type: " + io.Type);
      return ErrorCode.TYPE_MISMATCH;
    }

    v = (int) io.Length;
    return ErrorCode.SUCCESS;
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
    while(ArrayEntriesLeft() > -1)
    {
      SkipField();
      MoveNext();
    }
  }

  public ErrorCode EndArray() 
  {
    SkipTrailingFields();
    stack.Pop();
    MoveNext();
    return ErrorCode.SUCCESS;
  }

  int ArrayEntriesLeft()
  {
    if(stack.Count == 0)
      return 0;
    ArrayPosition ap = stack.Peek();
    return (int)ap.max - (int)ap.curr;
  }

  bool ArrayPositionValid()
  {
    return ArrayEntriesLeft() >= 0;
  }

  void MoveNext()
  {
    if(stack.Count > 0)
    {
      ArrayPosition ap = stack.Pop();
      ap.curr++;
      stack.Push(ap);
    }
  }
}

} // namespace marshall
} // namespace bhl
