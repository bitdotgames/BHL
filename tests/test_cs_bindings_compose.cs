using System;
using System.Collections.Generic;
using System.IO;
using bhl;
using Xunit;

// Every binding is its own bhl.proj entry, so a 'types.RegisterBindings(new LibBindings())'
// in one entry's sources always points at a class another entry loads. The C# reader
// recognizes the call and ignores it - the real one still runs at runtime.
public class TestCsBindingsCompose : BHL_TestBase
{
  static string MakeTempDir()
  {
    string dir = Path.Combine(Path.GetTempPath(), "bhl_cs_compose_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    return dir;
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
  public void TestRegisteringAnotherEntrysBindingIsSkipped()
  {
    var dir = MakeTempDir();
    try
    {
      var app = WriteApp(dir, @"
    types.RegisterBindings(new elsewhere.OtherBindings());
    types.ns.Define(new FuncSymbolNative(new Origin(), ""app_fn"", Types.Int, null));");

      var ts = Read(app);

      Assert.True(ts.TryGetBindingsVersion("compose_app", out _));
      //NOTE: the call didn't stop the rest of Register from running
      Assert.NotNull(ts.ns.Resolve("app_fn"));
    }
    finally { Directory.Delete(dir, true); }
  }

  [Fact]
  public void TestTwoRegisterClassesInOneEntryIsRejected()
  {
    var dir = MakeTempDir();
    try
    {
      var lib = Path.Combine(dir, "lib.cs");
      File.WriteAllText(lib, @"
using bhl;
namespace lib
{
[BhlBinding(""compose_lib"", ""2.1.0"")]
public class LibBindings : IUserBindings { public void Register(Types types) {} }
}
");
      var app = WriteApp(dir, "");

      var e = Assert.Throws<CsBindingsException>(() => Read(lib, app));
      Assert.Contains("more than one", e.Message);
      Assert.Contains("one bindings entry is one binding", e.Message);
    }
    finally { Directory.Delete(dir, true); }
  }
}
