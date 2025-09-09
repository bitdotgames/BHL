using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using bhl.lsp.proto;

namespace bhl.lsp
{

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
  public string jsonrpc = "2.0";

  public bool IsValid()
  {
    return jsonrpc == "2.0";
  }
}

public class Request : MessageBase
{
  public proto.EitherType<Int32, Int64, string> id;
  public string method;
  public JToken @params;

  //for tests convenience
  public Request(int id, string method, object @params = null)
  {
    this.id = id;
    this.method = method;
    if(@params != null)
      this.@params = JToken.FromObject(@params);
  }
}

public class ResponseError
{
  public int code;
  public string message;
  public object data;
}

public class Response : MessageBase
{
  public proto.EitherType<Int32, Int64, string> id;
  public object result;
  public ResponseError error;

  public Response()
  {
  }

  //for tests convenience
  public Response(int id, object result, ResponseError error = null)
  {
    this.id = id;
    this.result = result;
    this.error = error;
  }
}

public class Notification
{
  public string method { get; set; }
  public object @params { get; set; }
}

public class RpcResult
{
  public object result;
  public ResponseError error;

  static public RpcResult Null = null;

  public static RpcResult Error(ResponseError error)
  {
    return new RpcResult(null, error);
  }

  public static RpcResult Error(ErrorCodes code, string msg = "")
  {
    return new RpcResult(null, new ResponseError() { code = (int)code, message = msg });
  }

  public RpcResult(object result, ResponseError error = null)
  {
    this.result = result;
    this.error = error;
  }
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
