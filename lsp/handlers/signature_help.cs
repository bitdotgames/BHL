using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace bhl.lsp.handlers;

public class TextDocumentSignatureHelpHandler : SignatureHelpHandlerBase
{
  private readonly ILogger _logger;
  private readonly Workspace _workspace;

  public TextDocumentSignatureHelpHandler(ILogger<TextDocumentSignatureHelpHandler> logger, Workspace workspace)
  {
    _logger = logger;
    _workspace = workspace;
  }

  protected override SignatureHelpRegistrationOptions CreateRegistrationOptions(
    SignatureHelpCapability capability, ClientCapabilities clientCapabilities)
  {
    return new()
    {
      DocumentSelector = TextDocumentSelector.ForLanguage("bhl"),
      TriggerCharacters = new Container<string>("(", ","),
    };
  }

  public override async Task<SignatureHelp> Handle(SignatureHelpParams request, CancellationToken ct)
  {
    await _workspace.SetupProjectIfEmptyAsync(request.TextDocument.Uri.PathNormalized(), ct);
    return _workspace.GetSignatureHelp(request.TextDocument.Uri, request.Position);
  }
}
