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

public class TextDocumentCodeActionHandler : CodeActionHandlerBase
{
  private readonly ILogger _logger;
  private readonly Workspace _workspace;

  public TextDocumentCodeActionHandler(ILogger<TextDocumentCodeActionHandler> logger, Workspace workspace)
  {
    _logger = logger;
    _workspace = workspace;
  }

  protected override CodeActionRegistrationOptions CreateRegistrationOptions(CodeActionCapability capability,
    ClientCapabilities clientCapabilities)
  {
    return new()
    {
      DocumentSelector = TextDocumentSelector.ForLanguage("bhl"),
      CodeActionKinds = new Container<CodeActionKind>(CodeActionKind.QuickFix, CodeActionKind.SourceOrganizeImports),
      ResolveProvider = false,
    };
  }

  public override Task<CommandOrCodeActionContainer> Handle(CodeActionParams request,
    CancellationToken cancellationToken)
  {
    var uri = request.TextDocument.Uri;

    // Clients that explicitly ask for one kind of action (e.g. the "Organize Imports" command,
    // or the inline quick-fix lightbulb) scope the request via Context.Only; when it's absent,
    // offer whatever applies.
    var only = request.Context?.Only;
    bool wants_quick_fix = only == null || only.Any(k => k.Equals(CodeActionKind.QuickFix));
    bool wants_organize = only == null || only.Any(k => k.Equals(CodeActionKind.SourceOrganizeImports));

    var missing_edits = _workspace.GetMissingImportEdits(uri);
    var actions = new List<CommandOrCodeAction>();

    if(wants_quick_fix)
    {
      // Only offer the fix when the client is actually asking about a "not resolved" diagnostic
      // (the client scopes Context.Diagnostics to ones overlapping the requested range) - this
      // keeps the quick-fix tied to the erroring identifier instead of appearing anywhere in the
      // file just because some import is missing somewhere.
      var diagnostics = request.Context?.Diagnostics?
        .Where(d => d.Message != null && d.Message.Contains("not resolved"))
        .ToList();

      if(diagnostics != null && diagnostics.Count > 0 && missing_edits != null)
      {
        actions.Add(new CommandOrCodeAction(new CodeAction
        {
          Title = "Add missing imports",
          Kind = CodeActionKind.QuickFix,
          Diagnostics = new Container<Diagnostic>(diagnostics),
          Edit = MakeWorkspaceEdit(uri, missing_edits),
        }));
      }
    }

    if(wants_organize)
    {
      // Document-wide, unlike the quick-fix above: adds every missing import and drops every
      // unused one, regardless of where the cursor/selection is.
      var unused_edits = _workspace.GetUnusedImportEdits(uri);

      var combined = new List<TextEdit>();
      if(missing_edits != null)
        combined.AddRange(missing_edits);
      if(unused_edits != null)
        combined.AddRange(unused_edits);

      if(combined.Count > 0)
      {
        actions.Add(new CommandOrCodeAction(new CodeAction
        {
          Title = "Organize imports",
          Kind = CodeActionKind.SourceOrganizeImports,
          Edit = MakeWorkspaceEdit(uri, combined),
        }));
      }
    }

    return Task.FromResult<CommandOrCodeActionContainer>(new CommandOrCodeActionContainer(actions));
  }

  public override Task<CodeAction> Handle(CodeAction request, CancellationToken cancellationToken) =>
    Task.FromResult(request);

  static WorkspaceEdit MakeWorkspaceEdit(DocumentUri uri, List<TextEdit> edits) => new()
  {
    DocumentChanges = new Container<WorkspaceEditDocumentChange>(
      new WorkspaceEditDocumentChange(new TextDocumentEdit
      {
        TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = uri },
        Edits = new TextEditContainer(edits),
      })
    ),
  };
}
