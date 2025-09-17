using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace bhl.lsp.handlers;

internal class TextDocumentHandler : TextDocumentSyncHandlerBase
{
  private readonly ILanguageServerFacade _server;
  private readonly ILogger _logger;
  private readonly ILanguageServerConfiguration _configuration;
  private readonly Workspace _workspace;

  private readonly TextDocumentSelector _textDocumentSelector = new TextDocumentSelector(
    new TextDocumentFilter
    {
      Pattern = "**/*.bhl"
    }
  );

  public TextDocumentHandler(ILogger<TextDocumentHandler> logger, ILanguageServerFacade server,
    Workspace workspace, ILanguageServerConfiguration configuration)
  {
    _logger = logger;
    _server = server;
    _configuration = configuration;
    _workspace = workspace;
  }

  public TextDocumentSyncKind SyncKind { get; } = TextDocumentSyncKind.Full;

  public override Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
  {
    _logger.LogInformation("Handle Change Document");
    return Unit.Task;
  }

  Task IndexWorkspaceIfNeededAsync(string path)
  {
    if (string.IsNullOrEmpty(_workspace.ProjConf.proj_file))
    {
      _workspace.ProjConf.src_dirs.Add(Path.GetDirectoryName(path));
      _workspace.ProjConf.inc_dirs.Add(Path.GetDirectoryName(path));
      _workspace.ProjConf.Setup();
      _workspace.IndexFiles();
    }
    return Task.CompletedTask;
  }

  public override async Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
  {
    _logger.LogInformation("Handle Open Document");

    await IndexWorkspaceIfNeededAsync(notification.TextDocument.Uri.Path);

    _workspace.OpenDocument(
      notification.TextDocument.Uri,
      notification.TextDocument.Text
      );

    CheckDiagnostics(_workspace.GetCompileErrors());

    return Unit.Value;
  }

  void CheckDiagnostics(Dictionary<string, CompileErrors> errors)
  {
    foreach(var kv in errors)
    {
      List<Diagnostic> diagnostics = new();
      foreach (var err in kv.Value)
      {
        var range = new Range(
          err.range.start.line, err.range.start.column,
          err.range.end.line, err.range.end.column
          );
        //NOTE: let's force the error to be 'one-line'
        if (range.Start.Line != range.End.Line)
        {
          range.End.Line = range.Start.Line + 1;
          range.End.Character = 0;
        }

        var current = new Diagnostic()
        {
          Severity = DiagnosticSeverity.Error,
          Range = range,
          Message = err.text
        };

        if (diagnostics.Count > 0)
        {
          var prev = diagnostics[^1];
          //NOTE: let's skip diagnostics which starts on the same line
          //      just like the previous one
          if (prev.Range.Start.Line == current.Range.Start.Line)
            continue;
        }

        diagnostics.Add(current);
      }

      var dparams = new PublishDiagnosticsParams()
      {
        Uri = DocumentUri.Parse(kv.Key),
        Diagnostics = diagnostics,
      };
      _server.TextDocument.PublishDiagnostics(dparams);
    }
  }

  public override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
  {
    _logger.LogInformation("Handle Did Close Document");

    //if (_configuration.TryGetScopedConfiguration(notification.TextDocument.Uri, out var disposable))
    //  disposable.Dispose();

    return Unit.Task;
  }

  public override Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken token) => Unit.Task;

  protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
    TextSynchronizationCapability capability, ClientCapabilities clientCapabilities) =>
    new ()
    {
      DocumentSelector = _textDocumentSelector,
      Change = SyncKind,
      Save = new SaveOptions() { IncludeText = true }
    };

  public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) =>
    new (uri, "bhl");
}
