using System.Collections.Generic;
using System.Threading.Tasks;
using bhl.lsp.proto;

namespace bhl.lsp {

public class TextDocumentSignatureHelpService : IService
{
  Workspace workspace;

  public TextDocumentSignatureHelpService(Server srv)
  {
    this.workspace = srv.workspace;
  }

  public void GetCapabilities(ClientCapabilities cc, ref ServerCapabilities sc)
  {
    //TODO:
    //if(cc.textDocument?.signatureHelp != null)
    //{
    //  sc.signatureHelpProvider = new SignatureHelpOptions { triggerCharacters = new[] {"(", ","} };
    //}
  }

  [RpcMethod("textDocument/signatureHelp")]
  public Task<RpcResult> SignatureHelp(SignatureHelpParams args)
  {
    var document = workspace.GetOrLoadDocument(args.textDocument.uri);

    if(document != null)
    {
      var node = document.FindTerminalNode((int)args.position.line, (int)args.position.character);
      if(node != null)
      {
        //Console.WriteLine("NODE " + node.GetType().Name + " " + node.GetText() + 
        //  ", parent " + node.Parent.GetType().Name + " " + node.Parent.GetText() + 
        //  " parent parent " + node.Parent.Parent.GetType().Name
        //);
        var symb = document.FindFuncByCallStatement(node);
        if(symb != null)
        {
          var ps = GetSignatureParams(symb);

          return Task.FromResult(new RpcResult(new SignatureHelp() {
              signatures = new SignatureInformation[] {
                new SignatureInformation() {
                  label = symb.ToString(),
                  parameters = ps,
                  activeParameter = 0
                }
              },
              activeSignature = 0,
              activeParameter = 0
            }
          ));
        }
      }
    }
    
    return Task.FromResult(new RpcResult(null));
  }

  static ParameterInformation[] GetSignatureParams(FuncSymbol symb)
  {
    var ps = new List<ParameterInformation>();

    foreach(var s in symb)
    {
      ps.Add(new ParameterInformation() {
        label = (s as VariableSymbol).type.Get() + " " + s,
        documentation = ""
      });
    }

    return ps.ToArray();
  }
}

}
