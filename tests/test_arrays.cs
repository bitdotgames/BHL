using System;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
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
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { TypeIdx(c, ts.TArr("int")) }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    Assert.False(vm.Tick());
    var lst = fb.result.Pop();
    Assert.Equal(0, (lst.obj as IList<Val>).Count);
    lst.Release();
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
    AssertEqual(fb.result.PopRelease().str, "test");
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
    var res = Execute(vm, "test").result.PopRelease().str;
    AssertEqual(res, "foo");
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
    Assert.Equal(1, fb.result.PopRelease().num);
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
    var lst = fb.result.Pop();
    Assert.Equal(1, (lst.obj as IList<Val>).Count);
    lst.Release();
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
    Assert.Equal(2, fb.result.PopRelease().num);
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
      Assert.Equal(1, Execute(vm, "test").result.PopRelease().num);
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
      Assert.Equal(0, Execute(vm, "test").result.PopRelease().num);
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
      Assert.Equal(2, Execute(vm, "test").result.PopRelease().num);
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
      Assert.Equal(-1, Execute(vm, "test").result.PopRelease().num);
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
    var val = fb.result.Pop();
    var lst = val.obj as IList<Val>;
    Assert.Equal(2, lst.Count);
    AssertEqual(lst[0].str, "tst");
    AssertEqual(lst[1].str, "bar");
    val.Release();
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
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

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
    Assert.Equal(100, fb.result.PopRelease().num);
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
    Assert.Equal(100, fb.result.PopRelease().num);
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
    var res = Execute(vm, "test").result.PopRelease().num;
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
    var res = Execute(vm, "test").result.PopRelease().str;
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
    Assert.Equal(42, Execute(vm, "test1").result.PopRelease().num);
    Assert.Equal(42, Execute(vm, "test2").result.PopRelease().num);
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
    var res = Execute(vm, "test").result.Pop();

    var lst = res.obj as IList<Val>;
    Assert.Equal(2, lst.Count);
    AssertEqual(lst[0].str, "foo");
    AssertEqual(lst[1].str, "bar");

    Assert.Equal(1, vm.vlsts_pool.MissCount);
    Assert.Equal(0, vm.vlsts_pool.IdleCount);
    
    res.Release();

    CommonChecks(vm);
  }

  [Fact]
  public void TestArrayPoolInInfiniteLoop()
  {
    string bhl = @"

    func []string make()
    {
      []string arr = new []string
      return arr
    }
      
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
    
    for(int i=0;i<3;++i)
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

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().str;
    AssertEqual(res, "3102030");
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

      var ts_fn = new Action<Types>((ts) => {
        BindColor(ts);
      });

      var vm = MakeVM(bhl, ts_fn);
      Assert.Equal(0, Execute(vm, "test").result.PopRelease().num);
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

      var ts_fn = new Action<Types>((ts) => {
        BindColor(ts);
      });

      var vm = MakeVM(bhl, ts_fn);
      Assert.Equal(1, Execute(vm, "test").result.PopRelease().num);
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

      var ts_fn = new Action<Types>((ts) => {
        BindColor(ts);
      });

      var vm = MakeVM(bhl, ts_fn);
      Assert.Equal(-1, Execute(vm, "test").result.PopRelease().num);
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

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test").result.PopRelease().num;
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

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
      BindFoo(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().str;
    AssertEqual(res, "2102030");
    CommonChecks(vm);
  }
  
  [Fact]
  public void TestNativeListTypeSymbolBasicOperations()
  {
    var ArrayInts = new NativeListTypeSymbol<int>(
      new Origin(),
      "List_int",
      (v) => (int)v._num,
      (_vm, itype, n) => Val.NewInt(_vm, n),
      Types.Int
    );
    ArrayInts.Setup();

    var list = new List<int>();
    
    var vm = new VM();
    var arr = Val.NewObj(vm, list, ArrayInts);
    
    Assert.Equal(0, ArrayInts.ArrCount(arr));

    {
      var val = Val.NewInt(vm, 10);
      ArrayInts.ArrAdd(arr, val);
      val.Release();
      
      Assert.Equal(1, ArrayInts.ArrCount(arr));
      Assert.Equal(1, list.Count);
    }

    {
      var val = ArrayInts.ArrGetAt(arr, 0);
      Assert.Equal(10, val.num);
      val.Release();
      
      Assert.Equal(10, list[0]);
    }

    {
      ArrayInts.ArrRemoveAt(arr, 0);
      Assert.Equal(0, ArrayInts.ArrCount(arr));
      
      Assert.Equal(0, list.Count);
    }

    arr.Release();
    
    CommonChecks(vm);
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
    var ts_fn = new Action<Types>((ts) => {
      var ArrayInts = new NativeListTypeSymbol<int>(
        new Origin(), 
        "List_int",
        (v) => (int)v._num,
        (_vm, itype, n) => Val.NewInt(_vm, n),
        Types.Int
        ); 
      ArrayInts.Setup();
      ts.ns.Define(ArrayInts);
      
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual(log.ToString(), "200;300");
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
    var ts_fn = new Action<Types>((ts) => {
      var ArrayInts = new NativeListTypeSymbol<int>(
        new Origin(), 
        "List_int",
        (v) => (int)v._num,
        (_vm, itype, n) => Val.NewInt(_vm, n),
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
    var ts_fn = new Action<Types>((ts) => {
      var ArrayInts = new NativeListTypeSymbol<int>(
        new Origin(), 
        "List_int",
        (v) => (int)v._num,
        (_vm, itype, n) => Val.NewInt(_vm, n),
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
      (v) => (int)v._num,
      (_vm, itype, n) => Val.NewInt(_vm, n),
      Types.Int
      ); 
    ArrayInts1.Setup();
    
    var ArrayInts2 = new NativeListTypeSymbol<int>(
      new Origin(), 
      "List_int",
      (v) => (int)v._num,
      (_vm, itype, n) => Val.NewInt(_vm, n),
      Types.Int
      ); 
    ArrayInts2.Setup();
    
    var ArrayString = new NativeListTypeSymbol<string>(
      new Origin(), 
      "List_string",
      (v) => v.str,
      (_vm, itype, n) => Val.NewStr(_vm, n),
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
      (v) => (int)v._num,
      (_vm, itype, n) => Val.NewInt(_vm, n),
      Types.Int
    );
    ArrayInts.Setup();
    
    var vm = new VM();

    var vals = ValList.New(vm);
    {
      var v1 = Val.NewInt(vm, 10);
      vals.Add(v1);
      v1.Release();
    }

    {
      var v2 = Val.NewInt(vm, 20);
      vals.Add(v2);
      v2.Release();
    }

    var vals_typed = ValList<int>.New(vals, ArrayInts.Val2Native);

    Assert.Equal(1, vals_typed.IndexOf(20));
    Assert.Equal(0, vals_typed.IndexOf(10));
    Assert.Equal(-1, vals_typed.IndexOf(30));

    Assert.True(vals_typed.Contains(10));
    Assert.True(vals_typed.Contains(20));
    Assert.False(vals_typed.Contains(30));
    
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

      Assert.Equal(1, lst.Count);
      Assert.Equal(10, lst[0]);

      lst.Release();
    }
    
    {
      var lst = RefcList<int>.New();
      
      Assert.Equal(0, lst.Count);
      
      lst.Release();
    }
  }
  
  [Fact]
  public void TestValListOwnership()
  {
    var vm = new VM();

    var lst = ValList.New(vm);

    {
      var dv = Val.New(vm);
      lst.Add(dv);
      Assert.Equal(1, dv._refs);

      lst.Clear();
      Assert.Equal(1, dv._refs);
      dv.Release();
    }

    {
      var dv = Val.New(vm);
      lst.Add(dv);
      Assert.Equal(1, dv._refs);

      lst.RemoveAt(0);
      Assert.Equal(1, dv._refs);

      lst.Clear();
      Assert.Equal(1, dv._refs);
      dv.Release();
    }

    {
      var dv0 = Val.New(vm);
      var dv1 = Val.New(vm);
      lst.Add(dv0);
      lst.Add(dv1);
      Assert.Equal(1, dv0._refs);
      Assert.Equal(1, dv1._refs);

      lst.RemoveAt(1);
      Assert.Equal(1, dv0._refs);
      Assert.Equal(1, dv1._refs);

      lst.Clear();
      Assert.Equal(1, dv0._refs);
      Assert.Equal(1, dv1._refs);
      dv0.Release();
      dv1.Release();
    }

    lst.Release();

    CommonChecks(vm);
  }
  
  [Fact]
  public void TestValListSort()
  {
    var vm = new VM();

    var lst = ValList.New(vm);

    var v1 = Val.NewInt(vm, 10);
    lst.Add(v1);
    v1.Release();
    
    var v2 = Val.NewInt(vm, 1);
    lst.Add(v2);
    v2.Release();
    
    var v3 = Val.NewInt(vm, 13);
    lst.Add(v3);
    v3.Release();

    var sorted = lst.OrderBy(v => v.num).ToList();
    Assert.Equal(3, sorted.Count);
    Assert.Equal(1, sorted[0].num);
    Assert.Equal(10, sorted[1].num);
    Assert.Equal(13, sorted[2].num);

    lst.Release();

    CommonChecks(vm);
  }
  
  [Fact]
  public void TestValListAsIListHasDifferentOwnershipSemantics()
  {
    var vm = new VM();

    var lst = ValList.New(vm);

    var v1 = Val.NewInt(vm, 10);
    lst.Add(v1);
    v1.Release();
    
    var v2 = Val.NewInt(vm, 1);
    lst.Add(v2);
    v2.Release();

    var ilst = (IList)lst;
    Assert.Equal(2, ilst.Count);

    //swapping items has now effect on ownership
    (ilst[0], ilst[1]) = (ilst[1], ilst[0]);
    Assert.Equal(1, lst[0].num);
    Assert.Equal(10, lst[1].num);
    
    //removing an item still releases it
    ilst.RemoveAt(1);
    Assert.Equal(1, ilst.Count);
    Assert.Equal(1, lst[0].num);

    //adding an item implies ownership over it
    ilst.Add(Val.NewInt(vm, 20));
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

    var ts_fn = new Action<Types>((ts) => {
      
      BindColor(ts);

      {
        var fn = new FuncSymbolNative(new Origin(), "get_colors", ts.TArr("Color"),
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
          {
            {
              var dv0 = Val.New(frm.vm);
              var dvl = ValList.New(frm.vm);
              for(int i=0;i<10;++i)
              {
                var c = new Color();
                c.r = i;
                var tdv = Val.New(frm.vm);
                tdv.SetObj(c, ts.T("Color").Get());
                dvl.lst.Add(tdv);
              }
              dv0.SetObj(dvl, Types.Array);
              stack.Push(dv0);
            }
            return null;
          }
        );
        ts.ns.Define(fn);
      }
    });

    var vm = MakeVM(bhl, ts_fn);
    Assert.Equal(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [Fact]
  public void TestValListGetEnumerator()
  {
    var vm = new VM();

    var lst = ValList.New(vm);

    var dv1 = Val.NewInt(vm, 1);
    lst.Add(dv1);

    var dv2 = Val.NewInt(vm, 2);
    lst.Add(dv2);

    int c = 0;
    foreach(var tmp in lst) 
    {
      ++c;
      if(c == 1)
        Assert.Equal(tmp.num, dv1.num);
      else if(c == 2)
        Assert.Equal(tmp.num, dv2.num);
    }
    Assert.Equal(2, c);
  }

}
