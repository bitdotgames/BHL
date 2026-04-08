using System.Collections.Generic;
using Antlr4.Runtime.Tree;
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
      if(Path2Doc.TryGetValue(uri.PathNormalized(), out var document))
        return document;
    }
    return LoadDocument(uri);
  }

  public BHLDocument LoadDocument(DocumentUri uri)
  {
    byte[] buffer = File.ReadAllBytes(uri.PathNormalized());
    string text = Encoding.UTF8.GetString(buffer);
    var document = new BHLDocument(uri);
    ParseDocument(document, text);
    return document;
  }

  public BHLDocument FindDocument(DocumentUri uri)
  {
    lock(_syncRoot)
    {
      Path2Doc.TryGetValue(uri.PathNormalized(), out var document);
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
      Path2Doc.TryGetValue(uri.PathNormalized(), out document);
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
      var changed_path = document.Uri.PathNormalized();

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
    lock(_syncRoot)
    {
      var uri2errs = new Dictionary<string, CompileErrors>();
      foreach(var kv in Path2Proc)
        uri2errs[kv.Key] = kv.Value.result.errors;

      var result = uri2errs.GetDiagnostics();

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

  public List<CompletionItem> GetCompletions(DocumentUri uri, Position position, string trigger_character)
  {
    lock(_syncRoot)
    {
      var path = uri.PathNormalized();
      if(!Path2Proc.TryGetValue(path, out var proc))
        return new List<CompletionItem>();

      var items = new List<CompletionItem>();
      var seen = new HashSet<Symbol>();

      if(trigger_character == "." && Path2Doc.TryGetValue(path, out var document))
      {
        var scope = GetScopeBeforeDot(document, position);
        if(scope != null)
        {
          AddMemberCompletions(items, seen, scope);
          return items;
        }
      }

      foreach(var sym in proc.module.ns)
        AddCompletionItem(items, seen, sym);

      foreach(var sym in Types.ns)
        AddCompletionItem(items, seen, sym);

      return items;
    }
  }

  static IScope GetScopeBeforeDot(BHLDocument document, Position pos)
  {
    // dot is at col-1; collect tokens on this line before it and resolve the chain
    if(pos.Character < 1)
      return null;

    return ResolveChainBeforeDot(document, pos.Line, pos.Character - 1);
  }

  // Collects identifier tokens before `dot_column` on the given line, reconstructs
  // the access chain (e.g. ["foo","bar"] from "foo.bar." or ["MakeFoo",true] from "MakeFoo()."),
  // and resolves it to a scope using annotations and the module namespace.
  static IScope ResolveChainBeforeDot(BHLDocument document, int line, int dot_column)
  {
    // Gather tokens on this line that end before the trailing dot, sorted by column.
    var line_tokens = new List<TerminalNodeImpl>();
    foreach(var t in document.TermNodes)
    {
      int t_line = t.Symbol.Line - 1; // ANTLR lines are 1-based
      if(t_line == line && t.Symbol.Column < dot_column)
        line_tokens.Add(t);
    }
    if(line_tokens.Count == 0)
      return null;

    line_tokens.Sort((a, b) => a.Symbol.Column.CompareTo(b.Symbol.Column));

    // Build chain: walk tokens right-to-left collecting (name, is_call) parts
    // separated by dots.  Stop when we hit something that isn't a NAME/dot/paren.
    var chain = new List<(string name, bool is_call)>();
    int i = line_tokens.Count - 1;

    while(i >= 0)
    {
      var tok = line_tokens[i];
      var txt = tok.GetText();

      // Consume a possible "(args...)" call suffix before the name
      bool is_call = false;
      if(txt == ")")
      {
        int depth = 1;
        i--;
        while(i >= 0 && depth > 0)
        {
          var t = line_tokens[i].GetText();
          if(t == ")") depth++;
          else if(t == "(") depth--;
          i--;
        }
        if(depth != 0) break; // unbalanced
        is_call = true;
        if(i < 0) break;
        tok = line_tokens[i];
        txt = tok.GetText();
      }

      // Expect a NAME token
      if(txt == "." || txt == "(" || txt == ")")
        break;

      chain.Insert(0, (txt, is_call));
      i--;

      // Expect a dot before the next part (or we're at the root)
      if(i >= 0 && line_tokens[i].GetText() == ".")
        i--;
      else
        break;
    }

    if(chain.Count == 0)
      return null;

    // Resolve each part of the chain
    IScope curr_scope = null;

    for(int c = 0; c < chain.Count; c++)
    {
      var (name, is_call) = chain[c];

      Symbol sym;
      if(c == 0)
      {
        // Root: check local declarations first, then module namespace
        sym = FindSymbolByName(document, name)
              ?? document.Processed.module.ns.ResolveWithFallback(name);
      }
      else
      {
        if(curr_scope == null)
          return null;
        sym = curr_scope.ResolveRelatedOnly(name);
      }

      curr_scope = ScopeFromSymbol(sym, is_call);
      if(curr_scope == null)
        return null;
    }

    return curr_scope;
  }

  // Extracts an IScope from a symbol, taking into account whether it's being called.
  static IScope ScopeFromSymbol(Symbol sym, bool is_call)
  {
    if(sym == null)
      return null;
    if(is_call && sym is FuncSymbol fs)
      return fs.GetReturnType() as IScope;
    if(sym is IScope direct)
      return direct;
    if(sym is VariableSymbol vs)
      return vs.type.Get() as IScope;
    return null;
  }

  // Finds the first annotated declaration of `name` in the document and returns its scope type.
  static Symbol FindSymbolByName(BHLDocument document, string name)
  {
    foreach(var kv in document.Processed.annotated_nodes)
    {
      var ann = kv.Value;
      if(ann.lsp_symbol?.name == name)
        return ann.lsp_symbol;
    }
    return null;
  }

  static void AddMemberCompletions(List<CompletionItem> items, HashSet<Symbol> seen, IScope scope)
  {
    if(scope is IEnumerable<Symbol> ies)
      foreach(var sym in ies)
        AddCompletionItem(items, seen, sym);
  }

  static void AddCompletionItem(List<CompletionItem> items, HashSet<Symbol> seen, Symbol sym)
  {
    if(!seen.Add(sym))
      return;

    items.Add(new CompletionItem
    {
      //TODO:?
      //Label = sym.GetFullTypePath().ToString(),
      Label = sym.name,
      Kind = GetCompletionKind(sym),
      Detail = sym.ToString(),
    });
  }

  static CompletionItemKind GetCompletionKind(Symbol sym) => sym switch
  {
    FuncSymbol       => CompletionItemKind.Function,
    ClassSymbol      => CompletionItemKind.Class,
    InterfaceSymbol  => CompletionItemKind.Interface,
    EnumSymbol       => CompletionItemKind.Enum,
    EnumItemSymbol   => CompletionItemKind.EnumMember,
    Namespace        => CompletionItemKind.Module,
    _                => CompletionItemKind.Variable,
  };

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
