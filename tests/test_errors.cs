using System;
using System.Collections.Generic;
using bhl;

public class TestErrors : BHL_TestBase
{
  [IsTested()]
  public void TestSeveralErrorsInOneFile()
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
  public void TestSeveralErrorsInManyFiles()
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
}
