using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using bhl.lsp.proto;
using Microsoft.Extensions.DependencyInjection;
using ILogger = Serilog.ILogger;

namespace bhl.lsp {

public class Server
{
  public IConnection connection { get; private set; }
  public Logger logger { get; private set; }
  public Workspace workspace { get; private set; }
  public ServerCapabilities capabilities { get; private set; } = new ServerCapabilities();

  public struct ServiceMethod
  {
    public IService service;
    public MethodInfo method;
    public System.Type arg_type;
  }

  public List<IService> services { get; private set; } = new List<IService>();
  Dictionary<string, ServiceMethod> name2method = new Dictionary<string, ServiceMethod>();

  public bool going_to_exit { get; private set; } = false;

  public Server(Logger logger, IConnection connection, Workspace workspace)
  {
    this.logger = logger;
    this.connection = connection;
    this.workspace = workspace;
  }

  public void AttachAllServices()
  {
    AttachService(new LifecycleService(this));
    AttachService(new DiagnosticService(this));
    AttachService(new TextDocumentSynchronizationService(this));
    AttachService(new TextDocumentSignatureHelpService(this));
    AttachService(new TextDocumentGoToService(this));
    AttachService(new TextDocumentCompletionService(this));
    AttachService(new TextDocumentFindReferencesService(this));
    AttachService(new TextDocumentHoverService(this));
    AttachService(new TextDocumentSemanticTokensService(this));
  }

  public void AttachService(IService service)
  {
    foreach(var method in service.GetType().GetMethods())
    {
      string name = FindRpcMethodName(method);
      if(name != null)
      {
        var sm = new ServiceMethod();
        sm.service = service;
        sm.method = method;

        var pms = method.GetParameters();
        if(pms.Length == 1)
          sm.arg_type = pms[0].ParameterType; 
        else if(pms.Length == 0)
          sm.arg_type = null;
        else
          throw new Exception("Too many method arguments count");

        name2method.Add(name, sm);
      }
    }
    services.Add(service);
  }

  public async Task Start(CancellationToken ct)
  {
    logger.Log(1, "Starting BHL (" + bhl.Version.Name + ") LSP server...");

    while(true)
    {
      try
      {
        bool success = await ReadAndHandle(ct);
        if(!success)
          break;
      }
      catch(Exception e)
      {
        logger.Log(0, e.ToString());
        break;
      }
    }

    logger.Log(1, "Stopping BHL (" + bhl.Version.Name + ") LSP server...");
  }

  async Task<bool> ReadAndHandle(CancellationToken ct)
  {
    try
    {
      string json = await connection.Read(ct);
      if(string.IsNullOrEmpty(json))
        return false;
    
      string response = await Handle(json);

      if(!string.IsNullOrEmpty(response))
        await connection.Write(response, ct);
      else if(going_to_exit)
        return false;
    }
    catch(Exception e)
    {
      logger.Log(0, e.ToString());
      return false;
    }
    
    return true;
  }

  public async Task<string> Handle(string req_json)
  {
    var sw = Stopwatch.StartNew();
    
    Request req = null;
    Response rsp = null;
    
    try
    {
      req = req_json.FromJson<Request>();
    }
    catch(Exception e)
    {
      logger.Log(0, e.ToString());
      logger.Log(0, req_json);

      rsp = new Response
      {
        error = new ResponseError
        {
          code = (int)ErrorCodes.ParseError,
          message = "Parse error"
        }
      };
    }

    string resp_json = string.Empty;

    logger.Log(1, $"> ({req?.method}, id: {req?.id.Value}) {(req_json.Length > 500 ? req_json.Substring(0,500) + ".." : req_json)}");
    
    //NOTE: if there's no response error by this time 
    //      let's handle the request
    if(req != null && rsp == null)
    {
      if(req.IsValid())
        rsp = await HandleRequest(req);
      else
        rsp = new Response
        {
          error = new ResponseError
          {
            code = (int)ErrorCodes.InvalidRequest,
            message = ""
          }
        };
    }

    if(rsp != null)
    {
      //NOTE: handling special type of response: server exit 
      if((rsp.error?.code??0) == (int)ErrorCodes.Exit)
        going_to_exit = true;
      else if(rsp.error != null || rsp.result != null)
        resp_json = rsp.ToJson();
      //NOTE: special 'null-result' case: we need the null result to be sent to the client,
      //      however null values are omitted when serialized to JSON, hence the hack
      else if(rsp.result == null)
        resp_json = rsp.ToJson().TrimEnd('}') + ",\"result\":null}";
    }

    sw.Stop();
    logger.Log(1, $"< ({req?.method}, id: {req?.id.Value}) done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec) {(resp_json?.Length > 500 ? resp_json?.Substring(0,500) + ".." : resp_json)}");
    
    return resp_json;
  }
  
  public async Task<Response> HandleRequest(Request request)
  {
    Response response;
    
    try
    {
      if(!string.IsNullOrEmpty(request.method))
      {
        RpcResult result = await CallRpcMethod(request.method, request.@params);
        if(result != null)
        {
          response = new Response
          {
            id = request.id,
            result = result.result,
            error = result.error
          };
        }
        else
          response = null;
      }
      else
      {
        response = new Response
        {
          id = request.id, 
          error = new ResponseError
          {
            code = (int)ErrorCodes.InvalidRequest,
            message = ""
          }
        };
      }
    }
    catch(Exception e)
    {
      logger.Log(0, e.ToString());

      response = new Response
      {
        id = request.id, 
        error = new ResponseError
        {
          code = (int)ErrorCodes.InternalError,
          message = e.ToString()
        }
      };
    }
    
    return response;
  }

  async Task<RpcResult> CallRpcMethod(string name, JToken @params)
  {
    if(double.TryParse(name, out _))
    {
      return RpcResult.Error(new ResponseError
      {
        code = (int)ErrorCodes.InvalidRequest,
        message = ""
      });
    }

    if(!name2method.TryGetValue(name, out var sm)) 
    {
      return RpcResult.Error(new ResponseError
      {
        code = (int)ErrorCodes.MethodNotFound,
        message = "Method not found: " + name
      });
    }
    
    object[] args = null;
    if(sm.arg_type != null)
    {
      try
      {
        args = new[] { @params.ToObject(sm.arg_type) };
      }
      catch(Exception e)
      {
        logger.Log(0, e.ToString());

        return RpcResult.Error(new ResponseError
        {
          code = (int)ErrorCodes.InvalidParams,
          message = e.Message
        });
      }
    }
    
    return await (Task<RpcResult>)sm.method.Invoke(sm.service, args);
  }

  public async Task Publish(Notification notification, CancellationToken ct)
  {
    string json = notification.ToJson();

    logger.Log(1, $"<< ({notification.method} {(json?.Length > 500 ? json?.Substring(0,500) + ".." : json)}");

    await connection.Write(notification.ToJson(), ct);
  }
  
  static string FindRpcMethodName(MethodInfo m)
  {
    foreach(var attribute in m.GetCustomAttributes(true))
    {
      if(attribute is RpcMethod rm)
        return rm.Method;
    }

    return null;
  }

  public static async Task<LanguageServer> CreateAsync(ILogger logger, Workspace workspace, CancellationToken ct)
  {
    Console.OutputEncoding = new UTF8Encoding();

    IObserver<WorkDoneProgressReport> work_done = null;
    
    var server = await LanguageServer.From(options => options
      .WithInput(Console.OpenStandardInput())
      .WithOutput(Console.OpenStandardOutput())
      .ConfigureLogging(
        x => x
          .AddSerilog(logger)
          .AddLanguageProtocolLogging()
          .SetMinimumLevel(LogLevel.Trace)
      )
      .WithServices(services => 
        services
          .AddSingleton(workspace)
          .AddSingleton(logger)
        )
      .WithServices(x => x.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace)))
      .WithHandler<SemanticTokensHandler>()
      .OnInitialize(async (server, request, token) =>
      {
        var ts = new Types();
        ProjectConf proj = null;
        
        if(request.WorkspaceFolders != null)
        {
          logger.Debug("WorkspaceFolders");
          foreach(var wf in request.WorkspaceFolders)
          {
            proj = ProjectConf.TryReadFromDir(wf.Uri.Path);
            if(proj != null)
              break;
          }
        }
        else if(request.RootUri != null) // @deprecated in favour of `workspaceFolders`
        {
          logger.Debug("RootUri");
          proj = ProjectConf.TryReadFromDir(request.RootUri.Path);
        }
        else if(!string.IsNullOrEmpty(request.RootPath)) // @deprecated in favour of `rootUri`.
        {
          logger.Debug("RootPath");
          proj = ProjectConf.TryReadFromDir(request.RootPath);
        }                                                                         
        else
          logger.Error("No root path specified");

        if(proj == null)
          proj = new ProjectConf();
        
        proj.LoadBindings().Register(ts);
    
        workspace.Init(ts, proj);

        //TODO: run it in background?
        workspace.IndexFiles();
        
        var manager = server.WorkDoneManager.For(
          request, new WorkDoneProgressBegin
          {
            Title = "Server is starting...",
            Percentage = 10,
          }
        );
        work_done = manager;
        
        await Task.Delay(500).ConfigureAwait(false);
        
        manager.OnNext(new WorkDoneProgressReport() { Percentage = 20, Message = "Loading in progress"});
      })
      .OnInitialized(
        async (server, request, response, token) =>
        {
          work_done.OnNext(
            new WorkDoneProgressReport
            {
              Percentage = 40,
              Message = "loading almost done",
            }
          );

          await Task.Delay(500).ConfigureAwait(false);

          work_done.OnNext(
            new WorkDoneProgressReport
            {
              Message = "loading done",
              Percentage = 100,
            }
          );
          work_done.OnCompleted();
        }
      )
    , 
    ct
    );
    return server;
  }
}

}
