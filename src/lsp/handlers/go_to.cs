using System.Threading.Tasks;

namespace bhl.lsp.handlers;

//public class TextDocumentGoToService : IService
//{
//  Workspace workspace;
//
//  public TextDocumentGoToService(Server srv)
//  {
//    this.workspace = srv.workspace;
//  }
//
//  public void GetCapabilities(ClientCapabilities cc, ref ServerCapabilities sc)
//  {
//    if(cc.textDocument?.definition != null)
//      sc.definitionProvider = true; //textDocument/definition
//
//    if(cc.textDocument?.declaration != null)
//      sc.declarationProvider = false; //textDocument/declaration
//
//    if(cc.textDocument?.typeDefinition != null)
//      sc.typeDefinitionProvider = false; //textDocument/typeDefinition
//
//    if(cc.textDocument?.implementation != null)
//      sc.implementationProvider = false; //textDocument/implementation
//  }
//
//  /**
//   * The result type LocationLink[] got introduced with version 3.14.0
//   * and depends on the corresponding client capability textDocument.definition.linkSupport.
//   */
//  [RpcMethod("textDocument/definition")]
//  public Task<RpcResult> GotoDefinition(DefinitionParams args)
//  {
//    var document = workspace.GetOrLoadDocument(args.textDocument.uri);
//
//    if(document != null)
//    {
//      var symb = document.FindSymbol((int)args.position.line, (int)args.position.character);
//      if(symb != null)
//      {
//        var range = (bhl.lsp.proto.Range)symb.origin.source_range;
//        return Task.FromResult(new RpcResult(new Location
//        {
//          uri = new proto.Uri(symb.origin.source_file),
//          range = range
//        }));
//      }
//    }
//
//    return Task.FromResult(new RpcResult(new Location()));
//  }
//
//  /**
//   * The result type LocationLink[] got introduced with version 3.14.0
//   * and depends on the corresponding client capability textDocument.declaration.linkSupport.
//   */
//  [RpcMethod("textDocument/declaration")]
//  public Task<RpcResult> GotoDeclaration(DeclarationParams args)
//  {
//    return Task.FromResult(RpcResult.Error(
//      ErrorCodes.RequestFailed,
//      "Not supported"
//    ));
//  }
//
//  /**
//   * The result type LocationLink[] got introduced with version 3.14.0
//   * and depends on the corresponding client capability textDocument.typeDefinition.linkSupport.
//   */
//  [RpcMethod("textDocument/typeDefinition")]
//  public Task<RpcResult> GotoTypeDefinition(TypeDefinitionParams args)
//  {
//    return Task.FromResult(RpcResult.Error(
//      ErrorCodes.RequestFailed,
//      "Not supported"
//    ));
//  }
//
//  /**
//   * The result type LocationLink[] got introduced with version 3.14.0
//   * and depends on the corresponding client capability textDocument.implementation.linkSupport.
//   */
//  [RpcMethod("textDocument/implementation")]
//  public Task<RpcResult> GotoImplementation(ImplementationParams args)
//  {
//    return Task.FromResult(RpcResult.Error(
//      ErrorCodes.RequestFailed,
//      "Not supported"
//    ));
//  }
//}
