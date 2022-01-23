using System;
using System.Collections.Generic;
using System.IO;
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

    [JsonRpcMethod("textDocument/definition")]
    public abstract RpcResult GotoDefinition(TextDocumentPositionParams args);

    [JsonRpcMethod("textDocument/typeDefinition")]
    public abstract RpcResult GotoTypeDefinition(TextDocumentPositionParams args);

    [JsonRpcMethod("textDocument/implementation")]
    public abstract RpcResult GotoImplementation(TextDocumentPositionParams args);

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
            openClose = false, //didOpen, didClose
            change = TextDocumentSyncKind.None, //didChange
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
    List<bhlParser.FuncDeclContext> FindFuncDecls(string[] document)
    {
      string file = "";
      for(int i = 0; i < document.Length; i++)
        file += document[i];
      
      List<bhlParser.FuncDeclContext> fndecls = new List<bhlParser.FuncDeclContext>();

      var ais = new AntlrInputStream(file.ToStream());
      var lex = new bhlLexer(ais);
        
      lex.RemoveErrorListeners();
        
      var tokens = new CommonTokenStream(lex);
      var p = new bhlParser(tokens);
        
      p.RemoveErrorListeners();
      
      var progblock = p.program().progblock();
      if(progblock.Length == 0)
        return fndecls;
      
      for(int i = 0; i < progblock.Length; ++i)
      {
        var decls = progblock[i].decls().decl();
        for (int j = 0; j < decls.Length; j++)
        {
          var fndecl = decls[j].funcDecl();
          if(fndecl != null)
            fndecls.Add(fndecl);
        }
      }
      
      return fndecls;
    }

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

    bhlParser.FuncDeclContext FindFuncDecl(string[] document, Position position, out int startIndex)
    {
      startIndex = -1;
      
      var funcDecls = FindFuncDecls(document);
      string line = document[position.line];
      
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
    
    public override RpcResult SignatureHelp(SignatureHelpParams args)
    {
      if(args.textDocument.uri.IsFile)
      {
        string[] document = System.IO.File.ReadAllLines(args.textDocument.uri.LocalPath);
        var funcDecl = FindFuncDecl(document, args.position, out var startIndex);
        if(funcDecl != null)
        {
          string line = document[args.position.line];
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
}