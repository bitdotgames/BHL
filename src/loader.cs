using System;
using System.IO;
using System.Collections.Generic;

namespace bhl {

public interface IUserBindings
{
  void Register(Types ts);
}

public class EmptyUserBindings : IUserBindings 
{
  public void Register(Types ts)
  {}
}

public enum ModuleBinaryFormat
{
  FMT_BIN      = 0,
  FMT_LZ4      = 1,
  FMT_FILE_REF = 2,
}

public interface IModuleLoader
{
  CompiledModule Load(string module_name, ISymbolResolver resolver, System.Action<string> on_import);
}

public class ModuleLoader : IModuleLoader
{
  public const byte COMPILE_FMT = 2;

  Types types;
  Stream source;
  marshall.MsgPackDataReader reader;
  Lz4DecoderStream decoder = new Lz4DecoderStream();
  MemoryStream mod_stream = new MemoryStream();
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
    Util.Verify(file_format == COMPILE_FMT);

    uint file_version = 0;
    reader.ReadU32(ref file_version);
    Util.Verify(file_version == 1);

    int num_entries = 0;
    reader.ReadI32(ref num_entries);
    //Util.Debug("Total modules: " + total_modules);
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
        Util.Verify(false, "Key already exists: " + name);
      name2entry.Add(name, ent);

      //skipping binary blob
      var tmp_buf = TempBuffer.Get();
      int tmp_buf_len = 0;
      reader.ReadRaw(ref tmp_buf, ref tmp_buf_len);
      TempBuffer.Update(tmp_buf);
    }
  }

  public CompiledModule Load(string module_name, ISymbolResolver resolver, System.Action<string> on_import)
  {
    Entry ent;
    if(!name2entry.TryGetValue(module_name, out ent))
      return null;

    byte[] res = null;
    int res_len = 0;
    DecodeBin(ent, ref res, ref res_len);

    mod_stream.SetData(res, 0, res_len);

    return CompiledModule.FromStream(types, mod_stream, resolver, on_import);
  }

  void DecodeBin(Entry ent, ref byte[] res, ref int res_len)
  {
    if(ent.format == ModuleBinaryFormat.FMT_BIN)
    {
      var tmp_buf = TempBuffer.Get();
      int tmp_buf_len = 0;
      reader.SetPos(ent.stream_pos);
      reader.ReadRaw(ref tmp_buf, ref tmp_buf_len);
      TempBuffer.Update(tmp_buf);
      res = tmp_buf;
      res_len = tmp_buf_len;
    }
    else if(ent.format == ModuleBinaryFormat.FMT_LZ4)
    {
      var lz_buf = TempBuffer.Get();
      int lz_buf_len = 0;
      reader.SetPos(ent.stream_pos);
      reader.ReadRaw(ref lz_buf, ref lz_buf_len);
      TempBuffer.Update(lz_buf);

      var dst_buf = TempBuffer.Get();
      var lz_size = (int)BitConverter.ToUInt32(lz_buf, 0);
      if(lz_size > dst_buf.Length)
        Array.Resize(ref dst_buf, lz_size);
      TempBuffer.Update(dst_buf);

      lz_dst_stream.SetData(dst_buf, 0, dst_buf.Length);
      //NOTE: uncompressed size is only added by PHP implementation
      //taking into account first 4 bytes which store uncompressed size
      //lz_stream.SetData(lz_buf, 4, lz_buf_len-4);
      lz_stream.SetData(lz_buf, 0, lz_buf_len);
      decoder.Reset(lz_stream);
      decoder.CopyTo(lz_dst_stream);
      res = lz_dst_stream.GetBuffer();
      res_len = (int)lz_dst_stream.Position;
    }
    else if(ent.format == ModuleBinaryFormat.FMT_FILE_REF)
    {
      var tmp_buf = TempBuffer.Get();
      int tmp_buf_len = 0;
      reader.SetPos(ent.stream_pos);
      reader.ReadRaw(ref tmp_buf, ref tmp_buf_len);
      TempBuffer.Update(tmp_buf);
      string file_path = System.Text.Encoding.UTF8.GetString(tmp_buf, 0, tmp_buf_len);
      var file_bytes = File.ReadAllBytes(file_path);
      res = file_bytes;
      res_len = file_bytes.Length;
    }
    else
      throw new Exception("Unknown format: " + ent.format);
  }
}

} //namespace bhl
