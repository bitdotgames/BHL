using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.IO;
using game; //for metagen

namespace bhl {

public class UserError : Exception
{
  public string file;

  public UserError(string file, string str)
    : base(str)
  {
    this.file = file;
  }

  public UserError(string str)
    : base(str)
  {
    this.file = null;
  }

  public string ToJson()
  {
    var msg = Message.Replace("\"", "\\\""); 
    msg = msg.Replace("\n", " ");
    return "{\"error\": \"" + msg + "\", \"file\": \"" + (file == null ? "<?>" : file.Replace("\\", "/")) + "\"}";
  }
}

public static class Hash
{
  static public uint CRC32(string id)
  {
    var bytes = System.Text.Encoding.ASCII.GetBytes(id);
    return Crc32.Compute(bytes, bytes.Length);
  }

  static public uint CRC28(string id)
  {
    return CRC32(id) & 0xFFFFFFF;
  }

  static public uint CRC20(string id)
  {
    return CRC32(id) & 0xFFFFF;
  }

  static public uint CRC32(byte[] bytes)
  {
    return Crc32.Compute(bytes, bytes.Length);
  }

  static public uint CRC28(byte[] bytes)
  {
    return CRC32(bytes) & 0xFFFFFFF;
  }

  static public uint CRC20(byte[] bytes)
  {
    return CRC32(bytes) & 0xFFFFF;
  }
}

public struct HashedName
{
  public ulong n;
  public string s;

  public HashedName(ulong n, string s = "")
  {
    this.n = n;
    this.s = s;
  }

  public HashedName(string s)
    : this(Hash.CRC28(s))
  {
    if(Util.DEBUG)
      this.s = s;
  }

  public static implicit operator HashedName(ulong n)
  {
    return new HashedName(n);
  }

  public static implicit operator HashedName(uint n)
  {
    return new HashedName((ulong)n);
  }

  public static implicit operator HashedName(string s)
  {
    return new HashedName(s);
  }

  public bool IsEmpty()
  {
    return n == 0;
  }

  public override string ToString() 
  {
    return "(" + n + ":" + s + ")";
  }
}

static public class OPool
{
  public struct PoolItem
  {
    public bool used;
    public object obj;
  }

  static public List<PoolItem> pool = new List<PoolItem>();
  static public int hits = 0;
  static public int miss = 0;

  static public T Request<T>() where T : class, new()
  {
    for(int i=0;i<pool.Count;++i)
    {
      var item = pool[i];
      if(!item.used && item.obj is T)
      {
        ++hits;

        var res = item.obj as T;
        item.used = true;
        pool[i] = item;
        return res;
      }
    }

    {
      var res = new T();
      AddToPool(res);
      return res;
    }
  }

  static void AddToPool(object obj)
  {
    ++miss;

    var item = new PoolItem();
    item.used = true;
    item.obj = obj;
    pool.Add(item);
  }

  static public void Release(object obj)
  {
    if(obj == null)
      return;

    for(int i=0;i<pool.Count;++i)
    {
      var item = pool[i];
      if(item.obj == obj)
      {
        item.used = false;
        pool[i] = item;
        break;
      }
    }
  }

  static public void Clear()
  {
    hits = 0;
    miss = 0;
    pool.Clear();
  }
}

public static class Extensions
{
  public static List<string> GetStringKeys(this OrderedDictionary col)
  {
    List<string> res = new List<string>();
    foreach(var k in col.Keys)
    {
      var str = (string)k;
      res.Add(str);
    }
    return res;
  }

  public static int FindStringKeyIndex(this OrderedDictionary col, string key)
  {
    int idx = 0;
    foreach(var k in col.Keys)
    {
      if((string)k == key)
        return idx;
      ++idx;
    }
    return -1;
  }

  public static void Append(this AST dst, AST src)
  {
    for(int i=0;i<src.children.Count;++i)
    {
      dst.children.Add(src.children[i]);
    }
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
}

static public class Util
{
  static public bool DEBUG = false;

  public delegate void LogDebugCb(string text);
  static public LogDebugCb Debug = DefaultDebug;

  static public void DefaultDebug(string str)
  {
    Console.WriteLine("[DEBUG] " + str);
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

  static public string ResolveImportPath(List<string> inc_paths, string self_path, string path)
  {
    //relative import
    if(!Path.IsPathRooted(path))
    {
      var dir = Path.GetDirectoryName(self_path);
      return Path.GetFullPath(Path.Combine(dir, path) + ".bhl");
    }
    //absolute import
    else
      return TryIncludePaths(inc_paths, path);
  }

  static string TryIncludePaths(List<string> inc_paths, string path)
  {
    for(int i=0;i<inc_paths.Count;++i)
    {
      var file_path = Path.GetFullPath(inc_paths[i] + "/" + path  + ".bhl");
      if(File.Exists(file_path))
        return file_path;
    }
    throw new Exception("Could not find file for path '" + path + "'");
  }

  ////////////////////////////////////////////////////////

  static public bool NeedToRegen(string file, List<string> deps)
  {
    if(!File.Exists(file))
      return true;

    var fmtime = new FileInfo(file).LastWriteTime; 
    foreach(var dep in deps)
    {
      if(File.Exists(dep) && (new FileInfo(dep).LastWriteTime > fmtime))
        return true;
    }

    return false;
  }

  ////////////////////////////////////////////////////////

  static public T File2Meta<T>(string file) where T : game.IMetaStruct, new()
  {
    using(FileStream rfs = File.Open(file, FileMode.Open, FileAccess.Read))
    {
      return Bin2Meta<T>(rfs);
    }
  }

  static game.MetaHelper.CreateByIdCb prev_create_by_id; 

  static public void SetupAutogenFactory()
  {
    prev_create_by_id = game.MetaHelper.CreateById;
    game.MetaHelper.CreateById = AutogenBundle.createById;
  }

  static public void RestoreAutogenFactory()
  {
    game.MetaHelper.CreateById = prev_create_by_id;
  }

  static public T Bin2Meta<T>(Stream s) where T : game.IMetaStruct, new()
  {
    var reader = new game.MsgPackDataReader(s);
    var ast = new T();
    var err = ast.read(reader);

    if(err != game.MetaIoError.SUCCESS)
      throw new Exception("Could not read: " + err);

    return ast;
  }

  static public T Bin2Meta<T>(byte[] bytes) where T : game.IMetaStruct, new()
  {
    return Bin2Meta<T>(new MemoryStream(bytes));
  }

  static public void Meta2Bin<T>(T ast, Stream dst) where T : game.IMetaStruct
  {
    var writer = new game.MsgPackDataWriter(dst);
    var err = ast.write(writer);

    if(err != game.MetaIoError.SUCCESS)
      throw new Exception("Could not write: " + err);
  }

  static public void Meta2File<T>(T ast, string file) where T : game.IMetaStruct
  {
    using(FileStream wfs = new FileStream(file, FileMode.Create, System.IO.FileAccess.Write))
    {
      Meta2Bin(ast, wfs);
    }
  }

  static public ulong GetFuncId(uint mod_id, string name)
  {
    return GetFuncId(mod_id, Hash.CRC28(name));
  }

  static public ulong GetFuncId(uint mod_id, uint name)
  {
    return ((ulong)mod_id << 31) | ((ulong)name);
  }

  static public ulong GetFuncId(string module, string name)
  {
    return GetFuncId(GetModuleId(module), name);
  }

  static public uint GetModuleId(string module)
  {
    return Hash.CRC32(module);
  }
}

static public class AST_Util
{
  static public void AddChild(this AST self, AST c)
  {
    if(c == null)
      return;
    self.children.Add(c);
  }

  static public AST NewInterimChild(this AST self)
  {
    var c = AST_Util.New_Interim();
    self.AddChild(c);
    return c;
  }

  ////////////////////////////////////////////////////////

  static public AST_Interim New_Interim()
  {
    return new AST_Interim();
  }

  ////////////////////////////////////////////////////////

  static public AST_Module New_Module(uint nname)
  {
    var n = new AST_Module();
    n.nname = nname;
    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_Import New_Imports()
  {
    var n = new AST_Import();
    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_FuncDecl New_FuncDecl(uint mod_id, string type, string name)
  {
    var n = new AST_FuncDecl();
    Init_FuncDecl(n, mod_id, type, name);
    return n;
  }

  static void Init_FuncDecl(AST_FuncDecl n, uint mod_id, string type, string name)
  {
    n.ntype = Hash.CRC28(type); 
    if(Util.DEBUG)
      n.type = type;

    n.nname2 = mod_id;
    n.nname1 = Hash.CRC28(name);

    if(Util.DEBUG)
      n.name = name;

    //fparams
    n.NewInterimChild();
    //block
    n.NewInterimChild();
  }

  static public AST fparams(this AST_FuncDecl n)
  {
    return n.children[0];
  }

  static public AST block(this AST_FuncDecl n)
  {
    return n.children[1];
  }

  static public int GetDefaultArgsNum(this AST_FuncDecl n)
  {
    var fparams = n.fparams();
    if(fparams.children.Count == 0)
      return 0;

    var sub_fparams = fparams.children[0];
    int num = 0;
    for(int i=0;i<sub_fparams.children.Count;++i)
    {
      var fc = sub_fparams.children[i];
      if(fc.children.Count > 0)
        ++num;
    }
    return num;
  }

  static public int GetTotalArgsNum(this AST_FuncDecl n)
  {
    var fparams = n.fparams();
    if(fparams.children.Count == 0)
      return 0;
    int num = fparams.children[0].children.Count;
    return num;
  }

  static public HashedName Name(this AST_FuncDecl n)
  {
    return (n == null) ? new HashedName(0, "?") : new HashedName(n.nname(), n.name);
  }

  static public ulong nname(this AST_FuncDecl n)
  {
    return ((ulong)n.nname2 << 31) | ((ulong)n.nname1);
  }


  ////////////////////////////////////////////////////////

  static public AST_LambdaDecl New_LambdaDecl(uint mod_id, string type, string name)
  {
    var n = new AST_LambdaDecl();
    Init_FuncDecl(n, mod_id, type, name);

    return n;
  }

  static public HashedName Name(this AST_LambdaDecl n)
  {
    return new HashedName(n.nname(), n.name);
  }

  static public HashedName Name(this AST_UseParam n)
  {
    return new HashedName(n.nname, n.name);
  }

  ////////////////////////////////////////////////////////
  
  static public AST_UnaryOpExp New_UnaryOpExp(EnumUnaryOp type)
  {
    var n = new AST_UnaryOpExp();
    n.type = type;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_BinaryOpExp New_BinaryOpExp(EnumBinaryOp type)
  {
    var n = new AST_BinaryOpExp();
    n.type = type;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_TypeCast New_TypeCast(string type)
  {
    var n = new AST_TypeCast();
    n.ntype = Hash.CRC28(type);
    if(Util.DEBUG)
    {
      n.type = type;
    }

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_UseParam New_UseParam(string name)
  {
    var n = new AST_UseParam();
    n.nname = Hash.CRC28(name);
    if(Util.DEBUG)
    {
      n.name = name;
    }

    return n;
  }

  ////////////////////////////////////////////////////////

  static public void SplitName(ulong nname, out uint nname1, out uint nname2)
  {
    nname1 = (uint)(nname & 0xFFFFFFFF);
    nname2 = (uint)(nname >> 31);
  }

  ////////////////////////////////////////////////////////

  static public AST_Call New_Call(EnumCall type, string name, ulong nname, Symbol scope_symb = null)
  {
    var n = new AST_Call();
    n.type = type;
    n.nname1 = (uint)(nname & 0xFFFFFFFF);
    n.nname2 = (uint)(nname >> 31);
    if(Util.DEBUG)
    {
      n.name = name;
    }
    n.scope_ntype = scope_symb != null ? scope_symb.nname : 0;

    return n;
  }

  static public HashedName Name(this AST_Call n)
  {
    return new HashedName(n.nname(), n.name);
  }

  static public ulong nname(this AST_Call n)
  {
    return ((ulong)n.nname2 << 31) | ((ulong)n.nname1);
  }

  static public ulong FuncId(this AST_Call n)
  {
    if(n.nname2 == 0)
      return ((ulong)n.scope_ntype << 31) | ((ulong)n.nname1);
    else
      return ((ulong)n.nname2 << 31) | ((ulong)n.nname1);
  }

  ////////////////////////////////////////////////////////

  static public AST_Return New_Return()
  {
    return new AST_Return();
  }

  static public AST_Break New_Break()
  {
    return new AST_Break();
  }

  ////////////////////////////////////////////////////////

  static public AST_New New_New(string type)
  {
    var n = new AST_New();
    n.ntype = Hash.CRC28(type);
    if(Util.DEBUG)
    {
      n.type = type;
    }

    return n;
  }

  static public HashedName Name(this AST_New n)
  {
    return new HashedName(n.ntype, n.type);
  }

  ////////////////////////////////////////////////////////

  static public AST_Literal New_Literal(EnumLiteral type)
  {
    var n = new AST_Literal();
    n.type = type;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_VarDecl New_VarDecl(string type, string name, bool is_ref)
  {
    var n = new AST_VarDecl();
    n.type = type;
    n.nname = Hash.CRC28(name) | ((is_ref ? 0u : 1u) << 29);
    if(Util.DEBUG)
    {
      n.name = name;
    }

    return n;
  }

  static public HashedName Name(this AST_VarDecl n)
  {
    return new HashedName(n.nname & 0xFFFFFFF, n.name);
  }

  static public bool IsRef(this AST_VarDecl n)
  {
    return (n.nname & (1u << 29)) != 0; 
  }

  ////////////////////////////////////////////////////////

  static public AST_Assign New_Assign()
  {
    var n = new AST_Assign();
    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_Block New_Block(EnumBlock type)
  {
    var n = new AST_Block();
    n.type = type;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_JsonObj New_JsonObj(string root_type_name)
  {
    var n = new AST_JsonObj();
    n.ntype = Hash.CRC28(root_type_name);
    return n;
  }

  static public AST_JsonArr New_JsonArr(ArrayTypeSymbol arr_type)
  {
    var n = new AST_JsonArr();
    n.ntype = arr_type.nname;
    return n;
  }

  static public AST_JsonPair New_JsonPair(string scope_type, string name)
  {
    var n = new AST_JsonPair();
    //NOTE: using CRC28 for compatibility with metagen
    n.scope_ntype = Hash.CRC28(scope_type);
    n.nname = Hash.CRC28(name);
    if(Util.DEBUG)
    {
      n.name = name;
    }

    return n;
  }

  static public HashedName Name(this AST_JsonPair n)
  {
    return new HashedName(n.nname, n.name);
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

public class AST_Dumper : AST_Visitor
{
  public override void DoVisit(AST_Interim node)
  {
    Console.Write("(");
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_Module node)
  {
    Console.Write("(MODULE " + node.nname);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_Import node)
  {
    Console.Write("(IMPORT ");
    foreach(var m in node.modules)
      Console.Write("" + m);
    Console.Write(")");
  }

  public override void DoVisit(AST_FuncDecl node)
  {
    Console.Write("(FUNC ");
    Console.Write(node.type + " " + node.name + " " + node.nname());
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_LambdaDecl node)
  {
    Console.Write("(LMBD ");
    Console.Write(node.type + " " + node.nname() + " USE:");
    for(int i=0;i<node.useparams.Count;++i)
      Console.Write(" " + node.useparams[i].nname);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_Block node)
  {
    Console.Write("(BLOCK ");
    Console.Write(node.type + " ");
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_TypeCast node)
  {
    Console.Write("(CAST ");
    Console.Write(node.type + " ");
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_Call node)
  {
    Console.Write("(CALL ");
    Console.Write(node.type + " " + node.name + " " + node.nname());
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_Return node)
  {
    Console.Write("(RET ");
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_Break node)
  {
    Console.Write("(BRK ");
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_Literal node)
  {
    if(node.type == EnumLiteral.NUM)
      Console.Write(" (" + node.nval + ")");
    else if(node.type == EnumLiteral.BOOL)
      Console.Write(" (" + node.nval + ")");
    else if(node.type == EnumLiteral.STR)
      Console.Write(" ('" + node.sval + "')");
  }

  public override void DoVisit(AST_BinaryOpExp node)
  {
    Console.Write("(BOP " + node.type);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_UnaryOpExp node)
  {
    Console.Write("(UOP " + node.type);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_New node)
  {
    Console.Write("(NEW " + node.type + " " + node.ntype);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_Assign node)
  {
    Console.Write("(ASSIGN ");
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_VarDecl node)
  {
    Console.Write("(VARDECL " + node.type + " " + node.name + " " + node.nname);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_JsonObj node)
  {
    Console.Write("(JOBJ " + node.ntype);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_JsonArr node)
  {
    Console.Write("(JARR ");
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_JsonPair node)
  {
    Console.Write("(JPAIR " + node.name + " " + node.nname);
    VisitChildren(node);
    Console.Write(")");
  }
}

public class FastStackDynamic<T> : List<T>
{
	public FastStackDynamic(int startingCapacity)
		: base(startingCapacity)
	{}

	public T Push(T item)
	{
		this.Add(item);
		return item;
	}

	public void Expand(int size)
	{
		for(int i = 0; i < size; i++)
			this.Add(default(T));
	}

	public void Zero(int index)
	{
		this[index] = default(T);
	}

	public T Peek(int idxofs = 0)
	{
		T item = this[this.Count - 1 - idxofs];
		return item;
	}
	public void CropAtCount(int p)
	{
		RemoveLast(Count - p);
	}

	public void RemoveLast( int cnt = 1)
	{
		if (cnt == 1)
		{
			this.RemoveAt(this.Count - 1);
		}
		else
		{
			this.RemoveRange(this.Count - cnt, cnt);
		}
	}

	public T Pop()
	{
		T retval = this[this.Count - 1];
		this.RemoveAt(this.Count - 1);
		return retval;
	}
}

public class FastStack<T>
{
  T[] m_Storage;
  int m_HeadIdx = 0;

  public FastStack(int maxCapacity)
  {
    m_Storage = new T[maxCapacity];
  }

  public T this[int index]
  {
    get { return m_Storage[index]; }
    set { m_Storage[index] = value; }
  }

  public T Push(T item)
  {
    m_Storage[m_HeadIdx++] = item;
    return item;
  }

  public T Peek(int idxofs = 0)
  {
    T item = m_Storage[m_HeadIdx - 1 - idxofs];
    return item;
  }

  public void Set(int idxofs, T item)
  {
    m_Storage[m_HeadIdx - 1 - idxofs] = item;
  }

  public T Pop()
  {
    --m_HeadIdx;
    T retval = m_Storage[m_HeadIdx];
    m_Storage[m_HeadIdx] = default(T);
    return retval;
  }

  public T PopFast()
  {
    --m_HeadIdx;
    return  m_Storage[m_HeadIdx];
  }

  public void Clear()
  {
    Array.Clear(m_Storage, 0, m_Storage.Length);
    m_HeadIdx = 0;
  }

  public int Count
  {
    get { return m_HeadIdx; }
  }
}

//NOTE: cache for less GC operations
public static class TempBuffer
{
  static byte[][] tmp_bufs = new byte[][] { new byte[512], new byte[512], new byte[512] };
  static int tmp_buf_counter = 0;
  static public int stats_max_buf = 0;

  static public byte[] Get()
  {
    var buf = tmp_bufs[tmp_buf_counter % 3];
    ++tmp_buf_counter;
    return buf;
  }

  static public void Update(byte[] buf)
  {
    var idx = (tmp_buf_counter-1) % 3;
    //for debug
    //var curr = tmp_bufs[idx];
    //if(curr != buf)
    //  Log.Debug(curr.Length + " VS " + buf.Length);
    tmp_bufs[idx] = buf;
    stats_max_buf = stats_max_buf < buf.Length ? buf.Length : stats_max_buf;
  }
}

} //namespace bhl
