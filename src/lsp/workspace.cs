using System;
using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;
using bhl;

namespace bhlsp
{
  public abstract class BHLSPTextDocument
  {
    public string text;
    
    public abstract List<IFuncDecl> FindFuncDeclsByName(string funcName);
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
    
    public override List<IFuncDecl> FindFuncDeclsByName(string funcName)
    {
      FindFuncDecls();
      
      List<IFuncDecl> result = new List<IFuncDecl>();
      if(!string.IsNullOrEmpty(funcName))
      {
        for(int i = 0; i < funcDecls.Count; i++)
        {
          var funcDecl = funcDecls[i];
          var name = funcDecl.ctx.NAME();
          
          if(name != null && name.GetText() == funcName)
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
        for(int j = 0; j < decls.Length; j++)
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
    
    private Dictionary<string, BHLSPTextDocument> documents = new Dictionary<string, BHLSPTextDocument>();

    public TextDocumentSyncKind syncKind { get; set; } = TextDocumentSyncKind.Full;

    public bool declarationLinkSupport;
    public bool definitionLinkSupport;
    public bool typeDefinitionLinkSupport;
    public bool implementationLinkSupport;
    
    public void Shutdown()
    {
      documents.Clear();
    }
    
    public void TryAddTextDocuments(string pathFolder)
    {
      TryAddTextDocuments(new Uri(pathFolder));
    }
    
    public void TryAddTextDocuments(WorkspaceFolder folder)
    {
      TryAddTextDocuments(folder.uri);
    }
    
    public void TryAddTextDocuments(Uri uriFolder)
    {
      if(!Directory.Exists(uriFolder.LocalPath))
        return;
      
      var sw = new System.Diagnostics.Stopwatch();
      sw.Start();

      SearchFolderFiles(uriFolder.LocalPath, path => TryAddTextDocument(path));
      
      sw.Stop();
      BHLSPLogger.WriteLine($"SearchFolderFiles done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
    }
    
    void SearchFolderFiles(string targetDirectory, Action<string> onFile)
    {
      foreach(string filePath in Directory.GetFiles(targetDirectory, "*", SearchOption.AllDirectories))
        onFile?.Invoke(filePath);
    }
    
    public void TryAddTextDocument(string path)
    {
      TryAddTextDocument(new Uri(String.Concat("file://", path)));
    }
    
    public void TryAddTextDocument(Uri uri)
    {
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
        
        document.text = System.IO.File.ReadAllText(uri.LocalPath);
        
        documents.Add(uri.LocalPath, document);
      }
    }

    public List<IFuncDecl> FindFuncDeclsByName(string funcName)
    {
      List<IFuncDecl> result = new List<IFuncDecl>();
      foreach(var document in documents.Values)
      {
        var decls = document.FindFuncDeclsByName(funcName);
        if(decls.Count > 0)
          result.AddRange(decls);
      }
      return result;
    }
    
    public BHLSPTextDocument FindTextDocument(Uri uri)
    {
      return FindTextDocument(uri.LocalPath);
    }
    
    public BHLSPTextDocument FindTextDocument(string path)
    {
      if(documents.ContainsKey(path))
        return documents[path];

      return null;
    }
  }
}