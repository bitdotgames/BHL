using System;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using bhl;

public class TestArrays : BHL_TestBase
{
  [IsTested()]
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
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    var lst = fb.result.Pop();
    AssertEqual((lst.obj as IList<Val>).Count, 0);
    lst.Release();
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().str, "test");
    CommonChecks(vm);
  }

  [IsTested()]
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

  [IsTested()]
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
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertFalse(vm.Tick());
    var lst = fb.result.Pop();
    AssertEqual((lst.obj as IList<Val>).Count, 1);
    lst.Release();
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 2);
    CommonChecks(vm);
  }

  [IsTested()]
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

  [IsTested()]
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

  [IsTested()]
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
      AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
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
      AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
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
      AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
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
      AssertEqual(-1, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }
  }

  [IsTested()]
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
    AssertFalse(vm.Tick());
    var val = fb.result.Pop();
    var lst = val.obj as IList<Val>;
    AssertEqual(lst.Count, 2);
    AssertEqual(lst[0].str, "tst");
    AssertEqual(lst[1].str, "bar");
    val.Release();
    CommonChecks(vm);
  }

  [IsTested()]
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

  [IsTested()]
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
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 100);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 100);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(res, 2);
    CommonChecks(vm);
  }

  [IsTested()]
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
  
  [IsTested()]
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
    AssertEqual(42, Execute(vm, "test1").result.PopRelease().num);
    AssertEqual(42, Execute(vm, "test2").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
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
    AssertEqual(lst.Count, 2);
    AssertEqual(lst[0].str, "foo");
    AssertEqual(lst[1].str, "bar");

    AssertEqual(vm.vlsts_pool.MissCount, 1);
    AssertEqual(vm.vlsts_pool.IdleCount, 0);
    
    res.Release();

    CommonChecks(vm);
  }

  [IsTested()]
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

    AssertEqual(vm.vlsts_pool.MissCount, 2);
    CommonChecks(vm);
  }

  [IsTested()]
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

  [IsTested()]
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
      AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
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
      AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
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
      AssertEqual(-1, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }
  }

  [IsTested()]
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
    AssertEqual(res, 10);
    CommonChecks(vm);
  }

  [IsTested()]
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

  [IsTested()]
  public void TestNativeListInheritance()
  {
    string bhl = @"
    func test() 
    {
      []int ns = new List_int
      ns.Add(100)
      ns.RemoveAt(0)
      ns.Add(200)
    }
    ";

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
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    CommonChecks(vm);
  }
  
  [IsTested()]
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
    
    AssertTrue(ArrayInts1.Equals(ArrayInts2));
    AssertFalse(ArrayString.Equals(ArrayInts1));
  }
  
  [IsTested()]
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
    
    AssertTrue(ArrayInts1.Equals(ArrayInts2));
    AssertFalse(ArrayString.Equals(ArrayInts1));

  }
}
