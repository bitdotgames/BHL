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
func string,string BindingInfo() {{
  return ""{decl_name}"", ""{version}""
}}
func RegisterBindings(std.bind.Types types) {{
}}
");
    return path;
  }

  [Fact]
  public void DiscoversScriptedBindingVersionWhenEntryNameMatchesDeclaredName()
  {
    var dir = MakeTempDir();
    try
    {
      var script_path = WriteScriptedBinding(dir, "mybindings.bhl", "mytest", "2.3.4");

      var proj = new ProjectConf();
      proj.tmp_dir = dir;
      proj.use_cache = false;
      proj.bindings.Add(new BindingsEntryConf { name = "mytest", sources = new List<string> { script_path } });
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
  public void EntryNameNotMatchingDeclaredNameThrows()
  {
    var dir = MakeTempDir();
    try
    {
      var script_path = WriteScriptedBinding(dir, "mybindings.bhl", "mytest", "2.3.4");

      var proj = new ProjectConf();
      proj.tmp_dir = dir;
      proj.use_cache = false;
      //NOTE: entry's `name` ("entry_label") never matches what the script actually
      //      declares ("mytest") - a stale/typo'd name that would otherwise silently
      //      never cherry-pick from BindingsRegistry even when it should
      proj.bindings.Add(new BindingsEntryConf { name = "entry_label", sources = new List<string> { script_path } });
      proj.Setup();

      Assert.Throws<Exception>(() => proj.LoadBindings());
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
      //NOTE: the dict key becomes the entry's `name` (see BindingsListConverter), which
      //      must now match what the script actually declares via BindingInfo()
      var script_path = WriteScriptedBinding(dir, "mybindings.bhl", "legacydict", "1.0.0");

      string json = JsonConvert.SerializeObject(new
      {
        bindings = new Dictionary<string, object>
        {
          ["legacydict"] = new { sources = new[] { script_path } }
        },
        tmp_dir = dir,
        use_cache = false,
      });

      var proj = JsonConvert.DeserializeObject<ProjectConf>(json);
      proj.Setup();

      Assert.Single(proj.bindings);
      Assert.False(proj.bindings[0].is_legacy);
      Assert.Equal("legacydict", proj.bindings[0].name);

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

  //NOTE: legacy entries have no 'name' in bhl.proj to verify a declared version against, so
  //      BindingInfo() on a legacy script is rejected outright instead of silently baked in
  [Fact]
  public void LegacyFlatFieldsWithBindingInfoThrows()
  {
    var dir = MakeTempDir();
    try
    {
      var script_path = WriteScriptedBinding(dir, "mybindings.bhl", "legacy_opted_in", "2.0.0");

      var proj = new ProjectConf();
      proj.tmp_dir = dir;
      proj.use_cache = false;
      proj.bindings_sources.Add(script_path);
      proj.Setup();

      Assert.Single(proj.bindings);
      Assert.True(proj.bindings[0].is_legacy);

      Assert.Throws<Exception>(() => proj.LoadBindings());
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void EntryWithNoDeclaredVersionThrows()
  {
    var proj = new ProjectConf();
    proj.bindings.Add(new BindingsEntryConf { name = "no_version_here" });
    proj.Setup();

    Assert.Throws<Exception>(() => proj.LoadBindings());
  }

  [Fact]
  public void EntryWithEmptyNameThrowsDuringSetup()
  {
    var proj = new ProjectConf();
    proj.bindings.Add(new BindingsEntryConf { sources = new List<string> { "somefile.bhl" } });

    Assert.Throws<Exception>(() => proj.Setup());
  }

  //NOTE: registers a module under a distinctive name so a live Register() call is easy to
  //      tell apart from the scripted-binding fallback below, which would register a
  //      DIFFERENT ModuleDeclared object under the very same name and crash on the
  //      resulting Dictionary key collision if both ever ran together. [BhlBinding] makes it
  //      discoverable by BindingsRegistry without any manual registration call
  [BhlBinding(FakeLiveBindings.TestName, "1.0.0")]
  public class FakeLiveBindings : IUserBindings
  {
    public const string TestName = "test_named_bindings_9f3a1c";

    public void Register(Types ts)
    {
      ts.RegisterModule(new ModuleDeclared(TestName));
    }
  }

  [Fact]
  public void UsesLiveBindingAndNeverTouchesConflictingSourcesWhenNameIsRegistered()
  {
    var dir = MakeTempDir();
    try
    {
      //NOTE: if this ever got compiled/registered too (alongside the live binding above),
      //      it would throw registering the same module name twice - proving
      //      LoadBindings() never falls back to it when the name is live
      var conflicting_script = Path.Combine(dir, "conflicting.bhl");
      File.WriteAllText(conflicting_script, "this is not even valid bhl syntax {{{");

      var proj = new ProjectConf();
      proj.tmp_dir = dir;
      proj.use_cache = false;
      proj.bindings.Add(new BindingsEntryConf
      {
        name = FakeLiveBindings.TestName,
        sources = new List<string> { conflicting_script }
      });
      proj.Setup();

      var bindings = (UserBindingsWithInfo)proj.LoadBindings();

      Assert.Single(bindings.info);
      Assert.Equal(FakeLiveBindings.TestName, bindings.info[0].name);
      Assert.Equal("1.0.0", bindings.info[0].version);

      //NOTE: doesn't throw despite the module already existing on ts from bindings.Register
      //      above - proves the sources-based scripted binding never ran
      var ts = new Types();
      bindings.Register(ts);
      Assert.NotNull(ts.FindRegisteredModule(FakeLiveBindings.TestName));
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void FallsBackToSourcesWhenNameIsNotRegistered()
  {
    var dir = MakeTempDir();
    try
    {
      var script_path = WriteScriptedBinding(dir, "fallback.bhl", "not_registered_anywhere_9f3a1c", "3.0.0");

      var proj = new ProjectConf();
      proj.tmp_dir = dir;
      proj.use_cache = false;
      proj.bindings.Add(new BindingsEntryConf
      {
        name = "not_registered_anywhere_9f3a1c",
        sources = new List<string> { script_path }
      });
      proj.Setup();

      var bindings = (UserBindingsWithInfo)proj.LoadBindings();

      Assert.Single(bindings.info);
      Assert.Equal("not_registered_anywhere_9f3a1c", bindings.info[0].name);
      Assert.Equal("3.0.0", bindings.info[0].version);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void NoExplicitDllAutoDerivesOneUnderTmpDirWithMatchingExtension()
  {
    var proj = new ProjectConf();
    proj.bindings.Add(new BindingsEntryConf { name = "bhl_one", sources = new List<string> { "foo.bhl" } });
    proj.bindings.Add(new BindingsEntryConf { name = "cs_one", sources = new List<string> { "foo.cs" } });
    proj.Setup();

    Assert.EndsWith(".bhc", proj.bindings[0].dll);
    Assert.EndsWith(".dll", proj.bindings[1].dll);
    Assert.StartsWith(proj.tmp_dir, proj.bindings[0].dll);
    Assert.StartsWith(proj.tmp_dir, proj.bindings[1].dll);
  }

  [Fact]
  public void AutoDerivedDllPathIsStablePerEntryNameAndDiffersAcrossEntries()
  {
    //NOTE: fixed tmp_dir isolates the entry-name part - tmp_dir itself is covered by
    //      TestProjConf's own tests
    var shared_tmp_dir = MakeTempDir();
    try
    {
      var proj_a = new ProjectConf();
      proj_a.tmp_dir = shared_tmp_dir;
      proj_a.bindings.Add(new BindingsEntryConf { name = "same_name", sources = new List<string> { "foo.bhl" } });
      proj_a.Setup();

      var proj_b = new ProjectConf();
      proj_b.tmp_dir = shared_tmp_dir;
      proj_b.bindings.Add(new BindingsEntryConf { name = "same_name", sources = new List<string> { "foo.bhl" } });
      proj_b.Setup();

      Assert.Equal(proj_a.bindings[0].dll, proj_b.bindings[0].dll);

      var proj_c = new ProjectConf();
      proj_c.tmp_dir = shared_tmp_dir;
      proj_c.bindings.Add(new BindingsEntryConf { name = "different_name", sources = new List<string> { "foo.bhl" } });
      proj_c.Setup();

      Assert.NotEqual(proj_a.bindings[0].dll, proj_c.bindings[0].dll);
    }
    finally
    {
      Directory.Delete(shared_tmp_dir, true);
    }
  }

  [Fact]
  public void ExplicitDllIsNotOverridden()
  {
    var proj = new ProjectConf();
    proj.bindings.Add(new BindingsEntryConf
    {
      name = "explicit",
      sources = new List<string> { "foo.bhl" },
      dll = "my_custom_output.bhc"
    });
    proj.Setup();

    Assert.EndsWith("my_custom_output.bhc", proj.bindings[0].dll);
  }
}
