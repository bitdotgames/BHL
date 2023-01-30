using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace bhl.lsp {

public class Workspace
{
  List<string> roots = new List<string>();

  Dictionary<string, BHLDocument> documents = new Dictionary<string, BHLDocument>();

  public spec.TextDocumentSyncKind syncKind = spec.TextDocumentSyncKind.Full;

  public bool declarationLinkSupport;
  public bool definitionLinkSupport;
  public bool typeDefinitionLinkSupport;
  public bool implementationLinkSupport;
  
  public void Shutdown()
  {
    //TODO: why doing this?
    documents.Clear();
  }

  public void AddRoot(string path)
  {
    roots.Add(path);
  }

  //NOTE: naive initial implementation
  public void IndexFiles(Types ts)
  {
    documents.Clear();

    var inc_path = new IncludePath();
    foreach(var path in roots)
      inc_path.Add(path);

    var file2proc = new Dictionary<string, ANTLR_Processor>(); 
    var file2text = new Dictionary<string, string>();

    foreach(var path in roots)
    {
      var files = Directory.GetFiles(path, "*.bhl", SearchOption.AllDirectories);
      foreach(var file in files)
      {
        using(var sfs = File.OpenRead(file))
        {
          var imports = CompilationExecutor.ParseWorker.ParseImports(inc_path, file, sfs);
          var mdl = new bhl.Module(ts, inc_path.FilePath2ModuleName(file), file);
          var parser = ANTLR_Processor.Stream2Parser(file, sfs);
          var parsed = new ANTLR_Parsed(parser.TokenStream, parser.program());
          var proc = ANTLR_Processor.MakeProcessor(mdl, imports, parsed, ts);
          file2proc.Add(file, proc);
          file2text.Add(file, File.ReadAllText(file));
        }
      }
    }

    ANTLR_Processor.ProcessAll(file2proc, inc_path);

    foreach(var kv in file2proc)
    {
      var doc = new BHLDocument(new Uri("file://" + kv.Key)); 
      doc.Update(file2text[kv.Key], kv.Value);
      documents.Add(kv.Key, doc);
    }
  }

  public BHLDocument GetOrLoadDocument(Uri uri)
  {
    string path = bhl.Util.NormalizeFilePath(uri.LocalPath);
    BHLDocument document;
    if(documents.TryGetValue(path, out document))
      return document;
    else
      return LoadDocument(uri);
  }

  public BHLDocument LoadDocument(Uri uri)
  {
    byte[] buffer = File.ReadAllBytes(uri.LocalPath);
    string text = Encoding.UTF8.GetString(buffer);
    return OpenDocument(uri, text);
  }

  public BHLDocument OpenDocument(Uri uri, string text)
  {
    string path = bhl.Util.NormalizeFilePath(uri.LocalPath);

    BHLDocument document;
    //TODO: use Uri as a key
    if(!documents.TryGetValue(path, out document))
    {
      document = new BHLDocument(uri);  
      documents.Add(path, document);
    }
    
    //TODO: parse document
    //document.Update(text);

    return document;
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
  
  //public IEnumerable<BHLDocument> ForEachBhlImports(BHLDocument root)
  //{
  //  var toVisit = new Queue<BHLDocument>();
  //  
  //  toVisit.Enqueue(root);
  //  while(toVisit.Count > 0)
  //  {
  //    var document = toVisit.Dequeue();
  //    
  //    string ext = Path.GetExtension(document.uri.AbsolutePath);
  //    foreach(var import in document.Imports)
  //    {
  //      string path = ResolveImportPath(document.uri.LocalPath, import, ext);
  //      if(!string.IsNullOrEmpty(path))
  //      {
  //        var doc = GetOrLoadDocument(new Uri($"file://{path}"));
  //        toVisit.Enqueue(doc);
  //      }
  //    }
  //    
  //    yield return document;
  //  }
  //}

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
