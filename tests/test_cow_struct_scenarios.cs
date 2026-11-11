using System;
using System.Collections.Generic;
using bhl;
using Xunit;

// Script-level behaviour of a native struct bound through ValHolder<T>. These are the shapes
// a script actually writes: copying, passing to functions, returning, storing in arrays, and
// mutating through each.
//
// The contract being pinned: a bound struct behaves like a C# struct. Assignment copies, and
// the copy is shallow - a reference field is shared between copies, exactly as 'a = b' does
// for a struct in C#.
public class TestCowStructScenarios : BHL_TestBase
{
  public class Cell
  {
    public int id;
  }

  public struct DamageSpec
  {
    public int DamageAmount;
    public int PosX;
    public Cell Cell;
  }

  VM Make(string bhl) => MakeVM(bhl, BindSpec);

  int Run(VM vm, string func) => (int)vm.Execute((FuncSymbolScript)vm.ResolveNamedByPath(func)).Pop().num;

  [Fact]
  public void TestFillAndReadBack()
  {
    var vm = Make(@"
    func int test()
    {
      DamageSpec d = new DamageSpec
      d.DamageAmount = 7
      d.PosX = 3
      return d.DamageAmount * 10 + d.PosX
    }
    ");

    Assert.Equal(73, Run(vm, "test"));
  }

  [Fact]
  public void TestAssignmentCopies()
  {
    var vm = Make(@"
    func int test()
    {
      DamageSpec a = new DamageSpec
      a.DamageAmount = 1

      DamageSpec b = a
      b.DamageAmount = 2

      //NOTE: mutating the original after the copy must not disturb the copy either
      a.DamageAmount = 3

      return a.DamageAmount * 10 + b.DamageAmount
    }
    ");

    Assert.Equal(32, Run(vm, "test"));
  }

  [Fact]
  public void TestPassingToAFunctionDoesNotMutateTheCaller()
  {
    var vm = Make(@"
    func Bump(DamageSpec d)
    {
      d.DamageAmount = 99
    }

    func int test()
    {
      DamageSpec a = new DamageSpec
      a.DamageAmount = 1
      Bump(a)
      return a.DamageAmount
    }
    ");

    Assert.Equal(1, Run(vm, "test"));
  }

  [Fact]
  public void TestReturningFromAFunction()
  {
    var vm = Make(@"
    func DamageSpec Make(int amount)
    {
      DamageSpec d = new DamageSpec
      d.DamageAmount = amount
      return d
    }

    func int test()
    {
      DamageSpec a = Make(5)
      DamageSpec b = Make(6)
      b.DamageAmount = b.DamageAmount + 1
      return a.DamageAmount * 10 + b.DamageAmount
    }
    ");

    Assert.Equal(57, Run(vm, "test"));
  }

  [Fact]
  public void TestStoredInAnArrayIndependentlyOfTheOriginal()
  {
    var vm = Make(@"
    func int test()
    {
      []DamageSpec all = []

      DamageSpec d = new DamageSpec
      for(int i = 0; i < 3; i++) {
        d.DamageAmount = i + 1
        all.Add(d)
      }

      //NOTE: each Add stored a copy, so later writes to d must not rewrite history
      int s = 0
      foreach(DamageSpec e in all) {
        s = s * 10 + e.DamageAmount
      }
      return s
    }
    ");

    Assert.Equal(123, Run(vm, "test"));
  }

  [Fact]
  public void TestMutatingAnArrayElementDoesNotTouchItsNeighbours()
  {
    var vm = Make(@"
    func int test()
    {
      []DamageSpec all = []

      DamageSpec d = new DamageSpec
      d.DamageAmount = 1
      all.Add(d)
      all.Add(d)

      DamageSpec first = all[0]
      first.DamageAmount = 9

      return all[0].DamageAmount * 10 + all[1].DamageAmount
    }
    ");

    Assert.Equal(11, Run(vm, "test"));
  }

  //NOTE: a C# struct copy is shallow - both copies point at the same Cell. Deep copying here
  //      would diverge from what the same assignment does on the native side
  [Fact]
  public void TestCopyIsShallowForReferenceFields()
  {
    var vm = Make(@"
    func int test()
    {
      DamageSpec a = new DamageSpec
      a.Cell = MakeCell(4)

      DamageSpec b = a
      b.DamageAmount = 1   //NOTE: forces a copy of the struct

      //NOTE: the struct was copied, the Cell reference inside it was not
      return a.Cell.id * 10 + b.Cell.id
    }
    ");

    Assert.Equal(44, Run(vm, "test"));
  }

  [Fact]
  public void TestLoopWritesStayInPlaceAndRecycle()
  {
    var vm = Make(@"
    func int test()
    {
      DamageSpec d = new DamageSpec
      int s = 0
      for(int i = 0; i < 50; i++) {
        d.DamageAmount = i
        d.PosX = i
        s = s + d.DamageAmount + d.PosX
      }
      return s
    }
    ");

    Run(vm, "test");

    //NOTE: steady state rents from the pool rather than allocating
    int after_first = vm.GetHolderPool<DamageSpec>().MissCount;
    for(int i = 0; i < 10; ++i)
      Run(vm, "test");

    Assert.Equal(after_first, vm.GetHolderPool<DamageSpec>().MissCount);
    Assert.True(vm.GetHolderPool<DamageSpec>().HitCount > 0);
  }

  void BindSpec(Types ts)
  {
    var cell_cl = new ClassSymbolNative(new Origin(), "Cell", typeof(Cell),
      delegate(VM.ExecState exec, ref Val v, IType type) { v.SetObj(new Cell(), type); }
    );
    ts.ns.Define(cell_cl);
    cell_cl.Define(new FieldSymbol(new Origin(), "id", Types.Int,
      delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld)
      {
        v.SetInt(((Cell)ctx.obj).id);
      },
      null
    ));
    cell_cl.Setup();

    var cl = new ClassSymbolNative(new Origin(), "DamageSpec", typeof(DamageSpec),
      delegate(VM.ExecState exec, ref Val v, IType type) { v.SetObj(ValHolder<DamageSpec>.New(exec.vm), type); }
    );
    ts.ns.Define(cl);

    DefineInt(cl, "DamageAmount",
      (ref DamageSpec d) => d.DamageAmount,
      (ref DamageSpec d, int n) => d.DamageAmount = n);

    DefineInt(cl, "PosX",
      (ref DamageSpec d) => d.PosX,
      (ref DamageSpec d, int n) => d.PosX = n);

    cl.Define(new FieldSymbol(new Origin(), "Cell", ts.T("Cell"),
      delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld)
      {
        v.SetObj(ValHolder<DamageSpec>.Ref(ctx).Cell, fld.type.Get());
      },
      delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld)
      {
        ValHolder<DamageSpec>.MutableRef(ref ctx).Cell = (Cell)v.obj;
      }
    ));
    cl.Setup();

    ts.ns.Define(new FuncSymbolNative(new Origin(), "MakeCell", ts.T("Cell"),
      delegate(VM.ExecState exec, FuncArgsInfo args_info)
      {
        int id = (int)exec.stack.Pop().num;
        exec.stack.Push(Val.NewObj(new Cell() { id = id }, ts.T("Cell").Get()));
        return null;
      },
      new FuncArgSymbol("id", Types.Int)
    ));
  }

  delegate int Getter(ref DamageSpec d);
  delegate void Setter(ref DamageSpec d, int n);

  void DefineInt(ClassSymbolNative cl, string name, Getter get, Setter set)
  {
    cl.Define(new FieldSymbol(new Origin(), name, Types.Int,
      delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld)
      {
        v.SetInt(get(ref ValHolder<DamageSpec>.Ref(ctx)));
      },
      delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld)
      {
        set(ref ValHolder<DamageSpec>.MutableRef(ref ctx), (int)v.num);
      }
    ));
  }
}
