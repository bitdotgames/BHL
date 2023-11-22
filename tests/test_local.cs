using System;
using System.Collections.Generic;
using System.IO;
using bhl;

public class TestLocal : BHL_TestBase
{
  [IsTested()]
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
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 10);
    CommonChecks(vm);
  }

  [IsTested()]
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
      delegate() { 
        MakeVM(bhl);
      },
      "already defined symbol 'foo'",
      new PlaceAssert(bhl, @"
    static func int foo()
--------------------^"
      )
    );
  }

  [IsTested()]
  public void TestLocalFuncNotImported()
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

    AssertError<Exception>(
      delegate() { 
        MakeVM(new Dictionary<string, string>() {
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

  [IsTested()]
  public void TestLocalNamespaceFuncNotImported()
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

    AssertError<Exception>(
      delegate() { 
        MakeVM(new Dictionary<string, string>() {
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

  [IsTested()]
  public void TestLocalFuncDontClash()
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"test.bhl", file_test},
        {"a.bhl", file_a},
      }
    );
    vm.LoadModule("test");
    AssertEqual(30, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLocalFuncDontClashInDifferentNamespaces()
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"test.bhl", file_test},
        {"a.bhl", file_a},
      }
    );
    vm.LoadModule("test");
    AssertEqual(30, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(Execute(vm, "test").result.PopRelease().num, 10);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestLocalModuleVarsClash()
  {
    string bhl = @"
    static int foo = 10
    static int foo = 100
    ";

    AssertError<Exception>(
      delegate() { 
        MakeVM(bhl);
      },
      "already defined symbol 'foo'",
      new PlaceAssert(bhl, @"
    static int foo = 100
---------------^"
      )
    );
  }

  [IsTested()]
  public void TestLocalModuleVarNotImported()
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

    AssertError<Exception>(
      delegate() { 
        MakeVM(new Dictionary<string, string>() {
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

  [IsTested()]
  public void TestLocalModuleVarsDontClash()
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"test.bhl", file_test},
        {"a.bhl", file_a},
      }
    );
    vm.LoadModule("test");
    AssertEqual(20, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

}
