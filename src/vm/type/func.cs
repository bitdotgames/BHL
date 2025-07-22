using System;
using System.Collections.Generic;

namespace bhl {
  
public class FuncSignature : IEphemeralType, IEquatable<FuncSignature>
{
  public const uint CLASS_ID = 14; 

  //full type name
  string name;

  public ProxyType ret_type;
  //TODO: include arg names as well since we support named args?
  public List<ProxyType> arg_types = new List<ProxyType>();

  byte _attribs = 0;
  public FuncSignatureAttrib attribs {
    get {
      return (FuncSignatureAttrib)_attribs;
    }
    set {
      _attribs = (byte)value;
    }
  }

  public string GetName() { return name; }

  public FuncSignature(ProxyType ret_type, params ProxyType[] arg_types)
    : this(0, ret_type, arg_types)
  {}

  public FuncSignature(FuncSignatureAttrib attribs, ProxyType ret_type, params ProxyType[] arg_types)
  {
    this.attribs = attribs;
    this.ret_type = ret_type;
    foreach(var arg_type in arg_types)
      this.arg_types.Add(arg_type);
    Update();
  }

  public FuncSignature(FuncSignatureAttrib attribs, ProxyType ret_type, List<ProxyType> arg_types)
  {
    this.attribs = attribs;
    this.ret_type = ret_type;
    this.arg_types = arg_types;
    Update();
  }

  //symbol factory version
  public FuncSignature()
  {}

  public void AddArg(ProxyType arg_type)
  {
    arg_types.Add(arg_type);
    Update();
  }

  [ThreadStatic] static StringBuilder _signature_buf;

  void Update()
  {
    if(_signature_buf == null)
      _signature_buf = new StringBuilder();

    _signature_buf.Append("func ");
    _signature_buf.Append(ret_type.ToString());
    _signature_buf.Append('(');
    if((attribs & FuncSignatureAttrib.Coro) != 0)
      _signature_buf.Insert(0, "coro ");
    for(int i=0;i<arg_types.Count;++i)
    {
      if(i > 0)
        _signature_buf.Append(',');
      if((attribs & FuncSignatureAttrib.VariadicArgs) != 0 && i == arg_types.Count-1)
        _signature_buf.Append("...");
      _signature_buf.Append(arg_types[i].ToString());
    }
    _signature_buf.Append(')');

    name = _signature_buf.ToString();
    _signature_buf.Clear();
  }

  public uint ClassId()
  {
    return CLASS_ID;
  }

  public void IndexTypeRefs(TypeRefIndex refs)
  {
    refs.Index(ret_type);
    refs.Index(arg_types);
  }

  public void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref _attribs);
    marshall.Marshall.SyncTypeRef(ctx, ref ret_type);
    marshall.Marshall.SyncTypeRefs(ctx, arg_types);
    if(ctx.is_read)
      Update();
  }

  public override bool Equals(object o)
  {
    if(!(o is FuncSignature))
      return false;
    return this.Equals((FuncSignature)o);
  }

  public bool Equals(FuncSignature o)
  {
    if(ReferenceEquals(o, null))
      return false;
    if(ReferenceEquals(this, o))
      return true;
    //TODO: 'non-coro' function is a subset of a 'coro' one
    if(attribs.HasFlag(FuncSignatureAttrib.Coro) && !o.attribs.HasFlag(FuncSignatureAttrib.Coro))
      return false;
    if(!ret_type.Equals(o.ret_type))
      return false;
    if(arg_types.Count != o.arg_types.Count)
      return false;
    for(int i=0;i<arg_types.Count;++i)
      if(!arg_types[i].Equals(o.arg_types[i]))
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
