using System;
using System.IO;
using System.Collections.Generic;
using bhl.lsp;

public class TestLSP : BHL_TestBase
{
  [IsTested()]
  public void TestDocumentCalcByteIndex()
  {
    SubTest("Unix line endings", () => {
      string bhl = "func int test()\n{\nwhat()\n}";
      var doc = new BHLTextDocument();
      doc.Sync(bhl);

      AssertEqual("f", bhl[doc.CalcByteIndex(0, 0)].ToString());
      AssertEqual("u", bhl[doc.CalcByteIndex(0, 1)].ToString());
      AssertEqual("{", bhl[doc.CalcByteIndex(1, 0)].ToString());
      AssertEqual("h", bhl[doc.CalcByteIndex(2, 1)].ToString());
      AssertEqual("}", bhl[doc.CalcByteIndex(3, 0)].ToString());

      AssertEqual(-1, doc.CalcByteIndex(4, 0));
      AssertEqual(-1, doc.CalcByteIndex(100, 1));
    });

    SubTest("Windows line endings", () => {
      string bhl = "func int test()\r\n{\r\nwhat()\r\n}";
      var doc = new BHLTextDocument();
      doc.Sync(bhl);

      AssertEqual("f", bhl[doc.CalcByteIndex(0, 0)].ToString());
      AssertEqual("u", bhl[doc.CalcByteIndex(0, 1)].ToString());
      AssertEqual("{", bhl[doc.CalcByteIndex(1, 0)].ToString());
      AssertEqual("h", bhl[doc.CalcByteIndex(2, 1)].ToString());
      AssertEqual("}", bhl[doc.CalcByteIndex(3, 0)].ToString());

      AssertEqual(-1, doc.CalcByteIndex(4, 0));
      AssertEqual(-1, doc.CalcByteIndex(100, 1));
    });

    SubTest("Mixed line endings", () => {
      string bhl = "func int test()\n{\r\nwhat()\r}";
      var doc = new BHLTextDocument();
      doc.Sync(bhl);

      AssertEqual("f", bhl[doc.CalcByteIndex(0, 0)].ToString());
      AssertEqual("u", bhl[doc.CalcByteIndex(0, 1)].ToString());
      AssertEqual("{", bhl[doc.CalcByteIndex(1, 0)].ToString());
      AssertEqual("h", bhl[doc.CalcByteIndex(2, 1)].ToString());
      AssertEqual("}", bhl[doc.CalcByteIndex(3, 0)].ToString());

      AssertEqual(-1, doc.CalcByteIndex(4, 0));
      AssertEqual(-1, doc.CalcByteIndex(100, 1));
    });
  }

  [IsTested()]
  public void TestDocumentGetLineColumn()
  {
    string bhl = "func int test()\n{\nwhat()\n}";
    var doc = new BHLTextDocument();
    doc.Sync(bhl);

    {
      var lc = doc.GetLineColumn(0);
      AssertEqual("f", bhl[doc.CalcByteIndex(lc.Item1, lc.Item2)].ToString());
    }

    {
      var lc = doc.GetLineColumn(1);
      AssertEqual("u", bhl[doc.CalcByteIndex(lc.Item1, lc.Item2)].ToString());
    }

    {
      var lc = doc.GetLineColumn(16);
      AssertEqual("{", bhl[doc.CalcByteIndex(lc.Item1, lc.Item2)].ToString());
    }

    {
      var lc = doc.GetLineColumn(bhl.Length-1);
      AssertEqual("}", bhl[doc.CalcByteIndex(lc.Item1, lc.Item2)].ToString());
    }

    {
      var lc = doc.GetLineColumn(bhl.Length);
      AssertEqual(-1, lc.Item1);
      AssertEqual(-1, lc.Item2);
    }
  }

  [IsTested()]
  public void TestRpcResponseErrors()
  {
    SubTest("parse error", () =>
    {
      var rpc = new JsonRpc();
      string json = "{\"jsonrpc\": \"2.0\", \"method\": \"initialize";
      AssertEqual(
        rpc.Handle(json),
        "{\"id\":null,\"error\":{\"code\":-32700,\"message\":\"Parse error\"},\"jsonrpc\":\"2.0\"}"
      );
    });
    
    SubTest("invalid request", () =>
    {
      var rpc = new JsonRpc();
      string json = "{\"jsonrpc\": \"2.0\", \"id\": 1}";
      AssertEqual(
        rpc.Handle(json),
        "{\"id\":1,\"error\":{\"code\":-32600,\"message\":\"\"},\"jsonrpc\":\"2.0\"}"
      );
    });
    
    SubTest("invalid request", () =>
    {
      var rpc = new JsonRpc();
      string json = "{\"jsonrpc\": \"2.0\", \"method\": 1, \"params\": \"bar\",\"id\": 1}";
      AssertEqual(
        rpc.Handle(json),
        "{\"id\":1,\"error\":{\"code\":-32600,\"message\":\"\"},\"jsonrpc\":\"2.0\"}"
      );
    });
    
    SubTest("method not found", () =>
    {
      var rpc = new JsonRpc();
      string json = "{\"jsonrpc\": \"2.0\", \"method\": \"foo\", \"id\": 1}";
      AssertEqual(
        rpc.Handle(json),
        "{\"id\":1,\"error\":{\"code\":-32601,\"message\":\"Method not found\"},\"jsonrpc\":\"2.0\"}"
      );
    });
    
    SubTest("invalid params", () =>
    {
      var rpc = new JsonRpc();
      rpc.AttachService(new GeneralService(new Workspace()));
      string json = "{\"jsonrpc\": \"2.0\", \"method\": \"initialize\", \"params\": \"bar\",\"id\": 1}";
      AssertEqual(
        rpc.Handle(json),
        "{\"id\":1,\"error\":{\"code\":-32602,\"message\":\"\"},\"jsonrpc\":\"2.0\"}"
      );
    });
  }

  [IsTested()]
  public void TestInitShutdownExitRpc()
  {
    var rpc = new JsonRpc();
    rpc.AttachService(new GeneralService(new Workspace()));

    SubTest(() => {
      string req = "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"initialize\", \"params\": {\"capabilities\":{}}}";
      
      string rsp = "{\"id\":1,\"result\":{" +
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

      AssertEqual(rpc.Handle(req), rsp);
    });
    
    SubTest(() => {
      string json = "{\"params\":{},\"method\":\"initialized\",\"jsonrpc\":\"2.0\"}";
      AssertEqual(
        rpc.Handle(json),
        string.Empty
      );
    });
    
    SubTest(() => {
      string json = "{\"id\":1,\"params\":null,\"method\":\"shutdown\",\"jsonrpc\":\"2.0\"}";
      AssertEqual(
        rpc.Handle(json),
        "{\"id\":1,\"result\":\"null\",\"jsonrpc\":\"2.0\"}"
      );
    });
    
    SubTest(() => {
      string json = "{\"params\":null,\"method\":\"exit\",\"jsonrpc\":\"2.0\"}";
      AssertEqual(
        rpc.Handle(json),
        string.Empty
      );
    });
  }

  [IsTested()]
  public void TestSyncDocument()
  {
    string bhl_v1 = @"
    func float test1(float k) 
    {
      return 0
    }

    func test2() 
    {
      test1()
    }
    ";
    
    string bhl_v2 = @"
    func float test1(float k, int j) 
    {
      return 0
    }

    func test2() 
    {
      test1()
    }
    ";
    
    var ws = new Workspace();

    var rpc = new JsonRpc();
    rpc.AttachService(new TextDocumentSynchronizationService(ws));
    
    string dir = GetDirPath();
    Directory.Delete(dir, true/*recursive*/);

    var uri = GetUri(NewTestDocument("bhl1.bhl", bhl_v1));
    
    {
      string json =
        "{\"params\":{\"textDocument\":{\"languageId\":\"bhl\",\"version\":0,\"uri\":\"" + uri.ToString() +
        "\",\"text\":\"" + bhl_v1 +
        "\"}},\"method\":\"textDocument/didOpen\",\"jsonrpc\":\"2.0\"}";
      
      AssertEqual(
        rpc.Handle(json),
        string.Empty
      );

      var document = ws.FindDocument(uri);
      
      AssertEqual(
        bhl_v1,
        document.text
      );
    }

    Directory.Delete(dir, true/*recursive*/);

    {
      NewTestDocument("bhl1.bhl", bhl_v2);

      string json = "{\"params\":{\"textDocument\":{\"version\":1,\"uri\":\"" + uri.ToString() +
             "\"},\"contentChanges\":[{\"text\":\"" + bhl_v2 +
             "\"}]},\"method\":\"textDocument/didChange\",\"jsonrpc\":\"2.0\"}";
      
      AssertEqual(
        rpc.Handle(json),
        string.Empty
      );

      var document = ws.FindDocument(uri);

      AssertEqual(
        bhl_v2,
        document.text
      );
    }
    
    {
      string json = "{\"params\":{\"textDocument\":{\"uri\":\"" + uri +
             "\"}},\"method\":\"textDocument/didClose\",\"jsonrpc\":\"2.0\"}";
      
      AssertEqual(
        rpc.Handle(json),
        string.Empty
      );
    }
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
    
    var ws = new Workspace();

    var rpc = new JsonRpc();
    rpc.AttachService(new TextDocumentSignatureHelpService(ws));
    
    Directory.Delete(GetDirPath(), true/*recursive*/);
    
    var files = new List<string>();
    NewTestDocument("bhl1.bhl", bhl1, files);
    
    Uri uri = GetUri(files[0]);

    SubTest(() => {
      string json = "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/signatureHelp\", \"params\":";
      json += "{\"textDocument\": {\"uri\": \"" + uri.ToString();
      json += "\"}, \"position\": {\"line\": 8, \"character\": 12}}}";
      
      AssertEqual(
        rpc.Handle(json),
        "{\"id\":1,\"result\":{\"signatures\":[{\"label\":\"test1(float k, float n):float \",\"documentation\":null,\"parameters\":[" +
        "{\"label\":\"float k\",\"documentation\":\"\"},{\"label\":\"float n\",\"documentation\":\"\"}]," +
        "\"activeParameter\":0}],\"activeSignature\":0,\"activeParameter\":0},\"jsonrpc\":\"2.0\"}"
      );
    });
    
    SubTest(() => {
      string json = "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/signatureHelp\", \"params\":";
      json += "{\"textDocument\": {\"uri\": \"" + uri.ToString();
      json += "\"}, \"position\": {\"line\": 13, \"character\": 16}}}";
      
      AssertEqual(
        rpc.Handle(json),
        "{\"id\":1,\"result\":{\"signatures\":[{\"label\":\"test1(float k, float n):float \",\"documentation\":null,\"parameters\":[" +
        "{\"label\":\"float k\",\"documentation\":\"\"},{\"label\":\"float n\",\"documentation\":\"\"}]," +
        "\"activeParameter\":1}],\"activeSignature\":0,\"activeParameter\":1},\"jsonrpc\":\"2.0\"}"
      );
    });
  }

  [IsTested()]
  public void TestGoToDefinition()
  {
    string bhl1 = @"
    class Foo {
      int BAR
    }

    Foo foo = {
      BAR : 0
    }

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
    import ""bhl1""

    func float test3(float k, int j) 
    {
      return 0
    }

    func test4() 
    {
      test2()
      foo.BAR = 1
    }
    ";
    
    var ws = new Workspace();

    var rpc = new JsonRpc();
    rpc.AttachService(new TextDocumentGoToService(ws));
    
    string dir = GetDirPath();
    if(Directory.Exists(dir))
      Directory.Delete(dir, true/*recursive*/);
    
    var files = new List<string>();
    NewTestDocument("bhl1.bhl", bhl1, files);
    NewTestDocument("bhl2.bhl", bhl2, files);
    
    ws.AddRoot(GetDirPath(), true);
    
    Uri uri1 = GetUri(files[0]);
    Uri uri2 = GetUri(files[1]);
    
    SubTest(() => {
      string json = "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/definition\", \"params\":";
      json += "{\"textDocument\": {\"uri\": \"" + uri1.ToString();
      json += "\"}, \"position\": {\"line\": 16, \"character\": 8}}}";
      
      AssertEqual(
          rpc.Handle(json),
          "{\"id\":1,\"result\":{\"uri\":\"" + uri1.ToString() +
          "\",\"range\":{\"start\":{\"line\":9,\"character\":4},\"end\":{\"line\":9,\"character\":4}}},\"jsonrpc\":\"2.0\"}"
      );
    });
    
    SubTest(() => {
      string json = "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/definition\", \"params\":";
      json += "{\"textDocument\": {\"uri\": \"" + uri2.ToString();
      json += "\"}, \"position\": {\"line\": 10, \"character\": 8}}}";
      
      AssertEqual(
          rpc.Handle(json),
          "{\"id\":1,\"result\":{\"uri\":\"" + uri1.ToString() +
          "\",\"range\":{\"start\":{\"line\":14,\"character\":4},\"end\":{\"line\":14,\"character\":4}}},\"jsonrpc\":\"2.0\"}"
      );
    });
    
    SubTest(() => {
      string json = "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/definition\", \"params\":";
      json += "{\"textDocument\": {\"uri\": \"" + uri1.ToString();
      json += "\"}, \"position\": {\"line\": 5, \"character\": 5}}}";
      
      AssertEqual(
        rpc.Handle(json),
        "{\"id\":1,\"result\":{\"uri\":\"" + uri1.ToString() +
        "\",\"range\":{\"start\":{\"line\":1,\"character\":4},\"end\":{\"line\":1,\"character\":4}}},\"jsonrpc\":\"2.0\"}"
      );
    });
  }

  [IsTested()]
  public void TestSemanticTokens()
  {
    string bhl1 = @"
    class Foo {
      int BAR
    }

    Foo foo = {
      BAR : 0
    }

    func float test1(float k) 
    {
      return 0
    }

    func test2() 
    {
      test1()
    }
    ";
    
    var ws = new Workspace();

    var rpc = new JsonRpc();
    rpc.AttachService(new TextDocumentSemanticTokensService(ws));
    
    string dir = GetDirPath();
    Directory.Delete(dir, true/*recursive*/);
    
    var files = new List<string>();
    NewTestDocument("bhl1.bhl", bhl1, files);
    
    Uri uri1 = GetUri(files[0]);
    
    var json = "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/semanticTokens/full\", \"params\":";
    json += "{\"textDocument\": {\"uri\": \"" + uri1.ToString() + "\"}}}";
    
    AssertEqual(
        rpc.Handle(json),
        "{\"id\":1,\"result\":{\"data\":" +
        "[1,4,6,6,0,0,6,3,0,0,1,6,3,6,0,0,4,3,2,10,3,4,3,5,0,0,4,3,2,0,1,6,3,2,0,0,6,1,3,0,3,4,5,6,0,0,5,5,6," +
        "0,0,6,5,1,10,0,6,5,6,0,2,6,7,6,0,0,7,1,3,0,3,4,5,6,0,0,5,5,1,10,2,6,5,1,0]},\"jsonrpc\":\"2.0\"}"
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
  
  static string NewTestDocument(string path, string text, List<string> files = null)
  {
    string full_path = GetDirPath() + "/" + path;
    Directory.CreateDirectory(Path.GetDirectoryName(full_path));
    File.WriteAllText(full_path, text);
    if(files != null)
      files.Add(full_path);
    return full_path;
  }
}
