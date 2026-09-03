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

      if(!TryGetReloadTarget(_workspace.ProjConf, path, out var reload_from))
        continue;

      ProjectConf proj;
      try
      {
        proj = ProjectConf.ReadFromFile(reload_from);
      }
      catch(System.Exception e)
      {
        _logger.LogWarning(e, "bhl.proj changed but could not be re-read from {File}", reload_from);
        continue;
      }

      _logger.LogInformation("bhl.proj changed ({Path}), reloading workspace", path);

      try
      {
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
      }
      catch(System.Exception e)
      {
        _logger.LogError(e, "reload on bhl.proj change failed");
        _server.SendNotification("window/showMessage", new ShowMessageParams
        {
          Type = MessageType.Error,
          Message = $"BHL: reload failed: {e.Message}",
        });
      }
      break;
    }

    return Unit.Value;
  }

  //NOTE: the client's glob watches EVERY bhl.proj under the workspace, so with 'includes'
  //      it's normal to have several - only reload (always from the ROOT's own proj_file,
  //      never the changed file's dir) if the change is relevant to the tracked root
  public static bool TryGetReloadTarget(ProjectConf current, string changed_path, out string reload_from)
  {
    reload_from = null;

    var root_proj_file = current.proj_file;
    if(string.IsNullOrEmpty(root_proj_file) || !File.Exists(root_proj_file))
      return false;

    var norm_path = BuildUtils.NormalizeFilePath(changed_path);
    bool is_relevant = norm_path == BuildUtils.NormalizeFilePath(root_proj_file)
      || current.included_files.Contains(norm_path);
    if(!is_relevant)
      return false;

    reload_from = root_proj_file;
    return true;
  }
}
