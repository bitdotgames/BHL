using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using bhlsp;

public class TestLSP : BHL_TestBase
{
  private const string version = "0.0.1";
  
  [IsTested()]
  public void TestRpcResponseErrors()
  {
    string json = "";
    var rpc = new BHLSPJsonRpc();
    
    //ParseError
    json = "{\"jsonrpc\": \"2.0\", \"method\": \"initialize";
    AssertEqual(
      rpc.HandleMessage(json),
      "{\"id\":null,\"error\":{\"code\":-32700,\"message\":\"\"},\"jsonrpc\":\"2.0\"}"
    );
    
    json = "blah";
    AssertEqual(
      rpc.HandleMessage(json),
      "{\"id\":null,\"error\":{\"code\":-32700,\"message\":\"\"},\"jsonrpc\":\"2.0\"}"
    );
    
    json = "{\"jsonrpc\": \"2.0\", \"method\": {}, \"id\": 1}";
    AssertEqual(
      rpc.HandleMessage(json),
      "{\"id\":null,\"error\":{\"code\":-32700,\"message\":\"\"},\"jsonrpc\":\"2.0\"}"
    );
    
    //MethodNotFound
    json = "{\"jsonrpc\": \"2.0\", \"method\": \"blah\", \"id\": 1}";
    AssertEqual(
      rpc.HandleMessage(json),
      "{\"id\":1,\"error\":{\"code\":-32601,\"message\":\"\"},\"jsonrpc\":\"2.0\"}"
    );
    
    //InvalidRequest
    json = "{\"jsonrpc\": \"2.0\", \"id\": 1}";
    AssertEqual(
      rpc.HandleMessage(json),
      "{\"id\":1,\"error\":{\"code\":-32600,\"message\":\"\"},\"jsonrpc\":\"2.0\"}"
    );
    
    json = "{\"jsonrpc\": \"2.0\", \"method\": 1, \"params\": \"bar\"}";
    AssertEqual(
      rpc.HandleMessage(json),
      "{\"id\":null,\"error\":{\"code\":-32600,\"message\":\"\"},\"jsonrpc\":\"2.0\"}"
    );
    
    //InvalidParams
    rpc.AttachRpcService(new BHLSPGeneralJsonRpcService());
    json = "{\"jsonrpc\": \"2.0\", \"method\": \"initialize\", \"params\": \"bar\"}";
    AssertEqual(
      rpc.HandleMessage(json),
      "{\"id\":null,\"error\":{\"code\":-32602,\"message\":\"\"},\"jsonrpc\":\"2.0\"}"
    );
    
    json = "{\"jsonrpc\": \"2.0\", \"method\": \"initialize\", \"params\": \"bar\", \"id\": 1}";
    AssertEqual(
      rpc.HandleMessage(json),
      "{\"id\":1,\"error\":{\"code\":-32602,\"message\":\"\"},\"jsonrpc\":\"2.0\"}"
    );
  }
}