using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Serilog;

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

  readonly object _syncRoot = new object();

  public bool Indexed { get; private set; }

  HashSet<string> _filesWithDiagnostics = new HashSet<string>();

  ILogger _logger;

  public void Init(Types ts, ProjectConf conf, ILogger logger = null)
  {
    Types = ts;
    ProjConf = conf;
    _logger = logger;
  }

  public void Shutdown()
  {
  }

  //NOTE: naive initial implementation
  public Task IndexFilesAsync(CancellationToken ct = default)
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

    ct.ThrowIfCancellationRequested();

    var proc_bundle = new ProjectCompilationStateBundle(Types);
    proc_bundle.file2proc = Path2Proc;
    //TODO: use compiled cache if needed
    proc_bundle.file2cached = null;
    ANTLR_Processor.ProcessAll(proc_bundle);

    ct.ThrowIfCancellationRequested();

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
    lock(_syncRoot)
    {
      if(Path2Doc.TryGetValue(uri.PathFixed(), out var document))
        return document;
    }
    return LoadDocument(uri);
  }

  public BHLDocument LoadDocument(DocumentUri uri)
  {
    byte[] buffer = File.ReadAllBytes(uri.PathFixed());
    string text = Encoding.UTF8.GetString(buffer);
    var document = new BHLDocument(uri);
    ParseDocument(document, text);
    return document;
  }

  public BHLDocument FindDocument(DocumentUri uri)
  {
    lock(_syncRoot)
    {
      Path2Doc.TryGetValue(uri.PathFixed(), out var document);
      return document;
    }
  }

  public BHLDocument FindDocument(string path)
  {
    lock(_syncRoot)
    {
      Path2Doc.TryGetValue(path, out var document);
      return document;
    }
  }

  public void OpenDocument(DocumentUri uri, string text)
  {
    var document = new BHLDocument(uri);
    ParseDocument(document, text);
  }

  public bool UpdateDocument(DocumentUri uri, string text)
  {
    BHLDocument document;
    lock(_syncRoot)
    {
      Path2Doc.TryGetValue(uri.PathFixed(), out document);
    }
    if(document == null)
      return false;

    ParseDocument(document, text);

    return true;
  }

  ANTLR_Processor ParseDocument(BHLDocument document, string text)
  {
    lock(_syncRoot)
    {
      var changed_path = document.Uri.PathFixed();

      foreach(var kv in Path2Proc)
      {
        if(kv.Key != changed_path)
          kv.Value.Reset();
      }

      var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
      var proc = ParseFile(changed_path, ms);
      Path2Proc[changed_path] = proc;

      var proc_bundle = new ProjectCompilationStateBundle(Types);
      proc_bundle.file2proc = Path2Proc;
      //TODO: use compiled cache if needed
      proc_bundle.file2cached = null;
      ANTLR_Processor.ProcessAll(proc_bundle);

      document.Update(text, proc);
      Path2Doc[changed_path] = document;
      return proc;
    }
  }

  public Dictionary<string, List<Diagnostic>> GetDiagnosticsToPublish()
  {
    var errors = GetCompileErrors();
    var result = errors.GetDiagnostics();

    //NOTE: send empty diagnostics for files that had errors before but now don't
    foreach(var file in _filesWithDiagnostics)
    {
      if(!result.ContainsKey(file))
        result[file] = new List<Diagnostic>();
    }

    _filesWithDiagnostics.Clear();
    foreach(var kv in result)
    {
      if(kv.Value.Count > 0)
        _filesWithDiagnostics.Add(kv.Key);
    }

    return result;
  }

  public Dictionary<string, CompileErrors> GetCompileErrors(bool filter_empty = false)
  {
    lock(_syncRoot)
    {
      var uri2errs = new Dictionary<string, CompileErrors>();
      foreach(var kv in Path2Proc)
      {
        if(!filter_empty || kv.Value.result.errors.Count > 0)
          uri2errs[kv.Key] = kv.Value.result.errors;
      }
      return uri2errs;
    }
  }

  public List<Location> FindRefs(Symbol symb)
  {
    var refs = new List<Location>();

    lock(_syncRoot)
    {
      foreach(var kv in Path2Proc)
      {
        foreach(var anKv in kv.Value.annotated_nodes)
        {
          if(anKv.Value.lsp_symbol == symb)
            refs.Add(new Location
            {
              Uri = DocumentUri.File(kv.Key),
              Range = anKv.Value.range.FromAntlr2LspRange()
            });
        }
      }
    }

    refs.Sort((a, b) =>
    {
      if(a.Uri.Path == b.Uri.Path)
        return a.Range.Start.Line.CompareTo(b.Range.Start.Line);
      else
        return a.Uri.Path.CompareTo(b.Uri.Path);
    });

    if(symb is FuncSymbolNative)
    {
      refs.Add(new Location
      {
        Uri = DocumentUri.File(symb.origin.source_file),
        Range = symb.origin.source_range.FromAntlr2LspRange()
      });
    }

    return refs;
  }
}
