using System;
using System.Collections.Generic;
using System.IO;
using Mono.Options;

namespace bhl {
  
public static partial class Tasks
{
  public static string GetSelfFile()
  {
    return System.Reflection.Assembly.GetExecutingAssembly().Location;
  }
  
  public static string BHL_ROOT
  {
    get
    {
      return Path.GetDirectoryName(
        Path.GetFullPath(
          Path.GetDirectoryName(GetSelfFile()) + "/../../../../")
      );
    }
  }
  
  const int ERROR_EXIT_CODE = 2;
  
  [Task(verbose: false)]
  public static void version(Taskman tm, string[] args)
  {
    Console.WriteLine(bhl.Version.Name);
  }

  [Task]
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