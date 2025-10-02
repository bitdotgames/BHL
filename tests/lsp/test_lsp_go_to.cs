using System;
using System.Threading.Tasks;
using bhl;
using bhl.lsp;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Xunit;

public class TestLSPGoToDefinition : TestLSPShared, IDisposable
{
  string bhl1 = @"
  class Foo {
    int BAR
  }

  Foo foo = {
    BAR : 0
  }

  enum ErrorCodes {
    Ok = 0
    Bad = 1
  }

  func float test1(float k)
  {
    return 0
  }

  func test2() //test2
  {
    var tmp_foo = new Foo //create new Foo
    test1(42)
  }
  ";

  string bhl2 = @"
  import ""bhl1""

  func float test3(float k, int j)
  {
    k = 10
    j = 100
    return 0
  }

  func ErrorCodes test4() //test4
  {
    test2() //call test2()
    foo.BAR = 1

    ErrorCodes err = ErrorCodes.Bad //error code
    return err
  }

  func test5()
  {
    TEST() //native call
    test4() //call test4()
  }

  func test6()
  {
    int upval = 1 //upval value
    func() {
      func () {
        upval = upval + 1
      }()
    }()
  }
  ";

  TestLSPHost srv;

  DocumentUri uri1;
  DocumentUri uri2;

  FuncSymbolNative fn_TEST;

  public TestLSPGoToDefinition()
  {
    var ws = new Workspace();
    srv = NewTestServer(ws);

    CleanTestFiles();

    uri1 = MakeTestDocument("bhl1.bhl", bhl1);
    uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    var ts = new bhl.Types();
    fn_TEST = new FuncSymbolNative(new Origin(), "TEST", Types.Void, null);
    ts.ns.Define(fn_TEST);

    ws.Init(ts, MakeTestProjConf());
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
      GoToDefinitionRsp(uri1, "func float test1(float k)", end_line_offset: 3),
      await GoToDefinition(srv, uri1, "st1(42)")
    );
  }

  [Fact]
  public async Task _2()
  {
    await SendInit(srv);

    Assert.Equal(
      GoToDefinitionRsp(uri1, "func test2() //test2", end_line_offset: 4),
      await GoToDefinition(srv, uri2, "est2() //call test2()")
    );
  }

  [Fact]
  public async Task _3()
  {
    await SendInit(srv);

    Assert.Equal(
      GoToDefinitionRsp(uri1, "class Foo {", end_line_offset: 2),
      await GoToDefinition(srv, uri1, "oo foo = {")
    );
  }

  [Fact]
  public async Task _4()
  {
    await SendInit(srv);

    Assert.Equal(
      GoToDefinitionRsp(uri1, "class Foo {", end_line_offset: 2),
      await GoToDefinition(srv, uri1, "Foo //create new Foo")
    );
  }

  [Fact]
  public async Task _5()
  {
    await SendInit(srv);

    Assert.Equal(
      GoToDefinitionRsp(uri1, "int BAR", end_column_offset: 6),
      await GoToDefinition(srv, uri1, "BAR : 0")
    );
  }

  [Fact]
  public async Task _6()
  {
    await SendInit(srv);

    Assert.Equal(
      GoToDefinitionRsp(uri1, "int BAR", end_column_offset: 6),
      await GoToDefinition(srv, uri2, "BAR = 1")
    );
  }

  [Fact]
  public async Task _7()
  {
    await SendInit(srv);

    Assert.Equal(
      GoToDefinitionRsp(uri1, "int BAR", end_column_offset: 6),
      await GoToDefinition(srv, uri2, "AR = 1")
    );
  }

  [Fact]
  public async Task _8()
  {
    await SendInit(srv);

    Assert.Equal(
      GoToDefinitionRsp(uri1, "foo = {", end_column_offset: 2),
      await GoToDefinition(srv, uri2, "foo.BAR")
    );
  }

  [Fact]
  public async Task _9()
  {
    await SendInit(srv);

    Assert.Equal(
      GoToDefinitionRsp(uri2, "k, int j)"),
      await GoToDefinition(srv, uri2, "k = 10")
    );
  }

  [Fact]
  public async Task _10()
  {
    await SendInit(srv);

    Assert.Equal(
      GoToDefinitionRsp(uri2, "j)"),
      await GoToDefinition(srv, uri2, "j = 100")
    );
  }

  [Fact]
  public async Task _11()
  {
    await SendInit(srv);

    Assert.Equal(
      GoToDefinitionRsp(uri2, "func ErrorCodes test4() //test4", end_line_offset: 7),
      await GoToDefinition(srv, uri2, "est4() //call test4()")
    );
  }

  [Fact]
  public async Task _11_1()
  {
    await SendInit(srv);

    Assert.Equal(
      GoToDefinitionRsp(uri2, "func ErrorCodes test4() //test4", end_line_offset: 7),
      await GoToDefinition(srv, uri2, "est4() //test4")
    );
  }

  [Fact]
  public async Task _12()
  {
    await SendInit(srv);

    Assert.Equal(
      GoToDefinitionRsp(uri1, "enum ErrorCodes", end_line_offset: 3),
      await GoToDefinition(srv, uri2, "rrorCodes err")
    );
  }

  [Fact]
  public async Task _13()
  {
    await SendInit(srv);

    Assert.Equal(
      GoToDefinitionRsp(uri1, "Bad = 1", end_column_offset: 6),
      await GoToDefinition(srv, uri2, "Bad //error code")
    );
  }

  [Fact]
  public async Task _14()
  {
    await SendInit(srv);

    var rsp = await GoToDefinition(srv, uri2, "EST() //native call");
    Console.WriteLine("OUTPUT " + rsp.ToJson());
    //AssertContains(rsp, "test_lsp_go_to.cs");
    //AssertContains(rsp,
    //  "\"start\":{\"line\":" + (fn_TEST.origin.source_range.start.line - 1) + ",\"character\":1},\"end\":{\"line\":" +
    //  (fn_TEST.origin.source_range.start.line - 1) + ",\"character\":1}}");
  }

//  [Fact]
//  public async Task _15()
//  {
//    AssertEqual(
//      await srv.Handle(GoToDefinitionReq(uri2, "pval + 1")),
//      GoToDefinitionRsp(uri2, "upval = 1", end_column_offset: 4)
//    );
//  }
}
