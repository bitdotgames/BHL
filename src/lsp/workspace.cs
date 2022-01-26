using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Antlr4.Runtime;
using bhl;

namespace bhlsp
{
  public abstract class BHLSPTextDocument
  {
    protected string text;
    public string Text => text;

    public virtual void Sync(string text)
    {
      this.text = text;
    }
    
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
    
    public override void Sync(string text)
    {
      base.Sync(text);
      FindFuncDecls();
    }
    
    public override List<IFuncDecl> FindFuncDeclsByName(string funcName)
    {
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
      
      SearchFolderFiles(uriFolder.LocalPath);
    }
    
    void SearchFolderFiles(string targetDirectory)
    {
      Dictionary<string, string> contents = new Dictionary<string, string>();
      
      var sw = new System.Diagnostics.Stopwatch();
      sw.Start();
      
      var files = Directory.GetFiles(targetDirectory, "*.bhl", SearchOption.AllDirectories);
      
      var tasks = new List<Task>();
      for (int i = 0; i < files.Length; i++)
      {
        string path = files[i];
        if(!documents.ContainsKey(path))
          tasks.Add(ReadAsyncFile(files[i], contents));
      }
      
      Task.WhenAll(tasks).Wait();
      
      tasks.Clear();
      foreach(var path in contents.Keys)
        tasks.Add(TryAddAsyncTextDocument(path, contents[path]));
      
      Task.WhenAll(tasks).Wait();
      
      sw.Stop();
      BHLSPLogger.WriteLine($"SearchFolderFiles done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
    }
    
    public async Task TryAddAsyncTextDocument(string path, string text)
    {
      var task = Task.Run(() => TryAddTextDocument(path, text));
      await task;
    }
    
    object lockRead = new object();
    
    async Task ReadAsyncFile(string file, Dictionary<string, string> map)
    {
      byte[] buffer;
      using (FileStream sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read))
      {
        buffer = new byte[sourceStream.Length];
        await sourceStream.ReadAsync(buffer, 0, (int)sourceStream.Length);
      }

      string content = System.Text.Encoding.ASCII.GetString(buffer);
      
      lock(lockRead)
      {
        map.Add(file, content);
      }
    }
    
    public void TryAddTextDocument(string path, string text)
    {
      TryAddTextDocument(new Uri($"file://{path}"), text);
    }
    
    object lockAddDocument = new object();
    
    public void TryAddTextDocument(Uri uri, string text = null)
    {
      string path = uri.LocalPath;

      if(!documents.ContainsKey(path))
      {
        
      }
      
      BHLSPTextDocument document = CreateDocument(path);
      if(document != null)
      {
        document.Sync(string.IsNullOrEmpty(text) ? System.IO.File.ReadAllText(path) : text);
        
        lock(lockAddDocument)
        {
          if(!documents.ContainsKey(path))
            documents.Add(path, document);
        }
      }
    }

    BHLSPTextDocument CreateDocument(string path)
    {
      string ext = Path.GetExtension(path);
      switch(ext)
      {
        case ".bhl": return new BHLTextDocument();
      }

      return null;
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