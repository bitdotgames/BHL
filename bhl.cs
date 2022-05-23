using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Mono.Options;

public static class Tasks
{
  [Task()]
  public static void build_front_dll(Taskman tm, string[] args)
  {
    MCSBuild(tm, new string[] {
      $"{BHL_ROOT}/src/*.cs",
      $"{BHL_ROOT}/src/g/*.cs",
      $"{BHL_ROOT}/src/msgpack/*.cs",
      $"{BHL_ROOT}/src/vm/*.cs",
      $"{BHL_ROOT}/deps/Antlr4.Runtime.Standard.dll", 
      $"{BHL_ROOT}/deps/lz4.dll", 
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
    var extra_args = "";
    
    if(args.Length > 2)
    {
      for(int i = 2; i < args.Length; i++)
      {
        extra_args += args[i];
        if(i != args.Length - 1)
          extra_args += " ";
      }
    }  
    else
    {
      extra_args = "-debug";
    }

    MCSBuild(tm, new string[] {
        $"{BHL_ROOT}/src/msgpack/*.cs",
        $"{BHL_ROOT}/src/vm/*.cs",
      },
      dll_file,
      $"{extra_args} -target:library",
      mcs_bin
    );
  }

  [Task()]
  public static void clean(Taskman tm, string[] args)
  {
    tm.Rm($"{BHL_ROOT}/src/autogen.cs");

    foreach(var dll in tm.Glob($"{BHL_ROOT}/*.dll"))
    {
      if(!dll.StartsWith("bhl_"))
        continue;
      tm.Rm(dll);
      tm.Rm($"{dll}.mdb");
    }

    foreach(var exe in tm.Glob($"{BHL_ROOT}/*.exe"))
    {
      //NOTE: when removing itself under Windows we can get an exception, so let's force its staleness
      if(exe.EndsWith("bhlb.exe"))
      {
        tm.Touch(exe, new DateTime(1970, 3, 1, 7, 0, 0)/*some random date in the past*/);
        continue;
      }
      
      tm.Rm(exe);
      tm.Rm($"{exe}.mdb");
    }
  }

  [Task(deps: "geng")]
  public static void regen(Taskman tm, string[] args)
  {}

  [Task()]
  public static void geng(Taskman tm, string[] args)
  {
    tm.Mkdir($"{BHL_ROOT}/tmp");

    tm.Copy($"{BHL_ROOT}/bhl.g", $"{BHL_ROOT}/tmp/bhl.g");
    tm.Copy($"{BHL_ROOT}/util/g4sharp", $"{BHL_ROOT}/tmp/g4sharp");

    tm.Shell("sh", $"-c 'cd {BHL_ROOT}/tmp && sh g4sharp bhl.g && cp bhl*.cs ../src/g/' ");
  }

  [Task(deps: "build_front_dll")]
  public static void compile(Taskman tm, string[] args)
  {
    List<string> postproc_sources;
    List<string> user_sources;
    var runtime_args = ExtractBinArgs(args, out user_sources, out postproc_sources);

    BuildAndRunCompiler(tm, user_sources, postproc_sources, ref runtime_args);
  }

  [Task(deps: "build_front_dll", verbose: false)]
  public static void run(Taskman tm, string[] args)
  {
    BuildAndRunDllCmd(
      tm,
      "run",
      new string[] {
        $"{BHL_ROOT}/src/cmd/run.cs",
        $"{BHL_ROOT}/src/cmd/cmd.cs",
        $"{BHL_ROOT}/bhl_front.dll", 
        $"{BHL_ROOT}/deps/mono_opts.dll",
        $"{BHL_ROOT}/deps/Newtonsoft.Json.dll",
        $"{BHL_ROOT}/deps/Antlr4.Runtime.Standard.dll", 
      },
      "-define:BHL_FRONT",
      args
    );
  }

  [Task("build_front_dll", "build_lsp_dll")]
  public static void test(Taskman tm, string[] args)
  {
    MCSBuild(tm, 
     new string[] {
        $"{BHL_ROOT}/tests/*.cs",
        $"{BHL_ROOT}/deps/mono_opts.dll",
        $"{BHL_ROOT}/bhl_front.dll",
        $"{BHL_ROOT}/bhl_lsp.dll",
        $"{BHL_ROOT}/deps/Antlr4.Runtime.Standard.dll"
     },
      $"{BHL_ROOT}/test.exe",
      "-define:BHL_FRONT -debug"
    );

    MonoRun(tm, $"{BHL_ROOT}/test.exe", args, "--debug ");
  }
  
  [Task(deps: "build_front_dll")]
  public static void build_lsp_dll(Taskman tm, string[] args)
  {
    MCSBuild(tm, 
      new string[] {
        $"{BHL_ROOT}/src/lsp/*.cs",
        $"{BHL_ROOT}/bhl_front.dll",
        $"{BHL_ROOT}/deps/Antlr4.Runtime.Standard.dll",
        $"{BHL_ROOT}/deps/Newtonsoft.Json.dll"
      },
      $"{BHL_ROOT}/bhl_lsp.dll",
      "-target:library -define:BHLSP_DEBUG"
    );
  }
  
  [Task(deps: "build_lsp_dll", verbose: false)]
  public static void lsp(Taskman tm, string[] args)
  {
    BuildAndRunDllCmd(
      tm,
      "lsp",
      new string[] {
        $"{BHL_ROOT}/src/cmd/lsp.cs",
        $"{BHL_ROOT}/src/cmd/cmd.cs",
        $"{BHL_ROOT}/bhl_lsp.dll",
        $"{BHL_ROOT}/deps/mono_opts.dll"
      },
      "-define:BHLSP_DEBUG",
      args
    );
  }
  
  /////////////////////////////////////////////////

  public static string BHL_ROOT {
    get {
      return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
    }
  }

  public static List<string> ExtractBinArgs(string[] args, out List<string> user_sources, out List<string> postproc_sources)
  {
    var _postproc_sources = new List<string>();
    var _user_sources = new List<string>(); 

    var p = new OptionSet() {
      { "postproc-sources=", "Postprocessing sources",
        v => _postproc_sources.AddRange(v.Split(',')) },
      { "user-sources=", "User sources",
        v => _user_sources.AddRange(v.Split(',')) },
     };

    postproc_sources = _postproc_sources;
    user_sources = _user_sources;

    var left = p.Parse(args);
    return left;
  }
  
  public static void BuildAndRunCompiler(Taskman tm, List<string> user_sources, List<string> postproc_sources, ref List<string> runtime_args)
  {
    var sources = new string[] {
      $"{BHL_ROOT}/src/cmd/compile.cs",
      $"{BHL_ROOT}/src/cmd/cmd.cs",
      $"{BHL_ROOT}/bhl_front.dll", 
      $"{BHL_ROOT}/deps/mono_opts.dll",
      $"{BHL_ROOT}/deps/Antlr4.Runtime.Standard.dll", 
    };

    if(user_sources.Count > 0)
    {
      user_sources.Add($"{BHL_ROOT}/bhl_front.dll");
      user_sources.Add($"{BHL_ROOT}/deps/Antlr4.Runtime.Standard.dll"); 
      MCSBuild(tm, 
        user_sources.ToArray(),
        $"{BHL_ROOT}/bhl_user.dll",
        "-define:BHL_FRONT -debug -target:library"
      );
      runtime_args.Add($"--bindings-dll={BHL_ROOT}/bhl_user.dll");
    }

    if(postproc_sources.Count > 0)
    {
      postproc_sources.Add($"{BHL_ROOT}/bhl_front.dll");
      postproc_sources.Add($"{BHL_ROOT}/deps/Antlr4.Runtime.Standard.dll"); 
      MCSBuild(tm, 
        postproc_sources.ToArray(),
        $"{BHL_ROOT}/bhl_postproc.dll",
        "-define:BHL_FRONT -debug -target:library"
      );
      runtime_args.Add($"--postproc-dll={BHL_ROOT}/bhl_postproc.dll");
    }

    BuildAndRunDllCmd(
      tm,
      "compile",
      sources,
      "-define:BHL_FRONT",
      runtime_args
    );
  }

  public static void MonoRun(Taskman tm, string exe, string[] args = null, string opts = "")
  {
    var mono_args = $"{opts} {exe} " + String.Join(" ", args);
    tm.Shell("mono", mono_args);
  }

  public static int TryMonoRun(Taskman tm, string exe, string[] args = null, string opts = "")
  {
    var mono_args = $"{opts} {exe} " + String.Join(" ", args);
    return tm.TryShell("mono", mono_args);
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

    if(files.Count == 0)
      throw new Exception("No files");

    string args = (refs.Count > 0 ? " -r:" + String.Join(" -r:", refs.Select(r => tm.CLIPath(r))) : "") + $" {opts} -out:{tm.CLIPath(result)} " + String.Join(" ", files.Select(f => tm.CLIPath(f)));
    string cmd = binary + " " + args;
	
    uint cmd_hash = Hash.CRC32(cmd);
    string cmd_hash_file = $"{BHL_ROOT}/build/" + Hash.CRC32(result) + ".mhash";
    if(!File.Exists(cmd_hash_file) || File.ReadAllText(cmd_hash_file) != cmd_hash.ToString()) 
      tm.Write(cmd_hash_file, cmd_hash.ToString());

    files.Add(cmd_hash_file);

    if(tm.NeedToRegen(result, files) || tm.NeedToRegen(result, refs))
      tm.Shell(binary, args);
  }

  public static void BuildAndRunDllCmd(Taskman tm, string name, string[] sources, string extra_mcs_args, IList<string> args)
  {
    var dll_path = $"{BHL_ROOT}/bhl_cmd_{name}.dll";

    MCSBuild(tm, 
      sources.ToArray(),
      dll_path,
      extra_mcs_args + " -debug -target:library"
    );

    var cmd_assembly = System.Reflection.Assembly.LoadFrom(dll_path);
    var cmd_class = cmd_assembly.GetTypes()[0];
    var cmd = System.Activator.CreateInstance(cmd_class, null);
    if(cmd == null)
      throw new Exception("Could not create instance of: " + cmd_class.Name);
    var cmd_method = cmd.GetType().GetMethod("Run");
    if(cmd_method == null)
      throw new Exception("No Run method in class: " + cmd_class.Name);
    cmd_method.Invoke(cmd, new object[] { args.ToArray() });
  }
}

public class ShellException : Exception
{
  public int code;

  public ShellException(int code, string message)
    : base(message)
  {
    this.code = code;
  }
}

public static class BHLBuild
{
  public static void Main(string[] args)
  {
    var tm = new Taskman(typeof(Tasks));
    try
    {
      tm.Run(args);
    }
    catch(TargetInvocationException e)
    {
      if(e.InnerException is ShellException)
        System.Environment.Exit((e.InnerException as ShellException).code);
      else
        throw;
    }
  }
}

public class Taskman
{
  public bool verbose = true;

  public class Task
  {
    public TaskAttribute attr;
    public MethodInfo func;

    public string Name {
      get {
        return func.Name;
      }
    }

    public List<Task> Deps = new List<Task>();
  }

  List<Task> tasks = new List<Task>();
  HashSet<Task> invoked = new HashSet<Task>();

  public bool IsWin {
    get {
      return !IsUnix;
    }
  }

  public bool IsUnix {
    get {
      int p = (int)Environment.OSVersion.Platform;
      return (p == 4) || (p == 6) || (p == 128);
    }
  }

  public Taskman(Type tasks_class)
  {
    foreach(var method in tasks_class.GetMethods())
    {
      var attr = GetAttribute<TaskAttribute>(method);
      if(attr == null)
        continue;
      var task = new Task() {
        attr = attr,
        func = method
      };
      tasks.Add(task);
    }

    foreach(var task in tasks)
    {
      foreach(var dep_name in task.attr.deps)
      {
        var dep = FindTask(dep_name);
        if(dep == null)
          throw new Exception($"No such dependency '{dep_name}' for task '{task.Name}'");
        task.Deps.Add(dep);
      }
    }
  }

  static T GetAttribute<T>(MemberInfo member) where T : Attribute
  {
    foreach(var attribute in member.GetCustomAttributes(true))
    {
      if(attribute is TaskAttribute)
        return (T)attribute;
    }
    return null;
  }

  public void Run(string[] args)
  {
    if(args.Length == 0)
      throw new Exception("No task specified");

    var task = FindTask(args[0]);
    if(task == null)
      throw new Exception("No such task: " + args[0]);

    var task_args = new string[args.Length-1];
    for(int i=1;i<args.Length;++i)
      task_args[i-1] = args[i];

    verbose = task.attr.verbose;

    Invoke(task, task_args);
  }

  public void Invoke(Task task, string[] task_args)
  {
    if(invoked.Contains(task))
      return;
    invoked.Add(task);

    foreach(var dep in task.Deps)
      Invoke(dep, new string[] {});

    Echo($"************************ Running task '{task.Name}' ************************");
    var sw = new Stopwatch();
    sw.Start();
    try
    {
    task.func.Invoke(null, new object[] { this, task_args });
    }
    catch(Exception)
    {
      throw;
    }
    finally
    {
    var elapsed = Math.Round(sw.ElapsedMilliseconds/1000.0f,2);
    Echo($"************************ '{task.Name}' done({elapsed} sec.)  ************************");
  }
  }

  public Task FindTask(string name)
  {
    foreach(var t in tasks)
    {
      if(t.Name == name)
        return t;
    }
    return null;
  }

  public string CLIPath(string p)
  {
  	if(p.IndexOf(" ") == -1)
      return p;
	  
    if(IsWin)
    {
      p = "\"" + p.Trim(new char[]{'"'}) + "\"";
      return p;
    }
    else
    {
      p = "'" + p.Trim(new char[]{'\''}) + "'";
      return p;
    }
  }

  public void Echo(string s)
  {
    if(verbose)
      Console.WriteLine(s);
  }

  public void Mkdir(string path)
  {
    if(!Directory.Exists(path))
      Directory.CreateDirectory(path);
  }

  public void Copy(string src, string dst)
  {
    if(File.Exists(dst))
      File.Delete(dst);
    Mkdir(Path.GetDirectoryName(dst));
    File.Copy(src, dst);
  }

  public void Shell(string binary, string args)
  {
    int exit_code = TryShell(binary, args);
    if(exit_code != 0)
      throw new ShellException(exit_code, $"Error exit code: {exit_code}");
  }

  public int TryShell(string binary, string args)
  {
    binary = CLIPath(binary);
    Echo($"shell: {binary} {args}");

    var p = new System.Diagnostics.Process();

  	if(IsWin)
  	{
  	  string tmp_path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/build/";
  	  string cmd = binary + " " + args;
  	  var bat_file = tmp_path + Hash.CRC32(cmd) + ".bat";    
  	  Write(bat_file, cmd);
  	  
  	  p.StartInfo.FileName = "cmd.exe";
  	  p.StartInfo.Arguments = "/c " + bat_file;
  	}
  	else
  	{
  	  p.StartInfo.FileName = binary;
  	  p.StartInfo.Arguments = args;
  	}
      
  	p.StartInfo.UseShellExecute = false;
    p.StartInfo.RedirectStandardOutput = false;
    p.StartInfo.RedirectStandardError = false;
    p.Start();
    
    p.WaitForExit();

    return p.ExitCode;
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
    Mkdir(Path.GetDirectoryName(path));
    File.WriteAllText(path, text);
  }

  public void Touch(string path, DateTime dt)
  {
    if(!File.Exists(path))
      File.WriteAllText(path, "");
    File.SetLastWriteTime(path, dt);
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
{
  public bool verbose = true;
  public string[] deps;

  public TaskAttribute(params string[] deps)
  {
    this.deps = deps;
  }

  public TaskAttribute(bool verbose, params string[] deps)
  {
    this.verbose = verbose;
    this.deps = deps;
  }
}

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

      if(BitConverter.IsLittleEndian)
        Array.Reverse(result);

      return result;
    }
  }
}
