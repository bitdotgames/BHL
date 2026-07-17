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

  //NOTE: keeping both collections for convenience of re-indexing
  public Dictionary<string, ANTLR_Processor> Path2Proc { get ; private set; } = new ();
  public Dictionary<string, BHLDocument> Path2Doc { get ; private set; } = new ();


  readonly object _syncRoot = new object();

  public bool Indexed { get; private set; }

  public int IndexedFileCount => Path2Proc.Count;

  HashSet<string> _filesWithDiagnostics = new HashSet<string>();

  ILogger _logger;

  public event System.Action BindingsDllChanged;

  System.IO.FileSystemWatcher _bindingsWatcher;

  public void Init(Types ts, ProjectConf conf, ILogger logger = null)
  {
    Types = ts;
    ProjConf = conf;
    _logger = logger;
    WatchBindingsDll(conf.bindings_dll);
  }

  void WatchBindingsDll(string path)
  {
    _bindingsWatcher?.Dispose();
    _bindingsWatcher = null;

    if(string.IsNullOrEmpty(path) || !File.Exists(path))
      return;

    _bindingsWatcher = new System.IO.FileSystemWatcher(
      Path.GetDirectoryName(path),
      Path.GetFileName(path))
    {
      NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.Size,
      EnableRaisingEvents = true,
    };
    // External build tools rarely rewrite the DLL in place (which would raise Changed);
    // they typically build to a temp file and atomically replace the destination (rename-over,
    // or delete+recreate), which raises Renamed/Created instead. Watch all three so a rebuild
    // by an external process is detected the same way an in-place `touch` is.
    _bindingsWatcher.Changed += (_, _) => BindingsDllChanged?.Invoke();
    _bindingsWatcher.Created += (_, _) => BindingsDllChanged?.Invoke();
    _bindingsWatcher.Renamed += (_, _) => BindingsDllChanged?.Invoke();
  }

  public void Shutdown()
  {
    _bindingsWatcher?.Dispose();
    _bindingsWatcher = null;
  }

  public Task ReloadAsync(CancellationToken ct = default)
    => ReloadAsync(ProjConf, ct);

  public async Task ReloadAsync(ProjectConf proj, CancellationToken ct = default)
  {
    var new_types = new Types();
    proj.LoadBindings().Register(new_types);
    Init(new_types, proj, _logger);
    await IndexFilesAsync(ct);
  }

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

    ANTLR_Processor.ProcessAll(BuildBundle());

    ct.ThrowIfCancellationRequested();

    foreach(var kv in Path2Proc)
    {
      var document = new BHLDocument(kv.Key);
      document.Update(File.ReadAllText(kv.Key), kv.Value);
      Path2Doc.Add(kv.Key, document);
    }

    return Task.CompletedTask;
  }

  string GetCompiledCacheFile(string file)
  {
    if(string.IsNullOrEmpty(ProjConf.tmp_dir))
      return null;
    return CompilationExecutor.GetCompiledCacheFile(ProjConf.tmp_dir, file);
  }

  ProjectCompilationStateBundle BuildBundle()
    => new (Types, Path2Proc);

  ANTLR_Processor ParseFile(string file, Stream stream)
  {
    var imports = CompilationExecutor.ParseWorker.ParseMaybeImports(ProjConf.inc_path, file, stream);
    var module = new bhl.ModuleDeclared(ProjConf.inc_path.FilePath2ModuleName(file), file);

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

      // Parse the changed file first to get its updated module name and parse tree.
      var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
      var proc = ParseFile(changed_path, ms);

      // Identify direct importers of the changed module BEFORE any Reset(),
      // because Reset() clears each processor's imports list.
      var affected = FindAffectedFiles(changed_path, proc.module.name);

      // Register the new processor and reset only the affected importers.
      // Unaffected processors are left intact — their previous results remain valid.
      Path2Proc[changed_path] = proc;
      foreach(var kv in Path2Proc)
      {
        if(kv.Key != changed_path && affected.Contains(kv.Key))
          kv.Value.Reset();
      }

      // Build a partial bundle: affected files go into file2proc (will run all phases);
      // unaffected files go into file2cached (modules available for import resolution only).
      var bundle = new ProjectCompilationStateBundle(Types);
      foreach(var kv in Path2Proc)
      {
        if(affected.Contains(kv.Key))
          bundle.file2proc.Add(kv.Key, kv.Value);
        else
          bundle.file2cached.Add(kv.Key, kv.Value.module);
      }

      ANTLR_Processor.ProcessAll(bundle);

      document.Update(text, proc);
      Path2Doc[changed_path] = document;
      return proc;
    }
  }

  HashSet<string> FindAffectedFiles(string changed_path, string changed_module_name)
  {
    var affected = new HashSet<string> { changed_path };
    foreach(var kv in Path2Proc)
    {
      if(kv.Key == changed_path)
        continue;
      foreach(var imported in kv.Value.imports)
      {
        if(imported.name == changed_module_name)
        {
          affected.Add(kv.Key);
          break;
        }
      }
    }
    return affected;
  }

  public Dictionary<string, List<Diagnostic>> GetDiagnosticsToPublish()
  {
    lock(_syncRoot)
    {
      var uri2errs = new Dictionary<string, CompileErrors>();
      var uri2warns = new Dictionary<string, CompileWarnings>();
      foreach(var kv in Path2Proc)
      {
        uri2errs[kv.Key] = kv.Value.result.errors;
        uri2warns[kv.Key] = kv.Value.result.warnings;
      }

      var result = uri2errs.GetDiagnostics(uri2warns);

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

  static readonly System.Text.RegularExpressions.Regex _not_resolved_re =
    new System.Text.RegularExpressions.Regex(@"symbol '(.+?)' not resolved");

  public List<TextEdit> GetMissingImportEdits(DocumentUri uri)
  {
    lock(_syncRoot)
    {
      var path = uri.PathNormalized();
      if(!Path2Proc.TryGetValue(path, out var proc) || proc.result == null)
        return null;

      // Collect unique symbol names from "symbol 'X' not resolved" errors
      var missing = new HashSet<string>();
      foreach(var err in proc.result.errors)
      {
        var m = _not_resolved_re.Match(err.text);
        if(m.Success)
          missing.Add(m.Groups[1].Value);
      }

      if(missing.Count == 0)
        return null;

      if(!Path2Doc.TryGetValue(path, out var document))
        return null;

      var import_map = BuildImportEditsMap(document);

      var edits = new List<TextEdit>();
      var added = new HashSet<string>();

      foreach(var sym_name in missing)
      {
        foreach(var kv in Path2Proc)
        {
          var mod_name = kv.Value.module.name;
          if(!import_map.TryGetValue(mod_name, out var edit) || added.Contains(mod_name))
            continue;

          if(kv.Value.module.ns.members.Find(sym_name) != null)
          {
            edits.Add(edit);
            added.Add(mod_name);
            break;
          }
        }
      }

      return edits.Count > 0 ? edits : null;
    }
  }

  public List<TextEdit> GetUnusedImportEdits(DocumentUri uri)
  {
    lock(_syncRoot)
    {
      var path = uri.PathNormalized();
      if(!Path2Proc.TryGetValue(path, out var proc))
        return null;

      var edits = new List<TextEdit>();
      foreach(var warn in proc.result.warnings)
      {
        if(!warn.text.StartsWith("Unused import"))
          continue;
        // warn.range.start.line is 1-based (ANTLR); convert to 0-based LSP
        int lsp_line = warn.range.start.line - 1;
        edits.Add(new TextEdit
        {
          Range = new Range(new Position(lsp_line, 0), new Position(lsp_line + 1, 0)),
          NewText = "",
        });
      }
      return edits.Count > 0 ? edits : null;
    }
  }

  public List<CompletionItem> GetCompletions(DocumentUri uri, Position position, string trigger_character)
  {
    lock(_syncRoot)
    {
      var path = uri.PathNormalized();
      if(!Path2Proc.TryGetValue(path, out var _))
        return new List<CompletionItem>();

      var items = new List<CompletionItem>();
      var seen = new HashSet<Symbol>();

      if(Path2Doc.TryGetValue(path, out var document) && IsInsideString(document, position))
      {
        if(IsInsideImportString(document, position))
          return GetImportStringCompletions(document);
        return items;
      }

      // Build auto-import edits for source modules not yet imported in the current document.
      // Computed before the dot-trigger branch so both paths can attach them.
      Dictionary<string, TextEdit> import_edits = document != null
        ? BuildImportEditsMap(document)
        : null;

      // Sublime Text (and some other clients) omit the context field entirely,
      // so trigger_character is null even when the user just typed '.'.
      // Fall back to inspecting the character immediately before the cursor.
      if(trigger_character == null && document != null)
      {
        int idx = document.Index.CalcByteIndex(position.Line, position.Character - 1);
        if(idx >= 0 && idx < document.Text.Length && document.Text[idx] == '.')
          trigger_character = ".";
      }

      if(trigger_character == "." && document != null)
      {
        var (scope, static_only) = GetScopeBeforeDot(document, position, Path2Proc);
        AddMemberCompletions(items, seen, scope, static_only, import_edits);
        return items;
      }

      // Offer symbols from every module in the workspace, not just the current one and its imports.
      // A namespace can be split across multiple files, so deduplicate by qualified label.
      var seenLabels = new HashSet<string>();

      foreach(var kv in Path2Proc)
        AddModuleNsCompletions(kv.Value.module.ns, "", items, seen, seenLabels, import_edits);

      //foreach(var kv in _path2cached)
      //  AddModuleNsCompletions(kv.Value.ns, "", items, seen, seenLabels, import_edits);

      // Native modules (std, std.io, etc.) are registered in Types but not linked
      // into any source module namespace — add their top-level symbols explicitly.
      foreach(var m in Types.GetModules())
        AddModuleNsCompletions(m.ns, "", items, seen, seenLabels, import_edits);

      foreach(var sym in Types.ns)
        AddCompletionItem(items, seen, sym);

      if(document != null)
        AddLocalVarCompletions(document, position, items, seen);

      AddKeywordCompletions(items);

      return items;
    }
  }

  // Language keywords (grammar/bhlLexer.g) aren't symbols in any scope — e.g. 'yield' compiles
  // down to a call of the hidden native '$yield', and 'paral'/'paral_all'/'defer' are pure
  // syntax — so they're never offered by the symbol-based completion paths above. List them
  // explicitly so statement/expression-position completion still suggests them.
  static readonly string[] _keywords =
  {
    "if", "else", "while", "do", "for", "foreach", "in",
    "break", "continue", "return", "yield",
    "paral", "paral_all", "defer",
    "as", "is", "typeof", "new",
    "namespace", "class", "interface", "enum",
    "virtual", "override", "static", "coro", "func", "ref",
    "import", "null", "false", "true",
  };

  static void AddKeywordCompletions(List<CompletionItem> items)
  {
    foreach(var kw in _keywords)
      items.Add(new CompletionItem { Label = kw, Kind = CompletionItemKind.Keyword });
  }

  public SignatureHelp GetSignatureHelp(DocumentUri uri, Position position)
  {
    lock(_syncRoot)
    {
      var path = uri.PathNormalized();
      if(!Path2Doc.TryGetValue(path, out var document))
        return null;
      return FindSignatureHelp(document, position, Path2Proc);
    }
  }

  static SignatureHelp FindSignatureHelp(
    BHLDocument document, Position position, Dictionary<string, ANTLR_Processor> path2proc)
  {
    int cur_line = position.Line;     // 0-based (LSP)
    int cur_col  = position.Character;

    if(IsInsideString(document, position))
      return null;

    // Collect tokens strictly before the cursor, sorted forward by position.
    var tokens = new List<TerminalNodeImpl>();
    foreach(var t in document.TermNodes)
    {
      int t_line = t.Symbol.Line - 1; // ANTLR is 1-based
      if(t_line < cur_line || (t_line == cur_line && t.Symbol.Column < cur_col))
        tokens.Add(t);
    }
    if(tokens.Count == 0)
      return null;

    // Forward scan with a stack: push on '(', pop on ')'.
    // Each stack frame records the index of the '(' token and the running comma count.
    // At the end, the top of the stack is the innermost unmatched '(' — i.e., the call
    // the cursor is currently inside.  An empty stack means "not inside any call".
    var stack = new System.Collections.Generic.Stack<(int open_idx, int commas)>();
    int cur_commas = 0;

    for(int i = 0; i < tokens.Count; i++)
    {
      var txt = tokens[i].GetText();
      if(txt == "(")
      {
        stack.Push((i, cur_commas));
        cur_commas = 0;
      }
      else if(txt == ")")
      {
        if(stack.Count > 0)
          cur_commas = stack.Pop().commas; // restore outer comma count
      }
      else if(txt == "," && stack.Count > 0)
        cur_commas++;
    }

    if(stack.Count == 0)
      return null; // cursor is not inside any open call

    // cur_commas is the comma count at the innermost level = active parameter index.
    // The saved value in the stack frame is the outer level's count (used only for restore on pop).
    var (open_paren_idx, _) = stack.Peek();
    int active_param = cur_commas;

    // Token immediately before '(' should be the function name.
    int name_idx = open_paren_idx - 1;
    if(name_idx < 0)
      return null;
    var name_tok = tokens[name_idx].GetText();
    if(name_tok == "." || name_tok == ")" || name_tok == "(")
      return null;

    // Resolve the function symbol.  Two cases:
    //   1. Plain call:  "test1("  — resolve name in module scope / all modules.
    //   2. Member call: "foo.Bar(" — resolve chain up to '.' to get owner scope,
    //                               then resolve name inside that scope.
    Symbol sym;
    if(name_idx >= 2 && tokens[name_idx - 1].GetText() == ".")
    {
      // Collect the chain before the dot and resolve it to a scope
      int dot_col  = tokens[name_idx - 1].Symbol.Column;
      int dot_line = tokens[name_idx - 1].Symbol.Line - 1; // 0-based
      var (owner_scope, _) = ResolveChainBeforeDot(document, dot_line, dot_col, path2proc);
      sym = owner_scope?.ResolveRelatedOnly(name_tok);
    }
    else
    {
      sym = FindSymbolByName(document, name_tok)
            ?? document.Processed.module.ns.ResolveWithFallback(name_tok)
            ?? FindSymbolInAllModules(path2proc, document.Processed.types, name_tok);
    }

    if(sym is not FuncSymbol func_sym)
      return null;

    return BuildSignatureHelp(func_sym, active_param);
  }

  static SignatureHelp BuildSignatureHelp(FuncSymbol func_sym, int active_param)
  {
    int arg_count = func_sym.signature.arg_types.Count;

    // Build parameters using GetArg(i) which correctly skips the implicit 'this'
    // argument that non-static class methods store at members[0].
    var param_infos = new List<ParameterInformation>(arg_count);
    var param_labels = new List<string>(arg_count);
    for(int i = 0; i < arg_count; i++)
    {
      var arg = func_sym.GetArg(i);
      bool variadic = func_sym.signature.attribs.HasFlag(FuncSignatureAttrib.VariadicArgs)
                      && i == arg_count - 1;
      string param_label = (variadic ? "..." : "") + func_sym.signature.arg_types[i] + " " + arg.name;
      param_labels.Add(param_label);
      param_infos.Add(new ParameterInformation { Label = new ParameterInformationLabel(param_label) });
    }

    // Build the label string the same way FuncSymbol.ToString() does, but using
    // the correct arg names (via GetArg) rather than members[i] directly.
    string coro = func_sym.signature.attribs.HasFlag(FuncSignatureAttrib.Coro) ? "coro " : "";
    string label = $"{coro}func {func_sym.signature.return_type} {func_sym.name}({string.Join(",", param_labels)})";

    var sig = new SignatureInformation
    {
      Label = label,
      Parameters = new Container<ParameterInformation>(param_infos),
    };

    return new SignatureHelp
    {
      Signatures = new Container<SignatureInformation>(sig),
      ActiveSignature = 0,
      ActiveParameter = arg_count > 0 ? System.Math.Min(active_param, arg_count - 1) : 0,
    };
  }

  static bool IsInsideString(BHLDocument document, Position position)
  {
    int byte_idx = document.Index.CalcByteIndex(position.Line, position.Character);
    foreach(var t in document.TermNodes)
    {
      if(t.Symbol.Type == bhlLexer.NORMALSTRING &&
         t.Symbol.StartIndex <= byte_idx && t.Symbol.StopIndex >= byte_idx)
        return true;
    }
    return false;
  }

  static bool IsInsideImportString(BHLDocument document, Position position)
  {
    int byte_idx = document.Index.CalcByteIndex(position.Line, position.Character);
    var nodes = document.TermNodes;
    for(int i = 0; i < nodes.Count; i++)
    {
      var tok = nodes[i];
      if(tok.Symbol.Type != bhlLexer.NORMALSTRING)
        continue;
      if(tok.Symbol.StartIndex > byte_idx || tok.Symbol.StopIndex < byte_idx)
        continue;
      // cursor is inside this string — check if an IMPORT token precedes it on the same line
      int str_line = tok.Symbol.Line;
      for(int j = i - 1; j >= 0; j--)
      {
        var prev = nodes[j];
        if(prev.Symbol.Line != str_line)
          break;
        if(prev.Symbol.Type == bhlLexer.IMPORT)
          return true;
      }
    }
    return false;
  }

  List<CompletionItem> GetImportStringCompletions(BHLDocument document)
  {
    var (already_imported, _) = GetImportContext(document, ProjConf.inc_path);
    var curr_module = document.Processed?.module?.name ?? "";
    var items = new List<CompletionItem>();

    foreach(var kv in Path2Proc)
    {
      var mod_name = kv.Value.module.name;
      if(mod_name == curr_module || already_imported.Contains(mod_name))
        continue;
      items.Add(new CompletionItem
      {
        Label = mod_name,
        Kind = CompletionItemKind.Module,
        InsertText = mod_name,
      });
    }

    return items;
  }

  // Returns (scope, static_only): static_only=true when the identifier before the dot is a
  // class/type name — only static members should be offered in that case.
  static (IScope scope, bool static_only) GetScopeBeforeDot(
    BHLDocument document, Position pos, Dictionary<string, ANTLR_Processor> path2proc)
  {
    // dot is at col-1; collect tokens on this line before it and resolve the chain
    if(pos.Character < 1)
      return (null, false);

    return ResolveChainBeforeDot(document, pos.Line, pos.Character - 1, path2proc);
  }

  // Collects identifier tokens before `dot_column` on the given line, reconstructs
  // the access chain (e.g. ["foo","bar"] from "foo.bar." or ["MakeFoo",true] from "MakeFoo()."),
  // and resolves it to a scope using annotations and the module namespace.
  // Returns (scope, static_only): static_only=true when the last symbol was a class/type name
  // (meaning only static members should be offered).
  static (IScope scope, bool static_only) ResolveChainBeforeDot(
    BHLDocument document, int line, int dot_column, Dictionary<string, ANTLR_Processor> path2proc)
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
      return (null, false);

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
      return (null, false);

    // Resolve each part of the chain
    IScope curr_scope = null;
    bool static_only = false;

    for(int c = 0; c < chain.Count; c++)
    {
      var (name, is_call) = chain[c];

      Symbol sym;
      if(c == 0)
      {
        // Root: check local declarations first, then current module namespace, then all modules
        sym = FindSymbolByName(document, name)
              ?? document.Processed.module.ns.ResolveWithFallback(name)
              ?? FindSymbolInAllModules(path2proc, document.Processed.types, name);

        // Special case: 'this' inside an incomplete expression may not be annotated.
        // Fall back to scanning for the enclosing non-static class method at this line.
        if(sym == null && name == "this")
          sym = FindThisArgAtLine(document, line);
      }
      else
      {
        if(curr_scope == null)
          return (null, false);
        sym = curr_scope.ResolveRelatedOnly(name);
      }

      (curr_scope, static_only) = ScopeFromSymbol(sym, is_call);
      if(curr_scope == null)
        return (null, false);
    }

    return (curr_scope, static_only);
  }

  // Extracts an IScope from a symbol, and whether only static members should be shown.
  // static_only=true when sym is a class/type name used directly (not an instance variable).
  static (IScope scope, bool static_only) ScopeFromSymbol(Symbol sym, bool is_call)
  {
    if(sym == null)
      return (null, false);
    if(is_call && sym is FuncSymbol fs)
      return (fs.GetReturnType() as IScope, false);
    // Class name used directly → show only static members
    if(sym is ClassSymbol cs)
      return (cs, true);
    // Enum name used directly → all items are static-like, show all
    if(sym is IScope direct)
      return (direct, false);
    // Instance variable → show all members of its type
    if(sym is VariableSymbol vs)
      return (vs.type.Get() as IScope, false);
    return (null, false);
  }

  // Returns the 'this' FuncArgSymbol for the innermost non-static class method whose
  // source range contains `lsp_line` (0-based).  Returns null if none found.
  // Class method FuncSymbolScripts are NOT stored in annotated_nodes directly, so we
  // scan for ClassSymbolScript entries and walk their members.
  static Symbol FindThisArgAtLine(BHLDocument document, int lsp_line)
  {
    ClassSymbolScript best_class = null;
    int best_span = int.MaxValue;

    foreach(var kv in document.Processed.annotated_nodes)
    {
      if(kv.Value.lsp_symbol is not ClassSymbolScript class_sym)
        continue;

      // Check that the class's source range contains the cursor
      var class_range = class_sym.origin.source_range;
      int cls_start = class_range.start.line - 1;
      int cls_end   = class_range.end.line   - 1;
      if(lsp_line < cls_start || lsp_line > cls_end)
        continue;

      // Walk class members looking for the narrowest non-static method containing lsp_line
      foreach(var sym in class_sym)
      {
        if(sym is not FuncSymbolScript fsym)
          continue;
        if(!fsym.IsInstanceMethod())
          continue; // static method — skip

        // FuncSymbolScript.origin.parsed is the AnnotatedParseTree for the funcDecl ctx
        var fsym_range = fsym.origin.source_range;
        int fstart = fsym_range.start.line - 1;
        int fend   = fsym_range.end.line   - 1;

        if(lsp_line < fstart || lsp_line > fend)
          continue;

        int span = fend - fstart;
        if(span < best_span)
        {
          best_span = span;
          best_class = class_sym;
        }
      }
    }

    return best_class != null ? new FuncArgSymbol("this", new ProxyType(best_class)) : null;
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

  // Collects own symbols from a module namespace into the completion list, recursing into
  // sub-namespaces and emitting fully-qualified labels (e.g. "ns.NsFunc", "std.io.Write").
  Dictionary<string, TextEdit> BuildImportEditsMap(BHLDocument document)
  {
    var (already_imported, insert_pos) = GetImportContext(document, ProjConf.inc_path);
    var curr_module = document.Processed.module.name;
    var map = new Dictionary<string, TextEdit>();

    //script modules from the workspace
    foreach(var kv in Path2Proc)
    {
      var mod_name = kv.Value.module.name;
      if(mod_name == curr_module || already_imported.Contains(mod_name))
        continue;
      map[mod_name] = new TextEdit
      {
        Range = new Range(insert_pos, insert_pos),
        NewText = $"import \"{mod_name}\"\n",
      };
    }

    //native modules (std, std.io, etc.) registered in Types but not in Path2Proc
    foreach(var m in Types.GetModules())
    {
      var mod_name = m.name;
      if(string.IsNullOrEmpty(mod_name) || already_imported.Contains(mod_name))
        continue;
      map.TryAdd(mod_name, new TextEdit
      {
        Range = new Range(insert_pos, insert_pos),
        NewText = $"import \"{mod_name}\"\n",
      });
    }

    return map;
  }

  // Scans the document for existing `import "..."` statements.
  // Returns the set of already-imported module names and the position where a new
  // import line should be inserted (right after the last existing import, or line 0).
  static (HashSet<string> imported, Position insert_pos) GetImportContext(BHLDocument document, IncludePath inc_path)
  {
    var imported = new HashSet<string>();
    int last_import_line = -1; // 0-based LSP line of the last import keyword seen
    var self_path = document.Uri.PathNormalized();

    var nodes = document.TermNodes;
    for(int i = 0; i < nodes.Count; i++)
    {
      var tok = nodes[i];
      if(tok.Symbol.Type != bhlLexer.IMPORT)
        continue;

      int import_line = tok.Symbol.Line; // ANTLR 1-based
      last_import_line = import_line - 1; // convert to 0-based

      // NORMALSTRING (the quoted path) follows IMPORT on the same line.
      for(int j = i + 1; j < nodes.Count; j++)
      {
        var next = nodes[j];
        if(next.Symbol.Line != import_line)
          break;
        if(next.Symbol.Type == bhlLexer.NORMALSTRING)
        {
          var raw = next.GetText(); // e.g. "./utils" or "atf/utils" (includes quotes)
          if(raw.Length >= 2)
          {
            var import_str = raw.Substring(1, raw.Length - 2);
            // Normalize relative imports (e.g. "./utils") to module names (e.g. "atf/utils").
            try
            {
              inc_path.ResolvePath(self_path, import_str, out _, out var mod_name);
              imported.Add(mod_name);
            }
            catch { imported.Add(import_str); }
          }
          break;
        }
      }
    }

    // Insert after the last import line, or at the very top if there are none.
    var insert_pos = new Position(last_import_line + 1, 0);
    return (imported, insert_pos);
  }

  static void AddModuleNsCompletions(
    Namespace ns, string prefix, List<CompletionItem> items, HashSet<Symbol> seen, HashSet<string> seenLabels,
    Dictionary<string, TextEdit> import_edits = null)
  {
    foreach(var sym in ns.members)
    {
      if(sym is Namespace ns_sym)
      {
        if(ns_sym.IsLinkedShadow)
          continue;
        var label = prefix.Length > 0 ? prefix + "." + ns_sym.name : ns_sym.name;
        if(seenLabels.Add(label))
          AddCompletionItem(items, seen, ns_sym, label, import_edits);
        // Always recurse even if this namespace label was already seen — another module
        // fragment may contribute new sub-symbols under the same namespace name.
        AddModuleNsCompletions(ns_sym, label, items, seen, seenLabels, import_edits);
      }
      else
      {
        var label = prefix.Length > 0 ? prefix + "." + sym.name : sym.name;
        if(seenLabels.Add(label))
          AddCompletionItem(items, seen, sym, label, import_edits);
      }
    }
  }

  // Searches top-level own members of every module (BHL source + native registered modules).
  // When multiple modules contribute fragments of the same namespace, returns a temporary
  // merged Namespace so all fragments' members are visible in one scope.
  static Symbol FindSymbolInAllModules(Dictionary<string, ANTLR_Processor> path2proc, Types types, string name)
  {
    Symbol found = null;
    Namespace merged_ns = null;

    foreach(var kv in path2proc)
      CollectSymbolFromNs(kv.Value.module.ns, name, ref found, ref merged_ns);
    foreach(var m in types.GetModules())
      CollectSymbolFromNs(m.ns, name, ref found, ref merged_ns);

    return found;
  }

  static void CollectSymbolFromNs(Namespace ns, string name, ref Symbol found, ref Namespace merged_ns)
  {
    foreach(var sym in ns.members)
    {
      if(sym is Namespace ns_sym && ns_sym.IsLinkedShadow)
        continue;
      if(sym.name != name)
        continue;

      if(sym is Namespace ns_frag)
      {
        // Namespace can be split across modules — merge all fragments into one temporary scope
        if(merged_ns == null)
        {
          merged_ns = new Namespace(null, name);
          merged_ns.TryLink(ns_frag);
          found = merged_ns;
        }
        else
          merged_ns.TryLink(ns_frag);
      }
      else if(found == null)
        found = sym;
    }
  }

  static void AddLocalVarCompletions(
    BHLDocument document, Position position, List<CompletionItem> items, HashSet<Symbol> seen)
  {
    int lsp_line = position.Line;
    int lsp_col  = position.Character;

    foreach(var kv in document.Processed.annotated_nodes)
    {
      var sym = kv.Value.lsp_symbol;

      // Only local vars and func args; skip globals and class fields
      if(sym is not VariableSymbol || sym is GlobalVariableSymbol)
        continue;

      // Class fields have a ClassSymbol scope → FindEnclosingFuncSymbol() returns null
      var func = sym.scope?.FindEnclosingFuncSymbol();
      if(func == null)
        continue;

      // Cursor must be inside the enclosing function's source range (ANTLR 1-based → LSP 0-based)
      var func_range = func.origin.source_range;
      int func_start = func_range.start.line - 1;
      int func_end   = func_range.end.line   - 1;
      if(func_start < 0 || lsp_line < func_start || lsp_line > func_end)
        continue;

      // Symbol must be declared at or before the cursor position
      var decl_range = sym.origin.source_range;
      int decl_line = decl_range.start.line - 1;
      if(decl_line < 0)
        continue;
      if(decl_line > lsp_line || (decl_line == lsp_line && decl_range.start.column > lsp_col))
        continue;

      AddCompletionItem(items, seen, sym, sym.name);
    }
  }

  static void AddMemberCompletions(List<CompletionItem> items, HashSet<Symbol> seen, IScope scope, bool static_only = false, Dictionary<string, TextEdit> import_edits = null)
  {
    if(scope is IEnumerable<Symbol> ies)
      foreach(var sym in ies)
        if(static_only ? sym.IsStatic() : !sym.IsStatic())
          AddCompletionItem(items, seen, sym, sym.name, import_edits);
  }

  static void AddCompletionItem(List<CompletionItem> items, HashSet<Symbol> seen, Symbol sym)
    => AddCompletionItem(items, seen, sym, sym.name);

  static void AddCompletionItem(
    List<CompletionItem> items, HashSet<Symbol> seen, Symbol sym, string label,
    Dictionary<string, TextEdit> import_edits = null)
  {
    // '$'-prefixed names (e.g. '$yield', the hidden native backing the 'yield' keyword) are
    // compiler-internal — the NAME token can't start with '$' (grammar/bhlLexer.g), so BHL
    // source can never reference them by name. Keep them out of completions.
    if(sym.name != null && sym.name.StartsWith("$"))
      return;

    if(!seen.Add(sym))
      return;

    bool is_native = sym is FuncSymbolNative || sym is ClassSymbolNative
      || sym is EnumSymbolNative || sym is InterfaceSymbolNative;
    var module_name = GetSymbolModuleName(sym);
    var suffix = (is_native ? " [native]" : "") + (module_name != null ? $" [{module_name}]" : "");
    var detail = suffix.Length > 0 ? DescribeSymbolKind(sym) + suffix : DescribeSymbolKind(sym);

    TextEditContainer additional_edits = null;
    if(import_edits != null && module_name != null && import_edits.TryGetValue(module_name, out var import_edit))
      additional_edits = new TextEditContainer(import_edit);

    items.Add(new CompletionItem
    {
      Label = label,
      Kind = GetCompletionKind(sym),
      Detail = detail,
      AdditionalTextEdits = additional_edits,
    });
  }

  // Human-readable description of a symbol, used in both completion item details and hover text.
  // Symbol.ToString() only returns the bare name for class/enum/interface/namespace symbols,
  // so this prepends the kind keyword to make the meaning unambiguous (e.g. "enum EnumUnit"
  // rather than just "EnumUnit"). Does NOT include the "[native]"/"[module]" suffix used in
  // completion details — that's noise in a hover tooltip for a symbol already in view.
  public static string DescribeSymbolKind(Symbol sym)
  {
    // Strip any existing "[native] " prefix from ToString() — completion details re-append it.
    var desc = sym.ToString();
    if(desc.StartsWith("[native] "))
      desc = desc.Substring("[native] ".Length);

    if(sym is ClassSymbol)
      return "class " + desc;
    if(sym is EnumSymbol)
      return "enum " + desc;
    if(sym is InterfaceSymbol)
      return "interface " + desc;
    // Namespace.ToString() appends " -> N" for linked-shadow entries (an internal marker
    // of import re-export depth) — use the bare name instead, that detail is not user-facing.
    if(sym is Namespace)
      return "namespace " + sym.name;
    return desc;
  }

  // Walks up the scope chain to find the module name for a symbol.
  static string GetSymbolModuleName(Symbol sym)
  {
    var scope = sym.scope;
    while(scope != null)
    {
      if(scope is Namespace ns && ns.module != null && !string.IsNullOrEmpty(ns.module.name))
        return ns.module.name;
      scope = (scope as Symbol)?.scope ?? (scope as IScope)?.GetFallbackScope();
    }
    return null;
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

  // If the cursor is on the quoted path of an import statement, returns the resolved absolute file path.
  public string FindImportFile(BHLDocument document, Position position)
  {
    var node = document.FindTerminalNode(position.Line, position.Character);
    if(node == null || node.Symbol.Type != bhlLexer.NORMALSTRING)
      return null;

    // Confirm that an IMPORT token appears earlier on the same line
    bool has_import = false;
    foreach(var t in document.TermNodes)
    {
      if(t.Symbol.Line != node.Symbol.Line)
        continue;
      if(t.Symbol.Type == bhlLexer.IMPORT)
      {
        has_import = true;
        break;
      }
    }
    if(!has_import)
      return null;

    var raw = node.GetText(); // includes surrounding quotes
    if(raw.Length < 2)
      return null;
    var import_str = raw.Substring(1, raw.Length - 2);

    try
    {
      ProjConf.inc_path.ResolvePath(document.Uri.PathNormalized(), import_str, out var resolved_path, out _);
      return resolved_path;
    }
    catch { return null; }
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
