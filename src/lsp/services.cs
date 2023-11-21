using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using bhl.lsp.proto;

namespace bhl.lsp {

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
  {}

  [RpcMethod("initialize")]
  public RpcResult Initialize(InitializeParams args)
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

    //TODO: run it in background
    workspace.IndexFiles();

    var server_capabilities = srv.capabilities;
    foreach(var service in srv.services)
      service.GetCapabilities(args.capabilities, ref server_capabilities);
    
    return new RpcResult(new InitializeResult
    {
      capabilities = server_capabilities,
      serverInfo = new InitializeResult.InitializeResultsServerInfo
      {
        name = "bhlsp",
        version = Version.Name
      }
    });
  }

  [RpcMethod("initialized")]
  public RpcResult Initialized()
  {
    return null;
  }

  [RpcMethod("shutdown")]
  public RpcResult Shutdown()
  {
    workspace.Shutdown();

    return new RpcResult(null);
  }

  [RpcMethod("exit")]
  public RpcResult Exit()
  {
    return RpcResult.Error(ErrorCodes.Exit);
  }
}

public class DiagnosticService : IService
{
  Server srv;

  Dictionary<string, CompileErrors> uri2errs = new Dictionary<string, CompileErrors>();
  
  public DiagnosticService(Server srv)
  {
    this.srv = srv;

    srv.workspace.OnDiagnostics += PublishDiagnostics;
  }

  public void GetCapabilities(ClientCapabilities cc, ref ServerCapabilities sc)
  {
    if(cc.textDocument?.publishDiagnostics != null)
    {
      sc.diagnosticProvider = new DiagnosticOptions
      {
        interFileDependencies = true,
        workspaceDiagnostics = true
      };
    }
  }

  void PublishDiagnostics(Dictionary<string, CompileErrors> new_uri2errs)
  {
    foreach(var kv in new_uri2errs)
    {
      if(!uri2errs.TryGetValue(kv.Key, out var errs) || errs.Count != kv.Value.Count)
      {
        var dparams = new PublishDiagnosticParams() {
          uri = new proto.Uri(kv.Key),
          diagnostics = new List<Diagnostic>()
        };

        foreach(var err in kv.Value)
        {
          var dg = new Diagnostic();
          dg.severity = DiagnosticSeverity.Error;
          dg.range = err.range;
          //NOTE: let's force the error to be 'one-line'
          if(dg.range.start.line != dg.range.end.line)
          {
            dg.range.end.line = dg.range.start.line+1;
            dg.range.end.character = 0;
          }
          dg.message = err.text;

          if(dparams.diagnostics.Count > 0)
          {
            var prev_dg = dparams.diagnostics[dparams.diagnostics.Count-1];
            //NOTE: let's skip diagnostics which starts on the same line
            //      just like the previous one
            if(prev_dg.range.start.line == dg.range.start.line)
              continue;
          }
          
          dparams.diagnostics.Add(dg);
        }

        var notification = new Notification() {
          method = "textDocument/publishDiagnostics",
          @params = dparams
        };

        srv.Publish(notification);
      }
    }

    uri2errs = new_uri2errs;
  }
}

public class TextDocumentSynchronizationService : IService
{
  Workspace workspace;

  public TextDocumentSynchronizationService(Server srv)
  {
    this.workspace = srv.workspace;
  }
  
  public void GetCapabilities(ClientCapabilities cc, ref ServerCapabilities sc)
  {
    if(cc.textDocument?.synchronization != null)
    {
      sc.textDocumentSync = new TextDocumentSyncOptions
      {
        openClose = true, //open and close notifications are sent to server
        change = proto.TextDocumentSyncKind.Full,
        save = false //didSave
      };
    }
  }

  [RpcMethod("textDocument/didOpen")]
  public RpcResult DidOpenTextDocument(DidOpenTextDocumentParams args)
  {
    workspace.OpenDocument(args.textDocument.uri, args.textDocument.text);

    return null;
  }
  
  [RpcMethod("textDocument/didChange")]
  public RpcResult DidChangeTextDocument(DidChangeTextDocumentParams args)
  {
    foreach(var changes in args.contentChanges)
    {
      if(!workspace.UpdateDocument(args.textDocument.uri, changes.text))
        return RpcResult.Error(new ResponseError
        {
          code = (int)ErrorCodes.RequestFailed,
          message = "Update failed"
        });
    }
    
    return null;
  }

  [RpcMethod("textDocument/didClose")]
  public RpcResult DidCloseTextDocument(DidCloseTextDocumentParams args)
  {
    return null;
  }
  
  [RpcMethod("textDocument/willSave")]
  public RpcResult WillSaveTextDocument(WillSaveTextDocumentParams args)
  {
    return RpcResult.Error(
      ErrorCodes.RequestFailed,
      "Not supported"
    );
  }

  [RpcMethod("textDocument/willSaveWaitUntil")]
  public RpcResult WillSaveWaitUntilTextDocument(WillSaveTextDocumentParams args)
  {
    return RpcResult.Error(
      ErrorCodes.RequestFailed,
      "Not supported"
    );
  }

  [RpcMethod("textDocument/didSave")]
  public RpcResult DidSaveTextDocument(DidSaveTextDocumentParams args)
  {
    return RpcResult.Error(
      ErrorCodes.RequestFailed,
      "Not supported"
    );
  }
}

public class TextDocumentSignatureHelpService : IService
{
  Workspace workspace;

  public TextDocumentSignatureHelpService(Server srv)
  {
    this.workspace = srv.workspace;
  }

  public void GetCapabilities(ClientCapabilities cc, ref ServerCapabilities sc)
  {
    //TODO:
    //if(cc.textDocument?.signatureHelp != null)
    //{
    //  sc.signatureHelpProvider = new SignatureHelpOptions { triggerCharacters = new[] {"(", ","} };
    //}
  }

  [RpcMethod("textDocument/signatureHelp")]
  public RpcResult SignatureHelp(SignatureHelpParams args)
  {
    var document = workspace.GetOrLoadDocument(args.textDocument.uri);

    if(document != null)
    {
      var node = document.FindTerminalNode((int)args.position.line, (int)args.position.character);
      if(node != null)
      {
        //Console.WriteLine("NODE " + node.GetType().Name + " " + node.GetText() + 
        //  ", parent " + node.Parent.GetType().Name + " " + node.Parent.GetText() + 
        //  " parent parent " + node.Parent.Parent.GetType().Name
        //);
        var symb = document.FindFuncByCallStatement(node);
        if(symb != null)
        {
          var ps = GetSignatureParams(symb);

          return new RpcResult(new SignatureHelp() {
              signatures = new SignatureInformation[] {
                new SignatureInformation() {
                  label = symb.ToString(),
                  parameters = ps,
                  activeParameter = 0
                }
              },
              activeSignature = 0,
              activeParameter = 0
            }
          );
        }
      }
    }
    
    return new RpcResult(null);
  }

  static ParameterInformation[] GetSignatureParams(FuncSymbol symb)
  {
    var en = symb.GetSymbolsIterator();

    var ps = new ParameterInformation[en.Count];

    for(int i=0;i<en.Count;++i)
    {
      ps[i] = new ParameterInformation() {
        label = (en[i] as VariableSymbol).type.Get() + " " + en[i],
        documentation = ""
      };
    }

    return ps;
  }
}

public class TextDocumentGoToService : IService
{
  Workspace workspace;

  public TextDocumentGoToService(Server srv)
  {
    this.workspace = srv.workspace;
  }

  public void GetCapabilities(ClientCapabilities cc, ref ServerCapabilities sc)
  {
    if(cc.textDocument?.definition != null)
      sc.definitionProvider = true; //textDocument/definition

    if(cc.textDocument?.declaration != null)
      sc.declarationProvider = false; //textDocument/declaration

    if(cc.textDocument?.typeDefinition != null)
      sc.typeDefinitionProvider = false; //textDocument/typeDefinition
    
    if(cc.textDocument?.implementation != null)
      sc.implementationProvider = false; //textDocument/implementation
  }

  /**
   * The result type LocationLink[] got introduced with version 3.14.0
   * and depends on the corresponding client capability textDocument.definition.linkSupport.
   */
  [RpcMethod("textDocument/definition")]
  public RpcResult GotoDefinition(DefinitionParams args)
  {
    var document = workspace.GetOrLoadDocument(args.textDocument.uri);
        
    if(document != null)
    {
      var symb = document.FindSymbol((int)args.position.line, (int)args.position.character);

      if(symb != null)
      {
        var range = (bhl.lsp.proto.Range)symb.origin.source_range;
        return new RpcResult(new Location
        {
          uri = new proto.Uri(symb.origin.source_file),
          range = range
        });
      }
    }

    return new RpcResult(new Location());
  }
  
  /**
   * The result type LocationLink[] got introduced with version 3.14.0
   * and depends on the corresponding client capability textDocument.declaration.linkSupport.
   */
  [RpcMethod("textDocument/declaration")]
  public RpcResult GotoDeclaration(DeclarationParams args)
  {
    return RpcResult.Error(
      ErrorCodes.RequestFailed,
      "Not supported"
    );
  }
  
  /**
   * The result type LocationLink[] got introduced with version 3.14.0
   * and depends on the corresponding client capability textDocument.typeDefinition.linkSupport.
   */
  [RpcMethod("textDocument/typeDefinition")]
  public RpcResult GotoTypeDefinition(TypeDefinitionParams args)
  {
    return RpcResult.Error(
      ErrorCodes.RequestFailed,
      "Not supported"
    );
  }
  
  /**
   * The result type LocationLink[] got introduced with version 3.14.0
   * and depends on the corresponding client capability textDocument.implementation.linkSupport.
   */
  [RpcMethod("textDocument/implementation")]
  public RpcResult GotoImplementation(ImplementationParams args)
  {
    return RpcResult.Error(
      ErrorCodes.RequestFailed,
      "Not supported"
    );
  }
}

public class TextDocumentHoverService : IService
{
  Workspace workspace;

  public TextDocumentHoverService(Server srv)
  {
    this.workspace = srv.workspace;
  }

  public void GetCapabilities(ClientCapabilities cc, ref ServerCapabilities sc)
  {
    if(cc.textDocument?.hover != null)
      sc.hoverProvider = true; //textDocument/hover
  }

  [RpcMethod("textDocument/hover")]
  public RpcResult Hover(TextDocumentPositionParams args)
  {
    var document = workspace.GetOrLoadDocument(args.textDocument.uri);

    Symbol symb = null;
    if(document != null)
      symb = document.FindSymbol((int)args.position.line, (int)args.position.character);

    if(symb != null)
    {
      return new RpcResult(new Hover
      {
        contents = new MarkupContent
        {
          kind = "plaintext",
          value = symb.ToString()
        }
      });
    }
    
    return new RpcResult(null);
  }
}

public class TextDocumentSemanticTokensService : IService
{
  Workspace workspace;

  public TextDocumentSemanticTokensService(Server srv)
  {
    this.workspace = srv.workspace;
  }
  
  public void GetCapabilities(ClientCapabilities cc, ref ServerCapabilities sc)
  {
    if(cc.textDocument?.semanticTokens != null)
    {
      sc.semanticTokensProvider = new SemanticTokensOptions
      {
        full = true,
        range = false,
        legend = new SemanticTokensLegend
        {
          tokenTypes = bhl.ANTLR_Processor.SemanticTokens.token_types,
          tokenModifiers = bhl.ANTLR_Processor.SemanticTokens.modifiers
        }
      };
    }
  }

  [RpcMethod("textDocument/semanticTokens/full")]
  public RpcResult SemanticTokensFull(SemanticTokensParams args)
  {
    var document = workspace.GetOrLoadDocument(args.textDocument.uri);

    if(document?.proc != null)
    {
      return new RpcResult(new SemanticTokens
      {
        data = document.proc.GetEncodedSemanticTokens().ToArray()
      });
    }
    else
      return new RpcResult(null);
  }
}

public class TextDocumentFindReferencesService : IService
{
  Workspace workspace;

  public TextDocumentFindReferencesService(Server srv)
  {
    this.workspace = srv.workspace;
  }

  public void GetCapabilities(ClientCapabilities cc, ref ServerCapabilities sc)
  {
    if(cc.textDocument?.references != null)
      sc.referencesProvider = true; //textDocument/references
  }

  /**
   * The result type Location[] | null
   */
  [RpcMethod("textDocument/references")]
  public RpcResult FindReferences(ReferenceParams args)
  {
    var document = workspace.GetOrLoadDocument(args.textDocument.uri);

    var refs = new List<Location>();

    if(document != null)
    {
      var symb = document.FindSymbol((int)args.position.line, (int)args.position.character);
      if(symb != null)
      {
        //1. adding all found references
        foreach(var kv in workspace.uri2doc)
        {
          foreach(var an_kv in kv.Value.proc.annotated_nodes)
          {
            if(an_kv.Value.lsp_symbol == symb)
            {
              var range = (bhl.lsp.proto.Range)an_kv.Value.range;
              var loc = new Location {
                uri = new proto.Uri(kv.Key),
                range = range
              };
              refs.Add(loc);
            }
          }
        }

        //2. sorting by file name
        refs.Sort(
          (a, b) => {
            if(a.uri.path == b.uri.path)
              return a.range.start.line.CompareTo(b.range.start.line);
            else
              return a.uri.path.CompareTo(b.uri.path);
          }
        );

        //3. adding definition for native symbol (if any)
        if(symb is FuncSymbolNative)
        {
          refs.Add(new Location {
            uri = new proto.Uri(symb.origin.source_file),
            range = (bhl.lsp.proto.Range)symb.origin.source_range
          });
        }
      }
    }

    return new RpcResult(refs);
  }
}

}
