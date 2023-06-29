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
}
