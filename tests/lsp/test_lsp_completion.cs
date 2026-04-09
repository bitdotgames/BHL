using System;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

public class TestLSPCompletion : TestLSPShared, IDisposable
{
  string bhl1 = @"
  class Inner {
    int VAL
  }

  class Foo {
    int BAR
    Inner inner
    static int STATIC_VAL
    static func int StaticMethod() { return 0 }
  }

  enum ErrorCodes {
    Ok = 0
    Bad = 1
  }

  Foo foo = {
    BAR : 0
  }

  func float test1(float k)
  {
    return 0
  }

  func test2()
  {
    test1(42)
  }

  func Foo MakeFoo(int x, int y)
  {
    Foo f
    return f
  }

  func test5()
  {
    Foo local_foo
    int x = local_foo.BAR
    int y = local_foo.inner.VAL
    ErrorCodes local_err = ErrorCodes.Ok
  }
  ";

  string bhl2 = @"
  import ""bhl1""

  func float test3(float k, int j)
  {
    return 0
  }

  func test4()
  {
    test2()
  }
  ";

  // incomplete member access expressions — dot is already typed, cursor right after it
  string bhl3 = @"
  import ""bhl1""

  func test_foo_dot()
  {
    Foo local_foo
    local_foo.
  }

  func test_err_dot()
  {
    ErrorCodes local_err = ErrorCodes.Ok
    local_err.
  }

  func test_nested_dot()
  {
    Foo local_foo
    local_foo.inner.
  }

  func test_func_ret_dot()
  {
    MakeFoo(1, 2).
  }
  ";

  string bhl4 = @"
  namespace ns {
    class Bar {
      int FIELD
      static int STATIC_FIELD
      static func int StaticBarMethod() { return 0 }
    }

    func int NsFunc() { return 0 }

    namespace nested {
      class DeepClass {
        int DEEP_VAL
      }
    }
  }
  ";

  // incomplete namespace access — dot already typed, cursor right after it
  string bhl5 = @"
  import ""bhl4""

  func test_ns_dot()
  {
    ns.
  }

  func test_ns_nested_dot()
  {
    ns.nested.
  }

  func test_ns_bar_dot()
  {
    ns.Bar.
  }
  ";

  // no imports — used to test that symbols from other (non-imported) modules are still offered
  string bhl6 = @"
  func test_no_imports()
  {
    Foo.
    ns.
    ns.nested.
    std.
    std.io.
  }
  ";

  // class with a method that uses 'this.' (incomplete expression) for completion
  string bhl7 = @"
  class MyClass {
    int Field1
    float Field2

    func void MyMethod()
    {
      this.
    }
  }
  ";

  TestLSPHost srv;
  DocumentUri uri1;
  DocumentUri uri2;
  DocumentUri uri3;
  DocumentUri uri4;
  DocumentUri uri5;
  DocumentUri uri6;
  DocumentUri uri7;

  public TestLSPCompletion()
  {
    srv = NewTestServer();
    CleanTestFiles();
    uri1 = MakeTestDocument("bhl1.bhl", bhl1);
    uri2 = MakeTestDocument("bhl2.bhl", bhl2);
    uri3 = MakeTestDocument("bhl3.bhl", bhl3);
    uri4 = MakeTestDocument("bhl4.bhl", bhl4);
    uri5 = MakeTestDocument("bhl5.bhl", bhl5);
    uri6 = MakeTestDocument("bhl6.bhl", bhl6);
    uri7 = MakeTestDocument("bhl7.bhl", bhl7);
  }

  public void Dispose()
  {
    srv.Dispose();
  }

  [Fact]
  public async Task local_symbols()
  {
    await SendInit(srv);

    var result = await GetCompletions(srv, uri1);
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("test1", labels);
    Assert.Contains("test2", labels);
    Assert.Contains("Foo", labels);
    Assert.Contains("ErrorCodes", labels);
    Assert.Contains("foo", labels);
  }

  [Fact]
  public async Task symbol_kinds()
  {
    await SendInit(srv);

    var result = await GetCompletions(srv, uri1);
    var byLabel = result.Items.ToDictionary(i => i.Label, i => i.Kind);

    Assert.Equal(CompletionItemKind.Function, byLabel["test1"]);
    Assert.Equal(CompletionItemKind.Function, byLabel["test2"]);
    Assert.Equal(CompletionItemKind.Class,    byLabel["Foo"]);
    Assert.Equal(CompletionItemKind.Enum,     byLabel["ErrorCodes"]);
  }

  [Fact]
  public async Task imported_symbols_visible()
  {
    await SendInit(srv);

    var result = await GetCompletions(srv, uri2);
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    // own symbols
    Assert.Contains("test3", labels);
    Assert.Contains("test4", labels);

    // imported from bhl1
    Assert.Contains("test1", labels);
    Assert.Contains("test2", labels);
    Assert.Contains("Foo", labels);
    Assert.Contains("ErrorCodes", labels);
  }

  [Fact]
  public async Task member_completion_class_field()
  {
    await SendInit(srv);

    // foo is of type Foo which has field BAR
    var result = await GetMemberCompletions(srv, uri1, "foo");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("BAR", labels);
  }

  [Fact]
  public async Task member_completion_local_instance()
  {
    await SendInit(srv);

    // local_foo is a local variable of type Foo
    var result = await GetMemberCompletions(srv, uri1, "local_foo");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("BAR", labels);
  }

  [Fact]
  public async Task member_completion_local_enum()
  {
    await SendInit(srv);

    var result = await GetMemberCompletions(srv, uri1, "local_err");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("Ok", labels);
    Assert.Contains("Bad", labels);
  }

  [Fact]
  public async Task member_completion_instance_excludes_static()
  {
    await SendInit(srv);

    // "foo." on an instance shows only instance members, not static ones
    var result = await GetMemberCompletions(srv, uri1, "foo");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("BAR", labels);
    Assert.Contains("inner", labels);
    Assert.DoesNotContain("STATIC_VAL", labels);
    Assert.DoesNotContain("StaticMethod", labels);
  }

  [Fact]
  public async Task member_completion_class_static_members()
  {
    await SendInit(srv);

    // "Foo." on a class name shows only static members
    var result = await GetMemberCompletions(srv, uri1, "Foo");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("STATIC_VAL", labels);
    Assert.Contains("StaticMethod", labels);
    Assert.DoesNotContain("BAR", labels);
    Assert.DoesNotContain("inner", labels);
  }

  [Fact]
  public async Task member_completion_enum()
  {
    await SendInit(srv);

    // ErrorCodes. → enum items
    var result = await GetMemberCompletions(srv, uri1, "enum ErrorCodes");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("Ok", labels);
    Assert.Contains("Bad", labels);
  }

  [Fact]
  public async Task member_completion_incomplete_class_instance()
  {
    await SendInit(srv);

    // "local_foo." is in the document as an incomplete expression
    var result = await GetMemberCompletionsInDoc(srv, uri3, "local_foo.");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("BAR", labels);
  }

  [Fact]
  public async Task member_completion_incomplete_enum()
  {
    await SendInit(srv);

    // "local_err." is in the document as an incomplete expression
    var result = await GetMemberCompletionsInDoc(srv, uri3, "local_err.");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("Ok", labels);
    Assert.Contains("Bad", labels);
  }

  [Fact]
  public async Task member_completion_chained_field()
  {
    await SendInit(srv);

    // "local_foo.inner" — inner is a field of Foo (not a module-level name),
    // so resolving it requires walking the chain: local_foo → Foo → inner → Inner.
    // A naive "look up last token only" approach would fail here.
    var result = await GetMemberCompletions(srv, uri1, "local_foo.inner");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("VAL", labels);
  }

  [Fact]
  public async Task member_completion_nested_chain()
  {
    await SendInit(srv);

    // "local_foo.inner." — complete members of Inner
    var result = await GetMemberCompletionsInDoc(srv, uri3, "local_foo.inner.");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("VAL", labels);
  }

  [Fact]
  public async Task member_completion_func_return()
  {
    await SendInit(srv);

    // "MakeFoo()." — complete members of the return type Foo
    var result = await GetMemberCompletionsInDoc(srv, uri3, "MakeFoo(1, 2).");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("BAR", labels);
    Assert.Contains("inner", labels);
  }

  [Fact]
  public async Task namespace_visible_in_global_completions()
  {
    await SendInit(srv);

    // namespace symbol itself should appear in top-level completions
    var result = await GetCompletions(srv, uri4);
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("ns", labels);
    Assert.Equal(CompletionItemKind.Module, result.Items.First(i => i.Label == "ns").Kind);
  }

  [Fact]
  public async Task namespace_member_completions()
  {
    await SendInit(srv);

    // "ns." — shows members of the namespace: Bar, NsFunc, nested
    var result = await GetMemberCompletionsInDoc(srv, uri5, "ns.");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("Bar", labels);
    Assert.Contains("NsFunc", labels);
    Assert.Contains("nested", labels);
  }

  [Fact]
  public async Task namespace_nested_completions()
  {
    await SendInit(srv);

    // "ns.nested." — shows members of the nested namespace
    var result = await GetMemberCompletionsInDoc(srv, uri5, "ns.nested.");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("DeepClass", labels);
  }

  [Fact]
  public async Task namespace_class_static_members()
  {
    await SendInit(srv);

    // "ns.Bar." — Bar is a class inside a namespace, should show only static members
    var result = await GetMemberCompletionsInDoc(srv, uri5, "ns.Bar.");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("STATIC_FIELD", labels);
    Assert.Contains("StaticBarMethod", labels);
    Assert.DoesNotContain("FIELD", labels);
  }

  [Fact]
  public async Task non_imported_symbols_in_global_completions()
  {
    await SendInit(srv);

    // bhl6 imports nothing, but all workspace symbols should be offered
    var result = await GetCompletions(srv, uri6);
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    // from bhl1 (not imported by bhl6)
    Assert.Contains("Foo", labels);
    Assert.Contains("test1", labels);
    Assert.Contains("ErrorCodes", labels);

    // from bhl4 (not imported by bhl6)
    Assert.Contains("ns", labels);
  }

  [Fact]
  public async Task non_imported_class_member_completions()
  {
    await SendInit(srv);

    // bhl6 doesn't import bhl1, but "Foo." should still resolve to Foo's members
    var result = await GetMemberCompletionsInDoc(srv, uri6, "Foo.");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("STATIC_VAL", labels);
    Assert.Contains("StaticMethod", labels);
    Assert.DoesNotContain("BAR", labels);
    Assert.DoesNotContain("inner", labels);
  }

  [Fact]
  public async Task non_imported_namespace_member_completions()
  {
    await SendInit(srv);

    // bhl6 doesn't import bhl4, but "ns." should still resolve to ns members
    var result = await GetMemberCompletionsInDoc(srv, uri6, "ns.");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("Bar", labels);
    Assert.Contains("NsFunc", labels);
    Assert.Contains("nested", labels);
  }

  [Fact]
  public async Task non_imported_namespace_nested_completions()
  {
    await SendInit(srv);

    // bhl6 doesn't import bhl4, but "ns.nested." should still resolve to nested's members
    var result = await GetMemberCompletionsInDoc(srv, uri6, "ns.nested.");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("DeepClass", labels);
  }

  [Fact]
  public async Task nested_namespace_symbols_in_global_completions()
  {
    await SendInit(srv);

    var result = await GetCompletions(srv, uri6);
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    // deeply nested symbols appear with fully-qualified labels so IDEs can
    // match them by substring (e.g. typing "NewFunc" finds "std.bind.NewFuncSymbolNative")
    Assert.Contains("std.bind.NewFuncSymbolNative", labels);
    Assert.Contains("std.bind.NewFuncArgSymbol", labels);
    Assert.Contains("std.bind", labels);
    Assert.Contains("std.io.Write", labels);
    Assert.Contains("std.io.WriteLine", labels);
    // user-defined nested namespaces also get qualified entries
    Assert.Contains("ns.NsFunc", labels);
    Assert.Contains("ns.nested.DeepClass", labels);
  }

  [Fact]
  public async Task std_namespace_visible_in_global_completions()
  {
    await SendInit(srv);

    var result = await GetCompletions(srv, uri6);
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("std", labels);
    Assert.Equal(CompletionItemKind.Module, result.Items.First(i => i.Label == "std").Kind);
  }

  [Fact]
  public async Task std_namespace_member_completions()
  {
    await SendInit(srv);

    // "std." should show functions declared in the std namespace
    var result = await GetMemberCompletionsInDoc(srv, uri6, "std.");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("GetType", labels);
    Assert.Contains("Is", labels);
    Assert.Contains("NextTrue", labels);
    // nested std.io namespace should also be visible
    Assert.Contains("io", labels);
  }

  [Fact]
  public async Task std_io_namespace_member_completions()
  {
    await SendInit(srv);

    // "std.io." should show functions declared in the std.io namespace
    var result = await GetMemberCompletionsInDoc(srv, uri6, "std.io.");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("Write", labels);
    Assert.Contains("WriteLine", labels);
  }

  [Fact]
  public async Task builtin_types_visible()
  {
    await SendInit(srv);

    var result = await GetCompletions(srv, uri1);
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("int", labels);
    Assert.Contains("float", labels);
    Assert.Contains("string", labels);
    Assert.Contains("bool", labels);
  }

  [Fact]
  public async Task this_dot_member_completion()
  {
    await SendInit(srv);

    // "this." inside a class method — should show the class fields
    var result = await GetMemberCompletionsInDoc(srv, uri7, "this.");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("Field1", labels);
    Assert.Contains("Field2", labels);
    Assert.DoesNotContain("this", labels);
  }

  [Fact]
  public async Task auto_import_added_for_non_imported_symbol()
  {
    await SendInit(srv);

    // bhl6 has no imports; "Foo" comes from bhl1 — completion should carry an auto-import edit
    var result = await GetCompletions(srv, uri6);
    var foo = result.Items.First(i => i.Label == "Foo");
    Assert.NotNull(foo.AdditionalTextEdits);
    var edit = foo.AdditionalTextEdits.Single();
    Assert.Equal("import \"bhl1\"\n", edit.NewText);
  }

  [Fact]
  public async Task auto_import_not_added_when_already_imported()
  {
    await SendInit(srv);

    // bhl2 already imports bhl1, so "Foo" from bhl1 should have no auto-import edit
    var result = await GetCompletions(srv, uri2);
    var foo = result.Items.First(i => i.Label == "Foo");
    Assert.Null(foo.AdditionalTextEdits);
  }

  [Fact]
  public async Task auto_import_not_added_for_own_module_symbols()
  {
    await SendInit(srv);

    // "test1" is declared in bhl1 itself — no auto-import needed
    var result = await GetCompletions(srv, uri1);
    var test1 = result.Items.First(i => i.Label == "test1");
    Assert.Null(test1.AdditionalTextEdits);
  }

  [Fact]
  public async Task auto_import_inserts_after_last_existing_import()
  {
    await SendInit(srv);

    // bhl2 already imports bhl1; bhl4 is not imported yet —
    // ns from bhl4 should carry an auto-import edit inserted after the existing import
    var result = await GetCompletions(srv, uri2);
    var ns = result.Items.First(i => i.Label == "ns");
    Assert.NotNull(ns.AdditionalTextEdits);
    var edit = ns.AdditionalTextEdits.Single();
    Assert.Equal("import \"bhl4\"\n", edit.NewText);
    // Insertion must be AFTER the existing `import "bhl1"` line, not at the very top
    Assert.True(edit.Range.Start.Line > 0);
  }
}
