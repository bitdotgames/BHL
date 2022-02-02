using System;
using System.Collections.Generic;

namespace bhl {

public interface IScope
{
  // Scope in which this scope defined. For global scope, it's null
  IScope GetOriginScope();
  // Where to look next for symbols in case if not found (e.g super class) 
  IScope GetFallbackScope();

  // Define a symbol in the current scope
  void Define(Symbol sym);
  // Look up name in this scope or in fallback scope if not here
  Symbol Resolve(string name);

  // Readonly collection of members
  SymbolsDictionary GetMembers();
}

public abstract class BaseScope : IScope 
{
  protected IScope fallback;

  protected SymbolsDictionary members = new SymbolsDictionary();

  Dictionary<string, TypeRef> type_cache = new Dictionary<string, TypeRef>();

  public BaseScope(IScope fallback = null) 
  { 
    this.fallback = fallback;  
  }

  public SymbolsDictionary GetMembers() { return members; }

  public Symbol Resolve(string name) 
  {
    Symbol s = null;
    members.TryGetValue(name, out s);
    if(s != null)
      return s;

    // if not here, check any fallback scope
    if(fallback != null) 
      return fallback.Resolve(name);

    return null;
  }

  public virtual void Define(Symbol sym) 
  {
    if(members.Contains(sym.name))
      throw new UserError(sym.Location() + " : already defined symbol '" + sym.name + "'"); 

    members.Add(sym);

    sym.scope = this; // track the scope in each symbol
  }

  public void Append(BaseScope other)
  {
    var ms = other.GetMembers();
    for(int i=0;i<ms.Count;++i)
    {
      var s = ms[i];
      Define(s);
    }
  }

  public IScope GetOriginScope() { return null; }
  public IScope GetFallbackScope() { return fallback; }

  public override string ToString() { return string.Join(",", members.GetStringKeys().ToArray()); }

#if BHL_FRONT
  public TypeRef Type(bhlParser.TypeContext parsed)
  {
    var str = parsed.GetText();
    var type = Resolve(str) as IType;

    if(type == null && parsed != null)
    {    
      if(parsed.fnargs() != null)
        type = TypeSystem.GetFuncType(this, parsed);

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
      if(TypeSystem.IsCompoundType(name))
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

public class Scope : BaseScope 
{
  public Scope(IScope fallback = null) 
    : base(fallback) 
  {}

  public override void Define(Symbol sym) 
  {
    if(fallback != null && fallback.Resolve(sym.name) != null)
      throw new UserError(sym.Location() + " : already defined symbol '" + sym.name + "'"); 
    base.Define(sym);
  }
}

public class GlobalScope : BaseScope 
{
  public GlobalScope() 
    : base(null) 
  {}
}

public class ModuleScope : Scope
{
  uint module_id;
  public GlobalScope globs;

  public ModuleScope(uint module_id, GlobalScope fallback) 
    : base(fallback) 
  {
    this.globs = fallback;
    this.module_id = module_id;
  }

  public override void Define(Symbol sym) 
  {
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
      if(fs.decl.module_id == 0)
        fs.decl.module_id = module_id;
    }
    base.Define(sym);
  }
}

} //namespace bhl
