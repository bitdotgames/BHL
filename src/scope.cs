using System;
using System.Collections.Specialized;
using System.Collections.Generic;

namespace bhl {

public interface Scope
{
  string GetScopeName();

  // Where to look next for symbols; superclass or enclosing scope
  Scope GetParentScope();
  // Scope in which this scope defined. For global scope, it's null
  Scope GetEnclosingScope();

  // Define a symbol in the current scope
  void define(Symbol sym);
  // Look up name in this scope or in parent scope if not here
  Symbol resolve(string name);
}

public abstract class BaseScope : Scope 
{
  protected Scope enclosing_scope; // null if global (outermost) scope
  protected OrderedDictionary symbols = new OrderedDictionary();

  public BaseScope(Scope parent) 
  { 
    enclosing_scope = parent;  
  }

  public OrderedDictionary GetMembers()
  {
    return symbols;
  }

  public Symbol resolve(string name) 
  {
    Symbol s = (Symbol)symbols[name];
    if(s != null)
      return s;

    // if not here, check any enclosing scope
    if(GetParentScope() != null) 
      return GetParentScope().resolve(name);

    return null;
  }

  public virtual void define(Symbol sym) 
  {
    if(symbols.Contains(sym.name))
      throw new Exception(sym.Location() + ": Already defined symbol '" + sym.name + "'"); 

    symbols.Add(sym.name, sym);

    sym.scope = this; // track the scope in each symbol
  }

  public void Append(BaseScope other)
  {
    foreach(var val in other.GetMembers().Values)
    {
      var s = (Symbol)val;
      define(s);
    }
  }

  public Scope GetParentScope() { return GetEnclosingScope(); }
  public Scope GetEnclosingScope() { return enclosing_scope; }

  public abstract string GetScopeName();

  public override string ToString() { return string.Join(",", symbols.GetStringKeys().ToArray()); }
}

public class GlobalScope : BaseScope 
{
  Dictionary<ulong, Symbol> hashed_symbols = new Dictionary<ulong, Symbol>();

  public GlobalScope() 
    : base(null) 
  {}

  public override string GetScopeName() { return "global"; }

  public override void define(Symbol sym) 
  {
    base.define(sym);

    hashed_symbols.Add(sym.nname, sym);
  }

  static bool IsBuiltin(Symbol s)
  {
    return ((s is BuiltInTypeSymbol) || 
            (s is ArrayTypeSymbol)
           );
  }

  public T FindBinding<T>(ulong nname) where T : Symbol
  {
    Symbol sym;
    if(hashed_symbols.TryGetValue(nname, out sym))
      return sym as T;
    return null;
  }

  public TypeRef type(string name)
  {
    return new TypeRef(this, name);
  }
}

public class LocalScope : BaseScope 
{
  public LocalScope(Scope parent) 
    : base(parent) {}
  public override string GetScopeName() { return "local"; }
}

} //namespace bhl
