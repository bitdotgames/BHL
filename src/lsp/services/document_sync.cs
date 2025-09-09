using System.Threading.Tasks;
using bhl.lsp.proto;

namespace bhl.lsp
{

public class TextDocumentSynchronizationService : IService
{
  Workspace workspace;

  public TextDocumentSynchronizationService(Server srv)
  {
    this.workspace = srv.workspace;
  }

  public void GetCapabilities(ClientCapabilities cc, ref ServerCapabilities sc)
  {
    if(cc.textDocument?.synchronization != null)
    {
      sc.textDocumentSync = new TextDocumentSyncOptions
      {
        openClose = true, //open and close notifications are sent to server
        change = proto.TextDocumentSyncKind.Full,
        save = false //didSave
      };
    }
  }

  [RpcMethod("textDocument/didOpen")]
  public Task<RpcResult> DidOpenTextDocument(DidOpenTextDocumentParams args)
  {
    workspace.OpenDocument(args.textDocument.uri, args.textDocument.text);

    return Task.FromResult(RpcResult.Null);
  }

  [RpcMethod("textDocument/didChange")]
  public Task<RpcResult> DidChangeTextDocument(DidChangeTextDocumentParams args)
  {
    foreach(var changes in args.contentChanges)
    {
      if(!workspace.UpdateDocument(args.textDocument.uri, changes.text))
        return Task.FromResult(RpcResult.Error(new ResponseError
        {
          code = (int)ErrorCodes.RequestFailed,
          message = "Update failed"
        }));
    }

    return Task.FromResult(RpcResult.Null);
  }

  [RpcMethod("textDocument/didClose")]
  public Task<RpcResult> DidCloseTextDocument(DidCloseTextDocumentParams args)
  {
    return Task.FromResult(RpcResult.Null);
  }

  [RpcMethod("textDocument/willSave")]
  public Task<RpcResult> WillSaveTextDocument(WillSaveTextDocumentParams args)
  {
    return Task.FromResult(RpcResult.Error(
      ErrorCodes.RequestFailed,
      "Not supported"
    ));
  }

  [RpcMethod("textDocument/willSaveWaitUntil")]
  public Task<RpcResult> WillSaveWaitUntilTextDocument(WillSaveTextDocumentParams args)
  {
    return Task.FromResult(RpcResult.Error(
      ErrorCodes.RequestFailed,
      "Not supported"
    ));
  }

  [RpcMethod("textDocument/didSave")]
  public Task<RpcResult> DidSaveTextDocument(DidSaveTextDocumentParams args)
  {
    return Task.FromResult(RpcResult.Error(
      ErrorCodes.RequestFailed,
      "Not supported"
    ));
  }
}

}