using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using bhl;

public class TestNamespace : BHL_TestBase
{
  [IsTested()]
  public void TestSimpleLink()
  {
    var m = new Module(new Types());

    var ns1 = new Namespace(m);
    {
      var foo = new Namespace(m, "foo");
      ns1.Define(foo);
    }

    var ns2 = new Namespace(m);

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
      AssertEqual(0, foo.Count());
    }
  }

  [IsTested()]
  public void TestNestedLink()
  {
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
    var m = new Module(new Types());

    var ns1 = new Namespace(m);
    {
      var foo = new Namespace(m, "foo");

      var foo_sub = new Namespace(m, "foo_sub");

      var cl = new ClassSymbolNative(new Origin(), "Wow");
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns1.Define(foo);

      var wow = new Namespace(m, "wow");
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
    var ns2 = new Namespace(m);
    {
      var foo = new Namespace(m, "foo");

      var foo_sub = new Namespace(m, "foo_sub");

      var cl = new ClassSymbolNative(new Origin(), "Hey");
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns2.Define(foo);

      var bar = new Namespace(m, "bar");
      ns2.Define(bar);
    }

    ns2.Link(ns1);

    /*
    {
      foo {
        [foo]
        foo_sub {
         [foo_sub { Wow {} }]

          class Hey {}
        }
      }

      bar {
      }
      
      [ns1 { wow { } }]
    }
    */
    {
      var foo = ns2.Resolve("foo") as Namespace;
      AssertTrue(foo != null);
      AssertEqual(1, foo.Count());

      var foo_sub = foo.Resolve("foo_sub") as Namespace;
      AssertTrue(foo_sub != null);
      AssertEqual(2, foo_sub.Count());

      var cl_wow = foo_sub.Resolve("Wow") as ClassSymbol;
      AssertTrue(cl_wow != null);

      var cl_hey = foo_sub.Resolve("Hey") as ClassSymbol;
      AssertTrue(cl_hey != null);

      var bar = ns2.Resolve("bar") as Namespace;
      AssertTrue(bar != null);
      AssertEqual(0, bar.Count());

      var wow = ns2.Resolve("wow") as Namespace;
      AssertTrue(wow != null);
      AssertEqual(0, wow.Count());
    }

    AssertEqual("foo", ns2.ResolveNamedByPath("foo").GetName());
    AssertEqual("foo_sub", ns2.ResolveNamedByPath("foo.foo_sub").GetName());
    AssertEqual("Hey", ns2.ResolveNamedByPath("foo.foo_sub.Hey").GetName());
    AssertEqual("Wow", ns2.ResolveNamedByPath("foo.foo_sub.Wow").GetName());
    AssertEqual("wow", ns2.ResolveNamedByPath("wow").GetName());
    AssertEqual("bar", ns2.ResolveNamedByPath("bar").GetName());

    AssertTrue(ns2.ResolveNamedByPath("") == null);
    AssertTrue(ns2.ResolveNamedByPath(".") == null);
    AssertTrue(ns2.ResolveNamedByPath("foo.") == null);
    AssertTrue(ns2.ResolveNamedByPath(".foo.") == null);
    AssertTrue(ns2.ResolveNamedByPath("foo..") == null);
    AssertTrue(ns2.ResolveNamedByPath("foo.bar") == null);
    AssertTrue(ns2.ResolveNamedByPath(".foo.foo_sub..") == null);
  }

  [IsTested()]
  public void TestLinkSeveralSimilarNamespaces()
  {
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
    var m = new Module(new Types());

    var ns1 = new Namespace(m);
    {
      var foo = new Namespace(m, "foo");

      var foo_sub = new Namespace(m, "foo_sub");

      var cl = new ClassSymbolNative(new Origin(), "Wow");
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns1.Define(foo);

      var wow = new Namespace(m, "wow");
      ns1.Define(wow);
    }

    /*
    {
      foo {
        foo_sub {
        }
      }

      bar {
      }
    }
    */
    var ns2 = new Namespace(m);
    {
      var foo = new Namespace(m, "foo");

      var foo_sub = new Namespace(m, "foo_sub");
      foo.Define(foo_sub);

      ns2.Define(foo);

      var bar = new Namespace(m, "bar");
      ns2.Define(bar);
    }

    ns2.Link(ns1);

    /*
    {
      foo {
        [foo]
        foo_sub {
         [foo_sub: { Wow ()}]
        }
      }

      bar {
      }
      
      [ ns1 { wow {} } ]
    }
    */
    {
      var foo = ns2.Resolve("foo") as Namespace;
      AssertTrue(foo != null);
      AssertEqual(1, foo.Count());

      var foo_sub = foo.Resolve("foo_sub") as Namespace;
      AssertTrue(foo_sub != null);
      AssertEqual(1, foo_sub.Count());

      var cl_wow = foo_sub.Resolve("Wow") as ClassSymbol;
      AssertTrue(cl_wow != null);

      var bar = ns2.Resolve("bar") as Namespace;
      AssertTrue(bar != null);
      AssertEqual(0, bar.Count());

      var wow = ns2.Resolve("wow") as Namespace;
      AssertTrue(wow != null);
      AssertEqual(0, wow.Count());
    }

    AssertEqual("foo", ns2.ResolveNamedByPath("foo").GetName());
    AssertEqual("foo_sub", ns2.ResolveNamedByPath("foo.foo_sub").GetName());
    AssertEqual("Wow", ns2.ResolveNamedByPath("foo.foo_sub.Wow").GetName());
    AssertEqual("wow", ns2.ResolveNamedByPath("wow").GetName());
    AssertEqual("bar", ns2.ResolveNamedByPath("bar").GetName());

    AssertTrue(ns2.ResolveNamedByPath("") == null);
    AssertTrue(ns2.ResolveNamedByPath(".") == null);
    AssertTrue(ns2.ResolveNamedByPath("foo.") == null);
    AssertTrue(ns2.ResolveNamedByPath(".foo.") == null);
    AssertTrue(ns2.ResolveNamedByPath("foo..") == null);
    AssertTrue(ns2.ResolveNamedByPath("foo.bar") == null);
    AssertTrue(ns2.ResolveNamedByPath(".foo.foo_sub..") == null);
  }
  
  [IsTested()]
  public void TestMultipleLink()
  {
    /*
    {
      foo {
        sub1 {
          class Wow {}
        }
      }
    }
    */
    var m = new Module(new Types(), "1");

    var ns1 = new Namespace(m);
    {
      var foo = new Namespace(m, "foo");

      var sub1 = new Namespace(m, "sub1");

      var cl = new ClassSymbolNative(new Origin(), "Wow");
      sub1.Define(cl);

      foo.Define(sub1);

      ns1.Define(foo);
    }

    /*
    {
      foo {
        sub2 {
          class Hey {}
        } 
      }
    }
    */
    var ns2 = new Namespace(m);
    {
      var foo = new Namespace(m, "foo");

      var sub2 = new Namespace(m, "sub2");

      var cl = new ClassSymbolNative(new Origin(), "Hey");
      sub2.Define(cl);

      foo.Define(sub2);

      ns2.Define(foo);
    }

    var ns3 = new Namespace(m);
    ns3.Link(ns2);
    ns3.Link(ns1);
    
    AssertEqual("foo", ns3.ResolveNamedByPath("foo").GetName());
    AssertEqual("sub2", ns3.ResolveNamedByPath("foo.sub2").GetName());
    AssertEqual("sub1", ns3.ResolveNamedByPath("foo.sub1").GetName());
    AssertEqual("Wow", ns3.ResolveNamedByPath("foo.sub1.Wow").GetName());
    AssertEqual("Hey", ns3.ResolveNamedByPath("foo.sub2.Hey").GetName());
  }
  
  [IsTested()]
  public void TestMultipleLink2()
  {
    /*
    {
      foo {
        sub1 {
          class Wow {}
        }
      }
    }
    */
    var m = new Module(new Types());

    var ns1 = new Namespace(m);
    {
      var foo = new Namespace(m, "foo");

      var sub1 = new Namespace(m, "sub1");

      var cl = new ClassSymbolNative(new Origin(), "Wow");
      sub1.Define(cl);

      foo.Define(sub1);

      ns1.Define(foo);
    }

    /*
    {
      foo {
        sub2 {
          class Hey {}
        } 
      }
    }
    */
    var ns2 = new Namespace(m);
    {
      var foo = new Namespace(m, "foo");

      var sub2 = new Namespace(m, "sub2");

      var cl = new ClassSymbolNative(new Origin(), "Hey");
      sub2.Define(cl);

      foo.Define(sub2);

      ns2.Define(foo);
    }
    
    /*
    {
      foo {
        sub2 {
          class Wow {}
        } 
      }
    }
    */
    var ns3 = new Namespace(m);
    {
      var foo = new Namespace(m, "foo");

      var sub2 = new Namespace(m, "sub2");

      var cl = new ClassSymbolNative(new Origin(), "Wow");
      sub2.Define(cl);

      foo.Define(sub2);

      ns3.Define(foo);
    }

    var ns4 = new Namespace(m);
    ns4.Link(ns2);
    ns4.Link(ns1);
    ns4.Link(ns3);
    
    AssertEqual("foo", ns4.ResolveNamedByPath("foo").GetName());
    AssertEqual("sub2", ns4.ResolveNamedByPath("foo.sub2").GetName());
    AssertEqual("sub1", ns4.ResolveNamedByPath("foo.sub1").GetName());
    AssertEqual("Wow", ns4.ResolveNamedByPath("foo.sub1.Wow").GetName());
    AssertEqual("Hey", ns4.ResolveNamedByPath("foo.sub2.Hey").GetName());
  }

  [IsTested()]
  public void TestLinkNamespaceWithOtherLinkedNamespace()
  {
    /*
    {
      foo {
        sub1 {
          class Foo { }
        }
      }
    }
    */
    var m = new Module(new Types());

    var ns1 = new Namespace(m);
    {
      var foo = new Namespace(m, "foo");

      var sub1 = new Namespace(m, "sub1");

      var cl = new ClassSymbolNative(new Origin(), "Foo");
      sub1.Define(cl);

      foo.Define(sub1);

      ns1.Define(foo);
    }

    AssertEqual("Foo", ns1.ResolveNamedByPath("foo.sub1.Foo").GetName());

    /*
    import "ns1" 
    {
      foo {
        sub2 {
          class Bar { }
        }
      }
    }
    */
    var ns2 = new Namespace(m);
    {
      var foo = new Namespace(m, "foo");
      
      var sub2 = new Namespace(m, "sub2");

      var cl = new ClassSymbolNative(new Origin(), "Bar");
      sub2.Define(cl);
      
      foo.Define(sub2);

      ns2.Define(foo);
    }

    ns2.Link(ns1);
    
    AssertEqual("Foo", ns2.ResolveNamedByPath("foo.sub1.Foo").GetName());
    AssertEqual("Bar", ns2.ResolveNamedByPath("foo.sub2.Bar").GetName());

    /*
    import "ns2" 
    {
      foo {
        sub3 {
          class Wow {}
        }
      }
    }
    */
    var ns3 = new Namespace(m);
    {
      var foo = new Namespace(m, "foo");
      
      var sub3 = new Namespace(m, "sub3");

      var cl = new ClassSymbolNative(new Origin(), "Wow");
      sub3.Define(cl);
      
      foo.Define(sub3);

      ns3.Define(foo);
    }

    ns3.Link(ns2);

    AssertEqual("Wow", ns3.ResolveNamedByPath("foo.sub3.Wow").GetName());
    AssertEqual("Bar", ns3.ResolveNamedByPath("foo.sub2.Bar").GetName());
    AssertTrue(ns3.ResolveNamedByPath("foo.sub1.Foo") == null);
  }
  
  [IsTested()]
  public void TestLinkNamespaceWithOtherLinkedNamespace2()
  {
    /*
    {
      foo {
        sub1 {
          class Foo { }
        }
      }
    }
    */
    var m = new Module(new Types());

    var ns1 = new Namespace(m);
    {
      var foo = new Namespace(m, "foo");

      var sub1 = new Namespace(m, "sub1");

      var cl = new ClassSymbolNative(new Origin(), "Foo");
      sub1.Define(cl);

      foo.Define(sub1);

      ns1.Define(foo);
    }

    /*
    import "ns1" 
    {
      class Bar { }
    }
    */
    var ns2 = new Namespace(m);
    {
      var cl = new ClassSymbolNative(new Origin(), "Bar");

      ns2.Define(cl);
    }

    ns2.Link(ns1);
    /*
    {
      class Bar { }
      
      * foo {
        [->foo]
        * sub1 {
           [->sub1]
        }
      }
     [->ns1]
    }
    */

    /*
    import "ns2" 
    */
    var ns3 = new Namespace(m);

    ns3.Link(ns2);
    /*
    {
     ** foo {
       [-> *foo]
       ** sub1 {
        [-> *sub1]
       }
     }
     [->ns2]
    }
    */

    AssertTrue(ns3.ResolveNamedByPath("foo") == null);
    AssertTrue(ns3.ResolveNamedByPath("foo.sub1") == null);
    AssertTrue(ns3.ResolveNamedByPath("foo.sub1.Foo") == null);
    AssertEqual("Bar", ns3.ResolveNamedByPath("Bar").GetName());
  }
  
  [IsTested()]
  public void TestLinkNamespaceWithOtherLinkedNamespace3()
  {
    /*
    {
      foo {
        sub1 {
          class Foo { }
        }
      }
    }
    */
    var m = new Module(new Types());

    var ns1 = new Namespace(m);
    {
      var foo = new Namespace(m, "foo");

      var sub1 = new Namespace(m, "sub1");

      var cl = new ClassSymbolNative(new Origin(), "Foo");
      sub1.Define(cl);

      foo.Define(sub1);

      ns1.Define(foo);
    }

    /*
    import "ns1" 
    {
      class Bar { }
      
      foo {
        class Hey { }
      }
    }
    */
    var ns2 = new Namespace(m);
    {
      var cl = new ClassSymbolNative(new Origin(), "Bar");

      ns2.Define(cl);
      
      var foo = new Namespace(m, "foo");
      var cl2 = new ClassSymbolNative(new Origin(), "Hey");
      foo.Define(cl2);
      
      ns2.Define(foo);
    }

    ns2.Link(ns1);

    /*
    import "ns2" 
    */
    var ns3 = new Namespace(m);

    ns3.Link(ns2);

    AssertEqual("foo", ns3.ResolveNamedByPath("foo").GetName());
    AssertEqual("Hey", ns3.ResolveNamedByPath("foo.Hey").GetName());
    AssertTrue(ns3.ResolveNamedByPath("foo.sub1.Foo") == null);
    AssertTrue(ns3.ResolveNamedByPath("foo.sub1") == null);
    AssertEqual("Bar", ns3.ResolveNamedByPath("Bar").GetName());
  }
  
  [IsTested()]
  public void TestLinkNamespaceWithOtherLinkedNamespace4()
  {
    /*
    {
      foo {
        sub1 {
          class Foo { }
        }
      }
    }
    */
    var m = new Module(new Types());

    var ns1 = new Namespace(m);
    {
      var foo = new Namespace(m, "foo");

      var sub1 = new Namespace(m, "sub1");

      var cl = new ClassSymbolNative(new Origin(), "Foo");
      sub1.Define(cl);

      foo.Define(sub1);

      ns1.Define(foo);
    }

    /*
    import "ns1" 
    {
      class Bar { }
    }
    */
    var ns2 = new Namespace(m);
    {
      var cl = new ClassSymbolNative(new Origin(), "Bar");

      ns2.Define(cl);
    }

    ns2.Link(ns1);
    
    /*
    import "ns2" 
    {
    }
    */
    var ns3 = new Namespace(m);
    
    ns3.Link(ns2);

    /*
    import "ns2" 
    import "ns3" 
    import "ns1" 
    */
    var ns4 = new Namespace(m);

    ns4.Link(ns2);
    ns4.Link(ns3);
    ns4.Link(ns1);

    AssertEqual("foo", ns4.ResolveNamedByPath("foo").GetName());
    AssertEqual("sub1", ns4.ResolveNamedByPath("foo.sub1").GetName());
    AssertEqual("Foo", ns4.ResolveNamedByPath("foo.sub1.Foo").GetName());
    AssertEqual("Bar", ns4.ResolveNamedByPath("Bar").GetName());
  }

  [IsTested()]
  public void TestLinkConflict()
  {
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
    var m = new Module(new Types());

    var ns1 = new Namespace(m);
    {
      var foo = new Namespace(m, "foo");

      var foo_sub = new Namespace(m, "foo_sub");

      var cl = new ClassSymbolNative(new Origin(), "Wow");
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns1.Define(foo);

      var wow = new Namespace(m, "wow");
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
    var ns2 = new Namespace(m);
    {
      var foo = new Namespace(m, "foo");

      var foo_sub = new Namespace(m, "foo_sub");

      var cl = new ClassSymbolNative(new Origin(), "Wow");
      foo_sub.Define(cl);

      foo.Define(foo_sub);

      ns2.Define(foo);
    }

    var conflict = ns2.TryLink(ns1);
    AssertEqual("Wow", conflict.local.name);

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

    var foo = vm.ResolveNamedByPath("foo") as Namespace;
    AssertTrue(foo != null);
    AssertEqual(1, foo.Count());
    AssertTrue(foo.Resolve("test") is FuncSymbol);

    var bar = vm.ResolveNamedByPath("bar") as Namespace;
    AssertTrue(bar != null);
    AssertEqual(1, bar.Count());
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

    var foo = vm.ResolveNamedByPath("foo") as Namespace;
    AssertTrue(foo != null);
    AssertEqual(2, foo.Count());
    AssertTrue(foo.Resolve("test") is FuncSymbol);
    AssertTrue(foo.Resolve("what") is FuncSymbol);

    var bar = vm.ResolveNamedByPath("bar") as Namespace;
    AssertTrue(bar != null);
    AssertEqual(1, bar.Count());
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

    var foo = vm.ResolveNamedByPath("foo") as Namespace;
    AssertTrue(foo != null);
    AssertEqual(3, foo.Count());
    AssertTrue(foo.Resolve("test") is FuncSymbol);
    AssertTrue(foo.Resolve("what") is FuncSymbol);
    AssertTrue(foo.Resolve("bar") is Namespace);

    var bar = vm.ResolveNamedByPath("bar") as Namespace;
    AssertTrue(bar != null);
    AssertEqual(2, bar.Count());
    AssertTrue(bar.Resolve("test") is FuncSymbol);
    AssertTrue(bar.Resolve("foo") is Namespace);
  }

  [IsTested()]
  public void TestSubNamespacesShortNotation()
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

    namespace bar.foo {
      func bool test()
      {
        return true
      }
    }

    namespace foo {
      func bool what()
      {
        return false
      }
    }

    namespace foo.bar {
      func bool test()
      {
        return true
      }
    }
    ";

    var vm = MakeVM(bhl);

    var foo = vm.ResolveNamedByPath("foo") as Namespace;
    AssertTrue(foo != null);
    AssertEqual(3, foo.Count());
    AssertTrue(foo.Resolve("test") is FuncSymbol);
    AssertTrue(foo.Resolve("what") is FuncSymbol);
    AssertTrue(foo.Resolve("bar") is Namespace);

    var bar = vm.ResolveNamedByPath("bar") as Namespace;
    AssertTrue(bar != null);
    AssertEqual(2, bar.Count());
    AssertTrue(bar.Resolve("test") is FuncSymbol);
    AssertTrue(bar.Resolve("foo") is Namespace);
  }

  [IsTested()]
  public void TestSubNamespacesMixedWithShortNotationConflict()
  {
    string bhl = @"
    namespace foo.bar.zoo {
      func bool test()
      {
        return true
      }
    }

    namespace foo {
      namespace bar.zoo {
        func bool test()
        {
          return false
        }
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "already defined symbol 'test'",
      new PlaceAssert(bhl, @"
        func bool test()
------------------^"
      )
    );
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
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestEmptyNamespace()
  {
    string bhl = @"
    namespace bar {
    }
    ";
    var vm = MakeVM(bhl);

    var cm = vm.FindModule("");
    AssertEqual(cm.ns.members.Count, 1);
    AssertTrue(cm.ns.members[0] is Namespace);
    AssertEqual(cm.ns.members[0].name, "bar");
    CommonChecks(vm);
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
      CommonChecks(vm);
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
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestMixNativeAndUserland()
  {
    var ts_fn = new Action<Types>((ts) => {
      {
        var fn = new FuncSymbolNative(new Origin(), "wow", ts.T("void"),
            delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
              return null;
            }
        );
        ts.ns.Nest("bar").Define(fn);
      }
    });

    string test_bhl = @"
    namespace bar {
    }
    ";
    
    var files = new List<string>();
    NewTestFile("test.bhl", test_bhl, ref files);

    var _ts = new Types();
    ts_fn(_ts);
    var loader = new ModuleLoader(_ts, CompileFiles(files, ts_fn));

    var vm = new VM(_ts, loader);
    vm.LoadModule("test");

    var cm = vm.FindModule("test");
    AssertEqual(cm.ns.Resolve("bar").name, "bar");
    Execute(vm, "bar.wow");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestMixNativeAndUserlandWithCall()
  {
    var ts_fn = new Action<Types>((ts) => {
      {
        var fn = new FuncSymbolNative(new Origin(), "wow", ts.T("int"),
            delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
              stack.Push(Val.NewInt(frm.vm, 1)); 
              return null;
            }
        );
        ts.ns.Nest("foo").Define(fn);
      }
      {
        var fn = new FuncSymbolNative(new Origin(), "wow", ts.T("int"),
            delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
              stack.Push(Val.NewInt(frm.vm, 10)); 
              return null;
            }
        );
        //NOTE: let's mix namespace defined natively and the one in bhl code
        ts.ns.Nest("bar").Define(fn);
      }
    });

    string bhl = @"
    namespace bar {
      func int test() {
        return foo.wow() + wow() /*from bar ns*/
      }
    }
    ";
    
    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(11, Execute(vm, "bar.test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNamespaceAndGlobalVarsFromCompiledModule()
  {
    string bhl = @"
    namespace bar {
      int A
      namespace foo {
        int B
      }
    }
    ";

    var vm = MakeVM(bhl);
    var m = vm.FindModule("");
    AssertTrue(m.ns.module != null);
    AssertEqual(m.ns.members.Count, 1);

    var bar = m.ns.members[0] as Namespace;
    AssertTrue(bar != null);
    AssertEqual(bar.name, "bar");
    AssertTrue(bar.module == m.ns.module);
    AssertEqual(bar.members.Count, 2);

    var foo = bar.members[1] as Namespace;
    AssertTrue(foo != null);
    AssertEqual(foo.name, "foo");
    AssertTrue(foo.module == m.ns.module);
    AssertEqual(foo.members.Count, 1);

    AssertEqual(m.gvar_index.Count, 2);
    AssertEqual(m.gvar_index[0].name, "A");
    AssertEqual(m.gvar_index[1].name, "B");

    CommonChecks(vm);
  }

  //NOTE: this is quite contraversary
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
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestEnum()
  {
    string bhl = @"
    namespace foo {
      namespace bar {
        enum E {
          V = 1
          W = 2
        }
      }

      func int ToInt(bar.E e) {
        return (int)e
      }
    }

    func int test() {
      return (int)foo.bar.E.W + foo.ToInt(foo.bar.E.V)
    }
    ";
    var vm = MakeVM(bhl);
    AssertEqual(3, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestEnumImported()
  {
    string bhl1 = @"
    namespace foo {
      namespace bar {
        enum E {
          V = 1
          W = 2
        }
      }

      func int ToInt(bar.E e) {
        return (int)e
      }
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    import ""bhl1""

    func int test() {
      foo.bar.E e = foo.bar.E.W
      return (int)e + foo.bar.ToInt(foo.bar.E.V)
    }
    ";

    string bhl3 = @"
    namespace foo {
      namespace view {
        func dummy_garbage() {
        }
      }
    }
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      }
    );
    vm.LoadModule("bhl2");
    AssertEqual(3, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCompilationBugIBarNotFound()
  {
    string bhl = @"
  namespace foo {
    namespace bar {
      enum EnumBar {
        ONE = 1
      }

      interface IBar {
        func IBar Init()
      }

      func IBar CreateBar(EnumBar id) {
        return null
      }

      class Bar : IBar {
        func IBar Init() {
          return this
        }
      }
    }
  }
";
    Compile(bhl);
  }

  [IsTested()]
  public void TestBugPostAssignOperators()
  {
    string bhl = @"
    namespace a {
      namespace b {
        int test = 1
      }
    }

    func int test() {
      a.b.test += 10
      return a.b.test
    }
  ";

    var vm = MakeVM(bhl);
    AssertEqual(11, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestBugPostIncrement()
  {
    string bhl = @"
    namespace a {
      namespace b {
        int test = 1
      }
    }

    func int test() {
      a.b.test++
      return a.b.test
    }
  ";

    var vm = MakeVM(bhl);
    AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
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
        return ..bar()
      }
    }
    ";
    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "foo.test").result.PopRelease().num);
    CommonChecks(vm);
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
  public void TestNestedClass()
  {
    string bhl = @"
    namespace bar {
      class Hey {
        class Hey_Sub {
          int f
          func int getF() {
            return this.f
          }
        }
      }
    }

    func int test() 
    {
      bar.Hey.Hey_Sub sub = {f: 10}
      return sub.getF()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
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
  public void TestNamespaceVisibilityInLambda()
  {
    string bhl = @"
    namespace bar {

      func int _42() {
        return 42
      }

      func int Do() {
        return func int() {
            return _42();
        }()
      }
    }

    func int test() 
    {
      return bar.Do()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNamespaceVisibilityInLambdaFuncArg()
  {
    string bhl = @"
    namespace a {

      class A {}

      class B {
        func int Call()
        {
          return this.Choice(func bool(A a) {
              return a == null ? false : true
          })
        }

        func int Choice(func bool(A) fn) {
          return fn(new A) ? 1 : 0
        }
      }

    }

    func int test() 
    {
      a.B b = {}
      return b.Call()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2}
      }
    );

    vm.LoadModule("bhl2");
    AssertEqual(11, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserFuncPtr()
  {
    string bhl = @"
    namespace foo {
      func int Bar() {
        return 100
      }

      func int Foo() {
        return 10
      }
    }

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
      
    var vm = MakeVM(bhl);
    AssertEqual(11, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportUserFuncPtr()
  {
    string bhl1 = @"
    namespace foo {
      func int Bar() {
        return 100
      }

      func int Foo() {
        return 10
      }
    }
    ";
      
  string bhl2 = @"
    import ""bhl1""  

    class Garbage {
      func garbage() {}
    }

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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2}
      }
    );

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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2}
      }
    );

    vm.LoadModule("bhl2");
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportAndLocalVisibility()
  {
    string bhl1 = @"
    namespace foo {
      class A { 
        func int a() {
          return 10
        }
      }
    }
    ";
      
  string bhl2 = @"
    import ""bhl1""  
    namespace foo {
      class B { 
        func int b() {
          //visible without namespace prefix
          A a = {}
          return a.a()
        }
      }
    }

    func int test() {
      foo.B b = {}
      return b.b()
    }
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2}
      }
    );

    vm.LoadModule("bhl2");
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportAndRelativeLocalVisibility()
  {
    string bhl1 = @"
    namespace foo {
      namespace sub_foo {
        class A { 
          func int a() {
            return 10
          }
        }
      }
    }
    ";
      
  string bhl2 = @"
    import ""bhl1""  
    namespace foo {
      class B { 
        func int b() {
          sub_foo.A a = {}
          return a.a()
        }
      }
    }

    func int test() {
      foo.B b = {}
      return b.b()
    }
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2}
      }
    );

    vm.LoadModule("bhl2");
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportUserInterface()
  {
    string bhl1 = @"
    namespace foo {
      interface IFoo { 
        func int Do()
      }
    }
    ";
      
  string bhl2 = @"
    import ""bhl1""  

    class Foo : foo.IFoo {
      func int Do() {
        return 42
      }
    }

    func int test() 
    {
      Foo f = {}
      return f.Do()
    }
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2}
      }
    );

    vm.LoadModule("bhl2");
    AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
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

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3}
      }
    );

    vm.LoadModule("bhl1");
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestImportSymbolsConflict()
  {
    string bhl1 = @"
    namespace foo {
      func int Foo() {
        return 10
      }
    }
    ";
      
  string bhl2 = @"
    namespace foo {
      func void Foo() {
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        MakeVM(new Dictionary<string, string>() {
            {"bhl1.bhl", bhl1},
            {"bhl2.bhl", bhl2},
          }
        );
      },
      @"symbol 'foo.Foo' is already declared in module 'bhl2'",
      new PlaceAssert(bhl1, @"
      func int Foo() {
------^"
      )
    );
  }

  [IsTested()]
  public void TestImportedSymbolsInGlobalNamespace()
  {
    string bhl1 = @"
    namespace foo {
      class Foo { 
        int Int
      }
    }
    ";

    string bhl2 = @"
    import ""bhl1""  
    namespace foo {
      namespace sub {
        class Sub { 
          foo.Foo foo
        }
      }
    }
    ";
      
      
  string bhl3 = @"
    import ""bhl1""  
    import ""bhl2""  

    func int test() 
    {
      foo.Foo f = {Int: 10}
      foo.sub.Sub s = {foo: f}
      return s.foo.Int
    }
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3}
      }
    );

    vm.LoadModule("bhl3");
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    AssertTrue(vm.ResolveNamedByPath("foo.sub.Sub") is ClassSymbol);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestIncompatibleTypes()
  {
    string bhl = @"

    namespace ecs {
      class Entity {
      }

      namespace sub {
        class Entity {
        }
        func Entity fetch() {
          return null
        }
      }

      func test() {
        Entity es = sub.fetch()
      }
    }
    ";
    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incompatible types: 'ecs.Entity' and 'ecs.sub.Entity'",
      new PlaceAssert(bhl, @"
        Entity es = sub.fetch()
---------------^"
      )
    );
   }

  [IsTested()]
  public void TestIncompatibleTypesArrays()
  {
    string bhl = @"

    namespace ecs {
      class Entity {
      }

      namespace sub {
        class Entity {
        }
        func []Entity fetch() {
          return null
        }
      }

      func test() {
        []Entity es = sub.fetch()
      }
    }
    ";
    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "incompatible types: '[]ecs.Entity' and '[]ecs.sub.Entity'",
      new PlaceAssert(bhl, @"
        []Entity es = sub.fetch()
-----------------^"
      )
    );
   }

  [IsTested()]
  public void TestImportSeveralNestedNamespacesWithSameLastName()
  {
    string bhl1 = @"
    namespace ns1.View {
      func int Foo() {
        return 10
      }
    }

    namespace ns2.View {
      func int Bar() {
        return 110
      }
    }
    ";
      
  string bhl2 = @"
    import ""bhl1""  

    func int test() 
    {
      return ns1.View.Foo() + 
        ns2.View.Bar()
    }
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2}
      }
    );

    vm.LoadModule("bhl2");
    AssertEqual(120, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }
}
