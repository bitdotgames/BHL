using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using bhl;

namespace bhl.lsp {

public interface IService {}

public class GeneralService : IService
{
  Workspace workspace;

  int? processId;
  
  public GeneralService(Workspace workspace)
  {
    this.workspace = workspace;
  }

  [RpcMethod("initialize")]
  public RpcResult Initialize(InitializeParams args)
  {
    processId = args.processId;
    
    if(args.workspaceFolders != null)
    {
      for(int i = 0; i < args.workspaceFolders.Length; i++)
        workspace.AddRoot(args.workspaceFolders[i].uri.LocalPath);
    }
    else if(args.rootUri != null) // @deprecated in favour of `workspaceFolders`
    {
      workspace.AddRoot(args.rootUri.LocalPath);
    }
    else if(!string.IsNullOrEmpty(args.rootPath)) // @deprecated in favour of `rootUri`.
    {
      workspace.AddRoot(args.rootPath);
    }
    
    //TODO: do indexing in background
    //workspace.IndexInBackground();
    
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
            tokenTypes = BHLSemanticTokens.semanticTokenTypes,
            tokenModifiers = BHLSemanticTokens.semanticTokenModifiers
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
    if(processId != null)
    {
      //TODO: in this case success response won't be sent
      Environment.Exit(0);
    }
    
    return RpcResult.Success();
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

    int line = (int)args.position.line;
    int character = (int)args.position.character;
    
    int start = document.CalcByteIndex(line);
    int stop = document.CalcByteIndex(line, character);
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
      foreach(var doc in workspace.ForEachBhlImports(document))
      {
        if(doc.FuncDecls.ContainsKey(funcName))
        {
          funcDecl = doc.FuncDecls[funcName];
          break;
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
    
    return RpcResult.Success();
  }
  
  SignatureInformation GetFuncSignInfo(bhlParser.FuncDeclContext funcDecl)
  {
    SignatureInformation funcSignature = new SignatureInformation();
    
    string label = funcDecl.NAME().GetText()+"(";
    
    List<ParameterInformation> funcParameters = Util.GetInfoParams(funcDecl);
    
    if(funcParameters.Count > 0)
    {
      for(int k = 0; k < funcParameters.Count; k++)
      {
        var funcParameter = funcParameters[k];
        label += funcParameter.label.Value;
        if(k != funcParameters.Count - 1)
          label += ", ";
      }
    }
    else
      label += "<no parameters>";

    label += ")";

    if(funcDecl.retType() is bhlParser.RetTypeContext retType)
    {
      label += ":";

      var types = retType.type();
      for (int n = 0; n < types.Length; n++)
      {
        var t = types[n];
        if(t.exception != null)
          continue;

        label += t.nsName().GetText() + " ";
      }
    }
    else
      label += ":void";

    funcSignature.label = label;
    funcSignature.parameters = funcParameters.ToArray();
    return funcSignature;
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

    return RpcResult.Success();
  }
  
  [RpcMethod("textDocument/didChange")]
  public RpcResult DidChangeTextDocument(DidChangeTextDocumentParams args)
  {
    var document = workspace.FindDocument(args.textDocument.uri);
    if(document != null)
    {
      if(workspace.syncKind == TextDocumentSyncKind.Full)
      {
        foreach(var changes in args.contentChanges)
          document.Sync(changes.text);
      }
      else if(workspace.syncKind == TextDocumentSyncKind.Incremental)
      {
        return RpcResult.Error(new ResponseError
        {
          code = (int)ErrorCodes.RequestFailed,
          message = "Not supported"
        });
      }
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

    int line = (int)args.position.line;
    int character = (int)args.position.character;
    
    int idx = document.CalcByteIndex(line, character);
    
    BHLDocument funcDeclBhlDocument = null;

    bhlParser.FuncDeclContext funcDecl = null;
    bhlParser.CallExpContext callExp = null;
    bhlParser.MemberAccessContext memberAccess = null;
    bhlParser.TypeContext type = null;
    bhlParser.StatementContext statement = null;
    bhlParser.NsNameContext nsName = null;
    bhlParser.DotNameContext dotName = null;

    foreach(IParseTree node in Util.DFS(document.ToParser().program()))
    {
      if(node is ParserRuleContext prc)
      {
        if(prc.Start.StartIndex <= idx && idx <= prc.Stop.StopIndex)
        {
          //Console.WriteLine("GOTCHA " + idx + " " + prc.GetType().Name + " @" + prc.Start.Line + ":" + prc.Start.Column + " " + prc.GetText());

          funcDecl     = prc as bhlParser.FuncDeclContext;
          callExp      = prc as bhlParser.CallExpContext;
          memberAccess = prc as bhlParser.MemberAccessContext;
          type         = prc as bhlParser.TypeContext;
          statement    = prc as bhlParser.StatementContext;
          nsName       = prc as bhlParser.NsNameContext;
          dotName       = prc as bhlParser.DotNameContext;
          break;
        }
      }
    }
    
    if(funcDecl == null)
    {
      string classTypeName = string.Empty;
      string memberClassName = string.Empty;
      
      if(type?.nsName() != null)
      {
        classTypeName = type.nsName().GetText();
      }
      else if(nsName != null)
      {
        var nsNameStr = nsName.dotName()?.NAME()?.GetText();
        if(nsNameStr != null)
          classTypeName = nsNameStr;
      }
      else if(dotName != null)
      {
        var nsNameStr = dotName.NAME()?.GetText();
        if(nsNameStr != null)
          classTypeName = nsNameStr;
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
                if(funcParamDeclareType.funcType() != null || funcParamDeclareType.ARR() != null)
                  continue;
                
                if(funcParamDeclare.NAME()?.GetText() == callExpMemberAccessName)
                {
                  classTypeName = funcParamDeclareType.GetText();
                  break;
                }
              }

              if(node is bhlParser.VarDeclareContext varDeclare && varDeclare?.NAME().GetText() == callExpMemberAccessName)
              {
                classTypeName = varDeclare.type().nsName().GetText();
                break;
              }
            }
          }
        }
      }
      
      if(!string.IsNullOrEmpty(classTypeName))
      {
        bhlParser.ClassDeclContext classDecl = null;
        BHLDocument classDeclBhlDocument = null;
        
        foreach(var doc in workspace.ForEachBhlImports(document))
        {
          if(doc.ClassDecls.ContainsKey(classTypeName))
          {
            classDecl = doc.ClassDecls[classTypeName];
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
              
              if(classMemberContext.fldDeclare()?.varDeclare()?.NAME()?.GetText() != null)
              {
                if(classMemberContext.fldDeclare().varDeclare().NAME().GetText() == memberClassName)
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
        
        foreach(var doc in workspace.ForEachBhlImports(document))
        {
          if(doc.FuncDecls.ContainsKey(callExpName))
          {
            funcDecl = doc.FuncDecls[callExpName];
            funcDeclBhlDocument = doc;
            break;
          }
        }
      }
      
      if(statement != null && funcDecl == null)
      {
        string funcName = string.Empty;
        
        string pattern = @"([a-zA-Z_][a-zA-Z_0-9]*)(\({1}.*?)";
        MatchCollection matches = Regex.Matches(statement.GetText(), pattern, RegexOptions.Multiline);
        for(int i = 0; i < matches.Count; i++)
        {
          var m = matches[i];
          if(m.Groups.Count > 1)
          {
            Group g = m.Groups[1];
            funcName = g.Value;
            break;
          }
        }

        if(!string.IsNullOrEmpty(funcName))
        {
          foreach(var doc in workspace.ForEachBhlImports(document))
          {
            if(doc.FuncDecls.ContainsKey(funcName))
            {
              funcDecl = doc.FuncDecls[funcName];
              funcDeclBhlDocument = doc;
              break;
            }
          }
        }
      }
    }
    else
    {
      funcDeclBhlDocument = document;
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
    
    return RpcResult.Success();
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
  Workspace workspace;

  public TextDocumentHoverService(Workspace workspace)
  {
    this.workspace = workspace;
  }

  [RpcMethod("textDocument/hover")]
  public RpcResult Hover(TextDocumentPositionParams args)
  {
    var document = workspace.GetOrLoadDocument(args.textDocument.uri);

    int line = (int)args.position.line;
    int character = (int)args.position.character;
    
    int idx = document.CalcByteIndex(line, character);

    bhlParser.CallExpContext callExp = null;
    
    foreach(IParseTree node in Util.DFS(document.ToParser().program()))
    {
      if(node is ParserRuleContext prc)
      {
        if(prc.Start.StartIndex <= idx && idx <= prc.Stop.StopIndex)
        {
          callExp = prc as bhlParser.CallExpContext;
          break;
        }
      }
    }
    
    bhlParser.FuncDeclContext funcDecl = null;
    
    if(callExp != null)
    {
      string callExpName = callExp.NAME().GetText();
      
      foreach(var doc in workspace.ForEachBhlImports(document))
      {
        if(doc.FuncDecls.ContainsKey(callExpName))
        {
          funcDecl = doc.FuncDecls[callExpName];
          break;
        }
      }
    }
    
    if(funcDecl != null)
    {
      string label = funcDecl.NAME().GetText()+"(";
  
      var funcParameters = Util.GetInfoParams(funcDecl);
  
      if(funcParameters.Count > 0)
      {
        for(int k = 0; k < funcParameters.Count; k++)
        {
          var funcParameter = funcParameters[k];
          label += funcParameter.label.Value;
          if(k != funcParameters.Count - 1)
            label += ", ";
        }
      }
      else
        label += "<no parameters>";

      label += ")";

      if(funcDecl.retType() is bhlParser.RetTypeContext retType)
      {
        label += ":";

        var types = retType.type();
        for (int n = 0; n < types.Length; n++)
        {
          var t = types[n];
          if(t.exception != null)
            continue;

          label += t.nsName().GetText() + " ";
        }
      }
      else
        label += ":void";
      
      return RpcResult.Success(new Hover
      {
        contents = new MarkupContent
        {
          kind = "plaintext",
          value = label
        }
      });
    }
    
    return RpcResult.Success();
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

    return RpcResult.Success(new SemanticTokens
    {
      data = document.DataSemanticTokens.ToArray()
    });
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
