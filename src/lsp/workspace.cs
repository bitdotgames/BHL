using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using bhl;

namespace bhlsp
{
  public abstract class BHLSPTextDocument
  {
    protected string[] lines;
    protected string text;
    
    public virtual void Sync(string text)
    {
      lines = text.Split('\n');
      this.text = text;
    }
    
    public virtual void Sync(string[] lines)
    {
      this.lines = lines;
      for(int i = 0; i < lines.Length; i++)
        text += lines[i];
    }

    public abstract TextDocumentSignatureHelpContext GetSignatureHelp(Position position);
    public abstract List<IFuncDecl> GetFuncDeclsByName(string funcName);
  }

  public class TextDocumentSignatureHelpContext
  {
    public string line;
    public string funcName;
    public int funcNameStartIdx;
  }
  
  public interface IFuncDecl
  {
    
  }
  
  public class BHLFuncDecl : IFuncDecl
  {
    public bhlParser.FuncDeclContext ctx;

    public BHLFuncDecl(bhlParser.FuncDeclContext ctx)
    {
      this.ctx = ctx;
    }
  }
  
  public class BHLTextDocument : BHLSPTextDocument
  {
    List<BHLFuncDecl> funcDecls = new List<BHLFuncDecl>();
    
    public override void Sync(string text)
    {
      base.Sync(text);
      FindFuncDecls();
    }
    
    public override void Sync(string[] lines)
    {
      base.Sync(lines);
      FindFuncDecls();
    }

    public override TextDocumentSignatureHelpContext GetSignatureHelp(Position position)
    {
      TextDocumentSignatureHelpContext result = new TextDocumentSignatureHelpContext();

      result.line = lines[position.line];
      
      string pattern = @"[a-zA-Z_][a-zA-Z_0-9]*\({1}.*?";
      MatchCollection matches = Regex.Matches(result.line, pattern, RegexOptions.Multiline);
      for(int i = matches.Count-1; i >= 0; i--)
      {
        var m = matches[i];
        if(m.Index < position.character)
        {
          string v = m.Value;
          int len = v.Length - 1;

          if(len > 0)
          {
            result.funcName = result.line.Substring(m.Index, len);
            result.funcNameStartIdx = m.Index;
            break;
          }
        }
      }
      
      return result;
    }

    public override List<IFuncDecl> GetFuncDeclsByName(string funcName)
    {
      List<IFuncDecl> result = new List<IFuncDecl>();
      if(!string.IsNullOrEmpty(funcName))
      {
        for(int i = 0; i < funcDecls.Count; i++)
        {
          var funcDecl = funcDecls[i];
          if(funcDecl.ctx.NAME().GetText() == funcName)
            result.Add(funcDecl);
        }
      }
      return result;
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
            funcDecls.Add(new BHLFuncDecl(fndecl));
        }
      }
    }
  }
  
  public class BHLSPWorkspace
  {
    private static BHLSPWorkspace self_;

    public static BHLSPWorkspace self
    {
      get
      {
        if(self_ == null)
          self_ = new BHLSPWorkspace();

        return self_;
      }
    }
    
    private Dictionary<string, WorkspaceFolder> folders = new Dictionary<string, WorkspaceFolder>();
    private Dictionary<string, BHLSPTextDocument> documents = new Dictionary<string, BHLSPTextDocument>();
    
    public bool declarationLinkSupport;
    public bool definitionLinkSupport;
    public bool typeDefinitionLinkSupport;
    public bool implementationLinkSupport;
    
    public void Shutdown()
    {
      folders.Clear();
      documents.Clear();
    }
    
    public void TryAddFolder(string path)
    {
      TryAddFolder(new Uri(path));
    }
    
    public void TryAddFolder(Uri uri)
    {
      TryAddFolder(new WorkspaceFolder
      {
        uri = uri,
        name = uri.LocalPath.Substring(uri.LocalPath.LastIndexOf("/") + 1)
      });
    }
    
    public void TryAddFolder(WorkspaceFolder folder)
    {
      if(!Directory.Exists(folder.uri.LocalPath) || folders.ContainsKey(folder.uri.LocalPath))
        return;
      
      folders.Add(folder.uri.LocalPath, folder);
      TryAddTextDocuments(folder);
    }

    public void TryAddTextDocument(string path, string text = null)
    {
      TryAddTextDocument(new Uri($"file://{path}"), text);
    }
    
    public void TryAddTextDocument(Uri uri, string text = null)
    {
      if(!File.Exists(uri.LocalPath))
        return;
      
      if(!documents.ContainsKey(uri.LocalPath))
      {
        BHLSPTextDocument document;

        string ext = Path.GetExtension(uri.LocalPath);
        switch(ext)
        {
          case ".bhl":
            document = new BHLTextDocument();
            break;
          default:
              return;
        }
        
        if(!string.IsNullOrEmpty(text))
          document.Sync(text);
        else
          document.Sync(System.IO.File.ReadAllLines(uri.LocalPath));
        
        documents.Add(uri.LocalPath, document);
      }
    }

    public List<BHLSPTextDocument> FindTextDocuments(TextDocumentSignatureHelpContext signatureHelp)
    {
      List<BHLSPTextDocument> result = new List<BHLSPTextDocument>();
      foreach(var document in documents.Values)
      {
        if(!string.IsNullOrEmpty(signatureHelp.funcName))
        {
          var decls = document.GetFuncDeclsByName(signatureHelp.funcName);
          if(decls.Count > 0)
            result.Add(document);
        }
      }
      return result;
    }
    
    public BHLSPTextDocument GetTextDocument(Uri uri)
    {
      return GetTextDocument(uri.LocalPath);
    }
    
    public BHLSPTextDocument GetTextDocument(string path)
    {
      if(documents.ContainsKey(path))
        return documents[path];

      return null;
    }
    
    public void OpenTextDocument(TextDocumentItem textDocument)
    {
      TryAddTextDocument(textDocument.uri, textDocument.text);
    }
    
    public void ChangeTextDocument(VersionedTextDocumentIdentifier textDocument, TextDocumentContentChangeEvent[] contentChanges)
    {
      if(GetTextDocument(textDocument.uri) is BHLSPTextDocument document)
      {
        for (int i = 0; i < contentChanges.Length; i++)
          document.Sync(contentChanges[i].text);
      }
    }
    
    public void CloseTextDocument(TextDocumentIdentifier textDocument)
    {
      
    }
    
    void TryAddTextDocuments(WorkspaceFolder folder)
    {
      SearchFolderFiles(folder.uri.LocalPath, path => TryAddTextDocument(path));
    }
    
    void SearchFolderFiles(string targetDirectory, Action<string> on_file, bool recursive = true)
    {
      foreach(string fileName in Directory.GetFiles(targetDirectory))
        on_file?.Invoke(fileName);

      if(recursive)
      {
        foreach(string subdirectory in Directory.GetDirectories(targetDirectory))
          SearchFolderFiles(subdirectory, on_file);
      }
    }
  }
}