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
        "can't determine the type",
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
        "can't determine the type",
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
---------------^"
       )
      );
  }
}
