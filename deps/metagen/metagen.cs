using MsgPack;
using System.IO;
using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

//NOTE: don't change existing values
public enum MetaIoError
{
  NONE                   = -1,
  SUCCESS                = 0,
  NO_SPACE_LEFT_IN_ARRAY = 1,
  HAS_LEFT_SPACE         = 2,
  ARRAY_STACK_IS_EMPTY   = 3,
  FAIL_READ              = 4,
  TYPE_DONT_MATCH        = 5,
  DATA_MISSING           = 7,
  GENERIC                = 10,
}

public class MetaException : Exception
{
  public MetaIoError err;

  public MetaException(MetaIoError err)
  {
    this.err = err;
  }
}

public struct MetaSyncContext
{
  public bool is_read;
  public IDataReader reader;
  public IDataWriter writer;
  public uint opts;

  public static MetaSyncContext NewForRead(IDataReader reader, uint opts = 0)
  {
    var ctx = new MetaSyncContext() {
      is_read = true,
      reader = reader,
      writer = null,
      opts = opts
    };
    return ctx;
  }

  public static MetaSyncContext NewForWrite(IDataWriter writer, uint opts = 0)
  {
    var ctx = new MetaSyncContext() {
      is_read = false,
      reader = null,
      writer = writer,
      opts = opts
    };
    return ctx;
  }
}

public interface IMetaStruct 
{
  uint CLASS_ID();

  int getFieldsCount();

  void syncFields(MetaSyncContext ctx);

  void copy(IMetaStruct source);
  IMetaStruct clone();

  void reset();
}

public abstract class BaseMetaStruct : IMetaStruct
{
  public virtual uint CLASS_ID() { return 0; }

  public virtual int getFieldsCount() { return 0; }

  public virtual void copy(IMetaStruct source) {}
  public virtual IMetaStruct clone() { return null; }

  public virtual void reset() {}

  public virtual void sync(MetaSyncContext ctx)
  {
    if(ctx.is_read)
    {
      MetaHelper.ensure(ctx.reader.BeginArray());
      syncFields(ctx);
      MetaHelper.ensure(ctx.reader.EndArray());  
    }
    else
    {
      MetaHelper.ensure(ctx.writer.BeginArray(getFieldsCount()));
      syncFields(ctx);
      MetaHelper.ensure(ctx.writer.EndArray());
    }
  }

  public virtual void syncFields(MetaSyncContext ctx) {}
}

public interface IRpcError
{
  bool isOk();
  int getServerError();
}

public interface IRpc
{
  int getCode();
  IMetaStruct getRequest();
  IMetaStruct getResponse();
  IRpcError getError();
  void setError(IRpcError err);
}

public static class MetagenExtensions
{
  static public bool isDone(this IRpc r)
  {
    return r.getError() != null;
  }

  static public bool isSuccess(this IRpc r)
  {
    return r.getError() == null || r.getError().isOk();
  }

  static public bool isFailed(this IRpc r)
  {
    return r.isDone() && !r.isSuccess();
  }

  static public void reset(this IRpc r) 
  {
    r.setError(null);
    r.getRequest().reset();
    r.getResponse().reset();
  }
}

public interface IDataReader 
{
  MetaIoError ReadI8(ref sbyte v);
  MetaIoError ReadU8(ref byte v);
  MetaIoError ReadI16(ref short v);
  MetaIoError ReadU16(ref ushort v);
  MetaIoError ReadI32(ref int v);
  MetaIoError ReadU32(ref uint v);
  MetaIoError ReadU64(ref ulong v);
  MetaIoError ReadI64(ref long v);
  MetaIoError ReadFloat(ref float v);
  MetaIoError ReadBool(ref bool v);
  MetaIoError ReadDouble(ref double v);
  MetaIoError ReadString(ref string v);
  MetaIoError ReadRaw(ref byte[] v, ref int vlen);
  MetaIoError BeginArray(); 
  MetaIoError GetArraySize(ref int v);
  MetaIoError EndArray(); 
}

public interface IDataWriter 
{
  MetaIoError WriteI8(sbyte v);
  MetaIoError WriteU8(byte v);
  MetaIoError WriteI16(short v);
  MetaIoError WriteU16(ushort v);
  MetaIoError WriteI32(int v);
  MetaIoError WriteU32(uint v);
  MetaIoError WriteI64(long v);
  MetaIoError WriteU64(ulong v);
  MetaIoError WriteFloat(float v);
  MetaIoError WriteBool(bool v);
  MetaIoError WriteDouble(double v);
  MetaIoError WriteString(string v);
  MetaIoError BeginArray(int len); 
  MetaIoError EndArray(); 
  MetaIoError End(); 
}

public static class MetaHelper 
{
  public delegate IMetaStruct CreateByIdCb(int id); 
  static public CreateByIdCb CreateById;

  public delegate string L_TCb(string text);
  static public L_TCb L_T;

  public delegate string L_FromListCb(IList<string> list);
  static public L_FromListCb L_FromList;

  public delegate string L_PFromListCb(IList<string> list, double force_n = double.NaN);
  static public L_PFromListCb L_Pluralize;

  public delegate bool L_PListCheckCb(IList<string> list, string plural_mark);
  static public L_PListCheckCb L_IsPlural;

  public delegate void LogWarnCb(string text);
  static public LogWarnCb LogWarn = DefaultWarn;

  public delegate void LogErrorCb(string text);
  static public LogErrorCb LogError = DefaultError;

  public delegate void LogDebugCb(string text);
  static public LogDebugCb LogDebug = DefaultDebug;

  static void DefaultError(string s)
  {
    throw new Exception(s);
  }

  static void DefaultWarn(string s)
  {}

  static void DefaultDebug(string s)
  {}

  static public void ensure(MetaIoError err)
  {
    if(err != MetaIoError.SUCCESS)
      throw new MetaException(err);
  }

  static public void sync(MetaSyncContext ctx, ref string v)
  {
    if(ctx.is_read)
      ensure(ctx.reader.ReadString(ref v));
    else
      ensure(ctx.writer.WriteString(v));
  }

  static public void sync(MetaSyncContext ctx, ref long v)
  {
    if(ctx.is_read)
      ensure(ctx.reader.ReadI64(ref v));
    else
      ensure(ctx.writer.WriteI64(v));
  }

  static public void sync(MetaSyncContext ctx, ref int v)
  {
    if(ctx.is_read)
      ensure(ctx.reader.ReadI32(ref v));
    else
      ensure(ctx.writer.WriteI32(v));
  }

  static public void sync(MetaSyncContext ctx, ref short v)
  {
    if(ctx.is_read)
      ensure(ctx.reader.ReadI16(ref v));
    else
      ensure(ctx.writer.WriteI16(v));
  }

  static public void sync(MetaSyncContext ctx, ref sbyte v)
  {
    if(ctx.is_read)
      ensure(ctx.reader.ReadI8(ref v));
    else
      ensure(ctx.writer.WriteI8(v));
  }

  static public void sync(MetaSyncContext ctx, ref ulong v)
  {
    if(ctx.is_read)
      ensure(ctx.reader.ReadU64(ref v));
    else
      ensure(ctx.writer.WriteU64(v));
  }

  static public void sync(MetaSyncContext ctx, ref ushort v)
  {
    if(ctx.is_read)
      ensure(ctx.reader.ReadU16(ref v));
    else
      ensure(ctx.writer.WriteU16(v));
  }

  static public void sync(MetaSyncContext ctx, ref uint v)
  {
    if(ctx.is_read)
      ensure(ctx.reader.ReadU32(ref v));
    else
      ensure(ctx.writer.WriteU32(v));
  }

  static public void sync(MetaSyncContext ctx, ref byte v)
  {
    if(ctx.is_read)
      ensure(ctx.reader.ReadU8(ref v));
    else
      ensure(ctx.writer.WriteU8(v));
  }

  static public void sync(MetaSyncContext ctx, ref bool v)
  {
    if(ctx.is_read)
      ensure(ctx.reader.ReadBool(ref v));
    else
      ensure(ctx.writer.WriteBool(v));
  }

  static public void sync(MetaSyncContext ctx, ref float v)
  {
    if(ctx.is_read)
      ensure(ctx.reader.ReadFloat(ref v));
    else
      ensure(ctx.writer.WriteFloat(v));
  }

  static public void sync(MetaSyncContext ctx, ref double v)
  {
    if(ctx.is_read)
      ensure(ctx.reader.ReadDouble(ref v));
    else
      ensure(ctx.writer.WriteDouble(v));
  }

  static int syncBeginArray<T>(MetaSyncContext ctx, ref List<T> v)
  {
    if(ctx.is_read)
    {
      ensure(ctx.reader.BeginArray());

      int size = 0;
      ensure(ctx.reader.GetArraySize(ref size));

      if(v.Capacity < size) 
        v.Capacity = size;

      return size;
    }
    else
    {
      int size = v == null ? 0 : v.Count;
      ensure(ctx.writer.BeginArray(size));
      return size;
    }
  }

  static void syncEndArray<T>(MetaSyncContext ctx, ref List<T> v)
  {
    if(ctx.is_read)
      ensure(ctx.reader.EndArray());
    else
      ensure(ctx.writer.EndArray());
  }

  static public void sync(MetaSyncContext ctx, ref List<string> v)
  {
    int size = syncBeginArray(ctx, ref v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? "" : v[i];
      sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    syncEndArray(ctx, ref v);
  }

  static public void sync(MetaSyncContext ctx, ref List<long> v)
  {
    int size = syncBeginArray(ctx, ref v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(long) : v[i];
      sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    syncEndArray(ctx, ref v);
  }

  static public void sync(MetaSyncContext ctx, ref List<int> v)
  {
    int size = syncBeginArray(ctx, ref v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(int) : v[i];
      sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    syncEndArray(ctx, ref v);
  }

  static public void sync(MetaSyncContext ctx, ref List<short> v)
  {
    int size = syncBeginArray(ctx, ref v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(short) : v[i];
      sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    syncEndArray(ctx, ref v);
  }

  static public void sync(MetaSyncContext ctx, ref List<sbyte> v)
  {
    int size = syncBeginArray(ctx, ref v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(sbyte) : v[i];
      sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    syncEndArray(ctx, ref v);
  }

  static public void sync(MetaSyncContext ctx, ref List<ulong> v)
  {
    int size = syncBeginArray(ctx, ref v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(ulong) : v[i];
      sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    syncEndArray(ctx, ref v);
  }

  static public void sync(MetaSyncContext ctx, ref List<uint> v)
  {
    int size = syncBeginArray(ctx, ref v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(uint) : v[i];
      sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    syncEndArray(ctx, ref v);
  }

  static public void sync(MetaSyncContext ctx, ref List<ushort> v)
  {
    int size = syncBeginArray(ctx, ref v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(ushort) : v[i];
      sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    syncEndArray(ctx, ref v);
  }

  static public void sync(MetaSyncContext ctx, ref List<byte> v)
  {
    int size = syncBeginArray(ctx, ref v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(byte) : v[i];
      sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    syncEndArray(ctx, ref v);
  }

  static public void sync(MetaSyncContext ctx, ref List<bool> v)
  {
    int size = syncBeginArray(ctx, ref v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(bool) : v[i];
      sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    syncEndArray(ctx, ref v);
  }

  static public void sync(MetaSyncContext ctx, ref List<float> v)
  {
    int size = syncBeginArray(ctx, ref v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(float) : v[i];
      sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    syncEndArray(ctx, ref v);
  }

  static public void sync(MetaSyncContext ctx, ref List<double> v)
  {
    int size = syncBeginArray(ctx, ref v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? default(double) : v[i];
      sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    syncEndArray(ctx, ref v);
  }
  
  static public void sync<T>(MetaSyncContext ctx, ref List<T> v) where T : IMetaStruct, new()
  {
    int size = syncBeginArray(ctx, ref v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? new T() : v[i];
      sync(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    syncEndArray(ctx, ref v);
  }

  static public void syncVirtual<T>(MetaSyncContext ctx, ref T v) where T : IMetaStruct
  {
    if(ctx.is_read)
    {
      ensure(ctx.reader.BeginArray());

      uint clid = 0;
      ensure(ctx.reader.ReadU32(ref clid));
      
      //TODO: why CreateById uses int ? 
      v = (T)CreateById((int)clid);
      if(v == null) 
      {
        LogError("Could not create struct: " + clid);
        ensure(MetaIoError.TYPE_DONT_MATCH);
      }

      v.syncFields(ctx);
      ensure(ctx.reader.EndArray());
    }
    else
    {
      ensure(ctx.writer.BeginArray(v.getFieldsCount() + 1));
      ensure(ctx.writer.WriteU32(v.CLASS_ID()));
      v.syncFields(ctx);
      ensure(ctx.writer.EndArray());
    }
  }

  static public void syncVirtual<T>(MetaSyncContext ctx, ref List<T> v) where T : IMetaStruct, new()
  {
    int size = syncBeginArray(ctx, ref v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? new T() : v[i];
      syncVirtual(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    syncEndArray(ctx, ref v);
  }

  static public void sync<T>(MetaSyncContext ctx, ref T v) where T : IMetaStruct
  {
    if(ctx.is_read)
    {
      MetaHelper.ensure(ctx.reader.BeginArray());
      v.syncFields(ctx);
      MetaHelper.ensure(ctx.reader.EndArray());  
    }
    else
    {
      MetaHelper.ensure(ctx.writer.BeginArray(v.getFieldsCount()));
      v.syncFields(ctx);
      MetaHelper.ensure(ctx.writer.EndArray());
    }
  }

  static public MetaIoError syncSafe<T>(MetaSyncContext ctx, ref T v) where T : IMetaStruct
  {
    try 
    {
      MetaHelper.sync(ctx, ref v);
    }
    catch(MetaException e)
    {
      return e.err;
    }
    catch(Exception)
    {
      return MetaIoError.GENERIC;
    }
    return MetaIoError.SUCCESS;
  }
}

public class MsgPackDataWriter : IDataWriter 
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

  MetaIoError decSpace() 
  {
    if(space.Count == 0)
      return MetaIoError.NO_SPACE_LEFT_IN_ARRAY;
    
    int left = space.Pop();
    left--;
    if(left < 0)
      return MetaIoError.NO_SPACE_LEFT_IN_ARRAY;
    
    space.Push(left);
    return MetaIoError.SUCCESS;    
  }

  public MetaIoError BeginArray(int size) 
  {
    MetaIoError err = decSpace();
    if(err != MetaIoError.SUCCESS)
      return err;
    
    space.Push(size);
    io.WriteArrayHeader(size);
    return MetaIoError.SUCCESS;
  }

  public MetaIoError EndArray() 
  {
    if(space.Count <= 1)
      return MetaIoError.ARRAY_STACK_IS_EMPTY;
    
    int left = space.Pop();
    if(left != 0)
      return MetaIoError.HAS_LEFT_SPACE;
    
    return MetaIoError.SUCCESS;
  }
  
  public MetaIoError End() 
  {
    if(space.Count != 1)
      return MetaIoError.ARRAY_STACK_IS_EMPTY;
    
    return MetaIoError.SUCCESS;
  }

  public MetaIoError WriteI8(sbyte v) 
  {
    io.Write(v);
    return decSpace();
  }
 
  public MetaIoError WriteU8(byte v) 
  {
    io.Write(v);
    return decSpace();
  }
  
  public MetaIoError WriteI16(short v) 
  {
    io.Write(v);
    return decSpace();
  }

  public MetaIoError WriteU16(ushort v) 
  {
    io.Write(v);
    return decSpace();
  }

  public MetaIoError WriteI32(int v) 
  {
    io.Write(v);
    return decSpace();
  }

  public MetaIoError WriteU32(uint v) 
  {
    io.Write(v);
    return decSpace();
  }

  public MetaIoError WriteU64(ulong v) 
  {
    io.Write(v);
    return decSpace();
  }

  public MetaIoError WriteI64(long v) 
  {
    io.Write(v);
    return decSpace();
  }

  public MetaIoError WriteBool(bool v) 
  {
    io.Write(v ? 1 : 0);
    return decSpace();
  }

  public MetaIoError WriteFloat(float v) 
  {
    io.Write(v);
    return decSpace();
  }

  public MetaIoError WriteDouble(double v) 
  {
    io.Write(v);
    return decSpace();
  }

  public MetaIoError WriteString(string v) 
  {
    if(v == null) 
      io.Write("");
    else
      io.Write(v);
    
    return decSpace();
  }

  public MetaIoError WriteNil()
  {
    io.WriteNil();
    
    return decSpace();
  }
}

public class MsgPackDataReader : IDataReader 
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

  int nextInt(ref MetaIoError err) 
  {
    if(!ArrayPositionValid())
    {
      err = MetaIoError.DATA_MISSING;
      return 0;
    }

    if(!io.Read()) 
    {
      err = MetaIoError.FAIL_READ;
      return 0;
    }
    
    MoveNext();
    
    if(io.IsSigned())
      return io.ValueSigned;
    else if(io.IsUnsigned())
      return (int)io.ValueUnsigned;
    else
    {
      MetaHelper.LogWarn("Got type: " + io.Type);
      err = MetaIoError.TYPE_DONT_MATCH; 
      return 0;
    }
  }
   
  uint nextUint(ref MetaIoError err) 
  {
    if(!ArrayPositionValid())
    {
      err = MetaIoError.DATA_MISSING;
      return 0;
    }

    if(!io.Read()) 
    {
      err = MetaIoError.FAIL_READ;
      return 0;
    }
    
    MoveNext();
    
    if(io.IsUnsigned())
      return io.ValueUnsigned;
    else if(io.IsSigned())
      return (uint)io.ValueSigned;
    else
    {
      MetaHelper.LogWarn("Got type: " + io.Type);
      err = MetaIoError.TYPE_DONT_MATCH; 
      return 0;
    }
  }

  bool nextBool(ref MetaIoError err) 
  {
    if(!ArrayPositionValid())
    {
      err = MetaIoError.DATA_MISSING;
      return false;
    }

    if(!io.Read()) 
    {
      err = MetaIoError.FAIL_READ;
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
      MetaHelper.LogWarn("Got type: " + io.Type);
      err = MetaIoError.TYPE_DONT_MATCH; 
      return false;
    }
  }

  ulong nextUint64(ref MetaIoError err) 
  {
    if(!ArrayPositionValid())
    {
      err = MetaIoError.DATA_MISSING;
      return 0;
    }

    if(!io.Read()) 
    {
      err = MetaIoError.FAIL_READ;
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
      MetaHelper.LogWarn("Got type: " + io.Type);
      err = MetaIoError.TYPE_DONT_MATCH; 
      return 0;
    }
  }

  long nextInt64(ref MetaIoError err) 
  {
    if(!ArrayPositionValid())
    {
      err = MetaIoError.DATA_MISSING;
      return 0;
    }

    if(!io.Read()) 
    {
      err = MetaIoError.FAIL_READ;
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
      MetaHelper.LogWarn("Got type: " + io.Type);
      err = MetaIoError.TYPE_DONT_MATCH; 
      return 0;
    }
  }
  
  public MetaIoError ReadI8(ref sbyte v) 
  {
    MetaIoError err = 0;
    v = (sbyte) nextInt(ref err);
    return err;
  }
  
  public MetaIoError ReadI16(ref short v) 
  {
    MetaIoError err = 0;
    v = (short) nextInt(ref err);
    return err;
  }
  
  public MetaIoError ReadI32(ref int v) 
  {
    MetaIoError err = 0;
    v = nextInt(ref err);
    return err;
  }
  
  public MetaIoError ReadU8(ref byte v) 
  {
    MetaIoError err = 0;
    v = (byte) nextUint(ref err);
    return err;
  }
  
  public MetaIoError ReadU16(ref ushort v) 
  {
    MetaIoError err = 0;
    v = (ushort) nextUint(ref err);
    return err;
  }
  
  public MetaIoError ReadU32(ref uint v) 
  {
    MetaIoError err = 0;
    v = nextUint(ref err);
    return err;
  }

  public MetaIoError ReadU64(ref ulong v) 
  {
    MetaIoError err = 0;
    v = nextUint64(ref err);
    return err;
  }

  public MetaIoError ReadI64(ref long v) 
  {
    MetaIoError err = 0;
    v = nextInt64(ref err);
    return err;
  }

  public MetaIoError ReadBool(ref bool v) 
  {
    MetaIoError err = 0;
    v = nextBool(ref err);
    return err;
  }

  public MetaIoError ReadFloat(ref float v) 
  {
    if(!ArrayPositionValid())
      return MetaIoError.DATA_MISSING;

    if(!io.Read()) 
    {
      return MetaIoError.FAIL_READ;
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
          MetaHelper.LogWarn("Got type: " + io.Type);
          return MetaIoError.TYPE_DONT_MATCH; 
      }
    }
    MoveNext();
    return MetaIoError.SUCCESS;
  }

  public MetaIoError ReadDouble(ref double v) 
  {
    if(!ArrayPositionValid())
      return MetaIoError.DATA_MISSING;

    if(!io.Read()) 
    {
      return MetaIoError.FAIL_READ;
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
          MetaHelper.LogWarn("Got type: " + io.Type);
          return MetaIoError.TYPE_DONT_MATCH; 
      }
    }
    MoveNext();
    return MetaIoError.SUCCESS;
  }

  public MetaIoError ReadRaw(ref byte[] v, ref int vlen) 
  {
    if(!ArrayPositionValid())
      return MetaIoError.DATA_MISSING;

    if(!io.Read()) 
      return MetaIoError.FAIL_READ;

    if(!io.IsRaw()) 
    {
      MetaHelper.LogWarn("Got type: " + io.Type);
      return MetaIoError.TYPE_DONT_MATCH;
    }

    vlen = (int)io.Length;
    if(v.Length < vlen)
      Array.Resize(ref v, vlen);
    io.ReadValueRaw(v, 0, vlen);
    MoveNext();
    return MetaIoError.SUCCESS;
  }

  public MetaIoError ReadString(ref string v) 
  {
    if(!ArrayPositionValid())
      return MetaIoError.DATA_MISSING;

    if(!io.Read()) 
      return MetaIoError.FAIL_READ;

    if(!io.IsRaw()) 
    {
      MetaHelper.LogWarn("Got type: " + io.Type);
      return MetaIoError.TYPE_DONT_MATCH;
    }

    //TODO: use shared buffer for strings loading
    byte[] strval = new byte[io.Length];
    io.ReadValueRaw(strval, 0, strval.Length);
    v = System.Text.Encoding.UTF8.GetString(strval);
    MoveNext();
    return MetaIoError.SUCCESS;
  }

  public MetaIoError BeginArray() 
  {
    if(!ArrayPositionValid())
      return MetaIoError.DATA_MISSING;

    if(!io.Read())
      return MetaIoError.FAIL_READ;

    if(!io.IsArray())
    {
      MetaHelper.LogWarn("Got type: " + io.Type);
      return MetaIoError.TYPE_DONT_MATCH;
    }

    ArrayPosition ap = new ArrayPosition(io.Length);
    stack.Push(ap);
    return MetaIoError.SUCCESS;
  } 

  public MetaIoError GetArraySize(ref int v) 
  {
    if(!io.IsArray())
    {
      MetaHelper.LogWarn("Got type: " + io.Type);
      return MetaIoError.TYPE_DONT_MATCH;
    }

    v = (int) io.Length;
    return MetaIoError.SUCCESS;
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

  public MetaIoError EndArray() 
  {
    SkipTrailingFields();
    stack.Pop();
    MoveNext();
    return MetaIoError.SUCCESS;
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

} // namespace bhl
