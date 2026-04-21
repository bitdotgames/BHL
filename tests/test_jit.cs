using System;
using bhl;
using Xunit;

public class TestJIT : BHL_TestBase
{
  static void DumpFibBytecode(FuncSymbolScript fs)
  {
    var bc = fs.module.compiled.bytecode;
    var consts = fs.module.compiled.constants;
    int ip = fs.ip_addr;
    Console.WriteLine($"\nFunction '{fs.name}' bytecode starting at ip={ip}:");
    for(int limit = 0; limit < 50 && ip < bc.Length; limit++)
    {
      var op = (Opcodes)bc[ip];
      var def = ModuleCompiler.LookupOpcode(op);
      Console.Write($"  [{ip:D3}] {op,-20}");
      int rpos = ip + 1;
      for(int i = 0; i < def.operand_width.Length; i++)
      {
        int w = def.operand_width[i];
        int val = w switch {
          1 => (sbyte)bc[rpos],
          2 => (short)(bc[rpos] | (bc[rpos+1] << 8)),
          3 => (int)((uint)(bc[rpos] | bc[rpos+1]<<8 | bc[rpos+2]<<16)),
          4 => (int)((uint)(bc[rpos] | (uint)bc[rpos+1]<<8 | (uint)bc[rpos+2]<<16 | (uint)bc[rpos+3]<<24)),
          _ => 0
        };
        if(op == Opcodes.Constant && i == 0 && val < consts.Length)
          Console.Write($"  const[{val}]={consts[val].num}");
        else if((op == Opcodes.Jump || op == Opcodes.JumpZ) && i == 0)
          Console.Write($"  offset={val} -> target={ip + def.size + val}");
        else
          Console.Write($"  {val}");
        rpos += w;
      }
      Console.WriteLine();
      ip += def.size;
    }
    Console.WriteLine();
  }

  [Fact]
  public void TestJitSimple()
  {
    string bhl = @"
    func int triple(int x) {
      return x + x + x
    }
    ";
    var vm = MakeVM(bhl);
    vm.TryFindFuncAddr("triple", out var addr);
    var fs = addr.fs;
    DumpFibBytecode(fs);
    BHLJit.TryCompile(fs);
    Assert.NotNull(fs._jit_func);
    Assert.Equal(0, (int)fs._jit_func(0.0));
    Assert.Equal(3, (int)fs._jit_func(1.0));
    Assert.Equal(15, (int)fs._jit_func(5.0));
  }

  [Fact]
  public void TestJitBranching()
  {
    string bhl = @"
    func int classify(int x) {
      if(x == 0) {
        return 0
      } else {
        return 1
      }
    }
    ";
    var vm = MakeVM(bhl);
    vm.TryFindFuncAddr("classify", out var addr);
    var fs = addr.fs;
    DumpFibBytecode(fs);
    BHLJit.TryCompile(fs);
    Assert.NotNull(fs._jit_func);
    Assert.Equal(0, (int)fs._jit_func(0.0));
    Assert.Equal(1, (int)fs._jit_func(1.0));
    Assert.Equal(1, (int)fs._jit_func(-1.0));
  }

  [Fact]
  public void TestJitFibCorrectness()
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
    ";

    var vm = MakeVM(bhl);
    vm.TryFindFuncAddr("fib", out var addr);
    var fs = addr.fs;

    DumpFibBytecode(fs);

    // Force JIT compile
    BHLJit.TryCompile(fs);
    Assert.NotNull(fs._jit_func);

    int[] expected = { 0, 1, 1, 2, 3, 5, 8, 13, 21, 34, 55 };

    // Direct delegate call
    for(int n = 0; n <= 10; n++)
      Assert.Equal(expected[n], (int)fs._jit_func((double)n));

    // Via vm.Execute (uses JIT fast path)
    for(int n = 0; n <= 10; n++)
      Assert.Equal(expected[n], (int)Execute(vm, "fib", (Val)n).Stack.Pop().num);
  }
}
