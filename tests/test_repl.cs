using bhl;
using Xunit;

public class TestRepl : BHL_TestBase
{
  ReplSession MakeRepl(string prelude = "")
  {
    var vm = prelude.Length > 0
      ? MakeVM(Compile(prelude))
      : new VM(new Types());
    return new ReplSession(vm);
  }

  [Fact]
  public void TestEvalExpression()
  {
    var repl = MakeRepl();

    var result = repl.Eval("1 + 2");

    Assert.Single(result);
    Assert.Equal(3, (int)result[0].num);
  }

  [Fact]
  public void TestDefineFuncAndCall()
  {
    var repl = MakeRepl();

    repl.Eval("func int add(int a, int b) { return a + b }");
    var result = repl.Eval("add(10, 7)");

    Assert.Single(result);
    Assert.Equal(17, (int)result[0].num);
  }

  [Fact]
  public void TestAccumulateDeclarations()
  {
    var repl = MakeRepl();

    repl.Eval("func int square(int x) { return x * x }");
    repl.Eval("func int sum_of_squares(int a, int b) { return square(a) + square(b) }");
    var result = repl.Eval("sum_of_squares(3, 4)");

    Assert.Single(result);
    Assert.Equal(25, (int)result[0].num);
  }

  [Fact]
  public void TestDefineClass()
  {
    var repl = MakeRepl();

    repl.Eval("class Point { int x int y }");
    repl.Eval(@"func int manhattan() {
      Point p = new Point
      p.x = 3
      p.y = 4
      return p.x + p.y
    }");
    var result = repl.Eval("manhattan()");

    Assert.Single(result);
    Assert.Equal(7, (int)result[0].num);
  }

  [Fact]
  public void TestDeclarationReturnsEmpty()
  {
    var repl = MakeRepl();

    var result = repl.Eval("func void noop() {}");

    Assert.Empty(result);
  }

  [Fact]
  public void TestClassInstantiation()
  {
    var repl = MakeRepl();

    repl.Eval("class Point { int x int y }");
    // `var p = new Point` is a statement, not an expression; should execute silently
    var result = repl.Eval("var p = new Point");

    Assert.Empty(result);
  }

  [Fact]
  public void TestStatementContextPersists()
  {
    var repl = MakeRepl();

    repl.Eval("class Point { int x int y }");
    repl.Eval("var p = new Point");
    repl.Eval("p.x = 10");
    var result = repl.Eval("p.x");

    Assert.Single(result);
    Assert.Equal(10, (int)result[0].num);
  }

  [Fact]
  public void TestPreloadedModuleVisible()
  {
    var repl = MakeRepl(@"
func int base_val() { return 100 }
");

    var result = repl.Eval("base_val()");

    Assert.Single(result);
    Assert.Equal(100, (int)result[0].num);
  }
}
