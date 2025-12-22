using System;
using System.IO;
using System.Collections.Generic;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;

namespace bhl
{

public interface IUserBindings
{
  void Register(Types ts);
}

public class EmptyUserBindings : IUserBindings
{
  public void Register(Types ts)
  {
  }
}

public enum ModuleBinaryFormat
{
  FMT_BIN      = 0,
  FMT_LZ4      = 1,
  FMT_FILE_REF = 2,
}

public interface IModuleLoader
{
  Module Load(string module_name, INamedResolver resolver);
}

public class ModuleLoader : IModuleLoader
{
  public const byte COMPILE_FMT = 2;

  Types types;
  Stream source;
  marshall.MsgPackDataReader reader;
  Lz4DecoderStream decoder = new Lz4DecoderStream();
  MemoryStream module_stream = new MemoryStream();
  MemoryStream lz_stream = new MemoryStream();
  MemoryStream lz_dst_stream = new MemoryStream();

  public class Entry
  {
    public ModuleBinaryFormat format;
    public long stream_pos;
  }

  Dictionary<string, Entry> name2entry = new Dictionary<string, Entry>();

  public ModuleLoader(Types types, Stream source)
  {
    this.types = types;
    Init(source);
  }

  void Init(Stream source_)
  {
    name2entry.Clear();

    source = source_;
    source.Position = 0;

    reader = new marshall.MsgPackDataReader(source);

    byte file_format = 0;
    reader.ReadU8(ref file_format);
    if(file_format != COMPILE_FMT)
      throw new Exception("Bad file format");

    uint file_version = 0;
    reader.ReadU32(ref file_version);
    if(file_version != 1)
      throw new Exception("Bad file version");

    int num_entries = 0;
    reader.ReadI32(ref num_entries);

    //TODO: don't store binary blobs alongside entries
    while(num_entries-- > 0)
    {
      int format = 0;
      reader.ReadI32(ref format);

      string name = "";
      reader.ReadString(ref name);

      var ent = new Entry();
      ent.format = (ModuleBinaryFormat)format;
      ent.stream_pos = source.Position;
      if(name2entry.ContainsKey(name))
        throw new Exception("Key already exists: " + name);
      name2entry.Add(name, ent);

      //skipping binary blob
      int tmp_buf_len = 0;
      reader.ReadRawBegin(ref tmp_buf_len);
      var tmp_buf = ArrayPool<byte>.Shared.Rent(tmp_buf_len);
      reader.ReadRawEnd(tmp_buf);
      ArrayPool<byte>.Shared.Return(tmp_buf);
    }
  }

  public Module Load(string module_name, INamedResolver resolver)
  {
    if(!name2entry.TryGetValue(module_name, out var entry))
      return null;

    DecodeBin(entry, out var bytes, out var bytes_len, out var return_to_pool);

    module_stream.SetData(bytes, 0, bytes_len);

    var module = CompiledModule.FromStream(types, module_stream, resolver);

    if(return_to_pool)
      ArrayPool<byte>.Shared.Return(bytes);

    return module;
  }

  void DecodeBin(Entry ent, out byte[] bytes, out int bytes_len, out bool return_to_pool)
  {
    if(ent.format == ModuleBinaryFormat.FMT_BIN)
    {
      int tmp_buf_len = 0;
      reader.SetPos(ent.stream_pos);
      reader.ReadRawBegin(ref tmp_buf_len);
      var tmp_buf = ArrayPool<byte>.Shared.Rent(tmp_buf_len);
      reader.ReadRawEnd(tmp_buf);
      bytes = tmp_buf;
      bytes_len = tmp_buf_len;
      return_to_pool = true;
    }
    else if(ent.format == ModuleBinaryFormat.FMT_LZ4)
    {
      int lz_buf_len = 0;
      reader.SetPos(ent.stream_pos);
      reader.ReadRawBegin(ref lz_buf_len);
      var lz_buf = ArrayPool<byte>.Shared.Rent(lz_buf_len);
      reader.ReadRawEnd(lz_buf);

      var lz_size = (int)BitConverter.ToUInt32(lz_buf, 0);
      var dst_buf = ArrayPool<byte>.Shared.Rent(lz_size);

      lz_dst_stream.SetData(dst_buf, 0, dst_buf.Length);
      //NOTE: uncompressed size is only added by PHP implementation
      //taking into account first 4 bytes which store uncompressed size
      //lz_stream.SetData(lz_buf, 4, lz_buf_len-4);
      lz_stream.SetData(lz_buf, 0, lz_buf_len);
      decoder.Reset(lz_stream);
      decoder.CopyTo(lz_dst_stream);
      bytes = lz_dst_stream.GetBuffer();
      bytes_len = (int)lz_dst_stream.Position;
      return_to_pool = false;

      ArrayPool<byte>.Shared.Return(lz_buf);
      ArrayPool<byte>.Shared.Return(dst_buf);
    }
    else if(ent.format == ModuleBinaryFormat.FMT_FILE_REF)
    {
      int tmp_buf_len = 0;
      reader.SetPos(ent.stream_pos);
      reader.ReadRawBegin(ref tmp_buf_len);
      var tmp_buf = ArrayPool<byte>.Shared.Rent(tmp_buf_len);
      reader.ReadRawEnd(tmp_buf);
      string file_path = System.Text.Encoding.UTF8.GetString(tmp_buf, 0, tmp_buf_len);
      var file_bytes = File.ReadAllBytes(file_path);
      bytes = file_bytes;
      bytes_len = file_bytes.Length;
      return_to_pool = false;

      ArrayPool<byte>.Shared.Return(tmp_buf);
    }
    else
      throw new Exception("Unknown format: " + ent.format);
  }
}

public class CachingModuleLoader : IModuleLoader
{
  Types types;
  IModuleLoader loader;

  Dictionary<string, MemoryStream> name2prefab = new ();

  public int Count => name2prefab.Count;
  public int Hits => hits;
  public int Misses => misses;
  int hits;
  int misses;

  public CachingModuleLoader(Types types, IModuleLoader loader)
  {
    this.types = types;
    this.loader = loader;
  }

  public Module Load(string module_name, INamedResolver resolver)
  {
    lock(name2prefab)
    {
      if(!name2prefab.TryGetValue(module_name, out var ms))
      {
        ++misses;
        var module = loader.Load(module_name, resolver);
        ms = new MemoryStream();
        CompiledModule.ToStream(module, ms, leave_open: true);
        name2prefab[module_name] = ms;
      }
      else
        ++hits;

      ms.Position = 0;
      return CompiledModule.FromStream(types, ms, resolver);
    }
  }
}

}
