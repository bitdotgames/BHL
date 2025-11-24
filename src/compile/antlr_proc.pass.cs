using System;
using System.Collections.Generic;
using Antlr4.Runtime;

namespace bhl;

public partial class ANTLR_Processor
{
  private class ParserPass
  {
    public IAST ast;
    public IScope scope;

    public Namespace ns;
    public string ns_full_path;

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

    public ParserPass(IScope scope, Namespace ns, string ns_full_path)
    {
      this.scope = scope;
      this.ns = ns;
      this.ns_full_path = ns_full_path;
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

  static public void ProcessAll(ProjectCompilationStateBundle proc_bundle)
  {
    foreach(var kv in proc_bundle.file2proc)
      WrapError(kv.Value, () => kv.Value.Phase_Outline());

    var name2module = proc_bundle.GroupModulesByName();

    SetupCachedModules(
      name2module,
      proc_bundle,
      Module.SetupFlags.Namespaces |
      Module.SetupFlags.Imports |
      Module.SetupFlags.Gvars
    );

    foreach(var kv in proc_bundle.file2proc)
      WrapError(kv.Value, () => kv.Value.Phase_LinkImports1(proc_bundle));

    foreach(var kv in proc_bundle.file2proc)
      WrapError(kv.Value, () => kv.Value.Phase_LinkImports2(proc_bundle));

    foreach(var kv in proc_bundle.file2proc)
      WrapError(kv.Value, () => kv.Value.Phase_ParseTypes1());

    foreach(var kv in proc_bundle.file2proc)
      WrapError(kv.Value, () => kv.Value.Phase_ParseTypes2());

    //NOTE: we may setup cached module classes only when processed classes
    //      are also setup
    SetupCachedModules(
      name2module,
      proc_bundle,
      Module.SetupFlags.Funcs |
      Module.SetupFlags.Classes
    );

    foreach(var kv in proc_bundle.file2proc)
      WrapError(kv.Value, () => kv.Value.Phase_ParseFuncBodies());

    foreach(var kv in proc_bundle.file2proc)
      WrapError(kv.Value, () => kv.Value.Phase_SetResult());
  }

  static void WrapError(ANTLR_Processor proc, Action action)
  {
    try
    {
      action();
    }
    catch(Exception e)
    {
      if(e is ICompileError ce)
        proc.errors.Add(ce);
      else
        //NOTE: let's turn other exceptions into BuildErrors
        proc.errors.Add(new BuildError(proc.module.file_path, e));
    }
  }

  static void SetupCachedModules(
    Dictionary<string, Module> name2module,
    ProjectCompilationStateBundle proc_bundle,
    Module.SetupFlags flags
  )
  {
    if(proc_bundle.file2cached == null)
      return;

    foreach(var kv in proc_bundle.file2cached)
      kv.Value.Setup(name => name2module[name], flags);
  }


  internal void Phase_Outline()
  {
    root_ast = new AST_Module(module.name);

    passes.Clear();

    PushAST(root_ast);
    VisitProgram((bhlParser.ProgramContext)parsed.parse_tree);
    PopAST();

    for(int p = 0; p < passes.Count; ++p)
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

  internal void Phase_LinkImports1(ProjectCompilationStateBundle proc_bundle)
  {
    var already_imported = new HashSet<Module>();

    //NOTE: getting a copy of keys since we might modify the dictionary during traversal
    var keys = new List<bhlParser.MimportContext>(raw_imports_parsed.Keys);
    foreach (var k in keys)
    {
      var import = raw_imports_parsed[k];

      if(!ResolveImportedModule(import, proc_bundle, out var imported_module))
        continue;

      //protection against self import
      if(imported_module.name == module.name)
      {
        raw_imports_parsed.Remove(k);
        continue;
      }

      //NOTE: let's remove duplicated imports
      if(already_imported.Contains(imported_module))
      {
        AddError(k, "already imported '" + import + "'");
        raw_imports_parsed.Remove(k);
        continue;
      }

      already_imported.Add(imported_module);

      try
      {
        //TODO: should this be in this phase?
        module.ns.Link(imported_module.ns);
      }
      catch (SymbolError se)
      {
        errors.Add(se);
        continue;
      }
    }
  }

  internal void Phase_LinkImports2(ProjectCompilationStateBundle proc_bundle)
  {
    if(raw_imports_parsed.Count == 0)
      return;

    var ast_import = new AST_Import();

    foreach(var kv in raw_imports_parsed)
    {
      if(!ResolveImportedModule(kv.Value, proc_bundle, out var imported_module))
      {
        AddError(kv.Key, "invalid import '" + kv.Value + "'");
        continue;
      }

      module.AddImportedGlobalVars(imported_module);

      imports.Add(imported_module);
      ast_import.module_names.Add(imported_module.name);
    }

    //let's force it to be the first one
    root_ast.children.Insert(0, ast_import);
  }

  internal void Phase_ParseTypes1()
  {
    for(int p = 0; p < passes.Count; ++p)
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
    for(int p = 0; p < passes.Count; ++p)
    {
      var pass = passes[p];

      PushScope(pass.scope);

      Pass_ParseFuncSignature_2(pass);

      Pass_AddInterfaceExtensions(pass);

      PopScope();
    }

    for(int p = 0; p < passes.Count; ++p)
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
    for(int p = 0; p < passes.Count; ++p)
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

  void AddPass(ParserRuleContext ctx, IScope scope, IAST ast)
  {
    passes.Add(new ParserPass(ast, scope, ctx));
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

    LSP_SetSymbol(pass.func_ctx.NAME(), pass.func_symb);
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

  void Pass_ParseFuncBlock(ParserPass pass)
  {
    if(pass.func_ctx == null || pass.func_ast?.symbol == null)
      return;

    PushScope(pass.func_ast.symbol);
    ParseFuncBlock(pass.func_ctx, pass.func_ctx.funcBlock(), pass.func_ctx.retType(), pass.func_ast);
    PopScope();
  }

  void ParseFuncBlock(ParserRuleContext ctx, bhlParser.FuncBlockContext block_ctx, bhlParser.RetTypeContext ret_ctx,
    AST_FuncDecl func_ast)
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

    pass.gvar_symb = new GlobalVariableSymbol(Annotate(vd.NAME()), vd.NAME().GetText(), new ProxyType());
    pass.gvar_symb.is_module_local = pass.gvar_decl_ctx.STATIC() != null;

    LSP_AddSemanticToken(pass.gvar_decl_ctx.STATIC(), SemanticToken.Keyword);
    LSP_SetSymbol(vd.NAME(), pass.gvar_symb);

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
    LSP_SetSymbol(pass.iface_ctx.NAME(), pass.iface_symb);

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

    for(int i = 0; i < pass.iface_ctx.interfaceBlock()?.interfaceMembers()?.interfaceMember().Length; ++i)
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
          AddError(fd.funcParams().funcParamDeclare()[sig.arg_types.Count - default_args_num],
            "default argument value is not allowed in this context");
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
      for(int i = 0; i < pass.iface_ctx.extensions().nsName().Length; ++i)
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

  Namespace FindNamespaceInPasses(string full_path)
  {
    foreach (var p in passes)
    {
      if(p.ns != null && p.ns_full_path == full_path)
        return p.ns;
    }

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

    LSP_SetSymbol(pass.class_ctx.NAME(), pass.class_symb);

    pass.class_ast = new AST_ClassDecl(pass.class_symb);

    //class members
    for(int i = 0; i < pass.class_ctx.classBlock()?.classMembers()?.classMember().Length; ++i)
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

        var fld_symb = new FieldSymbolScript(Annotate(vd), vd.NAME().GetText(), new ProxyType());

        for(int f = 0; f < fldd.fldAttribs().Length; ++f)
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

        for(int f = 0; f < fd.funcAttribs().Length; ++f)
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
    for(int i = 0; i < pass.class_ctx.classBlock()?.classMembers()?.classMember().Length; ++i)
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

      for(int i = 0; i < pass.class_ctx.extensions().nsName().Length; ++i)
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

          LSP_SetSymbol(ext_name.dotName().NAME(), cs);
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

          LSP_SetSymbol(ext_name.dotName().NAME(), ifs);
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
    for(int m = 0; m < pass.class_symb.members.Count; ++m)
    {
      if(pass.class_symb.members[m] is FieldSymbol fld && fld.attribs.HasFlag(FieldAttrib.Static))
        pass.class_ast.AddChild(new AST_VarDecl(fld, module.gvar_index.IndexOf(fld)));
    }
  }

  void Pass_ParseClassMethodsBlocks(ParserPass pass)
  {
    if(pass.class_symb == null || pass.class_ast == null)
      return;

    //class methods bodies
    for(int i = 0; i < pass.class_ctx.classBlock()?.classMembers()?.classMember().Length; ++i)
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
          throw new Exception("Method '" + func_symb.name + "' decl not found for class '" + pass.class_symb.name +
                              "'");

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
      LSP_SetSymbol(vd.type().nsName().dotName().NAME(), pass.gvar_symb.type.Get() as Symbol);

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

    AST_Tree ast = assign_exp != null
      ?
      new AST_Call(EnumCall.VARWDCL, vd.NAME().Symbol.Line, pass.gvar_symb, 0, vd.NAME())
      : new AST_VarDecl(pass.gvar_symb, vd.NAME().Symbol.Line);

    if(exp_ast != null)
      PeekAST().AddChild(exp_ast);
    PeekAST().AddChild(ast);

    if(assign_exp != null)
      types.CheckAssign(Annotate(vd.NAME()), Annotate(assign_exp), errors);

    PopAST();

    EnableVar(((Namespace)curr_scope).members, pass.gvar_symb, subst_symbol);
  }


}
