using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.IO;

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
    var msg = Message.Replace("\\", " ");
    msg = msg.Replace("\n", " ");
    msg = msg.Replace("\r", " ");
    msg = msg.Replace("\"", "\\\""); 
    return "{\"error\": \"" + msg + "\", \"file\": \"" + (file == null ? "<?>" : file.Replace("\\", "/")) + "\"}";
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
}

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

  static MetaHelper.CreateByIdCb prev_create_factory; 

  static public void SetupASTFactory()
  {
    prev_create_factory = MetaHelper.CreateById;
    MetaHelper.CreateById = AST_Factory.createById;
  }

  static public void RestoreASTFactory()
  {
    MetaHelper.CreateById = prev_create_factory;
  }

  static public T File2Meta<T>(string file) where T : IMetaStruct, new()
  {
    using(FileStream rfs = File.Open(file, FileMode.Open, FileAccess.Read))
    {
      return Bin2Meta<T>(rfs);
    }
  }

  static public T Bin2Meta<T>(Stream s) where T : IMetaStruct, new()
  {
    var reader = new MsgPackDataReader(s);
    var meta = new T();
    MetaHelper.sync(MetaSyncContext.NewForRead(reader), ref meta);
    return meta;
  }

  static public T Bin2Meta<T>(byte[] bytes) where T : IMetaStruct, new()
  {
    return Bin2Meta<T>(new MemoryStream(bytes));
  }

  static public void Meta2Bin<T>(T meta, Stream dst) where T : IMetaStruct
  {
    var writer = new MsgPackDataWriter(dst);
    MetaHelper.sync(MetaSyncContext.NewForWrite(writer), ref meta);
  }

  static public void Meta2File<T>(T meta, string file) where T : IMetaStruct
  {
    using(FileStream wfs = new FileStream(file, FileMode.Create, System.IO.FileAccess.Write))
    {
      Meta2Bin(meta, wfs);
    }
  }

  static public void Compiled2Bin(CompiledModule m, Stream dst)
  {
    using(BinaryWriter w = new BinaryWriter(dst, System.Text.Encoding.UTF8))
    {
      //TODO: add better support for version
      w.Write((uint)1);

      w.Write(m.name);

      w.Write(m.initcode == null ? (int)0 : m.initcode.Length);
      if(m.initcode != null)
        w.Write(m.initcode, 0, m.initcode.Length);

      w.Write(m.bytecode == null ? (int)0 : m.bytecode.Length);
      if(m.bytecode != null)
        w.Write(m.bytecode, 0, m.bytecode.Length);

      w.Write(m.constants.Count);
      foreach(var cn in m.constants)
      {
        w.Write((byte)cn.type);
        if(cn.type == EnumLiteral.STR)
          w.Write(cn.str);
        else
          w.Write(cn.num);
      }

      //TODO: add this info only for development builds
      w.Write(m.ip2src_line.Count);
      foreach(var kv in m.ip2src_line)
      {
        w.Write(kv.Key);
        w.Write(kv.Value);
      }
    }
  }

  static public void Compiled2File(CompiledModule m, string file)
  {
    using(FileStream wfs = new FileStream(file, FileMode.Create, System.IO.FileAccess.Write))
    {
      Compiled2Bin(m, wfs);
    }
  }

  static public CompiledModule Bin2Compiled(Stream src)
  {
    using(BinaryReader r = new BinaryReader(src, System.Text.Encoding.UTF8, true/*leave open*/))
    {
      //TODO: add better support for version
      uint version = r.ReadUInt32();
      if(version != 1)
        throw new Exception("Unsupported version: " + version);

      string name = r.ReadString();

      byte[] initcode = null;
      int initcode_len = r.ReadInt32();
      if(initcode_len > 0)
        initcode = r.ReadBytes(initcode_len);

      byte[] bytecode = null;
      int bytecode_len = r.ReadInt32();
      if(bytecode_len > 0)
        bytecode = r.ReadBytes(bytecode_len);

      var constants = new List<Const>();
      int constants_len = r.ReadInt32();
      for(int i=0;i<constants_len;++i)
      {
        var cn_type = (EnumLiteral)r.Read();
        double cn_num = 0;
        string cn_str = "";
        if(cn_type == EnumLiteral.STR)
          cn_str = r.ReadString();
        else
          cn_num = r.ReadDouble();
        var cn = new Const(cn_type, cn_num, cn_str);
        constants.Add(cn);
      }

      var ip2src_line = new Dictionary<int, int>();
      int ip2src_line_len = r.ReadInt32();
      for(int i=0;i<ip2src_line_len;++i)
        ip2src_line.Add(r.ReadInt32(), r.ReadInt32());

      return new CompiledModule(name, bytecode, constants, initcode, ip2src_line);
    }
  }

  public static void ASTDump(AST ast)
  {
    new AST_Dumper().Visit(ast);
    Console.WriteLine("\n=============");
  }
}

public struct FuncArgsInfo
{
  //NOTE: 6 bits are used for a total number of args passed (max 63), 
  //      26 bits are reserved for default args set bits (max 26 default args)
  const int ARGS_NUM_BITS = 6;
  const uint ARGS_NUM_MASK = ((1 << ARGS_NUM_BITS) - 1);
  const int MAX_ARGS = (int)ARGS_NUM_MASK;
  const int MAX_DEFAULT_ARGS = 32 - ARGS_NUM_BITS; 

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

static public class AST_Util
{
  static public List<AST_Base> GetChildren(this AST_Base self)
  {
    var ast = self as AST;
    return ast == null ? null : ast.children;
  }

  static public void AddChild(this AST_Base self, AST_Base c)
  {
    if(c == null)
      return;
    var ast = self as AST;
    ast.children.Add(c);
  }

  static public void AddChild(this AST self, AST_Base c)
  {
    if(c == null)
      return;
    self.children.Add(c);
  }

  static public void AddChildren(this AST self, AST_Base b)
  {
    if(b is AST c)
    {
      for(int i=0;i<c.children.Count;++i)
        self.AddChild(c.children[i]);
    }
  }

  static public AST NewInterimChild(this AST self)
  {
    var c = new AST_Interim();
    self.AddChild(c);
    return c;
  }

  ////////////////////////////////////////////////////////

  static public AST_Module New_Module(uint id, string name)
  {
    var n = new AST_Module();
    n.id = id;
    n.name = name;
    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_Import New_Imports()
  {
    var n = new AST_Import();
    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_FuncDecl New_FuncDecl(string name, uint module_id, string type)
  {
    var n = new AST_FuncDecl();
    Init_FuncDecl(n, name, module_id, type);
    return n;
  }

  static void Init_FuncDecl(AST_FuncDecl n, string name, uint module_id, string type)
  {
    n.type = type;
    n.module_id = module_id;
    n.name = name;
    //fparams
    n.NewInterimChild();
    //block
    n.NewInterimChild();
  }

  static public AST fparams(this AST_FuncDecl n)
  {
    return n.GetChildren()[0] as AST;
  }

  static public AST block(this AST_FuncDecl n)
  {
    return n.GetChildren()[1] as AST;
  }

  static public int GetDefaultArgsNum(this AST_FuncDecl n)
  {
    var fparams = n.fparams();
    if(fparams.children.Count == 0)
      return 0;

    int num = 0;
    for(int i=0;i<fparams.children.Count;++i)
    {
      var fc = fparams.children[i].GetChildren();
      if(fc != null && fc.Count > 0)
        ++num;
    }
    return num;
  }

  static public int GetTotalArgsNum(this AST_FuncDecl n)
  {
    var fparams = n.fparams();
    if(fparams.children.Count == 0)
      return 0;
    int num = fparams.children.Count;
    return num;
  }

  ////////////////////////////////////////////////////////

  static public AST_LambdaDecl New_LambdaDecl(string name, uint module_id, string type)
  {
    var n = new AST_LambdaDecl();
    Init_FuncDecl(n, name, module_id, type);

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_ClassDecl New_ClassDecl(string name, string parent)
  {
    var n = new AST_ClassDecl();
    n.name = name;
    n.parent = parent;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_EnumDecl New_EnumDecl(string name)
  {
    var n = new AST_EnumDecl();
    n.name = name;

    return n;
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
    n.type = type;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_UpVal New_UpVal(string name, int symb_idx, int upsymb_idx)
  {
    var n = new AST_UpVal();
    n.name = name;
    n.symb_idx = (uint)symb_idx;
    n.upsymb_idx = (uint)upsymb_idx;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_Call New_Call(EnumCall type, int line_num, string name = "", uint module_id = 0, ClassSymbol scope_symb = null, int symb_idx = 0)
  {
    return New_Call(type, line_num, name, scope_symb != null ? scope_symb.Type() : "", symb_idx, module_id);
  }

  static public AST_Call New_Call(EnumCall type, int line_num, VariableSymbol symb, ClassSymbol scope_symb = null)
  {
    return New_Call(type, line_num, symb.name, scope_symb != null ? scope_symb.Type() : "", symb.scope_idx, symb.module_id);
  }

  static public AST_Call New_Call(EnumCall type, int line_num, string name, string scope_type, int symb_idx = 0, uint module_id = 0)
  {
    var n = new AST_Call();
    n.type = type;
    n.name = name;
    n.scope_type = scope_type;
    n.line_num = (uint)line_num;
    n.symb_idx = (uint)symb_idx;
    n.module_id = module_id;

    return n;
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
  
  static public AST_Continue New_Continue(bool jump_marker = false)
  {
    return new AST_Continue() {
      jump_marker = jump_marker
    };
  }
  
  ////////////////////////////////////////////////////////

  static public AST_PopValue New_PopValue()
  {
    return new AST_PopValue();
  }

  ////////////////////////////////////////////////////////

  static public AST_New New_New(ClassSymbol type)
  {
    var n = new AST_New();
    n.type = type.Type();

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_Literal New_Literal(EnumLiteral type)
  {
    var n = new AST_Literal();
    n.type = type;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_VarDecl New_VarDecl(VariableSymbol symb, bool is_ref)
  {
    return New_VarDecl(symb.name, is_ref, symb is FuncArgSymbol, symb.type.name, symb.scope_idx);
  }

  static public AST_VarDecl New_VarDecl(string name, bool is_ref, bool is_func_arg, string type, int symb_idx)
  {
    var n = new AST_VarDecl();
    n.name = name;
    n.type = type;
    n.is_ref = is_ref;
    n.is_func_arg = is_func_arg;
    n.symb_idx = (uint)symb_idx;

    return n;
  }

  ////////////////////////////////////////////////////////

  static public AST_Inc New_Inc(VariableSymbol symb)
  {
    var n = new AST_Inc();
    n.symb_idx = (uint)symb.scope_idx;

    return n;
  }
  
  ////////////////////////////////////////////////////////

  static public AST_Dec New_Dec(VariableSymbol symb)
  {
    var n = new AST_Dec();
    n.symb_idx = (uint)symb.scope_idx;

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
    n.type = root_type_name;
    return n;
  }

  static public AST_JsonArr New_JsonArr(ArrayTypeSymbol arr_type)
  {
    var n = new AST_JsonArr();
    n.type = arr_type.Type();
    return n;
  }

  static public AST_JsonArrAddItem New_JsonArrAddItem()
  {
    var n = new AST_JsonArrAddItem();
    return n;
  }

  static public AST_JsonPair New_JsonPair(string scope_type, string name, int symb_idx)
  {
    var n = new AST_JsonPair();
    n.scope_type = scope_type;
    n.name = name;
    n.symb_idx = (uint)symb_idx;

    return n;
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
    Console.Write("(MODULE " + node.id + " " + node.name);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_Import node)
  {
    Console.Write("(IMPORT ");
    foreach(var m in node.module_names)
      Console.Write("" + m);
    Console.Write(")");
  }

  public override void DoVisit(AST_FuncDecl node)
  {
    Console.Write("(FUNC ");
    Console.Write(node.type + " " + node.name);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_LambdaDecl node)
  {
    Console.Write("(LMBD ");
    Console.Write(node.type + " " + node.name);
    if(node.upvals.Count > 0)
      Console.Write(" USE:");
    for(int i=0;i<node.upvals.Count;++i)
      Console.Write(" " + node.upvals[i].name);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_ClassDecl node)
  {
    Console.Write("(CLASS ");
    Console.Write(node.name);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_EnumDecl node)
  {
    Console.Write("(ENUM ");
    Console.Write(node.name);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_Block node)
  {
    Console.Write("(BLK ");
    Console.Write(node.type + " {");
    VisitChildren(node);
    Console.Write("})");
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
    Console.Write(node.type + " " + node.name);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_Inc node)
  {
    Console.Write("(INC ");
    Console.Write(node.symb_idx);
    Console.Write(")");
  }
  
  public override void DoVisit(AST_Dec node)
  {
    Console.Write("(DEC ");
    Console.Write(node.symb_idx);
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
    Console.Write("(BRK)");
  }

  public override void DoVisit(AST_Continue node)
  {
    Console.Write("(CONT)");
  }

  public override void DoVisit(AST_PopValue node)
  {
    Console.Write("(POP)");
  }

  public override void DoVisit(AST_Literal node)
  {
    if(node.type == EnumLiteral.NUM)
      Console.Write(" (NUM " + node.nval + ")");
    else if(node.type == EnumLiteral.BOOL)
      Console.Write(" (BOOL " + node.nval + ")");
    else if(node.type == EnumLiteral.STR)
      Console.Write(" (STR '" + node.sval + "')");
    else if(node.type == EnumLiteral.NIL)
      Console.Write(" (NULL)");
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
    Console.Write("(NEW " + node.type);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_VarDecl node)
  {
    Console.Write("(VARDECL " + node.name);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_JsonObj node)
  {
    Console.Write("(JOBJ " + node.type);
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
    Console.Write("(JPAIR " + node.name);
    VisitChildren(node);
    Console.Write(")");
  }

  public override void DoVisit(AST_JsonArrAddItem node)
  {
    Console.Write("(JARDD)");
  }
}

public class FixedStack<T>
{
  T[] storage;
  int head = 0;

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
      //for extra debug
      //ValidateIndex(index);
      return storage[index]; 
    }
    set { 
      //for extra debug
      //ValidateIndex(index);
      storage[index] = value; 
    }
  }

  void ValidateIndex(int index)
  {
    if(index < 0 || index >= head)
      throw new Exception("Out of index " + index + " vs " + head);
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
    //  Util.Debug(curr.Length + " VS " + buf.Length);
    tmp_bufs[idx] = buf;
    stats_max_buf = stats_max_buf < buf.Length ? buf.Length : stats_max_buf;
  }
}

// A dictionary that remembers the order that keys were first inserted. If a new entry overwrites an existing entry, 
// the original insertion position is left unchanged. 
// Deleting an entry and reinserting it will move it to the end.
public class OrderedDictionary<TKey, TValue>
{
  // An unordered dictionary of key pairs.
  Dictionary<TKey, TValue> dict;

  // The keys of the dictionary in the exposed order.
  List<TKey> keys = new List<TKey>();

  public OrderedDictionary()
  {
    dict = new Dictionary<TKey, TValue>();
  }

  public OrderedDictionary(IEqualityComparer<TKey> cmp)
  {
    dict = new Dictionary<TKey, TValue>(cmp);
  }

  public int Count
  {
    get {
      return keys.Count;
    }
  }

  public ICollection<TKey> Keys
  {
    get {
      return keys.AsReadOnly();
    }
  }

  // The value at the given index.
  public TValue this[int index]
  {
    get {
      var key = keys[index];
      return dict[key];
    }
    set {
      var key = keys[index];
      dict[key] = value;
    }
  }

  public int IndexOf(TKey key)
  {
    return keys.IndexOf(key);
  }

  public void RemoveAt(int index)
  {
    var key = keys[index];
    dict.Remove(key);
    keys.RemoveAt(index);
  }

  public bool ContainsKey(TKey key)
  {
    return dict.ContainsKey(key);
  }

  public bool TryGetValue(TKey key, out TValue value)
  {
    return dict.TryGetValue(key, out value);
  }

  public void Insert(int index, TKey key, TValue value)
  {
    // Dictionary operation first, so exception thrown if key already exists.
    dict.Add(key, value);
    keys.Insert(index, key);
  }

  public void Add(TKey key, TValue value)
  {
    // Dictionary operation first, so exception thrown if key already exists.
    dict.Add(key, value);
    keys.Add(key);
  }

  public void Remove(TKey key)
  {
    dict.Remove(key);
    keys.Remove(key);
  }

  public void Clear()
  {
    dict.Clear();
    keys.Clear();
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

} //namespace bhl
