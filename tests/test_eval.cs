using bhl;
using Xunit;

public class TestEval : BHL_TestBase
{
  [Fact]
  public void TestArithmetic()
  {
    var vm = MakeVM(Compile(""));

    var result = vm.EvalExpression("1 + 2");

    Assert.Single(result);
    Assert.Equal(3, (int)result[0].num);
  }

  [Fact]
  public void TestFuncCall()
  {
    string bhl = @"
func int add(int a, int b) {
  return a + b
}
";
    var vm = MakeVM(Compile(bhl));

    var result = vm.EvalExpression("add(10, 7)");

    Assert.Single(result);
    Assert.Equal(17, (int)result[0].num);
  }

  [Fact]
  public void TestVoidFunc()
  {
    string bhl = @"
func void noop() {}
";
    var vm = MakeVM(Compile(bhl));

    var result = vm.EvalExpression("noop()");

    Assert.Empty(result);
  }

  [Fact]
  public void TestTupleFunc()
  {
    string bhl = @"
func int,string pair() {
  return 42, ""hello""
}
";
    var vm = MakeVM(Compile(bhl));

    var result = vm.EvalExpression("pair()");

    Assert.Equal(2, result.Length);
    Assert.Equal("hello", result[0].str);
    Assert.Equal(42, (int)result[1].num);
  }
}
