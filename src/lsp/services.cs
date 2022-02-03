using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using bhl;
using bhlsp;

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
      
      if(args.workspaceFolders != null)
      {
        for(int i = 0; i < args.workspaceFolders.Length; i++)
          BHLSPWorkspace.self.TryAddDocuments(args.workspaceFolders[i]);
      }
      else if(args.rootUri != null) // @deprecated in favour of `workspaceFolders`
      {
        BHLSPWorkspace.self.TryAddDocuments(args.rootUri);
      }
      else if(!string.IsNullOrEmpty(args.rootPath)) // @deprecated in favour of `rootUri`.
      {
        BHLSPWorkspace.self.TryAddDocuments(args.rootPath);
      }
      
      ServerCapabilities capabilities = new ServerCapabilities();

      if(args.capabilities.textDocument != null)
      {
        if(args.capabilities.textDocument.synchronization != null)
        {
          capabilities.textDocumentSync = new TextDocumentSyncOptions
          {
            openClose = true, //didOpen, didClose
            change = BHLSPWorkspace.self.syncKind, //didChange
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
            BHLSPWorkspace.self.declarationLinkSupport = (bool)args.capabilities.textDocument.declaration.linkSupport;

          capabilities.declarationProvider = false; //textDocument/declaration
        }

        if(args.capabilities.textDocument.definition != null)
        {
          if(args.capabilities.textDocument.definition.linkSupport != null)
            BHLSPWorkspace.self.definitionLinkSupport = (bool)args.capabilities.textDocument.definition.linkSupport;

          capabilities.definitionProvider = true; //textDocument/definition
        }

        if(args.capabilities.textDocument.typeDefinition != null)
        {
          if(args.capabilities.textDocument.typeDefinition.linkSupport != null)
            BHLSPWorkspace.self.typeDefinitionLinkSupport = (bool)args.capabilities.textDocument.typeDefinition.linkSupport;

          capabilities.typeDefinitionProvider = false; //textDocument/typeDefinition
        }
        
        if(args.capabilities.textDocument.implementation != null)
        {
          if(args.capabilities.textDocument.implementation.linkSupport != null)
            BHLSPWorkspace.self.implementationLinkSupport = (bool)args.capabilities.textDocument.implementation.linkSupport;

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
      BHLSPWorkspace.self.Shutdown();
      
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
    public override RpcResult SignatureHelp(SignatureHelpParams args)
    {
      BHLSPWorkspace.self.TryAddDocument(args.textDocument.uri);
      if(BHLSPWorkspace.self.FindDocument(args.textDocument.uri) is BHLTextDocument document && document.funcDecls.Count > 0)
      {
        int line = (int)args.position.line;
        int character = (int)args.position.character;
        
        int start = document.GetIndex(line);
        int stop = document.GetIndex(line, character);
        var text = document.text;

        var txtLine = text.Substring(start, stop - start);
        string funcName = string.Empty;
        uint activeParameter = 0;
        
        if(txtLine.IndexOf("func", StringComparison.Ordinal) == -1)
        {
          string pattern = @"[a-zA-Z_][a-zA-Z_0-9]*\({1}.*?";
          MatchCollection matches = Regex.Matches(txtLine, pattern, RegexOptions.Multiline);
          for(int i = matches.Count-1; i >= 0; i--)
          {
            var m = matches[i];
            if(m.Index < character)
            {
              string v = m.Value;
              int len = v.Length - 1;

              if(len > 0)
              {
                funcName = txtLine.Substring(m.Index, len);
                var funcDeclStr = txtLine.Substring(m.Index, Math.Max(0, character - m.Index));
                activeParameter = (uint)Math.Max(0, funcDeclStr.Split(',').Length - 1);
                break;
              }
            }
          }
        }
        
        bhlParser.FuncDeclContext funcDecl = null;
        if(!string.IsNullOrEmpty(funcName))
        {
          foreach(BHLTextDocument doc in BHLSPWorkspace.self.forEachImports(document))
          {
            if(doc.funcDecls.ContainsKey(funcName))
            {
              funcDecl = doc.funcDecls[funcName];
              break;
            }
          }
          
          if(funcDecl == null)
          {
            foreach(var doc in BHLSPWorkspace.self.ForEachDocuments())
            {
              if(doc is BHLTextDocument bhlDocument)
              {
                if(bhlDocument.funcDecls.ContainsKey(funcName))
                {
                  funcDecl = bhlDocument.funcDecls[funcName];
                  break;
                }
              }
            }
          }
        }
        
        if(funcDecl != null)
        {
          SignatureInformation signInfo = GetFuncSignInfo(funcDecl);
          signInfo.activeParameter = activeParameter;
          
          var result = new SignatureHelp();
          result.activeSignature = 0;
          result.signatures = new[] { signInfo };
          result.activeParameter = signInfo.activeParameter;
            
          return RpcResult.Success(result);
        }
      }
      
      return RpcResult.Success();
    }
    
    SignatureInformation GetFuncSignInfo(bhlParser.FuncDeclContext funcDecl)
    {
      SignatureInformation funcSignature = new SignatureInformation();
      List<ParameterInformation> funcParameters = new List<ParameterInformation>();
      
      funcSignature.label = funcDecl.NAME().GetText()+"(";

      if(funcDecl.funcParams() is bhlParser.FuncParamsContext funcParams)
      {
        var funcParamDeclares = funcParams.funcParamDeclare();
        for (int k = 0; k < funcParamDeclares.Length; k++)
        {
          var fpd = funcParamDeclares[k];
          if(fpd.exception != null)
            continue;

          var fpdl = $"{(fpd.isRef() != null ? "ref " : "")}{fpd.type().NAME().GetText()} {fpd.NAME().GetText()}";
          if(fpd.assignExp() is bhlParser.AssignExpContext assignExp)
            fpdl += assignExp.GetText();
          
          funcParameters.Add(new ParameterInformation
          {
            label = fpdl,
            documentation = ""
          });

          funcSignature.label += fpdl;
          if(k != funcParamDeclares.Length - 1)
            funcSignature.label += ", ";
        }
      }
      else
        funcSignature.label += "<no parameters>";

      funcSignature.label += ")";

      if(funcDecl.retType() is bhlParser.RetTypeContext retType)
      {
        funcSignature.label += ":";

        var types = retType.type();
        for (int n = 0; n < types.Length; n++)
        {
          var t = types[n];
          if(t.exception != null)
            continue;

          funcSignature.label += t.NAME().GetText() + " ";
        }
      }
      else
        funcSignature.label += ":void";

      funcSignature.parameters = funcParameters.ToArray();
      return funcSignature;
    }
  }
  
  public class BHLSPTextDocumentSynchronizationJsonRpcService : BHLSPTextDocumentSynchronizationJsonRpcServiceTemplate
  {
    public override RpcResult DidOpenTextDocument(DidOpenTextDocumentParams args)
    {
      BHLSPWorkspace.self.TryAddDocument(args.textDocument.uri, args.textDocument.text);
      return RpcResult.Success();
    }
    
    public override RpcResult DidChangeTextDocument(DidChangeTextDocumentParams args)
    {
      if(BHLSPWorkspace.self.FindDocument(args.textDocument.uri) is BHLSPTextDocument document)
      {
        if(BHLSPWorkspace.self.syncKind == TextDocumentSyncKind.Full)
        {
          for(int i = 0; i < args.contentChanges.Length; i++)
          {
            document.Sync(args.contentChanges[i].text);
          }
        }
        else if(BHLSPWorkspace.self.syncKind == TextDocumentSyncKind.Incremental)
        {
          //TODO: ...
        }
      }
      
      return RpcResult.Success();
    }

    public override RpcResult DidCloseTextDocument(DidCloseTextDocumentParams args)
    {
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
     * and depends on the corresponding client capability textDocument.definition.linkSupport.
     */
    public override RpcResult GotoDefinition(DefinitionParams args)
    {
      BHLSPWorkspace.self.TryAddDocument(args.textDocument.uri);
      if(BHLSPWorkspace.self.FindDocument(args.textDocument.uri) is BHLTextDocument document)
      {
        int line = (int)args.position.line;
        int character = (int)args.position.character;
        
        int idx = document.GetIndex(line, character);

        bhlParser.CallExpContext callExp = null;
        bhlParser.MemberAccessContext memberAccess = null;
        bhlParser.TypeContext type = null;

        foreach(IParseTree node in Util.DFS(document.ToParser().program()))
        {
          if(node is ParserRuleContext prc)
          {
            if(prc.Start.StartIndex <= idx && idx <= prc.Stop.StopIndex)
            {
              callExp      = prc as bhlParser.CallExpContext;
              memberAccess = prc as bhlParser.MemberAccessContext;
              type         = prc as bhlParser.TypeContext;
              break;
            }
          }
        }
        
        string classTypeName = string.Empty;
        string memberClassName = string.Empty;
        
        if(type?.NAME() != null)
        {
          classTypeName = type.NAME().GetText();
        }
        else if(memberAccess != null)
        {
          bhlParser.CallExpContext callExpMemberAccess = null;
          bhlParser.FuncDeclContext memberAccessParentFuncDecl = null;

          memberClassName = memberAccess.NAME().GetText();
          
          for(RuleContext parent = memberAccess.Parent; parent != null; parent = parent.Parent)
          {
            if(callExpMemberAccess == null && parent is bhlParser.CallExpContext)
              callExpMemberAccess = parent as bhlParser.CallExpContext;

            if(parent is bhlParser.FuncDeclContext)
            {
              memberAccessParentFuncDecl = parent as bhlParser.FuncDeclContext;
              break;
            }
          }
          
          if(callExpMemberAccess != null)
          {
            string callExpMemberAccessName = callExpMemberAccess.NAME().GetText();
            
            if(memberAccessParentFuncDecl?.NAME() != null)
            {
              foreach(IParseTree node in Util.DFS(memberAccessParentFuncDecl))
              {
                if(node is bhlParser.FuncParamDeclareContext funcParamDeclare)
                {
                  bhlParser.TypeContext funcParamDeclareType = funcParamDeclare.type();
                  if(funcParamDeclareType.fnargs() != null || funcParamDeclareType.ARR() != null)
                    continue;
                  
                  if(funcParamDeclare.NAME()?.GetText() == callExpMemberAccessName)
                  {
                    classTypeName = funcParamDeclareType.GetText();
                    break;
                  }
                }

                if(node is bhlParser.VarDeclareContext varDeclare && varDeclare?.NAME().GetText() == callExpMemberAccessName)
                {
                  classTypeName = varDeclare.type().NAME().GetText();
                  break;
                }
              }
            }
            
            if(string.IsNullOrEmpty(classTypeName) && !string.IsNullOrEmpty(callExpMemberAccessName))
            {
              foreach(var doc in ForEachDocuments(document))
              {
                if(doc.varDeclars.ContainsKey(callExpMemberAccessName))
                {
                  var varDeclareAssign = doc.varDeclars[callExpMemberAccessName];
                  classTypeName = varDeclareAssign.varDeclare().type().NAME().GetText();
                  break;
                }
              }
            }
          }
        }
        
        if(!string.IsNullOrEmpty(classTypeName))
        {
          bhlParser.ClassDeclContext classDecl = null;
          BHLTextDocument classDeclBhlDocument = null;
          
          foreach(var doc in ForEachDocuments(document))
          {
            if(doc.classDecls.ContainsKey(classTypeName))
            {
              classDecl = doc.classDecls[classTypeName];
              classDeclBhlDocument = doc;
              break;
            }
          }
          
          if(classDecl != null)
          {
            bhlParser.ClassMemberContext classMember = null;

            if(!string.IsNullOrEmpty(memberClassName))
            {
              foreach(var classMemberContext in classDecl.classBlock().classMembers().classMember())
              {
                if(classMemberContext.funcDecl()?.NAME()?.GetText() != null)
                {
                  if(classMemberContext.funcDecl().NAME().GetText() == memberClassName)
                  {
                    classMember = classMemberContext;
                    break;
                  }
                }
              
                if(classMemberContext.varDeclare()?.NAME()?.GetText() != null)
                {
                  if(classMemberContext.varDeclare().NAME().GetText() == memberClassName)
                  {
                    classMember = classMemberContext;
                    break;
                  }
                }
              }
            }

            int classDeclIdx = classMember?.Start.StartIndex ?? classDecl.Start.StartIndex;
            var start = classDeclBhlDocument.GetLineColumn(classDeclIdx);
            var startPos = new Position {line = (uint) start.Item1, character = (uint) start.Item2};
        
            return RpcResult.Success(new Location
            {
              uri = classDeclBhlDocument.uri,
              range = new Range
              {
                start = startPos,
                end = startPos
              }
            });
          }
        }
        
        if(callExp != null)
        {
          string callExpName = callExp.NAME().GetText();
          
          bhlParser.FuncDeclContext funcDecl = null;
          BHLTextDocument funcDeclBhlDocument = null;
          
          foreach(var doc in ForEachDocuments(document))
          {
            if(doc.funcDecls.ContainsKey(callExpName))
            {
              funcDecl = doc.funcDecls[callExpName];
              funcDeclBhlDocument = doc;
              break;
            }
          }
          
          if(funcDecl != null)
          {
            var start = funcDeclBhlDocument.GetLineColumn(funcDecl.Start.StartIndex);
            var startPos = new Position {line = (uint) start.Item1, character = (uint) start.Item2};
            
            return RpcResult.Success(new Location
            {
              uri = funcDeclBhlDocument.uri,
              range = new Range
              {
                start = startPos,
                end = startPos
              }
            });
          }
        }
      }
      
      return RpcResult.Success();
    }
    
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
    
    IEnumerable<BHLTextDocument> ForEachDocuments(BHLTextDocument root)
    {
      foreach(BHLTextDocument doc in BHLSPWorkspace.self.forEachImports(root))
      {
        yield return doc;
      }
      
      foreach(var doc in BHLSPWorkspace.self.ForEachDocuments())
      {
        if(doc is BHLTextDocument bhlDocument)
          yield return bhlDocument;
      }
    }
  }
}