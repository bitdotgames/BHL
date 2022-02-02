using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Antlr4.Runtime.Dfa;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Sharpen;

namespace bhl {

public class ParseError : Exception
{
  public ParseError(string str)
    : base(str)
  {}
}

public class ErrorLexerListener : IAntlrErrorListener<int>
{
  public virtual void SyntaxError(TextWriter tw, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
  {
    throw new ParseError("@(" + line + "," + charPositionInLine + ") " + (msg.Length > 200 ? msg.Substring(0, 100) + "..." + msg.Substring(msg.Length-100) : msg));
  }
}

public class ErrorStrategy : DefaultErrorStrategy
{
  public override void Sync(Parser recognizer) {}
}

public class ErrorParserListener : IParserErrorListener
{
  public virtual void SyntaxError(TextWriter tw, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
  {
    throw new ParseError("@(" + line + "," + charPositionInLine + ") " + (msg.Length > 200 ? msg.Substring(0, 100) + "..." + msg.Substring(msg.Length-100) : msg));
  }

  public virtual void ReportAmbiguity(Parser recognizer, DFA dfa, int startIndex, int stopIndex, bool exact, BitSet ambigAlts, ATNConfigSet configs)
  {}
  public virtual void ReportAttemptingFullContext(Parser recognizer, DFA dfa, int startIndex, int stopIndex, BitSet conflictingAlts, SimulatorState conflictState)
  {}
  public virtual void ReportContextSensitivity(Parser recognizer, DFA dfa, int startIndex, int stopIndex, int prediction, SimulatorState acceptState)
  {}
}

public class Parsed
{
  public bhlParser.ProgramContext prog;
  public ITokenStream tokens;
}

public class Frontend : bhlBaseVisitor<object>
{
  static int lambda_id = 0;

  static int NextLambdaId()
  {
    Interlocked.Increment(ref lambda_id);
    return lambda_id;
  }

  //NOTE: in 'declarations only' mode only declarations are filled,
  //      full definitions are omitted. This is useful when we only need 
  //      to know which symbols can be imported from the current module
  bool decls_only = false;

  Module curr_module;
  ModuleRegistry mreg;
  ITokenStream tokens;
  ParseTreeProperty<WrappedParseTree> trees = new ParseTreeProperty<WrappedParseTree>();
  //NOTE: current module's scope, it contains only symbols which belong to the current module
  //      and symbols which were imported from other modules, it fallbacks to the global scope
  //      if symbol is not found
  ModuleScope mscope;
  IScope curr_scope;
  int scope_level;

  public static CommonTokenStream Source2Tokens(Stream s)
  {
    var ais = new AntlrInputStream(s);
    var lex = new bhlLexer(ais);
    lex.AddErrorListener(new ErrorLexerListener());
    return new CommonTokenStream(lex);
  }

  public static AST_Module File2AST(string file, GlobalScope globs, ModuleRegistry mr)
  {
    using(var sfs = File.OpenRead(file))
    {
      var mod = new Module(mr.FilePath2ModuleName(file), file);
      return Source2AST(mod, sfs, globs, mr);
    }
  }

  public static bhlParser Source2Parser(Stream src)
  {
    var tokens = Source2Tokens(src);
    var p = new bhlParser(tokens);
    p.AddErrorListener(new ErrorParserListener());
    p.ErrorHandler = new ErrorStrategy();
    return p;
  }
  
  public static AST_Module Source2AST(Module module, Stream src, GlobalScope globs, ModuleRegistry mr, bool decls_only = false)
  {
    try
    {
      var p = Source2Parser(src);

      var parsed = new Parsed();
      parsed.tokens = p.TokenStream;
      parsed.prog = p.program();

      return Parsed2AST(module, parsed, globs, mr, decls_only);
    }
    catch(ParseError e)
    {
      throw new UserError(module.file_path, e.Message);
    }
  }

  public static AST_Module Parsed2AST(Module module, Parsed p, GlobalScope globs, ModuleRegistry mr, bool decls_only = false)
  {
    try
    {
      //var sw1 = System.Diagnostics.Stopwatch.StartNew();
      var f = new Frontend(module, p.tokens, globs, mr, decls_only);
      var ast = f.ParseModule(p.prog);
      //sw1.Stop();
      //Console.WriteLine("Module {0} ({1} sec)", module.norm_path, Math.Round(sw1.ElapsedMilliseconds/1000.0f,2));
      return ast;
    }
    catch(ParseError e)
    {
      throw new UserError(module.file_path, e.Message);
    }
  }

  public static bhlParser.ProgramContext ParseProgram(bhlParser parser, string file_path)
  {
    try
    {
      return parser.program();
    }
    catch(ParseError e)
    {
      throw new UserError(file_path, e.Message);
    }
  }

  public static bhlParser.RetTypeContext ParseType(string type)
  {
    try
    {
      var tokens = Source2Tokens(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(type)));
      var p = new bhlParser(tokens);
      p.AddErrorListener(new ErrorParserListener());
      p.ErrorHandler = new ErrorStrategy();
      return p.retType();
    }
    catch(Exception)
    {
      return null;
    }
  }

  static public void Source2Bin(Module module, Stream src, Stream dst, GlobalScope globs, ModuleRegistry mr)
  {
    var ast = Source2AST(module, src, globs, mr);
    Util.Meta2Bin(ast, dst);
  }

  public void FireError(string msg) 
  {
    //Console.Error.WriteLine(err);
    throw new UserError(curr_module.file_path, msg);
  }

  public Frontend(Module module, ITokenStream tokens, GlobalScope globs, ModuleRegistry mreg, bool decls_only = false)
  {
    if(globs == null)
      throw new Exception("Global scope is not set");

    this.curr_module = module;

    this.tokens = tokens;
    this.decls_only = decls_only;
    this.mscope = new ModuleScope(module.id, globs);
    this.mreg = mreg;

    curr_scope = this.mscope;
  }

  Stack<AST> ast_stack = new Stack<AST>();

  void PushAST(AST ast)
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

  AST PeekAST()
  {
    return ast_stack.Peek();
  }

  public string Location(IParseTree t)
  {
    return Wrap(t).Location();
  }

  public WrappedParseTree Wrap(IParseTree t)
  {
    var n = trees.Get(t);
    if(n == null)
    {
      n = new WrappedParseTree();
      n.tree = t;
      n.tokens = tokens;
      trees.Put(t, n);
    }
    return n;
  }

  public AST_Module ParseModule(bhlParser.ProgramContext p)
  {
    var ast = AST_Util.New_Module(curr_module.id, curr_module.name);
    PushAST(ast);
    VisitProgram(p);
    PopAST();
    return ast;
  }

  public override object VisitProgram(bhlParser.ProgramContext ctx)
  {
    //Console.WriteLine(">>>> PROG VISIT " + curr_module.norm_path + " decls: " + decls_only);
    for(int i=0;i<ctx.progblock().Length;++i)
      Visit(ctx.progblock()[i]);
    return null;
  }

  public override object VisitProgblock(bhlParser.ProgblockContext ctx)
  {
    try
    {
      var imps = ctx.imports();
      if(imps != null)
        Visit(imps);
      
      Visit(ctx.decls()); 
    }
    catch(UserError e)
    {
      //NOTE: if file is not set we need to update it and re-throw the exception
      if(e.file == null)
        e.file = curr_module.file_path;
      throw e;
    }

    return null;
  }

  public override object VisitImports(bhlParser.ImportsContext ctx)
  {
    var res = AST_Util.New_Imports();

    var imps = ctx.mimport();
    for(int i=0;i<imps.Length;++i)
      AddImport(res, imps[i]);

    PeekAST().AddChild(res);
    return null;
  }

  public void AddImport(AST_Import ast, bhlParser.MimportContext ctx)
  {
    var import = ctx.NORMALSTRING().GetText();
    //removing quotes
    import = import.Substring(1, import.Length-2);
    
    var module = mreg.ImportModule(curr_module, mscope.globs, import);
    //NOTE: null means module is already imported
    if(module != null)
    {
      mscope.Append(module.symbols);
      ast.module_ids.Add(module.id);
      ast.module_names.Add(import);
    }
  }

  public override object VisitSymbCall(bhlParser.SymbCallContext ctx)
  {
    var exp = ctx.callExp(); 
    Visit(exp);
    var eval_type = Wrap(exp).eval_type;
    if(eval_type != null && eval_type != TypeSystem.Void)
    {
      //TODO: add a warning?
      var mtype = eval_type as MultiType;
      if(mtype != null)
      {
        for(int i=0;i<mtype.items.Count;++i)
          PeekAST().AddChild(AST_Util.New_PopValue());
      }
      else
        PeekAST().AddChild(AST_Util.New_PopValue());
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
    ClassSymbol curr_class = null;

    if(root_name != null)
    {
      var root_str_name = root_name.GetText();
      var name_symb = curr_scope.Resolve(root_str_name);
      if(name_symb == null)
        FireError(Location(root_name) + " : symbol not resolved");
      if(name_symb.type == null)
        FireError(Location(root_name) + " : bad chain call");
      curr_type = name_symb.type.Get(mscope);
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
          ProcCallChainItem(curr_name, cargs, null, curr_class, ref curr_type, ref pre_call, line, write: false);
          curr_class = null;
          curr_name = null;
        }
        else if(arracc != null)
        {
          ProcCallChainItem(curr_name, null, arracc, curr_class, ref curr_type, ref pre_call, line, write: write && is_last);
          curr_class = null;
          curr_name = null;
        }
        else if(macc != null)
        {
          if(curr_name != null)
            ProcCallChainItem(curr_name, null, null, curr_class, ref curr_type, ref pre_call, line, write: false);

          curr_class = curr_type as ClassSymbol; 
          if(curr_class == null)
            FireError(Location(macc) + " : type doesn't support member access via '.'");

          curr_name = macc.NAME();
        }
      }
    }

    //checking the leftover of the call chain
    if(curr_name != null)
      ProcCallChainItem(curr_name, null, null, curr_class, ref curr_type, ref pre_call, line, write);

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
    ClassSymbol class_scope, 
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
      var name_symb = class_scope == null ? curr_scope.Resolve(str_name) : class_scope.Resolve(str_name);
      if(name_symb == null)
        FireError(Location(name) + " : symbol not resolved");

      var var_symb = name_symb as VariableSymbol;
      var func_symb = name_symb as FuncSymbol;

      //func or method call
      if(cargs != null)
      {
        if(name_symb is FieldSymbol)
          FireError(Location(name) + " : symbol is not a function");

        //func ptr
        if(var_symb != null && var_symb.type.Get(mscope) is FuncType)
        {
          var ftype = var_symb.type.Get(mscope) as FuncType;

          if(class_scope == null)
          {
            ast = AST_Util.New_Call(EnumCall.FUNC_VAR, line, var_symb);
            AddCallArgs(ftype, cargs, ref ast, ref pre_call);
            type = ftype.ret_type.Get(mscope);
          }
          else //func ptr member of class
          {
            PeekAST().AddChild(AST_Util.New_Call(EnumCall.MVAR, line, var_symb, class_scope));
            ast = AST_Util.New_Call(EnumCall.FUNC_MVAR, line);
            AddCallArgs(ftype, cargs, ref ast, ref pre_call);
            type = ftype.ret_type.Get(mscope);
          }
        }
        else if(func_symb != null)
        {
          ast = AST_Util.New_Call(class_scope != null ? EnumCall.MFUNC : EnumCall.FUNC, line, func_symb.name, (func_symb is FuncSymbolScript fss ? fss.decl.module_id : 0), class_scope);
          AddCallArgs(func_symb, cargs, ref ast, ref pre_call);
          type = func_symb.GetReturnType();
        }
        else
        {
          //NOTE: let's try fetching func symbol from the module scope
          func_symb = mscope.Resolve(str_name) as FuncSymbol;
          if(func_symb != null)
          {
            ast = AST_Util.New_Call(EnumCall.FUNC, line, func_symb.name, (func_symb is FuncSymbolScript fss ? fss.decl.module_id : 0));
            AddCallArgs(func_symb, cargs, ref ast, ref pre_call);
            type = func_symb.GetReturnType();
          }
          else
            FireError(Location(name) +  " : symbol is not a function");
        }
      }
      //variable or attribute call
      else
      {
        if(var_symb != null)
        {
          bool is_write = write && arracc == null;
          bool is_global = var_symb.scope is ModuleScope;

          ast = AST_Util.New_Call(class_scope != null ? 
            (is_write ? EnumCall.MVARW : EnumCall.MVAR) : 
            (is_global ? (is_write ? EnumCall.GVARW : EnumCall.GVAR) : (is_write ? EnumCall.VARW : EnumCall.VAR)), 
            line, 
            var_symb.name,
            class_scope != null ? class_scope.Type() : "",
            var_symb.scope_idx,
            var_symb.module_id
          );
          //handling passing by ref for class fields
          if(class_scope != null && PeekCallByRef())
          {
            if(class_scope is ClassSymbolNative)
              FireError(Location(name) +  " : getting field by 'ref' not supported for this class");
            ast.type = EnumCall.MVARREF; 
          }
          type = var_symb.type.Get(mscope);
        }
        else if(func_symb != null)
        {
          var call_func_symb = mscope.Resolve(str_name) as FuncSymbol;
          if(call_func_symb == null)
            FireError(Location(name) +  " : no such function found");
          var func_call_name = call_func_symb.name;

          ast = AST_Util.New_Call(EnumCall.GET_ADDR, line, func_call_name, (call_func_symb is FuncSymbolScript fss ? fss.decl.module_id : 0));
          type = func_symb.type.Get(mscope);
        }
        else
        {
          FireError(Location(name) +  " : symbol usage is not valid");
        }
      }
    }
    else if(cargs != null)
    {
      var ftype = type as FuncType;
      if(ftype == null)
        FireError(Location(cargs) +  " : no func to call");
      
      ast = AST_Util.New_Call(EnumCall.LMBD, line);
      AddCallArgs(ftype, cargs, ref ast, ref pre_call);
      type = ftype.ret_type.Get(mscope);
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
      FireError(Location(arracc) +  " : accessing not an array type '" + type.GetName() + "'");

    var arr_exp = arracc.exp();
    Visit(arr_exp);

    if(Wrap(arr_exp).eval_type != TypeSystem.Int)
      FireError(Location(arr_exp) +  " : array index expression is not of type int");

    type = arr_type.item_type.Get(mscope);

    var ast = AST_Util.New_Call(write ? EnumCall.ARR_IDXW : EnumCall.ARR_IDX, line);
    ast.scope_type = arr_type.Type();

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
          FireError(Location(ca_name) +  " : no such named argument");

        if(norm_cargs[idx].ca != null)
          FireError(Location(ca_name) +  " : argument already passed before");
      }
      
      if(idx >= func_args.Count)
        FireError(Location(ca) +  " : there is no argument " + (idx + 1) + ", total arguments " + func_args.Count);

      if(idx >= func_args.Count)
        FireError(Location(ca) +  " : too many arguments for function");

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
          FireError(Location(next_arg) +  " : missing argument '" + norm_cargs[i].orig.name + "'");
        }
        else
        {
          //NOTE: for func bind symbols we assume default arguments  
          //      are specified manually in bindings
          if(func_symb is FuncSymbolNative || func_symb.GetDefaultArgsExprAt(i) != null)
          {
            int default_arg_idx = i - required_args_num;
            if(!args_info.UseDefaultArg(default_arg_idx, true))
              FireError(Location(next_arg) +  " : max default arguments reached");
          }
          else
            FireError(Location(next_arg) +  " : missing argument '" + norm_cargs[i].orig.name + "'");
        }
      }
      else
      {
        prev_ca = ca;
        if(!args_info.IncArgsNum())
          FireError(Location(ca) +  " : max arguments reached");

        var func_arg_symb = (Symbol)func_args[i];
        var func_arg_type = func_arg_symb.parsed == null ? func_arg_symb.type.Get(mscope) : func_arg_symb.parsed.eval_type;  

        bool is_ref = ca.isRef() != null;
        if(!is_ref && func_symb.IsArgRefAt(i))
          FireError(Location(ca) +  " : 'ref' is missing");
        else if(is_ref && !func_symb.IsArgRefAt(i))
          FireError(Location(ca) +  " : argument is not a 'ref'");

        PushCallByRef(is_ref);
        PushJsonType(func_arg_type);
        PushAST(new AST_Interim());
        Visit(ca);
        TryProtectStackInterleaving(ca, func_arg_type, i, ref pre_call);
        PopAddOptimizeAST();
        PopJsonType();
        PopCallByRef();

        var wca = Wrap(ca);

        //NOTE: if symbol is from bindings we don't have a source node attached to it
        if(func_arg_symb.parsed == null)
        {
          if(func_arg_symb.type.Get(mscope) == null)
            FireError(Location(ca) +  " : invalid type");
          TypeSystem.CheckAssign(func_arg_symb.type.Get(mscope), wca);
        }
        else
          TypeSystem.CheckAssign(func_arg_symb.parsed, wca);
      }
    }

    PopAST();

    call.cargs_bits = args_info.bits;
  }

  void AddCallArgs(FuncType func_type, bhlParser.CallArgsContext cargs, ref AST_Call call, ref AST_Interim pre_call)
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
        FireError(Location(next_arg) +  " : missing argument of type '" + arg_type_ref.name + "'");
      }

      var ca = cargs.callArg()[i];
      var ca_name = ca.NAME();

      if(ca_name != null)
        FireError(Location(ca_name) +  " : named arguments not supported for function pointers");

      var arg_type = arg_type_ref.Get(mscope);
      PushJsonType(arg_type);
      PushAST(new AST_Interim());
      Visit(ca);
      TryProtectStackInterleaving(ca, arg_type, i, ref pre_call);
      PopAddOptimizeAST();
      PopJsonType();

      var wca = Wrap(ca);
      TypeSystem.CheckAssign(arg_type, wca);

      if(arg_type_ref.is_ref && ca.isRef() == null)
        FireError(Location(ca) +  " : 'ref' is missing");
      else if(!arg_type_ref.is_ref && ca.isRef() != null)
        FireError(Location(ca) +  " : argument is not a 'ref'");

      prev_ca = ca;
    }
    PopAST();

    if(ca_len != func_args.Count)
      FireError(Location(cargs) +  " : too many arguments");

    var args_info = new FuncArgsInfo();
    if(!args_info.SetArgsNum(func_args.Count))
      FireError(Location(cargs) +  " : max arguments reached");
    call.cargs_bits = args_info.bits;
  }

  static bool HasFuncCalls(AST ast)
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
      if(ast.children[i] is AST sub)
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

    var var_tmp_symb = new VariableSymbol(Wrap(ca), "$_tmp_" + ca.Start.Line + "_" + ca.Start.Column, new TypeRef(func_arg_type));
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

  void CommonVisitLambda(IParseTree ctx, bhlParser.FuncLambdaContext funcLambda)
  {
    var tr = mscope.Type(funcLambda.retType());
    if(tr.type == null)
      FireError(Location(tr.parsed) + " : type '" + tr.name + "' not found");

    var func_name = curr_module.id + "_lmb_" + NextLambdaId(); 
    var ast = AST_Util.New_LambdaDecl(func_name, curr_module.id, tr.name);
    var symb = new LambdaSymbol(
      Wrap(ctx), funcLambda,
      mscope, ast, tr, 
      this.func_decl_stack
    );

    PushFuncDecl(symb);

    var scope_backup = curr_scope;
    curr_scope = symb;

    var fparams = funcLambda.funcParams();
    if(fparams != null)
    {
      PushAST(ast.fparams());
      Visit(fparams);
      PopAST();
    }

    mscope.Define(symb);

    //NOTE: while we are inside lambda the eval type is the return type of
    Wrap(ctx).eval_type = symb.GetReturnType();

    PushAST(ast.block());
    Visit(funcLambda.funcBlock());
    PopAST();

    PopFuncDecl();

    //NOTE: once we are out of lambda the eval type is the lambda itself
    var curr_type = symb.type.Get(mscope); 
    Wrap(ctx).eval_type = curr_type;

    curr_scope = scope_backup;

    ast.local_vars_num = (uint)symb.GetMembers().Count;

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
      var tr = mscope.Type(new_exp.type());
      if(tr.type == null)
        FireError(Location(new_exp.type()) + " : type '" + tr.name + "' not found");
      PushJsonType(tr.type);
    }

    var curr_type = PeekJsonType();

    if(curr_type == null)
      FireError(Location(ctx) + " : {..} not expected");

    if(!(curr_type is ClassSymbol) || (curr_type is ArrayTypeSymbol))
      FireError(Location(ctx) + " : type '" + curr_type + "' can't be specified with {..}");

    Wrap(ctx).eval_type = curr_type;
    var root_type_name = curr_type.GetName();

    var ast = AST_Util.New_JsonObj(root_type_name);

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
      FireError(Location(ctx) + " : [..] not expected");

    if(!(curr_type is ArrayTypeSymbol))
      FireError(Location(ctx) + " : [..] is not expected, need '" + curr_type + "'");

    var arr_type = curr_type as ArrayTypeSymbol;
    var orig_type = arr_type.item_type.Get(mscope);
    if(orig_type == null)
      FireError(Location(ctx) + " : type '" + arr_type.item_type.name + "' not found");
    PushJsonType(orig_type);

    var ast = AST_Util.New_JsonArr(arr_type);

    PushAST(ast);
    var vals = ctx.jsonValue();
    for(int i=0;i<vals.Length;++i)
    {
      Visit(vals[i]);
      //the last item is added implicitely
      if(i+1 < vals.Length)
        ast.AddChild(AST_Util.New_JsonArrAddItem());
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
      FireError(Location(ctx) + " : expecting class type, got '" + curr_type + "' instead");

    var name_str = ctx.NAME().GetText();
    
    var member = scoped_symb.Resolve(name_str);
    if(member == null)
      FireError(Location(ctx) + " : no such attribute '" + name_str + "' in class '" + scoped_symb.name + "'");

    int name_idx = scoped_symb.GetMembers().FindStringKeyIndex(name_str);
    if(name_idx == -1)
      throw new Exception("Symbol index not found for '" + name_str + "'");
    var ast = AST_Util.New_JsonPair(curr_type.GetName(), name_str, name_idx);

    PushJsonType(member.type.Get(mscope));

    var jval = ctx.jsonValue(); 
    PushAST(ast);
    Visit(jval);
    PopAST();

    PopJsonType();

    Wrap(ctx).eval_type = member.type.Get(mscope);

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

      TypeSystem.CheckAssign(curr_type, Wrap(exp));
    }
    else if(jobj != null)
      Visit(jobj);
    else
      Visit(jarr);

    return null;
  }

  public override object VisitExpTypeid(bhlParser.ExpTypeidContext ctx)
  {
    var type = ctx.typeid().type();
    var tr = mscope.Type(type);
    if(tr.type == null)
      FireError(Location(tr.parsed) +  " : type '" + tr.name + "' not found");

    Wrap(ctx).eval_type = TypeSystem.Int;

    var ast = AST_Util.New_Literal(EnumLiteral.NUM);
    ast.nval = Hash.CRC28(tr.name);
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpStaticCall(bhlParser.ExpStaticCallContext ctx)
  {
    var exp = ctx.staticCallExp(); 
    var ctx_name = exp.NAME();
    var enum_symb = mscope.Resolve(ctx_name.GetText()) as EnumSymbol;
    if(enum_symb == null)
      FireError(Location(ctx) + " : type '" + ctx_name + "' not found");

    var item_name = exp.staticCallItem().NAME();
    var enum_val = enum_symb.FindValue(item_name.GetText());

    if(enum_val == null)
      FireError(Location(ctx) + " : enum value not found '" + item_name.GetText() + "'");

    Wrap(ctx).eval_type = enum_symb;

    var ast = AST_Util.New_Literal(EnumLiteral.NUM);
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
    var tr = mscope.Type(ctx.newExp().type());
    if(tr.type == null)
      FireError(Location(tr.parsed) + " : type '" + tr.name + "' not found");

    var ast = AST_Util.New_New((ClassSymbol)tr.type);
    Wrap(ctx).eval_type = tr.type;
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
    var tr = mscope.Type(ctx.type());
    if(tr.type == null)
      FireError(Location(tr.parsed) + " : type '" + tr.name + "' not found");

    var ast = AST_Util.New_TypeCast(tr.name);
    var exp = ctx.exp();
    PushAST(ast);
    Visit(exp);
    PopAST();

    Wrap(ctx).eval_type = tr.type;

    TypeSystem.CheckCast(Wrap(ctx), Wrap(exp)); 

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
      TypeSystem.TypeForUnaryMinus(Wrap(exp)) : 
      TypeSystem.TypeForLogicalNot(Wrap(exp));

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
      FireError(Location(ctx.NAME()) + " : symbol not resolved");

    if(!TypeSystem.IsRtlOpCompatible(vlhs.type.type))
      throw new UserError(Location(ctx.NAME()) + " : incompatible types");

    var op = $"{ctx.operatorPostOpAssign().GetText()[0]}";
    var op_type = GetBinaryOpType(op);
    AST bin_op_ast = AST_Util.New_BinaryOpExp(op_type);

    PushAST(bin_op_ast);
    bin_op_ast.AddChild(AST_Util.New_Call(EnumCall.VAR, ctx.Start.Line, vlhs));
    Visit(ctx.exp());
    PopAST();

    TypeSystem.CheckAssign(vlhs.type.type, Wrap(ctx.exp()));

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
      FireError(Location(v) + " : symbol not resolved");
    
    var wv = Wrap(v);
    bool is_negative = ctx.decrementOperator() != null;
    
    if(!TypeSystem.IsRtlOpCompatible(vs.type.type)) // only numeric types
    {
      throw new UserError(
        $"{wv.Location()} : operator {(is_negative ? "--" : "++")} is not supported for {vs.type.name} type"
      );
    }
    
    if(is_negative)
      ast.AddChild(AST_Util.New_Dec(vs));
    else
      ast.AddChild(AST_Util.New_Inc(vs));
    
    Wrap(ctx).eval_type = TypeSystem.Void;
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
    AST ast = AST_Util.New_BinaryOpExp(op_type);
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

      Wrap(ctx).eval_type = TypeSystem.TypeForBinOpOverload(mscope, wlhs, wrhs, op_func);

      //NOTE: replacing original AST, a bit 'dirty' but kinda OK
      var over_ast = new AST_Interim();
      for(int i=0;i<ast.children.Count;++i)
        over_ast.AddChild(ast.children[i]);
      over_ast.AddChild(AST_Util.New_Call(EnumCall.MFUNC, ctx.Start.Line, op, 0, class_symb));
      ast = over_ast;
    }
    else if(
      op_type == EnumBinaryOp.EQ || 
      op_type == EnumBinaryOp.NQ
    )
      Wrap(ctx).eval_type = TypeSystem.TypeForEqOp(wlhs, wrhs);
    else if(
      op_type == EnumBinaryOp.GT || 
      op_type == EnumBinaryOp.GTE ||
      op_type == EnumBinaryOp.LT || 
      op_type == EnumBinaryOp.LTE
    )
      Wrap(ctx).eval_type = TypeSystem.TypeForRtlOp(wlhs, wrhs);
    else
      Wrap(ctx).eval_type = TypeSystem.TypeForBinOp(wlhs, wrhs);

    PeekAST().AddChild(ast);
  }

  public override object VisitExpBitAnd(bhlParser.ExpBitAndContext ctx)
  {
    var ast = AST_Util.New_BinaryOpExp(EnumBinaryOp.BIT_AND);
    var exp_0 = ctx.exp(0);
    var exp_1 = ctx.exp(1);

    PushAST(ast);
    Visit(exp_0);
    Visit(exp_1);
    PopAST();

    Wrap(ctx).eval_type = TypeSystem.TypeForBitOp(Wrap(exp_0), Wrap(exp_1));

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpBitOr(bhlParser.ExpBitOrContext ctx)
  {
    var ast = AST_Util.New_BinaryOpExp(EnumBinaryOp.BIT_OR);
    var exp_0 = ctx.exp(0);
    var exp_1 = ctx.exp(1);

    PushAST(ast);
    Visit(exp_0);
    Visit(exp_1);
    PopAST();

    Wrap(ctx).eval_type = TypeSystem.TypeForBitOp(Wrap(exp_0), Wrap(exp_1));

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpAnd(bhlParser.ExpAndContext ctx)
  {
    var ast = AST_Util.New_BinaryOpExp(EnumBinaryOp.AND);
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

    Wrap(ctx).eval_type = TypeSystem.TypeForLogicalOp(Wrap(exp_0), Wrap(exp_1));

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpOr(bhlParser.ExpOrContext ctx)
  {
    var ast = AST_Util.New_BinaryOpExp(EnumBinaryOp.OR);
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

    Wrap(ctx).eval_type = TypeSystem.TypeForLogicalOp(Wrap(exp_0), Wrap(exp_1));

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralNum(bhlParser.ExpLiteralNumContext ctx)
  {
    var ast = AST_Util.New_Literal(EnumLiteral.NUM);

    var number = ctx.number();
    var int_num = number.INT();
    var flt_num = number.FLOAT();
    var hex_num = number.HEX();

    if(int_num != null)
    {
      Wrap(ctx).eval_type = TypeSystem.Int;
      ast.nval = double.Parse(int_num.GetText(), System.Globalization.CultureInfo.InvariantCulture);
    }
    else if(flt_num != null)
    {
      Wrap(ctx).eval_type = TypeSystem.Float;
      ast.nval = double.Parse(flt_num.GetText(), System.Globalization.CultureInfo.InvariantCulture);
    }
    else if(hex_num != null)
    {
      Wrap(ctx).eval_type = TypeSystem.Int;
      ast.nval = Convert.ToUInt32(hex_num.GetText(), 16);
    }

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralFalse(bhlParser.ExpLiteralFalseContext ctx)
  {
    Wrap(ctx).eval_type = TypeSystem.Bool;

    var ast = AST_Util.New_Literal(EnumLiteral.BOOL);
    ast.nval = 0;
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralNull(bhlParser.ExpLiteralNullContext ctx)
  {
    Wrap(ctx).eval_type = TypeSystem.Null;

    var ast = AST_Util.New_Literal(EnumLiteral.NIL);
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralTrue(bhlParser.ExpLiteralTrueContext ctx)
  {
    Wrap(ctx).eval_type = TypeSystem.Bool;

    var ast = AST_Util.New_Literal(EnumLiteral.BOOL);
    ast.nval = 1;
    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpLiteralStr(bhlParser.ExpLiteralStrContext ctx)
  {
    Wrap(ctx).eval_type = TypeSystem.String;

    var ast = AST_Util.New_Literal(EnumLiteral.STR);
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

  //NOTE: a list is used instead of stack, so that it's easier to traverse by index
  public List<FuncSymbol> func_decl_stack = new List<FuncSymbol>();

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

  Stack<IType> json_type_stack = new Stack<IType>();

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

  Stack<bool> call_by_ref_stack = new Stack<bool>();

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

  public override object VisitReturn(bhlParser.ReturnContext ctx)
  {
    if(defer_stack > 0)
      FireError(Location(ctx) + " : return is not allowed in defer block");

    var func_symb = PeekFuncDecl();
    if(func_symb == null)
      FireError(Location(ctx) + " : return statement is not in function");
    
    func_symb.return_statement_found = true;

    var ret_ast = AST_Util.New_Return();
    
    var explist = ctx.explist();
    if(explist != null)
    {
      int explen = explist.exp().Length;

      var fret_type = func_symb.GetReturnType();

      //NOTE: immediately adding return node in case of void return type
      if(fret_type == TypeSystem.Void)
        PeekAST().AddChild(ret_ast);
      else
        PushAST(ret_ast);

      var fmret_type = fret_type as MultiType;

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
        if(Wrap(exp_item).eval_type != TypeSystem.Void)
          ret_ast.num = fmret_type != null ? fmret_type.items.Count : 1;

        TypeSystem.CheckAssign(func_symb.parsed, Wrap(exp_item));
        Wrap(ctx).eval_type = Wrap(exp_item).eval_type;
      }
      else
      {
        if(fmret_type == null)
          FireError(Location(ctx) + " : function doesn't support multi return");

        if(fmret_type.items.Count != explen)
          FireError(Location(ctx) + " : multi return size doesn't match destination");

        var ret_type = new MultiType();

        //NOTE: we traverse expressions in reversed order so that returned
        //      values are properly placed on a stack
        for(int i=explen;i-- > 0;)
        {
          var exp = explist.exp()[i];
          Visit(exp);
          ret_type.items.Add(new TypeRef(Wrap(exp).eval_type));
        }

        //type checking is in proper order
        for(int i=0;i<explen;++i)
        {
          var exp = explist.exp()[i];
          TypeSystem.CheckAssign(fmret_type.items[i].Get(mscope), Wrap(exp));
        }

        ret_type.Update();
        Wrap(ctx).eval_type = ret_type;

        ret_ast.num = fmret_type.items.Count;
      }

      if(fret_type != TypeSystem.Void)
      {
        PopAST();
        PeekAST().AddChild(ret_ast);
      }
    }
    else
    {
      if(func_symb.GetReturnType() != TypeSystem.Void)
        FireError(Location(ctx) + " : return value is missing");
      Wrap(ctx).eval_type = TypeSystem.Void;
      PeekAST().AddChild(ret_ast);
    }

    return null;
  }

  public override object VisitBreak(bhlParser.BreakContext ctx)
  {
    if(defer_stack > 0)
      FireError(Location(ctx) + " : not within loop construct");

    if(loops_stack == 0)
      FireError(Location(ctx) + " : not within loop construct");

    PeekAST().AddChild(AST_Util.New_Break());

    return null;
  }

  public override object VisitContinue(bhlParser.ContinueContext ctx)
  {
    if(defer_stack > 0)
      FireError(Location(ctx) + " : not within loop construct");

    if(loops_stack == 0)
      FireError(Location(ctx) + " : not within loop construct");

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
    var func_ast = CommonFuncDecl(ctx, mscope);
    PeekAST().AddChild(func_ast);
    return null;
  }

  public override object VisitClassDecl(bhlParser.ClassDeclContext ctx)
  {
    var class_name = ctx.NAME().GetText();

    ClassSymbol super_class = null;
    if(ctx.classEx() != null)
    {
      super_class = mscope.Resolve(ctx.classEx().NAME().GetText()) as ClassSymbol;
      if(super_class == null)
        FireError(Location(ctx.classEx()) + " : parent class symbol not resolved");
    }

    var ast = AST_Util.New_ClassDecl(class_name, super_class == null ? "" : super_class.name);
    var class_symb = new ClassSymbolScript(class_name, ast, super_class);

    if(decls_only)
      curr_module.symbols.Define(class_symb);

    mscope.Define(class_symb);

    for(int i=0;i<ctx.classBlock().classMembers().classMember().Length;++i)
    {
      var cb = ctx.classBlock().classMembers().classMember()[i];

      var vd = cb.varDeclare();
      if(vd != null)
      {
        if(vd.NAME().GetText() == "this")
          FireError("the keyword \"this\" is reserved");

        var decl = CommonDeclVar(class_symb, vd.NAME(), vd.type(), is_ref: false, func_arg: false, write: false);
        //NOTE: forcing name to be always present due to current class members declaration requirement
        (decl as AST_VarDecl).name = vd.NAME().GetText();
        ast.AddChild(decl);
      }

      var fd = cb.funcDecl();
      if(fd != null)
      {
        if(fd.NAME().GetText() == "this")
          FireError("the keyword \"this\" is reserved");

        var func_ast = CommonFuncDecl(fd, class_symb);
        ast.AddChild(func_ast);
      }
    }

    PeekAST().AddChild(ast);

    return null;
  }

  AST_FuncDecl CommonFuncDecl(bhlParser.FuncDeclContext context, IScope scope)
  {
    var tr = mscope.Type(context.retType());

    if(tr.type == null)
      FireError(Location(tr.parsed) + " : type '" + tr.name + "' not found");

    var fstr_name = context.NAME().GetText();

    var func_node = Wrap(context);
    func_node.eval_type = tr.type;

    var ast = AST_Util.New_FuncDecl(fstr_name, curr_module.id, tr.name);

    var func_symb = new FuncSymbolScript(
      mscope, 
      ast, 
      func_node, 
      tr, 
      context.funcParams()
    );
    scope.Define(func_symb);

    if(scope is ClassSymbolScript class_scope)
    {
      var this_symb = new VariableSymbol(func_node, "this", new TypeRef(class_scope));
      func_symb.Define(this_symb);
    }
    else if(decls_only)
      curr_module.symbols.Define(func_symb);

    curr_scope = func_symb;
    PushFuncDecl(func_symb);

    var func_params = context.funcParams();
    if(func_params != null)
    {
      PushAST(ast.fparams());
      Visit(func_params);
      PopAST();
    }

    if(!decls_only)
    {
      PushAST(ast.block());
      Visit(context.funcBlock());
      PopAST();

      if(tr.type != TypeSystem.Void && !func_symb.return_statement_found)
        FireError(Location(context.NAME()) + " : matching 'return' statement not found");
    }
    
    PopFuncDecl();

    curr_scope = scope;

    ast.local_vars_num = (uint)func_symb.GetMembers().Count;
    ast.required_args_num = (byte)func_symb.GetRequiredArgsNum();
    ast.default_args_num = (byte)func_symb.GetDefaultArgsNum();

    return ast;
  }

  public override object VisitEnumDecl(bhlParser.EnumDeclContext ctx)
  {
    var enum_name = ctx.NAME().GetText();

    //NOTE: currently all enum values are replaced with literals,
    //      so that it doesn't really make sense to create AST for them.
    //      But we do it just for consistency. Later once we have runtime 
    //      type info this will be justified.
    var ast = AST_Util.New_EnumDecl(enum_name);

    var symb = new EnumSymbolScript(mscope, enum_name);
    if(decls_only)
      curr_module.symbols.Define(symb);
    mscope.Define(symb);
    curr_scope = symb;

    for(int i=0;i<ctx.enumBlock().enumMember().Length;++i)
    {
      var em = ctx.enumBlock().enumMember()[i];
      var em_name = em.NAME().GetText();
      int em_val = int.Parse(em.INT().GetText(), System.Globalization.CultureInfo.InvariantCulture);

      int res = symb.TryAddItem(em_name, em_val);
      if(res == 1)
        FireError(Location(em.NAME()) + " : duplicate key '" + em_name + "'");
      else if(res == 2)
        FireError(Location(em.INT()) + " : duplicate value '" + em_val + "'");

      var ast_item = new AST_EnumItem();
      ast_item.name = enum_name;
      ast_item.value = em_val;
      ast.AddChild(ast_item);
    }

    curr_scope = mscope;

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitVarDeclareAssign(bhlParser.VarDeclareAssignContext ctx)
  {
    var vd = ctx.varDeclare(); 

    if(decls_only)
    {
      var tr = mscope.Type(vd.type().GetText());
      var symb = new VariableSymbol(Wrap(vd.NAME()), vd.NAME().GetText(), tr);
      curr_module.symbols.Define(symb);
      mscope.Define(symb);
    }
    else
    {
      var assign_exp = ctx.assignExp();

      AST_Interim exp_ast = null;
      if(assign_exp != null)
      {
        var tr = mscope.Type(vd.type());
        if(tr.type == null)
          FireError(Location(tr.parsed) +  " : type '" + tr.name + "' not found");

        exp_ast = new AST_Interim();
        PushAST(exp_ast);
        PushJsonType(tr.type);
        Visit(assign_exp);
        PopJsonType();
        PopAST();
      }

      var ast = CommonDeclVar(curr_scope, vd.NAME(), vd.type(), is_ref: false, func_arg: true, write: assign_exp != null);

      if(exp_ast != null)
        PeekAST().AddChild(exp_ast);
      PeekAST().AddChild(ast);

      if(assign_exp != null)
        TypeSystem.CheckAssign(Wrap(vd.NAME()), Wrap(assign_exp));
    }

    return null;
  }

  public override object VisitFuncParams(bhlParser.FuncParamsContext ctx)
  {
    var func = curr_scope as FuncSymbol;

    var fparams = ctx.funcParamDeclare();
    bool found_default_arg = false;

    for(int i=0;i<fparams.Length;++i)
    {
      var fp = fparams[i]; 
      if(fp.assignExp() != null)
        found_default_arg = true;
      else if(found_default_arg)
        FireError(Location(fp.NAME()) + " : missing default argument expression");

      bool pop_json_type = false;
      if(found_default_arg)
      {
        var tr = mscope.Type(fp.type());
        PushJsonType(tr.Get(mscope));
        pop_json_type = true;
      }

      Visit(fp);

      if(pop_json_type)
        PopJsonType();

      func.DefineArg(fp.NAME().GetText());
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
          FireError(Location(cexp) + " : assign expression expected");

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
            FireError(Location(vd) + " : symbol not resolved");
          curr_type = vd_symb.type.Get(mscope);

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
            FireError(Location(assign_exp) + " : multi assign not supported for JSON expression");

          pop_json_type = true;

          PushJsonType(curr_type);
        }

        //TODO: below is quite an ugly hack, fix it traversing the expression first
        //NOTE: temporarily replacing just declared variable with dummy one when visiting 
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

        var mtype = assign_type as MultiType; 
        if(vdecls.Length > 1)
        {
          if(mtype == null)
            FireError(Location(assign_exp) + " : multi return expected");

          if(mtype.items.Count != vdecls.Length)
            FireError(Location(assign_exp) + " : multi return size doesn't match destination");
        }
        else if(mtype != null)
          FireError(Location(assign_exp) + " : multi return size doesn't match destination");
      }

      if(assign_type != null)
      {
        var mtype = assign_type as MultiType;
        if(mtype != null)
          TypeSystem.CheckAssign(ptree, mtype.items[i].Get(mscope));
        else
          TypeSystem.CheckAssign(ptree, Wrap(assign_exp));
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
        FireError(Location(name) +  " : 'ref' is not allowed to have a default value");
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
      TypeSystem.CheckAssign(Wrap(name), Wrap(assign_exp));
    return null;
  }

  AST CommonDeclVar(IScope curr_scope, ITerminalNode name, bhlParser.TypeContext type, bool is_ref, bool func_arg, bool write)
  {
    var str_name = name.GetText();

    var tr = mscope.Type(type);
    if(tr.type == null)
      FireError(Location(tr.parsed) +  " : type '" + tr.name + "' not found");

    var var_node = Wrap(name); 
    var_node.eval_type = tr.type;

    if(is_ref && !func_arg)
      FireError(Location(name) +  " : 'ref' is only allowed in function declaration");

    VariableSymbol symb = func_arg ? 
      (VariableSymbol) new FuncArgSymbol(var_node, str_name, tr, is_ref) :
      (VariableSymbol) new VariableSymbol(var_node, str_name, tr);

    symb.scope_level = scope_level;
    curr_scope.Define(symb);

    if(write)
      return AST_Util.New_Call(EnumCall.VARW, name.Symbol.Line, symb);
    else
      return AST_Util.New_VarDecl(symb, is_ref);
  }

  public override object VisitBlock(bhlParser.BlockContext ctx)
  {
    CommonVisitBlock(EnumBlock.SEQ, ctx.statement(), new_local_scope: false);
    return null;
  }

  public override object VisitFuncBlock(bhlParser.FuncBlockContext ctx)
  {
    CommonVisitBlock(EnumBlock.FUNC, ctx.block().statement(), new_local_scope: false);
    return null;
  }

  public override object VisitParal(bhlParser.ParalContext ctx)
  {
    CommonVisitBlock(EnumBlock.PARAL, ctx.block().statement(), new_local_scope: false);
    return null;
  }

  public override object VisitParalAll(bhlParser.ParalAllContext ctx)
  {
    CommonVisitBlock(EnumBlock.PARAL_ALL, ctx.block().statement(), new_local_scope: false);
    return null;
  }

  public override object VisitDefer(bhlParser.DeferContext ctx)
  {
    ++defer_stack;
    if(defer_stack > 1)
      FireError(Location(ctx) + " : nested defers are not allowed");
    CommonVisitBlock(EnumBlock.DEFER, ctx.block().statement(), new_local_scope: false);
    --defer_stack;
    return null;
  }

  public override object VisitIf(bhlParser.IfContext ctx)
  {
    var ast = AST_Util.New_Block(EnumBlock.IF);

    var main = ctx.mainIf();

    var main_cond = AST_Util.New_Block(EnumBlock.SEQ);
    PushAST(main_cond);
    Visit(main.exp());
    PopAST();

    TypeSystem.CheckAssign(TypeSystem.Bool, Wrap(main.exp()));

    var func_symb = PeekFuncDecl();
    bool seen_return = func_symb.return_statement_found;
    func_symb.return_statement_found = false;

    ast.AddChild(main_cond);
    PushAST(ast);
    CommonVisitBlock(EnumBlock.SEQ, main.block().statement(), new_local_scope: false);
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
    if(!seen_return && func_symb.return_statement_found && (ctx.elseIf() == null || ctx.@else() == null))
      func_symb.return_statement_found = false;

    var else_if = ctx.elseIf();
    for(int i=0;i<else_if.Length;++i)
    {
      var item = else_if[i];
      var item_cond = AST_Util.New_Block(EnumBlock.SEQ);
      PushAST(item_cond);
      Visit(item.exp());
      PopAST();

      TypeSystem.CheckAssign(TypeSystem.Bool, Wrap(item.exp()));

      seen_return = func_symb.return_statement_found;
      func_symb.return_statement_found = false;

      ast.AddChild(item_cond);
      PushAST(ast);
      CommonVisitBlock(EnumBlock.SEQ, item.block().statement(), new_local_scope: false);
      PopAST();

      if(!seen_return && func_symb.return_statement_found)
        func_symb.return_statement_found = false;
    }

    var @else = ctx.@else();
    if(@else != null)
    {
      seen_return = func_symb.return_statement_found;
      func_symb.return_statement_found = false;

      PushAST(ast);
      CommonVisitBlock(EnumBlock.SEQ, @else.block().statement(), new_local_scope: false);
      PopAST();

      if(!seen_return && func_symb.return_statement_found)
        func_symb.return_statement_found = false;
    }

    PeekAST().AddChild(ast);

    return null;
  }

  public override object VisitExpTernaryIf(bhlParser.ExpTernaryIfContext ctx)
  {
    var ast = AST_Util.New_Block(EnumBlock.IF); //short-circuit evaluation

    var exp_0 = ctx.exp();
    var exp_1 = ctx.ternaryIfExp().exp(0);
    var exp_2 = ctx.ternaryIfExp().exp(1);

    var condition = new AST_Interim();
    PushAST(condition);
    Visit(exp_0);
    PopAST();

    TypeSystem.CheckAssign(TypeSystem.Bool, Wrap(exp_0));

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

    TypeSystem.CheckAssign(wrap_exp_1, Wrap(exp_2));
    PeekAST().AddChild(ast);
    return null;
  }

  public override object VisitWhile(bhlParser.WhileContext ctx)
  {
    var ast = AST_Util.New_Block(EnumBlock.WHILE);

    ++loops_stack;

    var cond = AST_Util.New_Block(EnumBlock.SEQ);
    PushAST(cond);
    Visit(ctx.exp());
    PopAST();

    TypeSystem.CheckAssign(TypeSystem.Bool, Wrap(ctx.exp()));

    ast.AddChild(cond);

    PushAST(ast);
    CommonVisitBlock(EnumBlock.SEQ, ctx.block().statement(), new_local_scope: false);
    PopAST();
    ast.children[ast.children.Count-1].AddChild(AST_Util.New_Continue(jump_marker: true));

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

    var ast = AST_Util.New_Block(EnumBlock.WHILE);

    ++loops_stack;

    var cond = AST_Util.New_Block(EnumBlock.SEQ);
    PushAST(cond);
    Visit(for_cond);
    PopAST();

    TypeSystem.CheckAssign(TypeSystem.Bool, Wrap(for_cond.exp()));

    ast.AddChild(cond);

    PushAST(ast);
    var block = CommonVisitBlock(EnumBlock.SEQ, ctx.block().statement(), new_local_scope: false);
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

    var ast = AST_Util.New_Block(EnumBlock.WHILE);

    ++loops_stack;

    var cond = AST_Util.New_Block(EnumBlock.SEQ);
    PushAST(cond);
    Visit(ctx.exp());
    PopAST();

    TypeSystem.CheckAssign(TypeSystem.Bool, Wrap(ctx.exp()));

    ast.AddChild(cond);

    var body = AST_Util.New_Block(EnumBlock.SEQ);
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
    string iter_str_type = "";
    string iter_str_name = "";
    AST iter_ast_decl = null;
    VariableSymbol iter_symb = null;
    if(vod.NAME() != null)
    {
      iter_str_name = vod.NAME().GetText();
      iter_symb = curr_scope.Resolve(iter_str_name) as VariableSymbol;
      if(iter_symb == null)
        FireError(Location(vod.NAME()) +  " : symbol is not a valid variable");
      iter_str_type = iter_symb.type.name;
    }
    else
    {
      iter_str_name = vd.NAME().GetText();
      iter_str_type = vd.type().GetText();
      iter_ast_decl = CommonDeclVar(curr_scope, vd.NAME(), vd.type(), is_ref: false, func_arg: false, write: false);
      iter_symb = curr_scope.Resolve(iter_str_name) as VariableSymbol;
    }
    var arr_type = (ClassSymbol)mscope.Type(iter_str_type+"[]").Get(mscope);

    PushJsonType(arr_type);
    var exp = ctx.foreachExp().exp();
    //evaluating array expression
    Visit(exp);
    PopJsonType();
    TypeSystem.CheckAssign(Wrap(exp), arr_type);

    //generic fallback if the concrete type is not found 
    string arr_stype = GenericArrayTypeSymbol.CLASS_TYPE;
    if(!(arr_type is GenericArrayTypeSymbol))
      arr_stype = arr_type.GetName();

    var arr_tmp_name = "$foreach_tmp" + loops_stack;
    var arr_tmp_symb = curr_scope.Resolve(arr_tmp_name) as VariableSymbol;
    if(arr_tmp_symb == null)
    {
      arr_tmp_symb = new VariableSymbol(Wrap(exp), arr_tmp_name, mscope.Type(iter_str_type));
      curr_scope.Define(arr_tmp_symb);
    }

    var arr_cnt_name = "$foreach_cnt" + loops_stack;
    var arr_cnt_symb = curr_scope.Resolve(arr_cnt_name) as VariableSymbol;
    if(arr_cnt_symb == null)
    {
      arr_cnt_symb = new VariableSymbol(Wrap(exp), arr_cnt_name, mscope.Type("int"));
      curr_scope.Define(arr_cnt_symb);
    }

    PeekAST().AddChild(AST_Util.New_Call(EnumCall.VARW, ctx.Start.Line, arr_tmp_symb));
    //declaring counter var
    PeekAST().AddChild(AST_Util.New_VarDecl(arr_cnt_symb, is_ref: false));

    //declaring iterating var
    if(iter_ast_decl != null)
      PeekAST().AddChild(iter_ast_decl);

    var ast = AST_Util.New_Block(EnumBlock.WHILE);

    ++loops_stack;

    //adding while condition
    var cond = AST_Util.New_Block(EnumBlock.SEQ);
    var bin_op = AST_Util.New_BinaryOpExp(EnumBinaryOp.LT);
    bin_op.AddChild(AST_Util.New_Call(EnumCall.VAR, ctx.Start.Line, arr_cnt_symb));
    bin_op.AddChild(AST_Util.New_Call(EnumCall.VAR, ctx.Start.Line, arr_tmp_symb));
    bin_op.AddChild(AST_Util.New_Call(EnumCall.MVAR, ctx.Start.Line, "Count", arr_stype, arr_type.members.FindStringKeyIndex("Count")));
    cond.AddChild(bin_op);
    ast.AddChild(cond);

    PushAST(ast);
    var block = CommonVisitBlock(EnumBlock.SEQ, ctx.block().statement(), new_local_scope: false);
    //prepending filling of the iterator var
    block.children.Insert(0, AST_Util.New_Call(EnumCall.VARW, ctx.Start.Line, iter_symb));
    block.children.Insert(0, AST_Util.New_Call(EnumCall.MFUNC, ctx.Start.Line, "At", arr_stype));
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

  AST CommonVisitBlock(EnumBlock type, IParseTree[] sts, bool new_local_scope, bool auto_add = true)
  {
    ++scope_level;

    if(new_local_scope)
      curr_scope = new Scope(curr_scope); 

    bool is_paral = 
      type == EnumBlock.PARAL || 
      type == EnumBlock.PARAL_ALL;

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
          var seq = AST_Util.New_Block(EnumBlock.SEQ);
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
      PeekFuncDecl().return_statement_found = false;

    if(auto_add)
      PeekAST().AddChild(ast);
    return ast;
  }
}

public class ModulePath
{
  public string name;
  uint _id;
  public uint id {
    get {
      if(_id == 0)
        _id = Hash.CRC32(name);
      return _id;
    }
  }
  public string file_path;

  public ModulePath(string name, string file_path)
  {
    this.name = name;
    this.file_path = file_path;
  }
}

public class Module
{
  public uint id {
    get {
      return path.id;
    }
  }
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
  public Scope symbols = new Scope();

  public Module(ModulePath module_path)
  {
    this.path = module_path;
  }

  public Module(string name, string file_path)
    : this(new ModulePath(name, file_path))
  {}
}

public class ModuleRegistry
{
  List<string> include_path = new List<string>();
  Dictionary<string, Module> modules = new Dictionary<string, Module>(); 
  Dictionary<string, Parsed> parsed_cache = null;

  public void SetParsedCache(Dictionary<string, Parsed> cache)
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

  public Module ImportModule(Module curr_module, GlobalScope globals, string path)
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

    //2. checking global presence
    Module m = TryGet(full_path);
    if(m != null)
    {
      curr_module.imports.Add(full_path, m);
      return m;
    }

    //3. Ok, let's parse it otherwise
    m = new Module(norm_path, full_path);
   
    //Console.WriteLine("ADDING: " + full_path + " TO:" + curr_module.file_path);
    curr_module.imports.Add(full_path, m);
    Register(m);

    Parsed parsed;
    //4. Let's try the parsed cache if it's present
    if(parsed_cache != null && parsed_cache.TryGetValue(full_path, out parsed) && parsed != null)
    {
      //Console.WriteLine("HIT " + full_path);
      Frontend.Parsed2AST(m, parsed, globals, this, decls_only: true);
    }
    else
    {
      var stream = File.OpenRead(full_path);

      //Console.WriteLine("MISS " + full_path);
      Frontend.Source2AST(m, stream, globals, this, decls_only: true);

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

public interface IPostProcessor
{
  //returns path to the result file
  string Patch(LazyAST lazy_ast, string src_file, string result_file);
  void Tally();
}

public class EmptyPostProcessor : IPostProcessor 
{
  public string Patch(LazyAST lazy_ast, string src_file, string result_file) { return result_file; }
  public void Tally() {}
}

public interface IASTResolver
{
  AST_Module Get();
}

public class LazyAST
{
  IASTResolver resolver;
  AST_Module resolved;

  public LazyAST(IASTResolver resolver)
  {
    this.resolver = resolver;
  }

  public LazyAST(AST_Module resolved)
  {
    this.resolved = resolved;
  }

  public AST_Module Get()
  {
    if(resolved == null)
      resolved = resolver.Get();
    return resolved;
  }
}

} //namespace bhl
