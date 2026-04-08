using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace bhl.lsp.handlers;

public class ExecuteCommandHandler : ExecuteCommandHandlerBase
{
  public const string Reload = "bhl.reload";

  private readonly ILogger _logger;
  private readonly Workspace _workspace;
  private readonly ILanguageServerFacade _server;

  public ExecuteCommandHandler(
    ILogger<ExecuteCommandHandler> logger,
    Workspace workspace,
    ILanguageServerFacade server)
  {
    _logger = logger;
    _workspace = workspace;
    _server = server;
  }

  protected override ExecuteCommandRegistrationOptions CreateRegistrationOptions(
    ExecuteCommandCapability capability, ClientCapabilities clientCapabilities)
  {
    return new ExecuteCommandRegistrationOptions
    {
      Commands = new Container<string>(Reload),
    };
  }

  public override async Task<Unit> Handle(ExecuteCommandParams request, CancellationToken ct)
  {
    if(request.Command == Reload)
    {
      _logger.LogInformation("bhl.reload: reloading bindings and recompiling workspace");
      await _workspace.ReloadAsync(ct);
      var diagnostics = _workspace.GetDiagnosticsToPublish();
      _ = Task.Run(() => _server.PublishDiagnostics(diagnostics), ct);
    }
    else
      _logger.LogWarning("bhl.reload: unknown command {Command}", request.Command);

    return Unit.Value;
  }
}
