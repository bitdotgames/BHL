using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using bhl;
using Xunit;

public class TestLocal : BHL_TestBase
{
  [Fact]
  public void TestLocalModuleFunc()
  {
    string bhl = @"
    static func int foo()
    {
      return 10
    }

    func int test()
    {
      return foo()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(10, Execute(vm, "test").result_old.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestLocalModuleFuncClash()
  {
    string bhl = @"
    static func int foo()
    {
      return 10
    }

    static func int foo()
    {
      return 100
    }
    ";

    AssertError<Exception>(
      delegate() { MakeVM(bhl); },
      "already defined symbol 'foo'",
      new PlaceAssert(bhl, @"
    static func int foo()
--------------------^"
      )
    );
  }

  [Fact]
  public async Task TestLocalFuncNotImported()
  {
    string file_a = @"
      static func int foo() { return 10 }

      func int bar() { return 42 }
    ";

    string file_test = @"
    import ""./a""

    func int test()
    {
      return foo()
    }
    ";

    await AssertErrorAsync<Exception>(
      async delegate()
      {
        await MakeVM(new Dictionary<string, string>()
          {
            {"test.bhl", file_test},
            {"a.bhl", file_a},
          }
        );
      },
      "symbol 'foo' not resolved",
      new PlaceAssert(file_test, @"
      return foo()
-------------^"
      )
    );
  }

  [Fact]
  public async Task TestLocalNamespaceFuncNotImported()
  {
    string file_a = @"
      namespace Foo {
        static func int foo() { return 10 }

        func int bar() { return 42 }
      }
    ";

    string file_test = @"
    import ""./a""

    func int test()
    {
      return foo()
    }
    ";

    await AssertErrorAsync<Exception>(
      async delegate()
      {
        await MakeVM(new Dictionary<string, string>()
          {
            {"test.bhl", file_test},
            {"a.bhl", file_a},
          }
        );
      },
      "symbol 'foo' not resolved",
      new PlaceAssert(file_test, @"
      return foo()
-------------^"
      )
    );
  }

  [Fact]
  public async Task TestLocalFuncDontClash()
  {
    string file_a = @"
      static func int foo() { return 10 }

      func int bar() { return foo() }
    ";

    string file_test = @"
    import ""./a""

    static func int foo() { return 20 }

    func int test()
    {
      return foo() + bar()
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"test.bhl", file_test},
        {"a.bhl", file_a},
      }
    );
    vm.LoadModule("test");
    Assert.Equal(30, Execute(vm, "test").result_old.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestLocalFuncPtrDontClash()
  {
    string file_a = @"
      static func int foo() { return 10 }
    ";

    string file_test = @"
    import ""./a""

    func int test()
    {
      func int() p = foo
      return p()
    }

    static func int foo() { return 20 }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"test.bhl", file_test},
        {"a.bhl", file_a},
      }
    );
    vm.LoadModule("test");
    Assert.Equal(20, Execute(vm, "test").result_old.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestLocalFuncDontClashInDifferentNamespaces()
  {
    string file_a = @"
      namespace Foo {
        static func int foo() { return 10 }

        func int bar() { return foo() }
      }
    ";

    string file_test = @"
    import ""./a""

    namespace Foo {
      static func int foo() { return 20 }
    }

    func int test()
    {
      return Foo.foo() + Foo.bar()
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"test.bhl", file_test},
        {"a.bhl", file_a},
      }
    );
    vm.LoadModule("test");
    Assert.Equal(30, Execute(vm, "test").result_old.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestLocalModuleFuncClashWithOtherGlobalFunc()
  {
    string file_a = @"
      func foo() { }
    ";

    string file_test = @"
    import ""./a""

    static func foo() { }
    ";

    await AssertErrorAsync<Exception>(
      async delegate()
      {
        await MakeVM(new Dictionary<string, string>()
          {
            {"test.bhl", file_test},
            {"a.bhl", file_a},
          }
        );
      },
      "already defined symbol 'foo'",
      new PlaceAssert(file_test, @"
    static func foo() { }
----^"
      )
    );
  }

  [Fact]
  public void TestLocalModuleVar()
  {
    string bhl = @"
    static int foo = 10

    func int test()
    {
      return foo
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(10, Execute(vm, "test").result_old.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestLocalModuleVarsClash()
  {
    string bhl = @"
    static int foo = 10
    static int foo = 100
    ";

    AssertError<Exception>(
      delegate() { MakeVM(bhl); },
      "already defined symbol 'foo'",
      new PlaceAssert(bhl, @"
    static int foo = 100
---------------^"
      )
    );
  }

  [Fact]
  public async Task TestLocalModuleVarNotImported()
  {
    string file_a = @"
      static int foo = 10

      int bar = 42
    ";

    string file_test = @"
    import ""./a""

    func int test()
    {
      return foo
    }
    ";

    await AssertErrorAsync<Exception>(async delegate()
      {
        await MakeVM(new Dictionary<string, string>()
          {
            {"test.bhl", file_test},
            {"a.bhl", file_a},
          }
        );
      },
      "symbol 'foo' not resolved",
      new PlaceAssert(file_test, @"
      return foo
-------------^"
      )
    );
  }

  [Fact]
  public async Task TestLocalModuleVarsDontClash()
  {
    string file_a = @"
      static int foo = 10
    ";

    string file_test = @"
    import ""./a""

    static int foo = 20

    func int test()
    {
      return foo
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string>()
      {
        {"test.bhl", file_test},
        {"a.bhl", file_a},
      }
    );
    vm.LoadModule("test");
    Assert.Equal(20, Execute(vm, "test").result_old.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public async Task TestLocalModuleVarClashWithOtherGlobalVar()
  {
    string file_a = @"
      int foo = 10
    ";

    string file_test = @"
    import ""./a""

    static int foo = 20
    ";

    await AssertErrorAsync<Exception>(
      async delegate()
      {
        await MakeVM(new Dictionary<string, string>()
          {
            {"test.bhl", file_test},
            {"a.bhl", file_a},
          }
        );
      },
      "already defined symbol 'foo'",
      new PlaceAssert(file_test, @"
    static int foo = 20
---------------^"
      )
    );
  }
}
