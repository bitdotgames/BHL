using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using bhl;

public class TestNamespace : BHL_TestBase
{
  [IsTested()]
  public void TestSimpleLink()
  {
    var ts = new Types();

    var ns1 = new Namespace(ts);
    {
      var foo = new Namespace(ts, "foo");
      ns1.Define(foo);
    }

    var ns2 = new Namespace(ts);

    /*
    {
      foo {
      }
    }
    */
    ns2.Link(ns1);

    {
      var foo = ns2.Resolve("foo") as Namespace;
      AssertTrue(foo != null);
      AssertEqual(0, foo.GetMembers().Count);
    }
  }

  [IsTested()]
  public void TestDeepLink()
  {
    var ts = new Types();

    /*
    {
      foo {
        foo_sub {
          class Wow {}
        }
      }

      wow {
      }
    }
    */
    var ns1 = new Namespace(ts);
    {
      var foo = new Namespace(ts, "foo");

      var foo_sub = new Namespace(ts, "foo_sub");

      var cl = new ClassSymbolNative("Wow", null);
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns1.Define(foo);

      var wow = new Namespace(ts, "wow");
      ns1.Define(wow);
    }

    /*
    {
      foo {
        foo_sub {
          class Hey {}
        }
      }

      bar {
      }
    }
    */
    var ns2 = new Namespace(ts);
    {
      var foo = new Namespace(ts, "foo");

      var foo_sub = new Namespace(ts, "foo_sub");

      var cl = new ClassSymbolNative("Hey", null);
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns2.Define(foo);

      var bar = new Namespace(ts, "bar");
      ns2.Define(bar);
    }

    ns2.Link(ns1);

    /*
    {
      foo {
        foo_sub {
          class Wow {}

          class Hey {}
        }
      }

      wow {
      }

      bar {
      }
    }
    */
    {
      var foo = ns2.Resolve("foo") as Namespace;
      AssertTrue(foo != null);
      AssertEqual(1, foo.GetMembers().Count);

      var foo_sub = foo.Resolve("foo_sub") as Namespace;
      AssertTrue(foo_sub != null);
      AssertEqual(2, foo_sub.GetMembers().Count);

      var cl_wow = foo_sub.Resolve("Wow") as ClassSymbol;
      AssertTrue(cl_wow != null);

      var cl_hey = foo_sub.Resolve("Hey") as ClassSymbol;
      AssertTrue(cl_hey != null);

      var bar = ns2.Resolve("bar") as Namespace;
      AssertTrue(bar != null);
      AssertEqual(0, bar.GetMembers().Count);

      var wow = ns2.Resolve("wow") as Namespace;
      AssertTrue(wow != null);
      AssertEqual(0, wow.GetMembers().Count);
    }

    AssertEqual("foo", ns2.ResolveFullName("foo").name);
    AssertEqual("foo_sub", ns2.ResolveFullName("foo.foo_sub").name);
    AssertEqual("Hey", ns2.ResolveFullName("foo.foo_sub.Hey").name);
    AssertEqual("Wow", ns2.ResolveFullName("foo.foo_sub.Wow").name);
    AssertEqual("wow", ns2.ResolveFullName("wow").name);
    AssertEqual("bar", ns2.ResolveFullName("bar").name);

    AssertTrue(ns2.ResolveFullName("") == null);
    AssertTrue(ns2.ResolveFullName(".") == null);
    AssertTrue(ns2.ResolveFullName("foo.") == null);
    AssertTrue(ns2.ResolveFullName(".foo.") == null);
    AssertTrue(ns2.ResolveFullName("foo..") == null);
    AssertTrue(ns2.ResolveFullName("foo.bar") == null);
    AssertTrue(ns2.ResolveFullName(".foo.foo_sub..") == null);
  }

  [IsTested()]
  public void TestUnlink()
  {
    var ts = new Types();

    /*
    {
      foo {
        foo_sub {
          class Wow {}
        }
      }

      wow {
      }
    }
    */
    var ns1 = new Namespace(ts);
    {
      var foo = new Namespace(ts, "foo");

      var foo_sub = new Namespace(ts, "foo_sub");

      var cl = new ClassSymbolNative("Wow", null);
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns1.Define(foo);

      var wow = new Namespace(ts, "wow");
      ns1.Define(wow);
    }

    /*
    {
      foo {
        foo_sub {
          class Hey {}
        }
      }

      bar {
      }
    }
    */
    var ns2 = new Namespace(ts);
    {
      var foo = new Namespace(ts, "foo");

      var foo_sub = new Namespace(ts, "foo_sub");

      var cl = new ClassSymbolNative("Hey", null);
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns2.Define(foo);

      var bar = new Namespace(ts, "bar");
      ns2.Define(bar);
    }

    ns2.Link(ns1);

    /*
    {
      foo {
        foo_sub {
          class Wow {}

          class Hey {}
        }
      }

      wow {
      }

      bar {
      }
    }
    */

    ns2.Unlink(ns1);

    /*
    {
      foo {
        foo_sub {
          class Hey {}
        }
      }

      bar {
      }
    }
    */

    {
      var foo = ns2.Resolve("foo") as Namespace;
      AssertTrue(foo != null);
      AssertEqual(1, foo.GetMembers().Count);

      var foo_sub = foo.Resolve("foo_sub") as Namespace;
      AssertTrue(foo_sub != null);
      AssertEqual(1, foo_sub.GetMembers().Count);

      var cl_wow = foo_sub.Resolve("Wow") as ClassSymbol;
      AssertTrue(cl_wow == null);

      var cl_hey = foo_sub.Resolve("Hey") as ClassSymbol;
      AssertTrue(cl_hey != null);

      var bar = ns2.Resolve("bar") as Namespace;
      AssertTrue(bar != null);

      var wow = ns2.Resolve("wow") as Namespace;
      AssertTrue(wow == null);
    }
  }

  [IsTested()]
  public void TestUnlinkPreserveChangedStuff()
  {
    var ts = new Types();

    /*
    {
      foo {
        foo_sub {
          class Wow {}
        }
      }

      wow {
      }
    }
    */
    var ns1 = new Namespace(ts);
    {
      var foo = new Namespace(ts, "foo");

      var foo_sub = new Namespace(ts, "foo_sub");

      var cl = new ClassSymbolNative("Wow", null);
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns1.Define(foo);

      var wow = new Namespace(ts, "wow");
      ns1.Define(wow);
    }

    /*
    {
      foo {
        foo_sub {
          class Hey {}
        }
      }

      bar {
      }
    }
    */
    var ns2 = new Namespace(ts);
    {
      var foo = new Namespace(ts, "foo");

      var foo_sub = new Namespace(ts, "foo_sub");

      var cl = new ClassSymbolNative("Hey", null);
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns2.Define(foo);

      var bar = new Namespace(ts, "bar");
      ns2.Define(bar);
    }

    ns2.Link(ns1);

    /*
    {
      foo {
        foo_sub {
          class Wow {}

          class Hey {}
        }
      }

      wow {
      }

      bar {
      }
    }
    */

    (ns2.Resolve("wow") as Namespace).Define(new Namespace(ts, "wow_sub"));
    ns2.Unlink(ns1);

    /*
    {
      foo {
        foo_sub {
          class Hey {}
        }
      }

      wow {
        wow_sub {
        }
      }

      bar {
      }
    }
    */

    {
      var foo = ns2.Resolve("foo") as Namespace;
      AssertTrue(foo != null);
      AssertEqual(1, foo.GetMembers().Count);

      var foo_sub = foo.Resolve("foo_sub") as Namespace;
      AssertTrue(foo_sub != null);
      AssertEqual(1, foo_sub.GetMembers().Count);

      var cl_wow = foo_sub.Resolve("Wow") as ClassSymbol;
      AssertTrue(cl_wow == null);

      var cl_hey = foo_sub.Resolve("Hey") as ClassSymbol;
      AssertTrue(cl_hey != null);

      var bar = ns2.Resolve("bar") as Namespace;
      AssertTrue(bar != null);

      var wow = ns2.Resolve("wow") as Namespace;
      AssertTrue(wow != null);

      AssertTrue(wow.Resolve("wow_sub") is Namespace);
    }
  }

  [IsTested()]
  public void TestLinkConflict()
  {
    var ts = new Types();

    /*
    {
      foo {
        foo_sub {
          class Wow {}
        }
      }

      wow {
      }
    }
    */
    var ns1 = new Namespace(ts);
    {
      var foo = new Namespace(ts, "foo");

      var foo_sub = new Namespace(ts, "foo_sub");

      var cl = new ClassSymbolNative("Wow", null);
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns1.Define(foo);

      var wow = new Namespace(ts, "wow");
      ns1.Define(wow);
    }

    /*
    {
      foo {
        foo_sub {
          class Wow {}
        }
      }
    }
    */
    var ns2 = new Namespace(ts);
    {
      var foo = new Namespace(ts, "foo");

      var foo_sub = new Namespace(ts, "foo_sub");

      var cl = new ClassSymbolNative("Wow", null);
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns2.Define(foo);
    }

    var conflict_symb = ns2.TryLink(ns1);
    AssertEqual("Wow", conflict_symb.name);

  }

  [IsTested()]
  public void TestSimpleDecl()
  {
    string bhl = @"
    namespace foo {
      func bool test()
      {
        return true
      }
    }

    namespace bar {
      func bool test()
      {
        return false
      }
    }
    ";

    var vm = MakeVM(bhl);

    var foo = vm.Types.ns.Resolve("foo") as Namespace;
    AssertTrue(foo != null);
    AssertEqual(1, foo.GetMembers().Count);
    AssertTrue(foo.Resolve("test") is FuncSymbol);

    var bar = vm.Types.ns.Resolve("bar") as Namespace;
    AssertTrue(bar != null);
    AssertEqual(1, bar.GetMembers().Count);
    AssertTrue(foo.Resolve("test") is FuncSymbol);
  }

  [IsTested()]
  public void TestPartialDecls()
  {
    string bhl = @"
    namespace foo {
      func bool test()
      {
        return true
      }
    }

    namespace bar {
      func bool test()
      {
        return false
      }
    }

    namespace foo {
      func bool what()
      {
        return false
      }
    }
    ";

    var vm = MakeVM(bhl);

    var foo = vm.Types.ns.Resolve("foo") as Namespace;
    AssertTrue(foo != null);
    AssertEqual(2, foo.GetMembers().Count);
    AssertTrue(foo.Resolve("test") is FuncSymbol);
    AssertTrue(foo.Resolve("what") is FuncSymbol);

    var bar = vm.Types.ns.Resolve("bar") as Namespace;
    AssertTrue(bar != null);
    AssertEqual(1, bar.GetMembers().Count);
    AssertTrue(bar.Resolve("test") is FuncSymbol);
  }

  [IsTested()]
  public void TestSubNamespaces()
  {
    string bhl = @"
    namespace foo {
      func bool test()
      {
        return true
      }
    }

    namespace bar {
      func bool test()
      {
        return false
      }

      namespace foo {
        func bool test()
        {
          return true
        }
      }
    }

    namespace foo {
      func bool what()
      {
        return false
      }

      namespace bar {
        func bool test()
        {
          return true
        }
      }
    }
    ";

    var vm = MakeVM(bhl);

    var foo = vm.Types.ns.Resolve("foo") as Namespace;
    AssertTrue(foo != null);
    AssertEqual(3, foo.GetMembers().Count);
    AssertTrue(foo.Resolve("test") is FuncSymbol);
    AssertTrue(foo.Resolve("what") is FuncSymbol);
    AssertTrue(foo.Resolve("bar") is Namespace);

    var bar = vm.Types.ns.Resolve("bar") as Namespace;
    AssertTrue(bar != null);
    AssertEqual(2, bar.GetMembers().Count);
    AssertTrue(bar.Resolve("test") is FuncSymbol);
    AssertTrue(bar.Resolve("foo") is Namespace);
  }

  [IsTested()]
  public void TestStartFuncByPath()
  {
    string bhl = @"
    namespace foo {
      func bool test()
      {
        return true
      }
    }

    namespace bar {
      func bool test()
      {
        return false
      }
    }
    ";
    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "foo.test").result.PopRelease().num);
    AssertEqual(0, Execute(vm, "bar.test").result.PopRelease().num);
  }

  [IsTested()]
  public void TestCallFuncByPath()
  {
    {
      string bhl = @"
      namespace foo {
        func bool test() {
          return true
        }
      }

      namespace bar {
        func bool test() {
          return foo.test()
        }
      }
      ";
      var vm = MakeVM(bhl);
      AssertEqual(1, Execute(vm, "bar.test").result.PopRelease().num);
    }

    {
      string bhl = @"
      namespace foo {
        func int wow() {
          return 1
        }
      }

      namespace bar {
        func int wow() {
          return 10
        }

        func int test() {
          return foo.wow() + wow()
        }
      }
      ";
      var vm = MakeVM(bhl);
      AssertEqual(11, Execute(vm, "bar.test").result.PopRelease().num);
    }
  }

  [IsTested()]
  public void TestCallNativeFuncByPath()
  {
    {
      string bhl = @"
      namespace bar {
        func int test() {
          return foo.wow() + wow()
        }
      }
      ";
      var ts = new Types();
      var ns = new Namespace(ts);
      {
        var foo = new Namespace(ts, "foo");
        var fn = new FuncSymbolNative("wow", ts.Type("int"),
            delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
              frm.stack.Push(Val.NewInt(frm.vm, 1)); 
              return null;
            }
        );
        foo.Define(fn);
        ns.Define(foo);
      }
      {
        var bar = new Namespace(ts, "bar");
        var fn = new FuncSymbolNative("wow", ts.Type("int"),
            delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
              frm.stack.Push(Val.NewInt(frm.vm, 10)); 
              return null;
            }
        );
        bar.Define(fn);
        ns.Define(bar);
      }
      ts.ns.Link(ns);

      var vm = MakeVM(bhl, ts);
      AssertEqual(11, Execute(vm, "bar.test").result.PopRelease().num);
    }
  }

  //TODO: this is quite contraversary
  [IsTested()]
  public void TestPreferLocalVersion()
  {
    string bhl = @"
    func int bar() { 
      return 1
    }
    namespace foo {
      func int bar() {
        return 10
      }

      func int test() {
        return bar()
      }
    }
    ";
    var vm = MakeVM(bhl);
    AssertEqual(10, Execute(vm, "foo.test").result.PopRelease().num);
  }

  [IsTested()]
  public void TestCallGlobalVersion()
  {
    string bhl = @"
    func int bar() { 
      return 1
    }
    namespace foo {
      func int bar() {
        return 10
      }

      func int test() {
        return .bar()
      }
    }
    ";
    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "foo.test").result.PopRelease().num);
  }

  [IsTested()]
  public void TestUserClasses()
  {
    string bhl = @"

    namespace bar {
      class Bar {
        float a
      }
    }
      
    namespace foo {
      namespace sub_foo {
        class Foo : bar.Bar { 
          float b
          int c
        }
      }
    }

    func float test() 
    {
      foo.sub_foo.Foo f = { c : 2, b : 101.5, a : 1 }
      bar.Bar b = { a : 10 }
      f.b = f.b + f.c + b.a + f.a
      return f.b
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(114.5, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserInterface()
  {
    string bhl = @"
    namespace bar {
      interface IFoo {
        func int test()
      }
    }

    namespace foo {
      namespace sub_foo {
        class Foo : bar.IFoo { 
          func int test() {
            return 42
          }
        }
      }
    }

    func int test() 
    {
      foo.sub_foo.Foo f = {}
      return f.test()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportUserFunc()
  {
    string bhl1 = @"
    namespace foo {
      func int Foo() {
        return 10
      }
    }
    ";
      
  string bhl2 = @"
    import ""bhl1""  

    func int Foo() {
      return 1
    }

    func int test() 
    {
      return foo.Foo() + Foo()
    }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);

    var ts = new Types();
    var loader = new ModuleLoader(ts, CompileFiles(files, ts));
    var vm = new VM(ts, loader);

    vm.LoadModule("bhl2");
    AssertEqual(11, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportUserFuncPtr()
  {
    string bhl1 = @"
    namespace foo {
      func int Foo() {
        return 10
      }
    }
    ";
      
  string bhl2 = @"
    import ""bhl1""  

    func int Foo() {
      return 1
    }

    func int test() 
    {
      func int() p1 = foo.Foo
      func int() p2 = Foo
      return p1() + p2()
    }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);

    var ts = new Types();
    var loader = new ModuleLoader(ts, CompileFiles(files, ts));
    var vm = new VM(ts, loader);

    vm.LoadModule("bhl2");
    AssertEqual(11, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportUserClass()
  {
    string bhl1 = @"
    namespace foo {
      class Foo { 
        int Int
        float Flt
        string Str
      }
    }
    ";
      
  string bhl2 = @"
    import ""bhl1""  
    func int test() 
    {
      foo.Foo f = {}
      f.Int = 10
      return f.Int
    }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);

    var ts = new Types();
    var loader = new ModuleLoader(ts, CompileFiles(files, ts));
    var vm = new VM(ts, loader);

    vm.LoadModule("bhl2");
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportGlobalObjectVar()
  {
    string bhl1 = @"
    import ""bhl3""  
    func float test() {
      return bar.foo.x
    }
    ";

    string bhl2 = @"
    namespace foo {
      class Foo {
        float x
      }
    }
    ";

    string bhl3 = @"
    import ""bhl2""  
    namespace bar {
      foo.Foo foo = {x : 10}
    }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);
    NewTestFile("bhl3.bhl", bhl3, ref files);

    var ts = new Types();
    var loader = new ModuleLoader(ts, CompileFiles(files));

    var vm = new VM(ts, loader);

    vm.LoadModule("bhl1");
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }
}
