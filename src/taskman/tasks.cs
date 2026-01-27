using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using ThreadTask = System.Threading.Tasks.Task;

#pragma warning disable CS8981

namespace bhl.taskman;

public static partial class Tasks
{
  public static string BHL_ROOT
  {
    get
    {
      return Path.GetDirectoryName(
        Path.GetFullPath(
          Path.GetDirectoryName(BuildUtils.GetSelfFile()) + "/../../../../")
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

  [Task(verbose: false)]
  public static ThreadTask version(Taskman tm, string[] args)
  {
    Console.WriteLine(bhl.Version.Name);
    return ThreadTask.CompletedTask;
  }

  [Task(verbose: false)]
  public static ThreadTask help(Taskman tm, string[] args)
  {
    Console.WriteLine("BHL build tool version " + bhl.Version.Name + "\n");
    Console.WriteLine("Available tasks:");

    var tasks = new List<Taskman.Task>(tm.Tasks);
    tasks.Sort((a, b) => a.Name.CompareTo(b.Name));

    foreach(var t in tasks)
      Console.WriteLine(" " + t.Name);

    return ThreadTask.CompletedTask;
  }

  [Task]
  public static ThreadTask clean(Taskman tm, string[] args)
  {
    //touching version file which is used for detection of bhl dll
    //'staleness' in top level scripts
    tm.Touch($"{BHL_ROOT}/src/vm/version.cs", DateTime.Now);

    //invoking dotnet clean to remove all build products
    tm.TryShell("dotnet", $"clean {BHL_ROOT}/bhl.csproj");

    return ThreadTask.CompletedTask;
  }

  public static string DotnetBuildLibrary(
    Taskman tm,
    bool force,
    string[] srcs,
    string result,
    List<string> defines
  )
  {
    var files = new List<string>();
    foreach(var s in srcs)
      files.AddRange(tm.Glob(s));

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

    //TODO: use system temporary directory for that?
    var csproj_file = result + ".csproj";
    Directory.CreateDirectory(Path.GetDirectoryName(csproj_file));
    if(!File.Exists(csproj_file) || File.ReadAllText(csproj_file) != csproj)
      tm.Write(csproj_file, csproj);

    //let's add bhl binary as a dependency
    deps.Add(BuildUtils.GetSelfFile());
    //let's generated csproj as a dependency
    deps.Add(csproj_file);

    if(force ||
       tm.NeedToRegen(result_dll, files) ||
       tm.NeedToRegen(result_dll, deps))
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
      tm.Touch(result_dll, DateTime.Now);
    }

    return result_dll;
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
