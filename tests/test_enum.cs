using System;
using System.Collections.Generic;
using bhl;

public class TestEnum : BHL_TestBase
{
  [IsTested()]
  public void TestBindEnum()
  {
    string bhl = @"
      
    func int test() 
    {
      return (int)EnumState.SPAWNED + (int)EnumState.SPAWNED2
    }
    ";

    var ts_fn = new Func<Types>(() => {
      var ts = new Types();
      BindEnum(ts);
      return ts;
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 30);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestEqEnum()
  {
    string bhl = @"
      
    func bool test(EnumState state) 
    {
      return state == EnumState.SPAWNED2
    }
    ";

    var ts_fn = new Func<Types>(() => {
      var ts = new Types();
      BindEnum(ts);
      return ts;
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test", Val.NewNum(vm, 20)).result.PopRelease().num;
    AssertEqual(res, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNotEqEnum()
  {
    string bhl = @"
      
    func bool test(EnumState state) 
    {
      return state != EnumState.SPAWNED
    }
    ";

    var ts_fn = new Func<Types>(() => {
      var ts = new Types();
      BindEnum(ts);
      return ts;
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test", Val.NewNum(vm, 20)).result.PopRelease().num;
    AssertEqual(res, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestEnumArray()
  {
    string bhl = @"
      
    func []EnumState test() 
    {
      []EnumState arr = new []EnumState
      arr.Add(EnumState.SPAWNED2)
      arr.Add(EnumState.SPAWNED)
      return arr
    }
    ";

    var ts_fn = new Func<Types>(() => {
      var ts = new Types();
      BindEnum(ts);
      return ts;
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test").result.Pop();
    var lst = res.obj as IList<Val>;
    AssertEqual(lst.Count, 2);
    AssertEqual(lst[0].num, 20);
    AssertEqual(lst[1].num, 10);
    res.Release();
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassEnumToNativeFunc()
  {
    string bhl = @"
      
    func bool test() 
    {
      return StateIs(state : EnumState.SPAWNED2)
    }
    ";

    var ts_fn = new Func<Types>(() => {
      var ts = new Types();

      BindEnum(ts);

      {
        var fn = new FuncSymbolNative(new Origin(), "StateIs", ts.T("bool"),
            delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            var n = stack.PopRelease().num;
            stack.Push(Val.NewBool(frm.vm, n == 20));
            return null;
          },
          new FuncArgSymbol("state", ts.T("EnumState"))
          );

        ts.ns.Define(fn);
      }
      return ts;
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test").result.PopRelease().bval;
    AssertTrue(res);
  }

  [IsTested()]
  public void TestUserEnum()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
    }
      
    func int test() 
    {
      Foo f = Foo.B 
      return (int)f
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserEnumOrderIrrelevant()
  {
    string bhl = @"

    func int test() {
      return (int)foo()
    }

    func Foo foo() {
      return Foo.B
    }

    enum Foo
    {
      A = 1
      B = 2
    }
      
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 2);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserEnumWithDuplicateKey()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
      A = 10
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"duplicate key 'A'",
      new PlaceAssert(bhl, @"
      A = 10
------^"
      )
    );
  }

  [IsTested()]
  public void TestUserEnumWithDuplicateValue()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
      C = 1
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"duplicate value '1'",
      new PlaceAssert(bhl, @"
      C = 1
----------^"
      )
    );
  }

  [IsTested()]
  public void TestUserEnumConflictsWithAnotherEnum()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
    }

    enum Foo
    {
      C = 3
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"already defined symbol 'Foo'",
      new PlaceAssert(bhl, @"
    enum Foo
---------^"
      )
    );
  }

  [IsTested()]
  public void TestUserEnumConflictsWithClass()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = 2
    }

    class Foo
    {
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"already defined symbol 'Foo'",
      new PlaceAssert(bhl, @"
    class Foo
----------^"
      )
    );
  }
}
