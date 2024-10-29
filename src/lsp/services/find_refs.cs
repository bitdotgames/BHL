using System.Collections.Generic;
using System.Threading.Tasks;
using bhl.lsp.proto;

namespace bhl.lsp {

public class TextDocumentFindReferencesService : IService
{
  Server srv;
  Workspace workspace;

  public TextDocumentFindReferencesService(Server srv)
  {
    this.srv = srv;
    this.workspace = srv.workspace;
  }

  public void GetCapabilities(ClientCapabilities cc, ref ServerCapabilities sc)
  {
    if(cc.textDocument?.references != null)
      sc.referencesProvider = true; //textDocument/references
  }

  /**
   * The result type Location[] | null
   */
  [RpcMethod("textDocument/references")]
  public Task<RpcResult> FindReferences(ReferenceParams args)
  {
    var document = workspace.GetOrLoadDocument(args.textDocument.uri);

    var refs = new List<Location>();

    if(document != null)
    {
      var symb = document.FindSymbol((int)args.position.line, (int)args.position.character);
      if(symb != null)
      {
        //1. adding all found references
        foreach(var kv in workspace.uri2doc)
        {
          foreach(var an_kv in kv.Value.proc.annotated_nodes)
          {
            if(an_kv.Value.lsp_symbol == symb)
            {
              var range = (bhl.lsp.proto.Range)an_kv.Value.range;
              var loc = new Location {
                uri = new proto.Uri(kv.Key),
                range = range
              };
              refs.Add(loc);
            }
          }
        }

        //2. sorting by file name
        refs.Sort(
          (a, b) => {
            if(a.uri.path == b.uri.path)
              return a.range.start.line.CompareTo(b.range.start.line);
            else
              return a.uri.path.CompareTo(b.uri.path);
          }
        );

        //3. adding definition for native symbol (if any)
        if(symb is FuncSymbolNative)
        {
          refs.Add(new Location {
            uri = new proto.Uri(symb.origin.source_file),
            range = (bhl.lsp.proto.Range)symb.origin.source_range
          });
        }
      }
    }

    return Task.FromResult(new RpcResult(refs));
  }
}

}
