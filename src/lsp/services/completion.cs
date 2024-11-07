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
    var document = workspace.GetOrLoadDocument(args.textDocument.uri);
    
    var list = new CompletionList();
    list.isIncomplete = false;

    if(document != null)
    {
      //var symb = document.FindSymbol((int)args.position.line, (int)args.position.character);
      
      var node = document.FindTerminalNode((int)args.position.line, (int)args.position.character-1);
      if(node != null)
      {
        Logger.current.Log(0, "NODE " + node.GetType().Name + " " + node.GetText() + " " + node.GetHashCode() + "; parent " + node.Parent.GetType().Name + " " + node.Parent.GetText());
        
      }
      
      list.items = new List<CompletionItem>();

      //TODO:
      //var item = new CompletionItem();
      //item.label = "Boo";
      //item.insertText = item.label;
      //list.items.Add(item);
    }
    
    return Task.FromResult(new RpcResult(list));
  }
}
 
}
