using System;
using System.Collections.Generic;
using bhl;
using Xunit;

public class TestEnum : BHL_TestBase
{
  void BindEnumState(Types ts)
  {
    var en = new EnumSymbolScript(new Origin(), "EnumState");
    ts.ns.Define(en);

    en.Define(new EnumItemSymbol(new Origin(), "SPAWNED",  10));
    en.Define(new EnumItemSymbol(new Origin(), "SPAWNED2", 20));
  }

  [Fact]
  public void TestBindEnum()
  {
    string bhl = @"
      
    func int test() 
    {
      return (int)EnumState.SPAWNED + (int)EnumState.SPAWNED2
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindEnumState(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 30);
    CommonChecks(vm);
  }

  public enum NativeEnum
  {
    Foo = 1,
    Bar = 2
  }

  [Fact]
  public void TestBindEnumNative()
  {
    string bhl = @"
      
    func int test() 
    {
      return (int)NativeEnum.Foo + (int)NativeEnum.Bar
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      var en = new EnumSymbolNative(new Origin(), "NativeEnum", typeof(NativeEnum));
      ts.ns.Define(en);

      en.Define(new EnumItemSymbol(new Origin(), "Foo",  (int)NativeEnum.Foo));
      en.Define(new EnumItemSymbol(new Origin(), "Bar", (int)NativeEnum.Bar));
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, 3);
    CommonChecks(vm);
  }

  [Fact]
  public void TestEqEnum()
  {
    string bhl = @"
      
    func bool test(EnumState state) 
    {
      return state == EnumState.SPAWNED2
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindEnumState(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test", Val.NewNum(vm, 20)).result.PopRelease().num;
    AssertEqual(res, 1);
    CommonChecks(vm);
  }

  [Fact]
  public void TestNotEqEnum()
  {
    string bhl = @"
      
    func bool test(EnumState state) 
    {
      return state != EnumState.SPAWNED
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindEnumState(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test", Val.NewNum(vm, 20)).result.PopRelease().num;
    AssertEqual(res, 1);
    CommonChecks(vm);
  }

  [Fact]
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

    var ts_fn = new Action<Types>((ts) => {
      BindEnumState(ts);
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

  [Fact]
  public void TestPassEnumToNativeFunc()
  {
    string bhl = @"
      
    func bool test() 
    {
      return StateIs(state : EnumState.SPAWNED2)
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindEnumState(ts);

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
    });

    var vm = MakeVM(bhl, ts_fn);
    var res = Execute(vm, "test").result.PopRelease().bval;
    AssertTrue(res);
  }

  [Fact]
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

  [Fact]
  public void TestUserNegativeEnum()
  {
    string bhl = @"

    enum Foo
    {
      A = 1
      B = -2
    }
      
    func int test() 
    {
      Foo f = Foo.B 
      return (int)f
    }
    ";

    var vm = MakeVM(bhl);
    var res = Execute(vm, "test").result.PopRelease().num;
    AssertEqual(res, -2);
    CommonChecks(vm);
  }

  [Fact]
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

  [Fact]
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

  [Fact]
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

  [Fact]
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

  [Fact]
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

  [Fact]
  public void TestImplicitCastEnumToInt()
  {
    SubTest(() => {
      string bhl = @"
      enum Foo {
        A = 1
        B = 2
      }

      func bool test() 
      {
        return Foo.B == 2
      }
      ";

      var vm = MakeVM(bhl);
      AssertTrue(Execute(vm, "test").result.PopRelease().bval);
      CommonChecks(vm);
    });

    SubTest(() => {
      string bhl = @"
      enum Foo {
        A = 1
        B = 2
      }

      func int test() 
      {
        int tmp = Foo.B
        return tmp
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    });

    SubTest(() => {
      string bhl = @"
      enum Foo {
        A = 1
        B = 2
      }

      func int get(int a) 
      {
        return a
      }

      func int test() 
      {
        return get(Foo.B)
      }
      ";

      var vm = MakeVM(bhl);
      AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
      CommonChecks(vm);
    });
  }

  [Fact]
  public void TestImplicitCastFromIntToEnumIsNotAllowed()
  {
    string bhl = @"
    enum Foo {
      A = 1
      B = 2
    }

    func test() {
      Foo foo = 1
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"incompatible types: 'Foo' and 'int'",
      new PlaceAssert(bhl, @"
      Foo foo = 1
----------^"
      )
    );
  }

  [Fact]
  public void TestImplicitCastFromFloatToEnumIsNotAllowed()
  {
    string bhl = @"
    enum Foo {
      A = 1
      B = 2
    }

    func test() {
      float b = Foo.B
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"incompatible types: 'float' and 'Foo'",
      new PlaceAssert(bhl, @"
      float b = Foo.B
------------^"
      )
    );
  }

}
