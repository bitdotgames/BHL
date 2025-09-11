using System.Threading.Tasks;
using bhl;
using bhl.lsp;
using Xunit;

//public class TestLSPSemanticTokens : TestLSPShared
//{
//  [Fact]
//  public async Task TestSemanticTokensBasic()
//  {
//    string bhl = @"
//    class BHL_M3Globals
//    {
//      []string match_chips_sfx
//    }
//    ";
//
//    var ws = new Workspace();
//
//    var srv = new ServerCreator(NoLogger(), NoConnection(), ws);
//    srv.AttachService(new bhl.lsp.TextDocumentSemanticTokensService(srv));
//
//    CleanTestFiles();
//
//    var uri1 = MakeTestDocument("bhl1.bhl", bhl);
//
//    var ts = new bhl.Types();
//    ws.Init(ts, GetTestProjConf());
//    ws.IndexFiles();
//
//    var json = "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/semanticTokens/full\", \"params\":";
//    json += "{\"textDocument\": {\"uri\": \"" + uri1.ToString() + "\"}}}";
//
//    AssertEqual(
//      await srv.Handle(json),
//      "{\"id\":1,\"result\":{\"data\":" +
//      "[1,4,5,6,0," + "0,6,13,0,0," + "2,8,6,5,0," + "0,7,15,2,2]},\"jsonrpc\":\"2.0\"}"
//    );
//  }
//}
