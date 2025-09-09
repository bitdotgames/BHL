using System.Threading.Tasks;
using bhl;
using bhl.lsp;
using Xunit;

public class TestLSPSignatureHelp : TestLSPShared
{
  [Fact(Skip = "TODO: not implemented yet")]
  public async Task TestSignatureHelp()
  {
    string bhl1 = @"
    func float test1(float k, float n) 
    {
      return 0
    }

    func test2() 
    {
      test1() //signature help 1
    }

    func test3() 
    {
      test1(5.2, //signature help 2
    }
    ";

    var ws = new Workspace();

    var srv = new Server(NoLogger(), NoConnection(), ws);
    srv.AttachService(new bhl.lsp.TextDocumentSignatureHelpService(srv));

    CleanTestFiles();

    var uri = MakeTestDocument("bhl1.bhl", bhl1);

    ws.Init(new bhl.Types(), GetTestProjConf());
    ws.IndexFiles();

    AssertEqual(
      await srv.Handle(SignatureHelpReq(uri, ") //signature help 1")),
      "{\"id\":1,\"result\":{\"signatures\":[{\"label\":\"func float test1(float,float)\",\"documentation\":null,\"parameters\":[" +
      "{\"label\":\"float k\",\"documentation\":\"\"},{\"label\":\"float n\",\"documentation\":\"\"}]," +
      "\"activeParameter\":0}],\"activeSignature\":0,\"activeParameter\":0},\"jsonrpc\":\"2.0\"}"
    );

    AssertEqual(
      await srv.Handle(SignatureHelpReq(uri, ", //signature help 2")),
      "{\"id\":1,\"result\":{\"signatures\":[{\"label\":\"func float test1(float,float)\",\"documentation\":null,\"parameters\":[" +
      "{\"label\":\"float k\",\"documentation\":\"\"},{\"label\":\"float n\",\"documentation\":\"\"}]," +
      "\"activeParameter\":0}],\"activeSignature\":0,\"activeParameter\":0},\"jsonrpc\":\"2.0\"}"
    );
  }
}