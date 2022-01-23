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
    
    public string GetLine(uint idx)
    {
      return lines[idx];
    }
  }
  
  public class BHLTextDocument : BHLSPTextDocument
  {
    List<bhlParser.FuncDeclContext> funcDecls = new List<bhlParser.FuncDeclContext>();
    
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