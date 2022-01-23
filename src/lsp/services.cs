using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Dfa;
using Antlr4.Runtime.Sharpen;
using Antlr4.Runtime.Tree;
using System.Text.RegularExpressions;
using bhl;

namespace bhlsp
{
#region -- ABSTRACT --
  public abstract class BHLSPJsonRpcService
  {
  }
  
  public abstract class BHLSPGeneralJsonRpcServiceTemplate : BHLSPJsonRpcService
  {
    [JsonRpcMethod("initialize")]
    public abstract RpcResult Initialize(InitializeParams args);

    [JsonRpcMethod("initialized")]
    public abstract RpcResult Initialized();

    [JsonRpcMethod("shutdown")]
    public abstract RpcResult Shutdown();

    [JsonRpcMethod("exit")]
    public abstract RpcResult Exit();
  }

  public abstract class BHLSPTextDocumentSynchronizationJsonRpcServiceTemplate : BHLSPJsonRpcService
  {
    [JsonRpcMethod("textDocument/didOpen")]
    public abstract RpcResult DidOpenTextDocument(DidOpenTextDocumentParams args);
    
    [JsonRpcMethod("textDocument/didChange")]
    public abstract RpcResult DidChangeTextDocument(DidChangeTextDocumentParams args);
    
    [JsonRpcMethod("textDocument/willSave")]
    public abstract RpcResult WillSaveTextDocument(WillSaveTextDocumentParams args);
    
    [JsonRpcMethod("textDocument/willSaveWaitUntil")]
    public abstract RpcResult WillSaveWaitUntilTextDocument(WillSaveTextDocumentParams args);
    
    [JsonRpcMethod("textDocument/didSave")]
    public abstract RpcResult DidSaveTextDocument(DidSaveTextDocumentParams args);
    
    [JsonRpcMethod("textDocument/didClose")]
    public abstract RpcResult DidCloseTextDocument(DidCloseTextDocumentParams args);
  }

  public abstract class BHLSPTextDocumentSignatureHelpJsonRpcServiceTemplate : BHLSPJsonRpcService
  {
    [JsonRpcMethod("textDocument/signatureHelp")]
    public abstract RpcResult SignatureHelp(SignatureHelpParams args);
  }

  public abstract class BHLSPTextDocumentGoToJsonRpcServiceTemplate : BHLSPJsonRpcService
  {
    [JsonRpcMethod("textDocument/declaration")]
    public abstract RpcResult GotoDeclaration(DeclarationParams args);
    
    [JsonRpcMethod("textDocument/definition")]
    public abstract RpcResult GotoDefinition(DefinitionParams args);

    [JsonRpcMethod("textDocument/typeDefinition")]
    public abstract RpcResult GotoTypeDefinition(TypeDefinitionParams args);

    [JsonRpcMethod("textDocument/implementation")]
    public abstract RpcResult GotoImplementation(ImplementationParams args);
  }
  
  public abstract class BHLSPTextDocumentJsonRpcServiceTemplate : BHLSPJsonRpcService
  {
    [JsonRpcMethod("textDocument/completion")]
    public abstract RpcResult Completion(CompletionParams args);

    [JsonRpcMethod("completionItem/resolve")]
    public abstract RpcResult ResolveCompletionItem(CompletionItem args);

    [JsonRpcMethod("textDocument/hover")]
    public abstract RpcResult Hover(TextDocumentPositionParams args);
    
    [JsonRpcMethod("textDocument/references")]
    public abstract RpcResult FindReferences(ReferenceParams args);

    [JsonRpcMethod("textDocument/documentHighlight")]
    public abstract RpcResult DocumentHighlight(TextDocumentPositionParams args);

    [JsonRpcMethod("textDocument/documentSymbol")]
    public abstract RpcResult DocumentSymbols(DocumentSymbolParams args);

    [JsonRpcMethod("textDocument/documentColor")]
    public abstract RpcResult DocumentColor(DocumentColorParams args);

    [JsonRpcMethod("textDocument/colorPresentation")]
    public abstract RpcResult ColorPresentation(ColorPresentationParams args);

    [JsonRpcMethod("textDocument/formatting")]
    public abstract RpcResult DocumentFormatting(DocumentFormattingParams args);

    [JsonRpcMethod("textDocument/rangeFormatting")]
    public abstract RpcResult DocumentRangeFormatting(DocumentRangeFormattingParams args);

    [JsonRpcMethod("textDocument/onTypeFormatting")]
    public abstract RpcResult DocumentOnTypeFormatting(DocumentOnTypeFormattingParams args);
    
    [JsonRpcMethod("textDocument/codeAction")]
    public abstract RpcResult CodeAction(CodeActionParams args);

    [JsonRpcMethod("textDocument/codeLens")]
    public abstract RpcResult CodeLens(CodeLensParams args);

    [JsonRpcMethod("codeLens/resolve")]
    public abstract RpcResult ResolveCodeLens(CodeLens args);

    [JsonRpcMethod("textDocument/documentLink")]
    public abstract RpcResult DocumentLink(DocumentLinkParams args);

    [JsonRpcMethod("documentLink/resolve")]
    public abstract RpcResult ResolveDocumentLink(DocumentLink args);

    [JsonRpcMethod("textDocument/rename")]
    public abstract RpcResult Rename(RenameParams args);

    [JsonRpcMethod("textDocument/foldingRange")]
    public abstract RpcResult FoldingRange(FoldingRangeParams args);
  }

  public abstract class BHLSPWorkspaceJsonRpcServiceTemplate : BHLSPJsonRpcService
  {
    [JsonRpcMethod("workspace/didChangeWorkspaceFolders")]
    public abstract RpcResult DidChangeWorkspaceFolders(DidChangeWorkspaceFoldersParams args);

    [JsonRpcMethod("workspace/didChangeConfiguration")]
    public abstract RpcResult DidChangeConfiguration(DidChangeConfigurationParams args);

    [JsonRpcMethod("workspace/didChangeWatchedFiles")]
    public abstract RpcResult DidChangeWatchedFiles(DidChangeWatchedFilesParams args);

    [JsonRpcMethod("workspace/symbol")]
    public abstract RpcResult Symbol(WorkspaceSymbolParams args);

    [JsonRpcMethod("workspace/executeCommand")]
    public abstract RpcResult ExecuteCommand(ExecuteCommandParams args);
  }
#endregion
  
  public class BHLSPGeneralJsonRpcService : BHLSPGeneralJsonRpcServiceTemplate
  {
    private int? processId;
    
    public override RpcResult Initialize(InitializeParams args)
    {
      processId = args.processId;
      
      //workspaceFolders || rootUri || rootPath
      
      ServerCapabilities capabilities = new ServerCapabilities();

      if(args.capabilities.textDocument != null)
      {
        if(args.capabilities.textDocument.synchronization != null)
        {
          capabilities.textDocumentSync = new TextDocumentSyncOptions
          {
            openClose = true, //didOpen, didClose
            change = TextDocumentSyncKind.Full, //didChange
            save = false //didSave
          };
        }
        
        if(args.capabilities.textDocument.signatureHelp != null)
        {
          capabilities.signatureHelpProvider = new SignatureHelpOptions
          {
            triggerCharacters = new[] {"(", ","}
          };
        }

        if(args.capabilities.textDocument.declaration != null)
        {
          if(args.capabilities.textDocument.declaration.linkSupport != null)
            TextDocuments.self.declarationLinkSupport = (bool)args.capabilities.textDocument.declaration.linkSupport;

          capabilities.declarationProvider = false; //textDocument/declaration
        }

        if(args.capabilities.textDocument.definition != null)
        {
          if(args.capabilities.textDocument.definition.linkSupport != null)
            TextDocuments.self.definitionLinkSupport = (bool)args.capabilities.textDocument.definition.linkSupport;

          capabilities.definitionProvider = false; //textDocument/definition
        }

        if(args.capabilities.textDocument.typeDefinition != null)
        {
          if(args.capabilities.textDocument.typeDefinition.linkSupport != null)
            TextDocuments.self.typeDefinitionLinkSupport = (bool)args.capabilities.textDocument.typeDefinition.linkSupport;

          capabilities.typeDefinitionProvider = false; //textDocument/typeDefinition
        }
        
        if(args.capabilities.textDocument.implementation != null)
        {
          if(args.capabilities.textDocument.implementation.linkSupport != null)
            TextDocuments.self.implementationLinkSupport = (bool)args.capabilities.textDocument.implementation.linkSupport;
          
          capabilities.implementationProvider = false; //textDocument/implementation
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

    public override RpcResult Initialized()
    {
      return RpcResult.Success();
    }

    public override RpcResult Shutdown()
    {
      return RpcResult.Success();
    }

    public override RpcResult Exit()
    {
      if(processId != null)
        Environment.Exit(0);
      
      return RpcResult.Success();
    }
  }

  public class BHLSPTextDocumentSignatureHelpJsonRpcService : BHLSPTextDocumentSignatureHelpJsonRpcServiceTemplate
  {
    SignatureInformation GetSignInfo(bhlParser.FuncDeclContext funcDecl)
    {
      var signature = new SignatureInformation();
      List<ParameterInformation> parameters = new List<ParameterInformation>();
      
      signature.label = funcDecl.NAME().GetText()+"(";
      
      if (funcDecl.funcParams() is bhlParser.FuncParamsContext funcParams)
      {
        var funcParamDeclares = funcParams.funcParamDeclare();
        for (int i = 0; i < funcParamDeclares.Length; i++)
        {
          var fpd = funcParamDeclares[i];
          if(fpd.exception != null)
            continue;

          var fpdl = $"{(fpd.isRef() != null ? "ref " : "")}{fpd.type().NAME().GetText()} {fpd.NAME().GetText()}";
          
          parameters.Add(new ParameterInformation
          {
            label = fpdl,
            documentation = ""
          });

          signature.label += fpdl;
          if (i != funcParamDeclares.Length - 1)
            signature.label += ", ";
        }
      }
      else
        signature.label += "<no parameters>";

      signature.label += ")";
      
      if(funcDecl.retType() is bhlParser.RetTypeContext retType)
      {
        signature.label += ":";
        
        var types = retType.type();
        for (int i = 0; i < types.Length; i++)
        {
          if(types[i].exception != null)
            continue;

          signature.label += types[i].NAME().GetText() + " ";
        }
      }
      else
        signature.label += ":void";
      
      signature.parameters = parameters.ToArray();
      
      return signature;
    }
    
    public override RpcResult SignatureHelp(SignatureHelpParams args)
    {
      if(TextDocuments.self.MakeTextDocument(args.textDocument.uri) is TextDocuments.TextDocument document)
      {
        if(document.FindFuncDecl(args.position, out var startIndex) is bhlParser.FuncDeclContext funcDecl)
        {
          string line = document.GetLine(args.position.line);
          var funcDeclStr = line.Substring(startIndex, Math.Max(0, (int) args.position.character - startIndex));
          
          var result = new SignatureHelp();
          result.activeSignature = 0;
          result.activeParameter = (uint)Math.Max(0, funcDeclStr.Split(',').Length - 1); 

          var signature = GetSignInfo(funcDecl);
          signature.activeParameter = result.activeParameter;

          result.signatures = new[] {signature};
          
          return RpcResult.Success(result);
        }
      }

      return RpcResult.Success();
    }
  }

  public class TextDocuments
  {
    public class TextDocument
    {
      private string[] lines;
      private string text;
      
      List<bhlParser.FuncDeclContext> funcDecls = new List<bhlParser.FuncDeclContext>();
      
      public void Sync(string text)
      {
        lines = text.Split('\n');
        this.text = text;

        FindFuncDecls();
      }
      
      public void Sync(string[] lines)
      {
        this.lines = lines;
        for(int i = 0; i < lines.Length; i++)
          text += lines[i];
        
        FindFuncDecls();
      }

      public string GetLine(uint idx)
      {
        return lines[idx];
      }
      
      public bhlParser.FuncDeclContext FindFuncDecl(Position position, out int startIndex)
      {
        startIndex = -1;
        
        string line = lines[position.line];
      
        string pattern = @"[a-zA-Z_][a-zA-Z_0-9]*\({1}.*?";
        MatchCollection matches = Regex.Matches(line, pattern, RegexOptions.Multiline);
      
        for(int i = 0; i < funcDecls.Count; i++)
        {
          foreach (Match m in matches)
          {
            if(funcDecls[i].NAME().GetText() + "(" == m.Value)
            {
              startIndex = m.Index;
              return funcDecls[i];
            }
          }
        }
      
        return null;
      }
      
      void FindFuncDecls(bool errors = false)
      {
        funcDecls.Clear();
        
        var ais = new AntlrInputStream(text.ToStream());
        var lex = new bhlLexer(ais);
        
        if(!errors)
          lex.RemoveErrorListeners();
        
        var tokens = new CommonTokenStream(lex);
        var p = new bhlParser(tokens);
        
        if(!errors)
          p.RemoveErrorListeners();
      
        var progblock = p.program().progblock();
        if(progblock.Length == 0)
          return;
      
        for(int i = 0; i < progblock.Length; ++i)
        {
          var decls = progblock[i].decls().decl();
          for (int j = 0; j < decls.Length; j++)
          {
            var fndecl = decls[j].funcDecl();
            if(fndecl != null)
              funcDecls.Add(fndecl);
          }
        }
      }
    }
    
    private static TextDocuments self_;

    public static TextDocuments self
    {
      get
      {
        if(self_ == null)
          self_ = new TextDocuments();

        return self_;
      }
    }

    public bool declarationLinkSupport;
    public bool definitionLinkSupport;
    public bool typeDefinitionLinkSupport;
    public bool implementationLinkSupport;
    
    
    public bool IsBhl(Uri uri, string languageId = null)
    {
      if(!string.IsNullOrEmpty(languageId))
      {
        bool ok = languageId == "txt" || languageId == "bhl";
        if (!ok)
          return false;
      }
      
      return uri.IsFile &&
             (!string.IsNullOrEmpty(Path.GetExtension(uri.LocalPath)) ||
              Path.GetExtension(uri.LocalPath) != ".bhl");
    }

    private Dictionary<string, TextDocument> documents = new Dictionary<string, TextDocument>();
    
    public void Clear()
    {
      documents.Clear();
    }
    
    public TextDocument MakeTextDocument(string uri, string text = null, string languageId = null)
    {
      return MakeTextDocument(new Uri(uri), text);
    }

    public TextDocument MakeTextDocument(Uri uri, string text = null, string languageId = null)
    {
      if(!IsBhl(uri))
        return null;

      if(!documents.ContainsKey(uri.LocalPath))
      {
        var document = new TextDocument();
        
        if(!string.IsNullOrEmpty(text))
          document.Sync(text);
        else
          document.Sync(System.IO.File.ReadAllLines(uri.LocalPath));
          
        documents.Add(uri.LocalPath, document);
      }
      
      return documents[uri.LocalPath];
    }
    
    public void Open(TextDocumentItem textDocument)
    {
      MakeTextDocument(textDocument.uri, textDocument.text, textDocument.languageId);
    }
    
    public void Change(VersionedTextDocumentIdentifier textDocument, TextDocumentContentChangeEvent[] contentChanges)
    {
      if(MakeTextDocument(textDocument.uri) is TextDocument document)
      {
        for (int i = 0; i < contentChanges.Length; i++)
          document.Sync(contentChanges[i].text);
      }
    }
    
    public void Close(TextDocumentIdentifier textDocument)
    {
      
    }
  }
  
  public class BHLSPTextDocumentSynchronizationJsonRpcService : BHLSPTextDocumentSynchronizationJsonRpcServiceTemplate
  {
    public override RpcResult DidOpenTextDocument(DidOpenTextDocumentParams args)
    {
      TextDocuments.self.Open(args.textDocument);
      
      return RpcResult.Success();
    }
    
    public override RpcResult DidChangeTextDocument(DidChangeTextDocumentParams args)
    {
      TextDocuments.self.Change(args.textDocument, args.contentChanges);
      
      return RpcResult.Success();
    }

    public override RpcResult DidCloseTextDocument(DidCloseTextDocumentParams args)
    {
      TextDocuments.self.Close(args.textDocument);
      
      return RpcResult.Success();
    }
    
    public override RpcResult WillSaveTextDocument(WillSaveTextDocumentParams args)
    {
      return RpcResult.Error(new ResponseError
      {
        code = (int) ErrorCodes.RequestFailed,
        message = "Not supported"
      });
    }

    public override RpcResult WillSaveWaitUntilTextDocument(WillSaveTextDocumentParams args)
    {
      return RpcResult.Error(new ResponseError
      {
        code = (int) ErrorCodes.RequestFailed,
        message = "Not supported"
      });
    }

    public override RpcResult DidSaveTextDocument(DidSaveTextDocumentParams args)
    {
      return RpcResult.Error(new ResponseError
      {
        code = (int) ErrorCodes.RequestFailed,
        message = "Not supported"
      });
    }
  }
  
  public class BHLSPTextDocumentGoToJsonRpcService : BHLSPTextDocumentGoToJsonRpcServiceTemplate
  {
    /**
     * The result type LocationLink[] got introduced with version 3.14.0
     * and depends on the corresponding client capability textDocument.declaration.linkSupport.
     */
    public override RpcResult GotoDeclaration(DeclarationParams args)
    {
      return RpcResult.Error(new ResponseError
      {
        code = (int) ErrorCodes.RequestFailed,
        message = "Not supported"
      });
    }
    
    /**
     * The result type LocationLink[] got introduced with version 3.14.0
     * and depends on the corresponding client capability textDocument.definition.linkSupport.
     */
    public override RpcResult GotoDefinition(DefinitionParams args)
    {
      return RpcResult.Error(new ResponseError
      {
        code = (int) ErrorCodes.RequestFailed,
        message = "Not supported"
      });
    }
    
    /**
     * The result type LocationLink[] got introduced with version 3.14.0
     * and depends on the corresponding client capability textDocument.typeDefinition.linkSupport.
     */
    public override RpcResult GotoTypeDefinition(TypeDefinitionParams args)
    {
      return RpcResult.Error(new ResponseError
      {
        code = (int) ErrorCodes.RequestFailed,
        message = "Not supported"
      });
    }
    
    /**
     * The result type LocationLink[] got introduced with version 3.14.0
     * and depends on the corresponding client capability textDocument.implementation.linkSupport.
     */
    public override RpcResult GotoImplementation(ImplementationParams args)
    {
      return RpcResult.Error(new ResponseError
      {
        code = (int) ErrorCodes.RequestFailed,
        message = "Not supported"
      });
    }
  }
}