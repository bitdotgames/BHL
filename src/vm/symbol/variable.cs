namespace bhl
{

public class VariableSymbol : Symbol, ITyped, IScopeIndexed
{
  public const uint CLASS_ID = 8;

  public ProxyType type;

  int _scope_idx = -1;

  public int scope_idx
  {
    get { return _scope_idx; }
    set { _scope_idx = value; }
  }

#if BHL_FRONT
  //referenced upvalue, keep in mind it can be a 'local' variable from the   
  //outer lambda wrapping the current one
  internal VariableSymbol _upvalue;
#endif

  public VariableSymbol(Origin origin, string name, ProxyType type)
    : base(origin, name)
  {
    this.type = type;
  }

  //marshall factory version
  public VariableSymbol()
    : base(null, "")
  {
  }

#if BHL_FRONT
  public IType GuessType()
  {
    return origin?.parsed == null ? type.Get() : origin.parsed.eval_type;
  }
#endif

  public IType GetIType()
  {
    return type.Get();
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {
    refs.Index(type);
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.SyncTypeRef(ctx, ref type);
    marshall.Marshall.Sync(ctx, ref _scope_idx);
  }

  public override string ToString()
  {
    return type + " " + name;
  }
}

public class GlobalVariableSymbol : VariableSymbol
{
  new public const uint CLASS_ID = 22;

  public bool is_local;

  public GlobalVariableSymbol(Origin origin, string name, ProxyType type)
    : base(origin, name, type)
  {
  }

  //marshall factory version
  public GlobalVariableSymbol()
    : base(null, "", new ProxyType())
  {
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    base.Sync(ctx);

    marshall.Marshall.Sync(ctx, ref is_local);
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }
}

}