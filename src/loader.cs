using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

public class UserBindings
{
  public virtual void Register(GlobalScope globs) {}
}

public class EmptyUserBindings : UserBindings {}

public interface IModuleLoader
{
  //NOTE: must return null if no such module
  AST_Module LoadModule(HashedName id);
}

public enum ModuleBinaryFormat
{
  FMT_BIN      = 0,
  FMT_LZ4      = 1,
  FMT_FILE_REF = 2,
}

public class ModuleLoader : IModuleLoader
{
  Stream source;
  MsgPackDataReader reader;
  Lz4DecoderStream decoder = new Lz4DecoderStream();
  MemoryStream mod_stream = new MemoryStream();
  MsgPackDataReader mod_reader;
  MemoryStream lz_stream = new MemoryStream();
  MemoryStream lz_dst_stream = new MemoryStream();
  bool strict;

  public class Entry
  {
    public ModuleBinaryFormat format;
    public long stream_pos;
  }

  Dictionary<ulong, Entry> entries = new Dictionary<ulong, Entry>();

  public ModuleLoader(Stream source, bool strict = true)
  {
    this.strict = strict;
    Load(source);
  }

  void Load(Stream source_)
  {
    entries.Clear();

    source = source_;
    source.Position = 0;

    mod_reader = new MsgPackDataReader(mod_stream);

    reader = new MsgPackDataReader(source);

    int total_modules = 0;

    Util.Verify(reader.ReadI32(ref total_modules) == MetaIoError.SUCCESS);
    //Util.Debug("Total modules: " + total_modules);
    while(total_modules-- > 0)
    {
      int format = 0;
      Util.Verify(reader.ReadI32(ref format) == MetaIoError.SUCCESS);

      uint id = 0;
      Util.Verify(reader.ReadU32(ref id) == MetaIoError.SUCCESS);

      var ent = new Entry();
      ent.format = (ModuleBinaryFormat)format;
      ent.stream_pos = source.Position;
      if(entries.ContainsKey(id))
        Util.Verify(false, "Key already exists: " + id);
      entries.Add(id, ent);

      //skipping binary blob
      var tmp_buf = TempBuffer.Get();
      int tmp_buf_len = 0;
      Util.Verify(reader.ReadRaw(ref tmp_buf, ref tmp_buf_len) == MetaIoError.SUCCESS);
      TempBuffer.Update(tmp_buf);
    }
  }

  public AST_Module LoadModule(HashedName id)
  {
    Entry ent;
    if(!entries.TryGetValue(id.n, out ent))
      return null;

    byte[] res = null;
    int res_len = 0;
    DecodeBin(ent, ref res, ref res_len);

    mod_stream.SetData(res, 0, res_len);
    mod_reader.setPos(0);

    Util.SetupASTFactory();

    var ast = new AST_Module();

    var ok = MetaHelper.syncSafe(MetaSyncContext.NewForRead(mod_reader), ref ast) == MetaIoError.SUCCESS;
    if(strict && !ok)
      Util.Verify(false, "Can't load module " + id);

    Util.RestoreASTFactory();

    return ast;
  }

  void DecodeBin(Entry ent, ref byte[] res, ref int res_len)
  {
    if(ent.format == ModuleBinaryFormat.FMT_BIN)
    {
      var tmp_buf = TempBuffer.Get();
      int tmp_buf_len = 0;
      reader.setPos(ent.stream_pos);
      Util.Verify(reader.ReadRaw(ref tmp_buf, ref tmp_buf_len) == MetaIoError.SUCCESS);
      TempBuffer.Update(tmp_buf);
      res = tmp_buf;
      res_len = tmp_buf_len;
    }
    else if(ent.format == ModuleBinaryFormat.FMT_LZ4)
    {
      var lz_buf = TempBuffer.Get();
      int lz_buf_len = 0;
      reader.setPos(ent.stream_pos);
      Util.Verify(reader.ReadRaw(ref lz_buf, ref lz_buf_len) == MetaIoError.SUCCESS);
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
      reader.setPos(ent.stream_pos);
      Util.Verify(reader.ReadRaw(ref tmp_buf, ref tmp_buf_len) == MetaIoError.SUCCESS);
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

public class ExtensibleModuleLoader : IModuleLoader
{
  public List<IModuleLoader> loaders = new List<IModuleLoader>();

  public AST_Module LoadModule(HashedName id)
  {
    for(int i=0;i<loaders.Count;++i)
    {
      var ld = loaders[i];
      var ast = ld.LoadModule(id);
      if(ast != null)
        return ast;
    }
    return null;
  }
}

public interface IModuleImporter
{
  CompiledModule Import(string module_name);
}

} //namespace bhl
