using System;           
using System.IO;
using System.Collections.Generic;
using System.Text;
using bhl;

public class TestStd : BHL_TestBase
{
  [IsTested()]
  public void TestSimpleIO()
  {
    string bhl = @"
    import ""std""  
    func test() {
      std.io.WriteLine(""Hello!"")
    }
    ";

    var vm = MakeVM(bhl);
    Execute(vm, "test");
    CommonChecks(vm);
  }
}
