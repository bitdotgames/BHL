using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace bhl.lsp {

public interface IService 
{
  //TODO:
  //public void Tick();
}

public interface IPublisher
{
  void Publish(Notification notification);
}

public interface IRpcHandler
{
  ResponseMessage HandleRequest(RequestMessage request);
}

public class RpcServer : IRpcHandler, IPublisher
{
  IConnection connection;
  Logger logger;

  public struct ServiceMethod
  {
    public IService service;
    public MethodInfo method;
    public System.Type arg_type;
  }

  List<IService> services = new List<IService>();
  Dictionary<string, ServiceMethod> name2method = new Dictionary<string, ServiceMethod>();

  public RpcServer(Logger logger, IConnection connection)
  {
    this.logger = logger;
    this.connection = connection;
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

  public void Start()
  {
    logger.Log(1, "Starting BHL LSP server...");

    while(true)
    {
      try
      {
        bool success = ReadAndHandle();
        if(!success)
          break;
      }
      catch(Exception e)
      {
        logger.Log(0, e.ToString());
      }
    }

    logger.Log(1, "Stopping BHL LSP server...");
  }

  bool ReadAndHandle()
  {
    try
    {
      string json = connection.Read();
      if(string.IsNullOrEmpty(json))
        return false;
    
      string response = Handle(json);

      if(!string.IsNullOrEmpty(response))
        connection.Write(response);
      //let's exit the loop
      else if(response == null)
        return false;
    }
    catch(Exception e)
    {
      logger.Log(0, e.ToString());
      return false;
    }
    
    return true;
  }

  //NOTE: returns 'null' if the server must break its reading loop and exit
  public string Handle(string req_json)
  {
    var sw = Stopwatch.StartNew();
    
    RequestMessage req = null;
    ResponseMessage rsp = null;
    
    try
    {
      req = req_json.FromJson<RequestMessage>();
    }
    catch(Exception e)
    {
      logger.Log(0, e.ToString());
      logger.Log(0, $"{req_json}");

      rsp = new ResponseMessage
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
      if(req.IsMessage())
        rsp = HandleRequest(req);
      else
        rsp = new ResponseMessage
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
      if((rsp.error?.code??0) == (int)ErrorCodes.Exit)
        resp_json = null;
      //NOTE: we send responses only if there were an error or if the request 
      //      contains an id
      else if(req == null || req.id.Value != null)
        resp_json = rsp.ToJson();
    }

    sw.Stop();
    logger.Log(1, $"< ({req?.method}, id: {req?.id.Value}) done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec) {(resp_json?.Length > 500 ? resp_json?.Substring(0,500) + ".." : resp_json)}");
    
    return resp_json;
  }
  
  public ResponseMessage HandleRequest(RequestMessage request)
  {
    ResponseMessage response;
    
    try
    {
      if(!string.IsNullOrEmpty(request.method))
      {
        RpcResult result = CallRpcMethod(request.method, request.@params);
        
        response = new ResponseMessage
        {
          id = request.id,
          result = result.result,
          error = result.error
        };
      }
      else
      {
        response = new ResponseMessage
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

      response = new ResponseMessage
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

  RpcResult CallRpcMethod(string name, JToken @params)
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
        message = "Method not found"
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
    
    return (RpcResult)sm.method.Invoke(sm.service, args);
  }

  public void Publish(Notification notification)
  {
    connection.Write(notification.ToJson());
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
}

public class RpcResult
{
  public object result;
  public ResponseError error;
  
  public static RpcResult Success(object result = null)
  {
    return new RpcResult(result ?? "null", null);
  }

  public static RpcResult Error(ResponseError error)
  {
    return new RpcResult(null, error);
  }

  public static RpcResult Exit()
  {
    return new RpcResult(null, new ResponseError() { code = (int)ErrorCodes.Exit } );
  }
  
  RpcResult(object result, ResponseError error)
  {
    this.result = result;
    this.error = error;
  }
}

public enum ErrorCodes
{
  Exit = -1, //special error code which makes the server to exit

  ParseError = -32700,
  InvalidRequest = -32600,
  MethodNotFound = -32601,
  InvalidParams = -32602,
  InternalError = -32603,
  ServerErrorStart = -32099,
  ServerErrorEnd = -32000,
  ServerNotInitialized = -32002,
  UnknownErrorCode = -32001,
  RequestCancelled = -32800,
  RequestFailed = -32803 // @since 3.17.0
}

public abstract class MessageBase
{
  public string jsonrpc { get; set; } = "2.0";

  public bool IsMessage()
  {
    return jsonrpc == "2.0";
  }
}

public class RequestMessage : MessageBase
{
  public proto.EitherType<Int32, Int64, string> id { get; set; }
  public string method { get; set; }
  public JToken @params { get; set; }
}

public class ResponseError
{
  public int code { get; set; }
  public string message { get; set; }
  public object data { get; set; }
}

public class ResponseMessage : MessageBase
{
  public proto.EitherType<Int32, Int64, string> id { get; set; }
  public object result { get; set; }
  public ResponseError error { get; set; }
}

public class Notification 
{
  public string method { get; set; }
  public object @params { get; set; }
}

[AttributeUsage(AttributeTargets.Method)]
public class RpcMethod : Attribute
{
  private string method;

  public RpcMethod(string method)
  {
    this.method = method;
  }

  public string Method => method;
}

}
