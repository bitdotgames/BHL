using System;
using System.Threading.Tasks;
using bhl.lsp;
using Xunit;

public class TestLSPFindRefs : TestLSPShared
{
  string bhl1 = @"
  func float test1(float k) 
  {
    return 0
  }

  func test2() 
  {
    test1(42)
  }

  func test3()
  {
    int upval = 1 //upval value
    func() {
      upval = upval + 1 //upval1
    }()
  }

  func test4(int foo, int bar, int upval) //upval arg
  {
    sink(
      func() {
        func() {
          upval = upval + 1 //upval2 
        }()
      }
    )
  }

  func sink(func() ptr) {}

  class Foo {} //class Foo
  ";
  
  string bhl2 = @"
  import ""bhl1""

  func float test5(float k, int j)
  {
    test1(24)
    return 0
  }

  func Foo test6() //test6 
  {
    return new Foo //new Foo
  }
  ";

  Server srv;

  bhl.lsp.proto.Uri uri1;
  bhl.lsp.proto.Uri uri2;

  public TestLSPFindRefs()
  {
    var ws = new Workspace();

    srv = new Server(NoLogger(), NoConnection(), ws);
    srv.AttachService(new bhl.lsp.TextDocumentFindReferencesService(srv));

    CleanTestFiles();

    uri1 = MakeTestDocument("bhl1.bhl", bhl1);
    uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    var ts = new bhl.Types();

    ws.Init(ts, GetTestProjConf());
    ws.IndexFiles();
  }

  [Fact]
  public async Task _1()
  {
    AssertEqual(
      await srv.Handle(FindReferencesReq(uri1, "st1(42)")),
      FindReferencesRsp(
        new UriNeedle(uri1, "func float test1(float k)", end_line_offset: 3),
        new UriNeedle(uri1, "test1(42)", end_column_offset: 4),
        new UriNeedle(uri2, "test1(24)", end_column_offset: 4)
      )
    );
  }

  [Fact]
  public async Task _2()
  {
    AssertEqual(
      await srv.Handle(FindReferencesReq(uri1, "pval + 1")),
      FindReferencesRsp(
        new UriNeedle(uri1, "int upval = 1", end_column_offset: 8),
        new UriNeedle(uri1, "upval = upval + 1 //upval1", end_column_offset: 4),
        new UriNeedle(uri1, "upval + 1 //upval1", end_column_offset: 4)
      )
    );
  }

  [Fact]
  public async Task _3()
  {
    AssertEqual(
      await srv.Handle(FindReferencesReq(uri1, "upval) //upval arg")),
      FindReferencesRsp(
        new UriNeedle(uri1, "int upval) //upval arg", end_column_offset: 8),
        new UriNeedle(uri1, "upval = upval + 1 //upval2", end_column_offset: 4),
        new UriNeedle(uri1, "upval + 1 //upval2", end_column_offset: 4)
      )
    );
  }

  [Fact]
  public async Task _4()
  {
    AssertEqual(
      await srv.Handle(FindReferencesReq(uri2, "o test6() //test6")),
      FindReferencesRsp(
        new UriNeedle(uri1, "Foo {} //class Foo", end_column_offset: 2),
        new UriNeedle(uri2, "Foo test6() //test6", end_column_offset: 2),
        new UriNeedle(uri2, "Foo //new Foo", end_column_offset: 2)
      )
    );
  }
}