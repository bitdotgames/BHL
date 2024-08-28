using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

public abstract class ArrayTypeSymbol : ClassSymbol
{
  public ProxyType item_type;

  //marshall factory version
  public ArrayTypeSymbol()     
    : base(null, null)
  {}

  public ArrayTypeSymbol(Origin origin, string name, ProxyType item_type)     
    : base(origin, name)
  {
    this.item_type = item_type;
  }

  protected virtual void DefineMembers()
  {
    //NOTE: must be first member of the class
    {
      var fn = new FuncSymbolNative(new Origin(), "Add", Types.Void, BindAdd,
        new FuncArgSymbol("o", item_type)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "RemoveAt", Types.Void, BindRemoveAt,
        new FuncArgSymbol("idx", Types.Int)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "Clear", Types.Void, BindClear);
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "Insert", Types.Void, BindInsert,
        new FuncArgSymbol("idx", Types.Int),
        new FuncArgSymbol("o", item_type)
      );
      this.Define(fn);
    }

    {
      var vs = new FieldSymbol(new Origin(), "Count", Types.Int, BindCount, null);
      this.Define(vs);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "IndexOf", Types.Int, BindIndexOf,
        new FuncArgSymbol("o", item_type)
      );
      this.Define(fn);
    }
  }

  public override void Setup()
  {
    if(item_type.IsEmpty())
      throw new Exception("Invalid item type");

    this.creator = BindCreateArr;

    DefineMembers();

    base.Setup();
  }

  public ArrayTypeSymbol(Origin origin, string name)     
    : base(origin, name)
  {}

  public ArrayTypeSymbol(Origin origin, ProxyType item_type) 
    : this(origin, "[]" + item_type, item_type)
  {}

  void BindCreateArr(VM.Frame frame, ref Val v, IType type)
  {
    ArrCreate(frame.vm, ref v);
  }
  
  void BindCount(VM.Frame frame, Val ctx, ref Val v, FieldSymbol fld)
  {
    v.SetNum(ArrCount(ctx));
  }

  Coroutine BindAdd(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var val = stack.Pop();
    var arr = stack.Pop();

    ArrAdd(arr, val);
    
    val.Release();
    arr.Release();
    return null;
  }

  Coroutine BindRemoveAt(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)stack.PopRelease().num;
    var arr = stack.Pop();
    
    ArrRemoveAt(arr, idx);
    
    arr.Release();
    return null;
  }

  Coroutine BindIndexOf(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var val = stack.Pop();
    var arr = stack.Pop();

    int idx = ArrIndexOf(arr, val);
    
    val.Release();
    arr.Release();
    stack.Push(Val.NewInt(frame.vm, idx));
    return null;
  }

  Coroutine BindClear(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var arr = stack.Pop();
    
    ArrClear(arr);
    
    arr.Release();
    return null;
  }

  Coroutine BindInsert(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var val = stack.Pop();
    var idx = stack.Pop();
    var arr = stack.Pop();

    ArrInsert(arr, (int)idx._num, val);
    
    arr.Release();
    idx.Release();
    val.Release();
    return null;
  }
  
  public override void IndexTypeRefs(TypeRefIndex refs)
  {
    refs.Index(item_type);
  }
  
  public abstract void ArrCreate(VM vm, ref Val arr);
  public abstract int ArrCount(Val arr);
  public abstract void ArrAdd(Val arr, Val val);
  public abstract Val ArrGetAt(Val arr, int idx);
  public abstract void ArrSetAt(Val arr, int idx, Val val);
  public abstract void ArrRemoveAt(Val arr, int idx);
  public abstract int ArrIndexOf(Val arr, Val val);
  public abstract void ArrClear(Val arr);
  public abstract void ArrInsert(Val arr, int idx, Val val);
}

public class GenericArrayTypeSymbol : 
  ArrayTypeSymbol, IEquatable<GenericArrayTypeSymbol>, IEphemeralType
{
  public const uint CLASS_ID = 10;
  
  public GenericArrayTypeSymbol(Origin origin, ProxyType item_type)
    : base(origin, item_type)
  {}
    
  //marshall factory version
  public GenericArrayTypeSymbol()
    : base()
  {}
    
  static IList<Val> AsList(Val arr)
  {
    var lst = arr._obj as IList<Val>;
    if(lst == null)
      throw new Exception("Not a IList<Val>: " + (arr._obj != null ? arr.obj.GetType().Name : ""+arr));
    return lst;
  }

  public override void ArrCreate(VM vm, ref Val arr)
  {
    arr.SetObj(ValList.New(vm), this);
  }

  public override int ArrCount(Val arr)
  {
    return AsList(arr).Count;
  }
  
  public override void ArrAdd(Val arr, Val val)
  {
    var lst = AsList(arr);
    lst.Add(val);
  }

  public override int ArrIndexOf(Val arr, Val val)
  {
    var lst = AsList(arr);

    int idx = -1;
    for(int i=0;i<lst.Count;++i)
    {
      if(lst[i].IsValueEqual(val))
      {
        idx = i;
        break;
      }
    }

    return idx;
  }

  public override Val ArrGetAt(Val arr, int idx)
  {
    var lst = AsList(arr);
    var res = lst[idx];
    res.Retain();
    return res;
  }

  public override void ArrSetAt(Val arr, int idx, Val val)
  {
    var lst = AsList(arr);
    lst[idx] = val;
  }

  public override void ArrRemoveAt(Val arr, int idx)
  {
    var lst = AsList(arr);
    lst.RemoveAt(idx); 
  }

  public override void ArrClear(Val arr)
  {
    var lst = AsList(arr);
    lst.Clear();
  }
  
  public override void ArrInsert(Val arr, int idx, Val val)
  {
    var lst = AsList(arr);
    lst.Insert(idx, val);
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.SyncTypeRef(ctx, ref item_type);

    if(ctx.is_read)
    {
      name = "[]" + item_type;

      //NOTE: once we have all members unmarshalled we should actually Setup() the instance 
      Setup();
    }
  }

  public override bool Equals(object o)
  {
    if(!(o is GenericArrayTypeSymbol))
      return false;
    return this.Equals((GenericArrayTypeSymbol)o);
  }

  public bool Equals(GenericArrayTypeSymbol o)
  {
    if(ReferenceEquals(o, null))
      return false;
    if(ReferenceEquals(this, o))
      return true;
    return item_type.Equals(o.item_type);
  }

  public override int GetHashCode()
  {
    return name.GetHashCode();
  }
}

public abstract class GenericNativeArrayTypeSymbol : 
  ArrayTypeSymbol, IEquatable<GenericNativeArrayTypeSymbol>
{
  public const uint CLASS_ID = 24;
  
  public GenericNativeArrayTypeSymbol(
    Origin origin, string name, ProxyType item_type)
    : base(origin, name, item_type)
  {}
  
  //NOTE: making it sort of 'inherit' from the base GenericArrayTypeSymbol
  protected override HashSet<IInstantiable> CollectAllRelatedTypesSet()
  {
    var related_types = new HashSet<IInstantiable>();
    related_types.Add(this);
    var arr_type = new GenericArrayTypeSymbol(new Origin(), item_type);
    arr_type.Setup();
    related_types.Add(arr_type);
    return related_types;
  }
  
  static IList AsIList(Val arr)
  {
    var lst = arr._obj as IList;
    if(lst == null)
      throw new Exception("Not a IList: " + (arr._obj != null ? arr._obj.GetType().Name : ""+arr));
    return lst;
  }

  public abstract IList CreateList();

  public override void ArrCreate(VM vm, ref Val arr)
  {
    arr.SetObj(CreateList(), this);
  }

  public override int ArrCount(Val arr)
  {
    return AsIList(arr).Count;
  }

  public override void ArrRemoveAt(Val arr, int idx)
  {
    AsIList(arr).RemoveAt(idx);
  }

  public override void ArrClear(Val arr)
  {
    AsIList(arr).Clear();
  }
  
  public override uint ClassId()
  {
    throw new NotImplementedException();
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {}
  public override void Sync(marshall.SyncContext ctx)
  {}
  
  public override bool Equals(object o)
  {
    if(!(o is GenericNativeArrayTypeSymbol))
      return false;
    return this.Equals((GenericNativeArrayTypeSymbol)o);
  }

  public bool Equals(GenericNativeArrayTypeSymbol o)
  {
    if(ReferenceEquals(o, null))
      return false;
    if(ReferenceEquals(this, o))
      return true;
    return item_type.Equals(o.item_type);
  }

  public override int GetHashCode()
  {
    return name.GetHashCode();
  }
}

public class NativeListTypeSymbol<T> : GenericNativeArrayTypeSymbol
{
  Func<Val, T> val2native;
  Func<VM, ProxyType, T, Val> native2val;

  public NativeListTypeSymbol(
    Origin origin, string name, 
    Func<Val, T> val2native,
    Func<VM, ProxyType, T, Val> native2val,
    ProxyType item_type
    )
    : base(origin, name, item_type)
  {
    this.val2native = val2native;
    this.native2val = native2val;
  }
  
  protected override void DefineMembers()
  {
    base.DefineMembers();
    
    {
      var fn = new FuncSymbolNative(new Origin(), "At", item_type,
      delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
      {
        int idx = (int)stack.PopRelease().num;
        var arr = stack.Pop();

        var res = ArrGetAt(arr, idx);
        
        stack.Push(res);
        
        arr.Release();
        return null;
      },
      new FuncArgSymbol("idx", Types.Int)
      );
      Define(fn);
    }
  }
  
  public override IList CreateList()
  {
    return new List<T>();
  }
  
  public override void ArrAdd(Val arr, Val val)
  {
    var lst = (IList<T>)arr._obj;
    lst.Add(val2native(val));
  }
  
  public override void ArrInsert(Val arr, int idx, Val val)
  {
    var lst = (IList<T>)arr._obj;
    lst.Insert(idx, val2native(val));
  }

  public override Val ArrGetAt(Val arr, int idx)
  {
    var lst = (IList<T>)arr._obj;
    return native2val(arr.vm, item_type, lst[idx]);
  }

  public override void ArrSetAt(Val arr, int idx, Val val)
  {
    var lst = (IList<T>)arr._obj;
    lst[idx] = val2native(val);
  }

  public override int ArrIndexOf(Val arr, Val val)
  {
    var lst = (IList<T>)arr._obj;
    return lst.IndexOf(val2native(val));
  }
  
  public override uint ClassId()
  {
    throw new NotImplementedException();
  }
}

}
