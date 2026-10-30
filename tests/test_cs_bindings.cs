using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using bhl;
using bhl.test_cs_bindings;
using Xunit;

// Coverage for CsSourceBindings: C# bindings registration read without compiling it
// (src/front/cs_bindings.cs, docs/csharp-bindings.md)
public class TestCsBindings : BHL_TestBase
{
  static string SamplePath([CallerFilePath] string self = "")
  {
    return Path.Combine(Path.GetDirectoryName(self), "cs_bindings", "sample_bindings.cs");
  }

  static string MakeTempDir()
  {
    string dir = Path.Combine(Path.GetTempPath(), "bhl_cs_bindings_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    return dir;
  }

  static Types ReadSample()
  {
    var ts = new Types();
    new CsSourceBindings(new List<string>() { SamplePath() }).Register(ts);
    return ts;
  }

  //NOTE: the source of a single file binding with a Register method around the given statements
  static string Wrap(string stmts, string extra_members = "")
  {
    return $@"
using bhl;
namespace test
{{
static class Bindings
{{
  public static void Register(Types types)
  {{
{stmts}
  }}
{extra_members}
}}
}}
";
  }

  static Types ReadSource(string src)
  {
    var dir = MakeTempDir();
    try
    {
      var path = Path.Combine(dir, "bindings.cs");
      File.WriteAllText(path, src);
      var ts = new Types();
      new CsSourceBindings(new List<string>() { path }).Register(ts);
      return ts;
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  static void ReadSourceInto(Types ts, string src)
  {
    var dir = MakeTempDir();
    try
    {
      var path = Path.Combine(dir, "bindings.cs");
      File.WriteAllText(path, src);
      new CsSourceBindings(new List<string>() { path }).Register(ts);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  static CsBindingsException ReadSourceError(string src)
  {
    return Assert.Throws<CsBindingsException>(() => ReadSource(src));
  }

  [Fact]
  public void ReadSymbolsAreTheSameAsCompiledRegistration()
  {
    var compiled = new Types();
    SampleCsBindings.Register(compiled);

    var read = ReadSample();

    Assert.Equal(compiled.ns.DumpMembers(), read.ns.DumpMembers());
  }

  [Fact]
  public void NativeCodeIsReplacedWithFrontEndPlaceholders()
  {
    var ts = ReadSample();

    var point = (ClassSymbolNative)ts.ns.Resolve("SamplePoint");
    Assert.Same(ClassSymbolNative.PendingCreator, point.creator);

    var x = (FieldSymbol)point.Resolve("x");
    Assert.Same(FieldSymbol.PendingGetter, x.getter);
    Assert.Same(FieldSymbol.PendingSetter, x.setter);

    var len = (FieldSymbol)point.Resolve("len");
    Assert.Same(FieldSymbol.PendingGetter, len.getter);
    Assert.Null(len.setter);

    var sum = (FuncSymbolNative)ts.ns.Resolve("SampleSum");
    Assert.Null(sum.cb);
  }

  [Fact]
  public void OriginPointsToTheRegistrationCode()
  {
    var ts = ReadSample();

    var sum = ts.ns.Resolve("SampleSum");
    Assert.Equal(SamplePath(), sum.origin.source_file);
    int expected_line = File.ReadAllLines(SamplePath()).ToList().FindIndex(l => l.Contains("\"SampleSum\"")) + 1;
    Assert.Equal(expected_line, sum.origin.source_line);
  }

  [Fact]
  public void ScriptsCompileAgainstReadBindings()
  {
    string bhl = @"
    func test() {
      SamplePoint p = new SamplePoint
      p.x = 1.5
      float l = p.len
      SamplePoint p2 = p.Scale(2)
      int s = SampleSum(1)
      string str
      []SamplePoint pts
      str, pts = SamplePair(func() {})
      bool ok = SampleUI.Open(""wnd"", SampleColor.Blue)
      ISampleWidget w = null
      bool same = SampleButton.Same(null, null)
    }
    ";

    Compile(bhl, ts => new CsSourceBindings(new List<string>() { SamplePath() }).Register(ts));
  }

  [Fact]
  public void ReadOnlyFieldIsNotWritableFromScripts()
  {
    string bhl = @"
    func test() {
      SamplePoint p = new SamplePoint
      p.len = 1
    }
    ";

    AssertError<Exception>(
      () => Compile(bhl, ts => new CsSourceBindings(new List<string>() { SamplePath() }).Register(ts)),
      "set operation is"
    );
  }

  [Fact]
  public void DeclaredVersionComesFromBhlBindingAttribute()
  {
    var dir = MakeTempDir();
    try
    {
      var path = Path.Combine(dir, "bindings.cs");
      File.WriteAllText(path, @"
using bhl;
[BhlBinding(""cs_one"", ""1.2.3"")]
static class Bindings
{
  public static void Register(Types types)
  {
    types.ns.Define(new FuncSymbolNative(new Origin(), ""Foo"", types.T(""int""), null));
  }
}
");

      var proj = new ProjectConf();
      proj.tmp_dir = dir;
      proj.use_cache = false;
      proj.bindings.Add(new BindingsEntryConf { name = "cs_one", sources = new List<string> { path } });
      proj.Setup();

      Assert.Equal("", proj.bindings[0].dll);

      var bindings = (UserBindingsWithInfo)proj.LoadBindings();
      Assert.Single(bindings.info);
      Assert.Equal(("cs_one", "1.2.3"), bindings.info[0]);

      var ts = new Types();
      bindings.Register(ts);
      Assert.NotNull(ts.ns.Resolve("Foo"));
      Assert.True(ts.TryGetBindingsVersion("cs_one", out var version));
      Assert.Equal("1.2.3", version);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void ExplicitDllKeepsBuildingCsSources()
  {
    //NOTE: a rooted path of the platform ('/tmp/...' gets a drive letter on Windows)
    string dll = Path.Combine(Path.GetTempPath(), "foo.dll");
    var proj = new ProjectConf();
    proj.bindings.Add(new BindingsEntryConf { name = "cs_built", sources = new List<string> { "foo.cs" }, dll = dll });
    proj.Setup();

    Assert.Equal(BuildUtils.NormalizeFilePath(dll), proj.bindings[0].dll);
  }

  [Fact]
  public void GlobbedSourcesAreReadTogether()
  {
    var dir = MakeTempDir();
    try
    {
      File.WriteAllText(Path.Combine(dir, "register.cs"), Wrap("    Register_0(types);").Replace("static class Bindings", "[BhlBinding(\"globbed\", \"1.0.0\")]\nstatic class Bindings"));
      File.WriteAllText(Path.Combine(dir, "register_0.cs"), @"
using bhl;
namespace test
{
static partial class Bindings
{
  public static void Register_0(Types types)
  {
    types.ns.Define(new FuncSymbolNative(new Origin(), ""FromSecondFile"", types.T(""void""), null));
  }
}
}
");
      var proj = new ProjectConf();
      proj.tmp_dir = dir;
      proj.bindings.Add(new BindingsEntryConf { name = "globbed", sources = new List<string> { dir.Replace('\\', '/') + "/*.cs" } });
      proj.Setup();
      var ts = new Types();
      proj.LoadBindings().Register(ts);

      Assert.NotNull(ts.ns.Resolve("FromSecondFile"));
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void InactivePreprocessorBranchesAreSkipped()
  {
    var ts = ReadSource(Wrap(@"
#if BHL_FRONT && !SOMETHING_ELSE
    types.ns.Define(new FuncSymbolNative(new Origin(), ""Front"", types.T(""void""), null));
#elif true
    types.ns.Define(new FuncSymbolNative(new Origin(), ""NotFront"", types.T(""void""), null));
#else
    this is not even C#
#endif
"));

    Assert.NotNull(ts.ns.Resolve("Front"));
    Assert.Null(ts.ns.Resolve("NotFront"));
  }

  [Fact]
  public void SetupTypeResolvesDottedNamesOfNamespaces()
  {
    string src = Wrap(@"
    {
      var cl = new ClassSymbolNative(new Origin(), ""Base"", new ProxyType(), proxy_implements: null, creator: null);
      types.ns.Define(cl);
      cl.Define(new FieldSymbol(new Origin(), ""hp"", types.T(""int""), get_hp, null));
    }
    var ui = types.ns.Nest(""UI"");
    {
      var cl = new ClassSymbolNative(new Origin(), ""Panel"", types.T(""Base""), proxy_implements: null, creator: ctor_Panel);
      ui.Define(cl);
    }
    types.SetupType(""Base"");
    types.SetupType(""UI.Panel"");
");

    string bhl = @"
    func int test() {
      UI.Panel p = new UI.Panel
      return p.hp
    }
    ";

    Compile(bhl, ts => ReadSourceInto(ts, src));

    var err = ReadSourceError(Wrap(@"    types.SetupType(""UI.Missing"");"));
    Assert.Contains("Type 'UI.Missing' not resolved", err.Message);
  }

  [Fact]
  public void FlagsAreCombinedWithBitOr()
  {
    var ts = ReadSource(Wrap(@"
    types.ns.Define(new FuncSymbolNative(new Origin(), ""F"", FuncAttrib.Coro | FuncAttrib.Static, types.T(""void""), 0, null));
"));

    var f = (FuncSymbolNative)ts.ns.Resolve("F");
    Assert.True(f.attribs.HasFlag(FuncAttrib.Coro));
    Assert.True(f.attribs.HasFlag(FuncAttrib.Static));
  }

  [Fact]
  public void ObjectInitializersOtherThanListsAreAnError()
  {
    Assert.Contains("object initializers are not supported",
      ReadSourceError(Wrap("    var o = new Origin() { native_line = 1 };")).Message);
  }

  [Fact]
  public void DefineIfAbsentSkipsAlreadyDefinedSymbol()
  {
    var ts = ReadSource(Wrap(@"
    types.ns.Define(new FuncSymbolNative(new Origin(), ""Once"", types.T(""void""), null));
    if(types.ns.Resolve(""Once"") == null)
      types.ns.Define(new FuncSymbolNative(new Origin(), ""Once"", types.T(""void""), null));
    if(types.ns.Resolve(""Twice"") == null)
    {
      types.ns.Define(new FuncSymbolNative(new Origin(), ""Twice"", types.T(""void""), null));
    }
"));

    Assert.NotNull(ts.ns.Resolve("Twice"));
  }

  [Fact]
  public void LambdasAreAnError()
  {
    var err = ReadSourceError(Wrap(@"
    System.Func<int> f = () => 1;
"));
    Assert.Contains("lambdas are not supported", err.Message);
    Assert.Contains("bindings.cs:10:", err.Message);
  }

  [Fact]
  public void ControlFlowIsAnError()
  {
    Assert.Contains("'for' is not supported",
      ReadSourceError(Wrap("    for(int i = 0; i < 1; ++i) {}")).Message);
    Assert.Contains("only 'if(<scope>.Resolve(<name>) == null)' is supported",
      ReadSourceError(Wrap("    if(true) { }")).Message);
    Assert.Contains("'else' is not supported",
      ReadSourceError(Wrap(@"
    if(types.ns.Resolve(""X"") == null)
      Foo(types);
    else
      Foo(types);
", "  static void Foo(Types types) {}")).Message);
  }

  [Fact]
  public void AssignmentsAreAnError()
  {
    var err = ReadSourceError(Wrap(@"
    var fn = new FuncSymbolNative(new Origin(), ""X"", types.T(""void""), null);
    fn.name = ""Y"";
"));
    Assert.Contains("assignments are not supported", err.Message);
  }

  [Fact]
  public void UnknownNamesAreAnError()
  {
    Assert.Contains("unknown method 'NoSuchMethod'",
      ReadSourceError(Wrap("    NoSuchMethod(types);")).Message);
    Assert.Contains("unknown type 'NoSuchSymbol'",
      ReadSourceError(Wrap("    types.ns.Define(new NoSuchSymbol());")).Message);
    Assert.Contains("has no method 'NoSuchCall'",
      ReadSourceError(Wrap("    types.ns.NoSuchCall();")).Message);
    //NOTE: a method group is allowed only where native code (a delegate) is expected
    Assert.Contains("no 'FuncSymbolNative' matches arguments",
      ReadSourceError(Wrap("    types.ns.Define(new FuncSymbolNative(new Origin(), some_native, types.T(\"void\"), null));")).Message);
  }

  [Fact]
  public void SeveralModulesAreCombinedByUserBindingsRegister()
  {
    var dir = MakeTempDir();
    try
    {
      string Module(string ns, string cls, string func) => $@"
using bhl;
namespace {ns} {{
static partial class {cls}
{{
  public static void Register(Types types)
  {{
    RegisterBegin(types);
    RegisterEnd(types);
  }}
  public static void RegisterBegin(Types types)
  {{
    Register_0(types);
  }}
  public static void RegisterEnd(Types types)
  {{
  }}
  public static void Register_0(Types types)
  {{
    types.ns.Define(new FuncSymbolNative(new Origin(), ""{func}"", types.T(""void""), null));
  }}
}}
}}
";
      File.WriteAllText(Path.Combine(dir, "game.cs"), Module("Game.Bind", "GameBindings", "FromGame"));
      File.WriteAllText(Path.Combine(dir, "m3.cs"), Module("M3.Bind", "M3Bindings", "FromM3"));
      //NOTE: like the runtime registration of a project, an instance method of IUserBindings
      File.WriteAllText(Path.Combine(dir, "bind_register.cs"), @"
using bhl;
namespace Game.Bind {
public class Bindings : IUserBindings
{
  public void Register(Types types)
  {
    GameBindings.RegisterBegin(types);
    M3.Bind.M3Bindings.RegisterBegin(types);
    GameBindings.RegisterEnd(types);
    M3.Bind.M3Bindings.RegisterEnd(types);
  }
}
}
");
      var files = Directory.GetFiles(dir, "*.cs").ToList();
      var ts = new Types();
      new CsSourceBindings(files).Register(ts);
      Assert.NotNull(ts.ns.Resolve("FromGame"));
      Assert.NotNull(ts.ns.Resolve("FromM3"));

      //NOTE: same short names in other namespaces are fine until a call is ambiguous
      File.WriteAllText(Path.Combine(dir, "types.cs"), @"
namespace Game.Bind { static class Types_Shared { static public bhl.IType Value; } }
namespace M3.Bind { static class Types_Shared { static public bhl.IType Value; } }
");
      ts = new Types();
      new CsSourceBindings(Directory.GetFiles(dir, "*.cs").ToList()).Register(ts);
      Assert.NotNull(ts.ns.Resolve("FromM3"));

      File.Delete(Path.Combine(dir, "bind_register.cs"));
      var err = Assert.Throws<CsBindingsException>(() => new CsSourceBindings(Directory.GetFiles(dir, "*.cs").ToList()).Register(new Types()));
      Assert.Contains("'Register' is declared in more than one class", err.Message);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void RegisterMustBeTheOnlyEntry()
  {
    Assert.Contains("no 'Register' method found",
      ReadSourceError(@"
using bhl;
static class Bindings
{
  public static void Other(Types types) {}
}
").Message);

    Assert.Contains("declared more than once",
      ReadSourceError(Wrap("", "  public static void Register(Types types, int i) {}")).Message);
  }
}
