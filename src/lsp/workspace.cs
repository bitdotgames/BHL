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
  List<string> roots = new List<string>();
  Dictionary<string, BHLDocument> documents = new Dictionary<string, BHLDocument>();

  public TextDocumentSyncKind syncKind { get; set; } = TextDocumentSyncKind.Full;

  public bool declarationLinkSupport;
  public bool definitionLinkSupport;
  public bool typeDefinitionLinkSupport;
  public bool implementationLinkSupport;
  
  public void Shutdown()
  {
    documents.Clear();
  }

  public void AddRoot(string path)
  {
    roots.Add(path);
  }
  
  public void TryAddDocument(string path, string text = null)
  {
    TryAddDocument(new Uri($"file://{path}"), text);
  }
  
  public void TryAddDocument(Uri uri, string text = null)
  {
    string path = bhl.Util.NormalizeFilePath(uri.LocalPath);
    
    if(!documents.ContainsKey(path))
    {
      var document = CreateDocument(path);
      if(document != null)
      {
        document.uri = uri;

        if(string.IsNullOrEmpty(text))
        {
          byte[] buffer = File.ReadAllBytes(path);
          text = Encoding.UTF8.GetString(buffer);
        }
        
        document.Sync(text);
        documents.Add(path, document);
      }
    }
  }

  public BHLDocument OpenDocument(Uri uri, string text)
  {
    string path = bhl.Util.NormalizeFilePath(uri.LocalPath);

    BHLDocument document;
    if(documents.TryGetValue(path, out document))
    {
      document.Sync(text);
    }
    else
    {
      document = CreateDocument(path);

      document.uri = uri;
      document.Sync(text);

      documents.Add(path, document);
    }

    return document;
  }
  
  BHLDocument CreateDocument(string path)
  {
    if(File.Exists(path))
    {
      string ext = Path.GetExtension(path);
      switch(ext)
      {
        case ".bhl": return new BHLDocument();
      }
    }
    
    return null;
  }
  
  public BHLDocument FindDocument(Uri uri)
  {
    return FindDocument(uri.LocalPath);
  }
  
  public BHLDocument FindDocument(string path)
  {
    path = bhl.Util.NormalizeFilePath(path);
    
    BHLDocument document;
    documents.TryGetValue(path, out document);

    return document;
  }
  
  public IEnumerable<BHLDocument> ForEachBhlImports(BHLDocument root)
  {
    var toVisit = new Queue<BHLDocument>();
    
    toVisit.Enqueue(root);
    while(toVisit.Count > 0)
    {
      var document = toVisit.Dequeue();
      
      string ext = Path.GetExtension(document.uri.AbsolutePath);
      foreach(var import in document.Imports)
      {
        string path = ResolveImportPath(document.uri.LocalPath, import, ext);
        if(!string.IsNullOrEmpty(path))
        {
          TryAddDocument(path);

          if(FindDocument(path) is BHLDocument doc)
            toVisit.Enqueue(doc);
        }
      }
      
      yield return document;
    }
  }

  public string ResolveImportPath(string docpath, string import, string ext)
  {
    var resolved_path = string.Empty;

    foreach(var root in roots)
    {
      string path = string.Empty;
      if(!Path.IsPathRooted(import))
      {
        var dir = Path.GetDirectoryName(docpath);
        path = Path.GetFullPath(Path.Combine(dir, import) + ext);
        if(File.Exists(path))
        {
          resolved_path = path;
          break;
        }
      }
      else
      {
        path = Path.GetFullPath(root + "/" + import + ext);
        if(File.Exists(path))
        {
          resolved_path = path;
          break;
        }
      }
    }
    
    if(!string.IsNullOrEmpty(resolved_path))
      resolved_path = bhl.Util.NormalizeFilePath(resolved_path);
    
    return resolved_path;
  }
}

}
