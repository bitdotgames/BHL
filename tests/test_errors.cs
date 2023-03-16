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
      "extraneous input '('",
      new PlaceAssert(bhl, @"
    []Color color() {
-----------------^"
      )
    );
  }

  //TODO:
  //[IsTested()]
  public void TestIncompleteFuncCall()
  {
    string bhl = @"
    func foo(int a) {
    }

    func test() {
      foo(1,
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incomplete statement",
      new PlaceAssert(bhl, @"
      foo(1,
------^"
      )
    );
  }

  //TODO:
  //[IsTested()]
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

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incomplete statement",
      new PlaceAssert(bhl, @"
      f.
------^"
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

    var lines = File.ReadAllText(conf.err_file).Split('\n');
    AssertEqual(2, lines.Length);

    AssertTrue(lines[0].Contains("{\"error\": \"matching 'return' statement not found\","));
    AssertTrue(lines[0].Contains("bhl1.bhl\", \"line\": 2, \"column\" : 9"));

    AssertTrue(lines[1].Contains("{\"error\": \"incompatible types: 'void' and 'int'\","));
    AssertTrue(lines[1].Contains("bhl1.bhl\", \"line\": 8, \"column\" : 13"));
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
