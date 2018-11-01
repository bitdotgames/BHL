using System;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections;
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
      
    func int test() 
    {
      return 100
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var n = ExtractNum(intp.ExecNode(node));

    AssertEqual(n, 100);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNumberPrecision()
  {
    DoTestReturnNum(bhlnum: "100", expected_num: 100);
    DoTestReturnNum(bhlnum: "2147483647", expected_num: 2147483647);
    DoTestReturnNum(bhlnum: "2147483648", expected_num: 2147483648);
    DoTestReturnNum(bhlnum: "100.5", expected_num: 100.5);
    DoTestReturnNum(bhlnum: "2147483648.1", expected_num: 2147483648.1);
  }

  void DoTestReturnNum(string bhlnum, double expected_num) 
  {
    string bhl = @"
      
    func float test() 
    {
      return "+bhlnum+@"
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(num, expected_num);
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
  public void TestReturnBoolFalseNegated()
  {
    string bhl = @"
      
    func bool test() 
    {
      return !false
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnBoolTrueNegated()
  {
    string bhl = @"
      
    func bool test() 
    {
      return !true
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
      "symbol not resolved"
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
  public void TestUserFuncBinding()
  {
    string bhl = @"
      
    func void test() 
    {
      trace(""HERE"")
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);
    //NodeDump(node);

    var str = GetString(trace_stream);

    AssertEqual("HERE", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserFuncBindWithoutArgs()
  {
    string bhl = @"
      
    func int test() 
    {
      int n = answer42()
      return n
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindAnswer42(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(42, num);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserFuncBindConflict()
  {
    string bhl = @"

    func trace(string hey)
    {
    }
      
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "already defined symbol 'trace'"
    );
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
  public void TestFuncManyDefaultArgs()
  {
    string bhl = @"

    func float foo(
          float k1 = 1, float k2 = 1, float k3 = 1, float k4 = 1, float k5 = 1, float k6 = 1, float k7 = 1, float k8 = 1, float k9 = 1,
          float k10 = 1, float k11 = 1, float k12 = 1, float k13 = 1, float k14 = 1, float k15 = 1, float k16 = 1, float k17 = 1, float k18 = 1,
          float k19 = 1, float k20 = 1, float k21 = 1, float k22 = 1, float k23 = 1, float k24 = 1, float k25 = 1, float k26 = 42
        )
    {
      return k26
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
  public void TestFuncTooManyDefaultArgs()
  {
    string bhl = @"

    func float foo(
          float k1 = 1, float k2 = 1, float k3 = 1, float k4 = 1, float k5 = 1, float k6 = 1, float k7 = 1, float k8 = 1, float k9 = 1,
          float k10 = 1, float k11 = 1, float k12 = 1, float k13 = 1, float k14 = 1, float k15 = 1, float k16 = 1, float k17 = 1, float k18 = 1,
          float k19 = 1, float k20 = 1, float k21 = 1, float k22 = 1, float k23 = 1, float k24 = 1, float k25 = 1, float k26 = 1, float k27 = 1
        )
    {
      return k27
    }
      
    func float test() 
    {
      return foo()
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      "max default arguments reached"
    );
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
      "missing argument 'k'"
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
      "missing argument 'radius'"
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
      "f: no such named argument"
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

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var res = intp.ExecNode(node).val;

    //NodeDump(node);

    var lst = res.obj as DynValList;
    AssertEqual(lst.Count, 2);
    AssertEqual(lst[0].str, "foo");
    AssertEqual(lst[1].str, "bar");
    lst.TryDel();
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestStringArrayIndex()
  {
    string bhl = @"
      
    func string test() 
    {
      string[] arr = new string[]
      arr.Add(""bar"")
      arr.Add(""foo"")
      return arr[1]
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var res = ExtractStr(intp.ExecNode(node));

    //NodeDump(node);

    AssertEqual(res, "foo");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestStringArrayAssign()
  {
    string bhl = @"
      
    func string[] test() 
    {
      string[] arr = new string[]
      arr.Add(""foo"")
      arr[0] = ""bar""
      return arr
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    //NodeDump(node);

    var res = intp.ExecNode(node).val;

    var lst = res.obj as DynValList;
    AssertEqual(lst.Count, 1);
    AssertEqual(lst[0].str, "bar");
    lst.TryDel();
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRemoveFromArray()
  {
    string bhl = @"
      
    func string[] test() 
    {
      string[] arr = new string[]
      arr.Add(""foo"")
      arr.Add(""bar"")
      arr.RemoveAt(1)
      return arr
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var res = intp.ExecNode(node).val;

    //NodeDump(node);

    var lst = res.obj as DynValList;
    AssertEqual(lst.Count, 1);
    AssertEqual(lst[0].str, "foo");
    lst.TryDel();
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTempArrayIdx()
  {
    string bhl = @"

    func int[] mkarray()
    {
      int[] arr = new int[]
      arr.Add(1)
      arr.Add(100)
      return arr
    }
      
    func int test() 
    {
      return mkarray()[1]
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 100);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTempArrayCount()
  {
    string bhl = @"

    func int[] mkarray()
    {
      int[] arr = new int[]
      arr.Add(1)
      arr.Add(100)
      return arr
    }
      
    func int test() 
    {
      return mkarray().Count
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 2);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTempArrayRemoveAt()
  {
    string bhl = @"

    func int[] mkarray()
    {
      int[] arr = new int[]
      arr.Add(1)
      arr.Add(100)
      return arr
    }
      
    func void test() 
    {
      mkarray().RemoveAt(0)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    intp.ExecNode(node, 0);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTempArrayAdd()
  {
    string bhl = @"

    func int[] mkarray()
    {
      int[] arr = new int[]
      arr.Add(1)
      arr.Add(100)
      return arr
    }
      
    func void test() 
    {
      mkarray().Add(300)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    intp.ExecNode(node, 0);

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
  public void TestPassByRefNonAssignedValue()
  {
    string bhl = @"

    func foo(ref float a) 
    {
      a = a + 1
    }
      
    func float test() 
    {
      float k
      foo(ref k)
      return k
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassByRefAlreadyDefinedError()
  {
    string bhl = @"

    func foo(ref float a, float a) 
    {
      a = a + 1
    }
      
    func float test(float k) 
    {
      foo(ref k, k)
      return k
    }
    ";

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl);
      },
      "already defined symbol 'a'"
    );
  }

  [IsTested()]
  public void TestPassByRefAssignToNonRef()
  {
    string bhl = @"

    func foo(ref float a) 
    {
      float b = a
      b = b + 1
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

    AssertEqual(num, 3);
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
  public void TestPassByRefInUserBinding()
  {
    string bhl = @"

    func float test(float k) 
    {
      func_with_ref(k, ref k)
      return k
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new SimpleFuncBindSymbol("func_with_ref", globs.type("void"), 
          delegate()
          {
            var interp = Interpreter.instance;
            var b = interp.PopRef();
            var a = interp.PopValue().num;

            b.num = a * 2;

            return BHS.SUCCESS;
          }
          );
      fn.define(new FuncArgSymbol("a", globs.type("float")));
      fn.define(new FuncArgSymbol("b", globs.type("float"), true/*is ref*/));

      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);

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
  public void TestPassByRefAndThenReturn()
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
      "mismatched input 'ref'"
    );
  }

  [IsTested()]
  public void TestPassByRefDefaultArgsNotAllowed()
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
      "'ref' is not allowed to have a default value"
    );
  }

  [IsTested()]
  public void TestPassByRefNullValue()
  {
    string bhl = @"

    func float foo(ref float k = null)
    {
      if((any)k != null) {
        k = k + 1
        return k
      } else {
        return 1
      }
    }

    func float,float test() 
    {
      float res = 0
      float k = 10
      res = res + foo()
      res = res + foo(ref k)
      return res,k
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = intp.ExecNode(node, 2); 
    //NodeDump(node);

    AssertEqual(res.vals[0].num, 12);
    AssertEqual(res.vals[1].num, 11);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassByRefLiteralNotAllowed()
  {
    string bhl = @"

    void foo(ref int a) 
    {
    }

    func void test() 
    {
      foo(ref 10)  
    }
    ";

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl);
      },
      "mismatched input '('"
    );
  }

  [IsTested()]
  public void TestPassByRefClassField()
  {
    string bhl = @"

    class Bar
    {
      float a
    }

    func foo(ref float a) 
    {
      a = a + 1
    }
      
    func float test() 
    {
      Bar b = { a: 10}

      foo(ref b.a)
      return b.a
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 11);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassByRefArrayItem()
  {
    string bhl = @"

    func foo(ref float a) 
    {
      a = a + 1
    }
      
    func float test() 
    {
      float[] fs = [1,10,20]

      foo(ref fs[1])
      return fs[0] + fs[1] + fs[2]
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 32);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassByRefArrayObj()
  {
    string bhl = @"

    class Bar
    {
      float f
    }

    func foo(ref float a) 
    {
      a = a + 1
    }
      
    func float test() 
    {
      Bar[] bs = [{f:1},{f:10},{f:20}]

      foo(ref bs[1].f)
      return bs[0].f + bs[1].f + bs[2].f
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 32);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassByRefTmpClassField()
  {
    string bhl = @"

    class Bar
    {
      float a
    }

    func float foo(ref float a) 
    {
      a = a + 1
      return a
    }
      
    func float test() 
    {
      return foo(ref (new Bar).a)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassByRefClassFieldNested()
  {
    string bhl = @"

    class Wow
    {
      float c
    }

    class Bar
    {
      Wow w
    }

    func foo(ref float a) 
    {
      a = a + 1
    }
      
    func float test() 
    {
      Bar b = { w: { c : 4} }

      foo(ref b.w.c)
      return b.w.c
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 5);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassByRefClassFieldFuncPtr()
  {
    string bhl = @"

    class Bar
    {
      int^() p
    }

    func int _5()
    {
      return 5
    }

    func int _10()
    {
      return 10
    }

    func foo(ref int^() p) 
    {
      p = _10
    }
      
    func int test() 
    {
      Bar b = { p: _5}

      foo(ref b.p)
      return b.p()
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassByRefBindClassFieldNotSupported()
  {
    string bhl = @"

    func foo(ref float a) 
    {
      a = a + 1
    }
      
    func float test() 
    {
      Color c = new Color

      foo(ref c.r)
      return c.r
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl, globs);
      },
      "getting field by 'ref' not supported"
    );
  }

  [IsTested()]
  public void TestLambdaUsesValueImplicit()
  {
    string bhl = @"

    func foo(void^() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float a = 2
      foo(func() { a = a + 1 } )
      return a
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaUsesArrayImplicit()
  {
    string bhl = @"

    func foo(void^() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float[] a = []
      foo(func() { a.Add(10) } )
      return a[0]
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaUsesByRefExplicit()
  {
    string bhl = @"

    func foo(void^() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float a = 2
      foo(func() use(ref a) { a = a + 1 } )
      return a
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 3);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncReplacesArrayValueByRef()
  {
    string bhl = @"

    func float[] make()
    {
      float[] fs = [42]
      return fs
    }

    func foo(ref float[] a) 
    {
      a = make()
    }
      
    func float test() 
    {
      float[] a
      foo(ref a)
      return a[0]
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncReplacesArrayValueByRef2()
  {
    string bhl = @"

    func foo(ref float[] a) 
    {
      a = [42]
    }
      
    func float test() 
    {
      float[] a = []
      foo(ref a)
      return a[0]
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);

    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncReplacesArrayValueByRef3()
  {
    string bhl = @"

    func foo(ref float[] a) 
    {
      a = [42]
      float[] b = a
    }
      
    func float test() 
    {
      float[] a = []
      foo(ref a)
      return a[0]
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);

    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassArrayToFunctionByValue()
  {
    string bhl = @"

    func foo(int[] a)
    {
      a = [100]
    }
      
    func int test() 
    {
      int[] a = [1, 2]

      foo(a)

      return a[0]
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaReplacesArrayValueByRef()
  {
    string bhl = @"

    func foo(void^() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float[] a
      foo(func() use(ref a) { 
          a = [42]
        } 
      )
      return a[0]
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaReplacesArrayValueByRef2()
  {
    string bhl = @"

    func foo(void^() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float[] a = []
      foo(func() use(ref a) { 
          a = [42]
        } 
      )
      return a[0]
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncReplacesFuncPtrByRef()
  {
    string bhl = @"

    func float bar() 
    { 
      return 42
    } 

    func foo(ref float^() a) 
    {
      a = bar
    }
      
    func float test() 
    {
      float^() a
      foo(ref a)
      return a()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncReplacesFuncPtrByRef2()
  {
    string bhl = @"

    func float bar() 
    { 
      return 42
    } 

    func foo(ref float^() a) 
    {
      a = bar
    }
      
    func float test() 
    {
      float^() a = func float() { return 45 } 
      foo(ref a)
      return a()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaReplacesFuncPtrByRef()
  {
    string bhl = @"

    func foo(void^() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float^() a
      foo(func() use(ref a) { 
          a = func float () { return 42 }
        } 
      )
      return a()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaReplacesFuncPtrByRef2()
  {
    string bhl = @"

    func foo(void^() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float^() a = func float() { return 45 } 
      foo(func() use(ref a) { 
          a = func float () { return 42 }
        } 
      )
      return a()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaUseByValue()
  {
    string bhl = @"

    func foo(void^() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float a = 2
      foo(func() use(a) { a = a + 1 } )
      return a
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaUseMixed()
  {
    string bhl = @"

    func foo(void^() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float a = 2
      float b = 10
      foo(func() use(a, ref b) 
        { 
          a = a + 1 
          b = b * 2
        } 
      )
      return a + b
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 22);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaUseNested()
  {
    string bhl = @"

    func foo(void^() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float a = 2
      float b = 10
      foo(func() use(a, ref b) 
        { 
          void^() fn = func() use(ref a, ref b) {
            a = a + 1 
            b = b * 2
          }
          fn()
        } 
      )
      return a + b
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 22);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaUseNestedOverride()
  {
    string bhl = @"

    func foo(void^() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float a = 2
      float b = 10
      foo(func() use(a, ref b) 
        { 
          void^() fn = func() use(ref a, b) {
            a = a + 1 
            b = b * 2
          }
          fn()
        } 
      )
      return a + b
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 12);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaUseParamDoesntExist()
  {
    string bhl = @"

    func foo(void^() fn) 
    {
      fn()
    }
      
    func void test() 
    {
      foo(func() use(a) { a = a + 1 } )
    }
    ";

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl);
      },
      "symbol 'a' not defined in parent scope"
    );
  }

  [IsTested()]
  public void TestLambdaDoubleUseParam()
  {
    string bhl = @"

    func foo(void^() fn) 
    {
      fn()
    }
      
    func void test() 
    {
      float a = 1
      foo(func() use(a, a) { a = a + 1 } )
    }
    ";

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl);
      },
      "already defined symbol 'a'"
    );
  }

  [IsTested()]
  public void TestLambdaDoubleUseParam2()
  {
    string bhl = @"

    func foo(void^() fn) 
    {
      fn()
    }
      
    func void test() 
    {
      float a = 1
      foo(func() use(a, ref a) { a = a + 1 } )
    }
    ";

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl);
      },
      "already defined symbol 'a'"
    );
  }

  [IsTested()]
  public void TestLambdaUseReserveValueUpfront()
  {
    string bhl = @"

    func foo(void^() fn) 
    {
      fn()
    }
      
    func float test() 
    {
      float a = 2
      paral_all {
        foo(func() use(a) { 
          WaitTicks(2, true)
          a = a + 1 
          trace(""A:"" + (string)a)
        } )
        seq { a = 100 }
      }
      return a
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();
    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run();
      AssertEqual(BHS.SUCCESS, status);
    }

    var num = intp.PopValue().num;

    AssertEqual(num, 100);
    var str = GetString(trace_stream);
    AssertEqual("A:3", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaUseReserveValueUpfrontForUserBind()
  {
    string bhl = @"

    func float test() 
    {
      float a = 2
      paral_all {
        StartScript(func() use(a) { 
          WaitTicks(2, true)
          a = a + 1 
          trace(""A:"" + (string)a)
        } )
        seq { a = 100 }
      }
      return a
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();
    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);
    BindStartScript(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run();
      AssertEqual(BHS.SUCCESS, status);
    }

    var num = intp.PopValue().num;

    AssertEqual(num, 100);
    var str = GetString(trace_stream);
    AssertEqual("A:3", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaSelfCallAndBindValues()
  {
    string bhl = @"

    func float test() 
    {
      float a = 2

      float res

      void^() fn = func void^() (float a, int b) use(ref res)  
      { 
        return func() use(ref res) 
        { 
          res = a + b 
        }
      }(a, 1) 

      a = 100

      fn()

      return res
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 3);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestClosure()
  {
    string bhl = @"

    func float test() 
    {
      //TODO: need more flexible types support for this:
      //float^(float)^(float)
      
      return func float^(float) (float a) {
        return func float (float b) { return a + b }
      }(2)(3)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 5);
    CommonChecks(intp);
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
    public override void init()
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
          delegate()
          {
            var interp = Interpreter.instance;
            var b = interp.GetFuncArgsInfo().IsDefaultArgUsed(0) ? 2 : interp.PopValue().num;
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
          delegate()
          {
            var interp = Interpreter.instance;
            var b = interp.GetFuncArgsInfo().IsDefaultArgUsed(0) ? 2 : interp.PopValue().num;
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
          delegate()
          {
            var interp = Interpreter.instance;
            var a = interp.GetFuncArgsInfo().IsDefaultArgUsed(0) ? 14 : interp.PopValue().num;

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
  public void TestBindFunctionWithDefaultArgsOmittingSome()
  {
    string bhl = @"
      
    func float test(int k) 
    {
      return func_with_def(b : k)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new SimpleFuncBindSymbol("func_with_def", globs.type("float"), 
          delegate()
          {
            var interp = Interpreter.instance;
            var arinfo = interp.GetFuncArgsInfo();
            var b = arinfo.IsDefaultArgUsed(1) ? 2 : interp.PopValue().num;
            var a = arinfo.IsDefaultArgUsed(0) ? 10 : interp.PopValue().num;

            interp.PushValue(DynVal.NewNum(a + b));

            return BHS.SUCCESS;
          },
          2);
      fn.define(new FuncArgSymbol("a", globs.type("int")));
      fn.define(new FuncArgSymbol("b", globs.type("int")));

      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    node.SetArgs(DynVal.NewNum(42));
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 52);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnFailureFirst()
  {
    string bhl = @"

    func float foo()
    {
      fail()
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
    var res = intp.ExecNode(node, 0);

    AssertEqual(res.status, BHS.FAILURE);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFailureInBindFunction()
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
          delegate() { return BHS.FAILURE; } );
      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var res = intp.ExecNode(node, 0);

    AssertEqual(res.status, BHS.FAILURE);
    CommonChecks(intp);
  }

  public class Color
  {
    public float r;
    public float g;

    public override string ToString()
    {
      return "[r="+r+",g="+g+"]";
    }
  }

  public class ColorAlpha : Color
  {
    public float a;

    public override string ToString()
    {
      return "[r="+r+",g="+g+",a="+a+"]";
    }
  }

  public class ColorNested
  {
    public Color c = new Color();
  }

  public class StringClass
  {
    public string str;
  }

  public struct IntStruct
  {
    public int n;
    public int n2;

    public static void Decode(DynVal dv, ref IntStruct dst)
    {
      dst.n = (int)dv._num;
      dst.n2 = (int)dv._num2;
    }

    public static void Encode(DynVal dv, IntStruct dst)
    {
      dv._type = DynVal.ENCODED;
      dv._num = dst.n;
      dv._num2 = dst.n2;
    }
  }

  public struct MasterStruct
  {
    public StringClass child;
    public StringClass child2;
    public IntStruct child_struct;
    public IntStruct child_struct2;
  }

  public class MkColorNode : BehaviorTreeTerminalNode
  {
    public override void init()
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
      globs.define(new ArrayTypeSymbolT<Color>(globs, new TypeRef(cl), delegate() { return new List<Color>(); } ));

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
          delegate()
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
          delegate()
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

    {
      var fn = new SimpleFuncBindSymbol("mkcolor_null", globs.type("Color"),
          delegate() { 
            var interp = Interpreter.instance;
            var dv = DynVal.New();
            dv.obj = null;
            interp.PushValue(dv);
            return BHS.SUCCESS;
          }
      );

      globs.define(fn);
    }
  }

  void BindStringClass(GlobalScope globs)
  {
    {
      var cl = new ClassBindSymbol("StringClass",
        delegate(ref DynVal v) 
        { 
          v.obj = new StringClass();
        }
      );

      globs.define(cl);

      cl.define(new FieldSymbol("str", globs.type("string"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var c = (StringClass)ctx.obj;
          v.str = c.str;
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var c = (StringClass)ctx.obj;
          c.str = v._str; 
          ctx.obj = c;
        }
      ));
    }
  }

  void BindIntStruct(GlobalScope globs)
  {
    {
      var cl = new ClassBindSymbol("IntStruct",
        delegate(ref DynVal v) 
        { 
          var s = new IntStruct();
          IntStruct.Encode(v, s);
        }
      );

      globs.define(cl);

      cl.define(new FieldSymbol("n", globs.type("int"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var s = new IntStruct();
          IntStruct.Decode(ctx, ref s);
          v.num = s.n;
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var s = new IntStruct();
          IntStruct.Decode(ctx, ref s);
          s.n = (int)v.num;
          IntStruct.Encode(ctx, s);
        }
      ));

      cl.define(new FieldSymbol("n2", globs.type("int"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var s = new IntStruct();
          IntStruct.Decode(ctx, ref s);
          v.num = s.n2;
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var s = new IntStruct();
          IntStruct.Decode(ctx, ref s);
          s.n2 = (int)v.num;
          IntStruct.Encode(ctx, s);
        }
      ));
    }
  }

  void BindMasterStruct(GlobalScope globs)
  {
    BindStringClass(globs);
    BindIntStruct(globs);

    {
      var cl = new ClassBindSymbol("MasterStruct",
        delegate(ref DynVal v) 
        { 
          var o = new MasterStruct();
          o.child = new StringClass();
          o.child2 = new StringClass();
          v.obj = o;
        }
      );

      globs.define(cl);

      cl.define(new FieldSymbol("child", globs.type("StringClass"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var c = (MasterStruct)ctx.obj;
          v.SetObj(c.child);
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var c = (MasterStruct)ctx.obj;
          c.child = (StringClass)v._obj; 
          ctx.obj = c;
        }
      ));

      cl.define(new FieldSymbol("child2", globs.type("StringClass"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var c = (MasterStruct)ctx.obj;
          v.obj = c.child2;
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var c = (MasterStruct)ctx.obj;
          c.child2 = (StringClass)v.obj; 
          ctx.obj = c;
        }
      ));

      cl.define(new FieldSymbol("child_struct", globs.type("IntStruct"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct.Encode(v, c.child_struct);
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct s = new IntStruct();
          IntStruct.Decode(v, ref s);
          c.child_struct = s;
          ctx.obj = c;
        }
      ));

      cl.define(new FieldSymbol("child_struct2", globs.type("IntStruct"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct.Encode(v, c.child_struct2);
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct s = new IntStruct();
          IntStruct.Decode(v, ref s);
          c.child_struct2 = s;
          ctx.obj = c;
        }
      ));

    }
  }

  void BindColorAlpha(GlobalScope globs, bool bind_parent = true)
  {
    if(bind_parent)
      BindColor(globs);

    {
      var cl = new ClassBindSymbol("ColorAlpha", globs.type("Color"),
        delegate(ref DynVal v) 
        { 
          v.obj = new ColorAlpha();
        }
      );

      globs.define(cl);

      cl.define(new FieldSymbol("a", globs.type("float"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var c = (ColorAlpha)ctx.obj;
          v.SetNum(c.a);
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var c = (ColorAlpha)ctx.obj;
          c.a = (float)v.num; 
          ctx.obj = c;
        }
      ));

      {
        var m = new SimpleFuncBindSymbol("mult_summ_alpha", globs.type("float"),
          delegate()
          {
            var interp = Interpreter.instance;

            var c = (ColorAlpha)interp.PopValue().obj;

            interp.PushValue(DynVal.NewNum((c.r * c.a) + (c.g * c.a)));

            return BHS.SUCCESS;
          }
        );

        cl.define(m);
      }
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

    public void Retain()
    {
      var sw = new StreamWriter(stream);
      ++refs;
      sw.Write("INC" + refs + ";");
      sw.Flush();
    }

    public void Release(bool can_del = true)
    {
      var sw = new StreamWriter(stream);
      --refs;
      sw.Write("DEC" + refs + ";");
      sw.Flush();
      if(can_del)
        TryDel();
    }

    public bool TryDel()
    {
      var sw = new StreamWriter(stream);
      sw.Write("REL" + refs + ";");
      sw.Flush();
      return refs == 0;
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
      globs.define(new GenericArrayTypeSymbol(globs, new TypeRef(cl)));
    }
  }

  void BindFoo(GlobalScope globs)
  {
    {
      var cl = new ClassBindSymbol("Foo",
        delegate(ref DynVal v) 
        { 
          v.obj = new Foo();
        }
      );
      globs.define(cl);
      globs.define(new ArrayTypeSymbolT<Foo>(globs, new TypeRef(cl), delegate() { return new List<Foo>(); } ));

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

  public class DynValContainer
  {
    public DynVal dv;
  }

  void BindDynValContainer(GlobalScope globs, DynValContainer c)
  {
    {
      var cl = new ClassBindSymbol("DynValContainer",
        delegate(ref DynVal v) 
        { 
          v.obj = new DynValContainer();
        }
      );
      globs.define(cl);

      cl.define(new FieldSymbol("dv", globs.type("any"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var f = (DynValContainer)ctx.obj;
          if(f.dv != null)
          {
            v.TryDel();
            v = f.dv;
          }
          else
          {
            v.SetNil();
          }
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var f = (DynValContainer)ctx.obj;
          f.dv = DynVal.Assign(f.dv, v);
          ctx.obj = f;
        }
      ));
    }

    {
      var fn = new SimpleFuncBindSymbol("get_dv_container", globs.type("DynValContainer"),
          delegate() { Interpreter.instance.PushValue(DynVal.NewObj(c)); return BHS.SUCCESS; } );

      globs.define(fn);
    }
  }

  void BindFooLambda(GlobalScope globs)
  {
    {
      var cl = new ClassBindSymbol("FooLambda",
        delegate(ref DynVal v) 
        { 
          v.obj = new FooLambda();
        }
      );
      globs.define(cl);
      globs.define(new ArrayTypeSymbolT<Foo>(globs, new TypeRef(cl), delegate() { return new List<Foo>(); } ));

      cl.define(new FieldSymbol("script", globs.type("void^()"),
          delegate(DynVal ctx, ref DynVal v) {
            var f = (FooLambda)ctx.obj;
            v.obj = f.script.Count == 0 ? null : ((BaseLambda)(f.script[0])).fct.obj;
          },
          delegate(ref DynVal ctx, DynVal v) {
            var f = (FooLambda)ctx.obj;
            var fctx = (FuncCtx)v.obj;
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
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

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
  public void TestPlusNotOverloadedForBindClass()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c1
      Color c2
      Color c3 = c1 + c2
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestMinusNotOverloadedForBindClass()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c1
      Color c2
      Color c3 = c1 - c2
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestMultNotOverloadedForBindClass()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c1
      Color c2
      Color c3 = c1 * c2
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestDivNotOverloadedForBindClass()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c1
      Color c2
      Color c3 = c1 / c2
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestGtNotOverloadedForBindClass()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c1
      Color c2
      bool r = c1 > c2
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestGteNotOverloadedForBindClass()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c1
      Color c2
      bool r = c1 >= c2
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestLtNotOverloadedForBindClass()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c1
      Color c2
      bool r = c1 > c2
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestLteNotOverloadedForBindClass()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c1
      Color c2
      bool r = c1 > c2
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestUnaryMinusNotOverloadedForBindClass()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c1
      Color c2 = -c1
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"must be numeric type"
    );
  }

  [IsTested()]
  public void TestBitAndNotOverloadedForBindClass()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c1
      Color c2
      int a = c2 & c1
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"must be int type"
    );
  }

  [IsTested()]
  public void TestBitOrNotOverloadedForBindClass()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c1
      Color c2
      int a = c2 | c1
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"must be int type"
    );
  }

  [IsTested()]
  public void TestLogicalAndNotOverloadedForBindClass()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c1
      Color c2
      bool a = c2 && c1
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"must be bool type"
    );
  }

  [IsTested()]
  public void TestLogicalOrNotOverloadedForBindClass()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c1
      Color c2
      bool a = c2 || c1
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"must be bool type"
    );
  }

  [IsTested()]
  public void TestUnaryNotNotOverloadedForBindClass()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c1
      bool a = !c1
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"must be bool type"
    );
  }

  [IsTested()]
  public void TestAnyNullEquality()
  {
    string bhl = @"
      
    func bool test() 
    {
      any foo
      return foo == null
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractBool(intp.ExecNode(node));

    AssertTrue(res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestAnyNullAssign()
  {
    string bhl = @"
      
    func bool test() 
    {
      any foo = null
      return foo == null
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractBool(intp.ExecNode(node));

    AssertTrue(res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestBindChildClass()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      ColorAlpha c = new ColorAlpha
      c.r = k*1
      c.g = k*100
      c.a = 1000
      return c.r + c.g + c.a
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);

    AssertEqual(res, 1202);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestBindChildClassLazilyNotAllowed()
  {
    var globs = SymbolTable.CreateBuiltins();
    
    AssertError<UserError>(
      delegate() { 
        BindColorAlpha(globs, bind_parent: false);
      },
      @"parent class not resolved"
    );
  }

  [IsTested()]
  public void TestBindChildClassCallParentMethod()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      ColorAlpha c = new ColorAlpha
      c.r = 10
      c.g = 20
      return c.mult_summ(k)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);

    AssertEqual(res, 60);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestBindChildClassCallOwnMethod()
  {
    string bhl = @"
      
    func float test() 
    {
      ColorAlpha c = new ColorAlpha
      c.r = 10
      c.g = 20
      c.a = 3
      return c.mult_summ_alpha()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);

    AssertEqual(res, 90);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestBindChildClassImplicitBaseCast()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      Color c = new ColorAlpha
      c.r = k*1
      c.g = k*100
      return c.r + c.g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);

    AssertEqual(res, 202);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestBindChildClassExplicitBaseCast()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      Color c = (Color)new ColorAlpha
      c.r = k*1
      c.g = k*100
      return c.r + c.g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);

    AssertEqual(res, 202);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestBindChildClassExplicitDownCast()
  {
    string bhl = @"
      
    func float test() 
    {
      ColorAlpha orig = new ColorAlpha
      orig.a = 1000
      Color tmp = (Color)orig
      tmp.r = 1
      tmp.g = 100
      ColorAlpha c = (ColorAlpha)tmp
      return c.r + c.g + c.a
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);

    AssertEqual(res, 1101);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestIncompatibleImplicitClassCast()
  {
    string bhl = @"
      
    func float test() 
    {
      Foo tmp = new Color
      return 1
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);
    BindFoo(globs);

    AssertError<UserError>(
       delegate() {
         Interpret("", bhl, globs);
       },
      "have incompatible types"
    );
  }

  [IsTested()]
  public void TestIncompatibleExplicitClassCast()
  {
    string bhl = @"
      
    func float test() 
    {
      Foo tmp = (Foo)new Color
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);
    BindFoo(globs);

    AssertError<UserError>(
       delegate() {
         Interpret("", bhl, globs);
       },
      "have incompatible types for casting"
    );
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
      
    func Color MakeColor(float g)
    {
      Color c = new Color
      c.g = g
      return c
    }

    func float test(float k) 
    {
      return MakeColor(k).g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestChainCall3()
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
      return MakeColor(k).Add(1).g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 3);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestChainCall4()
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
      return MakeColor(MakeColor(k).g).g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestChainCall5()
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
    globs.define(new GenericArrayTypeSymbol(globs, new TypeRef(en)));

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
  public void TestUsingBultinTypeAsFunc()
  {
    string bhl = @"

    func float foo()
    {
      return 14
    }
      
    func int test() 
    {
      return int(foo())
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "int : symbol is not a function"
    );
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
  public void TestVarInForever()
  {
    string bhl = @"

    func test() 
    {
      forever {
        int foo = 1
        trace((string)foo + "";"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    //NodeDump(node);
    
    for(int i=0;i<5;++i)
      node.run();
    AssertEqual(DynVal.PoolCount, 4);
    AssertEqual(DynVal.PoolCountFree, 3);

    var str = GetString(trace_stream);

    AssertEqual("1;1;1;1;1;", str);

    for(int i=0;i<5;++i)
      node.run();
    AssertEqual(DynVal.PoolCount, 4);
    AssertEqual(DynVal.PoolCountFree, 3);

    str = GetString(trace_stream);

    AssertEqual("1;1;1;1;1;" + "1;1;1;1;1;", str);

    node.stop();

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
    node.run();

    //NodeDump(node);
    
    for(int i=0;i<2;++i)
      node.run();

    node.stop();

    AssertEqual(DynValList.PoolCount, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestStackInForever()
  {
    string bhl = @"

    func int foo()
    {
      return 100
    }

    func hey(int a)
    {
    }

    func test() 
    {
      forever {
        hey(foo())
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    //NodeDump(node);
    
    for(int i=0;i<5;++i)
      node.run();
    AssertEqual(intp.stack.Count, 0);
    AssertEqual(DynVal.PoolCount, 2);
    AssertEqual(DynVal.PoolCountFree, 2);

    node.stop();
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPassingDynValToBindClass()
  {
    string bhl = @"

    class Foo
    {
      int b
    }

    func int test() 
    {
      Foo f1 = {b: 14} 
      Foo f2 = {b: 10} 

      DynValContainer c = get_dv_container()
      if(c.dv != null) {
        fail()
      }
      c.dv = f1
      c.dv = f2

      Foo tmp = (Foo)get_dv_container().dv
      return tmp.b
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var c = new DynValContainer();

    BindDynValContainer(globs, c);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 10);

    c.dv.Release();

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDynValToBindClassAnyNull()
  {
    string bhl = @"

    func bool test() 
    {
      DynValContainer c = get_dv_container()
      any foo = c.dv
      return foo == null
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var c = new DynValContainer();

    BindDynValContainer(globs, c);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var res = ExtractBool(intp.ExecNode(node));

    AssertTrue(res);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDynValToBindClassAssignNull()
  {
    string bhl = @"

    func bool test() 
    {
      DynValContainer c = get_dv_container()
      c.dv = null
      return c.dv == null
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var c = new DynValContainer();

    BindDynValContainer(globs, c);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractBool(intp.ExecNode(node));

    AssertTrue(res);

    c.dv.Release();

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDynValListOwnership()
  {
    var lst = DynValList.New();

    {
      var dv = DynVal.New();
      lst.Add(dv);
      AssertEqual(dv.refs, 1);

      lst.Clear();
      AssertEqual(dv.refs, -1);
    }

    {
      var dv = DynVal.New();
      lst.Add(dv);
      AssertEqual(dv.refs, 1);

      lst.RemoveAt(0);
      AssertEqual(dv.refs, -1);

      lst.Clear();
      AssertEqual(dv.refs, -1);
    }

    {
      var dv0 = DynVal.New();
      var dv1 = DynVal.New();
      lst.Add(dv0);
      lst.Add(dv1);
      AssertEqual(dv0.refs, 1);
      AssertEqual(dv1.refs, 1);

      lst.RemoveAt(1);
      AssertEqual(dv0.refs, 1);
      AssertEqual(dv1.refs, -1);

      lst.Clear();
      AssertEqual(dv0.refs, -1);
      AssertEqual(dv1.refs, -1);
    }

    DynValList.Del(lst);

    AssertEqual(DynValList.PoolCount, DynValList.PoolCountFree);
    AssertEqual(DynVal.PoolCount, DynVal.PoolCountFree);
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
    lst.TryDel();
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
    //NodeDump(node);
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
    lst.TryDel();
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

  [IsTested()]
  public void TestBindedClassTmpArray()
  {
    string bhl = @"

    func Color[] mkarray()
    {
      Color[] cs = new Color[]
      Color c0 = new Color
      c0.g = 1
      cs.Add(c0)
      Color c1 = new Color
      c1.r = 10
      cs.Add(c1)
      return cs
    }
      
    func float test() 
    {
      return mkarray()[1].r
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 10);
    CommonChecks(intp);
  }

  public class BaseScript
  {}

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
      "symbol is not a function"
    );
  }

  public class StateIsNode : BehaviorTreeTerminalNode
  {
    public override BHS execute()
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
    var res = intp.ExecNode(node, 0);
    AssertEqual(res.status, BHS.FAILURE);
  }

  [IsTested()]
  public void TestUserEnum()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
    }
      
    func int test() 
    {
      Foo f = Foo::B 
      return (int)f
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserEnumIntCast()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
    }
      
    func int test() 
    {
      return (int)Foo::B + (int)Foo::A
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 3);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestIntCastToUserEnum()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
    }
      
    func int test() 
    {
      Foo f = (Foo)2
      return (int)f
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserEnumWithDuplicateKey()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
      A = 10
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      @"duplicate key 'A'"
    );
  }

  [IsTested()]
  public void TestUserEnumWithDuplicateValue()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
      C = 1
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      @"duplicate value '1'"
    );
  }

  [IsTested()]
  public void TestUserEnumConflictsWithAnotherEnum()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
    }

    enum Foo
    {
      C = 3
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      @"already defined symbol 'Foo'"
    );
  }

  [IsTested()]
  public void TestUserEnumConflictsWithClass()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
    }

    class Foo
    {
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      @"already defined symbol 'Foo'"
    );
  }

  [IsTested()]
  public void TestUserEnumItemBadChainCall()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
    }

    func foo(Foo f) 
    {
    }

    func test() 
    {
      foo(Foo.A)
    }

    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      @"bad chain call"
    );
  }

  public class StartScriptNode : BehaviorTreeDecoratorNode
  {
    FuncCtx fct;

    public override void init()
    {
      var interp = Interpreter.instance;
      var dv = interp.PopValue(); 
      fct = (FuncCtx)dv.obj;
      fct.Retain();

      var func_node = fct.EnsureNode();

      this.setSlave(func_node);

      base.init();
    }

    public override void deinit()
    {
      base.deinit();
      fct.Release();
      fct = null;
    }
  }

  public class TraceNode : BehaviorTreeTerminalNode
  {
    Stream sm;

    public TraceNode(Stream sm)
    {
      this.sm = sm;
    }

    public override void init()
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

    public override void init()
    {
      var interp = Interpreter.instance;
      var s = interp.PopValue();
      var v = interp.PopValue();
      ticks = (int)v.num;
      result = s.bval ? BHS.SUCCESS : BHS.FAILURE;
    }

    public override BHS execute()
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

  void BindAnswer42(GlobalScope globs)
  {
    {
      var fn = new SimpleFuncBindSymbol("answer42", globs.type("int"),
          delegate() { Interpreter.instance.PushValue(DynVal.NewNum(42)); return BHS.SUCCESS; } );
      globs.define(fn);
    }
  }

  //simple console outputting version
  void BindLog(GlobalScope globs)
  {
    {
      var fn = new SimpleFuncBindSymbol("log", globs.type("void"),
          delegate() { Console.WriteLine(Interpreter.instance.PopValue().str); return BHS.SUCCESS; } );
      fn.define(new FuncArgSymbol("str", globs.type("string")));

      globs.define(fn);
    }
  }

  void BindMin(GlobalScope globs)
  {
    {
      var fn = new SimpleFuncBindSymbol("min", globs.type("float"),
        delegate()
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

    public override BHS execute()
    {
      return BHS.SUCCESS;
    }

    public override void defer()
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
    intp.ExecNode(node, 0);
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

  void BindStartScriptInMgr(GlobalScope globs)
  {
    {
      var fn = new SimpleFuncBindSymbol("StartScriptInMgr", globs.type("void"),
          delegate()
          {
            var interp = Interpreter.instance;
            bool now = interp.PopValue().bval;
            int num = (int)interp.PopValue().num;
            var fct = (FuncCtx)interp.PopValue().obj;

            for(int i=0;i<num;++i)
            {
              fct = fct.AutoClone();
              fct.Retain();
              var node = fct.EnsureNode();
              //Console.WriteLine("FREFS START: " + fct.GetHashCode() + " " + fct.refs);
              ScriptMgr.instance.add(node, now);
            }

            return BHS.SUCCESS;
          }
      );

      fn.define(new FuncArgSymbol("script", globs.type("void^()")));
      fn.define(new FuncArgSymbol("num", globs.type("int")));
      fn.define(new FuncArgSymbol("now", globs.type("bool")));

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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);

    AssertEqual("HERE", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestParseType()
  {
    {
      var type = Frontend.ParseType("bool").type()[0];
      AssertEqual(type.NAME().GetText(), "bool");
      AssertTrue(type.fnargs() == null);
      AssertTrue(type.ARR() == null);
    }

    {
      var ret = Frontend.ParseType("bool,float");
      AssertEqual(ret.type().Length, 2);
      AssertEqual(ret.type()[0].NAME().GetText(), "bool");
      AssertTrue(ret.type()[0].fnargs() == null);
      AssertTrue(ret.type()[0].ARR() == null);
      AssertEqual(ret.type()[1].NAME().GetText(), "float");
      AssertTrue(ret.type()[1].fnargs() == null);
      AssertTrue(ret.type()[1].ARR() == null);
    }

    {
      var type = Frontend.ParseType("int[]").type()[0];
      AssertEqual(type.NAME().GetText(), "int");
      AssertTrue(type.fnargs() == null);
      AssertTrue(type.ARR() != null);
    }

    {
      var type = Frontend.ParseType("bool^(int,string)").type()[0];
      AssertEqual(type.NAME().GetText(), "bool");
      AssertEqual(type.fnargs().names().refName()[0].NAME().GetText(), "int");
      AssertEqual(type.fnargs().names().refName()[1].NAME().GetText(), "string");
      AssertTrue(type.ARR() == null);
    }

    {
      var ret = Frontend.ParseType("bool^(int,string),float[]");
      AssertEqual(ret.type().Length, 2);
      var type = ret.type()[0];
      AssertEqual(type.NAME().GetText(), "bool");
      AssertEqual(type.fnargs().names().refName()[0].NAME().GetText(), "int");
      AssertEqual(type.fnargs().names().refName()[1].NAME().GetText(), "string");
      AssertTrue(type.ARR() == null);
      type = ret.type()[1];
      AssertEqual(type.NAME().GetText(), "float");
      AssertTrue(type.fnargs() == null);
      AssertTrue(type.ARR() != null);
    }

    {
      var type = Frontend.ParseType("float^(int)[]").type()[0];
      AssertEqual(type.NAME().GetText(), "float");
      AssertEqual(type.fnargs().names().refName()[0].NAME().GetText(), "int");
      AssertTrue(type.ARR() != null);
    }

    {
      var ret = Frontend.ParseType("Vec3,Vec3,Vec3");
      AssertEqual(ret.type().Length, 3);
      var type = ret.type()[0];
      AssertEqual(type.NAME().GetText(), "Vec3");
      AssertTrue(type.fnargs() == null);
      AssertTrue(type.ARR() == null);
      type = ret.type()[1];
      AssertEqual(type.NAME().GetText(), "Vec3");
      AssertTrue(type.fnargs() == null);
      AssertTrue(type.ARR() == null);
      type = ret.type()[2];
      AssertEqual(type.NAME().GetText(), "Vec3");
      AssertTrue(type.fnargs() == null);
      AssertTrue(type.ARR() == null);
    }

    {
      //malformed
      var type = Frontend.ParseType("float^");
      AssertTrue(type == null);
    }

    {
      //TODO:
      //malformed
      //var type = Frontend.ParseType("int]");
      //AssertTrue(type == null);
    }
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
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);

    AssertEqual("FOO", str);
    AssertEqual(1, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncPtrLambda()
  {
    string bhl = @"

    func void test() 
    {
      void^() ptr = func() {
        trace(""FOO"")
      }
      ptr()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);

    AssertEqual("FOO", str);
    AssertEqual(1, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncPtrReturningArrLambda()
  {
    string bhl = @"

    func void test() 
    {
      int[]^() ptr = func int[]() {
        return [1,2]
      }
      trace((string)ptr()[1])
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);

    AssertEqual("2", str);
    AssertEqual(1, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  //TODO:?
  //[IsTested()]
  public void TestFuncPtrReturningArrOfArrLambda()
  {
    string bhl = @"

    typedef string[]^()[] arr_of_funcs

    func arr_of_func^() hey() {
      return func() {
        func string[]() { return [""a"",""b""] },
        func string[]() { return [""c"",""d""] }
      }
    }

    func void test() 
    {
      string[]^()[] ptr = [
        func string[]() { return [""a"",""b""] },
        func string[]() { return [""c"",""d""] }
      ]
      trace(ptr[1]()[0])
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);

    AssertEqual("c", str);
    AssertEqual(1, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncPtrSeveralLambda()
  {
    string bhl = @"

    func void test() 
    {
      void^() ptr = func() {
        trace(""FOO"")
      }
      ptr()
      ptr()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);

    AssertEqual("FOOFOO", str);
    AssertEqual(1, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncPtrSeveralLambdaRunning()
  {
    string bhl = @"

    func void test() 
    {
      void^(string) ptr = func(string arg) {
        trace(arg)
        yield()
      }
      paral {
        ptr(""FOO"")
        ptr(""BAR"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run();
      AssertEqual(status, BHS.RUNNING);
    }

    {
      var status = node.run();
      AssertEqual(status, BHS.SUCCESS);
    }

    var str = GetString(trace_stream);

    AssertEqual("FOOBAR", str);
    AssertEqual(2, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestComplexFuncPtr()
  {
    string bhl = @"
    func bool foo(int a, string k)
    {
      trace(k)
      return a > 2
    }

    func bool test(int a) 
    {
      bool^(int,string) ptr = foo
      return ptr(a, ""HEY"")
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractBool(intp.ExecNode(node));
    //NodeDump(node);
    AssertTrue(res);
    var str = GetString(trace_stream);
    AssertEqual("HEY", str);
    AssertEqual(1, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestComplexFuncPtrSeveralTimes()
  {
    string bhl = @"
    func bool foo(int a, string k)
    {
      trace(k)
      return a > 2
    }

    func bool test(int a) 
    {
      bool^(int,string) ptr = foo
      return ptr(a, ""HEY"") && ptr(a-1, ""BAR"")
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractBool(intp.ExecNode(node));
    //NodeDump(node);
    AssertTrue(!res);
    var str = GetString(trace_stream);
    AssertEqual("HEYBAR", str);
    AssertEqual(1, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestComplexFuncPtrSeveralTimes2()
  {
    string bhl = @"
    func int foo(int a)
    {
      int^(int) p = 
        func int (int a) {
          return a * 2
        }

      return p(a)
    }

    func int test(int a) 
    {
      return foo(a) + foo(a+1)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    {
      var node = intp.GetFuncNode("test");
      node.SetArgs(DynVal.NewNum(3));
      var res = ExtractNum(intp.ExecNode(node));
      //NodeDump(node);
      AssertEqual(res, 14);
    }
    AssertEqual(1, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestComplexFuncPtrSeveralTimes3()
  {
    string bhl = @"
    func int foo(int a)
    {
      int^(int) p = 
        func int (int a) {
          return a
        }

      return p(a)
    }

    func int test(int a) 
    {
      return foo(a) + foo(a)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    {
      var node = intp.GetFuncNode("test");
      node.SetArgs(DynVal.NewNum(3));
      var res = ExtractNum(intp.ExecNode(node));
      //NodeDump(node);
      AssertEqual(res, 6);
    }
    {
      var node = intp.GetFuncNode("test");
      node.SetArgs(DynVal.NewNum(4));
      var res = ExtractNum(intp.ExecNode(node));
      //NodeDump(node);
      AssertEqual(res, 8);
    }
    AssertEqual(1, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestComplexFuncPtrSeveralTimes4()
  {
    string bhl = @"
    func int foo(int a)
    {
      int^(int) p = 
        func int (int a) {
          return a
        }

      int tmp = p(a)

      p = func int (int a) {
          return a * 2
      }

      return tmp + p(a)
    }

    func int test(int a) 
    {
      return foo(a) + foo(a+1)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    {
      var node = intp.GetFuncNode("test");
      node.SetArgs(DynVal.NewNum(3));
      var res = ExtractNum(intp.ExecNode(node));
      //NodeDump(node);
      AssertEqual(res, 9 + 12);
    }
    {
      var node = intp.GetFuncNode("test");
      node.SetArgs(DynVal.NewNum(4));
      var res = ExtractNum(intp.ExecNode(node));
      //NodeDump(node);
      AssertEqual(res, 12 + 15);
    }
    AssertEqual(2, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestComplexFuncPtrSeveralTimes5()
  {
    string bhl = @"
    func int foo(int^(int) p, int a)
    {
      return p(a)
    }

    func int test(int a) 
    {
      return foo(func int(int a) { return a }, a) + 
             foo(func int(int a) { return a * 2 }, a+1)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    {
      var node = intp.GetFuncNode("test");
      node.SetArgs(DynVal.NewNum(3));
      var res = ExtractNum(intp.ExecNode(node));
      //NodeDump(node);
      AssertEqual(res, 3 + 8);
    }
    {
      var node = intp.GetFuncNode("test");
      node.SetArgs(DynVal.NewNum(4));
      var res = ExtractNum(intp.ExecNode(node));
      //NodeDump(node);
      AssertEqual(res, 4 + 10);
    }
    AssertEqual(2, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestArrayOfComplexFuncPtrs()
  {
    string bhl = @"
    func int test(int a) 
    {
      int^(int,string)[] ptrs = new int^(int,string)[]
      ptrs.Add(func int(int a, string b) { 
          trace(b) 
          return a*2 
      })
      ptrs.Add(func int(int a, string b) { 
          trace(b)
          return a*10
      })

      return ptrs[0](a, ""what"") + ptrs[1](a, ""hey"")
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);
    AssertEqual(res, 3*2 + 3*10);
    var str = GetString(trace_stream);
    AssertEqual("whathey", str);
    AssertEqual(2, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnComplexFuncPtr()
  {
    string bhl = @"
    func bool^(int) foo()
    {
      return func bool(int a) { return a > 2 } 
    }

    func bool test(int a) 
    {
      bool^(int) ptr = foo()
      return ptr(a)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractBool(intp.ExecNode(node));
    //NodeDump(node);
    AssertTrue(res);
    AssertEqual(1, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnAndCallComplexFuncPtr()
  {
    string bhl = @"
    func bool^(int) foo()
    {
      return func bool(int a) { return a > 2 } 
    }

    func bool test(int a) 
    {
      return foo()(a)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractBool(intp.ExecNode(node));
    //NodeDump(node);
    AssertTrue(res);
    AssertEqual(1, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCallLambdaInPlace()
  {
    string bhl = @"

    func bool test(int a) 
    {
      return func bool(int a) { return a > 2 }(a) 
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractBool(intp.ExecNode(node));
    //NodeDump(node);
    AssertTrue(res);
    AssertEqual(1, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCallLambdaInPlaceArray()
  {
    string bhl = @"

    func int test(int a) 
    {
      return func int[](int a) { 
        int[] ns = new int[]
        ns.Add(a)
        ns.Add(a*2)
        return ns
      }(a)[1] 
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));
    AssertEqual(res, 6);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCallLambdaInPlaceInvalid()
  {
    string bhl = @"

    func bool test(int a) 
    {
      return func bool(int a) { return a > 2 }.foo 
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl, globs);
      },
      "type doesn't support member access via '.'"
    );
  }

  [IsTested()]
  public void TestCallLambdaInPlaceInvalid2()
  {
    string bhl = @"

    func bool test(int a) 
    {
      return func bool(int a) { return a > 2 }[10] 
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl, globs);
      },
      "accessing not an array type 'bool^(int)'"
    );
  }

  [IsTested()]
  public void TestComplexFuncPtrPassRef()
  {
    string bhl = @"
    func void foo(int a, string k, ref bool res)
    {
      trace(k)
      res = a > 2
    }

    func bool test(int a) 
    {
      void^(int,string, ref  bool) ptr = foo
      bool res = false
      ptr(a, ""HEY"", ref res)
      return res
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractBool(intp.ExecNode(node));
    //NodeDump(node);
    AssertTrue(res);
    var str = GetString(trace_stream);
    AssertEqual("HEY", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestComplexFuncPtrLambda()
  {
    string bhl = @"
    func bool test(int a) 
    {
      bool^(int,string) ptr = 
        func bool (int a, string k)
        {
          trace(k)
          return a > 2
        }
      return ptr(a, ""HEY"")
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractBool(intp.ExecNode(node));
    //NodeDump(node);
    AssertTrue(res);
    var str = GetString(trace_stream);
    AssertEqual("HEY", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestComplexFuncPtrLambdaInALoop()
  {
    string bhl = @"
    func test() 
    {
      void^(int) ptr = 
        func void (int a)
        {
          trace((string)a)
        }
      int i = 0
      while(i < 5)
      {
        ptr(i)
        i = i + 1
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);
    var str = GetString(trace_stream);
    AssertEqual("01234", str);
    AssertEqual(1, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestComplexFuncPtrIncompatibleRetType()
  {
    string bhl = @"
    func void foo(int a) { }

    func void test() 
    {
      bool^(int) ptr = foo
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl, globs);
      },
      "@(6,17) ptr:<bool^(int)>, @(6,21) =foo:<void^(int)> have incompatible types"
    );
  }

  [IsTested()]
  public void TestComplexFuncPtrArgTypeCheck()
  {
    string bhl = @"
    func void foo(int a) { }

    func void test() 
    {
      void^(float) ptr = foo
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl, globs);
      },
      "@(6,19) ptr:<void^(float)>, @(6,23) =foo:<void^(int)> have incompatible types"
    );
  }

  [IsTested()]
  public void TestComplexFuncPtrCallArgTypeCheck()
  {
    string bhl = @"
    func void foo(int a) { }

    func void test() 
    {
      void^(int) ptr = foo
      ptr(""hey"")
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl, globs);
      },
      @"int, @(7,10) ""hey"":<string> have incompatible types"
    );
  }

  [IsTested()]
  public void TestComplexFuncPtrCallArgRefTypeCheck()
  {
    string bhl = @"
    func void foo(int a, ref  float b) { }

    func void test() 
    {
      float b = 1
      void^(int, ref float) ptr = foo
      ptr(10, b)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl, globs);
      },
      @"b:<float>: 'ref' is missing"
    );
  }

  [IsTested()]
  public void TestComplexFuncPtrCallArgRefTypeCheck2()
  {
    string bhl = @"
    func void foo(int a, ref float b) { }

    func void test() 
    {
      float b = 1
      void^(int, float) ptr = foo
      ptr(10, ref b)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl, globs);
      },
      "ptr:<void^(int,float)>, @(7,28) =foo:<void^(int,ref float)> have incompatible types"
    );
  }

  [IsTested()]
  public void TestComplexFuncPtrPassConflictingRef()
  {
    string bhl = @"
    func void foo(int a, string k, refbool res)
    {
      trace(k)
    }

    func bool test(int a) 
    {
      void^(int,string,ref bool) ptr = foo
      bool res = false
      ptr(a, ""HEY"", ref res)
      return res
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    {
      var cl = new ClassBindSymbol("refbool",
        delegate(ref DynVal v) 
        {}
      );
      globs.define(cl);
    }

    BindTrace(globs, trace_stream);

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl, globs);
      },
      "have incompatible types"
    );
  }

  [IsTested()]
  public void TestComplexFuncPtrCallArgNotEnoughArgsCheck()
  {
    string bhl = @"
    func void foo(int a) { }

    func void test() 
    {
      void^(int) ptr = foo
      ptr()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl, globs);
      },
      "missing argument of type 'int'"
    );
  }

  [IsTested()]
  public void TestComplexFuncPtrCallArgNotEnoughArgsCheck2()
  {
    string bhl = @"
    func void foo(int a, float b) { }

    func void test() 
    {
      void^(int, float) ptr = foo
      ptr(10)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl, globs);
      },
      "missing argument of type 'float'"
    );
  }

  [IsTested()]
  public void TestComplexFuncPtrCallArgTooManyArgsCheck()
  {
    string bhl = @"
    func void foo(int a) { }

    func void test() 
    {
      void^(int) ptr = foo
      ptr(10, 30)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() {
        Interpret("", bhl, globs);
      },
      "too many arguments"
    );
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
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);

    AssertEqual("HERE", str);
    CommonChecks(intp);
  }

  public class ScriptMgr : BehaviorTreeInternalNode
  {
    public static ScriptMgr instance = new ScriptMgr();

    List<BehaviorTreeNode> pending_removed = new List<BehaviorTreeNode>();
    bool in_exec = false;

    public List<BehaviorTreeNode> getChildren()
    {
      return children;
    }

    public void add(BehaviorTreeNode node, bool now = false)
    {
      children.Add(node);

      if(now)
        runNode(node);
    }

    public override void init()
    {}

    public override BHS execute()
    {
      in_exec = true;

      for(int i=0;i<children.Count;i++)
        runAt(i);

      in_exec = false;

      for(int i=0;i<pending_removed.Count;++i)
        doDel(pending_removed[i]);
      pending_removed.Clear();

      return BHS.RUNNING;
    }

    public override void stop()
    {
      for(int i=children.Count;i-- > 0;)
      {
        var node = children[i];
        doDel(node);
      }
    }

    void runAt(int idx)
    {
      var node = children[idx];
      
      if(pending_removed.Count > 0 && pending_removed.Contains(node))
        return;

      var result = node.run();

      if(result != BHS.RUNNING)
        doDel(node);
    }

    void doDel(BehaviorTreeNode node)
    {
      int idx = children.IndexOf(node);
      if(idx == -1)
        return;
      
      if(in_exec)
      {
        if(pending_removed.IndexOf(node) == -1)
          pending_removed.Add(node);
      }
      else
      {
        //removing child ASAP so that it can't be 
        //be removed several times
        children.RemoveAt(idx);

        node.stop();

        var fnode = node as FuncNode;
        if(fnode != null && fnode.fct != null)
          fnode.fct.Release();
      }
    }

    public void runNode(BehaviorTreeNode node)
    {
      int idx = children.IndexOf(node);
      runAt(idx);
    }

    public bool busy()
    {
      return children.Count > 0 || pending_removed.Count > 0;
    }
  }

  [IsTested()]
  public void TestStartLambdaInScriptMgr()
  {
    string bhl = @"

    func void test() 
    {
      forever {
        StartScriptInMgr(
          script: func() { 
            trace(""HERE;"") 
          },
          num : 1,
          now : false
        )
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScriptInMgr(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run();
      AssertEqual(status, BHS.RUNNING);

      ScriptMgr.instance.run();

      var str = GetString(trace_stream);
      AssertEqual("HERE;", str);

      var cs = ScriptMgr.instance.getChildren();
      AssertEqual(0, cs.Count); 
    }

    //NodeDump(node);

    {
      var status = node.run();
      AssertEqual(status, BHS.RUNNING);

      ScriptMgr.instance.run();

      var str = GetString(trace_stream);
      AssertEqual("HERE;HERE;", str);

      var cs = ScriptMgr.instance.getChildren();
      AssertEqual(0, cs.Count); 
    }

    ScriptMgr.instance.stop();

    AssertTrue(!ScriptMgr.instance.busy());
    AssertEqual(1, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestStartLambdaRunninInScriptMgr()
  {
    string bhl = @"

    func void test() 
    {
      forever {
        StartScriptInMgr(
          script: func() { 
            trace(""HERE;"") 
            suspend()
          },
          num : 1,
          now : false
        )
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScriptInMgr(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run();
      AssertEqual(status, BHS.RUNNING);

      ScriptMgr.instance.run();

      var str = GetString(trace_stream);
      AssertEqual("HERE;", str);

      var cs = ScriptMgr.instance.getChildren();
      AssertEqual(1, cs.Count); 
    }

    //NodeDump(node);

    {
      var status = node.run();
      AssertEqual(status, BHS.RUNNING);

      ScriptMgr.instance.run();

      var str = GetString(trace_stream);
      AssertEqual("HERE;HERE;", str);

      var cs = ScriptMgr.instance.getChildren();
      AssertEqual(2, cs.Count); 
      AssertTrue(cs[0].GetHashCode() != cs[1].GetHashCode());
    }

    ScriptMgr.instance.stop();

    AssertTrue(!ScriptMgr.instance.busy());
    AssertEqual(2, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestStartLambdaManyTimesInScriptMgr()
  {
    string bhl = @"

    func void test() 
    {
      StartScriptInMgr(
        script: func() { 
          suspend()
        },
        num : 3,
        now : false
      )
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindStartScriptInMgr(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var status = node.run();
    AssertEqual(status, BHS.SUCCESS);

    ScriptMgr.instance.run();

    var cs = ScriptMgr.instance.getChildren();
    AssertEqual(3, cs.Count); 
    AssertTrue(cs[0].GetHashCode() != cs[1].GetHashCode());
    AssertTrue(cs[1].GetHashCode() != cs[2].GetHashCode());
    AssertTrue(cs[0].GetHashCode() != cs[2].GetHashCode());

    //NodeDump(node);

    ScriptMgr.instance.stop();

    AssertTrue(!ScriptMgr.instance.busy());
    AssertEqual(3, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestStartFuncPtrManyTimesInScriptMgr()
  {
    string bhl = @"

    func void test() 
    {
      void^() fn = func() {
        trace(""HERE;"")
        suspend()
      }

      StartScriptInMgr(
        script: fn,
        num : 2,
        now : true
      )
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindStartScriptInMgr(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var status = node.run();
    AssertEqual(status, BHS.SUCCESS);

    var cs = ScriptMgr.instance.getChildren();
    AssertEqual(2, cs.Count); 
    AssertTrue(cs[0].GetHashCode() != cs[1].GetHashCode());

    //NodeDump(node);

    var str = GetString(trace_stream);
    AssertEqual("HERE;HERE;", str);

    ScriptMgr.instance.stop();
    AssertTrue(!ScriptMgr.instance.busy());

    AssertEqual(2, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestStartBindFuncPtrManyTimesInScriptMgr()
  {
    string bhl = @"

    func void test() 
    {
      StartScriptInMgr(
        script: say_here,
        num : 2,
        now : true
      )
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindStartScriptInMgr(globs);
    BindTrace(globs, trace_stream);

    {
      var fn = new SimpleFuncBindSymbol("say_here", globs.type("void"), 
          delegate()
          {
            AddString(trace_stream, "HERE;");
            return BHS.RUNNING;
          }
          );
      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var status = node.run();
    AssertEqual(status, BHS.SUCCESS);

    var cs = ScriptMgr.instance.getChildren();
    AssertEqual(2, cs.Count); 
    AssertTrue(cs[0].GetHashCode() != cs[1].GetHashCode());

    //NodeDump(node);

    var str = GetString(trace_stream);
    AssertEqual("HERE;HERE;", str);

    ScriptMgr.instance.stop();
    AssertTrue(!ScriptMgr.instance.busy());

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestStartLambdaVarManyTimesInScriptMgr()
  {
    string bhl = @"

    func void test() 
    {
      void^() fn = func() {
        trace(""HERE;"")
        suspend()
      }

      StartScriptInMgr(
        script: func() { 
          fn()
        },
        num : 2,
        now : true
      )
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindStartScriptInMgr(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var status = node.run();
    AssertEqual(status, BHS.SUCCESS);

    var cs = ScriptMgr.instance.getChildren();
    AssertEqual(2, cs.Count); 
    AssertTrue(cs[0].GetHashCode() != cs[1].GetHashCode());

    //NodeDump(node);

    var str = GetString(trace_stream);
    AssertEqual("HERE;HERE;", str);

    ScriptMgr.instance.stop();
    AssertTrue(!ScriptMgr.instance.busy());

    AssertEqual(2+2, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaIsKeptInFuncCallScope()
  {
    string bhl = @"

    func void test() 
    {
      StartScriptInMgr(
        script: func() { 
          trace(""DO;"")
        },
        num : 2,
        now : true
      )
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScriptInMgr(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var status = node.run();
    AssertEqual(status, BHS.SUCCESS);
    AssertTrue(!ScriptMgr.instance.busy());

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestStartLambdaManyTimesInScriptMgrWithUseVals()
  {
    string bhl = @"

    func void test() 
    {
      float a = 0
      StartScriptInMgr(
        script: func() { 
          a = a + 1
          trace((string) a + "";"") 
          suspend()
        },
        num : 3,
        now : false
      )
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScriptInMgr(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var status = node.run();
    AssertEqual(status, BHS.SUCCESS);

    ScriptMgr.instance.run();

    var cs = ScriptMgr.instance.getChildren();
    AssertEqual(3, cs.Count); 
    AssertTrue(cs[0].GetHashCode() != cs[1].GetHashCode());
    AssertTrue(cs[1].GetHashCode() != cs[2].GetHashCode());
    AssertTrue(cs[0].GetHashCode() != cs[2].GetHashCode());

    //NodeDump(node);

    var str = GetString(trace_stream);
    AssertEqual("1;1;1;", str);

    ScriptMgr.instance.stop();

    AssertTrue(!ScriptMgr.instance.busy());
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestStartLambdaManyTimesInScriptMgrWithRefs()
  {
    string bhl = @"

    func void test() 
    {
      float a = 0
      float b = 0
      float[] fs = []
      StartScriptInMgr(
        script: func() use(ref b, ref fs) { 
          a = a + 1
          b = b + 1
          fs.Add(b)
          trace((string) a + "","" + (string) b + "","" + (string) fs.Count + "";"") 
          suspend()
        },
        num : 3,
        now : false
      )
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScriptInMgr(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var status = node.run();
    AssertEqual(status, BHS.SUCCESS);

    ScriptMgr.instance.run();

    //NodeDump(node);

    var str = GetString(trace_stream);
    AssertEqual("1,1,1;1,2,2;1,3,3;", str);

    ScriptMgr.instance.stop();

    AssertTrue(!ScriptMgr.instance.busy());

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
    intp.ExecNode(node, 0);

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
          delegate()
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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node1, 0);
    intp.ExecNode(node2, 0);

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
    intp.ExecNode(intp.GetFuncNode("test"), 0);
    intp.ExecNode(intp.GetFuncNode("test2"), 0);

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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

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
      "already defined symbol 'a'"
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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

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
      var status = node.run();
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
      fail()
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
      var status = node.run();
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
      var status = node.run();
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
      fail()
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
      var status = node.run();
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
      fail()
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
      var status = node.run();
      AssertEqual(BHS.FAILURE, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("BARFOO", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCheckTrue()
  {
    string bhl = @"

    func test() 
    {
      check(true)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var status = node.run();
    AssertEqual(BHS.SUCCESS, status);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCheckFalse()
  {
    string bhl = @"

    func test() 
    {
      check(false)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var status = node.run();
    AssertEqual(BHS.FAILURE, status);

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
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run();
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
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run();
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
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run();
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
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run();
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
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run();
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
        suspend()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();
    
    BindNodeWithDefer(globs, trace_stream);
    BindTrace(globs, trace_stream);
   
    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
   
    var status = node.run();
    AssertEqual(BHS.RUNNING, status);
    //NodeDump(node);
    
    var str = GetString(trace_stream);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestParalAllDefer()
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
        defer {
          trace(""DEFER"")
        }
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
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run();
      AssertEqual(BHS.SUCCESS, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("ABDEFER", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPrio()
  {
    string bhl = @"

    func foo()
    {
      trace(""FOO"")
      fail()
    }

    func hey()
    {
      trace(""HEY"")
    }

    func bar()
    {
      trace(""BAR"")
      fail()
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
      var status = node.run();
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
      fail()
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
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run();
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
        fail()
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
      var status = node.run();
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
    intp.ExecNode(node, 0);

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
      var status = node.run();
      AssertEqual(BHS.SUCCESS, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("BAR~BARFOO~FOO", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferNested()
  {
    string bhl = @"

    func bar()
    {
      defer {
        trace(""~BAR1"")
        defer {
          trace(""~BAR2"")
        }
      }
      trace(""BAR"")
    }

    func test() 
    {
      bar()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run();
      AssertEqual(BHS.SUCCESS, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("BAR~BAR1~BAR2", str);
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
      var status = node.run();
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
      fail()
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
      var status = node.run();
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
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }

    {
      var status = node.run();
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
      suspend()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindNodeWithDefer(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInvertNode()
  {
    string bhl = @"

    func test() 
    {
      not {
        nop()
      }
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    AssertTrue(BHS.FAILURE == node.run());
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInvertFailureNode()
  {
    string bhl = @"

    func test() 
    {
      not {
        fail()
      }
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    AssertTrue(BHS.SUCCESS == node.run());
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInvertRunningNode()
  {
    string bhl = @"

    func test() 
    {
      not {
        suspend()
      }
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    AssertTrue(BHS.RUNNING == node.run());
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInvertMultipleNodes()
  {
    string bhl = @"

    func test() 
    {
      not {
        nop()
        nop()
      }
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    AssertTrue(BHS.FAILURE == node.run());
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInvertNodeWithResult()
  {
    string bhl = @"

    func int foo() 
    {
      fail()
      return 1
    }

    func test() 
    {
      not {
        foo()
      }
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    AssertTrue(BHS.SUCCESS == node.run());
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
      suspend()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindNodeWithDefer(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    {
      var status = node.run();
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
      var status = node.run();
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
      var status = node.run();
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
        nop()
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
        fail()
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
        suspend()
      }
      return res
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var status = node.run();
    AssertEqual(BHS.RUNNING, status);
  }

  [IsTested()]
  public void TestEvalDanglingReturn()
  {
    string bhl = @"

    func void test() 
    {
      eval { 
        nop()
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
        nop()
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
          nop()
        } else {
          fail()
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
      if(eval { nop() })
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
      if(eval { fail() })
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
  public void TestRecursion()
  {
    string bhl = @"
      
    func float mult(float k)
    {
      if(k == 0) {
        return 1
      }
      return 2 * mult(k-1)
    }

    func float test(float k) 
    {
      return mult(k)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 8);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestBadRecursion()
  {
    string bhl = @"
      
    func test() 
    {
      test()
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    AssertError<IndexOutOfRangeException>(
      delegate() { 
        intp.ExecNode(node, 0);
      },
      "Index was outside the bounds of the array"
    );
  }

  [IsTested()]
  public void TestForever()
  {
    string bhl = @"

    func bar()
    {
      trace(""B"")
      fail()
    }

    func foo()
    {
      trace(""A"")
      fail()
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
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }

    //...will be running forever, well, we assume that :)

    var str = GetString(trace_stream);
    AssertEqual("BBBBB", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferInForever()
  {
    string bhl = @"

    func test() 
    {
      forever {
        defer {
          trace(""HEY;"")
        }
      }
      defer {
        trace(""NEVER;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    for(int i=0;i<3;++i)
    {
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }

    //...will be running forever, well, we assume that :)

    var str = GetString(trace_stream);
    AssertEqual("HEY;HEY;HEY;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeverBreak()
  {
    string bhl = @"

    func int test() 
    {
      int i = 0
      forever {
        i = i + 1
        if(i == 3) {
          break
        }
      }
      return i
    }
    ";


    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");

    var status = node.run();
    AssertEqual(BHS.RUNNING, status);

    status = node.run();
    AssertEqual(BHS.RUNNING, status);

    status = node.run();
    AssertEqual(BHS.SUCCESS, status);

    var res = intp.PopValue();
    AssertEqual(res.num, 3);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferInForeverWithBreak()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      forever {
        defer {
          trace(""HEY;"")
        }
        i = i + 1
        if(i == 2) {
          break
        }
      }
      defer {
        trace(""YOU;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    for(int i=0;i<2;++i)
      node.run();

    var str = GetString(trace_stream);
    AssertEqual("HEY;HEY;YOU;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNodeWithDeferInForever()
  {
    string bhl = @"

    func test() 
    {
      forever {
        NodeWithDefer()
      }
      defer {
        trace(""NEVER;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();
    
    BindNodeWithDefer(globs, trace_stream);
    BindTrace(globs, trace_stream);
   
    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    for(int i=0;i<3;++i)
    {
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }

    //...will be running forever, well, we assume that :)

    var str = GetString(trace_stream);
    AssertEqual("DEFER!!!DEFER!!!DEFER!!!", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferScopes()
  {
    string bhl = @"

    func test() 
    {
      defer {
        trace(""0"")
      }

      {
        defer {
          trace(""1"")
        }
        trace(""2"")
      }

      trace(""3"")

      defer {
        trace(""4"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("21340", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferInWhile()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      while(i < 3) {
        defer {
          trace(""WHILE;"")
        }
        i = i + 1
      }
      defer {
        trace(""AFTER;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("WHILE;WHILE;WHILE;AFTER;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferInIf()
  {
    string bhl = @"

    func test() 
    {
      if (true) {
        defer {
          trace(""0"")
        }
        trace(""1"")
      } else {
        defer {
          trace(""3"")
        }
      }
      trace(""2"")
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("102", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferInElse()
  {
    string bhl = @"

    func test() 
    {
      if (false) {
        defer {
          trace(""0"")
        }
      } else {
        defer {
          trace(""3"")
        }
        trace(""1"")
      }
      trace(""2"")
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("132", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferInPrio()
  {
    string bhl = @"

    func foo()
    {
      defer {
        trace(""FOO;"")
      }
      fail()
    }

    func bar()
    {
      defer {
        trace(""BAR;"")
      }
    }

    func never()
    {
      defer {
        trace(""NEVER;"")
      }
    }

    func test() 
    {
      prio {
        defer {
          trace(""PRIO;"")
        }
        foo() 
        bar()
        never()
      }
      defer {
        trace(""AFTER;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("FOO;BAR;PRIO;AFTER;", str);
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
          fail()
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
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }
    AssertEqual(BHS.SUCCESS, node.run());

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
          fail()
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
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }
    AssertEqual(BHS.SUCCESS, node.run());

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestYield()
  {
    string bhl = @"

    func test() 
    {
      yield()
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");

    var status = node.run();
    AssertEqual(BHS.RUNNING, status);

    status = node.run();
    AssertEqual(BHS.SUCCESS, status);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestYieldAfterNodeStop()
  {
    string bhl = @"

    func test() 
    {
      yield()
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");

    var status = node.run();
    AssertEqual(BHS.RUNNING, status);

    status = node.run();
    AssertEqual(BHS.SUCCESS, status);

    status = node.run();
    AssertEqual(BHS.RUNNING, status);

    status = node.run();
    AssertEqual(BHS.SUCCESS, status);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestYieldInParal()
  {
    string bhl = @"

    func int test() 
    {
      int i = 0
      paral {
        while(i < 3) { yield() }
        forever {
          i = i + 1
        }
      }
      return i
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");

    var status = node.run();
    AssertEqual(BHS.RUNNING, status);

    status = node.run();
    AssertEqual(BHS.RUNNING, status);

    status = node.run();
    AssertEqual(BHS.RUNNING, status);

    status = node.run();
    AssertEqual(BHS.SUCCESS, status);

    var val = intp.PopValue();
    AssertEqual(3, val.num);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestYieldWhileInParal()
  {
    string bhl = @"

    func int test() 
    {
      int i = 0
      paral {
        yield while(i < 3)
        forever {
          i = i + 1
        }
      }
      return i
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");

    var status = node.run();
    AssertEqual(BHS.RUNNING, status);

    status = node.run();
    AssertEqual(BHS.RUNNING, status);

    status = node.run();
    AssertEqual(BHS.RUNNING, status);

    status = node.run();
    AssertEqual(BHS.SUCCESS, status);

    var val = intp.PopValue();
    AssertEqual(3, val.num);

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
  public void TestFuncCachingAndClearingPool()
  {
    string bhl = @"
      
    func foo()
    {
      yield()
    }

    func test() 
    {
      foo()
    }
    ";

    var intp = Interpret("", bhl);

    AssertEqual(FuncCallNode.PoolCount, 0);

    {
      var node = intp.GetFuncNode("test");
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);

      FuncCallNode.PoolClear();

      status = node.run();
      AssertEqual(BHS.SUCCESS, status);
    }

    AssertEqual(FuncCallNode.PoolCount, 0);
  }

  [IsTested()]
  public void TestFuncCachingAndClearingPoolWithInterleaving()
  {
    string bhl = @"
      
    func foo()
    {
      yield()
    }

    func bar()
    {
      yield()
    }

    func test1() 
    {
      foo()
    }

    func test2() 
    {
      bar()
    }
    ";

    var intp = Interpret("", bhl);

    AssertEqual(FuncCallNode.PoolCount, 0);

    var node1 = intp.GetFuncNode("test1");
    var status = node1.run();
    AssertEqual(BHS.RUNNING, status);

    FuncCallNode.PoolClear();

    var node2 = intp.GetFuncNode("test2");
    status = node2.run();
    AssertEqual(BHS.RUNNING, status);
    AssertEqual(FuncCallNode.PoolCount, 1);

    //NOTE: at this point the node will try to free the cached pool item,
    //      however its version of pool item is outdated
    status = node1.run();
    AssertEqual(BHS.SUCCESS, status);

    status = node2.run();
    AssertEqual(BHS.SUCCESS, status);

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
        suspend()
      }
    }
    ";

    var intp = Interpret("", bhl);

    AssertEqual(FuncCallNode.PoolCount, 0);
    AssertEqual(FuncCallNode.PoolCountFree, 0);

    var node1 = intp.GetFuncNode("test");
    node1.SetArgs(DynVal.NewNum(3));
    var status = node1.run();
    AssertEqual(BHS.RUNNING, status);
    
    AssertEqual(FuncCallNode.PoolCount, 1);
    AssertEqual(FuncCallNode.PoolCountFree, 1);

    var node2 = intp.GetFuncNode("test");
    node2.SetArgs(DynVal.NewNum(30));
    status = node2.run();
    AssertEqual(BHS.RUNNING, status);

    AssertEqual(FuncCallNode.PoolCount, 1);
    AssertEqual(FuncCallNode.PoolCountFree, 1);

    node1.stop();

    AssertEqual(FuncCallNode.PoolCount, 1);
    AssertEqual(FuncCallNode.PoolCountFree, 1);

    node2.stop();

    AssertEqual(FuncCallNode.PoolCount, 1);
    AssertEqual(FuncCallNode.PoolCountFree, 1);
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
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("OK", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDanglingBrackets()
  {
    string bhl = @"

    func test() 
    {
      ()
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      "mismatched input '(' expecting '}'"
    );
  }

  [IsTested()]
  public void TestDanglingBrackets2()
  {
    string bhl = @"

    func foo() 
    {
    }

    func test() 
    {
      foo() ()
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      "no func to call"
    );
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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

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
    //NodeDump(node);
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("NULL;NOT NULL;EQ;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNullStruct()
  {
    string bhl = @"
      
    func void test() 
    {
      IntStruct c = null
      IntStruct c2 = new IntStruct
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
    BindIntStruct(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("NULL;NOT NULL;EQ;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestOrShortCircuit()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c = null
      if(c == null || c.r == 0) {
        trace(""OK;"")
      } else {
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
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("OK;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestAndShortCircuit()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c = null
      if(c != null && c.r == 0) {
        trace(""NEVER;"")
      } else {
        trace(""OK;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("OK;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestAndShortCircuitWithBoolNot()
  {
    string bhl = @"
      
    func void test() 
    {
      bool ready = false
      bool activated = true
      if(!ready && activated) {
        trace(""OK;"")
      } else {
        trace(""NEVER;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("OK;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestAndShortCircuitWithBoolNotForClassMembers()
  {
    string bhl = @"

    class Foo 
    {
      bool ready
      bool activated
    }
      
    func void test() 
    {
      Foo foo = {}
      foo.ready = false
      foo.activated = true
      if(!foo.ready && foo.activated) {
        trace(""OK;"")
      } else {
        trace(""NEVER;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("OK;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestOrShortCircuitWithBoolNotForClassMembers()
  {
    string bhl = @"

    class Foo 
    {
      bool ready
      bool activated
    }
      
    func void test() 
    {
      Foo foo = {}
      foo.ready = false
      foo.activated = true
      if(foo.ready || foo.activated) {
        trace(""OK;"")
      } else {
        trace(""NEVER;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("OK;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestSetNullObjFromUserBinding()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c = mkcolor_null()
      if(c == null) {
        trace(""NULL;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("NULL;", str);
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
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("NULL;NOT NULL;EQ;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNullArrayByDefault()
  {
    string bhl = @"
      
    func void test() 
    {
      Color[] cs
      if(cs == null) {
        trace(""NULL;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("NULL;", str);
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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("012", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestWhileEmptyArrLoop()
  {
    string bhl = @"

    func test() 
    {
      int[] arr = []
      int i = 0
      while(i < arr.Count) {
        int tmp = arr[i]
        trace((string)tmp)
        i = i + 1
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestWhileArrLoop()
  {
    string bhl = @"

    func test() 
    {
      int[] arr = [1,2,3]
      int i = 0
      while(i < arr.Count) {
        int tmp = arr[i]
        trace((string)tmp)
        i = i + 1
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNestedWhileArrLoopWithVarDecls()
  {
    string bhl = @"

    func test() 
    {
      int[] arr = [1,2]
      int i = 0
      while(i < arr.Count) {
        int tmp = arr[i]
        int j = 0
        while(j < arr.Count) {
          int tmp2 = arr[j]
          trace((string)tmp + "","" + (string)tmp2 + "";"")
          j = j + 1
        }
        i = i + 1
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("1,1;1,2;2,1;2,2;", str);
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
      var status = node.run();
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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFor()
  {
    string bhl = @"

    func test() 
    {
      for(int i = 0; i < 3; i = i + 1) {
        trace((string)i)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("012", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForMultiExpression()
  {
    string bhl = @"

    func test() 
    {
      for(int i = 0, int j = 1; i < 3; i = i + 1, j = j + 2) {
        trace((string)(i*j) + "";"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("0;3;10;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForReverse()
  {
    string bhl = @"

    func test() 
    {
      for(int i = 2; i >= 0; i = i - 1) {
        trace((string)i)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("210", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForUseExternalVar()
  {
    string bhl = @"

    func test() 
    {
      int i
      for(i = 1; i < 3; i = i + 1) {
        trace((string)i)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("12", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForNested()
  {
    string bhl = @"

    func test() 
    {
      for(int i = 0; i < 3; i = i + 1) {
        for(int j = 0; j < 2; j = j + 1) {
          trace((string)i + "","" + (string)j + "";"")
        }
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("0,0;0,1;1,0;1,1;2,0;2,1;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForSeveral()
  {
    string bhl = @"

    func test() 
    {
      for(int i = 0; i < 3; i = i + 1) {
        trace((string)i)
      }

      for(i = 0; i < 30; i = i + 10) {
        trace((string)i)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("01201020", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForInParal()
  {
    string bhl = @"

    func test() 
    {
      paral {
        for(int i = 0; i < 3; i = i + 1) {
          trace((string)i)
          yield()
        }
        suspend()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    AssertTrue(BHS.RUNNING == node.run());
    AssertTrue(BHS.RUNNING == node.run());
    AssertTrue(BHS.RUNNING == node.run());
    AssertTrue(BHS.SUCCESS == node.run());

    var str = GetString(trace_stream);
    AssertEqual("012", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForBadPreSection()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      for(i ; i < 3; i = i + 1) {
        trace((string)i)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "mismatched input ';' expecting '='"
    );
  }

  [IsTested()]
  public void TestForEmptyPreSection()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      for(; i < 3; i = i + 1) {
        trace((string)i)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("012", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForEmptyPostSection()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      for(; i < 3;) {
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
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("012", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForBadPostSection()
  {
    string bhl = @"

    func test() 
    {
      for(int i = 0 ; i < 3; i) {
        trace((string)i)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "mismatched input ')' expecting '='"
    );
  }

  [IsTested()]
  public void TestForCondIsRequired()
  {
    string bhl = @"

    func test() 
    {
      for(;;) {
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "no viable alternative at input ';'"
    );
  }

  [IsTested()]
  public void TestForNonBoolCond()
  {
    string bhl = @"

    func int foo() 
    {
      return 14
    }

    func test() 
    {
      for(; foo() ;) {
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "foo():<int> have incompatible types"
    );
  }

  [IsTested()]
  public void TestForeach()
  {
    string bhl = @"

    func test() 
    {
      int[] is = [1, 2, 3]
      foreach(is as int it) {
        trace((string)it)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachForCustomArrayBinding()
  {
    string bhl = @"

    func test() 
    {
      Color[] cs = [{r:1}, {r:2}, {r:3}]
      foreach(cs as Color c) {
        trace((string)c.r)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachUseExternalIteratorVar()
  {
    string bhl = @"

    func test() 
    {
      int it
      int[] is = [1, 2, 3]
      foreach(is as it) {
        trace((string)it)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachWithInPlaceArr()
  {
    string bhl = @"

    func test() 
    {
      foreach([1,2,3] as int it) {
        trace((string)it)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachWithReturnedArr()
  {
    string bhl = @"

    func int[] foo()
    {
      return [1,2,3]
    }

    func test() 
    {
      foreach(foo() as int it) {
        trace((string)it)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachInParal()
  {
    string bhl = @"

    func test() 
    {
      paral {
        foreach([1,2,3] as int it) {
          trace((string)it)
          yield()
        }
        suspend()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    AssertTrue(BHS.RUNNING == node.run());
    AssertTrue(BHS.RUNNING == node.run());
    AssertTrue(BHS.RUNNING == node.run());
    AssertTrue(BHS.SUCCESS == node.run());

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachBreak()
  {
    string bhl = @"

    func test() 
    {
      foreach([1,2,3] as int it) {
        if(it == 3) {
          break
        }
        trace((string)it)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("12", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachSeveral()
  {
    string bhl = @"

    func test() 
    {
      int[] is = [1,2,3]
      foreach(is as int it) {
        trace((string)it)
      }

      foreach(is as int it2) {
        trace((string)it2)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("123123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachNested()
  {
    string bhl = @"

    func test() 
    {
      int[] is = [1,2,3]
      foreach(is as int it) {
        foreach(is as int it2) {
          trace((string)it + "","" + (string)it2 + "";"")
        }
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("1,1;1,2;1,3;2,1;2,2;2,3;3,1;3,2;3,3;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachNested2()
  {
    string bhl = @"

    func test() 
    {
      foreach([1,2,3] as int it) {
        foreach([20,30] as int it2) {
          trace((string)it + "","" + (string)it2 + "";"")
        }
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("1,20;1,30;2,20;2,30;3,20;3,30;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachIteratorVarBadType()
  {
    string bhl = @"

    func test() 
    {
      foreach([1,2,3] as string it) {
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "have incompatible types"
    );
  }

  [IsTested()]
  public void TestForeachArrBadType()
  {
    string bhl = @"

    func float foo()
    {
      return 14
    }

    func test() 
    {
      foreach(foo() as float it) {
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "have incompatible types"
    );
  }

  [IsTested()]
  public void TestForeachExternalIteratorVarBadType()
  {
    string bhl = @"

    func test() 
    {
      string it
      foreach([1,2,3] as it) {
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "have incompatible types"
    );
  }

  [IsTested()]
  public void TestForeachRedeclareError()
  {
    string bhl = @"

    func test() 
    {
      string it
      foreach([1,2,3] as int it) {
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "already defined symbol 'it'"
    );
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
      "not within loop construct"
    );
  }

  [IsTested()]
  public void TestBadBreakInDefer()
  {
    string bhl = @"

    func test() 
    {
      while(true) {
        defer {
          break
        }
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "not within loop construct"
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
    intp.ExecNode(node, 0);

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
          fail()
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
      var status = node.run();
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
      "already defined symbol 'i'"
    );
  }

  [IsTested()]
  public void TestLocalScopeNotSupported2()
  {
    string bhl = @"

    func test() 
    {
      seq {
        int i = 1
        i = i + 1
      }
      seq {
        string i
        i = ""foo""
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "already defined symbol 'i'"
    );
  }


  [IsTested()]
  public void TestVarDeclMustBeInUpperScope()
  {
    string bhl = @"

    func test() 
    {
      seq {
        int i = 1
        i = i + 1
      }
      seq {
        i = i + 2
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "i : symbol not resolved"
    );
  }

  [IsTested()]
  public void TestVarDeclMustBeInUpperScope2()
  {
    string bhl = @"

    func test() 
    {
      paral_all {
        seq {
          int i = 1
          seq {
            i = i + 1
          }
        }
        seq {
          if(i == 2) {
            suspend()
          }
        }
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "i : symbol not resolved"
    );
  }

//TODO: continue not supported yet
//  [IsTested()]
  public void TestWhileContinue()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      while(i < 3) {
        i = i + 1
        if(i == 1) {
          continue
        } 
        trace((string)i)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("02", str);
    CommonChecks(intp);
  }

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
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("012", str);
    CommonChecks(intp);
  }

  public class MakeFooNode : BehaviorTreeTerminalNode
  {
    public override void init()
    {
      var interp = Interpreter.instance;
      var foo = interp.PopValue().ValueClone();
      interp.PushValue(foo);
    }
  }

  [IsTested()]
  public void TestJsonEmptyCtor()
  {
    string bhl = @"
    func float test()
    {
      Color c = {}
      return c.r + c.g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonPartialCtor()
  {
    string bhl = @"
    func float test()
    {
      Color c = {g: 10}
      return c.r + c.g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonFullCtor()
  {
    string bhl = @"
    func float test()
    {
      Color c = {r: 1, g: 10}
      return c.r + c.g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 11);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonCtorNotExpectedMember()
  {
    string bhl = @"
    func void test()
    {
      Color c = {b: 10}
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"no such attribute 'b' in class 'Color"
    );
  }

  [IsTested()]
  public void TestJsonCtorBadType()
  {
    string bhl = @"
    func void test()
    {
      Color c = {r: ""what""}
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"float, @(4,20) ""what"":<string> have incompatible types"
    );
  }

  [IsTested()]
  public void TestJsonExplicitEmptyClass()
  {
    string bhl = @"
      
    func float test() 
    {
      ColorAlpha c = new ColorAlpha {}
      return c.r + c.g + c.a
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonExplicitNoSuchClass()
  {
    string bhl = @"
      
    func void test() 
    {
      any c = new Foo {}
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"type 'Foo' not found"
    );
  }

  [IsTested()]
  public void TestJsonExplicitClass()
  {
    string bhl = @"
      
    func float test() 
    {
      ColorAlpha c = new ColorAlpha {a: 1, g: 10, r: 100}
      return c.r + c.g + c.a
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 111);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonMasterStructWithClass()
  {
    string bhl = @"
      
    func string test() 
    {
      MasterStruct n = {
        child : {str : ""hey""},
        child2 : {str : ""hey2""}
      }
      return n.child2.str
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindMasterStruct(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractStr(intp.ExecNode(node));

    AssertEqual(res, "hey2");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonMasterStructWithStruct()
  {
    string bhl = @"
      
    func int test() 
    {
      MasterStruct n = {
        child_struct : {n: 1, n2:10},
        child_struct2 : {n: 2, n2:20}
      }
      return n.child_struct2.n
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindMasterStruct(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonExplicitSubClass()
  {
    string bhl = @"
      
    func float test() 
    {
      Color c = new ColorAlpha {a: 1, g: 10, r: 100}
      ColorAlpha ca = (ColorAlpha)c
      return ca.r + ca.g + ca.a
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 111);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonExplicitSubClassNotCompatible()
  {
    string bhl = @"
      
    func void test() 
    {
      ColorAlpha c = new Color {g: 10, r: 100}
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"have incompatible types"
    );
  }

  [IsTested()]
  public void TestJsonReturnObject()
  {
    string bhl = @"

    func Color make()
    {
      return {r: 42}
    }
      
    func float test() 
    {
      Color c = make()
      return c.r
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonEmptyArrCtor()
  {
    string bhl = @"
    func int test()
    {
      int[] a = []
      return a.Count 
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonArrCtor()
  {
    string bhl = @"
    func int test()
    {
      int[] a = [1,2,3]
      return a[0] + a[1] + a[2]
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 6);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonArrComplexCtor()
  {
    string bhl = @"
    func float test()
    {
      Color[] cs = [{r:10, g:100}, {g:1000, r:1}]
      return cs[0].r + cs[0].g + cs[1].r + cs[1].g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 1111);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonDefaultEmptyArg()
  {
    string bhl = @"
    func float foo(Color c = {})
    {
      return c.r
    }

    func float test()
    {
      return foo()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonDefaultEmptyArgWithOtherDefaultArgs()
  {
    string bhl = @"
    func float foo(Color c = {}, float a = 10)
    {
      return c.r
    }

    func float test()
    {
      return foo(a : 20)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonDefaultArgWithOtherDefaultArgs()
  {
    string bhl = @"
    func float foo(Color c = {r:20}, float a = 10)
    {
      return c.r + a
    }

    func float test()
    {
      return foo(a : 20)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 40);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonDefaultArgIncompatibleType()
  {
    string bhl = @"
    func float foo(ColorAlpha c = new Color{r:20})
    {
      return c.r
    }

    func float test()
    {
      return foo()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColorAlpha(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"have incompatible types"
    );
  }

  [IsTested()]
  public void TestJsonArgTypeMismatch()
  {
    string bhl = @"
    func float foo(float a = 10)
    {
      return a
    }

    func float test()
    {
      return foo(a : {})
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"can't be specified with {..}"
    );
  }

  [IsTested()]
  public void TestJsonDefaultArg()
  {
    string bhl = @"
    func float foo(Color c = {r:10})
    {
      return c.r
    }

    func float test()
    {
      return foo()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonArrDefaultArg()
  {
    string bhl = @"
    func float foo(Color[] c = [{r:10}])
    {
      return c[0].r
    }

    func float test()
    {
      return foo()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));

    AssertEqual(num, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonObjReAssign()
  {
    string bhl = @"
    func float test()
    {
      Color c = {r: 1}
      c = {g:10}
      return c.r + c.g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonArrReAssign()
  {
    string bhl = @"
    func float test()
    {
      float[] b = [1]
      b = [2,3]
      return b[0] + b[1]
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var num = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 5);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonArrayExplicit()
  {
    string bhl = @"
      
    func float test() 
    {
      Color[] cs = [new ColorAlpha {a:4}, {g:10}, new Color {r:100}]
      ColorAlpha ca = (ColorAlpha)cs[0]
      return ca.a + cs[1].g + cs[2].r
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 114);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonArrayReturn()
  {
    string bhl = @"

    func Color[] make()
    {
      return [new ColorAlpha {a:4}, {g:10}, new Color {r:100}]
    }
      
    func float test() 
    {
      Color[] cs = make()
      ColorAlpha ca = (ColorAlpha)cs[0]
      return ca.a + cs[1].g + cs[2].r
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 114);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonArrayExplicitAsArg()
  {
    string bhl = @"

    func float foo(Color[] cs)
    {
      ColorAlpha ca = (ColorAlpha)cs[0]
      return ca.a + cs[1].g + cs[2].r
    }
      
    func float test() 
    {
      return foo([new ColorAlpha {a:4}, {g:10}, new Color {r:100}])
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 114);
    CommonChecks(intp);
  }


  [IsTested()]
  public void TestJsonArrayExplicitSubClass()
  {
    string bhl = @"
      
    func float test() 
    {
      Color[] cs = [{r:1,g:2}, new ColorAlpha {g: 10, r: 100, a:2}]
      ColorAlpha c = (ColorAlpha)cs[1]
      return c.r + c.g + c.a
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 112);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonArrayExplicitSubClassNotCompatible()
  {
    string bhl = @"
      
    func void test() 
    {
      ColorAlpha[] c = [{r:1,g:2,a:100}, new Color {g: 10, r: 100}]
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"have incompatible types"
    );
  }

  [IsTested()]
  public void TestJsonFuncArg()
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
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("14232342", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncStack()
  {
    string bhl = @"
    func int foo(int v) 
    {
      fail()
      return v
    }

    func bar()
    {
      Foo f = MakeFoo({hey:1, colors:[{r:foo(10)}]})
      trace((string)f.hey)
    }

    func void test() 
    {
      bar()
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindTrace(globs, trace_stream);

    {
      var fn = new FuncBindSymbol("MakeFoo", globs.type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.define(new FuncArgSymbol("conf", globs.type("Foo")));

      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("", str);
    AssertEqual(intp.stack.Count, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStack()
  {
    string bhl = @"

    func int foo()
    {
      fail()
      return 100
    }

    func hey(string b, int a)
    {
    }

    func test() 
    {
      hey(""bar"", foo())
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);
    //NodeDump(node);
    AssertEqual(intp.stack.Count, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackUserBind()
  {
    string bhl = @"

    func int foo()
    {
      fail()
      return 100
    }

    func test() 
    {
      hey(""bar"", foo())
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new SimpleFuncBindSymbol("hey", globs.type("void"),
          delegate() { return BHS.SUCCESS; } );
      fn.define(new FuncArgSymbol("s", globs.type("string")));
      fn.define(new FuncArgSymbol("i", globs.type("int")));
      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);
    //NodeDump(node);
    AssertEqual(intp.stack.Count, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackInUntilSuccess()
  {
    string bhl = @"

    func float bar()
    {
      fail()
      return 1
    }

    func int foo(float b, float c)
    {
      fail()
      return 100
    }

    func hey(string b, int a)
    {
    }

    func test() 
    {
      until_success {
        hey(""bar"", foo(10, bar()))
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    intp.ExecNode(node, ret_vals: 0, keep_running: false);
    AssertEqual(intp.stack.Count, 0);
    intp.ExecNode(node, ret_vals: 0, keep_running: false);
    AssertEqual(intp.stack.Count, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncThisArgOnStackForMethod()
  {
    string bhl = @"

    func float foo()
    {
      fail()
      return 10
    }

    func test() 
    {
      Color c = new Color
      c.mult_summ(foo())
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);
    //NodeDump(node);
    AssertEqual(intp.stack.Count, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForFuncPtr()
  {
    string bhl = @"
    func int foo(int v) 
    {
      fail()
      return v
    }

    func bar()
    {
      int^(int) p = foo
      Foo f = MakeFoo({hey:1, colors:[{r:p(42)}]})
      trace((string)f.hey)
    }

    func void test() 
    {
      bar()
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindTrace(globs, trace_stream);

    {
      var fn = new FuncBindSymbol("MakeFoo", globs.type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.define(new FuncArgSymbol("conf", globs.type("Foo")));

      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("", str);
    AssertEqual(intp.stack.Count, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForLambda()
  {
    string bhl = @"
    func bar()
    {
      Foo f = MakeFoo({hey:1, colors:[{r:
          func int (int v) { 
            fail()
            return v
          }(42) 
        }]})
      trace((string)f.hey)
    }

    func void test() 
    {
      bar()
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindTrace(globs, trace_stream);

    {
      var fn = new FuncBindSymbol("MakeFoo", globs.type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.define(new FuncArgSymbol("conf", globs.type("Foo")));

      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("", str);
    AssertEqual(intp.stack.Count, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanArgsStackInParal()
  {
    string bhl = @"
    func int foo(int ticks) 
    {
      WaitTicks(ticks, false)
      return 42
    }

    func bar()
    {
      Foo f
      paral_all {
        seq {
          f = MakeFoo({hey:10, colors:[{r:foo(2)}]})
        }
        seq {
          f = MakeFoo({hey:20, colors:[{r:foo(3)}]})
        }
      }
      trace((string)f.hey)
    }

    func void test() 
    {
      bar()
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    {
      var fn = new FuncBindSymbol("MakeFoo", globs.type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.define(new FuncArgSymbol("conf", globs.type("Foo")));

      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var status = node.run();
    AssertTrue(status == BHS.RUNNING);
    status = node.run();
    AssertTrue(status == BHS.FAILURE);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("", str);
    AssertEqual(intp.stack.Count, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncPtrArgsStackInParal()
  {
    string bhl = @"
    func int foo(int ticks) 
    {
      WaitTicks(ticks, false)
      return 42
    }

    func bar()
    {
      int^(int) p = foo
      Foo f
      paral_all {
        seq {
          f = MakeFoo({hey:10, colors:[{r:p(2)}]})
        }
        seq {
          f = MakeFoo({hey:20, colors:[{r:p(3)}]})
        }
      }
      trace((string)f.hey)
    }

    func void test() 
    {
      bar()
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    {
      var fn = new FuncBindSymbol("MakeFoo", globs.type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.define(new FuncArgSymbol("conf", globs.type("Foo")));

      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var status = node.run();
    AssertTrue(status == BHS.RUNNING);
    status = node.run();
    AssertTrue(status == BHS.FAILURE);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("", str);
    AssertEqual(intp.stack.Count, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanArgsStackInParalForLambda()
  {
    string bhl = @"
    func bar()
    {
      Foo f
      paral_all {
        seq {
          f = MakeFoo({hey:10, colors:[{r:
              func int (int ticks) 
              { 
                WaitTicks(ticks, false)
                fail()
                return ticks
              }(2) 
              }]})
        }
        seq {
          f = MakeFoo({hey:20, colors:[{r:
              func int (int ticks) 
              { 
                WaitTicks(ticks, false)
                fail()
                return ticks
              }(3) 
              }]})
        }
      }
      trace((string)f.hey)
    }

    func void test() 
    {
      bar()
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    {
      var fn = new FuncBindSymbol("MakeFoo", globs.type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.define(new FuncArgSymbol("conf", globs.type("Foo")));

      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var status = node.run();
    AssertTrue(status == BHS.RUNNING);
    status = node.run();
    AssertTrue(status == BHS.FAILURE);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("", str);
    AssertEqual(intp.stack.Count, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInterleaveFuncStackInParal()
  {
    string bhl = @"
    func int foo(int ticks) 
    {
      WaitTicks(ticks, true)
      return ticks
    }

    func bar()
    {
      Foo f
      paral_all {
        seq {
          f = MakeFoo({hey:foo(3), colors:[]})
          trace((string)f.hey)
        }
        seq {
          f = MakeFoo({hey:foo(2), colors:[]})
          trace((string)f.hey)
        }
      }
      trace((string)f.hey)
    }

    func void test() 
    {
      bar()
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);
    BindLog(globs);

    {
      var fn = new FuncBindSymbol("MakeFoo", globs.type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.define(new FuncArgSymbol("conf", globs.type("Foo")));

      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");

    var status = node.run();
    AssertTrue(status == BHS.RUNNING);
    status = node.run();
    AssertTrue(status == BHS.RUNNING);
    status = node.run();
    AssertTrue(status == BHS.SUCCESS);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("233", str);
    AssertEqual(intp.stack.Count, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonFuncArgChainCall()
  {
    string bhl = @"
    func void test(float b) 
    {
      trace((string)MakeFoo({hey:142, colors:[{r:2}, {g:3}, {g:b}]}).colors.Count)
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
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("3", str);
    CommonChecks(intp);
  }

  public class DummyModuleLoader : IModuleLoader
  {
    public AST_Module LoadModule(HashedName id)
    {
      return new AST_Module();
    }
  }

  [IsTested()]
  public void TestEmptyUserClass()
  {
    string bhl = @"

    class Foo { }
      
    func bool test() 
    {
      Foo f = {}
      return f != null
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    bool res = ExtractBool(intp.ExecNode(node));

    //NodeDump(node);
    AssertTrue(res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassBindConflict()
  {
    string bhl = @"

    class Foo { }
      
    func bool test() 
    {
      Foo f = {}
      return f != null
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindFoo(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      "already defined symbol 'Foo'"
    );
  }

  [IsTested()]
  public void TestSeveralEmptyUserClasses()
  {
    string bhl = @"

    class Foo { }
    class Bar { }
      
    func bool test() 
    {
      Foo f = {}
      Bar b = {}
      return f != null && b != null
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    bool res = ExtractBool(intp.ExecNode(node));

    //NodeDump(node);
    AssertTrue(res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassDefaultInit()
  {
    string bhl = @"

    class Foo { 
      float b
      int c
      string s
    }
      
    func void test() 
    {
      Foo f = {}
      trace((string)f.b + "";"" + (string)f.c + "";"" + f.s + "";"")
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("0;0;;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassDefaultInitInt()
  {
    string bhl = @"

    class Foo { 
      int c
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.c == 0
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var res = ExtractBool(intp.ExecNode(node));

    AssertTrue(res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTmpUserClassReadDefaultField()
  {
    string bhl = @"

    class Foo { 
      int c
    }
      
    func int test() 
    {
      return (new Foo).c
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(res, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTmpUserClassReadField()
  {
    string bhl = @"

    class Foo { 
      int c
    }
      
    func int test() 
    {
      return (new Foo{c: 10}).c
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(res, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTmpUserClassWriteFieldNotAllowed()
  {
    string bhl = @"

    class Foo { 
      int c
    }
      
    func test() 
    {
      (new Foo{c: 10}).c = 20
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      @"mismatched input '(' expecting '}'"
    );
  }

  [IsTested()]
  public void TestTypeidIsEncodedInUserClass()
  {
    string bhl = @"

    class Foo { }
      
    func Foo test() 
    {
      return {}
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = intp.ExecNode(node);

    AssertEqual(res.val.num, (new HashedName("Foo")).n);
    //NOTE: returned value must be manually removed
    DynValDict.Del((res.val.obj as DynValDict));
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTypeidIsEncodedInUserClassInHierarchy()
  {
    string bhl = @"

    class Foo { }
    class Bar : Foo { }
      
    func Bar test() 
    {
      return {}
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = intp.ExecNode(node);

    AssertEqual(res.val.num, (new HashedName("Bar")).n);
    //NOTE: returned value must be manually removed
    DynValDict.Del((res.val.obj as DynValDict));
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTypeidForUserClass()
  {
    string bhl = @"

    class Foo { }
      
    func int test() 
    {
      return typeid(Foo)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, (new HashedName("Foo")).n);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTypeidForBuiltinType()
  {
    string bhl = @"

    func int test() 
    {
      return typeid(int)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, (new HashedName("int")).n);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTypeidForBuiltinArrType()
  {
    string bhl = @"

    func int test() 
    {
      return typeid(int[])
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, (new HashedName("int[]")).n);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTypeidEqual()
  {
    string bhl = @"

    class Foo { }
      
    func bool test() 
    {
      return typeid(Foo) == typeid(Foo)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractBool(intp.ExecNode(node));

    //NodeDump(node);
    AssertTrue(res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTypeidNotEqual()
  {
    string bhl = @"

    class Foo { }
    class Bar { }
      
    func bool test() 
    {
      return typeid(Foo) == typeid(Bar)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractBool(intp.ExecNode(node));

    //NodeDump(node);
    AssertTrue(res == false);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTypeidNotEqualArrType()
  {
    string bhl = @"

    func bool test() 
    {
      return typeid(int[]) == typeid(float[])
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractBool(intp.ExecNode(node));

    //NodeDump(node);
    AssertTrue(res == false);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTypeidBadType()
  {
    string bhl = @"

    func int test() 
    {
      return typeid(Foo)
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      @"type 'Foo' not found"
    );
  }

  [IsTested()]
  public void TestEmptyParenExpression()
  {
    string bhl = @"

    func void test() 
    {
      ().foo
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      @"mismatched input '(' expecting '}'"
    );
  }

  [IsTested()]
  public void TestUserClassDefaultInitFloat()
  {
    string bhl = @"

    class Foo { 
      float c
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.c == 0
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var res = ExtractBool(intp.ExecNode(node));

    AssertTrue(res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassDefaultInitStr()
  {
    string bhl = @"

    class Foo { 
      string c
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.c == """"
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var res = ExtractBool(intp.ExecNode(node));

    AssertTrue(res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassDefaultInitBool()
  {
    string bhl = @"

    class Foo { 
      bool c
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.c == false
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var res = ExtractBool(intp.ExecNode(node));

    AssertTrue(res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassDefaultInitEnum()
  {
    string bhl = @"

    enum Bar {
      NONE = 0
      FOO  = 1
    }

    class Foo { 
      Bar b
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.b == Bar::NONE
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var res = ExtractBool(intp.ExecNode(node));

    AssertTrue(res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassDefaultInitArr()
  {
    string bhl = @"

    class Foo { 
      int[] a
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.a == null
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var res = ExtractBool(intp.ExecNode(node));

    AssertTrue(res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassWithArr()
  {
    string bhl = @"

    class Foo { 
      int[] a
    }
      
    func int test() 
    {
      Foo f = {a : [10, 20]}
      return f.a[1]
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 20);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassDefaultInitFuncPtr()
  {
    string bhl = @"

    class Foo { 
      void^(int) ptr
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.ptr == null
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var res = ExtractBool(intp.ExecNode(node));

    AssertTrue(res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassWithFuncPtr()
  {
    string bhl = @"

    func int foo(int a)
    {
      return a + 1
    }

    class Foo { 
      int^(int) ptr
    }
      
    func int test() 
    {
      Foo f = {}
      f.ptr = foo
      return f.ptr(2)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(3, res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackClassPtr()
  {
    string bhl = @"

    class Foo
    {
      void^(int,int) ptr
    }

    func int foo()
    {
      fail()
      return 10
    }

    func void bar(int a, int b)
    {
    }

    func test() 
    {
      Foo f = {}
      f.ptr = bar
      f.ptr(10, foo())
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);
    //NodeDump(node);
    AssertEqual(intp.stack.Count, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassWithFuncPtrsArray()
  {
    string bhl = @"

    func int foo(int a)
    {
      return a + 1
    }

    func int foo2(int a)
    {
      return a + 10
    }

    class Foo { 
      int^(int)[] ptrs
    }
      
    func int test() 
    {
      Foo f = {ptrs: []}
      f.ptrs.Add(foo)
      f.ptrs.Add(foo2)
      return f.ptrs[1](2)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(12, res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassWithFuncPtrsArrayCleanArgsStack()
  {
    string bhl = @"

    func int foo(int a,int b)
    {
      return a + 1
    }

    func int foo2(int a,int b)
    {
      return a + 10
    }

    class Foo { 
      int^(int,int)[] ptrs
    }

    func int bar()
    {
      fail()
      return 1
    }
      
    func void test() 
    {
      Foo f = {ptrs: []}
      f.ptrs.Add(foo)
      f.ptrs.Add(foo2)
      f.ptrs[1](2, bar())
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);

    intp.ExecNode(node, 0);
    //NodeDump(node);
    AssertEqual(intp.stack.Count, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassWithSubClass()
  {
    string bhl = @"

    class Bar {
      int[] a
    }

    class Foo { 
      Bar b
    }
      
    func int test() 
    {
      Foo f = {b : { a : [10, 21] } }
      return f.b.a[1]
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 21);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassDefaultInitStringConcat()
  {
    string bhl = @"

    class Foo { 
      string s1
      string s2
    }
      
    func void test() 
    {
      Foo f = {}
      string s = f.s1 + f.s2
      trace(s)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassMember()
  {
    string bhl = @"

    class Foo { 
      float b
    }
      
    func float test() 
    {
      Foo f = { b : 101 }
      f.b = f.b + 1
      return f.b
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 102);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassAny()
  {
    string bhl = @"

    class Foo { }
      
    func bool test() 
    {
      Foo f = {}
      any foo = f
      return foo != null
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    bool res = ExtractBool(intp.ExecNode(node));

    //NodeDump(node);
    AssertTrue(res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassAnyCastBack()
  {
    string bhl = @"

    class Foo { int x }
      
    func int test() 
    {
      Foo f = {x : 10}
      any foo = f
      Foo f1 = (Foo)foo
      return f1.x
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserSeveralClassMembers()
  {
    string bhl = @"

    class Foo { 
      float b
      int c
    }
      
    func float test() 
    {
      Foo f = { c : 2, b : 101.5 }
      f.b = f.b + f.c
      return f.b
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 103.5);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserSeveralClassMembers2()
  {
    string bhl = @"

    class Foo { 
      float b
      int c
    }

     class Bar {
       float a
     }
      
    func float test() 
    {
      Foo f = { c : 2, b : 101.5 }
      Bar b = { a : 10 }
      f.b = f.b + f.c + b.a
      return f.b
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 113.5);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestArrayOfUserClasses()
  {
    string bhl = @"

    class Foo { 
      float b
      int c
    }

    func float test() 
    {
      Foo[] fs = [{b:1, c:2}]
      fs.Add({b:10, c:20})

      return fs[0].b + fs[0].c + fs[1].b + fs[1].c
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 33);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassCast()
  {
    string bhl = @"

    class Foo { 
      float b
    }
      
    func float test() 
    {
      Foo f = { b : 101 }
      any a = f
      Foo f1 = (Foo)a
      return f1.b
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 101);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassInlineCast()
  {
    string bhl = @"

    class Foo { 
      float b
    }
      
    func float test() 
    {
      Foo f = { b : 101 }
      any a = f
      return ((Foo)a).b
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 101);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassInlineCastArr()
  {
    string bhl = @"

    class Foo { 
      int[] b
    }
      
    func int test() 
    {
      Foo f = { b : [101, 102] }
      any a = f
      return ((Foo)a).b[1]
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 102);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassInlineCastArr2()
  {
    string bhl = @"

    class Foo { 
      int[] b
    }
      
    func int test() 
    {
      Foo f = { b : [101, 102] }
      any a = f
      return ((Foo)a).b.Count
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestChildUserClass()
  {
    string bhl = @"

    class Base {
      float x 
    }

    class Foo : Base 
    { 
      float y
    }
      
    func float test() 
    {
      Foo f = { x : 1, y : 2}
      return f.x + f.y
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 3);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestChildUserClassDowncast()
  {
    string bhl = @"

    class Base {
      float x 
    }

    class Foo : Base 
    { 
      float y
    }
      
    func float test() 
    {
      Foo f = { x : 1, y : 2}
      Base b = (Foo)f
      return b.x
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestChildUserClassUpcast()
  {
    string bhl = @"

    class Base {
      float x 
    }

    class Foo : Base 
    { 
      float y
    }
      
    func float test() 
    {
      Foo f = { x : 1, y : 2}
      Base b = (Foo)f
      Foo f2 = (Foo)b
      return f2.x + f2.y
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 3);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestChildUserClassDefaultInit()
  {
    string bhl = @"

    class Base {
      float x 
    }

    class Foo : Base 
    { 
      float y
    }
      
    func float test() 
    {
      Foo f = { y : 2}
      return f.x + f.y
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestChildUserClassAlreadyDefinedMember()
  {
    string bhl = @"

    class Base {
      float x 
    }

    class Foo : Base 
    { 
      int x
    }
      
    func float test() 
    {
      Foo f = { x : 2}
      return f.x
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      @"already defined symbol 'x'"
    );
  }

  [IsTested()]
  public void TestChildUserClassOfBindClass()
  {
    string bhl = @"

    class ColorA : Color 
    { 
      float a
    }
      
    func float test() 
    {
      ColorA c = { r : 1, g : 2, a : 3}
      return c.r + c.g + c.a
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl, globs);
      },
      @"extending C# bound classes not currently supported"
    );
  }

  [IsTested()]
  public void TestGlobalVariableRead()
  {
    string bhl = @"

    class Foo { 
      float b
    }

    Foo foo = {b : 100}
      
    func float test() 
    {
      return foo.b
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 100);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestGlobalVariableWrite()
  {
    string bhl = @"

    class Foo { 
      float b
    }

    Foo foo = {b : 100}

    func float bar()
    {
      return foo.b
    }
      
    func float test() 
    {
      foo.b = 101
      return bar()
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 101);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestGlobalVariableAlreadyDeclared()
  {
    string bhl = @"

    int foo = 0
    int foo = 1
      
    func int test() 
    {
      return foo
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      @"already defined symbol 'foo'"
    );
  }

  [IsTested()]
  public void TestLocalVariableHasPriorityOverGlobalOne()
  {
    string bhl = @"

    class Foo { 
      float b
    }

    Foo foo = {b : 100}
      
    func float test() 
    {
      Foo foo = {b : 200}
      return foo.b
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 200);
    CommonChecks(intp);
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
    var res = intp.ExecNode(node).val;

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
    var res = intp.ExecNode(node).val;

    AssertEqual(res.num, 4);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestImportWithCycles()
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
    var res = intp.ExecNode(node).val;

    AssertEqual(res.num, 23);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestImportConflict()
  {
    string bhl1 = @"
    import ""bhl2""  

    func float bar() 
    {
      return 1
    }

    func float test() 
    {
      return bar()
    }
    ";

    string bhl2 = @"
    func float bar()
    {
      return 2
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var mreg = new ModuleRegistry();

    var fp2src = new Dictionary<string, string>();
    fp2src.Add("bhl1", bhl1);
    fp2src.Add("bhl2", bhl2);
    mreg.test_sources = fp2src;
    AssertError<UserError>(
      delegate() { 
        Interpret(fp2src, globs, mreg, new DummyModuleLoader());
      },
      @"already defined symbol 'bar'"
    );
  }

  [IsTested()]
  public void TestImportClass()
  {
    string bhl1 = @"
    import ""bhl2""  
    func float test(float k) 
    {
      Foo f = { x : k }
      return bar(f)
    }
    ";

    string bhl2 = @"
    import ""bhl3""  

    class Foo
    {
      float x
    }

    func float bar(Foo f)
    {
      return hey(f.x)
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
    node.SetArgs(DynVal.NewNum(42));
    //NodeDump(node);
    var res = intp.ExecNode(node).val;

    AssertEqual(res.num, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestImportClassConflict()
  {
    string bhl1 = @"
    import ""bhl2""  

    class Bar { }

    func test() { }
    ";

    string bhl2 = @"
    class Bar { }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var mreg = new ModuleRegistry();

    var fp2src = new Dictionary<string, string>();
    fp2src.Add("bhl1", bhl1);
    fp2src.Add("bhl2", bhl2);
    mreg.test_sources = fp2src;
    AssertError<UserError>(
      delegate() { 
        Interpret(fp2src, globs, mreg, new DummyModuleLoader());
      },
      @"already defined symbol 'Bar'"
    );
  }

  [IsTested()]
  public void TestImportEnum()
  {
    string bhl1 = @"
    import ""bhl2""  

    func float test() 
    {
      Foo f = Foo::B
      return bar(f)
    }
    ";

    string bhl2 = @"
    enum Foo
    {
      A = 2
      B = 3
    }

    func int bar(Foo f)
    {
      return (int)f * 10
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var mreg = new ModuleRegistry();

    var fp2src = new Dictionary<string, string>();
    fp2src.Add("bhl1", bhl1);
    fp2src.Add("bhl2", bhl2);
    mreg.test_sources = fp2src;
    var intp = Interpret(fp2src, globs, mreg, new DummyModuleLoader());

    var node = intp.GetFuncNode("bhl1", "test");
    //NodeDump(node);
    var res = intp.ExecNode(node).val;

    AssertEqual(res.num, 30);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestImportEnumConflict()
  {
    string bhl1 = @"
    import ""bhl2""  

    enum Bar { 
      FOO = 1
    }

    func test() { }
    ";

    string bhl2 = @"
    enum Bar { 
      BAR = 2
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var mreg = new ModuleRegistry();

    var fp2src = new Dictionary<string, string>();
    fp2src.Add("bhl1", bhl1);
    fp2src.Add("bhl2", bhl2);
    mreg.test_sources = fp2src;
    AssertError<UserError>(
      delegate() { 
        Interpret(fp2src, globs, mreg, new DummyModuleLoader());
      },
      @"already defined symbol 'Bar'"
    );
  }

  [IsTested()]
  public void TestImportGlobalVar()
  {
    string bhl1 = @"
    import ""bhl3""  
    func float test() 
    {
      return foo.x
    }
    ";

    string bhl2 = @"

    class Foo
    {
      float x
    }

    ";

    string bhl3 = @"
    import ""bhl2""  

    Foo foo = {x : 10}

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
    //NodeDump(node);
    var res = intp.ExecNode(node).val;

    AssertEqual(res.num, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestImportGlobalVarConflict()
  {
    string bhl1 = @"
    import ""bhl2""  

    int foo = 10

    func test() { }
    ";

    string bhl2 = @"
    float foo = 100
    ";

    var globs = SymbolTable.CreateBuiltins();

    var mreg = new ModuleRegistry();

    var fp2src = new Dictionary<string, string>();
    fp2src.Add("bhl1", bhl1);
    fp2src.Add("bhl2", bhl2);
    mreg.test_sources = fp2src;
    AssertError<UserError>(
      delegate() { 
        Interpret(fp2src, globs, mreg, new DummyModuleLoader());
      },
      @"already defined symbol 'foo'"
    );
  }

  [IsTested()]
  public void TestImportGlobalExecutionOnlyOnce()
  {
    string bhl1 = @"
    import ""bhl2""  
    import ""bhl2""

    func test1() { }
    ";

    string bhl2 = @"
    func int dummy()
    {
      trace(""once"")
      return 1
    }

    int a = dummy()

    ";

    string bhl3 = @"
    import ""bhl2""
    import ""bhl2""

    func test2() { }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    var mreg = new ModuleRegistry();

    var fp2src = new Dictionary<string, string>();
    fp2src.Add("bhl1", bhl1);
    fp2src.Add("bhl2", bhl2);
    fp2src.Add("bhl3", bhl3);
    mreg.test_sources = fp2src;

    var intp = Interpret(fp2src, globs, mreg, new DummyModuleLoader());

    var str = GetString(trace_stream);
    AssertEqual("once", str);

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
          delegate()
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
      globs.define(new GenericArrayTypeSymbol(globs, new TypeRef(cl)));

      cl.define(new FieldSymbol("position", globs.type("Vec3"),
        delegate(DynVal ctx, ref DynVal v)
        {},
        //setter not allowed
        null
      ));
    }

    {
      var fn = new SimpleFuncBindSymbol("get_units", globs.type("Unit[]"),
        delegate()
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
    if(node == null)
      throw new Exception("???");
    //NodeDump(node);
  }

  public class TestNode : BehaviorTreeTerminalNode
  {
    public BHS status = BHS.SUCCESS;
    public int execs;
    public int inits;
    public int deinits;
    public int defers;

    public override BHS execute()
    {
      ++execs;
      return status;
    }

    public override void init()
    {
      ++inits;
    }

    public override void deinit()
    {
      ++deinits;
    }

    public override void defer()
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

    AssertTrue(s.run() == BHS.RUNNING);
    AssertEqual(t1.inits, 1);
    AssertEqual(t1.execs, 1);
    AssertEqual(t1.deinits, 0);
    AssertEqual(t1.defers, 0);
    AssertEqual(t2.inits, 0);
    AssertEqual(t2.execs, 0);
    AssertEqual(t2.deinits, 0);
    AssertEqual(t2.defers, 0);

    t1.status = BHS.SUCCESS;

    AssertTrue(s.run() == BHS.SUCCESS);
    AssertEqual(t1.inits, 1);
    AssertEqual(t1.execs, 2);
    AssertEqual(t1.deinits, 1);
    AssertEqual(t1.defers, 1);
    AssertEqual(t2.inits, 1);
    AssertEqual(t2.execs, 1);
    AssertEqual(t2.deinits, 1);
    AssertEqual(t2.defers, 1);

    s.stop();
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

    AssertTrue(d.run() == BHS.RUNNING);
    AssertEqual(t.inits, 1);
    AssertEqual(t.execs, 1);
    AssertEqual(t.deinits, 0);
    AssertEqual(t.defers, 0);

    t.status = BHS.SUCCESS;
    AssertTrue(d.run() == BHS.SUCCESS);
    AssertEqual(t.inits, 1);
    AssertEqual(t.execs, 2);
    AssertEqual(t.deinits, 1);
    AssertEqual(t.defers, 0);

    d.stop();
    AssertEqual(t.defers, 1);
  }

  [IsTested()]
  public void TestReturnMultiple()
  {
    string bhl = @"
      
    func float,string test() 
    {
      return 100,""foo""
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var vals = intp.ExecNode(node, 2).vals;

    AssertEqual(vals[0].num, 100);
    AssertEqual(vals[1].str, "foo");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnMultiple3()
  {
    string bhl = @"
      
    func float,string,int test() 
    {
      return 100,""foo"",3
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var vals = intp.ExecNode(node, 3).vals;

    AssertEqual(vals[0].num, 100);
    AssertEqual(vals[1].str, "foo");
    AssertEqual(vals[2].num, 3);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnMultipleInFuncBadCast()
  {
    string bhl = @"

    func float,string foo() 
    {
      return ""bar"",100
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      @"float, @(5,13) ""bar"":<string> have incompatible types"
    );
  }

  [IsTested()]
  public void TestReturnMultipleInFuncNotEnough()
  {
    string bhl = @"

    func float,string foo() 
    {
      return ""bar""
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      @"<float,string>, @(5,13) ""bar"":<string> have incompatible types"
    );
  }

  [IsTested()]
  public void TestReturnMultipleInFuncTooMany()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 1,""bar"",1
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      "multi return size doesn't match destination"
    );
  }

  [IsTested()]
  public void TestReturnMultipleVarAssign()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func float,string test() 
    {
      float a,string s = foo()
      return a,s
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindLog(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var vals = intp.ExecNode(node, 2).vals;
    //NodeDump(node);

    AssertEqual(vals[0].num, 100);
    AssertEqual(vals[1].str, "bar");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnMultipleVarAssign2()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func float,string test() 
    {
      string s
      float a,s = foo()
      return a,s
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var vals = intp.ExecNode(node, 2).vals;

    AssertEqual(vals[0].num, 100);
    AssertEqual(vals[1].str, "bar");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnMultipleVarAssignObjectAttr()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func float,string test() 
    {
      string s
      Color c = {}
      c.r,s = foo()
      return c.r,s
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var vals = intp.ExecNode(node, 2).vals;

    AssertEqual(vals[0].num, 100);
    AssertEqual(vals[1].str, "bar");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnMultipleVarAssignArrItem()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func float,string test() 
    {
      string[] s = [""""]
      float r,s[0] = foo()
      return r,s[0]
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var vals = intp.ExecNode(node, 2).vals;

    AssertEqual(vals[0].num, 100);
    AssertEqual(vals[1].str, "bar");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnMultipleVarAssignArrItem2()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func float,string test() 
    {
      string s
      Color[] c = [{}]
      c[0].r,s = foo()
      return c[0].r,s
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var vals = intp.ExecNode(node, 2).vals;

    AssertEqual(vals[0].num, 100);
    AssertEqual(vals[1].str, "bar");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnMultipleVarAssignNoSuchSymbol()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func float,string test() 
    {
      float a,s = foo()
      return a,s
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      "s : symbol not resolved"
    );
  }

  [IsTested()]
  public void TestReturnMultipleLambda()
  {
    string bhl = @"

    func float,string test() 
    {
      return func float,string () 
        { return 30, ""foo"" }()
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var vals = intp.ExecNode(node, 2).vals;

    AssertEqual(vals[0].num, 30);
    AssertEqual(vals[1].str, "foo");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnMultipleLambdaViaVars()
  {
    string bhl = @"

    func float,string test() 
    {
      float a,string s = func float,string () 
        { return 30, ""foo"" }()
      return a,s
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var vals = intp.ExecNode(node, 2).vals;

    AssertEqual(vals[0].num, 30);
    AssertEqual(vals[1].str, "foo");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnMultipleBadCast()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func void test() 
    {
      string a,float s = foo()
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      "a:<string>, float have incompatible types"
    );
  }

  [IsTested()]
  public void TestReturnMultipleNotEnough()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func void test() 
    {
      float s = foo()
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      "multi return size doesn't match destination"
    );
  }

  [IsTested()]
  public void TestReturnMultipleTooMany()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func void test() 
    {
      float s,string a,int f = foo()
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret("", bhl);
      },
      "multi return size doesn't match destination"
    );
  }

  [IsTested()]
  public void TestVarValueNonConsumed()
  {
    string bhl = @"

    func int test() 
    {
      float foo = 1
      foo
      return 2
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncPtrReturnNonConsumed()
  {
    string bhl = @"

    func int test() 
    {
      int^() ptr = func int() {
        return 1
      }
      ptr()
      return 2
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncPtrArrReturnNonConsumed()
  {
    string bhl = @"

    func int test() 
    {
      int[]^() ptr = func int[]() {
        return [1,2]
      }
      ptr()
      return 2
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestAttributeNonConsumed()
  {
    string bhl = @"

    func int test() 
    {
      Color c = new Color
      c.r
      return 2
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnNonConsumed()
  {
    string bhl = @"

    func float foo() 
    {
      return 100
    }
      
    func int test() 
    {
      foo()
      return 2
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));
    //NodeDump(node);

    AssertEqual(res, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnNonConsumedInParal()
  {
    string bhl = @"

    func float foo() 
    {
      return 100
    }
      
    func int test() 
    {
      paral {
        foo()
        suspend()
      }
      return 2
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnMultipleNonConsumed()
  {
    string bhl = @"

    func float,string foo() 
    {
      return 100,""bar""
    }
      
    func int test() 
    {
      foo()
      return 2
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");
    var res = ExtractNum(intp.ExecNode(node));

    AssertEqual(res, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnMultipleFromBindings()
  {
    string bhl = @"
      
    func float,string test() 
    {
      return func_mult()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new SimpleFuncBindSymbol("func_mult", globs.type("float,string"), 
          delegate()
          {
            var interp = Interpreter.instance;
            
            interp.PushValue(DynVal.NewStr("foo"));
            interp.PushValue(DynVal.NewNum(42));
            return BHS.SUCCESS;
          }
        );
      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    //NodeDump(node);
    var vals = intp.ExecNode(node, 2).vals;

    AssertEqual(vals[0].num, 42);
    AssertEqual(vals[1].str, "foo");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnMultiple4FromBindings()
  {
    string bhl = @"
      
    func float,string,int,float test() 
    {
      float a,string b,int c,float d = func_mult()
      return a,b,c,d
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new SimpleFuncBindSymbol("func_mult", globs.type("float,string,int,float"), 
          delegate()
          {
            var interp = Interpreter.instance;
            
            interp.PushValue(DynVal.NewNum(42.5));
            interp.PushValue(DynVal.NewNum(12));
            interp.PushValue(DynVal.NewStr("foo"));
            interp.PushValue(DynVal.NewNum(104));
            return BHS.SUCCESS;
          }
        );
      globs.define(fn);
    }

    var intp = Interpret("", bhl, globs);
    var node = intp.GetFuncNode("test");
    var vals = intp.ExecNode(node, 4).vals;
    //NodeDump(node);

    AssertEqual(vals[0].num, 104);
    AssertEqual(vals[1].str, "foo");
    AssertEqual(vals[2].num, 12);
    AssertEqual(vals[3].num, 42.5);
    CommonChecks(intp);
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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("INC1;DEC0;INC1;INC2;DEC1;REL1;REFS1;INC2;INC3;INC4;DEC3;REL3;REFS3;DEC2;REL2;INC3;DEC2;REL2;REFS2;DEC1;REL1;DEC0;REL0;", str);
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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;INC1;INC1;DEC0;INC1;INC2;DEC1;REL1;DEC0;REL0;DEC0;REL0;", str);
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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

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
    intp.ExecNode(node, 0);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;INC1;INC2;DEC1;REL1;DEC0;INC1;INC2;DEC1;INC2;INC3;DEC2;INC3;DEC2;REL2;DEC1;REL1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestStack()
  {
    //Push/Pop
    {
      var st = new FastStack<int>(16); 
      st.Push(1);
      st.Push(10);

      AssertEqual(st.Count, 2);
      AssertEqual(1, st[0]);
      AssertEqual(10, st[1]);

      AssertEqual(10, st.Pop());
      AssertEqual(st.Count, 1);
      AssertEqual(1, st[0]);

      AssertEqual(1, st.Pop());
      AssertEqual(st.Count, 0);
    }

    //Push/PopFast
    {
      var st = new FastStack<int>(16); 
      st.Push(1);
      st.Push(10);

      AssertEqual(st.Count, 2);
      AssertEqual(1, st[0]);
      AssertEqual(10, st[1]);

      AssertEqual(10, st.PopFast());
      AssertEqual(st.Count, 1);
      AssertEqual(1, st[0]);

      AssertEqual(1, st.PopFast());
      AssertEqual(st.Count, 0);
    }

    //Push/DecFast
    {
      var st = new FastStack<int>(16); 
      st.Push(1);
      st.Push(10);

      AssertEqual(st.Count, 2);
      AssertEqual(1, st[0]);
      AssertEqual(10, st[1]);

      st.DecFast();
      AssertEqual(st.Count, 1);
      AssertEqual(1, st[0]);

      st.DecFast();
      AssertEqual(st.Count, 0);
    }

    //RemoveAtFast
    {
      var st = new FastStack<int>(16); 
      st.Push(1);
      st.Push(2);
      st.Push(3);

      st.RemoveAtFast(1);
      AssertEqual(st.Count, 2);
      AssertEqual(1, st[0]);
      AssertEqual(3, st[1]);
    }

    //RemoveAtFast
    {
      var st = new FastStack<int>(16); 
      st.Push(1);
      st.Push(2);
      st.Push(3);

      st.RemoveAtFast(0);
      AssertEqual(st.Count, 2);
      AssertEqual(2, st[0]);
      AssertEqual(3, st[1]);
    }

    //RemoveAtFast
    {
      var st = new FastStack<int>(16); 
      st.Push(1);
      st.Push(2);
      st.Push(3);

      st.RemoveAtFast(2);
      AssertEqual(st.Count, 2);
      AssertEqual(1, st[0]);
      AssertEqual(2, st[1]);
    }
  }
  
  [IsTested()]
  public void TestDynVal()
  {
    // ToAny()
    {
      string str = "str";
      double num = 33.0;
      bool bl = false;
      object obj = new List<int>(33);

      var dv = DynVal.NewStr(str);
      Assert(dv.ToAny() == (object)str);
      
      dv = DynVal.NewNum(num);
      Assert((double)dv.ToAny() == num);

      dv = DynVal.NewBool(bl);
      Assert((bool)dv.ToAny() == bl);

      dv = DynVal.NewObj(obj);
      Assert(dv.ToAny() == obj);
    }
  }

  static int Fib(int x)
  {
    if(x == 0)
      return 0;
    else 
    {
      if(x == 1) 
        return 1;
      else
        return Fib(x-1) + Fib(x-2);
    }
  }

  [IsTested()]
  public void TestFib()
  {
    string bhl = @"

    func int fib(int x)
    {
      if(x == 0) {
        return 0
      } else {
        if(x == 1) {
          return 1
        } else {
          return fib(x - 1) + fib(x - 2)
        }
      }
    }
      
    func int test(int x) 
    {
      return fib(x)
    }
    ";

    var intp = Interpret("", bhl);
    var node = intp.GetFuncNode("test");

    const int x = 15;

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      node.SetArgs(DynVal.NewNum(x));
      ExtractNum(intp.ExecNode(node));
      stopwatch.Stop();
      Console.WriteLine("bhl fib ticks: {0}", stopwatch.ElapsedTicks);
    }

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      node.SetArgs(DynVal.NewNum(x));
      ExtractNum(intp.ExecNode(node));
      stopwatch.Stop();
      Console.WriteLine("bhl fib ticks2: {0}", stopwatch.ElapsedTicks);
    }
    CommonChecks(intp);

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      Fib(x);
      stopwatch.Stop();
      Console.WriteLine("C# fib ticks: {0}", stopwatch.ElapsedTicks);
    }
  }

  ////////////////////////////////////////////////

  static void Assert(bool condition, string msg = null)
  {
    if(!condition)
      throw new Exception("Assertion failed " + (msg != null ? msg : ""));
  }
  
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
    AssertTrue(idx != -1, "Error message is: " + err.Message);
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
    //creating local copy before interpreting anything
    var local_globs = new GlobalScope();
    var ms = globs.GetMembers();
    for(int i=0;i<ms.Count;++i)
      local_globs.define(ms[i]);

    var intp = Interpreter.instance;
    intp.Init(local_globs, mloader);

    mreg = mreg == null ? new ModuleRegistry() : mreg;

    foreach(var item in fpath2src)
    {
      var bin = new MemoryStream();
      var mod = new bhl.Module(item.Key, item.Key);
      Frontend.Source2Bin(mod, item.Value.ToStream(), bin, globs, mreg);
      bin.Position = 0;

      var ast = Util.Bin2Meta<AST_Module>(bin);
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

  public static void NodeDump(BehaviorTreeNode node, bool only_running = false, int level = 0, bool is_term = true)
  {
    var fnode = node as FuncNodeAST;
    if(fnode != null)
      fnode.Inflate();

    var name = node.GetType().Name;
    var status = node.getStatus();

    var spaces = new String('_', level);
    var indent_str = spaces + (is_term ? "`" : "|");
    Console.WriteLine(indent_str + "-" + name + " (" + status + ") " + node.inspect());

    if(!only_running || (only_running && status == BHS.RUNNING))
    {
      var inode = node as BehaviorTreeInternalNode;
      if(inode != null)
      {
        for(int i=0;i<inode.children.Count;++i)
        {
          var child = inode.children[i]; 
          NodeDump(child, only_running, level + 1, i == inode.children.Count-1);
        }
      }
    }
  }

  void CommonChecks(Interpreter intp)
  {
    intp.glob_mem.Clear();

    //Console.WriteLine(DynVal.PoolDump());
    AssertEqual(intp.stack.Count, 0);
    AssertEqual(DynVal.PoolCount, DynVal.PoolCountFree);
    AssertEqual(DynValList.PoolCount, DynValList.PoolCountFree);
    AssertEqual(DynValDict.PoolCount, DynValDict.PoolCountFree);
    AssertEqual(FuncCtx.PoolCount, FuncCtx.PoolCountFree);
  }

  static double ExtractNum(Interpreter.Result res)
  {
    return res.val.num;
  }

  static bool ExtractBool(Interpreter.Result res)
  {
    return res.val.bval;
  }

  static string ExtractStr(Interpreter.Result res)
  {
    return res.val.str;
  }
}

public static class BHL_TestExt 
{
  public static void Decode(this DynVal dv, ref List<string> dst)
  {
    dst.Clear();
    var src = (DynValList)dv.obj;
    for(int i=0;i<src.Count;++i)
    {
      var tmp = src[i];
      dst.Add(tmp.str);
    }
  }

  public static void Encode(this DynVal dv, List<string> dst)
  {
    var lst = DynValList.New();
    for(int i=0;i<dst.Count;++i)
      lst.Add(DynVal.NewStr(dst[i]));
    dv.SetObj(lst);
  }

  public static void Decode(this DynVal dv, ref List<uint> dst)
  {
    dst.Clear();
    var src = (DynValList)dv.obj;
    for(int i=0;i<src.Count;++i)
    {
      var tmp = src[i];
      dst.Add((uint)tmp.num);
    }
  }

  public static void Encode(this DynVal dv, List<uint> dst)
  {
    var lst = DynValList.New();
    for(int i=0;i<dst.Count;++i)
      lst.Add(DynVal.NewNum(dst[i]));
    dv.SetObj(lst);
  }

  public static void Decode(this DynVal dv, ref List<int> dst)
  {
    dst.Clear();
    var src = (DynValList)dv.obj;
    for(int i=0;i<src.Count;++i)
    {
      var tmp = src[i];
      dst.Add((int)tmp.num);
    }
  }

  public static void Encode(this DynVal dv, List<int> dst)
  {
    var lst = DynValList.New();
    for(int i=0;i<dst.Count;++i)
      lst.Add(DynVal.NewNum(dst[i]));
    dv.SetObj(lst);
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

