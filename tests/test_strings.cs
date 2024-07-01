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

  [IsTested()]
  public void TestStringConcat()
  {
    string bhl = @"
    func string test() 
    {
      return ""Hello "" + ""world !""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("Hello world !", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrNewLine()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\n""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\n", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrNewLine2()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\n\n""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\n\n", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrNewLineEscape()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\\n""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\\n", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrNewLineEscape2()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\\n\n""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\\n\n", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrNewLineEscape3()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\\n\\n""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\\n\\n", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrTab()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\t""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\t", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrTab2()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\t\t""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\t\t", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrTabEscape()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\\t""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\\t", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrTabEscape2()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\\t\t""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\\t\t", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrTabEscape3()
  {
    string bhl = @"
    func string test() 
    {
      return ""bar\\t\\t""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("bar\\t\\t", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestStrQuoteEscape()
  {
    string bhl = "func string test() { return \"My name is \\\"Bar\\\", hello\" }";

    var vm = MakeVM(bhl);
    AssertEqual("My name is \"Bar\", hello", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }
  
  [IsTested()]
  public void TestStrConcat()
  {
    string bhl = @"
    func string test(int k) 
    {
      return (string)k + (string)(k*2)
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test", Val.NewNum(vm, 3)).result.PopRelease().str;
    AssertEqual(res, "36");
    CommonChecks(vm);
  }
}
