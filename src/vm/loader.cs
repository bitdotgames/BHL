using System;
using System.IO;
using System.Collections.Generic;
using System.Buffers;

namespace bhl
{

public enum ModuleBinaryFormat
{
  FMT_BIN         = 0,
  FMT_LZ4         = 1,
  //NOTE: same idea as FMT_LZ4, but modules are grouped into chunks and each
  //      chunk is LZ4-compressed as a whole (rather than each module on its
  //      own) - compressing many small modules together achieves better
  //      compaction than compressing each one individually
  FMT_LZ4_CHUNKED = 2,
}

public interface IModuleLoader
{
  ModuleDeclared Load(string module_name, INamedResolver resolver);
}

public class ModuleLoader : IModuleLoader
{
  public const byte COMPILE_FMT = 2;

  //NOTE: reserved module-name prefix (a NUL can never appear in a real
  //      file-path-derived module name) marking an entry as a FMT_LZ4_CHUNKED
  //      compressed chunk rather than a loadable module
  public const string CHUNK_ENTRY_NAME_PREFIX = "\0chunk";

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

  //NOTE: chunk index -> stream position of its (still compressed) raw blob
  Dictionary<int, long> chunk_stream_pos = new Dictionary<int, long>();
  //NOTE: chunk index -> decompressed bytes, populated lazily and kept for
  //      the lifetime of this loader so that N modules sharing a chunk only
  //      pay for decompressing it once
  Dictionary<int, byte[]> chunk_cache = new Dictionary<int, byte[]>();

  public ModuleLoader(Types types, Stream source)
  {
    this.types = types;
    Init(source);
  }

  void Init(Stream source_)
  {
    name2entry.Clear();
    chunk_stream_pos.Clear();
    chunk_cache.Clear();

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

      if(name.StartsWith(CHUNK_ENTRY_NAME_PREFIX))
      {
        int chunk_index = int.Parse(name.Substring(CHUNK_ENTRY_NAME_PREFIX.Length));
        chunk_stream_pos[chunk_index] = source.Position;
      }
      else
      {
        var ent = new Entry();
        ent.format = (ModuleBinaryFormat)format;
        ent.stream_pos = source.Position;
        if(name2entry.ContainsKey(name))
          throw new Exception("Key already exists: " + name);
        name2entry.Add(name, ent);
      }

      //skipping binary blob
      int tmp_buf_len = 0;
      reader.ReadRawBegin(ref tmp_buf_len);
      var tmp_buf = ArrayPool<byte>.Shared.Rent(tmp_buf_len);
      reader.ReadRawEnd(tmp_buf);
      ArrayPool<byte>.Shared.Return(tmp_buf);
    }

    required_bindings.Clear();
    //NOTE: trailing/optional - absent in bundles written before this section existed
    if(source.Position < source.Length)
    {
      int required_bindings_len = 0;
      reader.ReadI32(ref required_bindings_len);
      for(int i = 0; i < required_bindings_len; ++i)
      {
        string name = "";
        reader.ReadString(ref name);
        string hash = "";
        reader.ReadString(ref hash);
        required_bindings.Add((name, hash));
      }
    }
  }

  List<(string name, string hash)> required_bindings = new List<(string name, string hash)>();
  public IEnumerable<(string name, string hash)> RequiredBindings => required_bindings;

  public ModuleDeclared Load(string module_name, INamedResolver resolver)
  {
    if(!name2entry.TryGetValue(module_name, out var entry))
      return null;

    DecodeBin(entry, out var bytes, out var bytes_len, out var return_to_pool);

    module_stream.SetData(bytes, 0, bytes_len);

    var decl = ModuleDeclared.FromStream(types, module_stream, resolver);

    if(return_to_pool)
      ArrayPool<byte>.Shared.Return(bytes);

    return decl;
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
    else if(ent.format == ModuleBinaryFormat.FMT_LZ4_CHUNKED)
    {
      reader.SetPos(ent.stream_pos);
      int loc_len = 0;
      reader.ReadRawBegin(ref loc_len);
      var loc_buf = ArrayPool<byte>.Shared.Rent(loc_len);
      reader.ReadRawEnd(loc_buf);

      int chunk_index = BitConverter.ToInt32(loc_buf, 0);
      int chunk_offset = BitConverter.ToInt32(loc_buf, 4);
      int module_len = BitConverter.ToInt32(loc_buf, 8);
      ArrayPool<byte>.Shared.Return(loc_buf);

      var chunk_buf = GetDecompressedChunk(chunk_index);

      var module_buf = new byte[module_len];
      Array.Copy(chunk_buf, chunk_offset, module_buf, 0, module_len);

      bytes = module_buf;
      bytes_len = module_len;
      return_to_pool = false;
    }
    else
      throw new Exception("Unknown format: " + ent.format);
  }

  byte[] GetDecompressedChunk(int chunk_index)
  {
    if(chunk_cache.TryGetValue(chunk_index, out var cached))
      return cached;

    reader.SetPos(chunk_stream_pos[chunk_index]);
    int lz_buf_len = 0;
    reader.ReadRawBegin(ref lz_buf_len);
    var lz_buf = ArrayPool<byte>.Shared.Rent(lz_buf_len);
    reader.ReadRawEnd(lz_buf);

    var lz_size = (int)BitConverter.ToUInt32(lz_buf, 0);
    //NOTE: unlike the single-module FMT_LZ4 path, this buffer is cached for
    //      the loader's lifetime, so it must be its own dedicated array
    //      rather than a pooled/shared one
    var dst_buf = new byte[lz_size];

    using(var dst_stream = new MemoryStream(dst_buf, 0, dst_buf.Length, true))
    {
      lz_stream.SetData(lz_buf, 0, lz_buf_len);
      decoder.Reset(lz_stream);
      decoder.CopyTo(dst_stream);
    }

    ArrayPool<byte>.Shared.Return(lz_buf);

    chunk_cache[chunk_index] = dst_buf;
    return dst_buf;
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

  public ModuleDeclared Load(string module_name, INamedResolver resolver)
  {
    lock(name2prefab)
    {
      if(!name2prefab.TryGetValue(module_name, out var ms))
      {
        ++misses;
        var module = loader.Load(module_name, resolver);
        ms = new MemoryStream();
        module.ToStream(ms, leave_open: true);
        name2prefab[module_name] = ms;
      }
      else
        ++hits;

      ms.Position = 0;
      return ModuleDeclared.FromStream(types, ms, resolver);
    }
  }
}

}
