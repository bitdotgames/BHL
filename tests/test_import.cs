using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using bhl;
using bhl.marshall;

public class TestImport : BHL_TestBase
{
  [IsTested()]
  public void TestBadImport()
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

    AssertError<Exception>(
      delegate() { 
        CompileFiles(files);
      },
     "invalid import",
      new PlaceAssert(bhl1, @"
    import ""garbage""  
----^"
      )
    );
  }

  [IsTested()]
  public void TestImportEnum()
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );

    vm.LoadModule("bhl1");
    AssertEqual(30, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportEnumConflict()
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

    AssertError<Exception>(
      delegate() { 
        MakeVM(new Dictionary<string, string>() {
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

  [IsTested()]
  public void TestImportReadWriteGlobalVar()
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );

    vm.LoadModule("bhl1");
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportReadWriteSeveralGlobalVars()
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );

    vm.LoadModule("bhl1");
    AssertEqual(12, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportGlobalObjectVar()
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      }
    );

    vm.LoadModule("bhl1");
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportGlobalObjectVarWithCycles()
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"main.bhl", main_bhl},
        {"g.bhl", g_bhl},
        {"input.bhl", input_bhl},
        {"ability.bhl", ability_bhl},
        {"unit.bhl", unit_bhl},
      }
    );

    vm.LoadModule("main");
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportGlobalVarConflict()
  {
    string bhl1 = @"
    import ""bhl2""  

    int foo = 10

    func test() { }
    ";

    string bhl2 = @"
    float foo = 100
    ";

    AssertError<Exception>(
      delegate() { 
        MakeVM(new Dictionary<string, string>() {
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

  [IsTested()]
  public void TestImportMixed()
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      }
    );

    vm.LoadModule("bhl1");
    AssertEqual(4, Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportWithCycles()
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      }
    );

    vm.LoadModule("bhl1");
    AssertEqual(23, Execute(vm, "test", Val.NewNum(vm, 23)).result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportWithSemicolon()
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      }
    );

    vm.LoadModule("bhl1");
    AssertEqual(23, Execute(vm, "test", Val.NewNum(vm, 23)).result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportConflict()
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

    AssertError<Exception>(
      delegate() { 
        MakeVM(new Dictionary<string, string>() {
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

  [IsTested()]
  public void TestImportInvalidateCachesAfterChange()
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
      var ts = new Types();
      var loader = new ModuleLoader(ts, CompileFiles(files, ts, use_cache: true));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      AssertEqual(Execute(vm, "test").result.PopRelease().num, 23);
    }

    string new_file_unit = @"
      class Unit {
        string new_field
        int test
      }
      Unit u = {test: 32}
    ";
    NewTestFile("unit.bhl", new_file_unit, ref files, replace: true);
    System.IO.File.SetLastWriteTimeUtc(files[files.Count-1], DateTime.UtcNow.AddSeconds(1));

    {
      var ts = new Types();
      var loader = new ModuleLoader(ts, CompileFiles(files, ts, use_cache: true));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      AssertEqual(Execute(vm, "test").result.PopRelease().num, 32);
    }
  }

  [IsTested()]
  public void TestIncrementalBuildOfChangedFiles()
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
      var ts = new Types();
      var exec = new CompilationExecutor();
      var conf = MakeCompileConf(files, ts, use_cache: true, max_threads: 3);
      var loader = new ModuleLoader(ts, CompileFiles(exec, conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      AssertEqual(Execute(vm, "test").result.PopRelease().num, 23);
      AssertEqual(exec.parse_cache_hits, 0);
      AssertEqual(exec.parse_cache_miss, 3);
      AssertEqual(exec.compile_cache_hits, 0);
      AssertEqual(exec.compile_cache_miss, 3);
    }

    string new_file_unit = @"
      class Unit {
        string new_field
        int test
      }
      Unit u = {test: 32}
    ";
    NewTestFile("unit.bhl", new_file_unit, ref files, replace: true);
    System.IO.File.SetLastWriteTimeUtc(files[files.Count-1], DateTime.UtcNow.AddSeconds(1));

    {
      var ts = new Types();
      var exec = new CompilationExecutor();
      var conf = MakeCompileConf(files, ts, use_cache: true, max_threads: 3);
      var loader = new ModuleLoader(ts, CompileFiles(exec, conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      AssertEqual(Execute(vm, "test").result.PopRelease().num, 32);
      AssertEqual(exec.parse_cache_hits, 1);
      AssertEqual(exec.parse_cache_miss, 1+1);
      AssertEqual(exec.compile_cache_hits, 1);
      AssertEqual(exec.compile_cache_miss, 1+1);
    }
  }

  [IsTested()]
  public void TestIncrementalBuildOfChangedFilesWithIntermediateFile()
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
      var ts = new Types();
      var exec = new CompilationExecutor();
      var conf = MakeCompileConf(files, ts, use_cache: true, max_threads: 3);
      var loader = new ModuleLoader(ts, CompileFiles(exec, conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      AssertEqual(Execute(vm, "test").result.PopRelease().num, 23);
      AssertEqual(exec.parse_cache_hits, 0);
      AssertEqual(exec.parse_cache_miss, 3);
      AssertEqual(exec.compile_cache_hits, 0);
      AssertEqual(exec.compile_cache_miss, 3);
    }

    string new_file_get = @"
    import ""unit""

    func Unit get() { 
      Unit u = {test: 32}
      return u
    }
    ";
    NewTestFile("get.bhl", new_file_get, ref files, replace: true);
    System.IO.File.SetLastWriteTimeUtc(files[files.Count-1], DateTime.UtcNow.AddSeconds(1));

    {
      var ts = new Types();
      var exec = new CompilationExecutor();
      var conf = MakeCompileConf(files, ts, use_cache: true, max_threads: 3);
      var loader = new ModuleLoader(ts, CompileFiles(exec, conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      AssertEqual(Execute(vm, "test").result.PopRelease().num, 32);
      AssertEqual(exec.parse_cache_hits, 1);
      AssertEqual(exec.parse_cache_miss, 1+1);
      AssertEqual(exec.compile_cache_hits, 1);
      AssertEqual(exec.compile_cache_miss, 1+1);
    }
  }

  [IsTested()]
  public void TestIncrementalBuildOfChangedFilesWithIntermediateFile2()
  {
    string file_unit = @"
      class Unit {
        int test
      }
    ";

    string file_get = @"
    import ""/collections/unit""

    Unit global_unit

    func Unit get() { 
      Unit u = {test: 23}
      return u
    }
    ";

    string file_test = @"
    import ""/try/get""

    func int test() {
      return get().test
    }
    ";

    CleanTestDir();

    var files = new List<string>();
    NewTestFile("collections/unit.bhl", file_unit, ref files);
    NewTestFile("try/get.bhl", file_get, ref files);
    NewTestFile("test.bhl", file_test, ref files);

    {
      var ts = new Types();
      var exec = new CompilationExecutor();
      var conf = MakeCompileConf(files, ts, use_cache: true, max_threads: 3);
      var loader = new ModuleLoader(ts, CompileFiles(exec, conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      AssertEqual(Execute(vm, "test").result.PopRelease().num, 23);
    }

    string new_file_test = @"
    import ""/try/get""

    namespace foo {
      func int get_add() {
        return get().test + 1
      }
    }

    func int test() {
      return foo.get_add()
    }
    ";
    NewTestFile("test.bhl", new_file_test, ref files, replace: true);
    System.IO.File.SetLastWriteTimeUtc(files[files.Count-1], DateTime.UtcNow.AddSeconds(1));

    {
      var ts = new Types();
      var exec = new CompilationExecutor();
      var conf = MakeCompileConf(files, ts, use_cache: true, max_threads: 3);
      var loader = new ModuleLoader(ts, CompileFiles(exec, conf));
      var vm = new VM(ts, loader);
      vm.LoadModule("test");
      AssertEqual(Execute(vm, "test").result.PopRelease().num, 24);
      AssertEqual(exec.parse_cache_hits, 2);
      AssertEqual(exec.parse_cache_miss, 1);
      AssertEqual(exec.compile_cache_hits, 2);
      AssertEqual(exec.compile_cache_miss, 1);
    }
  }
}
