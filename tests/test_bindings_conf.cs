using System;
using System.Collections.Generic;
using System.IO;
using bhl;
using Newtonsoft.Json;
using Xunit;

// Coverage for ProjectConf.bindings as a list, with (name, version) discovered post-load
// instead of pre-declared as a dictionary key (see proj_conf.cs).
public class TestBindingsConf
{
  static string MakeTempDir()
  {
    string dir = Path.Combine(Path.GetTempPath(), "bhl_bindings_conf_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    return dir;
  }

  static string WriteScriptedBinding(string dir, string file_name, string decl_name, string version)
  {
    string path = Path.Combine(dir, file_name);
    File.WriteAllText(path, $@"
import ""std/bind""
func RegisterBindings(std.bind.Types types) {{
  types.RegisterVersion(""{decl_name}"", ""{version}"")
}}
");
    return path;
  }

  [Fact]
  public void DiscoversScriptedBindingVersionWithoutAName()
  {
    var dir = MakeTempDir();
    try
    {
      var script_path = WriteScriptedBinding(dir, "mybindings.bhl", "mytest", "2.3.4");

      var proj = new ProjectConf();
      proj.tmp_dir = dir;
      proj.use_cache = false;
      proj.bindings.Add(new BindingsEntryConf { sources = new List<string> { script_path } });
      proj.Setup();

      var bindings = (UserBindingsWithInfo)proj.LoadBindings();
      var versions = bindings.info;

      Assert.NotNull(bindings);
      Assert.Single(versions);
      Assert.Equal("mytest", versions[0].name);
      Assert.Equal("2.3.4", versions[0].version);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void LegacyDictShapedJsonStillLoads()
  {
    var dir = MakeTempDir();
    try
    {
      var script_path = WriteScriptedBinding(dir, "mybindings.bhl", "legacydict", "1.0.0");

      string json = JsonConvert.SerializeObject(new
      {
        bindings = new Dictionary<string, object>
        {
          ["whatever_name_used_to_be_here"] = new { sources = new[] { script_path } }
        },
        tmp_dir = dir,
        use_cache = false,
      });

      var proj = JsonConvert.DeserializeObject<ProjectConf>(json);
      proj.Setup();

      Assert.Single(proj.bindings);
      Assert.False(proj.bindings[0].is_legacy);

      var bindings = (UserBindingsWithInfo)proj.LoadBindings();
      Assert.Single(bindings.info);
      Assert.Equal("legacydict", bindings.info[0].name);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void LegacyFlatFieldsMigrateAndAreExemptFromVersionCheck()
  {
    var proj = new ProjectConf();
    proj.bindings_sources.Add("somefile.cs");
    proj.Setup();

    Assert.Single(proj.bindings);
    Assert.True(proj.bindings[0].is_legacy);

    var bindings = (UserBindingsWithInfo)proj.LoadBindings();
    Assert.NotNull(bindings);
    Assert.Empty(bindings.info);
  }

  [Fact]
  public void EntryWithNoDeclaredVersionThrows()
  {
    var proj = new ProjectConf();
    proj.bindings.Add(new BindingsEntryConf());
    proj.Setup();

    Assert.Throws<Exception>(() => proj.LoadBindings());
  }
}
