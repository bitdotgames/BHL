using System;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

public class TestLSPHover : TestLSPShared, IDisposable
{
  string bhl1 = @"
  func float test1(float k = 0, float n = 1)
  {
    return k //return k
  }

  func test2()
  {
    test1() //hover 1
  }
  ";

  TestLSPHost srv;

  DocumentUri uri;

  public TestLSPHover()
  {
    srv = NewTestServer();

    CleanTestFiles();

    uri = MakeTestDocument("bhl1.bhl", bhl1);
  }

  public void Dispose()
  {
    srv.Dispose();
  }

  [Fact]
  public async Task _1()
  {
    await SendInit(srv);

    Assert.Equal(
      new Hover() { Contents = new (new MarkupContent { Kind = MarkupKind.PlainText, Value = "func float test1(float k,float n)"})},
      await GetHover(srv, uri, "est1() //hover 1")
    );
  }

  [Fact]
  public async Task _2()
  {
    await SendInit(srv);

    Assert.Equal(
      new Hover() { Contents = new (new MarkupContent { Kind = MarkupKind.PlainText, Value = "float k"})},
      await GetHover(srv, uri, "k //return k")
    );
  }
}
