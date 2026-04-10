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

  public int IndexedFileCount => Path2Proc.Count;

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

  public async Task ReloadAsync(CancellationToken ct = default)
  {
    var new_types = new Types();
    ProjConf.LoadBindings().Register(new_types);
    Init(new_types, ProjConf, _logger);
    await IndexFilesAsync(ct);
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
      if(!Path2Proc.TryGetValue(path, out var _))
        return new List<CompletionItem>();

      var items = new List<CompletionItem>();
      var seen = new HashSet<Symbol>();

      if(Path2Doc.TryGetValue(path, out var document) && IsInsideString(document, position))
        return items;

      // Build auto-import edits for source modules not yet imported in the current document.
      // Computed before the dot-trigger branch so both paths can attach them.
      Dictionary<string, TextEdit> import_edits = null;
      if(document != null)
      {
        var (already_imported, insert_pos) = GetImportContext(document, ProjConf.inc_path);
        var curr_module = document.Processed.module.name;
        import_edits = new Dictionary<string, TextEdit>();
        foreach(var kv in Path2Proc)
        {
          var mod_name = kv.Value.module.name;
          if(mod_name == curr_module || already_imported.Contains(mod_name))
            continue;
          import_edits[mod_name] = new TextEdit
          {
            Range = new Range(insert_pos, insert_pos),
            NewText = $"import \"{mod_name}\"\n",
          };
        }
        foreach(var m in Types.GetModules())
        {
          var mod_name = m.name;
          if(string.IsNullOrEmpty(mod_name) || already_imported.Contains(mod_name))
            continue;
          import_edits[mod_name] = new TextEdit
          {
            Range = new Range(insert_pos, insert_pos),
            NewText = $"import \"{mod_name}\"\n",
          };
        }
      }

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

      // Native modules (std, std.io, etc.) are registered in Types but not linked
      // into any source module namespace — add their top-level symbols explicitly.
      foreach(var m in Types.GetModules())
        AddModuleNsCompletions(m.ns, "", items, seen, seenLabels, import_edits);

      foreach(var sym in Types.ns)
        AddCompletionItem(items, seen, sym);

      return items;
    }
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
            ?? FindSymbolInAllModules(path2proc, document.Processed.module.ts, name_tok);
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
              ?? FindSymbolInAllModules(path2proc, document.Processed.module.ts, name);

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
    if(!seen.Add(sym))
      return;

    bool is_native = sym is FuncSymbolNative || sym is ClassSymbolNative || sym is EnumSymbolNative;
    // Strip any existing "[native] " prefix from ToString() — we'll re-append it at the end.
    // For class/enum symbols, ToString() returns only the name; prepend the kind keyword.
    var base_desc = sym.ToString();
    if(base_desc.StartsWith("[native] "))
      base_desc = base_desc.Substring("[native] ".Length);
    if(sym is ClassSymbol)
      base_desc = "class " + base_desc;
    else if(sym is EnumSymbol)
      base_desc = "enum " + base_desc;

    var module_name = GetSymbolModuleName(sym);
    var suffix = (is_native ? " [native]" : "") + (module_name != null ? $" [{module_name}]" : "");
    var detail = suffix.Length > 0 ? base_desc + suffix : base_desc;

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
