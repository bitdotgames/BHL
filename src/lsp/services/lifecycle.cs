using System.Threading.Tasks;
using bhl.lsp.proto;

namespace bhl.lsp
{

public class LifecycleService : IService
{
  Server srv;
  Workspace workspace;

  public int? process_id { get; private set; }

  public LifecycleService(Server srv)
  {
    this.srv = srv;
    this.workspace = srv.workspace;
  }

  public void GetCapabilities(ClientCapabilities cc, ref ServerCapabilities sc)
  {
  }

  [RpcMethod("initialize")]
  public Task<RpcResult> Initialize(InitializeParams args)
  {
    process_id = args.processId;

    var ts = new Types();
    ProjectConf proj = null;

    if(args.workspaceFolders != null)
    {
      for(int i = 0; i < args.workspaceFolders.Length; i++)
      {
        proj = ProjectConf.TryReadFromDir(args.workspaceFolders[i].uri.path);
        if(proj != null)
          break;
      }
    }
    else if(args.rootUri != null) // @deprecated in favour of `workspaceFolders`
    {
      proj = ProjectConf.TryReadFromDir(args.rootUri.path);
    }
    else if(!string.IsNullOrEmpty(args.rootPath)) // @deprecated in favour of `rootUri`.
    {
      proj = ProjectConf.TryReadFromDir(args.rootPath);
    }

    if(proj == null)
      proj = new ProjectConf();

    srv.logger.Log(1, "Initializing workspace from project file '" + proj.proj_file + "'");

    proj.LoadBindings().Register(ts);

    workspace.Init(ts, proj);

    //TODO: run it in background?
    workspace.IndexFiles();

    var server_capabilities = srv.capabilities;
    foreach(var service in srv.services)
      service.GetCapabilities(args.capabilities, ref server_capabilities);

    return Task.FromResult(new RpcResult(new InitializeResult
    {
      capabilities = server_capabilities,
      serverInfo = new InitializeResult.InitializeResultsServerInfo
      {
        name = "bhlsp",
        version = Version.Name
      }
    }));
  }

  [RpcMethod("initialized")]
  public Task<RpcResult> Initialized()
  {
    return Task.FromResult(RpcResult.Null);
  }

  [RpcMethod("shutdown")]
  public Task<RpcResult> Shutdown()
  {
    workspace.Shutdown();

    return Task.FromResult(new RpcResult(null));
  }

  [RpcMethod("$/cancelRequest")]
  public Task<RpcResult> CancelRequest(CancelParams args)
  {
    //TODO: implement it
    return Task.FromResult(RpcResult.Error(ErrorCodes.RequestCancelled));
  }

  [RpcMethod("exit")]
  public Task<RpcResult> Exit()
  {
    return Task.FromResult(RpcResult.Error(ErrorCodes.Exit));
  }
}

}