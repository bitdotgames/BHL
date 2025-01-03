using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace bhl.lsp {

public class Workspace
{
  public Types ts;

  public ProjectConf conf;

  public event System.Func<Dictionary<string, CompileErrors>, Task> OnDiagnostics;

  //NOTE: keeping both collections for convenience of re-indexing
  Dictionary<string, ANTLR_Processor> uri2proc = new Dictionary<string, ANTLR_Processor>(); 
  public Dictionary<string, BHLDocument> uri2doc { get ; private set; } = new Dictionary<string, BHLDocument>();

  public lsp.proto.ClientCapabilities capabilities { get; private set; }

  public void Init(Types ts, ProjectConf conf)
  {
    this.ts = ts;
    this.conf = conf;
  }
  
  public void Shutdown()
  {}

  //NOTE: naive initial implementation
  public void IndexFiles()
  {
    uri2proc.Clear();
    uri2doc.Clear();

    for(int i=0;i<conf.src_dirs.Count;++i)
    {
      var src_dir = conf.src_dirs[i];

      var files = Directory.GetFiles(src_dir, "*.bhl", SearchOption.AllDirectories);
      foreach(var file in files)
      {
        string norm_file = BuildUtils.NormalizeFilePath(file);

        using(var sfs = File.OpenRead(norm_file))
        {
          var proc = ParseFile(norm_file, sfs);
          uri2proc.Add(norm_file, proc);
        }
      }
    }

    var proc_bundle = new ProjectCompilationStateBundle(ts);
    proc_bundle.file2proc = uri2proc;
    //TODO: use compiled cache if needed
    proc_bundle.file2cached = null;
    
    ANTLR_Processor.ProcessAll(proc_bundle);

    CheckDiagnostics();

    foreach(var kv in uri2proc)
    {
      var document = new BHLDocument(new proto.Uri(kv.Key)); 
      document.Update(File.ReadAllText(kv.Key), kv.Value);
      uri2doc.Add(kv.Key, document);
    }
  }

  ANTLR_Processor ParseFile(string file, Stream stream)
  {
    var imports = CompilationExecutor.ParseWorker.ParseMaybeImports(conf.inc_path, file, stream);
    var module = new bhl.Module(ts, conf.inc_path.FilePath2ModuleName(file), file);

    //TODO: use different error handlers?
    var err_hub = CompileErrorsHub.MakeStandard(file);
    
    var proc = ANTLR_Processor.ParseAndMakeProcessor(
      module, 
      imports, 
      stream, 
      ts, 
      err_hub,
      new HashSet<string>(conf.defines),
      out var _
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
    var proc = ParseFile(document.uri.path, ms);

    uri2proc[document.uri.path] = proc;

    var proc_bundle = new ProjectCompilationStateBundle(ts);
    proc_bundle.file2proc = uri2proc;
    //TODO: use compiled cache if needed
    proc_bundle.file2cached = null;
    
    ANTLR_Processor.ProcessAll(proc_bundle);

    CheckDiagnostics();

    document.Update(text, proc);
    return true;
  }

  void CheckDiagnostics()
  {
    var uri2errs = new Dictionary<string, CompileErrors>();
    foreach(var kv in uri2proc)
      uri2errs[kv.Key] = kv.Value.result.errors;

    OnDiagnostics?.Invoke(uri2errs);
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
