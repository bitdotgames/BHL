using System.Threading.Tasks;
using bhl;
using bhl.lsp;
using Xunit;

public class TestLSPGoToDefinition : TestLSPShared
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

  func test2() 
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

  func ErrorCodes test4() 
  {
    test2()
    foo.BAR = 1

    ErrorCodes err = ErrorCodes.Bad //error code 
    return err
  }

  func test5() 
  {
    TEST() //native call
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

  Server srv;

  bhl.lsp.proto.Uri uri1;
  bhl.lsp.proto.Uri uri2;

  FuncSymbolNative fn_TEST;

  public TestLSPGoToDefinition()
  {
    var ws = new Workspace();
  
    srv = new Server(NoLogger(), NoConnection(), ws);
    srv.AttachService(new bhl.lsp.TextDocumentGoToService(srv));

    CleanTestFiles();

    uri1 = MakeTestDocument("bhl1.bhl", bhl1);
    uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    var ts = new bhl.Types();
    fn_TEST = new FuncSymbolNative(new Origin(), "TEST", Types.Void, null);
    ts.ns.Define(fn_TEST);

    ws.Init(ts, GetTestProjConf());
    ws.IndexFiles();
  }

  [Fact]
  public async Task _1()
  {
    AssertEqual(
      await srv.Handle(GoToDefinitionReq(uri1, "st1(42)")),
      GoToDefinitionRsp(uri1, "func float test1(float k)", end_line_offset: 3)
    );
  }
  
  [Fact]
  public async Task _2()
  {
    AssertEqual(
      await srv.Handle(GoToDefinitionReq(uri2, "est2()")),
      GoToDefinitionRsp(uri1, "func test2()", end_line_offset: 4)
    );
  }
  
  [Fact]
  public async Task _3()
  {
    AssertEqual(
      await srv.Handle(GoToDefinitionReq(uri1, "oo foo = {")),
      GoToDefinitionRsp(uri1, "class Foo {", end_line_offset: 2)
    );
  }

  [Fact]
  public async Task _4()
  {
    AssertEqual(
      await srv.Handle(GoToDefinitionReq(uri1, "Foo //create new Foo")),
      GoToDefinitionRsp(uri1, "class Foo {", end_line_offset: 2)
    );
  }

  [Fact]
  public async Task _5()
  {
    AssertEqual(
      await srv.Handle(GoToDefinitionReq(uri1, "BAR : 0")),
      GoToDefinitionRsp(uri1, "int BAR", end_column_offset: 6)
    );
  }

  [Fact]
  public async Task _6()
  {
    AssertEqual(
      await srv.Handle(GoToDefinitionReq(uri2, "BAR = 1")),
      GoToDefinitionRsp(uri1, "int BAR", end_column_offset: 6)
    );
  }

  [Fact]
  public async Task _7()
  {
    AssertEqual(
      await srv.Handle(GoToDefinitionReq(uri2, "AR = 1")),
      GoToDefinitionRsp(uri1, "int BAR", end_column_offset: 6)
    );
  }

  [Fact]
  public async Task _8()
  {
    AssertEqual(
      await srv.Handle(GoToDefinitionReq(uri2, "foo.BAR")),
      GoToDefinitionRsp(uri1, "foo = {", end_column_offset: 2)
    );
  }

  [Fact]
  public async Task _9()
  {
    AssertEqual(
      await srv.Handle(GoToDefinitionReq(uri2, "k = 10")),
      GoToDefinitionRsp(uri2, "k, int j)")
    );
  }

  [Fact]
  public async Task _10()
  {
    AssertEqual(
      await srv.Handle(GoToDefinitionReq(uri2, "j = 100")),
      GoToDefinitionRsp(uri2, "j)")
    );
  }

  [Fact]
  public async Task _11()
  {
    AssertEqual(
      await srv.Handle(GoToDefinitionReq(uri2, "test4()")),
      GoToDefinitionRsp(uri2, "func ErrorCodes test4()", end_line_offset: 7)
    );
  }

  [Fact]
  public async Task _12()
  {
    AssertEqual(
      await srv.Handle(GoToDefinitionReq(uri2, "rrorCodes err")),
      GoToDefinitionRsp(uri1, "enum ErrorCodes", end_line_offset: 3)
    );
  }

  [Fact]
  public async Task _13()
  {
    AssertEqual(
      await srv.Handle(GoToDefinitionReq(uri2, "Bad //error code")),
      GoToDefinitionRsp(uri1, "Bad = 1", end_column_offset: 6)
    );
  }

  [Fact]
  public async Task _14()
  {
    var rsp = await srv.Handle(GoToDefinitionReq(uri2, "EST() //native call"));
    AssertContains(rsp, "test_lsp_go_to_definition.cs");
    AssertContains(rsp, "\"start\":{\"line\":"+(fn_TEST.origin.source_range.start.line-1)+",\"character\":1},\"end\":{\"line\":"+(fn_TEST.origin.source_range.start.line-1)+",\"character\":1}}");
  }

  [Fact]
  public async Task _15()
  {
    AssertEqual(
      await srv.Handle(GoToDefinitionReq(uri2, "pval + 1")),
      GoToDefinitionRsp(uri2, "upval = 1", end_column_offset: 4)
    );
  }
}