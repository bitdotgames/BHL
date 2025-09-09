using System;
using System.Text;
using System.Collections.Generic;
using bhl;
using Xunit;

public class TestParal : BHL_TestBase
{
  [Fact]
  public void TestBasicParal()
  {
    string bhl = @"
    coro func int test()
    {
      int a
      paral {
        {
          yield suspend() 
        }
        {
          yield()
          a = 1
        }
      }
      return a
    }
    ";

    var c = Compile(bhl);

    var ts = new Types();

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/ })
          .EmitThen(Opcodes.DeclVar, new int[] { 0, TypeIdx(c, ts.T("int")) })
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.PARAL, 30})
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 8})
          .EmitThen(Opcodes.CallGlobNative, new int[] { ts.module.nfunc_index.IndexOf("suspend"), 0 })
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 14})
          .EmitThen(Opcodes.CallGlobNative, new int[] { ts.module.nfunc_index.IndexOf(Prelude.YieldFunc), 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    Assert.Equal(1, fb.result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestBasicParalAutoSeqWrap()
  {
    string bhl = @"
    coro func int test()
    {
      int a
      paral {
        yield suspend() 
        {
          yield()
          a = 1
        }
        yield suspend() 
      }
      return a
    }
    ";

    var c = Compile(bhl);

    var ts = new Types();

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/ })
          .EmitThen(Opcodes.DeclVar, new int[] { 0, TypeIdx(c, ts.T("int")) })
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.PARAL, 42})
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 8})
          .EmitThen(Opcodes.CallGlobNative, new int[] { ts.module.nfunc_index.IndexOf("suspend"), 0})
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 14})
          .EmitThen(Opcodes.CallGlobNative, new int[] { ts.module.nfunc_index.IndexOf(Prelude.YieldFunc), 0 })
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 8})
          .EmitThen(Opcodes.CallGlobNative, new int[] { ts.module.nfunc_index.IndexOf("suspend"), 0})
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    Assert.Equal(1, fb.result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSubParalAutoSeqWrap()
  {
    string bhl = @"
    coro func int test()
    {
      int a
      paral {
        yield suspend() 
        paral {
          yield()
          a = 1
        }
        yield suspend() 
      }
      return a
    }
    ";

    var c = Compile(bhl);

    var ts = new Types();

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/ })
          .EmitThen(Opcodes.DeclVar, new int[] { 0, TypeIdx(c, ts.T("int")) })
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.PARAL, 54})
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 8})
          .EmitThen(Opcodes.CallGlobNative, new int[] { ts.module.nfunc_index.IndexOf("suspend"), 0})
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 26})
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.PARAL, 22})
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 8})
          .EmitThen(Opcodes.CallGlobNative, new int[] { ts.module.nfunc_index.IndexOf(Prelude.YieldFunc), 0 })
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 6})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 8})
          .EmitThen(Opcodes.CallGlobNative, new int[] { ts.module.nfunc_index.IndexOf("suspend"), 0})
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(1, fb.result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSubParalAutoSeqWrapLambda()
  {
    string bhl = @"
    coro func int test()
    {
      int a
      paral {
        yield suspend() 
        yield coro func() {
          paral {
            yield()
            a = 1
          }
        } ()
        yield suspend() 
      }
      return a
    }
    ";

    var c = Compile(bhl);

    var ts = new Types();

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/ })
          .EmitThen(Opcodes.DeclVar, new int[] { 0, TypeIdx(c, ts.T("int")) })
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.PARAL, 74})
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 8})
          .EmitThen(Opcodes.CallGlobNative, new int[] { ts.module.nfunc_index.IndexOf("suspend"), 0})
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 46})
          .EmitThen(Opcodes.Lambda, new int[] { 29 })
          .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/ })
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.PARAL, 22})
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 8})
          .EmitThen(Opcodes.CallGlobNative, new int[] { ts.module.nfunc_index.IndexOf(Prelude.YieldFunc), 0 })
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 6})
          .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 1) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.Return)
          .EmitThen(Opcodes.UseUpval, new int[] { 0, 0, 0 })
          .EmitThen(Opcodes.LastArgToTop, new int[] { 0 })
          .EmitThen(Opcodes.CallFuncPtr, new int[] { 0 })
          .EmitThen(Opcodes.Block, new int[] { (int)BlockType.SEQ, 8})
          .EmitThen(Opcodes.CallGlobNative, new int[] { ts.module.nfunc_index.IndexOf("suspend"), 0})
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(1, fb.result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalBreak()
  {
    string bhl = @"

    coro func int test() 
    {
      int n = 0
      while(true) {
        paral {
          {
            n = 1
            break
            yield suspend()
          }
          {
            n = 2
            yield suspend()
          }
        }
      }
      return n
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    Assert.Equal(1, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalAllBreak()
  {
    string bhl = @"

    coro func int test() 
    {
      int n = 0
      while(true) {
        paral_all {
          {
            n = 1
            break
            yield suspend()
          }
          {
            n = 2
            yield suspend()
          }
        }
      }
      return n
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    Assert.Equal(1, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalReuse()
  {
    string bhl = @"

    coro func int test() 
    {
      int n = 0
      paral {
        {
          yield suspend()
          n = 10
        }
        {
          n = 1
        }
      }

      paral {
        {
          n = 2
        }
        {
          yield suspend()
          n = 3
        }
      }
      return n
    }
    ";

    var vm = MakeVM(bhl);
    var num = Execute(vm, "test").result.PopRelease().num;
    Assert.Equal(2, num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalWithSubFuncs()
  {
    string bhl = @"
    coro func foo() {
      yield suspend()
    }

    coro func int bar() {
      yield()
      return 1
    }

    coro func int test() {
      int a
      paral {
        {
          yield foo()
        }
        {
          a = yield bar()
        }
      }
      return a
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    Assert.Equal(1, fb.result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalWithSubFuncsAndAutoSeqWrap()
  {
    string bhl = @"
    coro func foo() {
      yield suspend()
    }

    coro func int bar() {
      yield()
      return 1
    }

    coro func int test() {
      int a
      paral {
        yield foo()
        a = yield bar()
      }
      return a
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    Assert.Equal(1, fb.result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalForComplexStatement()
  {
    string bhl = @"

    func int foo(int a)
    {
      return a
    }

    func float test() 
    {
      Color c = new Color
      int a = 0
      float s = 0
      paral {
        a = foo(10)
        c.r = 142
        s = c.mult_summ(a)
      }
      return a
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    var vm = MakeVM(bhl, ts_fn);
    Assert.Equal(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalAllRunning()
  {
    string bhl = @"
    coro func int test()
    {
      int a
      paral_all {
        {
          yield suspend() 
        }
        {
          yield()
          a = 1
        }
      }
      return a
    }
    ";

    var vm = MakeVM(bhl);
    vm.Start("test");
    for(int i = 0; i < 99; i++)
      Assert.True(vm.Tick());
  }

  [Fact]
  public void TestParalAllFinished()
  {
    string bhl = @"
    coro func int test()
    {
      int a
      paral_all {
        {
          yield()
          yield()
        }
        {
          yield()
          a = 1
        }
      }
      return a
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());
    Assert.Equal(1, fb.result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalAllForComplexStatement()
  {
    string bhl = @"

    func int foo(int a)
    {
      return a
    }

    func float test() 
    {
      Color c = new Color
      int a = 0
      float s = 0
      paral_all {
        c.r = 142
        s = c.mult_summ(a)
        a = foo(10)
      }
      return a+c.r
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    var vm = MakeVM(bhl, ts_fn);
    Assert.Equal(152, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalAllForNestedSeqs()
  {
    string bhl = @"

    func int foo(int a)
    {
      paral {
        return a
      }
      return 0
    }

    coro func int test() 
    {
      int i = 0
      paral_all {
        {
          yield()
          if(i == 1) {
            i = foo(2)
          }
        }
        {
          if(i == 0) {
            i = foo(1)
          }
          yield()
        }
      }
      return i
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    var vm = MakeVM(bhl, ts_fn);
    Assert.Equal(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalFailure()
  {
    string bhl = @"
    coro func foo()
    {
      yield()
      yield()
      yield()
      trace(""A"")
    }

    coro func bar()
    {
      yield()
      yield()
      fail()
      trace(""B"")
    }

    coro func test() 
    {
      paral {
        yield bar()
        yield foo()
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindFail(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());

    AssertEqual("", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalAllFailure()
  {
    string bhl = @"
    coro func foo()
    {
      yield()
      yield()
      yield()
      trace(""A"")
    }

    coro func bar()
    {
      yield()
      yield()
      fail()
      trace(""B"")
    }

    coro func test() 
    {
      paral_all {
        yield bar()
        yield foo()
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      BindTrace(ts, log);
      BindFail(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    Assert.True(vm.Tick());
    Assert.True(vm.Tick());
    Assert.False(vm.Tick());

    AssertEqual("", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalReturn()
  {
    string bhl = @"

    coro func int test() 
    {
      paral {
        {
          return 1
        }
        yield suspend()
      }
      return 0
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalReturnFromSubFunc()
  {
    string bhl = @"

    func int foo() {
      paral {
        return 1
      }
      return 0
    }

    func int test() 
    {
      paral {
        return foo()
      }
      return 0
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalNestedReturnFromSubFuncWithSeq()
  {
    string bhl = @"

    func int foo() {
      {
        defer { }
        return 1
      }
      return 0
    }

    func int test() 
    {
      paral {
        paral {
          return foo()
        }
      }
      return 0
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalNestedReturnFromSubFuncWithParalAllSeq()
  {
    string bhl = @"

    func int foo() {
      paral_all {
        {
          {
            defer { }
            return 1
          }
        }
      }
      return 0
    }

    func int test() 
    {
      paral {
        paral {
          return foo()
        }
      }
      return 0
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalNestedReturn()
  {
    string bhl = @"

    coro func int test() 
    {
      paral {
        {
          paral {
            yield suspend()
            {
              return 1
            }
          }
        }
        yield suspend()
      }
      return 0
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalAllReturn()
  {
    string bhl = @"

    coro func int test() 
    {
      paral_all {
        {
          return 1
        }
        yield suspend()
      }
      return 0
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalAllYieldReturn()
  {
    string bhl = @"

    coro func int test() 
    {
      paral_all {
        yield()
        return 1
      }
      return 0
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalAllNestedReturn()
  {
    string bhl = @"

    coro func int test() 
    {
      paral_all {
        {
          paral_all {
            yield suspend()
            {
              return 1
            }
          }
        }
        yield suspend()
      }
      return 0
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestParalAllNestedParalInFunc()
  {
    string bhl = @"
    func bar() {
      trace(""bar"")
    }

    func foo() {
      paral {
        bar()
      }
    }

    func test() {
      paral_all {
        foo()
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual("bar", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestEmptyParalIsNotAllowed()
  {
    string bhl = @"
    func test() {
      paral {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      @"empty paral blocks are not allowed",
      new PlaceAssert(bhl, @"
      paral {
------^"
      )
    );
  }

  [Fact]
  public void TestEmptyParalAllIsNotAllowed()
  {
    string bhl = @"
    func test() {
      paral_all {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      @"empty paral blocks are not allowed",
      new PlaceAssert(bhl, @"
      paral_all {
------^"
      )
    );
  }
}