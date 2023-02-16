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
  public bhlParser.ProgramContext program_tree { get; private set; }

  public ANTLR_Parsed(bhlParser parser)
  {
    this.parser = parser;
    this.tokens = parser.TokenStream;
    this.program_tree = parser.program();
  }

  public override string ToString()
  {
    return PrintTree(parser, program_tree);
  }

  public static string PrintTree(Parser parser, IParseTree root)
  {
    var sb = new System.Text.StringBuilder();
    DoPrintTree(root, sb, 0, parser.RuleNames);
    return sb.ToString();
  }

  static void DoPrintTree(IParseTree root, System.Text.StringBuilder sb, int offset, IList<String> rule_names) 
  {
    for(int i = 0; i < offset; i++)
      sb.Append("  ");
    
    sb.Append(Trees.GetNodeText(root, rule_names)).Append("\n");
    if(root is ParserRuleContext prc) 
    {
      if(prc.children != null) 
      {
        foreach(var child in prc.children)
        {
          if(child is IErrorNode)
            sb.Append("!");
          DoPrintTree(child, sb, offset + 1, rule_names);
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

  public int line { 
    get { 
      return tokens.Get(tree.SourceInterval.a).Line;  
    } 
  }

  public int column { 
    get { 
      return tokens.Get(tree.SourceInterval.a).Column;  
    } 
  }

  public string file { 
    get { 
      return module.file_path;
    } 
  }
}

public class ANTLR_Processor : bhlBaseVisitor<object>
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

  public FileImports imports { get; private set; } 

  //NOTE: passed from above
  CompileErrors errors;

  Dictionary<bhlParser.MimportContext, string> parsed_imports = new Dictionary<bhlParser.MimportContext, string>();

  //NOTE: module.ns linked with types.ns
  Namespace ns;

  ITokenStream tokens;
  Dictionary<IParseTree, AnnotatedParseTree> annotated_nodes = new Dictionary<IParseTree, AnnotatedParseTree>();

  class ParserPass
  {
    public IAST ast;
    public IScope scope;

    public bhlParser.VarDeclareAssignContext gvar_ctx;
    public VariableSymbol gvar_symb;

    public bhlParser.FuncDeclContext func_ctx;
    public AST_FuncDecl func_ast;
    public FuncSymbolScript func_symb;

    public bhlParser.ClassDeclContext class_ctx;
    public ClassSymbolScript class_symb;
    public AST_ClassDecl class_ast;

    public bhlParser.InterfaceDeclContext iface_ctx;
    public InterfaceSymbolScript iface_symb;

    public bhlParser.EnumDeclContext enum_ctx;

    public ParserPass(IAST ast, IScope scope, ParserRuleContext ctx)
    {
      this.ast = ast;
      this.scope = scope;
      this.gvar_ctx = ctx as bhlParser.VarDeclareAssignContext;
      this.func_ctx = ctx as bhlParser.FuncDeclContext;
      this.class_ctx = ctx as bhlParser.ClassDeclContext;
      this.iface_ctx = ctx as bhlParser.InterfaceDeclContext;
      this.enum_ctx = ctx as bhlParser.EnumDeclContext;
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

  //NOTE: used for tracking whether an expression is passable by 'ref',
  //      before visiting an expression we remember the old value and
  //      compare it to the new one: if they differ expression is not  
  //      passable by 'ref'
  int ref_compatible_exp_counter;

  Stack<AST_Tree> ast_stack = new Stack<AST_Tree>();

  public static CommonTokenStream Stream2Tokens(string file, Stream s, ErrorHandlers handlers)
  {
    var ais = new AntlrInputStream(s);
    var lex = new bhlLexer(ais);

    if(handlers?.lexer_listener != null)
    {
      lex.RemoveErrorListeners();
      lex.AddErrorListener(handlers.lexer_listener);
    }

    return new CommonTokenStream(lex);
  }

  public static bhlParser Stream2Parser(string file, Stream src, ErrorHandlers handlers)
  {
    var tokens = Stream2Tokens(file, src, handlers);
    var p = new bhlParser(tokens);

    if(handlers?.parser_listener != null)
    {
      p.RemoveErrorListeners();
      p.AddErrorListener(handlers.parser_listener);
    }

    if(handlers?.error_strategy != null)
      p.ErrorHandler = handlers.error_strategy;

    return p;
  }
  
  public static ANTLR_Processor MakeProcessor(
    Module module, 
    FileImports imports, 
    ANTLR_Parsed parsed/*can be null*/, 
    Types ts, 
    CompileErrors errors,
    ErrorHandlers err_handlers
    )
  {
    if(parsed == null)
    {
      using(var sfs = File.OpenRead(module.file_path))
      {
        return MakeProcessor(
          module, 
          imports, 
          sfs, 
          ts, 
          errors,
          err_handlers
        );
      }
    }
    else 
      return new ANTLR_Processor(
        parsed, 
        module, 
        imports, 
        ts,
        errors
      );
  }

  public static ANTLR_Processor MakeProcessor(
    Module module, 
    FileImports imports, 
    Stream src, 
    Types ts, 
    CompileErrors errors,
    ErrorHandlers err_handlers
    )
  {
    var p = Stream2Parser(module.file_path, src, err_handlers);
    var parsed = new ANTLR_Parsed(p);
    return new ANTLR_Processor(parsed, module, imports, ts, errors);
  }

  public ANTLR_Processor(
      ANTLR_Parsed parsed, 
      Module module, 
      FileImports imports, 
      Types types,
      CompileErrors errors
    )
  {
    this.parsed = parsed;
    this.tokens = parsed.tokens;

    this.types = types;
    this.module = module;
    this.imports = imports;

    this.errors = errors;

    ns = module.ns;
    ns.Link(types.ns);

    PushScope(ns);
  }

  void AddSemanticError(IParseTree place, string msg) 
  {
    errors.Add(new SemanticError(module, place, tokens, msg));
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
    VisitProgram(parsed.program_tree);
    PopAST();

    for(int p=0;p<passes.Count;++p)
    {
      var pass = passes[p];

      PushScope(pass.scope);

      Pass_OutlineGlobalVar(pass);

      Pass_OutlineInterfaceDecl(pass);

      Pass_OutlineClassDecl(pass);

      Pass_OutlineFuncDecl(pass);

      Pass_OutlineEnumDecl(pass);

      PopScope();
    }
  }

  internal void Phase_LinkImports(Dictionary<string, ANTLR_Processor> file2proc, IncludePath inc_path)
  {
    if(parsed_imports.Count == 0)
      return;

    var ast_import = new AST_Import();

    foreach(var kv in parsed_imports)
    {
      //let's check if it's a registered native module
      var reg_mod = types.FindRegisteredModule(kv.Value);
      if(reg_mod != null)
      {
        try
        {
          ns.Link(reg_mod.ns);
        }
        catch(SymbolError se)
        {
          errors.Add(se);
        }
        continue;
      }

      //let's check if it's a compiled module
      var file_path = imports.MapToFilePath(kv.Value);
      if(file_path == null || !File.Exists(file_path))
      {
        AddSemanticError(kv.Key, "invalid import");
        continue;
      }

      var imported_module = file2proc[file_path].module;

      //NOTE: let's add imported global vars to module's global vars index
      if(module.local_gvars_mark == -1)
        module.local_gvars_mark = module.gvars.Count;

      for(int i=0;i<imported_module.local_gvars_num;++i)
        module.gvars.index.Add(imported_module.gvars.index[i]);

      try
      {
        ns.Link(imported_module.ns);
      }
      catch(SymbolError se)
      {
        errors.Add(se);
        continue;
      }

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

      Pass_ParseFuncSignature(pass);

      PopScope();
    }
  }

  internal void Phase_ParseTypes2()
  {
    for(int p=0;p<passes.Count;++p)
    {
      var pass = passes[p];

      PushScope(pass.scope);

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

    result = new Result(module, root_ast, errors);
  }

  static public void ProcessAll(Dictionary<string, ANTLR_Processor> file2proc, IncludePath inc_path)
  {
    foreach(var kv in file2proc)
      kv.Value.Phase_Outline();

    foreach(var kv in file2proc)
      kv.Value.Phase_LinkImports(file2proc, inc_path);

    foreach(var kv in file2proc)
      kv.Value.Phase_ParseTypes1();

    foreach(var kv in file2proc)
      kv.Value.Phase_ParseTypes2();

    foreach(var kv in file2proc)
      kv.Value.Phase_ParseFuncBodies();
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
    var name = ctx.NORMALSTRING().GetText();
    //removing quotes
    name = name.Substring(1, name.Length-2);

    parsed_imports.Add(ctx, name);
  }

  void AddPass(ParserRuleContext ctx, IScope scope, IAST ast)
  {
    passes.Add(new ParserPass(ast, scope, ctx));
  }

	IType CommonVisitFuncCallExp(
    ParserRuleContext ctx,
    bhlParser.FuncCallExpContext exp,
    bool yielded = false
  )
  {
    if(yielded)
      CheckCoroCallValidity(ctx);

    var chain = new ExpChain(exp);

    IType curr_type = null;
    CommonProcExpChain(chain, ref curr_type, yielded: yielded);

    return curr_type;
  }

  void CommonPopNonConsumed(IType ret_type)
  {
    if(ret_type == null || ret_type == Types.Void)
      return;
    
    //let's pop unused returned value
    var multi_type = new MultiTypeProxy(ret_type);
    for(int i=0;i<multi_type.Count;++i)
      PeekAST().AddChild(new AST_PopValue());
  }

  public override object VisitStmCall(bhlParser.StmCallContext ctx)
  {
    var ret_type = CommonVisitFuncCallExp(ctx, ctx.funcCallExp());
    CommonPopNonConsumed(ret_type);
    return null;
  }

  public override object VisitStmVarUseless(bhlParser.StmVarUselessContext ctx)
  {
    AddSemanticError(ctx, "useless statement");
    return null;
  }

  public override object VisitStmLambdaUseless(bhlParser.StmLambdaUselessContext ctx)
  {
    AddSemanticError(ctx, "useless statement");
    return null;
  }

  public override object VisitStmInvalidAssign(bhlParser.StmInvalidAssignContext ctx)
  {
    AddSemanticError(ctx.assignExp(), "invalid assignment");
    return null;
  }

  bool CommonProcExpChain(
    ParserRuleContext ctx,
    bhlParser.ChainContext chain_ctx,
    ref IType curr_type,
    bool write = false,
    bool yielded = false
  )
  {
    var chain = new ExpChain(ctx, chain_ctx);

    return CommonProcExpChain(
      chain,
      ref curr_type,
      write,
      yielded
    );
  }

  bool CommonProcExpChain(
    ExpChain chain,
    ref IType curr_type, 
    bool write = false,
    bool yielded = false,
    IScope root_scope = null
   )
  {
    if(root_scope == null)
      root_scope = chain.IsGlobalNs ? ns : curr_scope;

    if(chain.lambda_call != null)
    {
      if(!CommonVisitLambdaCall(
        chain.ctx,
        chain.lambda_call,
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
      if(chain.exp_ctx != null)
      {
        Visit(chain.exp_ctx);
        curr_type = Annotate(chain.exp_ctx).eval_type;
      }

      IScope scope = root_scope;

      PushAST(new AST_Interim());

      var curr_name = root_name;

      int chain_offset = 0;

      if(root_name != null)
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
          AddSemanticError(root_name, "symbol '" + curr_name.GetText() + "' not resolved");
          PopAST();
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
          AddSemanticError(root_name, "bad chain call");
          PopAST();
          return false;
        }
      }

      if(!CommonVisitChainItems(
          chain.items, 
          chain_offset,
          ref curr_name,
          ref scope,
          ref curr_type,
          write
        ))
      {
        PopAST();
        return false;
      }

      //checking the leftover of the call chain or a root call
      if(curr_name != null)
      {
        CommonChainItem(
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

      var chain_ast = PeekAST();
      PopAST();

      ValidateChainCall(
        chain.ctx, 
        chain.items, 
        0,
        chain_ast.children, 
        yielded
      );

      PeekAST().AddChildren(chain_ast);
    }

    return true;
  }

  bool CommonVisitChainItems(
    ExpChainItems chain_items, 
    int chain_offset, 
    ref ITerminalNode curr_name, 
    ref IScope scope, 
    ref IType curr_type, 
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
        CommonChainItem(
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
        CommonChainItem(
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
          macc_name_symb = CommonChainItem(
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
          AddSemanticError(macc, "type doesn't support member access via '.'");
          return false;
        }

        if(!(macc_name_symb is ClassSymbol) && 
            scope.ResolveWithFallback(macc.NAME().GetText()) is FuncSymbol macc_fs && 
            macc_fs.attribs.HasFlag(FuncAttrib.Static))
        {
          AddSemanticError(macc, "calling static method on instance is forbidden");
          return false;
        }

        curr_name = macc.NAME();
      }
    }

    return true;
  }
  
  void ValidateChainCall(
    ParserRuleContext ctx, 
    ExpChainItems chain_items, 
    int offset,
    List<IAST> chain_ast, 
    bool yielded
  )
  {
    for(int i = offset; i < chain_ast.Count; ++i)
    {
      if(chain_ast[i] is AST_Call call)
      {
        if((call.type == EnumCall.FUNC || call.type == EnumCall.MFUNC) &&
            call.symb is FuncSymbol fs)
        {
          ValidateFuncCall(ctx, chain_items, i, chain_ast.Count-1 == i, fs.signature, yielded);
        }
        else if((call.type == EnumCall.FUNC_VAR || call.type == EnumCall.FUNC_MVAR) && call.symb is VariableSymbol vs)
        {
          ValidateFuncCall(ctx, chain_items, i, chain_ast.Count-1 == i, vs.type.Get() as FuncSignature, yielded);
        }
      }
    }
  }

  void ValidateFuncCall(ParserRuleContext ctx, ExpChainItems chain_items, int idx, bool is_last, FuncSignature fsig, bool yielded)
  {
    if(PeekFuncDecl() == null)
    {
      AddSemanticError(idx == 0 ? ctx : chain_items.At(idx-1), "function calls not allowed in global context");
      return;
    }

    if(is_last)
    {
      if(!yielded && fsig.is_coro)
      {
        AddSemanticError(idx == 0 ? ctx : chain_items.At(idx-1), "coro function must be called via yield");
        return;
      }
      else if(yielded && !fsig.is_coro)
      {
        AddSemanticError(idx == 0 ? ctx : chain_items.At(idx-1), "not a coro function");
        return;
      }
    }
    else 
    {
      if(fsig.is_coro)
      {
        AddSemanticError(idx == 0 ? ctx : chain_items.At(idx-1), "coro function must be called via yield");
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
        AddSemanticError(curr_name, "no base class found");
        return;
      }
      else
      {
        name_symb = cs.super_class; 
        scope = cs.super_class;
        if(chain_items.Count <= chain_offset)
        {
          AddSemanticError(curr_name, "bad base call");
          return;
        }
        var macc = chain_items.At(chain_offset) as bhlParser.MemberAccessContext;
        if(macc == null)
        {
          AddSemanticError(chain_items.At(chain_offset), "bad base call");
          return;
        }
        curr_name = macc.NAME(); 
        ++chain_offset;

        PeekAST().AddChild(new AST_Call(EnumCall.VAR, line, PeekFuncDecl().Resolve("this")));
        PeekAST().AddChild(new AST_TypeCast(cs.super_class, true/*force type*/, line));
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
          AddSemanticError(chain_items.At(chain_offset), "bad chain call");
          return;
        }
        name_symb = scope.ResolveWithFallback(macc.NAME().GetText());
        if(name_symb == null)
        {
          AddSemanticError(macc.NAME(), "symbol '" + macc.NAME().GetText() + "' not resolved");
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

  Symbol CommonChainItem(
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
      name_symb = scope.ResolveChained(name.GetText(), is_root: is_root);
      if(name_symb == null)
      {
        AddSemanticError(name, "symbol '" + name.GetText() + "' not resolved");
        return name_symb;
      }

      Annotate(name.Parent).lsp_symbol = name_symb;

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
          AddSemanticError(name, "symbol is not a function");
          return name_symb;
        }

        //func ptr
        if(var_symb != null && var_symb.type.Get() is FuncSignature)
        {
          var ftype = var_symb.type.Get() as FuncSignature;

          if(!(scope is IInstanceType))
          {
            ast = new AST_Call(EnumCall.FUNC_VAR, line, var_symb);
            AddCallArgs(ftype, cargs, ref ast);
            type = ftype.ret_type.Get();
          }
          else //func ptr member of class
          {
            PeekAST().AddChild(new AST_Call(EnumCall.MVAR, line, var_symb));
            ast = new AST_Call(EnumCall.FUNC_MVAR, line, var_symb);
            AddCallArgs(ftype, cargs, ref ast);
            type = ftype.ret_type.Get();
          }
        }
        else if(func_symb != null)
        {
          ast = new AST_Call(scope is IInstanceType && !func_symb.attribs.HasFlag(FuncAttrib.Static) ? EnumCall.MFUNC : EnumCall.FUNC, line, func_symb);
          AddCallArgs(func_symb, cargs, ref ast);
          type = func_symb.GetReturnType();
        }
        else
        {
          AddSemanticError(name, "symbol is not a function");
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
            AddSemanticError(name, "attributes not supported by interfaces");
            return name_symb;
          }

          ast = new AST_Call(fld_symb != null && !is_global ? 
            (is_write ? EnumCall.MVARW : EnumCall.MVAR) : 
            (is_global ? (is_write ? EnumCall.GVARW : EnumCall.GVAR) : (is_write ? EnumCall.VARW : EnumCall.VAR)), 
            line, 
            var_symb
          );
          //handling passing by ref for class fields
          if(fld_symb != null && PeekCallByRef())
          {
            if(scope is ClassSymbolNative)
            {
              AddSemanticError(name, "getting native class field by 'ref' not supported");
              return name_symb;
            }
            ast.type = EnumCall.MVARREF; 
          }
          else if(fld_symb != null && scope is ClassSymbolNative)
          {
            if(ast.type == EnumCall.MVAR && fld_symb.getter == null)
            {
              AddSemanticError(name, "get operation is not defined");
              return name_symb;
            }
            else if(ast.type == EnumCall.MVARW && fld_symb.setter == null)
            {
              AddSemanticError(name, "set operation is not defined");
              return name_symb;
            }
          }

          type = var_symb.type.Get();
        }
        else if(func_symb != null)
        {
          ast = new AST_Call(EnumCall.GET_ADDR, line, func_symb);
          type = func_symb.signature;
        }
        else if(enum_symb != null)
        {
          if(is_leftover)
          {
            AddSemanticError(name, "symbol usage is not valid");
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
          type = class_symb;
        }
        else
        {
          AddSemanticError(name, "symbol usage is not valid");
          return name_symb;
        }
      }
    }
    else if(cargs != null)
    {
      var ftype = type as FuncSignature;
      if(ftype == null)
      {
        AddSemanticError(cargs, "no func to call");
        return name_symb;
      }
      
      ast = new AST_Call(EnumCall.LMBD, line, null);
      AddCallArgs(ftype, cargs, ref ast);
      type = ftype.ret_type.Get();
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
      Visit(arr_exp);

      if(Annotate(arr_exp).eval_type != Types.Int)
      {
        AddSemanticError(arr_exp, "array index expression is not of type int");
        return;
      }

      type = arr_type.item_type.Get();

      var ast = new AST_Call(write ? EnumCall.ARR_IDXW : EnumCall.ARR_IDX, line, null);
      PeekAST().AddChild(ast);
    }
    else if(type is MapTypeSymbol map_type)
    {
      var arr_exp = arracc.exp();
      Visit(arr_exp);

      if(!Annotate(arr_exp).eval_type.Equals(map_type.key_type.Get()))
      {
        AddSemanticError(arr_exp, "not compatible map key types");
        return;
      }

      type = map_type.val_type.Get();

      var ast = new AST_Call(write ? EnumCall.MAP_IDXW : EnumCall.MAP_IDX, line, null);
      PeekAST().AddChild(ast);
    }
    else
    {
      AddSemanticError(arracc, "accessing not an array/map type '" + type?.GetName() + "'");
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
    var func_args = func_symb.GetArgs();
    int total_args_num = func_symb.GetTotalArgsNum();
    //Console.WriteLine(func_args.Count + " " + total_args_num);
    int default_args_num = func_symb.GetDefaultArgsNum();
    int required_args_num = total_args_num - default_args_num;
    var args_info = new FuncArgsInfo();

    var norm_cargs = new List<NormCallArg>(total_args_num);
    for(int i=0;i<total_args_num;++i)
    {
      var arg = new NormCallArg();
      arg.orig = (Symbol)func_args[i];
      norm_cargs.Add(arg); 
    }

    var variadic_args = new List<bhlParser.CallArgContext>();
    if(func_symb.attribs.HasFlag(FuncAttrib.VariadicArgs))
      norm_cargs[total_args_num-1].variadic = true;

    //1. filling normalized call args
    for(int ci=0;ci<cargs.callArg().Length;++ci)
    {
      var ca = cargs.callArg()[ci];
      var ca_name = ca.NAME();

      var idx = ci;
      //NOTE: checking if it's a named arg and finding its index
      if(ca_name != null)
      {
        idx = func_args.IndexOf(ca_name.GetText());
        if(idx == -1)
        {
          AddSemanticError(ca_name, "no such named argument");
          return;
        }

        if(norm_cargs[idx].ca != null)
        {
          AddSemanticError(ca_name, "argument already passed before");
          return;
        }
      }

      if(func_symb.attribs.HasFlag(FuncAttrib.VariadicArgs) && idx >= func_args.Count-1)
        variadic_args.Add(ca);
      else if(idx >= func_args.Count)
      {
        AddSemanticError(ca, "there is no argument " + (idx + 1) + ", total arguments " + func_args.Count);
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
            AddSemanticError(next_arg, "missing argument '" + norm_cargs[i].orig.name + "'");
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
                AddSemanticError(next_arg, "max default arguments reached");
                PopAST();
                return;
              }
            }
            else
            {
              AddSemanticError(next_arg, "missing argument '" + norm_cargs[i].orig.name + "'");
              PopAST();
              return;
            }
          }
        }
        else
        {
          if(na.ca.VARIADIC() != null)
          {
            AddSemanticError(na.ca, "not variadic argument");
            PopAST();
            return;
          }

          prev_ca = na.ca;
          if(!args_info.IncArgsNum())
          {
            AddSemanticError(na.ca, "max arguments reached");
            PopAST();
            return;
          }

          var func_arg_symb = (FuncArgSymbol)func_args[i];
          var func_arg_type = func_arg_symb.parsed == null ? func_arg_symb.type.Get() : func_arg_symb.parsed.eval_type;  

          bool is_ref = na.ca.isRef() != null;
          if(!is_ref && func_arg_symb.is_ref)
          {
            AddSemanticError(na.ca, "'ref' is missing");
            PopAST();
            return;
          }
          else if(is_ref && !func_arg_symb.is_ref)
          {
            AddSemanticError(na.ca, "argument is not a 'ref'");
            PopAST();
            return;
          }

          PushCallByRef(is_ref);
          PushJsonType(func_arg_type);
          PushAST(new AST_Interim());
          int old_ref_counter = ref_compatible_exp_counter;
          Visit(na.ca);

          if(is_ref && ref_compatible_exp_counter == old_ref_counter)
          {
            AddSemanticError(na.ca, "expression is not passable by 'ref'");
            PopAST();
            PopAST();
            return;
          }

          PopAddOptimizeAST();
          PopJsonType();
          PopCallByRef();

          if(func_arg_symb.type.Get() == null)
          {
            AddSemanticError(na.ca, "unresolved type " + func_arg_symb.type);
            PopAST();
            return;
          }
          if(!types.CheckAssign(func_arg_symb.type.Get(), Annotate(na.ca), errors))
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
          AddSemanticError(na.ca, "max arguments reached");
          PopAST();
          return;
        }

        var func_arg_symb = (FuncArgSymbol)func_args[i];
        var func_arg_type = func_arg_symb.parsed == null ? func_arg_symb.type.Get() : func_arg_symb.parsed.eval_type;  

        var varg_arr_type = (ArrayTypeSymbol)func_arg_type;

        if(variadic_args.Count == 1 && variadic_args[0].VARIADIC() != null)
        {
          PushJsonType(varg_arr_type);
          Visit(variadic_args[0]);
          PopJsonType();

          if(!types.CheckAssign(varg_arr_type, Annotate(variadic_args[0]), errors))
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
            AddSemanticError(na.ca, "unresolved type " + varg_arr_type.item_type);
            PopAST();
            return;
          }
          var varg_ast = new AST_JsonArr(varg_arr_type, cargs.Start.Line);

          PushAST(varg_ast);
          PushJsonType(varg_type);
          for(int vidx = 0; vidx < variadic_args.Count; ++vidx)
          {
            var vca = variadic_args[vidx];
            Visit(vca);
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
    int ca_len = cargs.callArg().Length; 
    IParseTree prev_ca = null;
    PushAST(call);
    for(int i=0;i<func_args.Count;++i)
    {
      var arg_type_ref = func_args[i]; 

      if(i == ca_len)
      {
        var next_arg = FindNextCallArg(cargs, prev_ca);
        AddSemanticError(next_arg, "missing argument of type '" + arg_type_ref.path + "'");
        PopAST();
        return;
      }

      var ca = cargs.callArg()[i];
      var ca_name = ca.NAME();

      if(ca_name != null)
      {
        AddSemanticError(ca_name, "named arguments not supported for function pointers");
        PopAST();
        return;
      }

      var arg_type = arg_type_ref.Get();
      PushJsonType(arg_type);
      PushAST(new AST_Interim());
      Visit(ca);
      PopAddOptimizeAST();
      PopJsonType();

      if(!types.CheckAssign(arg_type is RefType rt ? rt.subj.Get() : arg_type, Annotate(ca), errors))
      {
        PopAST();
        return;
      }

      if(arg_type_ref.Get() is RefType && ca.isRef() == null)
      {
        AddSemanticError(ca, "'ref' is missing");
        PopAST();
        return;
      }
      else if(!(arg_type_ref.Get() is RefType) && ca.isRef() != null)
      {
        AddSemanticError(ca, "argument is not a 'ref'");
        PopAST();
        return;
      }

      prev_ca = ca;
    }
    PopAST();

    if(ca_len != func_args.Count)
    {
      AddSemanticError(cargs, "too many arguments");
      return;
    }

    var args_info = new FuncArgsInfo();
    if(!args_info.SetArgsNum(func_args.Count))
    {
      AddSemanticError(cargs, "max arguments reached");
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
    for(int i=0;i<cargs.callArg().Length;++i)
    {
      var ch = cargs.callArg()[i];
      if(ch == curr && (i+1) < cargs.callArg().Length)
        return cargs.callArg()[i+1];
    }

    //NOTE: graceful fallback
    return cargs;
  }

  FuncSignature ParseFuncSignature(bool is_async, bhlParser.RetTypeContext ret_ctx, bhlParser.TypesContext types_ctx)
  {
    var ret_type = ParseType(ret_ctx);

    var arg_types = new List<Proxy<IType>>();
    if(types_ctx != null)
    {
      for(int i=0;i<types_ctx.refType().Length;++i)
      {
        var refType = types_ctx.refType()[i];
        var arg_type = ParseType(refType.type());
        if(refType.isRef() != null)
          arg_type = curr_scope.R().TRef(arg_type);
        arg_types.Add(arg_type);
      }
    }

    return new FuncSignature(is_async, ret_type, arg_types);
  }

  FuncSignature ParseFuncSignature(bhlParser.FuncTypeContext ctx)
  {
    return ParseFuncSignature(ctx.coroFlag() != null, ctx.retType(), ctx.types());
  }

  FuncSignature ParseFuncSignature(bool is_async, Proxy<IType> ret_type, bhlParser.FuncParamsContext fparams, out int default_args_num)
  {
    default_args_num = 0;
    var sig = new FuncSignature(is_async, ret_type);
    if(fparams != null)
    {
      for(int i=0;i<fparams.funcParamDeclare().Length;++i)
      {
        var vd = fparams.funcParamDeclare()[i];

        var tp = ParseType(vd.type());
        if(vd.isRef() != null)
          tp = curr_scope.R().T(new RefType(tp));

        if(vd.VARIADIC() != null)
        {
          if(vd.isRef() != null)
            AddSemanticError(vd.isRef(), "pass by ref not allowed");

          if(i != fparams.funcParamDeclare().Length-1)
            AddSemanticError(vd, "variadic argument must be last");

          if(vd.assignExp() != null)
            AddSemanticError(vd.assignExp(), "default argument is not allowed");
          sig.has_variadic = true;
          ++default_args_num;
        }
        else if(vd.assignExp() != null)
          ++default_args_num;
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
      AddSemanticError(parsed, "type '" + tp.path + "' not found");

    return tp;
  }

  Proxy<IType> ParseType(bhlParser.TypeContext ctx)
  {
    Proxy<IType> tp;
    if(ctx.funcType() == null)
    {
      if(ctx.nsName().GetText() == "var")
        AddSemanticError(ctx.nsName(), "invalid usage context");

      tp = curr_scope.R().T(ctx.nsName().GetText());
    }
    else
      tp = curr_scope.R().T(ParseFuncSignature(ctx.funcType()));

    if(ctx.ARR() != null)
    {
      if(tp.Get() == null)
        AddSemanticError(ctx.nsName(), "type '" + tp.path + "' not found");
      tp = curr_scope.R().TArr(tp);
    }
    else if(ctx.mapType() != null)
    {
      if(tp.Get() == null)
        AddSemanticError(ctx.nsName(), "type '" + tp.path + "' not found");
      var ktp = curr_scope.R().T(ctx.mapType().nsName().GetText());
      if(ktp.Get() == null)
        AddSemanticError(ctx.mapType().nsName(), "type '" + ktp.path + "' not found");
      tp = curr_scope.R().TMap(ktp, tp);
    }

    if(tp.Get() == null)
      AddSemanticError(ctx, "type '" + tp.path + "' not found");

   return tp;
  }

  AST_Tree CommonVisitLambda(
     ParserRuleContext ctx, 
     bhlParser.FuncLambdaContext funcLambda, 
     ref IType curr_type,
     bool yielded = false
   )
  {
    if(yielded)
      CheckCoroCallValidity(ctx);

    var tp = ParseType(funcLambda.retType());

    var func_name = Hash.CRC32(module.name) + "_lmb_" + funcLambda.Stop.Line;
    var upvals = new List<AST_UpVal>();
    var lmb_symb = new LambdaSymbol(
      Annotate(ctx), 
      func_name,
      ParseFuncSignature(funcLambda.coroFlag() != null, tp, funcLambda.funcParams()),
      upvals,
      this.func_decl_stack
    );

    var ast = new AST_LambdaDecl(lmb_symb, upvals, funcLambda.Stop.Line);

    var scope_backup = curr_scope;
    PushScope(lmb_symb);

    //NOTE: lambdas are not defined (persisted) in any scope, however as a symbol resolve 
    //      fallback we set the scope to the one it's actually defined in during body parsing 
    lmb_symb.scope = scope_backup;

    var fparams = funcLambda.funcParams();
    if(fparams != null)
    {
      PushAST(ast.fparams());
      Visit(fparams);
      PopAST();
    }

    //NOTE: while we are inside lambda the eval type is its return type
    Annotate(ctx).eval_type = lmb_symb.GetReturnType();

    ParseFuncBlock(funcLambda, funcLambda.funcBlock(), funcLambda.retType(), ast);

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

  bool CommonVisitLambdaCall(
    ParserRuleContext ctx, 
    bhlParser.LambdaCallContext call, 
    ExpChainItems chain_items,
    ref IType curr_type,
    bool write,
    bool yielded
  )
  {
    var ast = CommonVisitLambda(
      ctx, 
      call.funcLambda(), 
      ref curr_type,
      yielded
    );

    var interim = new AST_Interim();
    interim.AddChild(ast);
    PushAST(interim);

    var scope = curr_scope;
    ITerminalNode curr_name = null;

    if(!CommonVisitChainItems(
      chain_items,
      0,
      ref curr_name,
      ref scope, 
      ref curr_type,
      write
    ))
    {
      PopAST();
      return false;
    }

    ValidateChainCall(
      ctx, 
      chain_items, 
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
    Visit(exp);
    Annotate(ctx).eval_type = Annotate(exp).eval_type;
    return null;
  }

  public override object VisitExpJsonObj(bhlParser.ExpJsonObjContext ctx)
  {
    var json = ctx.jsonObject();
    Visit(json);
    Annotate(ctx).eval_type = Annotate(json).eval_type;
    return null;
  }

  public override object VisitExpJsonArr(bhlParser.ExpJsonArrContext ctx)
  {
    var json = ctx.jsonArray();
    Visit(json);
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
      AddSemanticError(ctx, "can't determine type of {..} expression");
      return null;
    }

    if(!(curr_type is ClassSymbol) || 
        (curr_type is ArrayTypeSymbol) ||
        (curr_type is MapTypeSymbol)
        )
    {
      AddSemanticError(ctx, "type '" + curr_type + "' can't be specified with {..}");
      return null;
    }

    if(curr_type is ClassSymbolNative csn && csn.creator == null)
    {
      AddSemanticError(ctx, "constructor is not defined");
      return null;
    }

    Annotate(ctx).eval_type = curr_type;

    var ast = new AST_JsonObj(curr_type, ctx.Start.Line);

    PushAST(ast);
    var pairs = ctx.jsonPair();
    for(int i=0;i<pairs.Length;++i)
    {
      var pair = pairs[i]; 
      Visit(pair);
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
      AddSemanticError(ctx, "can't determine type of [..] expression");
      return null;
    }

    if(!(curr_type is ArrayTypeSymbol) && !(curr_type is MapTypeSymbol))
    {
      AddSemanticError(ctx, "type '" + curr_type + "' can't be specified with [..]");
      return null;
    }

    if(curr_type is ArrayTypeSymbol arr_type)
    {
      var orig_type = arr_type.item_type.Get();
      if(orig_type == null)
      {
        AddSemanticError(ctx,  "type '" + arr_type.item_type.path + "' not found");
        return null;
      }
      PushJsonType(orig_type);

      var ast = new AST_JsonArr(arr_type, ctx.Start.Line);

      PushAST(ast);
      var vals = ctx.jsonValue();
      for(int i=0;i<vals.Length;++i)
      {
        Visit(vals[i]);
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
        AddSemanticError(ctx,  "type '" + map_type.key_type.path + "' not found");
        return null;
      }
      var val_type = map_type.val_type.Get();
      if(val_type == null)
      {
        AddSemanticError(ctx,  "type '" + map_type.val_type.path + "' not found");
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
          AddSemanticError(val == null ? (IParseTree)ctx : (IParseTree)val,  "[k, v] expected");
          continue;
        }

        PushJsonType(key_type);
        Visit(val.jsonArray().jsonValue()[0]);
        PopJsonType();

        PushJsonType(val_type);
        Visit(val.jsonArray().jsonValue()[1]);
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
    var curr_type = PeekJsonType();
    var scoped_symb = curr_type as ClassSymbol;
    if(scoped_symb == null)
    {
      AddSemanticError(ctx, "expecting class type, got '" + curr_type + "' instead");
      return null;
    }

    var name_str = ctx.NAME().GetText();
    
    var member = scoped_symb.ResolveWithFallback(name_str) as VariableSymbol;
    if(member == null)
    {
      AddSemanticError(ctx, "no such attribute '" + name_str + "' in class '" + scoped_symb.name + "'");
      return null;
    }

    Annotate(ctx).lsp_symbol = member;

    var ast = new AST_JsonPair(curr_type, name_str, member.scope_idx);

    PushJsonType(member.type.Get());

    var jval = ctx.jsonValue(); 
    PushAST(ast);
    Visit(jval);
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
    Visit(exp);
    Annotate(ctx).eval_type = Annotate(exp).eval_type;

    types.CheckAssign(curr_type, Annotate(exp), errors);

    return null;
  }

  public override object VisitExpTypeof(bhlParser.ExpTypeofContext ctx)
  {
    var tp = ParseType(ctx.type());

    Annotate(ctx).eval_type = Types.ClassType;

    PeekAST().AddChild(new AST_Typeof(tp.Get()));

    return null;
  }

  public override object VisitExpChain(bhlParser.ExpChainContext ctx)
  {
    IType curr_type = null;
    CommonProcExpChain(ctx, ctx.chain(), ref curr_type);

    ++ref_compatible_exp_counter;

    Annotate(ctx).eval_type = curr_type;

    return null;
  }
  
  public override object VisitExpLambda(bhlParser.ExpLambdaContext ctx)
  {
    IType curr_type = null;
    var ast = CommonVisitLambda(
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
    var exp_type = CommonVisitFuncCallExp(ctx, ctx.funcCallExp(), yielded: true);
    Annotate(ctx).eval_type = exp_type;
    return null;
  }

  void CheckCoroCallValidity(ParserRuleContext ctx)
  {
    var curr_func = PeekFuncDecl();
    if(!curr_func.attribs.HasFlag(FuncAttrib.Coro))
    {
      AddSemanticError(curr_func.parsed.tree, "function with yield calls must be coro");
      return;
    }

    if(GetBlockLevel(BlockType.DEFER) != -1)
    {
      AddSemanticError(ctx, "yield is not allowed in defer block");
      return;
    }

    has_yield_calls.Add(curr_func);
  }

  public override object VisitExpNew(bhlParser.ExpNewContext ctx)
  {
    var tp = ParseType(ctx.newExp().type());
    var cl = tp.Get();
    Annotate(ctx).eval_type = cl;

    if(cl is ClassSymbolNative csn && csn.creator == null)
    {
      AddSemanticError(ctx, "constructor is not defined");
      return null;
    }

    if(ctx.newExp().type().nsName() != null)
      Annotate(ctx.newExp().type().nsName().dotName()).lsp_symbol = cl as Symbol;

    var ast = new AST_New(cl, ctx.Start.Line);
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitAssignExp(bhlParser.AssignExpContext ctx)
  {
    var exp = ctx.exp();

    //TODO: use more generic protection against parse errors
    if(exp == null)
      return false;

    Visit(exp);
    Annotate(ctx).eval_type = Annotate(exp).eval_type;

    return null;
  }

  public override object VisitExpTypeCast(bhlParser.ExpTypeCastContext ctx)
  {
    var tp = ParseType(ctx.type());

    var ast = new AST_TypeCast(tp.Get(), false/*don't force type*/, ctx.Start.Line);
    var exp = ctx.exp();
    PushAST(ast);
    Visit(exp);
    PopAST();

    Annotate(ctx).eval_type = tp.Get();

    Types.CheckCast(Annotate(ctx), Annotate(exp), errors); 

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpAs(bhlParser.ExpAsContext ctx)
  {
    var tp = ParseType(ctx.type());

    var ast = new AST_TypeAs(tp.Get(), ctx.Start.Line);
    var exp = ctx.exp();
    PushAST(ast);
    Visit(exp);
    PopAST();

    Annotate(ctx).eval_type = tp.Get();

    //TODO: do we need to pre-check absolutely unrelated types?
    //types.CheckCast(Annotate(ctx), Annotate(exp)); 

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpIs(bhlParser.ExpIsContext ctx)
  {
    var tp = ParseType(ctx.type());

    var ast = new AST_TypeIs(tp.Get(), ctx.Start.Line);
    var exp = ctx.exp();
    PushAST(ast);
    Visit(exp);
    PopAST();

    Annotate(ctx).eval_type = Types.Bool;

    //TODO: do we need to pre-check absolutely unrelated types?
    //types.CheckCast(Annotate(ctx), Annotate(exp)); 

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpUnary(bhlParser.ExpUnaryContext ctx)
  {
    EnumUnaryOp type;
    var op = ctx.operatorUnary().GetText(); 
    if(op == "-")
      type = EnumUnaryOp.NEG;
    else if(op == "!")
      type = EnumUnaryOp.NOT;
    else
      throw new Exception("Unknown type");

    var ast = new AST_UnaryOpExp(type);
    var exp = ctx.exp(); 
    PushAST(ast);
    Visit(exp);
    PopAST();

    Annotate(ctx).eval_type = type == EnumUnaryOp.NEG ? 
      types.CheckUnaryMinus(Annotate(exp), errors) : 
      types.CheckLogicalNot(Annotate(exp), errors);

    PeekAST().AddChild(ast);

    return null;
  }

  bool CommonVisitVarPostOp(bhlParser.VarPostOpContext ctx)
  {
    if(ctx.operatorPostOpAssign() != null)
    {
      string post_op = ctx.operatorPostOpAssign().GetText();
      CommonVisitBinOp(ctx, post_op.Substring(0, 1), ctx.varAccessExp(), ctx.exp());

      var chain = new ExpChain(ctx.varAccessExp());
      IType curr_type = null;
      if(!CommonProcExpChain(chain, ref curr_type, write: true))
        return false;

      //NOTE: strings concat special case
      if(curr_type == Types.String && post_op == "+=")
        return true;

      if(!Types.IsNumeric(curr_type))
      {
        AddSemanticError(ctx, "is not numeric type");
        return false;
      }

      return true;
    }
    else if(ctx.varPostIncDec() != null) 
      return CommonVisitPostIncDec(ctx.varPostIncDec());
    else if(ctx.assignExp() != null) 
    {
      var vproxy = new VarsDeclsProxy(new bhlParser.VarAccessExpContext[] { ctx.varAccessExp() });
      return CommonDeclOrAssign(vproxy, ctx.assignExp(), ctx.Start.Line);
    }
    
    return true;
  }

  public override object VisitExpAddSub(bhlParser.ExpAddSubContext ctx)
  {
    var op = ctx.operatorAddSub().GetText(); 

    CommonVisitBinOp(ctx, op, ctx.exp(0), ctx.exp(1));

    return null;
  }

  public override object VisitExpMulDivMod(bhlParser.ExpMulDivModContext ctx)
  {
    var op = ctx.operatorMulDivMod().GetText(); 

    CommonVisitBinOp(ctx, op, ctx.exp(0), ctx.exp(1));

    return null;
  }
  
  public override object VisitStmVarPostOp(bhlParser.StmVarPostOpContext ctx)
  {
    CommonVisitVarPostOp(ctx.varPostOp());
    return null;
  }

  bhlParser.ExpContext _one_literal_exp;
  bhlParser.ExpContext one_literal_exp {
    get {
      //TODO: make expression AST on the fly somehow?
      //var exp = new ExpContext(); 
      //exp.Add(new ExpLiteralNumContext("1"));
      if(_one_literal_exp == null)
      {
        var tmp_parser = Stream2Parser(
          "", 
          new MemoryStream(System.Text.Encoding.UTF8.GetBytes("1")), 
          ErrorHandlers.MakeCommon("", new CompileErrors())
        );
        _one_literal_exp = tmp_parser.exp();
      }
      return _one_literal_exp;
    }
  }

  bool CommonVisitPostIncDec(bhlParser.VarPostIncDecContext ctx)
  {
    //let's tweak the fake "1" expression placement
    //by assinging it the call expression placement
    var ann_one = Annotate(one_literal_exp);
    var ann_call = Annotate(ctx.varAccessExp());
    ann_one.module = ann_call.module;
    ann_one.tree = ann_call.tree;
    ann_one.tokens = ann_call.tokens;

    if(ctx.INC() != null)
      CommonVisitBinOp(ctx, "+", ctx.varAccessExp(), one_literal_exp);
    else if(ctx.INC() != null)
      CommonVisitBinOp(ctx, "-", ctx.varAccessExp(), one_literal_exp);
    else
    {
      AddSemanticError(ctx, "unknown operator");
      return false;
    }

    var chain = new ExpChain(ctx.varAccessExp());

    IType curr_type = null;
    if(!CommonProcExpChain(chain, ref curr_type, write: true))
      return false;

    if(!Types.IsNumeric(curr_type))
    {
      AddSemanticError(ctx, "only numeric types supported");
      return false;
    }
    return true;
  }
  
  public override object VisitExpCompare(bhlParser.ExpCompareContext ctx)
  {
    var op = ctx.operatorComparison().GetText(); 

    CommonVisitBinOp(ctx, op, ctx.exp(0), ctx.exp(1));

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

  void CommonVisitBinOp(ParserRuleContext ctx, string op, IParseTree lhs, IParseTree rhs)
  {
    EnumBinaryOp op_type = GetBinaryOpType(op);
    AST_Tree ast = new AST_BinaryOpExp(op_type, ctx.Start.Line);
    PushAST(ast);
    Visit(lhs);
    Visit(rhs);
    PopAST();

    var ann_lhs = Annotate(lhs);
    var ann_rhs = Annotate(rhs);

    var class_symb = ann_lhs.eval_type as ClassSymbol;
    //NOTE: checking if there's binary operator overload
    if(class_symb != null && class_symb.Resolve(op) is FuncSymbol)
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
      Annotate(ctx).eval_type = types.CheckBinOp(ann_lhs, ann_rhs, errors);

    PeekAST().AddChild(ast);
  }

  public override object VisitExpBitAnd(bhlParser.ExpBitAndContext ctx)
  {
    var ast = new AST_BinaryOpExp(EnumBinaryOp.BIT_AND, ctx.Start.Line);
    var exp_0 = ctx.exp(0);
    var exp_1 = ctx.exp(1);

    PushAST(ast);
    Visit(exp_0);
    Visit(exp_1);
    PopAST();

    Annotate(ctx).eval_type = types.CheckBitOp(Annotate(exp_0), Annotate(exp_1), errors);

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpBitOr(bhlParser.ExpBitOrContext ctx)
  {
    var ast = new AST_BinaryOpExp(EnumBinaryOp.BIT_OR, ctx.Start.Line);
    var exp_0 = ctx.exp(0);
    var exp_1 = ctx.exp(1);

    PushAST(ast);
    Visit(exp_0);
    Visit(exp_1);
    PopAST();

    Annotate(ctx).eval_type = types.CheckBitOp(Annotate(exp_0), Annotate(exp_1), errors);

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpAnd(bhlParser.ExpAndContext ctx)
  {
    var ast = new AST_BinaryOpExp(EnumBinaryOp.AND, ctx.Start.Line);
    var exp_0 = ctx.exp(0);
    var exp_1 = ctx.exp(1);

    //AND node has exactly two children
    var tmp0 = new AST_Interim();
    PushAST(tmp0);
    Visit(exp_0);
    PopAST();
    ast.AddChild(tmp0);

    var tmp1 = new AST_Interim();
    PushAST(tmp1);
    Visit(exp_1);
    PopAST();
    ast.AddChild(tmp1);

    Annotate(ctx).eval_type = types.CheckLogicalOp(Annotate(exp_0), Annotate(exp_1), errors);

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpOr(bhlParser.ExpOrContext ctx)
  {
    var ast = new AST_BinaryOpExp(EnumBinaryOp.OR, ctx.Start.Line);
    var exp_0 = ctx.exp(0);
    var exp_1 = ctx.exp(1);

    //OR node has exactly two children
    var tmp0 = new AST_Interim();
    PushAST(tmp0);
    Visit(exp_0);
    PopAST();
    ast.AddChild(tmp0);

    var tmp1 = new AST_Interim();
    PushAST(tmp1);
    Visit(exp_1);
    PopAST();
    ast.AddChild(tmp1);

    Annotate(ctx).eval_type = types.CheckLogicalOp(Annotate(exp_0), Annotate(exp_1), errors);

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralNum(bhlParser.ExpLiteralNumContext ctx)
  {
    AST_Literal ast = null;

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
      AddSemanticError(ctx, "unknown numeric literal type");
      return null;
    }

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralFalse(bhlParser.ExpLiteralFalseContext ctx)
  {
    Annotate(ctx).eval_type = Types.Bool;

    var ast = new AST_Literal(ConstType.BOOL);
    ast.nval = 0;
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralNull(bhlParser.ExpLiteralNullContext ctx)
  {
    Annotate(ctx).eval_type = Types.Null;

    var ast = new AST_Literal(ConstType.NIL);
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralTrue(bhlParser.ExpLiteralTrueContext ctx)
  {
    Annotate(ctx).eval_type = Types.Bool;

    var ast = new AST_Literal(ConstType.BOOL);
    ast.nval = 1;
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralStr(bhlParser.ExpLiteralStrContext ctx)
  {
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

  public override object VisitStmReturn(bhlParser.StmReturnContext ctx)
  {
    var ret_val = ctx.returnVal();

    //NOTE: special handling of the following case:
    //
    //      return
    //      string str
    //
    if(ret_val?.varDeclare() != null)
    {
      var vd = ret_val.varDeclare();
      VariableSymbol vd_symb;
      PeekAST().AddChild(
        CommonDeclVar(
          curr_scope, 
          vd.NAME(), 
          vd.type(), 
          is_ref: false, 
          func_arg: false, 
          write: false,
          symb: out vd_symb
        )
      );
      return null;
    }

    //NOTE: special handling of the following case:
    //
    //      return
    //      int foo = 1
    //
    if(ret_val?.varDeclareAssign() != null)
    {
      var vd = ret_val.varDeclareAssign().varDeclare();
      VariableSymbol vd_symb;
      var vd_ast = PeekAST();
      int vd_assign_idx = vd_ast.children.Count;
      vd_ast.AddChild(
        CommonDeclVar(
          curr_scope, 
          vd.NAME(), 
          vd.type(), 
          is_ref: false, 
          func_arg: false, 
          write: false,
          symb: out vd_symb
        )
      );
      CommonAssignToVar(
        vd_ast, 
        vd_assign_idx,
        Annotate(vd.NAME()),
        is_decl: true, 
        var_symb: vd_symb,
        var_idx: 0,
        vars_num: 1, 
        assign_exp: ret_val.varDeclareAssign().assignExp()
      );
      return null;
    }

    if(CountBlocks(BlockType.DEFER) > 0)
      //we can proceed
      AddSemanticError(ctx, "return is not allowed in defer block");

    var func_symb = PeekFuncDecl();
    if(func_symb == null)
    {
      AddSemanticError(ctx, "return statement is not in function");
      return null;
    }
    
    return_found.Add(func_symb);

    var ret_ast = new AST_Return(ctx.Start.Line);
    
    if(ret_val != null)
    {
      int explen = ret_val.exps().exp().Length;

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
        var exp_item = ret_val.exps().exp()[0];
        PushJsonType(fret_type);
        Visit(exp_item);
        PopJsonType();

        if(Annotate(exp_item).eval_type != Types.Void)
          ret_ast.num = fmret_type != null ? fmret_type.Count : 1;

        if(!types.CheckAssign(func_symb.parsed, Annotate(exp_item), errors))
          return null;
        Annotate(ctx).eval_type = Annotate(exp_item).eval_type;
      }
      else
      {
        if(fmret_type == null)
        {
          AddSemanticError(ctx, "function doesn't support multi return");
          return null;
        }

        if(fmret_type.Count != explen)
        {
          AddSemanticError(ctx, "multi return size doesn't match destination");
          return null;
        }

        var ret_type = new TupleType();

        //NOTE: we traverse expressions in reversed order so that returned
        //      values are properly placed on a stack
        for(int i=explen;i-- > 0;)
        {
          var exp = ret_val.exps().exp()[i];
          Visit(exp);
          var exp_eval_type = Annotate(exp).eval_type;
          if(exp_eval_type == null)
            return null;
          ret_type.Add(curr_scope.R().T(exp_eval_type));
        }

        //type checking is in proper order
        for(int i=0;i<explen;++i)
        {
          var exp = ret_val.exps().exp()[i];
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
        AddSemanticError(ctx, "return value is missing");
        return null;
      }
      Annotate(ctx).eval_type = Types.Void;
      PeekAST().AddChild(ret_ast);
    }

    return null;
  }

  public override object VisitStmBreak(bhlParser.StmBreakContext ctx)
  {
    int loop_level = GetLoopBlockLevel();

    if(loop_level == -1)
    {
      AddSemanticError(ctx, "not within loop construct");
      return null;
    }

    if(GetBlockLevel(BlockType.DEFER) > loop_level)
    {
      AddSemanticError(ctx, "not within loop construct");
      return null;
    }

    PeekAST().AddChild(new AST_Break());

    return null;
  }

  public override object VisitStmContinue(bhlParser.StmContinueContext ctx)
  {
    int loop_level = GetLoopBlockLevel();

    if(loop_level == -1)
    {
      AddSemanticError(ctx, "not within loop construct");
      return null;
    }

    if(GetBlockLevel(BlockType.DEFER) > loop_level)
    {
      AddSemanticError(ctx, "not within loop construct");
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
      Visit(nsdecl);
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

    string name = pass.func_ctx.NAME().GetText();

    if(pass.func_ctx.funcAttribs().Length > 0 && pass.func_ctx.funcAttribs()[0].coroFlag() == null)
      //we can proceed
      AddSemanticError(pass.func_ctx.funcAttribs()[0], "improper usage of attribute");

    pass.func_symb = new FuncSymbolScript(
      Annotate(pass.func_ctx), 
      new FuncSignature(),
      name
    ); 

    pass.scope.Define(pass.func_symb);

    pass.func_ast = new AST_FuncDecl(pass.func_symb, pass.func_ctx.Stop.Line);
    pass.ast.AddChild(pass.func_ast);
  }

  void Pass_ParseFuncSignature(ParserPass pass)
  {
    if(pass.func_ctx == null)
      return;

    pass.func_symb.signature = ParseFuncSignature(
      pass.func_ctx.funcAttribs().Length > 0 && pass.func_ctx.funcAttribs()[0].coroFlag() != null, 
      ParseType(pass.func_ctx.retType()), 
      pass.func_ctx.funcParams()
    );

    ParseFuncParams(pass.func_ctx, pass.func_ast);

    Annotate(pass.func_ctx).eval_type = pass.func_symb.GetReturnType();
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
    if(pass.func_ctx == null)
      return;

    PushScope(pass.func_ast.symbol);
    ParseFuncBlock(pass.func_ctx, pass.func_ctx.funcBlock(), pass.func_ctx.retType(), pass.func_ast);
    PopScope();
  }

  void ParseFuncBlock(ParserRuleContext ctx, bhlParser.FuncBlockContext block_ctx, bhlParser.RetTypeContext ret_ctx, AST_FuncDecl func_ast)
  {
    PushAST(func_ast.block());
    Visit(block_ctx);
    PopAST();

    if(func_ast.symbol.GetReturnType() != Types.Void && !return_found.Contains(func_ast.symbol))
      AddSemanticError(ret_ctx, "matching 'return' statement not found");

    if(func_ast.symbol.attribs.HasFlag(FuncAttrib.Coro) && !has_yield_calls.Contains(func_ast.symbol))
      AddSemanticError(ctx, "coro functions without yield calls not allowed");
  }

  void Pass_OutlineGlobalVar(ParserPass pass)
  {
    if(pass.gvar_ctx == null)
      return;

    var vd = pass.gvar_ctx.varDeclare(); 

    pass.gvar_symb = new VariableSymbol(Annotate(vd.NAME()), vd.NAME().GetText(), new Proxy<IType>());

    curr_scope.Define(pass.gvar_symb);
  }

  void Pass_OutlineInterfaceDecl(ParserPass pass)
  {
    if(pass.iface_ctx == null)
      return;

    var name = pass.iface_ctx.NAME().GetText();

    pass.iface_symb = new InterfaceSymbolScript(Annotate(pass.iface_ctx), name);

    pass.scope.Define(pass.iface_symb);
  }

  void Pass_ParseInterfaceMethods(ParserPass pass)
  {
    if(pass.iface_ctx == null)
      return;

    for(int i=0;i<pass.iface_ctx.interfaceBlock().interfaceMembers()?.interfaceMember().Length;++i)
    {
      var ib = pass.iface_ctx.interfaceBlock().interfaceMembers().interfaceMember()[i];

      var fd = ib.interfaceFuncDecl();
      if(fd != null)
      {
        int default_args_num;
        var sig = ParseFuncSignature(fd.coroFlag() != null, ParseType(fd.retType()), fd.funcParams(), out default_args_num);
        if(default_args_num != 0)
        {
          AddSemanticError(fd.funcParams().funcParamDeclare()[sig.arg_types.Count - default_args_num], "default argument value is not allowed in this context");
          return;
        }

        var func_symb = new FuncSymbolScript(
          null, 
          sig, 
          fd.NAME().GetText()
        );
        pass.iface_symb.Define(func_symb);

        var func_params = fd.funcParams();
        if(func_params != null)
        {
          PushScope(func_symb);
          //NOTE: we push some dummy interim AST and later
          //      simply discard it since we don't care about
          //      func args related AST for interfaces
          PushAST(new AST_Interim());
          Visit(func_params);
          PopAST();
          PopScope();
        }
      }
    }
  }

  void Pass_AddInterfaceExtensions(ParserPass pass)
  {
    if(pass.iface_ctx == null)
      return;

    if(pass.iface_ctx.extensions() != null)
    {
      var inherits = new List<InterfaceSymbol>();
      for(int i=0;i<pass.iface_ctx.extensions().nsName().Length;++i)
      {
        var ext_name = pass.iface_ctx.extensions().nsName()[i]; 
        string ext_full_path = curr_scope.GetFullPath(ext_name.GetText());
        var ext = ns.ResolveSymbolByPath(ext_full_path);
        if(ext is InterfaceSymbol ifs)
        {
          if(ext == pass.iface_symb)
          {
            AddSemanticError(ext_name, "self inheritance is not allowed");
            return;
          }

          if(inherits.IndexOf(ifs) != -1)
          {
            AddSemanticError(ext_name, "interface is inherited already");
            return;
          }

          inherits.Add(ifs);
        }
        else
        {
          AddSemanticError(ext_name, "not a valid interface");
          return;
        }
      }
      if(inherits.Count > 0)
        pass.iface_symb.SetInherits(inherits);
    }
  }

  public override object VisitNsDecl(bhlParser.NsDeclContext ctx)
  {
    string name = ctx.dotName().NAME().GetText();

    int n = 0;
    do 
    {
      var ns = curr_scope.Resolve(name) as Namespace;
      if(ns == null)
      {
        ns = new Namespace(types.nfunc_index, name, module.name, module.gvars);
        curr_scope.Define(ns);
      }
      else if(ns.module_name != module.name)
        throw new Exception("Unexpected namespace's module name: " + ns.module_name);

      PushScope(ns);

      if(n >= ctx.dotName().memberAccess().Length)
        break;

      name = ctx.dotName().memberAccess()[n].NAME().GetText();

      ++n;

    } while(true);

    VisitDecls(ctx.decls());

    for(int i = 0; i <= n; ++i) 
      PopScope();

    return null;
  }

  void Pass_OutlineClassDecl(ParserPass pass)
  {
    if(pass.class_ctx == null)
      return;

    var name = pass.class_ctx.NAME().GetText();

    pass.class_symb = new ClassSymbolScript(Annotate(pass.class_ctx), name);
    pass.scope.Define(pass.class_symb);

    pass.class_ast = new AST_ClassDecl(pass.class_symb);

    //class members
    for(int i=0;i<pass.class_ctx.classBlock().classMembers()?.classMember().Length;++i)
    {
      var cm = pass.class_ctx.classBlock().classMembers().classMember()[i];
      var fldd = cm.fldDeclare();
      if(fldd != null)
      {
        var vd = fldd.varDeclare();

        if(vd.NAME().GetText() == "this")
        {
          AddSemanticError(vd.NAME(), "the keyword 'this' is reserved");
          return;
        }

        var fld_symb = new FieldSymbolScript(Annotate(vd), vd.NAME().GetText(), new Proxy<IType>());

        for(int f=0;f<fldd.fldAttribs().Length;++f)
        {
          var attr = fldd.fldAttribs()[f];
          var attr_type = FieldAttrib.None;

          if(attr.staticFlag() != null)
            attr_type = FieldAttrib.Static;

          if(fld_symb.attribs.HasFlag(attr_type))
            AddSemanticError(attr, "this attribute is set already");

          fld_symb.attribs |= attr_type;
        }

        pass.class_symb.Define(fld_symb);
      }

      var fd = cm.funcDecl();
      if(fd != null)
      {
        if(fd.NAME().GetText() == "this")
        {
          AddSemanticError(fd.NAME(), "the keyword 'this' is reserved");
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

          if(attr.coroFlag() != null)
            attr_type = FuncAttrib.Coro;
          else if(attr.virtualFlag() != null)
            attr_type = FuncAttrib.Virtual;
          else if(attr.overrideFlag() != null)
            attr_type = FuncAttrib.Override;
          else if(attr.staticFlag() != null)
            attr_type = FuncAttrib.Static;

          if(func_symb.attribs.HasFlag(attr_type))
            AddSemanticError(attr, "this attribute is set already");

          func_symb.attribs |= attr_type;
        }

        if(!func_symb.attribs.HasFlag(FuncAttrib.Static))
          func_symb.ReserveThisArgument(pass.class_symb);

        pass.class_symb.Define(func_symb);

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
    if(pass.class_ctx == null)
      return;

    PushScope(pass.class_symb);
    //NOTE: we want to prevent resolving of attributes and methods at this point 
    //      since they might collide with types. For example:
    //      class Foo {
    //        a.A a <-- here attribute 'a' will prevent proper resolving of 'a.A' type  
    //      }
    pass.class_symb._resolve_only_decl_members = true;

    //class members
    for(int i=0;i<pass.class_ctx.classBlock().classMembers()?.classMember().Length;++i)
    {
      var cm = pass.class_ctx.classBlock().classMembers().classMember()[i];
      var fldd = cm.fldDeclare();
      if(fldd != null)
      {
        var vd = fldd.varDeclare();
        var fld_symb = (FieldSymbolScript)pass.class_symb.members.Find(vd.NAME().GetText());
        if(fld_symb == null)
          break;
        fld_symb.type = ParseType(vd.type());
      }

      var fd = cm.funcDecl();
      if(fd != null)
      {
        var func_symb = (FuncSymbolScript)pass.class_symb.members.Find(fd.NAME().GetText());
        if(func_symb == null)
          break;

        func_symb.signature = ParseFuncSignature(
          fd.funcAttribs().Length > 0 && fd.funcAttribs()[0].coroFlag() != null, 
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
    if(pass.class_ctx == null)
      return;

    if(pass.class_ctx.extensions() != null)
    {
      var implements = new List<InterfaceSymbol>();
      ClassSymbol super_class = null;

      for(int i=0;i<pass.class_ctx.extensions().nsName().Length;++i)
      {
        var ext_name = pass.class_ctx.extensions().nsName()[i]; 
        string ext_full_path = curr_scope.GetFullPath(ext_name.GetText());
        var ext = curr_scope.ResolveSymbolByPath(ext_full_path);
        if(ext is ClassSymbol cs)
        {
          if(ext == pass.class_symb)
          {
            AddSemanticError(ext_name, "self inheritance is not allowed");
            return;
          }

          if(super_class != null)
          {
            AddSemanticError(ext_name, "only one parent class is allowed");
            return;
          }

          if(cs is ClassSymbolNative)
          {
            AddSemanticError(ext_name, "extending native classes is not supported");
            return;
          }

          super_class = cs;
        }
        else if(ext is InterfaceSymbol ifs)
        {
          if(implements.IndexOf(ifs) != -1)
          {
            AddSemanticError(ext_name, "interface is implemented already");
            return;
          }

          if(ifs is InterfaceSymbolNative)
          {
            AddSemanticError(ext_name, "implementing native interfaces is not supported");
            return;
          }

          implements.Add(ifs);
        }
        else
        {
          AddSemanticError(ext_name, "not a class or an interface");
          return;
        }
      }

      pass.class_symb.SetSuperAndInterfaces(super_class, implements);
    }
  }

  void Pass_SetupClass(ParserPass pass)
  {
    if(pass.class_ctx == null)
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
    if(pass.class_ctx == null)
      return;

    //class methods bodies
    for(int i=0;i<pass.class_ctx.classBlock().classMembers()?.classMember().Length;++i)
    {
      var cm = pass.class_ctx.classBlock().classMembers().classMember()[i];
      var fd = cm.funcDecl();
      if(fd != null)
      {
        var func_symb = (FuncSymbol)pass.class_symb.Resolve(fd.NAME().GetText());
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
    var enum_name = ctx.NAME().GetText();

    //NOTE: currently all enum values are replaced with literals,
    //      so that it doesn't really make sense to create AST for them.
    //      But we do it just for consistency. Later once we have runtime 
    //      type info this will be justified.
    var symb = new EnumSymbolScript(Annotate(ctx), enum_name);
    curr_scope.Define(symb);

    for(int i=0;i<ctx.enumBlock().enumMember().Length;++i)
    {
      var em = ctx.enumBlock().enumMember()[i];
      var em_name = em.NAME().GetText();
      int em_val = int.Parse(em.INT().GetText(), System.Globalization.CultureInfo.InvariantCulture);

      int res = symb.TryAddItem(Annotate(em), em_name, em_val);
      if(res == 1)
      {
        AddSemanticError(em.NAME(), "duplicate key '" + em_name + "'");
        return null;
      }
      else if(res == 2)
      {
        AddSemanticError(em.INT(), "duplicate value '" + em_val + "'");
        return null;
      }
    }

    return null;
  }

  void Pass_ParseGlobalVar(ParserPass pass)
  {
    if(pass.gvar_ctx == null)
      return;

    var vd = pass.gvar_ctx.varDeclare(); 

    //NOTE: we want to temprarily 'disable' the symbol so that it doesn't
    //      interfere with type lookups and invalid self assignments
    var subst_symbol = DisableVar(((Namespace)curr_scope).members, pass.gvar_symb);

    pass.gvar_symb.type = ParseType(vd.type());
    pass.gvar_symb.parsed.eval_type = pass.gvar_symb.type.Get();

    Annotate(vd.type().nsName().dotName()).lsp_symbol = pass.gvar_symb.parsed.eval_type as Symbol;

    PushAST((AST_Tree)pass.ast);

    var assign_exp = pass.gvar_ctx.assignExp();

    AST_Interim exp_ast = null;
    if(assign_exp != null)
    {
      var tp = ParseType(vd.type());

      exp_ast = new AST_Interim();
      PushAST(exp_ast);
      PushJsonType(tp.Get());
      Visit(assign_exp);
      PopJsonType();
      PopAST();
    }

    AST_Tree ast = assign_exp != null ? 
      (AST_Tree)new AST_Call(EnumCall.VARW, vd.NAME().Symbol.Line, pass.gvar_symb) : 
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
      if(fp.assignExp() != null)
      {
        if(curr_scope is LambdaSymbol)
          AddSemanticError(fp.NAME(), "default argument values not allowed for lambdas");

        found_default_arg = true;
      }
      else if(found_default_arg && fp.VARIADIC() == null)
      {
        AddSemanticError(fp.NAME(), "missing default argument expression");
      }

      bool pop_json_type = false;
      if(found_default_arg)
      {
        var tp = ParseType(fp.type());
        PushJsonType(tp.Get());
        pop_json_type = true;
      }

      Visit(fp);

      if(pop_json_type)
        PopJsonType();
    }

    return null;
  }

  public override object VisitVarAccessExp(bhlParser.VarAccessExpContext ctx)
  {
    var chain = new ExpChain(ctx);

    IType curr_type = null;
    CommonProcExpChain(chain, ref curr_type);
    Annotate(ctx).eval_type = curr_type;

    return null;
  }

  bool CommonDeclOrAssign(bhlParser.VarOrDeclareContext vdecl, bhlParser.AssignExpContext assign_exp, int start_line)
  {
    return CommonDeclOrAssign(new VarsDeclsProxy(new bhlParser.VarOrDeclareContext[] {vdecl}), assign_exp, start_line);
  }

  class VarsDeclsProxy
  {
    bhlParser.VarDeclareContext[] vdecls;
    bhlParser.VarOrDeclareContext[] vodecls;
    bhlParser.VarAccessOrDeclareContext[] vaodecls;
    bhlParser.VarAccessExpContext[] vaccs;

    public int Count { 
      get {
        if(vdecls != null)
          return vdecls.Length;
        else if(vodecls != null)
          return vodecls.Length;
        else if(vaodecls != null)
          return vaodecls.Length;
        else if(vaccs != null)
          return vaccs.Length;
        return -1;
      }
    }

    public VarsDeclsProxy(bhlParser.VarDeclareContext[] vdecls)
    {
      this.vdecls = vdecls;
    }

    public VarsDeclsProxy(bhlParser.VarOrDeclareContext[] vodecls)
    {
      this.vodecls = vodecls;
    }

    public VarsDeclsProxy(bhlParser.VarAccessOrDeclareContext[] vaodecls)
    {
      this.vaodecls = vaodecls;
    }

    public VarsDeclsProxy(bhlParser.VarAccessExpContext[] vaccs)
    {
      this.vaccs = vaccs;
    }

    public IParseTree At(int i)
    {
      if(vdecls != null)
        return (IParseTree)vdecls[i];
      else if(vodecls != null)
        return (IParseTree)vodecls[i];
      else if(vaodecls != null)
        return (IParseTree)vaodecls[i];
      else if(vaccs != null)
        return (IParseTree)vaccs[i];

      return null;
    }

    public bhlParser.TypeContext TypeAt(int i)
    {
      if(vdecls != null)
        return vdecls[i].type();
      else if(vodecls != null)
        return vodecls[i].varDeclare()?.type();
      else if(vaodecls != null)
        return vaodecls[i].varDeclare()?.type();
      else if(vaccs != null)
        return null;

      return null;
    }

    public bhlParser.VarAccessExpContext VarAccessAt(int i)
    {
      if(vdecls != null)
        return null;
      else if(vodecls != null)
        return null;
      else if(vaodecls != null)
        return vaodecls[i].varAccessExp();
      else if(vaccs != null)
        return vaccs[i];

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
      else if(vaodecls != null)
      {
        if(vaodecls[i].varDeclare() != null)
          return vaodecls[i].varDeclare().NAME();
        else if(vaodecls[i].varAccessExp().name() != null && 
                vaodecls[i].varAccessExp().name().GLOBAL() == null)
          return vaodecls[i].varAccessExp().name().NAME();
      }
      else if(vaccs != null)
      {
        if(vaccs[i].name() != null && vaccs[i].name().GLOBAL() == null)
          return vaccs[i].name().NAME();
        return null;
      }
      
      return null;
    }
  }

  struct MultiTypeProxy
  {
    IType type;

    public int Count {
      get {
        if(type is TupleType tt)
          return tt.Count;
        return 1;
      }
    }

    public MultiTypeProxy(IType type)
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

  bool CommonDeclOrAssign(VarsDeclsProxy vdecls, bhlParser.AssignExpContext assign_exp, int start_line)
  {
    var var_ast = PeekAST();
    int var_assign_insert_idx = var_ast.children.Count;

    for(int i=0;i<vdecls.Count;++i)
    {
      VariableSymbol var_symb = null;
      AnnotatedParseTree var_ann = null;
      bool is_decl = false;

      //check if we declare a var or use an existing one
      if(vdecls.TypeAt(i) != null)
      {
        var vd_type = vdecls.TypeAt(i);
        bool is_auto_var = vd_type.GetText() == "var";

        if(is_auto_var && assign_exp == null)
        {
          AddSemanticError(vd_type, "invalid usage context");
          return false;
        }
        else if(is_auto_var && assign_exp?.GetText() == "=null")
        {
          AddSemanticError(vd_type, "invalid usage context");
          return false;
        }

        var ast = CommonDeclVar(
          curr_scope, 
          vdecls.LocalNameAt(i), 
          //NOTE: in case of 'var' let's temporarily declare var as 'any',
          //      below we'll setup the proper type
          is_auto_var ? Types.Any : ParseType(vd_type), 
          vd_type,
          is_ref: false, 
          func_arg: false, 
          write: assign_exp != null,
          symb: out var_symb
        );
        //checking if it's valid
        if(ast == null)
          return false;
        var_ast.AddChild(ast);

        is_decl = true;

        var_ann = var_symb.parsed;
        var_ann.eval_type = var_symb.type.Get();
      }
      else if(vdecls.LocalNameAt(i) != null)
      {
        var vd_name = vdecls.LocalNameAt(i);
        var_symb = curr_scope.ResolveWithFallback(vd_name.GetText()) as VariableSymbol;
        if(var_symb == null)
        {
          AddSemanticError(vd_name, "symbol '" + vd_name.GetText() + "' not resolved");
          return false;
        }

        var_ann = Annotate(vd_name);
        var_ann.eval_type = var_symb.type.Get();

        var ast = new AST_Call(EnumCall.VARW, start_line, var_symb);
        var_ast.AddChild(ast);
      }
      else if(vdecls.VarAccessAt(i) != null)
      {
        var var_exp = vdecls.VarAccessAt(i);
        if(assign_exp == null)
        {
          AddSemanticError(var_exp, "assign expression expected");
          return false;
        }

        var chain = new ExpChain(var_exp);

        IType curr_type = null;
        if(!CommonProcExpChain(chain, ref curr_type, write: true))
          return false;

        var_ann = Annotate(var_exp);
        var_ann.eval_type = curr_type;
      }

      if(assign_exp != null)
      {
        if(!CommonAssignToVar(
          var_ast, 
          var_assign_insert_idx,
          var_ann,
          is_decl,
          var_symb,
          i,
          vdecls.Count,
          assign_exp
        ))
          return false;
      }
    }
    return true;
  }

  static VariableSymbol DisableVar(SymbolsStorage members, VariableSymbol disabled_symbol)
  {
    var subst_symbol = new VariableSymbol(disabled_symbol.parsed, "#$"+disabled_symbol.name, disabled_symbol.type);
    members.Replace(disabled_symbol, subst_symbol);
    return subst_symbol;
  }

  static void EnableVar(SymbolsStorage members, VariableSymbol disabled_symbol, VariableSymbol subst_symbol)
  {
    members.Replace(subst_symbol, disabled_symbol);
  }

  public override object VisitStmDeclOptAssign(bhlParser.StmDeclOptAssignContext ctx)
  {
    var vdecls = new VarsDeclsProxy(ctx.varDeclaresOptAssign().varDeclare());
    var assign_exp = ctx.varDeclaresOptAssign().assignExp();
    CommonDeclOrAssign(vdecls, assign_exp, ctx.Start.Line);

    return null;
  }

  public override object VisitStmVarOrDeclAssign(bhlParser.StmVarOrDeclAssignContext ctx)
  {
    var vdecls = new VarsDeclsProxy(ctx.varAccessOrDeclaresAssign().varAccessOrDeclare());
    var assign_exp = ctx.varAccessOrDeclaresAssign().assignExp();
    CommonDeclOrAssign(vdecls, assign_exp, ctx.Start.Line);

    return null;
  }

  public override object VisitFuncParamDeclare(bhlParser.FuncParamDeclareContext ctx)
  {
    var name = ctx.NAME();
    var assign_exp = ctx.assignExp();
    bool is_ref = ctx.isRef() != null;
    bool is_null_ref = false;

    if(is_ref && assign_exp != null)
    {
      //NOTE: super special case for 'null refs'
      if(assign_exp.exp().GetText() == "null")
        is_null_ref = true;
      else
      {
        AddSemanticError(name, "'ref' is not allowed to have a default value");
        return null;
      }
    }

    AST_Interim exp_ast = null;
    if(assign_exp != null)
    {
      exp_ast = new AST_Interim();
      PushAST(exp_ast);
      Visit(assign_exp);
      PopAST();
    }

    VariableSymbol vd_symb;
    var decl_ast = CommonDeclVar(
      curr_scope, 
      name, 
      ctx.type(), 
      is_ref, 
      func_arg: true, 
      write: false,
      symb: out vd_symb
    );
    if(exp_ast != null)
      decl_ast.AddChild(exp_ast);
    PeekAST().AddChild(decl_ast);

    if(assign_exp != null && !is_null_ref)
      types.CheckAssign(Annotate(name), Annotate(assign_exp), errors);
    return null;
  }

  AST_Tree CommonDeclVar(
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
    return CommonDeclVar(
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

  AST_Tree CommonDeclVar(
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
    symb = null;

    if(name.GetText() == "base" && PeekFuncDecl()?.scope is ClassSymbol)
    {
      AddSemanticError(name, "keyword 'base' is reserved");
      return null;
    }

    var var_ann = Annotate(name); 
    var_ann.eval_type = tp.Get();

    if(tp_ctx?.nsName() != null)
      Annotate(tp_ctx.nsName().dotName()).lsp_symbol = var_ann.eval_type as Symbol;

    if(is_ref && !func_arg)
    {
      AddSemanticError(name, "'ref' is only allowed in function declaration");
      return null;
    }

    symb = func_arg ? 
      (VariableSymbol) new FuncArgSymbol(var_ann, name.GetText(), tp, is_ref) :
      new VariableSymbol(var_ann, name.GetText(), tp);

    curr_scope.Define(symb);

    if(write)
      return new AST_Call(EnumCall.VARW, name.Symbol.Line, symb);
    else
      return new AST_VarDecl(symb, is_ref);
  }

  bool CommonAssignToVar(
    AST_Tree ast_dest,
    int ast_insert_idx,
    AnnotatedParseTree var_ann, 
    bool is_decl,
    VariableSymbol var_symb, //can be null
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
        AddSemanticError(assign_exp, "multi assign not supported for JSON expression");
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

    var assign_ast = new AST_Interim();
    PushAST(assign_ast);
    Visit(assign_exp);
    PopAST();

    var assign_type = new MultiTypeProxy(Annotate(assign_exp).eval_type); 

    for(int s=assign_ast.children.Count;s-- > 0;)
      ast_dest.children.Insert(ast_insert_idx, assign_ast.children[s]);

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
        AddSemanticError(assign_exp, "multi return expected");
        return false;
      }

      if(assign_type.Count != vars_num)
      {
        AddSemanticError(assign_exp, "multi return size doesn't match destination");
        return false;
      }
    }
    else if(assign_type.Count > 1)
    {
      AddSemanticError(assign_exp, "multi return size doesn't match destination");
      return false;
    }

    return types.CheckAssign(var_ann, assign_type.At(var_idx), errors);
  }

  public override object VisitBlock(bhlParser.BlockContext ctx)
  {
    CommonVisitBlock(BlockType.SEQ, ctx.statement());
    return null;
  }

  public override object VisitFuncBlock(bhlParser.FuncBlockContext ctx)
  {
    CommonVisitBlock(BlockType.FUNC, ctx.block().statement());
    return null;
  }

  public override object VisitStmParal(bhlParser.StmParalContext ctx)
  {
    CommonVisitBlock(BlockType.PARAL, ctx.block().statement());
    return null;
  }

  public override object VisitStmParalAll(bhlParser.StmParalAllContext ctx)
  {
    CommonVisitBlock(BlockType.PARAL_ALL, ctx.block().statement());
    return null;
  }

  public override object VisitStmDefer(bhlParser.StmDeferContext ctx)
  {
    if(CountBlocks(BlockType.DEFER) > 0)
    {
      AddSemanticError(ctx, "nested defers are not allowed");
      return null;
    }
    CommonVisitBlock(BlockType.DEFER, ctx.block().statement());
    return null;
  }

  public override object VisitStmIf(bhlParser.StmIfContext ctx)
  {
    var ast = new AST_Block(BlockType.IF);

    var main = ctx.mainIf();

    var main_cond = new AST_Block(BlockType.SEQ);
    PushAST(main_cond);
    Visit(main.exp());
    PopAST();

    if(!types.CheckAssign(Types.Bool, Annotate(main.exp()), errors))
      return null;

    var func_symb = PeekFuncDecl();
    bool seen_return = return_found.Contains(func_symb);
    return_found.Remove(func_symb);

    ast.AddChild(main_cond);
    PushAST(ast);
    CommonVisitBlock(BlockType.SEQ, main.block().statement());
    PopAST();

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
      var item_cond = new AST_Block(BlockType.SEQ);
      PushAST(item_cond);
      Visit(item.exp());
      PopAST();

      if(!types.CheckAssign(Types.Bool, Annotate(item.exp()), errors))
        return null;

      seen_return = return_found.Contains(func_symb);
      return_found.Remove(func_symb);

      ast.AddChild(item_cond);
      PushAST(ast);
      CommonVisitBlock(BlockType.SEQ, item.block().statement());
      PopAST();

      if(!seen_return && return_found.Contains(func_symb))
        return_found.Remove(func_symb);
    }

    var @else = ctx.@else();
    if(@else != null)
    {
      seen_return = return_found.Contains(func_symb);
      return_found.Remove(func_symb);

      PushAST(ast);
      CommonVisitBlock(BlockType.SEQ, @else.block().statement());
      PopAST();

      if(!seen_return && return_found.Contains(func_symb))
        return_found.Remove(func_symb);
    }

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpTernaryIf(bhlParser.ExpTernaryIfContext ctx)
  {
    var ast = new AST_Block(BlockType.IF); //short-circuit evaluation

    var exp_0 = ctx.exp();
    var exp_1 = ctx.ternaryIfExp().exp(0);
    var exp_2 = ctx.ternaryIfExp().exp(1);

    var condition = new AST_Interim();
    PushAST(condition);
    Visit(exp_0);
    PopAST();

    if(!types.CheckAssign(Types.Bool, Annotate(exp_0), errors))
      return null;

    ast.AddChild(condition);

    var consequent = new AST_Interim();
    PushAST(consequent);
    Visit(exp_1);
    PopAST();

    ast.AddChild(consequent);

    var alternative = new AST_Interim();
    PushAST(alternative);
    Visit(exp_2);
    PopAST();

    ast.AddChild(alternative);

    var ann_exp_1 = Annotate(exp_1);
    Annotate(ctx).eval_type = ann_exp_1.eval_type;

    if(!types.CheckAssign(ann_exp_1, Annotate(exp_2), errors))
      return null;
    PeekAST().AddChild(ast);
    return null;
  }

  public override object VisitStmWhile(bhlParser.StmWhileContext ctx)
  {
    var ast = new AST_Block(BlockType.WHILE);

    PushBlock(ast);

    var cond = new AST_Block(BlockType.SEQ);
    PushAST(cond);
    Visit(ctx.exp());
    PopAST();

    if(!types.CheckAssign(Types.Bool, Annotate(ctx.exp()), errors))
      return null;

    ast.AddChild(cond);

    PushAST(ast);
    CommonVisitBlock(BlockType.SEQ, ctx.block().statement());
    PopAST();
    ast.children[ast.children.Count-1].AddChild(new AST_Continue(jump_marker: true));

    PeekAST().AddChild(ast);

    PopBlock(ast);

    return_found.Remove(PeekFuncDecl());

    return null;
  }

  public override object VisitStmDoWhile(bhlParser.StmDoWhileContext ctx)
  {
    var ast = new AST_Block(BlockType.DOWHILE);

    PushBlock(ast);

    PushAST(ast);
    CommonVisitBlock(BlockType.SEQ, ctx.block().statement());
    PopAST();
    ast.children[ast.children.Count-1].AddChild(new AST_Continue(jump_marker: true));

    var cond = new AST_Block(BlockType.SEQ);
    PushAST(cond);
    Visit(ctx.exp());
    PopAST();

    if(!types.CheckAssign(Types.Bool, Annotate(ctx.exp()), errors))
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

    var local_scope = new LocalScope(false, curr_scope);
    PushScope(local_scope);
    local_scope.Enter();
    
    var for_pre = ctx.forExp().forPreIter();
    if(for_pre != null)
      CommmonProcessForPreStatements(for_pre, ctx.Start.Line);

    var for_cond = ctx.forExp().exp();
    //TODO: use more generic protection against parse errors
    if(for_cond == null)
      return null;
    var for_post_iter = ctx.forExp().forPostIter();

    var ast = new AST_Block(BlockType.WHILE);

    PushBlock(ast);

    var cond = new AST_Block(BlockType.SEQ);
    PushAST(cond);
    Visit(for_cond);
    PopAST();

    if(!types.CheckAssign(Types.Bool, Annotate(for_cond), errors))
      return null;

    ast.AddChild(cond);

    PushAST(ast);
    var block = CommonVisitBlock(BlockType.SEQ, ctx.block().statement());
    //appending post iteration code
    if(for_post_iter != null)
    {
      PushAST(block);
      PeekAST().AddChild(new AST_Continue(jump_marker: true));
      CommmonProcessForPostStatements(for_post_iter, ctx.Start.Line);
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

  void CommmonProcessForPreStatements(bhlParser.ForPreIterContext pre, int start_line)
  {
    foreach(var vdecl in pre.varOrDeclareAssign())
    {
      CommonDeclOrAssign(vdecl.varOrDeclare(), vdecl.assignExp(), start_line);
    }
  }

  void CommmonProcessForPostStatements(bhlParser.ForPostIterContext post, int start_line)
  {
    foreach(var vp in post.varPostOp())
    {
      CommonVisitVarPostOp(vp);
    }
  }

  public override object VisitStmYield(bhlParser.StmYieldContext ctx)
  {
    CheckCoroCallValidity(ctx);

    int line = ctx.Start.Line;
    var ast = new AST_Yield(line);
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitStmYieldCall(bhlParser.StmYieldCallContext ctx)
  {
    var ret_type = CommonVisitFuncCallExp(ctx, ctx.funcCallExp(), yielded: true);
    CommonPopNonConsumed(ret_type);
    return null;
  }

  public override object VisitStmYieldWhile(bhlParser.StmYieldWhileContext ctx)
  {
    //NOTE: we're going to generate the following code
    //while(cond) { yield() }

    CheckCoroCallValidity(ctx);

    var ast = new AST_Block(BlockType.WHILE);

    PushBlock(ast);

    var cond = new AST_Block(BlockType.SEQ);
    PushAST(cond);
    Visit(ctx.exp());
    PopAST();

    if(!types.CheckAssign(Types.Bool, Annotate(ctx.exp()), errors))
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
          AddSemanticError(vod.NAME(), "symbol is not a valid variable");
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
            AddSemanticError(ctx.foreachExp().exp(), "expression is not of array type");
            goto Bail;
          }
          vd_type = predicted_arr_type.item_type;
        }
        else
          vd_type = ParseType(vd.type());

        iter_ast_decl = CommonDeclVar(
          curr_scope, 
          vd.NAME(), 
          vd_type,
          vd.type(),
          is_ref: false, 
          func_arg: false, 
          write: false,
          symb: out iter_symb
        );
        iter_type = iter_symb.type;
      }
      var arr_type = (ArrayTypeSymbol)curr_scope.R().TArr(iter_type).Get();

      PushJsonType(arr_type);
      var exp = ctx.foreachExp().exp();
      //evaluating array expression
      Visit(exp);
      PopJsonType();
      if(!types.CheckAssign(arr_type, Annotate(exp), errors))
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
      var block = CommonVisitBlock(BlockType.SEQ, ctx.block().statement());
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
          AddSemanticError(ctx.foreachExp().exp(), "expression is not of map type");
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
          AddSemanticError(vod_key.NAME(), "symbol is not a valid variable");
          goto Bail;
        }
        key_iter_type = key_iter_symb.type;
      }
      else
      {
        key_iter_ast_decl = CommonDeclVar(
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
          AddSemanticError(vod_val.NAME(), "symbol is not a valid variable");
          goto Bail;
        }
        val_iter_type = val_iter_symb.type;
      }
      else
      {
        val_iter_ast_decl = CommonDeclVar(
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
      Visit(exp);
      PopJsonType();
      if(!types.CheckAssign(map_type, Annotate(exp), errors))
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
      var block = CommonVisitBlock(BlockType.SEQ, ctx.block().statement());
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
      AddSemanticError(ctx.foreachExp(), "invalid 'foreach' syntax");

  Bail:
    local_scope.Exit();
    PopScope();

    return null;
  }

  IType PredictType(IParseTree tree)
  {
    PushAST(new AST_Interim());
    Visit(tree);
    PopAST();
    return Annotate(tree).eval_type;
  }

  AST_Block CommonVisitBlock(BlockType type, IParseTree[] sts)
  {
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
    for(int i=0;i<sts.Length;++i)
    {
      //NOTE: we need to understand if we need to wrap statements
      //      with a group 
      if(is_paral)
      {
        PushAST(tmp);

        Visit(sts[i]);

        PopAST();
        //NOTE: wrapping in group only in case there are more than one child
        if(tmp.children.Count > 1)
        {
          var seq = new AST_Block(BlockType.SEQ);
          for(int c=0;c<tmp.children.Count;++c)
            seq.AddChild(tmp.children[c]);
          ast.AddChild(seq);
        }
        else
          ast.AddChild(tmp.children[0]);
        tmp.children.Clear();
      }
      else
        Visit(sts[i]);
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
    public bhlParser.ExpContext exp_ctx;
    public bhlParser.LambdaCallContext lambda_call;
    public ExpChainItems items; 

    public bool IsGlobalNs { get { return name_ctx?.GLOBAL() != null; } }

    public ExpChain(ParserRuleContext ctx, bhlParser.ChainContext chain)
    {
      this.ctx = ctx;
      this.name_ctx = null;
      this.exp_ctx = null;
      this.lambda_call = null;
      items = new ExpChainItems();

      Init(ctx, chain);
    }

    public ExpChain(bhlParser.FuncCallExpContext ctx)
    {
      this.ctx = ctx;
      this.name_ctx = null;
      this.exp_ctx = null;
      this.lambda_call = null;
      items = new ExpChainItems();

      if(ctx.chain() != null)
      {
        Init(ctx, ctx.chain());
        items.Add(ctx.callArgs());
      }
      else
      {
        lambda_call = ctx.lambdaCall();
        items.Add(ctx.lambdaCall().callArgs());
      }
    }

    public ExpChain(bhlParser.VarAccessExpContext ctx)
    {
      this.ctx = ctx;
      this.name_ctx = null;
      this.exp_ctx = null;
      this.lambda_call = null;
      items = new ExpChainItems();

      if(ctx.chain() != null)
      {
        Init(ctx, ctx.chain());

        if(ctx.memberAccess() != null)
          items.Add(ctx.memberAccess());
        else
          items.Add(ctx.arrAccess());
      }
      else
        name_ctx = ctx.name();
    }

    void Init(ParserRuleContext ctx, bhlParser.ChainContext chain)
    {
      this.ctx = ctx;

      if(chain.namedChain() != null)
      {
        name_ctx = chain.namedChain().name();
        items = new ExpChainItems(chain.namedChain().chainExpItem());
      }
      else if(chain.parenChain() != null)
      {
        exp_ctx = chain.parenChain().exp();
        items = new ExpChainItems(chain.parenChain().chainExpItem());
      }
      else if(chain.lambdaChain() != null)
      {
        lambda_call = chain.lambdaChain().lambdaCall();
        var _items = new ExpChainItems();
        _items.Add(lambda_call.callArgs());
        _items.Add(chain.lambdaChain().chainExpItem());
        items = _items;
      }
    }
  }
}

} //namespace bhl
