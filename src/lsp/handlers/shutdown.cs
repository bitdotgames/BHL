using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.General;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace bhl.lsp.handlers;

public class ShutdownHandler : IShutdownHandler
{
  private readonly CancellationTokenSource _shutdownCts;
  private readonly ILogger _logger;

  public ShutdownHandler(ILogger<ShutdownHandler> logger, CancellationTokenSource shutdownCts)
  {
    _logger = logger;
    _shutdownCts = shutdownCts;
  }

  public Task<Unit> Handle(ShutdownParams request, CancellationToken cancellationToken)
  {
    _logger.LogInformation("Handle Shutdown");
    _shutdownCts.Cancel();
    return Unit.Task;
  }
}

public class ExitHandler : IExitHandler
{
  private readonly ILogger<ExitHandler> _logger;

  public ExitHandler(ILogger<ExitHandler> logger)
  {
    _logger = logger;
  }

  public Task<Unit> Handle(ExitParams request, CancellationToken cancellationToken)
  {
    _logger.LogInformation("Exit notification received. Terminating process.");
    // Exit gracefully
    Environment.Exit(0);
    return Unit.Task;
  }
}
