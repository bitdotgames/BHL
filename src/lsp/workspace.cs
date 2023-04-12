using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace bhl.lsp {

public class Workspace
{
  Types ts;
  IncludePath inc_path;
  Dictionary<Uri, ANTLR_Processor> file2proc = new Dictionary<Uri, ANTLR_Processor>(); 
  Dictionary<Uri, BHLDocument> file2doc = new Dictionary<Uri, BHLDocument>();

  public spec.TextDocumentSyncKind syncKind = spec.TextDocumentSyncKind.Full;

  public bool declarationLinkSupport;
  public bool definitionLinkSupport;
  public bool typeDefinitionLinkSupport;
  public bool implementationLinkSupport;

  public Workspace()
  {}

  public void Init(Types ts, IncludePath inc_path)
  {
    this.ts = ts;
    this.inc_path = inc_path;
  }
  
  public void Shutdown()
  {}

  //NOTE: naive initial implementation
  public void IndexFiles()
  {
    file2proc.Clear();
    file2doc.Clear();

    for(int i=0;i<inc_path.Count;++i)
    {
      var path = inc_path[i];

      var files = Directory.GetFiles(path, "*.bhl", SearchOption.AllDirectories);
      foreach(var file in files)
      {
        var uri = new Uri("file://" + file);

        using(var sfs = File.OpenRead(file))
        {
          var proc = ParseFile(ts, uri, sfs);
          file2proc.Add(uri, proc);
        }
      }
    }

    //TODO: use compiled cache if needed
    ANTLR_Processor.ProcessAll(file2proc, null, inc_path);

    foreach(var kv in file2proc)
    {
      var document = new BHLDocument(kv.Key); 
      document.Update(File.ReadAllText(kv.Key.LocalPath), kv.Value);
      file2doc.Add(kv.Key, document);
    }
  }

  ANTLR_Processor ParseFile(Types ts, Uri uri, Stream stream)
  {
    var imports = CompilationExecutor.ParseWorker.ParseImports(inc_path, uri.LocalPath, stream);
    var module = new bhl.Module(ts, inc_path.FileUri2ModuleName(uri), uri.LocalPath);

    var errors = new CompileErrors();

    var parser = ANTLR_Processor.Stream2Parser(uri.LocalPath, stream, null/*TODO*/);

    var parsed = new ANTLR_Parsed(parser);

    var proc = ANTLR_Processor.MakeProcessor(
      module, 
      imports, 
      parsed, 
      ts, 
      errors, 
      null/*TODO*/
    );

    return proc;
  }

  public BHLDocument GetOrLoadDocument(Uri uri)
  {
    BHLDocument document;
    if(file2doc.TryGetValue(uri, out document))
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
    BHLDocument document;
    if(!file2doc.TryGetValue(uri, out document))
    {
      document = new BHLDocument(uri);  
      file2doc.Add(uri, document);
    }
    
    //TODO: parse document?

    return document;
  }
  
  public BHLDocument FindDocument(Uri uri)
  {
    BHLDocument document;
    file2doc.TryGetValue(uri, out document);
    return document;
  }
  
  public bool UpdateDocument(Uri uri, string text)
  {
    var document = FindDocument(uri);
    if(document == null)
      return false;

    var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
    var proc = ParseFile(ts, document.uri, ms);

    file2proc[uri] = proc;

    ANTLR_Processor.ProcessAll(file2proc, null, inc_path);

    document.Update(text, proc);
    return true;
  }

  public string ResolveImportPath(string docpath, string import, string ext)
  {
    var resolved_path = string.Empty;

    for(int i=0;i<inc_path.Count;++i)
    {
      string root = inc_path[i];

      string path = string.Empty;
      if(!Path.IsPathRooted(import))
      {
        var dir = Path.GetDirectoryName(docpath);
        path = Util.NormalizeFilePath(Path.Combine(dir, import) + ext);
        if(File.Exists(path))
        {
          resolved_path = path;
          break;
        }
      }
      else
      {
        path = Util.NormalizeFilePath(root + "/" + import + ext);
        if(File.Exists(path))
        {
          resolved_path = path;
          break;
        }
      }
    }
    
    return resolved_path;
  }
}

}
