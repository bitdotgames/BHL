using System;
using System.Text;
using System.Collections.Generic;
using bhl;
using Xunit;

public class TestStackTrace : BHL_TestBase
{
  [Fact]
  public void TestGetStackTrace()
  {
    string bhl3 = @"
    func float wow(float b)
    {
      record_callstack()
      return b
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    func float bar(float b)
    {
      return wow(b)
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func float foo(float k)
    {
      return bar(k)
    }

    func float test(float k) 
    {
      return foo(k)
    }
    ";

    var trace = new List<VM.TraceItem>();
    var ts_fn = new Action<Types>((ts) => {
      {
        var fn = new FuncSymbolNative(new Origin(), "record_callstack", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            frm.fb.GetStackTrace(trace); 
            return null;
          });
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts_fn
    );
    vm.LoadModule("bhl1");
    var fb = vm.Start("test", Val.NewNum(vm, 3));
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 3);

    AssertEqual(4, trace.Count);

    AssertEqual("wow", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(4, trace[0].line);

    AssertEqual("bar", trace[1].func);
    AssertEqual("bhl2.bhl", trace[1].file);
    AssertEqual(5, trace[1].line);

    AssertEqual("foo", trace[2].func);
    AssertEqual("bhl1.bhl", trace[2].file);
    AssertEqual(5, trace[2].line);

    AssertEqual("test", trace[3].func);
    AssertEqual("bhl1.bhl", trace[3].file);
    AssertEqual(10, trace[3].line);
  }
  
  [Fact]
  public void TestGetStackTraceFromMethod()
  {
    string bhl3 = @"
    class Foo
    {
      func float wow(float b)
      {
        record_callstack()
        return b
      }
     }
    ";

    string bhl2 = @"
    import ""bhl3""
    func float bar(float b)
    {
      var foo = new Foo;
      return foo.wow(b)
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func float foo(float k)
    {
      return bar(k)
    }

    func float test(float k) 
    {
      return foo(k)
    }
    ";

    var trace = new List<VM.TraceItem>();
    var ts_fn = new Action<Types>((ts) => {
      {
        var fn = new FuncSymbolNative(new Origin(), "record_callstack", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            frm.fb.GetStackTrace(trace); 
            return null;
          });
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts_fn
    );
    vm.LoadModule("bhl1");
    var fb = vm.Start("test", Val.NewNum(vm, 3));
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 3);

    AssertEqual(4, trace.Count);

    AssertEqual("wow", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(6, trace[0].line);

    AssertEqual("bar", trace[1].func);
    AssertEqual("bhl2.bhl", trace[1].file);
    AssertEqual(6, trace[1].line);

    AssertEqual("foo", trace[2].func);
    AssertEqual("bhl1.bhl", trace[2].file);
    AssertEqual(5, trace[2].line);

    AssertEqual("test", trace[3].func);
    AssertEqual("bhl1.bhl", trace[3].file);
    AssertEqual(10, trace[3].line);
  }
  
  [Fact]
  public void TestGetStackTraceFromVirtualMethod()
  {
    string bhl3 = @"
    class Base 
    {
      virtual func float wow(float b) { return b }
    }
    class Foo : Base
    {
      override func float wow(float b)
      {
        record_callstack()
        return b
      }
     }
    ";

    string bhl2 = @"
    import ""bhl3""
    func float bar(float b)
    {
      var foo = new Foo;
      return foo.wow(b)
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func float foo(float k)
    {
      return bar(k)
    }

    func float test(float k) 
    {
      return foo(k)
    }
    ";

    var trace = new List<VM.TraceItem>();
    var ts_fn = new Action<Types>((ts) => {
      {
        var fn = new FuncSymbolNative(new Origin(), "record_callstack", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            frm.fb.GetStackTrace(trace); 
            return null;
          });
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts_fn
    );
    vm.LoadModule("bhl1");
    var fb = vm.Start("test", Val.NewNum(vm, 3));
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 3);

    AssertEqual(4, trace.Count);

    AssertEqual("wow", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(10, trace[0].line);

    AssertEqual("bar", trace[1].func);
    AssertEqual("bhl2.bhl", trace[1].file);
    AssertEqual(6, trace[1].line);

    AssertEqual("foo", trace[2].func);
    AssertEqual("bhl1.bhl", trace[2].file);
    AssertEqual(5, trace[2].line);

    AssertEqual("test", trace[3].func);
    AssertEqual("bhl1.bhl", trace[3].file);
    AssertEqual(10, trace[3].line);
  }
  
  [Fact]
  public void TestGetStackTraceFromInterfaceMethod()
  {
    string bhl3 = @"
    interface IBase 
    {
      func float wow(float b)
    }
    class Foo : IBase
    {
      func float wow(float b)
      {
        record_callstack()
        return b
      }
     }
    ";

    string bhl2 = @"
    import ""bhl3""
    func float bar(float b)
    {
      var foo = new Foo;
      return foo.wow(b)
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func float foo(float k)
    {
      return bar(k)
    }

    func float test(float k) 
    {
      return foo(k)
    }
    ";

    var trace = new List<VM.TraceItem>();
    var ts_fn = new Action<Types>((ts) => {
      {
        var fn = new FuncSymbolNative(new Origin(), "record_callstack", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            frm.fb.GetStackTrace(trace); 
            return null;
          });
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts_fn
    );
    vm.LoadModule("bhl1");
    var fb = vm.Start("test", Val.NewNum(vm, 3));
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 3);

    AssertEqual(4, trace.Count);

    AssertEqual("wow", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(10, trace[0].line);

    AssertEqual("bar", trace[1].func);
    AssertEqual("bhl2.bhl", trace[1].file);
    AssertEqual(6, trace[1].line);

    AssertEqual("foo", trace[2].func);
    AssertEqual("bhl1.bhl", trace[2].file);
    AssertEqual(5, trace[2].line);

    AssertEqual("test", trace[3].func);
    AssertEqual("bhl1.bhl", trace[3].file);
    AssertEqual(10, trace[3].line);
  }

  [Fact]
  public void TestGetStackTraceFromFuncAsArg()
  {
    string bhl3 = @"
    func float wow(float b)
    {
      record_callstack()
      return b
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    func float bar(float b)
    {
      return wow(b)
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func float foo(float k)
    {
      return k
    }

    func float test(float k) 
    {
      return foo(
          bar(k)
      )
    }
    ";

    var trace = new List<VM.TraceItem>();
    var ts_fn = new Action<Types>((ts) => {
      {
        var fn = new FuncSymbolNative(new Origin(), "record_callstack", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            frm.fb.GetStackTrace(trace); 
            return null;
          });
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts_fn
    );
    vm.LoadModule("bhl1");
    var fb = vm.Start("test", Val.NewNum(vm, 3));
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 3);

    AssertEqual(3, trace.Count);

    AssertEqual("wow", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(4, trace[0].line);

    AssertEqual("bar", trace[1].func);
    AssertEqual("bhl2.bhl", trace[1].file);
    AssertEqual(5, trace[1].line);

    AssertEqual("test", trace[2].func);
    AssertEqual("bhl1.bhl", trace[2].file);
    AssertEqual(11, trace[2].line);
  }

  [Fact]
  public void TestGetStackTraceForUserObjNullRef()
  {
    string bhl2 = @"
    class Foo {
      float k
    }
    func float bar()
    {
      Foo f
      return f.k
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func float test() 
    {
      return bar()
    }
    ";

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
      }
    );
    vm.LoadModule("bhl1");
    var fb = vm.Start("test");
    
    var info = new Dictionary<VM.Fiber, List<VM.TraceItem>>();

    try
    {
      vm.Tick();
    }
    catch(Exception)
    {
      vm.GetStackTrace(info);
    }

    AssertEqual(1, info.Count);

    var trace = info[fb];
    AssertEqual(2, trace.Count);

    AssertEqual("bar", trace[0].func);
    AssertEqual("bhl2.bhl", trace[0].file);
    AssertEqual(8, trace[0].line);

    AssertEqual("test", trace[1].func);
    AssertEqual("bhl1.bhl", trace[1].file);
    AssertEqual(5, trace[1].line);
  }

  [Fact]
  public void TestGetStackTraceAfterNativeException()
  {
    string bhl3 = @"
    func float wow(float b)
    {
      throw()
      return b
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    func float bar(float b)
    {
      return wow(b)
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func float foo(float k)
    {
      return bar(k)
    }

    func float test(float k) 
    {
      return foo(k)
    }
    ";

    var info = new Dictionary<VM.Fiber, List<VM.TraceItem>>();
    var ts_fn = new Action<Types>((ts) => {
      {
        var fn = new FuncSymbolNative(new Origin(), "throw", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            //emulating null reference
            frm = null;
            frm.fb = null;
            return null;
          });
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts_fn
    );
    vm.LoadModule("bhl1");
    var fb = vm.Start("test", Val.NewNum(vm, 3));
    try
    {
      vm.Tick();
    }
    catch(Exception)
    {
      vm.GetStackTrace(info);
    }

    AssertEqual(1, info.Count);

    var trace = info[fb];
    AssertEqual(4, trace.Count);

    AssertEqual("wow", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(4, trace[0].line);

    AssertEqual("bar", trace[1].func);
    AssertEqual("bhl2.bhl", trace[1].file);
    AssertEqual(5, trace[1].line);

    AssertEqual("foo", trace[2].func);
    AssertEqual("bhl1.bhl", trace[2].file);
    AssertEqual(5, trace[2].line);

    AssertEqual("test", trace[3].func);
    AssertEqual("bhl1.bhl", trace[3].file);
    AssertEqual(10, trace[3].line);
  }

  [Fact]
  public void TestGetStackTraceFromParal()
  {
    string bhl3 = @"
    coro func float wow(float b)
    {
      paral {
        {
          yield()
        }
        {
          record_callstack()
        }
      }
      return b
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    coro func float bar(float b)
    {
      return yield wow(b)
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    coro func float foo(float k)
    {
      return yield bar(k)
    }

    coro func float test(float k) 
    {
      return yield foo(k)
    }
    ";

    var trace = new List<VM.TraceItem>();
    var ts_fn = new Action<Types>((ts) => {
      {
        var fn = new FuncSymbolNative(new Origin(), "record_callstack", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            frm.fb.GetStackTrace(trace); 
            return null;
          });
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts_fn
    );
    vm.LoadModule("bhl1");
    var fb = vm.Start("test", Val.NewNum(vm, 3));
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 3);

    AssertEqual(4, trace.Count);

    AssertEqual("wow", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(9, trace[0].line);

    AssertEqual("bar", trace[1].func);
    AssertEqual("bhl2.bhl", trace[1].file);
    AssertEqual(5, trace[1].line);

    AssertEqual("foo", trace[2].func);
    AssertEqual("bhl1.bhl", trace[2].file);
    AssertEqual(5, trace[2].line);

    AssertEqual("test", trace[3].func);
    AssertEqual("bhl1.bhl", trace[3].file);
    AssertEqual(10, trace[3].line);
  }

  [Fact]
  public void TestGetStackTraceFromParalAll()
  {
    string bhl3 = @"
    coro func float wow(float b)
    {
      paral_all {
        {
          yield()
        }
        {
          record_callstack()
        }
      }
      return b
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    coro func float bar(float b)
    {
      return yield wow(b)
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    coro func float foo(float k)
    {
      return yield bar(k)
    }

    coro func float test(float k) 
    {
      return yield foo(k)
    }
    ";

    var trace = new List<VM.TraceItem>();
    var ts_fn = new Action<Types>((ts) => {
      {
        var fn = new FuncSymbolNative(new Origin(), "record_callstack", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            frm.fb.GetStackTrace(trace); 
            return null;
          });
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts_fn
    );
    vm.LoadModule("bhl1");
    vm.Start("test", Val.NewNum(vm, 3));
    AssertTrue(vm.Tick());

    AssertEqual(4, trace.Count);

    AssertEqual("wow", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(9, trace[0].line);

    AssertEqual("bar", trace[1].func);
    AssertEqual("bhl2.bhl", trace[1].file);
    AssertEqual(5, trace[1].line);

    AssertEqual("foo", trace[2].func);
    AssertEqual("bhl1.bhl", trace[2].file);
    AssertEqual(5, trace[2].line);

    AssertEqual("test", trace[3].func);
    AssertEqual("bhl1.bhl", trace[3].file);
    AssertEqual(10, trace[3].line);
  }

  [Fact]
  public void TestGetStackTraceInDefer()
  {
    string bhl3 = @"
    func float wow(float b)
    {

      defer {
        record_callstack()
      }
      return b
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    func float bar(float b)
    {
      return wow(b)
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func float foo(float k)
    {
      return bar(k)
    }

    func float test(float k) 
    {
      return foo(k)
    }
    ";

    var trace = new List<VM.TraceItem>();
    var ts_fn = new Action<Types>((ts) => {
      {
        var fn = new FuncSymbolNative(new Origin(), "record_callstack", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            frm.fb.GetStackTrace(trace); 
            return null;
          });
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts_fn
    );
    vm.LoadModule("bhl1");
    vm.Start("test", Val.NewNum(vm, 3));
    AssertFalse(vm.Tick());

    AssertEqual(4, trace.Count);

    AssertEqual("wow", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(6, trace[0].line);

    AssertEqual("bar", trace[1].func);
    AssertEqual("bhl2.bhl", trace[1].file);
    AssertEqual(5, trace[1].line);

    AssertEqual("foo", trace[2].func);
    AssertEqual("bhl1.bhl", trace[2].file);
    AssertEqual(5, trace[2].line);

    AssertEqual("test", trace[3].func);
    AssertEqual("bhl1.bhl", trace[3].file);
    AssertEqual(10, trace[3].line);
  }

  [Fact]
  public void TestGetStackTraceInParalDefer()
  {
    string bhl3 = @"
    func hey()
    {
      record_callstack()
    }

    func wow(float b)
    {

      defer {
        hey()
      }
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    func bar(float b)
    {
      paral_all {
        wow(b)
      }
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func foo(float k)
    {
      bar(k)
    }

    func test() 
    {
      foo(14)
    }
    ";

    var trace = new List<VM.TraceItem>();
    var ts_fn = new Action<Types>((ts) => {
      {
        var fn = new FuncSymbolNative(new Origin(), "record_callstack", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            frm.fb.GetStackTrace(trace); 
            return null;
          });
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts_fn
    );
    vm.LoadModule("bhl1");
    vm.Start("test");
    AssertFalse(vm.Tick());

    AssertEqual(5, trace.Count);

    AssertEqual("hey", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(4, trace[0].line);

    AssertEqual("wow", trace[1].func);
    AssertEqual("bhl3.bhl", trace[1].file);
    AssertEqual(11, trace[1].line);

    AssertEqual("bar", trace[2].func);
    AssertEqual("bhl2.bhl", trace[2].file);
    AssertEqual(6, trace[2].line);

    AssertEqual("foo", trace[3].func);
    AssertEqual("bhl1.bhl", trace[3].file);
    AssertEqual(5, trace[3].line);

    AssertEqual("test", trace[4].func);
    AssertEqual("bhl1.bhl", trace[4].file);
    AssertEqual(10, trace[4].line);
  }

  [Fact]
  public void TestGetStackTraceInSeqWithSuspendDefer()
  {
    string bhl3 = @"
    func foo() {
    }

    func problem() {
      throw()
    }

    coro func bar() {
      yield suspend()
    }

    coro func wow() {
      {
         defer {
           problem()
         }
         yield bar()
      }
    }
    ";

    string bhl2 = @"
    import ""bhl3""

    coro func chase()
    {
     paral {
       {
         yield()
         yield()
       }
       yield wow()
     }
    }
    ";

    string bhl1 = @"
    import ""bhl2""

    coro func test() 
    {
      yield chase()
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      {
        var fn = new FuncSymbolNative(new Origin(), "throw", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            //emulating null reference
            frm = null;
            frm.fb = null;
            return null;
          });
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts_fn
    );

    vm.LoadModule("bhl1");

    var info = new Dictionary<VM.Fiber, List<VM.TraceItem>>();

    var fb = vm.Start("test");
    try
    {
      for(int i=0;i<10;++i)
        vm.Tick();
    }
    catch(Exception)
    {
      vm.GetStackTrace(info);
    }

    AssertEqual(1, info.Count);

    var trace = info[fb];
    AssertEqual(4, trace.Count);

    AssertEqual("problem", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(6, trace[0].line);

    AssertEqual("wow", trace[1].func);
    AssertEqual("bhl3.bhl", trace[1].file);
    AssertEqual(16, trace[1].line);

    AssertEqual("chase", trace[2].func);
    AssertEqual("bhl2.bhl", trace[2].file);
    AssertEqual(11, trace[2].line);

    AssertEqual("test", trace[3].func);
    AssertEqual("bhl1.bhl", trace[3].file);
    AssertEqual(6, trace[3].line);
  }

  [Fact]
  public void TestGetStackTraceInParalWithSuspendDefer()
  {
    string bhl3 = @"
    func foo() {
    }

    func problem() {
      throw()
    }

    coro func bar() {
      yield suspend()
    }
    ";

    string bhl2 = @"
    import ""bhl3""

    coro func chase()
    {
     paral {
       {
         yield()
         yield()
       }
       {
         defer {
           problem()
         }
         yield bar()
       }
     }
    }
    ";

    string bhl1 = @"
    import ""bhl2""

    coro func test() 
    {
      yield chase()
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      {
        var fn = new FuncSymbolNative(new Origin(), "throw", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            //emulating null reference
            frm.fb.GetStackTrace();
            frm = null;
            frm.fb = null;
            return null;
          });
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts_fn
    );

    vm.LoadModule("bhl1");

    var info = new Dictionary<VM.Fiber, List<VM.TraceItem>>();

    var fb = vm.Start("test");
    try
    {
      for(int i=0;i<10;++i)
        vm.Tick();
    }
    catch(Exception)
    {
      vm.GetStackTrace(info);
    }

    AssertEqual(1, info.Count);

    var trace = info[fb];
    AssertEqual(3, trace.Count);

    AssertEqual("problem", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(6, trace[0].line);

    AssertEqual("chase", trace[1].func);
    AssertEqual("bhl2.bhl", trace[1].file);
    AssertEqual(13, trace[1].line);

    AssertEqual("test", trace[2].func);
    AssertEqual("bhl1.bhl", trace[2].file);
    AssertEqual(6, trace[2].line);
  }

  [Fact]
  public void TestGetStackTraceInSubParal()
  {
    string bhl3 = @"
    func hey()
    {
      record_callstack()
    }

    func wow(float b)
    {

      paral {
        hey()
      }
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    func bar(float b)
    {
      paral_all {
        wow(b)
      }
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func foo(float k)
    {
      bar(k)
    }

    func test() 
    {
      foo(14)
    }
    ";

    var trace = new List<VM.TraceItem>();
    var ts_fn = new Action<Types>((ts) => {
      {
        var fn = new FuncSymbolNative(new Origin(), "record_callstack", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            frm.fb.GetStackTrace(trace); 
            return null;
          });
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts_fn
    );
    vm.LoadModule("bhl1");
    vm.Start("test");
    AssertFalse(vm.Tick());

    AssertEqual(5, trace.Count);

    AssertEqual("hey", trace[0].func);
    AssertEqual("bhl3.bhl", trace[0].file);
    AssertEqual(4, trace[0].line);

    AssertEqual("wow", trace[1].func);
    AssertEqual("bhl3.bhl", trace[1].file);
    AssertEqual(11, trace[1].line);

    AssertEqual("bar", trace[2].func);
    AssertEqual("bhl2.bhl", trace[2].file);
    AssertEqual(6, trace[2].line);

    AssertEqual("foo", trace[3].func);
    AssertEqual("bhl1.bhl", trace[3].file);
    AssertEqual(5, trace[3].line);

    AssertEqual("test", trace[4].func);
    AssertEqual("bhl1.bhl", trace[4].file);
    AssertEqual(10, trace[4].line);
  }

  [Fact]
  public void TestGetStackTraceInLambda()
  {
    string bhl3 = @"
    func hey(func() cb)
    {
      cb()
    }

    func wow(func() cb)
    {

      paral {
        hey(cb)
      }
    }
    ";

    string bhl2 = @"
    import ""bhl3""
    func bar(func() cb)
    {
      paral_all {
        wow(cb)
      }
    }
    ";

    string bhl1 = @"
    import ""bhl2""
    func foo(func() cb)
    {
      bar(cb)
    }

    func test() 
    {
      foo(func() {

            record_callstack()
          }
      )
    }
    ";

    var trace = new List<VM.TraceItem>();

    var ts_fn = new Action<Types>((ts) => {
      {
        var fn = new FuncSymbolNative(new Origin(), "record_callstack", Types.Void,
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            frm.fb.GetStackTrace(trace); 
            return null;
          });
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(new Dictionary<string, string>() {
        {"bhl1.bhl", bhl1},
        {"bhl2.bhl", bhl2},
        {"bhl3.bhl", bhl3},
      },
      ts_fn
    );
    vm.LoadModule("bhl1");
    vm.Start("test");
    AssertFalse(vm.Tick());

    AssertEqual(6, trace.Count);

    AssertEqual("?", trace[0].func);
    AssertEqual("bhl1.bhl", trace[0].file);
    AssertEqual(12, trace[0].line);

    AssertEqual("hey", trace[1].func);
    AssertEqual("bhl3.bhl", trace[1].file);
    AssertEqual(4, trace[1].line);

    AssertEqual("wow", trace[2].func);
    AssertEqual("bhl3.bhl", trace[2].file);
    AssertEqual(11, trace[2].line);

    AssertEqual("bar", trace[3].func);
    AssertEqual("bhl2.bhl", trace[3].file);
    AssertEqual(6, trace[3].line);

    AssertEqual("foo", trace[4].func);
    AssertEqual("bhl1.bhl", trace[4].file);
    AssertEqual(5, trace[4].line);

    AssertEqual("test", trace[5].func);
    AssertEqual("bhl1.bhl", trace[5].file);
    AssertEqual(10, trace[5].line);
  }

  [Fact]
  public void TestGetStackTraceInDeferFromNullPtrWhenStopped()
  {
    string bhl = @"
    coro func wow()
    {
      defer {
        Color c = null
        c.mult_summ(1)
      }
      yield suspend()
    }

    coro func test() {
      yield wow()
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    vm.Tick();

    AssertError<Exception>(
      delegate() {
        vm.Stop();
      },
      "at wow(..) in .bhl:6"
    );
  }

  [Fact]
  public void TestGetStackTraceInDeferFromExceptionWhenStoppedWithinParal()
  {
    string bhl = @"
    coro func wow()
    {
      defer {
        borked()
      }
      yield suspend()
    }

    coro func test() {
      paral {
        yield wow()
        yield wow()
      }
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      var fn = new FuncSymbolNative(new Origin(), "borked", Types.Void,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
        {
          object o = null;
          o.GetType();
          return null;
        }
      );
      ts.ns.Define(fn);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    vm.Tick();

    AssertError<Exception>(
      delegate() {
        vm.Stop();
      },
      "at wow(..) in .bhl:5"
    );
  }

  [Fact]
  public void TestGetStackTraceInDeferFromExceptionWhenStoppedWithinParalAll()
  {
    string bhl = @"
    coro func wow()
    {
      defer {
        borked()
      }
      yield suspend()
    }

    coro func test() {
      paral_all {
        yield wow()
        yield wow()
      }
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      var fn = new FuncSymbolNative(new Origin(), "borked", Types.Void,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
        {
          object o = null;
          o.GetType();
          return null;
        }
      );
      ts.ns.Define(fn);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    vm.Tick();

    AssertError<Exception>(
      delegate() {
        vm.Stop();
      },
      "at wow(..) in .bhl:5"
    );
  }

}
