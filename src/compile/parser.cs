using System;
using System.IO;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace bhl {

public class ANTLR_Parsed
{
  public ITokenStream tokens { get; private set; }
  public bhlParser.ProgramContext prog { get; private set; }

  public ANTLR_Parsed(ITokenStream tokens, bhlParser.ProgramContext prog)
  {
    this.tokens = tokens;
    this.prog = prog;
  }
}

public class WrappedParseTree
{
  public IParseTree tree;
  public Module module;
  public ITokenStream tokens;
  public IType eval_type;

  public int line { 
    get { 
      return tokens.Get(tree.SourceInterval.a).Line;  
    } 
  }

  public int char_pos { 
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

    public Result(Module module, AST_Module ast)
    {
      this.module = module;
      this.ast = ast;
    }
  }

  AST_Module root_ast;
  public Result result;

  ANTLR_Parsed parsed;

  Types types;

  public Module module;

  public FileImports imports; 

  Dictionary<bhlParser.MimportContext, string> parsed_imports = new Dictionary<bhlParser.MimportContext, string>();

  //NOTE: module.ns linked with types.ns
  Namespace ns;

  ITokenStream tokens;
  ParseTreeProperty<WrappedParseTree> tree_props = new ParseTreeProperty<WrappedParseTree>();

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

  int ref_compatible_exp_counter;

  Stack<AST_Tree> ast_stack = new Stack<AST_Tree>();

  public static CommonTokenStream Stream2Tokens(string file, Stream s)
  {
    var ais = new AntlrInputStream(s);
    var lex = new bhlLexer(ais);
    lex.AddErrorListener(new ErrorLexerListener(file));
    return new CommonTokenStream(lex);
  }

  public static bhlParser Stream2Parser(string file, Stream src)
  {
    var tokens = Stream2Tokens(file, src);
    var p = new bhlParser(tokens);
    p.AddErrorListener(new ErrorParserListener(file));
    p.ErrorHandler = new ErrorStrategy();
    return p;
  }
  
  public static ANTLR_Processor MakeProcessor(Module module, FileImports imports, ANTLR_Parsed parsed/*can be null*/, Types ts)
  {
    if(parsed == null)
    {
      using(var sfs = File.OpenRead(module.file_path))
      {
        return MakeProcessor(module, imports, sfs, ts);
      }
    }
    else 
      return new ANTLR_Processor(parsed, module, imports, ts);
  }

  public static ANTLR_Processor MakeProcessor(Module module, FileImports imports, Stream src, Types ts)
  {
    var p = Stream2Parser(module.file_path, src);
    var parsed = new ANTLR_Parsed(p.TokenStream, p.program());
    return new ANTLR_Processor(parsed, module, imports, ts);
  }

  public ANTLR_Processor(ANTLR_Parsed parsed, Module module, FileImports imports, Types types)
  {
    this.parsed = parsed;
    this.tokens = parsed.tokens;

    this.types = types;
    this.module = module;
    this.imports = imports;

    ns = module.ns;
    ns.Link(types.ns);

    PushScope(ns);
  }

  void FireError(IParseTree place, string msg) 
  {
    throw new SemanticError(module, place, tokens, msg);
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

  WrappedParseTree Wrap(IParseTree t)
  {
    var w = tree_props.Get(t);
    if(w == null)
    {
      w = new WrappedParseTree();
      w.module = module;
      w.tree = t;
      w.tokens = tokens;

      tree_props.Put(t, w);
    }
    return w;
  }

  internal void Phase_Outline()
  {
    root_ast = new AST_Module(module.name);

    passes.Clear();

    PushAST(root_ast);
    VisitProgram(parsed.prog);
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
    var ast_import = new AST_Import();

    foreach(var kv in parsed_imports)
    {
      //let's check if it's a registered native module
      var reg_mod = types.FindRegisteredModule(kv.Value);
      if(reg_mod != null)
      {
        ns.Link(reg_mod.ns);
        continue;
      }

      //let's check if it's a compiled module
      var file_path = imports.MapToFilePath(kv.Value);
      if(file_path == null || !File.Exists(file_path))
        FireError(kv.Key, "invalid import");

      var imported_module = file2proc[file_path].module;

      //NOTE: let's add imported global vars to module's global vars index
      if(module.local_gvars_mark == -1)
        module.local_gvars_mark = module.gvars.Count;

      for(int i=0;i<imported_module.local_gvars_num;++i)
        module.gvars.index.Add(imported_module.gvars.index[i]);

      ns.Link(imported_module.ns);

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

    result = new Result(module, root_ast);
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
    for(int i=0;i<ctx.progblock().Length;++i)
      Visit(ctx.progblock()[i]);

    return null;
  }

  public override object VisitProgblock(bhlParser.ProgblockContext ctx)
  { 
    var imps = ctx.imports();
    if(imps != null)
    {
      for(int i=0;i<imps.mimport().Length;++i)
        ParseImport(imps.mimport()[i]);
    }

    Visit(ctx.decls()); 

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

  public override object VisitSymbCall(bhlParser.SymbCallContext ctx)
  {
    var exp = ctx.callExp(); 
    Visit(exp);
    var eval_type = Wrap(exp).eval_type;
    if(eval_type != null && eval_type != Types.Void)
    {
      bool has_calls = false;
      for(int c=0;c<exp.chainExp().Length;++c)
      {
        if(exp.chainExp()[c].callArgs() != null)
        {
          has_calls = true;
          break;
        }
      }

      if(!has_calls)
        FireError(exp, "useless statement");

      var tuple = eval_type as TupleType;
      if(tuple != null)
      {
        for(int i=0;i<tuple.Count;++i)
          PeekAST().AddChild(new AST_PopValue());
      }
      else
        PeekAST().AddChild(new AST_PopValue());
    }
    return null;
  }

  public override object VisitCallExp(bhlParser.CallExpContext ctx)
  {
    IType curr_type = null;

    //NOTE: if expression starts with '..' we consider the global namespace instead of current scope
    ProcChainedCall(
      ctx.GLOBAL() != null ? ns : curr_scope, 
      ctx.NAME(), 
      new ExpChain(ctx.chainExp()), 
      ref curr_type, 
      ctx
    );

    Wrap(ctx).eval_type = curr_type;

    return null;
  }

  public override object VisitLambdaCall(bhlParser.LambdaCallContext ctx)
  {
    CommonVisitLambda(ctx, ctx.funcLambda(), yielded: false);
    return null;
  }

  public override object VisitYieldLambdaCall(bhlParser.YieldLambdaCallContext ctx)
  {
    CommonVisitLambda(ctx, ctx.funcLambda(), yielded: true);
    return null;
  }

  AST_Tree ProcChainedCall(
    IScope root_scope,
    ITerminalNode root_name, 
    IExpChain chain, 
    ref IType curr_type, 
    ParserRuleContext chain_ctx,
    bool write = false,
    bool yielded = false
   )
  {
    IScope scope = root_scope;

    int line = chain_ctx.Start.Line;  

    PushAST(new AST_Interim());

    ITerminalNode curr_name = root_name;

    int chain_offset = 0;

    if(root_name != null)
    {
      var name_symb = scope.ResolveWithFallback(curr_name.GetText());

      TryProcessClassBaseCall(ref curr_name, ref scope, ref name_symb, ref chain_offset, chain, line);

      if(name_symb == null)
        FireError(root_name, "symbol '" + curr_name.GetText() + "' not resolved");

      TryApplyNamespaceOffset(ref curr_name, ref scope, ref name_symb, ref chain_offset, chain);

      if(name_symb is IType)
        curr_type = (IType)name_symb;
      else if(name_symb is ITyped typed)
        curr_type = typed.GetIType();
      else
        curr_type = null;
      if(curr_type == null)
        FireError(root_name, "bad chain call");
    }

    int c = chain_offset;
    if(chain != null)
    {
      for(;c<chain.Length;++c)
      {
        var cargs = chain.callArgs(c);
        var macc = chain.memberAccess(c);
        var arracc = chain.arrAccess(c);
        bool is_last = c == chain.Length-1;

        if(cargs != null)
        {
          ProcCallChainItem(scope, curr_name, cargs, null, ref curr_type, line, write: false);
          curr_name = null;
        }
        else if(arracc != null)
        {
          ProcCallChainItem(scope, curr_name, null, arracc, ref curr_type, line, write: write && is_last);
          curr_name = null;
        }
        else if(macc != null)
        {
          Symbol macc_name_symb = null;
          if(curr_name != null)
            macc_name_symb = ProcCallChainItem(scope, curr_name, null, null, ref curr_type, line, write: false);

          scope = curr_type as IScope;
          if(!(scope is IInstanceType) && !(scope is EnumSymbol))
            FireError(macc, "type doesn't support member access via '.'");

          if(!(macc_name_symb is ClassSymbol) && 
              scope.ResolveWithFallback(macc.NAME().GetText()) is FuncSymbol macc_fs && 
              macc_fs.attribs.HasFlag(FuncAttrib.Static))
            FireError(macc, "calling static method on instance is forbidden");

          curr_name = macc.NAME();
        }
      }
    }

    //checking the leftover of the call chain
    if(curr_name != null)
      ProcCallChainItem(scope, curr_name, null, null, ref curr_type, line, write, leftover: true);

    var chain_ast = PeekAST();
    PopAST();

    ValidateChainCall(chain, chain_ctx, chain_ast, yielded);

    PeekAST().AddChildren(chain_ast);

    return chain_ast;
  }
  
  void ValidateChainCall(IExpChain chain, ParserRuleContext chain_ctx, AST_Tree chain_ast, bool yielded)
  {
    for(int i = 0; i < chain_ast.children.Count; ++i)
    {
      if(chain_ast.children[i] is AST_Call call)
      {
        if((call.type == EnumCall.FUNC || call.type == EnumCall.MFUNC) &&
            call.symb is FuncSymbol fs)
        {
          ValidateFuncCall(chain, chain_ctx, i, chain_ast.children.Count-1 == i, fs.signature, yielded);
        }
        else if((call.type == EnumCall.FUNC_VAR || call.type == EnumCall.FUNC_MVAR) && call.symb is VariableSymbol vs)
        {
          ValidateFuncCall(chain, chain_ctx, i, chain_ast.children.Count-1 == i, vs.type.Get() as FuncSignature, yielded);
        }
      }
    }
  }

  void ValidateFuncCall(IExpChain chain, ParserRuleContext chain_ctx, int idx, bool is_last, FuncSignature fsig, bool yielded)
  {
    if(PeekFuncDecl() == null)
      FireError(idx == 0 ? chain_ctx : chain.parseTree(idx-1), "function calls not allowed in global context");

    if(is_last)
    {
      if(!yielded && fsig.is_coro)
      {
        FireError(idx == 0 ? chain_ctx : chain.parseTree(idx-1), "coro function must be called via yield");
      }
      else if(yielded && !fsig.is_coro)
        FireError(idx == 0 ? chain_ctx : chain.parseTree(idx-1), "not a coro function");
    }
    else 
    {
      if(fsig.is_coro)
        FireError(idx == 0 ? chain_ctx : chain.parseTree(idx-1), "coro function must be called via yield");
    }
  }

  void TryProcessClassBaseCall(ref ITerminalNode curr_name, ref IScope scope, ref Symbol name_symb, ref int chain_offset, IExpChain chain, int line)
  {
    if(curr_name.GetText() == "base" && PeekFuncDecl()?.scope is ClassSymbol cs)
    {
      if(cs.super_class == null)
        FireError(curr_name, "no base class found");
      else
      {
        name_symb = cs.super_class; 
        scope = cs.super_class;
        if(chain.Length <= chain_offset)
          FireError(curr_name, "bad base call");
        var macc = chain.memberAccess(chain_offset);
        if(macc == null)
          FireError(chain.parseTree(chain_offset), "bad base call");
        curr_name = macc.NAME(); 
        ++chain_offset;

        PeekAST().AddChild(new AST_Call(EnumCall.VAR, line, PeekFuncDecl().Resolve("this")));
        PeekAST().AddChild(new AST_TypeCast(cs.super_class, true/*force type*/, line));
      }
    }
  }

  void TryApplyNamespaceOffset(ref ITerminalNode curr_name, ref IScope scope, ref Symbol name_symb, ref int chain_offset, IExpChain chain)
  {
    if(name_symb is Namespace ns && chain != null)
    {
      scope = ns;
      for(chain_offset=0; chain_offset<chain.Length; )
      {
        var macc = chain.memberAccess(chain_offset);
        if(macc == null)
          FireError(chain.parseTree(chain_offset), "bad chain call");
        name_symb = scope.ResolveWithFallback(macc.NAME().GetText());
        if(name_symb == null)
          FireError(macc.NAME(), "symbol '" + macc.NAME().GetText() + "' not resolved");
         curr_name = macc.NAME(); 
        ++chain_offset;
        if(name_symb is Namespace name_ns)
          scope = name_ns;
        else
          break;
      }
    }
  }

  Symbol ProcCallChainItem(
    IScope scope, 
    ITerminalNode name, 
    bhlParser.CallArgsContext cargs, 
    bhlParser.ArrAccessContext arracc, 
    ref IType type, 
    int line, 
    bool write,
    bool leftover = false
    )
  {
    AST_Call ast = null;

    Symbol name_symb = null;

    if(name != null)
    {
      name_symb = scope.ResolveWithFallback(name.GetText());
      if(name_symb == null)
        FireError(name, "symbol '" + name.GetText() + "' not resolved");

      var var_symb = name_symb as VariableSymbol;
      var func_symb = name_symb as FuncSymbol;
      var enum_symb = name_symb as EnumSymbol;
      var enum_item = name_symb as EnumItemSymbol;
      var class_symb = name_symb as ClassSymbol;

      //func or method call
      if(cargs != null)
      {
        if(var_symb is FieldSymbol && !(var_symb.type.Get() is FuncSignature))
          FireError(name, "symbol is not a function");

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
          FireError(name, "symbol is not a function");
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
            FireError(name, "attributes not supported by interfaces");

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
              FireError(name, "getting native class field by 'ref' not supported");
            ast.type = EnumCall.MVARREF; 
          }
          else if(fld_symb != null && scope is ClassSymbolNative)
          {
            if(ast.type == EnumCall.MVAR && fld_symb.getter == null)
              FireError(name, "get operation is not defined");
            else if(ast.type == EnumCall.MVARW && fld_symb.setter == null)
              FireError(name, "set operation is not defined");
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
          if(leftover)
            FireError(name, "symbol usage is not valid");
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
          FireError(name, "symbol usage is not valid");
      }
    }
    else if(cargs != null)
    {
      var ftype = type as FuncSignature;
      if(ftype == null)
        FireError(cargs, "no func to call");
      
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

      if(Wrap(arr_exp).eval_type != Types.Int)
        FireError(arr_exp, "array index expression is not of type int");

      type = arr_type.item_type.Get();

      var ast = new AST_Call(write ? EnumCall.ARR_IDXW : EnumCall.ARR_IDX, line, null);
      PeekAST().AddChild(ast);
    }
    else if(type is MapTypeSymbol map_type)
    {
      var arr_exp = arracc.exp();
      Visit(arr_exp);

      if(!Wrap(arr_exp).eval_type.Equals(map_type.key_type.Get()))
        FireError(arr_exp, "not compatible map key types");

      type = map_type.val_type.Get();

      var ast = new AST_Call(write ? EnumCall.MAP_IDXW : EnumCall.MAP_IDX, line, null);
      PeekAST().AddChild(ast);
    }
    else
      FireError(arracc, "accessing not an array/map type '" + type.GetName() + "'");
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
          FireError(ca_name, "no such named argument");

        if(norm_cargs[idx].ca != null)
          FireError(ca_name, "argument already passed before");
      }

      if(func_symb.attribs.HasFlag(FuncAttrib.VariadicArgs) && idx >= func_args.Count-1)
        variadic_args.Add(ca);
      else if(idx >= func_args.Count)
        FireError(ca, "there is no argument " + (idx + 1) + ", total arguments " + func_args.Count);
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
            FireError(next_arg, "missing argument '" + norm_cargs[i].orig.name + "'");
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
                FireError(next_arg, "max default arguments reached");
            }
            else
              FireError(next_arg, "missing argument '" + norm_cargs[i].orig.name + "'");
          }
        }
        else
        {
          if(na.ca.VARIADIC() != null)
            FireError(na.ca, "not variadic argument");

          prev_ca = na.ca;
          if(!args_info.IncArgsNum())
            FireError(na.ca, "max arguments reached");

          var func_arg_symb = (FuncArgSymbol)func_args[i];
          var func_arg_type = func_arg_symb.parsed == null ? func_arg_symb.type.Get() : func_arg_symb.parsed.eval_type;  

          bool is_ref = na.ca.isRef() != null;
          if(!is_ref && func_arg_symb.is_ref)
            FireError(na.ca, "'ref' is missing");
          else if(is_ref && !func_arg_symb.is_ref)
            FireError(na.ca, "argument is not a 'ref'");

          PushCallByRef(is_ref);
          PushJsonType(func_arg_type);
          PushAST(new AST_Interim());
          int old_ref_counter = ref_compatible_exp_counter;
          Visit(na.ca);

          if(is_ref && ref_compatible_exp_counter == old_ref_counter)
            FireError(na.ca, "expression is not passable by 'ref'");

          PopAddOptimizeAST();
          PopJsonType();
          PopCallByRef();

          var wca = Wrap(na.ca);

          if(func_arg_symb.type.Get() == null)
            FireError(na.ca, "unresolved type " + func_arg_symb.type);
          types.CheckAssign(func_arg_symb.type.Get(), wca);
        }
      }
      //checking variadic argument
      else
      {
        if(!args_info.IncArgsNum())
          FireError(na.ca, "max arguments reached");

        var func_arg_symb = (FuncArgSymbol)func_args[i];
        var func_arg_type = func_arg_symb.parsed == null ? func_arg_symb.type.Get() : func_arg_symb.parsed.eval_type;  

        var varg_arr_type = (ArrayTypeSymbol)func_arg_type;

        if(variadic_args.Count == 1 && variadic_args[0].VARIADIC() != null)
        {
          PushJsonType(varg_arr_type);
          Visit(variadic_args[0]);
          PopJsonType();

          types.CheckAssign(varg_arr_type, Wrap(variadic_args[0]));
        }
        else
        {
          var varg_type = varg_arr_type.item_type.Get();
          if(variadic_args.Count > 0 && varg_type == null)
            FireError(na.ca, "unresolved type " + varg_arr_type.item_type);
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

            types.CheckAssign(varg_type, Wrap(vca));
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
        FireError(next_arg, "missing argument of type '" + arg_type_ref.path + "'");
      }

      var ca = cargs.callArg()[i];
      var ca_name = ca.NAME();

      if(ca_name != null)
        FireError(ca_name, "named arguments not supported for function pointers");

      var arg_type = arg_type_ref.Get();
      PushJsonType(arg_type);
      PushAST(new AST_Interim());
      Visit(ca);
      PopAddOptimizeAST();
      PopJsonType();

      var wca = Wrap(ca);
      types.CheckAssign(arg_type is RefType rt ? rt.subj.Get() : arg_type, wca);

      if(arg_type_ref.Get() is RefType && ca.isRef() == null)
        FireError(ca, "'ref' is missing");
      else if(!(arg_type_ref.Get() is RefType) && ca.isRef() != null)
        FireError(ca, "argument is not a 'ref'");

      prev_ca = ca;
    }
    PopAST();

    if(ca_len != func_args.Count)
      FireError(cargs, "too many arguments");

    var args_info = new FuncArgsInfo();
    if(!args_info.SetArgsNum(func_args.Count))
      FireError(cargs, "max arguments reached");
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

  public override object VisitExpLambda(bhlParser.ExpLambdaContext ctx)
  {
    CommonVisitLambda(ctx, ctx.funcLambda(), yielded: false);
    return null;
  }

  public override object VisitExpYieldLambda(bhlParser.ExpYieldLambdaContext ctx)
  {
    CommonVisitLambda(ctx, ctx.funcLambda(), yielded: true);
    return null;
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
            FireError(vd.isRef(), "pass by ref not allowed");

          if(i != fparams.funcParamDeclare().Length-1)
            FireError(vd, "variadic argument must be last");

          if(vd.assignExp() != null)
            FireError(vd.assignExp(), "default argument is not allowed");
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
      FireError(parsed, "type '" + tp.path + "' not found");

    return tp;
  }

  Proxy<IType> ParseType(bhlParser.TypeContext ctx)
  {
    Proxy<IType> tp;
    if(ctx.funcType() == null)
    {
      if(ctx.nsName().GetText() == "var")
        FireError(ctx.nsName(), "invalid usage context");

      tp = curr_scope.R().T(ctx.nsName().GetText());
    }
    else
      tp = curr_scope.R().T(ParseFuncSignature(ctx.funcType()));

    if(ctx.ARR() != null)
    {
      if(tp.Get() == null)
        FireError(ctx.nsName(), "type '" + tp.path + "' not found");
      tp = curr_scope.R().TArr(tp);
    }
    else if(ctx.mapType() != null)
    {
      if(tp.Get() == null)
        FireError(ctx.nsName(), "type '" + tp.path + "' not found");
      var ktp = curr_scope.R().T(ctx.mapType().nsName().GetText());
      if(ktp.Get() == null)
        FireError(ctx.mapType().nsName(), "type '" + ktp.path + "' not found");
      tp = curr_scope.R().TMap(ktp, tp);
    }

    if(tp.Get() == null)
      FireError(ctx, "type '" + tp.path + "' not found");

   return tp;
  }

  void CommonVisitLambda(ParserRuleContext ctx, bhlParser.FuncLambdaContext funcLambda, bool yielded)
  {
    if(yielded)
      CheckCoroCallValidity(ctx);

    var tp = ParseType(funcLambda.retType());

    var func_name = Hash.CRC32(module.name) + "_lmb_" + funcLambda.Stop.Line;
    var upvals = new List<AST_UpVal>();
    var lmb_symb = new LambdaSymbol(
      Wrap(ctx), 
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
    Wrap(ctx).eval_type = lmb_symb.GetReturnType();

    ParseFuncBlock(funcLambda, funcLambda.funcBlock(), funcLambda.retType(), ast);

    //NOTE: once we are out of lambda the eval type is the lambda itself
    var curr_type = (IType)lmb_symb.signature;
    Wrap(ctx).eval_type = curr_type;

    PopScope();

    //NOTE: since lambda func symbol is currently compile-time only,
    //      we need to reflect local variables number in AST
    //      (for regular funcs this number is taken from a symbol)
    ast.local_vars_num = lmb_symb.local_vars_num;

    var chain = funcLambda.chainExp(); 
    if(chain != null)
    {
      var interim = new AST_Interim();
      interim.AddChild(ast);
      PushAST(interim);
      ProcChainedCall(curr_scope, null, new ExpChain(chain), ref curr_type, funcLambda);
      PopAST();
      Wrap(ctx).eval_type = curr_type;
      PeekAST().AddChild(interim);
    }
    else
      PeekAST().AddChild(ast);
  }
  
  public override object VisitCallArg(bhlParser.CallArgContext ctx)
  {
    var exp = ctx.exp();
    Visit(exp);
    Wrap(ctx).eval_type = Wrap(exp).eval_type;
    return null;
  }

  public override object VisitExpJsonObj(bhlParser.ExpJsonObjContext ctx)
  {
    var json = ctx.jsonObject();

    Visit(json);
    Wrap(ctx).eval_type = Wrap(json).eval_type;
    return null;
  }

  public override object VisitExpJsonArr(bhlParser.ExpJsonArrContext ctx)
  {
    var json = ctx.jsonArray();
    Visit(json);
    Wrap(ctx).eval_type = Wrap(json).eval_type;
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
      FireError(ctx, "{..} not expected");

    if(!(curr_type is ClassSymbol) || 
        (curr_type is ArrayTypeSymbol) ||
        (curr_type is MapTypeSymbol)
        )
      FireError(ctx, "type '" + curr_type + "' can't be specified with {..}");

    if(curr_type is ClassSymbolNative csn && csn.creator == null)
      FireError(ctx, "constructor is not defined");

    Wrap(ctx).eval_type = curr_type;

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
      FireError(ctx, "[..] not expected");

    if(!(curr_type is ArrayTypeSymbol) && !(curr_type is MapTypeSymbol))
      FireError(ctx, "type '" + curr_type + "' can't be specified with [..]");

    if(curr_type is ArrayTypeSymbol arr_type)
    {
      var orig_type = arr_type.item_type.Get();
      if(orig_type == null)
        FireError(ctx,  "type '" + arr_type.item_type.path + "' not found");
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

      Wrap(ctx).eval_type = arr_type;

      PeekAST().AddChild(ast);
    }
    else if(curr_type is MapTypeSymbol map_type)
    {
      var key_type = map_type.key_type.Get();
      if(key_type == null)
        FireError(ctx,  "type '" + map_type.key_type.path + "' not found");
      var val_type = map_type.val_type.Get();
      if(val_type == null)
        FireError(ctx,  "type '" + map_type.val_type.path + "' not found");
      var ast = new AST_JsonMap(curr_type, ctx.Start.Line);

      PushAST(ast);
      var vals = ctx.jsonValue();
      for(int i=0;i<vals.Length;++i)
      {
        var val = vals[i].exp() as bhlParser.ExpJsonArrContext;
        if(val?.jsonArray()?.jsonValue()?.Length != 2)
          FireError(val == null ? (IParseTree)ctx : (IParseTree)val,  "[k, v] expected");

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


      Wrap(ctx).eval_type = map_type;

      PeekAST().AddChild(ast);
    }
    return null;
  }

  public override object VisitJsonPair(bhlParser.JsonPairContext ctx)
  {
    var curr_type = PeekJsonType();
    var scoped_symb = curr_type as ClassSymbol;
    if(scoped_symb == null)
      FireError(ctx, "expecting class type, got '" + curr_type + "' instead");

    var name_str = ctx.NAME().GetText();
    
    var member = scoped_symb.ResolveWithFallback(name_str) as VariableSymbol;
    if(member == null)
      FireError(ctx, "no such attribute '" + name_str + "' in class '" + scoped_symb.name + "'");

    var ast = new AST_JsonPair(curr_type, name_str, member.scope_idx);

    PushJsonType(member.type.Get());

    var jval = ctx.jsonValue(); 
    PushAST(ast);
    Visit(jval);
    PopAST();

    PopJsonType();

    Wrap(ctx).eval_type = member.type.Get();

    PeekAST().AddChild(ast);
    return null;
  }

  public override object VisitJsonValue(bhlParser.JsonValueContext ctx)
  {
    var exp = ctx.exp();

    var curr_type = PeekJsonType();
    Visit(exp);
    Wrap(ctx).eval_type = Wrap(exp).eval_type;

    types.CheckAssign(curr_type, Wrap(exp));

    return null;
  }

  public override object VisitExpTypeof(bhlParser.ExpTypeofContext ctx)
  {
    var tp = ParseType(ctx.@typeof().type());

    Wrap(ctx).eval_type = Types.ClassType;

    PeekAST().AddChild(new AST_Typeof(tp.Get()));

    return null;
  }

  public override object VisitExpCall(bhlParser.ExpCallContext ctx)
  {
    var exp = ctx.callExp(); 
    Visit(exp);
    Wrap(ctx).eval_type = Wrap(exp).eval_type;

    ++ref_compatible_exp_counter;

    return null;
  }

  public override object VisitExpYieldCall(bhlParser.ExpYieldCallContext ctx)
  {
    var exp = ctx.funcCallExp();
    CommonYieldFuncCall(ctx, exp);
    Wrap(ctx).eval_type = Wrap(exp).eval_type;

    return null;
  }

  void CheckCoroCallValidity(ParserRuleContext ctx)
  {
    var curr_func = PeekFuncDecl();
    if(!curr_func.attribs.HasFlag(FuncAttrib.Coro))
      FireError(curr_func.parsed.tree, "function with yield calls must be coro");

    if(GetBlockLevel(BlockType.DEFER) != -1)
      FireError(ctx, "yield is not allowed in defer block");

    has_yield_calls.Add(curr_func);
  }

  void CommonYieldFuncCall(ParserRuleContext ctx, bhlParser.FuncCallExpContext fn_call)
  {
    CheckCoroCallValidity(ctx);

    var chain = new ExpChainExtraCall(new ExpChain(fn_call.callExp().chainExp()), fn_call.callArgs());

    IType curr_type = null;
    ProcChainedCall(
      fn_call.callExp().GLOBAL() != null ? ns : curr_scope, 
      fn_call.callExp().NAME(), 
      chain, 
      ref curr_type, 
      fn_call.callExp(), 
      yielded: true
    );

    Wrap(fn_call).eval_type = curr_type;
  }

  public override object VisitExpNew(bhlParser.ExpNewContext ctx)
  {
    var tp = ParseType(ctx.newExp().type());
    var cl = tp.Get();
    Wrap(ctx).eval_type = cl;

    if(cl is ClassSymbolNative csn && csn.creator == null)
      FireError(ctx, "constructor is not defined");

    var ast = new AST_New(cl, ctx.Start.Line);
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitAssignExp(bhlParser.AssignExpContext ctx)
  {
    var exp = ctx.exp();
    Visit(exp);
    Wrap(ctx).eval_type = Wrap(exp).eval_type;

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

    Wrap(ctx).eval_type = tp.Get();

    Types.CheckCast(Wrap(ctx), Wrap(exp)); 

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

    Wrap(ctx).eval_type = tp.Get();

    //TODO: do we need to pre-check absolutely unrelated types?
    //types.CheckCast(Wrap(ctx), Wrap(exp)); 

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

    Wrap(ctx).eval_type = Types.Bool;

    //TODO: do we need to pre-check absolutely unrelated types?
    //types.CheckCast(Wrap(ctx), Wrap(exp)); 

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

    Wrap(ctx).eval_type = type == EnumUnaryOp.NEG ? 
      types.CheckUnaryMinus(Wrap(exp)) : 
      types.CheckLogicalNot(Wrap(exp));

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpParen(bhlParser.ExpParenContext ctx)
  {
    var ast = new AST_Interim();
    var exp = ctx.exp(); 
    PushAST(ast);
    Visit(exp);

    var curr_type = Wrap(exp).eval_type;
    var chain = ctx.chainExp(); 
    if(chain != null)
    {
      ProcChainedCall(curr_scope, null, new ExpChain(chain), ref curr_type, exp);
      ++ref_compatible_exp_counter;
    }
    PopAST();
    
    PeekAST().AddChild(ast);
    
    Wrap(ctx).eval_type = curr_type;

    return null;
  }

  public override object VisitExpYieldParen(bhlParser.ExpYieldParenContext ctx)
  {
    CheckCoroCallValidity(ctx);

    var ast = new AST_Interim();
    var exp = ctx.exp(); 
    PushAST(ast);
    Visit(exp);

    var curr_type = Wrap(exp).eval_type;
    var chain = new ExpChainExtraCall(new ExpChain(ctx.chainExp()), ctx.callArgs());
    ProcChainedCall(curr_scope, null, chain, ref curr_type, exp, yielded: true);
    PopAST();
    
    PeekAST().AddChild(ast);
    
    Wrap(ctx).eval_type = curr_type;

    return null;
  }

  public override object VisitVarPostOpAssign(bhlParser.VarPostOpAssignContext ctx)
  {
    string post_op = ctx.operatorPostOpAssign().GetText();
    CommonVisitBinOp(ctx, post_op.Substring(0, 1), ctx.callExp(), ctx.exp());

     //NOTE: if expression starts with '..' we consider the global namespace instead of current scope
     IType curr_type = null;
     ProcChainedCall(
      ctx.callExp().GLOBAL() != null ? ns : curr_scope, 
      ctx.callExp().NAME(), 
      new ExpChain(ctx.callExp().chainExp()), 
      ref curr_type, 
      ctx, 
      write: true
     );

    if(curr_type == Types.String && post_op == "+=")
      return null;

    if(!Types.IsNumeric(curr_type))
      FireError(ctx, "is not numeric type");

    return null;
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
  
  public override object VisitVarPostIncDec(bhlParser.VarPostIncDecContext ctx)
  {
    CommonVisitPostIncDec(ctx.callPostIncDec());
    return null;
  }

  bhlParser.ExpContext _one_literal_exp;
  bhlParser.ExpContext one_literal_exp {
    get {
      if(_one_literal_exp == null)
      {
        _one_literal_exp = Stream2Parser("", new MemoryStream(System.Text.Encoding.UTF8.GetBytes("1"))).exp();
      }
      return _one_literal_exp;
    }
  }

  void CommonVisitPostIncDec(bhlParser.CallPostIncDecContext ctx)
  {
    //let's tweak the fake "1" expression placement
    //by assinging it the call expression placement
    var wone = Wrap(one_literal_exp);
    var wcall = Wrap(ctx.callExp());
    wone.module = wcall.module;
    wone.tree = wcall.tree;
    wone.tokens = wcall.tokens;

    if(ctx.incrementOperator() != null)
      CommonVisitBinOp(ctx, "+", ctx.callExp(), one_literal_exp);
    else if(ctx.decrementOperator() != null)
      CommonVisitBinOp(ctx, "-", ctx.callExp(), one_literal_exp);
    else
      FireError(ctx, "unknown operator");

     //NOTE: if expression starts with '..' we consider the global namespace instead of current scope
     IType curr_type = null;
     ProcChainedCall(
      ctx.callExp().GLOBAL() != null ? ns : curr_scope, 
      ctx.callExp().NAME(), 
      new ExpChain(ctx.callExp().chainExp()), 
      ref curr_type, 
      ctx, 
      write: true
    );

    if(!Types.IsNumeric(curr_type))
      FireError(ctx, "only numeric types supported");
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

    var wlhs = Wrap(lhs);
    var wrhs = Wrap(rhs);

    var class_symb = wlhs.eval_type as ClassSymbol;
    //NOTE: checking if there's binary operator overload
    if(class_symb != null && class_symb.Resolve(op) is FuncSymbol)
    {
      var op_func = class_symb.Resolve(op) as FuncSymbol;

      Wrap(ctx).eval_type = types.CheckBinOpOverload(ns, wlhs, wrhs, op_func);

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
      Wrap(ctx).eval_type = types.CheckEqBinOp(wlhs, wrhs);
    else if(
      op_type == EnumBinaryOp.GT || 
      op_type == EnumBinaryOp.GTE ||
      op_type == EnumBinaryOp.LT || 
      op_type == EnumBinaryOp.LTE
    )
      Wrap(ctx).eval_type = types.CheckRtlBinOp(wlhs, wrhs);
    else
      Wrap(ctx).eval_type = types.CheckBinOp(wlhs, wrhs);

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

    Wrap(ctx).eval_type = types.CheckBitOp(Wrap(exp_0), Wrap(exp_1));

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

    Wrap(ctx).eval_type = types.CheckBitOp(Wrap(exp_0), Wrap(exp_1));

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

    Wrap(ctx).eval_type = types.CheckLogicalOp(Wrap(exp_0), Wrap(exp_1));

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

    Wrap(ctx).eval_type = types.CheckLogicalOp(Wrap(exp_0), Wrap(exp_1));

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
      Wrap(ctx).eval_type = Types.Int;
      ast.nval = double.Parse(int_num.GetText(), System.Globalization.CultureInfo.InvariantCulture);
    }
    else if(flt_num != null)
    {
      ast = new AST_Literal(ConstType.FLT);
      Wrap(ctx).eval_type = Types.Float;
      ast.nval = double.Parse(flt_num.GetText(), System.Globalization.CultureInfo.InvariantCulture);
    }
    else if(hex_num != null)
    {
      ast = new AST_Literal(ConstType.INT);
      Wrap(ctx).eval_type = Types.Int;
      ast.nval = Convert.ToUInt32(hex_num.GetText(), 16);
    }
    else
      FireError(ctx, "unknown numeric literal type");

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralFalse(bhlParser.ExpLiteralFalseContext ctx)
  {
    Wrap(ctx).eval_type = Types.Bool;

    var ast = new AST_Literal(ConstType.BOOL);
    ast.nval = 0;
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralNull(bhlParser.ExpLiteralNullContext ctx)
  {
    Wrap(ctx).eval_type = Types.Null;

    var ast = new AST_Literal(ConstType.NIL);
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralTrue(bhlParser.ExpLiteralTrueContext ctx)
  {
    Wrap(ctx).eval_type = Types.Bool;

    var ast = new AST_Literal(ConstType.BOOL);
    ast.nval = 1;
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralStr(bhlParser.ExpLiteralStrContext ctx)
  {
    Wrap(ctx).eval_type = Types.String;

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

  public override object VisitReturn(bhlParser.ReturnContext ctx)
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
      PeekAST().AddChild(CommonDeclVar(curr_scope, vd.NAME(), vd.type(), is_ref: false, func_arg: false, write: false));
      return null;
    }

    //NOTE: special handling of the following case:
    //
    //      return
    //      int foo = 1
    //
    if(ret_val?.varsDeclareAssign() != null)
    {
      var vdecls = ret_val.varsDeclareAssign().varsDeclareOrCallExps().varDeclareOrCallExp();
      var assign_exp = ret_val.varsDeclareAssign().assignExp();
      CommonDeclOrAssign(vdecls, assign_exp, ctx.Start.Line);
      return null;
    }

    if(CountBlocks(BlockType.DEFER) > 0)
      FireError(ctx, "return is not allowed in defer block");

    var func_symb = PeekFuncDecl();
    if(func_symb == null)
      FireError(ctx, "return statement is not in function");
    
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

        if(Wrap(exp_item).eval_type != Types.Void)
          ret_ast.num = fmret_type != null ? fmret_type.Count : 1;

        types.CheckAssign(func_symb.parsed, Wrap(exp_item));
        Wrap(ctx).eval_type = Wrap(exp_item).eval_type;
      }
      else
      {
        if(fmret_type == null)
          FireError(ctx, "function doesn't support multi return");

        if(fmret_type.Count != explen)
          FireError(ctx, "multi return size doesn't match destination");

        var ret_type = new TupleType();

        //NOTE: we traverse expressions in reversed order so that returned
        //      values are properly placed on a stack
        for(int i=explen;i-- > 0;)
        {
          var exp = ret_val.exps().exp()[i];
          Visit(exp);
          ret_type.Add(curr_scope.R().T(Wrap(exp).eval_type));
        }

        //type checking is in proper order
        for(int i=0;i<explen;++i)
        {
          var exp = ret_val.exps().exp()[i];
          types.CheckAssign(fmret_type[i].Get(), Wrap(exp));
        }

        Wrap(ctx).eval_type = ret_type;

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
        FireError(ctx, "return value is missing");
      Wrap(ctx).eval_type = Types.Void;
      PeekAST().AddChild(ret_ast);
    }

    return null;
  }

  public override object VisitBreak(bhlParser.BreakContext ctx)
  {
    int loop_level = GetLoopBlockLevel();

    if(loop_level == -1)
      FireError(ctx, "not within loop construct");

    if(GetBlockLevel(BlockType.DEFER) > loop_level)
      FireError(ctx, "not within loop construct");

    PeekAST().AddChild(new AST_Break());

    return null;
  }

  public override object VisitContinue(bhlParser.ContinueContext ctx)
  {
    int loop_level = GetLoopBlockLevel();

    if(loop_level == -1)
      FireError(ctx, "not within loop construct");

    if(GetBlockLevel(BlockType.DEFER) > loop_level)
      FireError(ctx, "not within loop construct");

    PeekAST().AddChild(new AST_Continue());

    return null;
  }

  public override object VisitDecls(bhlParser.DeclsContext ctx)
  {
    var decls = ctx.decl();
    for(int i=0;i<decls.Length;++i)
    {
      var nsdecl = decls[i].nsDecl();
      if(nsdecl != null)
      {
        Visit(nsdecl);
        continue;
      }
      var vdecl = decls[i].varDeclareAssign();
      if(vdecl != null)
      {
        AddPass(vdecl, curr_scope, PeekAST()); 
        continue;
      }
      var fndecl = decls[i].funcDecl();
      if(fndecl != null)
      {
        AddPass(fndecl, curr_scope, PeekAST());
        continue;
      }
      var cldecl = decls[i].classDecl();
      if(cldecl != null)
      {
        AddPass(cldecl, curr_scope, PeekAST());
        continue;
      }
      var ifacedecl = decls[i].interfaceDecl();
      if(ifacedecl != null)
      {
        AddPass(ifacedecl, curr_scope, PeekAST());
        continue;
      }
      var edecl = decls[i].enumDecl();
      if(edecl != null)
      {
        AddPass(edecl, curr_scope, PeekAST());
        continue;
      }
    }

    return null;
  }

  void Pass_OutlineFuncDecl(ParserPass pass)
  {
    if(pass.func_ctx == null)
      return;

    string name = pass.func_ctx.NAME().GetText();

    if(pass.func_ctx.funcAttribs().Length > 0 && pass.func_ctx.funcAttribs()[0].coroFlag() == null)
      FireError(pass.func_ctx.funcAttribs()[0], "improper usage of attribute");

    pass.func_symb = new FuncSymbolScript(
      Wrap(pass.func_ctx), 
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

    Wrap(pass.func_ctx).eval_type = pass.func_symb.GetReturnType();
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
      FireError(ret_ctx, "matching 'return' statement not found");

    if(func_ast.symbol.attribs.HasFlag(FuncAttrib.Coro) && !has_yield_calls.Contains(func_ast.symbol))
      FireError(ctx, "coro functions without yield calls not allowed");
  }

  void Pass_OutlineGlobalVar(ParserPass pass)
  {
    if(pass.gvar_ctx == null)
      return;

    var vd = pass.gvar_ctx.varDeclare(); 

    pass.gvar_symb = new VariableSymbol(Wrap(vd.NAME()), vd.NAME().GetText(), new Proxy<IType>());

    curr_scope.Define(pass.gvar_symb);
  }

  void Pass_OutlineInterfaceDecl(ParserPass pass)
  {
    if(pass.iface_ctx == null)
      return;

    var name = pass.iface_ctx.NAME().GetText();

    pass.iface_symb = new InterfaceSymbolScript(Wrap(pass.iface_ctx), name);

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
          FireError(fd.funcParams().funcParamDeclare()[sig.arg_types.Count - default_args_num], "default argument value is not allowed in this context");

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
            FireError(ext_name, "self inheritance is not allowed");

          if(inherits.IndexOf(ifs) != -1)
            FireError(ext_name, "interface is inherited already");
          inherits.Add(ifs);
        }
        else
          FireError(ext_name, "not a valid interface");
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

    pass.class_symb = new ClassSymbolScript(Wrap(pass.class_ctx), name);
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
          FireError(vd.NAME(), "the keyword \"this\" is reserved");

        var fld_symb = new FieldSymbolScript(Wrap(vd), vd.NAME().GetText(), new Proxy<IType>());

        for(int f=0;f<fldd.fldAttribs().Length;++f)
        {
          var attr = fldd.fldAttribs()[f];
          var attr_type = FieldAttrib.None;

          if(attr.staticFlag() != null)
            attr_type = FieldAttrib.Static;

          if(fld_symb.attribs.HasFlag(attr_type))
            FireError(attr, "this attribute is set already");

          fld_symb.attribs |= attr_type;
        }

        pass.class_symb.Define(fld_symb);
      }

      var fd = cm.funcDecl();
      if(fd != null)
      {
        if(fd.NAME().GetText() == "this")
          FireError(fd.NAME(), "the keyword \"this\" is reserved");

        var func_symb = new FuncSymbolScript(
            Wrap(fd), 
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
            FireError(attr, "this attribute is set already");

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
        fld_symb.type = ParseType(vd.type());
      }

      var fd = cm.funcDecl();
      if(fd != null)
      {
        var func_symb = (FuncSymbolScript)pass.class_symb.members.Find(fd.NAME().GetText());

        func_symb.signature = ParseFuncSignature(
          fd.funcAttribs().Length > 0 && fd.funcAttribs()[0].coroFlag() != null, 
          ParseType(fd.retType()), 
          fd.funcParams()
        );

        var func_ast = pass.class_ast.FindFuncDecl(func_symb);
        ParseFuncParams(fd, func_ast);

        Wrap(fd).eval_type = func_symb.GetReturnType(); 
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
            FireError(ext_name, "self inheritance is not allowed");

          if(super_class != null)
            FireError(ext_name, "only one parent class is allowed");

          if(cs is ClassSymbolNative)
            FireError(ext_name, "extending native classes is not supported");

          super_class = cs;
        }
        else if(ext is InterfaceSymbol ifs)
        {
          if(implements.IndexOf(ifs) != -1)
            FireError(ext_name, "interface is implemented already");

          if(ifs is InterfaceSymbolNative)
            FireError(ext_name, "implementing native interfaces is not supported");

          implements.Add(ifs);
        }
        else
          FireError(ext_name, "not a class or an interface");
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
    var symb = new EnumSymbolScript(Wrap(ctx), enum_name);
    curr_scope.Define(symb);

    for(int i=0;i<ctx.enumBlock().enumMember().Length;++i)
    {
      var em = ctx.enumBlock().enumMember()[i];
      var em_name = em.NAME().GetText();
      int em_val = int.Parse(em.INT().GetText(), System.Globalization.CultureInfo.InvariantCulture);

      int res = symb.TryAddItem(em_name, em_val);
      if(res == 1)
        FireError(em.NAME(), "duplicate key '" + em_name + "'");
      else if(res == 2)
        FireError(em.INT(), "duplicate value '" + em_val + "'");
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
      types.CheckAssign(Wrap(vd.NAME()), Wrap(assign_exp));

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
          FireError(fp.NAME(), "default argument values not allowed for lambdas");

        found_default_arg = true;
      }
      else if(found_default_arg && fp.VARIADIC() == null)
        FireError(fp.NAME(), "missing default argument expression");

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

  public override object VisitVarDecl(bhlParser.VarDeclContext ctx)
  {
    var vd = ctx.varDeclare(); 
    PeekAST().AddChild(CommonDeclVar(curr_scope, vd.NAME(), vd.type(), is_ref: false, func_arg: false, write: false));
    return null;
  }

  void CommonDeclOrAssign(bhlParser.VarDeclareOrCallExpContext[] vdecls, bhlParser.AssignExpContext assign_exp, int start_line)
  {
    var root = PeekAST();
    int root_first_idx = root.children.Count;

    IType assign_type = null;
    for(int i=0;i<vdecls.Length;++i)
    {
      var vdecl_tmp = vdecls[i];
      var cexp = vdecl_tmp.callExp();
      var vd = vdecl_tmp.varDeclare();

      WrappedParseTree var_ptree = null;
      IType curr_type = null;
      bool is_decl = false;
      bool is_auto_var = false;

      if(cexp != null)
      {
        if(assign_exp == null)
          FireError(cexp, "assign expression expected");
        else if(cexp.chainExp()?.Length > 0 && cexp.chainExp()[cexp.chainExp().Length-1].callArgs() != null)
          FireError(assign_exp, "invalid assignment");

        //NOTE: if expression starts with '..' we consider the global namespace instead of current scope
        ProcChainedCall(
          cexp.GLOBAL() != null ? ns : curr_scope, 
          cexp.NAME(), 
          new ExpChain(cexp.chainExp()), 
          ref curr_type, 
          cexp, 
          write: true
        );

        var_ptree = Wrap(cexp.NAME());
        var_ptree.eval_type = curr_type;
      }
      else 
      {
        var vd_type = vd.type();

        //check if we declare a var or use an existing one
        if(vd_type == null)
        {
          string vd_name = vd.NAME().GetText(); 
          var vd_symb = curr_scope.ResolveWithFallback(vd_name) as VariableSymbol;
          if(vd_symb == null)
            FireError(vd, "symbol '" + vd_name + "' not resolved");
          curr_type = vd_symb.type.Get();

          var_ptree = Wrap(vd.NAME());
          var_ptree.eval_type = curr_type;

          var ast = new AST_Call(EnumCall.VARW, start_line, vd_symb);
          root.AddChild(ast);
        }
        else
        {
          is_auto_var = vd_type.GetText() == "var";

          if(is_auto_var && assign_exp == null)
            FireError(vd_type, "invalid usage context");
          else if(is_auto_var && assign_exp?.GetText() == "=null")
            FireError(vd_type, "invalid usage context");

          var ast = CommonDeclVar(
            curr_scope, 
            vd.NAME(), 
            //NOTE: in case of 'var' let's temporarily declare var as 'any',
            //      below we'll setup the proper type
            is_auto_var ? Types.Any : ParseType(vd_type), 
            is_ref: false, 
            func_arg: false, 
            write: assign_exp != null
          );
          root.AddChild(ast);

          is_decl = true;

          var_ptree = Wrap(vd.NAME()); 
          curr_type = var_ptree.eval_type;
        }
      }

      //NOTE: if there is an assignment we have to visit it and push current
      //      json type if required
      if(assign_exp != null && assign_type == null)
      {
        //NOTE: look forward at expression and push json type 
        //      if it's a json-init-expression
        bool pop_json_type = false;
        if((assign_exp.exp() is bhlParser.ExpJsonObjContext || 
          assign_exp.exp() is bhlParser.ExpJsonArrContext))
        {
          if(vdecls.Length != 1)
            FireError(assign_exp, "multi assign not supported for JSON expression");

          pop_json_type = true;

          PushJsonType(curr_type);
        }

        //NOTE: temporarily replacing just declared variable with the dummy one when visiting 
        //      assignment expression in order to avoid error like: float k = k
        VariableSymbol disabled_symbol = null;
        VariableSymbol subst_symbol = null;
        if(is_decl)
        {
          var symbols = ((LocalScope)curr_scope).members;
          disabled_symbol = (VariableSymbol)symbols[symbols.Count - 1];
          subst_symbol = DisableVar(symbols, disabled_symbol);
        }

        //NOTE: need to put expression nodes first
        var stash = new AST_Interim();
        PushAST(stash);
        Visit(assign_exp);
        PopAST();
        for(int s=stash.children.Count;s-- > 0;)
          root.children.Insert(root_first_idx, stash.children[s]);

        assign_type = Wrap(assign_exp).eval_type;

        //NOTE: declaring disabled symbol again
        if(disabled_symbol != null)
        {
          var symbols = ((LocalScope)curr_scope).members;
          EnableVar(symbols, disabled_symbol, subst_symbol);
        }

        if(pop_json_type)
          PopJsonType();

        var assign_tuple = assign_type as TupleType; 
        if(vdecls.Length > 1)
        {
          if(assign_tuple == null)
            FireError(assign_exp, "multi return expected");

          if(assign_tuple.Count != vdecls.Length)
            FireError(assign_exp, "multi return size doesn't match destination");
        }
        else if(assign_tuple != null)
          FireError(assign_exp, "multi return size doesn't match destination");
      }

      if(assign_type != null)
      {
        if(assign_type is TupleType assign_tuple)
        {
          //NOTE: let's setup the proper 'var' type
          if(is_auto_var)
          {
            var symbols = ((LocalScope)curr_scope).members;
            var var_symbol = (VariableSymbol)symbols[symbols.Count - 1];
            var_symbol.type = new Proxy<IType>(assign_tuple[i].Get());
          }
          types.CheckAssign(var_ptree, assign_tuple[i].Get());
        }
        else
        {
          //NOTE: let's setup the proper 'var' type
          if(is_auto_var)
          {
            var symbols = ((LocalScope)curr_scope).members;
            var var_symbol = (VariableSymbol)symbols[symbols.Count - 1];
            var_symbol.type = new Proxy<IType>(assign_type);
          }
          types.CheckAssign(var_ptree, Wrap(assign_exp));
        }
      }
    }
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

  public override object VisitDeclAssign(bhlParser.DeclAssignContext ctx)
  {
    var vdecls = ctx.varsDeclareAssign().varsDeclareOrCallExps().varDeclareOrCallExp();
    var assign_exp = ctx.varsDeclareAssign().assignExp();

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
        FireError(name, "'ref' is not allowed to have a default value");
    }

    AST_Interim exp_ast = null;
    if(assign_exp != null)
    {
      exp_ast = new AST_Interim();
      PushAST(exp_ast);
      Visit(assign_exp);
      PopAST();
    }

    var ast = CommonDeclVar(curr_scope, name, ctx.type(), is_ref, func_arg: true, write: false);
    if(exp_ast != null)
      ast.AddChild(exp_ast);
    PeekAST().AddChild(ast);

    if(assign_exp != null && !is_null_ref)
      types.CheckAssign(Wrap(name), Wrap(assign_exp));
    return null;
  }

  AST_Tree CommonDeclVar(IScope curr_scope, ITerminalNode name, bhlParser.TypeContext type_ctx, bool is_ref, bool func_arg, bool write)
  {
    return CommonDeclVar(curr_scope, name, ParseType(type_ctx), is_ref, func_arg, write);
  }

  AST_Tree CommonDeclVar(IScope curr_scope, ITerminalNode name, Proxy<IType> tp, bool is_ref, bool func_arg, bool write)
  {
    if(name.GetText() == "base" && PeekFuncDecl()?.scope is ClassSymbol)
      FireError(name, "keyword 'base' is reserved");

    var var_tree = Wrap(name); 
    var_tree.eval_type = tp.Get();

    if(is_ref && !func_arg)
      FireError(name, "'ref' is only allowed in function declaration");

    VariableSymbol symb = func_arg ? 
      (VariableSymbol) new FuncArgSymbol(var_tree, name.GetText(), tp, is_ref) :
      new VariableSymbol(var_tree, name.GetText(), tp);

    curr_scope.Define(symb);

    if(write)
      return new AST_Call(EnumCall.VARW, name.Symbol.Line, symb);
    else
      return new AST_VarDecl(symb, is_ref);
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

  public override object VisitParal(bhlParser.ParalContext ctx)
  {
    CommonVisitBlock(BlockType.PARAL, ctx.block().statement());
    return null;
  }

  public override object VisitParalAll(bhlParser.ParalAllContext ctx)
  {
    CommonVisitBlock(BlockType.PARAL_ALL, ctx.block().statement());
    return null;
  }

  public override object VisitDefer(bhlParser.DeferContext ctx)
  {
    if(CountBlocks(BlockType.DEFER) > 0)
      FireError(ctx, "nested defers are not allowed");
    CommonVisitBlock(BlockType.DEFER, ctx.block().statement());
    return null;
  }

  public override object VisitIf(bhlParser.IfContext ctx)
  {
    var ast = new AST_Block(BlockType.IF);

    var main = ctx.mainIf();

    var main_cond = new AST_Block(BlockType.SEQ);
    PushAST(main_cond);
    Visit(main.exp());
    PopAST();

    types.CheckAssign(Types.Bool, Wrap(main.exp()));

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

      types.CheckAssign(Types.Bool, Wrap(item.exp()));

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

    types.CheckAssign(Types.Bool, Wrap(exp_0));

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

    var wrap_exp_1 = Wrap(exp_1);
    Wrap(ctx).eval_type = wrap_exp_1.eval_type;

    types.CheckAssign(wrap_exp_1, Wrap(exp_2));
    PeekAST().AddChild(ast);
    return null;
  }

  public override object VisitWhile(bhlParser.WhileContext ctx)
  {
    var ast = new AST_Block(BlockType.WHILE);

    PushBlock(ast);

    var cond = new AST_Block(BlockType.SEQ);
    PushAST(cond);
    Visit(ctx.exp());
    PopAST();

    types.CheckAssign(Types.Bool, Wrap(ctx.exp()));

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

  public override object VisitDoWhile(bhlParser.DoWhileContext ctx)
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

    types.CheckAssign(Types.Bool, Wrap(ctx.exp()));

    ast.AddChild(cond);

    PeekAST().AddChild(ast);

    PopBlock(ast);

    return_found.Remove(PeekFuncDecl());

    return null;
  }

  public override object VisitFor(bhlParser.ForContext ctx)
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
    
    var for_pre = ctx.forExp().forPre();
    if(for_pre != null)
    {
      var for_pre_stmts = for_pre.forStmts();
      for(int i=0;i<for_pre_stmts.forStmt().Length;++i)
      {
        var stmt = for_pre_stmts.forStmt()[i];
        var vdoce = stmt.varsDeclareOrCallExps();
        
        if(vdoce != null)
        {
          var pre_vdecls = vdoce.varDeclareOrCallExp();
          var pre_assign_exp = stmt.assignExp();
          CommonDeclOrAssign(pre_vdecls, pre_assign_exp, ctx.Start.Line);
        }
        else
        {
          var cpo = stmt.callPostIncDec();
          if(cpo != null)
            CommonVisitPostIncDec(cpo);
        }
      }
    }

    var for_cond = ctx.forExp().forCond();
    var for_post_iter = ctx.forExp().forPostIter();

    var ast = new AST_Block(BlockType.WHILE);

    PushBlock(ast);

    var cond = new AST_Block(BlockType.SEQ);
    PushAST(cond);
    Visit(for_cond);
    PopAST();

    types.CheckAssign(Types.Bool, Wrap(for_cond.exp()));

    ast.AddChild(cond);

    PushAST(ast);
    var block = CommonVisitBlock(BlockType.SEQ, ctx.block().statement());
    //appending post iteration code
    if(for_post_iter != null)
    {
      PushAST(block);
      
      PeekAST().AddChild(new AST_Continue(jump_marker: true));
      var for_post_iter_stmts = for_post_iter.forStmts();
      for(int i=0;i<for_post_iter_stmts.forStmt().Length;++i)
      {
        var stmt = for_post_iter_stmts.forStmt()[i];
        var vdoce = stmt.varsDeclareOrCallExps();
        
        if(vdoce != null)
        {
          var post_vdecls = vdoce.varDeclareOrCallExp();
          var post_assign_exp = stmt.assignExp();
          CommonDeclOrAssign(post_vdecls, post_assign_exp, ctx.Start.Line);
        }
        else
        {
          var cpo = stmt.callPostIncDec();
          if(cpo != null)
            CommonVisitPostIncDec(cpo);
        }
      }
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

  public override object VisitYield(bhlParser.YieldContext ctx)
  {
    CheckCoroCallValidity(ctx);

    int line = ctx.Start.Line;
    var ast = new AST_Yield(line);
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitYieldFunc(bhlParser.YieldFuncContext ctx)
  {
    CommonYieldFuncCall(ctx, ctx.funcCallExp());

    return null;
  }

  public override object VisitYieldWhile(bhlParser.YieldWhileContext ctx)
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

    types.CheckAssign(Types.Bool, Wrap(ctx.exp()));

    ast.AddChild(cond);

    var body = new AST_Block(BlockType.SEQ);
    int line = ctx.Start.Line;
    body.AddChild(new AST_Yield(line));
    ast.AddChild(body);

    PopBlock(ast);

    PeekAST().AddChild(ast);
    return null;
  }

  public override object VisitForeach(bhlParser.ForeachContext ctx)
  {
    var local_scope = new LocalScope(false, curr_scope);
    PushScope(local_scope);
    local_scope.Enter();

    if(ctx.foreachExp().varOrDeclares().varOrDeclare().Length == 1)
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
    
      var vod = ctx.foreachExp().varOrDeclares().varOrDeclare()[0];
      var vd = vod.varDeclare();
      Proxy<IType> iter_type;
      string iter_str_name = "";
      AST_Tree iter_ast_decl = null;
      VariableSymbol iter_symb = null;
      if(vod.NAME() != null)
      {
        iter_str_name = vod.NAME().GetText();
        iter_symb = curr_scope.ResolveWithFallback(iter_str_name) as VariableSymbol;
        if(iter_symb == null)
          FireError(vod.NAME(), "symbol is not a valid variable");
        iter_type = iter_symb.type;
      }
      else
      {
        iter_str_name = vd.NAME().GetText();
        iter_ast_decl = CommonDeclVar(curr_scope, vd.NAME(), vd.type(), is_ref: false, func_arg: false, write: false);
        iter_symb = curr_scope.ResolveWithFallback(iter_str_name) as VariableSymbol;
        iter_type = iter_symb.type;
      }
      var arr_type = (ArrayTypeSymbol)curr_scope.R().TArr(iter_type).Get();

      PushJsonType(arr_type);
      var exp = ctx.foreachExp().exp();
      //evaluating array expression
      Visit(exp);
      PopJsonType();
      types.CheckAssign(arr_type, Wrap(exp));

      var arr_tmp_name = "$foreach_tmp" + exp.Start.Line + "_" + exp.Start.Column;
      var arr_tmp_symb = curr_scope.ResolveWithFallback(arr_tmp_name) as VariableSymbol;
      if(arr_tmp_symb == null)
      {
        arr_tmp_symb = new VariableSymbol(Wrap(exp), arr_tmp_name, curr_scope.R().T(iter_type));
        curr_scope.Define(arr_tmp_symb);
      }

      var arr_cnt_name = "$foreach_cnt" + exp.Start.Line + "_" + exp.Start.Column;
      var arr_cnt_symb = curr_scope.ResolveWithFallback(arr_cnt_name) as VariableSymbol;
      if(arr_cnt_symb == null)
      {
        arr_cnt_symb = new VariableSymbol(Wrap(exp), arr_cnt_name, Types.Int);
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
    else if(ctx.foreachExp().varOrDeclares().varOrDeclare().Length == 2)
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

      var vod_key = ctx.foreachExp().varOrDeclares().varOrDeclare()[0];
      var vd_key = vod_key.varDeclare();
      var vod_val = ctx.foreachExp().varOrDeclares().varOrDeclare()[1];
      var vd_val = vod_val.varDeclare();

      Proxy<IType> key_iter_type;
      string key_iter_str_name = "";
      AST_Tree key_iter_ast_decl = null;
      VariableSymbol key_iter_symb = null;
      if(vod_key.NAME() != null)
      {
        key_iter_str_name = vod_key.NAME().GetText();
        key_iter_symb = curr_scope.ResolveWithFallback(key_iter_str_name) as VariableSymbol;
        if(key_iter_symb == null)
          FireError(vod_key.NAME(), "symbol is not a valid variable");
        key_iter_type = key_iter_symb.type;
      }
      else
      {
        key_iter_str_name = vd_key.NAME().GetText();
        key_iter_ast_decl = CommonDeclVar(curr_scope, vd_key.NAME(), vd_key.type(), is_ref: false, func_arg: false, write: false);
        key_iter_symb = curr_scope.ResolveWithFallback(key_iter_str_name) as VariableSymbol;
        key_iter_type = key_iter_symb.type;
      }

      Proxy<IType> val_iter_type;
      string val_iter_str_name = "";
      AST_Tree val_iter_ast_decl = null;
      VariableSymbol val_iter_symb = null;
      if(vod_val.NAME() != null)
      {
        val_iter_str_name = vod_val.NAME().GetText();
        val_iter_symb = curr_scope.ResolveWithFallback(val_iter_str_name) as VariableSymbol;
        if(val_iter_symb == null)
          FireError(vod_val.NAME(), "symbol is not a valid variable");
        val_iter_type = val_iter_symb.type;
      }
      else
      {
        val_iter_str_name = vd_val.NAME().GetText();
        val_iter_ast_decl = CommonDeclVar(curr_scope, vd_val.NAME(), vd_val.type(), is_ref: false, func_arg: false, write: false);
        val_iter_symb = curr_scope.ResolveWithFallback(val_iter_str_name) as VariableSymbol;
        val_iter_type = val_iter_symb.type;
      }
      var map_type = (MapTypeSymbol)curr_scope.R().TMap(key_iter_type, val_iter_type).Get();

      PushJsonType(map_type);
      var exp = ctx.foreachExp().exp();
      //evaluating array expression
      Visit(exp);
      PopJsonType();
      types.CheckAssign(map_type, Wrap(exp));

      var map_tmp_en_name = "$foreach_en" + exp.Start.Line + "_" + exp.Start.Column;
      var map_tmp_en_symb = curr_scope.ResolveWithFallback(map_tmp_en_name) as VariableSymbol;
      if(map_tmp_en_symb == null)
      {
        map_tmp_en_symb = new VariableSymbol(Wrap(exp), map_tmp_en_name, Types.Any);
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
      FireError(ctx.foreachExp().varOrDeclares(), "invalid 'foreach' syntax");

    local_scope.Exit();
    PopScope();

    return null;
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

  public interface IExpChain
  {
    int Length { get; }
    IParseTree parseTree(int i); 
    bhlParser.CallArgsContext callArgs(int i);
    bhlParser.MemberAccessContext memberAccess(int i);
    bhlParser.ArrAccessContext arrAccess(int i);
  }

  public class ExpChain : IExpChain 
  {
    bhlParser.ChainExpContext[] chain; 

    public int Length { get { return chain.Length; } }

    public ExpChain(bhlParser.ChainExpContext[] chain)
    {
      this.chain = chain;
    }

    public IParseTree parseTree(int i) 
    {
      return chain[i];
    }

    public bhlParser.CallArgsContext callArgs(int i) 
    {
      return chain[i].callArgs();
    }

    public bhlParser.MemberAccessContext memberAccess(int i)
    {
      return chain[i].memberAccess();
    }

    public bhlParser.ArrAccessContext arrAccess(int i)
    {
      return chain[i].arrAccess();
    }
  }

  public class ExpChainExtraCall : IExpChain 
  {
    IExpChain orig;
    bhlParser.CallArgsContext extra_call;

    public int Length { get { return orig.Length + 1; } }

    public ExpChainExtraCall(IExpChain chain, bhlParser.CallArgsContext extra_call)
    {
      this.orig = chain;
      this.extra_call = extra_call;
    }

    public IParseTree parseTree(int i) 
    {
      if(orig.Length == i)
        return extra_call;
      else
        return orig.parseTree(i);
    }

    public bhlParser.CallArgsContext callArgs(int i) 
    {
      if(orig.Length == i)
        return extra_call;
      else
        return orig.callArgs(i);
    }

    public bhlParser.MemberAccessContext memberAccess(int i)
    {
      if(orig.Length == i)
        return null;
      else
        return orig.memberAccess(i);
    }

    public bhlParser.ArrAccessContext arrAccess(int i)
    {
      if(orig.Length == i)
        return null;
      else
        return orig.arrAccess(i);
    }
  }
}

} //namespace bhl
