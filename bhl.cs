using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using Mono.Options;
using Newtonsoft.Json;

public static class Tasks
{
  static readonly string[] VM_SRC = new string[] {
    $"{BHL_ROOT}/src/vm/*.cs",
    $"{BHL_ROOT}/src/vm/type/*.cs",
    $"{BHL_ROOT}/src/vm/scope/*.cs",
    $"{BHL_ROOT}/src/vm/symbol/*.cs",
    $"{BHL_ROOT}/src/vm/std/*.cs",
    $"{BHL_ROOT}/src/vm/util/*.cs",
    $"{BHL_ROOT}/src/vm/msgpack/*.cs",
  };
  
  [Task(verbose: false)]
  public static void version(Taskman tm, string[] args)
  {
    BuildAndRunDllCmd(
      tm,
      "version",
      new string[] {
        $"{BHL_ROOT}/src/cmd/version.cs",
        $"{BHL_ROOT}/src/cmd/cmd.cs",
        $"{BHL_ROOT}/src/vm/version.cs",
      },
      "",
      args
    );
  }

  [Task()]
  public static void build_front_dll(Taskman tm, string[] args)
  {
    bool debug = Environment.GetEnvironmentVariable("BHL_DEBUG") == "1";
    bool test_debug = Environment.GetEnvironmentVariable("BHL_TDEBUG") == "1";
    bool tests = Environment.GetEnvironmentVariable("BHL_TEST") == "1";

    var front_src = new List<string>() {
      $"{BHL_ROOT}/src/compile/*.cs",
      $"{BHL_ROOT}/src/g/*.cs",
      $"{BHL_ROOT}/deps/Antlr4.Runtime.Standard.dll", 
      $"{BHL_ROOT}/deps/Newtonsoft.Json.dll",
      $"{BHL_ROOT}/deps/lz4.dll", 
    };
    //let's add runtime VM sources as well
    front_src.AddRange(VM_SRC);

    MCSBuild(tm, front_src.ToArray(),
     $"{BHL_ROOT}/build/bhl_front.dll",
     "-define:BHL_FRONT -warnaserror -warnaserror-:3021 -nowarn:3021 -debug -target:library" + 
     (debug ? " -define:BHL_DEBUG" : "") + 
     (test_debug ? " -define:BHL_TDEBUG" : "") + 
     (tests ? " -define:BHL_TEST" : "")
    );
  }

  [Task()]
  public static void build_back_dll(Taskman tm, string[] args)
  {
    var mcs_bin = args.Length > 0 ? args[0] : "mcs"; 
    var dll_file = args.Length > 1 ? args[1] : $"{BHL_ROOT}/build/bhl_back.dll";
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

    MCSBuild(tm, VM_SRC,
      dll_file,
      $"{extra_args} -target:library",
      mcs_bin
    );
  }

  [Task()]
  public static void clean(Taskman tm, string[] args)
  {
    foreach(var dll in tm.Glob($"{BHL_ROOT}/build/*.dll"))
    {
      tm.Rm(dll);
      tm.Rm($"{dll}.mdb");
    }

    foreach(var exe in tm.Glob($"{BHL_ROOT}/build/*.exe"))
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
    tm.Rm($"{BHL_ROOT}/tmp");
    tm.Mkdir($"{BHL_ROOT}/tmp");

    tm.Copy($"{BHL_ROOT}/grammar/bhlPreprocLexer.g", $"{BHL_ROOT}/tmp/bhlPreprocLexer.g");
    tm.Copy($"{BHL_ROOT}/grammar/bhlPreprocParser.g", $"{BHL_ROOT}/tmp/bhlPreprocParser.g");
    tm.Copy($"{BHL_ROOT}/grammar/bhlLexer.g", $"{BHL_ROOT}/tmp/bhlLexer.g");
    tm.Copy($"{BHL_ROOT}/grammar/bhlParser.g", $"{BHL_ROOT}/tmp/bhlParser.g");
    tm.Copy($"{BHL_ROOT}/util/g4sharp", $"{BHL_ROOT}/tmp/g4sharp");

    tm.Shell("sh", $"-c 'cd {BHL_ROOT}/tmp && sh g4sharp *.g && cp bhl*.cs ../src/g/' ");
  }

  [Task(deps: "build_front_dll")]
  public static void compile(Taskman tm, string[] args)
  {
    string proj_file;
    var runtime_args = GetProjectArg(args, out proj_file);

    BuildAndRunCompiler(tm, proj_file, ref runtime_args);
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
        $"{BHL_ROOT}/build/bhl_front.dll", 
        $"{BHL_ROOT}/deps/mono_opts.dll",
        $"{BHL_ROOT}/deps/Newtonsoft.Json.dll",
        $"{BHL_ROOT}/deps/Antlr4.Runtime.Standard.dll", 
      },
      "-define:BHL_FRONT",
      args
    );
  }
  
  [Task(deps: "build_front_dll", verbose: false)]
  public static void bench(Taskman tm, string[] args)
  {
    BuildAndRunDllCmd(
      tm,
      "bench",
      new string[] {
        $"{BHL_ROOT}/src/cmd/bench.cs",
        $"{BHL_ROOT}/src/cmd/cmd.cs",
        $"{BHL_ROOT}/build/bhl_front.dll", 
        $"{BHL_ROOT}/deps/mono_opts.dll",
        $"{BHL_ROOT}/deps/Newtonsoft.Json.dll",
        $"{BHL_ROOT}/deps/Antlr4.Runtime.Standard.dll", 
      },
      "",
      args
    );
  }

  [Task()]
  public static void set_env_BHL_TEST(Taskman tm, string[] args)
  {
    Environment.SetEnvironmentVariable("BHL_TEST", "1");
  }

  [Task("set_env_BHL_TEST", "build_front_dll", "build_lsp_dll")]
  public static void test(Taskman tm, string[] args)
  {
    MCSBuild(tm, 
     new string[] {
        $"{BHL_ROOT}/tests/*.cs",
        $"{BHL_ROOT}/deps/mono_opts.dll",
        $"{BHL_ROOT}/build/bhl_front.dll",
        $"{BHL_ROOT}/build/bhl_lsp.dll",
        $"{BHL_ROOT}/deps/Newtonsoft.Json.dll",
        $"{BHL_ROOT}/deps/Antlr4.Runtime.Standard.dll"
     },
      $"{BHL_ROOT}/build/test.exe",
      "-define:BHL_FRONT -debug"
    );

    string mono_opts = "--debug"; 
    if(Environment.GetEnvironmentVariable("BHL_TDEBUG") == "1")
      mono_opts += " --debugger-agent=transport=dt_socket,server=y,address=127.0.0.1:55556";

    MonoRun(tm, $"{BHL_ROOT}/build/test.exe", args, mono_opts);
  }
  
  [Task(deps: "build_front_dll")]
  public static void build_lsp_dll(Taskman tm, string[] args)
  {
    MCSBuild(tm, 
      new string[] {
        $"{BHL_ROOT}/src/lsp/*.cs",
        $"{BHL_ROOT}/build/bhl_front.dll",
        $"{BHL_ROOT}/deps/Antlr4.Runtime.Standard.dll",
        $"{BHL_ROOT}/deps/Newtonsoft.Json.dll"
      },
      $"{BHL_ROOT}/build/bhl_lsp.dll",
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
        $"{BHL_ROOT}/build/bhl_front.dll",
        $"{BHL_ROOT}/build/bhl_lsp.dll",
        $"{BHL_ROOT}/deps/mono_opts.dll"
      },
      "-define:BHLSP_DEBUG",
      args
    );
  }
  
  /////////////////////////////////////////////////

  public static string BHL_ROOT {
    get {
      return Path.GetDirectoryName(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
    }
  }

  public static List<string> GetProjectArg(string[] args, out string proj_file)
  {
    string _proj_file = "";

    var p = new OptionSet() {
      { "p|proj=", "project config file",
        v => _proj_file = v }
     };

    var left = p.Parse(args);

    proj_file = _proj_file;

    if(!string.IsNullOrEmpty(proj_file))
      left.Insert(0, "--proj=" + proj_file);
    return left;
  }
  
  public static void BuildAndRunCompiler(Taskman tm, string proj_file, ref List<string> runtime_args)
  {
    var sources = new string[] {
      $"{BHL_ROOT}/src/cmd/compile.cs",
      $"{BHL_ROOT}/src/cmd/cmd.cs",
      $"{BHL_ROOT}/build/bhl_front.dll", 
      $"{BHL_ROOT}/deps/mono_opts.dll",
      $"{BHL_ROOT}/deps/Newtonsoft.Json.dll",
      $"{BHL_ROOT}/deps/Antlr4.Runtime.Standard.dll", 
    };

    var proj = new ProjectConf();
    if(!string.IsNullOrEmpty(proj_file))
      proj = ProjectConf.ReadFromFile(proj_file);

    var bindings_sources = proj.bindings_sources;
    if(bindings_sources.Count > 0)
    {
      if(string.IsNullOrEmpty(proj.bindings_dll))
        throw new Exception("Resulting 'bindings_dll' is not set");

      bindings_sources.Add($"{BHL_ROOT}/build/bhl_front.dll");
      bindings_sources.Add($"{BHL_ROOT}/deps/Antlr4.Runtime.Standard.dll"); 
      MCSBuild(tm, 
        bindings_sources.ToArray(),
        proj.bindings_dll, 
        "-define:BHL_FRONT -debug -target:library"
      );
      runtime_args.Add($"--bindings-dll={proj.bindings_dll}");
    }

    var postproc_sources = proj.postproc_sources;
    if(postproc_sources.Count > 0)
    {
      if(string.IsNullOrEmpty(proj.postproc_dll))
        throw new Exception("Resulting 'postproc_dll' is not set");

      postproc_sources.Add($"{BHL_ROOT}/build/bhl_front.dll");
      postproc_sources.Add($"{BHL_ROOT}/deps/Antlr4.Runtime.Standard.dll"); 
      MCSBuild(tm, 
        postproc_sources.ToArray(),
        proj.postproc_dll,
        "-define:BHL_FRONT -debug -target:library"
      );
      runtime_args.Add($"--postproc-dll={proj.postproc_dll}");
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
	
    //TODO: use system temporary directory for that?
    uint cmd_hash = Hash.CRC32(cmd);
    string cmd_hash_file = $"{BHL_ROOT}/build/" + Hash.CRC32(result) + ".mhash";
    if(!File.Exists(cmd_hash_file) || File.ReadAllText(cmd_hash_file) != cmd_hash.ToString()) 
      tm.Write(cmd_hash_file, cmd_hash.ToString());

    files.Add(cmd_hash_file);

    //for debug
    //Console.WriteLine(cmd);

    if(tm.NeedToRegen(result, files) || tm.NeedToRegen(result, refs))
    {
      tm.Mkdir(Path.GetDirectoryName(result));
      tm.Shell(binary, args);
    }
  }

  public static void BuildAndRunDllCmd(Taskman tm, string name, string[] sources, string extra_mcs_args, IList<string> args)
  {
    var dll_path = $"{BHL_ROOT}/build/bhl_cmd_{name}.dll";

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

//NOTE: representing only part of the original ProjectConf
public class ProjectConf
{
  public static ProjectConf ReadFromFile(string file_path)
  {
    var proj = JsonConvert.DeserializeObject<ProjectConf>(File.ReadAllText(file_path));
    proj.proj_file = file_path;
    proj.Setup();
    return proj;
  }

  [JsonIgnore]
  public string proj_file = "";

  public List<string> bindings_sources = new List<string>();
  public string bindings_dll = "";

  public List<string> postproc_sources = new List<string>();
  public string postproc_dll = "";

  string NormalizePath(string file_path)
  {
    if(Path.IsPathRooted(file_path))
      return Path.GetFullPath(file_path);
    else if(!string.IsNullOrEmpty(proj_file) && !string.IsNullOrEmpty(file_path) && file_path[0] == '.')
      return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(proj_file), file_path));
    return file_path;
  }

  public void Setup()
  {
    for(int i=0;i<bindings_sources.Count;++i)
      bindings_sources[i] = NormalizePath(bindings_sources[i]);
    bindings_dll = NormalizePath(bindings_dll);

    for(int i=0;i<postproc_sources.Count;++i)
      postproc_sources[i] = NormalizePath(postproc_sources[i]);
    postproc_dll = NormalizePath(postproc_dll);
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

    Echo($"***** BHL '{task.Name}' start *****");
    var sw = new Stopwatch();
    sw.Start();
    task.func.Invoke(null, new object[] { this, task_args });
    var elapsed = Math.Round(sw.ElapsedMilliseconds/1000.0f,2);
    Echo($"***** BHL '{task.Name}' done({elapsed} sec.) *****");
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
