using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using bhl;
using Xunit;

public class TestGlobals : BHL_TestBase
{
  [Fact]
  public void TestSimpleGlobalVariableDecl()
  {
    string bhl = @"
    float foo

    func float test()
    {
      return foo
    }
    ";

    var c = Compile(bhl);

    var ts = new Types();

    var expected =
        new ModuleCompiler()
          .UseInit()
          .EmitChain(Opcodes.DeclVar, new int[] { 0, TypeIdx(c, ts.T("float")) })
          .EmitChain(Opcodes.MakeRef, new int[] { 0 })
          .UseCode()
          .EmitChain(Opcodes.Frame, new int[] { 0, 1 })
          .EmitChain(Opcodes.GetGVar, new int[] { 0 })
          .EmitChain(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    Assert.Equal(0, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGlobalVariableWriteRead()
  {
    string bhl = @"
    float foo = 10

    func float test()
    {
      foo = 20
      return foo
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseInit()
          .EmitChain(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitChain(Opcodes.MakeRef, new int[] { 0 })
          .EmitChain(Opcodes.SetGVar, new int[] { 0 })
          .UseCode()
          .EmitChain(Opcodes.Frame, new int[] { 0, 1 })
          .EmitChain(Opcodes.Constant, new int[] { ConstIdx(c, 20) })
          .EmitChain(Opcodes.SetGVar, new int[] { 0 })
          .EmitChain(Opcodes.GetGVar, new int[] { 0 })
          .EmitChain(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    Assert.Equal(20, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGlobalVariableAssignConst()
  {
    string bhl = @"
    float foo = 10

    func float test()
    {
      return foo
    }
    ";

    var c = Compile(bhl);

    var expected =
        new ModuleCompiler()
          .UseInit()
          .EmitChain(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
          .EmitChain(Opcodes.MakeRef, new int[] { 0 })
          .EmitChain(Opcodes.SetGVar, new int[] { 0 })
          .UseCode()
          .EmitChain(Opcodes.Frame, new int[] { 0, 1 })
          .EmitChain(Opcodes.GetGVar, new int[] { 0 })
          .EmitChain(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    Assert.Equal(10, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGlobalVariableAssignNegativeNumber()
  {
    string bhl = @"
    float foo = -10

    func float test()
    {
      return foo
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(-10, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestFindDeclaredClassGlobalVariable()
  {
    string bhl = @"
    class Foo {
      float b
    }

    Foo foo
    ";

    var vm = MakeVM(bhl);
    Assert.False(vm.TryFindVarAddr("bar", out var _));
    Assert.True(vm.TryFindVarAddr("foo", out var addr));
    Assert.Equal("Foo", addr.val_ref.val.type.GetName());
    CommonChecks(vm);
  }

  [Fact]
  public void TestFindAncChangeGlobalVariable()
  {
    string bhl = @"
    class Foo {
      float b
    }

    Foo foo = { b : 1 }

    func float test()
    {
      return foo.b
    }
    ";

    var vm = MakeVM(bhl);

    Assert.True(vm.TryFindVarAddr("foo", out var addr));
    Assert.Equal("Foo", addr.val_ref.val.type.GetName());
    ((ValList)addr.val_ref.val.obj)[0] = 42;

    Assert.Equal(42, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGlobalVariableAssignAndReadObject()
  {
    string bhl = @"

    class Foo {
      float b
    }

    Foo foo = {b : 100}

    func float test()
    {
      return foo.b
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(100, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGlobalVariableAssignAndReadArray()
  {
    string bhl = @"
    class Foo {
      float b
    }

    []Foo foos = [{b : 100}, {b: 200}]

    func float test()
    {
      return foos[1].b
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(200, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGlobalVariableWrite()
  {
    string bhl = @"
    class Foo {
      float b
    }

    Foo foo = {b : 100}

    func float bar()
    {
      return foo.b
    }

    func float test()
    {
      foo.b = 101
      return bar()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(101, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGlobalVariableAlreadyDeclared()
  {
    string bhl = @"

    int foo = 0
    int foo = 1

    func int test()
    {
      return foo
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      @"already defined symbol 'foo'",
      new PlaceAssert(bhl, @"
    int foo = 1
--------^"
      )
    );
  }

  [Fact]
  public void TestGlobalVariableSelfAssignError()
  {
    string bhl = @"

    int foo = foo
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      @"symbol 'foo' not resolved",
      new PlaceAssert(bhl, @"
    int foo = foo
--------------^"
      )
    );
  }

  [Fact]
  public void TestGlobalVariableFuncCallsNotAllowed()
  {
    {
      string bhl = @"
      func int make()
      {
        return 10
      }

      int foo = make()
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        @"function calls not allowed in global context",
        new PlaceAssert(bhl, @"
      int foo = make()
----------------^"
        )
      );
    }

    {
      string bhl = @"

      class Foo {
        func int make()
        {
          return 10
        }
      }

      int foo = (new Foo).make()
      ";

      AssertError<Exception>(
        delegate() { Compile(bhl); },
        @"function calls not allowed in global context",
        new PlaceAssert(bhl, @"
      int foo = (new Foo).make()
--------------------------^"
        )
      );
    }
  }

  [Fact]
  public void TestLocalVariableHasPriorityOverGlobalOne()
  {
    string bhl = @"

    class Foo {
      float b
    }

    Foo foo = {b : 100}

    func float test()
    {
      Foo foo = {b : 200}
      return foo.b
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(200, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestGlobalVarInLambda()
  {
    string bhl = @"

    class Foo {
      float b
    }

    Foo foo = {b : 100}

    func float test()
    {
      float a = 1
      return func float() {
        return foo.b + a
      }()
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(101, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  public class TestSetGlobalFromOutside : BHL_TestBase
  {
    [Fact]
    public void _1()
    {
      string bhl = @"
      int foo = 10
      ";

      var vm = MakeVM(bhl);
      Assert.False(vm.TryFindVarAddr("hey", out var addr));
    }

    [Fact]
    public void _2()
    {
      string bhl = @"
      namespace what {
        int hey = 10
      }
      ";

      var vm = MakeVM(bhl);
      Assert.False(vm.TryFindVarAddr("hey", out var addr));
    }

    [Fact]
    public void _3()
    {
      string bhl = @"
      int bar = 1
      int foo = 10

      func int test()
      {
        return foo
      }
      ";

      var vm = MakeVM(bhl);
      Assert.True(vm.TryFindVarAddr("foo", out var addr));
      addr.val_ref.val.num = 100;
      Assert.Equal(100, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }

    [Fact]
    public void _4()
    {
      string bhl = @"
      int bar = 1
      namespace what {
        int foo = 10
      }

      func int test()
      {
        return what.foo
      }
      ";

      var vm = MakeVM(bhl);
      Assert.True(vm.TryFindVarAddr("what.foo", out var addr));
      Assert.Equal(10, addr.val_ref.val.num);
      addr.val_ref.val.num = 100;
      Assert.Equal(100, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }

    [Fact]
    public async Task _5()
    {
      string bar_bhl = @"
      int bar = 1
      ";

      string test_bhl = @"
      import ""bar""

      func int test() {
        return bar
      }
      ";

      var vm = await MakeVM(new Dictionary<string, string>()
        {
          {"bar.bhl", bar_bhl},
          {"test.bhl", test_bhl},
        }
      );
      Assert.False(vm.TryFindVarAddr("bar", out var _));

      vm.LoadModule("bar");
      Assert.True(vm.TryFindVarAddr("bar", out var addr));
      Assert.Equal(1, addr.val_ref.val.num);
      addr.val_ref.val.num = 100;

      vm.LoadModule("test");
      Assert.Equal(100, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }

    [Fact]
    public async Task _6()
    {
      string bar_bhl = @"
      namespace N {
        int bar = 1
      }
      ";

      string test_bhl = @"
      import ""bar""

      namespace N {
        int foo = 2
      }

      func int test() {
        return N.bar
      }
      ";

      var vm = await MakeVM(new Dictionary<string, string>()
        {
          {"test.bhl", test_bhl},
          {"bar.bhl", bar_bhl},
        }
      );
      Assert.False(vm.TryFindVarAddr("N.bar", out var _));

      vm.LoadModule("test");
      Assert.True(vm.TryFindVarAddr("N.bar", out var addr));
      Assert.Equal(1, addr.val_ref.val.num);
      addr.val_ref.val.num = 100;

      vm.LoadModule("test");
      Assert.Equal(100, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }
  }

}
