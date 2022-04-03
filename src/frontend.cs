using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace bhl {

public class WrappedParseTree
{
  public IParseTree tree;
  public Module module;
  public ITokenStream tokens;
  public IType eval_type;
}

public class ANTLR_Result
{
  public ITokenStream tokens { get; private set; }
  public bhlParser.ProgramContext prog { get; private set; }

  public ANTLR_Result(ITokenStream tokens, bhlParser.ProgramContext prog)
  {
    this.tokens = tokens;
    this.prog = prog;
  }
}

public class Frontend : bhlBaseVisitor<object>
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

  Result result;

  ANTLR_Result parsed;

  static int lambda_id = 0;

  static int NextLambdaId()
  {
    Interlocked.Increment(ref lambda_id);
    return lambda_id;
  }

  Types types;

  //NOTE: in 'declarations only' mode only declarations are filled,
  //      full definitions are omitted. This is useful when we only need 
  //      to know which symbols can be imported from the current module
  bool decls_only;

  Importer importer;

  Module module;

  ITokenStream tokens;
  ParseTreeProperty<WrappedParseTree> tree_props = new ParseTreeProperty<WrappedParseTree>();

  IScope curr_scope;
  int scope_level;

  HashSet<FuncSymbol> return_found = new HashSet<FuncSymbol>();

  Dictionary<FuncSymbol, int> defers2func = new Dictionary<FuncSymbol, int>();
  int defer_stack {
    get {
      var fsymb = PeekFuncDecl();
      int v;
      defers2func.TryGetValue(fsymb, out v);
      return v;
    }

    set {
      var fsymb = PeekFuncDecl();
      defers2func[fsymb] = value;
    }
  }

  Dictionary<FuncSymbol, int> loops2func = new Dictionary<FuncSymbol, int>();
  int loops_stack {
    get {
      var fsymb = PeekFuncDecl();
      int v;
      loops2func.TryGetValue(fsymb, out v);
      return v;
    }

    set {
      var fsymb = PeekFuncDecl();
      loops2func[fsymb] = value;
    }
  }

  //NOTE: a list is used instead of stack, so that it's easier to traverse by index
  List<FuncSymbol> func_decl_stack = new List<FuncSymbol>();

  Stack<IType> json_type_stack = new Stack<IType>();

  Stack<bool> call_by_ref_stack = new Stack<bool>();

  Stack<AST_Tree> ast_stack = new Stack<AST_Tree>();

  public static CommonTokenStream Stream2Tokens(string file, Stream s)
  {
    var ais = new AntlrInputStream(s);
    var lex = new bhlLexer(ais);
    lex.AddErrorListener(new ErrorLexerListener(file));
    return new CommonTokenStream(lex);
  }

  public static Result ProcessFile(string file, Types ts, Frontend.Importer imp)
  {
    using(var sfs = File.OpenRead(file))
    {
      var mod = new Module(ts.globs, imp.FilePath2ModuleName(file), file);
      return ProcessStream(mod, sfs, ts, imp);
    }
  }

  public static bhlParser Stream2Parser(string file, Stream src)
  {
    var tokens = Stream2Tokens(file, src);
    var p = new bhlParser(tokens);
    p.AddErrorListener(new ErrorParserListener(file));
    p.ErrorHandler = new ErrorStrategy();
    return p;
  }
  
  public static Result ProcessStream(Module module, Stream src, Types ts, Frontend.Importer imp = null, bool decls_only = false)
  {
    var p = Stream2Parser(module.file_path, src);
    var parsed = new ANTLR_Result(p.TokenStream, p.program());
    return ProcessParsed(module, parsed, ts, imp, decls_only);
  }

  public static Result ProcessParsed(Module module, ANTLR_Result parsed, Types ts, Frontend.Importer imp = null, bool decls_only = false)
  {
    //var sw1 = System.Diagnostics.Stopwatch.StartNew();
    var f = new Frontend(parsed, module, ts, imp, decls_only);
    var res = f.Process();
    //sw1.Stop();
    //Console.WriteLine("Module {0} ({1} sec)", module.norm_path, Math.Round(sw1.ElapsedMilliseconds/1000.0f,2));
    return res;
  }

  public interface IParsedCache
  {
    bool TryFetch(string file, out ANTLR_Result parsed);
  }

  public class Importer
  {
    List<string> include_path = new List<string>();
    Dictionary<string, Module> modules = new Dictionary<string, Module>(); 
    IParsedCache parsed_cache = null;

    public void SetParsedCache(IParsedCache cache)
    {
      parsed_cache = cache;
    }

    public void AddToIncludePath(string path)
    {
      include_path.Add(Util.NormalizeFilePath(path));
    }

    public List<string> GetIncludePath()
    {
      return include_path;
    }

    public Module TryGet(string path)
    {
      Module m = null;
      modules.TryGetValue(path, out m);
      return m;
    }

    public void Register(Module m)
    {
      modules.Add(m.file_path, m);
    }

    public Module ImportModule(Module curr_module, Types ts, string path)
    {
      string full_path;
      string norm_path;
      ResolvePath(curr_module.file_path, path, out full_path, out norm_path);

      //Console.WriteLine("IMPORT: " + full_path + " FROM:" + curr_module.file_path);

      //1. checking repeated imports
      if(curr_module.imports.ContainsKey(full_path))
      {
        //Console.WriteLine("HIT: " + full_path);
        return null;
      }

      //2. checking if already exists
      Module m = TryGet(full_path);
      if(m != null)
      {
        curr_module.imports.Add(full_path, m);
        return m;
      }

      //3. Ok, let's parse it otherwise
      m = new Module(ts.globs, norm_path, full_path);
     
      //Console.WriteLine("ADDING: " + full_path + " TO:" + curr_module.file_path);
      curr_module.imports.Add(full_path, m);
      Register(m);

      ANTLR_Result parsed;
      //4. Let's try the parsed cache if it's present
      if(parsed_cache != null && parsed_cache.TryFetch(full_path, out parsed))
      {
        //Console.WriteLine("HIT " + full_path);
        Frontend.ProcessParsed(m, parsed, ts, this, decls_only: true);
      }
      else
      {
        var stream = File.OpenRead(full_path);
        //Console.WriteLine("MISS " + full_path);
        Frontend.ProcessStream(m, stream, ts, this, decls_only: true);
        stream.Close();
      }

      return m;
    }

    void ResolvePath(string self_path, string path, out string full_path, out string norm_path)
    {
      full_path = "";
      norm_path = "";

      if(path.Length == 0)
        throw new Exception("Bad path");

      full_path = Util.ResolveImportPath(include_path, self_path, path);
      norm_path = FilePath2ModuleName(full_path);
    }

    public string FilePath2ModuleName(string full_path)
    {
      full_path = Util.NormalizeFilePath(full_path);

      string norm_path = "";
      for(int i=0;i<include_path.Count;++i)
      {
        var inc_path = include_path[i];
        if(full_path.IndexOf(inc_path) == 0)
        {
          norm_path = full_path.Replace(inc_path, "");
          norm_path = norm_path.Replace('\\', '/');
          //stripping .bhl extension
          norm_path = norm_path.Substring(0, norm_path.Length-4);
          //stripping initial /
          norm_path = norm_path.TrimStart('/', '\\');
          break;
        }
      }

      if(norm_path.Length == 0)
        throw new Exception("File path '" + full_path + "' was not normalized");
      return norm_path;
    }
  }

  public Frontend(ANTLR_Result parsed, Module module, Types types, Importer importer, bool decls_only = false)
  {
    this.parsed = parsed;
    this.tokens = parsed.tokens;

    this.module = module;
    types.AddSource(module.scope);
    curr_scope = this.module.scope;

    if(importer == null)
      importer = new Importer();
    this.importer = importer;

    this.types = types;

    this.decls_only = decls_only;
  }

  void FireError(IParseTree place, string msg) 
  {
    throw new SemanticError(module, place, tokens, msg);
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

  public Result Process()
  {
    if(result == null)
    {
      var ast = new AST_Module(module.name);
      PushAST(ast);
      VisitProgram(parsed.prog);
      PopAST();

      result = new Result(module, ast);
    }
    return result;
  }

  public override object VisitProgram(bhlParser.ProgramContext ctx)
  {
    //Console.WriteLine(">>>> PROG VISIT " + module.name + " decls: " + decls_only);
    for(int i=0;i<ctx.progblock().Length;++i)
      Visit(ctx.progblock()[i]);
    return null;
  }

  public override object VisitProgblock(bhlParser.ProgblockContext ctx)
  {
    var imps = ctx.imports();
    if(imps != null)
      Visit(imps);
    
    Visit(ctx.decls()); 

    return null;
  }

  public override object VisitImports(bhlParser.ImportsContext ctx)
  {
    var ast = new AST_Import();

    var imps = ctx.mimport();
    for(int i=0;i<imps.Length;++i)
      AddImport(ast, imps[i]);

    PeekAST().AddChild(ast);
    return null;
  }

  public void AddImport(AST_Import ast, bhlParser.MimportContext ctx)
  {
    var name = ctx.NORMALSTRING().GetText();
    //removing quotes
    name = name.Substring(1, name.Length-2);
    
    var imported = importer.ImportModule(this.module, types, name);
    //NOTE: null means module is already imported
    if(imported != null)
    {
      module.scope.AddImport(imported.scope);
      ast.module_names.Add(imported.name);
    }
  }

  public override object VisitSymbCall(bhlParser.SymbCallContext ctx)
  {
    var exp = ctx.callExp(); 
    Visit(exp);
    var eval_type = Wrap(exp).eval_type;
    if(eval_type != null && eval_type != Types.Void)
    {
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

    ProcChainedCall(ctx.NAME(), ctx.chainExp(), ref curr_type, ctx.Start.Line, write: false);

    Wrap(ctx).eval_type = curr_type;
    return null;
  }

  public override object VisitLambdaCall(bhlParser.LambdaCallContext ctx)
  {
    CommonVisitLambda(ctx, ctx.funcLambda());
    return null;
  }

  void ProcChainedCall(
    ITerminalNode root_name, 
    bhlParser.ChainExpContext[] chain, 
    ref IType curr_type, 
    int line, 
    bool write
   )
  {
    AST_Interim pre_call = null;
    PushAST(new AST_Interim());

    var orig_scope = curr_scope;

    ITerminalNode curr_name = root_name;
    IInstanceType curr_scope_type = null;

    if(root_name != null)
    {
      var root_str_name = root_name.GetText();
      var name_symb = curr_scope.Resolve(root_str_name);
      if(name_symb == null)
        FireError(root_name, "symbol not resolved");
      if(name_symb.type.Get() == null)
        FireError(root_name, "bad chain call");
      curr_type = name_symb.type.Get();
    }

    if(chain != null)
    {
      for(int c=0;c<chain.Length;++c)
      {
        var ch = chain[c];

        var cargs = ch.callArgs();
        var macc = ch.memberAccess();
        var arracc = ch.arrAccess();
        bool is_last = c == chain.Length-1;

        if(cargs != null)
        {
          ProcCallChainItem(curr_name, cargs, null, curr_scope_type, ref curr_type, ref pre_call, line, write: false);
          curr_scope_type = null;
          curr_name = null;
        }
        else if(arracc != null)
        {
          ProcCallChainItem(curr_name, null, arracc, curr_scope_type, ref curr_type, ref pre_call, line, write: write && is_last);
          curr_scope_type = null;
          curr_name = null;
        }
        else if(macc != null)
        {
          if(curr_name != null)
            ProcCallChainItem(curr_name, null, null, curr_scope_type, ref curr_type, ref pre_call, line, write: false);

          curr_scope_type = curr_type as IInstanceType; 
          if(curr_scope_type == null)
            FireError(macc, "type doesn't support member access via '.'");

          curr_name = macc.NAME();
        }
      }
    }

    //checking the leftover of the call chain
    if(curr_name != null)
      ProcCallChainItem(curr_name, null, null, curr_scope_type, ref curr_type, ref pre_call, line, write);

    curr_scope = orig_scope;

    var chain_ast = PeekAST();
    PopAST();
    if(pre_call != null)
      PeekAST().AddChildren(pre_call);
    PeekAST().AddChildren(chain_ast);
  }

  void ProcCallChainItem(
    ITerminalNode name, 
    bhlParser.CallArgsContext cargs, 
    bhlParser.ArrAccessContext arracc, 
    IInstanceType scope_type, 
    ref IType type, 
    ref AST_Interim pre_call,
    int line, 
    bool write
    )
  {
    AST_Call ast = null;

    if(name != null)
    {
      string str_name = name.GetText();
      var name_symb = scope_type == null ? curr_scope.Resolve(str_name) : scope_type.Resolve(str_name);
      if(name_symb == null)
        FireError(name, "symbol not resolved");

      var var_symb = name_symb as VariableSymbol;
      var func_symb = name_symb as FuncSymbol;

      //func or method call
      if(cargs != null)
      {
        if(var_symb is FieldSymbol && !(var_symb.type.Get() is FuncSignature))
          FireError(name, "symbol is not a function");

        //func ptr
        if(var_symb != null && var_symb.type.Get() is FuncSignature)
        {
          var ftype = var_symb.type.Get() as FuncSignature;

          if(scope_type == null)
          {
            ast = AST_Util.New_Call(EnumCall.FUNC_VAR, line, var_symb);
            AddCallArgs(ftype, cargs, ref ast, ref pre_call);
            type = ftype.ret_type.Get();
          }
          else //func ptr member of class
          {
            PeekAST().AddChild(AST_Util.New_Call(EnumCall.MVAR, line, var_symb, scope_type));
            ast = AST_Util.New_Call(EnumCall.FUNC_MVAR, line);
            AddCallArgs(ftype, cargs, ref ast, ref pre_call);
            type = ftype.ret_type.Get();
          }
        }
        else if(func_symb != null)
        {
          ast = AST_Util.New_Call(scope_type != null ? EnumCall.MFUNC : EnumCall.FUNC, line, func_symb.name, (func_symb is FuncSymbolScript fss ? fss.module_name : ""), scope_type, func_symb.scope_idx);
          AddCallArgs(func_symb, cargs, ref ast, ref pre_call);
          type = func_symb.GetReturnType();
        }
        else
        {
          //NOTE: let's try fetching func symbol from the module scope
          func_symb = module.scope.Resolve(str_name) as FuncSymbol;
          if(func_symb != null)
          {
            ast = AST_Util.New_Call(EnumCall.FUNC, line, func_symb.name, (func_symb is FuncSymbolScript fss ? fss.module_name : ""), null, func_symb.scope_idx);
            AddCallArgs(func_symb, cargs, ref ast, ref pre_call);
            type = func_symb.GetReturnType();
          }
          else
            FireError(name, "symbol is not a function");
        }
      }
      //variable or attribute call
      else
      {
        if(var_symb != null)
        {
          bool is_write = write && arracc == null;
          bool is_global = var_symb.scope is ModuleScope;

          if(scope_type is InterfaceSymbol)
            FireError(name, "attributes not supported by interfaces");

          ast = AST_Util.New_Call(scope_type != null ? 
            (is_write ? EnumCall.MVARW : EnumCall.MVAR) : 
            (is_global ? (is_write ? EnumCall.GVARW : EnumCall.GVAR) : (is_write ? EnumCall.VARW : EnumCall.VAR)), 
            line, 
            var_symb.name,
            scope_type,
            var_symb.scope_idx,
            is_global ? var_symb.module_name : ""
          );
          //handling passing by ref for class fields
          if(scope_type != null && PeekCallByRef())
          {
            if(scope_type is ClassSymbolNative)
              FireError(name, "getting field by 'ref' not supported for this class");
            ast.type = EnumCall.MVARREF; 
          }
          type = var_symb.type.Get();
        }
        else if(func_symb != null)
        {
          var call_func_symb = module.scope.Resolve(str_name) as FuncSymbol;
          if(call_func_symb == null)
            FireError(name, "no such function found");

          ast = AST_Util.New_Call(EnumCall.GET_ADDR, line, call_func_symb.name, (call_func_symb is FuncSymbolScript fss ? fss.module_name : ""), null);
          type = func_symb.type.Get();
        }
        else
        {
          FireError(name, "symbol usage is not valid");
        }
      }
    }
    else if(cargs != null)
    {
      var ftype = type as FuncSignature;
      if(ftype == null)
        FireError(cargs, "no func to call");
      
      ast = AST_Util.New_Call(EnumCall.LMBD, line);
      AddCallArgs(ftype, cargs, ref ast, ref pre_call);
      type = ftype.ret_type.Get();
    }

    if(ast != null)
      PeekAST().AddChild(ast);

    if(arracc != null)
      AddArrIndex(arracc, ref type, line, write);
  }

  void AddArrIndex(bhlParser.ArrAccessContext arracc, ref IType type, int line, bool write)
  {
    var arr_type = type as ArrayTypeSymbol;
    if(arr_type == null)
      FireError(arracc, "accessing not an array type '" + type.GetName() + "'");

    var arr_exp = arracc.exp();
    Visit(arr_exp);

    if(Wrap(arr_exp).eval_type != Types.Int)
      FireError(arr_exp, "array index expression is not of type int");

    type = arr_type.item_type.Get();

    var ast = AST_Util.New_Call(write ? EnumCall.ARR_IDXW : EnumCall.ARR_IDX, line);
    ast.scope_type = arr_type;

    PeekAST().AddChild(ast);
  }

  class NormCallArg
  {
    public bhlParser.CallArgContext ca;
    public Symbol orig;
  }

  void AddCallArgs(FuncSymbol func_symb, bhlParser.CallArgsContext cargs, ref AST_Call call, ref AST_Interim pre_call)
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

    //1. filling normalized call args
    for(int ci=0;ci<cargs.callArg().Length;++ci)
    {
      var ca = cargs.callArg()[ci];
      var ca_name = ca.NAME();

      var idx = ci;
      //NOTE: checking if it's a named arg and finding its index
      if(ca_name != null)
      {
        idx = func_args.FindStringKeyIndex(ca_name.GetText());
        if(idx == -1)
          FireError(ca_name, "no such named argument");

        if(norm_cargs[idx].ca != null)
          FireError(ca_name, "argument already passed before");
      }
      
      if(idx >= func_args.Count)
        FireError(ca, "there is no argument " + (idx + 1) + ", total arguments " + func_args.Count);

      if(idx >= func_args.Count)
        FireError(ca, "too many arguments for function");

      norm_cargs[idx].ca = ca;
    }

    PushAST(call);
    IParseTree prev_ca = null;
    //2. traversing normalized args
    for(int i=0;i<norm_cargs.Count;++i)
    {
      var ca = norm_cargs[i].ca;

      //NOTE: if call arg is not specified, try to find the default one
      if(ca == null)
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
        prev_ca = ca;
        if(!args_info.IncArgsNum())
          FireError(ca, "max arguments reached");

        var func_arg_symb = (FuncArgSymbol)func_args[i];
        var func_arg_type = func_arg_symb.parsed == null ? func_arg_symb.type.Get() : func_arg_symb.parsed.eval_type;  

        bool is_ref = ca.isRef() != null;
        if(!is_ref && func_arg_symb.is_ref)
          FireError(ca, "'ref' is missing");
        else if(is_ref && !func_arg_symb.is_ref)
          FireError(ca, "argument is not a 'ref'");

        PushCallByRef(is_ref);
        PushJsonType(func_arg_type);
        PushAST(new AST_Interim());
        Visit(ca);
        TryProtectStackInterleaving(ca, func_arg_type, i, ref pre_call);
        PopAddOptimizeAST();
        PopJsonType();
        PopCallByRef();

        var wca = Wrap(ca);

        //NOTE: if symbol is from bindings we don't have a parse tree attached to it
        if(func_arg_symb.parsed == null)
        {
          if(func_arg_symb.type.Get() == null)
            FireError(ca, "invalid type");
          types.CheckAssign(func_arg_symb.type.Get(), wca);
        }
        else
          types.CheckAssign(func_arg_symb.parsed, wca);
      }
    }

    PopAST();

    call.cargs_bits = args_info.bits;
  }

  void AddCallArgs(FuncSignature func_type, bhlParser.CallArgsContext cargs, ref AST_Call call, ref AST_Interim pre_call)
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
        FireError(next_arg, "missing argument of type '" + arg_type_ref.name + "'");
      }

      var ca = cargs.callArg()[i];
      var ca_name = ca.NAME();

      if(ca_name != null)
        FireError(ca_name, "named arguments not supported for function pointers");

      var arg_type = arg_type_ref.Get();
      PushJsonType(arg_type);
      PushAST(new AST_Interim());
      Visit(ca);
      TryProtectStackInterleaving(ca, arg_type, i, ref pre_call);
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

  //NOTE: We really want to avoid stack interleaving for the following case: 
  //        
  //        foo(1, bar())
  //      
  //      where bar() might execute for many ticks and at the same time 
  //      somewhere *in parallel* executes some another function which pushes 
  //      result onto the stack *before* bar() finishes its execution. 
  //
  //      At the time foo(..) is actually called the stack will contain badly 
  //      interleaved arguments! 
  //
  //      For this reason we rewrite the example above into something as follows:
  //
  //        tmp_1 = bar()
  //        foo(1, tmp_1)
  //
  //      We also should take into account cases like: 
  //
  //        foo(wow().bar())
  //
  //      At the same time we should not rewrite trivial cases like:
  //
  //        foo(bar())
  //
  //      Since in this case there is no stack interleaving possible (only one argument) 
  //      and we really want to avoid introduction of the new temp local variable
  void TryProtectStackInterleaving(bhlParser.CallArgContext ca, IType func_arg_type, int i, ref AST_Interim pre_call)
  {
    var arg_ast = PeekAST();
    if(i == 0 || !HasFuncCalls(arg_ast))
      return;

    PopAST();

    var var_tmp_symb = new VariableSymbol(Wrap(ca), "$_tmp_" + ca.Start.Line + "_" + ca.Start.Column, types.Type(func_arg_type));
    curr_scope.Define(var_tmp_symb);

    var var_tmp_decl = AST_Util.New_Call(EnumCall.VARW, ca.Start.Line, var_tmp_symb);
    var var_tmp_read = AST_Util.New_Call(EnumCall.VAR, ca.Start.Line, var_tmp_symb);

    if(pre_call == null)
      pre_call = new AST_Interim();
    foreach(var chain_child in arg_ast.children)
      pre_call.children.Add(chain_child);
    pre_call.children.Add(var_tmp_decl);

    PushAST(var_tmp_read);
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
    CommonVisitLambda(ctx, ctx.funcLambda());
    return null;
  }

  FuncSignature ParseFuncSignature(bhlParser.RetTypeContext ret_ctx, bhlParser.TypesContext types_ctx)
  {
    var ret_type = ParseType(ret_ctx);

    var arg_types = new List<TypeProxy>();
    if(types_ctx != null)
    {
      for(int i=0;i<types_ctx.refType().Length;++i)
      {
        var refType = types_ctx.refType()[i];
        var arg_type = ParseType(refType.type());
        if(refType.isRef() != null)
          arg_type = this.types.TypeRef(arg_type);
        arg_types.Add(arg_type);
      }
    }

    return new FuncSignature(ret_type, arg_types);
  }

  FuncSignature ParseFuncSignature(bhlParser.FuncTypeContext ctx)
  {
    return ParseFuncSignature(ctx.retType(), ctx.types());
  }

  FuncSignature ParseFuncSignature(TypeProxy ret_type, bhlParser.FuncParamsContext fparams, out int default_args_num)
  {
    default_args_num = 0;
    var sig = new FuncSignature(ret_type);
    if(fparams != null)
    {
      for(int i=0;i<fparams.funcParamDeclare().Length;++i)
      {
        var vd = fparams.funcParamDeclare()[i];

        var tp = ParseType(vd.type());
        if(vd.isRef() != null)
          tp = types.Type(new RefType(tp));
        if(vd.assignExp() != null)
          ++default_args_num;
        sig.AddArg(tp);
      }
    }
    return sig;
  }

  TypeProxy ParseType(bhlParser.RetTypeContext parsed)
  {
    TypeProxy tp;

    //convenience special case
    if(parsed == null)
      tp = Types.Void;
    else if(parsed.type().Length > 1)
    {
      var tuple = new TupleType();
      for(int i=0;i<parsed.type().Length;++i)
        tuple.Add(ParseType(parsed.type()[i]));
      tp = types.Type(tuple);
    }
    else
      tp = ParseType(parsed.type()[0]);

    if(tp.Get() == null)
      FireError(parsed, "type '" + tp.name + "' not found");

    return tp;
  }

  TypeProxy ParseType(bhlParser.TypeContext ctx)
  {
    TypeProxy tp;
    if(ctx.funcType() != null)
      tp = types.Type(ParseFuncSignature(ctx.funcType()));
    else
      tp = types.Type(ctx.NAME().GetText());

    if(ctx.ARR() != null)
      tp = types.TypeArr(tp);

    if(tp.Get() == null)
      FireError(ctx, "type '" + tp.name + "' not found");

   return tp;
  }

  void CommonVisitLambda(IParseTree ctx, bhlParser.FuncLambdaContext funcLambda)
  {
    var tp = ParseType(funcLambda.retType());

    var func_name = Hash.CRC32(module.name) + "_lmb_" + NextLambdaId(); 
    int default_args_num;
    var upvals = new List<AST_UpVal>();
    var lmb_symb = new LambdaSymbol(
      Wrap(ctx), 
      func_name,
      ParseFuncSignature(tp, funcLambda.funcParams(), out default_args_num),
      upvals,
      this.func_decl_stack
    );

    var ast = AST_Util.New_LambdaDecl(lmb_symb, upvals, funcLambda.Stop.Line);

    PushFuncDecl(lmb_symb);

    var scope_backup = curr_scope;
    curr_scope = lmb_symb;

    var fparams = funcLambda.funcParams();
    if(fparams != null)
    {
      PushAST(ast.fparams());
      Visit(fparams);
      PopAST();
    }

    module.scope.Define(lmb_symb);

    //NOTE: while we are inside lambda the eval type is its return type
    Wrap(ctx).eval_type = lmb_symb.GetReturnType();

    PushAST(ast.block());
    Visit(funcLambda.funcBlock());
    PopAST();

    if(tp.Get() != Types.Void && !return_found.Contains(lmb_symb))
      FireError(funcLambda.funcBlock(), "matching 'return' statement not found");

    PopFuncDecl();

    //NOTE: once we are out of lambda the eval type is the lambda itself
    var curr_type = lmb_symb.type.Get(); 
    Wrap(ctx).eval_type = curr_type;

    curr_scope = scope_backup;

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
      ProcChainedCall(null, chain, ref curr_type, funcLambda.Start.Line, write: false);
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

    if(!(curr_type is ClassSymbol) || (curr_type is ArrayTypeSymbol))
      FireError(ctx, "type '" + curr_type + "' can't be specified with {..}");

    Wrap(ctx).eval_type = curr_type;

    var ast = AST_Util.New_JsonObj(curr_type, ctx.Start.Line);

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

    if(!(curr_type is ArrayTypeSymbol))
      FireError(ctx, "[..] is not expected, need '" + curr_type + "'");

    var arr_type = curr_type as ArrayTypeSymbol;
    var orig_type = arr_type.item_type.Get();
    if(orig_type == null)
      FireError(ctx,  "type '" + arr_type.item_type.name + "' not found");
    PushJsonType(orig_type);

    var ast = AST_Util.New_JsonArr(arr_type, ctx.Start.Line);

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

    return null;
  }

  public override object VisitJsonPair(bhlParser.JsonPairContext ctx)
  {
    var curr_type = PeekJsonType();
    var scoped_symb = curr_type as ClassSymbol;
    if(scoped_symb == null)
      FireError(ctx, "expecting class type, got '" + curr_type + "' instead");

    var name_str = ctx.NAME().GetText();
    
    var member = scoped_symb.Resolve(name_str) as VariableSymbol;
    if(member == null)
      FireError(ctx, "no such attribute '" + name_str + "' in class '" + scoped_symb.name + "'");

    var ast = AST_Util.New_JsonPair(curr_type, name_str, member.scope_idx);

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
    var jobj = ctx.jsonObject();
    var jarr = ctx.jsonArray();

    if(exp != null)
    {
      var curr_type = PeekJsonType();
      Visit(exp);
      Wrap(ctx).eval_type = Wrap(exp).eval_type;

      types.CheckAssign(curr_type, Wrap(exp));
    }
    else if(jobj != null)
      Visit(jobj);
    else
      Visit(jarr);

    return null;
  }

  public override object VisitExpTypeof(bhlParser.ExpTypeofContext ctx)
  {
    //TODO:
    //var tp = ParseType(ctx.typeid().type());

    return null;
  }

  public override object VisitExpStaticCall(bhlParser.ExpStaticCallContext ctx)
  {
    var exp = ctx.staticCallExp(); 
    var ctx_name = exp.NAME();
    var enum_symb = module.scope.Resolve(ctx_name.GetText()) as EnumSymbol;
    if(enum_symb == null)
      FireError(ctx, "type '" + ctx_name + "' not found");

    var item_name = exp.staticCallItem().NAME();
    var enum_val = enum_symb.FindValue(item_name.GetText());

    if(enum_val == null)
      FireError(ctx, "enum value not found '" + item_name.GetText() + "'");

    Wrap(ctx).eval_type = enum_symb;

    var ast = AST_Util.New_Literal(LiteralType.NUM);
    ast.nval = enum_val.val;
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpCall(bhlParser.ExpCallContext ctx)
  {
    var exp = ctx.callExp(); 
    Visit(exp);
    Wrap(ctx).eval_type = Wrap(exp).eval_type;

    return null;
  }

  public override object VisitExpNew(bhlParser.ExpNewContext ctx)
  {
    var tp = ParseType(ctx.newExp().type());
    Wrap(ctx).eval_type = tp.Get();

    var ast = AST_Util.New_New((ClassSymbol)tp.Get());
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

    var ast = new AST_TypeCast(tp.Get(), ctx.Start.Line);
    var exp = ctx.exp();
    PushAST(ast);
    Visit(exp);
    PopAST();

    Wrap(ctx).eval_type = tp.Get();

    types.CheckCast(Wrap(ctx), Wrap(exp)); 

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

    var ast = AST_Util.New_UnaryOpExp(type);
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
      ProcChainedCall(null, chain, ref curr_type, exp.Start.Line, write: false);
    PopAST();
    
    PeekAST().AddChild(ast);
    
    Wrap(ctx).eval_type = curr_type;

    return null;
  }

  public override object VisitVarPostOpAssign(bhlParser.VarPostOpAssignContext ctx)
  {
    var lhs = ctx.NAME().GetText();
    var vlhs = curr_scope.Resolve(lhs) as VariableSymbol;

    if(vlhs == null)
      FireError(ctx.NAME(), "symbol not resolved");

    if(!Types.IsRtlOpCompatible(vlhs.type.Get()))
      FireError(ctx.NAME(), "incompatible types");

    var op = $"{ctx.operatorPostOpAssign().GetText()[0]}";
    var op_type = GetBinaryOpType(op);
    AST_Tree bin_op_ast = AST_Util.New_BinaryOpExp(op_type, ctx.Start.Line);

    PushAST(bin_op_ast);
    bin_op_ast.AddChild(AST_Util.New_Call(EnumCall.VAR, ctx.Start.Line, vlhs));
    Visit(ctx.exp());
    PopAST();

    types.CheckAssign(vlhs.type.Get(), Wrap(ctx.exp()));

    PeekAST().AddChild(bin_op_ast);
    PeekAST().AddChild(AST_Util.New_Call(EnumCall.VARW, ctx.Start.Line, vlhs));

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
  
  public override object VisitPostOperatorCall(bhlParser.PostOperatorCallContext ctx)
  {
    CommonVisitCallPostOperators(ctx.callPostOperators());
    return null;
  }

  void CommonVisitCallPostOperators(bhlParser.CallPostOperatorsContext ctx)
  {
    var v = ctx.NAME();
    var ast = new AST_Interim();
    
    var vs = curr_scope.Resolve(v.GetText()) as VariableSymbol;
    if(vs == null)
      FireError(v, "symbol not resolved");
    
    bool is_negative = ctx.decrementOperator() != null;
    
    if(!Types.IsRtlOpCompatible(vs.type.Get())) // only numeric types
    {
      FireError(v,
        $"operator {(is_negative ? "--" : "++")} is not supported for {vs.type.name} type"
      );
    }
    
    if(is_negative)
      ast.AddChild(AST_Util.New_Dec(vs));
    else
      ast.AddChild(AST_Util.New_Inc(vs));
    
    Wrap(ctx).eval_type = Types.Void;
    PeekAST().AddChild(ast);
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
    AST_Tree ast = AST_Util.New_BinaryOpExp(op_type, ctx.Start.Line);
    PushAST(ast);
    Visit(lhs);
    Visit(rhs);
    PopAST();

    var wlhs = Wrap(lhs);
    var wrhs = Wrap(rhs);

    var class_symb = wlhs.eval_type as ClassSymbol;
    //NOTE: checking if there's an operator overload
    if(class_symb != null && class_symb.Resolve(op) is FuncSymbol)
    {
      var op_func = class_symb.Resolve(op) as FuncSymbol;

      Wrap(ctx).eval_type = types.CheckBinOpOverload(module.scope, wlhs, wrhs, op_func);

      //NOTE: replacing original AST, a bit 'dirty' but kinda OK
      var over_ast = new AST_Interim();
      for(int i=0;i<ast.children.Count;++i)
        over_ast.AddChild(ast.children[i]);
      var op_call = AST_Util.New_Call(EnumCall.MFUNC, ctx.Start.Line, op, "", class_symb, op_func.scope_idx, 1/*cargs bits*/);
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
    var ast = AST_Util.New_BinaryOpExp(EnumBinaryOp.BIT_AND, ctx.Start.Line);
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
    var ast = AST_Util.New_BinaryOpExp(EnumBinaryOp.BIT_OR, ctx.Start.Line);
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
    var ast = AST_Util.New_BinaryOpExp(EnumBinaryOp.AND, ctx.Start.Line);
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
    var ast = AST_Util.New_BinaryOpExp(EnumBinaryOp.OR, ctx.Start.Line);
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
    var ast = AST_Util.New_Literal(LiteralType.NUM);

    var number = ctx.number();
    var int_num = number.INT();
    var flt_num = number.FLOAT();
    var hex_num = number.HEX();

    if(int_num != null)
    {
      Wrap(ctx).eval_type = Types.Int;
      ast.nval = double.Parse(int_num.GetText(), System.Globalization.CultureInfo.InvariantCulture);
    }
    else if(flt_num != null)
    {
      Wrap(ctx).eval_type = Types.Float;
      ast.nval = double.Parse(flt_num.GetText(), System.Globalization.CultureInfo.InvariantCulture);
    }
    else if(hex_num != null)
    {
      Wrap(ctx).eval_type = Types.Int;
      ast.nval = Convert.ToUInt32(hex_num.GetText(), 16);
    }

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralFalse(bhlParser.ExpLiteralFalseContext ctx)
  {
    Wrap(ctx).eval_type = Types.Bool;

    var ast = AST_Util.New_Literal(LiteralType.BOOL);
    ast.nval = 0;
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralNull(bhlParser.ExpLiteralNullContext ctx)
  {
    Wrap(ctx).eval_type = Types.Null;

    var ast = AST_Util.New_Literal(LiteralType.NIL);
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralTrue(bhlParser.ExpLiteralTrueContext ctx)
  {
    Wrap(ctx).eval_type = Types.Bool;

    var ast = AST_Util.New_Literal(LiteralType.BOOL);
    ast.nval = 1;
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralStr(bhlParser.ExpLiteralStrContext ctx)
  {
    Wrap(ctx).eval_type = Types.String;

    var ast = AST_Util.New_Literal(LiteralType.STR);
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

  void PushFuncDecl(FuncSymbol symb)
  {
    func_decl_stack.Add(symb);
  }

  void PopFuncDecl()
  {
    func_decl_stack.RemoveAt(func_decl_stack.Count-1);
  }

  FuncSymbol PeekFuncDecl()
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
    if(defer_stack > 0)
      FireError(ctx, "return is not allowed in defer block");

    var func_symb = PeekFuncDecl();
    if(func_symb == null)
      FireError(ctx, "return statement is not in function");
    
    return_found.Add(func_symb);

    var ret_ast = AST_Util.New_Return(ctx.Start.Line);
    
    var explist = ctx.explist();
    if(explist != null)
    {
      int explen = explist.exp().Length;

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
        var exp_item = explist.exp()[0];
        PushJsonType(fret_type);
        Visit(exp_item);
        PopJsonType();

        //NOTE: workaround for cases like: `return \n trace(...)`
        //      where exp has void type, in this case
        //      we simply ignore exp_node since return will take
        //      effect right before it
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
          var exp = explist.exp()[i];
          Visit(exp);
          ret_type.Add(types.Type(Wrap(exp).eval_type));
        }

        //type checking is in proper order
        for(int i=0;i<explen;++i)
        {
          var exp = explist.exp()[i];
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
    if(defer_stack > 0)
      FireError(ctx, "not within loop construct");

    if(loops_stack == 0)
      FireError(ctx, "not within loop construct");

    PeekAST().AddChild(AST_Util.New_Break());

    return null;
  }

  public override object VisitContinue(bhlParser.ContinueContext ctx)
  {
    if(defer_stack > 0)
      FireError(ctx, "not within loop construct");

    if(loops_stack == 0)
      FireError(ctx, "not within loop construct");

    PeekAST().AddChild(AST_Util.New_Continue());

    return null;
  }

  public override object VisitDecls(bhlParser.DeclsContext ctx)
  {
    var decls = ctx.decl();
    for(int i=0;i<decls.Length;++i)
    {
      var fndecl = decls[i].funcDecl();
      if(fndecl != null)
      {
        Visit(fndecl);
        continue;
      }
      var cldecl = decls[i].classDecl();
      if(cldecl != null)
      {
        Visit(cldecl);
        continue;
      }
      var ifacedecl = decls[i].interfaceDecl();
      if(ifacedecl != null)
      {
        Visit(ifacedecl);
        continue;
      }
      var vdecl = decls[i].varDeclareAssign();
      if(vdecl != null)
      {
        Visit(vdecl);
        continue;
      }

      var edecl = decls[i].enumDecl();
      if(edecl != null)
      {
        Visit(edecl);
        continue;
      }
    }

    return null;
  }

  public override object VisitFuncDecl(bhlParser.FuncDeclContext ctx)
  {
    var func_ast = CommonFuncDecl(module.scope, ctx);
    PeekAST().AddChild(func_ast);
    return null;
  }

  public override object VisitInterfaceDecl(bhlParser.InterfaceDeclContext ctx)
  {
    var name = ctx.NAME().GetText();

    var inherits = new List<InterfaceSymbol>();
    if(ctx.extensions() != null)
    {
      for(int i=0;i<ctx.extensions().NAME().Length;++i)
      {
        var ext_name = ctx.extensions().NAME()[i]; 

        var ext = module.scope.Resolve(ext_name.GetText());
        if(ext is InterfaceSymbol ifs)
        {
          if(inherits.IndexOf(ifs) != -1)
            FireError(ext_name, "interface is inherited already");
          inherits.Add(ifs);
        }
        else if(ext_name.GetText() == name)
          FireError(ext_name, "self inheritance is not allowed");
        else
          FireError(ext_name, "not a valid interface");
      }
    }

    var iface_symb = new InterfaceSymbolScript(Wrap(ctx), name, inherits);

    module.scope.Define(iface_symb);

    for(int i=0;i<ctx.interfaceBlock().interfaceMembers().interfaceMember().Length;++i)
    {
      var ib = ctx.interfaceBlock().interfaceMembers().interfaceMember()[i];

      var fd = ib.interfaceFuncDecl();
      if(fd != null)
      {
        int default_args_num;
        var sig = ParseFuncSignature(ParseType(fd.retType()), fd.funcParams(), out default_args_num);
        if(default_args_num != 0)
          FireError(ib, "default value is not allowed in this context");

        var func_symb = new FuncSymbolScript(null, sig, fd.NAME().GetText(), 0, 0);
        iface_symb.Define(func_symb);

        var func_params = fd.funcParams();
        if(func_params != null)
        {
          var scope_bak = curr_scope;
          curr_scope = func_symb;
          //NOTE: we push some dummy interim AST and later
          //      simply discard it since we don't care about
          //      func args related AST for interfaces
          PushAST(new AST_Interim());
          Visit(func_params);
          PopAST();
          curr_scope = scope_bak;
        }
      }
    }

    return null;
  }

  public override object VisitClassDecl(bhlParser.ClassDeclContext ctx)
  {
    var name = ctx.NAME().GetText();

    var implements = new List<InterfaceSymbol>();
    ClassSymbol super_class = null;
    if(ctx.extensions() != null)
    {
      for(int i=0;i<ctx.extensions().NAME().Length;++i)
      {
        var ext_name = ctx.extensions().NAME()[i]; 

        var ext = module.scope.Resolve(ext_name.GetText());
        if(ext is ClassSymbol cs)
        {
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
          implements.Add(ifs);
        }
        else if(ext_name.GetText() == name)
          FireError(ext_name, "self inheritance is not allowed");
        else
          FireError(ext_name, "not a class or an interface");
      }
    }

    var class_symb = new ClassSymbolScript(Wrap(ctx), name, super_class, implements);

    module.scope.Define(class_symb);

    var ast = new AST_ClassDecl(class_symb);

    for(int i=0;i<ctx.classBlock().classMembers().classMember().Length;++i)
    {
      var cb = ctx.classBlock().classMembers().classMember()[i];

      var vd = cb.varDeclare();
      if(vd != null)
      {
        if(vd.NAME().GetText() == "this")
          FireError(vd.NAME(), "the keyword \"this\" is reserved");

        var fld_symb = new FieldSymbolScript(vd.NAME().GetText(), ParseType(vd.type()));
        class_symb.Define(fld_symb);
      }

      var fd = cb.funcDecl();
      if(fd != null)
      {
        if(fd.NAME().GetText() == "this")
          FireError(fd.NAME(), "the keyword \"this\" is reserved");

        var func_ast = CommonFuncDecl(class_symb, fd);
        ast.func_decls.Add(func_ast);
      }
    }

    CheckInterfaces(ctx, class_symb);

    PeekAST().AddChild(ast);

    return null;
  }

  void CheckInterfaces(bhlParser.ClassDeclContext ctx, ClassSymbolScript class_symb)
  {
    for(int i=0;i<class_symb.implements.Count;++i)
      ValidateInterfaceImplementation(ctx, class_symb.implements[i], class_symb);

    class_symb.UpdateVTable();
  }

  void ValidateInterfaceImplementation(bhlParser.ClassDeclContext ctx, InterfaceSymbol iface, ClassSymbolScript class_symb)
  {
    for(int i=0;i<iface.GetMembers().Count;++i)
    {
      var m = (FuncSymbol)iface.GetMembers()[i];
      var func_symb = class_symb.Resolve(m.name) as FuncSymbol;
      if(func_symb == null || !func_symb.GetSignature().Matches(m.GetSignature()))
        FireError(ctx, "class '" + class_symb.name + "' doesn't implement interface '" + iface.name + "' method '" + m + "'");
    }
  }

  AST_FuncDecl CommonFuncDecl(IScope scope, bhlParser.FuncDeclContext ctx)
  {
    var tp = ParseType(ctx.retType());
    var func_tree = Wrap(ctx);
    func_tree.eval_type = tp.Get();

    string name = ctx.NAME().GetText();

    int default_args_num;
    var func_symb = new FuncSymbolScript(
      func_tree, 
      ParseFuncSignature(tp, ctx.funcParams(), out default_args_num),
      name,
      default_args_num,
      0,
      scope as ClassSymbolScript
    );
    scope.Define(func_symb);

    var ast = AST_Util.New_FuncDecl(func_symb, ctx.Stop.Line);

    curr_scope = func_symb;
    PushFuncDecl(func_symb);

    var func_params = ctx.funcParams();
    if(func_params != null)
    {
      PushAST(ast.fparams());
      Visit(func_params);
      PopAST();
    }

    if(!decls_only)
    {
      PushAST(ast.block());
      Visit(ctx.funcBlock());
      PopAST();

      if(tp.Get() != Types.Void && !return_found.Contains(func_symb))
        FireError(ctx.NAME(), "matching 'return' statement not found");
    }
    
    PopFuncDecl();

    curr_scope = scope;

    func_symb.default_args_num = ast.GetDefaultArgsNum();

    return ast;
  }

  public override object VisitEnumDecl(bhlParser.EnumDeclContext ctx)
  {
    var enum_name = ctx.NAME().GetText();

    //NOTE: currently all enum values are replaced with literals,
    //      so that it doesn't really make sense to create AST for them.
    //      But we do it just for consistency. Later once we have runtime 
    //      type info this will be justified.
    var symb = new EnumSymbolScript(enum_name);
    module.scope.Define(symb);
    curr_scope = symb;

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

    curr_scope = module.scope;

    return null;
  }

  public override object VisitVarDeclareAssign(bhlParser.VarDeclareAssignContext ctx)
  {
    var vd = ctx.varDeclare(); 

    if(decls_only)
    {
      var tr = types.Type(vd.type().GetText());
      var symb = new VariableSymbol(Wrap(vd.NAME()), vd.NAME().GetText(), tr);
      module.scope.Define(symb);
    }
    else
    {
      var assign_exp = ctx.assignExp();

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

      var ast = CommonDeclVar(curr_scope, vd.NAME(), vd.type(), is_ref: false, func_arg: true, write: assign_exp != null);

      if(exp_ast != null)
        PeekAST().AddChild(exp_ast);
      PeekAST().AddChild(ast);

      if(assign_exp != null)
        types.CheckAssign(Wrap(vd.NAME()), Wrap(assign_exp));
    }

    return null;
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
      else if(found_default_arg)
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
      var tmp = vdecls[i];
      var cexp = tmp.callExp();
      var vd = tmp.varDeclare();

      WrappedParseTree ptree = null;
      IType curr_type = null;
      bool is_decl = false;

      if(cexp != null)
      {
        if(assign_exp == null)
          FireError(cexp, "assign expression expected");

        ProcChainedCall(cexp.NAME(), cexp.chainExp(), ref curr_type, cexp.Start.Line, write: true);

        ptree = Wrap(cexp.NAME());
        ptree.eval_type = curr_type;
      }
      else 
      {
        var vd_type = vd.type();

        //check if we declare a var or use an existing one
        if(vd_type == null)
        {
          string vd_name = vd.NAME().GetText(); 
          var vd_symb = curr_scope.Resolve(vd_name) as VariableSymbol;
          if(vd_symb == null)
            FireError(vd, "symbol not resolved");
          curr_type = vd_symb.type.Get();

          ptree = Wrap(vd.NAME());
          ptree.eval_type = curr_type;

          var ast = AST_Util.New_Call(EnumCall.VARW, start_line, vd_symb);
          root.AddChild(ast);
        }
        else
        {
          var ast = CommonDeclVar(curr_scope, vd.NAME(), vd_type, is_ref: false, func_arg: false, write: assign_exp != null);
          root.AddChild(ast);

          is_decl = true;

          ptree = Wrap(vd.NAME()); 
          curr_type = ptree.eval_type;
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

        //TODO: below is quite an ugly hack, fix it traversing the expression first
        //NOTE: temporarily replacing just declared variable with the dummy one when visiting 
        //      assignment expression in order to avoid error like: float k = k
        Symbol disabled_symbol = null;
        Symbol subst_symbol = null;
        if(is_decl)
        {
          var symbols = ((FuncSymbol)curr_scope).GetMembers();
          disabled_symbol = (Symbol)symbols[symbols.Count - 1];
          symbols.RemoveAt(symbols.Count - 1);
          subst_symbol = new VariableSymbol(disabled_symbol.parsed, "#$"+disabled_symbol.name, disabled_symbol.type);
          curr_scope.Define(subst_symbol);
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
          var symbols = ((FuncSymbol)curr_scope).GetMembers();
          symbols.RemoveAt(symbols.IndexOf(subst_symbol));
          curr_scope.Define(disabled_symbol);
        }

        if(pop_json_type)
          PopJsonType();

        var tuple = assign_type as TupleType; 
        if(vdecls.Length > 1)
        {
          if(tuple == null)
            FireError(assign_exp, "multi return expected");

          if(tuple.Count != vdecls.Length)
            FireError(assign_exp, "multi return size doesn't match destination");
        }
        else if(tuple != null)
          FireError(assign_exp, "multi return size doesn't match destination");
      }

      if(assign_type != null)
      {
        var tuple = assign_type as TupleType;
        if(tuple != null)
          types.CheckAssign(ptree, tuple[i].Get());
        else
          types.CheckAssign(ptree, Wrap(assign_exp));
      }
    }
  }

  public override object VisitDeclAssign(bhlParser.DeclAssignContext ctx)
  {
    var vdecls = ctx.varsDeclareOrCallExps().varDeclareOrCallExp();
    var assign_exp = ctx.assignExp();

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
    var tp = ParseType(type_ctx);

    var var_tree = Wrap(name); 
    var_tree.eval_type = tp.Get();

    if(is_ref && !func_arg)
      FireError(name, "'ref' is only allowed in function declaration");

    VariableSymbol symb = func_arg ? 
      (VariableSymbol) new FuncArgSymbol(var_tree, name.GetText(), tp, is_ref) :
      new VariableSymbol(var_tree, name.GetText(), tp);

    symb.scope_level = scope_level;
    curr_scope.Define(symb);

    if(write)
      return AST_Util.New_Call(EnumCall.VARW, name.Symbol.Line, symb);
    else
      return AST_Util.New_VarDecl(symb, is_ref);
  }

  public override object VisitBlock(bhlParser.BlockContext ctx)
  {
    CommonVisitBlock(BlockType.SEQ, ctx.statement(), new_local_scope: false);
    return null;
  }

  public override object VisitFuncBlock(bhlParser.FuncBlockContext ctx)
  {
    CommonVisitBlock(BlockType.FUNC, ctx.block().statement(), new_local_scope: false);
    return null;
  }

  public override object VisitParal(bhlParser.ParalContext ctx)
  {
    CommonVisitBlock(BlockType.PARAL, ctx.block().statement(), new_local_scope: false);
    return null;
  }

  public override object VisitParalAll(bhlParser.ParalAllContext ctx)
  {
    CommonVisitBlock(BlockType.PARAL_ALL, ctx.block().statement(), new_local_scope: false);
    return null;
  }

  public override object VisitDefer(bhlParser.DeferContext ctx)
  {
    ++defer_stack;
    if(defer_stack > 1)
      FireError(ctx, "nested defers are not allowed");
    CommonVisitBlock(BlockType.DEFER, ctx.block().statement(), new_local_scope: false);
    --defer_stack;
    return null;
  }

  public override object VisitIf(bhlParser.IfContext ctx)
  {
    var ast = AST_Util.New_Block(BlockType.IF);

    var main = ctx.mainIf();

    var main_cond = AST_Util.New_Block(BlockType.SEQ);
    PushAST(main_cond);
    Visit(main.exp());
    PopAST();

    types.CheckAssign(Types.Bool, Wrap(main.exp()));

    var func_symb = PeekFuncDecl();
    bool seen_return = return_found.Contains(func_symb);
    return_found.Remove(func_symb);

    ast.AddChild(main_cond);
    PushAST(ast);
    CommonVisitBlock(BlockType.SEQ, main.block().statement(), new_local_scope: false);
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
      var item_cond = AST_Util.New_Block(BlockType.SEQ);
      PushAST(item_cond);
      Visit(item.exp());
      PopAST();

      types.CheckAssign(Types.Bool, Wrap(item.exp()));

      seen_return = return_found.Contains(func_symb);
      return_found.Remove(func_symb);

      ast.AddChild(item_cond);
      PushAST(ast);
      CommonVisitBlock(BlockType.SEQ, item.block().statement(), new_local_scope: false);
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
      CommonVisitBlock(BlockType.SEQ, @else.block().statement(), new_local_scope: false);
      PopAST();

      if(!seen_return && return_found.Contains(func_symb))
        return_found.Remove(func_symb);
    }

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpTernaryIf(bhlParser.ExpTernaryIfContext ctx)
  {
    var ast = AST_Util.New_Block(BlockType.IF); //short-circuit evaluation

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
    var ast = AST_Util.New_Block(BlockType.WHILE);

    ++loops_stack;

    var cond = AST_Util.New_Block(BlockType.SEQ);
    PushAST(cond);
    Visit(ctx.exp());
    PopAST();

    types.CheckAssign(Types.Bool, Wrap(ctx.exp()));

    ast.AddChild(cond);

    PushAST(ast);
    CommonVisitBlock(BlockType.SEQ, ctx.block().statement(), new_local_scope: false);
    PopAST();
    ast.children[ast.children.Count-1].AddChild(AST_Util.New_Continue(jump_marker: true));

    --loops_stack;

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitDoWhile(bhlParser.DoWhileContext ctx)
  {
    var ast = AST_Util.New_Block(BlockType.DOWHILE);

    ++loops_stack;

    PushAST(ast);
    CommonVisitBlock(BlockType.SEQ, ctx.block().statement(), new_local_scope: false);
    PopAST();
    ast.children[ast.children.Count-1].AddChild(AST_Util.New_Continue(jump_marker: true));

    var cond = AST_Util.New_Block(BlockType.SEQ);
    PushAST(cond);
    Visit(ctx.exp());
    PopAST();

    types.CheckAssign(Types.Bool, Wrap(ctx.exp()));

    ast.AddChild(cond);

    --loops_stack;

    PeekAST().AddChild(ast);

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
          var cpo = stmt.callPostOperators();
          if(cpo != null)
            CommonVisitCallPostOperators(cpo);
        }
      }
    }

    var for_cond = ctx.forExp().forCond();
    var for_post_iter = ctx.forExp().forPostIter();

    var ast = AST_Util.New_Block(BlockType.WHILE);

    ++loops_stack;

    var cond = AST_Util.New_Block(BlockType.SEQ);
    PushAST(cond);
    Visit(for_cond);
    PopAST();

    types.CheckAssign(Types.Bool, Wrap(for_cond.exp()));

    ast.AddChild(cond);

    PushAST(ast);
    var block = CommonVisitBlock(BlockType.SEQ, ctx.block().statement(), new_local_scope: false);
    //appending post iteration code
    if(for_post_iter != null)
    {
      PushAST(block);
      
      PeekAST().AddChild(AST_Util.New_Continue(jump_marker: true));
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
          var cpo = stmt.callPostOperators();
          if(cpo != null)
            CommonVisitCallPostOperators(cpo);
        }
      }
      PopAST();
    }
    PopAST();

    --loops_stack;

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitYield(bhlParser.YieldContext ctx)
  {
     int line = ctx.Start.Line;
     var ast = AST_Util.New_Call(EnumCall.FUNC, line, "yield");
     PeekAST().AddChild(ast);
     return null;
  }

  public override object VisitYieldWhile(bhlParser.YieldWhileContext ctx)
  {
    //NOTE: we're going to generate the following code
    //while(cond) { yield() }

    var ast = AST_Util.New_Block(BlockType.WHILE);

    ++loops_stack;

    var cond = AST_Util.New_Block(BlockType.SEQ);
    PushAST(cond);
    Visit(ctx.exp());
    PopAST();

    types.CheckAssign(Types.Bool, Wrap(ctx.exp()));

    ast.AddChild(cond);

    var body = AST_Util.New_Block(BlockType.SEQ);
    int line = ctx.Start.Line;
    body.AddChild(AST_Util.New_Call(EnumCall.FUNC, line, "yield"));
    ast.AddChild(body);

    --loops_stack;

    PeekAST().AddChild(ast);
    return null;
  }

  public override object VisitForeach(bhlParser.ForeachContext ctx)
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
    
    var vod = ctx.foreachExp().varOrDeclare();
    var vd = vod.varDeclare();
    TypeProxy iter_type;
    string iter_str_name = "";
    AST_Tree iter_ast_decl = null;
    VariableSymbol iter_symb = null;
    if(vod.NAME() != null)
    {
      iter_str_name = vod.NAME().GetText();
      iter_symb = curr_scope.Resolve(iter_str_name) as VariableSymbol;
      if(iter_symb == null)
        FireError(vod.NAME(), "symbol is not a valid variable");
      iter_type = iter_symb.type;
    }
    else
    {
      iter_str_name = vd.NAME().GetText();
      iter_ast_decl = CommonDeclVar(curr_scope, vd.NAME(), vd.type(), is_ref: false, func_arg: false, write: false);
      iter_symb = curr_scope.Resolve(iter_str_name) as VariableSymbol;
      iter_type = iter_symb.type;
    }
    var arr_type = (ArrayTypeSymbol)types.TypeArr(iter_type).Get();

    PushJsonType(arr_type);
    var exp = ctx.foreachExp().exp();
    //evaluating array expression
    Visit(exp);
    PopJsonType();
    types.CheckAssign(Wrap(exp), arr_type);

    var arr_tmp_name = "$foreach_tmp" + exp.Start.Line + "_" + exp.Start.Column;
    var arr_tmp_symb = curr_scope.Resolve(arr_tmp_name) as VariableSymbol;
    if(arr_tmp_symb == null)
    {
      arr_tmp_symb = new VariableSymbol(Wrap(exp), arr_tmp_name, types.Type(iter_type));
      curr_scope.Define(arr_tmp_symb);
    }

    var arr_cnt_name = "$foreach_cnt" + exp.Start.Line + "_" + exp.Start.Column;
    var arr_cnt_symb = curr_scope.Resolve(arr_cnt_name) as VariableSymbol;
    if(arr_cnt_symb == null)
    {
      arr_cnt_symb = new VariableSymbol(Wrap(exp), arr_cnt_name, types.Type("int"));
      curr_scope.Define(arr_cnt_symb);
    }

    PeekAST().AddChild(AST_Util.New_Call(EnumCall.VARW, ctx.Start.Line, arr_tmp_symb));
    //declaring counter var
    PeekAST().AddChild(AST_Util.New_VarDecl(arr_cnt_symb, is_ref: false));

    //declaring iterating var
    if(iter_ast_decl != null)
      PeekAST().AddChild(iter_ast_decl);

    var ast = AST_Util.New_Block(BlockType.WHILE);

    ++loops_stack;

    //adding while condition
    var cond = AST_Util.New_Block(BlockType.SEQ);
    var bin_op = AST_Util.New_BinaryOpExp(EnumBinaryOp.LT, ctx.Start.Line);
    bin_op.AddChild(AST_Util.New_Call(EnumCall.VAR, ctx.Start.Line, arr_cnt_symb));
    bin_op.AddChild(AST_Util.New_Call(EnumCall.VAR, ctx.Start.Line, arr_tmp_symb));
    bin_op.AddChild(AST_Util.New_Call(EnumCall.MVAR, ctx.Start.Line, "Count", arr_type, ((FieldSymbol)arr_type.Resolve("Count")).scope_idx));
    cond.AddChild(bin_op);
    ast.AddChild(cond);

    PushAST(ast);
    var block = CommonVisitBlock(BlockType.SEQ, ctx.block().statement(), new_local_scope: false);
    //prepending filling of the iterator var
    block.children.Insert(0, AST_Util.New_Call(EnumCall.VARW, ctx.Start.Line, iter_symb));
    var arr_at = AST_Util.New_Call(EnumCall.ARR_IDX, ctx.Start.Line);
    block.children.Insert(0, arr_at);
    block.children.Insert(0, AST_Util.New_Call(EnumCall.VAR, ctx.Start.Line, arr_cnt_symb));
    block.children.Insert(0, AST_Util.New_Call(EnumCall.VAR, ctx.Start.Line, arr_tmp_symb));

    block.AddChild(AST_Util.New_Continue(jump_marker: true));
    //appending counter increment
    block.AddChild(AST_Util.New_Inc(arr_cnt_symb));
    PopAST();

    --loops_stack;

    PeekAST().AddChild(ast);

    return null;
  }

  AST_Tree CommonVisitBlock(BlockType type, IParseTree[] sts, bool new_local_scope, bool auto_add = true)
  {
    ++scope_level;

    if(new_local_scope)
      curr_scope = new Scope(curr_scope); 

    bool is_paral = 
      type == BlockType.PARAL || 
      type == BlockType.PARAL_ALL;

    var ast = AST_Util.New_Block(type);
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
          var seq = AST_Util.New_Block(BlockType.SEQ);
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

    //NOTE: we need to undefine all symbols which were defined at the current
    //      scope level
    var scope_members = curr_scope.GetMembers();
    for(int m=scope_members.Count;m-- > 0;)
    {
      if(scope_members[m] is VariableSymbol vs && vs.scope_level == scope_level)
        vs.is_out_of_scope = true;
    }
    --scope_level;

    if(new_local_scope)
      curr_scope = curr_scope.GetFallbackScope();

    if(is_paral)
      return_found.Remove(PeekFuncDecl());

    if(auto_add)
      PeekAST().AddChild(ast);
    return ast;
  }
}

public class ModulePath
{
  public string name;
  public string file_path;

  public ModulePath(string name, string file_path)
  {
    this.name = name;
    this.file_path = file_path;
  }
}

public class Module
{
  public string name {
    get {
      return path.name;
    }
  }
  public string file_path {
    get {
      return path.file_path;
    }
  }
  public ModulePath path;
  public Dictionary<string, Module> imports = new Dictionary<string, Module>(); 
  public ModuleScope scope;

  public Module(GlobalScope globs, ModulePath path)
  {
    this.path = path;
    scope = new ModuleScope(path.name, globs);
  }

  public Module(GlobalScope globs, string name, string file_path)
    : this(globs, new ModulePath(name, file_path))
  {}
}

} //namespace bhl
