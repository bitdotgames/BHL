using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using bhl;
using Xunit;

// Coverage for std.bind.NewClassSymbolScript/DefineVirtualMethod - lets a RegisterBindings-style
// .bhl (or native C#) binding declare a real extendable class with virtual method stubs.
public class TestStdBindClass : BHL_TestBase
{
  static string MakeTempDir()
  {
    string dir = Path.Combine(Path.GetTempPath(), "bhl_std_bind_class_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    return dir;
  }

  static string WriteBaseClassBinding(string dir)
  {
    string path = Path.Combine(dir, "mybase.bhl");
    File.WriteAllText(path, @"
import ""std/bind""

func RegisterBindings(std.bind.Types types)
{
  var module = std.bind.NewModuleDeclared(""mybase"")
  var ns = module.ns

  var cl = std.bind.NewClassSymbolScript(module, ""Base"", null, null)
  ns.Define(cl)
  cl.DefineVirtualMethod(""Overridden"", types.T(""void""), [])
  cl.DefineVirtualMethod(""NotOverridden"", types.T(""void""), [])
  cl.Setup()

  types.RegisterModule(module)
}
");
    return path;
  }

  [Fact]
  public async Task DeclaredClassIsExtendableAndVirtualStubsWork()
  {
    var dir = MakeTempDir();
    try
    {
      var binding_script = WriteBaseClassBinding(dir);
      var bindings = new ScriptedBindings(new List<string> { binding_script }, "RegisterBindings", use_cache: false, tmp_dir: dir);

      string derived_bhl = @"
      import ""mybase""

      class MyScript : Base
      {
        int overridden_calls

        override func Overridden()
        {
          this.overridden_calls = this.overridden_calls + 1
        }

        //NOTE: calls the unoverridden base stub via a real script-to-script virtual
        //      call (OpcodeCallMethodVirt), not just the host-facing FindMethod path
        func CallNotOverridden()
        {
          this.NotOverridden()
        }
      }
      ";

      var vm = await MakeVM(new Dictionary<string, string> { { "derived.bhl", derived_bhl } },
        ts_fn: ts => bindings.Register(ts));

      vm.LoadModule("derived");

      var instance = vm.NewInstance("MyScript");

      var overridden_symb = vm.FindMethod(instance, "Overridden");
      Assert.NotNull(overridden_symb);
      vm.ExecuteMethod(ref instance, overridden_symb, new StackList<Val>());
      Assert.Equal(1, vm.GetFieldValue(instance, "overridden_calls").num);

      var call_symb = vm.FindMethod(instance, "CallNotOverridden");
      Assert.NotNull(call_symb);
      vm.ExecuteMethod(ref instance, call_symb, new StackList<Val>());

      instance.ReleaseData();
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  //NOTE: mirrors how a native C# IUserBindings (e.g. Unity's UnityBindings.cs) declares a
  //      class - plain C# calls into std.bind's public API, no script/VM call involved
  class NativeBaseBindings : IUserBindings
  {
    public void Register(Types ts)
    {
      var module = new ModuleDeclared("nativebase");

      var cl = std.bind.NewClassSymbolScript(module, "Base");
      module.ns.Define(cl);
      std.bind.DefineVirtualMethod(cl, "Foo", Types.Void, Array.Empty<FuncArgSymbol>());
      cl.Setup();

      ts.RegisterModule(module);
    }
  }

  [Fact]
  public async Task NativeCSharpBindingCanDeclareExtendableClass()
  {
    var bindings = new NativeBaseBindings();

    string derived_bhl = @"
    import ""nativebase""

    class MyScript : Base
    {
      int foo_calls

      override func Foo()
      {
        this.foo_calls = this.foo_calls + 1
      }
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string> { { "derived.bhl", derived_bhl } },
      ts_fn: ts => bindings.Register(ts));

    vm.LoadModule("derived");

    var instance = vm.NewInstance("MyScript");

    var foo_symb = vm.FindMethod(instance, "Foo");
    Assert.NotNull(foo_symb);
    vm.ExecuteMethod(ref instance, foo_symb, new StackList<Val>());
    Assert.Equal(1, vm.GetFieldValue(instance, "foo_calls").num);

    instance.ReleaseData();
  }

  //NOTE: mirrors BHLComponent's actual shape (UnityBHL) - a FieldSymbolScript field (like
  //      gameObject/transform) set from the host via vm.SetFieldValue before a virtual
  //      method runs, then read/written from inside an overriding method's own body
  class NativeComponentBindings : IUserBindings
  {
    public void Register(Types ts)
    {
      var module = new ModuleDeclared("nativecomponent");

      var cl = std.bind.NewClassSymbolScript(module, "Component");
      module.ns.Define(cl);
      cl.Define(new FieldSymbolScript(new Origin(), "tag", Types.Int));
      std.bind.DefineVirtualMethod(cl, "Awake", Types.Void, Array.Empty<FuncArgSymbol>());
      cl.Setup();

      ts.RegisterModule(module);
    }
  }

  [Fact]
  public async Task InheritedFieldSymbolScriptFieldWorksWithHostSetAndVirtualOverride()
  {
    var bindings = new NativeComponentBindings();

    string derived_bhl = @"
    import ""nativecomponent""

    class MyScript : Component
    {
      override func Awake()
      {
        this.tag = this.tag + 100
      }
    }
    ";

    var vm = await MakeVM(new Dictionary<string, string> { { "derived.bhl", derived_bhl } },
      ts_fn: ts => bindings.Register(ts));

    vm.LoadModule("derived");

    var instance = vm.NewInstance("MyScript");
    vm.SetFieldValue(ref instance, "tag", Val.NewInt(1));

    var awake_symb = vm.FindMethod(instance, "Awake");
    Assert.NotNull(awake_symb);
    vm.ExecuteMethod(ref instance, awake_symb, new StackList<Val>());

    Assert.Equal(101, vm.GetFieldValue(instance, "tag").num);

    instance.ReleaseData();
  }

  [Fact]
  public async Task DefineVirtualMethodRejectsNonVoidReturnType()
  {
    var dir = MakeTempDir();
    try
    {
      string path = Path.Combine(dir, "badbase.bhl");
      File.WriteAllText(path, @"
import ""std/bind""

func RegisterBindings(std.bind.Types types)
{
  var module = std.bind.NewModuleDeclared(""badbase"")
  var cl = std.bind.NewClassSymbolScript(module, ""Base"", null, null)
  module.ns.Define(cl)
  cl.DefineVirtualMethod(""Foo"", types.T(""int""), [])
  cl.Setup()
  types.RegisterModule(module)
}
");
      var bindings = new ScriptedBindings(new List<string> { path }, "RegisterBindings", use_cache: false, tmp_dir: dir);

      var ts = new Types();
      var ex = Assert.Throws<VM.Error>(() => bindings.Register(ts));
      Assert.Contains("only void-returning stubs", ex.ToString());
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }
}
