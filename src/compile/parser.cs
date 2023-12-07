using System;
using System.IO;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace bhl {

public class ANTLR_Parsed
{
  public bhlParser parser { get; private set; }
  public ITokenStream tokens { get; private set; }
  public bhlParser.ProgramContext parse_tree { get; private set; }

  public ANTLR_Parsed(bhlParser parser)
  {
    this.parser = parser;
    this.tokens = parser.TokenStream;
    //NOTE: parsing happens here 
    parse_tree = parser.program();
  }

  public override string ToString()
  {
    return PrintTree(parser, parse_tree);
  }

  public static string PrintTree(Parser parser, IParseTree root)
  {
    var sb = new System.Text.StringBuilder();
    PrintTree(root, sb, 0, parser.RuleNames);
    return sb.ToString();
  }

  public static void PrintTree(IParseTree root, System.Text.StringBuilder sb, int offset, IList<String> rule_names) 
  {
    for(int i = 0; i < offset; i++)
      sb.Append("  ");
    
    sb.Append(Trees.GetNodeText(root, rule_names)).Append(" ("+root.GetType().Name+")").Append("\n");
    if(root is ParserRuleContext prc) 
    {
      if(prc.children != null) 
      {
        foreach(var child in prc.children)
        {
          if(child is IErrorNode)
            sb.Append("!");
          PrintTree(child, sb, offset + 1, rule_names);
        }
      }
    }
  }
}

public class AnnotatedParseTree
{
  public IParseTree tree;
  public Module module;
  public ITokenStream tokens;
  public IType eval_type;
  public Symbol lsp_symbol;

  public SourceRange range { 
    get { 
      return new SourceRange(tree.SourceInterval, tokens);
    } 
  }

  public string file { 
    get { 
      return module.file_path;
    } 
  }
}

public class ANTLR_Processor : bhlParserBaseVisitor<object>
{
  public class Result
  {
    public Module module { get; private set; }
    public AST_Module ast { get; private set; }
    public CompileErrors errors { get; private set; }

    public Result(Module module, AST_Module ast, CompileErrors errors)
    {
      this.module = module;
      this.ast = ast;
      this.errors = errors;
    }
  }

  Types types;

  AST_Module root_ast;
  public Result result { get; private set; }

  public ANTLR_Parsed parsed { get; private set; }

  public Module module { get; private set; }

  public FileImports imports_maybe { get; private set; } 

  //NOTE: passed from above
  CompileErrors errors;

  Dictionary<bhlParser.MimportContext, string> imports_parsed = new Dictionary<bhlParser.MimportContext, string>();

  //NOTE: module.ns linked with types.ns
  Namespace ns;

  ITokenStream tokens;
  public Dictionary<IParseTree, AnnotatedParseTree> annotated_nodes { get; private set; } = new Dictionary<IParseTree, AnnotatedParseTree>();

  class ParserPass
  {
    public IAST ast;
    public IScope scope;

    public Namespace ns;
    public int ns_level;

    public bhlParser.VarDeclareOptAssignContext gvar_decl_ctx;
    public bhlParser.AssignExpContext gvar_assign_ctx;
    public GlobalVariableSymbol gvar_symb;

    public bhlParser.FuncDeclContext func_ctx;
    public AST_FuncDecl func_ast;
    public FuncSymbolScript func_symb;

    public bhlParser.ClassDeclContext class_ctx;
    public ClassSymbolScript class_symb;
    public AST_ClassDecl class_ast;

    public bhlParser.InterfaceDeclContext iface_ctx;
    public InterfaceSymbolScript iface_symb;

    public bhlParser.EnumDeclContext enum_ctx;

    public ParserPass(IScope scope, Namespace ns, int ns_level)
    {
      this.scope = scope;
      this.ns = ns;
      this.ns_level = ns_level;
    }

    public ParserPass(IAST ast, IScope scope, ParserRuleContext ctx)
    {
      this.ast = ast;
      this.scope = scope;
      if(ctx is bhlParser.VarDeclareOptAssignContext vdoa)
      {
        this.gvar_decl_ctx = vdoa;
        this.gvar_assign_ctx = vdoa.assignExp();
        if(!IsValid(vdoa.varDeclare()))
          this.gvar_decl_ctx = null;
      }
      else if(ctx is bhlParser.FuncDeclContext fdc)
      {
        this.func_ctx = fdc;
        if(!IsValid(fdc))
          this.func_ctx = null;
      }
      else if(ctx is bhlParser.ClassDeclContext cdc)
      {
        this.class_ctx = cdc;
        if(!IsValid(cdc))
          this.class_ctx = null;
      }
      else if(ctx is bhlParser.InterfaceDeclContext idc)
      {
        this.iface_ctx = idc;
        if(!IsValid(idc))
          this.iface_ctx = null;
      }
      else if(ctx is bhlParser.EnumDeclContext edc)
      {
        this.enum_ctx = edc;
        if(!IsValid(edc))
          this.enum_ctx = null;
      }
    }

    public void Clear()
    {
      ast = null;
      scope = null;

      ns = null;

      gvar_decl_ctx = null;
      gvar_assign_ctx = null;
      gvar_symb = null;

      func_ctx = null;
      func_ast = null;
      func_symb = null;

      class_ctx = null;
      class_symb = null;
      class_ast = null;

      iface_ctx = null;
      iface_symb = null;

      enum_ctx = null;
    }
  }

  List<ParserPass> passes = new List<ParserPass>();

  Stack<IScope> scopes = new Stack<IScope>();
  IScope curr_scope {
    get {
      return scopes.Peek();
    }
  }

  HashSet<FuncSymbol> return_found = new HashSet<FuncSymbol>();

  HashSet<FuncSymbol> has_yield_calls = new HashSet<FuncSymbol>();

  Dictionary<FuncSymbol, List<AST_Block>> func2blocks = new Dictionary<FuncSymbol, List<AST_Block>>();

  //NOTE: a list is used instead of stack, so that it's easier to traverse by index
  List<FuncSymbolScript> func_decl_stack = new List<FuncSymbolScript>();

  Stack<IType> json_type_stack = new Stack<IType>();

  Stack<bool> call_by_ref_stack = new Stack<bool>();

  Stack<AST_Tree> ast_stack = new Stack<AST_Tree>();

  public class SemanticTokenNode
  {
    public int idx;
    public int line;
    public int column;
    public int len;
    public SemanticToken type_idx;
    public SemanticModifier mods;
  }
  List<SemanticTokenNode> semantic_tokens = new List<SemanticTokenNode>();
  List<uint> encoded_semantic_tokens = new List<uint>();

  static CommonTokenStream Stream2Tokens(Stream s, ErrorHandlers handlers)
  {
    var lex = new bhlLexer(new AntlrInputStream(s));

    if(handlers?.lexer_listener != null)
    {
      lex.RemoveErrorListeners();
      lex.AddErrorListener(handlers.lexer_listener);
    }

    return new CommonTokenStream(lex);
  }

  public static bhlParser Stream2Parser(
      Module module,
      CompileErrors errors,
      ErrorHandlers err_handlers,
      Stream src, 
      HashSet<string> defines
    )
  {
    src = ANTLR_Preprocessor.ProcessStream(module, errors, err_handlers, src, defines);

    var tokens = Stream2Tokens(src, err_handlers);

    var p = new bhlParser(tokens);

    err_handlers?.AttachToParser(p);

    return p;
  }

  public static ANTLR_Processor MakeProcessor(
    Module module, 
    FileImports imports_maybe, 
    ANTLR_Parsed parsed/*can be null*/, 
    Types ts, 
    CompileErrors errors,
    ErrorHandlers err_handlers,
    HashSet<string> defines = null
    )
  {
    if(parsed == null)
    {
      using(var sfs = File.OpenRead(module.file_path))
      {
        return MakeProcessor(
          module, 
          imports_maybe, 
          sfs, 
          ts, 
          errors,
          err_handlers,
          defines
        );
      }
    }
    else 
    {
      return new ANTLR_Processor(
        parsed, 
        module, 
        imports_maybe, 
        ts,
        errors
      );
    }
  }

  public static ANTLR_Processor MakeProcessor(
    Module module, 
    FileImports imports_maybe, 
    Stream src, 
    Types ts, 
    CompileErrors errors,
    ErrorHandlers err_handlers,
    HashSet<string> defines = null
    )
  {
    var p = Stream2Parser(module, errors, err_handlers, src, defines);

    //NOTE: parsing happens here 
    var parsed = new ANTLR_Parsed(p);

    return new ANTLR_Processor(
      parsed, 
      module, 
      imports_maybe, 
      ts, 
      errors
    );
  }

  public ANTLR_Processor(
      ANTLR_Parsed parsed, 
      Module module, 
      FileImports imports_maybe, 
      Types types,
      CompileErrors errors
    )
  {
    this.parsed = parsed;
    this.tokens = parsed.tokens;

    this.types = types;
    this.module = module;
    this.imports_maybe = imports_maybe;

    this.errors = errors;

    ns = module.ns;
    ns.Link(types.ns);

    PushScope(ns);
  }

  void AddError(Origin origin, string msg) 
  {
    AddError(origin.parsed.tree, msg);
  }

  void AddError(IParseTree place, string msg) 
  {
    try
    {
      errors.Add(new ParseError(module, place, tokens, msg));
    }
    catch(Exception)
    {}
  }

  void PushBlock(AST_Block block)
  {
    var fsymb = PeekFuncDecl();
    List<AST_Block> blocks;
    func2blocks.TryGetValue(fsymb, out blocks);
    if(blocks == null)
    {
      blocks = new List<AST_Block>();
      func2blocks[fsymb] = blocks;
    }
    blocks.Add(block);
  }

  void PopBlock(AST_Block block)
  {
    var fsymb = PeekFuncDecl();
    List<AST_Block> blocks;
    func2blocks.TryGetValue(fsymb, out blocks);
    blocks.Remove(block);
  }

  int CountBlocks(BlockType type)
  {
    var fsymb = PeekFuncDecl();
    List<AST_Block> blocks;
    func2blocks.TryGetValue(fsymb, out blocks);
    int c = 0;
    if(blocks != null)
    {
      foreach(var block in blocks)
        if(block.type == type)
          ++c;
    }
    return c;
  }

  int GetBlockLevel(BlockType type)
  {
    var fsymb = PeekFuncDecl();
    List<AST_Block> blocks;
    func2blocks.TryGetValue(fsymb, out blocks);
    if(blocks != null)
    {
      for(int i=blocks.Count;i-- > 0;)
      {
        var block = blocks[i];
        if(block.type == type)
          return i;
      }
    }
    return -1;
  }

  int GetLoopBlockLevel()
  {
    int level = GetBlockLevel(BlockType.FOR);
    if(level != -1)
      return level;
    level = GetBlockLevel(BlockType.WHILE);
    if(level != -1)
      return level;
    level = GetBlockLevel(BlockType.DOWHILE);
    return level;
  }

  void PushScope(IScope scope)
  {
    if(scope is FuncSymbolScript fsymb)
      func_decl_stack.Add(fsymb);
    scopes.Push(scope);
  }

  void PopScope()
  {
    if(curr_scope is FuncSymbolScript)
      func_decl_stack.RemoveAt(func_decl_stack.Count-1);
    scopes.Pop();
  }

  void PushAST(AST_Tree ast)
  {
    ast_stack.Push(ast);
  }

  void PopAST()
  {
    ast_stack.Pop();
  }

  void PopAddOptimizeAST()
  {
    var tmp = PeekAST();
    PopAST();
    if(tmp is AST_Interim intr)
    {
      if(intr.children.Count == 1)
        PeekAST().AddChild(intr.children[0]);
      else if(intr.children.Count > 1)
        PeekAST().AddChild(intr);
    }
    else
      PeekAST().AddChild(tmp);
  }

  void PopAddAST()
  {
    var tmp = PeekAST();
    PopAST();
    PeekAST().AddChild(tmp);
  }

  AST_Tree PeekAST()
  {
    return ast_stack.Peek();
  }

  AnnotatedParseTree Annotate(IParseTree t)
  {
    AnnotatedParseTree at;
    if(!annotated_nodes.TryGetValue(t, out at))
    {
      at = new AnnotatedParseTree();
      at.module = module;
      at.tree = t;
      at.tokens = tokens;

      annotated_nodes.Add(t, at);
    }
    return at;
  }

  public List<uint> GetEncodedSemanticTokens()
  {
    if(encoded_semantic_tokens.Count != 0)
      return encoded_semantic_tokens;

    encoded_semantic_tokens.Clear();

    semantic_tokens.Sort((a, b) => a.idx - b.idx);

    SemanticTokenNode prev = null;
    foreach(var token in semantic_tokens)
    {
      int diff_line = token.line - (prev?.line??1);
      int diff_column = diff_line != 0 ? token.column : token.column - prev?.column??0;

      // line
      encoded_semantic_tokens.Add((uint)diff_line);
      // startChar
      encoded_semantic_tokens.Add((uint)diff_column);
      // length
      encoded_semantic_tokens.Add((uint)token.len);
      // tokenType
      encoded_semantic_tokens.Add((uint)token.type_idx);
      // tokenModifiers
      encoded_semantic_tokens.Add((uint)token.mods);

      prev = token;
    }

    return encoded_semantic_tokens;
  }

  public AnnotatedParseTree FindAnnotated(IParseTree t)
  {
    AnnotatedParseTree at;
    annotated_nodes.TryGetValue(t, out at);
    return at;
  }

  internal void Phase_Outline()
  {
    root_ast = new AST_Module(module.name);

    passes.Clear();

    PushAST(root_ast);
    VisitProgram(parsed.parse_tree);
    PopAST();

    for(int p=0;p<passes.Count;++p)
    {
      var pass = passes[p];

      PushScope(pass.scope);

      Pass_OutlineNamespace(pass);

      Pass_OutlineGlobalVar(pass);

      Pass_OutlineInterfaceDecl(pass);

      Pass_OutlineClassDecl(pass);

      Pass_OutlineFuncDecl(pass);

      Pass_OutlineEnumDecl(pass);

      PopScope();
    }
  }

  internal void Phase_PreLinkImports(
    Dictionary<string, ANTLR_Processor> file2proc, 
    //NOTE: can be null, contains already cached compile modules.
    //      an entry present in file2compiled doesn't exist in file2proc
    Dictionary<string, CompiledModule> file2compiled, 
    IncludePath inc_path)
  {
    if(imports_parsed.Count == 0)
      return;

    foreach(var kv in imports_parsed)
    {
      Module imported_module;
      Namespace imported_ns;
      string file_path;

      if(ResolveImportedModule(
          kv.Value, 
          file2proc, 
          file2compiled, 
          out file_path,
          //in case of global native module this one will be null for now
          out imported_module, 
          out imported_ns
        ))
      {
        ns.PreLink(imported_ns);
      }
    }
  }

  bool ResolveImportedModule(
    string import,
    Dictionary<string, ANTLR_Processor> file2proc, 
    Dictionary<string, CompiledModule> file2compiled,
    out string file_path,
    out Module imported_module,
    out Namespace imported_ns
  )
  {
    file_path = "";
    imported_module = null;
    imported_ns = null;

    //let's check if it's a global native module
    var reg_mod = types.FindRegisteredModule(import);
    if(reg_mod != null)
    {
      imported_ns = reg_mod.ns;
      return true;
    }

    file_path = imports_maybe.MapToFilePath(import);
    if(file_path == null || !File.Exists(file_path))
      return false;

    //let's check if it's a compiled module and
    //try to fetch it from the cache first
    if(file2compiled != null && file2compiled.TryGetValue(file_path, out var cm))
      imported_module = cm.module;
    else if(file2proc.TryGetValue(file_path, out var proc))
      imported_module = proc.module;
    else 
      return false;

    imported_ns = imported_module.ns;

    return true;
  }

  internal void Phase_LinkImports(
    Dictionary<string, ANTLR_Processor> file2proc, 
    //NOTE: can be null, contains already cached compile modules.
    //      an entry present in file2compiled doesn't exist in file2proc
    Dictionary<string, CompiledModule> file2compiled, 
    IncludePath inc_path)
  {
    if(imports_parsed.Count == 0)
      return;

    var ast_import = new AST_Import();

    foreach(var kv in imports_parsed)
    {
      Module imported_module;
      Namespace imported_ns;
      string file_path;

      if(!ResolveImportedModule(
          kv.Value, 
          file2proc, 
          file2compiled, 
          out file_path,
          //in case of global native module this one will be null for now
          out imported_module, 
          out imported_ns
        ))
      {
        AddError(kv.Key, "invalid import '" + kv.Value + "'");
        continue;
      }

      if(imported_module != null)
      {
        //protection against self import
        if(imported_module.name == module.name) 
          continue;
            
        //NOTE: let's add imported global vars to module's global vars index
        if(module.local_gvars_mark == -1)
          module.local_gvars_mark = module.gvars.Count;

        //NOTE: adding directly without indexing
        for(int i=0;i<imported_module.local_gvars_num;++i)
          module.gvars.index.Add(imported_module.gvars[i]);
      }

      try
      {
        ns.Link(imported_ns);
      }
      catch(SymbolError se)
      {
        errors.Add(se);
        continue;
      }

      if(imported_module != null)
        ast_import.module_names.Add(inc_path.FilePath2ModuleName(file_path));
    }

    root_ast.AddChild(ast_import);
  }

  internal void Phase_ParseTypes1()
  {
    for(int p=0;p<passes.Count;++p)
    {
      var pass = passes[p];

      PushScope(pass.scope);

      Pass_ParseInterfaceMethods(pass);

      Pass_AddClassExtensions(pass);

      Pass_ParseClassMembersTypes(pass);

      Pass_ParseFuncSignature_1(pass);

      PopScope();
    }
  }

  internal void Phase_ParseTypes2()
  {
    for(int p=0;p<passes.Count;++p)
    {
      var pass = passes[p];

      PushScope(pass.scope);

      Pass_ParseFuncSignature_2(pass);

      Pass_AddInterfaceExtensions(pass);

      PopScope();
    }

    for(int p=0;p<passes.Count;++p)
    {
      var pass = passes[p];

      PushScope(pass.scope);

      Pass_SetupClass(pass);

      Pass_ParseGlobalVar(pass);

      PopScope();
    }
  }

  internal void Phase_ParseFuncBodies()
  {
    for(int p=0;p<passes.Count;++p)
    {
      var pass = passes[p];

      PushScope(pass.scope);

      Pass_ParseClassMethodsBlocks(pass);

      Pass_ParseFuncBlock(pass);

      PopScope();
    }
  }

  internal void Phase_SetResult()
  {
    result = new Result(module, root_ast, errors);
  }

  static public void ProcessAll(
    Dictionary<string, ANTLR_Processor> file2proc, 
    //NOTE: can be null, contains already cached compile modules.
    //      an entry present in file2compiled doesn't exist in file2proc
    Dictionary<string, CompiledModule> file2compiled, 
    IncludePath inc_path
  )
  {
    foreach(var kv in file2proc)
      WrapError(kv.Value, () => kv.Value.Phase_Outline());

    if(file2compiled != null)
      LinkCompiledImports(file2compiled, file2proc);

    foreach(var kv in file2proc)
      WrapError(kv.Value, () => kv.Value.Phase_PreLinkImports(file2proc, file2compiled, inc_path));

    foreach(var kv in file2proc)
      WrapError(kv.Value, () => kv.Value.Phase_LinkImports(file2proc, file2compiled, inc_path));

    foreach(var kv in file2proc)
      WrapError(kv.Value, () => kv.Value.Phase_ParseTypes1());

    foreach(var kv in file2proc)
      WrapError(kv.Value, () => kv.Value.Phase_ParseTypes2());

    foreach(var kv in file2proc)
      WrapError(kv.Value, () => kv.Value.Phase_ParseFuncBodies());

    foreach(var kv in file2proc)
      WrapError(kv.Value, () => kv.Value.Phase_SetResult()); 
  }

  static void WrapError(ANTLR_Processor proc, Action action)
  {
    try
    {
      action();
    }
    catch(SymbolError err)
    {
      proc.errors.Add(err);
    }
  }

  static void LinkCompiledImports(
    Dictionary<string, CompiledModule> file2compiled,
    Dictionary<string, ANTLR_Processor> file2proc
  )
  {
    var mod_name2ns = new Dictionary<string, Namespace>(); 
    //we need to try both compiled modules and modules yet to be parsed
    foreach(var kv in file2compiled)
      mod_name2ns.Add(kv.Value.module.name, kv.Value.module.ns);
    foreach(var kv in file2proc)
      mod_name2ns.Add(kv.Value.module.name, kv.Value.module.ns);

    foreach(var kv in file2compiled)
    {
      foreach(string import in kv.Value.imports)
        kv.Value.module.ns.PreLink(mod_name2ns[import]);
    }

    foreach(var kv in file2compiled)
    {
      foreach(string import in kv.Value.imports)
        kv.Value.module.ns.Link(mod_name2ns[import]);
    }
  }

  public override object VisitProgram(bhlParser.ProgramContext ctx)
  {
    foreach(var item in ctx.declOrImport())
    {
      if(item.mimport() != null)
        ParseImport(item.mimport());
    }

    foreach(var item in ctx.declOrImport())
    {
      if(item.decl() != null)
        ProcessDecl(item.decl());
    }

    return null;
  }

  void ParseImport(bhlParser.MimportContext ctx)
  {
    LSP_AddSemanticToken(ctx.IMPORT(), SemanticToken.Keyword);
    LSP_AddSemanticToken(ctx.NORMALSTRING(), SemanticToken.String);

    var name = ctx.NORMALSTRING().GetText();
    //removing quotes
    name = name.Substring(1, name.Length-2);

    imports_parsed[ctx] = name;
  }

  void AddPass(ParserRuleContext ctx, IScope scope, IAST ast)
  {
    passes.Add(new ParserPass(ast, scope, ctx));
  }

	IType ProcFuncCallExp(
    ParserRuleContext ctx,
    bhlParser.ChainExpContext exp,
    bool yielded = false
  )
  {
    var chain = new ExpChain(ctx, exp);

    if(!chain.IsFuncCall)
    {
      AddError(exp, "unexpected expression");
      return null;
    }

    if(chain.Incomplete)
    {
      AddError(exp, "incomplete statement");
      return null;
    }

    if(yielded)
      CheckCoroCallValidity(ctx);

    IType curr_type = null;
    ProcExpChain(chain, ref curr_type, yielded: yielded);

    return curr_type;
  }

  void ProcPopNonConsumed(IType ret_type)
  {
    if(ret_type == null || ret_type == Types.Void)
      return;
    
    //let's pop unused returned value
    var ret_type_arr = new TypeAsArr(ret_type);
    for(int i=0;i<ret_type_arr.Count;++i)
      PeekAST().AddChild(new AST_PopValue());
  }

  public override object VisitStmChainExp(bhlParser.StmChainExpContext ctx)
  {
    if(ctx.modifyOp() == null)
    {
      var ret_type = ProcFuncCallExp(ctx, ctx.chainExp());
      ProcPopNonConsumed(ret_type);
    }
    else 
    {
      ProcExpModifyOp(ctx, ctx.chainExp(), ctx.modifyOp());
    }
    return null;
  }

  bool ProcExpChain(
    ParserRuleContext ctx,
    bhlParser.ChainExpContext chain_ctx,
    ref IType curr_type,
    bool write = false,
    bool yielded = false
  )
  {
    var chain = new ExpChain(ctx, chain_ctx);

    return ProcExpChain(
      chain,
      ref curr_type,
      write,
      yielded
    );
  }

  bool ProcExpChain(
    ExpChain chain,
    ref IType curr_type, 
    bool write = false,
    bool yielded = false,
    IScope root_scope = null
   )
  {
    if(root_scope == null)
      root_scope = chain.IsGlobalNs ? ns : curr_scope;

    if(chain.lmb_ctx != null)
    {
      if(!ProcLambdaCall(
        chain.ctx,
        chain.lmb_ctx,
        chain.items,
        ref curr_type,
        write,
        yielded
      ))
        return false;
    }
    else
    {
      var root_name = chain.name_ctx?.NAME();

      //NOTE: if it's not 'terminal' let's visit it deeper
      if(chain.paren_exp_ctx != null)
      {
        if(TryVisit(chain.paren_exp_ctx))
          curr_type = Annotate(chain.paren_exp_ctx).eval_type;
      }

      IScope scope = root_scope;

      PushAST(new AST_Interim());

      var curr_name = root_name;

      int chain_offset = 0;

      if(root_name != null)
      {
        if(!ProcChainStartingName(
          chain,
          root_name, 
          ref curr_name, 
          ref scope,
          ref curr_type,
          ref chain_offset
        ))
        {
          PopAST();
          return false;
        }
      }

      Symbol curr_symb = null;

      if(!ProcChainItems(
          chain.items, 
          chain_offset,
          ref curr_name,
          ref scope,
          ref curr_type,
          ref curr_symb,
          write
        ))
      {
        PopAST();
        return false;
      }

      //checking the leftover of the call chain or a root call
      if(curr_name != null)
      {
        curr_symb = ProcChainItem(
          scope, 
          curr_name, 
          null, 
          null, 
          ref curr_type, 
          curr_name.Symbol.Line, 
          write, 
          is_leftover: true, 
          is_root: chain.items.Count == 0
        );
      }

      if(!CheckExpClassFunctionReadWrite(
          chain, 
          curr_name, 
          scope, 
          curr_type, 
          curr_symb,
          write
        ))
      {
        PopAST();
        return false;
      }

      var chain_ast = PeekAST();
      PopAST();

      ValidateChainCall(
        chain.ctx, 
        0,
        chain_ast.children, 
        yielded
      );

      PeekAST().AddChildren(chain_ast);
    }

    return true;
  }
  
  bool CheckExpClassFunctionReadWrite(
    ExpChain chain, 
    ITerminalNode curr_name, 
    IScope scope, 
    IType curr_type, 
    Symbol curr_symb,
    bool write
  )
  {
    if(chain.IsVarAccess && curr_symb is FuncSymbol m && scope is ClassSymbol)
    {
      if(!write)
      {
        //NOTE: allowing only static method pointers
        if(!m.attribs.HasFlag(FuncAttrib.Static))
        {
          AddError(chain.items.At(chain.items.Count-1), "method pointers not supported");
          return false;
        }
      }
      else
      {
        AddError(chain.items.At(chain.items.Count-1), "invalid assignment");
        return false;
      }
    }
    return true;
  }

  bool ProcChainStartingName(
    ExpChain chain,
    ITerminalNode root_name,
    ref ITerminalNode curr_name,
    ref IScope scope,
    ref IType curr_type,
    ref int chain_offset
   )
  {
    var name_symb = scope.ResolveWithFallback(curr_name.GetText());

    TryProcessClassBaseCall(
      ref curr_name, 
      ref scope, 
      ref name_symb, 
      ref chain_offset, 
      chain.items, 
      root_name.Symbol.Line
     );

    if(name_symb == null)
    {
      AddError(root_name, "symbol '" + curr_name.GetText() + "' not resolved");
      return false;
    }

    TryApplyNamespaceOffset(
      ref curr_name, 
      ref scope, 
      ref name_symb, 
      ref chain_offset, 
      chain.items
     );

    if(name_symb is IType)
      curr_type = (IType)name_symb;
    else if(name_symb is ITyped typed)
      curr_type = typed.GetIType();
    else
      curr_type = null;

    if(curr_type == null)
    {
      AddError(root_name, "bad chain call");
      return false;
    }

    return true;
  }

  bool ProcChainItems(
    ExpChainItems chain_items, 
    int chain_offset, 
    ref ITerminalNode curr_name, 
    ref IScope scope, 
    ref IType curr_type, 
    ref Symbol curr_symb,
    bool write
    )
  {
    for(int c=chain_offset;c<chain_items.Count;++c)
    {
      var item = chain_items.At(c);
      var cargs = item as bhlParser.CallArgsContext;
      var macc = item as bhlParser.MemberAccessContext;
      var arracc = item as bhlParser.ArrAccessContext;
      bool is_last = c == chain_items.Count-1;

      if(cargs != null)
      {
        curr_symb = ProcChainItem(
          scope, 
          curr_name, 
          cargs, 
          null, 
          ref curr_type, 
          cargs.Start.Line, 
          write: false, 
          is_root: c == chain_offset
        );
        curr_name = null;
      }
      else if(arracc != null)
      {
        curr_symb = ProcChainItem(
          scope, 
          curr_name, 
          null, 
          arracc, 
          ref curr_type, 
          arracc.Start.Line, 
          write: write && is_last, 
          is_root: c == chain_offset
        );
        curr_name = null;
      }
      else if(macc != null)
      {
        Symbol macc_name_symb = null;
        if(curr_name != null)
          macc_name_symb = ProcChainItem(
            scope, 
            curr_name, 
            null, 
            null, 
            ref curr_type, 
            macc.Start.Line, 
            write: false, 
            is_root: c == chain_offset
          );

        scope = curr_type as IScope;
        if(!(scope is IInstanceType) && !(scope is EnumSymbol))
        {
          AddError(macc, "type doesn't support member access via '.'");
          return false;
        }

        if(macc.NAME() == null)
        {
          AddError(macc, "incomplete parsing context");
          return false;
        }

        var macc_name_class_symb = macc_name_symb as ClassSymbol;
        var tmp_macc_symb = scope.ResolveWithFallback(macc.NAME().GetText());

        if(macc_name_class_symb == null && tmp_macc_symb is FuncSymbol macc_fs && 
           macc_fs.attribs.HasFlag(FuncAttrib.Static))
        {
          AddError(macc, "calling static method on instance is forbidden");
          return false;
        }

        if(macc_name_class_symb != null && tmp_macc_symb is FuncSymbol macc_fs2 && 
          !macc_fs2.attribs.HasFlag(FuncAttrib.Static))
        {
          AddError(macc, "calling instance method as static is forbidden");
          return false;
        }

        if(macc_name_class_symb == null && tmp_macc_symb is FieldSymbol macc_fld &&
           macc_fld.attribs.HasFlag(FieldAttrib.Static))
        {
          AddError(macc, "accessing static field on instance is forbidden");
          return false;
        }

        if(macc_name_class_symb != null && tmp_macc_symb is FieldSymbol macc_fld2 &&
           !macc_fld2.attribs.HasFlag(FieldAttrib.Static))
        {
          AddError(macc, "accessing instance attribute as static is forbidden");
          return false;
        }

        curr_name = macc.NAME();
        curr_symb = macc_name_symb;
      }
      else
        throw new Exception("Unhandled case");
    }

    return true;
  }
  
  void ValidateChainCall(
    ParserRuleContext ctx, 
    int offset,
    List<IAST> chain_ast, 
    bool yielded
  )
  {
    for(int i = offset; i < chain_ast.Count; ++i)
    {
      if(chain_ast[i] is AST_Call call)
      {
        if(call.type == EnumCall.FUNC || call.type == EnumCall.MFUNC)
        {
          if(call.symb is FuncSymbol fs)
            ValidateFuncCall(call, chain_ast.Count-1 == i, fs.signature, yielded);
        }
        else if(call.type == EnumCall.FUNC_VAR || call.type == EnumCall.FUNC_MVAR)
        {
          if(call.symb is VariableSymbol vs)
            ValidateFuncCall(call, chain_ast.Count-1 == i, vs.type.Get() as FuncSignature, yielded);
        }
      }
    }
  }

  void ValidateFuncCall(
    AST_Call call,
    bool is_last, 
    FuncSignature fsig, 
    bool yielded
  )
  {
    if(PeekFuncDecl() == null)
    {
      AddError(call.node, "function calls not allowed in global context");
      return;
    }

    if(is_last)
    {
      if(!yielded && fsig.attribs.HasFlag(FuncSignatureAttrib.Coro))
      {
        AddError(call.node, "coro function must be called via yield");
        return;
      }
      else if(yielded && !fsig.attribs.HasFlag(FuncSignatureAttrib.Coro))
      {
        AddError(call.node, "not a coro function");
        return;
      }
    }
    else 
    {
      if(fsig.attribs.HasFlag(FuncSignatureAttrib.Coro))
      {
        AddError(call.node, "coro function must be called via yield");
        return;
      }
    }
  }

  void TryProcessClassBaseCall(
    ref ITerminalNode curr_name, 
    ref IScope scope, 
    ref Symbol name_symb, 
    ref int chain_offset, 
    ExpChainItems chain_items, 
    int line
  )
  {
    if(curr_name.GetText() == "base" && PeekFuncDecl()?.scope is ClassSymbol cs)
    {
      if(cs.super_class == null)
      {
        AddError(curr_name, "no base class found");
        return;
      }
      else
      {
        name_symb = cs.super_class; 
        scope = cs.super_class;
        if(chain_items.Count <= chain_offset)
        {
          AddError(curr_name, "bad base call");
          return;
        }
        var macc = chain_items.At(chain_offset) as bhlParser.MemberAccessContext;
        if(macc == null)
        {
          AddError(chain_items.At(chain_offset), "bad base call");
          return;
        }
        curr_name = macc.NAME(); 
        ++chain_offset;

        PeekAST().AddChild(new AST_Call(EnumCall.VAR, line, PeekFuncDecl().Resolve("this")));
        PeekAST().AddChild(new AST_TypeCast(cs.super_class, force_type: true, line_num: line));
      }
    }
  }

  void TryApplyNamespaceOffset(
    ref ITerminalNode curr_name, 
    ref IScope scope, 
    ref Symbol name_symb, 
    ref int chain_offset, 
    ExpChainItems chain_items
  )
  {
    if(name_symb is Namespace ns && chain_items.Count > 0)
    {
      scope = ns;
      for(chain_offset=0; chain_offset<chain_items.Count;)
      {
        var macc = chain_items.At(chain_offset) as bhlParser.MemberAccessContext;
        if(macc == null)
        {
          AddError(chain_items.At(chain_offset), "bad chain call");
          return;
        }
        name_symb = scope.ResolveWithFallback(macc.NAME().GetText());
        if(name_symb == null)
        {
          AddError(macc.NAME(), "symbol '" + macc.NAME().GetText() + "' not resolved");
          return;
        }
        curr_name = macc.NAME(); 
        ++chain_offset;
        if(name_symb is Namespace name_ns)
          scope = name_ns;
        else
          break;
      }
    }
  }

  Symbol ProcChainItem(
    IScope scope, 
    ITerminalNode name, 
    bhlParser.CallArgsContext cargs, 
    bhlParser.ArrAccessContext arracc, 
    ref IType type, 
    int line, 
    bool write,
    bool is_leftover = false,
    bool is_root = false
    )
  {
    AST_Call ast = null;

    Symbol name_symb = null;

    if(name != null)
    {
      name_symb = is_root ? 
        scope.ResolveWithFallback(name.GetText()) : 
        scope.ResolveRelatedOnly(name.GetText());

      if(name_symb == null)
      {
        AddError(name, "symbol '" + name.GetText() + "' not resolved");
        return name_symb;
      }

      LSP_SetSymbol(name.Parent, name_symb);

      var var_symb = name_symb as VariableSymbol;
      var func_symb = name_symb as FuncSymbol;
      var enum_symb = name_symb as EnumSymbol;
      var enum_item = name_symb as EnumItemSymbol;
      var class_symb = name_symb as ClassSymbol;

      //func or method call
      if(cargs != null)
      {
        if(var_symb is FieldSymbol && !(var_symb.type.Get() is FuncSignature))
        {
          AddError(name, "symbol is not a function");
          return name_symb;
        }

        //func ptr
        if(var_symb != null && var_symb.type.Get() is FuncSignature)
        {
          var ftype = var_symb.type.Get() as FuncSignature;

          if(!(scope is IInstanceType))
          {
            ast = new AST_Call(EnumCall.FUNC_VAR, line, var_symb, 0, name);
            AddCallArgs(ftype, cargs, ref ast);
          }
          else //func ptr member of class
          {
            PeekAST().AddChild(new AST_Call(EnumCall.MVAR, line, var_symb, 0, name));
            ast = new AST_Call(EnumCall.FUNC_MVAR, line, var_symb, 0, name);
            AddCallArgs(ftype, cargs, ref ast);
          }

          type = ftype.ret_type.Get();
          if(type == null)
            AddError(name, "type '" + ftype.ret_type + "' not found");
        }
        else if(func_symb != null)
        {
          ast = new AST_Call(scope is IInstanceType && !func_symb.attribs.HasFlag(FuncAttrib.Static) ? 
            EnumCall.MFUNC : EnumCall.FUNC, 
            line, 
            func_symb,
            0,
            name
          );
          //NOTE: let's mark func calls native and useland with different colors
          LSP_AddSemanticToken(name, 
              func_symb is FuncSymbolNative ? SemanticToken.Parameter : SemanticToken.Function);
          AddCallArgs(func_symb, cargs, ref ast);
          type = func_symb.GetReturnType();
        }
        else
        {
          AddError(name, "symbol is not a function");
          return name_symb;
        }
      }
      //variable or attribute call
      else
      {
        if(var_symb != null)
        {
          bool is_write = write && arracc == null;
          bool is_global = var_symb.scope is Namespace;
          var fld_symb = var_symb as FieldSymbol;
          if(fld_symb != null && fld_symb.attribs.HasFlag(FieldAttrib.Static))
            is_global = true;

          if(scope is InterfaceSymbol)
          {
            AddError(name, "attributes not supported by interfaces");
            return name_symb;
          }

          ast = new AST_Call(fld_symb != null && !is_global ? 
            (is_write ? EnumCall.MVARW : EnumCall.MVAR) : 
            (is_global ? (is_write ? EnumCall.GVARW : EnumCall.GVAR) : (is_write ? EnumCall.VARW : EnumCall.VAR)), 
            line, 
            var_symb,
            0,
            name
          );
          //handling passing by ref for class fields
          if(fld_symb != null && PeekCallByRef())
          {
            if(scope is ClassSymbolNative)
            {
              AddError(name, "getting native class field by 'ref' not supported");
              return name_symb;
            }
            ast.type = EnumCall.MVARREF; 
          }
          else if(fld_symb != null && scope is ClassSymbolNative)
          {
            if(ast.type == EnumCall.MVAR && fld_symb.getter == null)
            {
              AddError(name, "get operation is not defined");
              return name_symb;
            }
            else if(ast.type == EnumCall.MVARW && fld_symb.setter == null)
            {
              AddError(name, "set operation is not defined");
              return name_symb;
            }
          }

          type = var_symb.type.Get();
          if(type == null)
            AddError(name, "type '" + var_symb.type + "' not found");
        }
        else if(func_symb != null)
        {
          ast = new AST_Call(EnumCall.GET_ADDR, line, func_symb, 0, name);
          type = func_symb.signature;
        }
        else if(enum_symb != null)
        {
          if(is_leftover)
          {
            AddError(name, "symbol usage is not valid");
            return name_symb;
          }
          type = enum_symb;
        }
        else if(enum_item != null)
        {
          var ast_literal = new AST_Literal(ConstType.INT);
          ast_literal.nval = enum_item.val;
          PeekAST().AddChild(ast_literal);
        }
        else if(class_symb != null)
        {
          if(class_symb is StringSymbol)
            AddError(name, "symbol usage is not valid");
          type = class_symb;
        }
        else
        {
          AddError(name, "symbol usage is not valid");
          return name_symb;
        }
      }
    }
    else if(cargs != null)
    {
      var ftype = type as FuncSignature;
      if(ftype == null)
      {
        AddError(cargs, "no func to call");
        return name_symb;
      }
      
      ast = new AST_Call(EnumCall.LMBD, line, null);
      AddCallArgs(ftype, cargs, ref ast);
      type = ftype.ret_type.Get();
      if(type == null)
        AddError(name, "type '" + ftype.ret_type + "' not found");
    }

    if(ast != null)
      PeekAST().AddChild(ast);

    if(arracc != null)
      AddArrIndex(arracc, ref type, line, write);

    return name_symb;
  }

  void AddArrIndex(bhlParser.ArrAccessContext arracc, ref IType type, int line, bool write)
  {
    if(type is ArrayTypeSymbol arr_type)
    {
      var arr_exp = arracc.exp();
      if(!TryVisit(arr_exp))
        return;

      if(Annotate(arr_exp).eval_type != Types.Int)
      {
        AddError(arr_exp, "array index expression is not of type int");
        return;
      }

      type = arr_type.item_type.Get();

      var ast = new AST_Call(write ? EnumCall.ARR_IDXW : EnumCall.ARR_IDX, line, null);
      PeekAST().AddChild(ast);
    }
    else if(type is MapTypeSymbol map_type)
    {
      var arr_exp = arracc.exp();
      if(!TryVisit(arr_exp))
        return;

      if(!Annotate(arr_exp).eval_type.Equals(map_type.key_type.Get()))
      {
        AddError(arr_exp, "not compatible map key types");
        return;
      }

      type = map_type.val_type.Get();

      var ast = new AST_Call(write ? EnumCall.MAP_IDXW : EnumCall.MAP_IDX, line, null);
      PeekAST().AddChild(ast);
    }
    else
    {
      AddError(arracc, "accessing not an array/map type '" + type?.GetName() + "'");
      return;
    }
  }

  class NormCallArg
  {
    public bhlParser.CallArgContext ca;
    public Symbol orig;
    public bool variadic;
  }

  void AddCallArgs(FuncSymbol func_symb, bhlParser.CallArgsContext cargs, ref AST_Call call)
  {     
    int total_args_num = func_symb.GetTotalArgsNum();
    int default_args_num = func_symb.GetDefaultArgsNum();
    int required_args_num = total_args_num - default_args_num;
    var args_info = new FuncArgsInfo();

    var norm_cargs = new List<NormCallArg>(total_args_num);
    for(int i=0;i<total_args_num;++i)
    {
      var arg = new NormCallArg();
      arg.orig = func_symb.TryGetArg(i);
      if(arg.orig == null)
      {
        AddError(func_symb.origin, "bad signature");
        return;
      }
      norm_cargs.Add(arg); 
    }

    var variadic_args = new List<bhlParser.CallArgContext>();
    if(func_symb.attribs.HasFlag(FuncAttrib.VariadicArgs))
      norm_cargs[total_args_num-1].variadic = true;

    //1. filling normalized call args
    for(int ci=0;ci<cargs.callArgsList()?.callArg().Length;++ci)
    {
      var ca = cargs.callArgsList().callArg()[ci];
      var ca_name = ca.NAME();

      var idx = ci;
      //NOTE: checking if it's a named arg and finding its index
      if(ca_name != null)
      {
        idx = func_symb.FindArgIdx(ca_name.GetText());
        if(idx == -1)
        {
          AddError(ca_name, "no such named argument");
          return;
        }

        if(norm_cargs[idx].ca != null)
        {
          AddError(ca_name, "argument already passed before");
          return;
        }
      }

      if(func_symb.attribs.HasFlag(FuncAttrib.VariadicArgs) && idx >= func_symb.GetTotalArgsNum() - 1)
        variadic_args.Add(ca);
      else if(idx >= func_symb.GetTotalArgsNum())
      {
        AddError(ca, "there is no argument " + (idx + 1) + ", total arguments " + func_symb.GetTotalArgsNum());
        return;
      }
      else
        norm_cargs[idx].ca = ca;
    }

    PushAST(call);
    IParseTree prev_ca = null;
    //2. traversing normalized args
    for(int i=0;i<norm_cargs.Count;++i)
    {
      var na = norm_cargs[i];

      if(!na.variadic)
      {
        //NOTE: if call arg is not specified, try to find the default one
        if(na.ca == null)
        {
          //this one is used for proper error reporting
          var next_arg = FindNextCallArg(cargs, prev_ca);

          if(i < required_args_num)
          {
            AddError(next_arg, "missing argument '" + norm_cargs[i].orig.name + "'");
            PopAST();
            return;
          }
          else
          {
            //NOTE: for func native symbols we assume default arguments  
            //      are specified manually in bindings
            if(func_symb is FuncSymbolNative || 
              (func_symb is FuncSymbolScript fss && fss.HasDefaultArgAt(i)))
            {
              int default_arg_idx = i - required_args_num;
              if(!args_info.UseDefaultArg(default_arg_idx, true))
              {
                AddError(next_arg, "max default arguments reached");
                PopAST();
                return;
              }
            }
            else
            {
              AddError(next_arg, "missing argument '" + norm_cargs[i].orig.name + "'");
              PopAST();
              return;
            }
          }
        }
        else
        {
          if(na.ca.VARIADIC() != null)
          {
            AddError(na.ca, "not variadic argument");
            PopAST();
            return;
          }

          prev_ca = na.ca;
          if(!args_info.IncArgsNum())
          {
            AddError(na.ca, "max arguments reached");
            PopAST();
            return;
          }

          var func_arg_symb = func_symb.GetArg(i);

          bool is_ref = na.ca.REF() != null;
          if(!is_ref && func_arg_symb.is_ref)
          {
            AddError(na.ca, "'ref' is missing");
            PopAST();
            return;
          }
          else if(is_ref && !func_arg_symb.is_ref)
          {
            AddError(na.ca, "argument is not a 'ref'");
            PopAST();
            return;
          }

          var func_arg_type = func_arg_symb.GuessType();
          if(func_arg_type == null)
          {
            AddError(na.ca, "type '" + func_arg_symb.type + "' not found");
            PopAST();
            return;
          }

          PushCallByRef(is_ref);
          PushJsonType(func_arg_type);
          PushAST(new AST_Interim());
          
          if(!TryVisit(na.ca))
          {
            PopAST();
            PopAST();
            return;
          }

          //let's check if there were any expressions compatible to be passed by ref
          if(is_ref)
          {
            if(!(na.ca.exp() is bhlParser.ExpChainContext ca_chain_exp) ||
                !(new ExpChain(ca_chain_exp, ca_chain_exp.chainExp()).IsVarAccess)) 
            {
              AddError(na.ca, "expression is not passable by 'ref'");
              PopAST();
              PopAST();
              return;
            }
          }

          PopAddOptimizeAST();
          PopJsonType();
          PopCallByRef();

          if(!types.CheckAssign(func_arg_type, Annotate(na.ca), errors))
          {
            PopAST();
            return;
          }
        }
      }
      //checking variadic argument
      else
      {
        if(!args_info.IncArgsNum())
        {
          AddError(na.ca, "max arguments reached");
          PopAST();
          return;
        }

        var func_arg_symb = func_symb.GetArg(i);
        var varg_arr_type = func_arg_symb.GuessType() as ArrayTypeSymbol;

        if(varg_arr_type == null)
        {
          AddError(na.ca, "type '" + func_arg_symb.type + "' not found");
          PopAST();
          return;
        }

        if(variadic_args.Count == 1 && variadic_args[0].VARIADIC() != null)
        {
          PushJsonType(varg_arr_type);
          bool ok = TryVisit(variadic_args[0]);
          PopJsonType();

          if(!ok || !types.CheckAssign(varg_arr_type, Annotate(variadic_args[0]), errors))
          {
            PopAST();
            return;
          }
        }
        else
        {
          var varg_type = varg_arr_type.item_type.Get();
          if(variadic_args.Count > 0 && varg_type == null)
          {
            AddError(na.ca, "type '" + varg_arr_type.item_type + "' not found");
            PopAST();
            return;
          }
          var varg_ast = new AST_JsonArr(varg_arr_type, cargs.Start.Line);

          PushAST(varg_ast);
          PushJsonType(varg_type);
          for(int vidx = 0; vidx < variadic_args.Count; ++vidx)
          {
            var vca = variadic_args[vidx];
            if(!TryVisit(vca))
              break;
            //the last item is added implicitely
            if(vidx+1 < variadic_args.Count)
              varg_ast.AddChild(new AST_JsonArrAddItem());

            if(!types.CheckAssign(varg_type, Annotate(vca), errors))
              break;
          }
          PopJsonType();
          PopAddAST();
        }
      }
    }

    PopAST();

    call.cargs_bits = args_info.bits;
  }

  void AddCallArgs(FuncSignature func_type, bhlParser.CallArgsContext cargs, ref AST_Call call)
  {     
    var func_args = func_type.arg_types;
    int ca_len = cargs.callArgsList() == null ? 0 : cargs.callArgsList().callArg().Length; 
    IParseTree prev_ca = null;
    PushAST(call);
    for(int i=0;i<func_args.Count;++i)
    {
      var arg_type_ref = func_args[i]; 

      if(i == ca_len)
      {
        var next_arg = FindNextCallArg(cargs, prev_ca);
        AddError(next_arg, "missing argument of type '" + arg_type_ref.path + "'");
        PopAST();
        return;
      }

      var ca = cargs.callArgsList().callArg()[i];
      var ca_name = ca.NAME();

      if(ca_name != null)
      {
        AddError(ca_name, "named arguments not supported for function pointers");
        PopAST();
        return;
      }

      var arg_type = arg_type_ref.Get();
      PushJsonType(arg_type);
      PushAST(new AST_Interim());
      bool ok = TryVisit(ca);
      PopAddOptimizeAST();
      PopJsonType();

      if(!ok || !types.CheckAssign(arg_type is RefType rt ? rt.subj.Get() : arg_type, Annotate(ca), errors))
      {
        PopAST();
        return;
      }

      if(arg_type_ref.Get() is RefType && ca.REF() == null)
      {
        AddError(ca, "'ref' is missing");
        PopAST();
        return;
      }
      else if(!(arg_type_ref.Get() is RefType) && ca.REF() != null)
      {
        AddError(ca, "argument is not a 'ref'");
        PopAST();
        return;
      }

      prev_ca = ca;
    }
    PopAST();

    if(ca_len != func_args.Count)
    {
      AddError(cargs, "too many arguments");
      return;
    }

    var args_info = new FuncArgsInfo();
    if(!args_info.SetArgsNum(func_args.Count))
    {
      AddError(cargs, "max arguments reached");
      return;
    }
    call.cargs_bits = args_info.bits;
  }

  static bool HasFuncCalls(AST_Tree ast)
  {
    if(ast is AST_Call call && 
        (call.type == EnumCall.FUNC || 
         call.type == EnumCall.MFUNC ||
         call.type == EnumCall.FUNC_VAR ||
         call.type == EnumCall.FUNC_MVAR ||
         call.type == EnumCall.LMBD
         ))
      return true;
    
    for(int i=0;i<ast.children.Count;++i)
    {
      if(ast.children[i] is AST_Tree sub)
      {
        if(HasFuncCalls(sub))
          return true;
      }
    }
    return false;
  }

  IParseTree FindNextCallArg(bhlParser.CallArgsContext cargs, IParseTree curr)
  {
    for(int i=0;i<cargs.callArgsList()?.callArg().Length;++i)
    {
      var ch = cargs.callArgsList().callArg()[i];
      if(ch == curr && (i+1) < cargs.callArgsList().callArg().Length)
        return cargs.callArgsList().callArg()[i+1];
    }

    //NOTE: graceful fallback
    return cargs;
  }

  FuncSignature ParseFuncSignature(bool is_coro, bhlParser.RetTypeContext ret_ctx, bhlParser.TypesContext types_ctx)
  {
    var ret_type = ParseType(ret_ctx);

    var arg_types = new List<Proxy<IType>>();
    if(types_ctx != null)
    {
      for(int i=0;i<types_ctx.refType().Length;++i)
      {
        var ref_type = types_ctx.refType()[i];
        var arg_type = ParseType(ref_type.type());
        if(ref_type.REF() != null)
        {
          LSP_AddSemanticToken(ref_type.REF(), SemanticToken.Keyword);
          arg_type = curr_scope.R().TRef(arg_type);
        }
        arg_types.Add(arg_type);
      }
    }

    return new FuncSignature(is_coro ? FuncSignatureAttrib.Coro : 0, ret_type, arg_types);
  }

  FuncSignature ParseFuncSignature(bhlParser.FuncTypeContext ctx)
  {
    LSP_AddSemanticToken(ctx.CORO(), SemanticToken.Keyword);

    return ParseFuncSignature(ctx.CORO() != null, ctx.retType(), ctx.types());
  }

  FuncSignature ParseFuncSignature(bool is_coro, Proxy<IType> ret_type, bhlParser.FuncParamsContext fparams, out int default_args_num)
  {
    default_args_num = 0;
    var sig = new FuncSignature(is_coro ? FuncSignatureAttrib.Coro : 0, ret_type);
    if(fparams != null)
    {
      for(int i=0;i<fparams.funcParamDeclare().Length;++i)
      {
        var tp = new Proxy<IType>();

        var vd = fparams.funcParamDeclare()[i];
        if(vd?.type() == null)
        {
          AddError(vd, "invalid func signature");
        }
        else
        {
          tp = ParseType(vd.type());
          if(vd.REF() != null)
            tp = curr_scope.R().T(new RefType(tp));

          if(vd.VARIADIC() != null)
          {
            if(vd.REF() != null)
              AddError(vd.REF(), "pass by ref not allowed");

            if(i != fparams.funcParamDeclare().Length-1)
              AddError(vd, "variadic argument must be last");

            if(vd.assignExp() != null)
              AddError(vd.assignExp(), "default argument is not allowed");

            sig.attribs |= FuncSignatureAttrib.VariadicArgs;
            ++default_args_num;
          }
          else if(vd.assignExp() != null)
            ++default_args_num;
        }

        sig.AddArg(tp);
      }
    }
    return sig;
  }

  FuncSignature ParseFuncSignature(bool is_coro, Proxy<IType> ret_type, bhlParser.FuncParamsContext fparams)
  {
    int default_args_num;
    return ParseFuncSignature(is_coro, ret_type, fparams, out default_args_num);
  }

  Proxy<IType> ParseType(bhlParser.RetTypeContext parsed)
  {
    Proxy<IType> tp;

    //convenience special case
    if(parsed == null)
      tp = Types.Void;
    else if(parsed.type().Length > 1)
    {
      var tuple = new TupleType();
      for(int i=0;i<parsed.type().Length;++i)
        tuple.Add(ParseType(parsed.type()[i]));
      tp = curr_scope.R().T(tuple);
    }
    else
      tp = ParseType(parsed.type()[0]);

    if(tp.Get() == null)
      AddError(parsed, "type '" + tp.path + "' not found");

    return tp;
  }

  Proxy<IType> ParseType(bhlParser.TypeContext ctx)
  {
    var tp = new Proxy<IType>();
    if(ctx.nsName() != null)
    {
      LSP_AddSemanticToken(ctx.nsName().dotName().NAME(), SemanticToken.Type);

      if(ctx.nsName().GetText() == "var")
      {
        AddError(ctx.nsName(), "unexpected expression");
        return tp;
      }

      tp = curr_scope.R().T(ctx.nsName().GetText());
    }
    else if(ctx.funcType() != null)
      tp = curr_scope.R().T(ParseFuncSignature(ctx.funcType()));

    if(ctx.ARR() != null)
    {
      if(tp.Get() == null)
      {
        AddError(ctx, "type '" + tp.path + "' not found");
        return tp;
      }
      tp = curr_scope.R().TArr(tp);
    }
    else if(ctx.mapType() != null)
    {
      if(tp.Get() == null)
      {
        AddError(ctx, "type '" + tp.path + "' not found");
        return tp;
      }
      var ktp = curr_scope.R().T(ctx.mapType().nsName().GetText());
      if(ktp.Get() == null)
      {
        AddError(ctx, "type '" + ktp.path + "' not found");
        return tp;
      }
      tp = curr_scope.R().TMap(ktp, tp);
    }

    if(tp.Get() == null)
    {
      AddError(ctx, "type '" + tp.path + "' not found");
      return tp;
    }

   return tp;
  }

  AST_Tree ProcLambda(
     ParserRuleContext ctx, 
     bhlParser.FuncLambdaContext lmb_ctx, 
     ref IType curr_type,
     bool yielded = false
   )
  {
    LSP_AddSemanticToken(lmb_ctx.FUNC(), SemanticToken.Keyword);
    LSP_AddSemanticToken(lmb_ctx.CORO(), SemanticToken.Keyword);

    if(yielded)
      CheckCoroCallValidity(ctx);

    var captures = ParseCaptureList(lmb_ctx.captureList());

    var tp = ParseType(lmb_ctx.retType());

    var func_name = Hash.CRC32(module.name) + "_lmb_" + lmb_ctx.Stop.Line;

    var upvals = new List<AST_UpVal>();
    var lmb_symb = new LambdaSymbol(
      Annotate(ctx), 
      func_name,
      ParseFuncSignature(lmb_ctx.CORO() != null, tp, lmb_ctx.funcParams()),
      upvals,
      captures,
      this.func_decl_stack
    );

    var ast = new AST_LambdaDecl(lmb_symb, upvals, lmb_ctx.Stop.Line);

    var scope_backup = curr_scope;
    PushScope(lmb_symb);

    //NOTE: lambdas are not defined (persisted) in any scope, however as a symbol resolve 
    //      fallback we set the scope to the one it's actually defined in during body parsing 
    lmb_symb.scope = scope_backup;

    var fparams = lmb_ctx.funcParams();
    if(fparams != null)
    {
      PushAST(ast.fparams());
      TryVisit(fparams);
      PopAST();
    }

    //NOTE: while we are inside lambda the eval type is its return type
    Annotate(ctx).eval_type = lmb_symb.GetReturnType();

    ParseFuncBlock(lmb_ctx, lmb_ctx.funcBlock(), lmb_ctx.retType(), ast);

    //NOTE: once we are out of lambda the eval type is the lambda itself
    curr_type = (IType)lmb_symb.signature;
    Annotate(ctx).eval_type = curr_type;

    PopScope();

    //NOTE: since lambda func symbol is currently compile-time only,
    //      we need to reflect local variables number in AST
    //      (for regular funcs this number is taken from a symbol)
    ast.local_vars_num = lmb_symb.local_vars_num;

    return ast;
  }

  Dictionary<VariableSymbol, UpvalMode> ParseCaptureList(bhlParser.CaptureListContext capture_list)
  {
    var sym2mode = new Dictionary<VariableSymbol, UpvalMode>();
    if(capture_list == null)
      return sym2mode;
    
    foreach(var captured in capture_list.NAME())
    {
      var var_symb = curr_scope.ResolveWithFallback(captured.GetText()) as VariableSymbol;
      if(var_symb == null)
      {
        AddError(captured, "symbol '" + captured.GetText() + "' not resolved");
      }
      else
      {
        if(sym2mode.ContainsKey(var_symb))
          AddError(captured, "symbol '" + captured.GetText() + "' is already included");
        else
          sym2mode[var_symb] = UpvalMode.COPY;
      }
    }

    return sym2mode;
  }

  bool ProcLambdaCall(
    ParserRuleContext ctx, 
    bhlParser.FuncLambdaContext lmb_ctx, 
    ExpChainItems chain_items,
    ref IType curr_type,
    bool write,
    bool yielded
  )
  {
    var ast = ProcLambda(
      ctx, 
      lmb_ctx, 
      ref curr_type,
      yielded
    );

    var interim = new AST_Interim();
    interim.AddChild(ast);
    PushAST(interim);

    var scope = curr_scope;
    ITerminalNode curr_name = null;
    Symbol curr_symb = null;

    if(!ProcChainItems(
      chain_items,
      0,
      ref curr_name,
      ref scope, 
      ref curr_type,
      ref curr_symb,
      write
    ))
    {
      PopAST();
      return false;
    }

    ValidateChainCall(
      ctx, 
      1,
      interim.children, 
      yielded
    );

    PopAST();
    Annotate(ctx).eval_type = curr_type;
    PeekAST().AddChild(interim);

    return true;
  }
  
  public override object VisitCallArg(bhlParser.CallArgContext ctx)
  {
    var exp = ctx.exp();
    if(TryVisit(exp))
      Annotate(ctx).eval_type = Annotate(exp).eval_type;
    return null;
  }

  public override object VisitExpJsonObj(bhlParser.ExpJsonObjContext ctx)
  {
    var json = ctx.jsonObject();
    if(TryVisit(json))
      Annotate(ctx).eval_type = Annotate(json).eval_type;
    return null;
  }

  public override object VisitExpJsonArr(bhlParser.ExpJsonArrContext ctx)
  {
    var json = ctx.jsonArray();
    if(TryVisit(json))
      Annotate(ctx).eval_type = Annotate(json).eval_type;
    return null;
  }

  public override object VisitJsonObject(bhlParser.JsonObjectContext ctx)
  {
    var new_exp = ctx.newExp();

    if(new_exp != null)
    {
      var tp = ParseType(new_exp.type());
      PushJsonType(tp.Get());
    }

    var curr_type = PeekJsonType();

    if(curr_type == null)
    {
      AddError(ctx, "can't determine type of {..} expression");
      return null;
    }

    if(!(curr_type is ClassSymbol) || 
        (curr_type is ArrayTypeSymbol) ||
        (curr_type is MapTypeSymbol)
        )
    {
      AddError(ctx, "type '" + curr_type + "' can't be specified with {..}");
      return null;
    }

    if(curr_type is ClassSymbolNative csn && csn.creator == null)
    {
      AddError(ctx, "constructor is not defined");
      return null;
    }

    Annotate(ctx).eval_type = curr_type;

    var ast = new AST_JsonObj(curr_type, ctx.Start.Line);

    PushAST(ast);
    var pairs = ctx.jsonPair();
    for(int i=0;i<pairs.Length;++i)
    {
      var pair = pairs[i]; 
      TryVisit(pair);
    }
    PopAST();

    if(new_exp != null)
      PopJsonType();

    PeekAST().AddChild(ast);
    return null;
  }

  public override object VisitJsonArray(bhlParser.JsonArrayContext ctx)
  {
    var curr_type = PeekJsonType();
    if(curr_type == null)
    {
      AddError(ctx, "can't determine type of [..] expression");
      return null;
    }

    if(!(curr_type is ArrayTypeSymbol) && !(curr_type is MapTypeSymbol))
    {
      AddError(ctx, "type '" + curr_type + "' can't be specified with [..]");
      return null;
    }

    if(curr_type is ArrayTypeSymbol arr_type)
    {
      var orig_type = arr_type.item_type.Get();
      if(orig_type == null)
      {
        AddError(ctx,  "type '" + arr_type.item_type.path + "' not found");
        return null;
      }
      PushJsonType(orig_type);

      var ast = new AST_JsonArr(arr_type, ctx.Start.Line);

      PushAST(ast);
      var vals = ctx.jsonValue();
      for(int i=0;i<vals.Length;++i)
      {
        TryVisit(vals[i]);
        //the last item is added implicitely
        if(i+1 < vals.Length)
          ast.AddChild(new AST_JsonArrAddItem());
      }
      PopAST();

      PopJsonType();

      Annotate(ctx).eval_type = arr_type;

      PeekAST().AddChild(ast);
    }
    else if(curr_type is MapTypeSymbol map_type)
    {
      var key_type = map_type.key_type.Get();
      if(key_type == null)
      {
        AddError(ctx,  "type '" + map_type.key_type.path + "' not found");
        return null;
      }
      var val_type = map_type.val_type.Get();
      if(val_type == null)
      {
        AddError(ctx,  "type '" + map_type.val_type.path + "' not found");
        return null;
      }
      var ast = new AST_JsonMap(curr_type, ctx.Start.Line);

      PushAST(ast);
      var vals = ctx.jsonValue();
      for(int i=0;i<vals.Length;++i)
      {
        var val = vals[i].exp() as bhlParser.ExpJsonArrContext;
        if(val?.jsonArray()?.jsonValue()?.Length != 2)
        {
          AddError(val == null ? (IParseTree)ctx : (IParseTree)val,  "[k, v] expected");
          continue;
        }

        PushJsonType(key_type);
        TryVisit(val.jsonArray().jsonValue()[0]);
        PopJsonType();

        PushJsonType(val_type);
        TryVisit(val.jsonArray().jsonValue()[1]);
        PopJsonType();

        //the last item is added implicitely
        if(i+1 < vals.Length)
          ast.AddChild(new AST_JsonMapAddItem());
      }
      PopAST();


      Annotate(ctx).eval_type = map_type;

      PeekAST().AddChild(ast);
    }
    return null;
  }

  public override object VisitJsonPair(bhlParser.JsonPairContext ctx)
  {
    LSP_AddSemanticToken(ctx.NAME(), SemanticToken.Variable);

    var curr_type = PeekJsonType();
    var scoped_symb = curr_type as ClassSymbol;
    if(scoped_symb == null)
    {
      AddError(ctx, "expecting class type, got '" + curr_type + "' instead");
      return null;
    }

    var name_str = ctx.NAME().GetText();
    
    var member = scoped_symb.ResolveWithFallback(name_str) as VariableSymbol;
    if(member == null)
    {
      AddError(ctx, "no such attribute '" + name_str + "' in class '" + scoped_symb.name + "'");
      return null;
    }

    LSP_SetSymbol(ctx, member);

    var ast = new AST_JsonPair(curr_type, name_str, member.scope_idx, ctx.NAME().Symbol.Line);

    PushJsonType(member.type.Get());

    var jval = ctx.jsonValue(); 
    PushAST(ast);
    TryVisit(jval);
    PopAST();

    PopJsonType();

    Annotate(ctx).eval_type = member.type.Get();

    PeekAST().AddChild(ast);
    return null;
  }

  public override object VisitJsonValue(bhlParser.JsonValueContext ctx)
  {
    var exp = ctx.exp();

    var curr_type = PeekJsonType();
    if(TryVisit(exp))
    {
      Annotate(ctx).eval_type = Annotate(exp).eval_type;
      types.CheckAssign(curr_type, Annotate(exp), errors);
    }

    return null;
  }

  public override object VisitExpTypeof(bhlParser.ExpTypeofContext ctx)
  {
    LSP_AddSemanticToken(ctx.TYPEOF(), SemanticToken.Keyword);

    var tp = ParseType(ctx.type());

    Annotate(ctx).eval_type = Types.ClassType;

    PeekAST().AddChild(new AST_Typeof(tp.Get()));

    return null;
  }

  public override object VisitExpChain(bhlParser.ExpChainContext ctx)
  {
    IType curr_type = null;
    ProcExpChain(ctx, ctx.chainExp(), ref curr_type);

    Annotate(ctx).eval_type = curr_type;

    return null;
  }
  
  //TODO: this is almost a copy paste of the code above, it's used 
  //      when we need to traverse chainExp rule when it is not a part of
  //      #ExpChain 
  public override object VisitChainExp(bhlParser.ChainExpContext ctx)
  {
    IType curr_type = null;
    ProcExpChain(ctx, ctx, ref curr_type);

    Annotate(ctx).eval_type = curr_type;

    return null;
  }
  
  public override object VisitExpLambda(bhlParser.ExpLambdaContext ctx)
  {
    IType curr_type = null;
    var ast = ProcLambda(
      ctx, 
      ctx.funcLambda(), 
      ref curr_type
    );

    Annotate(ctx).eval_type = curr_type;
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpYieldCall(bhlParser.ExpYieldCallContext ctx)
  {
    var exp_type = ProcFuncCallExp(ctx, ctx.chainExp(), yielded: true);
    Annotate(ctx).eval_type = exp_type;
    return null;
  }

  void CheckCoroCallValidity(ParserRuleContext ctx)
  {
    var curr_func = PeekFuncDecl();
    if(curr_func == null)
    {
      AddError(ctx, "invalid context");
      return;
    }

    if(!curr_func.attribs.HasFlag(FuncAttrib.Coro))
    {
      AddError(curr_func.origin, "function with yield calls must be coro");
      return;
    }

    if(GetBlockLevel(BlockType.DEFER) != -1)
    {
      AddError(ctx, "yield is not allowed in defer block");
      return;
    }

    has_yield_calls.Add(curr_func);
  }

  public override object VisitExpNew(bhlParser.ExpNewContext ctx)
  {
    LSP_AddSemanticToken(ctx.newExp().NEW(), SemanticToken.Keyword);

    var cl = ParseType(ctx.newExp().type()).Get();
    Annotate(ctx).eval_type = cl;

    if(cl is ClassSymbolNative csn && csn.creator == null)
    {
      AddError(ctx, "constructor is not defined");
      return null;
    }

    if(ctx.newExp().type().nsName() != null)
     LSP_SetSymbol(ctx.newExp().type().nsName().dotName(), cl as Symbol);

    var ast = new AST_New(cl, ctx.Start.Line);
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitAssignExp(bhlParser.AssignExpContext ctx)
  {
    var exp = ctx.exp();

    if(TryVisit(exp))
      Annotate(ctx).eval_type = Annotate(exp).eval_type;

    return null;
  }

  public override object VisitExpTypeCast(bhlParser.ExpTypeCastContext ctx)
  {
    var tp = ParseType(ctx.type());

    var cast_type = tp.Get();
    bool force_type = NeedToForceCastType(cast_type);

    var ast = new AST_TypeCast(cast_type, force_type, ctx.Start.Line);
    var exp = ctx.exp();
    PushAST(ast);
    bool ok = TryVisit(exp);
    PopAST();

    Annotate(ctx).eval_type = cast_type;

    if(ok)
      Types.CheckCast(Annotate(ctx), Annotate(exp), errors); 

    ast.hint_exp_type = Annotate(exp).eval_type;

    PeekAST().AddChild(ast);

    return null;
  }

  static bool NeedToForceCastType(IType cast_type)
  {
    //NOTE: For native types we need to enforce the cast type since we don't have
    //      vtables for them like for userland classes. (for native types we rely on 
    //      C# casting checks anyway)
    //
    //      If we don't enforce the type then later method/properties calls will use wrong type info.
    //
    //      We have no control on which instance of the native class is actually will 
    //      be set in the runtime when, say, the base class is used as a returned type 
    //      in the function signature.
    //
    //      For example, here's the native base and child classes:
    //
    //      class Base {
    //         int a;
    //      }
    //
    //      class Child : Base {
    //         int b;
    //      }
    //
    //      Base make() {
    //        return new Child() //this happens in C# bindings!
    //      }
    //
    //      Now if we make bindings for these entities the returned type of the 'make()' 
    //      function will be Base and the following bhl code will throw an exception in runtime
    //      without enforcing of the type:
    //
    //      Base b = make()
    //      Child c = (Child)b // without 'type enforcing' value's type will be 'Base'    
    //      c.b = 10           // runtime error: b's index can't be found in 'Base'
    //
    //      Or for example, here's the native interface and class:
    //
    //      interface IFoo {
    //      }
    //
    //      class Foo : IFoo {
    //        public int X() { .. }
    //
    //        static IFoo create() {
    //          return new Foo(); //this happens in C# bindings!
    //        }
    //      }
    //
    //      IFoo ifoo = create()
    //      Foo foo = (Foo)foo // without 'type enforcing' value's type will be 'IFoo'    
    //      foo.X()            // runtime error: X's index can't be found in 'IFoo'
    //
    //      In case of userland classes if we enforce the cast type we wipe information about 
    //      the original type and later virtual/interface method invocations will be wrong.
    //      For userland classes we build vtables/itables which contain all the neccessary 
    //      information for proper methods dispatching. Basically enforcing the cast type for 
    //      userland classes roughly equals 'static casting' in C++. We do that only in some
    //      edge cases, e.g. when calling 'base' virtual class method implementation from the 
    //      overriden one 
    return cast_type is ClassSymbolNative || 
      cast_type is InterfaceSymbolNative;
  }

  public override object VisitExpAs(bhlParser.ExpAsContext ctx)
  {
    LSP_AddSemanticToken(ctx.AS(), SemanticToken.Keyword);

    var tp = ParseType(ctx.type());
    var cast_type = tp.Get();
    bool force_type = NeedToForceCastType(cast_type);

    var ast = new AST_TypeAs(cast_type, force_type, ctx.Start.Line);
    var exp = ctx.exp();
    PushAST(ast);
    TryVisit(exp);
    PopAST();

    Annotate(ctx).eval_type = tp.Get();

    //TODO: do we need to pre-check absolutely unrelated types?
    //types.CheckCast(Annotate(ctx), Annotate(exp)); 

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpIs(bhlParser.ExpIsContext ctx)
  {
    LSP_AddSemanticToken(ctx.IS(), SemanticToken.Keyword);

    var tp = ParseType(ctx.type());

    var ast = new AST_TypeIs(tp.Get(), ctx.Start.Line);
    var exp = ctx.exp();
    PushAST(ast);
    TryVisit(exp);
    PopAST();

    Annotate(ctx).eval_type = Types.Bool;

    //TODO: do we need to pre-check absolutely unrelated types?
    //types.CheckCast(Annotate(ctx), Annotate(exp)); 

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpUnary(bhlParser.ExpUnaryContext ctx)
  {
    LSP_AddSemanticToken(ctx.operatorUnary(), SemanticToken.Keyword);

    EnumUnaryOp type = ctx.operatorUnary().NOT() != null ? EnumUnaryOp.NOT : EnumUnaryOp.NEG;

    var ast = new AST_UnaryOpExp(type);
    var exp = ctx.exp(); 
    PushAST(ast);
    bool ok = TryVisit(exp);
    PopAST();

    if(ok)
    {
      Annotate(ctx).eval_type = type == EnumUnaryOp.NEG ? 
        types.CheckUnaryMinus(Annotate(exp), errors) : 
        types.CheckLogicalNot(Annotate(exp), errors);
    }

    PeekAST().AddChild(ast);

    return null;
  }

  bool ProcExpModifyOp(
    ParserRuleContext ctx, 
    bhlParser.ChainExpContext chain_ctx, 
    bhlParser.ModifyOpContext op_ctx
  )
  {
    if(op_ctx.operatorSelfOp() != null)
    {
      LSP_AddSemanticToken(op_ctx.operatorSelfOp(), SemanticToken.Operator);

      string post_op = op_ctx.operatorSelfOp().GetText();
      ProcBinOp(ctx, post_op.Substring(0, 1), chain_ctx, op_ctx.exp(), lhs_self_op: true);

      var chain = new ExpChain(ctx, chain_ctx);
      if(!chain.IsVarAccess)
      {
        AddError(chain_ctx, "unexpected expression");
        return false;
      }

      IType curr_type = null;
      if(!ProcExpChain(chain, ref curr_type, write: true))
        return false;

      if(chain.Incomplete)
      {
        AddError(ctx, "incomplete statement");
        return false;
      }

      //NOTE: strings concat special case
      if(curr_type == Types.String && post_op == "+=")
        return true;

      if(!Types.IsNumeric(curr_type))
      {
        AddError(ctx, "is not numeric type");
        return false;
      }

      return true;
    }
    else if(op_ctx.operatorIncDec() != null)
      return ProcPostIncDec(ctx, chain_ctx, op_ctx.operatorIncDec());
    else if(op_ctx.assignExp() != null) 
    {
      var vproxy = new VarsOrDeclsProxy(new bhlParser.ChainExpContext[] { chain_ctx });
      return ProcDeclOrAssign(vproxy, op_ctx.assignExp(), op_ctx.Start.Line);
    }
    
    return true;
  }

  public override object VisitExpAddSub(bhlParser.ExpAddSubContext ctx)
  {
    LSP_AddSemanticToken(ctx.operatorAddSub(), SemanticToken.Operator);

    var op = ctx.operatorAddSub().GetText(); 

    ProcBinOp(ctx, op, ctx.exp(0), ctx.exp(1));

    return null;
  }

  public override object VisitExpMulDivMod(bhlParser.ExpMulDivModContext ctx)
  {
    LSP_AddSemanticToken(ctx.operatorMulDivMod(), SemanticToken.Operator);

    var op = ctx.operatorMulDivMod().GetText(); 

    ProcBinOp(ctx, op, ctx.exp(0), ctx.exp(1));

    return null;
  }

  bhlParser.ExpContext _one_literal_exp;
  bhlParser.ExpContext one_literal_exp {
    get {
      //TODO: make expression AST on the fly somehow?
      //exp = new bhlParser.ExpLiteralNumContext("1");
      if(_one_literal_exp == null)
      {
        _one_literal_exp = Stream2Parser(
          new Module(types), 
          null,
          ErrorHandlers.MakeStandard("", new CompileErrors()),
          new MemoryStream(System.Text.Encoding.UTF8.GetBytes("1")), 
          defines: null
        ).exp();
      }
      return _one_literal_exp;
    }
  }

  bool ProcPostIncDec(ParserRuleContext ctx, bhlParser.ChainExpContext chain_exp, bhlParser.OperatorIncDecContext inc_dec)
  {
    LSP_AddSemanticToken(inc_dec, SemanticToken.Operator);

    var chain = new ExpChain(ctx, chain_exp);
    if(chain.Incomplete)
    {
      AddError(ctx, "incomplete statement");
      return false;
    }

    //NOTE: let's process the assignment the operation result first 
    //      so that we have nice type check errors, however we need to 
    //      put the resulting AST after binary operation processing below 
    var chain_ast = new AST_Interim();
    PushAST(chain_ast);
    IType curr_type = null;
    if(!ProcExpChain(chain, ref curr_type, write: true))
    {
      PopAST();
      return false;
    }
    PopAST();

    if(!Types.IsNumeric(curr_type))
    {
      AddError(ctx, "only numeric types supported");
      return false;
    }

    //let's tweak the fake "1" expression placement
    //by assinging it the call expression placement
    var ann_one = Annotate(one_literal_exp);
    var ann_exp = Annotate(chain_exp);
    ann_one.module = ann_exp.module;
    ann_one.tree = ann_exp.tree;
    ann_one.tokens = ann_exp.tokens;

    //1. let's add/sub expression and '1' 
    ProcBinOp(ctx, inc_dec.GetText() == "++" ? "+" : "-", chain_exp, one_literal_exp);

    //2. let's attach the assignment
    PeekAST().AddChildren(chain_ast);

    return true;
  }
  
  public override object VisitExpCompare(bhlParser.ExpCompareContext ctx)
  {
    LSP_AddSemanticToken(ctx.operatorComparison(), SemanticToken.Operator);

    var op = ctx.operatorComparison().GetText(); 

    ProcBinOp(ctx, op, ctx.exp(0), ctx.exp(1));

    return null;
  }

  static EnumBinaryOp GetBinaryOpType(string op)
  {
    EnumBinaryOp op_type;

    if(op == "+")
      op_type = EnumBinaryOp.ADD;
    else if(op == "-")
      op_type = EnumBinaryOp.SUB;
    else if(op == "==")
      op_type = EnumBinaryOp.EQ;
    else if(op == "!=")
      op_type = EnumBinaryOp.NQ;
    else if(op == "*")
      op_type = EnumBinaryOp.MUL;
    else if(op == "/")
      op_type = EnumBinaryOp.DIV;
    else if(op == "%")
      op_type = EnumBinaryOp.MOD;
    else if(op == ">")
      op_type = EnumBinaryOp.GT;
    else if(op == ">=")
      op_type = EnumBinaryOp.GTE;
    else if(op == "<")
      op_type = EnumBinaryOp.LT;
    else if(op == "<=")
      op_type = EnumBinaryOp.LTE;
    else
      throw new Exception("Unknown type: " + op);

    return op_type;
  }

  void ProcBinOp(ParserRuleContext ctx, string op, IParseTree lhs, IParseTree rhs, bool lhs_self_op = false)
  {
    EnumBinaryOp op_type = GetBinaryOpType(op);
    AST_Tree ast = new AST_BinaryOpExp(op_type, ctx.Start.Line);
    PushAST(ast);
    bool ok1 = TryVisit(lhs);
    int ops_edge_idx = ast.children.Count;
    bool ok2 = TryVisit(rhs);
    PopAST();

    if(!ok1 || !ok2)
      return;

    var ann_lhs = Annotate(lhs);
    var ann_rhs = Annotate(rhs);

    //NOTE: checking if there's binary operator overload
    if(ann_lhs.eval_type is ClassSymbol class_symb  && class_symb.Resolve(op) is FuncSymbol)
    {
      var op_func = class_symb.Resolve(op) as FuncSymbol;

      Annotate(ctx).eval_type = types.CheckBinOpOverload(ns, ann_lhs, ann_rhs, op_func, errors);

      //NOTE: replacing original AST, a bit 'dirty' but kinda OK
      var over_ast = new AST_Interim();
      for(int i=0;i<ast.children.Count;++i)
        over_ast.AddChild(ast.children[i]);
      var op_call = new AST_Call(EnumCall.FUNC, ctx.Start.Line, op_func, 2/*cargs bits*/);
      over_ast.AddChild(op_call);
      ast = over_ast;
    }
    else if(
      op_type == EnumBinaryOp.EQ || 
      op_type == EnumBinaryOp.NQ
    )
      Annotate(ctx).eval_type = types.CheckEqBinOp(ann_lhs, ann_rhs, errors);
    else if(
      op_type == EnumBinaryOp.GT || 
      op_type == EnumBinaryOp.GTE ||
      op_type == EnumBinaryOp.LT || 
      op_type == EnumBinaryOp.LTE
    )
      Annotate(ctx).eval_type = types.CheckRelationalBinOp(ann_lhs, ann_rhs, errors);
    else
    {
      if(op == "+" && CheckImplicitCastToString(ctx, ast, ops_edge_idx, ann_lhs, ann_rhs, lhs_self_op))
        Annotate(ctx).eval_type = Types.String;
      else
        Annotate(ctx).eval_type = types.CheckBinOp(ann_lhs, ann_rhs, errors);
    }

    PeekAST().AddChild(ast);
  }

  static bool SupportsImplictCastToString(IType type)
  {
    return Types.IsNumeric(type) || type == Types.Bool || type is EnumSymbol;
  }

  bool CheckImplicitCastToString(ParserRuleContext ctx, AST_Tree ast, int ops_edge_idx, AnnotatedParseTree ann_lhs, AnnotatedParseTree ann_rhs, bool lhs_self_op)
  {
    //NOTE: only if it's NOT a 'left-side' modifying operation (e.g i += "foo")
    if(!lhs_self_op && SupportsImplictCastToString(ann_lhs.eval_type) && ann_rhs.eval_type == Types.String)
    {
      ast.children.Insert(ops_edge_idx, new AST_TypeCast(Types.String, force_type: false, line_num: ctx.Start.Line)); 
      return true;
    }
    else if(ann_lhs.eval_type == Types.String && SupportsImplictCastToString(ann_rhs.eval_type))
    {
      ast.children.Insert(ast.children.Count, new AST_TypeCast(Types.String, force_type: false, line_num: ctx.Start.Line)); 
      return true;
    }
    return false;
  }

  public override object VisitExpBitwise(bhlParser.ExpBitwiseContext ctx)
  {
    LSP_AddSemanticToken(ctx.operatorBitwise(), SemanticToken.Operator);

    var ast = new AST_BinaryOpExp(ctx.operatorBitwise().BOR() != null ? EnumBinaryOp.BIT_OR : EnumBinaryOp.BIT_AND, ctx.Start.Line);
    var exp_0 = ctx.exp(0);
    var exp_1 = ctx.exp(1);

    PushAST(ast);
    bool ok1 = TryVisit(exp_0);
    bool ok2 = TryVisit(exp_1);
    PopAST();

    if(!ok1 || !ok2)
      return null;

    Annotate(ctx).eval_type = types.CheckBitOp(Annotate(exp_0), Annotate(exp_1), errors);

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLogical(bhlParser.ExpLogicalContext ctx)
  {
    LSP_AddSemanticToken(ctx.operatorLogical(), SemanticToken.Operator);

    var ast = new AST_BinaryOpExp(ctx.operatorLogical().LOR() != null ? EnumBinaryOp.OR : EnumBinaryOp.AND, ctx.Start.Line);
    var exp_0 = ctx.exp(0);
    var exp_1 = ctx.exp(1);

    //logical operand node has exactly two children
    var tmp0 = new AST_Interim();
    PushAST(tmp0);
    bool ok1 = TryVisit(exp_0);
    PopAST();
    ast.AddChild(tmp0);

    var tmp1 = new AST_Interim();
    PushAST(tmp1);
    bool ok2 = TryVisit(exp_1);
    PopAST();
    ast.AddChild(tmp1);

    if(!ok1 || !ok2)
      return null;

    Annotate(ctx).eval_type = types.CheckLogicalOp(Annotate(exp_0), Annotate(exp_1), errors);

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralNum(bhlParser.ExpLiteralNumContext ctx)
  {
    AST_Literal ast = null;

    LSP_AddSemanticToken(ctx.number(), SemanticToken.Number);

    var number = ctx.number();
    var int_num = number.INT();
    var flt_num = number.FLOAT();
    var hex_num = number.HEX();

    if(int_num != null)
    {
      ast = new AST_Literal(ConstType.INT);
      Annotate(ctx).eval_type = Types.Int;
      ast.nval = double.Parse(int_num.GetText(), System.Globalization.CultureInfo.InvariantCulture);
    }
    else if(flt_num != null)
    {
      ast = new AST_Literal(ConstType.FLT);
      Annotate(ctx).eval_type = Types.Float;
      ast.nval = double.Parse(flt_num.GetText(), System.Globalization.CultureInfo.InvariantCulture);
    }
    else if(hex_num != null)
    {
      ast = new AST_Literal(ConstType.INT);
      Annotate(ctx).eval_type = Types.Int;
      ast.nval = Convert.ToUInt32(hex_num.GetText(), 16);
    }
    else
    {
      AddError(ctx, "unknown numeric literal type");
      return null;
    }

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralFalse(bhlParser.ExpLiteralFalseContext ctx)
  {
    LSP_AddSemanticToken(ctx.FALSE(), SemanticToken.Keyword);
      
    Annotate(ctx).eval_type = Types.Bool;

    var ast = new AST_Literal(ConstType.BOOL);
    ast.nval = 0;
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralNull(bhlParser.ExpLiteralNullContext ctx)
  {
    LSP_AddSemanticToken(ctx.NULL(), SemanticToken.Keyword);

    Annotate(ctx).eval_type = Types.Null;

    var ast = new AST_Literal(ConstType.NIL);
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralTrue(bhlParser.ExpLiteralTrueContext ctx)
  {
    LSP_AddSemanticToken(ctx.TRUE(), SemanticToken.Keyword);

    Annotate(ctx).eval_type = Types.Bool;

    var ast = new AST_Literal(ConstType.BOOL);
    ast.nval = 1;
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralStr(bhlParser.ExpLiteralStrContext ctx)
  {
    LSP_AddSemanticToken(ctx.@string().NORMALSTRING(), SemanticToken.String);

    Annotate(ctx).eval_type = Types.String;

    var ast = new AST_Literal(ConstType.STR);
    ast.sval = ctx.@string().NORMALSTRING().GetText();
    //removing quotes
    ast.sval = ast.sval.Substring(1, ast.sval.Length-2);
    //adding convenience support for newlines and tabs
    ast.sval = ast.sval.Replace("\\n", "\n");
    ast.sval = ast.sval.Replace("\\\n", "\\n");
    ast.sval = ast.sval.Replace("\\t", "\t");
    ast.sval = ast.sval.Replace("\\\t", "\\t");
    PeekAST().AddChild(ast);

    return null;
  }

  FuncSymbolScript PeekFuncDecl()
  {
    if(func_decl_stack.Count == 0)
      return null;

    return func_decl_stack[func_decl_stack.Count-1];
  }
  void PushJsonType(IType type)
  {
    json_type_stack.Push(type);
  }

  void PopJsonType()
  {
    json_type_stack.Pop();
  }

  IType PeekJsonType()
  {
    if(json_type_stack.Count == 0)
      return null;

    return json_type_stack.Peek();
  }

  void PushCallByRef(bool flag)
  {
    call_by_ref_stack.Push(flag);
  }

  void PopCallByRef()
  {
    call_by_ref_stack.Pop();
  }

  bool PeekCallByRef()
  {
    if(call_by_ref_stack.Count == 0)
      return false;

    return call_by_ref_stack.Peek();
  }

  static bool HasErrors(IParseTree tree)
  {
    for(int i=0;i<tree.ChildCount;++i)
      if(tree.GetChild(i) is ErrorNodeImpl)
        return true;
    return false;
  }

  static bool IsValid(IParseTree tree)
  {
    if(tree == null)
      return false;
    return !HasErrors(tree);
  }

  public override object VisitStmReturn(bhlParser.StmReturnContext ctx)
  {
    LSP_AddSemanticToken(ctx.RETURN(), SemanticToken.Keyword);

    var ret_val = ctx.expList();

    if(CountBlocks(BlockType.DEFER) > 0)
      //we can proceed
      AddError(ctx, "return is not allowed in defer block");

    var func_symb = PeekFuncDecl();
    if(func_symb == null)
    {
      AddError(ctx, "return statement is not in function");
      return null;
    }
    
    return_found.Add(func_symb);

    var ret_ast = new AST_Return(ctx.Start.Line);
    
    if(ret_val != null)
    {
      int explen = ret_val.exp().Length;

      var fret_type = func_symb.GetReturnType();

      //NOTE: immediately adding return node in case of void return type
      if(fret_type == Types.Void)
        PeekAST().AddChild(ret_ast);
      else
        PushAST(ret_ast);

      var fmret_type = fret_type as TupleType;

      //NOTE: there can be a situation when explen == 1 but the return type 
      //      is actually a multi return, like in the following case:
      //
      //      func int,string bar() {
      //        return foo()
      //      }
      //
      //      where foo has the following signature: func int,string foo() {..}
      if(explen == 1)
      {
        var exp_item = ret_val.exp()[0];
        PushJsonType(fret_type);
        bool ok = TryVisit(exp_item);
        PopJsonType();

        if(!ok)
          return null;

        if(Annotate(exp_item).eval_type != Types.Void)
          ret_ast.num = fmret_type != null ? fmret_type.Count : 1;

        if(!types.CheckAssign(func_symb.origin.parsed, Annotate(exp_item), errors))
          return null;
        Annotate(ctx).eval_type = Annotate(exp_item).eval_type;
      }
      else
      {
        if(fmret_type == null)
        {
          AddError(ctx, "function doesn't support multi return");
          return null;
        }

        if(fmret_type.Count != explen)
        {
          AddError(ctx, "multi return size doesn't match destination");
          return null;
        }

        var ret_type = new TupleType();

        //NOTE: we traverse expressions in reversed order so that returned
        //      values are properly placed on a stack
        for(int i=explen;i-- > 0;)
        {
          var exp = ret_val.exp()[i];
          if(!TryVisit(exp))
            return null;
          var exp_eval_type = Annotate(exp).eval_type;
          if(exp_eval_type == null)
            return null;
          ret_type.Add(curr_scope.R().T(exp_eval_type));
        }

        //type checking is in proper order
        for(int i=0;i<explen;++i)
        {
          var exp = ret_val.exp()[i];
          if(!types.CheckAssign(fmret_type[i].Get(), Annotate(exp), errors))
            return null;
        }

        Annotate(ctx).eval_type = ret_type;

        ret_ast.num = fmret_type.Count;
      }

      if(fret_type != Types.Void)
      {
        PopAST();
        PeekAST().AddChild(ret_ast);
      }
    }
    else
    {
      if(func_symb.GetReturnType() != Types.Void)
      {
        AddError(ctx, "return value is missing");
        return null;
      }
      Annotate(ctx).eval_type = Types.Void;
      PeekAST().AddChild(ret_ast);
    }

    return null;
  }

  public override object VisitStmBreak(bhlParser.StmBreakContext ctx)
  {
    LSP_AddSemanticToken(ctx.BREAK(), SemanticToken.Keyword);

    int loop_level = GetLoopBlockLevel();

    if(loop_level == -1)
    {
      AddError(ctx, "not within loop construct");
      return null;
    }

    if(GetBlockLevel(BlockType.DEFER) > loop_level)
    {
      AddError(ctx, "not within loop construct");
      return null;
    }

    PeekAST().AddChild(new AST_Break());

    return null;
  }

  public override object VisitStmContinue(bhlParser.StmContinueContext ctx)
  {
    LSP_AddSemanticToken(ctx.CONTINUE(), SemanticToken.Keyword);

    int loop_level = GetLoopBlockLevel();

    if(loop_level == -1)
    {
      AddError(ctx, "not within loop construct");
      return null;
    }

    if(GetBlockLevel(BlockType.DEFER) > loop_level)
    {
      AddError(ctx, "not within loop construct");
      return null;
    }

    PeekAST().AddChild(new AST_Continue());

    return null;
  }

  void ProcessDecl(bhlParser.DeclContext ctx)
  {
    var nsdecl = ctx.nsDecl();
    if(nsdecl != null)
    {
      TryVisit(nsdecl);
      return;
    }
    var vdecl = ctx.varDeclareOptAssign();
    if(vdecl != null)
    {
      AddPass(vdecl, curr_scope, PeekAST()); 
      return;
    }
    var fndecl = ctx.funcDecl();
    if(fndecl != null)
    {
      AddPass(fndecl, curr_scope, PeekAST());
      return;
    }
    var cldecl = ctx.classDecl();
    if(cldecl != null)
    {
      AddPass(cldecl, curr_scope, PeekAST());
      return;
    }
    var ifacedecl = ctx.interfaceDecl();
    if(ifacedecl != null)
    {
      AddPass(ifacedecl, curr_scope, PeekAST());
      return;
    }
    var edecl = ctx.enumDecl();
    if(edecl != null)
    {
      AddPass(edecl, curr_scope, PeekAST());
      return;
    }
  }

  void Pass_OutlineFuncDecl(ParserPass pass)
  {
    if(pass.func_ctx == null)
      return;

    LSP_AddSemanticToken(pass.func_ctx.FUNC(), SemanticToken.Keyword);
    LSP_AddSemanticToken(pass.func_ctx.NAME(), SemanticToken.Function, SemanticModifier.Definition);
    
    foreach(var attr in pass.func_ctx.funcAttribs())
      LSP_AddSemanticToken(attr, SemanticToken.Keyword);

    string name = pass.func_ctx.NAME().GetText();

    var func_ann = Annotate(pass.func_ctx);
    pass.func_symb = new FuncSymbolScript(func_ann, new FuncSignature(), name); 

    foreach(var attr in pass.func_ctx.funcAttribs())
    {
      var attr_type = FuncAttrib.None;

      if(attr.CORO() != null)
      {
        attr_type = FuncAttrib.Coro;
        LSP_AddSemanticToken(attr.CORO(), SemanticToken.Keyword);
      }
      else if(attr.STATIC() != null)
      {
        attr_type = FuncAttrib.Static;
        LSP_AddSemanticToken(attr.STATIC(), SemanticToken.Keyword);
      }
      else
      {
        //we can proceed after this error
        AddError(attr, "improper usage of attribute");
        continue;
      }

      if(pass.func_symb.attribs.HasFlag(attr_type))
        AddError(attr, "this attribute is set already");

      pass.func_symb.attribs |= attr_type;
    }

    if(!pass.scope.TryDefine(pass.func_symb, out SymbolError err))
    {
      AddError(pass.func_ctx.NAME(), err.Message);
      pass.Clear();
      return;
    }

    LSP_SetSymbol(func_ann, pass.func_symb);
    pass.func_ast = new AST_FuncDecl(pass.func_symb, pass.func_ctx.Stop.Line);
    pass.ast.AddChild(pass.func_ast);
  }

  void Pass_ParseFuncSignature_1(ParserPass pass)
  {
    if(pass.func_symb == null)
      return;

    pass.func_symb.signature = ParseFuncSignature(
      pass.func_symb.attribs.HasFlag(FuncAttrib.Coro),
      ParseType(pass.func_ctx.retType()), 
      pass.func_ctx.funcParams()
    );
  }

  void Pass_ParseFuncSignature_2(ParserPass pass)
  {
    if(pass.func_symb == null)
      return;

    ParseFuncParams(pass.func_ctx, pass.func_ast);

    ValidateModuleInitFunc(pass);

    Annotate(pass.func_ctx).eval_type = pass.func_symb.GetReturnType();
  }

  void ValidateModuleInitFunc(ParserPass pass)
  {
    if(pass.func_symb.attribs.HasFlag(FuncAttrib.Static) && 
        pass.func_symb.name == "init")
    {
      if(pass.func_symb.attribs.HasFlag(FuncAttrib.Coro))
        AddError(pass.func_symb.origin, "module 'init' function can't be a coroutine");

      if(pass.func_symb.GetTotalArgsNum() > 0)
        AddError(pass.func_symb.origin, "module 'init' function can't have any arguments");

      if(pass.func_symb.GetReturnType() != Types.Void)
        AddError(pass.func_symb.origin, "module 'init' function must be void");
    }
  }

  void ParseFuncParams(bhlParser.FuncDeclContext ctx, AST_FuncDecl func_ast)
  {
    var func_params = ctx.funcParams();
    if(func_params != null)
    {
      PushScope(func_ast.symbol);
      PushAST(func_ast.fparams());
      VisitFuncParams(func_params);
      PopAST();
      PopScope();
    }

    //TODO: why not storing the amount of default arguments in signature?
    func_ast.symbol.default_args_num = func_ast.GetDefaultArgsNum();
    if(func_ast.symbol.attribs.HasFlag(FuncAttrib.VariadicArgs))
      ++func_ast.symbol.default_args_num;
  }

  void Pass_ParseFuncBlock(ParserPass pass)
  {
    if(pass.func_ctx == null || pass.func_ast?.symbol == null)
      return;

    PushScope(pass.func_ast.symbol);
    ParseFuncBlock(pass.func_ctx, pass.func_ctx.funcBlock(), pass.func_ctx.retType(), pass.func_ast);
    PopScope();
  }

  void ParseFuncBlock(ParserRuleContext ctx, bhlParser.FuncBlockContext block_ctx, bhlParser.RetTypeContext ret_ctx, AST_FuncDecl func_ast)
  {
    PushAST(func_ast.block());
    TryVisit(block_ctx);
    PopAST();

    if(func_ast.symbol.GetReturnType() != Types.Void && !return_found.Contains(func_ast.symbol))
      AddError(ret_ctx, "matching 'return' statement not found");

    if(func_ast.symbol.attribs.HasFlag(FuncAttrib.Coro) && !has_yield_calls.Contains(func_ast.symbol))
      AddError(ctx, "coro functions without yield calls not allowed");
  }

  void Pass_OutlineNamespace(ParserPass pass)
  {
    if(pass.ns == null)
      return;

    //let's define a namespace only if it's not defined yet in some scope
    if(pass.ns.scope != null)
      return;

    curr_scope.Define(pass.ns);
  }

  void Pass_OutlineGlobalVar(ParserPass pass)
  {
    if(pass.gvar_decl_ctx == null)
      return;

    var vd = pass.gvar_decl_ctx.varDeclare();

    pass.gvar_symb = new GlobalVariableSymbol(Annotate(vd.NAME()), vd.NAME().GetText(), new Proxy<IType>());
    pass.gvar_symb.is_local = pass.gvar_decl_ctx.STATIC() != null;

    LSP_AddSemanticToken(pass.gvar_decl_ctx.STATIC(), SemanticToken.Keyword);
    LSP_SetSymbol(vd, pass.gvar_symb);

    if(!curr_scope.TryDefine(pass.gvar_symb, out SymbolError err))
    {
      AddError(vd.NAME(), err.Message);
      pass.Clear();
    }
  }

  void Pass_OutlineInterfaceDecl(ParserPass pass)
  {
    if(pass.iface_ctx?.NAME() == null)
      return;

    LSP_AddSemanticToken(pass.iface_ctx.INTERFACE(), SemanticToken.Keyword);
    LSP_AddSemanticToken(pass.iface_ctx.NAME(), SemanticToken.Class);

    var name = pass.iface_ctx.NAME().GetText();

    pass.iface_symb = new InterfaceSymbolScript(Annotate(pass.iface_ctx), name);
    LSP_SetSymbol(pass.iface_ctx, pass.iface_symb);

    if(!pass.scope.TryDefine(pass.iface_symb, out SymbolError err))
    {
      AddError(pass.iface_ctx.NAME(), err.Message);
      pass.Clear();
    }
  }

  void Pass_ParseInterfaceMethods(ParserPass pass)
  {
    if(pass.iface_ctx?.NAME() == null)
      return;

    for(int i=0;i<pass.iface_ctx.interfaceBlock()?.interfaceMembers()?.interfaceMember().Length;++i)
    {
      var ib = pass.iface_ctx.interfaceBlock().interfaceMembers().interfaceMember()[i];

      var fd = ib.interfaceFuncDecl();
      if(fd != null)
      {
        LSP_AddSemanticToken(fd.FUNC(), SemanticToken.Keyword);

        if(fd.NAME() == null)
        {
          AddError(fd, "incomplete parsing context");
          return;
        }

        int default_args_num;
        var sig = ParseFuncSignature(fd.CORO() != null, ParseType(fd.retType()), fd.funcParams(), out default_args_num);
        if(default_args_num != 0)
        {
          AddError(fd.funcParams().funcParamDeclare()[sig.arg_types.Count - default_args_num], "default argument value is not allowed in this context");
          return;
        }

        var func_symb = new FuncSymbolScript(
          null, 
          sig, 
          fd.NAME().GetText()
        );
        if(!pass.iface_symb.TryDefine(func_symb, out SymbolError err))
        {
          AddError(fd.NAME(), err.Message);
          return;
        }

        var func_params = fd.funcParams();
        if(func_params != null)
        {
          PushScope(func_symb);
          //NOTE: we push some dummy interim AST and later
          //      simply discard it since we don't care about
          //      func args related AST for interfaces
          PushAST(new AST_Interim());
          TryVisit(func_params);
          PopAST();
          PopScope();
        }
      }
    }
  }

  void Pass_AddInterfaceExtensions(ParserPass pass)
  {
    if(pass.iface_ctx?.NAME() == null)
      return;

    if(pass.iface_ctx.extensions() != null)
    {
      var inherits = new List<InterfaceSymbol>();
      for(int i=0;i<pass.iface_ctx.extensions().nsName().Length;++i)
      {
        var ext_name = pass.iface_ctx.extensions().nsName()[i]; 
        var ext = ns.ResolveSymbolByPath(ext_name.GetText());
        if(ext is InterfaceSymbol ifs)
        {
          if(ext == pass.iface_symb)
          {
            AddError(ext_name, "self inheritance is not allowed");
            return;
          }

          if(inherits.IndexOf(ifs) != -1)
          {
            AddError(ext_name, "interface is inherited already");
            return;
          }

          inherits.Add(ifs);
        }
        else
        {
          AddError(ext_name, "not a valid interface");
          return;
        }
      }
      if(inherits.Count > 0)
        pass.iface_symb.SetInherits(inherits);
    }
  }

  public override object VisitNsDecl(bhlParser.NsDeclContext ctx)
  {
    LSP_AddSemanticToken(ctx?.NAMESPACE(), SemanticToken.Keyword);

    string name = ctx?.dotName()?.NAME()?.GetText();
    if(name == null)
      return null;

    //NOTE: taking into account nested namespaces named like 'foo.bar'
    int nested_level = 0;
    do 
    {
      var ns = FindExistingOrPlannedNamespace(name);
      if(ns == null)
        ns = new Namespace(module, name);
      //NOTE: special case for namespace parser pass, we don't
      //      define it mmediately in the current scope but rather
      //      do it later, this way we preserve natural symbols order
      //      as they are declared in the source
      passes.Add(new ParserPass(curr_scope, ns, scopes.Count));

      //let's push it so that subsequent passes will have the proper curr_scope
      PushScope(ns);

      if(nested_level >= ctx.dotName().memberAccess().Length)
        break;

      name = ctx.dotName().memberAccess()[nested_level].NAME().GetText();

      ++nested_level;

    } while(true);

    foreach(var decl in ctx.decl())
      ProcessDecl(decl);

    for(int i = 0; i <= nested_level; ++i) 
      PopScope();

    return null;
  }

  Namespace FindExistingOrPlannedNamespace(string name)
  {
    var ns = curr_scope.Resolve(name) as Namespace;
    if(ns != null)
      return ns;

    foreach(var p in passes)
      if(p.ns != null && p.ns_level == scopes.Count && p.ns.name == name)
        return p.ns;

    return null;
  }

  void Pass_OutlineClassDecl(ParserPass pass)
  {
    if(pass.class_ctx?.NAME() == null)
      return;

    LSP_AddSemanticToken(pass.class_ctx.CLASS(), SemanticToken.Keyword);
    LSP_AddSemanticToken(pass.class_ctx.NAME(), SemanticToken.Class);

    var name = pass.class_ctx.NAME().GetText();

    pass.class_symb = new ClassSymbolScript(Annotate(pass.class_ctx), name);
    if(!pass.scope.TryDefine(pass.class_symb, out SymbolError err))
    {
      AddError(pass.class_ctx.NAME(), err.Message);
      return;
    }

    pass.class_ast = new AST_ClassDecl(pass.class_symb);

    //class members
    for(int i=0;i<pass.class_ctx.classBlock()?.classMembers()?.classMember().Length;++i)
    {
      var cm = pass.class_ctx.classBlock().classMembers().classMember()[i];
      var fldd = cm.fldDeclare();
      if(fldd != null)
      {
        if(fldd.varDeclare()?.NAME() == null)
        {
          AddError(fldd, "incomplete parsing context");
          return;
        }

        var vd = fldd.varDeclare();

        if(vd.NAME().GetText() == "this")
        {
          AddError(vd.NAME(), "the keyword 'this' is reserved");
          return;
        }

        LSP_AddSemanticToken(vd.NAME(), SemanticToken.Variable, SemanticModifier.Definition);

        var fld_symb = new FieldSymbolScript(Annotate(vd), vd.NAME().GetText(), new Proxy<IType>());

        for(int f=0;f<fldd.fldAttribs().Length;++f)
        {
          var attr = fldd.fldAttribs()[f];
          var attr_type = FieldAttrib.None;

          if(attr.STATIC() != null)
            attr_type = FieldAttrib.Static;

          if(fld_symb.attribs.HasFlag(attr_type))
            AddError(attr, "this attribute is set already");

          fld_symb.attribs |= attr_type;
        }

        if(!pass.class_symb.TryDefine(fld_symb, out SymbolError symb_err))
        {
          AddError(vd.NAME(), symb_err.Message);
          return;
        }
      }

      var fd = cm.funcDecl();
      if(fd != null)
      {
        if(fd.NAME().GetText() == "this")
        {
          AddError(fd.NAME(), "the keyword 'this' is reserved");
          return;
        }

        var func_symb = new FuncSymbolScript(
            Annotate(fd), 
            new FuncSignature(),
            fd.NAME().GetText()
          );

        for(int f=0;f<fd.funcAttribs().Length;++f)
        {
          var attr = fd.funcAttribs()[f];
          var attr_type = FuncAttrib.None;

          if(attr.CORO() != null)
          {
            attr_type = FuncAttrib.Coro;
            LSP_AddSemanticToken(attr.CORO(), SemanticToken.Keyword);
          }
          else if(attr.VIRTUAL() != null)
          {
            attr_type = FuncAttrib.Virtual;
            LSP_AddSemanticToken(attr.VIRTUAL(), SemanticToken.Keyword);
          }
          else if(attr.OVERRIDE() != null)
          {
            attr_type = FuncAttrib.Override;
            LSP_AddSemanticToken(attr.OVERRIDE(), SemanticToken.Keyword);
          }
          else if(attr.STATIC() != null)
          {
            attr_type = FuncAttrib.Static;
            LSP_AddSemanticToken(attr.STATIC(), SemanticToken.Keyword);
          }

          if(func_symb.attribs.HasFlag(attr_type))
            AddError(attr, "this attribute is set already");

          func_symb.attribs |= attr_type;
        }

        if(!func_symb.attribs.HasFlag(FuncAttrib.Static))
          func_symb.ReserveThisArgument(pass.class_symb);

        if(!pass.class_symb.TryDefine(func_symb, out SymbolError symb_err))
          AddError(fd.NAME(), symb_err.Message);

        var func_ast = new AST_FuncDecl(func_symb, fd.Stop.Line);
        pass.class_ast.AddChild(func_ast);
      }

      if(cm.classDecl() != null)
        AddPass(cm.classDecl(), pass.class_symb, pass.class_ast);
      else if(cm.enumDecl() != null)
        AddPass(cm.enumDecl(), pass.class_symb, pass.class_ast);
      else if(cm.interfaceDecl() != null)
        AddPass(cm.interfaceDecl(), pass.class_symb, pass.class_ast);
    }

    pass.ast.AddChild(pass.class_ast);
  }

  void Pass_ParseClassMembersTypes(ParserPass pass)
  {
    if(pass.class_symb == null)
      return;

    PushScope(pass.class_symb);
    //NOTE: we want to prevent resolving of attributes and methods at this point 
    //      since they might collide with types. For example:
    //      class Foo {
    //        a.A a <-- here attribute 'a' will prevent proper resolving of 'a.A' type  
    //      }
    pass.class_symb._resolve_only_decl_members = true;

    //class members
    for(int i=0;i<pass.class_ctx.classBlock()?.classMembers()?.classMember().Length;++i)
    {
      var cm = pass.class_ctx.classBlock().classMembers().classMember()[i];
      var fldd = cm.fldDeclare();
      if(fldd != null)
      {
        if(fldd.varDeclare()?.NAME() == null)
        {
          AddError(fldd, "incomplete parsing context");
          continue;
        }

        var vd = fldd.varDeclare();
        var fld_symb = (FieldSymbolScript)pass.class_symb.members.Find(vd.NAME().GetText());
        if(fld_symb == null)
          break;
        fld_symb.type = ParseType(vd.type());
      }

      var fd = cm.funcDecl();
      if(fd != null)
      {
        var func_symb = pass.class_symb.members.Find(fd.NAME().GetText()) as FuncSymbolScript;
        if(func_symb == null)
          break;

        func_symb.signature = ParseFuncSignature(
          fd.funcAttribs().Length > 0 && fd.funcAttribs()[0].CORO() != null, 
          ParseType(fd.retType()), 
          fd.funcParams()
        );

        var func_ast = pass.class_ast.FindFuncDecl(func_symb);
        ParseFuncParams(fd, func_ast);

        Annotate(fd).eval_type = func_symb.GetReturnType(); 
      }
    }

    pass.class_symb._resolve_only_decl_members = false;
    PopScope();
  }

  void Pass_AddClassExtensions(ParserPass pass)
  {
    if(pass.class_ctx?.NAME() == null)
      return;

    if(pass.class_ctx.extensions() != null)
    {
      var implements = new List<InterfaceSymbol>();
      ClassSymbol super_class = null;

      for(int i=0;i<pass.class_ctx.extensions().nsName().Length;++i)
      {
        var ext_name = pass.class_ctx.extensions().nsName()[i]; 

        LSP_AddSemanticToken(ext_name.dotName().NAME(), SemanticToken.Class);

        var ext = curr_scope.ResolveSymbolByPath(ext_name.GetText());
        if(ext is ClassSymbol cs)
        {
          if(ext == pass.class_symb)
          {
            AddError(ext_name, "self inheritance is not allowed");
            return;
          }

          if(super_class != null)
          {
            AddError(ext_name, "only one parent class is allowed");
            return;
          }

          if(cs is ClassSymbolNative)
          {
            AddError(ext_name, "extending native classes is not supported");
            return;
          }

          super_class = cs;
        }
        else if(ext is InterfaceSymbol ifs)
        {
          if(implements.IndexOf(ifs) != -1)
          {
            AddError(ext_name, "interface is implemented already");
            return;
          }

          if(ifs is InterfaceSymbolNative)
          {
            AddError(ext_name, "implementing native interfaces is not supported");
            return;
          }

          implements.Add(ifs);
        }
        else
        {
          AddError(ext_name, "not a class or an interface");
          return;
        }
      }

      pass.class_symb.SetSuperClassAndInterfaces(super_class, implements);
    }
  }

  void Pass_SetupClass(ParserPass pass)
  {
    if(pass.class_symb == null)
      return;

    pass.class_symb.Setup();
    
    //NOTE: let's declare static class variables as module global variables 
    //      so that they are properly initialized upon module loading
    for(int m=0;m<pass.class_symb.members.Count;++m)
    {
      if(pass.class_symb.members[m] is FieldSymbol fld && fld.attribs.HasFlag(FieldAttrib.Static)) 
        pass.class_ast.AddChild(new AST_VarDecl(fld, module.gvars.IndexOf(fld)));
    }
  }

  void Pass_ParseClassMethodsBlocks(ParserPass pass)
  {
    if(pass.class_symb == null)
      return;

    //class methods bodies
    for(int i=0;i<pass.class_ctx.classBlock()?.classMembers()?.classMember().Length;++i)
    {
      var cm = pass.class_ctx.classBlock().classMembers().classMember()[i];
      var fd = cm.funcDecl();

      if(fd != null)
      {
        LSP_AddSemanticToken(fd.FUNC(), SemanticToken.Keyword);

        var func_symb = pass.class_symb.Resolve(fd.NAME().GetText()) as FuncSymbol;
        if(func_symb == null)
          break;

        var func_ast = pass.class_ast.FindFuncDecl((FuncSymbolScript)func_symb);
        if(func_ast == null)
          throw new Exception("Method '" + func_symb.name + "' decl not found for class '" + pass.class_symb.name + "'");

        PushScope(func_symb);
        ParseFuncBlock(fd, fd.funcBlock(), fd.retType(), func_ast);
        PopScope();
      }
    }
  }

  void Pass_OutlineEnumDecl(ParserPass pass)
  {
    if(pass.enum_ctx == null)
      return;

    VisitEnumDecl(pass.enum_ctx);
  }

  public override object VisitEnumDecl(bhlParser.EnumDeclContext ctx)
  {
    if(ctx.NAME() == null)
      return null;

    LSP_AddSemanticToken(ctx.ENUM(), SemanticToken.Keyword);

    var enum_name = ctx.NAME().GetText();

    //NOTE: currently all enum values are replaced with literals,
    //      so that it doesn't really make sense to create AST for them.
    //      But we do it just for consistency. Later once we have runtime 
    //      type info this will be justified.
    var symb = new EnumSymbolScript(Annotate(ctx), enum_name);
    if(!curr_scope.TryDefine(symb, out SymbolError err))
    {
      AddError(ctx.NAME(), err.Message);
      return null;
    }

    for(int i=0;i<ctx.enumBlock()?.enumMember()?.Length;++i)
    {
      var em = ctx.enumBlock().enumMember()[i];
      if(em.NAME() == null || em.INT() == null)
      {
        AddError(em, "incomplete parsing context");
        return null;
      }

      var em_name = em.NAME().GetText();
      int em_val = 0;
      if(!int.TryParse(
          em.INT().GetText(), 
          System.Globalization.NumberStyles.Integer, 
          System.Globalization.CultureInfo.InvariantCulture, 
          out em_val)
        )
      {
        AddError(em, "invalid value");
        return null;
      }

      if(em.MINUS() != null)
        em_val = -em_val;

      int res = symb.TryAddItem(Annotate(em), em_name, em_val);
      if(res == 1)
      {
        AddError(em.NAME(), "duplicate key '" + em_name + "'");
        return null;
      }
      else if(res == 2)
      {
        AddError(em.INT(), "duplicate value '" + em_val + "'");
        return null;
      }
    }

    return null;
  }

  void Pass_ParseGlobalVar(ParserPass pass)
  {
    if(pass.gvar_symb == null)
      return;

    var vd = pass.gvar_decl_ctx.varDeclare();

    //NOTE: we want to temprarily 'disable' the symbol so that it doesn't
    //      interfere with type lookups and invalid self assignments
    var subst_symbol = DisableVar(((Namespace)curr_scope).members, pass.gvar_symb);

    pass.gvar_symb.type = ParseType(vd.type());
    pass.gvar_symb.origin.parsed.eval_type = pass.gvar_symb.type.Get();

    if(vd.type().nsName() != null)
      LSP_SetSymbol(vd.type().nsName().dotName(), pass.gvar_symb.type.Get() as Symbol);

    PushAST((AST_Tree)pass.ast);

    var assign_exp = pass.gvar_assign_ctx;

    AST_Interim exp_ast = null;
    if(assign_exp != null)
    {
      var tp = ParseType(vd.type());

      exp_ast = new AST_Interim();
      PushAST(exp_ast);
      PushJsonType(tp.Get());
      bool ok = TryVisit(assign_exp);
      PopJsonType();
      PopAST();

      if(!ok)
        return;
    }

    AST_Tree ast = assign_exp != null ? 
      //NOTE: we're in the global 'init' code, we use VARW instead of GVARW
      (AST_Tree)new AST_Call(EnumCall.VARW, vd.NAME().Symbol.Line, pass.gvar_symb, 0, vd.NAME()) : 
      (AST_Tree)new AST_VarDecl(pass.gvar_symb);

    if(exp_ast != null)
      PeekAST().AddChild(exp_ast);
    PeekAST().AddChild(ast);

    if(assign_exp != null)
      types.CheckAssign(Annotate(vd.NAME()), Annotate(assign_exp), errors);

    PopAST();

    EnableVar(((Namespace)curr_scope).members, pass.gvar_symb, subst_symbol);
  }

  public override object VisitFuncParams(bhlParser.FuncParamsContext ctx)
  {
    var fparams = ctx.funcParamDeclare();
    bool found_default_arg = false;

    for(int i=0;i<fparams.Length;++i)
    {
      var fp = fparams[i]; 

      if(fp.NAME() == null)
      {
        AddError(fp, "invalid argument");
        continue;
      }

      if(fp.assignExp() != null)
      {
        if(curr_scope is LambdaSymbol)
          AddError(fp.NAME(), "default argument values not allowed for lambdas");

        found_default_arg = true;
      }
      else if(found_default_arg && fp.VARIADIC() == null)
      {
        AddError(fp.NAME(), "missing default argument expression");
      }

      bool pop_json_type = false;
      if(found_default_arg)
      {
        var tp = ParseType(fp.type());
        PushJsonType(tp.Get());
        pop_json_type = true;
      }

      TryVisit(fp);

      if(pop_json_type)
        PopJsonType();
    }

    return null;
  }

  bool ProcDeclOrAssign(bhlParser.VarOrDeclareContext vdecl, bhlParser.AssignExpContext assign_exp, int start_line)
  {
    return ProcDeclOrAssign(new VarsOrDeclsProxy(new bhlParser.VarOrDeclareContext[] {vdecl}), assign_exp, start_line);
  }

  class VarsOrDeclsProxy
  {
    bhlParser.VarDeclareContext[] vdecls;
    bhlParser.VarOrDeclareContext[] vodecls;
    bhlParser.VarDeclareOrChainExpContext[] vdeclsorexps;
    bhlParser.ChainExpContext[] exps;

    public int Count { 
      get {
        if(vdecls != null)
          return vdecls.Length;
        else if(vodecls != null)
          return vodecls.Length;
        else if(vdeclsorexps != null)
          return vdeclsorexps.Length;
        else if(exps != null)
          return exps.Length;
        return -1;
      }
    }

    public VarsOrDeclsProxy(bhlParser.VarDeclareContext[] vdecls)
    {
      this.vdecls = vdecls;
    }

    public VarsOrDeclsProxy(bhlParser.VarOrDeclareContext[] vodecls)
    {
      this.vodecls = vodecls;
    }

    public VarsOrDeclsProxy(bhlParser.VarDeclareOrChainExpContext[] vdeclsorexps)
    {
      this.vdeclsorexps = vdeclsorexps;
    }

    public VarsOrDeclsProxy(bhlParser.ChainExpContext[] exps)
    {
      this.exps = exps;
    }

    public IParseTree At(int i)
    {
      if(vdecls != null)
        return (IParseTree)vdecls[i];
      else if(vodecls != null)
        return (IParseTree)vodecls[i];
      else if(vdeclsorexps != null)
        return (IParseTree)vdeclsorexps[i];
      else if(exps != null)
        return (IParseTree)exps[i];

      return null;
    }

    public bhlParser.TypeContext TypeAt(int i)
    {
      if(vdecls != null)
        return vdecls[i].type();
      else if(vodecls != null)
        return vodecls[i].varDeclare()?.type();
      else if(vdeclsorexps != null)
        return vdeclsorexps[i].varDeclare()?.type();

      return null;
    }

    public ITerminalNode LocalNameAt(int i)
    {
      if(vdecls != null)
        return vdecls[i].NAME();
      else if(vodecls != null)
      {
        if(vodecls[i].varDeclare() != null)
          return vodecls[i].varDeclare().NAME();
        else
          return vodecls[i].NAME();
      }
      else if(vdeclsorexps != null)
      {
        if(vdeclsorexps[i].varDeclare() != null)
          return vdeclsorexps[i].varDeclare().NAME();
        else if(vdeclsorexps[i].chainExp().name() != null &&
                vdeclsorexps[i].chainExp().chainExpItem().Length == 0)
          return vdeclsorexps[i].chainExp().name().NAME();
      }
      else if(exps != null)
      {
        if(exps[i].name() != null &&
           exps[i].chainExpItem().Length == 0)
          return exps[i].name().NAME();
      }
      
      return null;
    }

    public bhlParser.ChainExpContext VarAccessAt(int i)
    {
      if(vdeclsorexps != null && vdeclsorexps[i].chainExp() != null)
      {
        var chain = new ExpChain(null, vdeclsorexps[i].chainExp());
        if(chain.IsVarAccess)
          return vdeclsorexps[i].chainExp();
      }
      else if(exps != null)
      {
        var chain = new ExpChain(null, exps[i]);
        if(chain.IsVarAccess)
          return exps[i];
      }

      return null;
    }
  }

  struct TypeAsArr
  {
    IType type;

    public int Count {
      get {
        if(type is TupleType tt)
          return tt.Count;
        return 1;
      }
    }

    public TypeAsArr(IType type)
    {
      this.type = type;
    }

    public IType At(int i) 
    {
      if(type is TupleType tt)
        return tt[i].Get();
      else if(i == 0)
        return type;
      else
        throw new Exception("Out of bounds: " + i);
    }
  }

  bool ProcDeclOrAssign(VarsOrDeclsProxy vproxy, bhlParser.AssignExpContext assign_exp, int start_line)
  {
    var var_ast = PeekAST();
    int var_assign_insert_idx = var_ast.children.Count;

    for(int i=0;i<vproxy.Count;++i)
    {
      VariableSymbol var_symb = null;
      AnnotatedParseTree var_ann = null;
      bool is_decl = false;
      bool is_auto_var = false;

      //check if we declare a var or use an existing one
      if(vproxy.TypeAt(i) != null)
      {
        var vd_type = vproxy.TypeAt(i);
        is_auto_var = vd_type.GetText() == "var";

        if(is_auto_var && assign_exp == null)
        {
          AddError(vd_type, "unexpected expression");
          return false;
        }
        else if(is_auto_var && assign_exp?.GetText() == "=null")
        {
          AddError(vd_type, "unexpected expression");
          return false;
        }

        var vd_name = vproxy.LocalNameAt(i);
        var var_decl_ast = ProcDeclVar(
          curr_scope, 
          vd_name, 
          //NOTE: in case of 'var' let's temporarily declare var as 'any',
          //      below we'll setup the proper type
          is_auto_var ? Types.Any : ParseType(vd_type), 
          vd_type,
          is_ref: false, 
          func_arg: false, 
          write: assign_exp != null,
          symb: out var_symb
        );
        if(var_decl_ast == null)
          return false;
        var_ast.AddChild(var_decl_ast);

        is_decl = true;

        var_ann = var_symb.origin.parsed;
        var_ann.eval_type = var_symb.type.Get();
        LSP_SetSymbol(vd_name.Parent, var_symb);
      }
      else if(vproxy.LocalNameAt(i) != null)
      {
        var vd_name = vproxy.LocalNameAt(i);
        var_symb = curr_scope.ResolveWithFallback(vd_name.GetText()) as VariableSymbol;
        if(var_symb == null)
        {
          AddError(vd_name, "symbol '" + vd_name.GetText() + "' not resolved");
          return false;
        }

        var_ann = Annotate(vd_name);
        var_ann.eval_type = var_symb.type.Get();
        LSP_SetSymbol(vd_name.Parent, var_symb);

        bool is_global = var_symb.scope is Namespace;
        var ast = new AST_Call(is_global ? EnumCall.GVARW : EnumCall.VARW, start_line, var_symb);
        var_ast.AddChild(ast);
      }
      else if(vproxy.VarAccessAt(i) != null)
      {
        var var_exp = vproxy.VarAccessAt(i);
        if(assign_exp == null)
        {
          AddError(var_exp, "assign expression expected");
          return false;
        }

        var chain = new ExpChain(var_exp, var_exp);

        if(chain.Incomplete)
        {
          AddError(var_exp, "incomplete statement");
          return false;
        }

        IType curr_type = null;
        if(!ProcExpChain(chain, ref curr_type, write: true))
          return false;

        var_ann = Annotate(var_exp);
        var_ann.eval_type = curr_type;
      }
      else
      {
        if(assign_exp != null)
          AddError(assign_exp, "invalid assignment");
        else
          AddError(vproxy.At(i), "invalid expression");
        return false;
      }

      if(assign_exp != null)
      {
        if(!ProcAssignToVar(
          var_ast, 
          var_assign_insert_idx,
          var_ann,
          is_decl,
          is_auto_var,
          var_symb,
          i,
          vproxy.Count,
          assign_exp
        ))
          return false;
      }
    }
    return true;
  }

  static VariableSymbol DisableVar(SymbolsStorage members, VariableSymbol disabled_symbol)
  {
    var subst_symbol = new VariableSymbol(disabled_symbol.origin.parsed, "#$"+disabled_symbol.name, disabled_symbol.type);
    members.Replace(disabled_symbol, subst_symbol);
    return subst_symbol;
  }

  static void EnableVar(SymbolsStorage members, VariableSymbol disabled_symbol, VariableSymbol subst_symbol)
  {
    members.Replace(subst_symbol, disabled_symbol);
  }

  public override object VisitStmDeclOptAssign(bhlParser.StmDeclOptAssignContext ctx)
  {
    var vdecls = new VarsOrDeclsProxy(ctx.varDeclareList().varDeclare());
    var assign_exp = ctx.assignExp();
    ProcDeclOrAssign(vdecls, assign_exp, ctx.Start.Line);

    return null;
  }

  public override object VisitStmDeclOrExpAssign(bhlParser.StmDeclOrExpAssignContext ctx)
  {
    var vdecls = new VarsOrDeclsProxy(ctx.varDeclaresOrChainExps().varDeclareOrChainExp());
    var assign_exp = ctx.assignExp();
    ProcDeclOrAssign(vdecls, assign_exp, ctx.Start.Line);

    return null;
  }

  public override object VisitFuncParamDeclare(bhlParser.FuncParamDeclareContext ctx)
  {
    var name = ctx.NAME();
    if(name == null)
    {
      AddError(ctx, "missing name");
      return null;
    }

    LSP_AddSemanticToken(ctx.REF(), SemanticToken.Keyword);

    var assign_exp = ctx.assignExp();
    bool is_ref = ctx.REF() != null;
    bool is_null_ref = false;

    if(is_ref && assign_exp != null)
    {
      //NOTE: super special case for 'null refs'
      if(assign_exp.exp().GetText() == "null")
        is_null_ref = true;
      else
      {
        AddError(name, "'ref' is not allowed to have a default value");
        return null;
      }
    }

    AST_Interim exp_ast = null;
    if(assign_exp != null)
    {
      exp_ast = new AST_Interim();
      PushAST(exp_ast);
      bool ok = TryVisit(assign_exp);
      PopAST();

      if(!ok)
        return null;
    }

    VariableSymbol vd_symb;
    var decl_ast = ProcDeclVar(
      curr_scope, 
      name, 
      ctx.type(), 
      is_ref, 
      func_arg: true, 
      write: false,
      symb: out vd_symb
    );
    if(decl_ast == null)
      return null;

    if(exp_ast != null)
      decl_ast.AddChild(exp_ast);

    PeekAST().AddChild(decl_ast);

    if(assign_exp != null && !is_null_ref)
      types.CheckAssign(Annotate(name), Annotate(assign_exp), errors);
    return null;
  }

  AST_Tree ProcDeclVar(
    IScope curr_scope, 
    ITerminalNode name, 
    bhlParser.TypeContext tp_ctx, 
    bool is_ref, 
    bool func_arg, 
    bool write,
    out VariableSymbol symb
  )
  {
    var tp = ParseType(tp_ctx);
    return ProcDeclVar(
      curr_scope, 
      name, 
      tp, 
      tp_ctx, 
      is_ref, 
      func_arg, 
      write,
      out symb
    );
  }

  AST_Tree ProcDeclVar(
    IScope curr_scope, 
    ITerminalNode name, 
    Proxy<IType> tp, 
    bhlParser.TypeContext tp_ctx, //can be null, used for LSP discovery 
    bool is_ref, 
    bool func_arg, 
    bool write,
    out VariableSymbol symb 
  )
  {
    LSP_AddSemanticToken(name, SemanticToken.Variable);
    if(tp_ctx != null && tp_ctx.GetText() == "var")
      LSP_AddSemanticToken(tp_ctx, SemanticToken.Keyword);

    symb = null;

    if(name.GetText() == "base" && PeekFuncDecl()?.scope is ClassSymbol)
    {
      AddError(name, "keyword 'base' is reserved");
      return null;
    }

    var var_ann = Annotate(name); 
    var_ann.eval_type = tp.Get();

    if(tp_ctx?.nsName() != null)
      LSP_SetSymbol(tp_ctx.nsName().dotName(), var_ann.eval_type as Symbol);

    if(is_ref && !func_arg)
    {
      AddError(name, "'ref' is only allowed in function declaration");
      return null;
    }

    symb = func_arg ? 
      (VariableSymbol) new FuncArgSymbol(var_ann, name.GetText(), tp, is_ref) :
      new VariableSymbol(var_ann, name.GetText(), tp);

    if(!curr_scope.TryDefine(symb, out SymbolError err))
    {
      AddError(name, err.Message);
      return null;
    }

    LSP_SetSymbol(name.Parent, symb);

    if(write)
      return new AST_Call(EnumCall.VARW, name.Symbol.Line, symb, 0, name);
    else
      return new AST_VarDecl(symb, is_ref);
  }

  bool ProcAssignToVar(
    AST_Tree ast_dest,
    int ast_insert_idx,
    AnnotatedParseTree var_ann, 
    bool is_decl,
    bool is_auto_var,
    VariableSymbol var_symb,
    int var_idx,
    int vars_num,
    bhlParser.AssignExpContext assign_exp
  )
  {
    //NOTE: look forward at expression and push json type 
    //      if it's a json-init-expression
    bool pop_json_type = false;
    if((assign_exp.exp() is bhlParser.ExpJsonObjContext || 
      assign_exp.exp() is bhlParser.ExpJsonArrContext))
    {
      if(vars_num != 1)
      {
        AddError(assign_exp, "multi assign not supported for JSON expression");
        return false;
      }

      pop_json_type = true;

      PushJsonType(var_ann.eval_type);
    }

    //NOTE: temporarily replacing just declared variable with the dummy one when visiting 
    //      assignment expression in order to avoid error like: float k = k
    VariableSymbol subst_symb = null;
    if(is_decl)
    {
      var symbols = ((LocalScope)var_symb.scope).members;
      subst_symb = DisableVar(symbols, var_symb);
    }

    //NOTE: let's evaluate expression only once in case there are multiple variables
    if(var_idx == 0)
    {
      var assign_ast = new AST_Interim();
      PushAST(assign_ast);
      bool ok = TryVisit(assign_exp);
      PopAST();

      if(!ok)
        return false;

      for(int s=assign_ast.children.Count;s-- > 0;)
        ast_dest.children.Insert(ast_insert_idx, assign_ast.children[s]);
    }

    var assign_type = new TypeAsArr(Annotate(assign_exp).eval_type); 

    //NOTE: declaring disabled symbol again
    if(subst_symb != null)
    {
      var symbols = ((LocalScope)var_symb.scope).members;
      EnableVar(symbols, var_symb, subst_symb);
    }

    if(pop_json_type)
      PopJsonType();

    if(vars_num > 1)
    {
      if(assign_type.Count == 1)
      {
        AddError(assign_exp, "multi return expected");
        return false;
      }

      if(assign_type.Count != vars_num)
      {
        AddError(assign_exp, "multi return size doesn't match destination");
        return false;
      }
    }
    else if(assign_type.Count > 1)
    {
      AddError(assign_exp, "multi return size doesn't match destination");
      return false;
    }

    if(is_auto_var)
    {
      var auto_type = assign_type.At(var_idx);
      if(auto_type == null)
      {
        AddError(assign_exp, "can't determine type");
        return false;
      }
      else if(auto_type == Types.Void)
      {
        AddError(assign_exp, "void expression type");
        return false;
      }
      var_symb.type = new Proxy<IType>(auto_type); 
    }

    return types.CheckAssign(var_ann, assign_type.At(var_idx), errors);
  }

  public override object VisitBlock(bhlParser.BlockContext ctx)
  {
    ProcBlock(BlockType.SEQ, ctx.statement());
    return null;
  }

  public override object VisitFuncBlock(bhlParser.FuncBlockContext ctx)
  {
    ProcBlock(BlockType.FUNC, ctx.block()?.statement());
    return null;
  }

  public override object VisitStmParal(bhlParser.StmParalContext ctx)
  {
    LSP_AddSemanticToken(ctx.PARAL(), SemanticToken.Keyword);

    ProcBlock(BlockType.PARAL, ctx.block()?.statement());
    return null;
  }

  public override object VisitStmParalAll(bhlParser.StmParalAllContext ctx)
  {
    LSP_AddSemanticToken(ctx.PARAL_ALL(), SemanticToken.Keyword);

    ProcBlock(BlockType.PARAL_ALL, ctx.block()?.statement());
    return null;
  }

  public override object VisitStmDefer(bhlParser.StmDeferContext ctx)
  {
    LSP_AddSemanticToken(ctx.DEFER(), SemanticToken.Keyword);

    if(CountBlocks(BlockType.DEFER) > 0)
    {
      AddError(ctx, "nested defers are not allowed");
      return null;
    }
    ProcBlock(BlockType.DEFER, ctx.block()?.statement());
    return null;
  }

  public override object VisitStmIf(bhlParser.StmIfContext ctx)
  {
    var ast = new AST_Block(BlockType.IF);

    LSP_AddSemanticToken(ctx.IF(), SemanticToken.Keyword);

    var main_cond = new AST_Block(BlockType.SEQ);
    PushAST(main_cond);
    bool ok = TryVisit(ctx.exp());
    PopAST();

    if(!ok || !types.CheckAssign(Types.Bool, Annotate(ctx.exp()), errors))
      return null;

    var func_symb = PeekFuncDecl();
    bool seen_return = return_found.Contains(func_symb);
    return_found.Remove(func_symb);

    ast.AddChild(main_cond);
    PushAST(ast);
    ok = ProcBlock(BlockType.SEQ, ctx.block()?.statement()) != null;
    PopAST();
    if(!ok)
      return null;

    //NOTE: if in the block before there were no 'return' statements and in the current block
    //      *there's one* we need to reset the 'return found' flag since otherewise
    //      there's a code path without 'return', e.g:
    //
    //      func int foo() {
    //        if(..) {
    //          return 1
    //        } else {
    //          ...
    //        }
    //        return 3
    //      }
    //
    //      func int foo() {
    //        if(..) {
    //          ...
    //        } else {
    //          return 2
    //        }
    //        return 3
    //      }
    //
    //      func int foo() {
    //        if(..) {
    //          return 1 
    //        } else {
    //          return 2
    //        }
    //      }
    //
    if(!seen_return && return_found.Contains(func_symb) && (ctx.elseIf() == null || ctx.@else() == null))
      return_found.Remove(func_symb);

    var else_if = ctx.elseIf();
    for(int i=0;i<else_if.Length;++i)
    {
      var item = else_if[i];

      LSP_AddSemanticToken(item.ELSE(), SemanticToken.Keyword);
      LSP_AddSemanticToken(item.IF(), SemanticToken.Keyword);

      var item_cond = new AST_Block(BlockType.SEQ);
      PushAST(item_cond);
      bool item_ok = TryVisit(item.exp());
      PopAST();

      if(!item_ok || !types.CheckAssign(Types.Bool, Annotate(item.exp()), errors))
        return null;

      seen_return = return_found.Contains(func_symb);
      return_found.Remove(func_symb);

      ast.AddChild(item_cond);
      PushAST(ast);
      item_ok = ProcBlock(BlockType.SEQ, item.block()?.statement()) != null;
      PopAST();
      if(!item_ok)
        return null;

      if(!seen_return && return_found.Contains(func_symb))
        return_found.Remove(func_symb);
    }

    var @else = ctx.@else();
    if(@else != null)
    {
      LSP_AddSemanticToken(@else.ELSE(), SemanticToken.Keyword);

      seen_return = return_found.Contains(func_symb);
      return_found.Remove(func_symb);

      PushAST(ast);
      bool block_ok = ProcBlock(BlockType.SEQ, @else.block()?.statement()) != null;
      PopAST();
      if(!block_ok)
        return null;

      if(!seen_return && return_found.Contains(func_symb))
        return_found.Remove(func_symb);
    }

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpTernaryIf(bhlParser.ExpTernaryIfContext ctx)
  {
    var ast = new AST_Block(BlockType.IF); //short-circuit eval

    var exp_0 = ctx.exp();
    var exp_1 = ctx.ternaryIfExp().exp(0);
    var exp_2 = ctx.ternaryIfExp().exp(1);

    var condition = new AST_Interim();
    PushAST(condition);
    bool ok1 = TryVisit(exp_0);
    PopAST();

    if(!ok1 || !types.CheckAssign(Types.Bool, Annotate(exp_0), errors))
      return null;

    ast.AddChild(condition);

    var consequent = new AST_Interim();
    PushAST(consequent);
    TryVisit(exp_1);
    PopAST();

    ast.AddChild(consequent);

    var alternative = new AST_Interim();
    PushAST(alternative);
    bool ok2 = TryVisit(exp_2);
    PopAST();

    ast.AddChild(alternative);

    var ann_exp_1 = Annotate(exp_1);
    Annotate(ctx).eval_type = ann_exp_1.eval_type;

    if(!ok2 || !types.CheckAssign(ann_exp_1, Annotate(exp_2), errors))
      return null;
    PeekAST().AddChild(ast);
    return null;
  }

  public override object VisitStmWhile(bhlParser.StmWhileContext ctx)
  {
    LSP_AddSemanticToken(ctx.WHILE(), SemanticToken.Keyword);

    var ast = new AST_Block(BlockType.WHILE);

    PushBlock(ast);

    var cond = new AST_Block(BlockType.SEQ);
    PushAST(cond);
    bool ok = TryVisit(ctx.exp());
    PopAST();

    if(!ok || !types.CheckAssign(Types.Bool, Annotate(ctx.exp()), errors))
      return null;

    ast.AddChild(cond);

    PushAST(ast);
    ok = ProcBlock(BlockType.SEQ, ctx.block()?.statement()) != null;
    PopAST();
    if(!ok)
      return null;
    ast.children[ast.children.Count-1].AddChild(new AST_Continue(jump_marker: true));

    PeekAST().AddChild(ast);

    PopBlock(ast);

    return_found.Remove(PeekFuncDecl());

    return null;
  }

  public override object VisitStmDoWhile(bhlParser.StmDoWhileContext ctx)
  {
    LSP_AddSemanticToken(ctx.DO(), SemanticToken.Keyword);
    LSP_AddSemanticToken(ctx.WHILE(), SemanticToken.Keyword);

    var ast = new AST_Block(BlockType.DOWHILE);

    PushBlock(ast);

    PushAST(ast);
    bool ok = ProcBlock(BlockType.SEQ, ctx.block()?.statement()) != null;
    PopAST();
    if(!ok)
      return null;
    ast.children[ast.children.Count-1].AddChild(new AST_Continue(jump_marker: true));

    var cond = new AST_Block(BlockType.SEQ);
    PushAST(cond);
    ok = TryVisit(ctx.exp());
    PopAST();

    if(!ok || !types.CheckAssign(Types.Bool, Annotate(ctx.exp()), errors))
      return null;

    ast.AddChild(cond);

    PeekAST().AddChild(ast);

    PopBlock(ast);

    return_found.Remove(PeekFuncDecl());

    return null;
  }

  public override object VisitStmFor(bhlParser.StmForContext ctx)
  {
    //NOTE: we're going to generate the following code
    //
    //<pre code>
    //while(<condition>)
    //{
    // ...
    // <post iter code>
    //}

    LSP_AddSemanticToken(ctx.FOR(), SemanticToken.Keyword);

    var local_scope = new LocalScope(is_paral: false, fallback: curr_scope);
    PushScope(local_scope);
    local_scope.Enter();
    
    var for_pre = ctx.forExp().forPreIter();
    if(for_pre != null)
      ProcForPreStatements(for_pre, ctx.Start.Line);

    var for_cond = ctx.forExp().exp();
    var for_post_iter = ctx.forExp().forPostIter();

    var ast = new AST_Block(BlockType.WHILE);

    PushBlock(ast);

    var cond = new AST_Block(BlockType.SEQ);
    PushAST(cond);
    bool ok = TryVisit(for_cond);
    PopAST();

    if(!ok || !types.CheckAssign(Types.Bool, Annotate(for_cond), errors))
      return null;

    ast.AddChild(cond);

    PushAST(ast);
    var block = ProcBlock(BlockType.SEQ, ctx.block()?.statement());
    if(block == null)
      return null;
    //appending post iteration code
    if(for_post_iter != null)
    {
      PushAST(block);
      PeekAST().AddChild(new AST_Continue(jump_marker: true));
      ProcForPostStatements(for_post_iter, ctx.Start.Line);
      PopAST();
    }
    PopAST();

    PeekAST().AddChild(ast);

    PopBlock(ast);

    local_scope.Exit();
    PopScope();

    return_found.Remove(PeekFuncDecl());

    return null;
  }

  void ProcForPreStatements(bhlParser.ForPreIterContext pre, int start_line)
  {
    foreach(var vdecl in pre.varOrDeclareAssign())
    {
      ProcDeclOrAssign(vdecl.varOrDeclare(), vdecl.assignExp(), start_line);
    }
  }

  void ProcForPostStatements(bhlParser.ForPostIterContext post, int start_line)
  {
    foreach(var exp in post.expModifyOp())
    {
      ProcExpModifyOp(exp, exp.chainExp(), exp.modifyOp());
    }
  }

  public override object VisitStmYield(bhlParser.StmYieldContext ctx)
  {
    LSP_AddSemanticToken(ctx.YIELD(), SemanticToken.Keyword);

    CheckCoroCallValidity(ctx);

    int line = ctx.Start.Line;
    var ast = new AST_Yield(line);
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitStmYieldCall(bhlParser.StmYieldCallContext ctx)
  {
    LSP_AddSemanticToken(ctx.YIELD(), SemanticToken.Keyword);

    var ret_type = ProcFuncCallExp(ctx, ctx.chainExp(), yielded: true);
    ProcPopNonConsumed(ret_type);
    return null;
  }

  public override object VisitStmYieldWhile(bhlParser.StmYieldWhileContext ctx)
  {
    //NOTE: we're going to generate the following code
    //while(cond) { yield() }

    LSP_AddSemanticToken(ctx.YIELD(), SemanticToken.Keyword);
    LSP_AddSemanticToken(ctx.WHILE(), SemanticToken.Keyword);

    CheckCoroCallValidity(ctx);

    var ast = new AST_Block(BlockType.WHILE);

    PushBlock(ast);

    var cond = new AST_Block(BlockType.SEQ);
    PushAST(cond);
    bool ok = TryVisit(ctx.exp());
    PopAST();

    if(!ok || !types.CheckAssign(Types.Bool, Annotate(ctx.exp()), errors))
      return null;

    ast.AddChild(cond);

    var body = new AST_Block(BlockType.SEQ);
    int line = ctx.Start.Line;
    body.AddChild(new AST_Yield(line));
    ast.AddChild(body);

    PopBlock(ast);

    PeekAST().AddChild(ast);
    return null;
  }

  public override object VisitStmForeach(bhlParser.StmForeachContext ctx)
  {
    LSP_AddSemanticToken(ctx.FOREACH(), SemanticToken.Keyword);
    LSP_AddSemanticToken(ctx.foreachExp().IN(), SemanticToken.Keyword);
    
    var local_scope = new LocalScope(false, curr_scope);
    PushScope(local_scope);
    local_scope.Enter();

    if(ctx.foreachExp().varOrDeclare().Length == 1)
    {
      //NOTE: we're going to generate the following code
      //
      //$foreach_tmp = arr
      //$foreach_cnt = 0
      //while($foreach_cnt < $foreach_tmp.Count)
      //{
      // arr_it = $foreach_tmp[$foreach_cnt]
      // ...
      // $foreach_cnt++
      //}
    
      var vod = ctx.foreachExp().varOrDeclare()[0];
      var vd = vod.varDeclare();
      Proxy<IType> iter_type;
      AST_Tree iter_ast_decl = null;
      VariableSymbol iter_symb = null;
      if(vod.NAME() != null)
      {
        iter_symb = curr_scope.ResolveWithFallback(vod.NAME().GetText()) as VariableSymbol;
        if(iter_symb == null)
        {
          AddError(vod.NAME(), "symbol is not a valid variable");
          goto Bail;
        }
        iter_type = iter_symb.type;
      }
      else
      {
        var vd_type = new Proxy<IType>();
        if(vd.type().GetText() == "var")
        {
          var predicted_arr_type = PredictType(ctx.foreachExp().exp()) as GenericArrayTypeSymbol;
          if(predicted_arr_type == null)
          {
            AddError(ctx.foreachExp().exp(), "expression is not of array type");
            goto Bail;
          }
          vd_type = predicted_arr_type.item_type;
        }
        else
          vd_type = ParseType(vd.type());

        iter_ast_decl = ProcDeclVar(
          curr_scope, 
          vd.NAME(), 
          vd_type,
          vd.type(),
          is_ref: false, 
          func_arg: false, 
          write: false,
          symb: out iter_symb
        );
        if(iter_ast_decl == null)
          goto Bail;
        iter_type = iter_symb.type;
      }
      var arr_type = (ArrayTypeSymbol)curr_scope.R().TArr(iter_type).Get();

      PushJsonType(arr_type);
      var exp = ctx.foreachExp().exp();
      //evaluating array expression
      bool ok = TryVisit(exp);
      PopJsonType();
      if(!ok || !types.CheckAssign(arr_type, Annotate(exp), errors))
        goto Bail;

      var arr_tmp_name = "$foreach_tmp" + exp.Start.Line + "_" + exp.Start.Column;
      var arr_tmp_symb = curr_scope.ResolveWithFallback(arr_tmp_name) as VariableSymbol;
      if(arr_tmp_symb == null)
      {
        arr_tmp_symb = new VariableSymbol(Annotate(exp), arr_tmp_name, curr_scope.R().T(iter_type));
        curr_scope.Define(arr_tmp_symb);
      }

      var arr_cnt_name = "$foreach_cnt" + exp.Start.Line + "_" + exp.Start.Column;
      var arr_cnt_symb = curr_scope.ResolveWithFallback(arr_cnt_name) as VariableSymbol;
      if(arr_cnt_symb == null)
      {
        arr_cnt_symb = new VariableSymbol(Annotate(exp), arr_cnt_name, Types.Int);
        curr_scope.Define(arr_cnt_symb);
      }

      PeekAST().AddChild(new AST_Call(EnumCall.VARW, ctx.Start.Line, arr_tmp_symb));
      //declaring counter var
      PeekAST().AddChild(new AST_VarDecl(arr_cnt_symb, is_ref: false));

      //declaring iterating var
      if(iter_ast_decl != null)
        PeekAST().AddChild(iter_ast_decl);

      var ast = new AST_Block(BlockType.WHILE);
      PushBlock(ast);

      //while condition
      var cond = new AST_Block(BlockType.SEQ);
      var bin_op = new AST_BinaryOpExp(EnumBinaryOp.LT, ctx.Start.Line);
      bin_op.AddChild(new AST_Call(EnumCall.VAR, ctx.Start.Line, arr_cnt_symb));
      bin_op.AddChild(new AST_Call(EnumCall.VAR, ctx.Start.Line, arr_tmp_symb));
      bin_op.AddChild(new AST_Call(EnumCall.MVAR, ctx.Start.Line, arr_type.Resolve("Count")));
      cond.AddChild(bin_op);
      ast.AddChild(cond);

      //while body
      PushAST(ast);
      var block = ProcBlock(BlockType.SEQ, ctx.block()?.statement());
      if(block == null)
        goto Bail;
      //prepending filling of the iterator var
      block.children.Insert(0, new AST_Call(EnumCall.VARW, ctx.Start.Line, iter_symb));
      var arr_at = new AST_Call(EnumCall.ARR_IDX, ctx.Start.Line, null);
      block.children.Insert(0, arr_at);
      block.children.Insert(0, new AST_Call(EnumCall.VAR, ctx.Start.Line, arr_cnt_symb));
      block.children.Insert(0, new AST_Call(EnumCall.VAR, ctx.Start.Line, arr_tmp_symb));

      block.AddChild(new AST_Continue(jump_marker: true));
      //appending counter increment
      block.AddChild(new AST_Inc(arr_cnt_symb));
      PopAST();

      PeekAST().AddChild(ast);

      PopBlock(ast);
    }
    else if(ctx.foreachExp().varOrDeclare().Length == 2)
    {
      //NOTE: we're going to generate the following code
      //
      //$foreach_en = map.GetEnumerator() 
      //while($foreach_en.MoveNext())
      //{
      // map_key = $foreach_en.Current.Key
      // map_val = $foreach_en.Current.Value
      // ...
      //}

      var vod_key = ctx.foreachExp().varOrDeclare()[0];
      var vd_key = vod_key.varDeclare();
      var vd_key_type = new Proxy<IType>();
      var vod_val = ctx.foreachExp().varOrDeclare()[1];
      var vd_val = vod_val.varDeclare();
      var vd_val_type = new Proxy<IType>();

      if(vd_key.type().GetText() == "var" || vd_val.type().GetText() == "var")
      {
        var predicted_map_type = PredictType(ctx.foreachExp().exp()) as GenericMapTypeSymbol;
        if(predicted_map_type == null)
        {
          AddError(ctx.foreachExp().exp(), "expression is not of map type");
          goto Bail;
        }
        vd_key_type = predicted_map_type.key_type;
        vd_val_type = predicted_map_type.val_type;
      }

      if(vd_key.type().GetText() != "var")
        vd_key_type = ParseType(vd_key.type());

      if(vd_val.type().GetText() != "var")
        vd_val_type = ParseType(vd_val.type());

      Proxy<IType> key_iter_type;
      AST_Tree key_iter_ast_decl = null;
      VariableSymbol key_iter_symb = null;
      if(vod_key.NAME() != null)
      {
        key_iter_symb = curr_scope.ResolveWithFallback(vod_key.NAME().GetText()) as VariableSymbol;
        if(key_iter_symb == null)
        {
          AddError(vod_key.NAME(), "symbol is not a valid variable");
          goto Bail;
        }
        key_iter_type = key_iter_symb.type;
      }
      else
      {
        key_iter_ast_decl = ProcDeclVar(
          curr_scope, 
          vd_key.NAME(), 
          vd_key_type, 
          vd_key.type(),
          is_ref: false, 
          func_arg: false, 
          write: false,
          symb: out key_iter_symb 
        );
        if(key_iter_ast_decl == null)
          goto Bail;
        key_iter_type = key_iter_symb.type;
      }

      Proxy<IType> val_iter_type;
      AST_Tree val_iter_ast_decl = null;
      VariableSymbol val_iter_symb = null;
      if(vod_val.NAME() != null)
      {
        val_iter_symb = curr_scope.ResolveWithFallback(vod_val.NAME().GetText()) as VariableSymbol;
        if(val_iter_symb == null)
        {
          AddError(vod_val.NAME(), "symbol is not a valid variable");
          goto Bail;
        }
        val_iter_type = val_iter_symb.type;
      }
      else
      {
        val_iter_ast_decl = ProcDeclVar(
          curr_scope, 
          vd_val.NAME(), 
          vd_val_type, 
          vd_val.type(),
          is_ref: false, 
          func_arg: false, 
          write: false,
          symb: out val_iter_symb
        );
        if(val_iter_ast_decl == null)
          goto Bail;
        val_iter_type = val_iter_symb.type;
      }
      var map_type = (MapTypeSymbol)curr_scope.R().TMap(key_iter_type, val_iter_type).Get();

      PushJsonType(map_type);
      var exp = ctx.foreachExp().exp();
      //evaluating array expression
      bool ok = TryVisit(exp);
      PopJsonType();
      if(!ok || !types.CheckAssign(map_type, Annotate(exp), errors))
        goto Bail;

      var map_tmp_en_name = "$foreach_en" + exp.Start.Line + "_" + exp.Start.Column;
      var map_tmp_en_symb = curr_scope.ResolveWithFallback(map_tmp_en_name) as VariableSymbol;
      if(map_tmp_en_symb == null)
      {
        map_tmp_en_symb = new VariableSymbol(Annotate(exp), map_tmp_en_name, Types.Any);
        curr_scope.Define(map_tmp_en_symb);
      }

      //let's call GetEnumerator
      PeekAST().AddChild(new AST_Call(EnumCall.MVAR, ctx.Start.Line, map_type.Resolve("$Enumerator")));
      PeekAST().AddChild(new AST_Call(EnumCall.VARW, ctx.Start.Line, map_tmp_en_symb));

      //declaring iterating val
      if(key_iter_ast_decl != null)
        PeekAST().AddChild(key_iter_ast_decl);
      //declaring iterating key
      if(val_iter_ast_decl != null)
        PeekAST().AddChild(val_iter_ast_decl);

      var ast = new AST_Block(BlockType.WHILE);
      PushBlock(ast);

      //while condition
      var cond = new AST_Block(BlockType.SEQ);
      cond.AddChild(new AST_Call(EnumCall.VAR, ctx.Start.Line, map_tmp_en_symb));
      cond.AddChild(new AST_Call(EnumCall.MVAR, ctx.Start.Line, map_type.enumerator_type.Resolve("Next")));
      ast.AddChild(cond);

      //while body
      PushAST(ast);
      var block = ProcBlock(BlockType.SEQ, ctx.block()?.statement());
      if(block == null)
        goto Bail;
      //prepending filling of k/v
      block.children.Insert(0, new AST_Call(EnumCall.VARW, ctx.Start.Line, val_iter_symb));
      block.children.Insert(0, new AST_Call(EnumCall.VARW, ctx.Start.Line, key_iter_symb));
      block.children.Insert(0, new AST_Call(EnumCall.MFUNC, ctx.Start.Line, map_type.enumerator_type.Resolve("Current")));
      block.children.Insert(0, new AST_Call(EnumCall.VAR, ctx.Start.Line, map_tmp_en_symb));

      block.AddChild(new AST_Continue(jump_marker: true));
      PopAST();

      PeekAST().AddChild(ast);

      PopBlock(ast);
    }
    else
      AddError(ctx.foreachExp(), "invalid 'foreach' syntax");

  Bail:
    local_scope.Exit();
    PopScope();

    return null;
  }

  IType PredictType(IParseTree tree)
  {
    PushAST(new AST_Interim());
    bool ok = TryVisit(tree);
    PopAST();
    return !ok ? null : Annotate(tree).eval_type;
  }

  AST_Block ProcBlock(BlockType type, IParseTree[] sts)
  {
    if(sts == null)
      return null;

    bool is_paral = 
      type == BlockType.PARAL || 
      type == BlockType.PARAL_ALL;

    var local_scope = new LocalScope(is_paral, curr_scope);
    PushScope(local_scope);
    local_scope.Enter();

    var ast = new AST_Block(type);
    PushBlock(ast);

    var tmp = new AST_Interim();
    PushAST(ast);
    foreach(var st in sts)
    {
      //NOTE: we need to understand if we need to wrap statements
      //      with a group 
      if(is_paral)
      {
        PushAST(tmp);
        TryVisit(st);
        PopAST();

        //NOTE: wrapping in group only in case there are more than one child
        if(tmp.children.Count > 1)
        {
          var seq = new AST_Block(BlockType.SEQ);
          for(int c=0;c<tmp.children.Count;++c)
            seq.AddChild(tmp.children[c]);
          ast.AddChild(seq);
        }
        else if(tmp.children.Count > 0)
          ast.AddChild(tmp.children[0]);
        tmp.children.Clear();
      }
      else
        TryVisit(st);
    }
    PopAST();

    local_scope.Exit();
    PopScope();

    if(is_paral)
      return_found.Remove(PeekFuncDecl());

    PopBlock(ast);

    PeekAST().AddChild(ast);
    return ast;
  }

  public struct ExpChainItems
  {
    bhlParser.ChainExpItemContext[] items_arr; 
    List<ParserRuleContext> items_lst;

    public int Count {
      get {
        if(items_lst != null)
          return items_lst.Count;
        return items_arr == null ? 0 : items_arr.Length;
      }
    }

    public ExpChainItems(bhlParser.ChainExpItemContext[] items)
    {
      items_arr = items;
      items_lst = null;
    }
    
    public ParserRuleContext At(int i)
    {
      if(items_lst != null)
        return items_lst[i];

      return _Get(items_arr[i]);
    }

    void _Add(ParserRuleContext ctx)
    {
      //let's make a copy
      //TODO: a hybrid approach can be used instead
      if(items_lst == null)
      {
        items_lst = new List<ParserRuleContext>();
        if(items_arr != null)
        {
          foreach(var item in items_arr)
            items_lst.Add(_Get(item));
        }
      }
      items_lst.Add(ctx);
    }

    static ParserRuleContext _Get(bhlParser.ChainExpItemContext item)
    {
      if(item.callArgs() != null)
        return item.callArgs();
      else if(item.memberAccess() != null)
        return item.memberAccess();
      else
        return item.arrAccess();
    }

    public void Add(bhlParser.ChainExpItemContext item)
    {
      _Add(_Get(item));
    }

    public void Add(bhlParser.ChainExpItemContext[] items)
    {
      foreach(var item in items)
        Add(item);
    }

    public void Add(bhlParser.MemberAccessContext macc)
    {
      _Add(macc);
    }

    public void Add(bhlParser.CallArgsContext cargs)
    {
      _Add(cargs);
    }

    public void Add(bhlParser.ArrAccessContext acc)
    {
      _Add(acc);
    }
  }

  public struct ExpChain
  {
    public ParserRuleContext ctx;
    public bhlParser.NameContext name_ctx;
    public bhlParser.ExpContext paren_exp_ctx;
    public bhlParser.FuncLambdaContext lmb_ctx;
    public ExpChainItems items; 

    public bool Incomplete { get ; private set; }

    public bool IsGlobalNs { 
      get { 
        return name_ctx?.GLOBAL() != null; 
      } 
    }

    public bool IsFuncCall {
      get { 
        return items.Count > 0 && 
          items.At(items.Count-1) is bhlParser.CallArgsContext;
      } 
    }

    public bool IsVarAccess {
      get { 
        return 
          (items.Count == 0 && name_ctx != null) || 
          (items.Count > 0 && 
          (items.At(items.Count-1) is bhlParser.MemberAccessContext || 
           items.At(items.Count-1) is bhlParser.ArrAccessContext));
      } 
    }

    public ExpChain(ParserRuleContext ctx, bhlParser.ChainExpContext chain)
    {
      this.ctx = ctx;
      this.name_ctx = null;
      this.paren_exp_ctx = null;
      this.lmb_ctx = null;
      items = new ExpChainItems();

      Incomplete = false;

      Init(ctx, chain);
    }

    void Init(ParserRuleContext ctx, bhlParser.ChainExpContext chain)
    {
      this.ctx = ctx;

      if(chain.name() != null)
        name_ctx = chain.name();
      //paren chain
      else if(chain.exp() != null)
        paren_exp_ctx = chain.exp();
      else if(chain.funcLambda() != null)
        lmb_ctx = chain.funcLambda();
      items = new ExpChainItems(chain.chainExpItem());
    }
  }

  bool TryVisit(IParseTree tree)
  {
    if(tree?.ChildCount > 0)
    {
      Visit(tree);
      return true;
    }
    return false;
  }

  void LSP_SetSymbol(AnnotatedParseTree ann, Symbol s)
  {
    if(s is VariableSymbol vs && vs._upvalue != null)
    {
      //we need to find the 'most top one'
      var top = vs._upvalue;
      while(top._upvalue != null)
        top = top._upvalue; 
      ann.lsp_symbol = top;
    }
    else
      ann.lsp_symbol = s;
  }

  void LSP_SetSymbol(IParseTree t, Symbol s)
  {
    LSP_SetSymbol(Annotate(t), s);
  }

  void LSP_AddSemanticToken(ITerminalNode token, SemanticToken idx, SemanticModifier mods = 0)
  {
    if(token == null)
      return;

    semantic_tokens.Add(new SemanticTokenNode() { 
      idx = token.Symbol.StartIndex,
      line = token.Symbol.Line,
      column = token.Symbol.Column,
      len = token.Symbol.StopIndex - token.Symbol.StartIndex + 1,
      type_idx = idx, 
      mods = mods
    });
    encoded_semantic_tokens.Clear();
  }
  
  void LSP_AddSemanticToken(IParseTree tree, SemanticToken idx, SemanticModifier mods = 0)
  {
    if(tree == null)
      return;

    var interval = tree.SourceInterval;
    var a = tokens.Get(interval.a);
    var b = tokens.Get(interval.b);

    if(a.Line != b.Line)
      throw new Exception("Multiline semantic tokens not supported");

    semantic_tokens.Add(new SemanticTokenNode() { 
      idx = a.StartIndex,
      line = a.Line,
      column = a.Column,
      len = b.StopIndex - a.StartIndex +  1,
      type_idx = idx, 
      mods = mods
    });
  }

  //NOTE: synchronized with class below
  public enum SemanticToken
  {
    Class    = 0,
    Function = 1,
    Variable = 2,
    Number   = 3,
    String   = 4,
    Type     = 5,
    Keyword  = 6,
    Property = 7,
    Operator = 8,
    Parameter = 9,
  }

  [Flags]
  public enum SemanticModifier
  {
    Declaration = 1,
    Definition  = 2,
    Readonly    = 4,
    Static      = 8,
  }

  public static class SemanticTokens
  {
    public static string[] token_types = 
    {
      "class",
      "function",
      "variable",
      "number",
      "string",
      "type",
      "keyword",
      "property",
      "operator",
      "parameter"
    };
    
    public static string[] modifiers = 
    {
      "declaration",   // 1
      "definition",    // 2
      "readonly",      // 4
      "static",        // 8
    };
  }
}

} //namespace bhl
