using System;
using System.Collections.Generic;
using System.IO;
using bhl;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

public class TestErrors : BHL_TestBase
{
  [IsTested()]
  public void TestMissingFuncKeyword()
  {
    string bhl = @"
    []Color color() {
      []Color cs = get_colors()
      return cs
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "at input '('",
      new PlaceAssert(bhl, @"
    []Color color() {
-----------------^"
      )
    );
  }

  [IsTested()]
  public void TestInvalidIncOperator()
  {
    string bhl = @"
    func test() {
      int a
      a +++++++++== 1
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "rule eos failed predicate",
      new PlaceAssert(bhl, @"
      a +++++++++== 1
----------^"
      )
    );
  }

  [IsTested()]
  public void TestIncompleteFuncSignature()
  {
    //TODO: make this test work under Windows (error assertion doesn't match)
    if(!IsUnix())
      return;

    string bhl = @"
    func foo(int a,
    {";

    //TODO: error hint placement must be more precise
    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "mismatched input",
      new PlaceAssert(bhl, @"
    {
----^"
      )
    );
  }

  [IsTested()]
  public void TestIncompleteCoroFuncSignature()
  {
    string bhl = @"
    coro func foo(, int a)";

    //TODO: error hint placement must be more precise
    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "no viable alternative at input 'coro func foo(,'",
      new PlaceAssert(bhl, @"
    coro func foo(, int a)
------------------^"
      )
    );
  }

  [IsTested()]
  public void TestIncompleteFuncCall()
  {
    string bhl = @"
    func test() {
      foo(1,
    }
    ";

    //TODO: error hint placement must be more precise
    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "mismatched input",
      new PlaceAssert(bhl, @"
    }
----^"
      )
    );
  }

  [IsTested()]
  public void TestIncompleteMemberAccess()
  {
    string bhl = @"
    class Foo {
      int a
    }

    func test() {
      var f = new Foo
      f.
    }
    ";

    //TODO: error hint placement must be more precise
    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "no viable alternative at input",
      new PlaceAssert(bhl, @"
    }
----^"
      )
    );
  }

  [IsTested()]
  public void TestSeveralSemanticErrorsInOneFile()
  {
    string bhl = @"
    func int foo() 
    {
    }

    func bar() 
    {
      return 1
    }
    ";

    try
    {
      Compile(bhl);
    }
    catch(CompileErrorsException m)
    {
      AssertEqual(2, m.errors.Count);

      AssertError((Exception)m.errors[0],
        "matching 'return' statement not found",
        new PlaceAssert(bhl, @"
    func int foo() 
---------^"
        )
      );

      AssertError((Exception)m.errors[1],
        "incompatible types: 'void' and 'int'",
        new PlaceAssert(bhl, @"
      return 1
-------------^"
        )
      );
    }
  }

  [IsTested()]
  public void TestSeveralSemanticErrorsInManyFiles()
  {
    string bhl1 = @"
    func float bhl1() 
    {
      return 
    }
    ";

    string bhl2 = @"
    import ""bhl1""  

    func bhl2()
    {
      return bhl1()
    }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);

    try
    {
      CompileFiles(files);
    }
    catch(CompileErrorsException m)
    {
      AssertEqual(2, m.errors.Count);

      AssertError((Exception)m.errors[0],
        "return value is missing",
        new PlaceAssert(bhl1, @"
      return 
------^"
        )
      );

      AssertError((Exception)m.errors[1],
        "incompatible types: 'void' and 'float'",
        new PlaceAssert(bhl2, @"
      return bhl1()
-------------^"
        )
      );
    }
  }

  [IsTested()]
  public void TestSeveralSemanticErrorsDumpedIntoErrorFile()
  {
    string bhl1 = @"
    func int foo() 
    {
    }

    func bar() 
    {
      return 1
    }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);

    var conf = MakeCompileConf(files);
    try
    {
      CompileFiles(conf);
    }
    catch(Exception) 
    {}

    var lines = File.ReadAllText(conf.proj.error_file).Split('\n');
    AssertEqual(2, lines.Length);

    AssertTrue(lines[0].Contains("{\"error\": \"matching 'return' statement not found\","));
    AssertTrue(lines[0].Contains("bhl1.bhl\", \"line\": 2, \"column\" : 9"));

    AssertTrue(lines[1].Contains("{\"error\": \"incompatible types: 'void' and 'int'\","));
    AssertTrue(lines[1].Contains("bhl1.bhl\", \"line\": 8, \"column\" : 13"));
  }

  [IsTested()]
  public void TestSeveralErrorsDumpedIntoErrorFile()
  {
    string bhl1 = @"
    func int foo() { }

    func bar() 
    {
      fdf &* =1
    }
    ";

    string bhl2 = @"
    func wow() 
    {
      return 1
        fdf /
    }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);

    var conf = MakeCompileConf(files);
    try
    {
      CompileFiles(conf);
    }
    catch(Exception) 
    {}

    var lines = File.ReadAllText(conf.proj.error_file).Split('\n');
    AssertEqual(6, lines.Length);

    AssertTrue(lines[0].Contains("bhl1.bhl\", \"line\": 6, \"column\" : 10"));
    AssertTrue(lines[1].Contains("bhl2.bhl\", \"line\": 5, \"column\" : 12"));
    AssertTrue(lines[2].Contains("bhl1.bhl\", \"line\": 2, \"column\" : 9"));
    AssertTrue(lines[3].Contains("bhl1.bhl\", \"line\": 6, \"column\" : 6"));
    AssertTrue(lines[4].Contains("bhl2.bhl\", \"line\": 4, \"column\" : 13"));
    AssertTrue(lines[5].Contains("bhl2.bhl\", \"line\": 5, \"column\" : 8"));
  }

  //TODO
  //[IsTested()]
  public void TestBorkedInput()
  {
    string bhl = @"
    func test(fdf,sff=0) {
      //foo(wow(arg,fdf
      //foo.foo(f,
    }
    ";
    Compile(bhl, show_parse_tree: true, show_ast: true);
  }

  //TODO
  //[IsTested()]
  public void TestSeveralSyntaxErrors()
  {
    string bhl = @"
    func foo() { }

    func test() {
      foo(
    }

    func hey() {
      int i =
    }
    ";

    try
    {
      Compile(bhl);
    }
    catch(CompileErrorsException m)
    {
      AssertEqual(2 + 2, m.errors.Count);

      AssertError((Exception)m.errors[0],
        "no viable alternative at input '(",
        new PlaceAssert(bhl, @"
    }
----^"
        )
      );

      AssertError((Exception)m.errors[1],
        "no viable alternative at input 'i =",
        new PlaceAssert(bhl, @"
    }
----^"
        )
      );

      AssertError((Exception)m.errors[2],
        "useless statement",
        new PlaceAssert(bhl, @"
      foo(
------^"
        )
      );

      AssertError((Exception)m.errors[3],
        "useless statement",
        new PlaceAssert(bhl, @"
      int i =
------^"
        )
      );
    }
  }

//  [IsTested()]
//  public void TestErrorNodes()
//  {
//    string bhl = @"
//
//    func test() {
//      var f = new Foo
//      f[0].foo(
//      //f.k(,1)
//      //f.k(1,
//    }
//
//    class Foo
//    {
//      int a
//      func int foo (int f, int z) {
//        return 1
//      }
//    }
//
//    ";
//
//    var proc = Parse(bhl, new Types(), show_ast: true);
//
//    var test = (FuncSymbol)proc.result.module.ns.Resolve("test");
//    AssertTrue(test != null);
//
//    var foo = (ClassSymbol)proc.result.module.ns.Resolve("Foo");
//    AssertTrue(foo != null);
//
//    proc.result.errors.Dump();
//
//    var descs = Trees.Descendants(proc.parsed.prog);
//    IErrorNode en = null;
//    var derrs = new List<IErrorNode>();
//    foreach(var d in descs)
//    {
//      if(d is IErrorNode _en)
//      {
//        en = _en;
//        Console.WriteLine("ERR " + _en/* + " " + en.Parent.GetText() + " " + en.Parent.GetType().Name + " " + en.Parent.Parent.GetText() + " " + en.Parent.Parent.GetType().Name*/);
//        derrs.Add(_en);
//      }
//    }
//
//    if(en != null)
//    {
//      var errs = new List<IErrorNode>();
//
//      var ctx = en.Parent as ParserRuleContext;
//      if(ctx != null)
//      {
//        foreach(var c in ctx.children)
//        {
//          if(c is IErrorNode cen)
//            errs.Add(cen);
//        }
//      }
//
//      Console.WriteLine(errs.Count + " VS " + derrs.Count + " " + en.ToString());
//    }
//
//    //Console.WriteLine(proc.parsed);
//    //Console.WriteLine(Trees.ToStringTree(proc.parsed.prog, proc.parsed.parser.RuleNames));
//    Console.WriteLine(proc.parsed);
//  }
}
