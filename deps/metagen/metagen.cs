using MsgPack;
using System.IO;
using System;
using System.Collections;
using System.Collections.Generic;

namespace game {

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
  DATA_MISSING0          = 8, //means start of the struct is missing
}

public interface IMetaStruct 
{
  uint CLASS_ID();
  MetaIoError write(IDataWriter writer);  //could be moved to extension if we did't use structs
  MetaIoError read(IDataReader reader);   //could be moved to extension if we did't use structs
  MetaIoError writeFields(IDataWriter writer); //write fields ONLY, without struct meta info
  MetaIoError readFields(IDataReader reader); //read fields ONLY, without struct meta info
  int getFieldsCount();
  void copy(IMetaStruct source);
  IMetaStruct clone();
  void reset();
}

public abstract class BaseMetaStruct : IMetaStruct
{
  public virtual uint CLASS_ID() { return 0; }
  public virtual MetaIoError writeFields(IDataWriter writer) { return MetaIoError.SUCCESS; }
  public virtual MetaIoError readFields(IDataReader reader) { return MetaIoError.SUCCESS; }
  public virtual int getFieldsCount() { return 0; }
  public virtual void copy(IMetaStruct source) {}
  public virtual IMetaStruct clone() { return null; }
  public virtual void reset() {}

  public virtual MetaIoError write(IDataWriter writer) 
  {
    MetaIoError err = writer.BeginArray(getFieldsCount());
    if(err != MetaIoError.SUCCESS)
      return err;
    
    err = writeFields(writer);
    if(err != MetaIoError.SUCCESS)
      return err;
    
    return writer.EndArray();
  }

  public virtual MetaIoError read(IDataReader reader) 
  {
    MetaIoError err = MetaIoError.SUCCESS;

    err = reader.BeginArray();
    if(err != MetaIoError.SUCCESS)
      return err == MetaIoError.DATA_MISSING ? MetaIoError.DATA_MISSING0 : err;
    
    err = readFields(reader);
    if(err != MetaIoError.SUCCESS)
      return err;
    
    err = reader.EndArray();
    if(err != MetaIoError.SUCCESS)
      return err;
    
    return err;
  }
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

  public static MetaIoError writeClass(IDataWriter writer, IMetaStruct mstruct) 
  {
    MetaIoError err = writer.BeginArray(mstruct.getFieldsCount());
    if(err != MetaIoError.SUCCESS)
      return err;
    
    err = mstruct.writeFields(writer);
    if(err != MetaIoError.SUCCESS)
      return err;
    
    return writer.EndArray();
  }
  
  public static MetaIoError writeGeneric(IDataWriter writer, IMetaStruct mstruct) 
  {
    MetaIoError err = writer.BeginArray(mstruct.getFieldsCount() + 1);
    if(err != MetaIoError.SUCCESS)
      return err;
    
    //NOTE: Storing class names is not space effective
    //err = writer.WriteString(mstruct.CLASS_NAME());
    err = writer.WriteU32(mstruct.CLASS_ID());
    if(err != MetaIoError.SUCCESS)
      return err;
    
    err = mstruct.writeFields(writer);
    if(err != MetaIoError.SUCCESS)
      return err;
    
    return writer.EndArray();
  }

  public static IMetaStruct readClass(IDataReader reader, IMetaStruct mstruct, ref MetaIoError err)
  {
    err = reader.BeginArray();
    if(err != MetaIoError.SUCCESS)
      return null;
    
    err = mstruct.readFields(reader);
    if(err != MetaIoError.SUCCESS)
      return null;
    
    err = reader.EndArray();
    if(err != MetaIoError.SUCCESS)
      return null;
    
    return mstruct;
  }
  
  public static IMetaStruct readGeneric(IDataReader reader, ref MetaIoError err) 
  {
    err = reader.BeginArray();
    if(err != MetaIoError.SUCCESS)
      return null;

    uint clid = 0;
    err = reader.ReadU32(ref clid);
    if(err != MetaIoError.SUCCESS)
      return null;
    
    //NOTE: createById accepts int since it thinks a normal crc is used
    //      while we are actually using crc28 which is unsigned
    IMetaStruct mstruct = CreateById((int)clid);
    if(mstruct == null) 
    {
      MetaHelper.LogWarn("Could not create struct: " + clid);
      err = MetaIoError.TYPE_DONT_MATCH;
      return null;
    }

    err = mstruct.readFields(reader);
    if(err != MetaIoError.SUCCESS)
      return null;
    
    err = reader.EndArray();
    return mstruct;
  }
}

//NOTE: it implements IList interface partially! 
public class AutoArray<T> : IList<T>
{
  //for speed you can access directly the guts
  public T[] Data;
  public int Length;

  public int Capacity 
  {
    get {
      return Data == null ? 0 : Data.Length;
    }

    set {
      Grow(value);
    }
  }

  public int Count { get { return Length; }  }
  public bool IsFixedSize { get { return false; } }
  public bool IsReadOnly { get { return false; } }

  public AutoArray(int capacity = 0)
  {
    if(capacity <= 0)
      capacity = 1;

    Length = 0;
    Capacity = capacity;
  }

  public AutoArray(IList<T> list)
  {
    Capacity = list.Count;
    AddRange(list);
  }

  public void AddRange(IList<T> list)
  {
    for(int i=0; i<list.Count; ++i)
      Add(list[i]);
  }

  public void Clear()
  {
    Array.Clear(Data, 0, Length);
    Length = 0;
  }

  public T this[int i]
  {
    get {
      return Data[i];
    }
    set {
      Data[i] = value;
    }
  }

  public void Add(T val)
  {
    if((Length+1) > Data.Length)
      Grow(Data.Length*2);
    Data[Length] = val;
    Length++;
  }

  public void RemoveAt(int index)
  {
    Array.Copy(Data, index+1, Data, index, Length-index-1);
    //nullifying last element
    Data[Length-1] = default(T);
    Length--;
  }

  public void Grow(int capacity)
  {
    if(capacity < Length)
      return;

    var tmp = new T[capacity];
    for(int i=0;i<Length;++i)
      tmp[i] = Data[i];
    Data = tmp;
  }

  public int IndexOf(T o)
  {
    for(int i=0;i<Length;++i)
    {
      if(Data[i].Equals(o))
        return i;
    }
    return -1;
  }

  public bool Contains(T o)
  {
    return IndexOf(o) >= 0;
  }

  public bool Remove(T o)
  {
    int idx = IndexOf(o);
    if(idx < 0)
      return false;
    RemoveAt(idx);
    return true;
  }

  ///////////////// not implemented IList interface methods
  public void Remove(object v)
  {}

  public void CopyTo(T[] arr, int len)
  {}

  public void Insert(int pos, T o)
  {}

  public IEnumerator<T> GetEnumerator()
  {
    return null;
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return null;
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
    uint tmp = nextUint(ref err);
    v = tmp == 1 ? true : false;
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

} // namespace game
