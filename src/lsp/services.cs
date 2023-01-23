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

public abstract class GeneralServiceProto : IService
{
  [RpcMethod("initialize")]
  public abstract RpcResult Initialize(InitializeParams args);

  [RpcMethod("initialized")]
  public abstract RpcResult Initialized();

  [RpcMethod("shutdown")]
  public abstract RpcResult Shutdown();

  [RpcMethod("exit")]
  public abstract RpcResult Exit();
}

public abstract class DiagnosticServiceProto : IService
{
  [RpcMethod("$/cancelRequest")]
  public abstract RpcResult CancelRequest(CancelParams args);
}

public abstract class TextDocumentSynchronizationServiceProto : IService
{
  [RpcMethod("textDocument/didOpen")]
  public abstract RpcResult DidOpenTextDocument(DidOpenTextDocumentParams args);
  
  [RpcMethod("textDocument/didChange")]
  public abstract RpcResult DidChangeTextDocument(DidChangeTextDocumentParams args);
  
  [RpcMethod("textDocument/willSave")]
  public abstract RpcResult WillSaveTextDocument(WillSaveTextDocumentParams args);
  
  [RpcMethod("textDocument/willSaveWaitUntil")]
  public abstract RpcResult WillSaveWaitUntilTextDocument(WillSaveTextDocumentParams args);
  
  [RpcMethod("textDocument/didSave")]
  public abstract RpcResult DidSaveTextDocument(DidSaveTextDocumentParams args);
  
  [RpcMethod("textDocument/didClose")]
  public abstract RpcResult DidCloseTextDocument(DidCloseTextDocumentParams args);
}

public abstract class TextDocumentSignatureHelpServiceProto : IService
{
  [RpcMethod("textDocument/signatureHelp")]
  public abstract RpcResult SignatureHelp(SignatureHelpParams args);
}

public abstract class TextDocumentGoToServiceProto : IService
{
  [RpcMethod("textDocument/declaration")]
  public abstract RpcResult GotoDeclaration(DeclarationParams args);
  
  [RpcMethod("textDocument/definition")]
  public abstract RpcResult GotoDefinition(DefinitionParams args);

  [RpcMethod("textDocument/typeDefinition")]
  public abstract RpcResult GotoTypeDefinition(TypeDefinitionParams args);

  [RpcMethod("textDocument/implementation")]
  public abstract RpcResult GotoImplementation(ImplementationParams args);
}

public abstract class TextDocumentHoverServiceProto : IService
{
  [RpcMethod("textDocument/hover")]
  public abstract RpcResult Hover(TextDocumentPositionParams args);
}

public abstract class TextDocumentFindReferencesServiceProto : IService
{
  [RpcMethod("textDocument/references")]
  public abstract RpcResult FindReferences(ReferenceParams args);
}

public abstract class TextDocumentSemanticTokensServiceProto : IService
{
  [RpcMethod("textDocument/semanticTokens/full")]
  public abstract RpcResult SemanticTokensFull(SemanticTokensParams args);
}

public abstract class TextDocumentServiceProto : IService
{
  [RpcMethod("textDocument/completion")]
  public abstract RpcResult Completion(CompletionParams args);

  [RpcMethod("completionItem/resolve")]
  public abstract RpcResult ResolveCompletionItem(CompletionItem args);
  
  [RpcMethod("textDocument/documentHighlight")]
  public abstract RpcResult DocumentHighlight(TextDocumentPositionParams args);

  [RpcMethod("textDocument/documentSymbol")]
  public abstract RpcResult DocumentSymbols(DocumentSymbolParams args);

  [RpcMethod("textDocument/documentColor")]
  public abstract RpcResult DocumentColor(DocumentColorParams args);

  [RpcMethod("textDocument/colorPresentation")]
  public abstract RpcResult ColorPresentation(ColorPresentationParams args);

  [RpcMethod("textDocument/formatting")]
  public abstract RpcResult DocumentFormatting(DocumentFormattingParams args);

  [RpcMethod("textDocument/rangeFormatting")]
  public abstract RpcResult DocumentRangeFormatting(DocumentRangeFormattingParams args);

  [RpcMethod("textDocument/onTypeFormatting")]
  public abstract RpcResult DocumentOnTypeFormatting(DocumentOnTypeFormattingParams args);
  
  [RpcMethod("textDocument/codeAction")]
  public abstract RpcResult CodeAction(CodeActionParams args);

  [RpcMethod("textDocument/codeLens")]
  public abstract RpcResult CodeLens(CodeLensParams args);

  [RpcMethod("codeLens/resolve")]
  public abstract RpcResult ResolveCodeLens(CodeLens args);

  [RpcMethod("textDocument/documentLink")]
  public abstract RpcResult DocumentLink(DocumentLinkParams args);

  [RpcMethod("documentLink/resolve")]
  public abstract RpcResult ResolveDocumentLink(DocumentLink args);

  [RpcMethod("textDocument/rename")]
  public abstract RpcResult Rename(RenameParams args);

  [RpcMethod("textDocument/foldingRange")]
  public abstract RpcResult FoldingRange(FoldingRangeParams args);
}

public abstract class WorkspaceServiceProto : IService
{
  [RpcMethod("workspace/didChangeWorkspaceFolders")]
  public abstract RpcResult DidChangeWorkspaceFolders(DidChangeWorkspaceFoldersParams args);

  [RpcMethod("workspace/didChangeConfiguration")]
  public abstract RpcResult DidChangeConfiguration(DidChangeConfigurationParams args);

  [RpcMethod("workspace/didChangeWatchedFiles")]
  public abstract RpcResult DidChangeWatchedFiles(DidChangeWatchedFilesParams args);

  [RpcMethod("workspace/symbol")]
  public abstract RpcResult Symbol(WorkspaceSymbolParams args);

  [RpcMethod("workspace/executeCommand")]
  public abstract RpcResult ExecuteCommand(ExecuteCommandParams args);
}
  
public class GeneralService : GeneralServiceProto
{
  Workspace workspace;

  int? processId;
  
  public GeneralService(Workspace workspace)
  {
    this.workspace = workspace;
  }

  public override RpcResult Initialize(InitializeParams args)
  {
    processId = args.processId;
    
    if(args.workspaceFolders != null)
    {
      for(int i = 0; i < args.workspaceFolders.Length; i++)
        workspace.AddRoot(args.workspaceFolders[i].uri.LocalPath, cleanup: true);
    }
    else if(args.rootUri != null) // @deprecated in favour of `workspaceFolders`
    {
      workspace.AddRoot(args.rootUri.LocalPath, cleanup: true, check: false);
    }
    else if(!string.IsNullOrEmpty(args.rootPath)) // @deprecated in favour of `rootUri`.
    {
      workspace.AddRoot(args.rootPath, cleanup: true, check: false);
    }
    
    workspace.Scan();
    
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

  public override RpcResult Initialized()
  {
    return RpcResult.Success();
  }

  public override RpcResult Shutdown()
  {
    workspace.Shutdown();

    return RpcResult.Success();
  }

  public override RpcResult Exit()
  {
    if(processId != null)
    {
      //TODO: in this case success response won't be sent
      Environment.Exit(0);
    }
    
    return RpcResult.Success();
  }
}

public class TextDocumentSignatureHelpService : TextDocumentSignatureHelpServiceProto
{
  Workspace workspace;

  public TextDocumentSignatureHelpService(Workspace workspace)
  {
    this.workspace = workspace;
  }

  public override RpcResult SignatureHelp(SignatureHelpParams args)
  {
    workspace.TryAddDocument(args.textDocument.uri);
    if(workspace.FindDocument(args.textDocument.uri) is BHLTextDocument document)
    {
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

public class TextDocumentSynchronizationService : TextDocumentSynchronizationServiceProto
{
  Workspace workspace;

  public TextDocumentSynchronizationService(Workspace workspace)
  {
    this.workspace = workspace;
  }

  public override RpcResult DidOpenTextDocument(DidOpenTextDocumentParams args)
  {
    workspace.TryAddDocument(args.textDocument.uri, args.textDocument.text);
    return RpcResult.Success();
  }
  
  public override RpcResult DidChangeTextDocument(DidChangeTextDocumentParams args)
  {
    if(workspace.FindDocument(args.textDocument.uri) is TextDocument document)
    {
      if(workspace.syncKind == TextDocumentSyncKind.Full)
      {
        foreach(var contentChanges in args.contentChanges)
          document.Sync(contentChanges.text);
      }
      else if(workspace.syncKind == TextDocumentSyncKind.Incremental)
      {
        throw new NotImplementedException();
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

public class TextDocumentGoToService : TextDocumentGoToServiceProto
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
  public override RpcResult GotoDefinition(DefinitionParams args)
  {
    workspace.TryAddDocument(args.textDocument.uri);

    if(workspace.FindDocument(args.textDocument.uri) is BHLTextDocument document)
    {
      int line = (int)args.position.line;
      int character = (int)args.position.character;
      
      int idx = document.CalcByteIndex(line, character);
      
      BHLTextDocument funcDeclBhlDocument = null;

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
          BHLTextDocument classDeclBhlDocument = null;
          
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
}

public class TextDocumentHoverService : TextDocumentHoverServiceProto
{
  Workspace workspace;

  public TextDocumentHoverService(Workspace workspace)
  {
    this.workspace = workspace;
  }

  public override RpcResult Hover(TextDocumentPositionParams args)
  {
    workspace.TryAddDocument(args.textDocument.uri);

    if(workspace.FindDocument(args.textDocument.uri) is BHLTextDocument document)
    {
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
    }
    
    return RpcResult.Success();
  }
}

public class TextDocumentSemanticTokensService : TextDocumentSemanticTokensServiceProto
{
  Workspace workspace;

  public TextDocumentSemanticTokensService(Workspace workspace)
  {
    this.workspace = workspace;
  }

  public override RpcResult SemanticTokensFull(SemanticTokensParams args)
  {
    workspace.TryAddDocument(args.textDocument.uri);
    var document = workspace.FindDocument(args.textDocument.uri);
    
    if(document is BHLTextDocument bhldocument)
    {
      return RpcResult.Success(new SemanticTokens
      {
        data = bhldocument.DataSemanticTokens.ToArray()
      });
    }
    
    if(document is JSTextDocument /*jsdocument*/)
    {
      return RpcResult.Success(new SemanticTokens
      {
          data = new uint[0]
      });
    }
    
    return RpcResult.Success();
  }
}

}
