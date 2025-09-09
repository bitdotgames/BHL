using System;
using System.Runtime.CompilerServices;
using bhl.marshall;

namespace bhl
{

// For lazy evaluation of types and forward declarations
public struct ProxyType : IMarshallable, IEquatable<ProxyType>
{
  public IType resolved;

  public INamedResolver resolver;

  //NOTE: for symbols it's a full absolute path from the very top namespace
  public NamePath path;

  public ProxyType(INamedResolver resolver, string path)
  {
    if(path.Length == 0)
      throw new Exception("Type path is empty");
    if(Types.IsCompoundType(path))
      throw new Exception("Type spec contains illegal characters: '" + path + "'");

    this.resolver = resolver;
    this.path = path;

    resolved = null;
  }

  public ProxyType(IType obj)
  {
    resolver = null;
    path = default;

    resolved = null;

    SetResolved(obj);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public bool IsEmpty()
  {
    return path.Count == 0 && resolved == null;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public bool IsNull()
  {
    return resolved == null && resolver == null && path.Count == 0;
  }

  public void Clear()
  {
    path = default;
    resolved = null;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public IType Get()
  {
    if(resolved != null)
      return resolved;

    if(path.Count > 0)
      SetResolved(resolver.ResolveNamedByPath(path) as IType);

    return resolved;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void SetResolved(IType resolved)
  {
    this.resolved = resolved;
    //TODO: some smelly code below - after resolving we re-write the original path
    //      with the normalized one, this is useful for cases when comparing proxies
    //      pointing to types withing namespaces, e.g: func(fns.Item) vs func(Item)
    if(resolved != null)
      path = GetNormalizedTypePath(resolved);
  }

  static NamePath GetNormalizedTypePath(IType obj)
  {
    //for symbols full path is used
    return (obj is Symbol sym) ? sym.GetFullTypePath() : obj.GetName();
  }

  public void Sync(marshall.SyncContext ctx)
  {
    if(ctx.is_read)
      resolver = ((SymbolFactory)ctx.factory).resolver;

    bool is_ephemeral = false;
    if(!ctx.is_read)
      is_ephemeral = Get() is IEphemeralType;

    marshall.Marshall.Sync(ctx, ref is_ephemeral);

    if(is_ephemeral)
    {
      var eph = ctx.is_read ? null : Get() as IMarshallableGeneric;
      marshall.Marshall.SyncGeneric(ctx, ref eph);
      if(ctx.is_read)
        SetResolved((IType)eph);
    }
    else
      marshall.Marshall.Sync(ctx, ref path);
  }

  public override string ToString()
  {
    if(path.Count == 0)
      Get();
    return path.ToString();
  }

  public override bool Equals(object o)
  {
    if(!(o is ProxyType))
      return false;
    return this.Equals((ProxyType)o);
  }

  public bool Equals(ProxyType o)
  {
    if(resolved != null && o.resolved != null)
      return o.resolved.Equals(resolved);
    else if(resolver != null && o.resolver == resolver && o.path.Equals(path))
      return true;

    //OK nothing worked, let's resolve the type
    Get();

    if(resolved != null)
      return resolved.Equals(o.Get());
    else //null check
      return null == o.Get();
  }

  public override int GetHashCode()
  {
    if(resolved != null)
      return resolved.GetHashCode();

    return path.GetHashCode();
  }
}

}