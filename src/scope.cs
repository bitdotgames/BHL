using System;
using System.Collections.Generic;

namespace bhl {

public interface IScope
{
  // Scope in which this scope defined. For global scope, it's null
  IScope GetOriginScope();
  // Where to look next for symbols: superclass or origin (parent) scope
  IScope GetFallbackScope();

  // Define a symbol in the current scope
  void Define(Symbol sym);
  // Look up name in this scope or in origin scope if not here
  Symbol Resolve(string name);

  // Readonly collection of members
  SymbolsDictionary GetMembers();
}

public abstract class Scope : IScope 
{
  // null if global (outermost) scope
  protected IScope origin;

  protected SymbolsDictionary members = new SymbolsDictionary();

  Dictionary<string, TypeRef> type_cache = new Dictionary<string, TypeRef>();

  public Scope(IScope origin) 
  { 
    this.origin = origin;  
  }

  public SymbolsDictionary GetMembers() { return members; }

  public Symbol Resolve(string name) 
  {
    Symbol s = null;
    members.TryGetValue(name, out s);
    if(s != null)
      return s;

    // if not here, check any origin scope
    if(origin != null) 
      return origin.Resolve(name);

    return null;
  }

  public virtual void Define(Symbol sym) 
  {
    if(members.Contains(sym.name))
      throw new UserError(sym.Location() + " : already defined symbol '" + sym.name + "'"); 

    members.Add(sym);

    sym.scope = this; // track the scope in each symbol
  }

  public void Append(Scope other)
  {
    var ms = other.GetMembers();
    for(int i=0;i<ms.Count;++i)
    {
      var s = ms[i];
      Define(s);
    }
  }

  public IScope GetOriginScope() { return origin; }
  public IScope GetFallbackScope() { return origin; }

  public override string ToString() { return string.Join(",", members.GetStringKeys().ToArray()); }

  static bool IsBuiltin(Symbol s)
  {
    return ((s is BuiltInTypeSymbol) || 
            (s is ArrayTypeSymbol)
           );
  }

  public static bool IsCompoundType(string name)
  {
    for(int i=0;i<name.Length;++i)
    {
      char c = name[i];
      if(!(Char.IsLetterOrDigit(c) || c == '_'))
        return true;
    }
    return false;
  }

#if BHL_FRONT
  static FuncType GetFuncType(Scope scope, bhlParser.TypeContext ctx)
  {
    var fnargs = ctx.fnargs();

    string ret_type_str = ctx.NAME().GetText();
    if(fnargs.ARR() != null)
      ret_type_str += "[]";
    
    var ret_type = scope.Type(ret_type_str);

    var arg_types = new List<TypeRef>();
    var fnames = fnargs.names();
    if(fnames != null)
    {
      for(int i=0;i<fnames.refName().Length;++i)
      {
        var name = fnames.refName()[i];
        var arg_type = scope.Type(name.NAME().GetText());
        arg_type.is_ref = name.isRef() != null; 
        arg_types.Add(arg_type);
      }
    }

    return new FuncType(ret_type, arg_types);
  }

  public TypeRef Type(bhlParser.TypeContext parsed)
  {
    var str = parsed.GetText();
    var type = Resolve(str) as IType;

    if(type == null && parsed != null)
    {    
      if(parsed.fnargs() != null)
        type = GetFuncType(this, parsed);

      //NOTE: if array type was not explicitely defined we fallback to GenericArrayTypeSymbol
      if(parsed.ARR() != null)
      {
        //checking if it's an array of func ptrs
        if(type != null)
          type = new GenericArrayTypeSymbol(this, new TypeRef(type));
        else
          type = new GenericArrayTypeSymbol(this, new TypeRef(parsed.NAME().GetText()));
      }
    }

    var tr = new TypeRef(type, str);
    tr.parsed = parsed;
    return tr;
  }

  public TypeRef Type(bhlParser.RetTypeContext parsed)
  {
    var str = parsed == null ? "void" : parsed.GetText();
    var type = Resolve(str) as IType;

    if(type == null && parsed != null)
    {    
      if(parsed.type().Length > 1)
      {
        var mtype = new MultiType();
        for(int i=0;i<parsed.type().Length;++i)
          mtype.items.Add(this.Type(parsed.type()[i]));
        mtype.Update();
        type = mtype;
      }
      else
        return this.Type(parsed.type()[0]);
    }

    var tr = new TypeRef(type, str);
    tr.parsed = parsed;
    return tr;
  }
#endif

  public TypeRef Type(string name)
  {
    if(name.Length == 0)
      throw new Exception("Bad type: '" + name + "'");

    TypeRef tr;
    if(type_cache.TryGetValue(name, out tr))
      return tr;
    
    //let's check if the type was already explicitely defined
    var t = Resolve(name) as IType;
    if(t != null)
    {
      tr = new TypeRef(t);
    }
    else
    {
#if BHL_FRONT
      if(IsCompoundType(name))
      {
        var node = Frontend.ParseType(name);
        if(node == null)
          throw new Exception("Bad type: '" + name + "'");

        if(node.type().Length == 1)
          tr = this.Type(node.type()[0]);
        else
          tr = this.Type(node);
      }
      else
#endif
        tr = new TypeRef(name);
    }

    type_cache.Add(name, tr);
    
    return tr;
  }

  public TypeRef Type(IType t)
  {
    return new TypeRef(t);
  }
}

public class GlobalScope : Scope 
{
  public GlobalScope() 
    : base(null) 
  {}
}

public class LocalScope : Scope 
{
  public LocalScope(IScope origin) 
    : base(origin) 
  {}

  public override void Define(Symbol sym) 
  {
    if(origin != null && origin.Resolve(sym.name) != null)
      throw new UserError(sym.Location() + " : already defined symbol '" + sym.name + "'"); 
    base.Define(sym);
  }
}

public class ModuleScope : Scope 
{
  uint module_id;

  public ModuleScope(uint module_id, GlobalScope origin) 
    : base(origin) 
  {
    this.module_id = module_id;
  }

  public override void Define(Symbol sym) 
  {
    if(origin != null && origin.Resolve(sym.name) != null)
      throw new UserError(sym.Location() + " : already defined symbol '" + sym.name + "'"); 

    if(sym is VariableSymbol vs)
    {
      //NOTE: adding module id if it's not added already
      if(vs.module_id == 0)
        vs.module_id = module_id;
      vs.CalcVariableScopeIdx(this);
    } 
    else if(sym is FuncSymbolScript fs)
    {
      //NOTE: adding module id if it's not added already
      if(fs.module_id == 0)
        fs.module_id = module_id;
    }
    base.Define(sym);
  }
}

} //namespace bhl
