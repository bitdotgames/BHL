using System;           
using System.IO;
using System.Collections.Generic;
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
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, c.ns.TMap("string", "int")) }) 
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
        @"[k, v] expected"
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
        @"[k, v] expected"
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
        @"[k, v] expected"
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
        @"[k, v] expected"
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
        @"incompatible types"
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

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);

    var vm = MakeVM(bhl, ts);
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
        @"incompatible types"
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
        @"incompatible types"
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
        @"incompatible types"
      );
    }
  }
}
