using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Mono.Options;
using ThreadTask = System.Threading.Tasks.Task;

#pragma warning disable CS8981

namespace bhl.taskman;

public static partial class Tasks
{
  public static string BHL_ROOT
  {
    get
    {
      return Path.GetFullPath(
        Path.Combine(BuildUtils.GetSelfDir(), "..", "..", "..", "..")
      );
    }
  }

  private static string _targetFramework;

  public static string TargetFramework
  {
    get
    {
      if(_targetFramework == null)
      {
        var doc = XDocument.Parse(File.ReadAllText(BHL_ROOT + "/Directory.Build.props"));
        _targetFramework = doc.Root.Element("PropertyGroup").Element("TargetFramework").Value;
      }
      return _targetFramework;
    }
  }

  const int ERROR_EXIT_CODE = 2;

  [Task(verbose: false, desc: "Prints the tool's version")]
  public static ThreadTask version(Taskman tm, string[] args)
  {
    Console.WriteLine(bhl.Version.Name);
    return ThreadTask.CompletedTask;
  }

  [Task(verbose: false, desc: "Lists available tasks, or details one task's options ('bhl help <task>')")]
  public static ThreadTask help(Taskman tm, string[] args)
  {
    if(args.Length > 0)
    {
      PrintTaskHelp(tm, args[0]);
      return ThreadTask.CompletedTask;
    }

    Console.WriteLine("BHL language tool (" + bhl.Version.Name + ")");
    Console.WriteLine("Usage:");
    Console.WriteLine("\tbhl <task> [args]");
    Console.WriteLine("\tbhl help <task>    show a task's options");
    Console.WriteLine("Available tasks:");

    var tasks = new List<Taskman.Task>(tm.Tasks);
    tasks.Sort((a, b) => a.Name.CompareTo(b.Name));

    int name_col = tasks.Max(t => t.Name.Length) + 2;
    foreach(var t in tasks)
      Console.WriteLine("\t" + t.Name.PadRight(name_col) + t.attr.desc);

    Console.WriteLine("Environment variables:");

    var env_vars = new (string name, string desc)[]
    {
      ("BHL_REBUILD", "force a full rebuild, bypassing cache (also rebuilds the bhl tool itself)"),
      ("BHL_VERBOSE=<level>", "compile task verbosity, 0-2 - higher is more verbose (default: 1)"),
      ("BHL_SILENT=0|1", "0 = verbose, 1 = quiet (default) - for the bhl wrapper script's own rebuild output, and 'lsp' console logging")
    };
    int env_col = env_vars.Max(e => e.name.Length) + 2;
    foreach(var e in env_vars)
      Console.WriteLine("\t" + e.name.PadRight(env_col) + e.desc);

    return ThreadTask.CompletedTask;
  }

  //NOTE: every task's options live in a static '<task>_options(...)' method (see e.g.
  //      run_options in tasks.run.cs) shared between the task's own real parsing and this -
  //      it takes either zero args or one 'Args' holder we can Activator.CreateInstance,
  //      so we can build (but never Parse(), hence no side effects) the same OptionSet the
  //      task itself uses, purely to print its descriptions
  static void PrintTaskHelp(Taskman tm, string task_name)
  {
    var task = tm.FindTask(task_name);
    if(task == null)
    {
      Console.WriteLine($"No such task: {task_name}");
      Environment.Exit(1);
      return;
    }

    Console.WriteLine("bhl " + task_name + (string.IsNullOrEmpty(task.attr.desc) ? "" : " - " + task.attr.desc));

    var options_method = typeof(Tasks).GetMethod(task_name + "_options",
      BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    if(options_method == null)
    {
      Console.WriteLine("(no options)");
      return;
    }

    var method_params = options_method.GetParameters();
    var call_args = method_params.Length == 0
      ? Array.Empty<object>()
      : new object[] { Activator.CreateInstance(method_params[0].ParameterType) };

    var options = (OptionSet)options_method.Invoke(null, call_args);

    Console.WriteLine("Options:");
    options.WriteOptionDescriptions(Console.Out);
  }

  public static string DotnetBuildLibrary(
    Taskman tm,
    bool force,
    string[] srcs,
    string result,
    List<string> defines,
    string tmp_dir
  )
  {
    if(string.IsNullOrEmpty(tmp_dir))
      throw new Exception("'tmp_dir' is not set");

    var files = new List<string>();
    foreach(var s in srcs)
      files.AddRange(BuildUtils.Glob(s));

    //NOTE: in case of dotnet build result is a directory not a file,
    //      let's remove any conflicting files
    //TODO: is it OK to do this quietly?
    if(!Directory.Exists(result) && File.Exists(result))
      File.Delete(result);

    var deps = new List<string>();
    var pkgs = new List<string>();
    for(int i = files.Count; i-- > 0;)
    {
      if(files[i].EndsWith(".dll") || files[i].EndsWith(".csproj"))
      {
        deps.Add(files[i]);
        files.RemoveAt(i);
      }
      else if(files[i].Contains("="))
      {
        pkgs.Add(files[i]);
        files.RemoveAt(i);
      }
    }

    if(files.Count == 0)
      throw new Exception("No files");

    string csproj = MakeLibraryCSProj(
      Path.GetFileNameWithoutExtension(result),
      files,
      deps,
      pkgs,
      defines
    );

    string result_dll = result + "/" + Path.GetFileName(result);

    //NOTE: kept in tmp_dir (rather than alongside 'result') so the generated
    //      csproj and its 'obj' dir don't clutter a dist/release folder that's
    //      meant to hold only the final built dll
    var csproj_file = tmp_dir + "/" + Path.GetFileName(result) + ".csproj";
    Directory.CreateDirectory(Path.GetDirectoryName(csproj_file));
    if(!File.Exists(csproj_file) || File.ReadAllText(csproj_file) != csproj)
      BuildUtils.Write(csproj_file, csproj);

    //NOTE: using version.cs rather than the bhl binary itself as a dependency:
    //      the binary's mtime changes on every rebuild regardless of whether its
    //      actual compiled content changed, which would otherwise mark every
    //      previously-built bindings/postproc dll stale after any bhl rebuild;
    //      version.cs's mtime only changes when the version is deliberately bumped
    deps.Add($"{BHL_ROOT}/src/vm/version.cs");
    //let's add generated csproj as a dependency
    deps.Add(csproj_file);

    if(force ||
       BuildUtils.NeedToRegen(result_dll, files) ||
       BuildUtils.NeedToRegen(result_dll, deps))
    {
      if(force)
      {
        try
        {
          tm.Shell("dotnet", "clean --framework " + TargetFramework + " " + csproj_file);
        }
        catch(Exception)
        {}
      }

      tm.Shell("dotnet", "build --framework " + TargetFramework + " " + csproj_file + " -o " + result);

      //let's force file modification time since .Net may use result from the cache
      //without changing the file time
      BuildUtils.Touch(result_dll, DateTime.Now);
    }

    return result_dll;
  }

  //NOTE: returns null if the entry has no C# sources, in which case
  //      b.dll (if any) is assumed to already be a prebuilt dll
  public static string BuildBindingsDllForEntry(Taskman tm, bool force_rebuild, ProjectConf proj, BindingsEntryConf b)
  {
    var cs_sources = b.sources.Where(f => f.EndsWith(".cs")).ToList();
    if(cs_sources.Count == 0)
      return null;

    if(string.IsNullOrEmpty(b.dll))
      throw new Exception("Resulting bindings entry 'dll' is not set");

    if(!b.dll.EndsWith(".dll"))
      throw new Exception("Resulting bindings entry 'dll' invalid extension: " + b.dll);

    cs_sources.Add($"{BHL_ROOT}/src/front/bhl_front.csproj");
    return DotnetBuildLibrary(
      tm,
      force_rebuild,
      cs_sources.ToArray(),
      b.dll,
      new List<string>() { "BHL_FRONT" },
      proj.tmp_dir
    );
  }

  //NOTE: builds every bindings entry with C# sources that isn't opted out via
  //      manual_build (unless force_rebuild/bindings_only overrides that); returns
  //      only the entries that were actually (re)built, keyed by entry index in proj.bindings
  public static Dictionary<int, string> BuildBindingsDlls(
    Taskman tm, bool force_rebuild, ProjectConf proj, bool bindings_only
  )
  {
    var built = new Dictionary<int, string>();
    for(int i = 0; i < proj.bindings.Count; ++i)
    {
      var entry = proj.bindings[i];
      if(entry.manual_build && !bindings_only && !force_rebuild)
        continue;

      var path = BuildBindingsDllForEntry(tm, force_rebuild, proj, entry);
      if(path != null)
        built[i] = path;
    }
    return built;
  }

  //NOTE: returns null if proj has no C# postproc_sources, in which case
  //      proj.postproc_dll (if any) is assumed to already be a prebuilt dll
  public static string BuildPostprocDll(Taskman tm, bool force_rebuild, ProjectConf proj)
  {
    var postproc_sources = proj.postproc_sources.Where(f => f.EndsWith(".cs")).ToList();
    if(postproc_sources.Count == 0)
      return null;

    if(string.IsNullOrEmpty(proj.postproc_dll))
      throw new Exception("Resulting 'postproc_dll' is not set");

    if(!proj.postproc_dll.EndsWith(".dll"))
      throw new Exception("Resulting 'postproc_dll' invalid extension: " + proj.postproc_dll);

    postproc_sources.Add($"{BHL_ROOT}/src/front/bhl_front.csproj");
    postproc_sources.Add("Antlr4.Runtime.Standard=4.13.1");
    return DotnetBuildLibrary(
      tm,
      force_rebuild,
      postproc_sources.ToArray(),
      proj.postproc_dll,
      new List<string>() { "BHL_FRONT" },
      proj.tmp_dir
    );
  }

  public static string MakeLibraryCSProj(
    string name,
    List<string> files,
    List<string> deps,
    List<string> pkgs,
    List<string> defines
  )
  {
    string csproj_header = @$"
<Project Sdk=""Microsoft.NET.Sdk"">
<PropertyGroup>
  <AssemblyName>{name}</AssemblyName>
  <OutputType>Library</OutputType>
  <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  <TargetFramework>{TargetFramework}</TargetFramework>
  <DefineConstants>{string.Join(';', defines)}</DefineConstants>
</PropertyGroup>
 ";

    string csproj_footer = @"
</Project>
 ";

    string csproj_sources = "<ItemGroup>\n";
    foreach (var file in files)
      csproj_sources += $"<Compile Include=\"{file}\" />\n";
    csproj_sources += "</ItemGroup>\n\n";

    string csproj_deps = "<ItemGroup>\n";
    foreach (var dep in deps)
    {
      if(dep.EndsWith(".dll"))
      {
        csproj_deps +=
          $"<Reference Include=\"{Path.GetFileNameWithoutExtension(dep)}\"><HintPath>{dep}</HintPath></Reference>\n";
      }
      else if(dep.EndsWith(".csproj"))
      {
        csproj_deps +=
          $"<ProjectReference Include=\"{dep}\"/>\n";
      }
      else
        throw new Exception("Unknown dependency file: " + dep);
    }

    foreach(var pkg in pkgs)
    {
      var items = pkg.Split('=');
      csproj_deps +=
        $"<PackageReference Include=\"{items[0]}\" Version=\"{items[1]}\"/>\n";
    }

    csproj_deps += "</ItemGroup>\n\n";

    return
      csproj_header +
      csproj_sources +
      csproj_deps +
      csproj_footer;
  }
}
