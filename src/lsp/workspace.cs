using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using bhl;

namespace bhl.lsp {

public class Workspace
{
  private static Workspace self_;
  public static Workspace self
  {
    get
    {
      if(self_ == null)
        self_ = new Workspace();

      return self_;
    }
  }

  struct RootPath
  {
    public string path;
    public bool cleanup;
  }
  
  private List<RootPath> roots = new List<RootPath>();
  private Dictionary<string, TextDocument> documents = new Dictionary<string, TextDocument>();
  List<string> documentPaths = new List<string>();

  public TextDocumentSyncKind syncKind { get; set; } = TextDocumentSyncKind.Full;

  public bool declarationLinkSupport;
  public bool definitionLinkSupport;
  public bool typeDefinitionLinkSupport;
  public bool implementationLinkSupport;
  
  public void Shutdown()
  {
    documents.Clear();
    for(int i = roots.Count - 1; i >= 0; i--)
    {
      if(roots[i].cleanup)
        roots.RemoveAt(i);
    }
    
    lock(addDocumentPathLock)
    {
      documentPaths.Clear();
    }
  }

  public void AddRoot(string pathFolder, bool cleanup, bool check = true)
  {
    if(check && !Directory.Exists(pathFolder))
      return;
    
    roots.Add(new RootPath {path = pathFolder, cleanup = cleanup});
  }
  
  object addDocumentPathLock = new object();
  
  public void Scan()
  {
    Task.Run(() => {
      foreach(var root in roots)
      {
        var files = Directory.GetFiles(root.path, "*.bhl", SearchOption.AllDirectories);
        
        lock(addDocumentPathLock)
        {
          documentPaths.AddRange(files);
        }
      }
    });
  }
  
  public void TryAddDocument(string path, string text = null)
  {
    TryAddDocument(new Uri($"file://{path}"), text);
  }
  
  public void TryAddDocument(Uri uri, string text = null)
  {
    string path = uri.LocalPath;
    path = bhl.Util.NormalizeFilePath(path);
    
    if(!documents.ContainsKey(path))
    {
      TextDocument document = CreateDocument(path);
      if(document != null)
      {
        document.uri = uri;

        if(string.IsNullOrEmpty(text))
        {
          byte[] buffer = File.ReadAllBytes(path);
          text = Encoding.UTF8.GetString(buffer);
        }
        
        lock(addDocumentPathLock)
        {
          if(-1 == documentPaths.IndexOf(path))
            documentPaths.Add(path);
        }
        
        document.Sync(text);
        documents.Add(path, document);
      }
    }
  }
  
  TextDocument CreateDocument(string path)
  {
    if(File.Exists(path))
    {
      string ext = Path.GetExtension(path);
      switch(ext)
      {
        case ".bhl": return new BHLTextDocument();
        case ".js": return new JSTextDocument();
      }
    }
    
    return null;
  }
  
  public TextDocument FindDocument(Uri uri)
  {
    return FindDocument(uri.LocalPath);
  }
  
  public TextDocument FindDocument(string path)
  {
    path = bhl.Util.NormalizeFilePath(path);
    
    if(documents.ContainsKey(path))
      return documents[path];

    return null;
  }
  
  public IEnumerable<BHLTextDocument> ForEachBhlImports(BHLTextDocument root)
  {
    Queue<BHLTextDocument> toVisit = new Queue<BHLTextDocument>();
    
    toVisit.Enqueue(root);
    while(toVisit.Count > 0)
    {
      BHLTextDocument document = toVisit.Dequeue();
      
      string ext = Path.GetExtension(document.uri.AbsolutePath);
      foreach(var import in document.Imports)
      {
        string path = ResolveImportPath(document.uri.LocalPath, import, ext);
        if(!string.IsNullOrEmpty(path))
        {
          TryAddDocument(path);
          if(FindDocument(path) is BHLTextDocument doc)
            toVisit.Enqueue(doc);
        }
      }
      
      yield return document;
    }
  }

  public string ResolveImportPath(string docpath, string import, string ext)
  {
    var filePath = string.Empty;

    foreach(var root in roots)
    {
      string path = string.Empty;
      if(!Path.IsPathRooted(import))
      {
        var dir = Path.GetDirectoryName(docpath);
        path = Path.GetFullPath(Path.Combine(dir, import) + ext);
        if(File.Exists(path))
        {
          filePath = path;
          break;
        }
      }
      else
      {
        path = Path.GetFullPath(root.path + "/" + import + ext);
        if(File.Exists(path))
        {
          filePath = path;
          break;
        }
      }
    }
    
    lock(addDocumentPathLock)
    {
      foreach(var path in documentPaths)
      {
        if(-1 != path.IndexOf(import + ext, StringComparison.Ordinal))
        {
          filePath = path;
          break;
        }
      }
    }
    
    if(!string.IsNullOrEmpty(filePath))
      filePath = bhl.Util.NormalizeFilePath(filePath);
    
    return filePath;
  }
}

}
