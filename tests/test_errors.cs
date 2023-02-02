using System;
using bhl;

public class TestErrors : BHL_TestBase
{
  [IsTested()]
  public void TestSeveralErrors()
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
}
