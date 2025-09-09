using System;
using System.IO;
using Xunit;

public class TestStd : BHL_TestBase
{
  [Fact]
  public void TestSimpleIO()
  {
    string bhl = @"
    import ""std/io""  
    func test() {
      std.io.WriteLine(""Hello!"")
    }
    ";

    var vm = MakeVM(bhl);
    var w = new StringWriter();
    var std_out = Console.Out;
    Console.SetOut(w);

    Execute(vm, "test");
    Console.SetOut(std_out);

    AssertEqual("Hello!", w.ToString().Trim('\r', '\n'));
    CommonChecks(vm);
  }
}