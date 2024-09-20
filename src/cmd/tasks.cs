
using System;
using System.Collections.Generic;
using System.IO;
using Mono.Options;

namespace bhl {
public static class Tasks
{
  [Task(verbose: false)]
  public static void version(Taskman tm, string[] args)
  {
    Console.WriteLine(bhl.Version.Name);
  }

  [Task()]
  public static void clean(Taskman tm, string[] args)
  {
    foreach (var dll in tm.Glob($"{BHL_ROOT}/build/*.dll"))
    {
      tm.Rm(dll);
      tm.Rm($"{dll}.mdb");
    }

    foreach (var exe in tm.Glob($"{BHL_ROOT}/build/*.exe"))
    {
      //NOTE: when removing itself under Windows we can get an exception, so let's force its staleness
      if (exe.EndsWith("bhlb.exe"))
      {
        tm.Touch(exe, new DateTime(1970, 3, 1, 7, 0, 0) /*some random date in the past*/);
        continue;
      }

      tm.Rm(exe);
      tm.Rm($"{exe}.mdb");
    }
  }

  [Task(deps: "geng")]
  public static void regen(Taskman tm, string[] args)
  {
  }

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

  [Task]
  public static void compile(Taskman tm, string[] args)
  {
    string proj_file;
    var runtime_args = GetProjectArg(args, out proj_file);

    var proj = new ProjectConfPartial();
    if (!string.IsNullOrEmpty(proj_file))
      proj = ProjectConfPartial.ReadFromFile(proj_file);

    var bindings_sources = proj.bindings_sources;
    if (bindings_sources.Count > 0)
    {
      if (string.IsNullOrEmpty(proj.bindings_dll))
        throw new Exception("Resulting 'bindings_dll' is not set");

      bindings_sources.Add($"{BHL_ROOT}/src/compile/bhl_front.csproj");
      string bindings_dll_path = DotnetBuildLibrary(
        tm,
        bindings_sources.ToArray(),
        proj.bindings_dll,
        new List<string>() { "BHL_FRONT" }
      );
      runtime_args.Add($"--bindings-dll={bindings_dll_path}");
    }

    var postproc_sources = proj.postproc_sources;
    if (postproc_sources.Count > 0)
    {
      if (string.IsNullOrEmpty(proj.postproc_dll))
        throw new Exception("Resulting 'postproc_dll' is not set");

      postproc_sources.Add($"{BHL_ROOT}/src/compile/bhl_front.csproj");
      postproc_sources.Add($"{BHL_ROOT}/deps/Antlr4.Runtime.Standard.dll");
      string postproc_dll_path = DotnetBuildLibrary(
        tm,
        postproc_sources.ToArray(),
        proj.postproc_dll,
        new List<string>() { "BHL_FRONT" }
      );
      runtime_args.Add($"--postproc-dll={postproc_dll_path}");
    }

    var cmd = new CompileCmd();
    cmd.Run(runtime_args.ToArray());
  }

  [Task]
  public static void run(Taskman tm, string[] args)
  {
    var cmd = new RunCmd();
    cmd.Run(args);
  }

  [Task]
  public static void bench(Taskman tm, string[] args)
  {
    var cmd = new BenchCmd();
    cmd.Run(args);
  }

  [Task]
  public static void set_env_BHL_TEST(Taskman tm, string[] args)
  {
    Environment.SetEnvironmentVariable("BHL_TEST", "1");
  }

  public static void lsp(Taskman tm, string[] args)
  {
    var cmd = new LSPCmd();
    cmd.Run(args);
  }

  /////////////////////////////////////////////////

  public static string BHL_ROOT
  {
    get
    {
      return Path.GetDirectoryName(
        Path.GetFullPath(
          Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/../../../../")
      );
    }
  }

  public static List<string> GetProjectArg(string[] args, out string proj_file)
  {
    string _proj_file = "";

    var p = new OptionSet()
    {
      {
        "p|proj=", "project config file",
        v => _proj_file = v
      }
    };

    var left = p.Parse(args);

    proj_file = _proj_file;

    if (!string.IsNullOrEmpty(proj_file))
      left.Insert(0, "--proj=" + proj_file);
    return left;
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

  public static string DotnetBuildLibrary(
    Taskman tm,
    string[] srcs,
    string result,
    List<string> defines
  )
  {
    var files = new List<string>();
    foreach (var s in srcs)
      files.AddRange(tm.Glob(s));

    foreach (var f in files)
      if (!File.Exists(f))
        throw new Exception($"File not found: '{f}'");

    //NOTE: in case of dotnet build result is a directory not a file,
    //      let's remove any conflicting files
    //TODO: is it OK to do this quietly?
    if (File.Exists(result))
      File.Delete(result);

    var deps = new List<string>();
    for (int i = files.Count; i-- > 0;)
    {
      if (files[i].EndsWith(".dll") || files[i].EndsWith(".csproj"))
      {
        deps.Add(files[i]);
        files.RemoveAt(i);
      }
    }

    if (files.Count == 0)
      throw new Exception("No files");

    string csproj = MakeLibraryCSProj(
      Path.GetFileNameWithoutExtension(result),
      files,
      deps,
      defines
    );

    string result_dll = result + "/" + Path.GetFileName(result);

    //TODO: use system temporary directory for that?
    var csproj_file = result + ".csproj";
    File.WriteAllText(csproj_file, csproj);

    //TODO: use system temporary directory for that?
    string cmd_hash_file = csproj_file + ".mhash";
    uint cmd_hash = Hash.CRC32(csproj);
    if (!File.Exists(cmd_hash_file) || File.ReadAllText(cmd_hash_file) != cmd_hash.ToString())
      tm.Write(cmd_hash_file, cmd_hash.ToString());

    files.Add(cmd_hash_file);

    if (tm.NeedToRegen(result_dll, files) || tm.NeedToRegen(result_dll, deps))
      tm.Shell("dotnet", "build " + csproj_file + " -o " + result);

    return result_dll;
  }

  public static string MakeLibraryCSProj(
    string name,
    List<string> files,
    List<string> deps,
    List<string> defines
  )
  {
    string csproj_header = @$"
<Project Sdk=""Microsoft.NET.Sdk"">
<PropertyGroup>
  <AssemblyName>{name}</AssemblyName>
  <OutputType>Library</OutputType>
  <TargetFramework>netstandard2.1</TargetFramework>
  <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
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
      if (dep.EndsWith(".dll"))
      {
        csproj_deps +=
          $"<Reference Include=\"{Path.GetFileNameWithoutExtension(dep)}\"><HintPath>{dep}</HintPath></Reference>\n";
      }
      else if (dep.EndsWith(".csproj"))
      {
        csproj_deps +=
          $"<ProjectReference Include=\"{dep}\"/>\n";
      }
      else
        throw new Exception("Unknown dependency file: " + dep);
    }

    csproj_deps += "</ItemGroup>\n\n";

    return
      csproj_header +
      csproj_sources +
      csproj_deps +
      csproj_footer;
  }
}

}