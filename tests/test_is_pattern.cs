using System;
using bhl;
using Xunit;

// 'b is Foo f' declares f and assigns the cast result, so it reads like C#'s pattern form.
// It lowers to 'f = b as Foo' followed by testing f: a failed 'as' leaves null, and null is
// not a Foo, so no definite-assignment analysis is needed and f is usable immediately.
public class TestIsPattern : BHL_TestBase
{
  const string TYPES = @"
    class Base { int b }
    class Foo : Base { int x }
    class Bar : Base { int y }
    interface IThing { func int get() }
    class Thing : Base, IThing {
      func int get() { return 7 }
    }
  ";

  [Fact]
  public void TestBindsOnMatchAndNotOtherwise()
  {
    var vm = MakeVM(TYPES + @"
    func int test(Base b) {
      if(b is Foo f) {
        return f.x
      }
      return -1
    }

    func int run(bool as_foo) {
      if(as_foo) {
        Foo f = new Foo
        f.x = 42
        return test(f)
      }
      return test(new Bar)
    }
    ");

    double res = Execute(vm, "run", Val.NewBool(true)).Stack.Pop();
    Assert.Equal(42, res);
    res = Execute(vm, "run", Val.NewBool(false)).Stack.Pop();
    Assert.Equal(-1, res);
    CommonChecks(vm);
  }

  // the whole point: usable in the rest of the condition, which '&&' makes safe
  [Fact]
  public void TestUsableInTheRestOfTheCondition()
  {
    var vm = MakeVM(TYPES + @"
    func int test(Base b) {
      if(b is Foo f && f.x > 10) {
        return f.x
      }
      return -1
    }

    func int run(int x) {
      Foo f = new Foo
      f.x = x
      return test(f)
    }
    ");

    double res = Execute(vm, "run", Val.NewInt(42)).Stack.Pop();
    Assert.Equal(42, res);
    res = Execute(vm, "run", Val.NewInt(5)).Stack.Pop();
    Assert.Equal(-1, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestWorksForInterfaces()
  {
    var vm = MakeVM(TYPES + @"
    func int test(Base b) {
      if(b is IThing t) {
        return t.get()
      }
      return -1
    }

    func int run(bool thing) {
      return thing ? test(new Thing) : test(new Foo)
    }
    ");

    double res = Execute(vm, "run", Val.NewBool(true)).Stack.Pop();
    Assert.Equal(7, res);
    res = Execute(vm, "run", Val.NewBool(false)).Stack.Pop();
    Assert.Equal(-1, res);
    CommonChecks(vm);
  }

  // matching against a base type must not pin the value to it - the override still wins
  [Fact]
  public void TestKeepsTheRuntimeTypeWhenMatchingABase()
  {
    var vm = MakeVM(@"
    class Base {
      virtual func int get() { return 1 }
    }
    class Child : Base {
      override func int get() { return 2 }
    }

    func int test(Base b) {
      if(b is Base x) {
        return x.get()
      }
      return -1
    }

    func int run() { return test(new Child) }
    ");

    double res = Execute(vm, "run").Stack.Pop();
    Assert.Equal(2, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestWorksInWhile()
  {
    var vm = MakeVM(TYPES + @"
    func int test(Base b) {
      int n = 0
      while(b is Foo f && f.x > 0) {
        n = n + f.x
        f.x = f.x - 1
      }
      return n
    }

    func int run() {
      Foo f = new Foo
      f.x = 4
      return test(f)
    }
    ");

    double res = Execute(vm, "run").Stack.Pop();
    Assert.Equal(10, res);
    CommonChecks(vm);
  }

  // a name only binds when it sits on the same line as the type, otherwise the next
  // statement would be swallowed as one - newlines are on the hidden channel
  [Fact]
  public void TestNextLineIsNotTakenAsThePatternName()
  {
    var vm = MakeVM(TYPES + @"
    func int test(Base b) {
      bool ok = b is Foo
      int n = ok ? 1 : 0
      return n
    }

    func int run() { return test(new Bar) }
    ");

    double res = Execute(vm, "run").Stack.Pop();
    Assert.Equal(0, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestScalarCantBeMatched()
  {
    string bhl = @"
    func test(any a) {
      if(a is int i) { }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "can't be matched into a variable",
      new PlaceAssert(bhl, @"
      if(a is int i) { }
------------------^")
    );
  }

  [Fact]
  public void TestNameAlreadyDeclared()
  {
    string bhl = TYPES + @"
    func test(Base b) {
      int f = 1
      if(b is Foo f) { }
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "already defined symbol 'f'",
      new PlaceAssert(bhl, @"
      if(b is Foo f) { }
------------------^")
    );
  }
}
