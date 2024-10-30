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
      //TODO:
      //opts.triggerCharacters = new[] { "A" };
      sc.completionProvider = opts;
    }
  }

  [RpcMethod("textDocument/completion")]
  public Task<RpcResult> Completion(CompletionParams args)
  {
    var list = new CompletionList();
    list.isIncomplete = false;
    
    list.items = new List<CompletionItem>();

    //TODO:
    //var item = new CompletionItem();
    //item.label = "Boo";
    //item.insertText = item.label;
    //list.items.Add(item);
    
    return Task.FromResult(new RpcResult(list));
  }
}
 
}