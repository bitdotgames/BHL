using System.Collections.Generic;

namespace bhl
{

public interface IModuleIndexed
{
  //index in a module
  int module_idx { get; set; }
}

public interface IScopeIndexed
{
  //index in an enclosing scope
  int scope_idx { get; set; }
}

public class ModuleIndexer<T> where T : Symbol, IModuleIndexed
{
  internal List<T> index = new List<T>();

  public int Count
  {
    get { return index.Count; }
  }

  public void Index(T sym)
  {
    if(index.IndexOf(sym) != -1)
      return;
    sym.module_idx = index.Count;
    index.Add(sym);
  }

  public T this[int i]
  {
    get { return index[i]; }
  }

  public int IndexOf(T s)
  {
    return index.IndexOf(s);
  }

  public int IndexOf(string name)
  {
    for(int i = 0; i < index.Count; ++i)
      if(index[i].name == name)
        return i;
    return -1;
  }
}

public class ScopeIndexer<T> where T : Symbol, IScopeIndexed
{
  internal List<T> index = new List<T>();

  public int Count
  {
    get { return index.Count; }
  }

  public void Index(T sym)
  {
    if(index.IndexOf(sym) != -1)
      return;
    //TODO: do we really need this check?
    //if(sym.scope_idx == -1)
    sym.scope_idx = index.Count;
    index.Add(sym);
  }

  public T this[int i]
  {
    get { return index[i]; }
  }

  public int IndexOf(INamed s)
  {
    return index.IndexOf((T)s);
  }

  public int IndexOf(string name)
  {
    for(int i = 0; i < index.Count; ++i)
      if(index[i].GetName() == name)
        return i;
    return -1;
  }
}

public class VarScopeIndexer : ScopeIndexer<VariableSymbol>
{
}

public class FuncNativeModuleIndexer : ModuleIndexer<FuncSymbolNative>
{
}

public class FuncModuleIndexer : ModuleIndexer<FuncSymbolScript>
{
}

public class NativeFuncScopeIndexer : ScopeIndexer<FuncSymbolNative>
{
  public NativeFuncScopeIndexer()
  {
  }

  public NativeFuncScopeIndexer(NativeFuncScopeIndexer other)
  {
    index.AddRange(other.index);
  }
}

}