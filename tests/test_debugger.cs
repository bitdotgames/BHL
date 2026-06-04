using System.Collections.Generic;
using System.IO;
using System.Linq;
using bhl;
using Xunit;

public class TestDebugger : BHL_TestBase
{
  VMDebugger MakeDebugger(VM vm)
  {
    var d = new VMDebugger();
    vm.debugger = d;
    return d;
  }

  [Fact]
  public void TestBreakpointIsHit()
  {
    string bhl = @"
func int foo() {
  int x = 10
  return x
}
";
    var vm = MakeVM(bhl);
    var d = MakeDebugger(vm);

    bool hit = false;
    d.OnBreakpoint = _ => { hit = true; };
    d.AddBreakpoint(vm.FindModule(TestModuleName), line: 3);

    Execute(vm, "foo");

    Assert.True(hit);
  }

  [Fact]
  public void TestBreakpointNotHitWhenNoneSet()
  {
    string bhl = @"
func int foo() {
  int x = 10
  return x
}
";
    var vm = MakeVM(bhl);
    var d = MakeDebugger(vm);

    bool hit = false;
    d.OnBreakpoint = _ => { hit = true; };

    Execute(vm, "foo");

    Assert.False(hit);
  }

  [Fact]
  public void TestBreakpointHitsCorrectLine()
  {
    string bhl = @"
func void foo() {
  int x = 1
  int y = 2
  int z = 3
}
";
    var vm = MakeVM(bhl);
    var d = MakeDebugger(vm);

    var hit_lines = new List<int>();
    d.OnBreakpoint = b => { hit_lines.Add(b.line); };
    d.AddBreakpoint(vm.FindModule(TestModuleName), line: 4);

    Execute(vm, "foo");

    Assert.Single(hit_lines);
    Assert.Equal(4, hit_lines[0]);
  }

  [Fact]
  public void TestBreakpointHitCountInLoop()
  {
    string bhl = @"
func void foo() {
  for(int i = 0; i < 5; i++) {
    int x = i
  }
}
";
    var vm = MakeVM(bhl);
    var d = MakeDebugger(vm);

    int hits = 0;
    d.OnBreakpoint = _ => { hits++; };
    d.AddBreakpoint(vm.FindModule(TestModuleName), line: 4);

    Execute(vm, "foo");

    Assert.Equal(5, hits);
  }

  [Fact]
  public void TestMultipleBreakpoints()
  {
    string bhl = @"
func void foo() {
  int x = 1
  int y = 2
  int z = 3
}
";
    var vm = MakeVM(bhl);
    var d = MakeDebugger(vm);
    var module = vm.FindModule(TestModuleName);

    var hit_lines = new List<int>();
    d.OnBreakpoint = b => { hit_lines.Add(b.line); };
    d.AddBreakpoint(module, line: 3);
    d.AddBreakpoint(module, line: 5);

    Execute(vm, "foo");

    Assert.Equal(2, hit_lines.Count);
    Assert.Contains(3, hit_lines);
    Assert.Contains(5, hit_lines);
  }

  [Fact]
  public void TestBreakpointInCalledFunction()
  {
    string bhl = @"
func int bar(int n) {
  int result = n * 2
  return result
}

func int foo() {
  return bar(21)
}
";
    var vm = MakeVM(bhl);
    var d = MakeDebugger(vm);

    bool hit = false;
    d.OnBreakpoint = _ => { hit = true; };
    d.AddBreakpoint(vm.FindModule(TestModuleName), line: 3);

    var fb = Execute(vm, "foo");
    Assert.Equal(42, (int)fb.Stack.Pop().num);
    Assert.True(hit);
  }

  [Fact]
  public async System.Threading.Tasks.Task TestBreakpointDoesNotFireInOtherModule()
  {
    // Both modules have code that compiles to identical local IP offsets.
    // The breakpoint is set only in bhl1; running bhl2's function must not trigger it.
    string bhl1 = @"
import ""bhl2""

func int foo() {
  int result = bar()
  return result
}
";
    string bhl2 = @"
func int bar() {
  int result = 42
  return result
}
";
    var vm = await MakeVM(new System.Collections.Generic.Dictionary<string, string>()
    {
      {"bhl1.bhl", bhl1},
      {"bhl2.bhl", bhl2},
    });
    vm.LoadModule("bhl1");

    var d = MakeDebugger(vm);
    var hit_modules = new System.Collections.Generic.List<string>();
    d.OnBreakpoint = b => hit_modules.Add(b.module.name);

    // Set breakpoint on 'int result = bar()' in bhl1 — same bytecode shape as
    // 'int result = 42' in bhl2, so their local IP offsets coincide.
    d.AddBreakpoint(vm.FindModule("bhl1"), line: 5);

    Execute(vm, "foo");

    Assert.Single(hit_modules);
    Assert.Equal("bhl1", hit_modules[0]);
  }

  [Fact]
  public void TestBreakpointReportsModule()
  {
    string bhl = @"
func void foo() {
  int x = 1
}
";
    var vm = MakeVM(bhl);
    var d = MakeDebugger(vm);
    var module = vm.FindModule(TestModuleName);

    Module hit_module = null;
    d.OnBreakpoint = b => { hit_module = b.module; };
    d.AddBreakpoint(module, line: 3);

    Execute(vm, "foo");

    Assert.NotNull(hit_module);
    Assert.Equal(TestModuleName, hit_module.name);
  }

  [Fact]
  public void TestBreakpointOnNonExistentLineReturnsFalse()
  {
    string bhl = @"
func void foo() {
  int x = 1
}
";
    var vm = MakeVM(bhl);
    var d = MakeDebugger(vm);

    bool added = d.AddBreakpoint(vm.FindModule(TestModuleName), line: 999);

    Assert.False(added);
  }

  [Fact]
  public void TestStepOver()
  {
    string bhl = @"
func void foo() {
  int x = 1
  int y = 2
  int z = 3
}
";
    var vm = MakeVM(bhl);
    var d  = MakeDebugger(vm);
    var module = vm.FindModule(TestModuleName);

    var hit_lines = new List<int>();
    d.OnBreakpoint = b => {
      hit_lines.Add(b.line);
      d.StartStep(VMDebugger.StepMode.Over, b.exec, b.ip);
    };
    d.AddBreakpoint(module, line: 3);

    Execute(vm, "foo");

    Assert.Equal(new[] { 3, 4, 5, 6 }, hit_lines);
  }

  [Fact]
  public void TestStepOverSkipsCallees()
  {
    string bhl = @"
func int bar(int n) {
  int r = n * 2
  return r
}

func int foo() {
  int a = bar(1)
  int b = bar(2)
  return a + b
}
";
    var vm = MakeVM(bhl);
    var d  = MakeDebugger(vm);
    var module = vm.FindModule(TestModuleName);

    var hit_lines = new List<int>();
    d.OnBreakpoint = b => {
      hit_lines.Add(b.line);
      d.StartStep(VMDebugger.StepMode.Over, b.exec, b.ip);
    };
    d.AddBreakpoint(module, line: 8);

    var fb = Execute(vm, "foo");
    Assert.Equal(6, (int)fb.Stack.Pop().num);
    // step over: stays in foo, doesn't enter bar
    Assert.Equal(new[] { 8, 9, 10 }, hit_lines);
  }

  [Fact]
  public void TestStepInto()
  {
    string bhl = @"
func int bar(int n) {
  return n * 2
}

func int foo() {
  int a = bar(3)
  return a
}
";
    var vm = MakeVM(bhl);
    var d  = MakeDebugger(vm);
    var module = vm.FindModule(TestModuleName);

    var hit_lines = new List<int>();
    d.OnBreakpoint = b => {
      hit_lines.Add(b.line);
      d.StartStep(VMDebugger.StepMode.Into, b.exec, b.ip);
    };
    d.AddBreakpoint(module, line: 7);

    var fb = Execute(vm, "foo");
    Assert.Equal(6, (int)fb.Stack.Pop().num);
    // step into: enters bar on line 3
    Assert.Contains(3, hit_lines);
  }

  [Fact]
  public void TestStepOut()
  {
    string bhl = @"
func int bar(int n) {
  int r = n * 2
  return r
}

func int foo() {
  int a = bar(5)
  return a
}
";
    var vm = MakeVM(bhl);
    var d  = MakeDebugger(vm);
    var module = vm.FindModule(TestModuleName);

    var hit_lines = new List<int>();
    d.OnBreakpoint = b => {
      hit_lines.Add(b.line);
      if(b.reason == "breakpoint")
        d.StartStep(VMDebugger.StepMode.Out, b.exec, b.ip);
    };
    d.AddBreakpoint(module, line: 3);

    var fb = Execute(vm, "foo");
    Assert.Equal(10, (int)fb.Stack.Pop().num);
    // stepped out of bar back into foo
    Assert.Equal(3, hit_lines[0]);
    Assert.True(hit_lines.Count > 1);
    Assert.True(hit_lines[1] > 5); // back in foo
  }

  [Fact]
  public void TestStepReasonIsStep()
  {
    string bhl = @"
func void foo() {
  int x = 1
  int y = 2
}
";
    var vm = MakeVM(bhl);
    var d  = MakeDebugger(vm);

    var reasons = new List<string>();
    d.OnBreakpoint = b => {
      reasons.Add(b.reason);
      d.StartStep(VMDebugger.StepMode.Over, b.exec, b.ip);
    };
    d.AddBreakpoint(vm.FindModule(TestModuleName), line: 3);

    Execute(vm, "foo");

    Assert.Equal("breakpoint", reasons[0]);
    Assert.All(reasons.Skip(1), r => Assert.Equal("step", r));
  }

  [Fact]
  public void TestBreakpointDuringStepClearsStep()
  {
    string bhl = @"
func void foo() {
  int x = 1
  int y = 2
  int z = 3
}
";
    var vm = MakeVM(bhl);
    var d  = MakeDebugger(vm);
    var module = vm.FindModule(TestModuleName);

    var hits = new List<(int line, string reason)>();
    d.OnBreakpoint = b => {
      hits.Add((b.line, b.reason));
      if(b.reason == "breakpoint" && b.line == 3)
        d.StartStep(VMDebugger.StepMode.Over, b.exec, b.ip);
      // don't continue stepping after the second breakpoint
    };
    d.AddBreakpoint(module, line: 3);
    d.AddBreakpoint(module, line: 4); // planted mid-step

    Execute(vm, "foo");

    // step stops early because line 4 is a breakpoint → reason = "breakpoint", step cleared
    Assert.Equal(2, hits.Count);
    Assert.Equal((3, "breakpoint"), hits[0]);
    Assert.Equal((4, "breakpoint"), hits[1]);
  }

  [Fact]
  public void TestLocalVarTableNullWithoutFlag()
  {
    string bhl = @"
func void foo(int a) {
  int x = 1
}
";
    var m = Compile(bhl);
    Assert.Null(m.compiled.local_var_table);
  }

  [Fact]
  public void TestLocalVarTableFuncArgs()
  {
    string bhl = @"
func void foo(int a, int b) {
}
";
    var m = Compile(bhl, add_debug_info: true);
    var fs = m.ns.Resolve("foo") as FuncSymbolScript;
    var t = m.compiled.local_var_table;

    Assert.NotNull(t);
    Assert.Equal("a", t.TryGet(fs.ip_addr, 0));
    Assert.Equal("b", t.TryGet(fs.ip_addr, 1));
  }

  [Fact]
  public void TestLocalVarTableBodyLocals()
  {
    string bhl = @"
func void foo(int a) {
  int x = 1
  float y = 2.0
}
";
    var m = Compile(bhl, add_debug_info: true);
    var fs = m.ns.Resolve("foo") as FuncSymbolScript;
    var t = m.compiled.local_var_table;

    Assert.Equal("a", t.TryGet(fs.ip_addr, 0));
    Assert.Equal("x", t.TryGet(fs.ip_addr, 1));
    Assert.Equal("y", t.TryGet(fs.ip_addr, 2));
  }

  [Fact]
  public void TestLocalVarTableMultipleFunctions()
  {
    string bhl = @"
func void foo(int a) {
  int x = 1
}

func void bar(float b) {
  float y = 2.0
}
";
    var m = Compile(bhl, add_debug_info: true);
    var foo = m.ns.Resolve("foo") as FuncSymbolScript;
    var bar = m.ns.Resolve("bar") as FuncSymbolScript;
    var t = m.compiled.local_var_table;

    Assert.Equal("a", t.TryGet(foo.ip_addr, 0));
    Assert.Equal("x", t.TryGet(foo.ip_addr, 1));
    Assert.Equal("b", t.TryGet(bar.ip_addr, 0));
    Assert.Equal("y", t.TryGet(bar.ip_addr, 1));
    // no cross-contamination
    Assert.Null(t.TryGet(foo.ip_addr, 2));
    Assert.Null(t.TryGet(bar.ip_addr, 2));
  }

  [Fact]
  public void TestLocalVarTableRoundTrip()
  {
    string bhl = @"
func int foo(int a, int b) {
  int result = a + b
  return result
}
";
    var m = Compile(bhl, add_debug_info: true);
    var fs = m.ns.Resolve("foo") as FuncSymbolScript;
    int func_ip = fs.ip_addr;

    var ms = new MemoryStream();
    m.ToStream(ms, leave_open: true);
    ms.Position = 0;

    var ts = new Types();
    var reloaded = ModuleDeclared.FromStream(ts, ms);
    var t = reloaded.compiled.local_var_table;

    Assert.NotNull(t);
    Assert.Equal("a",      t.TryGet(func_ip, 0));
    Assert.Equal("b",      t.TryGet(func_ip, 1));
    Assert.Equal("result", t.TryGet(func_ip, 2));
  }

  [Fact]
  public void TestLocalVarTableRoundTripWithoutDebugInfo()
  {
    string bhl = @"
func int foo(int a) {
  return a
}
";
    var m = Compile(bhl, add_debug_info: false);

    var ms = new MemoryStream();
    m.ToStream(ms, leave_open: true);
    ms.Position = 0;

    var ts = new Types();
    var reloaded = ModuleDeclared.FromStream(ts, ms);

    Assert.Null(reloaded.compiled.local_var_table);
  }

  [Fact]
  public void TestRemoveBreakpoint()
  {
    string bhl = @"
func void foo() {
  int x = 1
  int y = 2
}
";
    var vm = MakeVM(bhl);
    var d = MakeDebugger(vm);
    var module = vm.FindModule(TestModuleName);

    // find the ip for line 3 so we can remove it
    int ip = module.decl.compiled.ip2src_line.FindIpForLine(3);
    d.AddBreakpoint(ip);

    bool hit = false;
    d.OnBreakpoint = _ => { hit = true; };
    d.RemoveBreakpoint(ip);

    Execute(vm, "foo");

    Assert.False(hit);
  }
}
