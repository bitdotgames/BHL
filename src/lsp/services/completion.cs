using System.Collections.Generic;
using System.Threading.Tasks;
using Antlr4.Runtime.Tree;
using bhl.lsp.proto;

namespace bhl.lsp {

public class TextDocumentCompletionService : IService
{
  Workspace workspace;

  public TextDocumentCompletionService(Server srv)
  {
    this.workspace = srv.workspace;
  }

  public void GetCapabilities(ClientCapabilities cc, ref ServerCapabilities sc)
  {
    if(cc.textDocument.completion != null)
    {
      var opts = new CompletionOptions();
      opts.resolveProvider = true;
      opts.triggerCharacters = new[] { "A" };
      sc.completionProvider = opts;
    }
  }

  /**
   * The result type LocationLink[] got introduced with version 3.14.0
   * and depends on the corresponding client capability textDocument.definition.linkSupport.
   */
  [RpcMethod("textDocument/completion")]
  public Task<RpcResult> Completion(CompletionParams args)
  {
    var items = new List<CompletionItem>();

    var item = new CompletionItem();
    item.label = "Boo";
    item.insertText = item.label;
    items.Add(item);
    
    return Task.FromResult(new RpcResult(items));
  }
}
 
}