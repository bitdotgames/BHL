using System;
using System.Collections.Generic;
using System.IO;
using bhl;
using Xunit;

public class TestInit : BHL_TestBase
{
  [Fact]
  public void TestAutoCallInitFunc()
  {
    string bhl = @"
    static int foo = 0

    static func init()
    {
      foo = 10
    }

    func int test() 
    {
      return foo
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestDontCallNonLocalInitFunc()
  {
    string bhl = @"
    static int foo = 0

    func init()
    {
      foo = 10
    }

    func int test() 
    {
      return foo
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestDontCallLocalInitFuncInNamespace()
  {
    string bhl = @"
    static int foo = 0

    namespace Foo {
      static func init()
      {
        foo = 10
      }
    }

    func int test() 
    {
      return foo
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestInitFuncCantBeCoro()
  {
    string bhl = @"
    static coro func init()
    {}
    ";

    AssertError<Exception>(
      delegate() { 
        MakeVM(bhl);
      },
      "module 'init' function can't be a coroutine"
    );
  }

  [Fact]
  public void TestInitFuncCantHaveArgs()
  {
    string bhl = @"
    static func init(int a)
    {}
    ";

    AssertError<Exception>(
      delegate() { 
        MakeVM(bhl);
      },
      "module 'init' function can't have any arguments"
    );
  }

  [Fact]
  public void TestInitFuncCantHaveReturnValue()
  {
    string bhl = @"
    static func int init()
    {
      return 1;
    }
    ";

    AssertError<Exception>(
      delegate() { 
        MakeVM(bhl);
      },
      "module 'init' function must be void"
    );
  }

  [Fact]
  public void TestInitFuncFiberIsDetached()
  {
    string bhl = @"
    static func init()
    {
    }

    func test()
    {
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(0, vm.Fibers.Count);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [Fact]
  public void TestSeveralModulesInit()
  {
    string file_foo = @"
      static int FOO

      static func init() { 
        FOO = 10
      }

      func int foo() { return FOO }
    ";

    string file_bar = @"
      static int BAR

      static func init() { 
        BAR = 100
      }

      func int bar() { return BAR }
    ";

    string file_test = @"
    import ""./foo""
    import ""./bar""

    func int test() 
    {
      return foo() + bar()
    }
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"test.bhl", file_test},
        {"foo.bhl", file_foo},
        {"bar.bhl", file_bar},
      }
    );
    vm.LoadModule("test");
    Assert.Equal(110, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }
}
