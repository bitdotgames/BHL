using System;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using bhl;
using Xunit;

public class TestArrays : BHL_TestBase
{
  [Fact]
  public void TestEmptyIntArray()
  {
    string bhl = @"
    func []int test()
    {
      []int a = new []int
      return a
    }
    ";

    var c = Compile(bhl);

    var ts = new Types();

    var expected =
        new ModuleCompiler()
          .UseCode()
          .EmitThen(Opcodes.InitFrame, new int[] { 1, 1 })
          .EmitThen(Opcodes.New, new int[] { TypeIdx(c, ts.TArr("int")) })
          .EmitThen(Opcodes.SetVar, new int[] { 0 })
          .EmitThen(Opcodes.GetVar, new int[] { 0 })
          .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    var lst = fb.Stack.Pop();
    Assert.Empty(lst.obj as IList<Val>);
    lst.ReleaseData();
    CommonChecks(vm);
  }

  [Fact]
  public void TestAddToStringArray()
  {
    string bhl = @"
    func string test()
    {
      []string a = new []string
      a.Add(""test"")
      return a[0]
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal("test", fb.Stack.Pop().str);
    CommonChecks(vm);
  }

  [Fact]
  public void TestStringArrayIndex()
  {
    string bhl = @"

    func string test()
    {
      []string arr = new []string
      arr.Add(""bar"")
      arr.Add(""foo"")
      return arr[1]
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").Stack.Pop().str;
    Assert.Equal("foo", res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestTmpArrayAtIdx()
  {
    string bhl = @"
    func []int mkarray()
    {
      []int a = new []int
      a.Add(1)
      a.Add(2)
      return a
    }

    func int test()
    {
      return mkarray()[0]
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(1, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestArrayRemoveAt()
  {
    string bhl = @"
    func []int mkarray()
    {
      []int arr = new []int
      arr.Add(1)
      arr.Add(100)
      arr.RemoveAt(0)
      return arr
    }

    func []int test()
    {
      return mkarray()
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    var lst = fb.Stack.Pop();
    Assert.Single((lst.obj as IList<Val>));
    lst.ReleaseData();
    CommonChecks(vm);
  }

  [Fact]
  public void TestTmpArrayCount()
  {
    string bhl = @"
    func []int mkarray()
    {
      []int arr = new []int
      arr.Add(1)
      arr.Add(100)
      return arr
    }

    func int test()
    {
      return mkarray().Count
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(2, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestTmpArrayRemoveAt()
  {
    string bhl = @"

    func []int mkarray()
    {
      []int arr = new []int
      arr.Add(1)
      arr.Add(100)
      return arr
    }

    func test()
    {
      mkarray().RemoveAt(0)
    }
    ";

    var vm = MakeVM(bhl);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [Fact]
  public void TestTmpArrayAdd()
  {
    string bhl = @"

    func []int mkarray()
    {
      []int arr = new []int
      arr.Add(1)
      arr.Add(100)
      return arr
    }

    func test()
    {
      mkarray().Add(300)
    }
    ";

    var vm = MakeVM(bhl);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [Fact]
  public void TestArrayIndexOf()
  {
    {
      string bhl = @"
      func int test()
      {
        []int arr = [1, 2, 10]
        return arr.IndexOf(2)
      }
      ";

      var vm = MakeVM(bhl);
      Assert.Equal(1, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func int test()
      {
        []int arr = [1, 2, 10]
        return arr.IndexOf(1)
      }
      ";

      var vm = MakeVM(bhl);
      Assert.Equal(0, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func int test()
      {
        []int arr = [1, 2, 10]
        return arr.IndexOf(10)
      }
      ";

      var vm = MakeVM(bhl);
      Assert.Equal(2, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func int test()
      {
        []int arr = [1, 2, 10]
        return arr.IndexOf(100)
      }
      ";

      var vm = MakeVM(bhl);
      Assert.Equal(-1, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }
  }

  [Fact]
  public void TestStringArrayAssign()
  {
    string bhl = @"

    func []string test()
    {
      []string arr = new []string
      arr.Add(""foo"")
      arr[0] = ""tst""
      arr.Add(""bar"")
      return arr
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    var val = fb.Stack.Pop();
    var lst = val.obj as IList<Val>;
    Assert.Equal(2, lst.Count);
    AssertEqual(lst[0].str, "tst");
    AssertEqual(lst[1].str, "bar");
    val.ReleaseData();
    CommonChecks(vm);
  }

  [Fact]
  public void TestValueIsClonedOnceStoredInArray()
  {
    string bhl = @"
    func test()
    {
      []int ints = []
      for(int i=0;i<3;i++) {
        ints.Add(i)
      }

      int local_var_garbage = 10
      for(int i=0;i<ints.Count;i++) {
        trace((string)ints[i] + "";"")
      }

      for(int i=0;i<ints.Count;i++) {
        ints[i] = i
      }

      int local_var_garbage2 = 20
      for(int i=0;i<ints.Count;i++) {
        trace((string)ints[i] + "";"")
      }

    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => { BindTrace(ts, log); });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual(log.ToString(), "0;1;2;0;1;2;");
    CommonChecks(vm);
  }

  [Fact]
  public void TestArrayPassedToFuncIsChanged()
  {
    string bhl = @"
    func foo([]int a)
    {
      a.Add(100)
    }

    func int test()
    {
      []int a = new []int
      a.Add(1)
      a.Add(2)

      foo(a)

      return a[2]
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(100, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestArrayPassedToFuncByRef()
  {
    string bhl = @"
    func foo(ref []int a)
    {
      a.Add(100)
    }

    func int test()
    {
      []int a = new []int
      a.Add(1)
      a.Add(2)

      foo(ref a)

      return a[2]
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    Assert.Equal(100, fb.Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestArrayCount()
  {
    string bhl = @"

    func int test()
    {
      []string arr = new []string
      arr.Add(""foo"")
      arr.Add(""bar"")
      int c = arr.Count
      return c
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(2, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestClearArray()
  {
    string bhl = @"

    func string test()
    {
      []string arr = new []string
      arr.Add(""bar"")
      arr.Clear()
      arr.Add(""foo"")
      return arr[0]
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").Stack.Pop().str;
    AssertEqual(res, "foo");
    CommonChecks(vm);
  }

  [Fact]
  public void TestInsert()
  {
    string bhl = @"

    func int test1()
    {
      []int arr = []
      arr.Add(14)
      arr.Insert(0, 42)
      return arr[0]
    }

    func int test2()
    {
      []int arr = []
      arr.Add(14)
      arr.Insert(arr.Count, 42)
      return arr[arr.Count - 1]
    }
    ";

    var vm = MakeVM(bhl);
    Assert.Equal(42, Execute(vm, "test1").Stack.Pop().num);
    Assert.Equal(42, Execute(vm, "test2").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestArrayPool()
  {
    string bhl = @"

    func []string make()
    {
      []string arr = new []string
      return arr
    }

    func add([]string arr)
    {
      arr.Add(""foo"")
      arr.Add(""bar"")
    }

    func []string test()
    {
      []string arr = make()
      add(arr)
      return arr
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").Stack.Pop();

    var lst = res.obj as IList<Val>;
    Assert.Equal(2, lst.Count);
    AssertEqual(lst[0].str, "foo");
    AssertEqual(lst[1].str, "bar");

    Assert.Equal(1, vm.vlsts_pool.MissCount);
    Assert.Equal(0, vm.vlsts_pool.IdleCount);

    res.ReleaseData();

    CommonChecks(vm);
  }

  [Fact]
  public void TestArrayPoolInInfiniteLoop()
  {
    string bhl = @"
    coro func test()
    {
      while(true) {
        []string arr = new []string
        yield()
      }
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");

    for(int i = 0; i < 3; ++i)
      vm.Tick();

    vm.Stop(fb);

    Assert.Equal(2, vm.vlsts_pool.MissCount);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeClassArray()
  {
    string bhl = @"

    func string test(float k)
    {
      ArrayT_Color cs = new ArrayT_Color
      Color c0 = new Color
      cs.Add(c0)
      cs.RemoveAt(0)
      Color c1 = new Color
      c1.r = 10
      Color c2 = new Color
      c2.g = 20
      cs.Add(c1)
      cs.Add(c2)
      Color c3 = new Color
      cs.Add(c3)
      cs[2].r = 30
      Color c4 = new Color
      cs.Add(c4)
      cs.RemoveAt(3)
      return (string)cs.Count + (string)cs[0].r + (string)cs[1].g + (string)cs[2].r
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test",  2).Stack.Pop().str;
    Assert.Equal("3102030", res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeClassArrayIndexOf()
  {
    {
      string bhl = @"
      func int test()
      {
        ArrayT_Color cs = []
        Color c0 = {r:1}
        Color c1 = {r:2}
        cs.Add(c0)
        cs.Add(c1)
        return cs.IndexOf(c0)
      }
      ";

      var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

      var vm = MakeVM(bhl, ts_fn);
      Assert.Equal(0, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func int test()
      {
        ArrayT_Color cs = []
        Color c0 = {r:1}
        Color c1 = {r:2}
        cs.Add(c0)
        cs.Add(c1)
        return cs.IndexOf(c1)
      }
      ";

      var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

      var vm = MakeVM(bhl, ts_fn);
      Assert.Equal(1, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }

    //NOTE: Color is a class not a struct, and when we search
    //      for a similar element in an array the 'value equality'
    //      routine is not used but rather the 'pointers equality'
    //      one. Maybe it's an expected behavior, maybe not :(
    //      Need more feedback on this issue.
    //
    {
      string bhl = @"
      func int test()
      {
        ArrayT_Color cs = []
        Color c0 = {r:1}
        Color c1 = {r:2}
        cs.Add(c0)
        cs.Add(c1)
        return cs.IndexOf({r:1})
      }
      ";

      var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

      var vm = MakeVM(bhl, ts_fn);
      Assert.Equal(-1, Execute(vm, "test").Stack.Pop().num);
      CommonChecks(vm);
    }
  }

  [Fact]
  public void TestNativeClassTmpArray()
  {
    string bhl = @"

    func ArrayT_Color mkarray()
    {
      ArrayT_Color cs = new ArrayT_Color
      Color c0 = new Color
      c0.g = 1
      cs.Add(c0)
      Color c1 = new Color
      c1.r = 10
      cs.Add(c1)
      return cs
    }

    func float test()
    {
      return mkarray()[1].r
    }
    ";

    var ts_fn = new Action<Types>((ts) => { BindColor(ts); });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test").Stack.Pop().num;
    Assert.Equal(10, res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeSubClassArray()
  {
    string bhl = @"

    func string test(float k)
    {
      Foo f = new Foo
      f.hey = 10
      Color c1 = new Color
      c1.r = 20
      Color c2 = new Color
      c2.g = 30
      f.colors.Add(c1)
      f.colors.Add(c2)
      return (string)f.colors.Count + (string)f.hey + (string)f.colors[0].r + (string)f.colors[1].g
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      BindColor(ts);
      BindFoo(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test", 2).Stack.Pop().str;
    Assert.Equal("2102030", res);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeListTypeSymbolBasicOperations()
  {
    var ArrayInts = new NativeListTypeSymbol<int>(
      new Origin(),
      "List_int",
      (v) => (int)v,
      (itype, n) => n,
      Types.Int
    );
    ArrayInts.Setup();

    var list = new List<int>();

    var arr = Val.NewObj(list, ArrayInts);

    Assert.Equal(0, ArrayInts.ArrCount(arr));

    {
      ArrayInts.ArrAdd(arr, 10);

      Assert.Equal(1, ArrayInts.ArrCount(arr));
      Assert.Single(list);
    }

    {
      int val = ArrayInts.ArrGetAt(arr, 0);
      Assert.Equal(10, val);

      Assert.Equal(10, list[0]);
    }

    {
      ArrayInts.ArrRemoveAt(arr, 0);
      Assert.Equal(0, ArrayInts.ArrCount(arr));

      Assert.Empty(list);
    }
  }

  [Fact]
  public void TestNativeListProxy()
  {
    string bhl = @"
    func test()
    {
      var lst = new List_int
      []int ns = lst
      ns.Add(100)
      ns.RemoveAt(0)
      ns.Add(200)
      ns.Add(300)

      trace((string)ns[0] + "";"" + lst.At(1))
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      var ArrayInts = new NativeListTypeSymbol<int>(
        new Origin(),
        "List_int",
        (v) => v,
        ( itype, n) => n,
        Types.Int
      );
      ArrayInts.Setup();
      ts.ns.Define(ArrayInts);

      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("200;300", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeListProxyForeach()
  {
    string bhl = @"
    func test()
    {
      var ns = new List_int
      ns.Add(100)
      ns.Add(200)
      foreach(var n in ns) {
        trace((string)n + "";"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      var ArrayInts = new NativeListTypeSymbol<int>(
        new Origin(),
        "List_int",
        (v) => v,
        (itype, n) => n,
        Types.Int
      );
      ArrayInts.Setup();
      ts.ns.Define(ArrayInts);

      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    Assert.Equal("100;200;", log.ToString());
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeListProxyForeachAsGenericType()
  {
    string bhl = @"
    func test()
    {
      []int ns = new List_int
      ns.Add(100)
      ns.Add(200)
      foreach(var n in ns) {
        trace((string)n + "";"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) =>
    {
      var ArrayInts = new NativeListTypeSymbol<int>(
        new Origin(),
        "List_int",
        (v) => v,
        (type, n) => n,
        Types.Int
      );
      ArrayInts.Setup();
      ts.ns.Define(ArrayInts);

      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual(log.ToString(), "100;200;");
    CommonChecks(vm);
  }

  [Fact]
  public void TestNativeListEquality()
  {
    var ArrayInts1 = new NativeListTypeSymbol<int>(
      new Origin(),
      "List_int",
      (v) => v,
      (itype, n) => n,
      Types.Int
    );
    ArrayInts1.Setup();

    var ArrayInts2 = new NativeListTypeSymbol<int>(
      new Origin(),
      "List_int",
      (v) => v,
      (itype, n) => n,
      Types.Int
    );
    ArrayInts2.Setup();

    var ArrayString = new NativeListTypeSymbol<string>(
      new Origin(),
      "List_string",
      (v) => v,
      (itype, n) => n,
      Types.String
    );
    ArrayString.Setup();

    Assert.True(ArrayInts1.Equals(ArrayInts2));
    Assert.False(ArrayString.Equals(ArrayInts1));
  }

  [Fact]
  public void TestGenericArrayEquality()
  {
    var ArrayInts1 = new GenericArrayTypeSymbol(
      new Origin(),
      Types.Int
    );
    ArrayInts1.Setup();

    var ArrayInts2 = new GenericArrayTypeSymbol(
      new Origin(),
      Types.Int
    );
    ArrayInts2.Setup();

    var ArrayString = new GenericArrayTypeSymbol(
      new Origin(),
      Types.String
    );
    ArrayString.Setup();

    Assert.True(ArrayInts1.Equals(ArrayInts2));
    Assert.False(ArrayString.Equals(ArrayInts1));
  }

  [Fact]
  public void TestValListTypedAdapater()
  {
    var ArrayInts = new NativeListTypeSymbol<int>(
      new Origin(),
      "List_int",
      (v) => v,
      (itype, n) => n,
      Types.Int
    );
    ArrayInts.Setup();

    var vm = new VM();

    var vals = ValList.New(vm);
    vals.Add(10);
    vals.Add(20);

    var vals_typed = ValList<int>.New(vals, ArrayInts.Val2Native);

    Assert.Equal(1, vals_typed.IndexOf(20));
    Assert.Equal(0, vals_typed.IndexOf(10));
    Assert.Equal(-1, vals_typed.IndexOf(30));

    Assert.Contains(10, vals_typed);
    Assert.Contains(20, vals_typed);
    Assert.DoesNotContain(30, vals_typed);

    var res = new List<int>();
    foreach(var n in vals_typed)
      res.Add(n);

    Assert.Equal(2, res.Count);
    Assert.Equal(10, res[0]);
    Assert.Equal(20, res[1]);

    Assert.True(vals_typed.Remove(10));
    Assert.False(vals_typed.Remove(30));
    Assert.Equal(-1, vals_typed.IndexOf(10));
    Assert.Equal(0, vals_typed.IndexOf(20));

    vals_typed.Release();

    CommonChecks(vm);
  }

  [Fact]
  public void TestRefcList()
  {
    {
      var lst = RefcList<int>.New();
      lst.Add(10);

      Assert.Single(lst);
      Assert.Equal(10, lst[0]);

      lst.Release();
    }

    {
      var lst = RefcList<int>.New();

      Assert.Empty(lst);

      lst.Release();
    }
  }

  [Fact]
  public void TestValList()
  {
    var vm = new VM();

    var lst = ValList.New(vm);

    lst.Add(10);
    lst.Add(1);

    Assert.Equal(2, lst.Count);

    (lst[0], lst[1]) = (lst[1], lst[0]);
    Assert.Equal(1, lst[0].num);
    Assert.Equal(10, lst[1].num);

    //removing an item still releases it
    lst.RemoveAt(1);
    Assert.Single(lst);
    Assert.Equal(1, lst[0].num);

    lst.Add(20);
    Assert.Equal(2, lst.Count);
    Assert.Equal(1, lst[0].num);
    Assert.Equal(20, lst[1].num);

    lst.Release();

    CommonChecks(vm);
  }

  [Fact]
  public void TestValListOwnership()
  {
    var vm = new VM();

    var lst = ValList.New(vm);

    {
      var vr = ValRef.New(vm);
      vr.Retain();
      lst.Add(Val.NewObj(vr, null));
      Assert.Equal(2, vr._refs);

      lst.Clear();
      Assert.Equal(1, vr._refs);
      vr.Release();
    }

    {
      var vr = ValRef.New(vm);
      vr.Retain();
      lst.Add(Val.NewObj(vr, null));
      Assert.Equal(2, vr._refs);

      lst.RemoveAt(0);
      Assert.Equal(1, vr._refs);

      lst.Clear();
      vr.Release();
    }

    lst.Release();

    CommonChecks(vm);
  }

  [Fact]
  public void TestValListSort()
  {
    var vm = new VM();

    var lst = ValList.New(vm);

    lst.Add(10);
    lst.Add(1);
    lst.Add(13);

    var sorted = lst.OrderBy(v => v.num).ToList();
    Assert.Equal(3, sorted.Count);
    Assert.Equal(1, sorted[0].num);
    Assert.Equal(10, sorted[1].num);
    Assert.Equal(13, sorted[2].num);

    lst.Release();

    CommonChecks(vm);
  }

  [Fact]
  public void TestValListAsIList()
  {
    var vm = new VM();

    var lst = ValList.New(vm);

    lst.Add(10);
    lst.Add(1);

    var ilst = (IList)lst;
    Assert.Equal(2, ilst.Count);

    //swapping items has now effect on ownership
    (ilst[0], ilst[1]) = (ilst[1], ilst[0]);
    Assert.Equal(1, lst[0].num);
    Assert.Equal(10, lst[1].num);

    //removing an item still releases it
    ilst.RemoveAt(1);
    Assert.Single(ilst);
    Assert.Equal(1, lst[0].num);

    //adding an item implies ownership over it
    ilst.Add(Val.NewInt(20));
    Assert.Equal(2, ilst.Count);
    Assert.Equal(1, lst[0].num);
    Assert.Equal(20, lst[1].num);

    lst.Release();

    CommonChecks(vm);
  }

  [Fact]
  public void TestReturnValListFromNativeFunc()
  {
    string bhl = @"
    func []Color colors() {
      []Color cs = get_colors()
      return cs
    }

    func float test()
    {
      var cs = colors()
      return cs[1].r
    }
    ";

    var ts_fn = new Action<Types>((ts) =>
    {
      BindColor(ts);

      {
        var fn = new FuncSymbolNative(new Origin(), "get_colors", ts.TArr("Color"),
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            {
              var dv0 = new Val();
              var dvl = ValList.New(exec.vm);
              for(int i = 0; i < 10; ++i)
              {
                var c = new Color();
                c.r = i;
                var tdv = new Val();
                tdv.SetObj(c, ts.T("Color").Get());
                dvl.Add(tdv);
              }

              dv0.SetObj(dvl, Types.Array);
              exec.stack.Push(dv0);
            }
            return null;
          }
        );
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    Assert.Equal(1, Execute(vm, "test").Stack.Pop().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestValListGetEnumerator()
  {
    var vm = new VM();

    var lst = ValList.New(vm);
    lst.Add(1);
    lst.Add(2);

    int c = 0;
    foreach(var tmp in lst)
    {
      ++c;
      if(c == 1)
        Assert.Equal(1, tmp.num);
      else if(c == 2)
        Assert.Equal(2, tmp.num);
    }

    Assert.Equal(2, c);

    lst.Release();

    CommonChecks(vm);
  }
}
