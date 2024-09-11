using System;
using System.Text;
using bhl;

public class TestInitializer : BHL_TestBase
{
  [IsTested()]
  public void TestJsonInitForUserClass()
  {
    string bhl = @"

    class Foo { 
      int Int
      float Flt
      string Str
    }
      
    func test() 
    {
      Foo f = {Int: 10, Flt: 14.2, Str: ""Hey""}
      trace((string)f.Int + "";"" + (string)f.Flt + "";"" + f.Str)
    }
    ";

    var log = new StringBuilder();
    FuncSymbolNative fn = null;
    var ts_fn = new Action<Types>((ts) => {
      fn = BindTrace(ts, log);
    });

    var c = Compile(bhl, ts_fn);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/, 73 /*exit offset*/})
      .EmitThen(Opcodes.New, new int[] { TypeIdx(c, c.ns.T("Foo")) }) 
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 14.2) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 2 })
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 0 })
      .EmitThen(Opcodes.TypeCast, new int[] { TypeIdx(c, c.ns.T("string")), 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 1 })
      .EmitThen(Opcodes.TypeCast, new int[] { TypeIdx(c, c.ns.T("string")), 1 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 2 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.CallGlobNative, new int[] { c.ts.module.nfunc_index.IndexOf(fn), 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts_fn);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("10;14.2;Hey", log.ToString().Replace(',', '.')/*locale issues*/);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonEmptyCtor()
  {
    string bhl = @"
    func float test()
    {
      Color c = {}
      return c.r + c.g
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonPartialCtor()
  {
    string bhl = @"
    func float test()
    {
      Color c = {g: 10}
      return c.r + c.g
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonFullCtor()
  {
    string bhl = @"
    func float test()
    {
      Color c = {r: 1, g: 10}
      return c.r + c.g
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(11, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonCtorNotExpectedMember()
  {
    string bhl = @"
    func test()
    {
      Color c = {b: 10}
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts_fn);
      },
      @"no such attribute 'b' in class 'Color",
      new PlaceAssert(bhl, @"
      Color c = {b: 10}
-----------------^"
      )
    );
  }

  [IsTested()]
  public void TestJsonCtorBadType()
  {
    string bhl = @"
    func test()
    {
      Color c = {r: ""what""}
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts_fn);
      },
      "incompatible types: 'float' and 'string'",
      new PlaceAssert(bhl, @"
      Color c = {r: ""what""}
--------------------^"
      )
    );
  }

  [IsTested()]
  public void TestJsonExplicitEmptyClass()
  {
    string bhl = @"
      
    func float test() 
    {
      ColorAlpha c = new ColorAlpha {}
      return c.r + c.g + c.a
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColorAlpha(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonExplicitClass()
  {
    string bhl = @"
      
    func float test() 
    {
      ColorAlpha c = new ColorAlpha {a: 1, g: 10, r: 100}
      return c.r + c.g + c.a
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColorAlpha(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonExplicitSubClass()
  {
    string bhl = @"
      
    func float test() 
    {
      Color c = new ColorAlpha {a: 1, g: 10, r: 100}
      ColorAlpha ca = (ColorAlpha)c
      return ca.r + ca.g + ca.a
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColorAlpha(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonExplicitSubClassNotCompatible()
  {
    string bhl = @"
      
    func test() 
    {
      ColorAlpha c = new Color {g: 10, r: 100}
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColorAlpha(ts);
    });

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts_fn);
      },
      "incompatible types: 'ColorAlpha' and 'Color'",
      new PlaceAssert(bhl, @"
      ColorAlpha c = new Color {g: 10, r: 100}
-----------------^"
      )
    );
  }

  [IsTested()]
  public void TestJsonReturnObject()
  {
    string bhl = @"

    func Color make()
    {
      return {r: 42}
    }
      
    func float test() 
    {
      Color c = make()
      return c.r
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }
  
  [IsTested()]
  public void TestJsonReturnObjectNewLine()
  {
    string bhl = @"

    func Color make()
    {
      return new Color 
        {r: 42}
    }
      
    func float test() 
    {
      Color c = make()
      return c.r
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(42, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonExplicitNoSuchClass()
  {
    string bhl = @"
      
    func test() 
    {
      any c = new Foo {}
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"type 'Foo' not found",
      new PlaceAssert(bhl, @"
      any c = new Foo {}
------------------^"
      )
    );
  }

  [IsTested()]
  public void TestJsonArrInitForUserClass()
  {
    string bhl = @"

    class Foo { 
      int Int
      float Flt
      string Str
    }
      
    func test() 
    {
      []Foo fs = [{Int: 10, Flt: 14.2, Str: ""Hey""}]
      Foo f = fs[0]
      trace((string)f.Int + "";"" + (string)f.Flt + "";"" + f.Str)
    }
    ";

    var log = new StringBuilder();
    FuncSymbolNative fn = null;
    var ts_fn = new Action<Types>((ts) => {
      fn = BindTrace(ts, log);
      BindBar(ts);
    });

    var c = Compile(bhl, ts_fn);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 2 + 1 /*args info*/, 87 /*exit offset*/})
      .EmitThen(Opcodes.New, new int[] { TypeIdx(c, c.ns.TArr("Foo")) }) 
      .EmitThen(Opcodes.New, new int[] { TypeIdx(c, c.ns.T( "Foo")) }) 
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 14.2) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 2 })
      .EmitThen(Opcodes.ArrAddInplace)
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .EmitThen(Opcodes.ArrIdx)
      .EmitThen(Opcodes.SetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetAttr, new int[] { 0 })
      .EmitThen(Opcodes.TypeCast, new int[] { TypeIdx(c, c.ns.T("string")), 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetAttr, new int[] { 1 })
      .EmitThen(Opcodes.TypeCast, new int[] { TypeIdx(c, c.ns.T("string")), 1 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetAttr, new int[] { 2 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.CallGlobNative, new int[] { c.ts.module.nfunc_index.IndexOf(fn), 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts_fn);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("10;14.2;Hey", log.ToString().Replace(',', '.')/*locale issues*/);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrInitForUserClassMultiple()
  {
    string bhl = @"

    class Foo { 
      int Int
      float Flt
      string Str
    }
      
    func test() 
    {
      []Foo fs = [{Int: 10, Flt: 14.2, Str: ""Hey""},{Flt: 15.1, Int: 2, Str: ""Foo""}]
      trace((string)fs[1].Int + "";"" + (string)fs[1].Flt + "";"" + fs[1].Str + ""-"" + 
           (string)fs[0].Int + "";"" + (string)fs[0].Flt + "";"" + fs[0].Str)
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
    });

    var vm = MakeVM(bhl, ts_fn);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("2;15.1;Foo-10;14.2;Hey", log.ToString().Replace(',', '.')/*locale issues*/);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonEmptyArrCtor()
  {
    string bhl = @"
    func int test()
    {
      []int a = []
      return a.Count 
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrCtor()
  {
    string bhl = @"
    func int test()
    {
      []int a = [1,2,3]
      return a[0] + a[1] + a[2]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(6, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrComplexCtor()
  {
    string bhl = @"
    func float test()
    {
      []Color cs = [{r:10, g:100}, {g:1000, r:1}]
      return cs[0].r + cs[0].g + cs[1].r + cs[1].g
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(1111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonDefaultEmptyArg()
  {
    string bhl = @"
    func float foo(Color c = {})
    {
      return c.r
    }

    func float test()
    {
      return foo()
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonDefaultEmptyArgWithOtherDefaultArgs()
  {
    string bhl = @"
    func float foo(Color c = {}, float a = 10)
    {
      return c.r
    }

    func float test()
    {
      return foo(a : 20)
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonDefaultArgWithOtherDefaultArgs()
  {
    string bhl = @"
    func float foo(Color c = {r:20}, float a = 10)
    {
      return c.r + a
    }

    func float test()
    {
      return foo(a : 20)
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(40, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonDefaultArgIncompatibleType()
  {
    string bhl = @"
    func float foo(ColorAlpha c = new Color{r:20})
    {
      return c.r
    }

    func float test()
    {
      return foo()
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColorAlpha(ts);
    });

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts_fn);
      },
      "incompatible types: 'ColorAlpha' and 'Color'",
      new PlaceAssert(bhl, @"
    func float foo(ColorAlpha c = new Color{r:20})
--------------------------------^"
      )
    );
  }

  [IsTested()]
  public void TestJsonArgTypeMismatch()
  {
    string bhl = @"
    func float foo(float a = 10)
    {
      return a
    }

    func float test()
    {
      return foo(a : {})
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"can't be specified with {..}",
      new PlaceAssert(bhl, @"
      return foo(a : {})
---------------------^"
      )
    );
  }

  [IsTested()]
  public void TestJsonDefaultArg()
  {
    string bhl = @"
    func float foo(Color c = {r:10})
    {
      return c.r
    }

    func float test()
    {
      return foo()
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrDefaultArg()
  {
    string bhl = @"
    func float foo([]Color c = [{r:10}])
    {
      return c[0].r
    }

    func float test()
    {
      return foo()
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrEmptyDefaultArg()
  {
    string bhl = @"
    func int foo([]Color c = [])
    {
      return c.Count
    }

    func int test()
    {
      return foo()
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonObjReAssign()
  {
    string bhl = @"
    func float test()
    {
      Color c = {r: 1}
      c = {g:10}
      return c.r + c.g
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrayReAssign()
  {
    string bhl = @"
    func float test()
    {
      []float b = [1]
      b = [2,3]
      return b[0] + b[1]
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColor(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(5, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrayExplicit()
  {
    string bhl = @"
      
    func float test() 
    {
      []Color cs = [new ColorAlpha {a:4}, {g:10}, new Color {r:100}]
      ColorAlpha ca = (ColorAlpha)cs[0]
      return ca.a + cs[1].g + cs[2].r
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColorAlpha(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(114, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }
  
  [IsTested()]
  public void TestJsonArrayInitializer()
  {
    string bhl = @"
    func int test() 
    {
      var ins = new []int [1, 10, 100]

      return ins[0] + ins[1] + ins[2]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }
  
  [IsTested()]
  public void TestComplexJsonArrayInitializer()
  {
    string bhl = @"
      
    func float test() 
    {
      var cs = new []Color [new ColorAlpha {a:4}, {g:10}, new Color {r:100}]
      ColorAlpha ca = (ColorAlpha)cs[0]
      return ca.a + cs[1].g + cs[2].r
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColorAlpha(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(114, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }
  
  [IsTested()]
  public void TestJsonArrayNewLineNotAllowed()
  {
    string bhl = @"
      
    func []int test() 
    {
      return new []int 
       [1, 2]
    }
    ";

    AssertError<Exception>(
      delegate() { Compile(bhl); },
      "no viable alternative at input",
      new PlaceAssert(bhl, @"
       [1, 2]
-------^"
      )
    );
  }

  [IsTested()]
  public void TestJsonArrayReturn()
  {
    string bhl = @"

    func []Color make()
    {
      return [new ColorAlpha {a:4}, {g:10}, new Color {r:100}]
    }
      
    func float test() 
    {
      []Color cs = make()
      ColorAlpha ca = (ColorAlpha)cs[0]
      return ca.a + cs[1].g + cs[2].r
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColorAlpha(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(114, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrayExplicitAsArg()
  {
    string bhl = @"

    func float foo([]Color cs)
    {
      ColorAlpha ca = (ColorAlpha)cs[0]
      return ca.a + cs[1].g + cs[2].r
    }
      
    func float test() 
    {
      return foo([new ColorAlpha {a:4}, {g:10}, new Color {r:100}])
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColorAlpha(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(114, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrayExplicitSubClass()
  {
    string bhl = @"
      
    func float test() 
    {
      []Color cs = [{r:1,g:2}, new ColorAlpha {g: 10, r: 100, a:2}]
      ColorAlpha c = (ColorAlpha)cs[1]
      return c.r + c.g + c.a
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindColorAlpha(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(112, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonArrayExplicitSubClassNotCompatible()
  {
    string bhl = @"
      
    func test() 
    {
      []ColorAlpha c = [{r:1,g:2,a:100}, new Color {g: 10, r: 100}]
    }
    ";

    
    var ts_fn = new Action<Types>((ts) => {
      BindColorAlpha(ts);
    });

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts_fn);
      },
      "incompatible types: 'ColorAlpha' and 'Color'",
      new PlaceAssert(bhl, @"
      []ColorAlpha c = [{r:1,g:2,a:100}, new Color {g: 10, r: 100}]
-----------------------------------------^"
      )
    );
  }

  [IsTested()]
  public void TestJsonMasterStructWithClass()
  {
    string bhl = @"
      
    func string test() 
    {
      MasterStruct n = {
        child : {str : ""hey""},
        child2 : {str : ""hey2""}
      }
      return n.child2.str
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindMasterStruct(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual("hey2", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonMasterStructWithStruct()
  {
    string bhl = @"
      
    func int test() 
    {
      MasterStruct n = {
        child_struct : {n: 1},
        child_struct2 : {n: 2}
      }
      return n.child_struct2.n
    }
    ";

    var ts_fn = new Action<Types>((ts) => {
      BindMasterStruct(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonFuncArg()
  {
    string bhl = @"
    func test(float b) 
    {
      Foo f = PassthruFoo({hey:142, colors:[{r:2}, {g:3}, {g:b}]})
      trace((string)f.hey + (string)f.colors.Count + (string)f.colors[0].r + (string)f.colors[1].g + (string)f.colors[2].g)
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
      BindColor(ts);
      BindFoo(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test", Val.NewNum(vm, 42));
    AssertEqual("14232342", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestJsonFuncArgChainCall()
  {
    string bhl = @"
    func test(float b) 
    {
      trace((string)PassthruFoo({hey:142, colors:[{r:2}, {g:3}, {g:b}]}).colors.Count)
    }
    ";

    var log = new StringBuilder();
    var ts_fn = new Action<Types>((ts) => {
      BindTrace(ts, log);
      BindColor(ts);
      BindFoo(ts);
    });

    var vm = MakeVM(bhl, ts_fn);
    Execute(vm, "test", Val.NewNum(vm, 42));
    AssertEqual("3", log.ToString());
    CommonChecks(vm);
  }
  
  [IsTested()]
  public void TestJsonArrInitForNativeClass()
  {
    string bhl = @"
    func test() 
    {
      []Bar bs = [{Int: 10, Flt: 14.5, Str: ""Hey""}]
      Bar b = bs[0]
      trace((string)b.Int + "";"" + (string)b.Flt + "";"" + b.Str)
    }
    ";

    var log = new StringBuilder();
    FuncSymbolNative fn = null;
    var ts_fn = new Action<Types>((ts) => {
      fn = BindTrace(ts, log);
      BindBar(ts);
    });

    var c = Compile(bhl, ts_fn);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 2 + 1 /*args info*/, 87 /*exit offset*/})
      .EmitThen(Opcodes.New, new int[] { TypeIdx(c, c.ns.TArr("Bar")) }) 
      .EmitThen(Opcodes.New, new int[] { TypeIdx(c, c.ns.T("Bar")) }) 
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 14.5) })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .EmitThen(Opcodes.SetAttrInplace, new int[] { 2 })
      .EmitThen(Opcodes.ArrAddInplace)
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 0) })
      .EmitThen(Opcodes.ArrIdx)
      .EmitThen(Opcodes.SetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetAttr, new int[] { 0 })
      .EmitThen(Opcodes.TypeCast, new int[] { TypeIdx(c, c.ns.T("string")), 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetAttr, new int[] { 1 })
      .EmitThen(Opcodes.TypeCast, new int[] { TypeIdx(c, c.ns.T("string")), 1 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 1 })
      .EmitThen(Opcodes.GetAttr, new int[] { 2 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.CallGlobNative, new int[] { c.ts.module.nfunc_index.IndexOf(fn), 1 })
      .EmitThen(Opcodes.ExitFrame)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts_fn);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("10;14.5;Hey", log.ToString().Replace(',', '.')/*locale issues*/);
    CommonChecks(vm);
  }
  
  [IsTested()]
  public void TestJsonMapInitializer()
  {
    string bhl = @"
    func int test() 
    {
      var ins = new [string]int [[""a"", 1], [""b"", 10], [""c"", 100]]

      return ins[""a""] + ins[""b""] + ins[""c""]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

}
