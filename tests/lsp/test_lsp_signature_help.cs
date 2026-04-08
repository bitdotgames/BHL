using System;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

public class TestLSPSignatureHelp : TestLSPShared, IDisposable
{
  string bhl1 = @"
  class Foo {
    int BAR

    func int Method(int x, int y)
    {
      return x
    }
  }

  func float test1(float k)
  {
    return 0
  }

  func Foo MakeFoo(int x, int y)
  {
    Foo f
    return f
  }

  func test_complete_calls()
  {
    test1(42)
    MakeFoo(1, 2)
    Foo local_foo
    local_foo.Method(1, 2)
  }

  func test_open_paren()
  {
    test1(
    MakeFoo(1,
  }
  ";

  string bhl2 = @"
  import ""bhl1""

  func test_imported()
  {
    test1(
    MakeFoo(1,
  }
  ";

  TestLSPHost srv;
  DocumentUri uri1;
  DocumentUri uri2;

  public TestLSPSignatureHelp()
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
  public async Task signature_shown_on_open_paren()
  {
    await SendInit(srv);

    var result = await GetSignatureHelp(srv, uri1, "test1(");
    Assert.NotNull(result);
    Assert.NotEmpty(result.Signatures);
    Assert.Equal("func float test1(float k)", result.Signatures.First().Label);
  }

  [Fact]
  public async Task active_parameter_first()
  {
    await SendInit(srv);

    var result = await GetSignatureHelp(srv, uri1, "test1(");
    Assert.Equal(0, result.ActiveParameter);
  }

  [Fact]
  public async Task active_parameter_second()
  {
    await SendInit(srv);

    // cursor right after ',' in "MakeFoo(1," — second argument active
    var result = await GetSignatureHelp(srv, uri1, "MakeFoo(1,");
    Assert.Equal(1, result.ActiveParameter);
  }

  [Fact]
  public async Task parameters_listed()
  {
    await SendInit(srv);

    var result = await GetSignatureHelp(srv, uri1, "MakeFoo(1,");
    Assert.NotNull(result);
    var sig = result.Signatures.First();
    var labels = sig.Parameters.Select(p => p.Label.Label).ToList();
    Assert.Equal(2, labels.Count);
    Assert.Equal("int x", labels[0]);
    Assert.Equal("int y", labels[1]);
  }

  [Fact]
  public async Task no_signature_outside_call()
  {
    await SendInit(srv);

    // cursor after the closing ')' — not inside a call
    var result = await GetSignatureHelp(srv, uri1, "test1(42)");
    Assert.Null(result);
  }

  [Fact]
  public async Task signature_for_imported_function()
  {
    await SendInit(srv);

    var result = await GetSignatureHelp(srv, uri2, "test1(");
    Assert.NotNull(result);
    Assert.Equal("func float test1(float k)", result.Signatures.First().Label);
  }

  [Fact]
  public async Task active_parameter_imported_second()
  {
    await SendInit(srv);

    var result = await GetSignatureHelp(srv, uri2, "MakeFoo(1,");
    Assert.NotNull(result);
    Assert.Equal(1, result.ActiveParameter);
  }

  [Fact]
  public async Task method_no_this_in_parameters()
  {
    await SendInit(srv);

    // "Method" is a non-static class method; 'this' must not appear in the param list
    // "local_foo.Method(" — cursor right after '('
    var result = await GetSignatureHelp(srv, uri1, "local_foo.Method(");
    Assert.NotNull(result);
    var sig = result.Signatures.First();
    var labels = sig.Parameters.Select(p => p.Label.Label).ToList();
    Assert.Equal(2, labels.Count);
    Assert.Equal("int x", labels[0]);
    Assert.Equal("int y", labels[1]);
    Assert.DoesNotContain("this", sig.Label);
  }
}
