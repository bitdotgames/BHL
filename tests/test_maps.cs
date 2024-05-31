using System;           
using System.Text;
using bhl;

public class TestMaps : BHL_TestBase
{
  [IsTested()]
  public void TestSimpleDeclare()
  {
    string bhl = @"
    [string]int m = []
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseInit()
      .EmitThen(Opcodes.New, new int[] { TypeIdx(c, c.ns.TMap("string", "int")) }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
    ;

    AssertEqual(c, expected);
  }

  [IsTested()]
  public void TestCanBeNull()
  {
    {
      string bhl = @"
      func bool test() {
        [string]int m
        return m == null
      }
      ";
      var vm = MakeVM(bhl);
      AssertTrue(Execute(vm, "test").result.PopRelease().bval);
      CommonChecks(vm);
    }

    {
      string bhl = @"
      func bool test() {
        [string]int m = []
        return m == null
      }
      ";
      var vm = MakeVM(bhl);
      AssertFalse(Execute(vm, "test").result.PopRelease().bval);
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestCountEmpty()
  {
    string bhl = @"

    func int test() 
    {
      [string]int m = []
      return m.Count
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSimpleReadWrite()
  {
    string bhl = @"

    func int test() 
    {
      [string]int m = []
      m[""hey""] = 42
      return m[""hey""]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSimpleInitWithJson()
  {
    {
      string bhl = @"

      func int test() 
      {
        [string]int m = [[""hey"", 42]]
        return m[""hey""]
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }

    {
      string bhl = @"

      func int test() 
      {
        [string]int m = [[""hey"", 42], [""foo"", 14], [""hey"", 43]]
        return m[""hey""]
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(43, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }

    {
      string bhl = @"

      func int test() 
      {
        [string]int m = [[""hey"", 42], [""foo"", 14], [""hey"", 43]]
        return m[""foo""]
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(14, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestComplexInitWithJson()
  {
    string bhl = @"

    [string]int m = [[""hey"", 42], [""foo"", 14], [""hey"", 43]]

    func int test1() {
      return m[""hey""]
    }
    func int test2() {
      return m[""foo""]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(43, Execute(vm, "test1").result.PopRelease().num);
    AssertEqual(14, Execute(vm, "test2").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestInitErrorsWithJson()
  {
    {
      string bhl = @"
      [string]int m = [""hey""]
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        @"[k, v] expected",
       new PlaceAssert(bhl, @"
      [string]int m = [""hey""]
----------------------^"
       )
      );
    }

    {
      string bhl = @"
      [string]int m = [""hey"", 1]
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        @"[k, v] expected",
       new PlaceAssert(bhl, @"
      [string]int m = [""hey"", 1]
----------------------^"
       )
      );
    }

    {
      string bhl = @"
      [string]int m = [[""hey""]]
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        @"[k, v] expected",
       new PlaceAssert(bhl, @"
      [string]int m = [[""hey""]]
-----------------------^"
       )
      );
    }

    {
      string bhl = @"
      [string]int m = [[""hey"", 1], [""hey""]]
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        @"[k, v] expected",
       new PlaceAssert(bhl, @"
      [string]int m = [[""hey"", 1], [""hey""]]
-----------------------------------^" /*taking into account ""*/
       )
      );
    }

    {
      string bhl = @"
      [string]int m = [[1, ""hey""]]
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        @"incompatible types: 'string' and 'int'",
       new PlaceAssert(bhl, @"
      [string]int m = [[1, ""hey""]]
------------------------^"
       )
      );
    }
  }

  [IsTested()]
  public void TestCountSeveral()
  {
    string bhl = @"

    func int test() 
    {
      [string]int m = []
      m[""hey""] = 42
      m[""bar""] = 14
      return m.Count
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestWriteAtTheSameKey()
  {
    string bhl = @"

    func int test() 
    {
      [string]int m = []
      m[""hey""] = 42
      m[""hey""] = 14
      return m[""hey""]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(14, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestRemoveNonExisting()
  {
    string bhl = @"

    func int test() 
    {
      [string]int m = []
      m.Remove(""hey"")
      return m.Count
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestRemove()
  {
    string bhl = @"

    func int test() 
    {
      [string]int m = []
      m[""foo""] = 42
      m[""hey""] = 14
      m.Remove(""foo"")
      return m.Count
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestClear()
  {
    string bhl = @"

    func int test() 
    {
      [string]int m = []
      m[""hey""] = 42
      m[""bar""] = 14
      m.Clear()
      return m.Count
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestContains()
  {
    {
      string bhl = @"

      func bool test() 
      {
        [string]int m = []
        return m.Contains(""hey"")
      }
      ";

      var vm = MakeVM(bhl);
      AssertFalse(Execute(vm, "test").result.PopRelease().bval);
      CommonChecks(vm);
    }

    {
      string bhl = @"

      func bool test() 
      {
        [string]int m = []
        m[""hey""] = 42
        return m.Contains(""hey"")
      }
      ";

      var vm = MakeVM(bhl);
      AssertTrue(Execute(vm, "test").result.PopRelease().bval);
      CommonChecks(vm);
    }

    {
      string bhl = @"

      func bool test() 
      {
        [string]int m = []
        m[""hey""] = 42
        m.Remove(""hey"")
        return m.Contains(""hey"")
      }
      ";

      var vm = MakeVM(bhl);
      AssertFalse(Execute(vm, "test").result.PopRelease().bval);
      CommonChecks(vm);
    }

    {
      string bhl = @"

      func bool test() 
      {
        [string]int m = []
        m[""hey""] = 42
        m.Remove(""bar"")
        return m.Contains(""hey"")
      }
      ";

      var vm = MakeVM(bhl);
      AssertTrue(Execute(vm, "test").result.PopRelease().bval);
      CommonChecks(vm);
    }

    {
      string bhl = @"

      func bool test() 
      {
        [string]int m = []
        m[""hey""] = 42
        m.Clear()
        return m.Contains(""hey"")
      }
      ";

      var vm = MakeVM(bhl);
      AssertFalse(Execute(vm, "test").result.PopRelease().bval);
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestTryGetNoValue()
  {
    {
      string bhl = @"

      func bool,int test() 
      {
        [string]int m = []
        return m.TryGet(""bar"")
      }
      ";

      var vm = MakeVM(bhl);
      var result =  Execute(vm, "test").result;
      bool ok = result.PopRelease().bval;
      var num = result.PopRelease().num;
      AssertFalse(ok);
      AssertEqual(0, num);
      CommonChecks(vm);
    }

    {
      string bhl = @"

      func bool,int test() 
      {
        [string]int m = []
        m[""hey""] = 14
        return m.TryGet(""bar"")
      }
      ";

      var vm = MakeVM(bhl);
      var result =  Execute(vm, "test").result;
      bool ok = result.PopRelease().bval;
      var num = result.PopRelease().num;
      AssertFalse(ok);
      AssertEqual(0, num);
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestTryGetOk()
  {
    string bhl = @"

    func bool,int test() 
    {
      [string]int m = []
      m[""hey""] = 14
      m[""bar""] = 4
      return m.TryGet(""hey"")
    }
    ";

    var vm = MakeVM(bhl);
    var result =  Execute(vm, "test").result;
    bool ok = result.PopRelease().bval;
    var num = result.PopRelease().num;
    AssertTrue(ok);
    AssertEqual(14, num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTryGetInSubCall()
  {
    string bhl = @"

    func bool,int get() {
      [string]int m = []
      m[""hey""] = 10
      return m.TryGet(""hey"")
    }

    func int test() {
      bool ok,int v = get()
      if(ok){
        return v+1
      } else {
        return 0
      }
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(11, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSimpleForeach()
  {
    string bhl = @"
    func test() 
    {
      [string]int m = [[""hey"", 14], [""bar"", 4]]
      foreach(string k,int v in m) {
        trace(k + "":"" + (string)v + "";"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual(log.ToString(), "hey:14;bar:4;");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestForeachChecks()
  {
    {
      string bhl = @"
      func test() 
      {
        [string]int m
        foreach(int v in m) {
        }
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        @"incompatible types: '[]int' and '[string]int'",
       new PlaceAssert(bhl, @"
        foreach(int v in m) {
-------------------------^"
       )
      );
    }

    {
      string bhl = @"
      func test() 
      {
        []int m
        foreach(int k,int v in m) {
        }
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        @"incompatible types: '[int]int' and '[]int'",
       new PlaceAssert(bhl, @"
        foreach(int k,int v in m) {
-------------------------------^"
       )
      );
    }

    {
      string bhl = @"
      func test() 
      {
        [string]int m
        foreach(int k,string v in m) {
        }
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        @"incompatible types: '[int]string' and '[string]int'",
       new PlaceAssert(bhl, @"
        foreach(int k,string v in m) {
----------------------------------^"
       )
      );
    }
  }

  [IsTested()]
  public void TestVarInMap()
  {
    string bhl = @"
    func int test1() {
      [string]int m = []
      string s1 = ""hey""
      string s2 = ""wow""
      m[s1] = 10
      m[s2] = 20
      return m[s2]
    }

    class Foo {
    }

    func int test2() {
      [Foo]int m = []
      Foo f1 = {}
      Foo f2 = {}
      m[f1] = 10
      m[f2] = 20
      return m[f1]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(20, Execute(vm, "test1").result.PopRelease().num);
    AssertEqual(10, Execute(vm, "test2").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestEnumInMap()
  {
    string bhl = @"

    enum Enum {
      Bar = 1
      Foo = 2
    }

    func int test1() {
      [Enum]int m = []
      m[Enum.Bar] = 10
      m[Enum.Foo] = 20
      return m[Enum.Foo]
    }

    func int test2() {
      [Enum]int m = []
      m[Enum.Bar] = 10
      m[Enum.Foo] = 20
      return m[Enum.Bar]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(20, Execute(vm, "test1").result.PopRelease().num);
    AssertEqual(10, Execute(vm, "test2").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestMixMapsAndLists()
  {
    string bhl = @"
    enum Id {
      Bar = 10
      Foo = 20
    }

    [Id]int nums = []
    [int]int nums2 = []

    func test1() {
      nums[Id.Bar] = 2
      nums[Id.Foo] = 1

      []Id ids = []
      foreach(Id id, int n in nums) {
        //NOTE: interested only in a specific item
        if(n == 2) {
          ids.Add(id)
          trace(""add:"" + (string)ids[ids.Count-1] + "";"")
        }
      }

      for(int i=0;i<ids.Count;i++) {
        bool ok, int _ = nums.TryGet(ids[i])
        if(ok) {
          nums.Remove(ids[i])
        }
      }

      foreach(Id id, int n in nums) {
        trace(""map:"" + (string)id + ""->"" + (string)n + "";"")
      }
    }

    func test2() {
      nums2[10] = 2
      nums2[20] = 1

      []int ids = []
      foreach(int id, int n in nums2) {
        //NOTE: interested only in a specific item
        if(n == 2) {
          ids.Add(id)
          trace(""add:"" + (string)ids[ids.Count-1] + "";"")
        }
      }

      for(int i=0;i<ids.Count;i++) {
        bool ok, int _ = nums2.TryGet(ids[i])
        if(ok) {
          nums2.Remove(ids[i])
        }
      }

      foreach(int id, int n in nums2) {
        trace(""map:"" + (string)id + ""->"" + (string)n + "";"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
      BindLog(ts);
    });

    var vm = MakeVM(bhl, ts_fn);

    Execute(vm, "test1");
    AssertEqual(log.ToString(), "add:10;map:20->1;");
    log.Clear();

    Execute(vm, "test2");
    AssertEqual(log.ToString(), "add:10;map:20->1;");
    log.Clear();

    CommonChecks(vm);
  }

  [IsTested()]
  public void TestValueIsClonedOnceStoredInMap()
  {
    string bhl = @"
    func test()
    {
      [int]int m = []
      for(int i=0;i<3;i++) {
        m[i] = i
      }
      
      int local_var_garbage = 10
      foreach(int k,int v in m) {
        trace((string)k + ""->"" + (string)v + "";"")
      }

      for(int i=0;i<m.Count;i++) {
        m[i] = i
      }

      int local_var_garbage2 = 20
      foreach(int k,int v in m) {
        trace((string)k + ""->"" + (string)v + "";"")
      }
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test");
    AssertEqual(log.ToString(), "0->0;1->1;2->2;0->0;1->1;2->2;");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNonExistingClass()
  {
    {
      string bhl = @"

      func [Foo]int fetch() {
        return null
      }

      func test() {
        [Foo]int fs = fetch()
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "type 'Foo' not found",
        new PlaceAssert(bhl, @"
      func [Foo]int fetch() {
-----------^"
        )
      );
    }

    {
      string bhl = @"

      func [int]Foo fetch() {
        return null
      }

      func test() {
        [int]Foo fs = fetch()
      }
      ";

      AssertError<Exception>(
        delegate() { 
          Compile(bhl);
        },
        "type 'Foo' not found",
        new PlaceAssert(bhl, @"
      func [int]Foo fetch() {
-----------^"
        )
      );
    }
  }

}
