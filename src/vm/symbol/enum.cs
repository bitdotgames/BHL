using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl
{

public abstract class EnumSymbol : Symbol, IScope, IType, IEnumerable<Symbol>
{
  internal SymbolsStorage members;

  public EnumSymbol(Origin origin, string name)
    : base(origin, name)
  {
    this.members = new SymbolsStorage(this);
  }

  public IScope GetFallbackScope()
  {
    return scope;
  }

  public Symbol Resolve(string name)
  {
    return members.Find(name);
  }

  public void Define(Symbol sym)
  {
    if(!(sym is EnumItemSymbol))
      throw new Exception("Invalid item");
    members.Add(sym);
  }

  public IEnumerator<Symbol> GetEnumerator()
  {
    return members.GetEnumerator();
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public EnumItemSymbol FindValue(string name)
  {
    return Resolve(name) as EnumItemSymbol;
  }

  public override uint ClassId()
  {
    throw new NotImplementedException();
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    throw new NotImplementedException();
  }
}

public class EnumSymbolScript : EnumSymbol
{
  public const uint CLASS_ID = 12;

  public EnumSymbolScript(Origin origin, string name)
    : base(origin, name)
  {
  }

  //marshall factory version
  public EnumSymbolScript()
    : base(null, null)
  {
  }

  //0 - OK, 1 - duplicate key, 2 - duplicate value
  public int TryAddItem(Origin origin, string name, int val)
  {
    for(int i = 0; i < members.Count; ++i)
    {
      var m = (EnumItemSymbol)members[i];
      if(m.val == val)
        return 2;
      else if(m.name == name)
        return 1;
    }

    var item = new EnumItemSymbol(origin, name, val);
    //TODO: should be set by SymbolsDictionary
    item.scope = this;
    members.Add(item);
    return 0;
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.Sync(ctx, ref members);
    if(ctx.is_read)
    {
      for(int i = 0; i < members.Count; ++i)
      {
        var item = (EnumItemSymbol)members[i];
        item.scope = this;
      }
    }
  }
}

public class EnumSymbolNative : EnumSymbol, INativeType
{
  System.Type native_type;

  public EnumSymbolNative(Origin origin, string name, System.Type native_type)
    : base(origin, name)
  {
    this.native_type = native_type;
  }

  public System.Type GetNativeType()
  {
    return native_type;
  }

  public object GetNativeObject(Val v)
  {
    //TODO: is it valid?
    return v?._num;
  }
}

public class EnumItemSymbol : Symbol, IType
{
  public const uint CLASS_ID = 15;

  public EnumSymbol owner
  {
    get { return scope as EnumSymbol; }
  }

  public int val;

  public EnumItemSymbol(Origin origin, string name, int val = 0)
    : base(origin, name)
  {
    this.val = val;
  }

  //marshall factory version
  public EnumItemSymbol()
    : base(null, null)
  {
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.Sync(ctx, ref val);
  }
}

}