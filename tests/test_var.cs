using System;           
using System.IO;
using System.Text;
using System.Collections.Generic;
using bhl;

public class TestVar : BHL_TestBase
{
  [IsTested()]
  public void TestSimpleCase()
  {
    string bhl = @"
    func int test() {
      var a = 10
      return a
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestMultiAssignment()
  {
    string bhl = @"
    func int,int fetch() {
      return 10, 20
    }
    func int test() {
      var a, var b = fetch()
      return b + a
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(30, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestMultiAssignmentMixed()
  {
    string bhl = @"
    func int,int fetch() {
      return 10, 20
    }
    func int test() {
      var a, int b = fetch()
      return b + a
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(30, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestFuncResult()
  {
    string bhl = @"

    func []int make() {
      return [1, 3, 4]
    }

    func int test() {
      var ns = make()
      return ns[1]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(3, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForeachArray()
  {
    string bhl = @"
    func int test() {
      []int ns = [1, 2, 3]
      int summ = 0
      foreach(var n in ns) {
        summ += n
      }
      return summ
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(6, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForeachNotArray()
  {
    string bhl = @"
    func test() {
      foreach(var n in 1) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "expression is not of array type",
      new PlaceAssert(bhl, @"
      foreach(var n in 1) {
-----------------------^"
       )
     );
  }

  [IsTested()]
  public void TestForeachNonTypedJson()
  {
    string bhl = @"
    func test() {
      foreach(var n in [1,2,3]) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "can't determine type of [..] expression",
      new PlaceAssert(bhl, @"
      foreach(var n in [1,2,3]) {
-----------------------^"
      )
    );
  }

  [IsTested()]
  public void TestForeachMap()
  {
    SubTest("both var", () => {
      string bhl = @"
      func int test() {
        [int]string m = [[1, ""a""], [2, ""b""], [3, ""c""]]
        int summ = 0
        foreach(var k,var v in m) {
          summ += k
          trace(v)
        }
        return summ
      }
      ";

      var log = new StringBuilder();

      var ts_fn = new Func<Types>(() => {
        var ts = new Types();
        BindTrace(ts, log);
        return ts;
      });

      var vm = MakeVM(bhl, ts_fn);
      AssertEqual(6, Execute(vm, "test").result.PopRelease().num);
      AssertEqual("abc", log.ToString());
      CommonChecks(vm);
    });

    SubTest("only key", () => {
      string bhl = @"
      func int test() {
        [int]string m = [[1, ""a""], [2, ""b""], [3, ""c""]]
        int summ = 0
        foreach(var k,string v in m) {
          summ += k
          trace(v)
        }
        return summ
      }
      ";

      var log = new StringBuilder();
      
      var ts_fn = new Func<Types>(() => {
        var ts = new Types();
        BindTrace(ts, log);
        return ts;
      });

      var vm = MakeVM(bhl, ts_fn);
      AssertEqual(6, Execute(vm, "test").result.PopRelease().num);
      AssertEqual("abc", log.ToString());
      CommonChecks(vm);
    });

    SubTest("only value", () => {
      string bhl = @"
      func int test() {
        [int]string m = [[1, ""a""], [2, ""b""], [3, ""c""]]
        int summ = 0
        foreach(int k,var v in m) {
          summ += k
          trace(v)
        }
        return summ
      }
      ";

      var log = new StringBuilder();

      var ts_fn = new Func<Types>(() => {
        var ts = new Types();
        BindTrace(ts, log);
        return ts;
      });

      var vm = MakeVM(bhl, ts_fn);
      AssertEqual(6, Execute(vm, "test").result.PopRelease().num);
      AssertEqual("abc", log.ToString());
      CommonChecks(vm);
    });
  }

  [IsTested()]
  public void TestForeachMapNonTypedJson()
  {
    string bhl = @"
    func test() {
      foreach(var k,var v in [[1, ""a""], [2, ""b""]]) {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "can't determine type of [..] expression",
      new PlaceAssert(bhl, @"
      foreach(var k,var v in [[1, ""a""], [2, ""b""]]) {
-----------------------------^"
      )
    );
  }

  [IsTested()]
  public void TestNamespaceClass()
  {
    string bhl = @"
    namespace foo {
      namespace bar {
        class Bar {
          int num
          string str
        }
      }
    }
    func int test() {
      var bar = new foo.bar.Bar
      bar.num = 14
      return bar.num
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(14, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNamespaceClassJsonInit()
  {
    string bhl = @"
    namespace foo {
      namespace bar {
        class Bar {
          int num
          string str
        }
      }
    }
    func int test() {
      var bar = new foo.bar.Bar{num: 14}
      return bar.num
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(14, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNullIsNotAllowed()
  {
    string bhl = @"
    func test() {
      var bar = null
    }
    ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "invalid usage context",
        new PlaceAssert(bhl, @"
      var bar = null
------^"
       )
      );
  }

  [IsTested()]
  public void TestVarWithoutAssignExpressionIsNotAllowed()
  {
    string bhl = @"
    func test() {
      var a
    }
    ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "invalid usage context",
        new PlaceAssert(bhl, @"
      var a
------^"
       )
      );
  }

  [IsTested()]
  public void TestVarIsAlreadyDefined()
  {
    string bhl = @"
    class var {
    }
    ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "already defined symbol 'var'",
        new PlaceAssert(bhl, @"
    class var {
----^"
       )
      );
  }

  [IsTested()]
  public void TestIcompatibleTypes()
  {
    string bhl = @"
      func bool test() {
        var a = 1
        var s = ""foo""
        return a == s
      }
    ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "incompatible types: 'int' and 'string'",
        new PlaceAssert(bhl, @"
        return a == s
--------------------^"
       )
      );
  }

  [IsTested()]
  public void TestGlobal()
  {
    string bhl = @"
    var a = 10
    func int test() {
      return a
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "invalid usage context",
      new PlaceAssert(bhl, @"
    var a = 10
----^"
     )
    );
  }

  [IsTested()]
  public void TestFuncSignatureMemberNotAllowed()
  {
    {
      string bhl = @"
        func var test() {
          return null
        }
      ";

        AssertError<Exception>(
          delegate() { 
            Compile(bhl);
          },
          "invalid usage context",
          new PlaceAssert(bhl, @"
        func var test() {
-------------^"
         )
        );
    }

    {
      string bhl = @"
        func int test(int a, var b) {
          return 1
        }
      ";

        AssertError<Exception>(
          delegate() { 
            Compile(bhl);
          },
          "invalid usage context",
          new PlaceAssert(bhl, @"
        func int test(int a, var b) {
-----------------------------^"
         )
        );
     }
  }

  [IsTested()]
  public void TestVarArrayNotAllowed()
  {
    string bhl = @"
      func test() {
        []var ns = []
      }
    ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "invalid usage context",
        new PlaceAssert(bhl, @"
        []var ns = []
----------^"
       )
      );
  }

  [IsTested()]
  public void TestVarTypeMemberNotAllowed()
  {
    string bhl = @"
      class Foo {
        var foo
      }
    ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "invalid usage context",
        new PlaceAssert(bhl, @"
        var foo
--------^"
       )
      );
  }
}
