using System;
using System.Collections.Generic;
using System.IO;
using bhl;
using Xunit;

// A library ships its own [BhlBinding] IUserBindings class; whoever depends on it writes one
// 'types.RegisterBindings(new LibBindings())' line. The compiler's C# reader follows that call
// instead of trying to resolve the type, so the same line means the same thing at compile time
// and at runtime - no separate bhl.proj entry per library.
public class TestCsBindingsCompose : BHL_TestBase
{
  static string MakeTempDir()
  {
    string dir = Path.Combine(Path.GetTempPath(), "bhl_cs_compose_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    return dir;
  }

  static string WriteLib(string dir)
  {
    var path = Path.Combine(dir, "lib.cs");
    File.WriteAllText(path, @"
using bhl;
namespace lib
{
[BhlBinding(""compose_lib"", ""2.1.0"")]
public class LibBindings : IUserBindings
{
  public void Register(Types types)
  {
    types.ns.Define(new FuncSymbolNative(new Origin(), ""lib_answer"", Types.Int,
      delegate(VM.ExecState exec, FuncArgsInfo args_info) {
        exec.stack.Push(Val.NewInt(42));
        return null;
      }
    ));
  }
}
}
");
    return path;
  }

  static string WriteApp(string dir, string body)
  {
    var path = Path.Combine(dir, "app.cs");
    File.WriteAllText(path, @"
using bhl;
namespace app
{
[BhlBinding(""compose_app"", ""1.0.0"")]
public class AppBindings : IUserBindings
{
  public void Register(Types types)
  {
" + body + @"
  }
}
}
");
    return path;
  }

  static Types Read(params string[] files)
  {
    var ts = new Types();
    new CsSourceBindings(new List<string>(files)).Register(ts);
    return ts;
  }

  [Fact]
  public void TestRegisterBindingsPullsInTheLibrary()
  {
    var dir = MakeTempDir();
    try
    {
      var lib = WriteLib(dir);
      var app = WriteApp(dir, "    types.RegisterBindings(new lib.LibBindings());");

      var ts = Read(lib, app);

      //NOTE: the library's symbols are there, having been reached through the call
      Assert.NotNull(ts.ns.Resolve("lib_answer"));
      //NOTE: and both identities are declared
      Assert.True(ts.TryGetBindingsVersion("compose_lib", out var lib_ver));
      Assert.Equal("2.1.0", lib_ver);
      Assert.True(ts.TryGetBindingsVersion("compose_app", out _));
    }
    finally { Directory.Delete(dir, true); }
  }

  //NOTE: the entry is the one nobody registers - two unrelated roots stay an error
  [Fact]
  public void TestTwoUnregisteredRootsIsAmbiguous()
  {
    var dir = MakeTempDir();
    try
    {
      var lib = WriteLib(dir);
      var app = WriteApp(dir, "    types.ns.Define(new FuncSymbolNative(new Origin(), \"noop\", Types.Int, null));");

      var e = Assert.Throws<CsBindingsException>(() => Read(lib, app));
      Assert.Contains("more than one", e.Message);
      Assert.Contains("the one no other registers", e.Message);
    }
    finally { Directory.Delete(dir, true); }
  }

  [Fact]
  public void TestSelfRegistrationIsRejected()
  {
    var dir = MakeTempDir();
    try
    {
      var app = WriteApp(dir, "    types.RegisterBindings(new app.AppBindings());");

      var e = Assert.Throws<CsBindingsException>(() => Read(app));
      Assert.Contains("registers itself", e.Message);
    }
    finally { Directory.Delete(dir, true); }
  }
}
