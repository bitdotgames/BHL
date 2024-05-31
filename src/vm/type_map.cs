using System;

namespace bhl {

public abstract class MapTypeSymbol : ClassSymbol
{
  internal FuncSymbolNative FuncMapIdx;
  internal FuncSymbolNative FuncMapIdxW;

  public ProxyType key_type;
  public ProxyType val_type;

  public ClassSymbol enumerator_type = new ClassSymbolNative(new Origin(), "Enumerator");

  //marshall factory version
  public MapTypeSymbol()     
    : base(null, null)
  {}

  public MapTypeSymbol(Origin origin, ProxyType key_type, ProxyType val_type)     
    : base(origin, "[" + key_type + "]" + val_type)
  {
    this.key_type = key_type;
    this.val_type = val_type;
  }

  public override void Setup()
  {
    if(key_type.IsEmpty())
      throw new Exception("Invalid key type");
    if(val_type.IsEmpty())
      throw new Exception("Invalid value type");

    this.creator = CreateMap;

    //NOTE: must be first member of the class
    {
      var fn = new FuncSymbolNative(new Origin(), "Add", Types.Void, Add,
        new FuncArgSymbol("key", key_type),
        new FuncArgSymbol("val", val_type)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "Remove", Types.Void, Remove,
        new FuncArgSymbol("key", key_type)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "Clear", Types.Void, Clear);
      this.Define(fn);
    }

    {
      var vs = new FieldSymbol(new Origin(), "Count", Types.Int, GetCount, null);
      this.Define(vs);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "Contains", Types.Bool, Contains,
        new FuncArgSymbol("key", key_type)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "TryGet", new ProxyType(new TupleType(Types.Bool, val_type)), TryGet,
        new FuncArgSymbol("key", key_type)
      );
      this.Define(fn);
    }

    {
      //hidden system method not available directly
      var vs = new FieldSymbol(new Origin(), "$Enumerator", new ProxyType(enumerator_type), GetEnumerator, null);
      this.Define(vs);
    }

    {
      //hidden system method not available directly
      FuncMapIdx = new FuncSymbolNative(new Origin(), "$MapIdx", val_type, MapIdx);
    }

    {
      //hidden system method not available directly
      FuncMapIdxW = new FuncSymbolNative(new Origin(), "$MapIdxW", Types.Void, MapIdxW);
    }

    {
      var vs = new FieldSymbol(new Origin(), "Next", Types.Bool, EnumeratorNext, null);
      enumerator_type.Define(vs);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "Current", new ProxyType(new TupleType(key_type, val_type)), EnumeratorCurrent);
      enumerator_type.Define(fn);
    }

    base.Setup();
    enumerator_type.Setup();
  }
  
  public override void IndexTypeRefs(TypeRefIndex refs)
  {
    key_type.IndexTypeRefs(refs);
    val_type.IndexTypeRefs(refs);
  }
  
  public abstract void CreateMap(VM.Frame frame, ref Val v, IType type);
  public abstract void GetCount(VM.Frame frame, Val ctx, ref Val v, FieldSymbol fld);
  public abstract void GetEnumerator(VM.Frame frame, Val ctx, ref Val v, FieldSymbol fld);
  public abstract Coroutine Add(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract Coroutine MapIdx(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract Coroutine MapIdxW(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract Coroutine Remove(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract Coroutine Contains(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract Coroutine TryGet(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract Coroutine Clear(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);

  public abstract void EnumeratorNext(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld);
  public abstract Coroutine EnumeratorCurrent(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
}

public class GenericMapTypeSymbol : MapTypeSymbol, IEquatable<GenericMapTypeSymbol>, IEphemeralType
{
  public const uint CLASS_ID = 21; 
  
  public GenericMapTypeSymbol(Origin origin, ProxyType key_type, ProxyType val_type)     
    : base(origin, key_type, val_type)
  {}
    
  //marshall factory version
  public GenericMapTypeSymbol()
    : base()
  {}
  
  static ValMap AsMap(Val arr)
  {
    var map = arr.obj as ValMap;
    if(map == null)
      throw new Exception("Not a ValMap: " + (arr.obj != null ? arr.obj.GetType().Name : ""+arr));
    return map;
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }
  
  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.SyncRef(ctx, ref key_type);
    marshall.Marshall.SyncRef(ctx, ref val_type);

    if(ctx.is_read)
    {
      name = "[" + key_type + "]" + val_type;

      //NOTE: once we have all members unmarshalled we should actually Setup() the instance 
      Setup();
    }
  }

  public override bool Equals(object o)
  {
    if(!(o is GenericMapTypeSymbol))
      return false;
    return this.Equals((GenericMapTypeSymbol)o);
  }

  public bool Equals(GenericMapTypeSymbol o)
  {
    if(ReferenceEquals(o, null))
      return false;
    if(ReferenceEquals(this, o))
      return true;
    return key_type.Equals(o.key_type) && 
           val_type.Equals(o.val_type);
  }

  public override int GetHashCode()
  {
    return name.GetHashCode();
  }

  public override void CreateMap(VM.Frame frm, ref Val v, IType type)
  {
    v.SetObj(ValMap.New(frm.vm), type);
  }

  public override void GetCount(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
  {
    var m = AsMap(ctx);
    v.SetNum(m.Count);
  }

  public override void GetEnumerator(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
  {
    var m = AsMap(ctx);
    v.SetObj(new ValMap.Enumerator(m), enumerator_type);
  }

  public override Coroutine Add(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var val = stack.Pop();
    var key = stack.Pop();
    var v = stack.Pop();
    var map = AsMap(v);
    //TODO: maybe we should rather user C# Dictionary.Add(k, v)?
    map[key] = val;
    key.Release();
    val.Release();
    v.Release();
    return null;
  }

  public override Coroutine Remove(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var key = stack.Pop();
    var v = stack.Pop();
    var map = AsMap(v);
    map.Remove(key);
    key.Release();
    v.Release();
    return null;
  }

  public override Coroutine Contains(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var key = stack.Pop();
    var v = stack.Pop();
    var map = AsMap(v);
    bool yes = map.ContainsKey(key);
    key.Release();
    v.Release();
    stack.Push(Val.NewBool(frame.vm, yes));
    return null;
  }

  public override Coroutine TryGet(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var key = stack.Pop();
    var v = stack.Pop();
    var map = AsMap(v);
    Val val;
    bool yes = map.TryGetValue(key, out val);
    key.Release();
    v.Release();
    if(yes)
      stack.PushRetain(val);
    else
      stack.Push(Val.New(frame.vm)); /*just dummy value*/
    stack.Push(Val.NewBool(frame.vm, yes));
    return null;
  }

  public override Coroutine Clear(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var v = stack.Pop();
    var map = AsMap(v);
    map.Clear();
    v.Release();
    return null;
  }

  //NOTE: follows special Opcodes.MapIdx conventions
  public override Coroutine MapIdx(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var key = stack.Pop();
    var v = stack.Pop();
    var map = AsMap(v);
    var res = map[key]; 
    stack.PushRetain(res);
    key.Release();
    v.Release();
    return null;
  }

  //NOTE: follows special Opcodes.MapIdxW conventions
  public override Coroutine MapIdxW(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var key = stack.Pop();
    var v = stack.Pop();
    var val = stack.Pop();
    var map = AsMap(v);
    map[key] = val;
    key.Release();
    val.Release();
    v.Release();
    return null;
  }

  public override void EnumeratorNext(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
  {
    var en = (ValMap.Enumerator)ctx._obj;
    bool ok = en.MoveNext();
    v.SetBool(ok);
  }

  public override Coroutine EnumeratorCurrent(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var v = stack.Pop();
    var en = (ValMap.Enumerator)v._obj;
    var key = (Val)en.Key; 
    var val = (Val)en.Value; 
    stack.PushRetain(val);
    stack.PushRetain(key);
    v.Release();
    return null;
  }
}

} //namespace bhl
