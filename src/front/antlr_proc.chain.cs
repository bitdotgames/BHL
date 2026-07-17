#if (BHL_FRONT || BHL_PARSER || UNITY_EDITOR)

using Antlr4.Runtime.Tree;


namespace bhl
{

public partial class ANTLR_Processor
{
  // Encapsulates the mutable cursor state for walking a single chain expression.
  // Created on the stack per chain evaluation; never stored or boxed.
  ref struct ExpChainWalker
  {
    readonly ANTLR_Processor _proc;
    readonly ExpChain _chain;
    readonly bool _write;
    readonly bool _yielded;

    ITerminalNode _curr_name;
    IScope        _scope;
    IType         _curr_type;
    Symbol        _curr_symb;
    int           _offset;

    internal IType CurrType => _curr_type;

    internal ExpChainWalker(ANTLR_Processor proc, ExpChain chain, IType currType, bool write, bool yielded, IScope rootScope)
    {
      _proc     = proc;
      _chain    = chain;
      _write    = write;
      _yielded  = yielded;
      _curr_type = currType;
      _curr_name = null;
      _curr_symb = null;
      _offset   = 0;
      _scope    = rootScope ?? (chain.IsGlobalNs ? proc.ns : proc.curr_scope);
    }

    // Minimal constructor for processing only chain items (lambda-tail case).
    internal ExpChainWalker(ANTLR_Processor proc, IType currType, bool write)
    {
      _proc     = proc;
      _chain    = default;
      _write    = write;
      _yielded  = false;
      _curr_type = currType;
      _curr_name = null;
      _curr_symb = null;
      _offset   = 0;
      _scope    = proc.curr_scope;
    }

    internal bool Walk(ref IType type)
    {
      if(_chain.paren_exp_ctx != null)
      {
        if(_proc.TryVisit(_chain.paren_exp_ctx))
          _curr_type = _proc.Annotate(_chain.paren_exp_ctx).eval_type;
      }

      _curr_name = _chain.name_ctx?.NAME();

      _proc.PushAST(new AST_Interim());

      if(_curr_name != null && !WalkHead())
      {
        _proc.PopAST();
        return false;
      }

      if(!WalkItems(_chain.items))
      {
        _proc.PopAST();
        return false;
      }

      if(_curr_name != null)
        WalkItem(null, null, _curr_name.Symbol.Line, _write,
          is_leftover: true, is_root: _chain.items.Count == 0);

      if(_chain.IsMemorySlotAccess && _curr_symb is FuncSymbol m && _scope is ClassSymbol)
      {
        if(!_write)
        {
          if(!m.attribs.HasFlag(FuncAttrib.Static))
          {
            _proc.AddError(_chain.items.At(_chain.items.Count - 1), "method pointers not supported");
            _proc.PopAST();
            return false;
          }
        }
        else
        {
          _proc.AddError(_chain.items.At(_chain.items.Count - 1), "invalid assignment");
          _proc.PopAST();
          return false;
        }
      }

      var chain_ast = _proc.PeekAST();
      _proc.PopAST();

      _proc.ValidateChainCall(_chain.ctx, 0, chain_ast.children, _yielded);
      _proc.PeekAST().AddChildren(chain_ast);

      type = _curr_type;
      return true;
    }

    bool WalkHead()
    {
      var nameText = _curr_name.GetText();

      if(nameText == "this" || nameText == "base")
        _proc.LSP_AddSemanticToken(_curr_name, SemanticToken.Keyword);

      var name_symb = _scope.ResolveWithFallback(nameText);

      ApplyBaseCall(ref name_symb);

      if(name_symb == null)
      {
        _proc.AddError(_curr_name, "symbol '" + _curr_name.GetText() + "' not resolved");
        return false;
      }

      ApplyNamespaceOffset(ref name_symb);

      if(name_symb is IType type_symb)
        _curr_type = type_symb;
      else if(name_symb is ITyped typed)
        _curr_type = typed.GetIType();
      else
        _curr_type = null;

      if(_curr_type == null)
      {
        _proc.AddError(_curr_name, $"'{_curr_name.GetText()}' cannot be used as an expression");
        return false;
      }

      return true;
    }

    void ApplyBaseCall(ref Symbol name_symb)
    {
      if(_curr_name.GetText() != "base" || !(_proc.PeekFuncDecl()?.scope is ClassSymbol cs))
        return;

      if(cs.super_class == null)
      {
        _proc.AddError(_curr_name, "no base class found");
        return;
      }

      name_symb = cs.super_class;
      _scope    = cs.super_class;

      if(_chain.items.Count <= _offset)
      {
        _proc.AddError(_curr_name, "bad base call");
        return;
      }

      var macc = _chain.items.At(_offset) as bhlParser.MemberAccessContext;
      if(macc == null)
      {
        _proc.AddError(_chain.items.At(_offset), "bad base call");
        return;
      }

      _curr_name = macc.NAME();
      ++_offset;

      var func_decl = _proc.PeekFuncDecl();
      _proc.PeekAST().AddChild(new AST_Call(EnumCall.VAR, _curr_name.Symbol.Line, func_decl.Resolve("this")));
      _proc.PeekAST().AddChild(new AST_TypeCast(cs.super_class, force_type: true, line_num: _curr_name.Symbol.Line));
    }

    void ApplyNamespaceOffset(ref Symbol name_symb)
    {
      if(!(name_symb is Namespace ns) || _chain.items.Count == 0)
        return;

      // Root name is itself a namespace (e.g. 'Unit' in 'Unit.Foo') — highlight it as such
      // so it reads distinctly from a class name or a variable, and annotate it so hover/
      // go-to-definition/find-refs work on it (WalkItem, which normally does this, is
      // bypassed for namespace segments — they're resolved inline in this loop instead).
      _proc.LSP_AddSemanticToken(_curr_name, SemanticToken.Namespace);
      _proc.LSP_SetSymbol(_curr_name, ns);

      _scope = ns;
      for(_offset = 0; _offset < _chain.items.Count; )
      {
        var macc = _chain.items.At(_offset) as bhlParser.MemberAccessContext;
        if(macc == null || macc.NAME() == null)
        {
          _proc.AddError(_chain.items.At(_offset), "bad chain call");
          return;
        }

        name_symb = _scope.ResolveWithFallback(macc.NAME().GetText());
        if(name_symb == null)
        {
          _proc.AddError(macc.NAME(), "symbol '" + macc.NAME().GetText() + "' not resolved");
          return;
        }

        _curr_name = macc.NAME();
        ++_offset;

        if(name_symb is Namespace name_ns)
        {
          // Intermediate segment is also a namespace (e.g. the middle part of 'a.b.Foo')
          _proc.LSP_AddSemanticToken(_curr_name, SemanticToken.Namespace);
          _proc.LSP_SetSymbol(_curr_name, name_ns);
          _scope = name_ns;
        }
        else
          break;
      }
    }

    internal bool WalkItems(ExpChainItems items)
    {
      for(int c = _offset; c < items.Count; ++c)
      {
        var item    = items.At(c);
        bool is_last = c == items.Count - 1;

        if(item is bhlParser.CallArgsContext cargs)
        {
          WalkItem(cargs, null, cargs.Start.Line, write: false, is_root: _offset == 0);
          _curr_name = null;
        }
        else if(item is bhlParser.ArrAccessContext arracc)
        {
          WalkItem(null, arracc, arracc.Start.Line, write: _write && is_last, is_root: c == _offset);
          _curr_name = null;
        }
        else if(item is bhlParser.MemberAccessContext macc)
        {
          ClassSymbol class_symb = null;
          if(_curr_name != null)
          {
            WalkItem(null, null, macc.Start.Line, write: false, is_root: c == _offset);
            class_symb = _curr_symb as ClassSymbol;
          }
          else
            _curr_symb = null;

          _scope = _curr_type as IScope;
          if(!(_scope is IInstantiable) && !(_scope is EnumSymbol))
          {
            _proc.AddError(macc, $"type '{_curr_type?.GetName() ?? "?"}' does not support member access");
            return false;
          }

          if(macc.NAME() == null)
          {
            _proc.AddError(macc, "incomplete parsing context");
            return false;
          }

          if(!CheckStaticInstanceAccess(macc, class_symb))
            return false;

          _curr_name = macc.NAME();
        }
        else
          throw new System.Exception("Unhandled chain item");
      }

      return true;
    }

    // Consolidated from four separate if-blocks: error if static-access-mode != member-is-static.
    bool CheckStaticInstanceAccess(bhlParser.MemberAccessContext macc, ClassSymbol class_symb)
    {
      bool is_via_class = class_symb != null;
      var member = _scope.ResolveWithFallback(macc.NAME().GetText());

      if(member is FuncSymbol fs)
      {
        bool is_static = fs.attribs.HasFlag(FuncAttrib.Static);
        if(!is_via_class && is_static)
        {
          _proc.AddError(macc, "calling static method on instance is forbidden");
          return false;
        }
        if(is_via_class && !is_static)
        {
          _proc.AddError(macc, "calling instance method as static is forbidden");
          return false;
        }
      }
      else if(member is FieldSymbol fld)
      {
        bool is_static = fld.attribs.HasFlag(FieldAttrib.Static);
        if(!is_via_class && is_static)
        {
          _proc.AddError(macc, "accessing static field on instance is forbidden");
          return false;
        }
        if(is_via_class && !is_static)
        {
          _proc.AddError(macc, "accessing instance attribute as static is forbidden");
          return false;
        }
      }

      return true;
    }

    void WalkItem(
      bhlParser.CallArgsContext cargs,
      bhlParser.ArrAccessContext arracc,
      int line,
      bool write,
      bool is_leftover = false,
      bool is_root = false
    )
    {
      AST_Call ast = null;

      if(_curr_name != null)
      {
        var nameText  = _curr_name.GetText();
        var name_symb = is_root
          ? _scope.ResolveWithFallback(nameText)
          : _scope.ResolveRelatedOnly(nameText);

        if(name_symb == null)
        {
          _proc.AddError(_curr_name, "symbol '" + nameText + "' not resolved");
          _curr_symb = null;
          return;
        }

        _proc.LSP_SetSymbol(_curr_name, name_symb);

        var prev_symb = _curr_symb;
        _curr_symb     = name_symb;

        var var_symb  = name_symb as VariableSymbol;
        var func_symb = name_symb as FuncSymbol;

        if(cargs != null)
        {
          if(var_symb is FieldSymbol && !(var_symb.type.Get() is FuncSignature))
          {
            _proc.AddError(_curr_name, $"'{nameText}' is not a function");
            return;
          }

          var ftype = var_symb?.type.Get() as FuncSignature;
          if(ftype != null)
          {
            if(!(_scope is IInstantiable))
            {
              ast = new AST_Call(EnumCall.FUNC_PTR_VAR, line, var_symb, 0, _curr_name);
              _proc.AddCallArgs(ftype, cargs, ref ast);
            }
            else
            {
              _proc.PeekAST().AddChild(new AST_Call(EnumCall.VAR, line, var_symb, 0, _curr_name));
              ast = new AST_Call(EnumCall.FUNC_PTR_MVAR, line, var_symb, 0, _curr_name);
              _proc.AddCallArgs(ftype, cargs, ref ast);
            }
            _curr_type = ftype.return_type.Get();
            if(_curr_type == null)
              _proc.AddError(_curr_name, "type '" + ftype.return_type + "' not found");
          }
          else if(func_symb != null)
          {
            var call_type = _scope is IInstantiable && !func_symb.attribs.HasFlag(FuncAttrib.Static)
              ? EnumCall.MFUNC : EnumCall.FUNC;
            ast = new AST_Call(call_type, line, func_symb, 0, _curr_name);
            //NOTE: attach receiver variable for proper MFUNC dispatch
            if(call_type == EnumCall.MFUNC && prev_symb is VariableSymbol var_symbol)
              ast.ctx_var = var_symbol;
            _proc.LSP_AddSemanticToken(_curr_name,
              func_symb is FuncSymbolNative ? SemanticToken.Parameter : SemanticToken.Function);
            _proc.AddCallArgs(func_symb, cargs, ref ast);
            _curr_type = func_symb.GetReturnType();
          }
          else
          {
            _proc.AddError(_curr_name, $"'{nameText}' is not a function");
            return;
          }
        }
        else
        {
          if(var_symb != null)
          {
            bool is_write = write && arracc == null;
            var fld_symb  = var_symb as FieldSymbol;

            if(_scope is InterfaceSymbol)
            {
              _proc.AddError(_curr_name, "attributes not supported by interfaces");
              return;
            }

            bool pass_as_ref = _proc.PeekCallByRef();
            if(!var_symb._is_ref && pass_as_ref)
              var_symb._is_ref_decl = true;

            ast = new AST_Call(is_write ? EnumCall.VARW : EnumCall.VAR,
              line, var_symb, 0, _curr_name, pass_as_ref);

            if(fld_symb != null && _scope is ClassSymbolNative)
            {
              if(ast.type == EnumCall.VAR && fld_symb.getter == null)
              {
                _proc.AddError(_curr_name, "get operation is not defined");
                return;
              }
              else if(ast.type == EnumCall.VARW && fld_symb.setter == null)
              {
                _proc.AddError(_curr_name, "set operation is not defined");
                return;
              }
            }

            _curr_type = var_symb.type.Get();
            if(_curr_type == null)
              _proc.AddError(_curr_name, "type '" + var_symb.type + "' not found");
          }
          else if(func_symb != null)
          {
            ast       = new AST_Call(EnumCall.GET_ADDR, line, func_symb, 0, _curr_name);
            _curr_type = func_symb.signature;
          }
          else if(name_symb is EnumSymbol enum_symb)
          {
            // Enum type name used as a qualifier (e.g. 'Color' in 'Color.Red') —
            // highlight it distinctly from a namespace, a class, or a regular variable.
            _proc.LSP_AddSemanticToken(_curr_name, SemanticToken.Enum);
            if(is_leftover)
            {
              _proc.AddError(_curr_name, $"'{nameText}' is an enum type and cannot be used as a value");
              return;
            }
            _curr_type = enum_symb;
          }
          else if(name_symb is EnumItemSymbol enum_item)
          {
            var ast_literal = new AST_Literal(ConstType.INT);
            ast_literal.nval = enum_item.val;
            _proc.PeekAST().AddChild(ast_literal);
          }
          else if(name_symb is ClassSymbol class_symb)
          {
            // Class name used as a value/qualifier (e.g. 'MyClass' in 'MyClass.StaticFoo()') —
            // highlight it distinctly from a namespace or a regular variable.
            _proc.LSP_AddSemanticToken(_curr_name, SemanticToken.Class);
            if(is_leftover)
              _proc.AddError(_curr_name, $"'{nameText}' is a class type and cannot be used as a value");
            _curr_type = class_symb;
          }
          else
          {
            _proc.AddError(_curr_name, $"'{nameText}' cannot be used in an expression");
            return;
          }
        }
      }
      else if(cargs != null)
      {
        var ftype = _curr_type as FuncSignature;
        if(ftype == null)
        {
          _proc.AddError(cargs, $"cannot call expression of type '{_curr_type?.GetName() ?? "?"}'");
          return;
        }
        ast       = new AST_Call(EnumCall.FUNC_PTR_RES, line, null);
        _proc.AddCallArgs(ftype, cargs, ref ast);
        _curr_type = ftype.return_type.Get();
        if(_curr_type == null)
          _proc.AddError(_curr_name, "type '" + ftype.return_type + "' not found");
      }

      if(ast != null)
        _proc.PeekAST().AddChild(ast);

      if(arracc != null)
        _proc.AddArrIndex(arracc, ref _curr_type, line, write);
    }
  }
}
}

#endif
