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

    var completions = new HashSet<Symbol>();
    if(document != null)
    {
      var node = document.FindTerminalNode((int)args.position.line, (int)args.position.character-1);
      if(node != null)
      {
        //Logger.current.Log(0, "NODE " + node.GetType().Name + " " + node.GetText() + " " + node.GetHashCode() + "; parent " + node.Parent.GetType().Name + " " + node.Parent.GetText());

        foreach(var kv in document.proc.annotated_nodes)
        {
          if(kv.Value.lsp_symbol != null && kv.Value.lsp_symbol.name.Contains(node.GetText())) 
            completions.Add(kv.Value.lsp_symbol);
        }
      }
      
      list.items = new List<CompletionItem>();

      foreach (var symbol in completions)
      {
        var item = new CompletionItem();
        item.label = symbol.name;
        item.insertText = item.label;
        list.items.Add(item);
      }
    }
    
    return Task.FromResult(new RpcResult(list));
  }
}
 
}
