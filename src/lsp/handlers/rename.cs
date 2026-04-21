using System.Collections.Generic;
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

    var refs = _workspace.FindRefs(symb);
    if(refs.Count == 0)
      return null;

    var new_name = request.NewName;

    // Group edits by document URI
    var changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>();
    var by_uri = new Dictionary<string, List<TextEdit>>();

    foreach(var loc in refs)
    {
      var uri_key = loc.Uri.ToString();
      if(!by_uri.TryGetValue(uri_key, out var edits))
      {
        edits = new List<TextEdit>();
        by_uri[uri_key] = edits;
        changes[loc.Uri] = edits;
      }
      // FindRefs stores inclusive end positions (ANTLR convention); LSP needs exclusive end.
      var range = new Range(loc.Range.Start, new Position(loc.Range.End.Line, loc.Range.End.Character + 1));
      edits.Add(new TextEdit { Range = range, NewText = new_name });
    }

    return new WorkspaceEdit { Changes = changes };
  }

}
