using System.Collections.Generic;

namespace bhl {
    
[System.Flags]
public enum FieldAttrib : byte
{
  None        = 0,
  Static      = 1,
}

public class FieldSymbol : VariableSymbol
{
  public delegate void FieldGetter(VM.Frame frm, Val v, ref Val res, FieldSymbol fld);
  public delegate void FieldSetter(VM.Frame frm, ref Val v, Val nv, FieldSymbol fld);
  public delegate void FieldRef(VM.Frame frm, Val v, out Val res, FieldSymbol fld);

  public FieldGetter getter;
  public FieldSetter setter;
  public FieldRef getref;

  protected byte _attribs = 0;
  public FieldAttrib attribs {
    get {
      return (FieldAttrib)_attribs;
    }
    set {
      _attribs = (byte)value;
    }
  }

  public FieldSymbol(Origin origin, string name, ProxyType type, FieldGetter getter = null, FieldSetter setter = null, FieldRef getref = null) 
    : this(origin, name, 0, type, getter, setter, getref)
  {
  }

  public FieldSymbol(Origin origin, string name, FieldAttrib attribs, ProxyType type, FieldGetter getter = null, FieldSetter setter = null, FieldRef getref = null) 
    : base(origin, name, type)
  {
    this.attribs = attribs;

    this.getter = getter;
    this.setter = setter;
    this.getref = getref;
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    base.Sync(ctx);

    marshall.Marshall.Sync(ctx, ref _attribs);
  }
}

public class FieldSymbolScript : FieldSymbol
{
  new public const uint CLASS_ID = 9;

  public FieldSymbolScript(Origin origin, string name, ProxyType type) 
    : base(origin, name, type, null, null, null)
  {
    this.getter = Getter;
    this.setter = Setter;
    this.getref = Getref;
  }
  //marshall factory version
  public FieldSymbolScript()
    : this(null, "", new ProxyType())
  {}

  void Getter(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
  {
    var m = (IList<Val>)ctx._obj;
    v.ValueCopyFrom(m[scope_idx]);
  }

  void Setter(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
  {
    var m = (IList<Val>)ctx._obj;
    var curr = m[scope_idx];
    for(int i=0;i<curr._refs;++i)
    {
      v._refc?.Retain();
      curr._refc?.Release();
    }
    curr.ValueCopyFrom(v);
  }

  void Getref(VM.Frame frm, Val ctx, out Val v, FieldSymbol fld)
  {
    var m = (IList<Val>)ctx._obj;
    v = m[scope_idx];
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }
}

}
