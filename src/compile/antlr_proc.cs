using System;
using System.IO;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;

namespace bhl;

public class AnnotatedParseTree
{
  public IParseTree tree;
  public ModuleDeclared module;
  public ITokenStream tokens;
  public IType eval_type;
  public Symbol lsp_symbol;

  public SourceRange range
  {
    get { return new SourceRange(tree.SourceInterval, tokens); }
  }

  public string file
  {
    get { return module.file_path; }
  }
}

public partial class ANTLR_Processor : bhlParserBaseVisitor<object>
{
  public class Result
  {
    public ModuleDeclared module { get; private set; }
    public Types types { get; private set; }
    public AST_Module ast { get; private set; }
    public CompileErrors errors { get; private set; }
    public CompileWarnings warnings { get; private set; }

    public Result(ModuleDeclared module, Types types, AST_Module ast, CompileErrors errors, CompileWarnings warnings)
    {
      this.module = module;
      this.types = types;
      this.ast = ast;
      this.errors = errors;
      this.warnings = warnings;
    }
  }

  Types _types;
  public Types types => _types;

  AST_Module root_ast;
  public Result result { get; private set; }

  public ANTLR_Parsed parsed { get; private set; }

  public ModuleDeclared module { get; private set; }

  public FileImports imports_maybe { get; private set; }

  public List<ModuleDeclared> imports { get; private set; } = new List<ModuleDeclared>();

  //NOTE: passed from above
  CompileErrors errors;
  CompileWarnings warnings;

  //NOTE: non-normalized names
  Dictionary<bhlParser.MimportContext, string> raw_imports_parsed = new Dictionary<bhlParser.MimportContext, string>();

  Dictionary<ModuleDeclared, bhlParser.MimportContext> import_to_ctx = new Dictionary<ModuleDeclared, bhlParser.MimportContext>();

  //NOTE: module.ns linked with types.ns
  Namespace ns;

  ITokenStream tokens;

  public Dictionary<IParseTree, AnnotatedParseTree> annotated_nodes { get; } =
    new Dictionary<IParseTree, AnnotatedParseTree>();

  Stack<IScope> scopes = new Stack<IScope>();

  IScope curr_scope
  {
    get { return scopes.Peek(); }
  }

  HashSet<FuncSymbol> return_found = new HashSet<FuncSymbol>();

  HashSet<FuncSymbol> has_yield_calls = new HashSet<FuncSymbol>();

  Dictionary<FuncSymbol, List<AST_Block>> func2blocks = new Dictionary<FuncSymbol, List<AST_Block>>();

  //NOTE: a list is used instead of stack, so that it's easier to traverse by index
  List<FuncSymbolScript> func_decl_stack = new List<FuncSymbolScript>();

  Stack<IType> json_type_stack = new Stack<IType>();

  Stack<bool> call_by_ref_stack = new Stack<bool>();

  Stack<AST_Tree> ast_stack = new Stack<AST_Tree>();

  public ANTLR_Processor(
    ANTLR_Parsed parsed,
    ModuleDeclared module,
    FileImports imports_maybe,
    Types types,
    CompileErrors errors,
    CompileWarnings warnings = null
  )
  {
    this.parsed = parsed;
    this.tokens = parsed.tokens;

    this._types = types;
    this.module = module;
    this.imports_maybe = imports_maybe;

    this.errors = errors;
    this.warnings = warnings ?? new CompileWarnings();

    InitScope();
  }

  void InitScope()
  {
    scopes.Clear();
    ns = module.ns;
    ns.Link(_types.ns);
    PushScope(ns);
  }

  public void Reset()
  {
    module = new ModuleDeclared(module.name, module.file_path);

    root_ast = null;
    result = null;
    errors = new CompileErrors();
    warnings = new CompileWarnings();

    raw_imports_parsed.Clear();
    import_to_ctx.Clear();
    imports.Clear();
    annotated_nodes.Clear();

    return_found.Clear();
    has_yield_calls.Clear();
    func2blocks.Clear();
    func_decl_stack.Clear();
    json_type_stack.Clear();
    call_by_ref_stack.Clear();
    ast_stack.Clear();

    passes.Clear();

    semantic_tokens.Clear();
    semantic_token_idx.Clear();
    encoded_semantic_tokens.Clear();

    InitScope();
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
    {
    }
  }

  void AddWarning(IParseTree place, string msg)
  {
    warnings.Add(new ParseWarning(module, place, tokens, msg));
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
      for(int i = blocks.Count; i-- > 0;)
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
      func_decl_stack.RemoveAt(func_decl_stack.Count - 1);
    scopes.Pop();
  }

  string GetCurrentScopeNamePath()
  {
    string path = "";
    foreach(var scope in scopes)
      if (scope is INamed named)
        path += named.GetName() + '.';
    return path.TrimEnd('.');
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
      int diff_line = token.line - (prev?.line ?? 1);
      int diff_column = diff_line != 0 ? token.column : token.column - prev?.column ?? 0;

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
    annotated_nodes.TryGetValue(t, out var at);
    return at;
  }

  bool ResolveImportedModule(string raw_import, ProjectCompilationStateBundle proc_bundle, out ModuleDeclared module)
  {
    module = null;

    //let's check if it's a global native module
    var native = _types.FindRegisteredModule(raw_import);
    if(native != null)
    {
      module = native;
      return true;
    }

    var file_path = imports_maybe.MapToFilePath(raw_import);
    if(file_path == null || !File.Exists(file_path))
      return false;

    module = proc_bundle.FindModule(file_path);
    return module != null;
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
    name = name.Substring(1, name.Length - 2);

    raw_imports_parsed[ctx] = name;
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
    var ret_type_arr = new TypeIterator(ret_type);
    for(int i = 0; i < ret_type_arr.Count; ++i)
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
    if(chain.lmb_ctx != null)
      return ProcLambdaCall(chain.ctx, chain.lmb_ctx, chain.items, ref curr_type,
        write: write, yielded: yielded, called_in_place: chain.IsFuncCall);

    var walker = new ChainWalker(this, chain, curr_type, write, yielded, root_scope);
    return walker.Walk(ref curr_type);
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
          if(call.symbol is FuncSymbol fs)
            ValidateFuncCall(call, chain_ast.Count - 1 == i, fs.signature, yielded);
        }
        else if(call.type == EnumCall.FUNC_PTR_VAR || call.type == EnumCall.FUNC_PTR_MVAR)
        {
          if(call.symbol is VariableSymbol vs)
            ValidateFuncCall(call, chain_ast.Count - 1 == i, vs.type.Get() as FuncSignature, yielded);
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



  bool AddArrIndex(bhlParser.ArrAccessContext arracc, ref IType type, int line, bool write)
  {
    if(type is ArrayTypeSymbol arr_type)
    {
      var arr_exp = arracc.exp();
      if(!TryVisit(arr_exp))
        return true;

      if(Annotate(arr_exp).eval_type != Types.Int)
      {
        AddError(arr_exp, "array index expression is not of type int");
        return false;
      }

      type = arr_type.item_type.Get();

      var ast = new AST_Call(write ? EnumCall.ARR_IDXW : EnumCall.ARR_IDX, line, null);
      PeekAST().AddChild(ast);
    }
    else if(type is MapTypeSymbol map_type)
    {
      var arr_exp = arracc.exp();
      if(!TryVisit(arr_exp))
        return false;

      if(!_types.CheckAssign(map_type.key_type.Get(), Annotate(arr_exp), errors))
        return false;

      type = map_type.val_type.Get();

      var ast = new AST_Call(write ? EnumCall.MAP_IDXW : EnumCall.MAP_IDX, line, null);
      PeekAST().AddChild(ast);
    }
    else
    {
      AddError(arracc, "accessing not an array/map type '" + type?.GetName() + "'");
      return false;
    }

    return true;
  }

  struct NormCallArg
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

    var norm_cargs = new NormCallArg[total_args_num];
    for(int i = 0; i < total_args_num; ++i)
    {
      norm_cargs[i].orig = func_symb.TryGetArg(i);
      if(norm_cargs[i].orig == null)
      {
        AddError(func_symb.origin, "bad signature");
        return;
      }
    }

    var variadic_args = new List<bhlParser.CallArgContext>();
    if(func_symb.attribs.HasFlag(FuncAttrib.VariadicArgs))
      norm_cargs[total_args_num - 1].variadic = true;

    //1. filling normalized call args
    var call_args = cargs.callArgsList()?.callArg();
    for(int ci = 0; ci < call_args?.Length; ++ci)
    {
      var ca = call_args[ci];
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

      if(func_symb.attribs.HasFlag(FuncAttrib.VariadicArgs) && idx >= total_args_num - 1)
        variadic_args.Add(ca);
      else if(idx >= total_args_num)
      {
        AddError(ca, "there is no argument " + (idx + 1) + ", total arguments " + total_args_num);
        return;
      }
      else
        norm_cargs[idx].ca = ca;
    }

    PushAST(call);
    IParseTree prev_ca = null;
    //2. traversing normalized args
    for(int i = 0; i < norm_cargs.Length; ++i)
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
            PopJsonType();
            PopAST();
            PopCallByRef();
            return;
          }

          //let's check if there were any expressions compatible to be passed by ref
          if(is_ref)
          {
            if(!(na.ca.exp() is bhlParser.ExpChainContext ca_chain_exp) ||
               !(new ExpChain(ca_chain_exp, ca_chain_exp.chainExp()).IsSimpleVarAccess))
            {
              AddError(na.ca, "expression is not passable by 'ref'");
              PopAST();
              PopJsonType();
              PopAST();
              PopCallByRef();
              return;
            }
          }

          PopAddOptimizeAST();
          PopJsonType();
          PopCallByRef();

          if(!_types.CheckAssign(func_arg_type, Annotate(na.ca), errors))
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

          if(!ok || !_types.CheckAssign(varg_arr_type, Annotate(variadic_args[0]), errors))
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
            if(vidx + 1 < variadic_args.Count)
              varg_ast.AddChild(new AST_JsonArrAddItem());

            if(!_types.CheckAssign(varg_type, Annotate(vca), errors))
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
    var call_args_list = cargs.callArgsList();
    var call_args = call_args_list?.callArg();
    int ca_len = call_args?.Length ?? 0;
    IParseTree prev_ca = null;
    PushAST(call);
    for(int i = 0; i < func_args.Count; ++i)
    {
      var arg_type_ref = func_args[i];

      if(i == ca_len)
      {
        var next_arg = FindNextCallArg(cargs, prev_ca);
        AddError(next_arg, "missing argument of type '" + arg_type_ref + "'");
        PopAST();
        return;
      }

      var ca = call_args[i];
      var ca_name = ca.NAME();
      bool is_ref = ca.REF() != null;

      if(ca_name != null)
      {
        AddError(ca_name, "named arguments not supported for function pointers");
        PopAST();
        return;
      }

      var arg_type = arg_type_ref.Get();
      PushCallByRef(is_ref);
      PushJsonType(arg_type);
      PushAST(new AST_Interim());
      bool ok = TryVisit(ca);
      PopAddOptimizeAST();
      PopJsonType();
      PopCallByRef();

      if(!ok ||
         !_types.CheckAssign(arg_type is RefType rt ? rt.subj.Get() : arg_type, Annotate(ca), errors))
      {
        PopAST();
        return;
      }

      if(arg_type is RefType && ca.REF() == null)
      {
        AddError(ca, "'ref' is missing");
        PopAST();
        return;
      }
      else if(arg_type is not RefType && ca.REF() != null)
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
        call.type == EnumCall.FUNC_PTR_VAR ||
        call.type == EnumCall.FUNC_PTR_MVAR ||
        call.type == EnumCall.FUNC_PTR_RES
       ))
      return true;

    for(int i = 0; i < ast.children.Count; ++i)
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
    var call_args = cargs.callArgsList()?.callArg();
    for(int i = 0; i < call_args?.Length; ++i)
    {
      if(call_args[i] == curr && (i + 1) < call_args.Length)
        return call_args[i + 1];
    }

    //NOTE: graceful fallback
    return cargs;
  }

  FuncSignature ParseFuncSignature(bool is_coro, bhlParser.RetTypeContext ret_ctx, bhlParser.TypesContext types_ctx)
  {
    var ret_type = ParseType(ret_ctx);

    var arg_types = new List<ProxyType>();
    if(types_ctx != null)
    {
      var ref_types = types_ctx.refType();
      for(int i = 0; i < ref_types.Length; ++i)
      {
        var ref_type = ref_types[i];
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

  FuncSignature ParseFuncSignature(bool is_coro, ProxyType ret_type, bhlParser.FuncParamsContext fparams,
    out int default_args_num)
  {
    default_args_num = 0;
    var sig = new FuncSignature(is_coro ? FuncSignatureAttrib.Coro : 0, ret_type);
    if(fparams != null)
    {
      var param_decls = fparams.funcParamDeclare();
      for(int i = 0; i < param_decls.Length; ++i)
      {
        var tp = new ProxyType();

        var vd = param_decls[i];
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
              AddError(vd.REF(), "pass by 'ref' not allowed");

            if(i != param_decls.Length - 1)
              AddError(vd, "variadic argument must be last");

            if(vd.assignExp() != null)
              AddError(vd.assignExp(), "default argument is not allowed");

            sig.attribs |= FuncSignatureAttrib.VariadicArgs;
            ++default_args_num;
          }
          else if(vd.assignExp() != null)
          {
            if(vd.REF() != null)
              AddError(vd.REF(), "default values for 'ref' argument not allowed");
            ++default_args_num;
          }
        }

        sig.AddArg(tp);
      }
    }

    return sig;
  }

  FuncSignature ParseFuncSignature(bool is_coro, ProxyType ret_type, bhlParser.FuncParamsContext fparams)
  {
    int default_args_num;
    return ParseFuncSignature(is_coro, ret_type, fparams, out default_args_num);
  }

  ProxyType ParseType(bhlParser.RetTypeContext parsed)
  {
    ProxyType tp;

    //convenience special case
    if(parsed == null)
      tp = Types.Void;
    else if(parsed.type().Length > 1)
    {
      var tuple = new TupleType();
      for(int i = 0; i < parsed.type().Length; ++i)
        tuple.Add(ParseType(parsed.type()[i]));
      tp = curr_scope.R().T(tuple);
    }
    else
      tp = ParseType(parsed.type()[0]);

    if(tp.Get() == null)
      AddError(parsed, "type '" + tp + "' not found");

    return tp;
  }

  ProxyType ParseType(bhlParser.TypeContext ctx)
  {
    var tp = new ProxyType();

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

    //NOTE: save base type before potential array/map wrapping so we can
    //      annotate the nsName token with the actual element type symbol,
    //      not the ephemeral array/map wrapper (which has no scope/module)
    var base_tp = tp;

    if(ctx.arrType() != null)
    {
      if(tp.Get() == null)
      {
        AddError(ctx, "type '" + tp + "' not found");
        return tp;
      }

      tp = curr_scope.R().TArr(tp);
    }
    else if(ctx.mapType() != null)
    {
      if(tp.Get() == null)
      {
        AddError(ctx, "type '" + tp + "' not found");
        return tp;
      }

      var ktp = curr_scope.R().T(ctx.mapType().nsName().GetText());
      if(ktp.Get() == null)
      {
        AddError(ctx, "type '" + ktp + "' not found");
        return tp;
      }

      tp = curr_scope.R().TMap(ktp, tp);
    }

    var resolved = tp.Get();
    if(resolved == null)
    {
      AddError(ctx, "type '" + tp + "' not found");
      return tp;
    }

    //NOTE: this is required for LSP, we might want to have
    //      a special LSP mode for that?
    if(ctx.nsName() != null && base_tp.Get() is Symbol symb)
      LSP_SetSymbol(ctx.nsName().dotName().NAME(), symb);

    return tp;
  }

  AST_Tree ProcLambda(
    ParserRuleContext ctx,
    bhlParser.FuncLambdaContext lmb_ctx,
    ref IType curr_type,
    bool yielded = false,
    bool called_in_place = false
  )
  {
    LSP_AddSemanticToken(lmb_ctx.FUNC(), SemanticToken.Keyword);
    LSP_AddSemanticToken(lmb_ctx.CORO(), SemanticToken.Keyword);

    if(yielded)
      CheckCoroCallValidity(ctx);

    var captures = ParseCaptureList(lmb_ctx.captureList());

    var tp = ParseType(lmb_ctx.retType());

    var func_name = "$_" + Hash.CRC32(module.name) + "_lmb_" + lmb_ctx.Stop.Line + "_" + lmb_ctx.Stop.Column;

    var upvals = new List<AST_UpVal>();
    var lmb_symb = new LambdaSymbol(
      Annotate(ctx),
      func_name,
      ParseFuncSignature(lmb_ctx.CORO() != null, tp, lmb_ctx.funcParams()),
      upvals,
      captures,
      this.func_decl_stack
    );

    var ast = new AST_LambdaDecl(
      lmb_symb,
      called_in_place,
      upvals,
      lmb_ctx.Stop.Line
      );

    var scope_backup = curr_scope;
    PushScope(lmb_symb);

    //NOTE: for proper a symbol resolve fallback we set the scope to the one it's
    //      actually defined in during body parsing
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

    ParseFuncBlock(lmb_ctx, lmb_ctx.block(), lmb_ctx.retType(), ast);

    //NOTE: once we are out of lambda the eval type is the lambda itself
    curr_type = lmb_symb.signature;
    Annotate(ctx).eval_type = curr_type;

    PopScope();

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

  bool ProcLambdaCall(ParserRuleContext ctx,
    bhlParser.FuncLambdaContext lmb_ctx,
    ExpChainItems chain_items,
    ref IType curr_type,
    bool write,
    bool yielded,
    bool called_in_place
    )
  {
    var ast = ProcLambda(
      ctx,
      lmb_ctx,
      ref curr_type,
      yielded: yielded,
      called_in_place: called_in_place
    );

    var interim = new AST_Interim();
    interim.AddChild(ast);
    PushAST(interim);

    var walker = new ChainWalker(this, curr_type, write);
    if(!walker.WalkItems(chain_items))
    {
      PopAST();
      return false;
    }
    curr_type = walker.CurrType;

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
    for(int i = 0; i < pairs.Length; ++i)
    {
      var pair = pairs[i];
      TryVisit(pair);
    }

    PopAST();

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
        AddError(ctx,  "type '" + arr_type.item_type + "' not found");
        return null;
      }

      PushJsonType(orig_type);

      var ast = new AST_JsonArr(arr_type, ctx.Start.Line);

      PushAST(ast);
      var vals = ctx.jsonValue();
      for(int i = 0; i < vals.Length; ++i)
      {
        TryVisit(vals[i]);
        //the last item is added implicitely
        if(i + 1 < vals.Length)
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
        AddError(ctx,  "type '" + map_type.key_type + "' not found");
        return null;
      }

      var val_type = map_type.val_type.Get();
      if(val_type == null)
      {
        AddError(ctx,  "type '" + map_type.val_type + "' not found");
        return null;
      }

      var ast = new AST_JsonMap(curr_type, ctx.Start.Line);

      PushAST(ast);
      var vals = ctx.jsonValue();
      for(int i = 0; i < vals.Length; ++i)
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
        if(i + 1 < vals.Length)
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

    LSP_SetSymbol(ctx.NAME(), member);

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
      _types.CheckAssign(curr_type, Annotate(exp), errors);
    }

    return null;
  }

  public override object VisitExpTypeof(bhlParser.ExpTypeofContext ctx)
  {
    LSP_AddSemanticToken(ctx.TYPEOF(), SemanticToken.Keyword);

    var tp = ParseType(ctx.type());

    Annotate(ctx).eval_type = Types.Type;

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

  public override object VisitExpYieldCall(bhlParser.ExpYieldCallContext ctx)
  {
    LSP_AddSemanticToken(ctx.YIELD(), SemanticToken.Keyword);

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
      LSP_SetSymbol(ctx.newExp().type().nsName().dotName().NAME(), cl as Symbol);

    if (ctx.newExp().jsonObject() != null)
    {
      PushJsonType(cl);
      TryVisit(ctx.newExp().jsonObject());
      PopJsonType();
    }
    else if (ctx.newExp().jsonArray() != null)
    {
      PushJsonType(cl);
      TryVisit(ctx.newExp().jsonArray());
      PopJsonType();
    }
    else
    {
      var ast = new AST_New(cl, ctx.Start.Line);
      PeekAST().AddChild(ast);
    }

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
      Types.CheckCastIsPossible(Annotate(ctx), Annotate(exp), errors);

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

    EnumUnaryOp type = ctx.operatorUnary().GetText() switch
    {
      "-" => EnumUnaryOp.NEG,
      "!" => EnumUnaryOp.NOT,
      "~" => EnumUnaryOp.BIT_NOT,
      _ => throw new Exception("Unexpected token")
    };

    var ast = new AST_UnaryOpExp(type);
    var exp = ctx.exp();
    PushAST(ast);
    bool ok = TryVisit(exp);
    PopAST();

    if(ok)
    {
      if(type == EnumUnaryOp.NOT)
        Annotate(ctx).eval_type = _types.CheckLogicalNot(Annotate(exp), errors);
      else if(type == EnumUnaryOp.NEG)
        Annotate(ctx).eval_type = _types.CheckUnaryMinus(Annotate(exp), errors);
      else if(type == EnumUnaryOp.BIT_NOT)
        Annotate(ctx).eval_type = _types.CheckBitNot(Annotate(exp), errors);
      else
        throw new Exception("Unexpected token");
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
      if(!chain.IsMemorySlotAccess)
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
      var vproxy = VarsOrDeclsProxy.From(chain_ctx);
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

  bhlParser.ExpContext one_literal_exp
  {
    get
    {
      //TODO: make expression AST on the fly somehow?
      //exp = new bhlParser.ExpLiteralNumContext("1");
      if(_one_literal_exp == null)
      {
        _one_literal_exp = Stream2Parser(
          new ModuleDeclared(),
          CompileErrorsHub.MakeEmpty(),
          new MemoryStream(System.Text.Encoding.UTF8.GetBytes("1")),
          defines: null,
          preproc_parsed: out var _,
          tokens: out var __
        ).exp();
      }

      return _one_literal_exp;
    }
  }

  bool ProcPostIncDec(ParserRuleContext ctx, bhlParser.ChainExpContext chain_exp,
    bhlParser.OperatorIncDecContext inc_dec)
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
      op_type = EnumBinaryOp.NEQ;
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
    //NOTE: op_type stays the same, but we might tune the bin_op_ast.type according to some specific types
    AST_BinaryOpExp bin_op_ast = new AST_BinaryOpExp(op_type, ctx.Start.Line);
    AST_Tree ast = bin_op_ast;
    PushAST(ast);
    bool ok1 = TryVisit(lhs);
    int ops_edge_idx = ast.children.Count;
    bool ok2 = TryVisit(rhs);
    PopAST();

    if(!ok1 || !ok2)
      return;

    var ann_lhs = Annotate(lhs);
    var ann_rhs = Annotate(rhs);

    FuncSymbol op_overload = null;
    if(ann_lhs.eval_type is ClassSymbol class_symb)
      op_overload = class_symb.Resolve(op) as FuncSymbol;

    //NOTE: only if there's no operator overload we might replace some opcodes
    //      (e.g NEQ => (NOT(EQ)) )
    if(op_overload == null)
    {
      if(op_type == EnumBinaryOp.NEQ)
      {
        var not_ast = new AST_UnaryOpExp(EnumUnaryOp.NOT);
        bin_op_ast.type = GetSpecificEqOp(ann_lhs.eval_type, ann_rhs.eval_type);
        not_ast.AddChild(bin_op_ast);
        ast = not_ast;
      }
      else if(op_type == EnumBinaryOp.EQ)
        bin_op_ast.type = GetSpecificEqOp(ann_lhs.eval_type, ann_rhs.eval_type);
      else if(op_type == EnumBinaryOp.ADD && (Types.IsString(ann_lhs.eval_type) || Types.IsString(ann_rhs.eval_type)))
        bin_op_ast.type = EnumBinaryOp.CONCAT;
    }

    //NOTE: checking if there's binary operator overload
    if(op_overload != null)
    {
      Annotate(ctx).eval_type = _types.CheckBinOpOverload(ns, ann_lhs, ann_rhs, op_overload, errors);

      //NOTE: replacing original AST, a bit 'dirty' but kinda OK
      var over_ast = new AST_Interim();
      for(int i = 0; i < ast.children.Count; ++i)
        over_ast.AddChild(ast.children[i]);
      var op_call = new AST_Call(EnumCall.FUNC, ctx.Start.Line, op_overload, 2 /*cargs bits*/);
      over_ast.AddChild(op_call);
      ast = over_ast;
    }
    else if(op_type == EnumBinaryOp.EQ || op_type == EnumBinaryOp.NEQ)
      Annotate(ctx).eval_type = _types.CheckEqBinOp(ann_lhs, ann_rhs, errors);
    else if(
      op_type == EnumBinaryOp.GT ||
      op_type == EnumBinaryOp.GTE ||
      op_type == EnumBinaryOp.LT ||
      op_type == EnumBinaryOp.LTE
    )
      Annotate(ctx).eval_type = _types.CheckRelationalBinOp(ann_lhs, ann_rhs, errors);
    else
    {
      if(op_type == EnumBinaryOp.ADD &&
         CheckImplicitCastToString(ctx, ast, ops_edge_idx, ann_lhs, ann_rhs, lhs_self_op))
        Annotate(ctx).eval_type = Types.String;
      else
        Annotate(ctx).eval_type = _types.CheckBinOp(ann_lhs, ann_rhs, errors);
    }

    PeekAST().AddChild(ast);

    //NOTE: adding implicit casting to int of the result of the division product of two ints
    if(op_type == EnumBinaryOp.DIV &&
       ann_lhs.eval_type == Types.Int &&
       ann_rhs.eval_type == Types.Int
      )
      PeekAST().AddChild(new AST_TypeCast(Types.Int, force_type: true, line_num: ctx.Start.Line));
  }

  static EnumBinaryOp GetSpecificEqOp(IType lhs, IType rhs)
  {
    if(Types.IsScalar(lhs) && Types.IsScalar(rhs))
      return EnumBinaryOp.EQ_SCLR;
    else if(Types.IsString(lhs) && Types.IsString(rhs))
      return EnumBinaryOp.EQ_STR;
    else
      return EnumBinaryOp.EQ;
  }

  static bool SupportsImplictCastToString(IType type)
  {
    return Types.IsNumeric(type) || type == Types.Bool || type is EnumSymbol;
  }

  bool CheckImplicitCastToString(ParserRuleContext ctx, AST_Tree ast, int ops_edge_idx, AnnotatedParseTree ann_lhs,
    AnnotatedParseTree ann_rhs, bool lhs_self_op)
  {
    //NOTE: only if it's NOT a 'left-side' modifying operation (e.g i += "foo")
    if(!lhs_self_op && SupportsImplictCastToString(ann_lhs.eval_type) && ann_rhs.eval_type == Types.String)
    {
      ast.children.Insert(ops_edge_idx, new AST_TypeCast(Types.String, force_type: false, line_num: ctx.Start.Line));
      return true;
    }
    else if(ann_lhs.eval_type == Types.String && SupportsImplictCastToString(ann_rhs.eval_type))
    {
      ast.children.Insert(ast.children.Count,
        new AST_TypeCast(Types.String, force_type: false, line_num: ctx.Start.Line));
      return true;
    }

    return false;
  }

  public override object VisitExpBitwiseAnd(bhlParser.ExpBitwiseAndContext ctx)
  {
    LSP_AddSemanticToken(ctx.BAND(), SemanticToken.Operator);
    WarnBitwiseNextToComparison(ctx.exp(0), ctx.exp(1), "&");
    return ProcBitOp(ctx, ctx.exp(0), ctx.exp(1), EnumBinaryOp.BIT_AND);
  }

  public override object VisitExpBitwiseOr(bhlParser.ExpBitwiseOrContext ctx)
  {
    LSP_AddSemanticToken(ctx.BOR(), SemanticToken.Operator);
    WarnBitwiseNextToComparison(ctx.exp(0), ctx.exp(1), "|");
    return ProcBitOp(ctx, ctx.exp(0), ctx.exp(1), EnumBinaryOp.BIT_OR);
  }

  void WarnBitwiseNextToComparison(bhlParser.ExpContext lhs, bhlParser.ExpContext rhs, string op)
  {
    if(lhs is bhlParser.ExpCompareContext || rhs is bhlParser.ExpCompareContext)
      AddWarning(lhs is bhlParser.ExpCompareContext ? lhs : rhs,
        $"suggest parentheses around comparison in operand of '{op}'");
  }

  public override object VisitExpShift(bhlParser.ExpShiftContext ctx)
  {
    LSP_AddSemanticToken(ctx.operatorShift(), SemanticToken.Operator);
    var op = ctx.operatorShift().SHR() != null ? EnumBinaryOp.BIT_SHR : EnumBinaryOp.BIT_SHL;
    return ProcBitOp(ctx, ctx.exp(0), ctx.exp(1), op);
  }

  object ProcBitOp(ParserRuleContext ctx, bhlParser.ExpContext exp_0, bhlParser.ExpContext exp_1, EnumBinaryOp op)
  {
    var ast = new AST_BinaryOpExp(op, ctx.Start.Line);

    PushAST(ast);
    bool ok1 = TryVisit(exp_0);
    bool ok2 = TryVisit(exp_1);
    PopAST();

    if(!ok1 || !ok2)
      return null;

    var op_str = op switch {
      EnumBinaryOp.BIT_AND => "&",
      EnumBinaryOp.BIT_OR  => "|",
      EnumBinaryOp.BIT_SHR => ">>",
      _                    => "<<"
    };
    Annotate(ctx).eval_type = _types.CheckBitOp(Annotate(exp_0), Annotate(exp_1), op_str, errors);

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLogicalAnd(bhlParser.ExpLogicalAndContext ctx)
  {
    LSP_AddSemanticToken(ctx.LAND(), SemanticToken.Operator);

    var ast = new AST_BinaryOpExp(EnumBinaryOp.AND, ctx.Start.Line);
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

    Annotate(ctx).eval_type = _types.CheckLogicalOp(Annotate(exp_0), Annotate(exp_1), errors);

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLogicalOr(bhlParser.ExpLogicalOrContext ctx)
  {
    LSP_AddSemanticToken(ctx.LOR(), SemanticToken.Operator);

    var ast = new AST_BinaryOpExp(EnumBinaryOp.OR, ctx.Start.Line);
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

    Annotate(ctx).eval_type = _types.CheckLogicalOp(Annotate(exp_0), Annotate(exp_1), errors);

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralNum(bhlParser.ExpLiteralNumContext ctx)
  {
    AST_Literal ast = null;

    var number = ctx.number();

    LSP_AddSemanticToken(number, SemanticToken.Number);

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
    LSP_AddSemanticToken(ctx.NORMALSTRING(), SemanticToken.String);

    Annotate(ctx).eval_type = Types.String;

    var ast = new AST_Literal(ConstType.STR);
    ast.sval = ctx.NORMALSTRING().GetText();
    //removing quotes
    ast.sval = ast.sval.Substring(1, ast.sval.Length - 2);
    //replacing extra slashes by quotes
    ast.sval = ast.sval.Replace("\\\"", "\"");
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

    return func_decl_stack[func_decl_stack.Count - 1];
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
    for(int i = 0; i < tree.ChildCount; ++i)
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

        if(!_types.CheckAssign(func_symb.origin.parsed, Annotate(exp_item), errors))
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
        for(int i = explen; i-- > 0;)
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
        for(int i = 0; i < explen; ++i)
        {
          var exp = ret_val.exp()[i];
          if(!_types.CheckAssign(fmret_type[i].Get(), Annotate(exp), errors))
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
    func_ast.symbol._default_args_num = func_ast.GetDefaultArgsNum();
    if(func_ast.symbol.attribs.HasFlag(FuncAttrib.VariadicArgs))
      ++func_ast.symbol._default_args_num;
  }

  public override object VisitNsDecl(bhlParser.NsDeclContext ctx)
  {
    LSP_AddSemanticToken(ctx?.NAMESPACE(), SemanticToken.Keyword);

    string name = ctx?.dotName()?.NAME()?.GetText();
    if(name == null)
      return null;

    //NOTE: traversing nested namespaces named like 'Foo.Bar'
    int dot_name_idx = 0;
    do
    {
      string full_path = (GetCurrentScopeNamePath() + '.' + name).TrimStart('.');

      //NOTE: let's first check if there's such a namespace in
      //      the current scope (e.g it was defined in bindings)
      var ns = curr_scope.Resolve(name) as Namespace;
      if(ns == null)
        //...otherwise let's check existing namespaces in passes
        ns = FindNamespaceInPasses(full_path);

      if(ns == null)
        ns = new Namespace(module, name);

      //NOTE: special case for namespace parser pass, we don't
      //      define it mmediately in the current scope but rather
      //      do it later, this way we preserve natural symbols order
      //      as they are declared in the source
      passes.Add(new ParserPass(curr_scope, ns, full_path));

      //let's push it so that subsequent passes will have the proper curr_scope
      PushScope(ns);

      if(dot_name_idx >= ctx.dotName().memberAccess().Length)
        break;
      name = ctx.dotName().memberAccess()[dot_name_idx].NAME().GetText();
      ++dot_name_idx;
    } while(true);

    foreach(var decl in ctx.decl())
      ProcessDecl(decl);

    for(int i = 0; i <= dot_name_idx; ++i)
      PopScope();

    return null;
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

    LSP_SetSymbol(ctx.NAME(), symb);

    for(int i = 0; i < ctx.enumBlock()?.enumMember()?.Length; ++i)
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

      LSP_SetSymbol(em.NAME(), (EnumItemSymbol)symb.members[symb.members.Count - 1]);
    }

    return null;
  }

  public override object VisitFuncParams(bhlParser.FuncParamsContext ctx)
  {
    var fparams = ctx.funcParamDeclare();
    bool found_default_arg = false;

    for(int i = 0; i < fparams.Length; ++i)
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
    return ProcDeclOrAssign(VarsOrDeclsProxy.From(vdecl), assign_exp, start_line);
  }

  struct TypeIterator
  {
    IType type;

    public int Count
    {
      get
      {
        if(type is TupleType tt)
          return tt.Count;
        return 1;
      }
    }

    public TypeIterator(IType type)
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

    for(int i = 0; i < vproxy.Count; ++i)
    {
      VariableSymbol var_symb = null;
      AnnotatedParseTree var_ann = null;
      bool is_decl = false;
      bool is_auto_var = false;

      //check if we declare a var or use an existing one
      var vd_type = vproxy.TypeAt(i);
      if(vd_type != null)
      {
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
        LSP_SetSymbol(vd_name, var_symb);
      }
      else if(vproxy.LocalNameAt(i) is {} vd_name)
      {
        var_symb = curr_scope.ResolveWithFallback(vd_name.GetText()) as VariableSymbol;
        if(var_symb == null)
        {
          AddError(vd_name, "symbol '" + vd_name.GetText() + "' not resolved");
          return false;
        }

        var_ann = Annotate(vd_name);
        var_ann.eval_type = var_symb.type.Get();
        LSP_SetSymbol(vd_name, var_symb);

        var ast = new AST_Call(EnumCall.VARW, start_line, var_symb);
        var_ast.AddChild(ast);
      }
      else if(vproxy.VarAccessAt(i) is {} var_exp)
      {
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
    var subst_symbol =
      new VariableSymbol(disabled_symbol.origin.parsed, "#$" + disabled_symbol.name, disabled_symbol.type);
    members.Replace(disabled_symbol, subst_symbol);
    return subst_symbol;
  }

  static void EnableVar(SymbolsStorage members, VariableSymbol disabled_symbol, VariableSymbol subst_symbol)
  {
    members.Replace(subst_symbol, disabled_symbol);
  }

  public override object VisitStmDeclOptAssign(bhlParser.StmDeclOptAssignContext ctx)
  {
    var vdecls = VarsOrDeclsProxy.From(ctx.varDeclareList().varDeclare());
    var assign_exp = ctx.assignExp();
    ProcDeclOrAssign(vdecls, assign_exp, ctx.Start.Line);

    return null;
  }

  public override object VisitStmDeclOrExpAssign(bhlParser.StmDeclOrExpAssignContext ctx)
  {
    var vdecls = VarsOrDeclsProxy.From(ctx.varDeclaresOrChainExps().varDeclareOrChainExp());
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

    if(assign_exp != null)
      _types.CheckAssign(Annotate(name), Annotate(assign_exp), errors);
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
    ProxyType tp,
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

    if(is_ref && !func_arg)
    {
      AddError(name, "'ref' is only allowed in function declaration");
      return null;
    }

    symb = func_arg
      ? (VariableSymbol) new FuncArgSymbol(var_ann, name.GetText(), tp, is_ref)
      : new VariableSymbol(var_ann, name.GetText(), tp);

    if(!curr_scope.TryDefine(symb, out SymbolError err))
    {
      AddError(name, err.Message);
      return null;
    }

    LSP_SetSymbol(name, symb);

    if(write)
      return new AST_Call(EnumCall.VARWDCL, name.Symbol.Line, symb, 0, name);
    return new AST_VarDecl(symb, name.Symbol.Line);
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

      for(int s = assign_ast.children.Count; s-- > 0;)
        ast_dest.children.Insert(ast_insert_idx, assign_ast.children[s]);
    }

    var assign_type = new TypeIterator(Annotate(assign_exp).eval_type);

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

      var_symb.type = new ProxyType(auto_type);
    }

    return _types.CheckAssign(var_ann, assign_type.At(var_idx), errors);
  }

  public override object VisitBlock(bhlParser.BlockContext ctx)
  {
    ProcBlock(BlockType.SEQ, ctx.statement());
    return null;
  }

  public override object VisitStmParal(bhlParser.StmParalContext ctx)
  {
    LSP_AddSemanticToken(ctx.PARAL(), SemanticToken.Keyword);

    var block = ProcBlock(BlockType.PARAL, ctx.block()?.statement());
    if(block == null)
      AddError(ctx, "empty paral blocks are not allowed");
    return null;
  }

  public override object VisitStmParalAll(bhlParser.StmParalAllContext ctx)
  {
    LSP_AddSemanticToken(ctx.PARAL_ALL(), SemanticToken.Keyword);

    var block = ProcBlock(BlockType.PARAL_ALL, ctx.block()?.statement());
    if(block == null)
      AddError(ctx, "empty paral blocks are not allowed");
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

    if(!ok || !_types.CheckAssign(Types.Bool, Annotate(ctx.exp()), errors))
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
    for(int i = 0; i < else_if.Length; ++i)
    {
      var item = else_if[i];

      LSP_AddSemanticToken(item.ELSE(), SemanticToken.Keyword);
      LSP_AddSemanticToken(item.IF(), SemanticToken.Keyword);

      var item_cond = new AST_Block(BlockType.SEQ);
      PushAST(item_cond);
      bool item_ok = TryVisit(item.exp());
      PopAST();

      if(!item_ok || !_types.CheckAssign(Types.Bool, Annotate(item.exp()), errors))
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

    if(!ok1 || !_types.CheckAssign(Types.Bool, Annotate(exp_0), errors))
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

    if(!ok2 || !_types.CheckAssign(ann_exp_1, Annotate(exp_2), errors))
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

    if(!ok || !_types.CheckAssign(Types.Bool, Annotate(ctx.exp()), errors))
      return null;

    ast.AddChild(cond);

    PushAST(ast);
    ok = ProcBlock(BlockType.SEQ, ctx.block()?.statement()) != null;
    PopAST();
    if(!ok)
      return null;
    ast.children[ast.children.Count - 1].AddChild(new AST_Continue(jump_marker: true));

    PeekAST().AddChild(ast);

    PopBlock(ast);

    BlockResetsCurrentFunctionReturnInfo();

    return null;
  }

  void BlockResetsCurrentFunctionReturnInfo()
  {
    return_found.Remove(PeekFuncDecl());
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
    ast.children[ast.children.Count - 1].AddChild(new AST_Continue(jump_marker: true));

    var cond = new AST_Block(BlockType.SEQ);
    PushAST(cond);
    ok = TryVisit(ctx.exp());
    PopAST();

    if(!ok || !_types.CheckAssign(Types.Bool, Annotate(ctx.exp()), errors))
      return null;

    ast.AddChild(cond);

    PeekAST().AddChild(ast);

    PopBlock(ast);

    BlockResetsCurrentFunctionReturnInfo();

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

    if(!ok || !_types.CheckAssign(Types.Bool, Annotate(for_cond), errors))
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

    BlockResetsCurrentFunctionReturnInfo();

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

    if(!ok || !_types.CheckAssign(Types.Bool, Annotate(ctx.exp()), errors))
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
      ProxyType iter_type;
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
        var vd_type = new ProxyType();
        if(vd.type().GetText() == "var")
        {
          var predicted_arr_type = PredictType(ctx.foreachExp().exp()) as ArrayTypeSymbol;
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
      if(!ok || !_types.CheckAssign(arr_type, Annotate(exp), errors))
        goto Bail;

      var arr_tmp_name = "$foreach_tmp" + exp.Start.Line + "_" + exp.Start.Column;
      var arr_tmp_symb = curr_scope.ResolveWithFallback(arr_tmp_name) as VariableSymbol;
      if(arr_tmp_symb == null)
      {
        arr_tmp_symb = new VariableSymbol(Annotate(exp), arr_tmp_name, curr_scope.R().TArr(iter_type));
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
      PeekAST().AddChild(new AST_VarDecl(arr_cnt_symb, ctx.Start.Line));

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
      bin_op.AddChild(new AST_Call(EnumCall.VAR, ctx.Start.Line, arr_type.Resolve("Count")));
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
      var vd_key_type = new ProxyType();
      var vod_val = ctx.foreachExp().varOrDeclare()[1];
      var vd_val = vod_val.varDeclare();
      var vd_val_type = new ProxyType();

      if(vd_key.type().GetText() == "var" || vd_val.type().GetText() == "var")
      {
        var predicted_map_type = PredictType(ctx.foreachExp().exp()) as MapTypeSymbol;
        if(predicted_map_type == null)
        {
          AddError(ctx.foreachExp().exp(), "expression is not of map type");
          goto Bail;
        }

        vd_key_type = predicted_map_type.key_type;
        vd_val_type = predicted_map_type.val_type;
      }

      if(vd_key_type.IsNull() && vd_key.type().GetText() != "var")
        vd_key_type = ParseType(vd_key.type());

      if(vd_val_type.IsNull() && vd_val.type().GetText() != "var")
        vd_val_type = ParseType(vd_val.type());

      ProxyType key_iter_type;
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

      ProxyType val_iter_type;
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
      if(!ok || !_types.CheckAssign(map_type, Annotate(exp), errors))
        goto Bail;

      var map_tmp_en_name = "$foreach_en" + exp.Start.Line + "_" + exp.Start.Column;
      var map_tmp_en_symb = curr_scope.ResolveWithFallback(map_tmp_en_name) as VariableSymbol;
      if(map_tmp_en_symb == null)
      {
        map_tmp_en_symb = new VariableSymbol(Annotate(exp), map_tmp_en_name, Types.Any);
        curr_scope.Define(map_tmp_en_symb);
      }

      //let's call GetEnumerator
      PeekAST().AddChild(new AST_Call(EnumCall.VAR, ctx.Start.Line, map_type.Resolve("$Enumerator")));
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
      cond.AddChild(new AST_Call(EnumCall.VAR, ctx.Start.Line, map_type.enumerator_type.Resolve("Next")));
      ast.AddChild(cond);

      //while body
      PushAST(ast);
      var block = ProcBlock(BlockType.SEQ, ctx.block()?.statement());
      if(block == null)
        goto Bail;
      //prepending filling of k/v
      block.children.Insert(0, new AST_Call(EnumCall.VARW, ctx.Start.Line, val_iter_symb));
      block.children.Insert(0, new AST_Call(EnumCall.VARW, ctx.Start.Line, key_iter_symb));
      block.children.Insert(0,
        new AST_Call(EnumCall.MFUNC, ctx.Start.Line, map_type.enumerator_type.Resolve("Current")));
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

    BlockResetsCurrentFunctionReturnInfo();

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
          for(int c = 0; c < tmp.children.Count; ++c)
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
      BlockResetsCurrentFunctionReturnInfo();

    PopBlock(ast);

    //NOTE: if there are no children, something is definitely wrong
    //      probably due to parsing errors, also we explicitely disallow
    //      empty paral branches
    if((sts.Length > 0 || is_paral) && ast.children.Count == 0)
      return null;

    PeekAST().AddChild(ast);
    return ast;
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

}
