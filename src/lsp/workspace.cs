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

  //TODO: do we need this one?
  //public event System.Action<Dictionary<string, CompileErrors>> OnCompileErrors;

  //NOTE: keeping both collections for convenience of re-indexing
  public Dictionary<string, ANTLR_Processor> Path2Proc { get ; private set; } = new ();
  public Dictionary<string, BHLDocument> Path2Doc { get ; private set; } = new ();

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
  public Task IndexFiles()
  {
    Indexed = true;

    Path2Proc.Clear();
    Path2Doc.Clear();

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
          Path2Proc.Add(norm_file, proc);
        }
      }
    }

    var proc_bundle = new ProjectCompilationStateBundle(Types);
    proc_bundle.file2proc = Path2Proc;
    //TODO: use compiled cache if needed
    proc_bundle.file2cached = null;

    ANTLR_Processor.ProcessAll(proc_bundle);

    foreach(var kv in Path2Proc)
    {
      var document = new BHLDocument(kv.Key);
      document.Update(File.ReadAllText(kv.Key), kv.Value);
      Path2Doc.Add(kv.Key, document);
    }
    return Task.CompletedTask;
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
    if(Path2Doc.TryGetValue(uri.Path, out var document))
      return document;
    else
      return LoadDocument(uri);
  }

  public BHLDocument LoadDocument(DocumentUri uri)
  {
    byte[] buffer = File.ReadAllBytes(uri.Path);
    string text = Encoding.UTF8.GetString(buffer);
    var document = new BHLDocument(uri);
    ParseDocument(document, text);
    return document;
  }

  public BHLDocument FindDocument(DocumentUri uri)
  {
    Path2Doc.TryGetValue(uri.Path, out var document);
    return document;
  }

  public BHLDocument FindDocument(string path)
  {
    Path2Doc.TryGetValue(path, out var document);
    return document;
  }

  public void OpenDocument(DocumentUri uri, string text)
  {
    var document = new BHLDocument(uri);
    ParseDocument(document, text);
  }

  public bool UpdateDocument(DocumentUri uri, string text)
  {
    var document = FindDocument(uri);
    if(document == null)
      return false;

    ParseDocument(document, text);

    return true;
  }

  ANTLR_Processor ParseDocument(BHLDocument document, string text)
  {
    var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
    var proc = ParseFile(document.Uri.Path, ms);
    Path2Proc[document.Uri.Path] = proc;

    var proc_bundle = new ProjectCompilationStateBundle(Types);
    proc_bundle.file2proc = Path2Proc;
    //TODO: use compiled cache if needed
    proc_bundle.file2cached = null;
    ANTLR_Processor.ProcessAll(proc_bundle);

    document.Update(text, proc);
    return proc;
  }

  public Dictionary<string, CompileErrors> GetCompileErrors()
  {
    var uri2errs = new Dictionary<string, CompileErrors>();
    foreach(var kv in Path2Proc)
      uri2errs[kv.Key] = kv.Value.result.errors;
    return uri2errs;
  }

  public List<AnnotatedParseTree> FindReferences(Symbol symb)
  {
    var refs = new List<AnnotatedParseTree>();
    foreach(var doc_kv in Path2Doc)
    {
      foreach(var node_kv in doc_kv.Value.Processed.annotated_nodes)
        if(node_kv.Value.lsp_symbol == symb)
          refs.Add(node_kv.Value);
    }

    return refs;
  }
}
