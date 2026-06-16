using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace bhl.lsp.handlers;

public class DidChangeWatchedFilesHandler : DidChangeWatchedFilesHandlerBase
{
  readonly ILogger _logger;
  readonly Workspace _workspace;
  readonly ILanguageServerFacade _server;

  public DidChangeWatchedFilesHandler(
    ILogger<DidChangeWatchedFilesHandler> logger,
    Workspace workspace,
    ILanguageServerFacade server)
  {
    _logger = logger;
    _workspace = workspace;
    _server = server;
  }

  protected override DidChangeWatchedFilesRegistrationOptions CreateRegistrationOptions(
    DidChangeWatchedFilesCapability capability, ClientCapabilities clientCapabilities)
  {
    return new DidChangeWatchedFilesRegistrationOptions
    {
      Watchers = new Container<OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher>(
        new OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher
        {
          GlobPattern = "**/bhl.proj",
          Kind = WatchKind.Change | WatchKind.Create,
        }
      ),
    };
  }

  public override async Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken ct)
  {
    foreach(var change in request.Changes)
    {
      var path = change.Uri.PathNormalized();
      if(!path.EndsWith("bhl.proj", System.StringComparison.OrdinalIgnoreCase))
        continue;
      if(change.Type == FileChangeType.Deleted)
        continue;

      var dir = Path.GetDirectoryName(path);
      var proj = ProjectConf.TryReadFromDir(dir);
      if(proj == null)
      {
        _logger.LogWarning("bhl.proj changed but could not be read from {Dir}", dir);
        continue;
      }

      _logger.LogInformation("bhl.proj changed, reloading workspace");

      _server.SendNotification("window/showMessage", new ShowMessageParams
      {
        Type = MessageType.Log,
        Message = "BHL: bhl.proj changed, reloading...",
      });

      var sw = System.Diagnostics.Stopwatch.StartNew();
      await _workspace.ReloadAsync(proj, ct);
      sw.Stop();

      _server.SendNotification("window/showMessage", new ShowMessageParams
      {
        Type = MessageType.Log,
        Message = $"BHL: {_workspace.IndexedFileCount} file(s) reloaded in {sw.ElapsedMilliseconds}ms",
      });

      var diagnostics = _workspace.GetDiagnosticsToPublish();
      _ = Task.Run(() => _server.PublishDiagnostics(diagnostics), ct);
      break;
    }

    return Unit.Value;
  }
}
