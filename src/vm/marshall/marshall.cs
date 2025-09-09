using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace bhl.marshall
{

public static class Marshall
{
  //TODO: strings sync should use some sort of lookup table
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
      int fields_num = v == null ? 0 : -1 /*unspecified*/;
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
      ctx.writer.BeginContainer(-1 /*unspecified amount of fields*/);
      v.Sync(ctx);
      ctx.writer.EndContainer();
    }
  }

  static public ProxyType ReadTypeRefAt(SyncContext ctx, int idx)
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

  static public void SyncTypeRef(SyncContext ctx, ref ProxyType v)
  {
    if(ctx.is_read)
    {
      int idx = 0;
      ctx.reader.ReadI32(ref idx);
      v = ReadTypeRefAt(ctx, idx);
    }
    else
    {
      int idx = ctx.type_refs.GetIndex(v);
      ctx.writer.WriteI32(idx);
    }
  }

  static public void SyncTypeRefs(SyncContext ctx, List<ProxyType> v)
  {
    int size = BeginArray(ctx, v);
    for(int i = 0; i < size; ++i)
    {
      var tmp = ctx.is_read ? new ProxyType() : v[i];
      SyncTypeRef(ctx, ref tmp);
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
        ctx.type_refs.SetAt(i, ReadTypeRefAt(ctx, i));
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

}