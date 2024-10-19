using System.Threading.Tasks;
using bhl.lsp.proto;

namespace bhl.lsp {
public class TextDocumentHoverService : IService
{
  Workspace workspace;

  public TextDocumentHoverService(Server srv)
  {
    this.workspace = srv.workspace;
  }

  public void GetCapabilities(ClientCapabilities cc, ref ServerCapabilities sc)
  {
    if(cc.textDocument?.hover != null)
      sc.hoverProvider = true; //textDocument/hover
  }

  [RpcMethod("textDocument/hover")]
  public Task<RpcResult> Hover(TextDocumentPositionParams args)
  {
    var document = workspace.GetOrLoadDocument(args.textDocument.uri);

    Symbol symb = null;
    if(document != null)
      symb = document.FindSymbol((int)args.position.line, (int)args.position.character);

    if(symb != null)
    {
      return Task.FromResult(new RpcResult(new Hover
      {
        contents = new MarkupContent
        {
          kind = "plaintext",
          value = symb.ToString()
        }
      }));
    }
    
    return Task.FromResult(new RpcResult(null));
  }
}

}