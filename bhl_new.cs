using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

public static class Tasks
{
  [Task()]
  public static void build_front_dll(Taskman tm, string[] args)
  {
    MCSBuild(tm, new string[] {
      $"{BHL_ROOT}/deps/msgpack/Compiler/*.cs",
      $"{BHL_ROOT}/deps/msgpack/*.cs",
      $"{BHL_ROOT}/deps/metagen.cs",
      $"{BHL_ROOT}/src/g/*.cs",
      $"{BHL_ROOT}/src/*.cs",
      $"{BHL_ROOT}/Antlr4.Runtime.Standard.dll", 
      $"{BHL_ROOT}/lz4.dll", 
     },
     $"{BHL_ROOT}/bhl_front.dll",
     "-define:BHL_FRONT -warnaserror -warnaserror-:3021 -nowarn:3021 -debug -target:library"
    );
  }

  [Task()]
  public static void build_back_dll(Taskman tm, string[] args)
  {
    var mcs_bin = args.Length > 0 ? args[0] : "/Applications/Unity/Unity.app/Contents/Frameworks/Mono/bin/gmcs"; 
    var dll_file = args.Length > 1 ? args[1] : $"{BHL_ROOT}/bhl_back.dll";
    var extra_args = args.Length > 2 ? args[2] : "-debug";

    MCSBuild(tm, new string[] {
      $"{BHL_ROOT}/deps/msgpack/Compiler/*.cs",
      $"{BHL_ROOT}/deps/msgpack/*.cs",
      $"{BHL_ROOT}/deps/metagen.cs",
      $"{BHL_ROOT}/src/ast.cs", 
      $"{BHL_ROOT}/src/symbol.cs", 
      $"{BHL_ROOT}/src/scope.cs", 
      $"{BHL_ROOT}/src/backend.cs", 
      $"{BHL_ROOT}/src/loader.cs", 
      $"{BHL_ROOT}/src/storage.cs", 
      $"{BHL_ROOT}/src/nodes.cs", 
      $"{BHL_ROOT}/src/util.cs",
      $"{BHL_ROOT}/src/lz4.cs",
     }, 
      dll_file,
      $"{extra_args} -target:library",
      mcs_bin
    );
  }

  [Task()]
  public static void clean(Taskman tm, string[] args)
  {
    tm.Rm("$BHL_ROOT/src/autogen.cs");

    foreach(var dll in tm.Glob($"{BHL_ROOT}/bhl_*.dll"))
    {
      tm.Rm(dll);
      tm.Rm($"{dll}.mdb");
    }

    foreach(var exe in tm.Glob($"{BHL_ROOT}/*.exe"))
    {
      tm.Rm(exe);
      tm.Rm($"{exe}.mdb");
    }
  }

  /////////////////////////////////////////////////

  public static string BHL_ROOT {
    get {
      return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
    }
  }

  public static bool IsWin {
    get {
      return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }
  }


  public static void MCSBuild(Taskman tm, string[] srcs, string result, string opts = "", string binary = "mcs")
  {
    var files = new List<string>();
    foreach(var s in srcs)
      files.AddRange(tm.Glob(s));

    foreach(var f in files)
      if(!File.Exists(f))
        throw new Exception($"Bad file {f}");

    var refs = new List<string>();
    for(int i=files.Count;i-- > 0;)
    {
      if(files[i].EndsWith(".dll"))
      {
        refs.Add(files[i]);
        files.RemoveAt(i);
      }
    }

    string args = (refs.Count > 0 ? " -r:" + String.Join(" -r:", refs.Select(r => CLIPath(r))) : "") + $" {opts} -out:{CLIPath(result)} " + String.Join(" ", files.Select(f => CLIPath(f)));
    string cmd = binary + " " + args; 

    uint cmd_hash = Hash.CRC32(cmd);
    string cmd_hash_file = $"{BHL_ROOT}/build/" + Hash.CRC32(result) + ".mhash";
    if(!File.Exists(cmd_hash_file) || File.ReadAllText(cmd_hash_file) != cmd_hash.ToString()) 
      tm.Write(cmd_hash_file, cmd_hash.ToString());

    files.Add(cmd_hash_file);

    if(tm.NeedToRegen(result, files) || tm.NeedToRegen(result, refs))
      tm.Shell(cmd);
  }

  public static string CLIPath(string p)
  {
    if(IsWin)
    {
      p = "\"" + Path.GetFullPath(p.Trim(new char[]{'"'})) + "\"";
      return p;
    }
    else
    {
      p = "'" + Path.GetFullPath(p.Trim(new char[]{'\''})) + "'";
      return p;
    }
  }
}

public static class BHL
{
  public static void Main(string[] args)
  {
    var tm = new Taskman(typeof(Tasks));
    tm.Run(args);
  }
}

public class Taskman
{
  List<MethodInfo> tasks = new List<MethodInfo>();

  public Taskman(Type tasks_class)
  {
    foreach(var method in tasks_class.GetMethods())
    {
      if(IsTask(method))
        tasks.Add(method);
    }
  }

  static bool IsTask(MemberInfo member)
  {
    foreach(var attribute in member.GetCustomAttributes(true))
    {
      if(attribute is TaskAttribute)
        return true;
    }
    return false;
  }

  public void Run(string[] args)
  {
    if(args.Length == 0)
      return;

    if(tasks.Count == 0)
      return;
    
    var task = FindTask(args[0]);
    if(task == null)
      return;

    var task_args = new string[args.Length-1];
    for(int i=1;i<args.Length;++i)
      task_args[i-1] = args[i];

    Echo($"************************ Running task '{task.Name}' ************************");
    var sw = new Stopwatch();
    sw.Start();
    task.Invoke(null, new object[] { this, task_args });
    var elapsed = Math.Round(sw.ElapsedMilliseconds/1000.0f,2);
    Echo($"************************ '{task.Name}' done({elapsed} sec.)  ************************");
  }

  public MethodInfo FindTask(string name)
  {
    foreach(var t in tasks)
    {
      if(t.Name == name)
        return t;
    }
    return null;
  }

  public void Echo(string s)
  {
    Console.WriteLine(s);
  }

  public void Shell(string cmd)
  {
    Echo($"shell: {cmd}");

    int idx = cmd.IndexOf(" ");
    if(idx == -1)
      throw new Exception($"Bad cmd: {cmd}");

    string binary = cmd.Substring(0, idx);
    string args = cmd.Substring(idx);

    var p = new System.Diagnostics.Process();
    p.StartInfo.FileName = binary;
    p.StartInfo.Arguments = args;
    p.StartInfo.UseShellExecute = false;
    p.StartInfo.RedirectStandardOutput = true;
    p.StartInfo.RedirectStandardError = true;

    p.Start();

    var lines = p.StandardOutput.ReadToEnd().Split(new [] { '\r', '\n' });
    foreach(string line in lines)
    {
      if(line != "")
        Echo(line);
    }

    p.WaitForExit();
    if(p.ExitCode != 0)
    {
      lines = p.StandardError.ReadToEnd().Split(new [] { '\r', '\n' });
      foreach(string line in lines)
      {
        if(line != "")
          Echo(line);
      }
      throw new Exception("$Error exit code: {p.ExitCode}");
    }
  }

  public string[] Glob(string s)
  {
    var files = new List<string>();
    int idx = s.IndexOf('*');
    if(idx != -1)
    {
      string dir = Path.GetDirectoryName(s);
      string mask = s.Substring(idx);
      files.AddRange(Directory.GetFiles(dir, mask));
    }
    else
      files.Add(s);
    return files.ToArray();
  }

  public void Rm(string path)
  {
    if(Directory.Exists(path))
      Directory.Delete(path, true);
    else
      File.Delete(path);
  }

  public void Write(string path, string text)
  {
    if(!Directory.Exists(Path.GetDirectoryName(path)))
      Directory.CreateDirectory(Path.GetDirectoryName(path));
    File.WriteAllText(path, text);
  }

  public bool NeedToRegen(string file, IList<string> deps)
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
}

public class TaskAttribute : Attribute
{}

public static class Hash
{
  static public uint CRC32(string id)
  {
    var bytes = System.Text.Encoding.ASCII.GetBytes(id);
    return CRC32(bytes);
  }

  static public uint CRC32(byte[] bytes)
  {
    return Crc32.Compute(bytes, bytes.Length);
  }

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
}

