using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace bhl.lsp.proto {

public class LifecycleService : IService
{
  Workspace workspace;
  IPublisher publisher;

  public int? process_id { get; private set; }

  public ServerCapabilities capabilities { get; private set; } = new ServerCapabilities();
  
  public LifecycleService(Workspace workspace, IPublisher publisher)
  {
    this.workspace = workspace;
    this.publisher = publisher;

    workspace.OnDiagnostics += PublishDiagnostics;
  }

  void PublishDiagnostics(Dictionary<string, CompileErrors> uri2errs)
  {
    foreach(var kv in uri2errs)
    {
      var dparams = new PublishDiagnosticParams() {
        uri = new proto.Uri(kv.Key),
        diagnostics = new Diagnostic[0]
      };

      var notification = new Notification() {
        method = "textDocument/publishDiagnostics",
        @params = dparams
      };

      publisher.Publish(notification);
    }
  }

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

    workspace.logger.Log(1, "Initializing workspace from project file '" + proj.proj_file + "'");

    proj.LoadBindings().Register(ts);
    
    workspace.Init(ts, proj.inc_path);

    //TODO: run it in background
    workspace.IndexFiles();

    var capabilities = new ServerCapabilities();

    if(args.capabilities.textDocument != null)
    {
      if(args.capabilities.textDocument.synchronization != null)
      {
        capabilities.textDocumentSync = new TextDocumentSyncOptions
        {
          openClose = true, //open and close notifications are sent to server
          change = proto.TextDocumentSyncKind.Full,
          save = false //didSave
        };
      }
      
      //TODO:
      //if(args.capabilities.textDocument.signatureHelp != null)
      //{
      //  capabilities.signatureHelpProvider = new SignatureHelpOptions { triggerCharacters = new[] {"(", ","} };
      //}

      if(args.capabilities.textDocument.hover != null)
      {
        capabilities.hoverProvider = true; //textDocument/hover
      }

      if(args.capabilities.textDocument.declaration != null)
      {
        capabilities.declarationProvider = false; //textDocument/declaration
      }

      if(args.capabilities.textDocument.definition != null)
      {
        capabilities.definitionProvider = true; //textDocument/definition
      }

      if(args.capabilities.textDocument.typeDefinition != null)
      {
        capabilities.typeDefinitionProvider = false; //textDocument/typeDefinition
      }
      
      if(args.capabilities.textDocument.implementation != null)
      {
        capabilities.implementationProvider = false; //textDocument/implementation
      }

      if(args.capabilities.textDocument.references != null)
      {
        capabilities.referencesProvider = true; //textDocument/references
      }

      if(args.capabilities.textDocument.semanticTokens != null)
      {
        capabilities.semanticTokensProvider = new SemanticTokensOptions
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

      if(args.capabilities.textDocument.publishDiagnostics != null)
      {
        capabilities.diagnosticProvider = new DiagnosticOptions
        {
          interFileDependencies = true,
          workspaceDiagnostics = true
        };
      }
    }

    this.capabilities = capabilities;
    
    return new RpcResult(new InitializeResult
    {
      capabilities = capabilities,
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

public class TextDocumentSynchronizationService : IService
{
  Workspace workspace;

  public TextDocumentSynchronizationService(Workspace workspace)
  {
    this.workspace = workspace;
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

  public TextDocumentSignatureHelpService(Workspace workspace)
  {
    this.workspace = workspace;
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

  public TextDocumentGoToService(Workspace workspace)
  {
    this.workspace = workspace;
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
        var range = (Range)symb.origin.source_range;
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

  public TextDocumentHoverService(Workspace workspace)
  {
    this.workspace = workspace;
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

  public TextDocumentSemanticTokensService(Workspace workspace)
  {
    this.workspace = workspace;
  }

  [RpcMethod("textDocument/semanticTokens/full")]
  public RpcResult SemanticTokensFull(SemanticTokensParams args)
  {
    var document = workspace.GetOrLoadDocument(args.textDocument.uri);

    if(document != null)
    {
      return new RpcResult(new SemanticTokens
      {
        data = document.proc.GetEncodedSemanticTokens().ToArray()
      });
    }

    return new RpcResult(null);
  }
}

public class TextDocumentFindReferencesService : IService
{
  Workspace workspace;

  public TextDocumentFindReferencesService(Workspace workspace)
  {
    this.workspace = workspace;
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
              var range = (Range)an_kv.Value.range;
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
            range = (Range)symb.origin.source_range
          });
        }
      }
    }

    return new RpcResult(refs);
  }
}

}
