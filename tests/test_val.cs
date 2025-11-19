using bhl;
using Xunit;

public class TestVal : BHL_TestBase
{
  [Fact]
  public void TestImplicitCast()
  {
    {
      int n = 10;
      Val v = n;
      Assert.Equal(n, (int)v);
    }

    {
      double n = 10.1;
      Val v = n;
      Assert.Equal(n, (double)v);
    }

    {
      float n = 10.1f;
      Val v = n;
      Assert.Equal(n, (float)v);
    }

    {
      uint n = 25379563u;
      Val v = n;
      Assert.Equal(n, (uint)v);
    }

    {
      bool n = true;
      Val v = n;
      Assert.Equal(n, (bool)v);
    }

    {
      string s = "hey";
      Val v = s;
      Assert.Equal(s, (string)v);
    }
  }

  [Fact]
  public void TestValStackGrow()
  {
    var stack = new ValStack(1);
    Assert.Equal(0, stack.sp);
    Assert.Single(stack.vals);

    stack.Push(10);
    Assert.Equal(1, stack.sp);
    Assert.Single(stack.vals);

    stack.Push(20);
    Assert.Equal(2, stack.sp);
    Assert.Equal(2, stack.vals.Length);

    stack.Push(30);
    Assert.Equal(3, stack.sp);
    Assert.Equal(4, stack.vals.Length);

    stack.Push(40);
    Assert.Equal(4, stack.sp);
    Assert.Equal(4, stack.vals.Length);

    stack.Push(50);
    Assert.Equal(5, stack.sp);
    Assert.Equal(8, stack.vals.Length);
  }

}
