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

  public override async Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
  {
    _logger.LogInformation("Handle Change Document");

    await _workspace.SetupProjectIfEmptyAsync(notification.TextDocument.Uri.Path);

    foreach(var change in notification.ContentChanges)
    {
      if(!_workspace.UpdateDocument(
           notification.TextDocument.Uri,
           change.Text
         ))
      {
        //TODO: send some diagnostics about missing document?
      }
    }

    _server.PublishDiagnostics(_workspace.GetCompileErrors().GetDiagnostics());

    return Unit.Value;
  }

  public override async Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
  {
    _logger.LogInformation("Handle Open Document");

    await _workspace.SetupProjectIfEmptyAsync(notification.TextDocument.Uri.Path);

    _workspace.OpenDocument(
      notification.TextDocument.Uri,
      notification.TextDocument.Text
      );

    _server.PublishDiagnostics(_workspace.GetCompileErrors().GetDiagnostics());

    return Unit.Value;
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
