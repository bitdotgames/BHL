using System;
using System.Text;
using System.Collections.Generic;
using bhl;
using Xunit;

public class TestPreproc : BHL_TestBase
{
  [Fact]
  public void TestIfMissingDefine()
  {
    string bhl = @"
    func bool test() 
    {
#if FOO
      return false
#endif
      return true
    }
    ";

    var vm = MakeVM(bhl);
    Assert.True(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestIfDefineExists()
  {
    string bhl = @"
    func bool test() 
    {
#if FOO
      return false
#endif
      return true
    }
    ";

    var vm = MakeVM(bhl, defines: new HashSet<string>() {"FOO"});
    Assert.False(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestIfNotMissingDefine()
  {
    string bhl = @"
    func bool test() 
    {
#if !FOO
      return false
#endif
      return true
    }
    ";

    var vm = MakeVM(bhl);
    Assert.False(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestIfNotDefineExists()
  {
    string bhl = @"
    func bool test() 
    {
#if !FOO
      return false
#endif
      return true
    }
    ";

    var vm = MakeVM(bhl, defines: new HashSet<string>() {"FOO"});
    Assert.True(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestElseMissingDefine()
  {
    string bhl = @"
    func int test() 
    {
#if FOO
      return 1
#else 
      return 2
#endif
      return 3
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestElseDefineExists()
  {
    string bhl = @"
    func int test() 
    {
#if FOO
      return 1
#else 
      return 2
#endif
      return 3
    }
    ";

    var vm = MakeVM(bhl, defines: new HashSet<string>() {"FOO"});
    Assert.Equal(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestDanglingEndif()
  {
    string bhl = @"
    func test() 
    {
  #endif
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      @"invalid usage",
      new PlaceAssert(bhl, @"
  #endif
---^"
      )
    );
  }

  [Fact]
  public void TestDoubleElse()
  {
    string bhl = @"
    func test() 
    {
  #if FOO
  #else
  #else
  #endif
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      @"invalid usage",
      new PlaceAssert(bhl, @"
  #else
---^"
      )
    );
  }

  [Fact]
  public void TestNotClosedIf()
  {
    string bhl = @"
    func test() 
    {
  #if FOO
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      @"invalid usage",
      new PlaceAssert(bhl, @"
  #if FOO
---^"
      )
    );
  }

  [Fact]
  public void TestWeirdIf()
  {
    string bhl = @"
    func test() 
    {
  #if
  #endif
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      @"mismatched input",
      new PlaceAssert(bhl, @"
  #if
-----^"
      )
    );
  }

  [Fact]
  public void TestErrorReportingFromParser()
  {
    string bhl = @"
    func bool test() 
    {
      return
#if FOO
      10
#endif
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      @"return value is missing",
      new PlaceAssert(bhl, @"
      return
------^"
      )
    );
  }

  [Fact]
  public void TestErrorReportingFromParser2()
  {
    string bhl = @"
    func bool test() 
    {
#if !FOO
      return 10
#endif
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      @"incompatible types: 'bool' and 'int'",
      new PlaceAssert(bhl, @"
      return 10
-------------^"
      )
    );
  }

  [Fact]
  public void TestErrorReportingPreserveLines()
  {
    string bhl = @"
    func test() 
    {
#if SERVER
      some junk
      some junk
      some junk
#endif
      int a = 10

      string f = a
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      @"incompatible types: 'string' and 'int'",
      new PlaceAssert(bhl, @"
      string f = a
-------------^"
      )
    );
  }

  [Fact]
  public void TestUtf8SymbolsCopiedProperly()
  {
    string bhl = @"
func test()
{
  //кек
  #if SERVER
  
  #endif
}
";

    var vm = MakeVM(bhl);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [Fact]
  public void TestUtf8SymbolsRemovedProperly()
  {
    string bhl = @"
func test()
{
  #if SERVER
  //你好
  
  #endif
}
";

    var vm = MakeVM(bhl);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [Fact]
  public void TestCommentedPreprocessorDirective()
  {
    string bhl = @"
func bool test()
{
  //#if SERVER
  return false
  //#endif
  return true
}
";

    var vm = MakeVM(bhl);
    Assert.False(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [Fact]
  public void TestSharpSymbolInComments()
  {
    string bhl = @"
func bool test()
{
  //TODO: move this to C#?
  return true
}
";

    var vm = MakeVM(bhl);
    Assert.True(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }
}