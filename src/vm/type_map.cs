using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

public abstract class MapTypeSymbol : ClassSymbol
{
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

    this.creator = BindCreateMap;

    DefineMembers();

    base.Setup();
    enumerator_type.Setup();
  }

  protected virtual void DefineMembers()
  {
    //NOTE: must be the first member of the class
    {
      var fn = new FuncSymbolNative(new Origin(), "Add", Types.Void, BindAdd,
        new FuncArgSymbol("key", key_type),
        new FuncArgSymbol("val", val_type)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "Remove", Types.Void, BindRemove,
        new FuncArgSymbol("key", key_type)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "Clear", Types.Void, BindClear);
      this.Define(fn);
    }

    {
      var vs = new FieldSymbol(new Origin(), "Count", Types.Int, BindGetCount, null);
      this.Define(vs);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "Contains", Types.Bool, BindContains,
        new FuncArgSymbol("key", key_type)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "TryGet", new ProxyType(new TupleType(Types.Bool, val_type)), BindTryGet,
        new FuncArgSymbol("key", key_type)
      );
      this.Define(fn);
    }

    {
      //hidden system method not available directly
      var vs = new FieldSymbol(new Origin(), "$Enumerator", new ProxyType(enumerator_type), BindGetEnumerator, null);
      this.Define(vs);
    }

    {
      var vs = new FieldSymbol(new Origin(), "Next", Types.Bool, BindEnumeratorNext, null);
      enumerator_type.Define(vs);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "Current", new ProxyType(new TupleType(key_type, val_type)), BindEnumeratorCurrent);
      enumerator_type.Define(fn);
    }
  }
  
  public override void IndexTypeRefs(TypeRefIndex refs)
  {
    refs.Index(key_type);
    refs.Index(val_type);
  }
  
  void BindCreateMap(VM.Frame frm, ref Val v, IType type)
  {
    MapCreate(frm.vm, ref v);
  }
  
  void BindGetCount(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
  {
    v.SetNum(MapCount(ctx));
  }
  
  Coroutine BindAdd(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var val = stack.Pop();
    var key = stack.Pop();
    var map = stack.Pop();
    
    MapSet(map, key, val);
    
    key.Release();
    val.Release();
    map.Release();
    return null;
  }
  
  Coroutine BindContains(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var key = stack.Pop();
    var map = stack.Pop();

    bool yes = MapContainsKey(map, key);
    
    key.Release();
    map.Release();
    stack.Push(Val.NewBool(frame.vm, yes));
    return null;
  }
  
  Coroutine BindTryGet(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var key = stack.Pop();
    var map = stack.Pop();

    bool yes = MapTryGet(map, key, out var val);
    
    key.Release();
    map.Release();
    if(yes)
      stack.PushRetain(val);
    else
      stack.Push(Val.New(frame.vm)); /*just dummy value*/
    stack.Push(Val.NewBool(frame.vm, yes));
    return null;
  }
  
  Coroutine BindRemove(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var key = stack.Pop();
    var map = stack.Pop();
    
    MapRemove(map, key);
    
    key.Release();
    map.Release();
    return null;
  }
  
  Coroutine BindClear(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var map = stack.Pop();

    MapClear(map);
    
    map.Release();
    return null;
  }
  
  void BindGetEnumerator(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
  {
    v.SetObj(MapGetEnumerator(ctx), enumerator_type);
  }
  
  void BindEnumeratorNext(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
  {
    v.SetBool(MapEnumeratorNext(ctx));
  }

  Coroutine BindEnumeratorCurrent(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var en = stack.Pop();
    
    MapEnumeratorCurrent(en, out var key, out var val);
    
    stack.PushRetain(val);
    stack.PushRetain(key);
    en.Release();
    return null;
  }

  public abstract void MapCreate(VM vm, ref Val map);
  public abstract int MapCount(Val map);
  public abstract bool MapTryGet(Val map, Val key, out Val val);
  public abstract void MapSet(Val map, Val key, Val val);
  public abstract void MapRemove(Val map, Val key);
  public abstract bool MapContainsKey(Val map, Val key);
  public abstract void MapClear(Val map);
  public abstract IEnumerator MapGetEnumerator(Val map);
  public abstract bool MapEnumeratorNext(Val en);
  public abstract void MapEnumeratorCurrent(Val en, out Val key, out Val val);
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
  
  static IDictionary<Val,Val> AsDictionary(Val map)
  {
    var dict = map._obj as IDictionary<Val,Val>;
    if(dict == null)
      throw new Exception("Not a IDictionary<Val,Val>: " + (map._obj != null ? map._obj.GetType().Name : ""+map));
    return dict;
  }
  
  public override void MapCreate(VM vm, ref Val map)
  {
    map.SetObj(ValMap.New(vm), this);
  }
  
  public override int MapCount(Val map)
  {
    return AsDictionary(map).Count;
  }

  public override IEnumerator MapGetEnumerator(Val map)
  {
    return ((IEnumerable)AsDictionary(map)).GetEnumerator();
  }
  
  public override bool MapEnumeratorNext(Val en)
  {
    return ((IEnumerator)en._obj).MoveNext();
  }
  
  public override void MapEnumeratorCurrent(Val en, out Val key, out Val val)
  {
    var _en = (ValMap.Enumerator)en._obj;
    key = (Val)_en.Key; 
    val = (Val)_en.Value; 
  }
  
  public override bool MapTryGet(Val map, Val key, out Val val)
  {
    var dict = AsDictionary(map);
    return dict.TryGetValue(key, out val);
  }
  
  public override void MapSet(Val map, Val key, Val val)
  {
    var dict = AsDictionary(map);
    dict[key] = val;
  }
  
  public override void MapRemove(Val map, Val key)
  {
    var dict = AsDictionary(map);
    dict.Remove(key);
  }
  
  public override bool MapContainsKey(Val map, Val key)
  {
    var dict = AsDictionary(map);
    return dict.ContainsKey(key);
  }
  
  public override void MapClear(Val map)
  {
    var dict = AsDictionary(map);
    dict.Clear();
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
}

} //namespace bhl
