using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace bhl.lsp;

public class Workspace
{
  public Types Types { get; private set; }

  public ProjectConf ProjConf { get; private set; }

  public event System.Action<Dictionary<string, CompileErrors>> OnDiagnostics;

  //NOTE: keeping both collections for convenience of re-indexing
  Dictionary<string, ANTLR_Processor> _uri2proc = new Dictionary<string, ANTLR_Processor>();
  public Dictionary<string, BHLDocument> Uri2Doc { get ; private set; } = new Dictionary<string, BHLDocument>();

  public bool Indexed { get; private set; }

  public void Init(Types ts, ProjectConf conf)
  {
    this.Types = ts;
    this.ProjConf = conf;
  }

  public void Shutdown()
  {
  }

  //NOTE: naive initial implementation
  public void IndexFiles()
  {
    Indexed = true;

    _uri2proc.Clear();
    Uri2Doc.Clear();

    for(int i = 0; i < ProjConf.src_dirs.Count; ++i)
    {
      var src_dir = ProjConf.src_dirs[i];

      var files = Directory.GetFiles(src_dir, "*.bhl", SearchOption.AllDirectories);
      foreach(var file in files)
      {
        string norm_file = BuildUtils.NormalizeFilePath(file);

        using(var sfs = File.OpenRead(norm_file))
        {
          var proc = ParseFile(norm_file, sfs);
          _uri2proc.Add(norm_file, proc);
        }
      }
    }

    var proc_bundle = new ProjectCompilationStateBundle(Types);
    proc_bundle.file2proc = _uri2proc;
    //TODO: use compiled cache if needed
    proc_bundle.file2cached = null;

    ANTLR_Processor.ProcessAll(proc_bundle);

    CheckDiagnostics();

    foreach(var kv in _uri2proc)
    {
      var document = new BHLDocument(kv.Key);
      document.Update(File.ReadAllText(kv.Key), kv.Value);
      Uri2Doc.Add(kv.Key, document);
    }
  }

  ANTLR_Processor ParseFile(string file, Stream stream)
  {
    var imports = CompilationExecutor.ParseWorker.ParseMaybeImports(ProjConf.inc_path, file, stream);
    var module = new bhl.Module(Types, ProjConf.inc_path.FilePath2ModuleName(file), file);

    //TODO: use different error handlers?
    var err_hub = CompileErrorsHub.MakeStandard(file);

    var proc = ANTLR_Processor.ParseAndMakeProcessor(
      module,
      imports,
      stream,
      Types,
      err_hub,
      new HashSet<string>(ProjConf.defines),
      out var _
    );

    return proc;
  }

  public BHLDocument GetOrLoadDocument(DocumentUri uri)
  {
    BHLDocument document;
    if(Uri2Doc.TryGetValue(uri.Path, out document))
      return document;
    else
      return LoadDocument(uri);
  }

  public BHLDocument LoadDocument(DocumentUri uri)
  {
    byte[] buffer = File.ReadAllBytes(uri.Path);
    string text = Encoding.UTF8.GetString(buffer);
    return OpenDocument(uri, text);
  }

  public BHLDocument OpenDocument(DocumentUri uri, string text)
  {
    BHLDocument document;
    if(!Uri2Doc.TryGetValue(uri.Path, out document))
    {
      document = new BHLDocument(uri);
      Uri2Doc.Add(uri.Path, document);
    }

    return document;
  }

  public BHLDocument FindDocument(DocumentUri uri)
  {
    return FindDocument(uri);
  }

  public BHLDocument FindDocument(string path)
  {
    BHLDocument document;
    Uri2Doc.TryGetValue(path, out document);
    return document;
  }

  public bool UpdateDocument(DocumentUri uri, string text)
  {
    var document = FindDocument(uri);
    if(document == null)
      return false;

    var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
    var proc = ParseFile(document.Uri.Path, ms);

    _uri2proc[document.Uri.Path] = proc;

    var proc_bundle = new ProjectCompilationStateBundle(Types);
    proc_bundle.file2proc = _uri2proc;
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
    foreach(var kv in _uri2proc)
      uri2errs[kv.Key] = kv.Value.result.errors;

    OnDiagnostics?.Invoke(uri2errs);
  }

  public List<AnnotatedParseTree> FindReferences(Symbol symb)
  {
    var refs = new List<AnnotatedParseTree>();
    foreach(var doc_kv in Uri2Doc)
    {
      foreach(var node_kv in doc_kv.Value.Processed.annotated_nodes)
        if(node_kv.Value.lsp_symbol == symb)
          refs.Add(node_kv.Value);
    }

    return refs;
  }
}
