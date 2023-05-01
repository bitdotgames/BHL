using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace bhl.lsp {

public class Workspace
{
  public Logger logger { get; private set; }

  Types ts;

  IncludePath inc_path;

  Dictionary<string, ANTLR_Processor> file2proc = new Dictionary<string, ANTLR_Processor>(); 

  public Dictionary<string, BHLDocument> uri2doc { get ; private set; } = new Dictionary<string, BHLDocument>();

  public bool declarationLinkSupport;
  public bool definitionLinkSupport;
  public bool typeDefinitionLinkSupport;
  public bool implementationLinkSupport;

  public Workspace(Logger logger)
  {
    this.logger = logger;
  }

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
    uri2doc.Clear();

    for(int i=0;i<inc_path.Count;++i)
    {
      var path = inc_path[i];

      var files = Directory.GetFiles(path, "*.bhl", SearchOption.AllDirectories);
      foreach(var file in files)
      {
        string norm_file = Util.NormalizeFilePath(file);

        using(var sfs = File.OpenRead(norm_file))
        {
          var proc = ParseFile(ts, norm_file, sfs);
          file2proc.Add(norm_file, proc);
        }
      }
    }

    //TODO: use compiled cache if needed
    ANTLR_Processor.ProcessAll(file2proc, null, inc_path);

    foreach(var kv in file2proc)
    {
      var document = new BHLDocument(new proto.Uri(kv.Key)); 
      document.Update(File.ReadAllText(kv.Key), kv.Value);
      uri2doc.Add(kv.Key, document);
    }
  }

  ANTLR_Processor ParseFile(Types ts, string file, Stream stream)
  {
    var imports = CompilationExecutor.ParseWorker.ParseImports(inc_path, file, stream);
    var module = new bhl.Module(ts, inc_path.FilePath2ModuleName(file), file);

    var errors = new CompileErrors();

    var parser = ANTLR_Processor.Stream2Parser(file, stream, null/*TODO*/);

    var parsed = new ANTLR_Parsed(parser);

    var proc = ANTLR_Processor.MakeProcessor(
      module, 
      imports, 
      parsed, 
      ts, 
      errors, 
      err_handlers: null/*TODO*/
    );

    return proc;
  }

  public BHLDocument GetOrLoadDocument(proto.Uri uri)
  {
    BHLDocument document;
    if(uri2doc.TryGetValue(uri.path, out document))
      return document;
    else
      return LoadDocument(uri);
  }

  public BHLDocument LoadDocument(proto.Uri uri)
  {
    byte[] buffer = File.ReadAllBytes(uri.path);
    string text = Encoding.UTF8.GetString(buffer);
    return OpenDocument(uri, text);
  }

  public BHLDocument OpenDocument(proto.Uri uri, string text)
  {
    BHLDocument document;
    if(!uri2doc.TryGetValue(uri.path, out document))
    {
      document = new BHLDocument(uri);  
      uri2doc.Add(uri.path, document);
    }
    return document;
  }
  
  public BHLDocument FindDocument(proto.Uri uri)
  {
    return FindDocument(uri.path);
  }
  
  public BHLDocument FindDocument(string path)
  {
    BHLDocument document;
    uri2doc.TryGetValue(path, out document);
    return document;
  }

  public bool UpdateDocument(proto.Uri uri, string text)
  {
    var document = FindDocument(uri);
    if(document == null)
      return false;

    var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
    var proc = ParseFile(ts, document.uri.path, ms);

    file2proc[document.uri.path] = proc;

    ANTLR_Processor.ProcessAll(file2proc, null, inc_path);

    document.Update(text, proc);
    return true;
  }

  public List<AnnotatedParseTree> FindReferences(Symbol symb)
  {
    var refs = new List<AnnotatedParseTree>(); 
    foreach(var doc_kv in uri2doc)
    {
      foreach(var node_kv in doc_kv.Value.proc.annotated_nodes)
        if(node_kv.Value.lsp_symbol == symb)
          refs.Add(node_kv.Value);
    }
    return refs;
  }
}

}
