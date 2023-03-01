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
      var code = new Code();
      code.Update(bhl);

      AssertEqual("f", bhl[code.CalcByteIndex(0, 0)].ToString());
      AssertEqual("u", bhl[code.CalcByteIndex(0, 1)].ToString());
      AssertEqual("{", bhl[code.CalcByteIndex(1, 0)].ToString());
      AssertEqual("h", bhl[code.CalcByteIndex(2, 1)].ToString());
      AssertEqual("}", bhl[code.CalcByteIndex(3, 0)].ToString());

      AssertEqual(-1, code.CalcByteIndex(4, 0));
      AssertEqual(-1, code.CalcByteIndex(100, 1));
    });

    SubTest("Windows line endings", () => {
      string bhl = "func int test()\r\n{\r\nwhat()\r\n}";
      var code = new Code();
      code.Update(bhl);

      AssertEqual("f", bhl[code.CalcByteIndex(0, 0)].ToString());
      AssertEqual("u", bhl[code.CalcByteIndex(0, 1)].ToString());
      AssertEqual("{", bhl[code.CalcByteIndex(1, 0)].ToString());
      AssertEqual("h", bhl[code.CalcByteIndex(2, 1)].ToString());
      AssertEqual("}", bhl[code.CalcByteIndex(3, 0)].ToString());

      AssertEqual(-1, code.CalcByteIndex(4, 0));
      AssertEqual(-1, code.CalcByteIndex(100, 1));
    });

    SubTest("Mixed line endings", () => {
      string bhl = "func int test()\n{\r\nwhat()\r}";
      var code = new Code();
      code.Update(bhl);

      AssertEqual("f", bhl[code.CalcByteIndex(0, 0)].ToString());
      AssertEqual("u", bhl[code.CalcByteIndex(0, 1)].ToString());
      AssertEqual("{", bhl[code.CalcByteIndex(1, 0)].ToString());
      AssertEqual("h", bhl[code.CalcByteIndex(2, 1)].ToString());
      AssertEqual("}", bhl[code.CalcByteIndex(3, 0)].ToString());

      AssertEqual(-1, code.CalcByteIndex(4, 0));
      AssertEqual(-1, code.CalcByteIndex(100, 1));
    });
  }

  [IsTested()]
  public void TestDocumentGetLineColumn()
  {
    string bhl = "func int test()\n{\nwhat()\n}";
    var code = new Code();
    code.Update(bhl);

    {
      var pos = code.GetIndexPosition(0);
      AssertEqual("f", bhl[code.CalcByteIndex(pos.line, pos.column)].ToString());
    }

    {
      var pos = code.GetIndexPosition(1);
      AssertEqual("u", bhl[code.CalcByteIndex(pos.line, pos.column)].ToString());
    }

    {
      var pos = code.GetIndexPosition(16);
      AssertEqual("{", bhl[code.CalcByteIndex(pos.line, pos.column)].ToString());
    }

    {
      var pos = code.GetIndexPosition(bhl.Length-1);
      AssertEqual("}", bhl[code.CalcByteIndex(pos.line, pos.column)].ToString());
    }

    {
      var pos = code.GetIndexPosition(bhl.Length);
      AssertEqual(-1, pos.line);
      AssertEqual(-1, pos.column);
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
      rpc.AttachService(new bhl.lsp.spec.LifecycleService(new Workspace()));
      string json = "{\"jsonrpc\": \"2.0\", \"method\": \"initialize\", \"params\": \"bar\",\"id\": 1}";
      AssertEqual(
        rpc.Handle(json),
        "{\"id\":1,\"error\":{\"code\":-32602,\"message\":\"\"},\"jsonrpc\":\"2.0\"}"
      );
    });
  }

  [IsTested()]
  public void TestInitShutdownExit()
  {
    var rpc = new JsonRpc();
    rpc.AttachService(new bhl.lsp.spec.LifecycleService(new Workspace()));

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
  public void TestOpenChangeCloseDocument()
  {
    string bhl_v1 = @"
    func float test1(float k) 
    {
      return 0
    }

    func test2() 
    {
      test1(1)
    }
    ";
    
    string bhl_v2 = @"
    func float test1(float k, int j) 
    {
      return 0
    }

    func test2() 
    {
      test1(1, 2)
    }
    ";
    
    var ws = new Workspace();

    var rpc = new JsonRpc();
    rpc.AttachService(new bhl.lsp.spec.TextDocumentSynchronizationService(ws));
    
    CleanTestFiles();

    var uri = MakeTestDocument("bhl1.bhl", bhl_v1);

    ws.AddRoot(GetTestDirPath());
    
    {
      ws.IndexFiles(new bhl.Types());

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
        document.code.Text
      );
    }

    CleanTestFiles();

    {
      MakeTestDocument("bhl1.bhl", bhl_v2);

      ws.IndexFiles(new bhl.Types());

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
        document.code.Text
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
  public void TestGoToDefinition()
  {
    string bhl1 = @"
    class Foo {
      int BAR
    }

    Foo foo = {
      BAR : 0
    }

    enum ErrorCodes {
      Ok = 0
      Bad = 1
    }

    func float test1(float k) 
    {
      return 0
    }

    func test2() 
    {
      var tmp_foo = new Foo //create new Foo
      test1(42)
    }
    ";
    
    string bhl2 = @"
    import ""bhl1""

    func float test3(float k, int j)
    {
      k = 10
      j = 100
      return 0
    }

    func ErrorCodes test4() 
    {
      test2()
      foo.BAR = 1

      ErrorCodes err = ErrorCodes.Bad //error code 
      return err
    }
    ";
    
    var ws = new Workspace();

    var rpc = new JsonRpc();
    rpc.AttachService(new bhl.lsp.spec.TextDocumentGoToService(ws));
    
    CleanTestFiles();
    
    Uri uri1 = MakeTestDocument("bhl1.bhl", bhl1);
    Uri uri2 = MakeTestDocument("bhl2.bhl", bhl2);
    
    ws.AddRoot(GetTestDirPath());

    ws.IndexFiles(new bhl.Types());
    
    SubTest(() => {
      AssertEqual(
        rpc.Handle(GoToDefinitionReq(uri1, "st1(42)")),
        GoToDefinitionRsp(uri1, "func float test1(float k)", line_offset: 3)
      );
    });
    
    SubTest(() => {
      AssertEqual(
        rpc.Handle(GoToDefinitionReq(uri2, "est2()")),
        GoToDefinitionRsp(uri1, "func test2()", line_offset: 4)
      );
    });
    
    SubTest(() => {
      AssertEqual(
        rpc.Handle(GoToDefinitionReq(uri1, "oo foo = {")),
        GoToDefinitionRsp(uri1, "class Foo {", line_offset: 2)
      );
    });

    SubTest(() => {
      AssertEqual(
        rpc.Handle(GoToDefinitionReq(uri1, "Foo //create new Foo")),
        GoToDefinitionRsp(uri1, "class Foo {", line_offset: 2)
      );
    });

    SubTest(() => {
      AssertEqual(
        rpc.Handle(GoToDefinitionReq(uri1, "BAR : 0")),
        GoToDefinitionRsp(uri1, "int BAR", column_offset: 6)
      );
    });

    SubTest(() => {
      AssertEqual(
        rpc.Handle(GoToDefinitionReq(uri2, "BAR = 1")),
        GoToDefinitionRsp(uri1, "int BAR", column_offset: 6)
      );
    });

    SubTest(() => {
      AssertEqual(
        rpc.Handle(GoToDefinitionReq(uri2, "AR = 1")),
        GoToDefinitionRsp(uri1, "int BAR", column_offset: 6)
      );
    });

    SubTest(() => {
      AssertEqual(
        rpc.Handle(GoToDefinitionReq(uri2, "foo.BAR")),
        GoToDefinitionRsp(uri1, "foo = {", column_offset: 2)
      );
    });

    SubTest(() => {
      AssertEqual(
        rpc.Handle(GoToDefinitionReq(uri2, "k = 10")),
        GoToDefinitionRsp(uri2, "k, int j)")
      );
    });

    SubTest(() => {
      AssertEqual(
        rpc.Handle(GoToDefinitionReq(uri2, "j = 100")),
        GoToDefinitionRsp(uri2, "j)")
      );
    });

    SubTest(() => {
      AssertEqual(
        rpc.Handle(GoToDefinitionReq(uri2, "test4()")),
        NullResultJson()
      );
    });

    SubTest(() => {
      AssertEqual(
        rpc.Handle(GoToDefinitionReq(uri2, "rrorCodes err")),
        GoToDefinitionRsp(uri1, "enum ErrorCodes", line_offset: 3)
      );
    });

    SubTest(() => {
      AssertEqual(
        rpc.Handle(GoToDefinitionReq(uri2, "Bad //error code")),
        GoToDefinitionRsp(uri1, "Bad = 1", column_offset: 6)
      );
    });

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
      test1() //signature help 1
    }

    func test3() 
    {
      test1(5.2, //signature help 2
    }
    ";
    
    var ws = new Workspace();

    var rpc = new JsonRpc();
    rpc.AttachService(new bhl.lsp.spec.TextDocumentSignatureHelpService(ws));
    
    CleanTestFiles();
    
    Uri uri = MakeTestDocument("bhl1.bhl", bhl1);

    ws.AddRoot(GetTestDirPath());

    ws.IndexFiles(new bhl.Types());

    SubTest(() => {
      AssertEqual(
        rpc.Handle(SignatureHelpReq(uri, ") //signature help 1")),
        "{\"id\":1,\"result\":{\"signatures\":[{\"label\":\"func float test1(float,float)\",\"documentation\":null,\"parameters\":[" +
        "{\"label\":\"float k\",\"documentation\":\"\"},{\"label\":\"float n\",\"documentation\":\"\"}]," +
        "\"activeParameter\":0}],\"activeSignature\":0,\"activeParameter\":0},\"jsonrpc\":\"2.0\"}"
      );
    });
    
    SubTest(() => {
      AssertEqual(
        rpc.Handle(SignatureHelpReq(uri, ", //signature help 2")),
        "{\"id\":1,\"result\":{\"signatures\":[{\"label\":\"func float test1(float,float)\",\"documentation\":null,\"parameters\":[" +
        "{\"label\":\"float k\",\"documentation\":\"\"},{\"label\":\"float n\",\"documentation\":\"\"}]," +
        "\"activeParameter\":0}],\"activeSignature\":0,\"activeParameter\":0},\"jsonrpc\":\"2.0\"}"
      );
    });
  }

  //[IsTested()]
  //public void TestSemanticTokens()
  //{
  //  string bhl1 = @"
  //  class Foo {
  //    int BAR
  //  }

  //  Foo foo = {
  //    BAR : 0
  //  }

  //  func float test1(float k) 
  //  {
  //    return 0
  //  }

  //  func test2() 
  //  {
  //    test1()
  //  }
  //  ";
  //  
  //  var ws = new Workspace();

  //  var rpc = new JsonRpc();
  //  rpc.AttachService(new bhl.lsp.spec.TextDocumentSemanticTokensService(ws));
  //  
  //  CleanTestFiles();
  //  
  //  Uri uri1 = MakeTestDocument("bhl1.bhl", bhl1);
  //  
  //  var json = "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/semanticTokens/full\", \"params\":";
  //  json += "{\"textDocument\": {\"uri\": \"" + uri1.ToString() + "\"}}}";
  //  
  //  AssertEqual(
  //    rpc.Handle(json),
  //    "{\"id\":1,\"result\":{\"data\":" +
  //    "[1,4,6,6,0,0,6,3,0,0,1,6,3,6,0,0,4,3,2,10,3,4,3,5,0,0,4,3,2,0,1,6,3,2,0,0,6,1,3,0,3,4,5,6,0,0,5,5,6," +
  //    "0,0,6,5,1,10,0,6,5,6,0,2,6,7,6,0,0,7,1,3,0,3,4,5,6,0,0,5,5,1,10,2,6,5,1,0]},\"jsonrpc\":\"2.0\"}"
  //  );
  //}

  static string GoToDefinitionReq(Uri uri, string needle)
  {
    var pos = Pos(File.ReadAllText(uri.LocalPath), needle);
    return "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/definition\", \"params\":" +
      "{\"textDocument\": {\"uri\": \"" + uri.ToString() +
      "\"}, \"position\": " + AsJson(pos) + "}}";
  }

  static string GoToDefinitionRsp(Uri uri, string needle, int line_offset = 0, int column_offset = 0)
  {
    var start = Pos(File.ReadAllText(uri.LocalPath), needle);
    var end = new bhl.SourcePos(start.line + line_offset, start.column + column_offset);
    return "{\"id\":1,\"result\":{\"uri\":\"" + uri.ToString() +
      "\",\"range\":{\"start\":" + AsJson(start) + ",\"end\":" + AsJson(end) + "}},\"jsonrpc\":\"2.0\"}";
  }

  static string SignatureHelpReq(Uri uri, string needle)
  {
    var pos = Pos(File.ReadAllText(uri.LocalPath), needle);
    return "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/signatureHelp\", \"params\":" +
      "{\"textDocument\": {\"uri\": \"" + uri.ToString() +
      "\"}, \"position\": " + AsJson(pos) + "}}";
  }

  static string NullResultJson()
  {
    return "{\"id\":1,\"result\":\"null\",\"jsonrpc\":\"2.0\"}";
  }
  
  static Uri MakeUri(string path)
  {
    return new Uri("file://" + path);
  }

  static void CleanTestFiles()
  {
    string dir = GetTestDirPath();
    if(Directory.Exists(dir))
      Directory.Delete(dir, true/*recursive*/);
  }
  
  static string GetTestDirPath()
  {
    string self_bin = System.Reflection.Assembly.GetExecutingAssembly().Location;
    return Path.GetDirectoryName(self_bin) + "/tmp/bhlsp";
  }
  
  static Uri MakeTestDocument(string path, string text, List<string> files = null)
  {
    string full_path = GetTestDirPath() + "/" + path;
    Directory.CreateDirectory(Path.GetDirectoryName(full_path));
    File.WriteAllText(full_path, text);
    if(files != null)
      files.Add(full_path);
    return MakeUri(full_path);
  }

  static bhl.SourcePos Pos(string code, string needle)
  {
    int idx = code.IndexOf(needle);
    if(idx == -1)
      throw new Exception("Needle not found: " + needle);
    var indexer = new Code();
    indexer.Update(code);
    var pos = indexer.GetIndexPosition(idx);
    if(pos.line == -1 && pos.column == -1)
      throw new Exception("Needle not mapped position: " + needle);
    return pos;
  }

  static string AsJson(bhl.SourcePos pos)
  {
    return "{\"line\":" + pos.line + ",\"character\":" + pos.column + "}";
  }
}
