#if (BHL_PARSER || UNITY_EDITOR)

using System;
using System.Collections.Generic;
using System.IO;

namespace bhl
{

//NOTE: consolidates multiple cache files into a single file so that
//      a project with many source files doesn't pay Windows' per-file I/O
//      overhead (antivirus scanning on every open/close, NTFS metadata churn)
//      on every cache lookup during a compile. Staleness is still purely
//      mtime-based, mirroring BuildUtils.NeedToRegen: an entry is valid as
//      long as none of its dependencies were touched after it was written.
public class CompileCacheBlob
{
  const uint MAGIC = 0x4c48425f;
  const uint VERSION = 1;

  struct Entry
  {
    public long write_ticks;
    public int offset;
    public int length;
  }

  Dictionary<string, Entry> maybe_imports = new Dictionary<string, Entry>();
  Dictionary<string, Entry> compiled = new Dictionary<string, Entry>();
  byte[] data = Array.Empty<byte>();

  public static CompileCacheBlob Load(string path)
  {
    var blob = new CompileCacheBlob();

    if(!File.Exists(path))
      return blob;

    try
    {
      using(var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
      using(var r = new BinaryReader(fs))
      {
        if(r.ReadUInt32() != MAGIC || r.ReadUInt32() != VERSION)
          return new CompileCacheBlob();

        int maybe_imports_count = r.ReadInt32();
        for(int i = 0; i < maybe_imports_count; ++i)
          blob.maybe_imports[r.ReadString()] = ReadEntry(r);

        int compiled_count = r.ReadInt32();
        for(int i = 0; i < compiled_count; ++i)
          blob.compiled[r.ReadString()] = ReadEntry(r);

        int data_len = r.ReadInt32();
        blob.data = r.ReadBytes(data_len);
      }
    }
    catch(Exception)
    {
      //NOTE: a corrupt/truncated blob is treated as a cold cache, not an error
      return new CompileCacheBlob();
    }

    return blob;
  }

  static Entry ReadEntry(BinaryReader r)
  {
    return new Entry {
      write_ticks = r.ReadInt64(),
      offset = r.ReadInt32(),
      length = r.ReadInt32()
    };
  }

  static void WriteEntry(BinaryWriter w, long write_ticks, int offset, int length)
  {
    w.Write(write_ticks);
    w.Write(offset);
    w.Write(length);
  }

  public bool TryGetMaybeImports(string file, out byte[] bytes, out long write_ticks)
  {
    return TryGet(maybe_imports, file, out bytes, out write_ticks);
  }

  public bool TryGetCompiled(string file, out byte[] bytes, out long write_ticks)
  {
    return TryGet(compiled, file, out bytes, out write_ticks);
  }

  bool TryGet(Dictionary<string, Entry> dict, string file, out byte[] bytes, out long write_ticks)
  {
    if(dict.TryGetValue(file, out var e))
    {
      bytes = new byte[e.length];
      Array.Copy(data, e.offset, bytes, 0, e.length);
      write_ticks = e.write_ticks;
      return true;
    }

    bytes = null;
    write_ticks = 0;
    return false;
  }

  public static bool IsStale(long entry_write_ticks, string dep)
  {
    return File.Exists(dep) && File.GetLastWriteTime(dep).Ticks > entry_write_ticks;
  }

  public static bool IsStale(long entry_write_ticks, IEnumerable<string> deps)
  {
    foreach(var dep in deps)
      if(IsStale(entry_write_ticks, dep))
        return true;
    return false;
  }

  public class Writer
  {
    struct Pending
    {
      public string key;
      public long write_ticks;
      public byte[] bytes;
    }

    List<Pending> maybe_imports = new List<Pending>();
    List<Pending> compiled = new List<Pending>();

    public void AddMaybeImports(string key, long write_ticks, byte[] bytes)
    {
      maybe_imports.Add(new Pending { key = key, write_ticks = write_ticks, bytes = bytes });
    }

    public void AddCompiled(string key, long write_ticks, byte[] bytes)
    {
      compiled.Add(new Pending { key = key, write_ticks = write_ticks, bytes = bytes });
    }

    public void Save(string path)
    {
      var data = new MemoryStream();

      var maybe_imports_meta = new List<(string key, long ticks, int offset, int length)>();
      foreach(var p in maybe_imports)
      {
        int offset = (int)data.Position;
        data.Write(p.bytes, 0, p.bytes.Length);
        maybe_imports_meta.Add((p.key, p.write_ticks, offset, p.bytes.Length));
      }

      var compiled_meta = new List<(string key, long ticks, int offset, int length)>();
      foreach(var p in compiled)
      {
        int offset = (int)data.Position;
        data.Write(p.bytes, 0, p.bytes.Length);
        compiled_meta.Add((p.key, p.write_ticks, offset, p.bytes.Length));
      }

      string tmp_path = path + ".tmp";

      using(var fs = new FileStream(tmp_path, FileMode.Create, FileAccess.Write))
      using(var w = new BinaryWriter(fs))
      {
        w.Write(MAGIC);
        w.Write(VERSION);

        w.Write(maybe_imports_meta.Count);
        foreach(var e in maybe_imports_meta)
        {
          w.Write(e.key);
          WriteEntry(w, e.ticks, e.offset, e.length);
        }

        w.Write(compiled_meta.Count);
        foreach(var e in compiled_meta)
        {
          w.Write(e.key);
          WriteEntry(w, e.ticks, e.offset, e.length);
        }

        var data_bytes = data.GetBuffer();
        int data_len = (int)data.Length;
        w.Write(data_len);
        w.Write(data_bytes, 0, data_len);
      }

      BuildUtils.Rm(path);
      File.Move(tmp_path, path);
    }
  }
}

}

#endif
