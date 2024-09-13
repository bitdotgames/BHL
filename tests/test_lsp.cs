using System;
using System.IO;
using System.Collections.Generic;
using bhl;
using bhl.lsp;
using Xunit;

public class TestLSP : BHL_TestBase
{
  [Fact]
  public void TestDocumentCalcByteIndex()
  {
    SubTest("Unix line endings", () => {
      string bhl = "func int test()\n{\nwhat()\n}";
      var code = new CodeIndex();
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
      var code = new CodeIndex();
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
      var code = new CodeIndex();
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

  [Fact]
  public void TestDocumentGetLineColumn()
  {
    string bhl = "func int test()\n{\nwhat()\n}";
    var code = new CodeIndex();
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

  [Fact]
  public void TestNativeSymbolReflection()
  {
    var fn = new FuncSymbolNative(new Origin(), "test", Types.Void,
      delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
        return null;
      }
    );
    AssertTrue(fn.origin.source_file.EndsWith("test_lsp.cs"));
    AssertTrue(fn.origin.source_range.start.line > 0);
  }

  [Fact]
  public void TestRpcResponseErrors()
  {
    SubTest("parse error", () =>
    {
      var srv = new Server(NoLogger(), NoConnection(), new Workspace());
      string json = "{\"jsonrpc\": \"2.0\", \"method\": \"initialize";
      AssertEqual(
        srv.Handle(json),
        "{\"id\":null,\"error\":{\"code\":-32700,\"message\":\"Parse error\"},\"jsonrpc\":\"2.0\"}"
      );
    });
    
    SubTest("invalid request", () =>
    {
      var srv = new Server(NoLogger(), NoConnection(), new Workspace());
      string json = "{\"jsonrpc\": \"2.0\", \"id\": 1}";
      AssertEqual(
        srv.Handle(json),
        "{\"id\":1,\"error\":{\"code\":-32600,\"message\":\"\"},\"jsonrpc\":\"2.0\"}"
      );
    });
    
    SubTest("invalid request", () =>
    {
      var srv = new Server(NoLogger(), NoConnection(), new Workspace());
      string json = "{\"jsonrpc\": \"2.0\", \"method\": 1, \"params\": \"bar\",\"id\": 1}";
      AssertEqual(
        srv.Handle(json),
        "{\"id\":1,\"error\":{\"code\":-32600,\"message\":\"\"},\"jsonrpc\":\"2.0\"}"
      );
    });
    
    SubTest("method not found", () =>
    {
      var srv = new Server(NoLogger(), NoConnection(), new Workspace());
      string json = "{\"jsonrpc\": \"2.0\", \"method\": \"foo\", \"id\": 1}";
      AssertEqual(
        srv.Handle(json),
        "{\"id\":1,\"error\":{\"code\":-32601,\"message\":\"Method not found: foo\"},\"jsonrpc\":\"2.0\"}"
      );
    });
    
    SubTest("invalid params", () =>
    {
      var srv = new Server(NoLogger(), NoConnection(), new Workspace());
      srv.AttachService(new bhl.lsp.LifecycleService(srv));
      string json = "{\"jsonrpc\": \"2.0\", \"method\": \"initialize\", \"params\": \"bar\",\"id\": 1}";
      AssertEqual(
        srv.Handle(json),
        "{\"id\":1,\"error\":{\"code\":-32602,\"message\":\"Error converting value \\\"bar\\\" to type 'bhl.lsp.proto.InitializeParams'. Path ''.\"},\"jsonrpc\":\"2.0\"}"
      );
    });
  }

  [Fact]
  public void TestInitShutdownExit()
  {
    var srv = new Server(NoLogger(), NoConnection(), new Workspace());
    srv.AttachService(new bhl.lsp.LifecycleService(srv));

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
                "\"serverInfo\":{\"name\":\"bhlsp\",\"version\":\"" + bhl.Version.Name + "\"}}," +
                "\"jsonrpc\":\"2.0\"}";

      AssertEqual(srv.Handle(req), rsp);
    });
    
    SubTest(() => {
      AssertEqual(
        srv.Handle(new Request(1, "initialized").ToJson()),
        string.Empty
      );
    });
    
    SubTest(() => {
      AssertEqual(
        srv.Handle(new Request(1, "shutdown").ToJson()),
        NullResultJson(1)
      );
    });
    
    SubTest(() => {
      AssertFalse(srv.going_to_exit);
      AssertEqual(
        srv.Handle(new Request(1, "exit").ToJson()),
        string.Empty
      );
      AssertTrue(srv.going_to_exit);
    });
  }

  public class MockConnection : IConnection
  {
    public string buffer = "";

    public string Read()
    {
      return null;
    }

    public void Write(string json)
    {
      buffer += json;
    }
  }

  [Fact]
  public void TestBadImportError()
  {
    string bhl_v1 = @"
    import hey""

    class Foo {
      int BAR
    }
    ";

    var ws = new Workspace();

    var conn = new MockConnection();
    var srv = new Server(NoLogger(), conn, ws);
    srv.AttachService(new bhl.lsp.TextDocumentSynchronizationService(srv));
    srv.AttachService(new bhl.lsp.DiagnosticService(srv));

    CleanTestFiles();

    var uri = MakeTestDocument("bhl1.bhl", bhl_v1);

    ws.Init(new bhl.Types(), GetTestProjConf());

    {
      ws.IndexFiles();

      AssertEqual(
        srv.Handle(new Request(1, "textDocument/didOpen",
          new bhl.lsp.proto.DidOpenTextDocumentParams() {
            textDocument = new bhl.lsp.proto.TextDocumentItem() { 
              languageId = "bhl", 
              version = 0, 
              uri = uri, 
              text = bhl_v1 
            }}
          ).ToJson()),
        string.Empty
      );

      AssertContains(conn.buffer, "invalid import 'missing NORMALSTRING'");
    }
  }

  [Fact]
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

    var srv = new Server(NoLogger(), NoConnection(), ws);
    srv.AttachService(new bhl.lsp.TextDocumentSynchronizationService(srv));
    
    CleanTestFiles();

    var uri = MakeTestDocument("bhl1.bhl", bhl_v1);

    ws.Init(new bhl.Types(), GetTestProjConf());
    
    {
      ws.IndexFiles();

      AssertEqual(
        srv.Handle(new Request(1, "textDocument/didOpen",
          new bhl.lsp.proto.DidOpenTextDocumentParams() {
            textDocument = new bhl.lsp.proto.TextDocumentItem() { 
              languageId = "bhl", 
              version = 0, 
              uri = uri, 
              text = bhl_v1 
            }}
          ).ToJson()),
        string.Empty
      );
    }

    CleanTestFiles();

    {
      MakeTestDocument("bhl1.bhl", bhl_v2);

      ws.IndexFiles();

      AssertEqual(
        srv.Handle(new Request(1, "textDocument/didChange",
          new bhl.lsp.proto.DidChangeTextDocumentParams() {
            textDocument = new bhl.lsp.proto.VersionedTextDocumentIdentifier() { 
              version = 1, 
              uri = uri
            },
            contentChanges = new[] {
             new bhl.lsp.proto.TextDocumentContentChangeEvent { text = bhl_v2 }
            }
          }).ToJson()
        ),
        string.Empty
      );
    }
    
    {
      AssertEqual(
        srv.Handle(new Request(1, "textDocument/didClose",
          new bhl.lsp.proto.DidCloseTextDocumentParams() {
            textDocument = new bhl.lsp.proto.TextDocumentIdentifier() { 
              uri = uri
            }
          }).ToJson()
        ),
        string.Empty
      );
    }
  }
  
  [Fact]
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

    func test5() 
    {
      TEST() //native call
    }
    
    func test6()
    {
      int upval = 1 //upval value
      func() {
        func () {
          upval = upval + 1 
        }()
      }()
    }
    ";
    
    var ws = new Workspace();

    var srv = new Server(NoLogger(), NoConnection(), ws);
    srv.AttachService(new bhl.lsp.TextDocumentGoToService(srv));
    
    CleanTestFiles();
    
    var uri1 = MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    var ts = new bhl.Types();
    var fn_TEST = new FuncSymbolNative(new Origin(), "TEST", Types.Void, null);
    ts.ns.Define(fn_TEST);
    
    ws.Init(ts, GetTestProjConf());
    ws.IndexFiles();
    
    SubTest(() => {
      AssertEqual(
        srv.Handle(GoToDefinitionReq(uri1, "st1(42)")),
        GoToDefinitionRsp(uri1, "func float test1(float k)", end_line_offset: 3)
      );
    });
    
    SubTest(() => {
      AssertEqual(
        srv.Handle(GoToDefinitionReq(uri2, "est2()")),
        GoToDefinitionRsp(uri1, "func test2()", end_line_offset: 4)
      );
    });
    
    SubTest(() => {
      AssertEqual(
        srv.Handle(GoToDefinitionReq(uri1, "oo foo = {")),
        GoToDefinitionRsp(uri1, "class Foo {", end_line_offset: 2)
      );
    });

    SubTest(() => {
      AssertEqual(
        srv.Handle(GoToDefinitionReq(uri1, "Foo //create new Foo")),
        GoToDefinitionRsp(uri1, "class Foo {", end_line_offset: 2)
      );
    });

    SubTest(() => {
      AssertEqual(
        srv.Handle(GoToDefinitionReq(uri1, "BAR : 0")),
        GoToDefinitionRsp(uri1, "int BAR", end_column_offset: 6)
      );
    });

    SubTest(() => {
      AssertEqual(
        srv.Handle(GoToDefinitionReq(uri2, "BAR = 1")),
        GoToDefinitionRsp(uri1, "int BAR", end_column_offset: 6)
      );
    });

    SubTest(() => {
      AssertEqual(
        srv.Handle(GoToDefinitionReq(uri2, "AR = 1")),
        GoToDefinitionRsp(uri1, "int BAR", end_column_offset: 6)
      );
    });

    SubTest(() => {
      AssertEqual(
        srv.Handle(GoToDefinitionReq(uri2, "foo.BAR")),
        GoToDefinitionRsp(uri1, "foo = {", end_column_offset: 2)
      );
    });

    SubTest(() => {
      AssertEqual(
        srv.Handle(GoToDefinitionReq(uri2, "k = 10")),
        GoToDefinitionRsp(uri2, "k, int j)")
      );
    });

    SubTest(() => {
      AssertEqual(
        srv.Handle(GoToDefinitionReq(uri2, "j = 100")),
        GoToDefinitionRsp(uri2, "j)")
      );
    });

    SubTest(() => {
      AssertEqual(
        srv.Handle(GoToDefinitionReq(uri2, "test4()")),
        GoToDefinitionRsp(uri2, "func ErrorCodes test4()", end_line_offset: 7)
      );
    });

    SubTest(() => {
      AssertEqual(
        srv.Handle(GoToDefinitionReq(uri2, "rrorCodes err")),
        GoToDefinitionRsp(uri1, "enum ErrorCodes", end_line_offset: 3)
      );
    });

    SubTest(() => {
      AssertEqual(
        srv.Handle(GoToDefinitionReq(uri2, "Bad //error code")),
        GoToDefinitionRsp(uri1, "Bad = 1", end_column_offset: 6)
      );
    });

    SubTest(() => {
      var rsp = srv.Handle(GoToDefinitionReq(uri2, "EST() //native call"));
      AssertContains(rsp, "test_lsp.cs");
      AssertContains(rsp, "\"start\":{\"line\":"+(fn_TEST.origin.source_range.start.line-1)+",\"character\":1},\"end\":{\"line\":"+(fn_TEST.origin.source_range.start.line-1)+",\"character\":1}}");
    });

    SubTest(() => {
      AssertEqual(
        srv.Handle(GoToDefinitionReq(uri2, "pval + 1")),
        GoToDefinitionRsp(uri2, "upval = 1", end_column_offset: 4)
      );
    });

  }

  [Fact]
  public void TestFindReferences()
  {
    string bhl1 = @"
    func float test1(float k) 
    {
      return 0
    }

    func test2() 
    {
      test1(42)
    }

    func test3()
    {
      int upval = 1 //upval value
      func() {
        upval = upval + 1 //upval1
      }()
    }

    func test4(int foo, int bar, int upval) //upval arg
    {
      sink(
        func() {
          func() {
            upval = upval + 1 //upval2 
          }()
        }
      )
    }

    func sink(func() ptr) {}
    ";
    
    string bhl2 = @"
    import ""bhl1""

    func float test5(float k, int j)
    {
      test1(24)
      return 0
    }
    ";
    
    var ws = new Workspace();

    var srv = new Server(NoLogger(), NoConnection(), ws);
    srv.AttachService(new bhl.lsp.TextDocumentFindReferencesService(srv));
    
    CleanTestFiles();
    
    var uri1 = MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    var ts = new bhl.Types();
    
    ws.Init(ts, GetTestProjConf());
    ws.IndexFiles();
    
    SubTest(() => {
      AssertEqual(
        srv.Handle(FindReferencesReq(uri1, "st1(42)")),
        FindReferencesRsp(
          new UriNeedle(uri1, "func float test1(float k)", end_line_offset: 3),
          new UriNeedle(uri1, "test1(42)", end_column_offset: 4),
          new UriNeedle(uri2, "test1(24)", end_column_offset: 4)
        )
      );
    });

    SubTest(() => {
      AssertEqual(
        srv.Handle(FindReferencesReq(uri1, "pval + 1")),
        FindReferencesRsp(
          new UriNeedle(uri1, "int upval = 1", end_column_offset: 8),
          new UriNeedle(uri1, "upval = upval + 1 //upval1", end_column_offset: 4),
          new UriNeedle(uri1, "upval + 1 //upval1", end_column_offset: 4)
        )
      );
    });

    SubTest(() => {
      AssertEqual(
        srv.Handle(FindReferencesReq(uri1, "upval) //upval arg")),
        FindReferencesRsp(
          new UriNeedle(uri1, "int upval) //upval arg", end_column_offset: 8),
          new UriNeedle(uri1, "upval = upval + 1 //upval2", end_column_offset: 4),
          new UriNeedle(uri1, "upval + 1 //upval2", end_column_offset: 4)
        )
      );
    });
  }

  [Fact]
  public void TestHover()
  {
    string bhl1 = @"
    func float test1(float k, float n) 
    {
      return k //return k
    }

    func test2() 
    {
      test1() //hover 1
    }
    ";
    
    var ws = new Workspace();

    var srv = new Server(NoLogger(), NoConnection(), ws);
    srv.AttachService(new bhl.lsp.TextDocumentHoverService(srv));
    
    CleanTestFiles();
    
    var uri = MakeTestDocument("bhl1.bhl", bhl1);

    ws.Init(new bhl.Types(), GetTestProjConf());
    ws.IndexFiles();

    SubTest(() => {
      AssertEqual(
        srv.Handle(HoverReq(uri, "est1() //hover 1")),
        "{\"id\":1,\"result\":{\"contents\":{\"kind\":\"plaintext\",\"value\":\"func float test1(float k,float n)\"}},\"jsonrpc\":\"2.0\"}"
      );
    });

    SubTest(() => {
      AssertEqual(
        srv.Handle(HoverReq(uri, "k //return k")),
        "{\"id\":1,\"result\":{\"contents\":{\"kind\":\"plaintext\",\"value\":\"float k\"}},\"jsonrpc\":\"2.0\"}"
      );
    });
  }

  //TODO:
  //[Fact]
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

    var srv = new Server(NoLogger(), NoConnection(), ws);
    srv.AttachService(new bhl.lsp.TextDocumentSignatureHelpService(srv));
    
    CleanTestFiles();
    
    var uri = MakeTestDocument("bhl1.bhl", bhl1);

    ws.Init(new bhl.Types(), GetTestProjConf());
    ws.IndexFiles();

    SubTest(() => {
      AssertEqual(
        srv.Handle(SignatureHelpReq(uri, ") //signature help 1")),
        "{\"id\":1,\"result\":{\"signatures\":[{\"label\":\"func float test1(float,float)\",\"documentation\":null,\"parameters\":[" +
        "{\"label\":\"float k\",\"documentation\":\"\"},{\"label\":\"float n\",\"documentation\":\"\"}]," +
        "\"activeParameter\":0}],\"activeSignature\":0,\"activeParameter\":0},\"jsonrpc\":\"2.0\"}"
      );
    });
    
    SubTest(() => {
      AssertEqual(
        srv.Handle(SignatureHelpReq(uri, ", //signature help 2")),
        "{\"id\":1,\"result\":{\"signatures\":[{\"label\":\"func float test1(float,float)\",\"documentation\":null,\"parameters\":[" +
        "{\"label\":\"float k\",\"documentation\":\"\"},{\"label\":\"float n\",\"documentation\":\"\"}]," +
        "\"activeParameter\":0}],\"activeSignature\":0,\"activeParameter\":0},\"jsonrpc\":\"2.0\"}"
      );
    });
  }

  [Fact]
  public void TestSemanticTokensBasic()
  {
    string bhl = @"
    class BHL_M3Globals
    {
      []string match_chips_sfx
    }
    ";

    var ws = new Workspace();

    var srv = new Server(NoLogger(), NoConnection(), ws);
    srv.AttachService(new bhl.lsp.TextDocumentSemanticTokensService(srv));
    
    CleanTestFiles();
    
    var uri1 = MakeTestDocument("bhl1.bhl", bhl);

    var ts = new bhl.Types();
    ws.Init(ts, GetTestProjConf());
    ws.IndexFiles();
    
    var json = "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/semanticTokens/full\", \"params\":";
    json += "{\"textDocument\": {\"uri\": \"" + uri1.ToString() + "\"}}}";
    
    AssertEqual(
      srv.Handle(json),
      "{\"id\":1,\"result\":{\"data\":" +
      "[1,4,5,6,0,"+"0,6,13,0,0,"+"2,8,6,5,0,"+"0,7,15,2,2]},\"jsonrpc\":\"2.0\"}"
    );
  }

  static string GoToDefinitionReq(bhl.lsp.proto.Uri uri, string needle)
  {
    var pos = Pos(File.ReadAllText(uri.path), needle);
    return "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/definition\", \"params\":" +
      "{\"textDocument\": {\"uri\": \"" + uri + "\"}, \"position\": " + AsJson(pos) + "}}";
  }

  static string GoToDefinitionRsp(bhl.lsp.proto.Uri uri, string needle, int end_line_offset = 0, int end_column_offset = 0)
  {
    var start = Pos(File.ReadAllText(uri.path), needle);
    var end = new bhl.SourcePos(start.line + end_line_offset, start.column + end_column_offset);
    return "{\"id\":1,\"result\":{\"uri\":\"" + uri + "\",\"range\":{\"start\":" + 
      AsJson(start) + ",\"end\":" + AsJson(end) + "}},\"jsonrpc\":\"2.0\"}";
  }

  static string FindReferencesReq(bhl.lsp.proto.Uri uri, string needle)
  {
    var pos = Pos(File.ReadAllText(uri.path), needle);
    return "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/references\", \"params\":" +
      "{\"textDocument\": {\"uri\": \"" + uri + "\"}, \"position\": " + AsJson(pos) + "}}";
  }

  public struct UriNeedle
  {
    public bhl.lsp.proto.Uri uri;
    public string needle;
    public int end_line_offset;
    public int end_column_offset;

    public UriNeedle(bhl.lsp.proto.Uri uri, string needle, int end_line_offset = 0, int end_column_offset = 0)
    {
      this.uri = uri;
      this.needle = needle;
      this.end_line_offset = end_line_offset;
      this.end_column_offset = end_column_offset;
    }
  }

  static string FindReferencesRsp(params UriNeedle[] uns)
  {
    string rsp = "{\"id\":1,\"result\":[";

    foreach(var un in uns)
    {
      var start = Pos(File.ReadAllText(un.uri.path), un.needle);
      var end = new bhl.SourcePos(start.line + un.end_line_offset, start.column + un.end_column_offset);
      rsp += "{\"uri\":\"" + un.uri + "\",\"range\":{\"start\":" + 
        AsJson(start) + ",\"end\":" + AsJson(end) + "}},";
    }
    rsp = rsp.TrimEnd(',');

    rsp += "],\"jsonrpc\":\"2.0\"}";

    return rsp;
  }

  static string HoverReq(bhl.lsp.proto.Uri uri, string needle)
  {
    var pos = Pos(File.ReadAllText(uri.path), needle);
    return "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/hover\", \"params\":" +
      "{\"textDocument\": {\"uri\": \"" + uri + "\"}, \"position\": " + AsJson(pos) + "}}";
  }

  static string SignatureHelpReq(bhl.lsp.proto.Uri uri, string needle)
  {
    var pos = Pos(File.ReadAllText(uri.path), needle);
    return "{\"id\": 1,\"jsonrpc\": \"2.0\", \"method\": \"textDocument/signatureHelp\", \"params\":" +
      "{\"textDocument\": {\"uri\": \"" + uri + "\"}, \"position\": " + AsJson(pos) + "}}";
  }

  static void CleanTestFiles()
  {
    string dir = GetTestDirPath();
    if(Directory.Exists(dir))
      Directory.Delete(dir, true/*recursive*/);
  }

  static bhl.ProjectConf GetTestProjConf()
  {
    var conf = new bhl.ProjectConf();
    conf.src_dirs.Add(GetTestDirPath());
    conf.inc_path = GetTestIncPath();
    return conf;
  }

  static bhl.IncludePath GetTestIncPath()
  {
    var inc_path = new bhl.IncludePath();
    inc_path.Add(GetTestDirPath());
    return inc_path;
  }
  
  static string GetTestDirPath()
  {
    string self_bin = System.Reflection.Assembly.GetExecutingAssembly().Location;
    return Path.GetDirectoryName(self_bin) + "/tmp/bhlsp";
  }
  
  static bhl.lsp.proto.Uri MakeTestDocument(string path, string text, List<string> files = null)
  {
    string full_path = bhl.BuildUtils.NormalizeFilePath(GetTestDirPath() + "/" + path);
    Directory.CreateDirectory(Path.GetDirectoryName(full_path));
    File.WriteAllText(full_path, text);
    if(files != null)
      files.Add(full_path);
    var uri = new bhl.lsp.proto.Uri(full_path);
    return uri;
  }

  static bhl.SourcePos Pos(string code, string needle)
  {
    int idx = code.IndexOf(needle);
    if(idx == -1)
      throw new Exception("Needle not found: " + needle);
    var indexer = new CodeIndex();
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

  static Logger NoLogger()
  {
    return new Logger(0, new NoLogger());
  }

  static string NullResultJson(int id)
  {
    return "{\"id\":" + id + ",\"jsonrpc\":\"2.0\",\"result\":null}";
  }

  public class DummyConnection : IConnection
  {
    public string Read()
    {
      return null;
    }

    public void Write(string json)
    {
    }
  }

  static IConnection NoConnection()
  {
    return new DummyConnection();
  }
}
