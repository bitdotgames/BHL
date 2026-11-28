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
    var parse_ts = new Types();
    //NOTE: a module's own symbols live on its namespace, not the Types' one
    var res = Parse(bhl, parse_ts).result;
    var ast = res.ast;
    var ns = res.module.ns;

    var cm = Compile(bhl);
    var vm = MakeVM(cm);
    var fb = args.Length == 0
      ? Execute(vm, func_name)
      : Execute(vm, func_name, args.Select(a => Val.NewFlt(a)).ToArray());
    double vm_res = fb.Stack.sp > 0 ? fb.Stack.Pop().num : 0;
    CommonChecks(vm);

    var src = CsEmitter.Emit(ast, "Gen", ns);
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

  [Fact]
  public void TestClassFieldsAndMethods()
  {
    AssertSame(@"
    class Foo {
      int x
      int y

      func int sum() {
        return this.x + this.y
      }
    }

    func int test(int k) {
      Foo f = new Foo
      f.x = k
      f.y = k * 2
      return f.sum()
    }
    ", "test", 5);
  }

  [Fact]
  public void TestArrayLiteralIndexAndCount()
  {
    AssertSame(@"
    func int test(int k) {
      []int a = [1,2,3]
      a.Add(k)
      int s = 0
      int i = 0
      while(i < a.Count) {
        s = s * 10 + a[i]
        i = i + 1
      }
      return s
    }
    ", "test", 9);
  }

  [Fact]
  public void TestArrayOfClasses()
  {
    AssertSame(@"
    class Item {
      int n
    }

    func int test(int k) {
      []Item items = []
      int i = 0
      while(i < 3) {
        Item it = new Item
        it.n = i + k
        items.Add(it)
        i = i + 1
      }

      int s = 0
      int j = 0
      while(j < items.Count) {
        s = s + items[j].n
        j = j + 1
      }
      return s
    }
    ", "test", 10);
  }

  [Fact]
  public void TestLambdaCapturingALocal()
  {
    AssertSame(@"
    func int apply(func int(int) fn, int v) {
      return fn(v)
    }

    func int twice(int x) { return x * 2 }

    func int test(int k) {
      func int(int) f = func int(int a) { return a + k }
      return apply(f, 1) + apply(twice, 3)
    }
    ", "test", 10);
  }

  [Fact]
  public void TestFuncPtrStoredAndCalled()
  {
    AssertSame(@"
    func int add(int a, int b) { return a + b }

    func int test(int k) {
      func int(int,int) f = add
      int s = f(k, 1)
      func int(int) g = func int(int x) { return f(x, x) }
      return s + g(k)
    }
    ", "test", 7);
  }

  [Fact]
  public void TestInheritanceAndVirtualDispatch()
  {
    AssertSame(@"
    class Base {
      int a
      virtual func int get() { return this.a }
    }

    class Child : Base {
      int b
      override func int get() { return this.a + this.b }
    }

    func int ask(Base x) { return x.get() }

    func int test(int k) {
      Child c = new Child
      c.a = k
      c.b = 100
      Base b = new Base
      b.a = 1
      return ask(c) + ask(b)
    }
    ", "test", 5);
  }

  [Fact]
  public void TestInterfaceDispatch()
  {
    AssertSame(@"
    interface IThing {
      func int val()
    }

    class Thing : IThing {
      int n
      func int val() { return this.n }
    }

    func int ask(IThing t) { return t.val() }

    func int test(int k) {
      Thing t = new Thing
      t.n = k
      return ask(t)
    }
    ", "test", 42);
  }

  [Fact]
  public void TestMapLiteralAndLookup()
  {
    AssertSame(@"
    func int test(int k) {
      [string]int m = []
      m[""a""] = 1
      m[""b""] = 2
      m[""c""] = k
      return m[""a""] * 100 + m[""b""] * 10 + m[""c""]
    }
    ", "test", 7);
  }

  //NOTE: what the slice refuses, so the boundary is explicit rather than a mystery crash
  //NOTE: coro scripts are compared by what they trace, the way the VM tests assert
  void AssertSameTrace(string bhl, string func_name)
  {
    //NOTE: the AST has to be built with the same bindings the VM gets, or unresolved calls
    //      simply aren't in it
    var parse_ts = new Types();
    BindTrace(parse_ts, new StringBuilder());
    var res = Parse(bhl, parse_ts).result;
    var ast = res.ast;
    var ns = res.module.ns;

    var vm_log = new StringBuilder();
    var vm = MakeVM(bhl, ts => BindTrace(ts, vm_log));
    Execute(vm, func_name);
    CommonChecks(vm);

    var src = CsEmitter.Emit(ast, "Gen", ns);
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
    func test() {
      int a = 1
      int b = typeof(int) == typeof(int) ? a : 0
    }
    ", new Types()).result.ast;

    var e = Assert.Throws<CsEmitter.NotSupported>(() => CsEmitter.Emit(ast));
    Assert.Contains("typeof", e.Message);
  }
}
