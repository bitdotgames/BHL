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
        var fn = std.bind.NewFuncSymbolNative(""Wait"", types.T(""void""),
         [
          std.bind.NewFuncArgSymbol(""sec"", types.T(""float""))
         ],
         is_coro: true
        )
        types.ns.Define(fn)
      }

      {
        var cl = std.bind.NewClassSymbolNative(""Color"", null, true)
        types.ns.Define(cl)

        cl.Define(std.bind.NewFieldSymbol(""r"", types.T(""float""), true, true))
        {
          var fn = std.bind.NewFuncSymbolNative(""Add"", types.T(""Color""),
            [
              std.bind.NewFuncArgSymbol(""other"", types.T(""Color""))
            ]
          )
          cl.Define(fn)
        }
        {
          var fn = std.bind.NewFuncSymbolNative(""Make"", types.T(""void""),
            [],
            is_coro: true, is_static: true
          )
          cl.Define(fn)
        }

        cl.Setup()
      }

      {
        var en = std.bind.NewEnumSymbolNative(""ModeType"")

        en.DefineItem(""DEFAULT"", 0)
        en.DefineItem(""BATTLE"", 1)

        types.ns.Define(en);
      }

      {
        var cl = std.bind.NewNativeListTypeSymbol(""List_Color"", types.T(""Color""))
        types.ns.Define(cl)
      }

      types.SetupType(""List_Color"")
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

    var wait_fn = (FuncSymbolNative)new_types.ns.Resolve("Wait");
    Assert.Equal("Wait", wait_fn.name);
    Assert.Equal(Types.Void, wait_fn.GetReturnType());
    Assert.Equal("sec", wait_fn.GetArg(0).name);
    Assert.Equal(FuncAttrib.Coro, wait_fn.attribs);

    var color_cl = (ClassSymbolNative)new_types.ns.Resolve("Color");
    Assert.Equal("Color", color_cl.name);
    var r_fld = (FieldSymbol)color_cl.Resolve("r");
    Assert.Equal("r", r_fld.name);
    Assert.Equal(Types.Float, r_fld.GetIType());
    var add_fn = (FuncSymbolNative)color_cl.Resolve("Add");
    Assert.Equal(color_cl, add_fn.GetReturnType());
    var maker_fn = (FuncSymbolNative)color_cl.Resolve("Make");
    Assert.Equal(FuncAttrib.Coro | FuncAttrib.Static, maker_fn.attribs);
    Assert.Equal(Types.Void, maker_fn.GetReturnType());

    var mode_enum = (EnumSymbolNative)new_types.ns.Resolve("ModeType");
    Assert.Equal("ModeType", mode_enum.name);
    var itm1 = (EnumItemSymbol)mode_enum.Resolve("DEFAULT");
    Assert.Equal("DEFAULT", itm1.name);
    Assert.Equal(0, itm1.val);
    var itm2 = (EnumItemSymbol)mode_enum.Resolve("BATTLE");
    Assert.Equal("BATTLE", itm2.name);
    Assert.Equal(1, itm2.val);

    var color_list = (NativeListTypeSymbol<object>)new_types.ns.Resolve("List_Color");
    Assert.Equal("List_Color", color_list.name);

    CommonChecks(vm);
  }
}
