using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using bhl;
using Xunit;

public class TestReload : BHL_TestBase
{
  //NOTE: mirrors MakeVM(ModuleDeclared,...) - total_gvars_num etc. is only
  //      finalized after a ToStream/FromStream round trip
  ModuleDeclared CompileModule(string bhl, Types ts, string name = "test")
  {
    var proc = Parse(bhl, ts, throw_errors: true);
    var raw = new ModuleCompiler(proc.result).Compile();
    raw.name = name;

    var ms = new MemoryStream();
    raw.ToStream(ms);
    return ModuleDeclared.FromStream(ts, new MemoryStream(ms.GetBuffer()));
  }

  [Fact]
  public void TestReloadNewCallsUseNewVersion()
  {
    var ts = new Types();

    var declV1 = CompileModule(@"
    func int calc()
    {
      return 1
    }
    ", ts);
    var vm = new VM(ts);
    vm.LoadModule(new Module(declV1));

    Assert.Equal(1, Execute(vm, "calc").Stack.Pop().num);

    var declV2 = CompileModule(@"
    func int calc()
    {
      return 2
    }
    ", ts);
    vm.Reload(new Module(declV2));

    Assert.Equal(2, Execute(vm, "calc").Stack.Pop().num);
  }

  [Fact]
  public void TestReloadPreservesMatchingGlobalVar()
  {
    var ts = new Types();

    var declV1 = CompileModule(@"
    int counter = 0

    func void bump()
    {
      counter += 1
    }

    func int get_counter()
    {
      return counter
    }
    ", ts);
    var vm = new VM(ts);
    vm.LoadModule(new Module(declV1));

    Execute(vm, "bump");
    Execute(vm, "bump");
    Assert.Equal(2, Execute(vm, "get_counter").Stack.Pop().num);

    var declV2 = CompileModule(@"
    int counter = 0

    func void bump()
    {
      counter += 10
    }

    func int get_counter()
    {
      return counter
    }
    ", ts);
    var report = vm.Reload(new Module(declV2));

    var counter_entry = report.globals.Find(g => g.name == "counter");
    Assert.NotNull(counter_entry);
    Assert.True(counter_entry.migrated);

    Assert.Equal(2, Execute(vm, "get_counter").Stack.Pop().num);

    Execute(vm, "bump");
    Assert.Equal(12, Execute(vm, "get_counter").Stack.Pop().num);
  }

  [Fact]
  public void TestReloadResetsGlobalVarOnTypeChange()
  {
    var ts = new Types();

    var declV1 = CompileModule(@"
    int flag = 0
    ", ts);
    var vm = new VM(ts);
    vm.LoadModule(new Module(declV1));

    var declV2 = CompileModule(@"
    string flag = ""new""

    func string get_flag()
    {
      return flag
    }
    ", ts);
    var report = vm.Reload(new Module(declV2));

    var flag_entry = report.globals.Find(g => g.name == "flag");
    Assert.NotNull(flag_entry);
    Assert.False(flag_entry.migrated);

    Assert.Equal("new", Execute(vm, "get_flag").Stack.Pop().str);
  }

  [Fact]
  public void TestReloadKeepsInFlightFiberOnOldCode()
  {
    var ts = new Types();

    var declV1 = CompileModule(@"
    coro func int runner()
    {
      yield()
      return 1
    }
    ", ts);
    var vm = new VM(ts);
    vm.LoadModule(new Module(declV1));

    var fiber1 = vm.Start("runner");
    //NOTE: suspends right at yield()
    Assert.True(vm.Tick());

    var declV2 = CompileModule(@"
    coro func int runner()
    {
      yield()
      return 2
    }
    ", ts);
    vm.Reload(new Module(declV2));

    //NOTE: fiber1 resumes and finishes against the *old* module it started on
    Assert.False(vm.Tick());
    Assert.Equal(1, fiber1.Stack.Pop().num);

    //NOTE: any new call resolves to the new version
    var fiber2 = vm.Start("runner");
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    Assert.Equal(2, fiber2.Stack.Pop().num);
  }

  [Fact]
  public void TestReloadThrowsWhenModuleNotLoaded()
  {
    var ts = new Types();
    var vm = new VM(ts);

    var decl = CompileModule(@"func void foo() { }", ts, name: "nope");

    Assert.Throws<Exception>(() => vm.Reload(new Module(decl)));
  }

  [Fact]
  public async Task TestReloadIndirectImportPropagatesOnlyAfterRelink()
  {
    string libV1 = @"
    func int lib_calc()
    {
      return 1
    }
    ";

    string mainSrc = @"
    import ""lib""

    func int main_calc()
    {
      return lib_calc()
    }
    ";

    CleanTestDir();
    var files = new List<string>();
    var lib_path = TestDirPath() + "/lib.bhl";
    NewTestFile("lib.bhl", libV1, ref files);
    NewTestFile("main.bhl", mainSrc, ref files);

    var conf = MakeCompileConf(files, max_threads: 1);
    conf.indirect_imports = true;

    var loader = new ModuleLoader(conf.ts, await CompileFiles(conf));
    var vm = new VM(conf.ts, loader);
    vm.LoadModule("main");

    Assert.Equal(1, Execute(vm, "main_calc").Stack.Pop().num);

    File.WriteAllText(lib_path, @"
    func int lib_calc()
    {
      return 2
    }
    ");

    var lib_conf = MakeCompileConf(new List<string>() { lib_path }, max_threads: 1);
    lib_conf.ts = conf.ts;
    var lib_loader = new ModuleLoader(lib_conf.ts, await CompileFiles(lib_conf));
    var declLibV2 = lib_loader.Load("lib", lib_conf.ts);

    vm.Reload(new Module(declLibV2));

    //NOTE: not relinked yet, "main" still calls old "lib"
    Assert.Equal(1, Execute(vm, "main_calc").Stack.Pop().num);

    vm.RelinkImports("lib");

    //NOTE: "main" picks up new "lib" with no recompilation of its own
    Assert.Equal(2, Execute(vm, "main_calc").Stack.Pop().num);
  }

  [Fact]
  public void TestMigrateInstancePreservesFieldsAndUsesNewMethods()
  {
    var ts = new Types();

    var declV1 = CompileModule(@"
    class Unit
    {
      int health

      func int get_health()
      {
        return this.health
      }

      func int tick()
      {
        return 1
      }
    }

    func Unit make_unit()
    {
      Unit u = new Unit
      u.health = 42
      return u
    }
    ", ts);

    var vm = new VM(ts);
    vm.LoadModule(new Module(declV1));

    var unit_val = Execute(vm, "make_unit").Stack.Pop();
    var old_class = unit_val.type;

    var declV2 = CompileModule(@"
    class Unit
    {
      int health
      int level

      func int get_health()
      {
        return this.health
      }

      func int get_level()
      {
        return this.level
      }

      func int tick()
      {
        return 2
      }
    }

    func int call_tick(Unit u)
    {
      return u.tick()
    }

    func int call_get_health(Unit u)
    {
      return u.get_health()
    }

    func int call_get_level(Unit u)
    {
      return u.get_level()
    }
    ", ts);

    vm.Reload(new Module(declV2));

    //NOTE: Reload() alone doesn't touch already-existing instances
    Assert.Same(old_class, unit_val.type);

    vm.MigrateInstance(ref unit_val);

    Assert.NotSame(old_class, unit_val.type);

    //NOTE: passing a Val as a call arg transfers ownership (same convention as
    //      ValList.Add) - retain before each reuse since we keep calling with it
    unit_val._refc?.Retain();
    Assert.Equal(2, Execute(vm, "call_tick", unit_val).Stack.Pop().num);
    unit_val._refc?.Retain();
    Assert.Equal(42, Execute(vm, "call_get_health", unit_val).Stack.Pop().num);
    unit_val._refc?.Retain();
    Assert.Equal(0, Execute(vm, "call_get_level", unit_val).Stack.Pop().num);

    unit_val._refc?.Release();
  }
}
