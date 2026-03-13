using bhl;
using Xunit;

public class TestBindings : BHL_TestBase
{
  [Fact]
  public void TestExample()
  {
    string bhl = @"
    import ""std/bind""

    func RegisterBindings(std.bind.Types types) {
      {
        var fn = std.bind.NewFuncSymbolNative(""Trace"", types.T(""void""),
         [
          std.bind.NewFuncArgSymbol(""str"", types.T(""string""))
         ]
        )
        types.ns.Define(fn)
      }

      {
        var fn = std.bind.NewFuncSymbolNative(""Rand"", types.T(""float""),
        []
        )
        types.ns.Define(fn)
      }

      {
        var cl = std.bind.NewClassSymbolNative(""Color"", null, true)
        types.ns.Define(cl)
      }
    }
    ";

    var new_types = new Types();

    var vm = MakeVM(bhl);
    vm.Execute("RegisterBindings", Val.NewObj(new_types, std.bind.TypesSymbol));

    var trace_fn = (FuncSymbolNative)new_types.ns.Resolve("Trace");
    Assert.Equal("Trace", trace_fn.name);
    Assert.Equal(Types.Void, trace_fn.GetReturnType());
    Assert.Equal("str", trace_fn.GetArg(0).name);

    var rand_fn = (FuncSymbolNative)new_types.ns.Resolve("Rand");
    Assert.Equal("Rand", rand_fn.name);
    Assert.Equal(Types.Float, rand_fn.GetReturnType());

    var color_cl = (ClassSymbolNative)new_types.ns.Resolve("Color");
    Assert.Equal("Color", color_cl.name);

    CommonChecks(vm);
  }
}
