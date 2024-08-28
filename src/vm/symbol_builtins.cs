using System.Collections.Generic;

namespace bhl {
  
public class IntSymbol : Symbol, IType
{
  public const uint CLASS_ID = 1;

  public IntSymbol()
    : base(new Origin(), "int")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }
  
  public override void IndexTypeRefs(TypeRefIndex refs)
  {}
  //contains no data
  public override void Sync(marshall.SyncContext ctx)
  {}

  //for convenience
  public static implicit operator ProxyType(IntSymbol s)
  {
    return new ProxyType(s);
  }
}

public class BoolSymbol : Symbol, IType
{
  public const uint CLASS_ID = 2;

  public BoolSymbol()
    : base(new Origin(), "bool")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {}
  //contains no data
  public override void Sync(marshall.SyncContext ctx)
  {}

  //for convenience
  public static implicit operator ProxyType(BoolSymbol s)
  {
    return new ProxyType(s);
  }
}

public class StringSymbol : ClassSymbolNative
{
  public const uint CLASS_ID = 3;

  public StringSymbol()
    : base(new Origin(), "string", native_type: typeof(string))
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }
  
  public override void IndexTypeRefs(TypeRefIndex refs)
  {}
  //contains no data
  public override void Sync(marshall.SyncContext ctx)
  {}

  //for convenience
  public static implicit operator ProxyType(StringSymbol s)
  {
    return new ProxyType(s);
  }
}

public class FloatSymbol : Symbol, IType
{
  public const uint CLASS_ID = 4;

  public FloatSymbol()
    : base(new Origin(), "float")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {}
  //contains no data
  public override void Sync(marshall.SyncContext ctx)
  {}

  //for convenience
  public static implicit operator ProxyType(FloatSymbol s)
  {
    return new ProxyType(s);
  }
}

public class VoidSymbol : Symbol, IType
{
  public const uint CLASS_ID = 5;

  public VoidSymbol()
    : base(new Origin(), "void")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {}
  //contains no data
  public override void Sync(marshall.SyncContext ctx)
  {}

  //for convenience
  public static implicit operator ProxyType(VoidSymbol s)
  {
    return new ProxyType(s);
  }
}

public class AnySymbol : Symbol, IType
{
  public const uint CLASS_ID = 6;

  public AnySymbol()
    : base(new Origin(), "any")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {}
  //contains no data
  public override void Sync(marshall.SyncContext ctx)
  {}

  //for convenience
  public static implicit operator ProxyType(AnySymbol s)
  {
    return new ProxyType(s);
  }
}

//NOTE: for better 'null ref. error' reports we extend the ClassSymbolScript  
public class NullSymbol : ClassSymbolScript
{
  public new const uint CLASS_ID = 7;

  public NullSymbol()
    : base(new Origin(), "null")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {}
  //contains no data
  public override void Sync(marshall.SyncContext ctx)
  {}
}

public class VarSymbol : Symbol
{
  public const uint CLASS_ID = 8;

  public VarSymbol()
    : base(new Origin(), "var")
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {}
  //contains no data
  public override void Sync(marshall.SyncContext ctx)
  {}
}

}
