using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using bhl;
using Xunit;

public class TestParsing : BHL_TestBase
{
  [Fact]
  public void TestUtf8Comments()
  {
    string bhl = @"
    //надеюсь тут нет ошибки?
    func int test()
    {
      //привет всем!
      return 10
    //а тут?
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(10, Execute(vm, "test").result.PopRelease().num);
  }

  public class TestReturnParseSpecialCasesForDeclVars : BHL_TestBase
  {
    [Fact]
    public void _1()
    {
      string bhl = @"
      coro func test()
      {
        yield()
        return
        string str
        trace(""NOPE"")
      }
      ";

      var log = new StringBuilder();
      var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

      var vm = MakeVM(bhl, ts_fn);
      Execute(vm, "test");
      AssertEqual("", log.ToString());
      CommonChecks(vm);
    }

    [Fact]
    public void _2()
    {
      string bhl = @"
      coro func test()
      {
        yield()
        return
        float b = 3
        trace(""NOPE"")
      }
      ";

      var log = new StringBuilder();
      var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

      var vm = MakeVM(bhl, ts_fn);
      Execute(vm, "test");
      AssertEqual("", log.ToString());
      CommonChecks(vm);
    }
  }

  [Fact]
  public void TestParseMapTypeAmbuiguityWithArrAccess()
  {
    string bhl = @"
    func test()
    {
      int foo = 1
      float a = 1/foo

      [int]string m = []
    }
    ";

    Compile(bhl);
  }

  [Fact]
  public void TestCommentInEmptyArray()
  {
    string bhl = @"
    func test()
    {
      []int foo = [/*kek*/]
    }
    ";

    Compile(bhl);
  }

  [Fact]
  public void TestSpaceInEmptyArray()
  {
    string bhl = @"
    func test()
    {
      []int foo = [ ]
    }
    ";

    Compile(bhl);
  }
}
