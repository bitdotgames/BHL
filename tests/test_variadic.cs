using System;           
using System.IO;
using System.Text;
using System.Collections.Generic;
using bhl;

public class TestVariadic : BHL_TestBase
{
  [IsTested()]
  public void TestSimpleUsage()
  {
    string bhl = @"
    func int sum(...[]int ns) {
      int s = 0
      foreach(int n in ns) {
        s += n
      }
      return s
    }

    func int test() {
      return sum(1, 2, 3)
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(6, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }
}
