using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace bhl.lsp.handlers;

public class TextDocumentDefinitionHandler : DefinitionHandlerBase
{
  private readonly ILogger _logger;
  private readonly Workspace _workspace;

  public TextDocumentDefinitionHandler(ILogger<TextDocumentReferencesHandler> logger, Workspace workspace)
  {
    _logger = logger;
    _workspace = workspace;
  }

  //TODO: does it provide necessary capabilities?
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
  protected override DefinitionRegistrationOptions CreateRegistrationOptions(DefinitionCapability capability,
    ClientCapabilities clientCapabilities)
  {
    return new()
    {
      DocumentSelector = TextDocumentSelector.ForLanguage("bhl")
    };
  }

  public override async Task<LocationOrLocationLinks> Handle(DefinitionParams request, CancellationToken cancellationToken)
  {
    await _workspace.SetupIfEmpty(request.TextDocument.Uri.Path);

    var document = _workspace.GetOrLoadDocument(request.TextDocument.Uri);

    if(document != null)
    {
      var symb = document.FindSymbol(request.Position.FromLsp2Antlr());
      if(symb != null)
      {
        var range = symb.origin.source_range;
        return new LocationOrLocationLinks(
          new Location()
          {
            Uri = symb.origin.source_file,
            Range = symb.origin.source_range.FromAntlr2Lsp().ToRange()
          });
      }
    }

    return null;
  }
}

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
