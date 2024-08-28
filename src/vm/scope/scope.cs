
namespace bhl {

public interface IScope
{
  // Look up name in this scope without fallback!
  Symbol Resolve(string name);

  // Define a symbol in the current scope, throws an exception 
  // if symbol with such a name already exists
  void Define(Symbol sym);

  // Where to look next for symbols in case if not found (e.g super class) 
  IScope GetFallbackScope();
}

// Denotes any named entity which can be located by this name
public interface INamed
{
  string GetName();
}

public interface INamedResolver
{
  INamed ResolveNamedByPath(string path);
}

}
