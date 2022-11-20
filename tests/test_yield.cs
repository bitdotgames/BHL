using bhl;

public class TestYield : BHL_TestBase
{
  [IsTested()]
  public void TestBasicYield()
  {
    string bhl = @"
    func test()
    {
      yield()
    }
    ";

    var ts = new Types();
    var vm = MakeVM(bhl, ts);
    vm.Start("test");
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestYieldSeveralTimesAndReturnValue()
  {
    string bhl = @"
    func int test()
    {
      yield()
      int a = 1
      yield()
      return a
    }
    ";

    var ts = new Types();

    var vm = MakeVM(bhl, ts);
    var fb = vm.Start("test");
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(fb.result.PopRelease().num, 1);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestYieldInParal()
  {
    string bhl = @"

    func int test() 
    {
      int i = 0
      paral {
        while(i < 3) { yield() }
        while(true) {
          i = i + 1
          yield()
        }
      }
      return i
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");

    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    AssertEqual(3, fb.result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestYieldWhileInParal()
  {
    string bhl = @"

    func int test() 
    {
      int i = 0
      paral {
        yield while(i < 3)
        while(true) {
          i = i + 1
          yield()
        }
      }
      return i
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);
    var fb = vm.Start("test");

    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertTrue(vm.Tick());
    AssertFalse(vm.Tick());
    var val = fb.result.PopRelease();
    AssertEqual(3, val.num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSeveralYieldWhilesInParal()
  {
    string bhl = @"

    func int test() 
    {
      int i = 0
      paral {
        yield while(i < 5)
        while(true) {
          yield while(i < 7)
        }
        while(true) {
          i = i + 1
          yield()
        }
      }
      return i
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(5, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestYieldWhileBugInParal()
  {
    string bhl = @"
    func Foo() {
      suspend()
    }

    func test() {
      paral {
        yield while(true)

        while(true) {
          yield while(false)
          Foo()
        }
      }
    }
    ";

    var vm = MakeVM(bhl);
    var fb = vm.Start("test");
    for(int i=0;i<5;++i)
      AssertTrue(vm.Tick());
    //...will be running forever, well, we assume that :)
    vm.Stop(fb);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSuspend()
  {
    string bhl = @"
    func test()
    {
      yield suspend()
    }
    ";

    var ts = new Types();
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 /*args info*/ })
      .EmitThen(Opcodes.CallNative, new int[] { ts.nfunc_index.IndexOf("suspend"), 0 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    var fb = vm.Start("test");
    for(int i=0;i<99;i++)
      AssertTrue(vm.Tick());
    vm.Stop(fb);
    CommonChecks(vm);
  }
}
