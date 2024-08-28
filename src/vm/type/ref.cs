using System;

namespace bhl {
  
public class RefType : IEphemeralType, IEquatable<RefType>
{
  public const uint CLASS_ID = 17;

  public ProxyType subj; 

  string name;
  public string GetName() { return name; }

  public RefType(ProxyType subj)
  {
    this.subj = subj;
    Update();
  }

  //marshall factory version
  public RefType()
  {}

  public uint ClassId()
  {
    return CLASS_ID;
  }

  void Update()
  {
    name = "ref " + subj;
  }

  public void IndexTypeRefs(TypeRefIndex refs)
  {
    refs.Index(subj);
  }
  
  public void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.SyncTypeRef(ctx, ref subj);
    if(ctx.is_read)
      Update();
  }

  public override bool Equals(object o)
  {
    if(!(o is RefType))
      return false;
    return this.Equals((RefType)o);
  }

  public bool Equals(RefType o)
  {
    if(ReferenceEquals(o, null))
      return false;
    if(ReferenceEquals(this, o))
      return true;
    return subj.Equals(o.subj);
  }

  public override int GetHashCode()
  {
    return name.GetHashCode();
  }
  
  public override string ToString()
  {
    return name;
  }
}

}
