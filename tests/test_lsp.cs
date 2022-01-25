using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using bhlsp;

public class TestLSP : BHL_TestBase
{
  [IsTested()]
  public void TestRpcResponseErrors()
  {
    string json = "";
    var rpc = new BHLSPJsonRpc();
    
    BHLSPWorkspace.self.Shutdown();
    
    //ParseError
    json = "{\"jsonrpc\": \"2.0\", \"method\": \"initialize";
    AssertEqual(
      rpc.HandleMessage(json),
      "{\"id\":null,\"error\":{\"code\":-32700,\"message\":\"Parse error\"},\"jsonrpc\":\"2.0\"}"
    );
    
    //InvalidRequest
    json = "";
    AssertEqual(
      rpc.HandleMessage(json),
      "{\"id\":null,\"error\":{\"code\":-32600,\"message\":\"\"},\"jsonrpc\":\"2.0\"}"
    );
    
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
    
    //MethodNotFound
    json = "{\"jsonrpc\": \"2.0\", \"method\": \"foo\", \"id\": 1}";
    AssertEqual(
      rpc.HandleMessage(json),
      "{\"id\":1,\"error\":{\"code\":-32601,\"message\":\"Method not found\"},\"jsonrpc\":\"2.0\"}"
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

  [IsTested()]
  public void TestGeneralRpc()
  {
    string json = "";
    string rsp = "";
    
    var rpc = new BHLSPJsonRpc();
    rpc.AttachRpcService(new BHLSPGeneralJsonRpcService());
    
    BHLSPWorkspace.self.Shutdown();
    
    json = "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"initialize\", \"params\": {\"capabilities\":{}}}";
    
    rsp = "{\"id\":1,\"result\":{" +
            "\"capabilities\":{" +
              "\"textDocumentSync\":null," +
              "\"hoverProvider\":null," +
              "\"declarationProvider\":null," +
              "\"definitionProvider\":null," +
              "\"typeDefinitionProvider\":null," +
              "\"implementationProvider\":null," +
              "\"referencesProvider\":null," +
              "\"documentHighlightProvider\":null," +
              "\"documentSymbolProvider\":null," +
              "\"codeActionProvider\":null," +
              "\"colorProvider\":null," +
              "\"documentFormattingProvider\":null," +
              "\"documentRangeFormattingProvider\":null," +
              "\"renameProvider\":null," +
              "\"foldingRangeProvider\":null," +
              "\"selectionRangeProvider\":null," +
              "\"linkedEditingRangeProvider\":null," +
              "\"callHierarchyProvider\":null," +
              "\"semanticTokensProvider\":null," +
              "\"monikerProvider\":null," +
              "\"workspaceSymbolProvider\":null}," +
            "\"serverInfo\":{\"name\":\"bhlsp\",\"version\":\"0.1\"}}," +
            "\"jsonrpc\":\"2.0\"}";

    AssertEqual(rpc.HandleMessage(json), rsp);
    
    json = "{\"params\":{},\"method\":\"initialized\",\"jsonrpc\":\"2.0\"}";
    AssertEqual(
      rpc.HandleMessage(json),
      string.Empty
    );
    
    json = "{\"id\":1,\"params\":null,\"method\":\"shutdown\",\"jsonrpc\":\"2.0\"}";
    AssertEqual(
      rpc.HandleMessage(json),
      "{\"id\":1,\"result\":\"null\",\"jsonrpc\":\"2.0\"}"
    );
    
    json = "{\"params\":null,\"method\":\"exit\",\"jsonrpc\":\"2.0\"}";
    AssertEqual(
      rpc.HandleMessage(json),
      string.Empty
    );
  }

  [IsTested()]
  public void TestSyncDocument()
  {
    string bhl1 = @"
    func float test1(float k) 
    {
      return 0
    }

    func test2() 
    {
      test1()
    }
    ";
    
    string bhl2 = @"
    func float test1(float k, int j) 
    {
      return 0
    }

    func test2() 
    {
      test1()
    }
    ";
    
    var rpc = new BHLSPJsonRpc();
    rpc.AttachRpcService(new BHLSPTextDocumentSynchronizationJsonRpcService());

    BHLSPWorkspace.self.Shutdown();
    
    string dir = GetDirPath();
    if(Directory.Exists(dir))
      Directory.Delete(dir, true/*recursive*/);
    
    var files = new List<string>();
    NewTestDocument("bhl1.bhl", bhl1, files);

    Uri uri = GetUri(files[0]);

    string json =
      "{\"params\":{\"textDocument\":{\"languageId\":\"bhl\",\"version\":0,\"uri\":\"" + uri.ToString() +
      "\",\"text\":\"" + bhl1 +
      "\"}},\"method\":\"textDocument/didOpen\",\"jsonrpc\":\"2.0\"}";
    
    AssertEqual(
      rpc.HandleMessage(json),
      string.Empty
    );

    var document = BHLSPWorkspace.self.FindTextDocument(uri);
    string line = document.Text.Split('\n')[1];
    
    AssertEqual(
      line,
      "    func float test1(float k) "
    );
    
    if(Directory.Exists(dir))
      Directory.Delete(dir, true/*recursive*/);
    
    NewTestDocument("bhl1.bhl", bhl2, files);

    uri = GetUri(files[1]);

    json = "{\"params\":{\"textDocument\":{\"version\":1,\"uri\":\"" + uri.ToString() +
           "\"},\"contentChanges\":[{\"text\":\"" + bhl2 +
           "\"}]},\"method\":\"textDocument/didChange\",\"jsonrpc\":\"2.0\"}";
    
    AssertEqual(
      rpc.HandleMessage(json),
      string.Empty
    );
    
    line = document.Text.Split('\n')[1];
    
    AssertEqual(
      line,
      "    func float test1(float k, int j) "
    );
    
    json = "{\"params\":{\"textDocument\":{\"uri\":\"" + uri +
           "\"}},\"method\":\"textDocument/didClose\",\"jsonrpc\":\"2.0\"}";
    
    AssertEqual(
      rpc.HandleMessage(json),
      string.Empty
    );
  }
  
  [IsTested()]
  public void TestSignatureHelp()
  {
    string bhl1 = @"
    func float test1(float k, float n) 
    {
      return 0
    }

    func test2() 
    {
      test1()
    }

    func test3() 
    {
      test1(5.2,)
    }
    ";
    
    var rpc = new BHLSPJsonRpc();
    rpc.AttachRpcService(new BHLSPTextDocumentSignatureHelpJsonRpcService());
    
    BHLSPWorkspace.self.Shutdown();
    
    string dir = GetDirPath();
    if(Directory.Exists(dir))
      Directory.Delete(dir, true/*recursive*/);
    
    var files = new List<string>();
    NewTestDocument("bhl1.bhl", bhl1, files);
    
    Uri uri = GetUri(files[0]);

    var json = "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/signatureHelp\", \"params\":";
    json += "{\"textDocument\": {\"uri\": \"" + uri.ToString();
    json += "\"}, \"position\": {\"line\": 8, \"character\": 12}}}";
    
    AssertEqual(
      rpc.HandleMessage(json),
      "{\"id\":1,\"result\":{\"signatures\":[{\"label\":\"test1(float k, float n):float \",\"documentation\":null,\"parameters\":[" +
      "{\"label\":\"float k\",\"documentation\":\"\"},{\"label\":\"float n\",\"documentation\":\"\"}]," +
      "\"activeParameter\":0}],\"activeSignature\":0,\"activeParameter\":0},\"jsonrpc\":\"2.0\"}"
    );
    
    json = "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/signatureHelp\", \"params\":";
    json += "{\"textDocument\": {\"uri\": \"" + uri.ToString();
    json += "\"}, \"position\": {\"line\": 13, \"character\": 16}}}";
    
    AssertEqual(
      rpc.HandleMessage(json),
      "{\"id\":1,\"result\":{\"signatures\":[{\"label\":\"test1(float k, float n):float \",\"documentation\":null,\"parameters\":[" +
      "{\"label\":\"float k\",\"documentation\":\"\"},{\"label\":\"float n\",\"documentation\":\"\"}]," +
      "\"activeParameter\":1}],\"activeSignature\":0,\"activeParameter\":1},\"jsonrpc\":\"2.0\"}"
    );
  }

  static Uri GetUri(string path)
  {
    return new Uri("file://" + path);
  }
  
  static string GetDirPath()
  {
    string self_bin = System.Reflection.Assembly.GetExecutingAssembly().Location;
    return Path.GetDirectoryName(self_bin) + "/tmp/bhlsp";
  }
  
  static void NewTestDocument(string path, string text, List<string> files)
  {
    string full_path = GetDirPath() + "/" + path;
    Directory.CreateDirectory(Path.GetDirectoryName(full_path));
    File.WriteAllText(full_path, text);
    files.Add(full_path);
  }
}