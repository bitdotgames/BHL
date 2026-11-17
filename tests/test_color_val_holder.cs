using System;
using bhl;
using Xunit;

// The Color binding in test_shared.cs boxes: every value is a heap allocated Color object
// living in Val.obj. This is the same surface backed by ValHolder<T> over a struct instead -
// pooled, so a steady state script allocates nothing, and with value semantics.
//
// The scripts below are copied verbatim from the boxed tests they mirror, so a difference in
// the expected result would be a difference in behaviour, not in the test.
public class TestColorValHolder : BHL_TestBase
{
  public struct ColorStruct
  {
    public float r;
    public float g;
  }

  static ref ColorStruct Ref(Val v) => ref ValHolder<ColorStruct>.Ref(v);

  void BindColor(Types ts)
  {
    var cl = new ClassSymbolNative(new Origin(), "Color", typeof(ColorStruct),
      delegate(VM.ExecState exec, ref Val v, IType type)
      {
        v.SetObj(ValHolder<ColorStruct>.New(exec.vm), type);
      }
    );
    ts.ns.Define(cl);

    cl.Define(new FieldSymbol(new Origin(), "r", ts.T("float"),
      delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld) { v.SetFlt(Ref(ctx).r); },
      delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld)
      {
        ValHolder<ColorStruct>.MutableRef(ref ctx).r = (float)v.num;
      }
    ));

    cl.Define(new FieldSymbol(new Origin(), "g", ts.T("float"),
      delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld) { v.SetFlt(Ref(ctx).g); },
      delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld)
      {
        ValHolder<ColorStruct>.MutableRef(ref ctx).g = (float)v.num;
      }
    ));

    cl.Define(new FuncSymbolNative(new Origin(), "Add", ts.T("Color"),
      (VM.ExecState exec, FuncArgsInfo args_info) =>
      {
        float k = exec.stack.Pop();
        var self = exec.stack.Pop();
        var c = Ref(self);

        exec.stack.Push(ValHolder<ColorStruct>.NewVal(exec.vm, ts.T("Color").Get(),
          new ColorStruct() { r = c.r + k, g = c.g + k }));

        self._refc?.Release();
        return null;
      },
      new FuncArgSymbol("k", ts.T("float"))
    ));

    cl.Define(new FuncSymbolNative(new Origin(), "mult_summ", ts.T("float"),
      (VM.ExecState exec, FuncArgsInfo args_info) =>
      {
        double k = exec.stack.Pop();
        var self = exec.stack.Pop();
        var c = Ref(self);
        exec.stack.Push((c.r * k) + (c.g * k));

        self._refc?.Release();
        return null;
      },
      new FuncArgSymbol("k", ts.T("float"))
    ));

    ts.ns.Define(new FuncSymbolNative(new Origin(), "mkcolor", ts.T("Color"),
      (VM.ExecState exec, FuncArgsInfo args_info) =>
      {
        double r = exec.stack.Pop();
        exec.stack.Push(ValHolder<ColorStruct>.NewVal(exec.vm, ts.T("Color").Get(),
          new ColorStruct() { r = (float)r }));
        return null;
      },
      new FuncArgSymbol("r", ts.T("float"))
    ));

    //NOTE: the VM carrying variant - a pooled holder can't be built without one
    NativeListTypeSymbol.DefineOnceWithVMItems<ColorStruct>(
      ts.ns, "ArrayT_Color",
      (v) => Ref(v),
      (vm, itype, n) => ValHolder<ColorStruct>.NewVal(vm, itype.Get(), n),
      ts.T("Color")
    );

    cl.Setup();
  }

  VM Make(string bhl) => MakeVM(bhl, ts => BindColor(ts));

  // mirrors TestNativeClassAttributesAccess
  [Fact]
  public void TestAttributesAccess()
  {
    var vm = Make(@"
    func float test(float k)
    {
      Color c = new Color
      c.r = k*1
      c.g = k*100
      return c.r + c.g
    }
    ");

    double res = Execute(vm, "test", 2).Stack.Pop();
    Assert.Equal(202, res);
    CommonChecks(vm);
  }

  // mirrors TestNativeClassCallMember
  [Fact]
  public void TestCallMemberOfATemporary()
  {
    var vm = Make(@"
    func float test(float a)
    {
      return mkcolor(a).r
    }
    ");

    double res = Execute(vm, "test", 10).Stack.Pop();
    Assert.Equal(10, res);
    res = Execute(vm, "test", 20).Stack.Pop();
    Assert.Equal(20, res);
    CommonChecks(vm);
  }

  // mirrors TestReturnNativeInstance
  [Fact]
  public void TestReturnNativeInstance()
  {
    var vm = Make(@"
    func Color MakeColor(float g)
    {
      Color c = new Color
      c.g = g
      return c
    }

    func float test(float k)
    {
      return MakeColor(k).g + MakeColor(k).r
    }
    ");

    double res = Execute(vm, "test", 2).Stack.Pop();
    Assert.Equal(2, res);
    CommonChecks(vm);
  }

  // mirrors TestTmpNativeClassCallMethod
  [Fact]
  public void TestCallMethodOnATemporary()
  {
    var vm = Make(@"
    func Color get() {
      return mkcolor(1)
    }

    func test()
    {
      get().mult_summ(10)
    }
    ");

    Execute(vm, "test");
    CommonChecks(vm);
  }

  [Fact]
  public void TestMethodReturningANewValue()
  {
    var vm = Make(@"
    func float test()
    {
      Color c = new Color
      c.r = 1
      c.g = 2
      Color d = c.Add(10)
      return d.r*100 + d.g*10 + c.r
    }
    ");

    double res = Execute(vm, "test").Stack.Pop();
    Assert.Equal(1221, res);
    CommonChecks(vm);
  }

  // This is where the two bindings genuinely differ: the boxed Color has reference semantics,
  // so 'b = a' aliases. Through a holder it copies, like the C# struct it wraps.
  [Fact]
  public void TestAssignmentCopiesUnlikeTheBoxedBinding()
  {
    var vm = Make(@"
    func float test()
    {
      Color a = new Color
      a.r = 1

      Color b = a
      b.r = 2

      return a.r*10 + b.r
    }
    ");

    double res = Execute(vm, "test").Stack.Pop();
    Assert.Equal(12, res);
    CommonChecks(vm);
  }

  // A native list of holder backed structs: the conversion back to a Val needs the VM for the
  // pool, which is what DefineOnceWithVMItems exists for.
  [Fact]
  public void TestNativeArrayOfHolderBackedStructs()
  {
    var vm = Make(@"
    func float test()
    {
      ArrayT_Color cs = new ArrayT_Color

      Color c = new Color
      for(int i = 0; i < 3; i++) {
        c.r = (float)i + 1
        cs.Add(c)
      }

      //NOTE: Add stored a copy each time, so later writes to c didn't rewrite history
      float s = 0
      foreach(Color e in cs) {
        s = s*10 + e.r
      }

      //NOTE: and an element read back is a copy too
      Color first = cs[0]
      first.r = 9

      return s*10 + cs[0].r
    }
    ");

    double res = Execute(vm, "test").Stack.Pop();
    Assert.Equal(1231, res);
    CommonChecks(vm);
  }

  // The point of the exercise: boxing allocates a Color per value, a holder rents one.
  [Fact]
  public void TestSteadyStateRentsFromThePoolInsteadOfAllocating()
  {
    var vm = Make(@"
    func float test()
    {
      float s = 0
      for(int i = 0; i < 50; i++) {
        Color c = new Color
        c.r = (float)i
        c.g = (float)i * 2
        s = s + c.r + c.g
      }
      return s
    }
    ");

    Execute(vm, "test").Stack.Pop();

    int misses = vm.GetHolderPool<ColorStruct>().MissCount;
    for(int i = 0; i < 10; ++i)
      Execute(vm, "test").Stack.Pop();

    Assert.Equal(misses, vm.GetHolderPool<ColorStruct>().MissCount);
    Assert.True(vm.GetHolderPool<ColorStruct>().HitCount > 0);
    CommonChecks(vm);
  }
}
