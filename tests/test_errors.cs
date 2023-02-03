using System;
using System.Collections.Generic;
using System.IO;
using bhl;

public class TestErrors : BHL_TestBase
{
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
    catch(MultiCompileErrors m)
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
    catch(MultiCompileErrors m)
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

  [IsTested()]
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
    catch(MultiCompileErrors m)
    {
      AssertEqual(2/* + 3*/, m.errors.Count);

      AssertError((Exception)m.errors[0],
        "no viable alternative at input 'foo(",
        new PlaceAssert(bhl, @"
    }
----^"
        )
      );

      AssertError((Exception)m.errors[1],
        "no viable alternative at input '}'",
        new PlaceAssert(bhl, @"
    }
----^"
        )
      );

//      AssertError((Exception)m.errors[2],
//        "symbol usage is not valid",
//        new PlaceAssert(bhl, @"
//      int i =
//------^"
//        )
//      );
//
//      AssertError((Exception)m.errors[3],
//        "useless statement",
//        new PlaceAssert(bhl, @"
//      int i =
//------^"
//        )
//      );
//
//      AssertError((Exception)m.errors[4],
//        "symbol 'i' not resolved",
//        new PlaceAssert(bhl, @"
//      int i =
//----------^"
//        )
//      );
    }
  }
}
