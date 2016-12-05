using System;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using Antlr4.Runtime;
using bhl;

public class IsTestedAttribute : Attribute
{
  public override string ToString()
  {
    return "Is Tested";
  }
}

public class BHL_Test
{
  [IsTested()]
  public void TestReturnNum()
  {
    string bhl = @"
      
    func float test() 
    {
      return 100
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(num, 100);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnStr()
  {
    string bhl = @"
      
    func string test() 
    {
      return ""bar""
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var str = ExtractStr(intp.ExecNode(node));

    AssertEqual(str, "bar");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnBoolTrue()
  {
    string bhl = @"
      
    func bool test() 
    {
      return true
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnBoolFalse()
  {
    string bhl = @"
      
    func bool test() 
    {
      return false
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnDefaultVar()
  {
    string bhl = @"
      
    func float test() 
    {
      float k
      return k
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestVarDecl()
  {
    string bhl = @"
      
    func float test() 
    {
      float k = 42
      return k
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestVarSelfDecl()
  {
    string bhl = @"
      
    func float test() 
    {
      float k = k
      return k
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      "Symbol not resolved"
    );
  }

  [IsTested()]
  public void TestAssign()
  {
    string bhl = @"
      
    func float test() 
    {
      float k
      k = 42
      float r
      r = k 
      return r
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestSeveralReturns()
  {
    string bhl = @"
      
    func float test() 
    {
      return 300
      float k = 1
      return 100
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 300);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassValue()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      return k
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 3);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassValueSeveralTimes()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      return k
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");

    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(intp.ExecNode(node));
    AssertEqual(num, 3);
    CommonChecks(intp);

    node.SetArgs(DynVal.NewNum(42));
    num = ExtractNum(intp.ExecNode(node));
    AssertEqual(num, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestBitAnd()
  {
    string bhl = @"
      
    func int test(int k) 
    {
      return k & 1
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestBitOr()
  {
    string bhl = @"
      
    func int test(int k) 
    {
      return k | 4
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 7);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestAdd()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      return k + 1
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 4);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestSub()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      return k - 1
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestMult()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      return k * 2
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 6);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDiv()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      return k / 2
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(4));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestGT()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k > 2
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(4));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNotGT()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k > 5
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(4));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLT()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k < 20
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(4));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNotLT()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k < 2
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(4));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestGTE()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k >= 2
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(4));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestGTE2()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k >= 2
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(2));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestEqNumber()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k == 2
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(2));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestEqString()
  {
    string bhl = @"
      
    func bool test(string k) 
    {
      return k == ""b""
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewStr("b"));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNotEqNum()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k == 2
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(20));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNotEqString()
  {
    string bhl = @"
      
    func bool test(string k) 
    {
      return k != ""c""
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewStr("b"));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNotEqString2()
  {
    string bhl = @"
      
    func bool test(string k) 
    {
      return k == ""c""
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewStr("b"));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLTE()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k <= 21
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(20));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLTE2()
  {
    string bhl = @"
      
    func bool test(float k) 
    {
      return k <= 20
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(20));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPass2Values()
  {
    string bhl = @"
      
    func float test(float k, float m) 
    {
      return m
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3), DynVal.NewNum(7));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 7);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPass2Values2()
  {
    string bhl = @"
      
    func float test(float k, float m) 
    {
      return k
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3), DynVal.NewNum(7));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 3);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestSubCall()
  {
    string bhl = @"
      
    func float foo(float k)
    {
      return k
    }

    func float test(float k) 
    {
      return foo(k)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 3);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassNamedValue()
  {
    string bhl = @"
      
    func float foo(float a)
    {
      return a
    }

    func float test(float k) 
    {
      return foo(a : k)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 3);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassSeveralNamedValues()
  {
    string bhl = @"
      
    func float foo(float a, float b)
    {
      return b - a
    }

    func float test() 
    {
      return foo(b : 5, a : 3)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncDefaultArg()
  {
    string bhl = @"

    func float foo(float k = 42)
    {
      return k
    }
      
    func float test() 
    {
      return foo()
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncNotEnoughArgs()
  {
    string bhl = @"

    func bool foo(bool k)
    {
      return k
    }
      
    func bool test() 
    {
      return foo()
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      "Missing argument 'k'"
    );
  }

  [IsTested()]
  public void TestFuncNotEnoughArgsWithDefaultArgs()
  {
    string bhl = @"

    func bool foo(float radius, bool k = true)
    {
      return k
    }
      
    func bool test() 
    {
      return foo()
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      "Missing argument 'radius'"
    );
  }

  [IsTested()]
  public void TestFuncDefaultArgIgnored()
  {
    string bhl = @"

    func float foo(float k = 42)
    {
      return k
    }
      
    func float test() 
    {
      return foo(24)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 24);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncDefaultArgMixed()
  {
    string bhl = @"

    func float foo(float b, float k = 42)
    {
      return b + k
    }
      
    func float test() 
    {
      return foo(24)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 66);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncDefaultArgIsFunc()
  {
    string bhl = @"

    func float bar(float m)
    {
      return m
    }

    func float foo(float b, float k = bar(1))
    {
      return b + k
    }
      
    func float test() 
    {
      return foo(24)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 25);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncDefaultArgIsFunc2()
  {
    string bhl = @"

    func float bar(float m)
    {
      return m
    }

    func float foo(float b = 1, float k = bar(1))
    {
      return b + k
    }
      
    func float test() 
    {
      return foo(26, bar(2))
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 28);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncMissingDefaultArgument()
  {
    string bhl = @"

    func float foo(float b = 23, float k)
    {
      return b + k
    }
      
    func float test() 
    {
      return foo(24)
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      "missing default argument expression"
    );
  }
  
  [IsTested()]
  public void TestFuncExtraArgumentMatchesLocalVariable()
  {
    string bhl = @"

    func void foo(float b, float k)
    {
      float f = 3
      float a = b + k
    }
      
    func void test() 
    {
      foo(b: 24, k: 3, f : 1)
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      "f: No such named argument"
    );
  }

  [IsTested()]
  public void TestFuncSeveralDefaultArgsMixed()
  {
    string bhl = @"

    func float foo(float b = 100, float k = 1000)
    {
      return k - b
    }
      
    func float test() 
    {
      return foo(k : 2, b : 5)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, -3);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncSeveralDefaultArgsOmittingSome()
  {
    string bhl = @"

    func float foo(float b = 100, float k = 1000)
    {
      return k - b
    }
      
    func float test() 
    {
      return foo(k : 2)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, -98);
    CommonChecks(intp);
  }

  //TODO: this is not supported yet
  //[IsTested()]
  public void TestFuncDefaultArgCall()
  {
    string bhl = @"

    func float test(float k = 42) 
    {
      return k
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassByValue()
  {
    string bhl = @"
      
    func void foo(float a)
    {
      a = a + 1
    }

    func float test() 
    {
      float k = 1
      foo(k)
      return k
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassByRef()
  {
    string bhl = @"

    func foo(ref float a) 
    {
      a = a + 1
    }
      
    func float test(float k) 
    {
      foo(ref k)
      return k
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 4);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassByRefNested()
  {
    string bhl = @"

    func bar(ref float b)
    {
      b = b * 2
    }

    func foo(ref float a) 
    {
      a = a + 1
      bar(ref a)
    }
      
    func float test(float k) 
    {
      foo(ref k)
      return k
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 8);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassByRefMixed()
  {
    string bhl = @"

    func foo(ref float a, float b) 
    {
      a = a + b
    }
      
    func float test(float k) 
    {
      foo(ref k, k)
      return k
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 6);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassByRefNamedArg()
  {
    string bhl = @"

    func foo(ref float a, float b) 
    {
      a = a + b
    }
      
    func float test(float k) 
    {
      foo(a : ref k, b: k)
      return k
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 6);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassByRefReturn()
  {
    string bhl = @"

    func float foo(ref float a) 
    {
      a = a + 1
      return a
    }
      
    func float test(float k) 
    {
      return foo(ref k)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 4);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefsAllowedInFuncArgsOnly()
  {
    string bhl = @"

    func void test() 
    {
      ref float a
    }
    ";

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl);
      },
      "ref is only allowed in function declaration"
    );
  }

  [IsTested()]
  public void TestRefsDefaultArgsNotAllowed()
  {
    string bhl = @"

    func void foo(ref float k = 10)
    {
    }
    ";

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl);
      },
      "ref is not allowed to have a default value"
    );
  }

  [IsTested()]
  public void TestSimpleExpression()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      return ((k*100) + 100) / 400
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLocalVariables()
  {
    string bhl = @"
      
    func float foo(float k)
    {
      float b = 5
      return k + 5
    }

    func float test(float k) 
    {
      float b = 10
      return k * foo(b)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 45);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCastFloatToStr()
  {
    string bhl = @"
      
    func string test(float k) 
    {
      return (string)k
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");

    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractStr(intp.ExecNode(node));

    AssertEqual(res, "3");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCastIntToStr()
  {
    string bhl = @"
      
    func string test(int k) 
    {
      return (string)k
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");

    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractStr(intp.ExecNode(node));

    AssertEqual(res, "3");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCastFloatToInt()
  {
    string bhl = @"

    func int test(float k) 
    {
      return (int)k
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");

    node.SetArgs(DynVal.NewNum(3.9));
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 3);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCastIntToAny()
  {
    string bhl = @"
      
    func any test() 
    {
      return (any)121
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 121);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCastIntToAny2()
  {
    string bhl = @"

    func int foo(any a)
    {
      return (int)a
    }
      
    func int test() 
    {
      return foo(121)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 121);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCastStrToAny()
  {
    string bhl = @"
      
    func any test() 
    {
      return (any)""foo""
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractStr(intp.ExecNode(node));

    AssertEqual(res, "foo");
    CommonChecks(intp);
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

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);

    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractStr(intp.ExecNode(node));

    AssertEqual(res, "36");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestImplicitIntArgsCast()
  {
    string bhl = @"

    func float foo(float a, float b)
    {
      return a + b
    }
      
    func float test() 
    {
      return foo(1, 2.0)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 3);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestImplicitIntArgsCastBindFunc()
  {
    string bhl = @"

    func float bar(float a)
    {
      return a
    }
      
    func float test() 
    {
      return bar(a : min(1, 0.3))
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindMin(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 0.3f);
    CommonChecks(intp);
  }

  public class RetValNode : BehaviorTreeTerminalNode
  {
    public override void init(object agent)
    {
      var interp = Interpreter.instance;
      var k = interp.PopValue().ValueClone();
      interp.PushValue(k);
    }
  }

  [IsTested()]
  public void TestBindFunction()
  {
    string bhl = @"
      
    func float test(int k) 
    {
      return ret_val(k)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new FuncBindSymbol("ret_val", globs.type("float"),
          delegate() { return new RetValNode(); } );
      fn.define(new FuncArgSymbol("k", globs.type("float")));

      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    node.SetArgs(DynVal.NewNum(42));
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestBindFunctionWithDefaultArgs()
  {
    string bhl = @"
      
    func float test(int k) 
    {
      return func_with_def(k)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new SimpleFuncBindSymbol("func_with_def", globs.type("float"), 
          delegate(object agent)
          {
            var interp = Interpreter.instance;
            var b = interp.GetFuncArgsNum() > 1 ? interp.PopValue().num : 2;
            var a = interp.PopValue().num;

            interp.PushValue(DynVal.NewNum(a + b));

            return BHS.SUCCESS;
          },
          1);
      fn.define(new FuncArgSymbol("a", globs.type("float")));
      fn.define(new FuncArgSymbol("b", globs.type("float")));

      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    node.SetArgs(DynVal.NewNum(42));
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 44);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestBindFunctionWithDefaultArgs2()
  {
    string bhl = @"

    func float foo(float a)
    {
      return a
    }
      
    func float test(int k) 
    {
      return func_with_def(k, foo(k)+1)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new SimpleFuncBindSymbol("func_with_def", globs.type("float"), 
          delegate(object agent)
          {
            var interp = Interpreter.instance;
            var b = interp.GetFuncArgsNum() > 1 ? interp.PopValue().num : 2;
            var a = interp.PopValue().num;

            interp.PushValue(DynVal.NewNum(a + b));

            return BHS.SUCCESS;
          },
          1);
      fn.define(new FuncArgSymbol("a", globs.type("float")));
      fn.define(new FuncArgSymbol("b", globs.type("float")));

      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    node.SetArgs(DynVal.NewNum(42));
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 42+43);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestBindFunctionWithDefaultArgs3()
  {
    string bhl = @"

    func float test() 
    {
      return func_with_def()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new SimpleFuncBindSymbol("func_with_def", globs.type("float"), 
          delegate(object agent)
          {
            var interp = Interpreter.instance;
            var a = interp.GetFuncArgsNum() > 1 ? interp.PopValue().num : 14;

            interp.PushValue(DynVal.NewNum(a));

            return BHS.SUCCESS;
          },
          1);
      fn.define(new FuncArgSymbol("a", globs.type("float")));

      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 14);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnFailureFirst()
  {
    string bhl = @"

    func float foo()
    {
      FAILURE()
      return 100
    }
      
    func float test() 
    {
      float val = foo()
      return val
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = intp.ExecNode(node, false);

    AssertEqual(res.status, BHS.FAILURE);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnFailureFirstFromBindFunction()
  {
    string bhl = @"

    func float test() 
    {
      float val = foo()
      return val
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new SimpleFuncBindSymbol("foo", globs.type("float"),
          delegate(object agent) { return BHS.FAILURE; } );
      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var res = intp.ExecNode(node, false);

    AssertEqual(res.status, BHS.FAILURE);
    CommonChecks(intp);
  }

  public class Color
  {
    public float r;
    public float g;
  }

  public class ColorNested
  {
    public Color c = new Color();
  }

  public class MkColorNode : BehaviorTreeTerminalNode
  {
    public override void init(object agent)
    {
      var interp = Interpreter.instance;

      var r = interp.PopValue().num;
      var c = new Color();
      c.r = (float)r;
      var dv = DynVal.NewObj(c);

      interp.PushValue(dv);
    }
  }

  void BindColor(GlobalScope globs)
  {
    {
      var cl = new ClassBindSymbol("Color",
        delegate(ref DynVal v) 
        { 
          v.obj = new Color();
        }
      );
      globs.define(cl);
      globs.define(new ArrayTypeSymbolT<Color>(globs, cl));

      cl.define(new FieldSymbol("r", globs.type("float"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var c = (Color)ctx.obj;
          v.SetNum(c.r);
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var c = (Color)ctx.obj;
          c.r = (float)v.num; 
          ctx.obj = c;
        }
      ));
      cl.define(new FieldSymbol("g", globs.type("float"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var c = (Color)ctx.obj;
          v.SetNum(c.g);
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var c = (Color)ctx.obj;
          c.g = (float)v.num; 
          ctx.obj = c;
        }
      ));

      {
        var m = new SimpleFuncBindSymbol("Add", globs.type("Color"),
          delegate(object agent)
          {
            var interp = Interpreter.instance;

            var k = (float)interp.PopValue().num;
            var c = (Color)interp.PopValue().obj;

            var newc = new Color();
            newc.r = c.r + k;
            newc.g = c.g + k;

            var dv = DynVal.NewObj(newc);
            interp.PushValue(dv);

            return BHS.SUCCESS;
          }
        );
        m.define(new FuncArgSymbol("k", globs.type("float")));

        cl.define(m);
      }

      {
        var m = new SimpleFuncBindSymbol("mult_summ", globs.type("float"),
          delegate(object agent)
          {
            var interp = Interpreter.instance;

            var k = interp.PopValue().num;
            var c = (Color)interp.PopValue().obj;

            interp.PushValue(DynVal.NewNum((c.r * k) + (c.g * k)));

            return BHS.SUCCESS;
          }
        );
        m.define(new FuncArgSymbol("k", globs.type("float")));

        cl.define(m);
      }
    }

    {
      var fn = new FuncBindSymbol("mkcolor", globs.type("Color"),
          delegate() { return new MkColorNode(); }
      );
      fn.define(new FuncArgSymbol("r", globs.type("float")));

      globs.define(fn);
    }
  }

  public class RefC : DynValRefcounted
  {
    public int refs;
    public Stream stream;

    public RefC(Stream stream)
    {
      this.stream = stream;
    }

    public void RefInc()
    {
      var sw = new StreamWriter(stream);
      ++refs;
      sw.Write("INC" + refs + ";");
      sw.Flush();
    }

    public void RefDec(bool can_release = true)
    {
      var sw = new StreamWriter(stream);
      --refs;
      sw.Write("DEC" + refs + ";");
      sw.Flush();
      if(can_release)
        RefTryRelease();
    }

    public bool RefTryRelease()
    {
      var sw = new StreamWriter(stream);
      sw.Write("REL" + refs + ";");
      sw.Flush();
      return refs == 0;
    }
  }

  public class ConfigNodeRefC
  {
    public RefC r = null;

    public void reset()
    {
      r = null;
    }
  }

  public class NodeRefC : BehaviorTreeTerminalNode
  {
    Stream sm;
    public ConfigNodeRefC conf;

    public NodeRefC(Stream sm)
    {
      this.sm = sm;
    }

    public override void init(object agent)
    {
      var sw = new StreamWriter(sm);
      sw.Write("NODE!");
      sw.Flush();
    }
  }

  void BindRefC(GlobalScope globs, MemoryStream mstream)
  {
    {
      var cl = new ClassBindSymbol("RefC",
        delegate(ref DynVal v) 
        { 
          v.obj = new RefC(mstream);
        }
      );
      {
        var vs = new bhl.FieldSymbol("refs", globs.type("int"),
          delegate(bhl.DynVal ctx, ref bhl.DynVal v)
          {
            v.num = ((RefC)ctx.obj).refs;
          },
          //read only property
          null
        );
        cl.define(vs);
      }
      globs.define(cl);
      globs.define(new ArrayTypeSymbol(globs, cl));
    }

    {
      var cl = new ClassBindSymbol( "ConfigNodeRefC",
        delegate(ref DynVal v) 
        { 
          v.obj = new ConfigNodeRefC();
        }
      );
      globs.define(cl);

      cl.define(new FieldSymbol("r", globs.type("RefC"),
          delegate(DynVal ctx, ref DynVal v) {
            var c = (ConfigNodeRefC)ctx.obj;
            v.obj = c.r;
            var sw = new StreamWriter(mstream);
            sw.Write("READ!");
            sw.Flush();
          },
          delegate(ref DynVal ctx, DynVal v) {
            var c = (ConfigNodeRefC)ctx.obj;
            c.r = (RefC)v.obj;
            ctx.obj = c;
            var sw = new StreamWriter(mstream);
            sw.Write("WRITE!");
            sw.Flush();
          }
     ));
    }

    {
      var fn = new ConfNodeSymbol("NodeRefC", globs.type("void"),
          delegate() { var n = new NodeRefC(mstream); n.conf = new ConfigNodeRefC(); n.conf.reset(); return n; }, 
          delegate(BehaviorTreeNode n, ref DynVal v, bool reset) { var conf = ((NodeRefC)n).conf; v.obj = conf; if(reset) conf.reset(); }
          );
      fn.define(new FuncArgSymbol("c", globs.type("ConfigNodeRefC")));

      globs.define(fn);
    }

  }

  void BindFoo(GlobalScope globs)
  {
    {
      var cl = new ClassBindSymbol( "Foo",
        delegate(ref DynVal v) 
        { 
          v.obj = new Foo();
        }
      );
      globs.define(cl);
      globs.define(new ArrayTypeSymbolT<Foo>(globs, cl));

      cl.define(new FieldSymbol("hey", globs.type("int"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var f = (Foo)ctx.obj;
          v.SetNum(f.hey);
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var f = (Foo)ctx.obj;
          f.hey = (int)v.num; 
          ctx.obj = f;
        }
      ));
      cl.define(new FieldSymbol("colors", globs.type("Color[]"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var f = (Foo)ctx.obj;
          v.obj = f.colors;
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var f = (Foo)ctx.obj;
          f.colors = (List<Color>)v.obj; 
          ctx.obj = f;
        }
      ));
      cl.define(new FieldSymbol("sub_color", globs.type("Color"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var f = (Foo)ctx.obj;
          v.obj = f.sub_color;
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var f = (Foo)ctx.obj;
          f.sub_color = (Color)v.obj; 
          ctx.obj = f;
        }
      ));
    }
  }

  void BindFooLambda(GlobalScope globs)
  {
    {
      var cl = new ClassBindSymbol( "FooLambda",
        delegate(ref DynVal v) 
        { 
          v.obj = new FooLambda();
        }
      );
      globs.define(cl);
      globs.define(new ArrayTypeSymbolT<Foo>(globs, cl));

      cl.define(new FieldSymbol("script", globs.type("void^()"),
          delegate(DynVal ctx, ref DynVal v) {
            var f = (FooLambda)ctx.obj;
            v.obj = f.script.Count == 0 ? null : ((BaseLambda)(f.script[0])).fct.obj;
          },
          delegate(ref DynVal ctx, DynVal v) {
            var f = (FooLambda)ctx.obj;
            var fctx = (FuncCtx)v.obj;
            fctx.RefInc();
            if(f.script.Count == 0) f.script.Add(new BaseLambda()); ((BaseLambda)(f.script[0])).fct.obj = fctx;
            ctx.obj = f;
          }
     ));
    }
  }

  [IsTested()]
  public void TestBindClass()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      Color c = new Color
      c.r = k*1
      c.g = k*100
      return c.r + c.g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);

    AssertEqual(res, 202);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestBindClassCallMethod()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      Color c = new Color
      c.r = 10
      c.g = 20
      return c.mult_summ(k)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 60);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCastClassToAny()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      Color c = new Color
      c.r = k
      c.g = k*100
      any a = (any)c
      Color b = (Color)a
      return b.g + b.r 
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 202);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNestedMembersAccess()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      ColorNested cn = new ColorNested
      cn.c.r = k*1
      cn.c.g = k*100
      return cn.c.r + cn.c.g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    {
      var cl = new ClassBindSymbol( "ColorNested",
        delegate(ref DynVal v) 
        { 
          v.obj = new ColorNested();
        }
      );
      globs.define(cl);

      cl.define(new FieldSymbol("c", globs.type("Color"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var cn = (ColorNested)ctx.obj;
          v.obj = cn.c;
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var cn = (ColorNested)ctx.obj;
          cn.c = (Color)v.obj;
          ctx.obj = cn;
        }
      ));
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 202);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnBinded()
  {
    string bhl = @"
      
    func Color MakeColor(float g)
    {
      Color c = new Color
      c.g = g
      return c
    }

    func float test(float k) 
    {
      return MakeColor(k).g + MakeColor(k).r
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestChainCall()
  {
    string bhl = @"
      
    func Color MakeColor(float g)
    {
      Color c = new Color
      c.g = g
      return c
    }

    func float test(float k) 
    {
      return MakeColor(k).mult_summ(k*2)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 8);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestChainCall2()
  {
    string bhl = @"
      
    func Color MakeColor2(string temp)
    {
      Color c = new Color
      return c
    }

    func Color MakeColor(float g)
    {
      Color c = new Color
      c.g = g
      return c
    }

    func bool Check(bool cond)
    {
      return cond
    }

    func bool test(float k) 
    {
      Color o = new Color
      o.g = k
      return Check(MakeColor2(""hey"").Add(1).Add(MakeColor(k).g).r == 3)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 1);
    CommonChecks(intp);
  }

  void BindEnum(GlobalScope globs)
  {
    var en = new EnumSymbol(null, "EnumState", null);
    globs.define(en);
    globs.define(new ArrayTypeSymbol(globs, en));

    en.define(new EnumItemSymbol(null, en, "SPAWNED",  10));
    en.define(new EnumItemSymbol(null, en, "SPAWNED2", 20));
  }

  [IsTested()]
  public void TestBindEnum()
  {
    string bhl = @"
      
    func int test() 
    {
      return (int)EnumState::SPAWNED + (int)EnumState::SPAWNED2
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindEnum(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 30);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCastEnumToInt()
  {
    string bhl = @"
      
    func int test() 
    {
      return (int)EnumState::SPAWNED2
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindEnum(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 20);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCastEnumToFloat()
  {
    string bhl = @"
      
    func float test() 
    {
      return (float)EnumState::SPAWNED
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindEnum(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCastEnumToStr()
  {
    string bhl = @"
      
    func string test() 
    {
      return (string)EnumState::SPAWNED2
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindEnum(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var res = intp.ExecNode(node).val;

    AssertEqual(res.str, "20");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestEqEnum()
  {
    string bhl = @"
      
    func bool test(EnumState state) 
    {
      return state == EnumState::SPAWNED2
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindEnum(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(20));
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNotEqEnum()
  {
    string bhl = @"
      
    func bool test(EnumState state) 
    {
      return state != EnumState::SPAWNED
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindEnum(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(20));
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestArrayPoolInForever()
  {
    string bhl = @"

    func string[] make()
    {
      string[] arr = new string[]
      return arr
    }
      
    func test() 
    {
      forever {
        string[] arr = new string[]
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.run(null);

    //NodeDump(node);
    
    for(int i=0;i<2;++i)
      node.run(null);

    node.stop(null);

    AssertEqual(DynValList.PoolCount, 2);
    AssertEqual(DynValList.PoolCount, DynValList.PoolCountFree);
  }

  [IsTested()]
  public void TestStringArray()
  {
    string bhl = @"
      
    func string[] test() 
    {
      string[] arr = new string[]
      arr.Add(""foo"")
      arr.Add(""bar"")
      return arr
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindEnum(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var res = intp.ExecNode(node).val;

    //NodeDump(node);

    var lst = res.obj as DynValList;
    AssertEqual(lst.Count, 2);
    AssertEqual(lst[0].str, "foo");
    AssertEqual(lst[1].str, "bar");
    res.ValueTryRelease();
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestArrayPool()
  {
    string bhl = @"

    func string[] make()
    {
      string[] arr = new string[]
      return arr
    }

    func add(string[] arr)
    {
      arr.Add(""foo"")
      arr.Add(""bar"")
    }
      
    func string[] test() 
    {
      string[] arr = make()
      add(arr)
      return arr
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var res = intp.ExecNode(node).val;

    var lst = res.obj as DynValList;
    AssertEqual(lst.Count, 2);
    AssertEqual(lst[0].str, "foo");
    AssertEqual(lst[1].str, "bar");

    //NodeDump(node);

    AssertEqual(DynValList.PoolCount, 1);
    AssertEqual(DynValList.PoolCountFree, 0);
    res.ValueTryRelease();
    AssertEqual(DynValList.PoolCount, 1);
    AssertEqual(DynValList.PoolCountFree, 1);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestArrayCount()
  {
    string bhl = @"
      
    func int test() 
    {
      string[] arr = new string[]
      arr.Add(""foo"")
      arr.Add(""bar"")
      int c = arr.Count
      return c
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 2);
    CommonChecks(intp);
    AssertEqual(DynValList.PoolCount, DynValList.PoolCountFree);
  }

  [IsTested()]
  public void TestEnumArray()
  {
    string bhl = @"
      
    func EnumState[] test() 
    {
      EnumState[] arr = new EnumState[]
      arr.Add(EnumState::SPAWNED2)
      arr.Add(EnumState::SPAWNED)
      return arr
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindEnum(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var res = intp.ExecNode(node).val;

    var lst = res.obj as DynValList;
    AssertEqual(lst.Count, 2);
    AssertEqual(lst[0].num, 20);
    AssertEqual(lst[1].num, 10);
    res.ValueTryRelease();
    AssertEqual(DynValList.PoolCount, DynValList.PoolCountFree);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestBindedClassArray()
  {
    string bhl = @"
      
    func string test(float k) 
    {
      Color[] cs = new Color[]
      Color c0 = new Color
      cs.Add(c0)
      cs.RemoveAt(0)
      Color c1 = new Color
      c1.r = 10
      Color c2 = new Color
      c2.g = 20
      cs.Add(c1)
      cs.Add(c2)
      Color c3 = new Color
      cs.Add(c3)
      cs[2].r = 30
      Color c4 = new Color
      cs.Add(c4)
      cs.RemoveAt(3)
      return (string)cs.Count + (string)cs[0].r + (string)cs[1].g + (string)cs[2].r
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = intp.ExecNode(node).val;

    AssertEqual(res.str, "3102030");
    CommonChecks(intp);
  }

  public class BaseScript
  {
  }

  public class TestPtr 
  {
    public object obj;
  }

  public class BaseLambda : BaseScript
  {
    public TestPtr fct = new TestPtr();
  }

  public class Foo
  {
    public int hey;
    public List<Color> colors = new List<Color>();
    public Color sub_color = new Color();

    public void reset()
    {
      hey = 0;
      colors.Clear();
      sub_color = new Color();
    }
  }

  public class FooLambda
  {
    public List<BaseScript> script = new List<BaseScript>();

    public void reset()
    {
      script.Clear();
    }
  }

  [IsTested()]
  public void TestBindedSubClassArray()
  {
    string bhl = @"
      
    func string test(float k) 
    {
      Foo f = new Foo
      f.hey = 10
      Color c1 = new Color
      c1.r = 20
      Color c2 = new Color
      c2.g = 30
      f.colors.Add(c1)
      f.colors.Add(c2)
      return (string)f.colors.Count + (string)f.hey + (string)f.colors[0].r + (string)f.colors[1].g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = intp.ExecNode(node).val;

    AssertEqual(res.str, "2102030");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestBindedAttributeIsNotAFunction()
  {
    string bhl = @"

    func float r()
    {
      return 0
    }
      
    func void test() 
    {
      Color c = new Color
      c.r()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "Symbol is not a function"
    );
  }

  public class StateIsNode : BehaviorTreeTerminalNode
  {
    public override BHS execute(object agent)
    {
      var interp = Interpreter.instance;
      var val = interp.PopValue();
      return val.num == 20 ? BHS.SUCCESS : BHS.FAILURE; 
    }
  }

  [IsTested()]
  public void TestPassEnumToBindedNode()
  {
    string bhl = @"
      
    func void test() 
    {
      StateIs(state : EnumState::SPAWNED)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindEnum(globs);

    {
      var fn = new FuncBindSymbol("StateIs", globs.type("void"),
          delegate() { return new StateIsNode(); });
      fn.define(new FuncArgSymbol("state", globs.type("EnumState")));

      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = intp.ExecNode(node, false);
    AssertEqual(res.status, BHS.FAILURE);
  }

  public class StartScriptNode : BehaviorTreeDecoratorNode
  {
    public override void init(object agent)
    {
      var interp = Interpreter.instance;
      var dv = interp.PopValue(); 
      var fct = (FuncCtx)dv.obj;
      fct.RefInc();

      var func_node = fct.GetNode();

      this.setSlave(func_node);

      base.init(agent);
    }

    public override void defer(object agent)
    {
      base.defer(agent);

      var lfunc = children[0] as FuncNodeLambda;
      if(lfunc != null)
      {
        lfunc.ctx.RefDec();
      }
    }
  }

  public class TraceNode : BehaviorTreeTerminalNode
  {
    Stream sm;

    public TraceNode(Stream sm)
    {
      this.sm = sm;
    }

    public override void init(object agent)
    {
      var interp = Interpreter.instance;
      var s = interp.PopValue();

      var sw = new StreamWriter(sm);
      sw.Write(s.str);
      sw.Flush();
    }
  }

  public class WaitTicksNode : BehaviorTreeTerminalNode
  {
    int ticks;
    BHS result;

    public override void init(object agent)
    {
      var interp = Interpreter.instance;
      var s = interp.PopValue();
      var v = interp.PopValue();
      ticks = (int)v.num;
      result = s.bval ? BHS.SUCCESS : BHS.FAILURE;
    }

    public override BHS execute(object agent)
    {
      return --ticks > 0 ? BHS.RUNNING : result;
    }
  }

  void BindTrace(GlobalScope globs, MemoryStream trace_stream)
  {
    {
      var fn = new FuncBindSymbol("trace", globs.type("void"),
          delegate() { return new TraceNode(trace_stream); } );
      fn.define(new FuncArgSymbol("str", globs.type("string")));

      globs.define(fn);
    }
  }

  void BindMin(GlobalScope globs)
  {
    {
      var fn = new SimpleFuncBindSymbol("min", globs.type("float"),
        delegate(object agent)
        {
          var interp = Interpreter.instance;
          var b = (float)interp.PopValue().num;
          var a = (float)interp.PopValue().num;
          interp.PushValue(DynVal.NewNum(a > b ? b : a)); 
          return BHS.SUCCESS;
        }
      );
      fn.define(new FuncArgSymbol("a", globs.type("float")));
      fn.define(new FuncArgSymbol("b", globs.type("float")));

      globs.define(fn);
    }
  }

  public class NodeWithDefer : BehaviorTreeTerminalNode
  {
    Stream sm;

    public NodeWithDefer(Stream sm)
    {
      this.sm = sm;
    }

    public override BHS execute(object agent)
    {
      return BHS.SUCCESS;
    }

    public override void defer(object agent)
    {
      var sw = new StreamWriter(sm);
      sw.Write("DEFER!!!");
      sw.Flush();
    }
  }

  void BindNodeWithDefer(GlobalScope globs, MemoryStream s)
  {
    {
      var fn = new FuncBindSymbol("NodeWithDefer", globs.type("void"),
          delegate() { return new NodeWithDefer(s); } );

      globs.define(fn);
    }
  }

  void BindWaitTicks(GlobalScope globs)
  {
    {
      var fn = new FuncBindSymbol("WaitTicks", globs.type("void"),
          delegate() { return new WaitTicksNode(); } );
      fn.define(new FuncArgSymbol("ticks", globs.type("int")));
      fn.define(new FuncArgSymbol("is_success", globs.type("bool")));

      globs.define(fn);
    }
  }

  void AddString(MemoryStream s, string str)
  {
    var sw = new StreamWriter(s);
    sw.Write(str);
    sw.Flush();
  }

  string GetString(MemoryStream s)
  {
    s.Position = 0;
    var sr = new StreamReader(s);
    return sr.ReadToEnd();
  }

  [IsTested()]
  public void TestReturnVoid()
  {
    string bhl = @"
      
    func void test() 
    {
      trace(""HERE"")
      return
      trace(""NOT HERE"")
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);
    //NodeDump(node);

    var str = GetString(trace_stream);

    AssertEqual("HERE", str);
    CommonChecks(intp);
  }

  void BindStartScript(GlobalScope globs)
  {
    {
      var fn = new FuncBindSymbol("StartScript", globs.type("void"),
          delegate() { return new StartScriptNode(); } );
      fn.define(new FuncArgSymbol("script", globs.type("void^()")));

      globs.define(fn);
    }
  }

  [IsTested()]
  public void TestLambda()
  {
    string bhl = @"
    func void test() 
    {
      StartScript(
        func()
        { 
          trace(""HERE"")
        }             
      ) 
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScript(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("HERE", str);
    CommonChecks(intp);
    //NodeDump(node);
  }

  [IsTested()]
  public void TestLambdaVar()
  {
    string bhl = @"
    func void test() 
    {
      void^() fun = 
        func()
        { 
          trace(""HERE"")
        }             

      fun()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScript(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("HERE", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncPtr()
  {
    string bhl = @"
    func foo()
    {
      trace(""FOO"")
    }

    func void test() 
    {
      void^() ptr = foo
      ptr()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("FOO", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaPassAsVar()
  {
    string bhl = @"

    func foo(void^() fn)
    {
      fn()
    }

    func void test() 
    {
      void^() fun = 
        func()
        { 
          trace(""HERE"")
        }             

      foo(fun)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScript(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("HERE", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncPtrWithDeclPassedAsArg()
  {
    string bhl = @"

    func foo(void^() fn)
    {
      fn()
    }

    func hey()
    {
      float foo
      trace(""HERE"")
    }

    func void test() 
    {
      foo(hey)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("HERE", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncPtrForBindFunc()
  {
    string bhl = @"
    func void test() 
    {
      void^() ptr = foo
      ptr()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    {
      var fn = new SimpleFuncBindSymbol("foo", globs.type("void"), 
          delegate(object agent)
          {
            AddString(trace_stream, "FOO");
            return BHS.SUCCESS;
          }
          );
      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("FOO", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaVarSeveralTimes()
  {
    string bhl = @"
    func void test() 
    {
      void^() fun = 
        func()
        { 
          trace(""HERE"")
        }             

      fun()
      fun()
      fun()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScript(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("HEREHEREHERE", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaSeveralTimes()
  {
    string bhl = @"
    func void test() 
    {
      StartScript(
        func()
        { 
          trace(""HERE"")
        }             
      ) 
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScript(globs);

    var intp = Interpret("", bhl, globs);
    var node1 = intp.GetFuncNode("test");
    var node2 = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node1, false);
    intp.ExecNode(node2, false);

    var str = GetString(trace_stream);

    AssertEqual("HEREHERE", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestSeveralLambdas()
  {
    string bhl = @"
    func void test() 
    {
      StartScript(
        func()
        { 
          trace(""HERE"")
        }             
      ) 
    }

    func void test2() 
    {
      StartScript(
        func()
        { 
          trace(""HERE2"")
        }             
      ) 
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScript(globs);

    var intp = Interpret("", bhl, globs);
    intp.ExecNode(intp.GetFuncNode("test"), false);
    intp.ExecNode(intp.GetFuncNode("test2"), false);

    var str = GetString(trace_stream);

    AssertEqual("HEREHERE2", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaCaptureVars()
  {
    string bhl = @"
    func void test() 
    {
      float a = 10
      float b = 20
      StartScript(
        func()
        { 
          float k = a 
          trace((string)k + (string)b)
        }             
      ) 
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScript(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("1020", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaCaptureVarsHiding()
  {
    string bhl = @"

    func float time()
    {
      return 42
    }

    func void foo(float time)
    {
      void^() fn = func()
      {
        float b = time
        trace((string)b)
      }
      fn()
    }

    func void test() 
    {
      foo(10)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("10", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaCaptureSelfVars()
  {
    string bhl = @"
    func void test() 
    {
      float a = 10
      float b = 20
      StartScript(
        func()
        { 
          float a = a 
          trace((string)a + (string)b)
        }             
      ) 
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScript(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "Already defined symbol 'a'"
    );
  }

  [IsTested()]
  public void TestLambdaCaptureVarsNested()
  {
    string bhl = @"
    func void test() 
    {
      float a = 10
      float b = 20
      StartScript(
        func()
        { 
          float k = a 

          void^() fn = func() 
          {
            trace((string)k + (string)b)
          }

          fn()
        }             
      ) 
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScript(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("1020", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaCaptureVarsNested2()
  {
    string bhl = @"
    func void test() 
    {
      float a = 10
      float b = 20
      StartScript(
        func()
        { 
          float k = a 

          void^() fn = func() 
          {
            void^() fn = func() 
            {
              trace((string)k + (string)b)
            }

            fn()
          }

          fn()
        }             
      ) 
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScript(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("1020", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaCaptureVars2()
  {
    string bhl = @"

    func call(void^() fn, void^() fn2)
    {
      StartScript(fn2)
    }

    func void foo(float a = 1, float b = 2)
    {
      call(fn2 :
        func()
        { 
          float k = a 
          trace((string)k + (string)b)
        },
        fn : func() { }
      ) 
    }

    func void bar()
    {
      call(
        fn2 :
        func()
        { 
          trace(""HEY!"")
        },             
        fn : func() { }
      ) 
    }

    func void test() 
    {
      foo(10, 20)
      bar()
      foo()
      bar()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScript(globs);

    var intp = Interpret("", bhl, globs);

    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("1020HEY!12HEY!", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestSequenceSuccess()
  {
    string bhl = @"

    func foo()
    {
      trace(""FOO"")
    }

    func bar()
    {
      trace(""BAR"")
    }

    func test() 
    {
      seq {
        bar()
        foo()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run(null);
      AssertEqual(BHS.SUCCESS, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("BARFOO", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestSequenceFailure()
  {
    string bhl = @"

    func foo()
    {
      trace(""FOO"")
      FAILURE()
    }

    func bar()
    {
      trace(""BAR"")
    }

    func test() 
    {
      seq {
        bar()
        foo()
        bar()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run(null);
      AssertEqual(BHS.FAILURE, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("BARFOO", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestSwallowingSequenceSuccess()
  {
    string bhl = @"

    func foo()
    {
      trace(""FOO"")
    }

    func bar()
    {
      trace(""BAR"")
    }

    func test() 
    {
      seq_ {
        bar()
        foo()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run(null);
      AssertEqual(BHS.SUCCESS, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("BARFOO", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestSwallowingSequenceFailure()
  {
    string bhl = @"

    func foo()
    {
      trace(""FOO"")
      FAILURE()
    }

    func bar()
    {
      trace(""BAR"")
    }

    func test() 
    {
      seq_ {
        bar()
        foo()
        bar()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run(null);
      AssertEqual(BHS.SUCCESS, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("BARFOO", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNormalBlockIsSequenceFailure()
  {
    string bhl = @"

    func foo()
    {
      trace(""FOO"")
      FAILURE()
    }

    func bar()
    {
      trace(""BAR"")
    }

    func test() 
    {
      bar()
      foo()
      bar()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run(null);
      AssertEqual(BHS.FAILURE, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("BARFOO", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestParalAnySuccess()
  {
    string bhl = @"

    func foo(int ticks)
    {
      WaitTicks(ticks, true)
      trace(""A"")
    }

    func bar(int ticks)
    {
      WaitTicks(ticks, true)
      trace(""B"")
    }

    func test() 
    {
      paral {
        bar(3)
        foo(2)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    {
      var status = node.run(null);
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run(null);
      AssertEqual(BHS.SUCCESS, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("A", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestParalAnyFailure()
  {
    string bhl = @"

    func foo(int ticks)
    {
      WaitTicks(ticks, false)
      trace(""A"")
    }

    func bar(int ticks)
    {
      WaitTicks(ticks, true)
      trace(""B"")
    }

    func test() 
    {
      paral {
        bar(3)
        foo(2)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    {
      var status = node.run(null);
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run(null);
      AssertEqual(BHS.FAILURE, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestParalAnyFirstNodeWins()
  {
    string bhl = @"
    func bar(int ticks)
    {
      WaitTicks(ticks, true)
      trace(""B"")
    }

    func foo(int ticks)
    {
      WaitTicks(ticks, false)
      trace(""A"")
    }

    func test() 
    {
      paral {
        bar(2)
        foo(2)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    {
      var status = node.run(null);
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run(null);
      AssertEqual(BHS.SUCCESS, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("B", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestParalForComplexStatement()
  {
    string bhl = @"

    func int foo(int a)
    {
      return a
    }

    func float test() 
    {
      Color c = new Color
      int a = 0
      float s = 0
      paral {
        a = foo(10)
        c.r = 142
        s = c.mult_summ(a)
      }
      return a
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(res, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestParalAllSuccess()
  {
    string bhl = @"

    func foo(int ticks)
    {
      WaitTicks(ticks, true)
      trace(""A"")
    }

    func bar(int ticks)
    {
      WaitTicks(ticks, true)
      trace(""B"")
    }

    func test() 
    {
      paral_all {
        bar(3)
        foo(2)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    {
      var status = node.run(null);
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run(null);
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run(null);
      AssertEqual(BHS.SUCCESS, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("AB", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestParalAllFailure()
  {
    string bhl = @"

    func foo(int ticks)
    {
      WaitTicks(ticks, false)
      trace(""A"")
    }

    func bar(int ticks)
    {
      WaitTicks(ticks, true)
      trace(""B"")
    }

    func test() 
    {
      paral_all {
        bar(3)
        foo(2)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    {
      var status = node.run(null);
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run(null);
      AssertEqual(BHS.FAILURE, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestParalAllForComplexStatement()
  {
    string bhl = @"

    func int foo(int a)
    {
      return a
    }

    func Color MakeColor(float g)
    {
      Color c = new Color
      c.g = g
      return c
    }

    func DoSomething(Color c)
    {
    }

    func float test() 
    {
      Color c = new Color
      int a = 0
      float s = 0
      paral_all {
        a = foo(10)
        c.r = 142
        s = MakeColor(a).mult_summ(a)
        DoSomething(MakeColor(a))
      }
      return a*c.r+s
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(res, 1520);
    CommonChecks(intp);
  }
  
  [IsTested()]
  public void TestParalAllDeferForComplexStatement()
  {
    string bhl = @"

    func void test() 
    {
      paral_all {
        NodeWithDefer()
        RUNNING()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();
    
    BindNodeWithDefer(globs, trace_stream);
    BindTrace(globs, trace_stream);
   
    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
   
    var status = node.run(null);
    AssertEqual(BHS.RUNNING, status);
    //NodeDump(node);
    
    var str = GetString(trace_stream);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPrio()
  {
    string bhl = @"

    func foo()
    {
      trace(""FOO"")
      FAILURE()
    }

    func hey()
    {
      trace(""HEY"")
      SUCCESS()
    }

    func bar()
    {
      trace(""BAR"")
      FAILURE()
    }

    func test() 
    {
      prio {
        bar()
        hey()
        foo()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run(null);
      AssertEqual(BHS.SUCCESS, status);
    }

    //NodeDump(node);
    var str = GetString(trace_stream);
    AssertEqual("BARHEY", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPrioSwitchFromFailedNode()
  {
    string bhl = @"

    func bar()
    {
      trace(""BAR"")
      FAILURE()
    }

    func hey()
    {
      trace(""HEY"")
      //once this one fails prio will switch to 'foo'
      WaitTicks(2, false)
      //will never run
      trace(""HEY2"")
    }

    func foo()
    {
      trace(""FOO"")
      SUCCESS()
    }

    func test() 
    {
      prio {
        bar()
        hey()
        foo()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run(null);
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run(null);
      AssertEqual(BHS.SUCCESS, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("BARHEYFOO", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPrioForComplexStatement()
  {
    string bhl = @"

    func float test() 
    {
      Color c = new Color
      prio {
        FAILURE()
        c.r = 142
      }
      return c.r
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 142);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDefer()
  {
    string bhl = @"

    func bar()
    {
      trace(""BAR"")
    }

    func hey()
    {
      trace(""HEY"")
    }

    func foo()
    {
      trace(""FOO"")
    }

    func test() 
    {
      defer {
        foo()
      }
      bar()
      hey()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run(null);
      AssertEqual(BHS.SUCCESS, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("BARHEYFOO", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferAccessVar()
  {
    string bhl = @"

    func foo(float k)
    {
      trace((string)k)
    }

    func test() 
    {
      float k = 142
      defer {
        foo(k)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("142", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferSeveral()
  {
    string bhl = @"

    func bar()
    {
      defer {
        trace(""~BAR"")
      }
      trace(""BAR"")
    }

    func foo()
    {
      defer {
        trace(""~FOO"")
      }
      trace(""FOO"")
    }

    func test() 
    {
      bar()
      foo()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run(null);
      AssertEqual(BHS.SUCCESS, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("BAR~BARFOO~FOO", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferAndReturn()
  {
    string bhl = @"

    func void bar(float k)
    {
      defer {
        trace(""~BAR"")
      }
      trace(""BAR"")
    }

    func float foo()
    {
      defer {
        trace(""~FOO"")
      }
      return 3
      trace(""FOO"")
    }

    func test() 
    {
      bar(foo())
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run(null);
      AssertEqual(BHS.SUCCESS, status);
    }

    //NodeDump(node);

    var str = GetString(trace_stream);
    AssertEqual("~FOOBAR~BAR", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferOnFailure()
  {
    string bhl = @"

    func bar()
    {
      trace(""BAR"")
      FAILURE()
    }

    func hey()
    {
      trace(""HEY"")
    }

    func foo()
    {
      trace(""FOO"")
    }

    func test() 
    {
      defer {
        foo()
      }
      bar()
      hey()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run(null);
      AssertEqual(BHS.FAILURE, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("BARFOO", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferInParal()
  {
    string bhl = @"

    func bar()
    {
      trace(""BAR"")
      WaitTicks(2, true)
    }

    func hey()
    {
      trace(""HEY"")
      WaitTicks(2, true)
    }

    func foo()
    {
      trace(""FOO"")
    }

    func test() 
    {
      paral {
        defer {
          foo()
        }
        bar()
        hey()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);

    {
      var status = node.run(null);
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run(null);
      AssertEqual(BHS.SUCCESS, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("BARHEYFOO", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferMethodIsNotTriggeredTooEarly()
  {
    string bhl = @"

    func test() 
    {
      NodeWithDefer()
      RUNNING()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindNodeWithDefer(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run(null);
      AssertEqual(BHS.RUNNING, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferMethodIsNotTriggeredTooEarlyInDecorator()
  {
    string bhl = @"

    func test() 
    {
      //double not in order no to interrupt the execution
      not { not { NodeWithDefer() } }
      RUNNING()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindNodeWithDefer(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run(null);
      AssertEqual(BHS.RUNNING, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferMethodIsProperlyTriggered()
  {
    string bhl = @"

    func test() 
    {
      NodeWithDefer()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindNodeWithDefer(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run(null);
      AssertEqual(BHS.SUCCESS, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("DEFER!!!", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferMethodIsProperlyTriggeredInDecorator()
  {
    string bhl = @"

    func test() 
    {
      not {
        NodeWithDefer()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindNodeWithDefer(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run(null);
      AssertEqual(BHS.FAILURE, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("DEFER!!!", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestEvalSuccess()
  {
    string bhl = @"

    func bool test() 
    {
      bool res = eval {
        SUCCESS()
      }
      return res
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestEvalFailure()
  {
    string bhl = @"

    func bool test() 
    {
      bool res = eval {
        FAILURE()
      }
      return res
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestEvalRunning()
  {
    string bhl = @"

    func bool test() 
    {
      bool res = eval {
        RUNNING()
      }
      return res
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var status = node.run(null);
    AssertEqual(BHS.RUNNING, status);
  }

  [IsTested()]
  public void TestEvalDanglingReturn()
  {
    string bhl = @"

    func void test() 
    {
      eval { 
        SUCCESS() 
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Interpret("", bhl);
      },
      "mismatched input 'eval'"
    );
  }

  [IsTested()]
  public void TestEvalWrongType()
  {
    string bhl = @"

    func void test() 
    {
      int k = eval { 
        SUCCESS() 
      }
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      "have incompatible types"
    );
  }

  [IsTested()]
  public void TestEvalMoreComplicated()
  {
    string bhl = @"

    func bool test() 
    {
      bool res = eval {
        if(false) {
          SUCCESS()
        } else {
          FAILURE()
        }
      }
      return res
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestEvalInIfSuccess()
  {
    string bhl = @"

    func int test(int k) 
    {
      if(eval { SUCCESS() })
      {
        return k
      }
      else 
      {
        return k+1
      }

    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);

    AssertEqual(res, 3);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestEvalInIfFailure()
  {
    string bhl = @"

    func int test(int k) 
    {
      if(eval { FAILURE() })
      {
        return k
      }
      else 
      {
        return k+1
      }

    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);

    AssertEqual(res, 4);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForever()
  {
    string bhl = @"

    func bar()
    {
      trace(""B"")
      FAILURE()
    }

    func foo()
    {
      trace(""A"")
      FAILURE()
    }

    func test() 
    {
      forever {
        bar()
        foo() //this one won't be executed
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    for(int i=0;i<5;++i)
    {
      var status = node.run(null);
      AssertEqual(BHS.RUNNING, status);
    }

    //...will be running forever, well, we assume that :)

    var str = GetString(trace_stream);
    AssertEqual("BBBBB", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUntilSuccess()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      until_success {
        i = i + 1
        if(i <= 3) {
          trace((string)i)
          FAILURE()
        }
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    for(int i=0;i<3;++i)
    {
      var status = node.run(null);
      AssertEqual(BHS.RUNNING, status);
    }
    AssertEqual(BHS.SUCCESS, node.run(null));

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUntilFailure()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      until_failure {
        i = i + 1
        if(i <= 3) {
          trace((string)i)
        } else {
          FAILURE()
        }
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    for(int i=0;i<3;++i)
    {
      var status = node.run(null);
      AssertEqual(BHS.RUNNING, status);
    }
    AssertEqual(BHS.FAILURE, node.run(null));

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUntilFailure_()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      until_failure_ {
        i = i + 1
        if(i <= 3) {
          trace((string)i)
        } else {
          FAILURE()
        }
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    for(int i=0;i<3;++i)
    {
      var status = node.run(null);
      AssertEqual(BHS.RUNNING, status);
    }
    AssertEqual(BHS.SUCCESS, node.run(null));

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncCaching()
  {
    string bhl = @"
      
    func float foo(float k)
    {
      return k
    }

    func float test(float k) 
    {
      return foo(k)
    }
    ";

    var intp = Interpret("", bhl);

    AssertEqual(FuncCallNode.PoolCount, 0);

    {
      var node = intp.GetFuncNode("test");
      node.SetArgs(DynVal.NewNum(3));
      var res = ExtractNum(intp.ExecNode(node));

      AssertEqual(res, 3);
      CommonChecks(intp);
    }

    AssertEqual(FuncCallNode.PoolCount, 1);

    {
      var node = intp.GetFuncNode("test");
      node.SetArgs(DynVal.NewNum(30));
      var res = ExtractNum(intp.ExecNode(node));

      AssertEqual(res, 30);
      CommonChecks(intp);
    }

    AssertEqual(FuncCallNode.PoolCount, 1);
  }

  [IsTested()]
  public void TestOnlyUserlandFuncsAreCached()
  {
    string bhl = @"
      
    func void foo(float k)
    {
      
    }

    func void test(float k) 
    {
      forever
      {
        foo(k)
        RUNNING()
      }
    }
    ";

    var intp = Interpret("", bhl);

    AssertEqual(FuncCallNode.PoolCount, 0);
    AssertEqual(FuncCallNode.PoolCountFree, 0);

    var node1 = intp.GetFuncNode("test");
    node1.SetArgs(DynVal.NewNum(3));
    var status = node1.run(null);
    AssertEqual(BHS.RUNNING, status);
    
    AssertEqual(FuncCallNode.PoolCount, 1);
    AssertEqual(FuncCallNode.PoolCountFree, 0);

    var node2 = intp.GetFuncNode("test");
    node2.SetArgs(DynVal.NewNum(30));
    status = node2.run(null);
    AssertEqual(BHS.RUNNING, status);

    AssertEqual(FuncCallNode.PoolCount, 2);
    AssertEqual(FuncCallNode.PoolCountFree, 0);

    node1.stop(null);

    AssertEqual(FuncCallNode.PoolCount, 2);
    AssertEqual(FuncCallNode.PoolCountFree, 1);

    node2.stop(null);

    AssertEqual(FuncCallNode.PoolCount, 2);
    AssertEqual(FuncCallNode.PoolCountFree, 2);
  }

  [IsTested()]
  public void TestFuncCachingForConfigNode()
  {
    string bhl = @"

    func void foo(void^() fn)
    {
      ConfigNodeLambda({script: fn})
    }

    func void^() mkfn(string msg)
    {
      return func() {
        trace(msg) 
      }
    }

      
    func void test() 
    {
      int c = 0
      paral_all {
        while(c < 3) {
          c = c + 1
          YIELD()
        }
        seq {
          while(c != 1) {
            RUNNING()
          }
          foo(func() {
            foo(func() { trace("">1"") } )
          })
        }

        seq {
          while(c != 2) {
            RUNNING()
          }
          foo(func() {
            foo(func() { trace("">2"") } )
          })
        }

        seq {
          while(c != 3) {
            RUNNING()
          }
          foo(func() {
            foo(mkfn("">3""))
          })
        }
      }
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFooLambda(globs);
    BindTrace(globs, trace_stream);
    BindConfigNodeLambda(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    AssertEqual(FuncCallNode.PoolCount, 0);
    {
      var s = node.run(null);
      AssertEqual(BHS.RUNNING, s);
    }
    {
      var s = node.run(null);
      AssertEqual(BHS.RUNNING, s);
    }
    {
      var s = node.run(null);
      AssertEqual(BHS.RUNNING, s);
    }
    {
      var s = node.run(null);
      AssertEqual(BHS.SUCCESS, s);
    }

    var str = GetString(trace_stream);

    //NodeDump(node);

    AssertEqual(">1" + ">2" + ">3", str);
    CommonChecks(intp);

    AssertEqual(FuncCallNode.PoolCount, 3);
    AssertEqual(FuncCallNode.PoolCountFree, 3);
  }

  [IsTested()]
  public void TestReturnFuncToConfigNode()
  {
    string bhl = @"

    func void^() mkfn(string msg)
    {
      return func() {
        trace(msg) 
      }
    }

      
    func void test() 
    {
      ConfigNodeLambda({script: mkfn("">1"")})
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFooLambda(globs);
    BindTrace(globs, trace_stream);
    BindConfigNodeLambda(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var s = node.run(null);
    AssertEqual(BHS.SUCCESS, s);
    var str = GetString(trace_stream);

    AssertEqual(">1", str);
    CommonChecks(intp);

    AssertEqual(1, FuncCtx.PoolCount);
    AssertEqual(1, FuncCtx.PoolCountFree);
  }

  [IsTested()]
  public void TestPassLambdaFuncToConfigNode()
  {
    string bhl = @"

    func void test() 
    {
      ConfigNodeLambda({script: func() { trace("">1"")} })
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFooLambda(globs);
    BindTrace(globs, trace_stream);
    BindConfigNodeLambda(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var s = node.run(null);
    AssertEqual(BHS.SUCCESS, s);
    var str = GetString(trace_stream);

    AssertEqual(">1", str);
    CommonChecks(intp);

    AssertEqual(1, FuncCtx.PoolCount);
    AssertEqual(1, FuncCtx.PoolCountFree);
  }

  [IsTested()]
  public void TestPassLambdaVarToConfigNode()
  {
    string bhl = @"

    func void test() 
    {
      void^() fn = func() { trace("">1"")} 
      ConfigNodeLambda({script:  fn})
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFooLambda(globs);
    BindTrace(globs, trace_stream);
    BindConfigNodeLambda(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var s = node.run(null);
    AssertEqual(BHS.SUCCESS, s);
    var str = GetString(trace_stream);

    AssertEqual(">1", str);
    CommonChecks(intp);

    AssertEqual(1, FuncCtx.PoolCount);
    AssertEqual(1, FuncCtx.PoolCountFree);
  }

  [IsTested()]
  public void TestBindClassCallMember()
  {
    string bhl = @"

    func float foo(float a)
    {
      return mkcolor(a).r
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();
    
    BindColor(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);

    var foo = intp.GetFuncNode("foo");
    foo.SetArgs(DynVal.NewNum(10));
    var res = ExtractNum(intp.ExecNode(foo));
    AssertEqual(res, 10);
    CommonChecks(intp);

    foo.SetArgs(DynVal.NewNum(20));
    res = ExtractNum(intp.ExecNode(foo));
    AssertEqual(res, 20);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestIfTrue()
  {
    string bhl = @"

    func test() 
    {
      if(true) {
        trace(""OK"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("OK", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestIfNoBrackets()
  {
    string bhl = @"

    func test() 
    {
      if true  {
        trace(""OK"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("OK", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNonMatchingReturnAfterIf()
  {
    string bhl = @"

    func int test() 
    {
      if(false) {
        return 10
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "matching 'return' statement not found"
    );
  }

  [IsTested()]
  public void TestNonMatchingReturnAfterElseIf()
  {
    string bhl = @"

    func int test() 
    {
      if(false) {
        return 10
      } else if (true) {
        return 20
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "matching 'return' statement not found"
    );
  }

  [IsTested()]
  public void TestMatchingReturnInElse()
  {
    string bhl = @"

    func int test() 
    {
      if(false) {
        return 10
      } else if (false) {
        return 20
      } else {
        return 30
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(30, res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestIfTrueComplexCondition()
  {
    string bhl = @"

    func test() 
    {
      if(true && true) {
        trace(""OK"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("OK", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestIfFalse()
  {
    string bhl = @"

    func test() 
    {
      if(false) {
        trace(""NEVER"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestIfWithMultipleReturns()
  {
    string bhl = @"

    func int test(int b) 
    {
      if(b == 1) {
        return 2
      }

      return 3
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(1));
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 2);
    CommonChecks(intp);

    node.SetArgs(DynVal.NewNum(10));
    res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 3);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestIfFalseComplexCondition()
  {
    string bhl = @"

    func test() 
    {
      if(false || !true) {
        trace(""NEVER"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestIfElseIf()
  {
    string bhl = @"

    func test() 
    {
      if(false) {
        trace(""NEVER"")
      } else if(false) {
        trace(""NEVER2"")
      } else if(true) {
        trace(""OK"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("OK", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestIfElseIfComplexCondition()
  {
    string bhl = @"

    func test() 
    {
      if(false) {
        trace(""NEVER"")
      } else if(false) {
        trace(""NEVER2"")
      } else if(true && !false) {
        trace(""OK"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("OK", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestIfElse()
  {
    string bhl = @"

    func test() 
    {
      if(false) {
        trace(""NEVER"")
      } else {
        trace(""ELSE"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("ELSE", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestIfElseIfElse()
  {
    string bhl = @"

    func test() 
    {
      if(false) {
        trace(""NEVER"")
      }
      else if (false) {
        trace(""NEVER2"")
      } else {
        trace(""ELSE"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("ELSE", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLocalVarHiding()
  {
    string bhl = @"
      
    func float time()
    {
      return 42
    }

    func float bar(float time)
    {
      return time
    }

    func float test() 
    {
      return bar(100)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = intp.ExecNode(node).val;

    AssertEqual(res.num, 100);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLocalVarHiding2()
  {
    string bhl = @"
      
    func float time()
    {
      return 42
    }

    func float bar(float time)
    {
      if(time == 0)
      {
        return time
      }
      else
      {
        return time()
      }
    }

    func float test() 
    {
      return bar(100)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = intp.ExecNode(node).val;

    AssertEqual(res.num, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNull()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c = null
      Color c2 = new Color
      if(c == null) {
        trace(""NULL;"")
      }
      if(c2 == null) {
        trace(""NULL2;"")
      }
      if(c2 != null) {
        trace(""NOT NULL;"")
      }
      c = c2
      if(c2 == c) {
        trace(""EQ;"")
      }
      if(c2 == null) {
        trace(""NEVER;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("NULL;NOT NULL;EQ;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNullIncompatible()
  {
    string bhl = @"
      
    func bool test() 
    {
      return 0 == null
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      "have incompatible types"
    );
  }

  [IsTested()]
  public void TestNullArray()
  {
    string bhl = @"
      
    func void test() 
    {
      Color[] cs = null
      Color[] cs2 = new Color[]
      if(cs == null) {
        trace(""NULL;"")
      }
      if(cs2 == null) {
        trace(""NULL2;"")
      }
      if(cs2 != null) {
        trace(""NOT NULL;"")
      }
      cs = cs2
      if(cs2 == cs) {
        trace(""EQ;"")
      }
      if(cs2 == null) {
        trace(""NEVER;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("NULL;NOT NULL;EQ;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNullFuncPtr()
  {
    string bhl = @"
      
    func void test() 
    {
      void^() fn = null
      void^() fn2 = func () { }
      if(fn == null) {
        trace(""NULL;"")
      }
      if(fn != null) {
        trace(""NEVER;"")
      }
      if(fn2 == null) {
        trace(""NEVER2;"")
      }
      if(fn2 != null) {
        trace(""NOT NULL;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("NULL;NOT NULL;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestWhile()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      while(i < 3) {
        trace((string)i)
        i = i + 1
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("012", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestWhileManyTimesAtOneRun()
  {
    string bhl = @"

    func int test() 
    {
      int i = 0
      while(i < 1000) {
        i = i + 1
      }
      return i
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    {
      var status = node.run(null);
      AssertEqual(BHS.SUCCESS, status);
    }
    var res = intp.PopValue();

    AssertEqual(1000, res.num);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestWhileCounterOnTheTop()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      while(i < 3) {
        i = i + 1
        trace((string)i)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestWhileFalse()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      while(false) {
        trace((string)i)
        i = i + 1
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestBadBreak()
  {
    string bhl = @"

    func test() 
    {
      break
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "Not within loop construct"
    );
  }

  [IsTested()]
  public void TestWhileBreak()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      while(i < 3) {
        trace((string)i)
        i = i + 1
        if(i == 2) {
          break
        }
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("01", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestWhileFailure()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      while(i < 3) {
        trace((string)i)
        i = i + 1
        if(i == 2) {
          FAILURE()
        }
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run(null);
      AssertEqual(BHS.FAILURE, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("01", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLocalScopeNotSupported()
  {
    string bhl = @"

    func test() 
    {
      int i = 1
      {
        int i = 2
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "Already defined symbol 'i'"
    );
  }

//TODO: continue not supported
//  [IsTested()]
//  public void TestWhileContinue()
//  {
//    string bhl = @"
//
//    func test() 
//    {
//      int i = 0
//      while(i < 3) {
//        i = i + 1
//        if(i == 1) {
//          continue
//        } 
//        trace((string)i)
//      }
//    }
//    ";
//
//    var globs = SymbolTable.CreateBuiltins();
//    var trace_stream = new MemoryStream();
//
//    BindTrace(globs, trace_stream);
//
//    var intp = Interpret("", bhl, globs);
//    var node = intp.GetFuncNode("test");
//    //NodeDump(node);
//    intp.ExecNode(node, false);
//
//    var str = GetString(trace_stream);
//    AssertEqual("02", str);
//    CommonChecks(intp);
//  }

  [IsTested()]
  public void TestWhileComplexCondition()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      while(i < 3 && true) {
        trace((string)i)
        i = i + 1
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);
    AssertEqual("012", str);
    CommonChecks(intp);
  }

  public class MakeFooNode : BehaviorTreeTerminalNode
  {
    public override void init(object agent)
    {
      var interp = Interpreter.instance;
      var foo = interp.PopValue().ValueClone();
      interp.PushValue(foo);
    }
  }

  [IsTested()]
  public void TestJsonInit()
  {
    string bhl = @"
    func void test(float b) 
    {
      Foo f = MakeFoo({hey:142, colors:[{r:2}, {g:3}, {g:b}]})
      trace((string)f.hey + (string)f.colors.Count + (string)f.colors[0].r + (string)f.colors[1].g + (string)f.colors[2].g)
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindTrace(globs, trace_stream);

    {
      var fn = new FuncBindSymbol("MakeFoo", globs.type("Foo"),
          delegate() { return new MakeFooNode(); } );
      fn.define(new FuncArgSymbol("conf", globs.type("Foo")));

      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(42));
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("14232342", str);
    CommonChecks(intp);
  }

  public class ConfigNode_Conf
  {
    public List<string> strs = new List<string>();
    public int hey;
    public List<Color> colors = new List<Color>();
    public Color sub_color = new Color();

    public void reset()
    {
      hey = 0;
      colors.Clear();
      sub_color = new Color();
      strs.Clear();
    }
  }

  public class ConfigNode : BehaviorTreeTerminalNode
  {
    public ConfigNode_Conf conf = new ConfigNode_Conf();

    Stream sm;

    public ConfigNode(Stream sm)
    {
      this.sm = sm;
    }

    public override void init(object agent)
    {
      var sw = new StreamWriter(sm);
      sw.Write(
        conf.hey + 
        (conf.colors.Count > 0 ? (":" + conf.colors.Count + ":" + conf.colors[0].r + ":" + conf.colors[1].g + ":" + conf.colors[2].g) : "") + 
        ":" + conf.sub_color.r + ":" + conf.sub_color.g + 
        ":" + string.Join(",", conf.strs));
      sw.Flush();
    }
  }

  public class ConfigNodeLambda : BehaviorTreeDecoratorNode
  {
    public FooLambda conf = new FooLambda();

    public override void init(object agent)
    {
      var fct = (FuncCtx)((BaseLambda)(conf.script[0])).fct.obj;
      var func_node = fct.GetNode();

      this.setSlave(func_node);

      base.init(agent);
    }

    public override void deinit(object agent)
    {
      var fct = (FuncCtx)((BaseLambda)(conf.script[0])).fct.obj;
      fct.RefDec();
    }
  }

  void BindConfigNode(GlobalScope globs, MemoryStream trace_stream)
  {
    {
      var cl = new ClassBindSymbol( "ConfigNode_Conf",
        delegate(ref DynVal v) 
        { 
          v.obj = new ConfigNode_Conf();
        }
      );
      globs.define(cl);
      globs.define(new ArrayTypeSymbolT<ConfigNode_Conf>(globs, cl));

      cl.define(new FieldSymbol("hey", globs.type("int"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var f = (ConfigNode_Conf)ctx.obj;
          v.SetNum(f.hey);
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var f = (ConfigNode_Conf)ctx.obj;
          f.hey = (int)v.num; 
          ctx.obj = f;
        }
      ));
      cl.define(new FieldSymbol("colors", globs.type("Color[]"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var f = (ConfigNode_Conf)ctx.obj;
          v.obj = f.colors;
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var f = (ConfigNode_Conf)ctx.obj;
          f.colors = (List<Color>)v.obj; 
          ctx.obj = f;
        }
      ));
      cl.define(new FieldSymbol("sub_color", globs.type("Color"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var f = (ConfigNode_Conf)ctx.obj;
          v.obj = f.sub_color;
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var f = (ConfigNode_Conf)ctx.obj;
          f.sub_color = (Color)v.obj; 
          ctx.obj = f;
        }
      ));
      cl.define(new FieldSymbol("strs", globs.type("string[]"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var f = (ConfigNode_Conf)ctx.obj;
          DynValList.Encode(f.strs, ref v);
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var f = (ConfigNode_Conf)ctx.obj;
          var tmp = f.strs;
          DynValList.Decode(v, ref tmp);
          ctx.obj = f;
        }
      ));
    }

    {
      var fn = new ConfNodeSymbol("ConfigNode", globs.type("void"),
          delegate() { var n = new ConfigNode(trace_stream); n.conf = new ConfigNode_Conf(); n.conf.reset(); return n; }, 
          delegate(BehaviorTreeNode n, ref DynVal v, bool reset) { var conf = ((ConfigNode)n).conf; v.obj = conf; if(reset) conf.reset(); }
          );
      fn.define(new FuncArgSymbol("c", globs.type("ConfigNode_Conf")));

      globs.define(fn);
    }

  }

  void BindConfigNodeLambda(GlobalScope globs)
  {
    {
      var fn = new ConfNodeSymbol("ConfigNodeLambda", globs.type("void"),
          delegate() { var n = new ConfigNodeLambda(); n.conf = new FooLambda(); n.conf.reset(); return n; }, 
          delegate(BehaviorTreeNode n, ref DynVal v, bool reset) { var conf = ((ConfigNodeLambda)n).conf; v.obj = conf; if(reset) conf.reset(); }
          );
      fn.define(new FuncArgSymbol("c", globs.type("FooLambda")));

      globs.define(fn);
    }

  }

  [IsTested()]
  public void TestConfigNodeArrayAsVar()
  {
    string bhl = @"
    func void test(float b) 
    {
      string[] strs = new string[]
      strs.Add(""foo"")
      strs.Add(""hey"")
      ConfigNode({hey:142, strs:strs, colors:[{r:2}, {g:3}, {g:b}], sub_color : {r:10, g:100}})
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindConfigNode(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(42));
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("142:3:2:3:42:10:100:foo,hey", str);
    CommonChecks(intp);
    AssertTrue(DynValList.PoolCount > 0);
    AssertEqual(DynValList.PoolCount, DynValList.PoolCountFree);
  }

  [IsTested()]
  public void TestConfigNodeArrayAsTemp()
  {
    string bhl = @"
    func void test(float b) 
    {
      ConfigNode({hey:142, strs:[""foo"", ""hey""], colors:[{r:2}, {g:3}, {g:b}], sub_color : {r:10, g:100}})
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindConfigNode(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(42));
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("142:3:2:3:42:10:100:foo,hey", str);
    CommonChecks(intp);
    AssertTrue(DynValList.PoolCount > 0);
    AssertEqual(DynValList.PoolCount, DynValList.PoolCountFree);
  }

  [IsTested()]
  public void TestConfigNodeWithEmptyConfig()
  {
    string bhl = @"
    func void test(float b) 
    {
      ConfigNode()
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindConfigNode(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(42));
    //NodeDump(node);
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("0:0:0:", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestConfigNodeWithConstConfig()
  {
    string bhl = @"
    func void test(float b) 
    {
      ConfigNode({hey:142, strs:[""foo"", ""hey""], sub_color : {r:10, g:100}})
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindConfigNode(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(42));
    intp.ExecNode(node, false);

    //NodeDump(node);

    var str = GetString(trace_stream);

    AssertEqual("142:10:100:foo,hey", str);
    CommonChecks(intp);
    AssertTrue(DynValList.PoolCount > 0);
    AssertEqual(DynValList.PoolCount, DynValList.PoolCountFree);
  }

  public class DummyModuleLoader : IModuleLoader
  {
    public AST_Module LoadModule(uint id)
    {
      return new AST_Module();
    }
  }

  [IsTested()]
  public void TestImport()
  {
    string bhl1 = @"
    import ""bhl2""  
    func float test(float k) 
    {
      return bar(k)
    }
    ";

    string bhl2 = @"
    import ""bhl3""  

    func float bar(float k)
    {
      return hey(k)
    }
    ";

    string bhl3 = @"
    func float hey(float k)
    {
      return k
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var mreg = new ModuleRegistry();

    var fp2src = new Dictionary<string, string>();
    fp2src.Add("bhl1", bhl1);
    fp2src.Add("bhl2", bhl2);
    fp2src.Add("bhl3", bhl3);
    mreg.test_sources = fp2src;
    var intp = Interpret(fp2src, globs, mreg, new DummyModuleLoader());

    var node = intp.GetFuncNode("bhl1", "test");
    node.SetArgs(DynVal.NewNum(23));
    //NodeDump(node);
    var res = intp.ExecNode(node, true).val;

    AssertEqual(res.num, 23);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestImportMixed()
  {
    string bhl1 = @"
    import ""bhl3""
    func float what(float k)
    {
      return hey(k)
    }

    import ""bhl2""  
    func float test(float k) 
    {
      return bar(k) * what(k)
    }

    ";

    string bhl2 = @"
    func float bar(float k)
    {
      return k
    }
    ";

    string bhl3 = @"
    func float hey(float k)
    {
      return k
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var mreg = new ModuleRegistry();

    var fp2src = new Dictionary<string, string>();
    fp2src.Add("bhl1", bhl1);
    fp2src.Add("bhl2", bhl2);
    fp2src.Add("bhl3", bhl3);
    mreg.test_sources = fp2src;
    var intp = Interpret(fp2src, globs, mreg, new DummyModuleLoader());

    var node = intp.GetFuncNode("bhl1", "test");
    node.SetArgs(DynVal.NewNum(2));
    //NodeDump(node);
    var res = intp.ExecNode(node, true).val;

    AssertEqual(res.num, 4);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestImportInterleaved()
  {
    string bhl1 = @"
    import ""bhl2""  
    import ""bhl3""  

    func float test(float k) 
    {
      return bar(k)
    }
    ";

    string bhl2 = @"
    import ""bhl3""  

    func float bar(float k)
    {
      return hey(k)
    }
    ";

    string bhl3 = @"
    import ""bhl2""

    func float hey(float k)
    {
      return k
    }
    ";


    var globs = SymbolTable.CreateBuiltins();

    var mreg = new ModuleRegistry();

    var fp2src = new Dictionary<string, string>();
    fp2src.Add("bhl1", bhl1);
    fp2src.Add("bhl2", bhl2);
    fp2src.Add("bhl3", bhl3);
    mreg.test_sources = fp2src;

    var intp = Interpret(fp2src, globs, mreg, new DummyModuleLoader());

    var node = intp.GetFuncNode("bhl1", "test");
    node.SetArgs(DynVal.NewNum(23));
    //NodeDump(node);
    var res = intp.ExecNode(node, true).val;

    AssertEqual(res.num, 23);
    CommonChecks(intp);
  }

  void BindForSlides(GlobalScope globs)
  {
    {
      var cl = new ClassBindSymbol("Vec3",
        delegate(ref DynVal v) 
        {}
      );

      globs.define(cl);

      {
        var m = new SimpleFuncBindSymbol("Sub", globs.type("Vec3"),
          delegate(object agent)
          {
            return BHS.SUCCESS;
          }
        );
        m.define(new FuncArgSymbol("val", globs.type("Vec3")));

        cl.define(m);
      }

      cl.define(new FieldSymbol("len", globs.type("float"),
        delegate(DynVal ctx, ref DynVal v)
        {},
        //setter not allowed
        null
      ));
    }

    {
      var cl = new ClassBindSymbol("Unit",
        delegate(ref DynVal v) 
        {}
      );

      globs.define(cl);
      globs.define(new ArrayTypeSymbol(globs, cl));

      cl.define(new FieldSymbol("position", globs.type("Vec3"),
        delegate(DynVal ctx, ref DynVal v)
        {},
        //setter not allowed
        null
      ));
    }

    {
      var fn = new SimpleFuncBindSymbol("get_units", globs.type("Unit[]"),
        delegate(object agent)
        {
          return BHS.SUCCESS;
        }
      );

      globs.define(fn);
    }

  }

  [IsTested()]
  public void TestForSlides()
  {
    string bhl = @"
func Unit FindUnit(Vec3 pos, float radius) {
  Unit[] us = get_units()
  int i = 0
  while(i < us.Count) {
    Unit u = us.At(i)
    if(u.position.Sub(pos).len < radius) {
      return u
    } 
    i = i + 1
  }
  return null
}
";
    var globs = SymbolTable.CreateBuiltins();

    BindForSlides(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("FindUnit");
    //NodeDump(node);
  }

  public class TestNode : BehaviorTreeTerminalNode
  {
    public BHS status = BHS.SUCCESS;
    public int execs;
    public int inits;
    public int deinits;
    public int defers;

    public override BHS execute(object agent)
    {
      ++execs;
      return status;
    }

    public override void init(object agent)
    {
      ++inits;
    }

    public override void deinit(object agent)
    {
      ++deinits;
    }

    public override void defer(object agent)
    {
      ++defers;
    }
  }

  public class DecoratorTestNode : BehaviorTreeDecoratorNode
  {
    public DecoratorTestNode(TestNode t)
    {
      setSlave(t);
    }
  }

  [IsTested()]
  public void TestNodeSequenceSuccess()
  {
    var t1 = new TestNode();
    var t2 = new TestNode();
    var s = new SequentialNode();
    s.children.Add(t1);
    s.children.Add(t2);

    t1.status = BHS.RUNNING;

    AssertTrue(s.run(null) == BHS.RUNNING);
    AssertEqual(t1.inits, 1);
    AssertEqual(t1.execs, 1);
    AssertEqual(t1.deinits, 0);
    AssertEqual(t1.defers, 0);
    AssertEqual(t2.inits, 0);
    AssertEqual(t2.execs, 0);
    AssertEqual(t2.deinits, 0);
    AssertEqual(t2.defers, 0);

    t1.status = BHS.SUCCESS;

    AssertTrue(s.run(null) == BHS.SUCCESS);
    AssertEqual(t1.inits, 1);
    AssertEqual(t1.execs, 2);
    AssertEqual(t1.deinits, 1);
    AssertEqual(t1.defers, 1);
    AssertEqual(t2.inits, 1);
    AssertEqual(t2.execs, 1);
    AssertEqual(t2.deinits, 1);
    AssertEqual(t2.defers, 1);

    s.stop(null);
    AssertEqual(t1.deinits, 1);
    AssertEqual(t1.defers, 1);
    AssertEqual(t2.defers, 1);
    AssertEqual(t2.deinits, 1);
  }

  [IsTested()]
  public void TestDecoratorNode()
  {
    var t = new TestNode();
    t.status = BHS.RUNNING;
    var d = new DecoratorTestNode(t);

    AssertTrue(d.run(null) == BHS.RUNNING);
    AssertEqual(t.inits, 1);
    AssertEqual(t.execs, 1);
    AssertEqual(t.deinits, 0);
    AssertEqual(t.defers, 0);

    t.status = BHS.SUCCESS;
    AssertTrue(d.run(null) == BHS.SUCCESS);
    AssertEqual(t.inits, 1);
    AssertEqual(t.execs, 2);
    AssertEqual(t.deinits, 1);
    AssertEqual(t.defers, 0);

    d.stop(null);
    AssertEqual(t.defers, 1);
  }

  [IsTested()]
  public void TestRefCountSimple()
  {
    string bhl = @"
    func void test() 
    {
      RefC r = new RefC
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;INC1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountReturnResult()
  {
    string bhl = @"
    func RefC test() 
    {
      return new RefC
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountAssignSame()
  {
    string bhl = @"
    func void test() 
    {
      RefC r1 = new RefC
      RefC r2 = r1
      r2 = r1
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("INC1;DEC0;INC1;INC2;DEC1;INC2;INC3;DEC2;INC3;DEC2;REL2;DEC1;REL1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountAssignSelf()
  {
    string bhl = @"
    func void test() 
    {
      RefC r1 = new RefC
      r1 = r1
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("INC1;DEC0;INC1;INC2;DEC1;INC2;DEC1;REL1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountAssignOverwrite()
  {
    string bhl = @"
    func void test() 
    {
      RefC r1 = new RefC
      RefC r2 = new RefC
      r1 = r2
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("INC1;DEC0;INC1;INC1;DEC0;INC1;INC2;DEC1;INC2;DEC0;REL0;DEC1;REL1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountSeveral()
  {
    string bhl = @"
    func void test() 
    {
      RefC r1 = new RefC
      RefC r2 = new RefC
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;INC1;INC1;DEC0;INC1;DEC0;REL0;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountInLambda()
  {
    string bhl = @"
    func void test() 
    {
      RefC r1 = new RefC

      trace(""REFS"" + (string)r1.refs + "";"")

      void^() fn = func() {
        trace(""REFS"" + (string)r1.refs + "";"")
      }
      
      fn()
      trace(""REFS"" + (string)r1.refs + "";"")
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("INC1;DEC0;INC1;INC2;DEC1;REFS1;INC2;INC3;INC4;DEC3;REFS3;DEC2;REL2;INC3;DEC2;REFS2;DEC1;REL1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountInArray()
  {
    string bhl = @"
    func void test() 
    {
      RefC[] rs = new RefC[]
      rs.Add(new RefC)
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;INC1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountSeveralInArrayAccess()
  {
    string bhl = @"
    func void test() 
    {
      RefC[] rs = new RefC[]
      rs.Add(new RefC)
      rs.Add(new RefC)
      float refs = rs[1].refs
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;INC1;INC1;DEC0;INC1;INC2;DEC1;DEC0;REL0;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountReturn()
  {
    string bhl = @"

    func RefC make()
    {
      RefC c = new RefC
      return c
    }

    func void test() 
    {
      RefC c1 = make()
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;INC1;INC2;DEC1;REL1;DEC0;INC1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountPass()
  {
    string bhl = @"

    func void foo(RefC c)
    { 
      trace(""HERE;"")
    }

    func void test() 
    {
      foo(new RefC)
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;INC1;HERE;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountReturnPass()
  {
    string bhl = @"

    func RefC make()
    {
      RefC c = new RefC
      return c
    }

    func void foo(RefC c)
    {
      RefC c2 = c
    }

    func void test() 
    {
      RefC c1 = make()
      foo(c1)
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;INC1;INC2;DEC1;REL1;DEC0;INC1;INC2;DEC1;INC2;INC3;DEC2;INC3;DEC2;REL2;DEC1;REL1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountInConfig()
  {
    string bhl = @"
    func void test() 
    {
      NodeRefC({r : new RefC})
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    //NodeDump(node);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;WRITE!REL0;NODE!", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountInConfigPassReturn()
  {
    string bhl = @"
    func void pass(RefC r)
    {
      NodeRefC({r : r})
    }

    func void test() 
    {
      RefC r = new RefC
      pass(r)
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, false);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;INC1;INC2;DEC1;INC2;INC3;DEC2;WRITE!REL2;NODE!DEC1;REL1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  ////////////////////////////////////////////////
  
  static void AssertEqual(double a, double b)
  {
    if(!(a == b))
      throw new Exception("Assertion failed: " + a + " != " + b);
  }

  static void AssertEqual(BHS a, BHS b)
  {
    if(!(a == b))
      throw new Exception("Assertion failed: " + a + " != " + b);
  }

  static void AssertEqual(string a, string b)
  {
    if(!(a == b))
      throw new Exception("Assertion failed: " + a + " != " + b);
  }

  static void AssertEqual(int a, int b)
  {
    if(!(a == b))
      throw new Exception("Assertion failed: " + a + " != " + b);
  }

  static void AssertTrue(bool cond, string msg = "")
  {
    if(!cond)
      throw new Exception("Assertion failed" + (msg.Length > 0 ? (": " + msg) : ""));
  }

  void AssertError<T>(Action action, string msg) where T : Exception
  {
    Exception err = null;
    try
    {
      action();
    }
    catch(T e)
    {
      err = e;
    }

    AssertTrue(err != null, "Error didn't occur"); 
    var idx = err.Message.IndexOf(msg);
    if(idx == -1)
      Console.WriteLine(err.Message);
    AssertTrue(idx != -1);
  }

  static Interpreter Interpret(string fpath, string src, GlobalScope globs = null, ModuleRegistry mreg = null, IModuleLoader mloader = null, bool show_ast = false)
  {
    var dict = new Dictionary<string, string>();
    dict.Add(fpath, src);
    return Interpret(dict, globs, mreg, mloader, show_ast);
  }

  static Interpreter Interpret(Dictionary<string, string> fpath2src, GlobalScope globs = null, ModuleRegistry mreg = null, IModuleLoader mloader = null, bool show_ast = false)
  {
    Util.DEBUG = true;

    DynVal.PoolClear();
    DynValList.PoolClear();
    FuncCallNode.PoolClear();
    FuncCtx.PoolClear();

    globs = globs == null ? SymbolTable.CreateBuiltins() : globs;

    var intp = Interpreter.instance;
    intp.Init(globs, mloader);

    mreg = mreg == null ? new ModuleRegistry() : mreg;

    foreach(var item in fpath2src)
    {
      var bin = new MemoryStream();
      var mod = new bhl.Module(item.Key, item.Key);
      AST_Builder.Source2Bin(mod, item.Value.ToStream(), bin, globs, mreg);
      bin.Position = 0;

      AST_Module ast = Util.Bin2Meta<AST_Module>(bin);
      if(show_ast)
        ASTDump(ast);

      intp.Interpret(ast);
    }

    return intp;
  }

  public static void ASTDump(AST ast)
  {
    new AST_Dumper().Visit(ast);
    Console.WriteLine("\n=============");
  }

  public static void NodeDump(BehaviorTreeNode node, object agent = null, bool only_running = false, int level = 0, bool is_term = true)
  {
    var fnode = node as FuncNodeAST;
    if(fnode != null)
      fnode.Inflate();

    var name = node.GetType().Name;
    var status = node.getStatus();

    var spaces = new String('_', level);
    var indent_str = spaces + (is_term ? "`" : "|");
    Console.WriteLine(indent_str + "-" + name + " (" + status + ") " + node.inspect(agent));

    if(!only_running || (only_running && status == BHS.RUNNING))
    {
      var inode = node as BehaviorTreeInternalNode;
      if(inode != null)
      {
        for(int i=0;i<inode.children.Count;++i)
        {
          var child = inode.children[i]; 
          NodeDump(child, agent, only_running, level + 1, i == inode.children.Count-1);
        }
      }
    }
  }

  void CommonChecks(Interpreter intp)
  {
    AssertEqual(intp.StackCount(), 0);
    AssertEqual(DynVal.PoolCount, DynVal.PoolCountFree);
  }

  static double ExtractNum(Interpreter.Result res)
  {
    return res.val.num;
  }

  static string ExtractStr(Interpreter.Result res)
  {
    return res.val.str;
  }
}

public class BHL_TestRunner
{
  public static void Main(string[] args)
  {
    Console.WriteLine("Testing BHL");

    var test = new BHL_Test();

    int c = 0;
    foreach(var method in (typeof(BHL_Test)).GetMethods())
    {
      if(IsMemberTested(method))
      {
        Util.SetupAutogenFactory();
        if(IsAllowedToRun(args, method))
        {
          ++c;
          method.Invoke(test, new object[] {});
        }
      }
    }
    Console.WriteLine("Done running "  + c + " tests");
  }

  static bool IsAllowedToRun(string[] args, MemberInfo member)
  {
    if(args == null || args.Length == 0)
      return true;

    for(int i=0;i<args.Length;++i)
    {
      if(args[i] == member.Name)
        return true;
    }

    return false;
  }

  static bool IsMemberTested(MemberInfo member)
  {
    foreach(var attribute in member.GetCustomAttributes(true))
    {
      if(attribute is IsTestedAttribute)
        return true;
    }
    return false;
  }
}

