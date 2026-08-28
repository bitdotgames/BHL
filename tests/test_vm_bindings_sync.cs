using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using bhl;
using Xunit;

// Regression coverage for a real bug: a VM constructed BEFORE its bindings are known (e.g.
// UnityBHL's BHL.cs, which creates an empty VM and attaches bytecode/bindings later) only
// gets types.modules mirrored into its OWN module registry once, at construction time (see
// VM's ctor in vm.cs). A native module registered onto types.modules AFTERWARD (e.g. via
// BindingsRegistry.RegisterRequiredBindings) is invisible to VM.FindModule unless also synced
// in explicitly via the new public VM.RegisterModule(ModuleDeclared) - see vm.module.cs.
public class TestVMBindingsSync
{
  public const string ModName = "test_vm_sync_mod_7c2a";

  [BhlBinding(ModName, "1.0.0")]
  public class FakeModBindings : IUserBindings
  {
    public void Register(Types ts)
    {
      ts.RegisterModule(new ModuleDeclared(ModName));
    }
  }

  static string MakeTempDir()
  {
    string dir = Path.Combine(Path.GetTempPath(), "bhl_vm_bindings_sync_" + Guid.NewGuid().ToString("N"));
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
    //NOTE: resolves via RegistryBindings since FakeModBindings' [BhlBinding] makes it
    //      discoverable under ModName - embeds required-bindings metadata into the
    //      compiled bytes (UserBindingsWithInfo)
    conf.bindings = proj.LoadBindings();

    var executor = new CompilationExecutor();
    var result = await executor.Exec(conf);
    Assert.Empty(result.errors);

    return (File.ReadAllBytes(proj.result_file), dir);
  }

  [Fact]
  public async Task ImportOfPostConstructionBindingFailsWithoutSync()
  {
    var (bytes, dir) = await CompileImporter();
    try
    {
      //NOTE: mirrors BHL.cs's DefaultVMCreator.MakeVM() - VM built before any bindings exist
      var vm = new VM();
      var loader = new ModuleLoader(vm.types, new MemoryStream(bytes));
      BindingsRegistry.RegisterRequiredBindings(vm.types, loader);
      vm.Loader = loader;

      Assert.Throws<Exception>(() => vm.LoadModule("importer"));
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public async Task ImportOfPostConstructionBindingSucceedsAfterSync()
  {
    var (bytes, dir) = await CompileImporter();
    try
    {
      var vm = new VM();
      var loader = new ModuleLoader(vm.types, new MemoryStream(bytes));
      BindingsRegistry.RegisterRequiredBindings(vm.types, loader);

      foreach(var (name, _) in loader.RequiredBindings)
      {
        var decl = vm.types.FindRegisteredModule(name);
        if(decl != null)
          vm.RegisterModule(decl);
      }

      vm.Loader = loader;

      Assert.True(vm.LoadModule("importer"));
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }
}
