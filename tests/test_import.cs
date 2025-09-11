using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using bhl;
using Xunit;

public class TestImport : BHL_TestBase
{
  [Fact]
  public async Task TestSimpleImport()
  {
    string bhl1 = @"
    import ""bhl2""

    func garbage1() { }

    func float bhl1()
    {
      return bhl2(23)
    }
    ";

    string bhl2 = @"
    import ""bhl3""

    func garbage2() { }

    func float bhl2(float k)
    {
      return bhl3(k)
    }
    ";

    string bhl3 = @"
    func float bhl3(float k)
    {
      return k
    }

    func garbage3() { }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var ts = new Types();
    var loader = new ModuleLoader(ts, await CompileFiles(files));

    AssertEqual(loader.Load("bhl1", ts),
      new ModuleCompiler()
        .UseCode()
        .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
        .EmitThen(Opcodes.Return)
        .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
        .EmitThen(Opcodes.Constant, new int[] { 0 })
        .EmitThen(Opcodes.Call, new int[] { 0, 3, 1 })
        .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
        .EmitThen(Opcodes.Return)
    );
    AssertEqual(loader.Load("bhl2", ts),
      new ModuleCompiler()
        .UseCode()
        .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
        .EmitThen(Opcodes.Return)
        .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/ })
        .EmitThen(Opcodes.ArgVar, new int[] { 0 })
        .EmitThen(Opcodes.GetVar, new int[] { 0 })
        .EmitThen(Opcodes.Call, new int[] { 0, 0, 1 })
        .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
        .EmitThen(Opcodes.Return)
    );
    AssertEqual(loader.Load("bhl3", ts),
      new ModuleCompiler()
        .UseCode()
        .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/ })
        .EmitThen(Opcodes.ArgVar, new int[] { 0 })
        .EmitThen(Opcodes.GetVar, new int[] { 0 })
        .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
        .EmitThen(Opcodes.Return)
        .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
        .EmitThen(Opcodes.Return)
    );

    var vm = new VM(ts, loader);
    vm.LoadModule("bhl1");
    Assert.Equal(23, Execute(vm, "bhl1").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestBadImport()
  {
    string bhl1 = @"
    import ""garbage""
    func float bhl1()
    {
      return bhl2(23)
    }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);

    await AssertErrorAsync<Exception>(
      async delegate() { await CompileFiles(files); },
      "invalid import 'garbage'",
      new PlaceAssert(bhl1, @"
    import ""garbage""
----^"
      )
    );
  }

  [Fact]
  public async Task TestBadImportNotInSourceFiles()
  {
    string bhl1 = @"
    import ""bhl2""
    func float bhl1()
    {
      return bhl2()
    }
    ";

    string bhl2 = @"
    func bhl2()
    {}
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    //NOTE: let's create it but remove from processed sources
    files.RemoveAt(NewTestFile("bhl2.bhl", bhl2, ref files));

    await AssertErrorAsync<Exception>(
      async delegate() { await CompileFiles(files); },
      "invalid import 'bhl2'",
      new PlaceAssert(bhl1, @"
    import ""bhl2""
----^"
      )
    );
  }

  [Fact]
  public async Task TestSelfImportIsIgnored()
  {
    string bhl1 = @"
    import ""bhl1""
    func bhl1()
    {}
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);

    await CompileFiles(files);
  }

  [Fact]
  public async Task TestDoubleImportError()
  {
    string file_a = @"
      func foo() {}
    ";

    string file_test = @"
    import ""./a""
    import ""a""

    func test() {}
    ";

    await AssertErrorAsync<Exception>(async delegate()
      {
        await CompileFiles(new Dictionary<string, string>()
          {
            {"a.bhl", file_a},
            {"test.bhl", file_test},
          }
        );
      },
      "already imported 'a'",
      new PlaceAssert(file_test, @"
    import ""a""
----^"
      )
    );
  }

  [Fact]
  public async Task TestIncrementalBuildOfChangedFiles()
  {
    string file_unit = @"
      class Unit {
        int test
      }
      Unit u = {test: 23}
    ";

    string file_test = @"
    import ""garbage"";import ""unit"";

    func int test()
    {
      return u.test
    }
    ";

    string file_garbage = @"
      func garbage() {}
    ";

    CleanTestDir();

    var files = new List<string>();
    NewTestFile("unit.bhl", file_unit, ref files);
    NewTestFile("test.bhl", file_test, ref files);
    NewTestFile("garbage.bhl", file_garbage, ref files);

    {
      var exec = new CompilationExecutor();
      var conf = MakeCompileConf(files, use_cache: true, max_threads: 3);
      var ts = new Types();
      var loader = new ModuleLoader(ts, await CompileFiles(exec, conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      Assert.Equal(23, Execute(vm, "test").result.PopRelease().num);
      Assert.Equal(0, exec.cache_hits);
      Assert.Equal(3, exec.cache_miss);
      Assert.Equal(0, exec.cache_errs);
    }

    string new_file_unit = @"
      class Unit {
        string new_field
        int test
      }
      Unit u = {test: 32}
    ";
    int fidx = NewTestFile("unit.bhl", new_file_unit, ref files, unique: true);
    System.IO.File.SetLastWriteTimeUtc(files[fidx], DateTime.UtcNow.AddSeconds(1));

    {
      var exec = new CompilationExecutor();
      var conf = MakeCompileConf(files, use_cache: true, max_threads: 3);
      var ts = new Types();
      var loader = new ModuleLoader(ts, await CompileFiles(exec, conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      Assert.Equal(32, Execute(vm, "test").result.PopRelease().num);
      Assert.Equal(1, exec.cache_hits);
      Assert.Equal(1 + 1, exec.cache_miss);
      Assert.Equal(0, exec.cache_errs);
    }
  }

  [Fact]
  public async Task TestIncrementalBuildOfChangedFilesWithIntermediateFile()
  {
    string file_unit = @"
      class Unit {
        int test
      }
    ";

    string file_get = @"
    import ""unit""

    func Unit get() {
      Unit u = {test: 23}
      return u
    }
    ";

    string file_test = @"
    import ""get""

    func int test() {
      return get().test
    }
    ";

    CleanTestDir();

    var files = new List<string>();
    NewTestFile("unit.bhl", file_unit, ref files);
    NewTestFile("get.bhl", file_get, ref files);
    NewTestFile("test.bhl", file_test, ref files);

    {
      var exec = new CompilationExecutor();
      var conf = MakeCompileConf(files, use_cache: true, max_threads: 3);
      var ts = new Types();
      var loader = new ModuleLoader(ts, await CompileFiles(exec, conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      Assert.Equal(23, Execute(vm, "test").result.PopRelease().num);
      Assert.Equal(0, exec.cache_hits);
      Assert.Equal(3, exec.cache_miss);
      Assert.Equal(0, exec.cache_errs);
    }

    string new_file_get = @"
    import ""unit""

    func Unit get() {
      Unit u = {test: 32}
      return u
    }
    ";
    int fidx = NewTestFile("get.bhl", new_file_get, ref files, unique: true);
    System.IO.File.SetLastWriteTimeUtc(files[fidx], DateTime.UtcNow.AddSeconds(1));

    {
      var exec = new CompilationExecutor();
      var conf = MakeCompileConf(files, use_cache: true, max_threads: 3);
      var ts = new Types();
      var loader = new ModuleLoader(ts, await CompileFiles(exec, conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      Assert.Equal(32, Execute(vm, "test").result.PopRelease().num);
      Assert.Equal(1, exec.cache_hits);
      Assert.Equal(1 + 1, exec.cache_miss);
      Assert.Equal(0, exec.cache_errs);
    }
  }

  [Fact]
  public async Task TestIncrementalBuildOfChangedFilesWithIntermediateFile2()
  {
    string file_unit = @"
      class Unit {
        int test
      }
    ";

    string file_get = @"
    import ""/collections/unit""

    Unit global_unit

    func Unit get(int a) {
      Unit u = {test: 23 + a}
      return u
    }
    ";

    string file_use =  @"
    import ""/collections/unit""

    func use_unit() {
      Unit u = {test: 42}
    }
    ";

    string file_test = @"
    import ""/try/get""

    func int test() {
      return get(1).test
    }
    ";

    CleanTestDir();

    var files = new List<string>();
    NewTestFile(Path.Combine("collections", "unit.bhl"), file_unit, ref files);
    NewTestFile(Path.Combine("try", "get.bhl"), file_get, ref files);
    NewTestFile("test.bhl", file_test, ref files);
    NewTestFile(Path.Combine("use", "use.bhl"), file_use, ref files);

    {
      var exec = new CompilationExecutor();
      var conf = MakeCompileConf(files, use_cache: true, max_threads: 3);
      var ts = new Types();
      var loader = new ModuleLoader(ts, await CompileFiles(exec, conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      Assert.Equal(23 + 1, Execute(vm, "test").result.PopRelease().num);
    }

    string new_file_test = @"
    import ""/try/get""

    namespace foo {
      func int get_add() {
        return get(2).test + 1
      }
    }

    func int test() {
      return foo.get_add()
    }
    ";
    int fidx = NewTestFile("test.bhl", new_file_test, ref files, unique: true);
    System.IO.File.SetLastWriteTimeUtc(files[fidx], DateTime.UtcNow.AddSeconds(1));

    {
      var exec = new CompilationExecutor();
      var conf = MakeCompileConf(files, use_cache: true, max_threads: 3);
      var ts = new Types();
      var loader = new ModuleLoader(ts, await CompileFiles(exec, conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      Assert.Equal(24 + 2, Execute(vm, "test").result.PopRelease().num);
      Assert.Equal(3, exec.cache_hits);
      Assert.Equal(1, exec.cache_miss);
      Assert.Equal(0, exec.cache_errs);
    }
  }

  [Fact]
  public async Task TestIncrementalBuildOfChangedFilesWithChangedImportedDependency()
  {
    string file_unit = @"
      class Unit {
        int test
      }
    ";

    string file_get = @"
    import ""unit""

    func Unit get() {
      Unit u = {test: 23}
      return u
    }
    ";

    string file_test = @"
    import ""get""

    func int test() {
      return get().test
    }
    ";

    CleanTestDir();

    var files = new List<string>();
    NewTestFile("unit.bhl", file_unit, ref files);
    NewTestFile("get.bhl", file_get, ref files);
    NewTestFile("test.bhl", file_test, ref files);

    {
      var exec = new CompilationExecutor();
      var conf = MakeCompileConf(files, use_cache: true, max_threads: 3);
      var ts = new Types();
      var loader = new ModuleLoader(ts, await CompileFiles(exec, conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      Assert.Equal(23, Execute(vm, "test").result.PopRelease().num);
      Assert.Equal(0, exec.cache_hits);
      Assert.Equal(3, exec.cache_miss);
      Assert.Equal(0, exec.cache_errs);
    }

    string new_file_unit = @"
      class Unit {
        int test
        int test2
      }
    ";
    int fidx = NewTestFile("unit.bhl", new_file_unit, ref files, unique: true);
    System.IO.File.SetLastWriteTimeUtc(files[fidx], DateTime.UtcNow.AddSeconds(1));

    {
      var exec = new CompilationExecutor();
      var conf = MakeCompileConf(files, use_cache: true, max_threads: 3);
      var ts = new Types();
      var loader = new ModuleLoader(ts, await CompileFiles(exec, conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      Assert.Equal(23, Execute(vm, "test").result.PopRelease().num);
      Assert.Equal(1, exec.cache_hits);
      Assert.Equal(2, exec.cache_miss);
      Assert.Equal(0, exec.cache_errs);
    }
  }

  [Fact]
  public async Task TestIncrementalBuildOfChangedFilesWithGlobalVars()
  {
    string file_unit = @"
      class Unit {
        int test
      }

      Unit gunit = {test: 42}
    ";

    string file_get =  @"
    import ""/unit""

    func int get(int i) {
      return gunit.test + i
    }
    ";

    string file_test = @"
    import ""/get""

    func int test() {
      return get(1)
    }
    ";

    CleanTestDir();

    var files = new List<string>();
    NewTestFile("unit.bhl", file_unit, ref files);
    NewTestFile("get.bhl", file_get, ref files);
    NewTestFile("test.bhl", file_test, ref files);

    {
      var exec = new CompilationExecutor();
      var conf = MakeCompileConf(files, use_cache: true, max_threads: 3);
      var ts = new Types();
      var loader = new ModuleLoader(ts, await CompileFiles(exec, conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      Assert.Equal(42 + 1, Execute(vm, "test").result.PopRelease().num);
    }

    string new_file_get = @"
    import ""/unit""

    Unit gunit2 = {test: 10}

    func int get(int i) {
      return gunit.test + i + gunit2.test
    }
    ";
    int fidx = NewTestFile("get.bhl", new_file_get, ref files, unique: true);
    System.IO.File.SetLastWriteTimeUtc(files[fidx], DateTime.UtcNow.AddSeconds(1));

    {
      var exec = new CompilationExecutor();
      var conf = MakeCompileConf(files, use_cache: true, max_threads: 3);
      var ts = new Types();
      var loader = new ModuleLoader(ts, await CompileFiles(exec, conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      Assert.Equal(42 + 1 + 10, Execute(vm, "test").result.PopRelease().num);
      Assert.Equal(1, exec.cache_hits);
      Assert.Equal(2, exec.cache_miss);
      Assert.Equal(0, exec.cache_errs);
    }
  }

  [Fact]
  public async Task TestImportEnum()
  {
    string bhl1 = @"
    import ""bhl2""

    func float test()
    {
      Foo f = Foo.B
      return bar(f)
    }
    ";

    string bhl2 = @"
    enum Foo
    {
      A = 2
      B = 3
    }

    func int bar(Foo f)
    {
      return (int)f * 10
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );

    vm.LoadModule("bhl1");
    Assert.Equal(30, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestImportEnumConflict()
  {
    string bhl1 = @"
    import ""bhl2""

    enum Bar {
      FOO = 1
    }

    func test() { }
    ";

    string bhl2 = @"
    enum Bar {
      BAR = 2
    }
    ";

    await AssertErrorAsync<Exception>(
      async delegate()
      {
        await MakeVM(new Dictionary<string, string>()
          {
            {"bhl1.bhl", bhl1},
            {"bhl2.bhl", bhl2},
          }
        );
      },
      @"already defined symbol 'Bar'",
      new PlaceAssert(bhl1, @"
    enum Bar {
----^"
      )
    );
  }

  [Fact]
  public async Task TestImportReadWriteGlobalVar()
  {
    string bhl1 = @"
    import ""bhl2""
    func float test()
    {
      foo = 10
      return foo
    }
    ";

    string bhl2 = @"

    float foo = 1

    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );

    vm.LoadModule("bhl1");
    Assert.Equal(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestImportReadWriteSeveralGlobalVars()
  {
    string bhl1 = @"
    import ""bhl2""
    func float test()
    {
      foo = 10
      return foo + boo
    }
    ";

    string bhl2 = @"

    float foo = 1
    float boo = 2

    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );

    vm.LoadModule("bhl1");
    Assert.Equal(12, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestImportGlobalObjectVar()
  {
    string bhl1 = @"
    import ""bhl3""
    func float test()
    {
      return foo.x
    }
    ";

    string bhl2 = @"

    class Foo
    {
      float x
    }

    ";

    string bhl3 = @"
    import ""bhl2""

    Foo foo = {x : 10}

    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      }
    );

    vm.LoadModule("bhl1");
    Assert.Equal(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestImportGlobalObjectVarWithDepCycles()
  {
    string main_bhl = @"
    import ""g""
    import ""input""
    import ""unit""

    func int test() {
      return unit_count()
    }
    ";

    string g_bhl = @"
    class G
    {
      float x
    }
    ";

    string input_bhl = @"
    import ""unit""

    []int ins = []

    func int unit_count() {
      return ins.Count
    }

    ";

    string unit_bhl = @"
    import ""g""
    import ""ability""

    func int garbage_func() {
      return 1
    }

    []int units = []

    ";

    string ability_bhl = @"
      import ""unit""

      func abliity_dummy() {}
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"main.bhl", main_bhl},
        {"g.bhl", g_bhl},
        {"input.bhl", input_bhl},
        {"ability.bhl", ability_bhl},
        {"unit.bhl", unit_bhl},
      }
    );

    vm.LoadModule("main");
    Assert.Equal(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestImportGlobalVarConflict()
  {
    string bhl1 = @"
    import ""bhl2""

    int foo = 10

    func test() { }
    ";

    string bhl2 = @"
    float foo = 100
    ";

    await AssertErrorAsync<Exception>(
      async delegate()
      {
        await MakeVM(new Dictionary<string, string>()
          {
            {"bhl1.bhl", bhl1},
            {"bhl2.bhl", bhl2},
          }
        );
      },
      @"already defined symbol 'foo'",
      new PlaceAssert(bhl1, @"
    int foo = 10
--------^"
      )
    );
  }

  [Fact]
  public async Task TestImportMixed()
  {
    string bhl1 = @"
    import ""bhl3""
    func float what(float k)
    {
      return hey(k)
    }

    import ""bhl2""
    func float test(float k)
    {
      return bar(k) * what(k)
    }

    ";

    string bhl2 = @"
    func float bar(float k)
    {
      return k
    }
    ";

    string bhl3 = @"
    func float hey(float k)
    {
      return k
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      }
    );

    vm.LoadModule("bhl1");
    Assert.Equal(4, Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestImportWithCycles()
  {
    string bhl1 = @"
    import ""bhl2""
    import ""bhl3""

    func float test(float k)
    {
      return bar(k)
    }
    ";

    string bhl2 = @"
    import ""bhl3""

    func float bar(float k)
    {
      return hey(k)
    }
    ";

    string bhl3 = @"
    import ""bhl2""

    func float hey(float k)
    {
      return k
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      }
    );

    vm.LoadModule("bhl1");
    Assert.Equal(23, Execute(vm, "test", Val.NewNum(vm, 23)).result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestImportWithSemicolon()
  {
    string bhl1 = @"
    import ""bhl2"";;;import ""bhl3"";

    func float test(float k)
    {
      return bar(k)
    }
    ";

    string bhl2 = @"
    import ""bhl3""

    func float bar(float k)
    {
      return hey(k)
    }
    ";

    string bhl3 = @"
    import ""bhl2""

    func float hey(float k)
    {
      return k
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      }
    );

    vm.LoadModule("bhl1");
    Assert.Equal(23, Execute(vm, "test", Val.NewNum(vm, 23)).result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestImportConflict()
  {
    string bhl1 = @"
    import ""bhl2""

    func float bar()
    {
      return 1
    }

    func float test()
    {
      return bar()
    }
    ";

    string bhl2 = @"
    func float bar()
    {
      return 2
    }
    ";

    await AssertErrorAsync<Exception>(async delegate()
      {
        await MakeVM(new Dictionary<string, string>()
          {
            {"bhl1.bhl", bhl1},
            {"bhl2.bhl", bhl2},
          }
        );
      },
      @"already defined symbol 'bar'",
      new PlaceAssert(bhl1, @"
    func float bar()
----^"
      )
    );
  }

  [Fact]
  public async Task TestImportInvalidateCachesAfterChange()
  {
    string file_unit = @"
      class Unit {
        int test
      }
      Unit u = {test: 23}
    ";

    string file_test = @"
    import ""garbage"";import ""unit"";

    func int test()
    {
      return u.test
    }
    ";

    string file_garbage = @"
      func garbage() {}
    ";

    var files = MakeFiles(new Dictionary<string, string>()
      {
        {"unit.bhl", file_unit},
        {"test.bhl", file_test},
        {"garbage.bhl", file_garbage},
      }
    );

    {
      var vm = await MakeVM(files, use_cache: true);
      vm.LoadModule("test");
      Assert.Equal(23, Execute(vm, "test").result.PopRelease().num);
    }

    string new_file_unit = @"
      class Unit {
        string new_field
        int test
      }
      Unit u = {test: 32}
    ";
    File.WriteAllText(files[0], new_file_unit);
    System.IO.File.SetLastWriteTimeUtc(files[0], DateTime.UtcNow.AddSeconds(1));

    {
      var vm = await MakeVM(files, use_cache: true);
      vm.LoadModule("test");
      Assert.Equal(32, Execute(vm, "test").result.PopRelease().num);
    }
  }

  [Fact]
  public async Task TestSearchIncludePath()
  {
    string file_unit = @"
      class Unit {
        int test
      }
      Unit u = {test: 23}
    ";

    string file_test = @"
    import ""/unit"";

    func int test()
    {
      return u.test
    }
    ";

    CleanTestDir();

    var files = new List<string>();
    NewTestFile(Path.Combine("unit", "unit.bhl"), file_unit, ref files);
    NewTestFile(Path.Combine("test", "test.bhl"), file_test, ref files);

    {
      var exec = new CompilationExecutor();
      var conf = MakeCompileConf(files, use_cache: true, max_threads: 3);
      conf.proj.inc_path.Clear();
      conf.proj.inc_path.Add(TestDirPath() + "/test");
      conf.proj.inc_path.Add(TestDirPath() + "/unit");

      var ts = new Types();
      var loader = new ModuleLoader(ts, await CompileFiles(exec, conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      Assert.Equal(23, Execute(vm, "test").result.PopRelease().num);
    }
  }

  [Fact]
  public async Task TestImportTryIncludePathWithSlash()
  {
    string file_unit = @"
      class Unit {
        int test
      }
      Unit u = {test: 23}
    ";

    string file_test = @"
    import ""/units/unit""

    func int test()
    {
      return u.test
    }
    ";

    CleanTestDir();

    var files = new List<string>();
    NewTestFile(Path.Combine("units", "unit.bhl"), file_unit, ref files);
    NewTestFile(Path.Combine("tests", "test.bhl"), file_test, ref files);

    {
      var ts = new Types();
      var loader = new ModuleLoader(ts, await CompileFiles(files, use_cache: true));
      var vm = new VM(ts, loader);
      vm.LoadModule("tests/test");
      Assert.Equal(23, Execute(vm, "test").result.PopRelease().num);
    }
  }

  [Fact]
  public async Task TestImportTryIncludePathNoInitialSlash()
  {
    string file_unit = @"
      class Unit {
        int test
      }
      Unit u = {test: 23}
    ";

    string file_test = @"
    import ""units/unit""

    func int test()
    {
      return u.test
    }
    ";

    CleanTestDir();

    var files = new List<string>();
    NewTestFile(Path.Combine("units", "unit.bhl"), file_unit, ref files);
    NewTestFile(Path.Combine("tests", "test.bhl"), file_test, ref files);

    {
      var ts = new Types();
      var loader = new ModuleLoader(ts, await CompileFiles(files, use_cache: true));
      var vm = new VM(ts, loader);
      vm.LoadModule("tests/test");
      Assert.Equal(23, Execute(vm, "test").result.PopRelease().num);
    }
  }

  [Fact]
  public async Task TestImportUseRelativePath()
  {
    string file_unit = @"
      class Unit {
        int test
      }
      Unit u = {test: 23}
    ";

    string file_test = @"
    import ""./unit""

    func int test()
    {
      return u.test
    }
    ";

    CleanTestDir();

    var files = new List<string>();
    NewTestFile(Path.Combine("src", "tests", "unit.bhl"), file_unit, ref files);
    NewTestFile(Path.Combine("src", "tests", "test.bhl"), file_test, ref files);

    {
      var ts = new Types();

      var conf = MakeCompileConf(files, use_cache: true);
      var loader = new ModuleLoader(ts, await CompileFiles(conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("src/tests/test");
      Assert.Equal(23, Execute(vm, "test").result.PopRelease().num);
    }
  }

  [Fact]
  public async Task TestMistakenlyImportedBy3dParty()
  {
    string file_unit = @"
    namespace units {
      class Unit {
        int foo
      }
    }
    ";
    string file_interim = @"
    import ""/unit""
    func interim() { }
    ";

    string file_test = @"
    import ""/interim""

    func test()
    {
      units.Unit u = {}
    }
    ";

    CleanTestDir();

    var files = new List<string>();
    NewTestFile(Path.Combine("test.bhl"), file_test, ref files);
    NewTestFile(Path.Combine("unit.bhl"), file_unit, ref files);
    NewTestFile(Path.Combine("interim.bhl"), file_interim, ref files);

    var exec = new CompilationExecutor();
    var conf = MakeCompileConf(files);
    conf.proj.use_cache = false;
    conf.proj.max_threads = 1;

    await AssertErrorAsync<Exception>(
      async delegate() { await CompileFiles(exec, conf); },
      "type 'units.Unit' not found",
      new PlaceAssert(file_test, @"
      units.Unit u = {}
------^"
      )
    );
  }

  [Fact]
  public async Task TestCommentedImportsIgnored()
  {
    string file_unit = @"
    namespace units {
      class Unit {
        int foo
      }
    }
    ";
    string file_test = @"
    //import ""/garbage1""
    import ""/unit""
    //import ""/garbage2""

    func int test()
    {
      units.Unit u = {foo: 10}
      return u.foo
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"test.bhl", file_test},
        {"unit.bhl", file_unit},
      }
    );

    vm.LoadModule("test");
    Assert.Equal(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestCommentedImportError()
  {
    string file_unit = @"
    namespace units {
      class Unit {
        int foo
      }
    }
    ";
    string file_test = @"
    //import ""/unit""

    func test()
    {
      units.Unit u = {}
    }
    ";

    await AssertErrorAsync<Exception>(
      async delegate()
      {
        await MakeVM(new Dictionary<string, string>()
        {
          {"test.bhl", file_test},
          {"unit.bhl", file_unit},
        });
      },
      "type 'units.Unit' not found",
      new PlaceAssert(file_test, @"
      units.Unit u = {}
------^"
      )
    );
  }

  [Fact]
  public async Task TestModuleNamesCollision()
  {
    string file_test1 = @"
      func test1() {
      }
    ";

    string file_test2 = @"
      func test2() {
      }
    ";

    CleanTestDir();

    var files = new List<string>();
    NewTestFile(Path.Combine("a", "test.bhl"), file_test1, ref files);
    NewTestFile(Path.Combine("b", "test.bhl"), file_test2, ref files);

    var conf = MakeCompileConf(files,
      src_dirs: new List<string>()
      {
        TestDirPath() + "/a",
        TestDirPath() + "/b"
      }
    );

    await AssertErrorAsync<Exception>(async delegate()
      {
        await CompileFiles(conf);
      },
      "module 'test' ambiguous resolving"
    );
  }

  [Fact]
  public async Task TestUseGlobalVarFromAnotherModuleAfterIncrementalBuild()
  {
    string file_a = @"
      []string Strings = []
    ";

    string file_test =  @"
    import ""/a""

    func int test() {
      return Strings.IndexOf(""test"")
    }
    ";

    var files = MakeFiles(new Dictionary<string, string>()
      {
        {"a.bhl", file_a},
        {"test.bhl", file_test},
      }
    );

    {
      var vm = await MakeVM(files);
      vm.LoadModule("test");
      Assert.Equal(-1, Execute(vm, "test").result.PopRelease().num);
    }

    {
      //emulate changes and try incremental build
      System.IO.File.SetLastWriteTimeUtc(files[1], DateTime.UtcNow.AddSeconds(1));
      var vm = await MakeVM(files, use_cache: true);
      vm.LoadModule("test");
      Assert.Equal(-1, Execute(vm, "test").result.PopRelease().num);
    }
  }

  [Fact]
  public async Task TestImportUseSrcDirsWithIncPath()
  {
    string file_unit = @"
      class Unit {
        int test
      }
      Unit u = {test: 23}
    ";

    string file_test = @"
    import ""/units/unit""

    func int test()
    {
      return u.test
    }
    ";

    CleanTestDir();

    var files = new List<string>();
    NewTestFile(Path.Combine("src", "units", "unit.bhl"), file_unit, ref files);
    NewTestFile(Path.Combine("src", "tests", "test.bhl"), file_test, ref files);

    {
      var ts = new Types();

      var conf = MakeCompileConf(files, use_cache: true, inc_paths: new List<string>() { TestDirPath() + "/src/" });
      var loader = new ModuleLoader(ts, await CompileFiles(conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("tests/test");
      Assert.Equal(23, Execute(vm, "test").result.PopRelease().num);
    }
  }

  [Fact]
  public async Task TestImportFuncPtrs()
  {
    string bhl1 = @"
    import ""bhl2""

    func int test0()
    {
      return calc(0)
    }

    func int test1()
    {
      return calc(1)
    }

    func int test2()
    {
      return calc(2)
    }
    ";

    string bhl2 = @"
    import ""bhl3""

    class Garbage : BaseGarbage {
      func garbage() { }
    }

    func int _100() { return 100 }

    func int calc(int i)
    {
       []func int() ptrs = [
          _1,
          _10,
          _100
       ]
       return ptrs[i]()
    }
    ";

    string bhl3 = @"
    class BaseGarbage {
      func base_garbage() { }
    }

    func int _1() { return 1 }

    func int _10() { return 10 }

    ";

    var files = new Dictionary<string, string>()
    {
      { "bhl1.bhl", bhl1 },
      { "bhl2.bhl", bhl2 },
      { "bhl3.bhl", bhl3 },
    };

    var vm = await MakeVM(files);

    vm.LoadModule("bhl1");
    Assert.Equal(1, Execute(vm, "test0").result.PopRelease().num);
    Assert.Equal(10, Execute(vm, "test1").result.PopRelease().num);
    Assert.Equal(100, Execute(vm, "test2").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestImportNativeModulesFromCachedModule()
  {
    string bhl1 = @"
    import ""std""

    class Foo { }

    func string GetFooName() {
      var foo = new Foo
      return std.GetType(foo).Name
    }
    ";

    string bhl2 = @"
    import ""bhl1""

    func string test() {
      return GetFooName()
    }
    ";


    var files = MakeFiles(new Dictionary<string, string>()
    {
      { "bhl1.bhl", bhl1 },
      { "bhl2.bhl", bhl2 },
    });

    {
      var vm = await MakeVM(files);
      vm.LoadModule("bhl2");
      AssertEqual("Foo", Execute(vm, "test").result.PopRelease().str);
      CommonChecks(vm);
    }

    {
      System.IO.File.SetLastWriteTimeUtc(files[1], DateTime.UtcNow.AddSeconds(1));
      var executor = new CompilationExecutor();
      var vm = await MakeVM(files, use_cache: true, executor: executor);
      vm.LoadModule("bhl2");
      Assert.Equal(1, executor.cache_hits);
      Assert.Equal(1, executor.cache_miss);
      AssertEqual("Foo", Execute(vm, "test").result.PopRelease().str);
      CommonChecks(vm);
    }
  }

  [Fact]
  public async Task TestImportInheritedClassFromCachedModule()
  {
    string bhl1 = @"
    import ""interim"" //we'll force this class change

    class Base {
     func int Do() { return 10 }
    }
    ";

    string bhl2 = @"
    import ""bhl1""

    class Foo : Base { }
    ";

    string interim = @"
    func interim() {}
    ";

    string bhl3 = @"
    import ""bhl2""

    func int test() {
      var foo = new Foo
      return foo.Do()
    }
    ";


    var files = MakeFiles(new Dictionary<string, string>()
    {
      { "bhl1.bhl", bhl1 },
      { "bhl2.bhl", bhl2 },
      { "bhl3.bhl", bhl3 },
      { "interim.bhl", interim },
    });

    {
      var vm = await MakeVM(files);
      vm.LoadModule("bhl3");
      Assert.Equal(10, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }

    {
      //let's 'touch' the interim file
      System.IO.File.SetLastWriteTimeUtc(files[3], DateTime.UtcNow.AddSeconds(1));
      var executor = new CompilationExecutor();
      var vm = await MakeVM(files, use_cache: true, executor: executor);
      vm.LoadModule("bhl3");
      Assert.Equal(10, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }
  }

  [Fact]
  public async Task TestNotFoundFuncSymbolImproperIncrementalBuildErrorBug()
  {
    string file_foo = @"
      namespace root.foo {
        func Foo() {}
      }
    ";
    string file_bar = @"
    import ""/foo""

    namespace root.bar {
      func Bar() {}
    }
    ";

    string file_test = @"
    import ""/bar""

    func test() {
      root.foo.Foo()
    }
    ";

    var conf = MakeCompileConf(
      MakeFiles(new Dictionary<string, string>()
      {
        {"test.bhl", file_test},
        {"bar.bhl", file_bar},
        {"foo.bhl", file_foo},
      }),
      use_cache: false,
      max_threads: 1);

    await AssertErrorAsync<Exception>(
      async delegate() { await CompileFiles(conf); },
      "symbol 'foo' not resolved",
      new PlaceAssert(file_test, @"
      root.foo.Foo()
-----------^"
      )
    );

    conf.proj.use_cache = true;

    await AssertErrorAsync<Exception>(
      async delegate() { await CompileFiles(conf); },
      "symbol 'foo' not resolved",
      new PlaceAssert(file_test, @"
      root.foo.Foo()
-----------^"
      )
    );
  }

  [Fact]
  public async Task TestNotFoundFuncSymbolImproperIncrementalBuildErrorBug2()
  {
    string file_foo = @"
      namespace root.foo {
        func Foo() {}
      }
    ";
    string file_bar = @"
    import ""/foo""

    namespace root.foo {
      func Bar() {}
    }
    ";

    string file_test = @"
    import ""/bar""

    func test() {
      root.foo.Foo()
    }
    ";

    var conf = MakeCompileConf(
      MakeFiles(new Dictionary<string, string>()
      {
        {"test.bhl", file_test},
        {"bar.bhl", file_bar},
        {"foo.bhl", file_foo},
      }),
      use_cache: false,
      max_threads: 1);

    await AssertErrorAsync<Exception>(
      async delegate() { await CompileFiles(conf); },
      "symbol 'Foo' not resolved",
      new PlaceAssert(file_test, @"
      root.foo.Foo()
---------------^"
      )
    );

    conf.proj.use_cache = true;

    await AssertErrorAsync<Exception>(
      async delegate() { await CompileFiles(conf); },
      "symbol 'Foo' not resolved",
      new PlaceAssert(file_test, @"
      root.foo.Foo()
---------------^"
      )
    );
  }
}
