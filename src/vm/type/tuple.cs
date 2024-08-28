
using System;
using System.Collections.Generic;

namespace bhl {
  
public class TupleType : IEphemeralType, IEquatable<TupleType>
{
  public const uint CLASS_ID = 16;

  string name;

  List<ProxyType> items = new List<ProxyType>();

  public string GetName() { return name; }

  public int Count {
    get {
      return items.Count;
    }
  }

  public TupleType(params ProxyType[] items)
  {
    foreach(var item in items)
      this.items.Add(item);
    Update();
  }

  //symbol factory version
  public TupleType()
  {}

  public ProxyType this[int index]
  {
    get { 
      return items[index]; 
    }
  }

  public void Add(ProxyType item)
  {
    items.Add(item);
    Update();
  }

  void Update()
  {
    string tmp = "";
    for(int i=0;i<items.Count;++i)
    {
      if(i > 0)
        tmp += ",";
      tmp += items[i];
    }

    name = tmp;
  }

  public uint ClassId()
  {
    return CLASS_ID;
  }

  public void IndexTypeRefs(TypeRefIndex refs)
  {
    refs.Index(items);
  }
  
  public void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.SyncTypeRefs(ctx, items);
    if(ctx.is_read)
      Update();
  }

  public override bool Equals(object o)
  {
    if(!(o is TupleType))
      return false;
    return this.Equals((TupleType)o);
  }

  public bool Equals(TupleType o)
  {
    if(ReferenceEquals(o, null))
      return false;
    if(ReferenceEquals(this, o))
      return true;
    if(items.Count != o.items.Count)
      return false;
    for(int i=0;i<items.Count;++i)
      if(!items[i].Equals(o.items[i]))
        return false;
    return true;
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
