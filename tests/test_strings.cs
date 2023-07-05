using System;           
using System.IO;
using System.Collections.Generic;
using System.Text;
using bhl;

public class TestStrings : BHL_TestBase
{
  [IsTested()]
  public void TestCount()
  {
    string bhl = @"
    func int test() {
      string a = ""FooBar""
      return a.Count
    }
    ";
    var vm = MakeVM(bhl);
    AssertEqual(6, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestAt()
  {
    string bhl = @"
    func string test() {
      string a = ""FooBar""
      return a.At(3)
    }
    ";
    var vm = MakeVM(bhl);
    AssertEqual("B", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTraverseString()
  {
    string bhl = @"
    func string test() {
      string a = ""FooBar""
      string b = """"
      for(int i=a.Count-1;i>=0;i--) {
        b += a.At(i)
      }
      return b
    }
    ";
    var vm = MakeVM(bhl);
    AssertEqual("raBooF", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIndexOf()
  {
    SubTest(() => {
      string bhl = @"
      func int test() {
        string a = ""FooBar""
        return a.IndexOf(""Bar"")
      }
      ";
      var vm = MakeVM(bhl);
      AssertEqual(3, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    });

    SubTest(() => {
      string bhl = @"
      func int test() {
        string a = ""FooBar""
        return a.IndexOf(""X"")
      }
      ";
      var vm = MakeVM(bhl);
      AssertEqual(-1, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    });

    SubTest(() => {
      string bhl = @"
      func int test() {
        string a = ""FooBar""
        return a.IndexOf("""")
      }
      ";
      var vm = MakeVM(bhl);
      //like in C#
      AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    });
  }

}
