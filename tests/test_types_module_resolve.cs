using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using bhl;
using Xunit;

// Regression coverage for Types.ResolveNamedByPath not searching registered modules
public class TestTypesModuleResolve
{
  static string MakeTempDir()
  {
    string dir = Path.Combine(Path.GetTempPath(), "bhl_types_module_resolve_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    return dir;
  }

  [Fact]
  public void ResolveNamedByPathFindsSymbolInRegisteredModule()
  {
    var types = new Types();

    var module = new ModuleDeclared("testmod");
    var ns = module.ns.Nest("testmod");
    var inner = new ClassSymbolNative(new Origin(), "Inner", typeof(object));
    ns.Define(inner);
    inner.Setup();
    types.RegisterModule(module);

    var proxy = types.T("testmod.Inner");
    Assert.Same(inner, proxy.Get());
  }

  //NOTE: module name and nested namespace name are unrelated here on purpose
  [Fact]
  public void ResolveNamedByPathDoesNotDependOnModuleNameMatchingNamespaceName()
  {
    var types = new Types();

    var module = new ModuleDeclared("completely_unrelated_module_name");
    var ns = module.ns.Nest("whatever");
    var inner = new ClassSymbolNative(new Origin(), "Inner", typeof(object));
    ns.Define(inner);
    inner.Setup();
    types.RegisterModule(module);

    var proxy = types.T("whatever.Inner");
    Assert.Same(inner, proxy.Get());
  }

  //NOTE: TArr/TMap bottom out in the same self.T(name) -> ResolveNamedByPath path for any
  //      name-based element/key/value type argument, so they're fixed by the same change
  [Fact]
  public void TArrAndTMapResolveElementTypeFromRegisteredModule()
  {
    var types = new Types();

    var module = new ModuleDeclared("testmod");
    var ns = module.ns.Nest("testmod");
    var inner = new ClassSymbolNative(new Origin(), "Inner", typeof(object));
    ns.Define(inner);
    inner.Setup();
    types.RegisterModule(module);

    var arr_proxy = types.TArr("testmod.Inner");
    Assert.IsType<GenericArrayTypeSymbol>(arr_proxy.Get());

    var map_proxy = types.TMap("string", "testmod.Inner");
    Assert.IsType<GenericMapTypeSymbol>(map_proxy.Get());
  }

  [Fact]
  public async Task ScriptedBindingCrossReferenceToOwnModuleResolves()
  {
    var dir = MakeTempDir();
    try
    {
      //NOTE: mirrors UnityBindings.bhl's shape - cross-references its own module via types.T
      File.WriteAllText(Path.Combine(dir, "bindings.bhl"), @"
import ""std/bind""
func string,string BindingInfo() {
  return ""testmod"", ""1.0.0""
}
func RegisterBindings(std.bind.Types types) {
  var module = std.bind.NewModuleDeclared(""testmod"")
  var ns = module.ns.Nest(""testmod"")

  var inner = std.bind.NewClassSymbolNative(""Inner"", null, false)
  ns.Define(inner)
  inner.Setup()

  var outer = std.bind.NewClassSymbolNative(""Outer"", null, false)
  ns.Define(outer)
  outer.Define(std.bind.NewFieldSymbol(""inner"", types.T(""testmod.Inner""), true, false))
  outer.Setup()

  types.RegisterModule(module)
}
");
      File.WriteAllText(Path.Combine(dir, "main.bhl"), @"
import ""testmod""
class Foo {
  testmod.Outer outer
}
func float test() {
  Foo f = new Foo
  testmod.Inner x = f.outer.inner
  return 0
}
");

      var proj = new ProjectConf();
      proj.src_dirs.Add(dir);
      proj.module_fmt = ModuleBinaryFormat.FMT_BIN;
      proj.result_file = Path.Combine(dir, "result.bin");
      proj.tmp_dir = Path.Combine(dir, "cache");
      proj.error_file = Path.Combine(dir, "error.log");
      proj.use_cache = false;
      proj.verbosity = 0;
      proj.bindings.Add(new BindingsEntryConf
      {
        name = "testmod",
        sources = new List<string> { Path.Combine(dir, "bindings.bhl") }
      });
      proj.Setup();

      var conf = new CompileConf();
      conf.ts = new Types();
      conf.logger = new Logger(0, new ConsoleLogger());
      conf.proj = proj;
      conf.files = BuildUtils.NormalizeFilePaths(new List<string> { Path.Combine(dir, "main.bhl") });
      conf.bindings = proj.LoadBindings();

      var executor = new CompilationExecutor();
      var result = await executor.Exec(conf);

      Assert.Empty(result.errors);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }
}
