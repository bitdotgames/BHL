using System;
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

namespace bhl.lsp
{

internal class TextDocumentHandler : TextDocumentSyncHandlerBase
{
  private readonly ILogger _logger;
  private readonly ILanguageServerConfiguration _configuration;
  private readonly Workspace _workspace;

  private readonly TextDocumentSelector _textDocumentSelector = new TextDocumentSelector(
    new TextDocumentFilter
    {
      Pattern = "**/*.bhl"
    }
  );

  public TextDocumentHandler(ILogger<TextDocumentHandler> logger, Workspace workspace,
    ILanguageServerConfiguration configuration)
  {
    _logger = logger;
    _configuration = configuration;
    _workspace = workspace;
  }

  public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

  public override Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
  {
    _logger.LogInformation("Handle Change Document");
    return Unit.Task;
  }

  public override async Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
  {
    //NOTE: a special case when a file is opened without any valid project file
    if (string.IsNullOrEmpty(_workspace.conf.proj_file))
    {
      _workspace.conf.src_dirs.Add(Path.GetDirectoryName(notification.TextDocument.Uri.Path));
      _workspace.conf.inc_dirs.Add(Path.GetDirectoryName(notification.TextDocument.Uri.Path));
      _workspace.conf.Setup();
      _workspace.IndexFiles();
    }

    await Task.Yield();
    _logger.LogInformation("Handle Open Document");
    await _configuration.GetScopedConfiguration(notification.TextDocument.Uri, token).ConfigureAwait(false);

    return Unit.Value;
  }

  public override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
  {
    _logger.LogInformation("Handle Did Close Document");

    if (_configuration.TryGetScopedConfiguration(notification.TextDocument.Uri, out var disposable))
    {
      disposable.Dispose();
    }

    return Unit.Task;
  }

  public override Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken token) => Unit.Task;

  protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
    TextSynchronizationCapability capability, ClientCapabilities clientCapabilities) =>
    new TextDocumentSyncRegistrationOptions()
    {
      DocumentSelector = _textDocumentSelector,
      Change = Change,
      Save = new SaveOptions() { IncludeText = true }
    };

  public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) =>
    new TextDocumentAttributes(uri, "csharp");
}

}
