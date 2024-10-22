using System.Threading.Tasks;
using bhl.lsp;
using Xunit;

public class TestLSPHover : TestLSPShared
{
  string bhl1 = @"
  func float test1(float k, float n) 
  {
    return k //return k
  }

  func test2() 
  {
    test1() //hover 1
  }
  ";
  
  Server srv;

  bhl.lsp.proto.Uri uri;

  public TestLSPHover()
  {
    var ws = new Workspace();
    srv = new Server(NoLogger(), NoConnection(), ws);
    srv.AttachService(new bhl.lsp.TextDocumentHoverService(srv));

    CleanTestFiles();

    uri = MakeTestDocument("bhl1.bhl", bhl1);

    ws.Init(new bhl.Types(), GetTestProjConf());
    ws.IndexFiles();
  }

  [Fact]
  public async Task _1()
  {
    AssertEqual(
      await srv.Handle(HoverReq(uri, "est1() //hover 1")),
      "{\"id\":1,\"result\":{\"contents\":{\"kind\":\"plaintext\",\"value\":\"func float test1(float k,float n)\"}},\"jsonrpc\":\"2.0\"}"
    );
  }

  [Fact]
  public async Task _2()
  {
    AssertEqual(
      await srv.Handle(HoverReq(uri, "k //return k")),
      "{\"id\":1,\"result\":{\"contents\":{\"kind\":\"plaintext\",\"value\":\"float k\"}},\"jsonrpc\":\"2.0\"}"
    );
  }
}
