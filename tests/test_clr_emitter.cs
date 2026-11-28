using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using bhl;
using System.Text;
using bhl.spike.clr;
using Xunit;

// Runs a script on the VM and on generated C#, and compares. The emitter is a slice - scalar
// functions, control flow and calls - so this is a conformance harness for that slice, not a
// backend. See spikes/clr/emitter/CsEmitter.cs.
public class TestCLREmitter : BHL_TestBase
{
  static Assembly CompileCs(string src)
  {
    var tree = CSharpSyntaxTree.ParseText(src);

    var refs = AppDomain.CurrentDomain.GetAssemblies()
      .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
      .Select(a => MetadataReference.CreateFromFile(a.Location))
      .Cast<MetadataReference>()
      .ToList();

    var comp = CSharpCompilation.Create(
      "clr_spike_" + Guid.NewGuid().ToString("N"),
      new[] { tree },
      refs,
      new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    using(var ms = new MemoryStream())
    {
      var res = comp.Emit(ms);
      if(!res.Success)
      {
        var errs = string.Join("\n", res.Diagnostics
          .Where(d => d.Severity == DiagnosticSeverity.Error)
          .Select(d => d.ToString()));
        throw new Exception("generated C# does not compile:\n" + errs + "\n--- source ---\n" + src);
      }
      return Assembly.Load(ms.ToArray());
    }
  }

  //NOTE: runs 'func_name' on the VM and on the generated C#, returns both results
  (double vm, double cs, string src) RunBoth(string bhl, string func_name, params double[] args)
  {
    //NOTE: the AST isn't kept on the compiled module, so parse separately for the emitter
    var ast = Parse(bhl, new Types()).result.ast;

    var cm = Compile(bhl);
    var vm = MakeVM(cm);
    var fb = args.Length == 0
      ? Execute(vm, func_name)
      : Execute(vm, func_name, args.Select(a => Val.NewFlt(a)).ToArray());
    double vm_res = fb.Stack.sp > 0 ? fb.Stack.Pop().num : 0;
    CommonChecks(vm);

    var src = CsEmitter.Emit(ast);
    var asm = CompileCs(src);
    var m = asm.GetType("Gen").GetMethod(func_name);

    var boxed = new object[args.Length];
    var ps = m.GetParameters();
    for(int i = 0; i < args.Length; ++i)
      boxed[i] = Convert.ChangeType(args[i], ps[i].ParameterType);

    double cs_res = Convert.ToDouble(m.Invoke(null, boxed) ?? 0.0);

    return (vm_res, cs_res, src);
  }

  void AssertSame(string bhl, string func_name, params double[] args)
  {
    var r = RunBoth(bhl, func_name, args);
    Assert.True(r.vm == r.cs,
      $"VM gave {r.vm}, generated C# gave {r.cs}\n--- generated ---\n{r.src}");
  }

  [Fact]
  public void TestArithmeticAndLocals()
  {
    AssertSame(@"
    func int test(int x) {
      int a = x * 2
      int b = a - 1
      return a + b * 3
    }
    ", "test", 7);
  }

  [Fact]
  public void TestIfElseChain()
  {
    string bhl = @"
    func int test(int x) {
      if(x < 0) {
        return -1
      } else if(x == 0) {
        return 0
      } else {
        return 1
      }
    }
    ";
    AssertSame(bhl, "test", -5);
    AssertSame(bhl, "test", 0);
    AssertSame(bhl, "test", 9);
  }

  [Fact]
  public void TestWhileWithBreakAndContinue()
  {
    AssertSame(@"
    func int test(int n) {
      int s = 0
      int i = 0
      while(i < n) {
        i = i + 1
        if(i == 3) {
          continue
        }
        if(i > 6) {
          break
        }
        s = s + i
      }
      return s
    }
    ", "test", 10);
  }

  [Fact]
  public void TestRecursiveCalls()
  {
    AssertSame(@"
    func int fib(int x) {
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

    func int test(int n) {
      return fib(n)
    }
    ", "test", 15);
  }

  [Fact]
  public void TestBoolAndComparisons()
  {
    AssertSame(@"
    func int test(int x) {
      bool a = x > 3 && x < 10
      bool b = !a || x == 42
      if(a && !b) {
        return 1
      }
      return 0
    }
    ", "test", 5);
  }

  //NOTE: what the slice refuses, so the boundary is explicit rather than a mystery crash
  //NOTE: coro scripts are compared by what they trace, the way the VM tests assert
  void AssertSameTrace(string bhl, string func_name)
  {
    //NOTE: the AST has to be built with the same bindings the VM gets, or unresolved calls
    //      simply aren't in it
    var parse_ts = new Types();
    BindTrace(parse_ts, new StringBuilder());
    var ast = Parse(bhl, parse_ts).result.ast;

    var vm_log = new StringBuilder();
    var vm = MakeVM(bhl, ts => BindTrace(ts, vm_log));
    Execute(vm, func_name);
    CommonChecks(vm);

    var src = CsEmitter.Emit(ast);
    var asm = CompileCs(src);
    var gen = asm.GetType("Gen");

    var cs_log = new StringBuilder();
    gen.GetField("__trace").SetValue(null, new Action<string>(x => cs_log.Append(x)));

    var ret = new BhlRt.Ret();
    var co = (IEnumerator<BhlRt.Tick>)gen.GetMethod(func_name).Invoke(null, new object[] { ret });
    BhlRt.Run(co, ret);

    Assert.True(vm_log.ToString() == cs_log.ToString(),
      $"VM traced '{vm_log}', generated C# traced '{cs_log}'\n--- generated ---\n{src}");
  }

  [Fact]
  public void TestYieldAndDefer()
  {
    AssertSameTrace(@"
    coro func test() {
      defer {
        trace(""~;"")
      }
      trace(""a;"")
      yield()
      trace(""b;"")
    }
    ", "test");
  }

  [Fact]
  public void TestParalWithForInABranchAndDefer()
  {
    AssertSameTrace(@"
    coro func test()
    {
      paral {
        {
          yield()
          yield()
          yield()
        }
        {
          for(int i = 0; i < 3; i++) {
            defer {
              trace((string)i+""~;"")
            }
            trace((string)i+"";"")
            yield()
          }
        }
      }
    }
    ", "test");
  }

  [Fact]
  public void TestParalAllWaitsForBoth()
  {
    AssertSameTrace(@"
    coro func test()
    {
      paral_all {
        {
          yield()
          trace(""a;"")
        }
        {
          yield()
          yield()
          trace(""b;"")
        }
      }
      trace(""done;"")
    }
    ", "test");
  }

  [Fact]
  public void TestCoroCallingCoro()
  {
    AssertSameTrace(@"
    coro func sub(int n) {
      defer { trace(""~sub;"") }
      trace(""sub;"")
      yield()
    }

    coro func test() {
      trace(""a;"")
      yield sub(1)
      trace(""b;"")
    }
    ", "test");
  }

  [Fact]
  public void TestUnsupportedIsReported()
  {
    var ast = Parse(@"
    class Foo { int x }

    func test() {
    }
    ", new Types()).result.ast;

    var e = Assert.Throws<CsEmitter.NotSupported>(() => CsEmitter.Emit(ast));
    Assert.Contains("classes", e.Message);
  }
}
