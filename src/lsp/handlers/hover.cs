using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace bhl.lsp.handlers;

public class TextDocumentHoverHandler : HoverHandlerBase
{
  private readonly ILogger _logger;
  private readonly Workspace _workspace;

  public TextDocumentHoverHandler(ILogger<TextDocumentReferencesHandler> logger, Workspace workspace)
  {
    _logger = logger;
    _workspace = workspace;
  }

  protected override HoverRegistrationOptions CreateRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities)
  {
    return new()
    {
      DocumentSelector = TextDocumentSelector.ForLanguage("bhl")
    };
  }

  public override async Task<Hover> Handle(HoverParams request, CancellationToken cancellationToken)
  {
    await _workspace.SetupProjectIfEmptyAsync(request.TextDocument.Uri.Path, cancellationToken);

    var document = _workspace.GetOrLoadDocument(request.TextDocument.Uri);

    Symbol symb = document?.FindSymbol(request.Position.FromLsp2Antlr()) ?? null;

    return symb == null ? null : new () {
      Contents = new MarkedStringsOrMarkupContent(
        new  MarkupContent()
        {
          Kind = MarkupKind.PlainText,
          Value = symb.ToString()
        }
      )
    };
  }

}
