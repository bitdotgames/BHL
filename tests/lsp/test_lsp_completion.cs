using System;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

public class TestLSPCompletion : TestLSPShared, IDisposable
{
  string bhl1 = @"
  class Foo {
    int BAR
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

  func test5()
  {
    Foo local_foo
    int x = local_foo.BAR
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

  TestLSPHost srv;
  DocumentUri uri1;
  DocumentUri uri2;

  public TestLSPCompletion()
  {
    srv = NewTestServer();
    CleanTestFiles();
    uri1 = MakeTestDocument("bhl1.bhl", bhl1);
    uri2 = MakeTestDocument("bhl2.bhl", bhl2);
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
  public async Task member_completion_class_name()
  {
    await SendInit(srv);

    // Foo. → class members (type name used as scope)
    var result = await GetMemberCompletions(srv, uri1, "Foo");
    var labels = result.Items.Select(i => i.Label).ToHashSet();

    Assert.Contains("BAR", labels);
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
