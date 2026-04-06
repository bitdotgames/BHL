using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;


namespace bhl.lsp.handlers;

public class TextDocumentReferencesHandler : ReferencesHandlerBase
{
  private readonly ILogger _logger;
  private readonly Workspace _workspace;

  public TextDocumentReferencesHandler(ILogger<TextDocumentReferencesHandler> logger, Workspace workspace)
  {
    _logger = logger;
    _workspace = workspace;
  }

  //TODO: does it provide necessary capabilities?
  protected override ReferenceRegistrationOptions CreateRegistrationOptions(ReferenceCapability capability,
    ClientCapabilities clientCapabilities)
  {
    return new()
    {
      DocumentSelector = TextDocumentSelector.ForLanguage("bhl")
    };
  }

  public override async Task<LocationContainer> Handle(ReferenceParams request, CancellationToken cancellationToken)
  {
    await _workspace.SetupProjectIfEmptyAsync(request.TextDocument.Uri.PathFixed(), cancellationToken);

    var document = _workspace.GetOrLoadDocument(request.TextDocument.Uri);

    if(document != null)
    {
      var symb = document.FindSymbol(request.Position);
      if(symb != null)
        return new LocationContainer(_workspace.FindRefs(symb));
    }

    return new LocationContainer();
  }
}
