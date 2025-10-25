using bhl;
using Xunit;

public class TestStrings : BHL_TestBase
{
  [Fact]
  public void TestCount()
  {
    string bhl = @"
    func int test() {
      string a = ""FooBar""
      return a.Count
    }
    ";
    var vm = MakeVM(bhl);
    Assert.Equal(6, Execute(vm, "test").result_old.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestAt()
  {
    string bhl = @"
    func string test() {
      string a = ""FooBar""
      return a.At(3)
    }
    ";
    var vm = MakeVM(bhl);
    AssertEqual("B", Execute(vm, "test").result_old.PopRelease().str);
    CommonChecks(vm);
  }

  [Fact]
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
    AssertEqual("raBooF", Execute(vm, "test").result_old.PopRelease().str);
    CommonChecks(vm);
  }

  public class TestIndexOf : BHL_TestBase
  {
    [Fact]
    public void _1()
    {
      string bhl = @"
      func int test() {
        string a = ""FooBar""
        return a.IndexOf(""Bar"")
      }
      ";
      var vm = MakeVM(bhl);
      Assert.Equal(3, Execute(vm, "test").result_old.PopRelease().num);
      CommonChecks(vm);
    }

    [Fact]
    public void _2()
    {
      string bhl = @"
      func int test() {
        string a = ""FooBar""
        return a.IndexOf(""X"")
      }
      ";
      var vm = MakeVM(bhl);
      Assert.Equal(-1, Execute(vm, "test").result_old.PopRelease().num);
      CommonChecks(vm);
    }

    [Fact]
    public void _3()
    {
      string bhl = @"
      func int test() {
        string a = ""FooBar""
        return a.IndexOf("""")
      }
      ";
      var vm = MakeVM(bhl);
      //like in C#
      Assert.Equal(0, Execute(vm, "test").result_old.PopRelease().num);
      CommonChecks(vm);
    }
  }

  [Fact]
  public void TestStringConcat()
  {
    string bhl = @"
    func string test() 
    {
      return ""Hello "" + ""world !""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("Hello world !", Execute(vm, "test").result_old.PopRelease().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStrNewLine()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\n""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\n", Execute(vm, "test").result_old.PopRelease().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStrNewLine2()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\n\n""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\n\n", Execute(vm, "test").result_old.PopRelease().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStrNewLineEscape()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\\n""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\\n", Execute(vm, "test").result_old.PopRelease().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStrNewLineEscape2()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\\n\n""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\\n\n", Execute(vm, "test").result_old.PopRelease().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStrNewLineEscape3()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\\n\\n""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\\n\\n", Execute(vm, "test").result_old.PopRelease().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStrTab()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\t""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\t", Execute(vm, "test").result_old.PopRelease().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStrTab2()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\t\t""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\t\t", Execute(vm, "test").result_old.PopRelease().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStrTabEscape()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\\t""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\\t", Execute(vm, "test").result_old.PopRelease().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStrTabEscape2()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\\t\t""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\\t\t", Execute(vm, "test").result_old.PopRelease().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStrTabEscape3()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\\t\\t""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\\t\\t", Execute(vm, "test").result_old.PopRelease().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStrQuoteEscape()
  {
    string bhl = "func string test() { return \"My name is \\\"Bar\\\", hello\" }";

    var vm = MakeVM(bhl);
    AssertEqual("My name is \"Bar\", hello", Execute(vm, "test").result_old.PopRelease().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStrConcat()
  {
    string bhl = @"
    func string test(int k) 
    {
      return (string)k + (string)(k*2)
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test", ValOld.NewNum(vm, 3)).result_old.PopRelease().str;
    AssertEqual(res, "36");
    CommonChecks(vm);
  }
}