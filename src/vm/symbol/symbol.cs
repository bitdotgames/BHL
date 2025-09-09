namespace bhl
{

public abstract class Symbol : INamed, ITypeRefIndexable, marshall.IMarshallableGeneric
{
  public string name;

  // All symbols know what scope contains them
  public IScope scope;

  // Information about origin where symbol is defined: file and line, parse tree
  public Origin origin;

  public Symbol(Origin origin, string name)
  {
    this.origin = origin;
    this.name = name;
  }

  public string GetName()
  {
    return name;
  }

  public override string ToString()
  {
    return name;
  }

  public abstract uint ClassId();

  public abstract void IndexTypeRefs(TypeRefIndex refs);
  public abstract void Sync(marshall.SyncContext ctx);
}

}