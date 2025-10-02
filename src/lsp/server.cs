using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;

namespace bhl.lsp;

public static class ServerFactory
{
  public static async Task<LanguageServer> CreateAsync(
    Serilog.ILogger logger, Stream input, Stream output,
    Types types, Workspace workspace, CancellationToken ct)
  {
    logger.Debug("BHL server starting...");
    //IObserver<WorkDoneProgressReport> workDone = null;

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
          services.AddSingleton(workspace)
        )
        .WithHandler<handlers.TextDocumentHandler>()
        .WithHandler<handlers.SemanticTokensHandler>()
        .WithHandler<handlers.TextDocumentReferencesHandler>()
        .WithHandler<handlers.TextDocumentDefinitionHandler>()
        .OnStarted((server, token) =>
          {
            logger.Debug("Server started");
            return Task.CompletedTask;
          })
        .OnInitialize(async (server, request, token) =>
        {
          ProjectConf proj = null;

          if(request.WorkspaceFolders != null)
          {
            foreach(var wf in request.WorkspaceFolders)
            {
              proj = ProjectConf.TryReadFromDir(wf.Uri.Path);
              if(proj != null)
                break;
            }
          }
          else if(request.RootUri != null) // @deprecated in favour of `workspaceFolders`
          {
            proj = ProjectConf.TryReadFromDir(request.RootUri.Path);
            if(proj == null)
            {
              proj = new ProjectConf();
              proj.SetupForRootPath(request.RootUri.Path);
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
          else
            logger.Error("No root path specified");

          proj ??= new ProjectConf();

          proj.LoadBindings().Register(types);

          workspace.Init(types, proj);

          //TODO: run it in async manner with progress
          await workspace.IndexFilesAsync();

          //var manager = server.WorkDoneManager.For(
          //  request, new WorkDoneProgressBegin
          //  {
          //    Title = "Server is starting...",
          //    Percentage = 10,
          //  }
          //);
          //work_done = manager;
          //work_done.OnNext(new WorkDoneProgressReport() { Percentage = 20, Message = "Loading in progress"});
        })
        .OnInitialized((server, request, response, token) =>
        {
          var diagnostics = workspace.GetCompileErrors().GetDiagnostics();
          _ = Task.Run(() => { server.PublishDiagnostics(diagnostics); });

          return Task.CompletedTask;
        })
      ,
      ct
    );
    return server;
  }
}
