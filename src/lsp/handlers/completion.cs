using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace bhl.lsp.handlers;

public class TextDocumentCompletionHandler : CompletionHandlerBase
{
  private readonly ILogger _logger;
  private readonly Workspace _workspace;

  public TextDocumentCompletionHandler(ILogger<TextDocumentCompletionHandler> logger, Workspace workspace)
  {
    _logger = logger;
    _workspace = workspace;
  }

  protected override CompletionRegistrationOptions CreateRegistrationOptions(
    CompletionCapability capability, ClientCapabilities clientCapabilities)
  {
    return new()
    {
      DocumentSelector = TextDocumentSelector.ForLanguage("bhl"),
      TriggerCharacters = new Container<string>("."),
    };
  }

  public override async Task<CompletionList> Handle(CompletionParams request, CancellationToken ct)
  {
    await _workspace.SetupProjectIfEmptyAsync(request.TextDocument.Uri.PathFixed(), ct);

    var items = _workspace.GetCompletions(request.TextDocument.Uri);
    return new CompletionList(items);
  }

  public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken ct)
  {
    return Task.FromResult(request);
  }
}
