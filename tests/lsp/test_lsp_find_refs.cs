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

  class Bar : IFoo, Foo {} //class Bar

  enum Item //enum Item
  {
    Type = 1
  }

  interface IFoo {} //interface IFoo
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

  func Item test7() //test7
  {
    return Item.Type //new Item.Type
  } 

  func IFoo test8() //test8
  {
    return null
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
  public async Task ref_test1()
  {
    AssertEqual(
      await srv.Handle(FindReferencesReq(uri1, "st1(42)")),
      FindReferencesRsp(
        new UriNeedle(uri1, "test1(float k)", end_column_offset: 4),
        new UriNeedle(uri1, "test1(42)", end_column_offset: 4),
        new UriNeedle(uri2, "test1(24)", end_column_offset: 4)
      )
    );
  }

  [Fact]
  public async Task ref_upval()
  {
    AssertEqual(
      await srv.Handle(FindReferencesReq(uri1, "pval + 1")),
      FindReferencesRsp(
        new UriNeedle(uri1, "upval = 1", end_column_offset: 4),
        new UriNeedle(uri1, "upval = upval + 1 //upval1", end_column_offset: 4),
        new UriNeedle(uri1, "upval + 1 //upval1", end_column_offset: 4)
      )
    );
  }

  [Fact]
  public async Task ref_upval_arg()
  {
    AssertEqual(
      await srv.Handle(FindReferencesReq(uri1, "upval) //upval arg")),
      FindReferencesRsp(
        new UriNeedle(uri1, "upval) //upval arg", end_column_offset: 4),
        new UriNeedle(uri1, "upval = upval + 1 //upval2", end_column_offset: 4),
        new UriNeedle(uri1, "upval + 1 //upval2", end_column_offset: 4)
      )
    );
  }

  [Fact]
  public async Task ref_class_Foo()
  {
    AssertEqual(
      await srv.Handle(FindReferencesReq(uri2, "o test6() //test6")),
      FindReferencesRsp(
        new UriNeedle(uri1, "Foo {} //class Foo", end_column_offset: 2),
        new UriNeedle(uri1, "Foo {} //class Bar", end_column_offset: 2),
        new UriNeedle(uri2, "Foo test6() //test6", end_column_offset: 2),
        new UriNeedle(uri2, "Foo //new Foo", end_column_offset: 2)
      )
    );
  }
  
  [Fact]
  public async Task ref_enum_Item()
  {
    AssertEqual(
      await srv.Handle(FindReferencesReq(uri1, "tem //enum Item")),
      FindReferencesRsp(
        new UriNeedle(uri1, "Item //enum Item", end_column_offset: 3),
        new UriNeedle(uri2, "Item test7() //test7", end_column_offset: 3),
        new UriNeedle(uri2, "Item.Type //new Item.Type", end_column_offset: 3)
      )
    );
  }
  
  [Fact]
  public async Task ref_interface_IFoo()
  {
    AssertEqual(
      await srv.Handle(FindReferencesReq(uri2, "o test8() //test8")),
      FindReferencesRsp(
        new UriNeedle(uri1, "IFoo, Foo {} //class Bar", end_column_offset: 3),
        new UriNeedle(uri1, "IFoo {} //interface IFoo", end_column_offset: 3),
        new UriNeedle(uri2, "IFoo test8() //test8", end_column_offset: 3)
      )
    );
  }
}