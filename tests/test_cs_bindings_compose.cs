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

  // the same short name in two namespaces: the entry search resolves registrations the same
  // way the interpreter does, so this reads as ambiguous rather than as a missing entry
  [Fact]
  public void TestAmbiguousRegisteredNameIsReportedAsSuch()
  {
    var dir = MakeTempDir();
    try
    {
      var a = Path.Combine(dir, "a.cs");
      File.WriteAllText(a, @"
using bhl;
namespace one
{
[BhlBinding(""compose_one"", ""1.0.0"")]
public class Shared : IUserBindings { public void Register(Types types) {} }
}
");
      var b = Path.Combine(dir, "b.cs");
      File.WriteAllText(b, @"
using bhl;
namespace two
{
[BhlBinding(""compose_two"", ""1.0.0"")]
public class Shared : IUserBindings { public void Register(Types types) {} }
}
");
      var app = WriteApp(dir, "    types.RegisterBindings(new Shared());");

      var e = Assert.Throws<CsBindingsException>(() => Read(a, b, app));
      Assert.Contains("is ambiguous", e.Message);
      Assert.Contains("one.Shared", e.Message);
      Assert.Contains("two.Shared", e.Message);
      //NOTE: and names who asked for it, instead of blaming the entry search
      Assert.Contains("app.AppBindings", e.Message);
    }
    finally { Directory.Delete(dir, true); }
  }

  // a class nothing registers contributes nothing, the same as at runtime
  [Fact]
  public void TestUnreachedBindingIsNotDeclared()
  {
    var dir = MakeTempDir();
    try
    {
      var lib = WriteLib(dir);
      var app = WriteApp(dir, "    types.ns.Define(new FuncSymbolNative(new Origin(), \"noop\", Types.Int, null));");

      //NOTE: two roots would be ambiguous, so give the library a reason not to be one
      File.WriteAllText(lib, File.ReadAllText(lib).Replace(": IUserBindings", ""));

      var ts = Read(lib, app);

      Assert.True(ts.TryGetBindingsVersion("compose_app", out _));
      //NOTE: present in the sources, reached by nothing
      Assert.False(ts.TryGetBindingsVersion("compose_lib", out _));
      Assert.Null(ts.ns.Resolve("lib_answer"));
    }
    finally { Directory.Delete(dir, true); }
  }

  // a library supplied by its own bhl.proj entry isn't in these sources - its entry loads it,
  // so there's nothing to follow here
  [Fact]
  public void TestRegisteringAClassFromAnotherEntryIsSkipped()
  {
    var dir = MakeTempDir();
    try
    {
      var app = WriteApp(dir, "    types.RegisterBindings(new elsewhere.OtherBindings());");

      var ts = Read(app);

      Assert.True(ts.TryGetBindingsVersion("compose_app", out _));
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
