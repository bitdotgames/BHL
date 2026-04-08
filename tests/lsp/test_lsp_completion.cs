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

  TestLSPHost srv;
  DocumentUri uri1;
  DocumentUri uri2;
  DocumentUri uri3;

  public TestLSPCompletion()
  {
    srv = NewTestServer();
    CleanTestFiles();
    uri1 = MakeTestDocument("bhl1.bhl", bhl1);
    uri2 = MakeTestDocument("bhl2.bhl", bhl2);
    uri3 = MakeTestDocument("bhl3.bhl", bhl3);
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
}
