using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Microsoft.Extensions.DependencyInjection;

namespace bhl.lsp;

public static class ServerCreator
{
  public static async Task<LanguageServer> CreateAsync(Serilog.ILogger logger, Stream input, Stream output,
    Workspace workspace, CancellationToken ct)
  {
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
          services
            .AddSingleton(workspace)
        )
        .WithHandler<handlers.TextDocumentHandler>()
        .WithHandler<handlers.SemanticTokensHandler>()
        .OnInitialize((server, request, token) =>
        {
          var ts = new Types();
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
          }
          else if(!string.IsNullOrEmpty(request.RootPath)) // @deprecated in favour of `rootUri`.
          {
            proj = ProjectConf.TryReadFromDir(request.RootPath);
          }
          else
            logger.Error("No root path specified");

          proj ??= new ProjectConf();

          proj.LoadBindings().Register(ts);

          workspace.Init(ts, proj);

          //TODO: run it in async manner with progress
          workspace.IndexFiles();

          //var manager = server.WorkDoneManager.For(
          //  request, new WorkDoneProgressBegin
          //  {
          //    Title = "Server is starting...",
          //    Percentage = 10,
          //  }
          //);
          //work_done = manager;
          //work_done.OnNext(new WorkDoneProgressReport() { Percentage = 20, Message = "Loading in progress"});
          return Task.CompletedTask;
        })
        .OnInitialized((server, request, response, token) => Task.CompletedTask)
      ,
      ct
    );
    return server;
  }
}
