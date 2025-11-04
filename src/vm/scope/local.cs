using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl
{

public class LocalScope : IScope, IEnumerable<Symbol>
{
  bool is_paral;
  int next_idx;

  FuncSymbolScript func_owner;

  IScope fallback;

  internal SymbolsStorage members;

  public LocalScope(bool is_paral, IScope fallback)
  {
    this.is_paral = is_paral;

    this.fallback = fallback;
    func_owner = fallback.FindEnclosingFuncSymbol();
    if (func_owner == null)
      throw new Exception("No top func symbol found");

    members = new SymbolsStorage(this);
  }

  public void Enter()
  {
    if (fallback is FuncSymbolScript fss)
      //start with func arguments number
      next_idx = fss._local_vars_num;
    else if (fallback is LocalScope fallback_ls)
      next_idx = fallback_ls.next_idx;
    func_owner._current_scope = this;
  }

  public void Exit()
  {
    if (fallback is LocalScope fallback_ls)
    {
      if (fallback_ls.is_paral)
        fallback_ls.next_idx = next_idx;
      else //TODO: just for now, trying to spot a possible bug
        fallback_ls.next_idx = next_idx;

      func_owner._current_scope = fallback_ls;
    }
  }

  public IEnumerator<Symbol> GetEnumerator()
  {
    return members.GetEnumerator();
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public Symbol Resolve(string name)
  {
    return members.Find(name);
  }

  public void Define(Symbol sym)
  {
    EnsureNotDefinedInEnclosingScopes(sym);

    DefineWithoutEnclosingChecks(sym);
  }

  void EnsureNotDefinedInEnclosingScopes(Symbol sym)
  {
    IScope tmp = this;
    while (true)
    {
      if (tmp.Resolve(sym.name) != null)
        throw new SymbolError(sym, "already defined symbol '" + sym.name + "'");
      //going 'up' until the owning function
      if (tmp == func_owner)
        break;
      tmp = tmp.GetFallbackScope();
    }
  }

  public void DefineWithoutEnclosingChecks(Symbol sym)
  {
    //NOTE: overriding SymbolsStorage scope_idx assing logic
    if (sym is IScopeIndexed si && si.scope_idx == -1)
      si.scope_idx = next_idx;

    if (next_idx >= func_owner._local_vars_num)
      func_owner._local_vars_num = next_idx + 1;

    ++next_idx;

    members.Add(sym);
  }

  public IScope GetFallbackScope()
  {
    return fallback;
  }
}

}