using System.Collections.Generic;
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
