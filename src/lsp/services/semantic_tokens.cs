using System.Threading.Tasks;
using bhl.lsp.proto;

namespace bhl.lsp {

public class TextDocumentSemanticTokensService : IService
{
  Workspace workspace;

  public TextDocumentSemanticTokensService(Server srv)
  {
    this.workspace = srv.workspace;
  }
  
  public void GetCapabilities(ClientCapabilities cc, ref ServerCapabilities sc)
  {
    if(cc.textDocument?.semanticTokens != null)
    {
      sc.semanticTokensProvider = new SemanticTokensOptions
      {
        full = true,
        range = false,
        legend = new SemanticTokensLegend
        {
          tokenTypes = bhl.ANTLR_Processor.SemanticTokens.token_types,
          tokenModifiers = bhl.ANTLR_Processor.SemanticTokens.modifiers
        }
      };
    }
  }

  [RpcMethod("textDocument/semanticTokens/full")]
  public Task<RpcResult> SemanticTokensFull(SemanticTokensParams args)
  {
    var document = workspace.GetOrLoadDocument(args.textDocument.uri);

    if(document?.proc != null)
    {
      return Task.FromResult(new RpcResult(new SemanticTokens
      {
        data = document.proc.GetEncodedSemanticTokens().ToArray()
      }));
    }
    else
      return Task.FromResult(new RpcResult(null));
  }
}

}
