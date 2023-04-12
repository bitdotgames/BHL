using System;
using System.Text.RegularExpressions;
using Antlr4.Runtime;

namespace bhl.lsp.spec {

public class LifecycleService : IService
{
  Logger logger;
  Workspace workspace;

  int? process_id;
  
  public LifecycleService(Logger logger, Workspace workspace)
  {
    this.logger = logger;
    this.workspace = workspace;
  }

  [RpcMethod("initialize")]
  public RpcResult Initialize(InitializeParams args)
  {
    process_id = args.processId;
    
    var ts = new Types();
    var inc_path = new IncludePath();

    if(args.workspaceFolders != null)
    {
      for(int i = 0; i < args.workspaceFolders.Length; i++)
        inc_path.Add(args.workspaceFolders[i].uri.LocalPath);
    }
    else if(args.rootUri != null) // @deprecated in favour of `workspaceFolders`
    {
      inc_path.Add(args.rootUri.LocalPath);
    }
    else if(!string.IsNullOrEmpty(args.rootPath)) // @deprecated in favour of `rootUri`.
    {
      inc_path.Add(args.rootPath);
    }
    
    workspace.Init(ts, inc_path);

    //TODO: run it in background
    workspace.IndexFiles();
    
    var capabilities = new ServerCapabilities();

    if(args.capabilities.textDocument != null)
    {
      if(args.capabilities.textDocument.synchronization != null)
      {
        capabilities.textDocumentSync = new TextDocumentSyncOptions
        {
          openClose = true, //didOpen, didClose
          change = workspace.syncKind, //didChange
          save = false //didSave
        };
      }
      
      if(args.capabilities.textDocument.signatureHelp != null)
      {
        capabilities.signatureHelpProvider = new SignatureHelpOptions { triggerCharacters = new[] {"(", ","} };
      }

      if(args.capabilities.textDocument.declaration != null)
      {
        if(args.capabilities.textDocument.declaration.linkSupport != null)
          workspace.declarationLinkSupport = (bool)args.capabilities.textDocument.declaration.linkSupport;

        capabilities.declarationProvider = false; //textDocument/declaration
      }

      if(args.capabilities.textDocument.definition != null)
      {
        if(args.capabilities.textDocument.definition.linkSupport != null)
          workspace.definitionLinkSupport = (bool)args.capabilities.textDocument.definition.linkSupport;

        capabilities.definitionProvider = true; //textDocument/definition
      }

      if(args.capabilities.textDocument.typeDefinition != null)
      {
        if(args.capabilities.textDocument.typeDefinition.linkSupport != null)
          workspace.typeDefinitionLinkSupport = (bool)args.capabilities.textDocument.typeDefinition.linkSupport;

        capabilities.typeDefinitionProvider = false; //textDocument/typeDefinition
      }
      
      if(args.capabilities.textDocument.implementation != null)
      {
        if(args.capabilities.textDocument.implementation.linkSupport != null)
          workspace.implementationLinkSupport = (bool)args.capabilities.textDocument.implementation.linkSupport;

        capabilities.implementationProvider = false; //textDocument/implementation
      }

      if(args.capabilities.textDocument.hover != null)
      {
        capabilities.hoverProvider = true; //textDocument/hover
      }
      
      if(args.capabilities.textDocument.references != null)
      {
        capabilities.referencesProvider = false; //textDocument/references
      }

      if(args.capabilities.textDocument.semanticTokens != null)
      {
        capabilities.semanticTokensProvider = new SemanticTokensOptions
        {
          full = true,
          range = false,
          legend = new SemanticTokensLegend
          {
            tokenTypes = BHLSemanticTokens.token_types,
            tokenModifiers = BHLSemanticTokens.modifiers
          }
        };
      }
    }
    
    return RpcResult.Success(new InitializeResult
    {
      capabilities = capabilities,
      serverInfo = new InitializeResult.InitializeResultsServerInfo
      {
        name = "bhlsp",
        version = "0.1"
      }
    });
  }

  [RpcMethod("initialized")]
  public RpcResult Initialized()
  {
    return RpcResult.Success();
  }

  [RpcMethod("shutdown")]
  public RpcResult Shutdown()
  {
    workspace.Shutdown();

    return RpcResult.Success();
  }

  [RpcMethod("exit")]
  public RpcResult Exit()
  {
    logger.Log(1, "Stopping BHL LSP server...");

    if(process_id != null)
    {
      //TODO: in this case success response won't be sent
      Environment.Exit(0);
    }
    
    return RpcResult.Success();
  }
}

public class TextDocumentSynchronizationService : IService
{
  Logger logger;
  Workspace workspace;

  public TextDocumentSynchronizationService(Logger logger, Workspace workspace)
  {
    this.logger = logger;
    this.workspace = workspace;
  }

  [RpcMethod("textDocument/didOpen")]
  public RpcResult DidOpenTextDocument(DidOpenTextDocumentParams args)
  {
    workspace.OpenDocument(args.textDocument.uri, args.textDocument.text);

    return RpcResult.Success();
  }
  
  [RpcMethod("textDocument/didChange")]
  public RpcResult DidChangeTextDocument(DidChangeTextDocumentParams args)
  {
    if(workspace.syncKind != TextDocumentSyncKind.Full)
    {
      return RpcResult.Error(new ResponseError
      {
        code = (int)ErrorCodes.RequestFailed,
        message = "Not supported"
      });
    }

    foreach(var changes in args.contentChanges)
    {
      if(!workspace.UpdateDocument(args.textDocument.uri, changes.text))
        return RpcResult.Error(new ResponseError
        {
          code = (int)ErrorCodes.RequestFailed,
          message = "Update failed"
        });
    }
    
    return RpcResult.Success();
  }

  [RpcMethod("textDocument/didClose")]
  public RpcResult DidCloseTextDocument(DidCloseTextDocumentParams args)
  {
    return RpcResult.Success();
  }
  
  [RpcMethod("textDocument/willSave")]
  public RpcResult WillSaveTextDocument(WillSaveTextDocumentParams args)
  {
    return RpcResult.Error(new ResponseError
    {
      code = (int)ErrorCodes.RequestFailed,
      message = "Not supported"
    });
  }

  [RpcMethod("textDocument/willSaveWaitUntil")]
  public RpcResult WillSaveWaitUntilTextDocument(WillSaveTextDocumentParams args)
  {
    return RpcResult.Error(new ResponseError
    {
      code = (int)ErrorCodes.RequestFailed,
      message = "Not supported"
    });
  }

  [RpcMethod("textDocument/didSave")]
  public RpcResult DidSaveTextDocument(DidSaveTextDocumentParams args)
  {
    return RpcResult.Error(new ResponseError
    {
      code = (int)ErrorCodes.RequestFailed,
      message = "Not supported"
    });
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
      var node = document.FindTerminalNode(args.position);
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

          return RpcResult.Success(new SignatureHelp() {
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
    
    return RpcResult.Success();
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
      var symb = document.FindSymbol(args.position);

      if(symb != null)
      {
        var range = (Range)symb.parsed.range;
        range.DecrementLine();
        return RpcResult.Success(new Location
        {
          uri = Util.MakeUri(symb.parsed.file),
          range = range
        });
      }
    }

    return RpcResult.Success(new Location());
  }
  
  /**
   * The result type LocationLink[] got introduced with version 3.14.0
   * and depends on the corresponding client capability textDocument.declaration.linkSupport.
   */
  [RpcMethod("textDocument/declaration")]
  public RpcResult GotoDeclaration(DeclarationParams args)
  {
    return RpcResult.Error(new ResponseError
    {
      code = (int)ErrorCodes.RequestFailed,
      message = "Not supported"
    });
  }
  
  /**
   * The result type LocationLink[] got introduced with version 3.14.0
   * and depends on the corresponding client capability textDocument.typeDefinition.linkSupport.
   */
  [RpcMethod("textDocument/typeDefinition")]
  public RpcResult GotoTypeDefinition(TypeDefinitionParams args)
  {
    return RpcResult.Error(new ResponseError
    {
      code = (int)ErrorCodes.RequestFailed,
      message = "Not supported"
    });
  }
  
  /**
   * The result type LocationLink[] got introduced with version 3.14.0
   * and depends on the corresponding client capability textDocument.implementation.linkSupport.
   */
  [RpcMethod("textDocument/implementation")]
  public RpcResult GotoImplementation(ImplementationParams args)
  {
    return RpcResult.Error(new ResponseError
    {
      code = (int)ErrorCodes.RequestFailed,
      message = "Not supported"
    });
  }
}

public class TextDocumentHoverService : IService
{
  //Workspace workspace;

  public TextDocumentHoverService(Workspace workspace)
  {
    //this.workspace = workspace;
  }

  [RpcMethod("textDocument/hover")]
  public RpcResult Hover(TextDocumentPositionParams args)
  {
//    var document = workspace.GetOrLoadDocument(args.textDocument.uri);
//
//    var node = document.FindParserRule(args.position);
//
//    var callExp = node as bhlParser.CallExpContext;
//    
//    bhlParser.FuncDeclContext funcDecl = null;
//    
//    if(callExp != null)
//    {
//      string callExpName = callExp.NAME().GetText();
//      
//      foreach(var doc in workspace.ForEachBhlImports(document))
//      {
//        if(doc.FuncDecls.ContainsKey(callExpName))
//        {
//          funcDecl = doc.FuncDecls[callExpName];
//          break;
//        }
//      }
//    }
//    
//    if(funcDecl != null)
//    {
//      //TODO: all this information is available for symbols
//      string label = funcDecl.NAME().GetText()+"(";
//  
//      var funcParameters = ParserAnalyzer.GetInfoParams(funcDecl);
//  
//      if(funcParameters.Count > 0)
//      {
//        for(int k = 0; k < funcParameters.Count; k++)
//        {
//          var funcParameter = funcParameters[k];
//          label += funcParameter.label.Value;
//          if(k != funcParameters.Count - 1)
//            label += ", ";
//        }
//      }
//      else
//        label += "<no parameters>";
//
//      label += ")";
//
//      if(funcDecl.retType() is bhlParser.RetTypeContext retType)
//      {
//        label += ":";
//
//        var types = retType.type();
//        for (int n = 0; n < types.Length; n++)
//        {
//          var t = types[n];
//          if(t.exception != null)
//            continue;
//
//          label += t.nsName().GetText() + " ";
//        }
//      }
//      else
//        label += ":void";
//      
//      return RpcResult.Success(new Hover
//      {
//        contents = new MarkupContent
//        {
//          kind = "plaintext",
//          value = label
//        }
//      });
//    }
    
    return RpcResult.Success();
  }
}

public class TextDocumentSemanticTokensService : IService
{
  //Workspace workspace;

  public TextDocumentSemanticTokensService(Workspace workspace)
  {
    //this.workspace = workspace;
  }

  [RpcMethod("textDocument/semanticTokens/full")]
  public RpcResult SemanticTokensFull(SemanticTokensParams args)
  {
    //var document = workspace.GetOrLoadDocument(args.textDocument.uri);

    //return RpcResult.Success(new SemanticTokens
    //{
    //  data = document.DataSemanticTokens.ToArray()
    //});

    return RpcResult.Success();
  }
}

//public class WorkspaceService : IService
//{
//  [RpcMethod("workspace/didChangeWorkspaceFolders")]
//  public RpcResult DidChangeWorkspaceFolders(DidChangeWorkspaceFoldersParams args);
//
//  [RpcMethod("workspace/didChangeConfiguration")]
//  public RpcResult DidChangeConfiguration(DidChangeConfigurationParams args);
//
//  [RpcMethod("workspace/didChangeWatchedFiles")]
//  public RpcResult DidChangeWatchedFiles(DidChangeWatchedFilesParams args);
//
//  [RpcMethod("workspace/symbol")]
//  public RpcResult Symbol(WorkspaceSymbolParams args);
//
//  [RpcMethod("workspace/executeCommand")]
//  public RpcResult ExecuteCommand(ExecuteCommandParams args);
//}
//  
//public class TextDocumentFindReferencesServiceProto : IService
//{
//  [RpcMethod("textDocument/references")]
//  public RpcResult FindReferences(ReferenceParams args);
//}
//
//public class TextDocumentServiceProto : IService
//{
//  [RpcMethod("textDocument/completion")]
//  public RpcResult Completion(CompletionParams args);
//
//  [RpcMethod("completionItem/resolve")]
//  public RpcResult ResolveCompletionItem(CompletionItem args);
//  
//  [RpcMethod("textDocument/documentHighlight")]
//  public RpcResult DocumentHighlight(TextDocumentPositionParams args);
//
//  [RpcMethod("textDocument/documentSymbol")]
//  public RpcResult DocumentSymbols(DocumentSymbolParams args);
//
//  [RpcMethod("textDocument/documentColor")]
//  public RpcResult DocumentColor(DocumentColorParams args);
//
//  [RpcMethod("textDocument/colorPresentation")]
//  public RpcResult ColorPresentation(ColorPresentationParams args);
//
//  [RpcMethod("textDocument/formatting")]
//  public RpcResult DocumentFormatting(DocumentFormattingParams args);
//
//  [RpcMethod("textDocument/rangeFormatting")]
//  public RpcResult DocumentRangeFormatting(DocumentRangeFormattingParams args);
//
//  [RpcMethod("textDocument/onTypeFormatting")]
//  public RpcResult DocumentOnTypeFormatting(DocumentOnTypeFormattingParams args);
//  
//  [RpcMethod("textDocument/codeAction")]
//  public RpcResult CodeAction(CodeActionParams args);
//
//  [RpcMethod("textDocument/codeLens")]
//  public RpcResult CodeLens(CodeLensParams args);
//
//  [RpcMethod("codeLens/resolve")]
//  public RpcResult ResolveCodeLens(CodeLens args);
//
//  [RpcMethod("textDocument/documentLink")]
//  public RpcResult DocumentLink(DocumentLinkParams args);
//
//  [RpcMethod("documentLink/resolve")]
//  public RpcResult ResolveDocumentLink(DocumentLink args);
//
//  [RpcMethod("textDocument/rename")]
//  public RpcResult Rename(RenameParams args);
//
//  [RpcMethod("textDocument/foldingRange")]
//  public RpcResult FoldingRange(FoldingRangeParams args);
//}

}
