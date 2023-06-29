using System;           
using System.IO;
using System.Collections.Generic;
using System.Text;
using bhl;

public class TestStrings : BHL_TestBase
{
  [IsTested()]
  public void TestLength()
  {
    string bhl = @"
    func int test() {
      string a = ""FooBar""
      return a.Length
    }
    ";
    var vm = MakeVM(bhl);
    AssertEqual(6, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }
}
