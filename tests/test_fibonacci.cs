using System;
using System.Text;
using bhl;
using Xunit;

public class TestFibonacci : BHL_TestBase
{
  [Fact]
  public void Test()
  {
    string bhl = @"
    func int fib(int x)
    {
      //__dump_opcodes_on();

      if(x == 0) {
        return 0
      } else {
        if(x == 1) {
          return 1
        } else {
          return fib(x - 1) + fib(x - 2)
        }
      }

      //__dump_opcodes_off();
    }

    func test() {
      //__dump_opcodes_on();

      fib(1)

      //__dump_opcodes_off();
    }
    ";

    Compile(bhl/*, show_bytes: true*/);
  }
}

