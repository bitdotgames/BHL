using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Microsoft.Extensions.DependencyInjection;

namespace bhl.lsp;

public static class ServerFactory
{
  public static async Task<LanguageServer> CreateAsync(
    Serilog.ILogger logger, Stream input, Stream output,
    Types types, Workspace workspace, CancellationToken ct = default)
  {
    logger.Information("BHL server starting...");

    var shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

    var server = await LanguageServer.From(options => options
        .WithInput(input)
        .WithOutput(output)
        .ConfigureLogging(x => x
          .AddSerilog(logger)
          .AddLanguageProtocolLogging()
          .SetMinimumLevel(LogLevel.Trace)
        )
        .WithServices(x => x.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace)))
        .WithServices(services =>
          services
            .AddSingleton(shutdownCts)
            .AddSingleton(workspace)
        )
        .WithHandler<handlers.TextDocumentHandler>()
        .WithHandler<handlers.SemanticTokensHandler>()
        .WithHandler<handlers.TextDocumentReferencesHandler>()
        .WithHandler<handlers.TextDocumentDefinitionHandler>()
        .WithHandler<handlers.TextDocumentHoverHandler>()
        .WithHandler<handlers.TextDocumentCompletionHandler>()
        .WithHandler<handlers.TextDocumentSignatureHelpHandler>()
        .WithHandler<handlers.TextDocumentRenameHandler>()
        .WithHandler<handlers.ExecuteCommandHandler>()
        .OnInitialize(async (server, request, token) =>
        {
          logger.Debug("OnInitialize");

          server.Shutdown.Subscribe(_ => shutdownCts.Cancel());

          ProjectConf proj = null;

          if(request.WorkspaceFolders != null)
          {
            foreach(var wf in request.WorkspaceFolders)
            {
              proj = ProjectConf.TryReadFromDir(wf.Uri.PathNormalized());
              if(proj != null)
                break;
            }
          }
          else if(!string.IsNullOrEmpty(request.RootPath)) // @deprecated in favour of `rootUri`.
          {
            proj = ProjectConf.TryReadFromDir(request.RootPath);
            if(proj == null)
            {
              proj = new ProjectConf();
              proj.SetupForRootPath(request.RootPath);
            }
          }
          else if(request.RootUri != null) // @deprecated in favour of `workspaceFolders`
          {
            proj = ProjectConf.TryReadFromDir(request.RootUri.PathNormalized());
            if(proj == null)
            {
              proj = new ProjectConf();
              proj.SetupForRootPath(request.RootUri.PathNormalized());
            }
          }
          else
            logger.Error("No root path specified");

          proj ??= new ProjectConf();

          proj.LoadBindings().Register(types);

          workspace.Init(types, proj, logger);
        })
        .OnInitialized(async (server, request, response, token) =>
        {
          logger.Debug("OnInitialized");

          server.SendNotification("window/showMessage", new ShowMessageParams
          {
            Type = MessageType.Log,
            Message = "BHL: Indexing...",
          });

          await workspace.IndexFilesAsync(token);

          server.SendNotification("window/showMessage", new ShowMessageParams
          {
            Type = MessageType.Log,
            Message = $"BHL: {workspace.IndexedFileCount} file(s) indexed",
          });

          var diagnostics = workspace.GetDiagnosticsToPublish();
          _ = Task.Run(() => { server.PublishDiagnostics(diagnostics); }, token);
        })
        .OnStarted((server, token) =>
        {
          logger.Debug("OnStarted");
          return Task.CompletedTask;
        })
      ,
      shutdownCts.Token
    );
    logger.Information("BHL server initialized...");
    return server;
  }
}
