using System;
using System.IO;
using bhl;
using Xunit;

public class TestVMInstance : BHL_TestBase
{
  //NOTE: mirrors TestReload.CompileModule - total_gvars_num etc. is only
  //      finalized after a ToStream/FromStream round trip
  ModuleDeclared CompileModule(string bhl, Types ts, string name = "test")
  {
    var proc = Parse(bhl, ts, throw_errors: true);
    var raw = new ModuleCompiler(proc.result).Compile();
    raw.name = name;

    var ms = new MemoryStream();
    raw.ToStream(ms);
    return ModuleDeclared.FromStream(ts, new MemoryStream(ms.GetBuffer()));
  }

  [Fact]
  public void TestNewInstanceHasDefaultFieldValues()
  {
    var ts = new Types();

    var decl = CompileModule(@"
    class Unit
    {
      int health
      string name

      func int get_health()
      {
        return this.health
      }

      func string get_name()
      {
        return this.name
      }
    }
    ", ts);
    var vm = new VM(ts);
    vm.LoadModule(new Module(decl));

    var unit_val = vm.NewInstance("Unit");

    var health_fiber = vm.CallMethod(ref unit_val, "get_health", new StackList<Val>());
    RunFiber(vm, health_fiber);
    Assert.Equal(0, health_fiber.Stack.Pop().num);

    var name_fiber = vm.CallMethod(ref unit_val, "get_name", new StackList<Val>());
    RunFiber(vm, name_fiber);
    Assert.Equal("", name_fiber.Stack.Pop().str);

    unit_val.ReleaseData();
  }

  [Fact]
  public void TestCallMethodInvokesMethodAndSeesFields()
  {
    var ts = new Types();

    var decl = CompileModule(@"
    class Unit
    {
      int health

      func int get_health()
      {
        return this.health
      }

      func void set_health(int h)
      {
        this.health = h
      }
    }
    ", ts);
    var vm = new VM(ts);
    vm.LoadModule(new Module(decl));

    var unit_val = vm.NewInstance("Unit");

    var set_fiber = vm.CallMethod(ref unit_val, "set_health", new StackList<Val>(Val.NewInt(42)));
    Assert.NotNull(set_fiber);
    RunFiber(vm, set_fiber);

    var get_fiber = vm.CallMethod(ref unit_val, "get_health", new StackList<Val>());
    Assert.NotNull(get_fiber);
    RunFiber(vm, get_fiber);
    Assert.Equal(42, get_fiber.Stack.Pop().num);

    unit_val.ReleaseData();
  }

  [Fact]
  public void TestCallMethodReturnsNullForMissingMethod()
  {
    var ts = new Types();

    var decl = CompileModule(@"
    class Unit
    {
      int health
    }
    ", ts);
    var vm = new VM(ts);
    vm.LoadModule(new Module(decl));

    var unit_val = vm.NewInstance("Unit");

    var fiber = vm.CallMethod(ref unit_val, "not_a_method", new StackList<Val>());
    Assert.Null(fiber);

    unit_val.ReleaseData();
  }

  [Fact]
  public void TestFindMethodThenCallMethodBySymbolMatchesCallByName()
  {
    var ts = new Types();

    var decl = CompileModule(@"
    class Unit
    {
      int health

      func int get_health()
      {
        return this.health
      }
    }
    ", ts);
    var vm = new VM(ts);
    vm.LoadModule(new Module(decl));

    var unit_val = vm.NewInstance("Unit");
    vm.SetFieldValue(ref unit_val, "health", Val.NewInt(7));

    var func_symb = vm.FindMethod(unit_val, "get_health");
    Assert.NotNull(func_symb);

    var fiber = vm.CallMethod(ref unit_val, func_symb, new StackList<Val>());
    RunFiber(vm, fiber);
    Assert.Equal(7, fiber.Stack.Pop().num);

    unit_val.ReleaseData();
  }

  [Fact]
  public void TestFindMethodReturnsNullForMissingMethod()
  {
    var ts = new Types();

    var decl = CompileModule(@"
    class Unit
    {
      int health
    }
    ", ts);
    var vm = new VM(ts);
    vm.LoadModule(new Module(decl));

    var unit_val = vm.NewInstance("Unit");

    Assert.Null(vm.FindMethod(unit_val, "not_a_method"));

    unit_val.ReleaseData();
  }

  [Fact]
  public void TestFindMethodResolvesToMostDerivedOverride()
  {
    var ts = new Types();

    var decl = CompileModule(@"
    class Foo
    {
      int a
      virtual func int getA()
      {
        return this.a
      }
    }
    class Bar : Foo
    {
      int new_a
      override func int getA()
      {
        return this.new_a
      }
    }
    ", ts);
    var vm = new VM(ts);
    vm.LoadModule(new Module(decl));

    var bar_val = vm.NewInstance("Bar");
    vm.SetFieldValue(ref bar_val, "a", Val.NewInt(1));
    vm.SetFieldValue(ref bar_val, "new_a", Val.NewInt(2));

    var func_symb = vm.FindMethod(bar_val, "getA");
    Assert.NotNull(func_symb);

    var result = vm.ExecuteMethod(ref bar_val, func_symb, new StackList<Val>());
    Assert.Equal(2, result.Pop().num);

    bar_val.ReleaseData();
  }

  [Fact]
  public void TestExecuteMethodRunsNonCoroMethodSynchronously()
  {
    var ts = new Types();

    var decl = CompileModule(@"
    class Unit
    {
      int health

      func int get_health()
      {
        return this.health
      }
    }
    ", ts);
    var vm = new VM(ts);
    vm.LoadModule(new Module(decl));

    var unit_val = vm.NewInstance("Unit");
    vm.SetFieldValue(ref unit_val, "health", Val.NewInt(5));

    var func_symb = vm.FindMethod(unit_val, "get_health");
    Assert.NotNull(func_symb);
    Assert.False(func_symb.attribs.HasFlag(FuncAttrib.Coro));

    var result = vm.ExecuteMethod(ref unit_val, func_symb, new StackList<Val>());
    Assert.Equal(5, result.Pop().num);

    unit_val.ReleaseData();
  }

  [Fact]
  public void TestFuncAttribCoroDistinguishesCoroutineMethods()
  {
    var ts = new Types();

    var decl = CompileModule(@"
    class Unit
    {
      func int plain()
      {
        return 1
      }

      coro func int suspends()
      {
        yield()
        return 2
      }
    }
    ", ts);
    var vm = new VM(ts);
    vm.LoadModule(new Module(decl));

    var unit_val = vm.NewInstance("Unit");

    var plain_symb = vm.FindMethod(unit_val, "plain");
    var coro_symb = vm.FindMethod(unit_val, "suspends");

    Assert.False(plain_symb.attribs.HasFlag(FuncAttrib.Coro));
    Assert.True(coro_symb.attribs.HasFlag(FuncAttrib.Coro));

    unit_val.ReleaseData();
  }

  [Fact]
  public void TestNewInstanceThenReloadThenMigrateThenCallMethodUsesNewCode()
  {
    var ts = new Types();

    var declV1 = CompileModule(@"
    class Unit
    {
      int health

      func int tick()
      {
        return 1
      }
    }
    ", ts);
    var vm = new VM(ts);
    vm.LoadModule(new Module(declV1));

    var unit_val = vm.NewInstance("Unit");

    var declV2 = CompileModule(@"
    class Unit
    {
      int health

      func int tick()
      {
        return 2
      }
    }
    ", ts);
    vm.Reload(new Module(declV2));
    vm.MigrateInstance(ref unit_val);

    var fiber = vm.CallMethod(ref unit_val, "tick", new StackList<Val>());
    RunFiber(vm, fiber);
    Assert.Equal(2, fiber.Stack.Pop().num);

    unit_val.ReleaseData();
  }

  [Fact]
  public void TestSetFieldValueThenGetFieldValueRoundTrips()
  {
    var ts = new Types();

    var decl = CompileModule(@"
    class Unit
    {
      int health
      string name
    }
    ", ts);
    var vm = new VM(ts);
    vm.LoadModule(new Module(decl));

    var unit_val = vm.NewInstance("Unit");

    vm.SetFieldValue(ref unit_val, "health", Val.NewInt(7));
    vm.SetFieldValue(ref unit_val, "name", Val.NewStr("Bob"));

    var health = vm.GetFieldValue(unit_val, "health");
    Assert.Equal(7, health.num);
    health.ReleaseData();

    var name = vm.GetFieldValue(unit_val, "name");
    Assert.Equal("Bob", name.str);
    name.ReleaseData();

    unit_val.ReleaseData();
  }

  [Fact]
  public void TestSetFieldValueVisibleToBHLMethod()
  {
    var ts = new Types();

    var decl = CompileModule(@"
    class Unit
    {
      int health

      func int get_health()
      {
        return this.health
      }
    }
    ", ts);
    var vm = new VM(ts);
    vm.LoadModule(new Module(decl));

    var unit_val = vm.NewInstance("Unit");
    vm.SetFieldValue(ref unit_val, "health", Val.NewInt(99));

    var fiber = vm.CallMethod(ref unit_val, "get_health", new StackList<Val>());
    RunFiber(vm, fiber);
    Assert.Equal(99, fiber.Stack.Pop().num);

    unit_val.ReleaseData();
  }

  static void RunFiber(VM vm, VM.Fiber fb)
  {
    const int LIMIT = 20;
    int c = 0;
    for(; c < LIMIT; ++c)
    {
      if(!vm.Tick())
        return;
    }
    throw new Exception("Too many iterations: " + c);
  }
}
