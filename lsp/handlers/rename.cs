using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace bhl.lsp.handlers;

public class TextDocumentRenameHandler : RenameHandlerBase
{
  private readonly ILogger _logger;
  private readonly Workspace _workspace;

  public TextDocumentRenameHandler(ILogger<TextDocumentRenameHandler> logger, Workspace workspace)
  {
    _logger = logger;
    _workspace = workspace;
  }

  protected override RenameRegistrationOptions CreateRegistrationOptions(RenameCapability capability,
    ClientCapabilities clientCapabilities)
  {
    return new()
    {
      DocumentSelector = TextDocumentSelector.ForLanguage("bhl"),
    };
  }

  public override async Task<WorkspaceEdit> Handle(RenameParams request, CancellationToken cancellationToken)
  {
    await _workspace.SetupProjectIfEmptyAsync(request.TextDocument.Uri.PathNormalized(), cancellationToken);

    var document = _workspace.GetOrLoadDocument(request.TextDocument.Uri);
    if(document == null)
      return null;

    var symb = document.FindSymbol(request.Position);
    if(symb == null)
      return null;

    var refs = _workspace.FindRefs(symb)
      .Where(loc => !loc.Uri.Path.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
      .ToList();
    if(refs.Count == 0)
      return null;

    var new_name = request.NewName;

    // Group edits by document URI, preserving insertion order for deterministic output
    var by_uri = new Dictionary<string, (DocumentUri Uri, List<TextEdit> Edits)>();
    var uri_order = new List<string>();

    foreach(var loc in refs)
    {
      var uri_key = loc.Uri.ToString();
      if(!by_uri.TryGetValue(uri_key, out var entry))
      {
        entry = (loc.Uri, new List<TextEdit>());
        by_uri[uri_key] = entry;
        uri_order.Add(uri_key);
      }
      // loc.Range already went through FromAntlr2LspRange() inside FindRefs, which converts
      // ANTLR's inclusive end-column to LSP's exclusive end (+1) — using it as-is here.
      entry.Edits.Add(new TextEdit { Range = loc.Range, NewText = new_name });
    }

    var document_changes = uri_order.Select(k => (WorkspaceEditDocumentChange)new TextDocumentEdit
    {
      TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = by_uri[k].Uri },
      Edits = new TextEditContainer(by_uri[k].Edits),
    });

    return new WorkspaceEdit
    {
      DocumentChanges = new Container<WorkspaceEditDocumentChange>(document_changes),
    };
  }

}
