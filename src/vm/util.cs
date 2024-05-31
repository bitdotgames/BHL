using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.IO;

#if BHL_FRONT
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
#endif

namespace bhl {

static public class Util
{
  public delegate void LogCb(string text);
  static public LogCb Debug = DefaultDebug;
  static public LogCb Error = DefaultError;

  static public void DefaultDebug(string str)
  {
    Console.WriteLine("[DBG] " + str);
  }

  static public void DefaultError(string str)
  {
    Console.WriteLine("[ERR] " + str);
  }
  ////////////////////////////////////////////////////////

  public static void Verify(bool condition)
  {
    if(!condition) throw new Exception();
  }

  public static void Verify(bool condition, string fmt, params object[] vals)
  {
    if(!condition) throw new Exception(string.Format(fmt, vals));
  }

  public static void Verify(bool condition, string msg)
  {
    if(!condition) throw new Exception(msg);
  }

  ////////////////////////////////////////////////////////

  static public string NormalizeFilePath(string file_path)
  {
    return Path.GetFullPath(file_path).Replace("\\", "/");
  }

  static public List<string> NormalizeFilePaths(List<string> file_paths)
  {
    var res = new List<string>();
    foreach(var path in file_paths)
      res.Add(NormalizeFilePath(path));
    return res;
  }

  public static string GetSelfFile()
  {
    return System.Reflection.Assembly.GetExecutingAssembly().Location;
  }

  public static string GetSelfDir()
  {
    return Path.GetDirectoryName(GetSelfFile());
  }

  static public bool NeedToRegen(string file, List<string> deps)
  {
    if(!File.Exists(file))
    {
      //Console.WriteLine("Missing " + file);
      return true;
    }

    var fmtime = GetLastWriteTime(file);
    foreach(var dep in deps)
    {
      if(File.Exists(dep) && GetLastWriteTime(dep) > fmtime)
      {
        //Console.WriteLine("Stale " + dep + " " + file + " : " + GetLastWriteTime(dep) + " VS " + fmtime);
        return true;
      }
    }

    //Console.WriteLine("Hit "+ file);
    return false;
  }

  //optimized version for just one dependency
  static public bool NeedToRegen(string file, string dep)
  {
    if(!File.Exists(file))
      return true;

    var fmtime = GetLastWriteTime(file);
    if(File.Exists(dep) && GetLastWriteTime(dep) > fmtime)
      return true;

    return false;
  }

  static DateTime GetLastWriteTime(string file)
  {
    return new FileInfo(file).LastWriteTime;
  }
}

public struct SourcePos
{
  public int line;
  public int column;

  public SourcePos(int line, int column)
  {
    this.line = line;
    this.column = column;
  }

  public override string ToString()
  {
    return line + ":" + column;
  }
}

public struct SourceRange
{
  public SourcePos start;
  public SourcePos end;

  public SourceRange(int line, int column)
  {
    this.start = new SourcePos(line, column); 
    this.end = new SourcePos(line, column);
  }

  public SourceRange(SourcePos start, SourcePos end)
  {
    this.start = start;
    this.end = end;
  }

  public SourceRange(SourcePos start)
  {
    this.start = start;
    this.end = start;
  }

#if BHL_FRONT
  public SourceRange(Antlr4.Runtime.Misc.Interval interval, ITokenStream tokens)
  {
    var a = tokens.Get(interval.a);
    var b = tokens.Get(interval.b);
    this.start = new SourcePos(a.Line, a.Column);
    this.end = new SourcePos(b.Line, b.Column + (b.StopIndex - b.StartIndex));
  }
#endif

  public override string ToString()
  {
    return start + ".." + end;
  }
}

public class IncludePath
{
  List<string> items = new List<string>();

  public int Count {
    get {
      return items.Count;
    }
  }

  public string this[int i]
  {
    get {
      return items[i];
    }

    set {
      items[i] = value;
    }
  }

  public IncludePath()
  {}

  public IncludePath(IList<string> items)
  {
    this.items.AddRange(items);
  }

  public void Add(string path)
  {
    items.Add(Util.NormalizeFilePath(path));
  }

  public void Clear()
  {
    items.Clear();
  }

  public string ResolveImportPath(string self_path, string path)
  {
    //relative import
    if(path[0] == '.')
    {
      var dir = Path.GetDirectoryName(self_path);
      return Util.NormalizeFilePath(Path.Combine(dir, path) + ".bhl");
    }
    //import via include path
    else
      return TryIncludePaths(path);
  }

  public string FilePath2ModuleName(string full_path, bool normalized = false)
  {
    return _FilePath2ModuleName(normalized ? full_path : Util.NormalizeFilePath(full_path));
  }

  string _FilePath2ModuleName(string full_path)
  {
    string norm_path = "";
    for(int i=0;i<items.Count;++i)
    {
      var inc_path = items[i];
      if(full_path.IndexOf(inc_path) == 0)
      {
        norm_path = full_path.Substring(inc_path.Length);
        norm_path = norm_path.Replace('\\', '/');
        //stripping .bhl extension
        norm_path = norm_path.Substring(0, norm_path.Length-4);
        //stripping initial /
        norm_path = norm_path.TrimStart('/', '\\');
        break;
      }
    }

    if(norm_path.Length == 0)
    {
      throw new Exception("File path '" + full_path + "' was not normalized");
    }
    return norm_path;
  }

  public void ResolvePath(string self_path, string path, out string file_path, out string norm_path)
  {
    file_path = "";
    norm_path = "";

    if(path.Length == 0)
      throw new Exception("Bad path");

    file_path = ResolveImportPath(self_path, path);
    norm_path = _FilePath2ModuleName(file_path);
  }

  public string TryIncludePaths(string path)
  {
    for(int i=0;i<this.Count;++i)
    {
      var file_path = Util.NormalizeFilePath(this[i] + "/" + path  + ".bhl");
      if(File.Exists(file_path))
        return file_path;
    }
    return null;
  }
}

public class FileImports : marshall.IMarshallable
{
  public List<string> import_paths = new List<string>();
  public List<string> file_paths = new List<string>();

  public FileImports()
  {}

  public FileImports(Dictionary<string, string> imps)
  {
    foreach(var kv in imps)
    {
      import_paths.Add(kv.Key);
      file_paths.Add(kv.Value);
    }
  }

  //TODO: handle duplicate file_paths?
  //NOTE: file_path can be null if imported module is native for example
  public void Add(string import_path, string file_path)
  {
    if(import_paths.Contains(import_path))
      return;
    import_paths.Add(import_path);
    file_paths.Add(file_path);
  }

  public string MapToFilePath(string import_path)
  {
    int idx = import_paths.IndexOf(import_path);
    if(idx == -1)
      return null;
    return file_paths[idx];
  }

  public void Reset() 
  {
    import_paths.Clear();
    file_paths.Clear();
  }
  
  public void IndexTypeRefs(marshall.SyncContext ctx)
  {}

  public void Sync(marshall.SyncContext ctx) 
  {
    marshall.Marshall.Sync(ctx, import_paths);
    marshall.Marshall.Sync(ctx, file_paths);
  }
}

public static class Hash
{
  static public uint CRC32(string id)
  {
    if(string.IsNullOrEmpty(id))
      return 0;
    var bytes = System.Text.Encoding.ASCII.GetBytes(id);
    return Crc32.Compute(bytes, bytes.Length);
  }

  static public uint CRC28(string id)
  {
    return CRC32(id) & 0xFFFFFFF;
  }

  static public uint CRC32(byte[] bytes)
  {
    return Crc32.Compute(bytes, bytes.Length);
  }

  static public uint CRC28(byte[] bytes)
  {
    return CRC32(bytes) & 0xFFFFFFF;
  }
}

public static class Extensions
{
  public static string GetFullMessage(this Exception ex)
  {
    return ex.InnerException == null 
      ? ex.Message 
      : ex.Message + " --> " + ex.InnerException.GetFullMessage();
  }

  public static Stream ToStream(this string str)
  {
    MemoryStream stream = new MemoryStream();
    StreamWriter writer = new StreamWriter(stream);
    writer.Write(str);
    writer.Flush();
    stream.Position = 0;
    return stream;
  }

  public static void SetData(this MemoryStream ms, byte[] b, int offset, int blen)
  {
    if(ms.Capacity < b.Length)
      ms.Capacity = b.Length;

    ms.SetLength(0);
    ms.Write(b, offset, blen);
    ms.Position = 0;
  }

  public static int CopyTo(this Stream src, Stream dest)
  {
    int size = (src.CanSeek) ? Math.Min((int)(src.Length - src.Position), 0x2000) : 0x2000;
    byte[] buffer = new byte[size];
    int total = 0;
    int n;
    do
    {
      n = src.Read(buffer, 0, buffer.Length);
      dest.Write(buffer, 0, n);
      total += n;
    } while (n != 0);           
    return total;
  }

  public static Val PopRelease(this FixedStack<Val> stack)
  {
    var val = stack.Pop();
    val.RefMod(RefOp.DEC | RefOp.USR_DEC);
    return val;
  }

  public static void PushRetain(this FixedStack<Val> stack, Val val)
  {
    val.RefMod(RefOp.INC | RefOp.USR_INC);
    stack.Push(val);
  }

  public static void Assign(this FixedStack<Val> s, VM vm, int idx, Val val)
  {
    var curr = s[idx];
    if(curr != null)
    {
      for(int i=0;i<curr._refs;++i)
      {
        val.RefMod(RefOp.USR_INC);
        curr.RefMod(RefOp.USR_DEC);
      }
      curr.ValueCopyFrom(val);
    }
    else
    {
      curr = Val.New(vm);
      curr.ValueCopyFrom(val);
      curr.RefMod(RefOp.USR_INC);
      s[idx] = curr;
    }
  }

  public static FuncSignatureAttrib ToFuncSignatureAttrib(this FuncAttrib attrib)
  {
    return (FuncSignatureAttrib)((byte)FuncSignatureAttrib.FuncAttribMask & (byte)attrib);
  }

  public static void SetFuncAttrib(this FuncSignatureAttrib attrib, ref byte other)
  {
    //NOTE: we want to force FuncSignatureAttrib bits only, for this we mask out
    //      these bits in the target value and 'or' the bits we want to set
    other = (byte)((other & (byte)~FuncSignatureAttrib.FuncAttribMask) | (byte)attrib);
  }
}

public struct FuncArgsInfo
{
  //NOTE: 6 bits are used for a total number of args passed (max 63), 
  //      26 bits are reserved for default args set bits (max 26 default args)
  public const int ARGS_NUM_BITS = 6;
  public const uint ARGS_NUM_MASK = ((1 << ARGS_NUM_BITS) - 1);
  public const int MAX_ARGS = (int)ARGS_NUM_MASK;
  public const int MAX_DEFAULT_ARGS = 32 - ARGS_NUM_BITS; 

  public uint bits;

  public FuncArgsInfo(uint bits)
  {
    this.bits = bits;
  }

  public int CountArgs()
  {
    return (int)(bits & ARGS_NUM_MASK);
  }

  public bool HasDefaultUsedArgs()
  {              
    return (bits & ~ARGS_NUM_MASK) > 0;
  }

  public int CountRequiredArgs()
  {
    return CountArgs() - CountUsedDefaultArgs();
  }

  public int CountUsedDefaultArgs()
  {
    int c = 0;
    for(int i=0;i<MAX_DEFAULT_ARGS;++i)
      if(IsDefaultArgUsed(i))
        ++c;
    return c;
  }

  public bool SetArgsNum(int num)
  {
    if(num > MAX_ARGS)
      return false;
    bits = (bits & ~ARGS_NUM_MASK) | (uint)num;
    return true;
  }

  public bool IncArgsNum()
  {
    uint num = bits & ARGS_NUM_MASK; 
    ++num;
    if(num > MAX_ARGS)
      return false;
    bits = (bits & ~ARGS_NUM_MASK) | num;
    return true;
  }

  //NOTE: idx starts from 0, it's the idx of the default argument *within* default arguments,
  //      e.g: func Foo(int a, int b = 1, int c = 2) { .. }  
  //           b is 0 default arg idx, c is 1 
  public bool UseDefaultArg(int idx, bool flag)
  {
    if(idx >= MAX_DEFAULT_ARGS)
      return false;
    uint mask = 1u << (idx + ARGS_NUM_BITS); 
    if(flag)
      bits |= mask;
    else
      bits &= ~mask;
    return true;
  }

  //NOTE: idx starts from 0
  public bool IsDefaultArgUsed(int idx)
  {
    return (bits & (1u << (idx + ARGS_NUM_BITS))) != 0;
  }
}

/// <summary>
/// Implements a 32-bit CRC hash algorithm compatible with Zip etc.
/// </summary>
/// <remarks>
/// Crc32 should only be used for backward compatibility with older file formats
/// and algorithms. It is not secure enough for new applications.
/// If you need to call multiple times for the same data either use the HashAlgorithm
/// interface or remember that the result of one Compute call needs to be ~ (XOR) before
/// being passed in as the seed for the next Compute call.
/// </remarks>
public sealed class Crc32 : HashAlgorithm
{
  public const UInt32 DefaultPolynomial = 0xedb88320u;
  public const UInt32 DefaultSeed = 0xffffffffu;

  private static UInt32[] defaultTable;

  private readonly UInt32 seed;
  private readonly UInt32[] table;
  private UInt32 hash;

  public Crc32()
    : this(DefaultPolynomial, DefaultSeed)
  {
  }

  public Crc32(UInt32 polynomial, UInt32 seed)
  {
    table = InitializeTable(polynomial);
    this.seed = hash = seed;
  }

  public override void Initialize()
  {
    hash = seed;
  }

  protected override void HashCore(byte[] buffer, int start, int length)
  {
    hash = CalculateHash(table, hash, buffer, start, length);
  }

  protected override byte[] HashFinal()
  {
    var hashBuffer = UInt32ToBigEndianBytes(~hash);
    HashValue = hashBuffer;
    return hashBuffer;
  }

  public override int HashSize { get { return 32; } }

  public static UInt32 Compute(byte[] buffer, int buffer_len)
  {
    return Compute(DefaultSeed, buffer, buffer_len);
  }

  public static UInt32 Compute(UInt32 seed, byte[] buffer, int buffer_len)
  {
    return Compute(DefaultPolynomial, seed, buffer, buffer_len);
  }

  public static UInt32 Compute(UInt32 polynomial, UInt32 seed, byte[] buffer, int buffer_len)
  {
    return ~CalculateHash(InitializeTable(polynomial), seed, buffer, 0, buffer_len);
  }

  private static UInt32[] InitializeTable(UInt32 polynomial)
  {
    if (polynomial == DefaultPolynomial && defaultTable != null)
      return defaultTable;

    var createTable = new UInt32[256];
    for (var i = 0; i < 256; i++)
    {
      var entry = (UInt32)i;
      for (var j = 0; j < 8; j++)
        if ((entry & 1) == 1)
          entry = (entry >> 1) ^ polynomial;
        else
          entry = entry >> 1;
      createTable[i] = entry;
    }

    if (polynomial == DefaultPolynomial)
      defaultTable = createTable;

    return createTable;
  }

  private static UInt32 CalculateHash(UInt32[] table, UInt32 seed, IList<byte> buffer, int start, int size)
  {
    var crc = seed;
    for (var i = start; i < size - start; i++)
      crc = (crc >> 8) ^ table[buffer[i] ^ crc & 0xff];
    return crc;
  }

  public static UInt32 BeginHash()
  {
    var crc = DefaultSeed;
    return crc;
  }

  public static UInt32 AddHash(UInt32 crc, byte data)
  {
    UInt32[] table = InitializeTable(DefaultPolynomial);
    crc = (crc >> 8) ^ table[data ^ crc & 0xff];
    return crc;
  }

  public static UInt32 FinalizeHash(UInt32 crc)
  {
    return ~crc;
  }

  private static byte[] UInt32ToBigEndianBytes(UInt32 uint32)
  {
    var result = BitConverter.GetBytes(uint32);

    if (BitConverter.IsLittleEndian)
      Array.Reverse(result);

    return result;
  }
}

public class FixedStack<T>
{
  internal T[] storage;
  internal int head = 0;

  public int Count
  {
    get { return head; }
  }

  public FixedStack(int max_capacity)
  {
    storage = new T[max_capacity];
  }

  public T this[int index]
  {
    get { 
      ValidateIndex(index);
      return storage[index]; 
    }
    set { 
      ValidateIndex(index);
      storage[index] = value; 
    }
  }

  //let's validate index during parsing
  [System.Diagnostics.Conditional("BHL_FRONT")]
  void ValidateIndex(int index)
  {
    if(index < 0 || index >= head)
      throw new Exception("Out of bounds: " + index + " vs " + head);
  }

  public bool TryGetAt(int index, out T v)
  {
    if(index < 0 || index >= storage.Length)
    {
      v = default(T);
      return false;
    }
    
    v = storage[index];
    return true;
  }

  public T Push(T item)
  {
    storage[head++] = item;
    return item;
  }

  public T Pop()
  {
    return storage[--head];
  }

  public T Pop(T repl)
  {
    --head;
    T retval = storage[head];
    storage[head] = repl;
    return retval;
  }

  public T Peek()
  {
    return storage[head - 1];
  }

  public void SetRelative(int offset, T item)
  {
    storage[head - 1 - offset] = item;
  }

  public void RemoveAt(int idx)
  {
    if(idx == (head-1))
    {
      Dec();
    }
    else
    {
      --head;
      Array.Copy(storage, idx+1, storage, idx, storage.Length-idx-1);
    }
  }

  public void MoveTo(int src, int dst)
  {
    if(src == dst)
      return;

    var tmp = storage[src];

    if(src > dst)
      Array.Copy(storage, dst, storage, dst + 1, src - dst);
    else
      Array.Copy(storage, src + 1, storage, src, dst - src);

    storage[dst] = tmp;
  }

  public void Dec()
  {
    --head;
  }

  public void Resize(int pos)
  {
    head = pos;
  }

  public void Clear()
  {
    Array.Clear(storage, 0, storage.Length);
    head = 0;
  }
}

public class Bytecode
{
  public ushort Position { get { return (ushort)stream.Position; } }

  MemoryStream stream = new MemoryStream();

  public void Reset(byte[] buffer, int size)
  {
    stream.SetLength(0);
    stream.Write(buffer, 0, size);
    stream.Position = 0;
  }

  public byte[] GetBytes()
  {
    return stream.ToArray();
  }

  public static byte Decode8(byte[] bytecode, ref int ip)
  {
    ++ip;
    return bytecode[ip];
  }

  public static ushort Decode16(byte[] bytecode, ref int ip)
  {
    ++ip;
    ushort val = (ushort)
      ((uint)bytecode[ip] | 
       ((uint)bytecode[ip+1]) << 8
       );
    ;
    ip += 1;
    return val;
  }

  public static uint Decode24(byte[] bytecode, ref int ip)
  {
    ++ip;
    uint val = (uint)
      ((uint)bytecode[ip]          | 
       ((uint)bytecode[ip+1]) << 8 |
       ((uint)bytecode[ip+2]) << 16
       );
    ;
    ip += 2;
    return val;
  }

  public static uint Decode32(byte[] bytecode, ref int ip)
  {
    ++ip;
    uint val = (uint)
      ((uint)bytecode[ip]           | 
       ((uint)bytecode[ip+1] << 8)  |
       ((uint)bytecode[ip+2] << 16) |
       ((uint)bytecode[ip+3] << 24)
       );
    ;
    ip += 3;
    return val;
  }

  public static uint Decode(byte[] bytecode, int num_bytes, ref int ip)
  {
    if(num_bytes < 1 || num_bytes > 4)
      throw new Exception("Invalid amount of bytes: " + num_bytes);

    uint val = 0;

    ++ip;

    if(num_bytes >= 1)
      val |= (uint)bytecode[ip];

    if(num_bytes >= 2)
      val |= ((uint)bytecode[ip+1]) << 8;

    if(num_bytes >= 3)
      val |= ((uint)bytecode[ip+2]) << 16;

    if(num_bytes == 4)
      val |= ((uint)bytecode[ip+3]) << 24;

    ip += (num_bytes-1);

    return val;
  }

  public int Write(byte value)
  {
    return Write8(value);
  }

  public int Write(ushort value)
  {
    return Write16(value);
  }

  public int Write(uint value)
  {
    return Write32(value);
  }

  public int Write(bool value)
  {
    stream.WriteByte((byte)(value ? 1 : 0));
    return 1;
  }

  public int Write8(uint value)
  {
    stream.WriteByte((byte)(value & 0xFF));
    return 1;
  }

  public int Write16(uint value)
  {
    Write8((byte)(value & 0xFF));
    Write8((byte)((value >> 8) & 0xFF));
    return 2;
  }

  public int Write24(uint value)
  {
    Write8((byte)(value & 0xFF));
    Write8((byte)((value >> 8) & 0xFF));
    Write8((byte)((value >> 16) & 0xFF));
    return 3;
  }

  public int Write32(uint value)
  {
    Write8((byte)(value & 0xFF));
    Write8((byte)((value >> 8) & 0xFF));
    Write8((byte)((value >> 16) & 0xFF));
    Write8((byte)((value >> 24) & 0xFF));
    return 4;
  }

  public int Write(uint value, int num_bytes)
  {
    if(num_bytes < 1 || num_bytes > 4)
      throw new Exception("Invalid amount of bytes: " + num_bytes);

    if(num_bytes >= 1)
      Write8((byte)(value & 0xFF));
    if(num_bytes >= 2)
      Write8((byte)((value >> 8) & 0xFF));
    if(num_bytes >= 3)
      Write8((byte)((value >> 16) & 0xFF));
    if(num_bytes == 4)
      Write8((byte)((value >> 24) & 0xFF));

    return num_bytes;
  }

  public void Write(Bytecode buffer)
  {
    buffer.WriteTo(stream);
  }

  void WriteTo(MemoryStream buffer_stream)
  {
    stream.WriteTo(buffer_stream);
  }

  public void PatchAt(int pos, uint value, int num_bytes)
  {
    long orig_pos = stream.Position;
    stream.Position = pos;
    Write(value, num_bytes);
    stream.Position = orig_pos;
  }
}

//TODO: Not all interfaces are fully implemented
//NOTE: StackList is passed by value since it's a struct
public struct StackList<T> : IList<T>, IReadOnlyList<T>, IList
{
  public struct Enumerator<T1> : IEnumerator<T1>
  {
    public T1 Current => target[index];
    private int index;
    private StackList<T1> target;

    public Enumerator(StackList<T1> target)
    {
      this.target = target;
      index = -1;
    }

    public bool MoveNext() => ++index < target.Count;

    public void Reset()
    {
      index = -1;
    }

    object IEnumerator.Current => Current;

    public void Dispose() { }
  }

  const int CAPACITY = 16;

  Array16<T> storage;
  List<T> fallback;

  public void CopyTo(Array array, int index)
  {
    throw new NotImplementedException();
  }
  public int Count { get ; private set; }
  public bool IsSynchronized { get; }
  public object SyncRoot { get; }

  public bool IsFixedSize { get { return false; } }
  public bool IsReadOnly { get { return false; } }

  object IList.this[int index]
  {
    get => this[index];
    set => this[index] = (T)value;
  }

  public StackList(IList<T> list) 
    : this()
  {
    storage = new Array16<T>();
    AddRange(list);
  }

  //convenience special case
  public StackList(T v) 
    : this()
  {
    storage = new Array16<T>();
    Add(v);
  }

  //convenience special case
  public StackList(T v1, T v2) 
    : this()
  {
    storage = new Array16<T>();
    Add(v1);
    Add(v2);
  }

  //convenience special case
  public StackList(T v1, T v2, T v3) 
    : this()
  {
    storage = new Array16<T>();
    Add(v1);
    Add(v2);
    Add(v3);
  }

  public void AddRange(IList<T> list)
  {
    for(int i=0; i<list?.Count; ++i)
      Add(list[i]);
  }

  public void Clear()
  {
    if(fallback == null)
    {
      for(int i=0;i<Count;++i)
        this[i] = default(T);
    }
    else
      fallback.Clear();

    Count = 0;
  }

  public T this[int i]
  {
    get {
      if(fallback == null)
        return storage[i];
      else
        return fallback[i];
    }
    set {
      if(fallback == null)
        storage[i] = value;
      else
        fallback[i] = value;
    }
  }

  public void Add(T val)
  {
    Count++;
    if(fallback == null && Count > CAPACITY)
      Fallback();

    if(fallback == null)
      this[Count-1] = val;
    else
      fallback.Add(val);
  }

  public void RemoveAt(int index)
  {
    if(fallback == null)
    {
      for(int i=0; i<Count-1; ++i)
      {
        if(i >= index)
          storage[i] = storage[i+1];
      }
    }
    else
      fallback.RemoveAt(index);

    Count--;
  }

  void Fallback()
  {
    fallback = new List<T>(2*CAPACITY);
    for(int i=0; i<CAPACITY; ++i)
      fallback.Add(storage[i]);
  }

  public int IndexOf(T o)
  {
    if(fallback != null)
      return fallback.IndexOf(o);

    for(int i=0; i<Count; ++i)
    {
      if(this[i].Equals(o))
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

  public void CopyTo(T[] array, int startIndex)
  {
    int targetSize = Count - startIndex;
    if(targetSize > array.Length)
      throw new ArgumentException(string.Format("Array size {0} less target size {1}", array.Length, targetSize));

    for (int i = startIndex, j = 0; i < Count; i++, j++)
      array[j] = this[i];
  }

  public Enumerator<T> GetEnumerator()
  {
    return new Enumerator<T>(this);
  }

  IEnumerator<T> IEnumerable<T>.GetEnumerator()
  {
    return GetEnumerator();
  }

  // not implemented IList<T> interface methods
  public void Insert(int pos, T o)
  {
    throw new NotImplementedException();
  }
  IEnumerator IEnumerable.GetEnumerator()
  {
    throw new NotImplementedException();
  }

  // not implemented IList interface methods
  public int Add(object value)
  {
    throw new NotImplementedException();
  }
  public bool Contains(object value)
  {
    throw new NotImplementedException();
  }
  public int IndexOf(object value)
  {
    throw new NotImplementedException();
  }
  public void Insert(int index, object value)
  {
    throw new NotImplementedException();
  }
  public void Remove(object value)
  {
    throw new NotImplementedException();
  }
}

public struct Array16<T>
{
  T v0;
  T v1;
  T v2;
  T v3;
  T v4;
  T v5;
  T v6;
  T v7;
  T v8;
  T v9;
  T v10;
  T v11;
  T v12;
  T v13;
  T v14;
  T v15;

  public T this[int i]
  {
    get {
      if(i == 0)
        return v0;
      else if(i == 1)
        return v1;
      else if(i == 2)
        return v2;
      else if(i == 3)
        return v3;
      else if(i == 4)
        return v4;
      else if(i == 5)
        return v5;
      else if(i == 6)
        return v6;
      else if(i == 7)
        return v7;
      else if(i == 8)
        return v8;
      else if(i == 9)
        return v9;
      else if(i == 10)
        return v10;
      else if(i == 11)
        return v11;
      else if(i == 12)
        return v12;
      else if(i == 13)
        return v13;
      else if(i == 14)
        return v14;
      else if(i == 15)
        return v15;
      else
      {
        throw new Exception("Too large idx: " + i);
      }
    }
    set {
      if(i == 0)
        v0 = value;
      else if(i == 1)
        v1 = value;
      else if(i == 2)
        v2 = value;
      else if(i == 3)
        v3 = value;
      else if(i == 4)
        v4 = value;
      else if(i == 5)
        v5 = value;
      else if(i == 6)
        v6 = value;
      else if(i == 7)
        v7 = value;
      else if(i == 8)
        v8 = value;
      else if(i == 9)
        v9 = value;
      else if(i == 10)
        v10 = value;
      else if(i == 11)
        v11 = value;
      else if(i == 12)
        v12 = value;
      else if(i == 13)
        v13 = value;
      else if(i == 14)
        v14 = value;
      else if(i == 15)
        v15 = value;
      else
      {
        throw new Exception("Too large idx: " + i);
      }
    }
  }
}

} //namespace bhl
