using System;
using System.IO;
using System.Collections.Generic;
using bhl;

public class BHL_TestInterpreter : BHL_TestBase
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var n = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var str = ExtractStr(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 0);
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var str = ExtractStr(ExecNode(node));

    AssertEqual(str, "bar\n");
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var str = ExtractStr(ExecNode(node));

    AssertEqual(str, "bar\n\n");
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var str = ExtractStr(ExecNode(node));

    AssertEqual(str, "bar\\n");
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var str = ExtractStr(ExecNode(node));

    AssertEqual(str, "bar\\n\n");
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var str = ExtractStr(ExecNode(node));

    AssertEqual(str, "bar\\n\\n");
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var str = ExtractStr(ExecNode(node));

    AssertEqual(str, "bar\t");
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var str = ExtractStr(ExecNode(node));

    AssertEqual(str, "bar\t\t");
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var str = ExtractStr(ExecNode(node));

    AssertEqual(str, "bar\\t");
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var str = ExtractStr(ExecNode(node));

    AssertEqual(str, "bar\\t\t");
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var str = ExtractStr(ExecNode(node));

    AssertEqual(str, "bar\\t\\t");
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

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
        Interpret(bhl);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");

    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(ExecNode(node));
    AssertEqual(num, 3);
    CommonChecks(intp);

    node.SetArgs(DynVal.NewNum(42));
    num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 4);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPostOpAddAssign()
  {
    string bhl = @"
      
    func int test() 
    {
      int k
      k += 10
      return k
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPostOpSubAssign()
  {
    string bhl = @"
      
    func int test() 
    {
      int k
      k -= 10
      return k
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, -10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPostOpMulAssign()
  {
    string bhl = @"
      
    func int test() 
    {
      int k = 1
      k *= 10
      return k
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPostOpDivAssign()
  {
    string bhl = @"
      
    func int test() 
    {
      int k = 10
      k /= 10
      return k
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 1);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPostOpAddAssignStringNotAllowed()
  {
    string bhl = @"
      
    func void test() 
    {
      string k
      k += ""foo""
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      " : incompatible variable type"
    );
  }

  [IsTested()]
  public void TestPostOpSubAssignStringNotAllowed()
  {
    string bhl = @"
      
    func void test() 
    {
      string k
      k -= ""foo""
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      " : incompatible variable type"
    );
  }

  [IsTested()]
  public void TestPostOpMulAssignStringNotAllowed()
  {
    string bhl = @"
      
    func void test() 
    {
      string k
      k *= ""foo""
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      " : incompatible variable type"
    );
  }

  [IsTested()]
  public void TestPostOpDivAssignStringNotAllowed()
  {
    string bhl = @"
      
    func void test() 
    {
      string k
      k /= ""foo""
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      " : incompatible variable type"
    );
  }

  [IsTested()]
  public void TestPostOpAssignExpIncompatibleTypesNotAllowed()
  {
    string bhl = @"
      
    func void test() 
    {
      int k
      float a
      k += a
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      " have incompatible types"
    );
  }

  [IsTested()]
  public void TestPostOpAssignExpCompatibleTypes()
  {
    string bhl = @"
      
    func float test() 
    {
      float k = 2.1
      int a = 1
      k += a
      return k
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 3.1);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(4));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(4));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(4));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(4));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(4));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(4));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(2));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(2));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewStr("b"));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(20));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewStr("b"));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewStr("b"));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(20));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(20));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3), DynVal.NewNum(7));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3), DynVal.NewNum(7));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 3);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNativeFuncBinding()
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);
    //NodeDump(node);

    var str = GetString(trace_stream);

    AssertEqual("HERE", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNativeFuncBindWithoutArgs()
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
    //NodeDump(node);

    AssertEqual(42, num);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNativeFuncBindConflict()
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
        Interpret(bhl, globs);
      },
      "already defined symbol 'trace'"
    );
  }

  [IsTested()]
  public void TestFuncAlreadyDeclaredArg()
  {
    string bhl = @"

    func void foo(int k, int k)
    {
    }
      
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      "@(3,29) k:<int>: already defined symbol 'k'"
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

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
        Interpret(bhl);
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
        Interpret(bhl);
      },
      "missing argument 'k'"
    );
  }

  [IsTested()]
  public void TestFuncPassingExtraNamedArgs()
  {
    string bhl = @"

    func int foo(int k)
    {
      return k
    }
      
    func int test() 
    {
      return foo(k: 1, k: 2)
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      "k: already passed before"
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
        Interpret(bhl);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 28);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncMissingReturnArgument()
  {
    string bhl = @"
    func int test() 
    {
      return
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      "return value is missing"
    );
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
        Interpret(bhl);
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
        Interpret(bhl);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var res = ExecNode(node).val;

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var res = ExtractStr(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    //NodeDump(node);

    var res = ExecNode(node).val;

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var res = ExecNode(node).val;

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    ExecNode(node, 0);

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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
        Interpret(bhl);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(ExecNode(node));
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
      var fn = new FuncSymbolSimpleNative("func_with_ref", globs.Type("void"), 
          delegate()
          {
            var interp = Interpreter.instance;
            var b = interp.PopRef();
            var a = interp.PopValue().num;

            b.num = a * 2;

            return BHS.SUCCESS;
          }
          );
      fn.Define(new FuncArgSymbol("a", globs.Type("float")));
      fn.Define(new FuncArgSymbol("b", globs.Type("float"), true/*is ref*/));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);

    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(ExecNode(node));
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
        Interpret(bhl);
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
        Interpret(bhl);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExecNode(node, 2); 
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
        Interpret(bhl);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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
        Interpret(bhl, globs);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);

    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);

    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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
        Interpret(bhl);
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
        Interpret(bhl);
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
        Interpret(bhl);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");

    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractStr(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");

    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractStr(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");

    node.SetArgs(DynVal.NewNum(3.9));
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractStr(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);

    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractStr(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var res = ExtractNum(ExecNode(node));

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
      var fn = new FuncSymbolNative("ret_val", globs.Type("float"),
          delegate() { return new RetValNode(); } );
      fn.Define(new FuncArgSymbol("k", globs.Type("float")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    node.SetArgs(DynVal.NewNum(42));
    var res = ExtractNum(ExecNode(node));

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
      var fn = new FuncSymbolSimpleNative("func_with_def", globs.Type("float"), 
          delegate()
          {
            var interp = Interpreter.instance;
            var b = interp.GetFuncArgsInfo().IsDefaultArgUsed(0) ? 2 : interp.PopValue().num;
            var a = interp.PopValue().num;

            interp.PushValue(DynVal.NewNum(a + b));

            return BHS.SUCCESS;
          },
          1);
      fn.Define(new FuncArgSymbol("a", globs.Type("float")));
      fn.Define(new FuncArgSymbol("b", globs.Type("float")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    node.SetArgs(DynVal.NewNum(42));
    var res = ExtractNum(ExecNode(node));

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
      var fn = new FuncSymbolSimpleNative("func_with_def", globs.Type("float"), 
          delegate()
          {
            var interp = Interpreter.instance;
            var b = interp.GetFuncArgsInfo().IsDefaultArgUsed(0) ? 2 : interp.PopValue().num;
            var a = interp.PopValue().num;

            interp.PushValue(DynVal.NewNum(a + b));

            return BHS.SUCCESS;
          },
          1);
      fn.Define(new FuncArgSymbol("a", globs.Type("float")));
      fn.Define(new FuncArgSymbol("b", globs.Type("float")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    node.SetArgs(DynVal.NewNum(42));
    var res = ExtractNum(ExecNode(node));

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
      var fn = new FuncSymbolSimpleNative("func_with_def", globs.Type("float"), 
          delegate()
          {
            var interp = Interpreter.instance;
            var a = interp.GetFuncArgsInfo().IsDefaultArgUsed(0) ? 14 : interp.PopValue().num;

            interp.PushValue(DynVal.NewNum(a));

            return BHS.SUCCESS;
          },
          1);
      fn.Define(new FuncArgSymbol("a", globs.Type("float")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var res = ExtractNum(ExecNode(node));

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
      var fn = new FuncSymbolSimpleNative("func_with_def", globs.Type("float"), 
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
      fn.Define(new FuncArgSymbol("a", globs.Type("int")));
      fn.Define(new FuncArgSymbol("b", globs.Type("int")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    node.SetArgs(DynVal.NewNum(42));
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExecNode(node, 0);

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
      var fn = new FuncSymbolSimpleNative("foo", globs.Type("float"),
          delegate() { return BHS.FAILURE; } );
      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExecNode(node, 0);

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

  public class CustomNull
  {
    public bool is_null;

    public static bool operator ==(CustomNull b1, CustomNull b2)
    {
      return b1.Equals(b2);
    }

    public static bool operator !=(CustomNull b1, CustomNull b2)
    {
      return !(b1 == b2);
    }

    public override bool Equals(System.Object obj)
    {
      if(obj == null && is_null)
        return true;
      return false;
    }

    public override int GetHashCode()
    {
      return 0;
    }
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

  ClassSymbolNative BindColor(GlobalScope globs)
  {
    var cl = new ClassSymbolNative("Color",
      delegate(ref DynVal v) 
      { 
        v.obj = new Color();
      }
    );

    globs.Define(cl);
    globs.Define(new ArrayTypeSymbolT<Color>(globs, new TypeRef(cl), delegate() { return new List<Color>(); } ));
    cl.Define(new FieldSymbol("r", globs.Type("float"),
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
    cl.Define(new FieldSymbol("g", globs.Type("float"),
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
      var m = new FuncSymbolSimpleNative("Add", globs.Type("Color"),
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
      m.Define(new FuncArgSymbol("k", globs.Type("float")));

      cl.Define(m);
    }

    {
      var m = new FuncSymbolSimpleNative("mult_summ", globs.Type("float"),
        delegate()
        {
          var interp = Interpreter.instance;

          var k = interp.PopValue().num;
          var c = (Color)interp.PopValue().obj;

          interp.PushValue(DynVal.NewNum((c.r * k) + (c.g * k)));

          return BHS.SUCCESS;
        }
      );
      m.Define(new FuncArgSymbol("k", globs.Type("float")));

      cl.Define(m);
    }

    {
      var fn = new FuncSymbolNative("mkcolor", globs.Type("Color"),
          delegate() { return new MkColorNode(); }
      );
      fn.Define(new FuncArgSymbol("r", globs.Type("float")));

      globs.Define(fn);
    }

    {
      var fn = new FuncSymbolSimpleNative("mkcolor_null", globs.Type("Color"),
          delegate() { 
            var interp = Interpreter.instance;
            var dv = DynVal.New();
            dv.obj = null;
            interp.PushValue(dv);
            return BHS.SUCCESS;
          }
      );

      globs.Define(fn);
    }

    return cl;
  }

  void BindStringClass(GlobalScope globs)
  {
    {
      var cl = new ClassSymbolNative("StringClass",
        delegate(ref DynVal v) 
        { 
          v.obj = new StringClass();
        }
      );

      globs.Define(cl);

      cl.Define(new FieldSymbol("str", globs.Type("string"),
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

  void BindCustomNull(GlobalScope globs)
  {
    {
      var cl = new ClassSymbolNative("CustomNull",
        delegate(ref DynVal v) 
        { 
          v.obj = new CustomNull();
        }
      );

      globs.Define(cl);
    }
  }

  void BindIntStruct(GlobalScope globs)
  {
    {
      var cl = new ClassSymbolNative("IntStruct",
        delegate(ref DynVal v) 
        { 
          var s = new IntStruct();
          IntStruct.Encode(v, s);
        }
      );

      globs.Define(cl);

      cl.Define(new FieldSymbol("n", globs.Type("int"),
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

      cl.Define(new FieldSymbol("n2", globs.Type("int"),
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
      var cl = new ClassSymbolNative("MasterStruct",
        delegate(ref DynVal v) 
        { 
          var o = new MasterStruct();
          o.child = new StringClass();
          o.child2 = new StringClass();
          v.obj = o;
        }
      );

      globs.Define(cl);

      cl.Define(new FieldSymbol("child", globs.Type("StringClass"),
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

      cl.Define(new FieldSymbol("child2", globs.Type("StringClass"),
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

      cl.Define(new FieldSymbol("child_struct", globs.Type("IntStruct"),
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

      cl.Define(new FieldSymbol("child_struct2", globs.Type("IntStruct"),
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
      var cl = new ClassSymbolNative("ColorAlpha", globs.Type("Color"),
        delegate(ref DynVal v) 
        { 
          v.obj = new ColorAlpha();
        }
      );

      globs.Define(cl);

      cl.Define(new FieldSymbol("a", globs.Type("float"),
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
        var m = new FuncSymbolSimpleNative("mult_summ_alpha", globs.Type("float"),
          delegate()
          {
            var interp = Interpreter.instance;

            var c = (ColorAlpha)interp.PopValue().obj;

            interp.PushValue(DynVal.NewNum((c.r * c.a) + (c.g * c.a)));

            return BHS.SUCCESS;
          }
        );

        cl.Define(m);
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
      var cl = new ClassSymbolNative("RefC",
        delegate(ref DynVal v) 
        { 
          v.obj = new RefC(mstream);
        }
      );
      {
        var vs = new bhl.FieldSymbol("refs", globs.Type("int"),
          delegate(bhl.DynVal ctx, ref bhl.DynVal v)
          {
            v.num = ((RefC)ctx.obj).refs;
          },
          //read only property
          null
        );
        cl.Define(vs);
      }
      globs.Define(cl);
      globs.Define(new GenericArrayTypeSymbol(globs, new TypeRef(cl)));
    }
  }

  void BindFoo(GlobalScope globs)
  {
    {
      var cl = new ClassSymbolNative("Foo",
        delegate(ref DynVal v) 
        { 
          v.obj = new Foo();
        }
      );
      globs.Define(cl);
      globs.Define(new ArrayTypeSymbolT<Foo>(globs, new TypeRef(cl), delegate() { return new List<Foo>(); } ));

      cl.Define(new FieldSymbol("hey", globs.Type("int"),
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
      cl.Define(new FieldSymbol("colors", globs.Type("Color[]"),
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
      cl.Define(new FieldSymbol("sub_color", globs.Type("Color"),
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
      var cl = new ClassSymbolNative("DynValContainer",
        delegate(ref DynVal v) 
        { 
          v.obj = new DynValContainer();
        }
      );
      globs.Define(cl);

      cl.Define(new FieldSymbol("dv", globs.Type("any"),
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
      var fn = new FuncSymbolSimpleNative("get_dv_container", globs.Type("DynValContainer"),
          delegate() { Interpreter.instance.PushValue(DynVal.NewObj(c)); return BHS.SUCCESS; } );

      globs.Define(fn);
    }
  }

  void BindFooLambda(GlobalScope globs)
  {
    {
      var cl = new ClassSymbolNative("FooLambda",
        delegate(ref DynVal v) 
        { 
          v.obj = new FooLambda();
        }
      );
      globs.Define(cl);
      globs.Define(new ArrayTypeSymbolT<Foo>(globs, new TypeRef(cl), delegate() { return new List<Foo>(); } ));

      cl.Define(new FieldSymbol("script", globs.Type("void^()"),
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
  public void TestNativeClass()
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(2));
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 202);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestPlusNotOverloadedForNativeClass()
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
        Interpret(bhl, globs);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestMinusNotOverloadedForNativeClass()
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
        Interpret(bhl, globs);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestMultNotOverloadedForNativeClass()
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
        Interpret(bhl, globs);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestDivNotOverloadedForNativeClass()
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
        Interpret(bhl, globs);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestGtNotOverloadedForNativeClass()
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
        Interpret(bhl, globs);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestGteNotOverloadedForNativeClass()
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
        Interpret(bhl, globs);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestLtNotOverloadedForNativeClass()
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
        Interpret(bhl, globs);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestLteNotOverloadedForNativeClass()
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
        Interpret(bhl, globs);
      },
      @"operator is not overloaded"
    );
  }

  [IsTested()]
  public void TestUnaryMinusNotOverloadedForNativeClass()
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
        Interpret(bhl, globs);
      },
      @"must be numeric type"
    );
  }

  [IsTested()]
  public void TestBitAndNotOverloadedForNativeClass()
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
        Interpret(bhl, globs);
      },
      @"must be int type"
    );
  }

  [IsTested()]
  public void TestBitOrNotOverloadedForNativeClass()
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
        Interpret(bhl, globs);
      },
      @"must be int type"
    );
  }

  [IsTested()]
  public void TestLogicalAndNotOverloadedForNativeClass()
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
        Interpret(bhl, globs);
      },
      @"must be bool type"
    );
  }

  [IsTested()]
  public void TestLogicalOrNotOverloadedForNativeClass()
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
        Interpret(bhl, globs);
      },
      @"must be bool type"
    );
  }

  [IsTested()]
  public void TestUnaryNotNotOverloadedForNativeClass()
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
        Interpret(bhl, globs);
      },
      @"must be bool type"
    );
  }

  [IsTested()]
  public void TestPlusOverloadedForNativeClass()
  {
    string bhl = @"
      
    func Color test() 
    {
      Color c1 = {r:1,g:2}
      Color c2 = {r:20,g:30}
      Color c3 = c1 + c2
      return c3
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    var cl = BindColor(globs);
    var op = new FuncSymbolSimpleNative("+", globs.Type("Color"),
      delegate()
      {
        var interp = Interpreter.instance;

        var r = (Color)interp.PopValue().obj;
        var c = (Color)interp.PopValue().obj;

        var newc = new Color();
        newc.r = c.r + r.r;
        newc.g = c.g + r.g;

        var dv = DynVal.NewObj(newc);
        interp.PushValue(dv);

        return BHS.SUCCESS;
      }
    );
    op.Define(new FuncArgSymbol("r", globs.Type("Color")));
    cl.OverloadBinaryOperator(op);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = (Color)ExtractObj(ExecNode(node));

    AssertEqual(21, res.r);
    AssertEqual(32, res.g);
  }

  [IsTested()]
  public void TestMultOverloadedForNativeClass()
  {
    string bhl = @"
      
    func Color test() 
    {
      Color c1 = {r:1,g:2}
      Color c2 = c1 * 2
      return c2
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    var cl = BindColor(globs);
    var op = new FuncSymbolSimpleNative("*", globs.Type("Color"),
      delegate()
      {
        var interp = Interpreter.instance;

        var k = (float)interp.PopValue().num;
        var c = (Color)interp.PopValue().obj;

        var newc = new Color();
        newc.r = c.r * k;
        newc.g = c.g * k;

        var dv = DynVal.NewObj(newc);
        interp.PushValue(dv);

        return BHS.SUCCESS;
      }
    );
    op.Define(new FuncArgSymbol("k", globs.Type("float")));
    cl.OverloadBinaryOperator(op);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = (Color)ExtractObj(ExecNode(node));

    AssertEqual(2, res.r);
    AssertEqual(4, res.g);
  }

  [IsTested()]
  public void TestOverloadedBinOpsPriorityForNativeClass()
  {
    string bhl = @"
      
    func Color test() 
    {
      Color c1 = {r:1,g:2}
      Color c2 = {r:10,g:20}
      Color c3 = c1 + c2 * 2
      return c3
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    var cl = BindColor(globs);
    {
      var op = new FuncSymbolSimpleNative("*", globs.Type("Color"),
        delegate()
        {
          var interp = Interpreter.instance;

          var k = (float)interp.PopValue().num;
          var c = (Color)interp.PopValue().obj;

          var newc = new Color();
          newc.r = c.r * k;
          newc.g = c.g * k;

          var dv = DynVal.NewObj(newc);
          interp.PushValue(dv);

          return BHS.SUCCESS;
        }
      );
      op.Define(new FuncArgSymbol("k", globs.Type("float")));
      cl.OverloadBinaryOperator(op);
    }
    
    {
      var op = new FuncSymbolSimpleNative("+", globs.Type("Color"),
        delegate()
        {
          var interp = Interpreter.instance;

          var r = (Color)interp.PopValue().obj;
          var c = (Color)interp.PopValue().obj;

          var newc = new Color();
          newc.r = c.r + r.r;
          newc.g = c.g + r.g;

          var dv = DynVal.NewObj(newc);
          interp.PushValue(dv);

          return BHS.SUCCESS;
        }
      );
      op.Define(new FuncArgSymbol("r", globs.Type("Color")));
      cl.OverloadBinaryOperator(op);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = (Color)ExtractObj(ExecNode(node));

    AssertEqual(21, res.r);
    AssertEqual(42, res.g);
  }

  [IsTested()]
  public void TestEqualityOverloadedForNativeClass()
  {
    string bhl = @"
      
    func test() 
    {
      Color c1 = {r:1,g:2}
      Color c2 = {r:1,g:2}
      Color c3 = {r:10,g:20}
      if(c1 == c2) {
        trace(""YES"")
      }
      if(c1 == c3) {
        trace(""NO"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    
    var cl = BindColor(globs);
    var op = new FuncSymbolSimpleNative("==", globs.Type("bool"),
      delegate()
      {
        var interp = Interpreter.instance;

        var arg = (Color)interp.PopValue().obj;
        var c = (Color)interp.PopValue().obj;

        var dv = DynVal.NewBool(c.r == arg.r && c.g == arg.g);
        interp.PushValue(dv);

        return BHS.SUCCESS;
      }
    );
    op.Define(new FuncArgSymbol("arg", globs.Type("Color")));
    cl.OverloadBinaryOperator(op);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    AssertEqual(str, "YES");
  }

  [IsTested()]
  public void TestCustomOperatorOverloadTypeMismatchForNativeClass()
  {
    string bhl = @"
      
    func Color test() 
    {
      Color c1 = {r:1,g:2}
      return c1 * ""hey""
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    var cl = BindColor(globs);
    var op = new FuncSymbolSimpleNative("*", globs.Type("Color"), null);
    op.Define(new FuncArgSymbol("k", globs.Type("float")));
    cl.OverloadBinaryOperator(op);

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      @"<string> have incompatible types"
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractBool(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractBool(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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
         Interpret(bhl, globs);
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
         Interpret(bhl, globs);
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
      var cl = new ClassSymbolNative( "ColorNested",
        delegate(ref DynVal v) 
        { 
          v.obj = new ColorNested();
        }
      );
      globs.Define(cl);

      cl.Define(new FieldSymbol("c", globs.Type("Color"),
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 202);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestReturnNativeInstance()
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 1);
    CommonChecks(intp);
  }

  void BindEnum(GlobalScope globs)
  {
    var en = new EnumSymbol(null, "EnumState", null);
    globs.Define(en);
    globs.Define(new GenericArrayTypeSymbol(globs, new TypeRef(en)));

    en.Define(new EnumItemSymbol(null, en, "SPAWNED",  10));
    en.Define(new EnumItemSymbol(null, en, "SPAWNED2", 20));
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var res = ExecNode(node).val;

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
        Interpret(bhl, globs);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(20));
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(20));
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractBool(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var res = ExecNode(node).val;

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var res = ExecNode(node).val;

    var lst = res.obj as DynValList;
    AssertEqual(lst.Count, 2);
    AssertEqual(lst[0].num, 20);
    AssertEqual(lst[1].num, 10);
    lst.TryDel();
    AssertEqual(DynValList.PoolCount, DynValList.PoolCountFree);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNativeClassArray()
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = ExecNode(node).val;

    AssertEqual(res.str, "3102030");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNativeClassTmpArray()
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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
  public void TestNativeSubClassArray()
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = ExecNode(node).val;

    AssertEqual(res.str, "2102030");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNativeAttributeIsNotAFunction()
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
        Interpret(bhl, globs);
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
  public void TestPassEnumToNativeNode()
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
      var fn = new FuncSymbolNative("StateIs", globs.Type("void"),
          delegate() { return new StateIsNode(); });
      fn.Define(new FuncArgSymbol("state", globs.Type("EnumState")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExecNode(node, 0);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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
        Interpret(bhl);
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
        Interpret(bhl);
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
        Interpret(bhl);
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
        Interpret(bhl);
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
        Interpret(bhl);
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

      var func_node = fct.GetCallNode();

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

      //Console.WriteLine("==============\n" + s.str + "\n" + Environment.StackTrace);

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
      var fn = new FuncSymbolNative("trace", globs.Type("void"),
          delegate() { return new TraceNode(trace_stream); } );
      fn.Define(new FuncArgSymbol("str", globs.Type("string")));

      globs.Define(fn);
    }
  }

  void BindAnswer42(GlobalScope globs)
  {
    {
      var fn = new FuncSymbolSimpleNative("answer42", globs.Type("int"),
          delegate() { Interpreter.instance.PushValue(DynVal.NewNum(42)); return BHS.SUCCESS; } );
      globs.Define(fn);
    }
  }

  //simple console outputting version
  void BindLog(GlobalScope globs)
  {
    {
      var fn = new FuncSymbolSimpleNative("log", globs.Type("void"),
          delegate() { Console.WriteLine(Interpreter.instance.PopValue().str); return BHS.SUCCESS; } );
      fn.Define(new FuncArgSymbol("str", globs.Type("string")));

      globs.Define(fn);
    }
  }

  void BindMin(GlobalScope globs)
  {
    {
      var fn = new FuncSymbolSimpleNative("min", globs.Type("float"),
        delegate()
        {
          var interp = Interpreter.instance;
          var b = (float)interp.PopValue().num;
          var a = (float)interp.PopValue().num;
          interp.PushValue(DynVal.NewNum(a > b ? b : a)); 
          return BHS.SUCCESS;
        }
      );
      fn.Define(new FuncArgSymbol("a", globs.Type("float")));
      fn.Define(new FuncArgSymbol("b", globs.Type("float")));

      globs.Define(fn);
    }
  }

  public class NodeWithLog : BehaviorTreeTerminalNode
  {
    Dictionary<int, BHS> ctl;
    Stream sm;
    int id;

    public NodeWithLog(Stream sm, Dictionary<int, BHS> ctl)
    {
      this.ctl = ctl;
      this.sm = sm;
    }

    public override void init()
    {
      var interp = Interpreter.instance;
      id = (int)interp.PopValue().num;

      var sw = new StreamWriter(sm);
      sw.Write("INIT(" + id + ");");
      sw.Flush();
    }

    public override void deinit()
    {
      var sw = new StreamWriter(sm);
      sw.Write("DEINIT(" + id + ");");
      sw.Flush();
    }

    public override BHS execute()
    {
      var sw = new StreamWriter(sm);
      sw.Write("EXEC(" + id + ");");
      sw.Flush();
      return ctl[id];
    }

    public override void defer()
    {
      var sw = new StreamWriter(sm);
      sw.Write("DEFER(" + id + ");");
      sw.Flush();
    }
  }

  void BindNodeWithLog(GlobalScope globs, MemoryStream s, Dictionary<int, BHS> ctl)
  {
    {
      var fn = new FuncSymbolNative("NodeWithLog", globs.Type("void"),
          delegate() { return new NodeWithLog(s, ctl); } );

      fn.Define(new FuncArgSymbol("id", globs.Type("int")));
      globs.Define(fn);
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

  public class NodeWithDeferRetInt : BehaviorTreeTerminalNode
  {
    Stream sm;
    int n;

    public NodeWithDeferRetInt(Stream sm)
    {
      this.sm = sm;
    }

    public override void init()
    {
      n = (int)Interpreter.instance.PopValue().num;
    }

    public override BHS execute()
    {
      Interpreter.instance.PushValue(DynVal.NewNum(n));
      return BHS.SUCCESS;
    }

    public override void defer()
    {
      var sw = new StreamWriter(sm);
      sw.Write("DEFER " + n + "!!!");
      sw.Flush();
    }
  }

  void BindNodeWithDefer(GlobalScope globs, MemoryStream s)
  {
    {
      var fn = new FuncSymbolNative("NodeWithDefer", globs.Type("void"),
          delegate() { return new NodeWithDefer(s); } );

      globs.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("NodeWithDeferRetInt", globs.Type("int"),
          delegate() { return new NodeWithDeferRetInt(s); } );

      fn.Define(new FuncArgSymbol("n", globs.Type("int")));
      globs.Define(fn);
    }
  }

  void BindWaitTicks(GlobalScope globs)
  {
    {
      var fn = new FuncSymbolNative("WaitTicks", globs.Type("void"),
          delegate() { return new WaitTicksNode(); } );
      fn.Define(new FuncArgSymbol("ticks", globs.Type("int")));
      fn.Define(new FuncArgSymbol("is_success", globs.Type("bool")));

      globs.Define(fn);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);
    //NodeDump(node);

    var str = GetString(trace_stream);

    AssertEqual("HERE", str);
    CommonChecks(intp);
  }

  void BindStartScript(GlobalScope globs)
  {
    {
      var fn = new FuncSymbolNative("StartScript", globs.Type("void"),
          delegate() { return new StartScriptNode(); } );
      fn.Define(new FuncArgSymbol("script", globs.Type("void^()")));

      globs.Define(fn);
    }
  }

  void BindStartScriptInMgr(GlobalScope globs)
  {
    {
      var fn = new FuncSymbolSimpleNative("StartScriptInMgr", globs.Type("void"),
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
              var node = fct.GetNode();
              //Console.WriteLine("FREFS START: " + fct.GetHashCode() + " " + fct.refs);
              ScriptMgr.instance.add(node, now);
            }

            return BHS.SUCCESS;
          }
      );

      fn.Define(new FuncArgSymbol("script", globs.Type("void^()")));
      fn.Define(new FuncArgSymbol("num", globs.Type("int")));
      fn.Define(new FuncArgSymbol("now", globs.Type("bool")));

      globs.Define(fn);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractBool(ExecNode(node));
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractBool(ExecNode(node));
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

    var intp = Interpret(bhl, globs);
    {
      var node = intp.GetFuncCallNode("test");
      node.SetArgs(DynVal.NewNum(3));
      var res = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl, globs);
    {
      var node = intp.GetFuncCallNode("test");
      node.SetArgs(DynVal.NewNum(3));
      var res = ExtractNum(ExecNode(node));
      //NodeDump(node);
      AssertEqual(res, 6);
    }
    {
      var node = intp.GetFuncCallNode("test");
      node.SetArgs(DynVal.NewNum(4));
      var res = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl, globs);
    {
      var node = intp.GetFuncCallNode("test");
      node.SetArgs(DynVal.NewNum(3));
      var res = ExtractNum(ExecNode(node));
      //NodeDump(node);
      AssertEqual(res, 9 + 12);
    }
    {
      var node = intp.GetFuncCallNode("test");
      node.SetArgs(DynVal.NewNum(4));
      var res = ExtractNum(ExecNode(node));
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
             foo(func int(int a) { return a * 2 }, a + 1)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    {
      var node = intp.GetFuncCallNode("test");
      node.SetArgs(DynVal.NewNum(3));
      var res = ExtractNum(ExecNode(node));
      //NodeDump(node);
      AssertEqual(res, 3 + 8);
    }
    {
      var node = intp.GetFuncCallNode("test");
      node.SetArgs(DynVal.NewNum(4));
      var res = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractBool(ExecNode(node));
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractBool(ExecNode(node));
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractBool(ExecNode(node));
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));
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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractBool(ExecNode(node));
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractBool(ExecNode(node));
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);
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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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
      var cl = new ClassSymbolNative("refbool",
        delegate(ref DynVal v) 
        {}
      );
      globs.Define(cl);
    }

    BindTrace(globs, trace_stream);

    AssertError<UserError>(
      delegate() {
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    public override void deinit()
    {}

    public override void defer()
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

    new public void stop()
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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
      var fn = new FuncSymbolSimpleNative("say_here", globs.Type("void"), 
          delegate()
          {
            AddString(trace_stream, "HERE;");
            return BHS.RUNNING;
          }
          );
      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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
      var fn = new FuncSymbolSimpleNative("foo", globs.Type("void"), 
          delegate()
          {
            AddString(trace_stream, "FOO");
            return BHS.SUCCESS;
          }
          );
      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node1 = intp.GetFuncCallNode("test");
    var node2 = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node1, 0);
    ExecNode(node2, 0);

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

    var intp = Interpret(bhl, globs);
    ExecNode(intp.GetFuncCallNode("test"), 0);
    ExecNode(intp.GetFuncCallNode("test2"), 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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
        Interpret(bhl, globs);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);

    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));
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
   
    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
   
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
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
  public void TestParalReturn()
  {
    string bhl = @"

    func int test() 
    {
      paral {
        seq {
          return 1
        }
        suspend()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
    AssertEqual(num, 1);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestParalAllReturn()
  {
    string bhl = @"

    func int test() 
    {
      paral_all {
        seq {
          return 1
        }
        suspend()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
    AssertEqual(num, 1);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestParalBreak()
  {
    string bhl = @"

    func int test() 
    {
      int n = 0
      while(true) {
        paral {
          seq {
            n = 1
            break
            suspend()
          }
          {
            n = 2
            suspend()
          }
        }
      }
      return n
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
    AssertEqual(num, 1);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestParalAllBreak()
  {
    string bhl = @"

    func int test() 
    {
      int n = 0
      while(true) {
        paral_all {
          seq {
            n = 1
            break
            suspend()
          }
          {
            n = 2
            suspend()
          }
        }
      }
      return n
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
    AssertEqual(num, 1);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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
      WaitTicks(2, is_success: false)
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    {
      var status = node.run();
      AssertEqual(BHS.SUCCESS, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("BARHEYFOO", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRunningInDeferIsException()
  {
    string bhl = @"

    func test() 
    {
      defer {
        suspend()
      }
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");

    AssertError<Exception>(
      delegate() { 
        node.run();
      },
      "Node is RUNNING during defer"
    );
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    {
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferInFuncCall()
  {
    string bhl = @"

    func int hey()
    {
      defer {
        trace(""hey"")
      }
      return 1
    }

    func foo(int h, int w)
    {
      defer {
        trace(""foo "" + (string)w)
      }
      NodeWithDefer()
    }

    func test() 
    {
      foo(hey(), NodeWithDeferRetInt(42))
      suspend()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindNodeWithDefer(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.RUNNING, node.run());

    var str = GetString(trace_stream);
    AssertEqual("heyDEFER!!!foo 42", str);
    node.stop();
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferInBindCall()
  {
    string bhl = @"

    func int hey()
    {
      defer {
        trace(""hey"")
      }
      return 1
    }

    func test() 
    {
      NodeWithDeferRetInt(NodeWithDeferRetInt(hey()))
      suspend()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindNodeWithDefer(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.RUNNING, node.run());

    var str = GetString(trace_stream);
    AssertEqual("hey", str);
    node.stop();
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferInFuncPtrCall()
  {
    string bhl = @"

    func int hey()
    {
      defer {
        trace(""hey"")
      }
      return 1
    }

    func foo(int h, int w)
    {
      defer {
        trace(""foo "" + (string)w)
      }
      NodeWithDefer()
    }

    func test() 
    {
      void^(int,int) p = foo
      p(hey(), NodeWithDeferRetInt(42))
      suspend()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindNodeWithDefer(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.RUNNING, node.run());

    var str = GetString(trace_stream);
    AssertEqual("heyDEFER!!!foo 42", str);
    node.stop();
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    AssertEqual(BHS.FAILURE, node.run());
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    AssertEqual(BHS.SUCCESS, node.run());
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    AssertEqual(BHS.RUNNING, node.run());
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInvertStopRunningNode()
  {
    string bhl = @"

    func test() 
    {
      paral_all {
        not {
          NodeWithLog(1)
        }
        NodeWithLog(2)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    var ctl = new Dictionary<int, BHS>();
    ctl[1] = BHS.RUNNING;
    ctl[2] = BHS.RUNNING;

    BindNodeWithLog(globs, trace_stream, ctl);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.RUNNING, node.run());
    node.stop();

    var str = GetString(trace_stream);
    AssertEqual("INIT(1);EXEC(1);INIT(2);EXEC(2);EXEC(1);EXEC(2);DEINIT(1);DEINIT(2);DEFER(2);DEFER(1);", str);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInvertInParal()
  {
    string bhl = @"

    func test() 
    {
      paral_all {
        not {
          NodeWithLog(1)
        }
        NodeWithLog(2)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    var ctl = new Dictionary<int, BHS>();
    ctl[1] = BHS.FAILURE;
    ctl[2] = BHS.SUCCESS;

    BindNodeWithLog(globs, trace_stream, ctl);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    AssertEqual(BHS.SUCCESS, node.run());

    var str = GetString(trace_stream);
    AssertEqual("INIT(1);EXEC(1);DEINIT(1);INIT(2);EXEC(2);DEINIT(2);DEFER(2);DEFER(1);", str);

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    AssertEqual(BHS.FAILURE, node.run());
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    AssertEqual(BHS.SUCCESS, node.run());
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDeferMethodIsNotTriggeredTooEarlyInDecorator()
  {
    string bhl = @"

    func test() 
    {
      not { NodeWithLog(1) }
      NodeWithLog(2)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    var ctl = new Dictionary<int, BHS>();
    ctl[1] = BHS.FAILURE;
    ctl[2] = BHS.RUNNING;

    BindNodeWithLog(globs, trace_stream, ctl);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    AssertEqual(BHS.RUNNING, node.run());

    var str = GetString(trace_stream);
    AssertEqual("INIT(1);EXEC(1);DEINIT(1);INIT(2);EXEC(2);", str);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
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
        Interpret(bhl);
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
        Interpret(bhl);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(3));
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 8);
    CommonChecks(intp);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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


    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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
   
    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");

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

    var intp = Interpret(bhl);

    AssertEqual(FuncCallNode.PoolCount, 0);

    {
      var node = intp.GetFuncCallNode("test");
      node.SetArgs(DynVal.NewNum(3));
      var res = ExtractNum(ExecNode(node));

      AssertEqual(res, 3);
      CommonChecks(intp);
    }

    AssertEqual(FuncCallNode.PoolCount, 1);

    {
      var node = intp.GetFuncCallNode("test");
      node.SetArgs(DynVal.NewNum(30));
      var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);

    AssertEqual(FuncCallNode.PoolCount, 0);

    {
      var node = intp.GetFuncCallNode("test");
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

    var intp = Interpret(bhl);

    AssertEqual(FuncCallNode.PoolCount, 0);

    var node1 = intp.GetFuncCallNode("test1");
    var status = node1.run();
    AssertEqual(BHS.RUNNING, status);

    FuncCallNode.PoolClear();

    var node2 = intp.GetFuncCallNode("test2");
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
    { }

    func void test(float k) 
    {
      forever
      {
        foo(k)
        suspend()
      }
    }
    ";

    var intp = Interpret(bhl);

    AssertEqual(FuncCallNode.PoolCount, 0);
    AssertEqual(FuncCallNode.PoolCountFree, 0);

    var node1 = intp.GetFuncCallNode("test");
    node1.SetArgs(DynVal.NewNum(3));
    var status = node1.run();
    AssertEqual(BHS.RUNNING, status);
    
    AssertEqual(FuncCallNode.PoolCount, 1);
    AssertEqual(FuncCallNode.PoolCountFree, 1);

    var node2 = intp.GetFuncCallNode("test");
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
  public void TestDetachedUserlandFuncsAreNotDoubleDeferred()
  {
    string bhl = @"
      
    func void foo(int n, bool keep_running)
    { 
      if(keep_running) {
        suspend()
      }
      NodeWithDeferRetInt(n)
    }

    func void test() 
    {
      paral_all {
        seq {
          foo(10, false)  //1st time, cached
          yield()
          yield()         
          //defer is triggered for parent seq, but MUST not be triggered for cached foo(),
          //at this moment cached foo() is already running in the block below, if we call
          //defer on it, it will invalidate its state and on the next run there will be
          //exception (it will try to pop the passed arguments again)
        }
        seq {
          yield()         //wait one tick
          paral {
            foo(20, true) //userland func is taken from cache, keeps ticking
            seq {         //let's interrupt paral after two ticks
              yield()
              yield()
            }
          }
        }
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);
    BindNodeWithDefer(globs, trace_stream);

    var intp = Interpret(bhl, globs);

    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("DEFER 10!!!", str);

    CommonChecks(intp);
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
    
    BindColor(globs);

    var intp = Interpret(bhl, globs);

    var foo = intp.GetFuncCallNode("foo");
    foo.SetArgs(DynVal.NewNum(10));
    var res = ExtractNum(ExecNode(foo));
    AssertEqual(res, 10);
    CommonChecks(intp);

    foo.SetArgs(DynVal.NewNum(20));
    res = ExtractNum(ExecNode(foo));
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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
        Interpret(bhl);
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
        Interpret(bhl);
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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
      },
      "matching 'return' statement not found"
    );
  }

  [IsTested()]
  public void TestNonMatchingReturnAfterElseIf2()
  {
    string bhl = @"

    func int test() 
    {
      if(false) {
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
        Interpret(bhl, globs);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(1));
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 2);
    CommonChecks(intp);

    node.SetArgs(DynVal.NewNum(10));
    res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExecNode(node).val;

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExecNode(node).val;

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
      if(c != null) {
        trace(""NEVER;"")
      }
      if(c2 == null) {
        trace(""NEVER;"")
      }
      if(c2 != null) {
        trace(""NOTNULL;"")
      }
      c = c2
      if(c2 == c) {
        trace(""EQ;"")
      }
      if(c2 == null) {
        trace(""NEVER;"")
      }
      if(c != null) {
        trace(""NOTNULL;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("NULL;NOTNULL;EQ;NOTNULL;", str);
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
        trace(""NEVER;"")
      }
      if(c2 != null) {
        trace(""NOTNULL;"")
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("NULL;NOTNULL;EQ;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNullPassedAsNullObj()
  {
    string bhl = @"
      
    func void test(StringClass c) 
    {
      if(c != null) {
        trace(""NEVER;"")
      }
      if(c == null) {
        trace(""NULL;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStringClass(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewObj(null));
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("NULL;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNullPassedAsNewNil()
  {
    string bhl = @"
      
    func void test(StringClass c) 
    {
      if(c != null) {
        trace(""NEVER;"")
      }
      if(c == null) {
        trace(""NULL;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStringClass(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNil());
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("NULL;", str);
    CommonChecks(intp);
  }

  //TODO: do we really need this behavior supported?
  //[IsTested()]
  public void TestNullPassedAsCustomNull()
  {
    string bhl = @"
      
    func void test(CustomNull c) 
    {
      if(c != null) {
        trace(""NOTNULL;"")
      }
      if(c == null) {
        trace(""NULL;"")
      }

      yield()

      if(c != null) {
        trace(""NOTNULL2;"")
      }
      if(c == null) {
        trace(""NULL2;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindCustomNull(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var cn = new CustomNull();
    node.SetArgs(DynVal.NewObj(cn));

    cn.is_null = false;
    node.run();

    cn.is_null = true;
    node.run();

    var str = GetString(trace_stream);
    AssertEqual("NOTNULL;NULL2;", str);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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
        Interpret(bhl);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.SUCCESS, node.run());

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
        Interpret(bhl, globs);
      },
      // "mismatched input ';' expecting '='"
      "no viable alternative at input 'i ;"
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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
        Interpret(bhl, globs);
      },
      // "mismatched input ')' expecting '='"
      "no viable alternative at input 'i)'"
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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.SUCCESS, node.run());

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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
        Interpret(bhl, globs);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractStr(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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
        Interpret(bhl, globs);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

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
        Interpret(bhl, globs);
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
        Interpret(bhl, globs);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonArrEmptyDefaultArg()
  {
    string bhl = @"
    func int foo(Color[] c = [])
    {
      return c.Count
    }

    func int test()
    {
      return foo()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 0);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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
        Interpret(bhl, globs);
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
      var fn = new FuncSymbolNative("MakeFoo", globs.Type("Foo"),
          delegate() { return new MakeFooNode(); } );
      fn.Define(new FuncArgSymbol("conf", globs.Type("Foo")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(42));
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("14232342", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInterleaveValuesStackInParal()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    func int ret_int(int val, int ticks)
    {
      while(ticks > 0)
      {
        yield()
        ticks = ticks - 1
      }
      return val
    }

    func void test() 
    {
      paral {
        seq {
          foo(1, ret_int(val: 2, ticks: 1))
          suspend()
        }
        foo(10, ret_int(val: 20, ticks: 2))
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("1 2;10 20;", str);

    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInterleaveValuesStackInParalWithPtrCall()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    func int ret_int(int val, int ticks)
    {
      while(ticks > 0)
      {
        yield()
        ticks = ticks - 1
      }
      return val
    }

    func void test() 
    {
      int^(int,int) p = ret_int
      paral {
        seq {
          foo(1, p(2, 1))
          suspend()
        }
        foo(10, p(20, 2))
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("1 2;10 20;", str);

    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInterleaveValuesStackInParalWithMemberPtrCall()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    class Bar { 
      int^(int,int) ptr
    }

    func int ret_int(int val, int ticks)
    {
      while(ticks > 0)
      {
        yield()
        ticks = ticks - 1
      }
      return val
    }

    func void test() 
    {
      Bar b = {}
      b.ptr = ret_int
      paral {
        seq {
          foo(1, b.ptr(2, 1))
          suspend()
        }
        foo(10, b.ptr(20, 2))
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("1 2;10 20;", str);

    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInterleaveValuesStackInParalWithLambdaCall()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    func void test() 
    {
      paral {
        seq {
          foo(1, 
              func int (int val, int ticks) {
                while(ticks > 0) {
                  yield()
                  ticks = ticks - 1
                }
                return val
              }(2, 1))
          suspend()
        }
        foo(10, 
            func int (int val, int ticks) {
              while(ticks > 0) {
                yield()
                ticks = ticks - 1
              }
              return val
            }(20, 2))
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("1 2;10 20;", str);

    //NodeDump(node);
    CommonChecks(intp);
  }

  public class Foo_ret_int  : BehaviorTreeTerminalNode
  {
    int ticks;
    int ret;

    public override void init()
    {
      var interp = Interpreter.instance;
      ticks = (int)interp.PopValue().num;
      ret = (int)interp.PopValue().num;
      interp.PopValue();
    }

    public override BHS execute()
    {
      if(ticks-- > 0)
        return BHS.RUNNING;
      Interpreter.instance.PushValue(DynVal.NewNum(ret));
      return BHS.SUCCESS;
    }
  }

  [IsTested()]
  public void TestInterleaveValuesStackInParalWithMethods()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    func void test() 
    {
      paral {
        seq {
          foo(1, (new Foo).self().ret_int(val: 2, ticks: 1))
          suspend()
        }
        foo(10, (new Foo).self().ret_int(val: 20, ticks: 2))
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    {
      var cl = new ClassSymbolNative("Foo",
        delegate(ref DynVal v) 
        { 
          //fake object
          v.obj = null;
        }
      );

      globs.Define(cl);

      {
        var m = new FuncSymbolSimpleNative("self", globs.Type("Foo"),
          delegate()
          {
            var interp = Interpreter.instance;
            var obj = interp.PopValue().obj;
            interp.PushValue(DynVal.NewObj(obj));
            return BHS.SUCCESS;
          }
        );
        cl.Define(m);
      }

      {
        var m = new FuncSymbolNative("ret_int", globs.Type("int"),
          delegate() { return new Foo_ret_int(); }
        );
        m.Define(new FuncArgSymbol("val", globs.Type("int")));
        m.Define(new FuncArgSymbol("ticks", globs.Type("int")));
        cl.Define(m);
      }

    }

    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("1 2;10 20;", str);

    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInterleaveValuesStackInParalAll()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    func int ret_int(int val, int ticks)
    {
      while(ticks > 0)
      {
        yield()
        ticks = ticks - 1
      }
      return val
    }

    func void test() 
    {
      paral_all {
        foo(1, ret_int(val: 2, ticks: 1))
        foo(10, ret_int(val: 20, ticks: 2))
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("1 2;10 20;", str);

    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsStack()
  {
    string bhl = @"
    func int foo(int v) 
    {
      fail()
      return v
    }

    func hey(int a, int b)
    {
    }

    func void test() 
    {
      hey(1, foo(10))
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForFail()
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);
    //NodeDump(node);
    AssertEqual(intp.stack.Count, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForYield()
  {
    string bhl = @"

    func int foo()
    {
      yield()
      return 100
    }

    func hey(string b, int a)
    {
    }

    func test() 
    {
      hey(""bar"", 1 + foo())
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    AssertEqual(node.run(), BHS.RUNNING);
    node.stop();
    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForParal()
  {
    string bhl = @"

    func int calc()
    {
      yield()
      yield()
      return 100
    }

    func foo()
    {
      yield()
      fail()
    }

    func bar()
    {
      int i = 10 + calc()
    }

    func test() 
    {
      paral_all {
        seq_ {
          foo()
        }
        bar()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    AssertEqual(node.run(), BHS.RUNNING);
    AssertEqual(node.run(), BHS.RUNNING);
    AssertEqual(node.run(), BHS.SUCCESS);
    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForYieldInWhile()
  {
    string bhl = @"

    func int foo()
    {
      yield()
      return 1
    }

    func doer(ref int c)
    {
      while(c < 2) {
        c = c + foo()
      }
    }

    func test() 
    {
      int c = 0
      doer(ref c)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NOTE: making several runs
    AssertEqual(node.run(), BHS.RUNNING);
    node.stop();

    AssertEqual(node.run(), BHS.RUNNING);
    AssertEqual(node.run(), BHS.RUNNING);
    AssertEqual(node.run(), BHS.SUCCESS);

    AssertEqual(node.run(), BHS.RUNNING);
    AssertEqual(node.run(), BHS.RUNNING);
    AssertEqual(node.run(), BHS.SUCCESS);
    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStack()
  {
    string bhl = @"

    func hey(int n, int m)
    {
    }

    func int bar()
    {
      return 1
    }

    func int foo()
    {
      fail()
      return 100
    }

    func test() 
    {
      hey(bar(), foo())
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);
    //NodeDump(node);
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
      var fn = new FuncSymbolSimpleNative("hey", globs.Type("void"),
          delegate() { return BHS.SUCCESS; } );
      fn.Define(new FuncArgSymbol("s", globs.Type("string")));
      fn.Define(new FuncArgSymbol("i", globs.Type("int")));
      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);
    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackUserBindInSafeSeq()
  {
    string bhl = @"
    
    func test() 
    {
      seq_ {
        hey(1, foo())
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new FuncSymbolSimpleNative("foo", globs.Type("int"),
          delegate() { return BHS.FAILURE; } );
      globs.Define(fn);
    }

    {
      var fn = new FuncSymbolSimpleNative("hey", globs.Type("void"),
          delegate() { return BHS.SUCCESS; } );
      fn.Define(new FuncArgSymbol("i", globs.Type("int")));
      fn.Define(new FuncArgSymbol("b", globs.Type("int")));
      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExecNode(node, 0);
    AssertEqual(BHS.SUCCESS, res.status);
    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackUserBindInBinOp()
  {
    string bhl = @"
    func test() 
    {
      bool res = foo(false) != bar(4)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new FuncSymbolSimpleNative("foo", globs.Type("int"),
          delegate() { 
            Interpreter.instance.PopValue();
            Interpreter.instance.PushValue(DynVal.NewNum(42));
            return BHS.SUCCESS; 
          } );
      fn.Define(new FuncArgSymbol("b", globs.Type("bool")));
      globs.Define(fn);
    }

    {
      var fn = new FuncSymbolSimpleNative("bar", globs.Type("int"),
          delegate() { 
            Interpreter.instance.PopValue();
            return BHS.FAILURE; 
          } );
      fn.Define(new FuncArgSymbol("n", globs.Type("int")));
      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    AssertEqual(BHS.FAILURE, node.run());
    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackUserBindInEval()
  {
    string bhl = @"
    
    func test() 
    {
      bool res = eval {
        hey(1, foo())
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new FuncSymbolSimpleNative("foo", globs.Type("int"),
          delegate() { return BHS.FAILURE; } );
      globs.Define(fn);
    }

    {
      var fn = new FuncSymbolSimpleNative("hey", globs.Type("void"),
          delegate() { return BHS.SUCCESS; } );
      fn.Define(new FuncArgSymbol("i", globs.Type("int")));
      fn.Define(new FuncArgSymbol("b", globs.Type("int")));
      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExecNode(node, 0);
    AssertEqual(BHS.SUCCESS, res.status);
    //NodeDump(node);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    node.run();
    node.stop();
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);
    //NodeDump(node);
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
      var fn = new FuncSymbolNative("MakeFoo", globs.Type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.Define(new FuncArgSymbol("conf", globs.Type("Foo")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  public class NodeTakingFunc : BehaviorTreeTerminalNode
  {
    MemoryStream stream;
    FuncCtx fct;

    public NodeTakingFunc(MemoryStream stream)
    {
      this.stream = stream;
    }

    public override void init()
    {
      var interp = Interpreter.instance;
      fct = (FuncCtx)interp.PopValue().obj;
      fct = fct.AutoClone();
      fct.Retain();
    }

    public override void deinit()
    {
      fct.Release();
      fct = null;
    }

    public override BHS execute()
    {
      var interp = Interpreter.instance;

      var node = fct.GetCallNode();
      var status = node.run();
      if(status == BHS.SUCCESS)
      {
        var lst = interp.PopValue().obj as DynValList;

        var sw = new StreamWriter(stream);
        for(int i=0;i<lst.Count;++i)
          sw.Write(lst[i].num + ";");
        sw.Flush();

        lst.TryDel();
      }
      return BHS.SUCCESS;
    }
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForFuncPtrPassedAsArg()
  {
    string bhl = @"

    func int[] make_ints(int n, int k)
    {
      int[] res = []
      return res
    }

    func int may_fail()
    {
      fail()
      return 10
    }

    func void test() 
    {
      NodeTakingFunc(fn: func int[] () { return make_ints(2, may_fail()) } )
      trace(""HERE"")
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindTrace(globs, trace_stream);

    {
      var fn = new FuncSymbolNative("NodeTakingFunc", globs.Type("Foo"),
        delegate() { 
          return new NodeTakingFunc(trace_stream);
        }
      );
      fn.Define(new FuncArgSymbol("fn", globs.Type("int[]^()")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    AssertEqual(node.run(), BHS.SUCCESS);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("HERE", str);
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
      var fn = new FuncSymbolNative("MakeFoo", globs.Type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.Define(new FuncArgSymbol("conf", globs.Type("Foo")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("", str);
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
      var fn = new FuncSymbolNative("MakeFoo", globs.Type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.Define(new FuncArgSymbol("conf", globs.Type("Foo")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var status = node.run();
    AssertEqual(status, BHS.RUNNING);
    status = node.run();
    AssertEqual(status, BHS.FAILURE);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("", str);
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
      var fn = new FuncSymbolNative("MakeFoo", globs.Type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.Define(new FuncArgSymbol("conf", globs.Type("Foo")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var status = node.run();
    AssertEqual(status, BHS.RUNNING);
    status = node.run();
    AssertEqual(status, BHS.FAILURE);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("", str);
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
      var fn = new FuncSymbolNative("MakeFoo", globs.Type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.Define(new FuncArgSymbol("conf", globs.Type("Foo")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var status = node.run();
    AssertEqual(status, BHS.RUNNING);
    status = node.run();
    AssertEqual(status, BHS.FAILURE);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("", str);
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
      var fn = new FuncSymbolNative("MakeFoo", globs.Type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.Define(new FuncArgSymbol("conf", globs.Type("Foo")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var status = node.run();
    AssertEqual(status, BHS.RUNNING);
    status = node.run();
    AssertEqual(status, BHS.RUNNING);
    status = node.run();
    AssertEqual(status, BHS.SUCCESS);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("233", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncStackInExpressionForYield()
  {
    string bhl = @"

    func int sub_sub_call()
    {
      yield()
      return 2
    }

    func int sub_call()
    {
      return 1 + 10 + 12 + sub_sub_call()
    }

    func test() 
    {
      int cost = 1 + sub_call()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    AssertEqual(node.run(), BHS.RUNNING);
    node.stop();
    //NodeDump(node);

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
      var fn = new FuncSymbolNative("MakeFoo", globs.Type("Foo"),
          delegate() { return new MakeFooNode(); } );
      fn.Define(new FuncArgSymbol("conf", globs.Type("Foo")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(42));
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("3", str);
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    bool res = ExtractBool(ExecNode(node));

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
        Interpret(bhl, globs);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    bool res = ExtractBool(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("0;0;;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassMethodDecl()
  {
    string bhl = @"

    class Foo {
      
      int a

      func int getA() 
      {
        return this.a
      }
    }

    func int test()
    {
      Foo f = {}
      f.a = 10
      return f.getA()
    }
    ";

    var intp = Interpret(bhl, null);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestSeveralUserClassMethodDecl()
  {
    string bhl = @"

    class Foo {
      
      int a
      int b

      func int getA()
      {
        return this.b
      }

      func int getB() 
      {
        return this.a
      }
    }

    func int test()
    {
      Foo f = {}
      f.a = 10
      f.b = 10
      return f.getA() + f.getB()
    }
    ";

    var intp = Interpret(bhl, null);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 20);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassMethodSameNameLikeClass()
  {
    string bhl = @"

    class Foo {
      
      func int Foo()
      {
        return 0
      }
    }

    func int test()
    {
      Foo f = {}
      return f.Foo()
    }
    ";

    var intp = Interpret(bhl, null);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 0);
    CommonChecks(intp);
  }


  [IsTested()]
  public void TestUserClassMethodDeclVarLikeClassVar()
  {
    string bhl = @"
      class Foo {
        
        int a

        func int Foo()
        {
          a = 10
          return this.a + a
        }
      }

      func int test()
      {
        Foo f = {}
        f.a = 10
        return f.Foo()
      }
    ";

    var intp = Interpret(bhl, null);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 20);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassContainThisMemberNotAllowed()
  {
    string bhl = @"
      class Foo {
        int this
      }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      "the keyword \"this\" is reserved"
    );
  }

  [IsTested()]
  public void TestUserClassContainThisMethodNotAllowed()
  {
    string bhl = @"
      class Foo {

        func bool this()
        {
          return false
        }
      }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      "the keyword \"this\" is reserved"
    );
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));
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
        Interpret(bhl);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExecNode(node);

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExecNode(node);

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

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
        Interpret(bhl);
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
        Interpret(bhl);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);

    ExecNode(node, 0);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    bool res = ExtractBool(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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
        Interpret(bhl);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var n = ExtractNum(ExecNode(node));

    AssertEqual(6, n);
  }

  [IsTested()]
  public void TestExtendCSharpClassSubChild()
  {
    string bhl = @"

    class ColorA : Color 
    { 
      float a
    }

    class ColorF : ColorA 
    { 
      float f
    }
      
    func float test() 
    {
      ColorF c = { r : 1, g : 2, f : 3, a: 4}
      return c.r + c.g + c.a + c.f
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var n = ExtractNum(ExecNode(node));

    AssertEqual(10, n);
  }

  [IsTested()]
  public void TestExtendCSharpClassMemberAlreadyExists()
  {
    string bhl = @"

    class ColorA : Color 
    { 
      float g
    }
      
    func float test() 
    {
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      "already defined symbol 'g'"
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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
        Interpret(bhl);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 200);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestGlobalVariableInitWithSubCall()
  {
    string bhl = @"

    class Foo { 
      float b
    }

    func float bar(float f) {
      return f
    }

    Foo foo = {b : bar(100)}
      
    func float test() 
    {
      return foo.b
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 100);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestGlobalVariableInitWithComplexSubCall()
  {
    string bhl = @"

    class Foo { 
      Color c
    }

    func any col() {
      Color c = {r: 100}
      return c
    }

    Foo foo = {c : (Color)col()}
      
    func float test() 
    {
      return foo.c.r
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 100);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestGlobalVariableInitWithSubcallFail()
  {
    string bhl = @"

    class Foo { 
      float b
    }

    func float bar() {
      fail()
      return 100
    }

    Foo foo = {b : bar()}
      
    func float test() 
    {
      return foo.b
    }
    ";

    var error = false;
    try
    {
      Interpret(bhl);
    }
    catch(Exception)
    {
      error = true;
    }
    AssertTrue(error);
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

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    TestNewFile("bhl3.bhl", bhl3, files);

    var intp = CompileFiles(files, globs);

    var node = intp.GetFuncCallNode("bhl1", "test");
    node.SetArgs(DynVal.NewNum(23));
    //NodeDump(node);
    var res = ExecNode(node).val;

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

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    TestNewFile("bhl3.bhl", bhl3, files);
    
    var intp = CompileFiles(files, globs);

    var node = intp.GetFuncCallNode("bhl1", "test");
    node.SetArgs(DynVal.NewNum(2));
    //NodeDump(node);
    var res = ExecNode(node).val;

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

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    TestNewFile("bhl3.bhl", bhl3, files);

    var intp = CompileFiles(files, globs);

    var node = intp.GetFuncCallNode("bhl1", "test");
    node.SetArgs(DynVal.NewNum(23));
    //NodeDump(node);
    var res = ExecNode(node).val;

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

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    
    AssertError<UserError>(
      delegate() { 
        CompileFiles(files, globs);
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

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    TestNewFile("bhl3.bhl", bhl3, files);
    
    var intp = CompileFiles(files, globs);

    var node = intp.GetFuncCallNode("bhl1", "test");
    node.SetArgs(DynVal.NewNum(42));
    //NodeDump(node);
    var res = ExecNode(node).val;

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

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    
    AssertError<UserError>(
      delegate() { 
        CompileFiles(files, globs);
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

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    
    var intp = CompileFiles(files, globs);

    var node = intp.GetFuncCallNode("bhl1", "test");
    //NodeDump(node);
    var res = ExecNode(node).val;

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

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    
    AssertError<UserError>(
      delegate() { 
        CompileFiles(files, globs);
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

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    TestNewFile("bhl3.bhl", bhl3, files);
    
    var intp = CompileFiles(files, globs);

    var node = intp.GetFuncCallNode("bhl1", "test");
    //NodeDump(node);
    var res = ExecNode(node).val;

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

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    
    AssertError<UserError>(
      delegate() { 
        CompileFiles(files, globs);
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

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);

    var intp = CompileFiles(files, globs);
    intp.LoadModule("bhl1");

    var str = GetString(trace_stream);
    AssertEqual("once", str);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestImportGlobalExecutionOnlyOnceNested()
  {
    string bhl1 = @"
    import ""bhl2""  

    func test1() { }
    ";

    string bhl2 = @"
    import ""bhl3""

    func int dummy()
    {
      trace(""once"")
      return 1
    }

    class Foo {
      int a
    }

    Foo foo = { a : dummy() }

    func test2() { }
    ";

    string bhl3 = @"
    func test3() { }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    TestNewFile("bhl3.bhl", bhl3, files);

    var intp = CompileFiles(files, globs);
    intp.LoadModule("bhl1");

    var str = GetString(trace_stream);
    AssertEqual("once", str);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestGetCallstackInfo()
  {
    string bhl3 = @"
    func float wow(float b)
    {
      record_callstack()
      return b
    }
    ";

    string bhl2 = @"
    import ""bhl3""

    func float bar(float b)
    {
      return wow(b)
    }
    ";

    string bhl1 = @"
    import ""bhl2""


    func float foo(float k)
    {
      return bar(k)
    }

    func float test(float k) 
    {
      return foo(k)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var info = new List<Interpreter.CallStackInfo>();
    {
      var fn = new FuncSymbolSimpleNative("record_callstack", globs.Type("void"),
        delegate() { 
          Interpreter.instance.GetCallStackInfo(info); 
          return BHS.SUCCESS; 
        });
      globs.Define(fn);
    }

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    TestNewFile("bhl3.bhl", bhl3, files);

    var intp = CompileFiles(files, globs);
    
    var node = intp.GetFuncCallNode("bhl1", "test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractNum(ExecNode(node));

    AssertEqual(3, res);
    AssertEqual(3, info.Count);
    AssertEqual("bar", info[0].func_name);
    AssertEqual("bhl2", info[0].module_name);
    AssertEqual(6, info[0].line_num);
    AssertEqual("foo", info[1].func_name);
    AssertEqual("bhl1", info[1].module_name);
    AssertEqual(7, info[1].line_num);
    AssertEqual("test", info[2].func_name);
    AssertEqual("bhl1", info[2].module_name);
    AssertEqual(12, info[2].line_num);
  }

  [IsTested()]
  public void TestGetCallstackInfoFromFuncAsArg()
  {
    string bhl2 = @"
    func float bar(float b)
    {
      return b
    }

    func float wow(float b)
    {
      record_callstack()
      return b
    }

    func float foo(float k)
    {
      return bar(
          wow(k)
      )
    }

    ";

    string bhl1 = @"
    import ""bhl2""

    func float test(float k) 
    {
      return foo(k)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var info = new List<Interpreter.CallStackInfo>();
    {
      var fn = new FuncSymbolSimpleNative("record_callstack", globs.Type("void"),
        delegate() { 
          Interpreter.instance.GetCallStackInfo(info); 
          return BHS.SUCCESS; 
        });
      globs.Define(fn);
    }

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);

    var intp = CompileFiles(files, globs);
    
    var node = intp.GetFuncCallNode("bhl1", "test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractNum(ExecNode(node));

    AssertEqual(3, res);
    AssertEqual(2, info.Count);
    AssertEqual("foo", info[0].func_name);
    AssertEqual("bhl2", info[0].module_name);
    AssertEqual(16, info[0].line_num);
    AssertEqual("test", info[1].func_name);
    AssertEqual("bhl1", info[1].module_name);
    AssertEqual(6, info[1].line_num);
  }

  void BindForSlides(GlobalScope globs)
  {
    {
      var cl = new ClassSymbolNative("Vec3",
        delegate(ref DynVal v) 
        {}
      );

      globs.Define(cl);

      {
        var m = new FuncSymbolSimpleNative("Sub", globs.Type("Vec3"),
          delegate()
          {
            return BHS.SUCCESS;
          }
        );
        m.Define(new FuncArgSymbol("val", globs.Type("Vec3")));

        cl.Define(m);
      }

      cl.Define(new FieldSymbol("len", globs.Type("float"),
        delegate(DynVal ctx, ref DynVal v)
        {},
        //setter not allowed
        null
      ));
    }

    {
      var cl = new ClassSymbolNative("Unit",
        delegate(ref DynVal v) 
        {}
      );

      globs.Define(cl);
      globs.Define(new GenericArrayTypeSymbol(globs, new TypeRef(cl)));

      cl.Define(new FieldSymbol("position", globs.Type("Vec3"),
        delegate(DynVal ctx, ref DynVal v)
        {},
        //setter not allowed
        null
      ));
    }

    {
      var fn = new FuncSymbolSimpleNative("get_units", globs.Type("Unit[]"),
        delegate()
        {
          return BHS.SUCCESS;
        }
      );

      globs.Define(fn);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("FindUnit");
    if(node == null)
      throw new Exception("???");
    //NodeDump(node);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var vals = ExecNode(node, 2).vals;

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var vals = ExecNode(node, 3).vals;

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
        Interpret(bhl);
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
        Interpret(bhl);
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
        Interpret(bhl);
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var vals = ExecNode(node, 2).vals;
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var vals = ExecNode(node, 2).vals;

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var vals = ExecNode(node, 2).vals;

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var vals = ExecNode(node, 2).vals;

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var vals = ExecNode(node, 2).vals;

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
        Interpret(bhl);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var vals = ExecNode(node, 2).vals;

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var vals = ExecNode(node, 2).vals;

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
        Interpret(bhl);
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
        Interpret(bhl);
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
        Interpret(bhl);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

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
      var fn = new FuncSymbolSimpleNative("func_mult", globs.Type("float,string"), 
          delegate()
          {
            var interp = Interpreter.instance;
            
            interp.PushValue(DynVal.NewStr("foo"));
            interp.PushValue(DynVal.NewNum(42));
            return BHS.SUCCESS;
          }
        );
      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var vals = ExecNode(node, 2).vals;

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
      var fn = new FuncSymbolSimpleNative("func_mult", globs.Type("float,string,int,float"), 
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
      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var vals = ExecNode(node, 4).vals;
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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

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

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;INC1;INC2;DEC1;REL1;DEC0;INC1;INC2;DEC1;INC2;INC3;DEC2;INC3;DEC2;REL2;DEC1;REL1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestHashedName()
  {
    {
      var hn = new HashedName(0xBEAFDEADDEADBEAF);
      AssertEqual(0xDEADBEAF, hn.n1);
      AssertEqual(0xBEAFDEAD, hn.n2);
      AssertEqual(0xBEAFDEADDEADBEAF, hn.n);
      AssertEqual("", hn.s);
    }

    {
      var hn = new HashedName("Foo");
      AssertEqual(Hash.CRC28("Foo"), hn.n1);
      AssertEqual(0, hn.n2);
      AssertEqual(Hash.CRC28("Foo"), hn.n);
      AssertEqual("Foo", hn.s);
    }

    {
      var hn = new HashedName(0xBEAFDEADDEADBEAF, "Foo");
      AssertEqual(0xDEADBEAF, hn.n1);
      AssertEqual(0xBEAFDEAD, hn.n2);
      AssertEqual(0xBEAFDEADDEADBEAF, hn.n);
      AssertEqual("Foo", hn.s);
    }

    {
      var hn = new HashedName("Foo", 0xDEADBEAF);
      AssertEqual(Hash.CRC28("Foo"), hn.n1);
      AssertEqual(0xDEADBEAF, hn.n2);
      AssertEqual((ulong)0xDEADBEAF << 32 | (ulong)(Hash.CRC28("Foo")), hn.n);
      AssertEqual("Foo", hn.s);
    }
  }

  [IsTested()]
  public void TestStack()
  {
    //Push/PopFast
    {
      var st = new FixedStack<int>(16); 
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
    
    //Push/Pop(repl)
    {
      var st = new FixedStack<int>(16); 
      st.Push(1);
      st.Push(10);

      AssertEqual(st.Count, 2);
      AssertEqual(1, st[0]);
      AssertEqual(10, st[1]);

      AssertEqual(10, st.Pop(0));
      AssertEqual(st.Count, 1);
      AssertEqual(1, st[0]);

      AssertEqual(1, st.Pop(0));
      AssertEqual(st.Count, 0);
    }

    //Push/Dec
    {
      var st = new FixedStack<int>(16); 
      st.Push(1);
      st.Push(10);

      AssertEqual(st.Count, 2);
      AssertEqual(1, st[0]);
      AssertEqual(10, st[1]);

      st.Dec();
      AssertEqual(st.Count, 1);
      AssertEqual(1, st[0]);

      st.Dec();
      AssertEqual(st.Count, 0);
    }

    //RemoveAt
    {
      var st = new FixedStack<int>(16); 
      st.Push(1);
      st.Push(2);
      st.Push(3);

      st.RemoveAt(1);
      AssertEqual(st.Count, 2);
      AssertEqual(1, st[0]);
      AssertEqual(3, st[1]);
    }

    //RemoveAt
    {
      var st = new FixedStack<int>(16); 
      st.Push(1);
      st.Push(2);
      st.Push(3);

      st.RemoveAt(0);
      AssertEqual(st.Count, 2);
      AssertEqual(2, st[0]);
      AssertEqual(3, st[1]);
    }

    //RemoveAt
    {
      var st = new FixedStack<int>(16); 
      st.Push(1);
      st.Push(2);
      st.Push(3);

      st.RemoveAt(2);
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

  [IsTested()]
  public void TestOperatorTernaryIfIncompatibleTypes()
  {
    string bhl1 = @"
    func test()
    {
      string foo = true ? ""Foo"" : 1
    }
    ";

    string bhl2 = @"
    func int test()
    {
      return true ? ""Foo"" : ""Bar""
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() {
        Interpret(bhl1, globs);
      },
      "@(4,26) \"Foo\":<string>, @(4,34) 1:<int> have incompatible types"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl2, globs);
      },
      "@(2,4) funcinttest(){returntrue?\"Foo\":\"Bar\"}:<int>, @(4,13) true?\"Foo\":\"Bar\":<string> have incompatible types"
    );
  }

  [IsTested()]
  public void TestOperatorTernaryIf()
  {
    string bhl = @"

    func int foo(float a, int b)
    {
      return a < b ? (int)a : b
    }

    func int test1() 
    {
      float a = 100500
      int b   = 500100

      int c = a > b ? b : (int)a
      
      return foo(a > c ? b : c, (int)a)
    }

    func int test2() 
    {
      int^() af = func int() { return 500100 } //500100

      int^() bf = func int() { 
        int a = 2
        int b = 1

        int c = a > b ? 100500 : 500100

        return c //100500
      }

      int^() cf = func int() { 
        return true ? 100500 : 500100 //100500
      }

      return foo(af(), af() > bf() ? false ? af() : cf() : bf()) > af() ? af() : cf()
    }

    func string test3(int v)
    {
      int a = 1
      int b = 2

      defer
      {
        int d = a > b ? b + a : 0
        if(b > a ? true : false)
        {
          int c = a > b ? 1 : 2
          seq {
            int e = a > b ? 1 : 2
          }

          int j = foo(1, d == c ? 4 : 3)
        }
      }

      string^() af = func string() use(ref v) {
        return v == 1 ? ""first value""  :
               v == 2 ? ""second value"" :
               v == 3 ? ""result value"" : ""default value""
      }

      return af()
    }
    ";

    var intp = Interpret(bhl);
    var node1 = intp.GetFuncCallNode("test1");
    var node2 = intp.GetFuncCallNode("test2");
    var node3 = intp.GetFuncCallNode("test3");

    AssertEqual(ExtractNum(ExecNode(node1)), 100500);
    AssertEqual(ExtractNum(ExecNode(node2)), 100500);

    node3.SetArgs(DynVal.NewNum(0));
    AssertEqual(ExtractStr(ExecNode(node3)), "default value");
    node3.SetArgs(DynVal.NewNum(2));
    AssertEqual(ExtractStr(ExecNode(node3)), "second value");

    //NodeDump(node3);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestOperatorPostfixIncrementCall()
  {
    string bhl = @"
    func int test1()
    {
      int i = 0
      i++
      i = i - 1
      i++
      i = i - 1

      return i
    }

    func test2()
    {
      for(int i = 0; i < 3;) {
        trace((string)i)

        i++
      }

      for(int j = 0; j < 3; j++) {
        trace((string)j)
      }

      for(j = 0, int k = 0, k++; j < 3; j++, k++) {
        trace((string)j)
        trace((string)k)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);

    var node1 = intp.GetFuncCallNode("test1");
    AssertEqual(ExtractNum(ExecNode(node1)), 0);

    var node2 = intp.GetFuncCallNode("test2");
    ExecNode(node2, 0);

    var str = GetString(trace_stream);
    AssertEqual("012012011223", str);

    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestBadOperatorPostfixIncrementCall()
  {
    string bhl1 = @"
    func test()
    {
      ++
    }
    ";

    string bhl2 = @"
    func test()
    {
      string str = ""Foo""
      str++
    }
    ";

    string bhl3 = @"
    func test()
    {
      for(int j = 0; j < 3; ++) {
      }
    }
    ";

    string bhl4 = @"
    func test()
    {
      int j = 0
      for(++; j < 3; j++) {

      }
    }
    ";

    string bhl5 = @"
    func test()
    {
      for(j = 0, k++, int k = 0; j < 3; j++, k++) {
        trace((string)j)
        trace((string)k)
      }
    }
    ";

    string bhl6 = @"
    
    func foo(float a)
    {
    }

    func int test()
    {
      int i = 0
      foo(i++)
      return i
    }
    ";

    string bhl7 = @"
    func test()
    {
      int[] arr = [0, 1, 3, 4]
      int i = 0
      int j = arr[i++]
    }
    ";

    string bhl8 = @"
    func int test()
    {
      bool^(int) foo = func bool(int b) { return b > 1 }

      int i = 0
      foo(i++)
      return i
    }
    ";

    string bhl9 = @"
    func int test()
    {
      int i = 0
      return i++
    }
    ";

    string bhl10 = @"
    func int, int test()
    {
      int i = 0
      int j = 1
      return j, i++
    }
    ";

    string bhl11 = @"
    func int, int test()
    {
      int i = 0
      int j = 1
      return j++, i
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() {
        Interpret(bhl1, globs);
      },
      "extraneous input '++' expecting '}'"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl2, globs);
      },
      "operator ++ is not supported for string type"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl3, globs);
      },
      "extraneous input '++' expecting ')'"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl4, globs);
      },
      "extraneous input '++' expecting ';'"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl5, globs);
      },
      "symbol not resolved"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl6, globs);
      },
      "no viable alternative at input 'foo(i++'"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl7, globs);
      },
      "extraneous input '++' expecting ']'"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl8, globs);
      },
      "no viable alternative at input 'foo(i++'"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl9, globs);
      },
      "return value is missing"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl10, globs);
      },
      "extraneous input '++' expecting '}'"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl11, globs);
      },
      "mismatched input ',' expecting '}'"
    );
  }

  [IsTested()]
  public void TestBadOperatorPostfixDecrementCall()
  {
    string bhl1 = @"
    func test()
    {
      --
    }
    ";

    string bhl2 = @"
    func test()
    {
      string str = ""Foo""
      str--
    }
    ";

    string bhl3 = @"
    func test()
    {
      for(int j = 0; j < 3; --) {
      }
    }
    ";

    string bhl4 = @"
    func test()
    {
      int j = 0
      for(--; j < 3; j++) {

      }
    }
    ";

    string bhl5 = @"
    
    func foo(float a)
    {
    }

    func int test()
    {
      int i = 0
      foo(i--)
      return i
    }
    ";

    string bhl6 = @"
    func test()
    {
      int[] arr = [0, 1, 3, 4]
      int i = 0
      int j = arr[i--]
    }
    ";

    string bhl7 = @"
    func int test()
    {
      int i = 0
      return i--
    }
    ";

    string bhl8 = @"
    func int, int test()
    {
      int i = 0
      int j = 1
      return j, i--
    }
    ";

    string bhl9 = @"
    func int, int test()
    {
      int i = 0
      int j = 1
      return j--, i
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() {
        Interpret(bhl1, globs);
      },
      "extraneous input '--' expecting '}'"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl2, globs);
      },
      "operator -- is not supported for string type"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl3, globs);
      },
      "extraneous input '--' expecting ')'"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl4, globs);
      },
      "extraneous input '--' expecting ';'"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl5, globs);
      },
      "no viable alternative at input 'foo(i--'"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl6, globs);
      },
      "extraneous input '--' expecting ']'"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl7, globs);
      },
      "return value is missing"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl8, globs);
      },
      "extraneous input '--' expecting '}'"
    );

    AssertError<UserError>(
      delegate() {
        Interpret(bhl9, globs);
      },
      "mismatched input ',' expecting '}'"
    );
  }

  [IsTested()]
  public void TestOperatorPostfixDecrementCall()
  {
    string bhl = @"
    func int test1()
    {
      int i = 0
      i--
      i = i + 1
      i--
      i = i + 1

      return i
    }

    func test2()
    {
      for(int i = 3; i >= 0;) {
        trace((string)i)

        i--
      }

      for(int j = 3; j >= 0; j--) {
        trace((string)j)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);

    var node1 = intp.GetFuncCallNode("test1");
    AssertEqual(ExtractNum(ExecNode(node1)), 0);

    var node2 = intp.GetFuncCallNode("test2");
    ExecNode(node2, 0);

    var str = GetString(trace_stream);
    AssertEqual("32103210", str);

    //NodeDump(node1);
    CommonChecks(intp);
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
  public void TestWeird()
  {
    string bhl = @"

    func A(int b = 1)
    {
      trace(""A"" + (string)b)
      suspend()
    }

    func test() 
    {
      forever {
        int i = 0
        paral {
          A()
          seq {
            while(i < 1) {
              yield()
              i = i + 1
            }
          }
        }
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScript(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    node.run();
    node.run();
    node.run();
    node.stop();

    var str = GetString(trace_stream);

    AssertEqual("A1A1", str);
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");

    const int x = 15;

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      node.SetArgs(DynVal.NewNum(x));
      ExtractNum(ExecNode(node));
      stopwatch.Stop();
      Console.WriteLine("bhl fib ticks: {0}", stopwatch.ElapsedTicks);
    }

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      node.SetArgs(DynVal.NewNum(x));
      ExtractNum(ExecNode(node));
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

  static string TestDirPath()
  {
    string self_bin = System.Reflection.Assembly.GetExecutingAssembly().Location;
    return Path.GetDirectoryName(self_bin) + "/tmp/tests";
  }

  static void TestCleanDir()
  {
    string dir = TestDirPath();
    if(Directory.Exists(dir))
      Directory.Delete(dir, true/*recursive*/);
  }

  static void TestNewFile(string path, string text, List<string> files)
  {
    string full_path = TestDirPath() + "/" + path;
    Directory.CreateDirectory(Path.GetDirectoryName(full_path));
    File.WriteAllText(full_path, text);
    files.Add(full_path);
  }

  static void SharedInit()
  {
    DynVal.PoolClear();
    DynValList.PoolClear();
    DynValDict.PoolClear();
    FuncCallNode.PoolClear();
    FuncCtx.PoolClear();
  }

  static Interpreter CompileFiles(List<string> test_files, GlobalScope globs = null)
  {
    globs = globs == null ? SymbolTable.CreateBuiltins() : globs;
    //NOTE: we want interpreter to work with original globs
    var globs_copy = globs.Clone();
    SharedInit();

    var conf = new BuildConf();
    conf.compile_fmt = CompileFormat.AST;
    conf.globs = globs;
    conf.files = test_files;
    conf.res_file = TestDirPath() + "/result.bin";
    conf.inc_dir = TestDirPath();
    conf.cache_dir = TestDirPath() + "/cache";
    conf.err_file = TestDirPath() + "/error.log";
    conf.use_cache = false;
    conf.debug = true;

    var bld = new Build();
    int res = bld.Exec(conf);
    if(res != 0)
      throw new UserError(File.ReadAllText(conf.err_file));

    var intp = Interpreter.instance;
    var bin = new MemoryStream(File.ReadAllBytes(conf.res_file));
    var mloader = new ModuleLoader(bin);

    intp.Init(globs_copy, mloader);

    return intp;
  }

  static void NodeDump(BehaviorTreeNode node)
  {
    Util.NodeDump(node);
  }

  public struct Result
  {
    public BHS status;
    public DynVal[] vals;

    public DynVal val 
    {
      get {
        return vals != null ? vals[0] : null;
      }
    }
  }

  static Result ExecNode(BehaviorTreeNode node, int ret_vals = 1, bool keep_running = true)
  {
    Result res = new Result();

    res.status = BHS.NONE;
    while(true)
    {
      res.status = node.run();
      if(res.status != BHS.RUNNING)
        break;

      if(!keep_running)
        break;
    }
    if(ret_vals > 0)
    {
      res.vals = new DynVal[ret_vals];
      for(int i=0;i<ret_vals;++i)
        res.vals[i] = Interpreter.instance.PopValue();
    }
    else
      res.vals = null;
    return res;
  }

  static Interpreter Interpret(string src, GlobalScope globs = null, bool show_ast = false)
  {
    globs = globs == null ? SymbolTable.CreateBuiltins() : globs;
    //NOTE: we want interpreter to work with original globs
    var globs_copy = globs.Clone();
    SharedInit();

    var intp = Interpreter.instance;
    intp.Init(globs_copy, null/*empty module loader*/);

    var mreg = new ModuleRegistry();
    //fake module for this specific case
    var mod = new bhl.Module("", "");
    var bin = new MemoryStream();
    Frontend.Source2Bin(mod, src.ToStream(), bin, globs, mreg);
    bin.Position = 0;

    var ast = Util.Bin2Meta<AST_Module>(bin);
    if(show_ast)
      Util.ASTDump(ast);

    intp.Interpret(ast);
  
    return intp;
  }

  void CommonChecks(Interpreter intp)
  {
    intp.glob_mem.Clear();

    //for extra debug
    //Console.WriteLine(DynVal.PoolDump());

    if(intp.stack.Count > 0)
    {
      Console.WriteLine("=== Dangling stack values ===");
      for(int i=0;i<intp.stack.Count;++i)
        Console.WriteLine("Stack value #" + i + " " + intp.stack[i]);
    }
    AssertEqual(intp.stack.Count, 0);
    AssertEqual(intp.node_ctx_stack.Count, 0);
    AssertEqual(DynVal.PoolCount, DynVal.PoolCountFree);
    AssertEqual(DynValList.PoolCount, DynValList.PoolCountFree);
    AssertEqual(DynValDict.PoolCount, DynValDict.PoolCountFree);
    AssertEqual(FuncCtx.PoolCount, FuncCtx.PoolCountFree);
  }

  static double ExtractNum(Result res)
  {
    return res.val.num;
  }

  static bool ExtractBool(Result res)
  {
    return res.val.bval;
  }

  static string ExtractStr(Result res)
  {
    return res.val.str;
  }

  static object ExtractObj(Result res)
  {
    return res.val.obj;
  }
}

