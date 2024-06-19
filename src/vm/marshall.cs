using System.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Buffers;

namespace bhl.marshall {

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
  public TypeRefIndex type_refs;
  public IReader type_refs_reader;
  public List<int> type_refs_offsets;

  public static SyncContext NewReader(IReader reader, IFactory factory = null, TypeRefIndex refs = null)
  {
    var ctx = new SyncContext() {
      is_read = true,
      reader = reader,
      writer = null,
      factory = factory,
      type_refs = refs ?? new TypeRefIndex()
    };
    return ctx;
  }

  public static SyncContext NewWriter(IWriter writer, IFactory factory = null, TypeRefIndex refs = null)
  {
    var ctx = new SyncContext() {
      is_read = false,
      reader = null,
      writer = writer,
      factory = factory,
      type_refs = refs ?? new TypeRefIndex()
      
    };
    return ctx;
  }
}

public interface IMarshallable
{
  void Sync(SyncContext ctx);
}

public interface IMarshallableGeneric : IMarshallable 
{
  uint ClassId();
}

public interface IReader 
{
  Stream Stream { get; }
  
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
  void ReadRawBegin(ref int vlen);
  void ReadRawEnd(byte[] v);
  int BeginContainer(); 
  void EndContainer(); 
}

public interface IWriter 
{
  Stream Stream { get; }
  
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
  //NOTE: -1 means 'unspecified'
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
  
  //TODO: make it private and deduce by IMarshallableGeneric interface
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
      int fields_num = v == null ? 0 : -1/*unspecified*/;
       //class id
      if(fields_num != -1)
        ++fields_num;
      ctx.writer.BeginContainer(fields_num);
      ctx.writer.WriteU32(v == null ? 0xFFFFFFFF : v.ClassId());
      if(v != null)
        v.Sync(ctx);
      ctx.writer.EndContainer();
    }
  }
  
  //TODO: make it private and deduce by IMarshallableGeneric interface?
  static public void SyncGeneric<T>(SyncContext ctx, List<T> v) where T : IMarshallableGeneric
  {
    int size = BeginArray(ctx, v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = (IMarshallableGeneric)(ctx.is_read ? (IMarshallable)null : v[i]);
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
      ctx.reader.BeginContainer();
      v.Sync(ctx);
      ctx.reader.EndContainer();
    }
    else
    {
      ctx.writer.BeginContainer(-1/*unspecified amount of fields*/);
      v.Sync(ctx);
      ctx.writer.EndContainer();
    }
  }

  static public ProxyType ReadRefAt(SyncContext ctx, int idx)
  {
     if(!ctx.type_refs.IsValid(idx))
     {
       //let's make a temporary specialized copy of the sync ctx
       var ctx_tmp = ctx;
       ctx_tmp.reader = ctx.type_refs_reader; 
       
       var old_pos = ctx_tmp.reader.Stream.Position;
       ctx_tmp.reader.Stream.Position = ctx.type_refs_offsets[idx];
       
       var tmp = new ProxyType();
       Sync(ctx_tmp, ref tmp);

       ctx_tmp.reader.Stream.Position = old_pos;

       ctx.type_refs.SetAt(idx, tmp);
     }

     return ctx.type_refs.Get(idx);
  }
  
  static public void SyncRef(SyncContext ctx, ref ProxyType v)
  {
    if(ctx.is_read)
    {
      int idx = 0; 
      ctx.reader.ReadI32(ref idx);
      v = ReadRefAt(ctx, idx);
    }
    else
    {
      int idx = ctx.type_refs.GetIndex(v);
      ctx.writer.WriteI32(idx);
    }
  }
  
  static public void SyncRefs(SyncContext ctx, List<ProxyType> v)
  {
    int size = BeginArray(ctx, v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? new ProxyType() : v[i];
      SyncRef(ctx, ref tmp);
      if(ctx.is_read)
        v.Add(tmp);
    }
    EndArray(ctx, v);
  }

  static public T File2Obj<T>(string file, IFactory f = null) where T : IMarshallable, new()
  {
    using(FileStream rfs = File.Open(file, FileMode.Open, FileAccess.Read))
    {
      return Stream2Obj<T>(rfs, f);
    }
  }

  static public void Stream2Obj<T>(Stream s, T obj, IFactory f = null, TypeRefIndex refs = null) where T : IMarshallable
  {
    var reader = new MsgPackDataReader(s);
    var ctx = SyncContext.NewReader(reader, f, refs); 
    Sync(ctx, ref obj);
  }

  static public T Stream2Obj<T>(Stream s, IFactory f = null, TypeRefIndex refs = null) where T : IMarshallable, new()
  {
    var obj = new T();
    Stream2Obj(s, obj, f, refs);
    return obj;
  }

  static public void Obj2Stream<T>(T obj, Stream dst) where T : IMarshallable
  {
    var writer = new MsgPackDataWriter(dst);
    Sync(SyncContext.NewWriter(writer), ref obj);
  }

  public static byte[] WriteTypeRefs(TypeRefIndex refs, out List<int> refs_offsets)
  {
    refs_offsets = new List<int>();
    
    var dst = new MemoryStream();
    var writer = new MsgPackDataWriter(dst);
    
    var ctx = SyncContext.NewWriter(writer, null, refs);
    
    BeginArray(ctx, refs.all);
    for(int i = 0; i < refs.all.Count; ++i)
    {
      var tmp = refs.all[i];
      refs_offsets.Add((int)dst.Position);
      Sync(ctx, ref tmp);
    }
    EndArray(ctx, refs.all);
    
    return dst.GetBuffer();
  }

  public static void ReadTypeRefs(SyncContext ctx)
  {
    for(int i = 0; i < ctx.type_refs_offsets.Count; ++i)
    {
      if(!ctx.type_refs.IsValid(i))
        ctx.type_refs.SetAt(i, ReadRefAt(ctx, i));
    }
  }

  static public byte[] Obj2Bytes<T>(T obj, TypeRefIndex refs = null) where T : IMarshallable
  {
    var dst = new MemoryStream();
    var writer = new MsgPackDataWriter(dst);
    var ctx = SyncContext.NewWriter(writer, null, refs);
    Sync(ctx, ref obj);
    return dst.GetBuffer();
  }

  static public void Obj2File<T>(T obj, string file) where T : IMarshallable
  {
    using(FileStream wfs = new FileStream(file, FileMode.Create, System.IO.FileAccess.Write))
    {
      Obj2Stream(obj, wfs);
    }
  }
}

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
    if(!SpaceIsValid())
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
    if(!SpaceIsValid())
      throw new Error(ErrorCode.INVALID_POS);

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

  bool SpaceIsValid()
  {
    int left = SpaceLeft(); 
    return left == -1 || left  >= 0;
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

} // namespace bhl
