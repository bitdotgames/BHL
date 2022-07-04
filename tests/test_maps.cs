using System;           
using System.IO;
using System.Collections.Generic;
using bhl;

public class TestMaps : BHL_TestBase
{
  [IsTested()]
  public void TestSimpleDeclare()
  {
    string bhl = @"
    [string]int m = {}
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
  public void TestCountEmpty()
  {
    string bhl = @"

    func int test() 
    {
      [string]int m = {}
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
      [string]int m = {}
      m[""hey""] = 42
      return m[""hey""]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCountSeveral()
  {
    string bhl = @"

    func int test() 
    {
      [string]int m = {}
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
      [string]int m = {}
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
      [string]int m = {}
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
      [string]int m = {}
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
      [string]int m = {}
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
        [string]int m = {}
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
        [string]int m = {}
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
        [string]int m = {}
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
        [string]int m = {}
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
        [string]int m = {}
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
        [string]int m = {}
        return m.TryGet(""bar"")
      }
      ";

      var vm = MakeVM(bhl);
      var result =  Execute(vm, "test").result;
      var num = result.PopRelease().num;
      bool ok = result.PopRelease().bval;
      AssertFalse(ok);
      AssertEqual(0, num);
      CommonChecks(vm);
    }

    {
      string bhl = @"

      func bool,int test() 
      {
        [string]int m = {}
        m[""hey""] = 14
        return m.TryGet(""bar"")
      }
      ";

      var vm = MakeVM(bhl);
      var result =  Execute(vm, "test").result;
      var num = result.PopRelease().num;
      bool ok = result.PopRelease().bval;
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
      [string]int m = {}
      m[""hey""] = 14
      m[""bar""] = 4
      return m.TryGet(""hey"")
    }
    ";

    var vm = MakeVM(bhl);
    var result =  Execute(vm, "test").result;
    var num = result.PopRelease().num;
    bool ok = result.PopRelease().bval;
    AssertTrue(ok);
    AssertEqual(14, num);
    CommonChecks(vm);
  }

}
