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
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
  }

  [Fact]
  public void TestReturnParseSpecialCasesForDeclVars()
  {
    SubTest(() => {
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
      var ts_fn = new Action<Types>((ts) => {
        BindTrace(ts, log);
      });

      var vm = MakeVM(bhl, ts_fn);
      Execute(vm, "test");
      AssertEqual("", log.ToString());
      CommonChecks(vm);
    });

    SubTest(() => {
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
      var ts_fn = new Action<Types>((ts) => {
        BindTrace(ts, log);
      });

      var vm = MakeVM(bhl, ts_fn);
      Execute(vm, "test");
      AssertEqual("", log.ToString());
      CommonChecks(vm);
    });
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

}
