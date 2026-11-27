using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using bhl;
using Xunit;

// Bindings a host already holds are attached with Types.RegisterBindings instead of being
// found by name. EnsureRequiredBindings then only version checks what's already there, so a
// host that registers everything explicitly never reaches BindingsRegistry's domain scan -
// which under IL2CPP materializes metadata that is never released.
public class TestExplicitBindings
{
  public const string ModName = "test_explicit_mod_4f91";

  [BhlBinding(ModName, "1.2.0")]
  public class ModBindings : IUserBindings
  {
    public void Register(Types ts) => ts.RegisterModule(new ModuleDeclared(ModName));
  }

  //NOTE: same module, deliberately a major behind what the bytecode will require
  public class StaleModBindings : IUserBindings
  {
    public void Register(Types ts) => ts.RegisterModule(new ModuleDeclared(ModName));
  }

  static string MakeTempDir()
  {
    string dir = Path.Combine(Path.GetTempPath(), "bhl_explicit_bindings_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    return dir;
  }

  static async Task<(byte[] bytes, string dir)> CompileImporter()
  {
    var dir = MakeTempDir();
    File.WriteAllText(Path.Combine(dir, "importer.bhl"), $"import \"{ModName}\"\n");

    var proj = new ProjectConf();
    proj.src_dirs.Add(dir);
    proj.module_fmt = ModuleBinaryFormat.FMT_BIN;
    proj.result_file = Path.Combine(dir, "result.bin");
    proj.tmp_dir = Path.Combine(dir, "cache");
    proj.error_file = Path.Combine(dir, "error.log");
    proj.use_cache = false;
    proj.verbosity = 0;
    proj.bindings.Add(new BindingsEntryConf { name = ModName });
    proj.Setup();

    var conf = new CompileConf();
    conf.ts = new Types();
    conf.logger = new Logger(0, new ConsoleLogger());
    conf.proj = proj;
    conf.files = BuildUtils.NormalizeFilePaths(new List<string> { Path.Combine(dir, "importer.bhl") });
    conf.bindings = proj.LoadBindings();

    var executor = new CompilationExecutor();
    var result = await executor.Exec(conf);
    Assert.Empty(result.errors);

    return (File.ReadAllBytes(proj.result_file), dir);
  }

  [Fact]
  public void TestRegisterBindingsRecordsTheDeclaredVersion()
  {
    var ts = new Types();
    ts.RegisterBindings(new ModBindings());

    Assert.True(ts.TryGetBindingsVersion(ModName, out var version));
    Assert.Equal("1.2.0", version);
  }

  //NOTE: without an attribute there's nothing to record, but Register still runs
  [Fact]
  public void TestRegisterBindingsWithoutAttributeStillRegisters()
  {
    var ts = new Types();
    ts.RegisterBindings(new StaleModBindings());

    Assert.False(ts.TryGetBindingsVersion(ModName, out _));
    Assert.NotNull(ts.FindRegisteredModule(ModName));
  }

  [Fact]
  public async Task TestExplicitlyRegisteredSatisfiesTheRequirement()
  {
    var (bytes, dir) = await CompileImporter();
    try
    {
      var ts = new Types();
      ts.RegisterBindings(new ModBindings());

      var loader = new ModuleLoader(ts, new MemoryStream(bytes));
      //NOTE: already present, so this only version checks it
      BindingsRegistry.EnsureRequiredBindings(ts, loader);

      Assert.True(ts.TryGetBindingsVersion(ModName, out _));
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public async Task TestExplicitlyRegisteredIsStillVersionChecked()
  {
    var (bytes, dir) = await CompileImporter();
    try
    {
      var ts = new Types();
      //NOTE: registered by hand under the required name, but a major behind
      ts.RegisterBindingsVersion(ModName, "0.9.0");
      new StaleModBindings().Register(ts);

      var loader = new ModuleLoader(ts, new MemoryStream(bytes));

      var e = Assert.Throws<Exception>(() => BindingsRegistry.EnsureRequiredBindings(ts, loader));
      Assert.Contains("version incompatible", e.Message);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }
}
