using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace bhl.lsp {

public interface IJsonRpc
{
  string Handle(string json);
}

public class JsonRpc : IJsonRpc
{
  List<JsonRpcService> services = new List<JsonRpcService>();

  public JsonRpc AttachService(JsonRpcService service)
  {
    services.Add(service);
    return this;
  }

  public string Handle(string req_json)
  {
    RequestMessage req = null;
    ResponseMessage resp = null;
    
    try
    {
      req = JsonConvert.DeserializeObject<RequestMessage>(req_json);
    }
    catch(Exception e)
    {
      Logger.WriteLine(e);
      Logger.WriteLine($"{req_json}");

      resp = new ResponseMessage
      {
        error = new ResponseError
        {
          code = (int)ErrorCodes.ParseError,
          message = "Parse error"
        }
      };
    }

    if(req != null)
      Logger.WriteLine($":: bhlsp <-- {req.method}({req.id.Value})");
    //Logger.WriteLine($":: bhlsp <-- {json}");
    
    if(resp == null && req != null)
    {
      if(req.IsMessage())
        resp = HandleMessage(req);
      else
        resp = new ResponseMessage
        {
          error = new ResponseError
          {
            code = (int)ErrorCodes.InvalidRequest,
            message = ""
          }
        };
    }
    
    string resp_json = string.Empty;

    if(resp != null)
    {
      // A processed notification message must not send a response back.
      // They work like events.
      bool isNotification = req != null && req.id.Value == null;
      if(!isNotification)
      {
        var jSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        resp_json = JsonConvert.SerializeObject(resp, Newtonsoft.Json.Formatting.None, jSettings);
        
        if(req != null)
          Logger.WriteLine($":: bhlsp --> {req.method}({req.id.Value})");
        
        //Logger.WriteLine($":: bhlsp --> {JsonConvert.SerializeObject(resp, Newtonsoft.Json.Formatting.Indented, jSettings)}");
      }
    }
    
    return resp_json;
  }
  
  ResponseMessage HandleMessage(RequestMessage request)
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
          id = request.id, error = new ResponseError
          {
            code = (int)ErrorCodes.InvalidRequest,
            message = ""
          }
        };
      }
    }
    catch(Exception e)
    {
      Logger.WriteLine(e);

      response = new ResponseMessage
      {
        id = request.id, error = new ResponseError
        {
          code = (int)ErrorCodes.InternalError,
          message = ""
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
    
    //TODO: build and cache a map of available methods
    foreach(var service in services)
    {
      foreach(var method in service.GetType().GetMethods())
      {
        if(IsAllowedToInvoke(method, name))
        {
          object[] args = null;
          var pms = method.GetParameters();
          if(pms.Length > 0)
          {
            try
            {
              args = new[] { @params.ToObject(pms[0].ParameterType) };
            }
            catch(Exception e)
            {
              Logger.WriteLine(e);

              return RpcResult.Error(new ResponseError
              {
                code = (int)ErrorCodes.InvalidParams,
                message = ""
              });
            }
          }
          
          return (RpcResult)method.Invoke(service, args);
        }
      }
    }
    
    return RpcResult.Error(new ResponseError
    {
      code = (int)ErrorCodes.MethodNotFound,
      message = "Method not found"
    });
  }
  
  private bool IsAllowedToInvoke(MethodInfo m, string name)
  {
    foreach(var attribute in m.GetCustomAttributes(true))
    {
      if(attribute is JsonRpcMethodAttribute jsonRpcMethod && jsonRpcMethod.Method == name)
        return true;
    }

    return false;
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
  
  RpcResult(object result, ResponseError error)
  {
    this.result = result;
    this.error = error;
  }
}

public enum ErrorCodes
{
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
  public SelectorType<Int32, Int64, string> id { get; set; }
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
  public SelectorType<Int32, Int64, string> id { get; set; }
  public object result { get; set; }
  public ResponseError error { get; set; }
}

[AttributeUsage(AttributeTargets.Method)]
public class JsonRpcMethodAttribute : Attribute
{
  private string method;

  public JsonRpcMethodAttribute(string method)
  {
    this.method = method;
  }

  public string Method => method;
}

}
